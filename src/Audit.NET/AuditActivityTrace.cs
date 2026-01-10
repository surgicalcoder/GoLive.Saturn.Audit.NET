using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using GoLive.Saturn.Data.Entities;

namespace Audit.Core;

public class AuditActivityTrace : Entity, IAuditOutput
{
    /// <summary>
    /// Date and time when the Activity started
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// SPAN part of the Id
    /// </summary>
    public string SpanId { get; set; }

    /// <summary>
    /// TraceId part of the Id
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Id of the activity's parent
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Operation name
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// List of tags (key/value pairs) associated to the activity
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AuditActivityTag> Tags { get; set; }

    /// <summary>
    /// List of events (timestamped messages) attached to the activity
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AuditActivityEvent> Events { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }

    /// <summary>
    /// Populates this instance from a System.Diagnostics.Activity
    /// </summary>
    /// <param name="activity">The activity to populate from</param>
    public void PopulateFromActivity(System.Diagnostics.Activity activity)
    {
        if (activity == null) return;

        StartTimeUtc = activity.StartTimeUtc;
        SpanId = activity.SpanId.ToString();
        TraceId = activity.TraceId.ToString();
        ParentId = activity.ParentSpanId.ToString();
        Operation = activity.OperationName;

        if (activity.Tags != null && activity.Tags.Any())
        {
            Tags = activity.Tags.Select(t => new AuditActivityTag(t.Key, t.Value)).ToList();
        }

        if (activity.Events != null && activity.Events.Any())
        {
            Events = activity.Events.Select(e => new AuditActivityEvent(e.Timestamp, e.Name, 
                e.Tags.Any() ? e.Tags.ToDictionary(t => t.Key, t => (object)t.Value) : null)).ToList();
        }
    }

    /// <summary>
    /// Creates a new AuditActivityTrace instance from a System.Diagnostics.Activity
    /// </summary>
    /// <param name="activity">The activity to create from</param>
    /// <returns>A new AuditActivityTrace instance</returns>
    public static AuditActivityTrace FromActivity(System.Diagnostics.Activity activity)
    {
        var trace = new AuditActivityTrace();
        trace.PopulateFromActivity(activity);
        return trace;
    }

    /// <summary>
    /// Serializes this Activity Info entity as a JSON string
    /// </summary>
    public string ToJson()
    {
        return Configuration.JsonAdapter.Serialize(this);
    }

    /// <summary>
    /// Parses an AuditActivityInfo entity from its JSON string representation.
    /// </summary>
    /// <param name="json">JSON string with the AuditActivityInfo entity representation.</param>
    public static AuditActivityTrace FromJson(string json)
    {
        return Configuration.JsonAdapter.Deserialize<AuditActivityTrace>(json);
    }
}

public class AuditActivityTag
{
    public AuditActivityTag(string key, object value, Dictionary<string, object> customFields = null)
    {
        Key = key;
        Value = value;
        CustomFields = customFields;
    }

    public string Key { get; set; }
    public object Value { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }
}

public class AuditActivityEvent
{
    public AuditActivityEvent(DateTimeOffset timestamp, string name, Dictionary<string, object> customFields = null)
    {
        Timestamp = timestamp;
        Name = name;
        CustomFields = customFields;
    }

    public DateTimeOffset Timestamp { get; set; }
    public string Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }
}