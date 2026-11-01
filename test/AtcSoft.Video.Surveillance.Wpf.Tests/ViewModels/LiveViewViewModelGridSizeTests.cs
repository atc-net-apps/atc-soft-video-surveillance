namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

using AtcSoft.Video.Surveillance.Models.Settings;
using AtcSoft.Video.Surveillance.Services;

/// <summary>
/// The grid size is picked with segments: a segment's place is the size
/// (0 automatic, then one to four cameras along a side), remembered for the next start.
/// </summary>
public sealed class LiveViewViewModelGridSizeTests
{
    [Fact]
    public void The_Grid_Starts_In_The_Remembered_Size_And_A_Picked_Segment_Is_Remembered()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.General.Returns(new GeneralSettings { GridSize = 2 });
        using var eventService = new SurveillanceEventService(
            Substitute.For<IListEventsEndpoint>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SurveillanceEventService>.Instance,
            new SurveillanceEventServiceOptions());
        var sut = new LiveViewViewModel(
            server.Gateway,
            eventService,
            Substitute.For<IVideoPlayerFactory>(),
            Substitute.For<IUsbCameraWatcher>(),
            "http://server.test",
            settings);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.SelectedGridPresetIndex.Should().Be(2);

        sut.SelectedGridPresetIndex = 4;

        sut.SelectedGridPreset.Should().Be("4");
        changed.Should().Contain(nameof(LiveViewViewModel.SelectedGridPresetIndex));
        settings.Received(1).SaveGeneral(Arg.Is<GeneralSettings>(g => g.GridSize == 4));
    }

    [Fact]
    public void No_Segment_Picked_Keeps_The_Size()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        using var eventService = new SurveillanceEventService(
            Substitute.For<IListEventsEndpoint>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SurveillanceEventService>.Instance,
            new SurveillanceEventServiceOptions());
        var sut = new LiveViewViewModel(
            server.Gateway,
            eventService,
            Substitute.For<IVideoPlayerFactory>(),
            Substitute.For<IUsbCameraWatcher>(),
            "http://server.test")
        {
            SelectedGridPresetIndex = 3,
        };

        sut.SelectedGridPresetIndex = -1;

        sut.SelectedGridPreset.Should().Be("3");
    }
}