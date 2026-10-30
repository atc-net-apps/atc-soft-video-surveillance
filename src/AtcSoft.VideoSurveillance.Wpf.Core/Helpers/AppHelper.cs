namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Helper methods for application initialization.
/// </summary>
public static partial class AppHelper
{
    /// <summary>
    /// Renders a loading initialization message to the splash screen.
    /// </summary>
    /// <param name="logger">Optional logger for debug output.</param>
    /// <param name="message">The message to display.</param>
    /// <param name="percentage">The progress percentage (0-100).</param>
    public static void RenderLoadingInitializeMessage(
        ILogger? logger,
        string message,
        int percentage)
    {
        var msg = Translations.InitializePrefix + " " + message;
        MessageListener.Instance.ReceiveMessage(msg);
        PercentListener.Instance.ReceivePercent(percentage);
        if (logger is not null)
        {
            LogInitMessage(logger, msg, percentage);
        }
    }
}