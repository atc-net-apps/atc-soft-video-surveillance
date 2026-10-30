namespace AtcSoft.VideoSurveillance.Factories;

/// <summary>
/// Factory for creating common dropdown/combobox items used across UIs.
/// Centralizes definitions to ensure consistency and simplify maintenance.
/// Note: DayFilterItems and TimeFilterItems that depend on WPF Translations remain in the WPF assembly.
/// </summary>
public static class DropDownItemsFactory
{
    public static IDictionary<string, string> VideoQualityItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Auto"] = "Auto (Source Quality)",
        ["1080p"] = "1080p",
        ["720p"] = "720p",
        ["480p"] = "480p",
        ["360p"] = "360p",
    };

    public static IDictionary<string, string> RtspTransportItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tcp"] = "TCP",
        ["udp"] = "UDP",
    };

    public static IDictionary<string, string> RecordingFormatItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mp4"] = "MP4 (H.264)",
        ["mkv"] = "MKV (Matroska)",
    };

    public static IDictionary<string, string> TranscodeVideoCodecItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["None"] = "As the camera sends it",
        ["H264"] = "Always H.264 (plays everywhere)",
    };

    public static IDictionary<string, string> OverlayPositionItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["TopLeft"] = "Top Left",
        ["TopRight"] = "Top Right",
        ["BottomLeft"] = "Bottom Left",
        ["BottomRight"] = "Bottom Right",
    };

    public static IDictionary<string, string> ConnectionTypeItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(CameraConnectionType.None)] = "Not set",
        [nameof(CameraConnectionType.Lan)] = "Wired (LAN)",
        [nameof(CameraConnectionType.Wifi)] = "Wi-Fi",
    };

    public static IDictionary<string, string> ConnectionTypeIconPlacementItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(ConnectionTypeIconPlacement.SameRow)] = "Same row as info box",
        [nameof(ConnectionTypeIconPlacement.SameColumn)] = "Same column as info box",
    };

    public static IDictionary<string, string> OverlayOpacityItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["0.0"] = "0%",
        ["0.1"] = "10%",
        ["0.2"] = "20%",
        ["0.3"] = "30%",
        ["0.4"] = "40%",
        ["0.5"] = "50%",
        ["0.6"] = "60%",
        ["0.7"] = "70%",
        ["0.8"] = "80%",
        ["0.9"] = "90%",
        ["1.0"] = "100%",
    };

    public static IDictionary<string, string> ProtocolItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Rtsp"] = "RTSP",
        ["Http"] = "HTTP",
    };

    public static IDictionary<string, string> MotionSensitivityItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["10"] = "Low (10)",
        ["30"] = "Medium (30)",
        ["50"] = "High (50)",
        ["70"] = "Very High (70)",
    };

    public static IDictionary<string, string> BoundingBoxThicknessItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1"] = "1 px",
        ["2"] = "2 px",
        ["3"] = "3 px",
        ["4"] = "4 px",
        ["5"] = "5 px",
    };

    public static IDictionary<string, string> BoundingBoxMinAreaItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["10"] = "10 px² (Tiny)",
        ["25"] = "25 px² (Small)",
        ["50"] = "50 px² (Medium)",
        ["100"] = "100 px² (Large)",
        ["200"] = "200 px² (Very Large)",
    };

    public static IDictionary<string, string> MotionAnalysisResolutionItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["320x240"] = "320x240 (Low CPU)",
        ["480x360"] = "480x360 (Balanced)",
        ["640x480"] = "640x480 (Better Detection)",
        ["800x600"] = "800x600 (High CPU)",
        ["960x720"] = "960x720 (Very High CPU)",
    };

    public static IDictionary<string, string> PostMotionDurationItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["5"] = "5 seconds",
        ["10"] = "10 seconds",
        ["15"] = "15 seconds",
        ["30"] = "30 seconds",
        ["60"] = "1 minute",
    };

    public static IDictionary<string, string> MediaCleanupScheduleItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Disabled"] = "Disabled",
        ["OnStartup"] = "On Application Startup",
        ["OnStartupAndPeriodically"] = "On Startup and Every 6 Hours",
    };

    public static IDictionary<string, string> MediaRetentionPeriodItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["7"] = "7 Days",
        ["14"] = "14 Days",
        ["30"] = "30 Days",
        ["60"] = "60 Days",
        ["90"] = "90 Days",
        ["180"] = "6 Months",
        ["365"] = "1 Year",
    };

    public static IDictionary<string, string> MinFreeSpaceItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["512"] = "500 MB",
        ["1024"] = "1 GB",
        ["2048"] = "2 GB (Recommended)",
        ["5120"] = "5 GB",
        ["10240"] = "10 GB",
        ["20480"] = "20 GB",
    };

    public static IDictionary<string, string> MaxRecordingDurationItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["15"] = "15 minutes",
        ["30"] = "30 minutes",
        ["60"] = "1 hour",
        ["120"] = "2 hours",
        ["180"] = "3 hours",
        ["240"] = "4 hours",
    };

    public static IDictionary<string, string> ThumbnailTileCountItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1"] = "1 tile (320x240)",
        ["4"] = "2x2 grid (640x480)",
    };

    public static IDictionary<string, string> TimelapseIntervalItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["10s"] = "10 seconds",
        ["30s"] = "30 seconds",
        ["1m"] = "1 minute",
        ["5m"] = "5 minutes",
        ["10m"] = "10 minutes",
        ["30m"] = "30 minutes",
        ["1h"] = "1 hour",
        ["3h"] = "3 hours",
        ["6h"] = "6 hours",
        ["12h"] = "12 hours",
        ["24h"] = "24 hours",
    };

    public const string DefaultVideoQuality = "Auto";
    public const string DefaultRtspTransport = "tcp";
    public const string DefaultRecordingFormat = "mkv";
    public const string DefaultTranscodeVideoCodec = "None";
    public const string DefaultOverlayPosition = "TopLeft";
    public const string DefaultConnectionType = nameof(CameraConnectionType.None);
    public const string DefaultConnectionTypeIconPlacement = nameof(ConnectionTypeIconPlacement.SameRow);
    public const string DefaultOverlayOpacity = "0.7";
    public const string DefaultProtocol = "Rtsp";
    public const int DefaultMotionSensitivity = 30;
    public const int DefaultPostMotionDuration = 10;
    public const string DefaultBoundingBoxColor = "Red";
    public const int DefaultBoundingBoxThickness = 2;
    public const int DefaultBoundingBoxMinArea = 25;
    public const string DefaultMotionAnalysisResolution = "320x240";
    public const string DefaultMediaCleanupSchedule = "Disabled";
    public const int DefaultRecordingRetentionDays = 30;
    public const int DefaultSnapshotRetentionDays = 7;
    public const int DefaultMaxRecordingDuration = 60;
    public const int DefaultThumbnailTileCount = 4;
    public const string DefaultDayFilter = "_ALL_";
    public const string DefaultTimeFilter = "_ALL_";
    public const string DefaultTimelapseInterval = "5m";
    public const string DefaultMinFreeSpaceMb = "2048";

    public static int GetMaxResolutionFromQuality(string videoQuality)
        => videoQuality switch
        {
            "360p" => 360,
            "480p" => 480,
            "720p" => 720,
            "1080p" => 1080,
            "Low" => 480,
            "Medium" => 720,
            "High" => 1080,
            _ => 0,
        };

    public static (int Width, int Height) ParseAnalysisResolution(
        string? resolution)
    {
        if (string.IsNullOrEmpty(resolution))
        {
            return (320, 240);
        }

        var parts = resolution.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return (width, height);
        }

        return (320, 240);
    }

    public static string FormatAnalysisResolution(
        int width,
        int height)
        => $"{width}x{height}";

    public static TimeSpan ParseTimelapseInterval(string? interval)
        => interval switch
        {
            "10s" => TimeSpan.FromSeconds(10),
            "30s" => TimeSpan.FromSeconds(30),
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            "10m" => TimeSpan.FromMinutes(10),
            "30m" => TimeSpan.FromMinutes(30),
            "1h" => TimeSpan.FromHours(1),
            "3h" => TimeSpan.FromHours(3),
            "6h" => TimeSpan.FromHours(6),
            "12h" => TimeSpan.FromHours(12),
            "24h" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromMinutes(5),
        };
}