namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// One file/directory error encountered during a cleanup pass.
/// </summary>
/// <param name="Path">Path that produced the error.</param>
/// <param name="Exception">Underlying exception.</param>
public sealed record MediaCleanupRunError(string Path, Exception Exception);