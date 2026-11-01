namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A settings file that cannot be read is never taken for a missing one: the server's
/// retention and recording folder are in it.
/// </summary>
public sealed class JsonApplicationSettingsServiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-settings-" + Guid.NewGuid().ToString("N"));

    public JsonApplicationSettingsServiceTests()
        => Directory.CreateDirectory(folder);

    private string FilePath
        => Path.Combine(folder, "settings.json");

    public void Dispose()
        => Directory.Delete(folder, recursive: true);

    [Fact]
    public void A_First_Start_Writes_The_Defaults()
    {
        var sut = CreateSut();

        sut.IsUnreadable.Should().BeFalse();
        File.Exists(FilePath).Should().BeTrue();
    }

    [Fact]
    public void A_Missing_File_With_A_Backup_Is_Read_From_The_Backup_Not_Replaced_By_Defaults()
    {
        var first = CreateSut();
        first.Recording.RecordingPath = Path.Combine(folder, "archive");
        first.Save();
        first.Save();
        File.Delete(FilePath);

        var sut = CreateSut();

        sut.Recording.RecordingPath.Should().Be(Path.Combine(folder, "archive"));
        sut.IsUnreadable.Should().BeFalse();
    }

    [Fact]
    public void A_Corrupt_File_Is_Reported_Unreadable_And_Kept_Aside()
    {
        File.WriteAllText(FilePath, "{ \"recording\": ");

        var sut = CreateSut();

        sut.IsUnreadable.Should().BeTrue("the cleanup must not delete by default retention");
        Directory.GetFiles(folder, "settings.json.unreadable-*").Should().ContainSingle();
    }

    private JsonApplicationSettingsService CreateSut()
        => new(FilePath, NullLogger<JsonApplicationSettingsService>.Instance, lockedAttempts: 2);
}