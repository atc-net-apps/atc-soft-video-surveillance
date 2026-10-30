namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The desktop client keeps a refresh token per server, encrypted for the
/// Windows user, so it is signed in again when it starts.
/// </summary>
public sealed class SessionStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-sessions-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                File.Delete(file);
            }

            Directory.Delete(folder);
        }
    }

    [Fact]
    public void A_Kept_Session_Is_Read_Back_For_Its_Server_Only()
    {
        var sut = new SessionStore(folder);

        sut.Save(new Uri("http://server-a:5000"), "refresh-a", "admin");

        sut.Load(new Uri("http://SERVER-A:5000/"))!.Value.RefreshToken.Should().Be("refresh-a");
        sut.Load(new Uri("http://server-b:5000")).Should().BeNull();
    }

    [Fact]
    public void The_Token_Is_Not_Kept_As_It_Is()
    {
        var sut = new SessionStore(folder);

        sut.Save(new Uri("http://server-a:5000"), "refresh-a-secret", "admin");

        Directory.GetFiles(folder).Select(File.ReadAllBytes)
            .Should().OnlyContain(bytes => !System.Text.Encoding.UTF8.GetString(bytes).Contains("refresh-a-secret", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Cleared_Session_Is_Gone()
    {
        var sut = new SessionStore(folder);
        sut.Save(new Uri("http://server-a:5000"), "refresh-a", "admin");

        sut.Clear(new Uri("http://server-a:5000"));

        sut.Load(new Uri("http://server-a:5000")).Should().BeNull();
    }

    [Fact]
    public void A_Refused_Token_Is_Forgotten_Only_While_It_Is_Still_Kept()
    {
        var sut = new SessionStore(folder);
        var server = new Uri("http://server-a:5000");
        sut.Save(server, "newer", "admin");

        sut.ClearIf(server, "older");
        sut.Load(server)!.Value.RefreshToken.Should().Be("newer", "another instance kept a newer session");

        sut.ClearIf(server, "newer");
        sut.Load(server).Should().BeNull();
    }
}