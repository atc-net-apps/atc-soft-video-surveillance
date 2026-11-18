namespace Atc.Video.Engine.Helpers;

/// <summary>
/// How loud a failed open is logged.
/// </summary>
/// <param name="Level">Warning for the first failure and once per reminder, debug otherwise.</param>
/// <param name="Attempts">The attempts that failed in a row, this one included.</param>
/// <param name="Since">The time since the first of them.</param>
internal sealed record OpenFailureNote(
    Microsoft.Extensions.Logging.LogLevel Level,
    int Attempts,
    TimeSpan Since);