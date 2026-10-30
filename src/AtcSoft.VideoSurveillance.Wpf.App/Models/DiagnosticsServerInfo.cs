namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

public sealed class DiagnosticsServerInfo
{
    public string ApiBaseAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the client had the server's live
    /// updates (GET /events) when the report was made.
    /// </summary>
    public bool LiveUpdatesConnected { get; set; }

    public int ConnectedCameras { get; set; }

    public int ActiveRecordings { get; set; }

    /// <summary>
    /// <see langword="null"/> when the gateway query failed; the JSON
    /// reader can then distinguish "no cameras" (empty array) from
    /// "couldn't reach the server" (null).
    /// </summary>
    public string? GatewayErrorMessage { get; set; }
}