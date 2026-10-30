namespace AtcSoft.Video.Surveillance.Api.Domain.Streaming;

/// <summary>
/// How a request to watch a camera went.
/// </summary>
public enum LiveStreamOutcome
{
    Started,
    CameraNotFound,
    Failed,
}