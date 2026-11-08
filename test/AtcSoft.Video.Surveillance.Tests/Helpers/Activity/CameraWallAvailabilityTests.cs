namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// CameraWall's availability history: its own files, and before them what its log tells, in both
/// of the log's line layouts; how often a camera dropped out; another CameraWall's shared folder.
/// </summary>
public sealed class CameraWallAvailabilityTests : IDisposable
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    private static readonly Guid TapoId = Guid.Parse("abe20afa-e817-4902-96cd-ea515a794f72");

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-camerawall-availability-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_State_Change_In_The_Diagnostics_Layout_Is_Read_With_Its_Zone()
    {
        var parsed = CameraWallLogHistory.ParseLine(
            "2026-11-07 16:57:28.639 +01:00 [INF] [2] AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'Connected' -> 'ConnectionFailed'",
            TimeZoneInfo.Utc);

        parsed.Should().NotBeNull();
        parsed!.Value.CameraName.Should().Be("TAPO-C120-04");
        parsed.Value.Entry.Should().Be(new AvailabilityEntry(new DateTimeOffset(2026, 11, 7, 15, 57, 28, 639, TimeSpan.Zero), AvailabilityChange.Offline));
    }

    [Fact]
    public void A_State_Change_In_The_Plain_Layout_Is_Read_In_The_Zone_Given()
    {
        var parsed = CameraWallLogHistory.ParseLine(
            "2026-11-07 17:00:00.000 [INF] AtcSoft.Video.Surveillance.CameraWall.Wpf.Services.CameraWallManager: Camera connection state changed: 'It's mine' - 'Connecting' -> 'Connected'",
            Copenhagen);

        parsed!.Value.CameraName.Should().Be("It's mine");
        parsed.Value.Entry.Should().Be(new AvailabilityEntry(new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero), AvailabilityChange.Online));
    }

    [Theory]
    [InlineData("Connecting", null)]
    [InlineData("Reconnecting", null)]
    [InlineData("Connected", AvailabilityChange.Online)]
    [InlineData("ConnectionFailed", AvailabilityChange.Offline)]
    [InlineData("Disconnected", AvailabilityChange.Released)]
    public void A_State_Is_Online_Offline_Or_Nothing(
        string state,
        AvailabilityChange? expected)
        => CameraWallLogHistory.FromConnectionState(state).Should().Be(expected);

    [Fact]
    public void The_Apps_Start_And_Close_Are_Read_And_Other_Lines_Are_Not()
    {
        CameraWallLogHistory.ParseLine("2026-11-05 22:13:00.787 +01:00 [INF] [2] AtcSoft.Video.Surveillance.CameraWall.Wpf.App.CameraWallApp: App starting", Copenhagen)!
            .Value.Entry.Change.Should().Be(AvailabilityChange.ServerStarted);
        CameraWallLogHistory.ParseLine("2026-11-05 23:13:00.787 +01:00 [INF] [2] AtcSoft.Video.Surveillance.CameraWall.Wpf.App.CameraWallApp: App closing", Copenhagen)!
            .Value.Entry.Change.Should().Be(AvailabilityChange.ServerStopped);
        CameraWallLogHistory.ParseLine("2026-11-07 16:57:28.894 +01:00 [INF] [2] X: Auto-reconnect scheduled for camera 'TAPO-C120-04' in 10s (attempt 1)", Copenhagen)
            .Should().BeNull();
        CameraWallLogHistory.ParseLine("not a log line at all, but long enough", Copenhagen).Should().BeNull();
    }

    [Fact]
    public void The_Log_Explains_The_Gaps_Before_The_Apps_Own_History_Began()
    {
        var logs = Directory.CreateDirectory(Path.Combine(root, "logs")).FullName;
        File.WriteAllLines(Path.Combine(logs, "camera-wall-20261107.log"), [
            "2026-11-07 08:00:00.000 +01:00 [INF] [2] A.CameraWallApp: App starting",
            "2026-11-07 08:00:05.000 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'Connecting' -> 'Connected'",
            "2026-11-07 16:57:28.639 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'Connected' -> 'ConnectionFailed'",
            "2026-11-07 16:57:38.890 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'ConnectionFailed' -> 'Connecting'",
            "2026-11-07 16:57:48.927 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'Connecting' -> 'ConnectionFailed'",
            "2026-11-07 17:30:00.000 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'TAPO-C120-04' - 'Connecting' -> 'Connected'",
        ]);
        var availability = Directory.CreateDirectory(Path.Combine(root, AvailabilityFolder.FolderName)).FullName;
        var kept = new DateTimeOffset(2026, 11, 7, 20, 0, 0, TimeSpan.Zero);
        AvailabilityLog.Append(Path.Combine(availability, AvailabilityFolder.AppFileName), new AvailabilityEntry(kept, AvailabilityChange.ServerStarted));
        AvailabilityLog.Append(AvailabilityFolder.CameraFile(availability, TapoId), new AvailabilityEntry(kept.AddMinutes(1), AvailabilityChange.Offline));

        var (from, to) = (new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero));
        var history = CameraWallAvailability.Read([new TimelineCamera(TapoId, "tapo-c120-04")], availability, logs, from, to, Copenhagen, new LogHistoryCache())[TapoId];

        history.Select(e => e.Change).Should().Equal(
            AvailabilityChange.ServerStarted,
            AvailabilityChange.Online,
            AvailabilityChange.Offline,
            AvailabilityChange.Offline,
            AvailabilityChange.Online,
            AvailabilityChange.ServerStarted,
            AvailabilityChange.Offline);

        var gaps = TimelineGaps.Find([], from, to, history);
        gaps.Should().Contain(new TimelineGap(new DateTimeOffset(2026, 11, 7, 15, 57, 28, 639, TimeSpan.Zero), new DateTimeOffset(2026, 11, 7, 16, 30, 0, TimeSpan.Zero), GapReason.Offline));

        // Failing to reconnect is one drop; the drop after the app's own history began is another.
        var (drops, offline) = OfflineSummary.Of(history, gaps, from, to);
        drops.Should().Be(2);
        offline.Should().BeGreaterThan(TimeSpan.FromMinutes(32));
    }

    [Fact]
    public void A_Camera_Let_Go_As_The_App_Closes_Is_Not_A_Drop()
    {
        var nine = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
        AvailabilityEntry[] history =
        [
            new(nine, AvailabilityChange.ServerStarted),
            new(nine.AddMinutes(1), AvailabilityChange.Online),
            new(nine.AddHours(1), AvailabilityChange.ServerStopped),
            new(nine.AddHours(1).AddSeconds(1), AvailabilityChange.Offline),
        ];

        var gaps = TimelineGaps.Find([], nine, nine.AddHours(2), history);

        OfflineSummary.Of(history, gaps, nine, nine.AddHours(2)).Drops.Should().Be(0);
        gaps[^1].Reason.Should().Be(GapReason.ServerDown);
    }

    [Fact]
    public void A_Camera_Taken_Off_The_Layout_Is_Not_Offline()
    {
        var nine = new DateTimeOffset(2026, 11, 5, 21, 13, 0, TimeSpan.Zero);
        AvailabilityEntry[] history =
        [
            new(nine, AvailabilityChange.ServerStarted),
            new(nine.AddSeconds(11), AvailabilityChange.Online),
            CameraWallLogHistory.ParseLine("2026-11-05 22:16:10.000 +01:00 [INF] [2] A.CameraWallManager: Camera connection state changed: 'Have' - 'Connected' -> 'Disconnected'", Copenhagen)!.Value.Entry,
        ];

        var gaps = TimelineGaps.Find([], nine, nine.AddHours(1), history);

        gaps[^1].Reason.Should().Be(GapReason.NotRecording);
        OfflineSummary.Of(history, gaps, nine, nine.AddHours(1)).Should().Be((0, TimeSpan.Zero));
    }

    [Fact]
    public void An_App_That_Stopped_Without_Saying_So_Stopped_When_It_Was_Last_Alive()
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, AvailabilityFolder.FolderName)).FullName;
        var started = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
        AvailabilityLog.Append(Path.Combine(folder, AvailabilityFolder.AppFileName), new AvailabilityEntry(started, AvailabilityChange.ServerStarted));
        AvailabilityFolder.FindUnannouncedStop(folder).Should().Be(started);

        File.WriteAllText(Path.Combine(folder, AvailabilityFolder.AliveFileName), started.AddHours(3).ToString("O", CultureInfo.InvariantCulture));
        AvailabilityFolder.FindUnannouncedStop(folder).Should().Be(started.AddHours(3));

        AvailabilityLog.Append(Path.Combine(folder, AvailabilityFolder.AppFileName), new AvailabilityEntry(started.AddHours(4), AvailabilityChange.ServerStopped));
        AvailabilityFolder.FindUnannouncedStop(folder).Should().BeNull();
    }

    [Fact]
    public void A_Shared_Data_Folder_Gives_Its_Cameras_Recordings_History_And_Logs()
    {
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(root).FullName, "cameras.json"), $$"""
            { "cameras": [
              { "id": "{{TapoId}}", "authentication": { "password": "dpapi:v1:not-for-this-pc" }, "display": { "displayName": "TAPO-C120-04" } },
              { "id": "not a guid", "display": { "displayName": "Broken" } }
            ] }
            """);
        Directory.CreateDirectory(Path.Combine(root, "recordings", "TAPO-C120-04"));
        Directory.CreateDirectory(Path.Combine(root, "recordings", "Old name"));
        Directory.CreateDirectory(Path.Combine(root, "recordings", "thumbnails"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));

        var source = TimelineFolders.Open(root + Path.DirectorySeparatorChar);
        var fromRecordings = TimelineFolders.Open(Path.Combine(root, "recordings"));

        source.Should().NotBeNull();
        source!.Cameras.Select(c => c.Name).Should().Equal("TAPO-C120-04", "Old name");
        source.Cameras[0].Id.Should().Be(TapoId);
        source.RecordingsFolder.Should().Be(Path.Combine(root, "recordings"));
        source.LogFolder.Should().Be(Path.Combine(root, "logs"));
        source.AvailabilityFolder.Should().BeNull();
        fromRecordings!.Root.Should().Be(root);
        fromRecordings.Cameras.Should().Equal(source.Cameras);
        TimelineFolders.Open(Path.Combine(root, "missing")).Should().BeNull();
    }
}