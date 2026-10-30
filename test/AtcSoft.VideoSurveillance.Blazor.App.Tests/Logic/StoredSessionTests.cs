namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The browser keeps the refresh token and the user's name between visits,
/// and reads past anything else that was kept.
/// </summary>
public sealed class StoredSessionTests
{
    [Fact]
    public void What_Is_Kept_Is_Read_Back()
    {
        var stored = StoredSession.Write("refresh-token", "admin");

        StoredSession.Read(stored).Should().Be(new StoredSession("refresh-token", "admin"));
        stored.Should().NotContain("accessToken");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"userName\":\"admin\"}")]
    public void Nothing_Or_Something_Else_Is_No_Session(string? stored)
        => StoredSession.Read(stored).Should().BeNull();
}