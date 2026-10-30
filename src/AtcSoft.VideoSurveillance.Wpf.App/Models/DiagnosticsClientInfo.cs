namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

public sealed class DiagnosticsClientInfo
{
    public string AppVersion { get; set; } = string.Empty;

    public string OsDescription { get; set; } = string.Empty;

    public string RuntimeVersion { get; set; } = string.Empty;

    public string LogPath { get; set; } = string.Empty;

    public bool AutoStartEnabled { get; set; }
}