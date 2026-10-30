namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

using ApiRecording = global::VideoSurveillance.Generated.Recordings.Models.Recording;

/// <summary>
/// The camera filter feeds an atc:LabelComboBox, which takes a dictionary:
/// "all cameras" first, then each camera once, by name.
/// </summary>
public sealed class RecordingsViewModelCameraFilterTests
{
    private static readonly Uri ApiBaseUrl = new("http://server.test");

    [Fact]
    public void CameraFilters_Lists_All_Cameras_First_Then_Each_Camera_Once_By_Name()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden"), Build("attic"), Build("garden")],
        };

        sut.RefreshTexts();

        sut.CameraFilters.Keys.Should().Equal("_ALL_", "attic", "Garden");
        sut.CameraFilters["_ALL_"].Should().Be(Translations.AllCameras);
    }

    [Fact]
    public void CameraFilters_Is_Raised_And_Keeps_The_Selection_While_The_Camera_Is_Listed()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden")],
        };
        sut.RefreshTexts();
        sut.SelectedCameraFilter = "Garden";
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.RefreshTexts();

        changed.Should().Contain(nameof(RecordingsViewModel.CameraFilters));
        sut.SelectedCameraFilter.Should().Be("Garden");
    }

    private static RecordingEntryViewModel Build(string cameraName)
        => new(
            new ApiRecording(
                Id: Guid.NewGuid(),
                CameraId: Guid.NewGuid(),
                CameraName: cameraName,
                FilePath: $"{cameraName}/{cameraName}_20261012_101500_000.mkv",
                StartedAt: DateTimeOffset.UnixEpoch,
                Duration: string.Empty,
                FileSizeBytes: 0,
                HasThumbnail: false,
                InProgress: false,
                ThumbnailPath: null),
            ApiBaseUrl,
            mediaToken: null);
}