namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// A client names the recording to delete by the path the listing gave it.
/// That path comes from outside, so it may only ever lead to a recording
/// below the recording root, and never to one that is still being made.
/// </summary>
public sealed class RecordingRemoverTests : IDisposable
{
    private static readonly HashSet<string> NoneActive = new(StringComparer.OrdinalIgnoreCase);

    private readonly string outer = Path.Combine(Path.GetTempPath(), "atcsoft-remover-" + Guid.NewGuid().ToString("N"));
    private readonly string root;

    public RecordingRemoverTests()
    {
        root = Path.Combine(outer, "recordings");
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(outer))
        {
            Directory.Delete(outer, recursive: true);
        }
    }

    [Fact]
    public void A_Recording_Is_Removed_With_Its_Thumbnail_Wherever_That_Is()
    {
        var video = Write("Front Door", "2026-10-12", "Front Door_20261012_101500_000.mkv");
        var besideIt = Write("Front Door", "2026-10-12", "Front Door_20261012_101500_000.png");
        var separate = Write("thumbnails", "Front Door", "2026-10-12", "Front Door_20261012_101500_000.png");
        var other = Write("Front Door", "2026-10-12", "Front Door_20261012_111500_000.mkv");

        var outcome = RecordingRemover.Remove(root, "Front Door/2026-10-12/Front Door_20261012_101500_000.mkv", NoneActive);

        outcome.Should().Be(RecordingRemovalOutcome.Removed);
        File.Exists(video).Should().BeFalse();
        File.Exists(besideIt).Should().BeFalse();
        File.Exists(separate).Should().BeFalse();
        File.Exists(other).Should().BeTrue("only the recording that was named goes");
    }

    [Fact]
    public void A_Recording_Is_Removed_With_Its_Activity_Index()
    {
        Write("Front Door", "Front Door_20261012_101500_000.mkv");
        var activity = Write("Front Door", "Front Door_20261012_101500_000.activity.jsonl");

        RecordingRemover.Remove(root, "Front Door/Front Door_20261012_101500_000.mkv", NoneActive)
            .Should().Be(RecordingRemovalOutcome.Removed);

        File.Exists(activity).Should().BeFalse();
    }

    [Fact]
    public void A_Path_With_Backslashes_Names_The_Same_Recording()
    {
        var video = Write("Garden", "Garden_20261012_101500_000.mp4");

        RecordingRemover.Remove(root, @"Garden\Garden_20261012_101500_000.mp4", NoneActive)
            .Should().Be(RecordingRemovalOutcome.Removed);

        File.Exists(video).Should().BeFalse();
    }

    [Fact]
    public void A_Recording_That_Is_Not_There_Is_Not_Found()
        => RecordingRemover.Remove(root, "Garden/Garden_20261012_101500_000.mp4", NoneActive)
            .Should().Be(RecordingRemovalOutcome.NotFound);

    [Theory]
    [InlineData("../secret/Cam_20261012_101500_000.mkv")]
    [InlineData("Garden/../../secret/Cam_20261012_101500_000.mkv")]
    [InlineData("Garden/..\\..\\secret\\Cam_20261012_101500_000.mkv")]
    public void A_Path_That_Leads_Out_Of_The_Recording_Folder_Deletes_Nothing(
        string relativePath)
    {
        var outside = Path.Combine(outer, "secret", "Cam_20261012_101500_000.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        File.WriteAllText(outside, "x");

        RecordingRemover.Remove(root, relativePath, NoneActive)
            .Should().Be(RecordingRemovalOutcome.NotARecording);

        File.Exists(outside).Should().BeTrue();
    }

    [Fact]
    public void A_Full_Path_Deletes_Nothing_Even_When_It_Points_Into_The_Recording_Folder()
    {
        var video = Write("Garden", "Garden_20261012_101500_000.mp4");

        RecordingRemover.Remove(root, video, NoneActive).Should().Be(RecordingRemovalOutcome.NotARecording);

        File.Exists(video).Should().BeTrue();
    }

    [Theory]
    [InlineData("Garden/notes.txt")]
    [InlineData("Garden/Garden_20261012_101500_000.png")]
    [InlineData("settings.json")]
    [InlineData("Garden_20261012_101500_000.mkv")]
    [InlineData("thumbnails/Garden/Garden_20261012_101500_000.mkv")]
    [InlineData("")]
    [InlineData("   ")]
    public void Only_A_Video_In_A_Camera_Folder_Is_A_Recording(
        string relativePath)
    {
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            Write(relativePath.Split('/'));
        }

        RecordingRemover.Remove(root, relativePath, NoneActive).Should().Be(RecordingRemovalOutcome.NotARecording);

        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            File.Exists(Path.Combine([root, .. relativePath.Split('/')])).Should().BeTrue();
        }
    }

    [Fact]
    public void A_Recording_That_Is_Known_To_Be_Running_Is_Left_Alone()
    {
        var video = Write("Garden", "Garden_20261012_101500_000.mp4");
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { video };

        RecordingRemover.Remove(root, "Garden/Garden_20261012_101500_000.mp4", active)
            .Should().Be(RecordingRemovalOutcome.InProgress);

        File.Exists(video).Should().BeTrue();
    }

    [Fact]
    public void A_Recording_Something_Else_Is_Still_Writing_Is_Left_Alone()
    {
        // Another program records into the same folder: nobody told us.
        var video = Path.Combine(root, "Garden", "Garden_20261012_101500_000.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        using var writer = new FileStream(video, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);

        RecordingRemover.Remove(root, "Garden/Garden_20261012_101500_000.mp4", NoneActive)
            .Should().Be(RecordingRemovalOutcome.InProgress);

        File.Exists(video).Should().BeTrue();
    }

    [Fact]
    public void Without_A_Recording_Folder_Nothing_Is_A_Recording()
        => RecordingRemover.Remove(string.Empty, "Garden/Garden_20261012_101500_000.mp4", NoneActive)
            .Should().Be(RecordingRemovalOutcome.NotARecording);

    private string Write(params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }
}