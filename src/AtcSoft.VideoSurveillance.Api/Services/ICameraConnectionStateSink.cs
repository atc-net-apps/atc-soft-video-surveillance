namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Takes the connection state of a camera the server keeps open without
/// recording it (one that is watched for motion), so clients see it as
/// connected like a camera that is being recorded.
/// </summary>
public interface ICameraConnectionStateSink
{
    /// <summary>
    /// Reports a camera's connection state.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="state">Its state now.</param>
    void Report(
        Guid cameraId,
        ConnectionState state);
}