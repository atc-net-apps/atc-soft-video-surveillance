namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// The availability history CameraWall's own log tells, for the days before CameraWall kept one
/// (<see cref="AvailabilityFolder"/>): each <c>Camera connection state changed: 'Name' - 'A' -&gt; 'B'</c>
/// line, and the app's <c>App starting</c> and <c>App closing</c>. Both of the log's line layouts are
/// read: with the zone (<c>2026-11-07 16:57:28.639 +01:00 [INF] [2] ...</c>) and without it, read in
/// the zone given.
/// </summary>
/// <remarks>
/// The log names a camera by its display name, so a camera renamed since is not found in it.
/// </remarks>
public static class CameraWallLogHistory
{
    private const string StateChanged = "Camera connection state changed: '";
    private const string AppStarting = "CameraWallApp: App starting";
    private const string AppClosing = "CameraWallApp: App closing";

    /// <summary>
    /// How many days before a span the log is read for the state at its start.
    /// </summary>
    public const int LookBackDays = 7;

    private static readonly Regex StateChangeLine = new(
        @"Camera connection state changed: '(?<name>.*)' - '(?<from>\w+)' -> '(?<to>\w+)'\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Maps a camera's new connection state, by name, to a change of whether it could be recorded:
    /// connected is online; failed (or an error, a USB camera unplugged) is offline; disconnected is
    /// let go of on purpose (another layout, by hand, the app closing); on the way (connecting) is nothing.
    /// </summary>
    /// <param name="state">The state's name, as CameraWall writes it.</param>
    /// <returns>The change, or <see langword="null"/>.</returns>
    public static AvailabilityChange? FromConnectionState(string? state)
        => state switch
        {
            "Connected" => AvailabilityChange.Online,
            "ConnectionFailed" or "Error" or "DeviceUnplugged" => AvailabilityChange.Offline,
            "Disconnected" => AvailabilityChange.Released,
            _ => null,
        };

    /// <summary>
    /// Reads one line of the log.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="zone">The zone of a time written without one.</param>
    /// <returns>
    /// The camera (null for the app's own start or stop) and the change, or <see langword="null"/>
    /// when the line tells none.
    /// </returns>
    public static (string? CameraName, AvailabilityEntry Entry)? ParseLine(
        string line,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (string.IsNullOrEmpty(line) || line.Length < 23)
        {
            return null;
        }

        string? camera;
        AvailabilityChange change;
        if (line.Contains(StateChanged, StringComparison.Ordinal))
        {
            var match = StateChangeLine.Match(line);
            if (!match.Success || FromConnectionState(match.Groups["to"].Value) is not { } mapped)
            {
                return null;
            }

            camera = match.Groups["name"].Value;
            change = mapped;
        }
        else if (line.Contains(AppStarting, StringComparison.Ordinal))
        {
            camera = null;
            change = AvailabilityChange.ServerStarted;
        }
        else if (line.Contains(AppClosing, StringComparison.Ordinal))
        {
            camera = null;
            change = AvailabilityChange.ServerStopped;
        }
        else
        {
            return null;
        }

        return ParseTime(line, zone) is { } time
            ? (camera, new AvailabilityEntry(time, change))
            : null;
    }

    /// <summary>
    /// Reads the log files that cover a span, and before it back to the app's last start (at most
    /// <see cref="LookBackDays"/> days), for every camera's state at the span's start.
    /// </summary>
    /// <param name="logFolder">The log folder: <c>camera-wall-yyyyMMdd.log</c>, and its rolled parts.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="zone">The zone of the file names and of a time written without one.</param>
    /// <param name="cache">The files already read; a file is read again only when it changed.</param>
    /// <returns>The app's starts and stops, and each camera's changes by its name, oldest first.</returns>
    public static LogAvailability Read(
        string? logFolder,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo zone,
        LogHistoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(cache);

        var app = new List<AvailabilityEntry>();
        var cameras = new Dictionary<string, List<AvailabilityEntry>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(logFolder) || to <= from)
        {
            return new LogAvailability([], new Dictionary<string, IReadOnlyList<AvailabilityEntry>>(StringComparer.OrdinalIgnoreCase));
        }

        // The span's days, then back day by day until the app's last start before the span: from
        // there on every camera's state is told. A week back at most (the log keeps one, or a month).
        var spanStart = TimeZoneInfo.ConvertTime(from, zone).Date;
        var last = TimeZoneInfo.ConvertTime(to, zone).Date;
        for (var day = spanStart; day <= last; day = day.AddDays(1))
        {
            Add(day);
        }

        for (var day = spanStart.AddDays(-1); day >= spanStart.AddDays(-LookBackDays) && !app.Any(e => e.Change == AvailabilityChange.ServerStarted && e.Time <= from); day = day.AddDays(-1))
        {
            Add(day);
        }

        return new LogAvailability(
            [.. app.OrderBy(e => e.Time)],
            cameras.ToDictionary(p => p.Key, p => (IReadOnlyList<AvailabilityEntry>)[.. p.Value.OrderBy(e => e.Time)], StringComparer.OrdinalIgnoreCase));

        void Add(DateTime day)
        {
            string[] files;
            try
            {
                files = Directory.Exists(logFolder)
                    ? Directory.GetFiles(logFolder, $"camera-wall-{day:yyyyMMdd}*.log")
                    : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files = [];
            }

            foreach (var file in files.Order(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var (camera, entry) in cache.Get(file, path => ReadFile(path, zone)))
                {
                    if (camera is null)
                    {
                        app.Add(entry);
                    }
                    else
                    {
                        if (!cameras.TryGetValue(camera, out var list))
                        {
                            list = [];
                            cameras[camera] = list;
                        }

                        list.Add(entry);
                    }
                }
            }
        }
    }

    private static List<(string? CameraName, AvailabilityEntry Entry)> ReadFile(
        string path,
        TimeZoneInfo zone)
    {
        var found = new List<(string? CameraName, AvailabilityEntry Entry)>();
        try
        {
            // The app writes on: read what is there, sharing the file.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                if (ParseLine(line, zone) is { } parsed)
                {
                    found.Add(parsed);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A log that cannot be read tells nothing: the gaps stay unexplained.
        }

        return found;
    }

    // "2026-11-07 16:57:28.639 +01:00 ..." or "2026-11-07 16:57:28.639 [INF] ...".
    private static DateTimeOffset? ParseTime(
        string line,
        TimeZoneInfo zone)
    {
        if (!DateTime.TryParseExact(line[..23], "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        if (line.Length >= 30 && line[23] == ' ' && line[24] is '+' or '-' &&
            TimeSpan.TryParseExact(line[25..30], @"hh\:mm", CultureInfo.InvariantCulture, out var offset))
        {
            return new DateTimeOffset(local, line[24] == '-' ? -offset : offset);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}