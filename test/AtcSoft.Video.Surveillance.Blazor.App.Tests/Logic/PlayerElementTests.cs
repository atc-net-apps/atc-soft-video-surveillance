namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// A player the browser gave up on names its video element; the page finds the camera by it.
/// </summary>
public sealed class PlayerElementTests
{
    [Fact]
    public void A_Camera_Is_Found_By_Its_Video_Element()
    {
        var cameraId = Guid.NewGuid();

        PlayerElement.CameraOf(PlayerElement.IdFor(cameraId)).Should().Be(cameraId);
        PlayerElement.IdFor(cameraId).Should().Be($"video-{cameraId}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("video-")]
    [InlineData("audio-0b8e4f4e-9a8c-4c5e-9d55-0f6d9b1d2a10")]
    public void Another_Element_Is_No_Camera(string? elementId)
        => PlayerElement.CameraOf(elementId).Should().BeNull();
}