using System;
using System.Collections.Generic;
using System.Text.Json;
using Audit.Core;

namespace Audit.Test
{
    /// <summary>
    /// Quick test to verify CustomFields serialization/deserialization with JsonExtensionData
    /// </summary>
    public class TestCustomFieldsSerialization
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing AuditActivityTag serialization...\n");

            // Test 1: Create with customFields via constructor
            var tag1 = new AuditActivityTag("testKey", "testValue", new Dictionary<string, object>
            {
                { "customField1", "value1" },
                { "customField2", 42 }
            });

            var json1 = JsonSerializer.Serialize(tag1);
            Console.WriteLine("Serialized (with customFields in constructor):");
            Console.WriteLine(json1);
            Console.WriteLine();

            var deserialized1 = JsonSerializer.Deserialize<AuditActivityTag>(json1);
            Console.WriteLine("Deserialized CustomFields:");
            if (deserialized1?.CustomFields != null)
            {
                foreach (var kvp in deserialized1.CustomFields)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            Console.WriteLine();

            // Test 2: Create without customFields, then add them
            var tag2 = new AuditActivityTag("testKey2", "testValue2")
            {
                CustomFields = new Dictionary<string, object>
                {
                    { "extraField1", "extraValue1" },
                    { "extraField2", true }
                }
            };

            var json2 = JsonSerializer.Serialize(tag2);
            Console.WriteLine("Serialized (with customFields set after):");
            Console.WriteLine(json2);
            Console.WriteLine();

            var deserialized2 = JsonSerializer.Deserialize<AuditActivityTag>(json2);
            Console.WriteLine("Deserialized CustomFields:");
            if (deserialized2?.CustomFields != null)
            {
                foreach (var kvp in deserialized2.CustomFields)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            Console.WriteLine();

            // Test 3: Deserialize JSON with extra fields (JsonExtensionData behavior)
            var jsonWithExtraFields = @"{""Key"":""testKey3"",""Value"":""testValue3"",""extraFieldA"":""valueA"",""extraFieldB"":123}";
            Console.WriteLine("Deserializing JSON with extra fields:");
            Console.WriteLine(jsonWithExtraFields);
            Console.WriteLine();

            var deserialized3 = JsonSerializer.Deserialize<AuditActivityTag>(jsonWithExtraFields);
            Console.WriteLine("Deserialized CustomFields (from extra JSON fields):");
            if (deserialized3?.CustomFields != null)
            {
                foreach (var kvp in deserialized3.CustomFields)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            Console.WriteLine();

            // Test 4: AuditActivityEvent
            Console.WriteLine("\nTesting AuditActivityEvent serialization...\n");

            var event1 = new AuditActivityEvent("TestEvent", new Dictionary<string, object>
            {
                { "eventCustomField", "eventValue" }
            });

            var eventJson = JsonSerializer.Serialize(event1);
            Console.WriteLine("Serialized Event:");
            Console.WriteLine(eventJson);
            Console.WriteLine();

            var deserializedEvent = JsonSerializer.Deserialize<AuditActivityEvent>(eventJson);
            Console.WriteLine("Deserialized Event CustomFields:");
            if (deserializedEvent?.CustomFields != null)
            {
                foreach (var kvp in deserializedEvent.CustomFields)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            Console.WriteLine("\n✓ All tests completed successfully!");
        }
    }
}

