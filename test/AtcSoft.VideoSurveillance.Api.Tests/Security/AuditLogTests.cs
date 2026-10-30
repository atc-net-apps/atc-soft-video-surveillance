namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// The audit log keeps the changes in a file in the data folder, newest first
/// when read, across a restart, and starts a new file when one grows large.
/// </summary>
public sealed class AuditLogTests : IDisposable
{
    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-audit-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));

    public AuditLogTests()
        => Directory.CreateDirectory(dataPath);

    public void Dispose()
    {
        foreach (var file in Directory.GetFiles(dataPath))
        {
            File.Delete(file);
        }

        Directory.Delete(dataPath);
    }

    [Fact]
    public void The_Latest_Change_Comes_First()
    {
        var sut = CreateSut();

        sut.Record("admin", "createCamera", "/cameras");
        time.Advance(TimeSpan.FromMinutes(1));
        sut.Record("ole", "updateSettings", "/settings");

        sut.Recent(10).Select(r => (r.UserName, r.Action, r.Target)).Should().Equal(
            ("ole", "updateSettings", "/settings"),
            ("admin", "createCamera", "/cameras"));
        sut.Recent(1).Should().ContainSingle().Which.Timestamp.Should().Be(time.GetUtcNow());
    }

    [Fact]
    public void The_Changes_Are_Kept_Across_A_Restart()
    {
        CreateSut().Record("admin", "deleteUser", "/users/1");

        CreateSut().Recent(10).Should().ContainSingle().Which.Action.Should().Be("deleteUser");
    }

    [Fact]
    public void A_Large_File_Is_Moved_Aside_And_A_New_One_Started()
    {
        var sut = new AuditLog(Path.Combine(dataPath, "audit.log"), time, maxFileBytes: 400);

        for (var i = 0; i < 10; i++)
        {
            sut.Record("admin", "updateCamera", $"/cameras/{i}");
        }

        new FileInfo(Path.Combine(dataPath, "audit.log")).Length.Should().BeLessThan(400);
        File.Exists(Path.Combine(dataPath, "audit.log.1")).Should().BeTrue();
        sut.Recent(3).Select(r => r.Target).Should().Equal("/cameras/9", "/cameras/8", "/cameras/7");
    }

    private AuditLog CreateSut()
        => new(Path.Combine(dataPath, "audit.log"), time);
}