namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The camera hub dialog says how the listing went, why a camera cannot be added, and a camera's
/// size and frame rate.
/// </summary>
public sealed class HubCameraTextTests
{
    [Fact]
    public void A_Listed_Hub_Says_How_Many_Cameras()
        => HubCameraText.Status(Listing(HubProbeResultOutcome.Listed, Camera("1"), Camera("2"))).Should().Be("Cameras on the hub: 2");

    [Fact]
    public void A_Refused_Sign_In_Says_So()
        => HubCameraText.Status(Listing(HubProbeResultOutcome.SignInRefused)).Should().Be("The hub refused the user name or password.");

    [Fact]
    public void A_Camera_That_Can_Be_Added_Has_No_Reason()
        => HubCameraText.Problem(HubCameraInfoProblem.None).Should().BeNull();

    [Fact]
    public void A_Camera_Streamed_To_The_Hub_Only_Says_What_To_Switch_On()
        => HubCameraText.Problem(HubCameraInfoProblem.StreamingOff).Should().Contain("Video Streaming");

    [Fact]
    public void A_Cameras_Details_Are_Its_Size_Frame_Rate_And_Whether_It_Is_USB()
        => HubCameraText.Details(Camera("1")).Should().Be("1280x720, 5 fps, USB");

    [Fact]
    public void A_Picture_Is_Shown_From_Its_Bytes()
        => HubCameraText.PictureAddress([0xFF, 0xD8]).Should().Be("data:image/jpeg;base64,/9g=");

    private static HubCameraInfo Camera(string id)
        => new(Id: id, Name: "Garage", StreamKind: HubCameraInfoStreamKind.Mjpeg, IsUsb: true, Problem: HubCameraInfoProblem.None, Resolution: "1280x720", FramesPerSecond: 5);

    private static HubProbeResult Listing(
        HubProbeResultOutcome outcome,
        params HubCameraInfo[] cameras)
        => new(Outcome: outcome, Kind: "motionEye", Address: "http://pi-garage:8765/", OlderSignIn: false, Cameras: [.. cameras]);
}