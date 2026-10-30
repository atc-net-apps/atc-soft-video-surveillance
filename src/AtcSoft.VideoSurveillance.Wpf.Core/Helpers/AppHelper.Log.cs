namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

public static partial class AppHelper
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Message} - {Percentage}%")]
    private static partial void LogInitMessage(ILogger logger, string message, int percentage);
}