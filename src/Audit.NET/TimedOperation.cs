using System;
using System.Collections.Generic;

namespace Audit.Core;

/// <summary>
/// A disposable helper class for timing operations and automatically recording them to an AuditActivityTrace.
/// Usage: using (var timer = new TimedOperation(activity, "OperationName")) { ... }
/// </summary>
public sealed class TimedOperation : IDisposable
{
    private readonly AuditActivityTrace? _activity;
    private readonly string _eventName;
    private readonly DateTimeOffset _startTime;
    private readonly Dictionary<string, object> _tags;
    private bool _disposed;

    public TimedOperation(AuditActivityTrace? activity, string eventName, Dictionary<string, object>? initialTags = null)
    {
        _activity = activity;
        _eventName = eventName;
        _startTime = DateTimeOffset.UtcNow;
        _tags = initialTags ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Add a tag that will be included in the event when disposed
    /// </summary>
    public void AddTag(string key, object? value)
    {
        _tags[key] = value;
    }

    /// <summary>
    /// Add multiple tags that will be included in the event when disposed
    /// </summary>
    public void AddTags(Dictionary<string, object> tags)
    {
        foreach (var tag in tags)
        {
            _tags[tag.Key] = tag.Value;
        }
    }

    /// <summary>
    /// Get the duration elapsed since the timer started
    /// </summary>
    public double GetElapsedMilliseconds()
    {
        return (DateTimeOffset.UtcNow - _startTime).TotalMilliseconds;
    }

    public void Dispose()
    {
        if (_disposed) return;

        var duration = GetElapsedMilliseconds();
        _tags["duration_ms"] = duration;

        _activity?.AddEvent(_eventName, _tags);

        _disposed = true;
    }
}

/// <summary>
/// Extension methods for creating TimedOperation instances from AuditScope
/// </summary>
public static class AuditScopeTimingExtensions
{
    /// <summary>
    /// Create a timed operation that will record to the audit scope's activity trace
    /// </summary>
    public static TimedOperation StartTimedOperation(this IAuditScope auditScope, string eventName, Dictionary<string, object>? initialTags = null)
    {
        return new TimedOperation(auditScope.Event.GetActivityTrace(), eventName, initialTags);
    }

    /// <summary>
    /// Create a timed operation with a single initial tag
    /// </summary>
    public static TimedOperation StartTimedOperation(this IAuditScope auditScope, string eventName, string tagKey, object? tagValue)
    {
        var tags = new Dictionary<string, object> { { tagKey, tagValue } };
        return new TimedOperation(auditScope.Event.GetActivityTrace(), eventName, tags);
    }
}

