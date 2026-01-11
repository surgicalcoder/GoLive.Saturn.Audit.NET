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

    /// <summary>
    /// Status code of the activity
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Status { get; set; }

    /// <summary>
    /// Status description of the activity
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string StatusDescription { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }

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
        Status = activity.Status.ToString();
        StatusDescription = activity.StatusDescription;
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
    /// Sets a tag (key/value pair) on the activity
    /// </summary>
    /// <param name="key">The tag key</param>
    /// <param name="value">The tag value</param>
    /// <returns>This AuditActivityTrace instance for chaining</returns>
    public AuditActivityTrace SetTag(string key, object value)
    {
        Tags ??= new List<AuditActivityTag>();
        
        // Remove existing tag with the same key if it exists
        var existingTag = Tags.FirstOrDefault(t => t.Key == key);
        if (existingTag != null)
        {
            Tags.Remove(existingTag);
        }
        
        Tags.Add(new AuditActivityTag(key, value));
        return this;
    }

    /// <summary>
    /// Adds an event to the activity
    /// </summary>
    /// <param name="activityEvent">The event to add</param>
    /// <returns>This AuditActivityTrace instance for chaining</returns>
    public AuditActivityTrace AddEvent(System.Diagnostics.ActivityEvent activityEvent)
    {
        Events ??= new List<AuditActivityEvent>();
        
        var customFields = activityEvent.Tags != null && activityEvent.Tags.Any()
            ? activityEvent.Tags.ToDictionary(t => t.Key, t => (object)t.Value)
            : null;
            
        Events.Add(new AuditActivityEvent(activityEvent.Timestamp, activityEvent.Name, customFields));
        return this;
    }

    /// <summary>
    /// Adds an event to the activity with a name and optional tags
    /// </summary>
    /// <param name="eventName">The name of the event</param>
    /// <param name="tags">Optional dictionary of tags</param>
    /// <returns>This AuditActivityTrace instance for chaining</returns>
    public AuditActivityTrace AddEvent(string eventName, Dictionary<string, object>? tags = null)
    {
        Events ??= new List<AuditActivityEvent>();
        Events.Add(new AuditActivityEvent(DateTimeOffset.UtcNow, eventName, tags));
        return this;
    }

    /// <summary>
    /// Sets the status of the activity
    /// </summary>
    /// <param name="status">The status code</param>
    /// <param name="description">Optional status description</param>
    /// <returns>This AuditActivityTrace instance for chaining</returns>
    public AuditActivityTrace SetStatus(System.Diagnostics.ActivityStatusCode status, string description = null)
    {
        Status = status.ToString();
        StatusDescription = description;
        return this;
    }
    
}


public class AuditActivityTag
{
    [JsonConstructor]
    public AuditActivityTag(string key, object value)
    {
        Key = key;
        Value = value;
    }

    public AuditActivityTag(string key, object value, Dictionary<string, object> customFields = null)
        : this(key, value)
    {
        CustomFields = customFields;
    }

    public string Key { get; set; }
    public object Value { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }
}

public class AuditActivityEvent
{
    [JsonConstructor]
    public AuditActivityEvent(DateTimeOffset timestamp, string name)
    {
        Timestamp = timestamp;
        Name = name;
    }

    public AuditActivityEvent(DateTimeOffset timestamp, string name, Dictionary<string, object> customFields = null)
        : this(timestamp, name)
    {
        CustomFields = customFields;
    }

    public AuditActivityEvent(string name, Dictionary<string, object> customFields = null)
        : this(DateTimeOffset.UtcNow, name)
    {
        CustomFields = customFields;
    }

    public DateTimeOffset Timestamp { get; set; }
    public string Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> CustomFields { get; set; }
}