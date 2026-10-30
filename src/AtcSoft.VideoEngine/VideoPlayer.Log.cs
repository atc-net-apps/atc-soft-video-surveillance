namespace AtcSoft.VideoEngine;

public sealed partial class VideoPlayer
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Recording started: {Path}")]
    private partial void LogRecordingStarted(string path);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Recording stopped")]
    private partial void LogRecordingStopped();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Recording switched atomically to: {Path}")]
    private partial void LogRecordingSwitched(string path);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Recording as H.264 with {Encoder}: the source is decoded and encoded again")]
    private partial void LogRecordingEncoder(string encoder);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Live stream started: {Path}")]
    private partial void LogStreamingStarted(string path);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Live stream stopped")]
    private partial void LogStreamingStopped();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "The source's sound cannot be recorded, recording the picture only: {Reason}")]
    private partial void LogRecordingWithoutSound(string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] Opening stream: {Uri}")]
    private partial void LogOpeningStream(string source, string uri);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[{Source}] The camera did not open together with its microphone '{Microphone}' ({Reason}). Opening it without sound; check that the microphone is connected")]
    private partial void LogOpeningWithoutMicrophone(string source, string microphone, string reason);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] Stream demuxer opened successfully")]
    private partial void LogStreamDemuxerOpened(string source);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] Decoder opened: HwAccel={HwAccel}")]
    private partial void LogDecoderOpened(string source, bool hwAccel);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] Stream opened: {StreamInfo} from {Uri}")]
    private partial void LogStreamOpened(string source, VideoStreamInfo streamInfo, string uri);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[{Source}] Demux loop failed")]
    private partial void LogDemuxLoopFailed(string source, Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "[{Source}] End of stream reached")]
    private partial void LogEndOfStreamReached(string source);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "[{Source}] Exceeded {Max} consecutive read errors")]
    private partial void LogExceededConsecutiveReadErrors(string source, int max);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[{Source}] Read-error burst classified: lastErrorCode={LastErrorCode}, reason={Reason}")]
    private partial void LogReadErrorCode(string source, int lastErrorCode, StreamFailureReason reason);
}