using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Audit.Core;

/// <summary>
/// Extension methods for AuditEvent to access Activity information
/// </summary>
public static class AuditEventExtensions
{
    /// <summary>
    /// Gets the Activity associated with this audit event.
    /// When an AuditScope is created with startActivityTrace: true, it creates or uses Activity.Current
    /// </summary>
    public static Activity? GetActivity(this AuditEvent auditEvent)
    {
        return Activity.Current;
    }

    /// <summary>
    /// Gets the AuditActivityTrace stored in this audit event's custom fields, if any
    /// </summary>
    public static AuditActivityTrace? GetActivityTrace(this AuditEvent auditEvent)
    {
        if (auditEvent.CustomFields != null && auditEvent.CustomFields.TryGetValue("_ActivityTrace", out var existing))
        {
            if (existing is AuditActivityTrace trace)
            {
                return trace;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts and returns a new AuditActivityTrace for this audit event, populating fields without using System.Diagnostics.Activity
    /// </summary>
    /// <param name="auditEvent">The audit event</param>
    /// <param name="operationName">The operation name for the activity</param>
    /// <returns>A new AuditActivityTrace instance</returns>
    public static AuditActivityTrace StartActivity(this AuditEvent auditEvent, string? operationName = null)
    {
        var activity = new AuditActivityTrace
        {
            StartTimeUtc = DateTime.UtcNow,
            SpanId = GenerateSpanId(),
            TraceId = GenerateTraceId(),
            Operation = operationName ?? auditEvent.EventType ?? "Unknown",
            Status = "Unset"
        };

        // Try to get parent context from existing activity or correlation
        if (auditEvent.CustomFields != null && auditEvent.CustomFields.TryGetValue("_ParentActivityTrace", out var parent))
        {
            if (parent is AuditActivityTrace parentTrace)
            {
                activity.ParentId = parentTrace.SpanId;
                activity.TraceId = parentTrace.TraceId; // Inherit trace ID from parent
            }
        }

        // Store the activity in custom fields so it can be retrieved later
        auditEvent.CustomFields ??= new Dictionary<string, object>();
        auditEvent.CustomFields["_ActivityTrace"] = activity;

        return activity;
    }

    /// <summary>
    /// Generates a random 16-character hex span ID
    /// </summary>
    private static string GenerateSpanId()
    {
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a random 32-character hex trace ID
    /// </summary>
    private static string GenerateTraceId()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
