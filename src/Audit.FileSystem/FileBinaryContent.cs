using System.Text.Json.Serialization;
using StringEnumConverter = System.Text.Json.Serialization.JsonStringEnumConverter;

namespace Audit.FileSystem;

public class FileBinaryContent : IFileContent
{
    public byte[] Value { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ContentType Type { get; set; } = ContentType.Binary;
}