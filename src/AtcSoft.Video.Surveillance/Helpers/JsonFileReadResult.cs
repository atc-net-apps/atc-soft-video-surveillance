namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// What reading a JSON data file found, and whether the file may be written over.
/// </summary>
/// <typeparam name="T">The type read.</typeparam>
/// <param name="Outcome">What was found.</param>
/// <param name="Value">The value read; <c>default</c> unless <paramref name="Outcome"/> is
/// <see cref="JsonFileReadOutcome.Read"/> or <see cref="JsonFileReadOutcome.ReadFromBackup"/>.</param>
/// <param name="Error">Why the file itself could not be read; <c>null</c> when it was.</param>
/// <param name="PreservedCopies">Where the files that could not be read were copied, so a save does not lose them.</param>
/// <param name="MayOverwrite">
/// <c>false</c> when a file that could not be read could not be copied aside either (it is
/// locked, or access is denied): saving would then destroy it, so the caller must not save.
/// </param>
public sealed record JsonFileReadResult<T>(
    JsonFileReadOutcome Outcome,
    T? Value,
    Exception? Error,
    IReadOnlyList<string> PreservedCopies,
    bool MayOverwrite)
{
    /// <summary>
    /// Gets a value indicating whether a value was read, from the file or its backup.
    /// </summary>
    public bool HasValue
        => Outcome is JsonFileReadOutcome.Read or JsonFileReadOutcome.ReadFromBackup;
}