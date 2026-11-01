namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A recording someone started is still wanted after the server restarts.
/// </summary>
public sealed class RecordingIntentsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-intents-" + Guid.NewGuid().ToString("N"));

    public RecordingIntentsTests()
        => Directory.CreateDirectory(folder);

    private string FilePath
        => Path.Combine(folder, "recording-intents.json");

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    [Fact]
    public void A_Wanted_Camera_Is_Still_Wanted_After_A_Restart_Until_It_Is_Dropped()
    {
        var cameraId = Guid.NewGuid();
        CreateSut().Want(cameraId);

        var restarted = CreateSut();
        restarted.IsWanted(cameraId).Should().BeTrue();

        restarted.Drop(cameraId);
        CreateSut().IsWanted(cameraId).Should().BeFalse();
    }

    [Fact]
    public void Without_A_File_Nothing_Is_Wanted()
        => CreateSut().IsWanted(Guid.NewGuid()).Should().BeFalse();

    private RecordingIntents CreateSut()
        => new(FilePath, NullLogger<RecordingIntents>.Instance, lockedAttempts: 2);
}