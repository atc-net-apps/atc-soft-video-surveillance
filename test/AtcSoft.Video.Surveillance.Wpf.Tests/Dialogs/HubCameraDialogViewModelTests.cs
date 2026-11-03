namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// A camera of a camera hub, added through the server: the hub's cameras listed, why one cannot
/// be added, and the one picked added.
/// </summary>
public sealed class HubCameraDialogViewModelTests
{
    private static readonly HubCameraInfo Garage = new(Id: "1", Name: "Garage", StreamKind: HubCameraInfoStreamKind.Mjpeg, IsUsb: true, Problem: HubCameraInfoProblem.None, Resolution: "1280x720", FramesPerSecond: 5);

    private static readonly HubCameraInfo Off = new(Id: "4", Name: "Local only", StreamKind: HubCameraInfoStreamKind.Mjpeg, IsUsb: true, Problem: HubCameraInfoProblem.StreamingOff);

    [Fact]
    public async Task Connecting_Lists_The_Cameras_With_Why_One_Cannot_Be_Added()
    {
        var sut = new HubCameraDialogViewModel(_ => Task.FromResult(Listing(olderSignIn: true)), _ => Task.FromResult(new HubCameraAdded(Guid.NewGuid(), "Garage")))
        {
            Address = "pi-garage",
        };

        await sut.ConnectAsync();

        sut.Status.Should().Be("Cameras on the hub: 2");
        sut.Warning.Should().Contain("less secure");
        sut.Cameras.Select(c => c.CanAdd).Should().Equal(true, false);
        sut.Cameras[0].Details.Should().Be("1280x720, 5 fps, USB");
        sut.Cameras[1].Problem.Should().Contain("Video Streaming");
    }

    [Fact]
    public async Task Adding_A_Camera_Sends_The_Hub_And_Its_Id_And_Closes()
    {
        AddHubCameraRequest? sent = null;
        bool? closed = null;
        var sut = new HubCameraDialogViewModel(
            _ => Task.FromResult(Listing(olderSignIn: false)),
            request =>
            {
                sent = request;
                return Task.FromResult(new HubCameraAdded(Guid.NewGuid(), "Garage"));
            })
        {
            Address = " pi-garage ",
            Password = "secret",
        };
        sut.CloseRequested += (_, e) => closed = e.DialogResult;
        await sut.ConnectAsync();

        await sut.AddAsync(sut.Cameras[0]);

        sent.Should().Be(new AddHubCameraRequest(Address: "pi-garage", CameraId: "1", UserName: "admin", Password: "secret"));
        sut.AddedName.Should().Be("Garage");
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task What_The_Server_Refused_Is_Shown_In_Its_Words()
    {
        var sut = new HubCameraDialogViewModel(_ => throw new HttpRequestException("The hub refused the user name or password."), _ => throw new InvalidOperationException())
        {
            Address = "pi-garage",
        };

        await sut.ConnectAsync();

        sut.Problem.Should().Be("The hub refused the user name or password.");
        sut.Cameras.Should().BeEmpty();
    }

    private static HubProbeResult Listing(bool olderSignIn)
        => new(Outcome: HubProbeResultOutcome.Listed, Kind: "motionEye", Address: "http://pi-garage:8765/", OlderSignIn: olderSignIn, Cameras: [Garage, Off]);
}