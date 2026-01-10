using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Audit.Core.Extensions;

namespace Audit.Core;

/// <summary>
/// Makes a code block auditable.
/// </summary>
public sealed partial class AuditScope : IAuditScope
{
    private readonly Activity _activity;


    private readonly AuditScopeOptions _options;
    private bool _disposed;
    private bool _ended;
    private Func<object> _targetGetter;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AuditScope(AuditScopeOptions options)
    {
        _options = options;
        EventCreationPolicy = options.CreationPolicy ?? Configuration.CreationPolicy;
        DataProvider = options.DataProvider ?? Configuration.DataProvider;
        _targetGetter = options.TargetGetter;

        Event = options.AuditEvent ?? new AuditEvent();

        Event.SetScope(this);

        Event.StartDate = Configuration.SystemClock.UtcNow;

        Event.Environment = GetEnvironmentInfo(options);

        if (options.StartActivityTrace)
        {
            var activitySource = new ActivitySource(nameof(AuditScope), typeof(AuditScope).Assembly.GetName().Version!.ToString());
            _activity = activitySource.StartActivity(Event.GetType().Name);
        }

        if (options.IncludeActivityTrace)
        {
            Event.Activity = GetActivityTrace();
        }

        if (options.EventType != null)
        {
            Event.EventType = options.EventType;
        }

        if (Event.CustomFields == null)
        {
            Event.CustomFields = new Dictionary<string, object>();
        }

        ProcessExtraFields(options.ExtraFields);

        if (options.TargetGetter != null)
        {
            var targetValue = options.TargetGetter.Invoke();
            Event.Target = new AuditTarget
            {
                Old = DataProvider.CloneValue(targetValue, Event),
                Type = targetValue?.GetType().GetFullTypeName() ?? "Object"
            };
        }
    }

    /// <summary>
    /// The current save mode. Useful on custom actions to determine the saving trigger.
    /// </summary>
    public SaveMode SaveMode { get; private set; }

    /// <summary>
    /// Indicates the change type
    /// </summary>
    public string EventType
    {
        get => Event.EventType;
        set => Event.EventType = value;
    }

    /// <summary>
    /// Gets the event related to this scope.
    /// </summary>
    public AuditEvent Event { get; }

    /// <summary>
    /// Gets the data provider for this AuditScope instance.
    /// </summary>
    public AuditDataProvider DataProvider { get; }

    /// <summary>
    /// Gets the current event ID, or NULL if not yet created.
    /// </summary>
    public object EventId { get; private set; }

    /// <summary>
    /// Gets the creation policy for this scope.
    /// </summary>
    public EventCreationPolicy EventCreationPolicy { get; }

    /// <summary>
    /// Replaces the target object getter whose old/new value will be stored on the AuditEvent.Target property
    /// </summary>
    /// <param name="targetGetter">A function that returns the target</param>
    public void SetTargetGetter(Func<object> targetGetter)
    {
        _targetGetter = targetGetter;

        if (_targetGetter != null)
        {
            var targetValue = targetGetter.Invoke();
            Event.Target = new AuditTarget
            {
                Old = DataProvider.CloneValue(targetValue, Event),
                Type = targetValue?.GetType().GetFullTypeName() ?? "Object"
            };
        }
        else
        {
            Event.Target = null;
        }
    }

    /// <summary>
    /// Add a textual comment to the event
    /// </summary>
    public void Comment(string text)
    {
        Comment(text, Array.Empty<object>());
    }

    /// <summary>
    /// Add a textual comment to the event
    /// </summary>
    public void Comment(string format, params object[] args)
    {
        if (Event.Comments == null)
        {
            Event.Comments = new List<string>();
        }

        Event.Comments.Add(string.Format(format, args));
    }

    /// <summary>
    /// Adds a custom field to the event
    /// </summary>
    /// <typeparam name="TC">The type of the value.</typeparam>
    /// <param name="fieldName">Name of the field.</param>
    /// <param name="value">The value object.</param>
    /// <param name="serialize">if set to <c>true</c> the value will be serialized immediately.</param>
    public void SetCustomField<TC>(string fieldName, TC value, bool serialize = false)
    {
        Event.CustomFields[fieldName] = serialize ? DataProvider.CloneValue(value, Event) : value;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        End();
        _activity?.Dispose();
        _activity?.Source?.Dispose();
        Configuration.InvokeScopeCustomActions(ActionType.OnScopeDisposed, this);
    }

