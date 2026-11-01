namespace AtcSoft.Video.Surveillance.Enums;

/// <summary>
/// What reading a data file with <see cref="Helpers.SafeJsonFile"/> found.
/// </summary>
public enum JsonFileReadOutcome
{
    /// <summary>
    /// Neither the file nor its backup exists: a first start.
    /// </summary>
    Missing = 0,

    /// <summary>
    /// The file was read.
    /// </summary>
    Read = 1,

    /// <summary>
    /// The file was missing or could not be read; its backup was read instead.
    /// </summary>
    ReadFromBackup = 2,

    /// <summary>
    /// The file exists but neither it nor its backup could be read.
    /// </summary>
    Unreadable = 3,
}