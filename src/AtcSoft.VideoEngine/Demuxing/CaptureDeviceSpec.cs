namespace AtcSoft.VideoEngine.Demuxing;

/// <summary>
/// Reads the device specifier of a capture source
/// (<see cref="StreamOptions.RawDeviceSpec"/>).
/// </summary>
internal static class CaptureDeviceSpec
{
    private const string VideoPrefix = "video=";
    private const string AudioSeparator = ":audio=";

    /// <summary>
    /// Gets the options that open a camera for its picture alone. A camera
    /// that is opened together with a microphone does not open at all when
    /// the microphone is not there (it sat in a monitor or dock that is not
    /// connected), so it is given this second way.
    /// </summary>
    /// <param name="options">The options that name the camera and its microphone.</param>
    /// <param name="videoOnly">A copy of the options without the microphone. The original is left as it is.</param>
    /// <param name="microphone">The microphone that is left out.</param>
    /// <returns><see langword="false"/> when the source is not a DirectShow camera with a microphone.</returns>
    public static bool TryWithoutAudio(
        StreamOptions options,
        out StreamOptions? videoOnly,
        out string? microphone)
    {
        ArgumentNullException.ThrowIfNull(options);

        videoOnly = null;
        microphone = null;

        var spec = options.RawDeviceSpec;
        if (options.InputFormat != InputFormatKind.Dshow ||
            spec is null ||
            !spec.StartsWith(VideoPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // A device name may hold a colon; the microphone starts at the last ":audio=".
        var at = spec.LastIndexOf(AudioSeparator, StringComparison.Ordinal);
        if (at <= VideoPrefix.Length)
        {
            return false;
        }

        microphone = spec[(at + AudioSeparator.Length)..];
        videoOnly = options.CopyWithDeviceSpec(spec[..at]);
        return true;
    }
}