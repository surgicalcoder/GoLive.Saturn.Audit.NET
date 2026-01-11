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

#pragma warning disable CS3002 // Activity not CLS-compliant

namespace Audit.Core;

/// <summary>
/// Makes a code block auditable.
/// </summary>
public sealed partial class AuditScope : IAuditScope
{
    private static readonly Lazy<ActivitySource> ActivitySource = new(() => new ActivitySource(typeof(AuditScope).FullName!, typeof(AuditScope).Assembly.GetName().Version!.ToString()));

    #region Constructors

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AuditScope(AuditScopeOptions options)
    {
        _options = options;
        EventCreationPolicy = options.CreationPolicy ?? Configuration.CreationPolicy;
        DataProvider = options.DataProvider ?? Configuration.DataProvider;
        _systemClock = options.SystemClock ?? Configuration.SystemClock;
        _targetGetter = options.TargetGetter;
        Items = options.Items ?? new Dictionary<string, object>();
        Event = options.AuditEvent ?? new AuditEvent();

        Event.SetScope(this);

        Event.StartDate = _systemClock.GetCurrentDateTime();

        if (options.IncludeTimestamps ?? Configuration.IncludeTimestamps)
        {
            Event.StartTimestamp = _systemClock.GetCurrentTimestamp();
        }

        Event.Environment = GetEnvironmentInfo(options);

        if (options.StartActivityTrace ?? Configuration.StartActivityTrace)
        {
            _activity = ActivitySource.Value.StartActivity(Event.GetType()!.Name);

            _activity?.SetCustomProperty(nameof(AuditEvent), Event);
        }

        if (options.IncludeActivityTrace ?? Configuration.IncludeActivityTrace)
        {
            Event.Activity = GetActivityTraceData();
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

    #endregion

    #region Public Properties

    /// <inheritdoc />
    public SaveMode SaveMode { get; private set; }

    /// <inheritdoc />
    public string EventType
    {
        get => Event.EventType;
        set => Event.EventType = value;
    }

    /// <inheritdoc />
    public AuditEvent Event { get; }

    /// <inheritdoc />
    public IAuditDataProvider DataProvider { get; }

    /// <inheritdoc />
    public object EventId { get; private set; }

    /// <inheritdoc />
    public EventCreationPolicy EventCreationPolicy { get; }

    /// <inheritdoc />
    public IDictionary<string, object> Items { get; }

    #endregion

    #region Private fields

    private readonly AuditScopeOptions _options;
    private bool _disposed;
    private bool _ended;
    private readonly ISystemClock _systemClock;
    private Func<object> _targetGetter;
    private readonly Activity _activity;

    #endregion

    #region Public Methods

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Comment(string text)
    {
        Comment(text, Array.Empty<object>());
    }

    /// <inheritdoc />
    public void Comment(string format, params object[] args)
    {
        if (Event.Comments == null)
        {
            Event.Comments = new List<string>();
        }

        Event.Comments.Add(string.Format(format, args));
    }

    /// <inheritdoc />
    public void SetCustomField<TC>(string fieldName, TC value, bool serialize = false)
    {
        Event.CustomFields[fieldName] = serialize ? DataProvider.CloneValue(value, Event) : value;
    }

    /// <inheritdoc />
    public T GetItem<T>(string key)
    {
        if (Items.TryGetValue(key, out var value))
        {
            if (value is T obj)
            {
                return obj;
            }
        }

        return default;
    }

    /// <inheritdoc />
    public void AddTimedEvent(object data, Dictionary<string, object> customFields = null)
    {
        var date = _systemClock.GetCurrentDateTime();

        long? timestamp = Configuration.IncludeTimestamps ? _systemClock.GetCurrentTimestamp() : null;

        var offset = (int)(date - Event.StartDate).TotalMilliseconds;

        Event.TimedEvents ??= [];

        Event.TimedEvents.Add(new TimedEvent
        {
            Date = date,
            Timestamp = timestamp,
            Offset = offset,
            Data = data,
            CustomFields = customFields
        });
    }

    /// <inheritdoc />
    public void AddTimedEvent(object data, object extraFields)
    {
        var customFields = GetExtraFields(extraFields);

        AddTimedEvent(data, customFields);
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
        Configuration.InvokeCustomActions(ActionType.OnScopeDisposed, this);
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
        await Configuration.InvokeCustomActionsAsync(ActionType.OnScopeDisposed, this, CancellationToken.None);
    }

    /// <inheritdoc />
    public void Discard()
    {
        // Mark as saved to ignore the saving
        _ended = true;
    }

    /// <summary>
    /// Ends the event.
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
    /// Ends the event.
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

    /// <inheritdoc />
    public void Save()
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();
        SaveEvent();
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        EndEvent();
        await SaveEventAsync(false, cancellationToken);
    }

    /// <inheritdoc />
    public T EventAs<T>() where T : AuditEvent
    {
        return Event as T;
    }

    /// <inheritdoc />
    public Activity GetActivity()
    {
        return _activity;
    }

    #endregion

    #region Private Methods

    public static AuditActivityTrace GetActivityTraceData()
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
        if (options.ExcludeEnvironmentInfo ?? Configuration.ExcludeEnvironmentInfo)
        {
            return null;
        }

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

        if (options.IncludeStackTrace ?? Configuration.IncludeStackTrace)
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
        Configuration.InvokeCustomActions(ActionType.OnScopeCreated, this);

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
        await Configuration.InvokeCustomActionsAsync(ActionType.OnScopeCreated, this, cancellationToken);

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
        if (Event.Environment != null)
        {
            var exception = GetCurrentException();
            Event.Environment.Exception = exception != null ? $"{exception.GetType().Name}: {exception.Message}" : null;
        }

        Event.EndDate = _systemClock.GetCurrentDateTime();

        if (Event.StartTimestamp.HasValue)
        {
            Event.EndTimestamp = _systemClock.GetCurrentTimestamp();
        }

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
        foreach (var prop in GetExtraFields(extraFields))
        {
            SetCustomField(prop.Key, prop.Value);
        }
    }

    private static Dictionary<string, object> GetExtraFields(object extraFields)
    {
        var results = new Dictionary<string, object>();

        if (extraFields == null)
        {
            return results;
        }

        var props = extraFields.GetType().GetRuntimeProperties();

        foreach (var prop in props)
        {
            results.Add(prop.Name, prop.GetValue(extraFields, null));
        }

        return results;
    }

    private void SaveEvent(bool forceInsert = false)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        // Execute custom on event saving actions
        Configuration.InvokeCustomActions(ActionType.OnEventSaving, this);

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
        Configuration.InvokeCustomActions(ActionType.OnEventSaved, this);
    }

    private async Task SaveEventAsync(bool forceInsert = false, CancellationToken cancellationToken = default)
    {
        if (IsEndedOrDisabled())
        {
            return;
        }

        // Execute custom on event saving actions
        await Configuration.InvokeCustomActionsAsync(ActionType.OnEventSaving, this, cancellationToken);

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
        await Configuration.InvokeCustomActionsAsync(ActionType.OnEventSaved, this, cancellationToken);
    }

    #endregion
}