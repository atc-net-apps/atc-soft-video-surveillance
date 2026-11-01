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
/// <param name="UnpreservedFiles">
/// The files that could not be read and could not be copied aside either (locked, access
/// denied): saving would destroy them.
/// </param>
public sealed record JsonFileReadResult<T>(
    JsonFileReadOutcome Outcome,
    T? Value,
    Exception? Error,
    IReadOnlyList<string> PreservedCopies,
    IReadOnlyList<string> UnpreservedFiles)
{
    /// <summary>
    /// Gets a value indicating whether a value was read, from the file or its backup.
    /// </summary>
    public bool HasValue
        => Outcome is JsonFileReadOutcome.Read or JsonFileReadOutcome.ReadFromBackup;

    /// <summary>
    /// Gets a value indicating whether a save may replace the file: <c>false</c> while a file
    /// that could not be read has not been copied aside.
    /// </summary>
    public bool MayOverwrite
        => UnpreservedFiles.Count == 0;
}