    /// <summary>
    /// Async version of the dispose method
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await EndAsync();
        _activity?.Dispose();
        _activity?.Source?.Dispose();
        await Configuration.InvokeScopeCustomActionsAsync(ActionType.OnScopeDisposed, this, CancellationToken.None);
    }

    /// <summary>
    /// Discards this audit scope, so the event will not be written.
    /// </summary>
    public void Discard()
    {
        // Mark as saved to ignore the saving
        _ended = true;
    }

    /// <summary>
    /// Manually Saves (insert/replace) the Event.
    /// Use this method to save (insert/replace) the event when CreationPolicy is set to Manual.
    /// </summary>
    public void Save()
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();
        SaveEvent();
    }

    /// <summary>
    /// Manually Saves (insert/replace) the Event asynchronously.
    /// Use this method to save (insert/replace) the event when CreationPolicy is set to Manual.
    /// </summary>
    /// <param name="cancellationToken">The Cancellation Token.</param>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();
        await SaveEventAsync(false, cancellationToken);
    }

    /// <summary>
    /// Gets the event related to this scope of a known AuditEvent derived type. Returns null if the event is not of the
    /// specified type.
    /// </summary>
    /// <typeparam name="T">The AuditEvent derived type</typeparam>
    public T EventAs<T>() where T : AuditEvent
    {
        return Event as T;
    }

    /// <summary>
    /// Saves the event.
    /// </summary>
    private void End()
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();

        // process event creation/replacement
        if (EventCreationPolicy == EventCreationPolicy.InsertOnEnd || EventCreationPolicy == EventCreationPolicy.InsertOnStartInsertOnEnd)
        {
            SaveEvent(true);
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd)
        {
            SaveEvent();
        }

        _ended = true;
    }

    /// <summary>
    /// Saves the event.
    /// </summary>
    private async Task EndAsync()
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();

        // process event creation/replacement
        if (EventCreationPolicy == EventCreationPolicy.InsertOnEnd || EventCreationPolicy == EventCreationPolicy.InsertOnStartInsertOnEnd)
        {
            await SaveEventAsync(true);
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd)
        {
            await SaveEventAsync();
        }

        _ended = true;
    }


    private AuditActivityTrace GetActivityTrace()
    {
        var activity = Activity.Current;

        if (activity == null)
        {
            return null;
        }

        var spanId = activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.Id,
            ActivityIdFormat.W3C => activity.SpanId.ToHexString(),
            _ => null
        };

        var traceId = activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.RootId,
            ActivityIdFormat.W3C => activity.TraceId.ToHexString(),
            _ => null
        };

        var parentId = activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.ParentId,
            ActivityIdFormat.W3C => activity.ParentSpanId.ToHexString(),
            _ => null
        };

        var result = new AuditActivityTrace
        {
            StartTimeUtc = activity.StartTimeUtc,
            SpanId = spanId,
            TraceId = traceId,
            ParentId = parentId,
            Operation = activity.OperationName
        };

        if (activity.Tags.Any())
        {
            result.Tags = new List<AuditActivityTag>();

            foreach (var tag in activity.Tags)
            {
                result.Tags.Add(new AuditActivityTag(tag.Key, tag.Value));
            }
        }

        if (activity.Events.Any())
        {
            result.Events = new List<AuditActivityEvent>();

            foreach (var ev in activity.Events)
            {
                result.Events.Add(new AuditActivityEvent(ev.Timestamp, ev.Name, ev.Tags.ToDictionary(t => t.Key, t => t.Value)));
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private AuditEventEnvironment GetEnvironmentInfo(AuditScopeOptions options)
    {
        var environment = new AuditEventEnvironment
        {
            Culture = CultureInfo.CurrentCulture.ToString()
        };
        var callingMethod = options.CallingMethod;
        environment.UserName = Environment.UserName;
        environment.MachineName = Environment.MachineName;
        environment.DomainName = Environment.UserDomainName;

        if (callingMethod == null)
        {
            callingMethod = new StackFrame(3 + options.SkipExtraFrames).GetMethod();
        }

        if (options.IncludeStackTrace)
        {
            environment.StackTrace = new StackTrace(options.SkipExtraFrames, true).ToString();
        }

        if (callingMethod != null)
        {
            environment.CallingMethodName = (callingMethod.DeclaringType != null ? callingMethod.DeclaringType.FullName + "." : "") + callingMethod.Name + "()";
            environment.AssemblyName = callingMethod.DeclaringType?.GetTypeInfo().Assembly.FullName;
        }

        return environment;
    }

    /// <summary>
    /// Starts an audit scope
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AuditScope Start()
    {
        SaveMode = SaveMode.InsertOnStart;
        // Execute custom on scope created actions
        Configuration.InvokeScopeCustomActions(ActionType.OnScopeCreated, this);

        // Process the event insertion (if applies)
        if (_options.IsCreateAndSave)
        {
            EndEvent();
            SaveEvent();
            _ended = true;
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd || EventCreationPolicy == EventCreationPolicy.InsertOnStartInsertOnEnd)
        {
            SaveEvent();
            SaveMode = EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd ? SaveMode.ReplaceOnEnd : SaveMode.InsertOnEnd;
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnEnd)
        {
            SaveMode = SaveMode.InsertOnEnd;
        }
        else if (EventCreationPolicy == EventCreationPolicy.Manual)
        {
            SaveMode = SaveMode.Manual;
        }

        return this;
    }

    /// <summary>
    /// Starts an audit scope asynchronously
    /// </summary>
    /// <param name="cancellationToken">The Cancellation Token.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal async Task<AuditScope> StartAsync(CancellationToken cancellationToken = default)
    {
        SaveMode = SaveMode.InsertOnStart;
        // Execute custom on scope created actions
        await Configuration.InvokeScopeCustomActionsAsync(ActionType.OnScopeCreated, this, cancellationToken);

        // Process the event insertion (if applies)
        if (_options.IsCreateAndSave)
        {
            EndEvent();
            await SaveEventAsync(false, cancellationToken);
            _ended = true;
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd || EventCreationPolicy == EventCreationPolicy.InsertOnStartInsertOnEnd)
        {
            await SaveEventAsync(false, cancellationToken);
            SaveMode = EventCreationPolicy == EventCreationPolicy.InsertOnStartReplaceOnEnd ? SaveMode.ReplaceOnEnd : SaveMode.InsertOnEnd;
        }
        else if (EventCreationPolicy == EventCreationPolicy.InsertOnEnd)
        {
            SaveMode = SaveMode.InsertOnEnd;
        }
        else if (EventCreationPolicy == EventCreationPolicy.Manual)
        {
            SaveMode = SaveMode.Manual;
        }

        return this;
    }

    private bool IsEndedOrDisabled()
    {
        if (!_ended && Configuration.AuditDisabled)
        {
            Discard();
        }

        return _ended;
    }

    // Update event info prior to save
    private void EndEvent()
    {
        var exception = GetCurrentException();
        Event.Environment.Exception = exception != null ? $"{exception.GetType().Name}: {exception.Message}" : null;
        Event.EndDate = Configuration.SystemClock.UtcNow;
        Event.Duration = Convert.ToInt32((Event.EndDate.Value - Event.StartDate).TotalMilliseconds);

        if (_targetGetter != null)
        {
            Event.Target.New = DataProvider.CloneValue(_targetGetter.Invoke(), Event);
        }
    }

    private Exception GetCurrentException()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        if (PlatformHelper.IsRunningOnMono())
        {
            // Mono doesn't implement Marshal.GetExceptionCode() (https://github.com/mono/mono/blob/master/mcs/class/corlib/System.Runtime.InteropServices/Marshal.cs#L521)
            return null;
        }

        if (Marshal.GetExceptionCode() != 0)
        {
            return Marshal.GetExceptionForHR(Marshal.GetExceptionCode());
        }

        return null;
#pragma warning restore CS0618 // Type or member is obsolete
    }

    private void ProcessExtraFields(object extraFields)
    {
        if (extraFields == null)
        {
            return;
        }

        var props = extraFields.GetType().GetRuntimeProperties();

        foreach (var prop in props)
        {
            SetCustomField(prop.Name, prop.GetValue(extraFields, null));
        }
    }

    private void SaveEvent(bool forceInsert = false)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        // Execute custom on event saving actions
        Configuration.InvokeScopeCustomActions(ActionType.OnEventSaving, this);

        if (IsEndedOrDisabled())
        {
            return;
        }

        if (EventId != null && !forceInsert)
        {
            DataProvider.ReplaceEvent(EventId, Event);
        }
        else
        {
            EventId = DataProvider.InsertEvent(Event);
        }

        // Execute custom after saving actions
        Configuration.InvokeScopeCustomActions(ActionType.OnEventSaved, this);
    }

    private async Task SaveEventAsync(bool forceInsert = false, CancellationToken cancellationToken = default)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        // Execute custom on event saving actions
        await Configuration.InvokeScopeCustomActionsAsync(ActionType.OnEventSaving, this, cancellationToken);

        if (IsEndedOrDisabled())
        {
            return;
        }

        if (EventId != null && !forceInsert)
        {
            await DataProvider.ReplaceEventAsync(EventId, Event, cancellationToken);
        }
        else
        {
            EventId = await DataProvider.InsertEventAsync(Event, cancellationToken);
        }

        // Execute custom after saving actions
        await Configuration.InvokeScopeCustomActionsAsync(ActionType.OnEventSaved, this, cancellationToken);
    }
}