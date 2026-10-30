namespace AtcSoft.VideoEngine.Demuxing;

internal sealed partial class Demuxer
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "[{Source}] avformat_open_input returned AVERROR_EXIT: abortRequested={Abort}, ctCancelled={CtCancelled}, elapsed={Elapsed:F1}s, timeout={Timeout}s")]
    private partial void LogAvformatOpenInputAborted(string source, bool abort, bool ctCancelled, double elapsed, int timeout);
}