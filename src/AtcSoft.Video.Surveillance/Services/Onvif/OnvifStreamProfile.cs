namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// One of a camera's ONVIF media profiles: a stream it offers.
/// </summary>
/// <param name="Token">The profile's token, as the camera names it.</param>
/// <param name="Name">The profile's name (MainStream, SubStream, Profile_1, ...).</param>
/// <param name="Encoding">The video encoding (H264, H265, JPEG, MPEG4).</param>
/// <param name="Width">The picture's width.</param>
/// <param name="Height">The picture's height.</param>
/// <param name="StreamUri">The RTSP address of the stream, as the camera gave it; <see langword="null"/> when it gave none.</param>
public sealed record OnvifStreamProfile(
    string Token,
    string Name,
    string Encoding,
    int Width,
    int Height,
    Uri? StreamUri)
{
    /// <summary>
    /// Gets the picture's area, to compare profiles by.
    /// </summary>
    public long Area => (long)Width * Height;
}