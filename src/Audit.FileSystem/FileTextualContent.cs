using System.Text.Json.Serialization;

namespace Audit.FileSystem;

public class FileTextualContent : IFileContent
{
    public string Value { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentType Type { get; set; } = ContentType.Text;
}