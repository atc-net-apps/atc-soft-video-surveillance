namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class CameraPasswordFieldTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void An_Empty_Password_Box_Keeps_The_Stored_Password(string? typed)
        => CameraPasswordField.ToSend(typed).Should().BeNull();

    [Fact]
    public void A_Typed_Password_Is_Sent()
        => CameraPasswordField.ToSend("secret").Should().Be("secret");
}