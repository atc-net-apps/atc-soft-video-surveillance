namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

/// <summary>
/// How a desktop player opens a camera: the same for a tile and for a camera watched without one.
/// </summary>
public static class CameraStreamOptions
{
    /// <summary>
    /// Gets the options to open a camera's stream with.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="hardwareAcceleration">Whether the stream is decoded on the graphics card.</param>
    /// <param name="videoQuality">The video quality, as in the settings (the highest resolution decoded).</param>
    /// <param name="transcodeVideoCodec">The codec recordings are made in.</param>
    /// <returns>The options.</returns>
    public static StreamOptions For(
        CameraConfiguration camera,
        bool hardwareAcceleration,
        string videoQuality,
        VideoTranscodeCodec transcodeVideoCodec)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var locator = camera.BuildSourceLocator();
        return new StreamOptions
        {
            Source = camera.Display.DisplayName,
            UseLowLatencyMode = camera.Stream.UseLowLatencyMode,
            MaxLatencyMs = camera.Stream.MaxLatencyMs,
            RtspTransport = camera.Stream.RtspTransport,
            BufferDurationMs = camera.Stream.BufferDurationMs,
            HardwareAcceleration = hardwareAcceleration,
            MaxVerticalResolution = DropDownItemsFactory.GetMaxResolutionFromQuality(videoQuality),
            InputFormat = MapInputFormat(locator.InputFormat),
            RawDeviceSpec = locator.RawDeviceSpec,
            RecordAudio = locator.IncludesAudio,
            RecordingPreRoll = AtcSoft.Video.Surveillance.Helpers.RecordingPreRoll.Motion,
            TranscodeRecordingToH264 = transcodeVideoCodec == VideoTranscodeCodec.H264,
            VideoSize = locator.VideoSize,
            FrameRate = locator.FrameRate,
            PixelFormat = locator.PixelFormat,
        };
    }

    private static InputFormatKind MapInputFormat(string? name)
        => name switch
        {
            "dshow" => InputFormatKind.Dshow,
            "v4l2" => InputFormatKind.V4l2,
            "avfoundation" => InputFormatKind.AVFoundation,
            _ => InputFormatKind.Auto,
        };
}