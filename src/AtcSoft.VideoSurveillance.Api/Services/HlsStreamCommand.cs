namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The FFmpeg command line that turns a network camera's stream into HLS
/// for browsers. A USB camera is not streamed this way: Windows gives it to
/// one program at a time, so it is streamed from the one pipeline the
/// server keeps for it (<see cref="ICameraPipelineHost"/>).
/// </summary>
public static class HlsStreamCommand
{
    private const string ToolName = "ffmpeg";
    private const string RedactedCredentials = "***:***@";

    // A password shorter than this is only removed where it stands as a
    // password ("user:password@"). Removing every "1" or "ab" from a log
    // line would ruin the line without hiding anything.
    private const int MinimumLengthToRemoveEverywhere = 6;

    /// <summary>
    /// Finds the FFmpeg command-line tool. It is taken from the folder the
    /// engine loads its libraries from, so it is the same version and needs
    /// no entry in the PATH; without it there, the PATH is searched.
    /// </summary>
    /// <param name="ffmpegFolder">The folder holding the FFmpeg libraries, if known.</param>
    /// <returns>The full path of the tool, or its bare name for a PATH lookup.</returns>
    public static string FindTool(string? ffmpegFolder)
    {
        if (!string.IsNullOrEmpty(ffmpegFolder))
        {
            var tool = Path.Combine(ffmpegFolder, OperatingSystem.IsWindows() ? ToolName + ".exe" : ToolName);
            if (File.Exists(tool))
            {
                return tool;
            }
        }

        return ToolName;
    }

    /// <summary>
    /// Builds the arguments that stream a camera to an HLS playlist.
    /// </summary>
    /// <param name="camera">The camera to stream.</param>
    /// <param name="playlistPath">The playlist file to write.</param>
    /// <returns>The argument string. It holds the camera's password: log it through <see cref="Redact"/>.</returns>
    public static string BuildArguments(
        CameraConfiguration camera,
        string playlistPath)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentException.ThrowIfNullOrEmpty(playlistPath);

        if (camera.Connection.Source == CameraSource.Usb)
        {
            throw new ArgumentException(
                "A USB camera is streamed from its shared pipeline, not by an FFmpeg process.",
                nameof(camera));
        }

        return BuildNetworkArguments(camera, playlistPath);
    }

    private static string BuildNetworkArguments(
        CameraConfiguration camera,
        string playlistPath)
    {
        var streamUri = camera.BuildUri();

        var arguments = new List<string>
        {
            // Everything FFmpeg prints is logged; its progress line comes
            // twice a second for every stream.
            "-nostats",
            "-v info",
            "-fflags nobuffer",
            "-flags low_delay",
            "-probesize 512000",
            "-analyzeduration 500000",
        };

        // An option the input format does not know makes FFmpeg refuse to
        // start, so the RTSP transport is only given to RTSP cameras.
        if (camera.Connection.Protocol == CameraProtocol.Rtsp)
        {
            arguments.Add($"-rtsp_transport {camera.Stream.RtspTransport ?? "tcp"}");
        }

        // FFmpeg 9 verifies a server's certificate by default, and most cameras
        // that offer HTTPS have a self-signed one: the engine does not verify
        // either (StreamOptions.VerifyTlsCertificate).
        if (camera.Connection.Protocol == CameraProtocol.Https)
        {
            arguments.Add("-tls_verify 0");
        }

        // -c:v copy: pass the camera's native H.264/H.265 video stream
        // straight through to HLS without re-encoding. Modern IP cameras
        // already produce HLS-compatible video, so transcoding is pure CPU
        // waste — at 10+ viewed cameras libx264 saturates the host. HLS
        // still honours -hls_time at GOP boundaries (typically 2-4 s).
        // Audio is always re-encoded because cameras commonly use G.711
        // / PCMU which HLS / browser MSE cannot demux. Cameras with non-
        // H.264/H.265 video will fail under this default — for those, add
        // a future per-camera "force transcoding" setting.
        // hls_flags split_by_time is critical for stream-copied input
        // (-c:v copy): without it, the HLS muxer only cuts segments at
        // keyframe boundaries, so a camera with a long GOP (30s+ between
        // I-frames) won't produce its first .ts file — and therefore won't
        // produce stream.m3u8 — within the StartStream playlist-ready
        // timeout. split_by_time relaxes that to cut at hls_time boundaries
        // regardless of keyframes; the first segment may briefly fail to
        // decode until the browser finds a keyframe, but playback recovers
        // within a fraction of a second and the stream actually starts.
        arguments.AddRange(
        [
            "-timeout 10000000",
            $"-i \"{streamUri}\"",
            "-c:v copy",
            "-c:a aac -b:a 64k",
            "-f hls",
            "-hls_time 2",
            "-hls_list_size 5",
            "-hls_flags delete_segments+split_by_time",
            $"\"{playlistPath}\"",
        ]);

        return string.Join(' ', arguments);
    }

    /// <summary>
    /// Removes the camera's user name and password from a text that goes
    /// into a log or to a client: the command line, and FFmpeg's own output,
    /// which prints the address it opened.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <param name="camera">The camera whose credentials must not appear.</param>
    /// <returns>The text with the credentials replaced.</returns>
    public static string Redact(
        string text,
        CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // A USB camera has no address and no credentials.
        if (camera.Connection.Source != CameraSource.Network)
        {
            return text;
        }

        var userInfo = camera.BuildUri().UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            text = text.Replace(userInfo + "@", RedactedCredentials, StringComparison.Ordinal);
        }

        // The same credentials, written unescaped.
        var userName = camera.Authentication.UserName;
        var password = camera.Authentication.Password;
        if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
        {
            text = text.Replace($"{userName}:{password}@", RedactedCredentials, StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(password) && password.Length >= MinimumLengthToRemoveEverywhere)
        {
            text = text.Replace(password, "***", StringComparison.Ordinal);
        }

        return text;
    }
}