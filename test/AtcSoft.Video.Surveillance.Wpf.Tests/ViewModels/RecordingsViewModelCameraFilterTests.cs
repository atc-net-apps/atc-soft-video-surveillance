namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

using ApiRecording = global::VideoSurveillance.Generated.Recordings.Models.Recording;

/// <summary>
/// The camera filter feeds an atc:LabelMultiSelectComboBox, which takes a dictionary:
/// each camera once, by name. No camera ticked lists every camera.
/// </summary>
public sealed class RecordingsViewModelCameraFilterTests
{
    private static readonly Uri ApiBaseUrl = new("http://server.test");

    [Fact]
    public void CameraFilters_Lists_Each_Camera_Once_By_Name()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden"), Build("attic"), Build("garden")],
        };

        sut.RefreshTexts();

        sut.CameraFilters.Keys.Should().Equal("attic", "Garden");
        sut.AllCamerasText.Should().Be(Translations.AllCameras);
    }

    [Fact]
    public void No_Camera_Ticked_Lists_Every_Camera_And_Ticked_Cameras_List_Only_Theirs()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden"), Build("Attic"), Build("Front")],
        };
        sut.RefreshTexts();

        sut.FilteredRecordings.Should().HaveCount(3);

        sut.SelectedCameraFilters.Add("Garden");
        sut.SelectedCameraFilters.Add("front");

        sut.FilteredRecordings.Select(r => r.CameraName).Should().BeEquivalentTo("Garden", "Front");
    }

    [Fact]
    public void CameraFilters_Is_Raised_And_Keeps_The_Ticks_While_The_Camera_Is_Listed()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden")],
        };
        sut.RefreshTexts();
        sut.SelectedCameraFilters.Add("Garden");
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.RefreshTexts();

        changed.Should().Contain(nameof(RecordingsViewModel.CameraFilters));
        sut.SelectedCameraFilters.Should().Equal("Garden");
    }

    [Fact]
    public void A_Ticked_Camera_Without_Recordings_Is_Unticked()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, ApiBaseUrl)
        {
            Recordings = [Build("Garden"), Build("Attic")],
        };
        sut.RefreshTexts();
        sut.SelectedCameraFilters.Add("Attic");

        sut.Recordings = [Build("Garden")];
        sut.RefreshTexts();

        sut.SelectedCameraFilters.Should().BeEmpty();
        sut.FilteredRecordings.Should().ContainSingle();
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