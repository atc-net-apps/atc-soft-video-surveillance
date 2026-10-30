namespace AtcSoft.VideoSurveillance.Wpf.Core.Factories;

/// <summary>
/// Factory for creating common dropdown/combobox items used across dialogs.
/// Centralizes definitions to ensure consistency and simplify maintenance.
/// </summary>
public static class DropDownItemsFactory
{
    /// <summary>
    /// Gets the video quality options for dropdowns.
    /// Keys are the stored values, values are display text.
    /// </summary>
    public static IDictionary<string, string> VideoQualityItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Auto"] = Translations.VideoQualityAuto,
            ["1080p"] = "1080p",
            ["720p"] = "720p",
            ["480p"] = "480p",
            ["360p"] = "360p",
        };

    /// <summary>
    /// Gets the RTSP transport protocol options for dropdowns.
    /// </summary>
    public static IDictionary<string, string> RtspTransportItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tcp"] = "TCP",
        ["udp"] = "UDP",
    };

    /// <summary>
    /// Gets the recording format options for dropdowns.
    /// Note: AVI removed as it doesn't properly support H.264/H.265 codecs used by modern IP cameras.
    /// </summary>
    public static IDictionary<string, string> RecordingFormatItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mp4"] = Translations.RecordingFormatMp4Video,
            ["mkv"] = Translations.RecordingFormatMkv,
        };

    /// <summary>
    /// Gets the overlay position options with friendly display names.
    /// </summary>
    public static IDictionary<string, string> OverlayPositionItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TopLeft"] = Translations.OverlayPositionTopLeft,
            ["TopRight"] = Translations.OverlayPositionTopRight,
            ["BottomLeft"] = Translations.OverlayPositionBottomLeft,
            ["BottomRight"] = Translations.OverlayPositionBottomRight,
        };

    /// <summary>
    /// Gets the connection types a user can pick for a network camera. USB is
    /// not offered: it is implied by the camera source.
    /// </summary>
    public static IDictionary<string, string> ConnectionTypeItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CameraConnectionType.None)] = Translations.ConnectionTypeNone,
            [nameof(CameraConnectionType.Lan)] = Translations.ConnectionTypeLan,
            [nameof(CameraConnectionType.Wifi)] = Translations.ConnectionTypeWifi,
        };

    /// <summary>
    /// Gets the connection-type icon placements, labelled with the concrete
    /// corner each resolves to for the given info-box position.
    /// </summary>
    public static IDictionary<string, string> GetConnectionTypeIconPlacementItems(
        OverlayPosition infoBoxPosition)
    {
        var cornerNames = OverlayPositionItems;
        var sameRow = CameraConnectionTypeHelper.ResolveIconCorner(infoBoxPosition, ConnectionTypeIconPlacement.SameRow);
        var sameColumn = CameraConnectionTypeHelper.ResolveIconCorner(infoBoxPosition, ConnectionTypeIconPlacement.SameColumn);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(ConnectionTypeIconPlacement.SameRow)] = string.Format(
                CultureInfo.CurrentCulture,
                Translations.ConnectionTypeIconPositionSameRowFormat1,
                cornerNames[sameRow.ToString()]),
            [nameof(ConnectionTypeIconPlacement.SameColumn)] = string.Format(
                CultureInfo.CurrentCulture,
                Translations.ConnectionTypeIconPositionSameColumnFormat1,
                cornerNames[sameColumn.ToString()]),
        };
    }

    /// <summary>
    /// Gets the camera rotation options. Keys match
    /// <see cref="CameraRotation"/> member names so they round-trip via
    /// <c>Enum.Parse</c> in <c>LabelComboBox.SelectedKey</c> bindings.
    /// </summary>
    public static IDictionary<string, string> RotationItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["None"] = Translations.RotationNone,
            ["Rotate90"] = Translations.RotationRotate90Degrees,
            ["Rotate180"] = Translations.RotationRotate180Degrees,
            ["Rotate270"] = Translations.RotationRotate270Degrees,
        };

    /// <summary>
    /// Gets the overlay opacity options for dropdowns.
    /// Keys are decimal values as strings, values are percentage display text.
    /// </summary>
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

    /// <summary>
    /// Gets the camera protocol options for dropdowns.
    /// </summary>
    public static IDictionary<string, string> ProtocolItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Rtsp"] = "RTSP",
        ["Http"] = "HTTP",
    };

    /// <summary>
    /// Gets the motion sensitivity options for dropdowns.
    /// Keys are sensitivity values (0-100), values are display text.
    /// </summary>
    public static IDictionary<string, string> MotionSensitivityItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["10"] = Translations.MotionSensitivityLow,
            ["30"] = Translations.MotionSensitivityMedium,
            ["50"] = Translations.MotionSensitivityHigh,
            ["70"] = Translations.MotionSensitivityVeryHigh,
        };

    /// <summary>
    /// Gets the bounding box thickness options for dropdowns.
    /// Keys are thickness in pixels, values are display text.
    /// </summary>
    public static IDictionary<string, string> BoundingBoxThicknessItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1"] = "1 px",
        ["2"] = "2 px",
        ["3"] = "3 px",
        ["4"] = "4 px",
        ["5"] = "5 px",
    };

    /// <summary>
    /// Gets the bounding box minimum area options for dropdowns.
    /// Keys are area in pixels (at 320x240 analysis resolution), values are display text.
    /// Lower values detect smaller/distant objects but may increase false positives.
    /// </summary>
    public static IDictionary<string, string> BoundingBoxMinAreaItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["10"] = Translations.BoundingBoxMinAreaTiny,
            ["25"] = Translations.BoundingBoxMinAreaSmall,
            ["50"] = Translations.BoundingBoxMinAreaMedium,
            ["100"] = Translations.BoundingBoxMinAreaLarge,
            ["200"] = Translations.BoundingBoxMinAreaVeryLarge,
        };

    /// <summary>
    /// Gets the motion analysis size options for dropdowns. Keys are the longer side of the
    /// analysed picture in pixels (the other side follows the picture's shape), values are display
    /// text with performance hints. Larger sizes find smaller objects but use more CPU.
    /// </summary>
    public static IDictionary<string, string> MotionAnalysisResolutionItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["320"] = Translations.MotionResolutionLowCpu,
            ["480"] = Translations.MotionResolutionBalanced,
            ["640"] = Translations.MotionResolutionBetterDetection,
            ["800"] = Translations.MotionResolutionHighCpu,
            ["960"] = Translations.MotionResolutionVeryHighCpu,
        };

    /// <summary>
    /// Gets the motion analysis size options with a size that is not among them (set in an
    /// older version or by hand) added, so it stays shown.
    /// </summary>
    /// <param name="current">The key shown now.</param>
    public static IDictionary<string, string> MotionAnalysisResolutionItemsWith(
        string? current)
    {
        var items = MotionAnalysisResolutionItems;
        if (!string.IsNullOrEmpty(current) && !items.ContainsKey(current))
        {
            items[current] = string.Format(CultureInfo.CurrentCulture, Translations.Pixels1, current);
        }

        return items;
    }

    /// <summary>
    /// Gets the post-motion duration options for dropdowns (in seconds).
    /// </summary>
    public static IDictionary<string, string> PostMotionDurationItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["5"] = Translations.Duration5Seconds,
            ["10"] = Translations.Duration10Seconds,
            ["15"] = Translations.Duration15Seconds,
            ["30"] = Translations.Duration30Seconds,
            ["60"] = Translations.Duration1Minute,
        };

    /// <summary>
    /// Gets the media cleanup schedule options for dropdowns.
    /// </summary>
    public static IDictionary<string, string> MediaCleanupScheduleItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Disabled"] = Translations.CleanupScheduleDisabled,
            ["OnStartup"] = Translations.CleanupScheduleOnStartup,
            ["OnStartupAndPeriodically"] = Translations.CleanupScheduleOnStartupAndPeriodically,
        };

    /// <summary>
    /// Gets the media retention period options for dropdowns (in days).
    /// </summary>
    public static IDictionary<string, string> MediaRetentionPeriodItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["7"] = Translations.RetentionPeriod7Days,
            ["14"] = Translations.RetentionPeriod14Days,
            ["30"] = Translations.RetentionPeriod30Days,
            ["60"] = Translations.RetentionPeriod60Days,
            ["90"] = Translations.RetentionPeriod90Days,
            ["180"] = Translations.RetentionPeriod6Months,
            ["365"] = Translations.RetentionPeriod1Year,
        };

    /// <summary>
    /// Gets the maximum recording duration options for dropdowns (in minutes).
    /// </summary>
    public static IDictionary<string, string> MaxRecordingDurationItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["15"] = Translations.Duration15Minutes,
            ["30"] = Translations.Duration30Minutes,
            ["60"] = Translations.Duration1Hour,
            ["120"] = Translations.Duration2Hours,
            ["180"] = Translations.Duration3Hours,
            ["240"] = Translations.Duration4Hours,
        };

    /// <summary>
    /// Gets the thumbnail tile count options for dropdowns.
    /// 1 = single image (320x240), 4 = 2x2 grid (640x480).
    /// </summary>
    public static IDictionary<string, string> ThumbnailTileCountItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["1"] = Translations.ThumbnailTileOne,
            ["4"] = Translations.ThumbnailTileFour,
        };

    /// <summary>
    /// Gets the minimum free disk space options for dropdowns (in megabytes).
    /// Keys are MB values as strings, values are display text.
    /// </summary>
    public static IDictionary<string, string> MinFreeSpaceItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["512"] = "500 MB",
        ["1024"] = "1 GB",
        ["2048"] = "2 GB (Recommended)",
        ["5120"] = "5 GB",
        ["10240"] = "10 GB",
        ["20480"] = "20 GB",
    };

    /// <summary>
    /// Gets the most space all recordings may take, in gigabytes; "0" is no limit.
    /// </summary>
    public static IDictionary<string, string> MaxRecordingStorageItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["0"] = Translations.NoLimit,
        ["50"] = "50 GB",
        ["100"] = "100 GB",
        ["250"] = "250 GB",
        ["500"] = "500 GB",
        ["1000"] = "1 TB",
        ["2000"] = "2 TB",
        ["4000"] = "4 TB",
    };

    /// <summary>
    /// Gets the timelapse capture interval options for dropdowns.
    /// Keys are interval codes (e.g., "10s", "1m"), values are display text.
    /// </summary>
    public static IDictionary<string, string> TimelapseIntervalItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["10s"] = Translations.Duration10Seconds,
            ["30s"] = Translations.Duration30Seconds,
            ["1m"] = Translations.Duration1Minute,
            ["5m"] = Translations.Duration5Minutes,
            ["10m"] = Translations.Duration10Minutes,
            ["30m"] = Translations.Duration30Minutes,
            ["1h"] = Translations.Duration1Hour,
            ["3h"] = Translations.Duration3Hours,
            ["6h"] = Translations.Duration6Hours,
            ["12h"] = Translations.Duration12Hours,
            ["24h"] = Translations.Duration24Hours,
        };

    /// <summary>
    /// Gets the day filter options for recordings browser.
    /// Keys are filter identifiers, values are display text.
    /// </summary>
    public static IDictionary<string, string> DayFilterItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DefaultDayFilter] = Translations.FilterAll,
            ["TODAY"] = Translations.FilterToday,
            ["YESTERDAY"] = Translations.FilterYesterday,
            ["LAST7"] = Translations.FilterLast7Days,
            ["LAST30"] = Translations.FilterLast30Days,
            ["THISWEEK"] = Translations.FilterThisWeek,
            ["THISMONTH"] = Translations.FilterThisMonth,
        };

    /// <summary>
    /// Gets the time filter options for recordings browser.
    /// Keys are filter identifiers, values are display text with time ranges.
    /// </summary>
    public static IDictionary<string, string> TimeFilterItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DefaultTimeFilter] = Translations.FilterAll,
            ["0_6"] = Translations.FilterNight,
            ["6_12"] = Translations.FilterMorning,
            ["12_18"] = Translations.FilterAfternoon,
            ["18_24"] = Translations.FilterEvening,
        };

    /// <summary>
    /// Gets the default video quality value.
    /// </summary>
    public const string DefaultVideoQuality = "Auto";

    /// <summary>
    /// Gets the default RTSP transport protocol.
    /// </summary>
    public const string DefaultRtspTransport = "tcp";

    /// <summary>
    /// Gets the default recording format.
    /// </summary>
    public const string DefaultRecordingFormat = "mp4";

    /// <summary>
    /// Gets the default overlay position.
    /// </summary>
    public const string DefaultOverlayPosition = "TopLeft";

    /// <summary>
    /// Gets the default connection type.
    /// </summary>
    public const string DefaultConnectionType = nameof(CameraConnectionType.None);

    /// <summary>
    /// Gets the default connection-type icon placement.
    /// </summary>
    public const string DefaultConnectionTypeIconPlacement = nameof(ConnectionTypeIconPlacement.SameRow);

    /// <summary>
    /// Gets the default overlay opacity.
    /// </summary>
    public const string DefaultOverlayOpacity = "0.7";

    /// <summary>
    /// Gets the default camera protocol.
    /// </summary>
    public const string DefaultProtocol = "Rtsp";

    /// <summary>
    /// Gets the default motion sensitivity.
    /// </summary>
    public const int DefaultMotionSensitivity = 30;

    /// <summary>
    /// Gets the default post-motion duration in seconds.
    /// </summary>
    public const int DefaultPostMotionDuration = 10;

    /// <summary>
    /// Gets the default bounding box color.
    /// </summary>
    public const string DefaultBoundingBoxColor = "Red";

    /// <summary>
    /// Gets the default bounding box thickness.
    /// </summary>
    public const int DefaultBoundingBoxThickness = 2;

    /// <summary>
    /// Gets the default bounding box minimum area (at 320x240 analysis resolution).
    /// Lower values detect smaller/distant objects.
    /// </summary>
    public const int DefaultBoundingBoxMinArea = 25;

    /// <summary>
    /// Gets the default motion analysis size: the longer side, as in Core's settings.
    /// </summary>
    public const string DefaultMotionAnalysisResolution = "800";

    /// <summary>
    /// Gets the default media cleanup schedule.
    /// </summary>
    public const string DefaultMediaCleanupSchedule = "Disabled";

    /// <summary>
    /// Gets the default recording retention period in days.
    /// </summary>
    public const int DefaultRecordingRetentionDays = 30;

    /// <summary>
    /// Gets the default snapshot retention period in days.
    /// </summary>
    public const int DefaultSnapshotRetentionDays = 7;

    /// <summary>
    /// Gets the default maximum recording duration in minutes.
    /// </summary>
    public const int DefaultMaxRecordingDuration = 60;

    /// <summary>
    /// Gets the default thumbnail tile count (4 = 2x2 grid).
    /// </summary>
    public const int DefaultThumbnailTileCount = 4;

    /// <summary>
    /// Gets the default day filter value (all days).
    /// </summary>
    public const string DefaultDayFilter = "_ALL_";

    /// <summary>
    /// Gets the default time filter value (all times).
    /// </summary>
    public const string DefaultTimeFilter = "_ALL_";

    /// <summary>
    /// Gets the default timelapse capture interval.
    /// </summary>
    public const string DefaultTimelapseInterval = "5m";

    /// <summary>
    /// Gets the default minimum free disk space in MB (2 GB).
    /// </summary>
    public const string DefaultMinFreeSpaceMb = "2048";

    public const string DefaultMaxRecordingStorageGb = "0";

    /// <summary>
    /// Converts a video quality setting to the maximum vertical resolution in pixels.
    /// Returns 0 for Auto (no limit).
    /// </summary>
    /// <param name="videoQuality">The video quality setting (e.g., "Auto", "1080p", "720p").</param>
    /// <returns>The maximum vertical resolution in pixels, or 0 for no limit.</returns>
    public static int GetMaxResolutionFromQuality(string videoQuality)
        => videoQuality switch
        {
            // Resolution-based format
            "360p" => 360,
            "480p" => 480,
            "720p" => 720,
            "1080p" => 1080,

            // Legacy format (backward compatibility for existing camera overrides)
            "Low" => 480,
            "Medium" => 720,
            "High" => 1080,

            // Auto = no limit
            _ => 0,
        };

    /// <summary>
    /// Parses a motion analysis size key ("800") into the longer side; an older "800x600" key
    /// gives its width.
    /// </summary>
    /// <param name="resolution">The key.</param>
    /// <returns>The longer side in pixels, or the default when the key cannot be read.</returns>
    public static int ParseAnalysisResolution(string? resolution)
        => int.TryParse(resolution?.Split('x')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var side) && side > 0
            ? side
            : MotionAnalysisSize.DefaultLongerSide;

    /// <summary>
    /// Formats the longer side of the analysed picture as a motion analysis size key ("800").
    /// </summary>
    /// <param name="longerSide">The longer side in pixels; 0 or less gives the default.</param>
    /// <returns>The key.</returns>
    public static string FormatAnalysisResolution(int longerSide)
        => (longerSide > 0 ? longerSide : MotionAnalysisSize.DefaultLongerSide).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a timelapse interval string (e.g., "10s", "5m", "1h") into a TimeSpan.
    /// </summary>
    /// <param name="interval">The interval string.</param>
    /// <returns>The parsed TimeSpan, or 5 minutes if parsing fails.</returns>
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