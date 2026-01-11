namespace Audit.FileSystem;

/// <summary>
/// The file system event type
/// </summary>
public enum FileSystemEventType
{
    Create = 0,
    Change = 1,
    Rename = 2,
    Delete = 3
}