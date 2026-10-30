namespace AtcSoft.VideoEngine.FFmpeg;

/// <summary>
/// Handles loading and initializing FFmpeg native libraries.
/// </summary>
internal static class FFmpegLoader
{
    private const int LogBufferSize = 4096;

    /// <summary>
    /// Gets the FFmpeg version string, or <c>null</c> if not yet loaded.
    /// </summary>
    internal static string? Version { get; private set; }

    /// <summary>
    /// Gets a value indicating whether FFmpeg 8.0 or greater is loaded.
    /// </summary>
    internal static bool IsVersion8OrGreater { get; private set; }

    /// <summary>
    /// Loads FFmpeg native libraries and configures logging.
    /// </summary>
    /// <param name="path">The path to FFmpeg native libraries, or <c>null</c> for default resolution.</param>
    /// <param name="logLevel">The FFmpeg log level.</param>
    /// <returns>The FFmpeg version string.</returns>
    internal static string Initialize(
        string? path,
        FFmpegLogLevel logLevel)
    {
        var resolvedPath = ResolvePath(path);
        LoadLibraries(resolvedPath, LoadProfile.All);

        // Initialize networking (OpenSSL/GnuTLS) for RTSP/HTTPS streams.
        _ = avformat_network_init();

        var ver = avformat_version();
        Version = $"{ver >> 16}.{(ver >> 8) & 255}.{ver & 255}";
        IsVersion8OrGreater = ver >> 16 > 61;

        ConfigureLogging(logLevel);

        return Version;
    }

    /// <summary>
    /// Converts an FFmpeg error code to a human-readable message.
    /// </summary>
    /// <param name="errorCode">The FFmpeg error code.</param>
    /// <returns>A human-readable error message.</returns>
    internal static unsafe string ErrorCodeToMessage(int errorCode)
    {
        var buffer = stackalloc byte[LogBufferSize];
        _ = av_strerror(errorCode, buffer, (nuint)LogBufferSize);
        return BytePtrToStringUtf8(buffer);
    }

    private static unsafe void ConfigureLogging(FFmpegLogLevel logLevel)
    {
        if (logLevel > FFmpegLogLevel.Quiet)
        {
            av_log_set_level(logLevel);
            av_log_set_callback(FFmpegLogCallback);
        }
        else
        {
            av_log_set_level(FFmpegLogLevel.Quiet);
            av_log_set_callback(null);
        }
    }

    private static readonly unsafe av_log_set_callback_callback FFmpegLogCallback = (p0, level, format, vl) =>
    {
        if (level > av_log_get_level())
        {
            return;
        }

        var buffer = stackalloc byte[LogBufferSize];
        var printPrefix = 1;
        av_log_format_line2(p0, level, format, vl, buffer, LogBufferSize, &printPrefix);
        var trimmed = BytePtrToStringUtf8(buffer).Trim();

        if (IsBenignNoise(trimmed))
        {
            return;
        }

        // Map FFmpeg severity to the matching .NET Trace channel so VS Output
        // (and external trace listeners) filter by level correctly.
        if (level <= FFmpegLogLevel.Error)
        {
            Trace.TraceError("FFmpeg|{0,-7}|{1}", level, trimmed);
        }
        else if (level <= FFmpegLogLevel.Warn)
        {
            Trace.TraceWarning("FFmpeg|{0,-7}|{1}", level, trimmed);
        }
        else
        {
            Trace.TraceInformation("FFmpeg|{0,-7}|{1}", level, trimmed);
        }
    };

    /// <summary>
    /// Returns true for FFmpeg messages that are spammy but safe to ignore —
    /// typically camera-side metadata that the bitstream parser flags but
    /// playback is unaffected. Filtered here to avoid drowning the output.
    /// </summary>
    private static bool IsBenignNoise(string message)
    {
        // Many IP cameras (Tapo, Hikvision, Dahua) embed vendor SEI messages
        // that FFmpeg considers malformed. Logged per-frame at AV_LOG_ERROR.
        if (message.Contains("SEI type", StringComparison.Ordinal)
            && message.Contains("truncated at", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string ResolvePath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return Path.GetFullPath(path);
        }

        // Default: look next to the entry assembly
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static unsafe string BytePtrToStringUtf8(byte* ptr)
    {
        if (ptr is null)
        {
            return string.Empty;
        }

        var length = 0;
        while (ptr[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(ptr, length);
    }
}