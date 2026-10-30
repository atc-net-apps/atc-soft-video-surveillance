namespace AtcSoft.VideoEngine;

/// <summary>
/// Static utility for probing media file metadata using FFmpeg.
/// </summary>
public static class MediaProbe
{
    /// <summary>
    /// Gets the duration of a media file.
    /// </summary>
    /// <param name="filePath">The path to the media file.</param>
    /// <returns>The duration, or <see cref="TimeSpan.Zero"/> if it cannot be determined.</returns>
    public static unsafe TimeSpan GetDuration(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        AVFormatContext* fmtCtx = null;
        AVDictionary* dict = null;
        try
        {
            int ret = avformat_open_input(ref fmtCtx, filePath, null, ref dict);
            if (ret < 0)
            {
                return TimeSpan.Zero;
            }

            // Not "ref dict": this argument is one dictionary per stream,
            // and FFmpeg reads past a single one as soon as the file has a
            // second stream (sound), which takes the process down.
            ret = avformat_find_stream_info(fmtCtx, (AVDictionary**)null);
            if (ret < 0)
            {
                return TimeSpan.Zero;
            }

            var duration = fmtCtx->duration;
            if (duration <= 0)
            {
                return TimeSpan.Zero;
            }

            // fmtCtx->duration is in AV_TIME_BASE (microseconds)
            return TimeSpan.FromTicks(duration * 10);
        }
        catch
        {
            return TimeSpan.Zero;
        }
        finally
        {
            if (fmtCtx is not null)
            {
                avformat_close_input(ref fmtCtx);
            }
        }
    }

    /// <summary>
    /// Gets the clockwise rotation a media file asks players to apply to its
    /// video, as flagged by the recorder for a rotated camera.
    /// </summary>
    /// <param name="filePath">The path to the media file.</param>
    /// <returns>0, 90, 180 or 270; 0 when the file has no rotation flag or cannot be read.</returns>
    public static unsafe int GetRotation(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        AVFormatContext* fmtCtx = null;
        AVDictionary* dict = null;
        try
        {
            int ret = avformat_open_input(ref fmtCtx, filePath, null, ref dict);
            if (ret < 0)
            {
                return 0;
            }

            for (var i = 0; i < fmtCtx->nb_streams; i++)
            {
                var codecpar = fmtCtx->streams[i]->codecpar;
                if (codecpar->codec_type == AVMediaType.Video)
                {
                    return Recording.DisplayMatrix.ReadClockwiseDegrees(codecpar);
                }
            }

            return 0;
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (fmtCtx is not null)
            {
                avformat_close_input(ref fmtCtx);
            }
        }
    }

    /// <summary>
    /// Gets the size of a media file's picture as it is displayed: its rotation flag applied, so a
    /// camera turned on its side gives a tall picture.
    /// </summary>
    /// <param name="filePath">The path to the media file.</param>
    /// <returns>The width and height; (0, 0) when the file has no video or cannot be read.</returns>
    public static unsafe (int Width, int Height) GetDisplaySize(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        AVFormatContext* fmtCtx = null;
        AVDictionary* dict = null;
        try
        {
            // The size may only be known once the streams are probed.
            if (avformat_open_input(ref fmtCtx, filePath, null, ref dict) < 0 ||
                avformat_find_stream_info(fmtCtx, (AVDictionary**)null) < 0)
            {
                return (0, 0);
            }

            for (var i = 0; i < fmtCtx->nb_streams; i++)
            {
                var codecpar = fmtCtx->streams[i]->codecpar;
                if (codecpar->codec_type == AVMediaType.Video)
                {
                    var degrees = Recording.DisplayMatrix.ReadClockwiseDegrees(codecpar);
                    return degrees is 90 or 270
                        ? (codecpar->height, codecpar->width)
                        : (codecpar->width, codecpar->height);
                }
            }

            return (0, 0);
        }
        catch
        {
            return (0, 0);
        }
        finally
        {
            if (fmtCtx is not null)
            {
                avformat_close_input(ref fmtCtx);
            }
        }
    }
}