namespace AtcSoft.Video.Surveillance.Api.Recordings;

/// <summary>
/// The one walk of the recording folder behind the listing and the timeline. One camera's
/// listing walks only that camera's folder; the separate thumbnails folder is never walked.
/// </summary>
public sealed class RecordingCatalogTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-catalog-" + Guid.NewGuid().ToString("N"));
    private readonly CameraConfiguration garden = CreateCamera("Garden");
    private readonly CameraConfiguration door = CreateCamera("Front Door");

    public RecordingCatalogTests()
    {
        Write("Garden", "2026-10-12", "Garden_20261012_101500_000.mp4");
        Write("Garden", "Garden_20261011_101500_000.mkv");
        Write("Front Door", "2026-10-12", "Front Door_20261012_101500_000.avi");
        Write("thumbnails", "Garden", "2026-10-12", "Garden_20261012_101500_000.png");
        Write("thumbnails", "Garden", "stray.mp4");
        Write("Garden", "2026-10-12", "Garden_20261012_101500_000.activity.jsonl");
        Write("Old Camera", "Old Camera_20250101_101500_000.mp4");
        Write("loose_20250101_101500_000.mp4");
    }

    public void Dispose()
        => Directory.Delete(root, recursive: true);

    [Fact]
    public void Every_Recording_Is_Found_With_Its_Camera_And_Nothing_From_The_Thumbnails_Folder()
    {
        var found = Find(cameraId: null);

        found.Select(f => f.RelativePath).Should().BeEquivalentTo(
            "Garden/2026-10-12/Garden_20261012_101500_000.mp4",
            "Garden/Garden_20261011_101500_000.mkv",
            "Front Door/2026-10-12/Front Door_20261012_101500_000.avi",
            "Old Camera/Old Camera_20250101_101500_000.mp4",
            "loose_20250101_101500_000.mp4");
        found.Single(f => f.RelativePath.StartsWith("Front Door", StringComparison.Ordinal)).CameraId.Should().Be(door.Id);
        found.Single(f => f.RelativePath.StartsWith("Old Camera", StringComparison.Ordinal)).CameraId.Should().BeEmpty("no camera has that folder any more");
        found.Should().OnlyContain(f => f.Length == 1, "the size comes from the walk");
    }

    [Fact]
    public void One_Cameras_Recordings_Are_Those_In_Its_Folder()
    {
        var found = Find(garden.Id);

        found.Select(f => f.RelativePath).Should().BeEquivalentTo(
            "Garden/2026-10-12/Garden_20261012_101500_000.mp4",
            "Garden/Garden_20261011_101500_000.mkv");
        found.Should().OnlyContain(f => f.CameraId == garden.Id && f.CameraName == "Garden");
    }

    [Fact]
    public void A_Camera_Without_A_Folder_Or_Unknown_Has_No_Recordings()
    {
        Find(Guid.NewGuid()).Should().BeEmpty();

        var newCamera = CreateCamera("Not Recorded Yet");
        RecordingCatalog.Find(root, [garden, door, newCamera], [], newCamera.Id, DateTimeOffset.UtcNow).Should().BeEmpty();
    }

    private static CameraConfiguration CreateCamera(string name)
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = name;
        return camera;
    }

    private IReadOnlyList<RecordingFile> Find(Guid? cameraId)
        => RecordingCatalog.Find(root, [garden, door], [], cameraId, DateTimeOffset.UtcNow);

    private void Write(params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0]);
    }
}