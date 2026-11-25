namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// The result of reading a camera's ONVIF profiles.
/// </summary>
/// <param name="Outcome">How it went.</param>
/// <param name="Profiles">The profiles read, with their stream addresses.</param>
/// <param name="Choice">The main stream and the sub-stream, when the profiles were read.</param>
public sealed record OnvifImportResult(
    OnvifImportOutcome Outcome,
    IReadOnlyList<OnvifStreamProfile> Profiles,
    OnvifStreamChoice? Choice)
{
    public static OnvifImportResult Failed(OnvifImportOutcome outcome)
        => new(outcome, [], Choice: null);
}