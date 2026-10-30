namespace AtcSoft.VideoSurveillance.Api.Mapping;

public class RecordingMappingExtensionsTests
{
    [Fact]
    public void ToApiModel_MapsAllFields()
    {
        // Arrange
        var recordingTime = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var core = new RecordingEntry
        {
            FilePath = @"C:\recordings\cam1\2025-06-15_10-30-00.mp4",
            CameraName = "Front Door",
            RecordingTime = recordingTime,
            FileSizeBytes = 1_048_576,
            Duration = TimeSpan.FromMinutes(5),
        };
        var cameraId = Guid.NewGuid();

        // Act
        var api = core.ToApiModel(cameraId);

        // Assert
        api.CameraId.Should().Be(cameraId);
        api.CameraName.Should().Be("Front Door");
        api.FilePath.Should().Be(@"C:\recordings\cam1\2025-06-15_10-30-00.mp4");
        api.StartedAt.Should().Be(new DateTimeOffset(recordingTime, TimeSpan.Zero));
        api.Duration.Should().Be("00:05:00");
        api.FileSizeBytes.Should().Be(1_048_576);
        api.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ToApiModel_GeneratesDeterministicId()
    {
        // Arrange
        var core = new RecordingEntry
        {
            FilePath = @"C:\recordings\test.mp4",
            CameraName = "Cam",
            RecordingTime = DateTime.UtcNow,
        };

        // Act
        var api1 = core.ToApiModel();
        var api2 = core.ToApiModel();

        // Assert
        api1.Id.Should().Be(api2.Id);
    }

    [Fact]
    public void ToApiModel_DifferentPaths_ProduceDifferentIds()
    {
        // Arrange
        var core1 = new RecordingEntry
        {
            FilePath = @"C:\recordings\file1.mp4",
            CameraName = "Cam",
            RecordingTime = DateTime.UtcNow,
        };
        var core2 = new RecordingEntry
        {
            FilePath = @"C:\recordings\file2.mp4",
            CameraName = "Cam",
            RecordingTime = DateTime.UtcNow,
        };

        // Act
        var id1 = core1.ToApiModel().Id;
        var id2 = core2.ToApiModel().Id;

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void ToApiModel_NoCameraId_DefaultsToEmpty()
    {
        // Arrange
        var core = new RecordingEntry
        {
            FilePath = @"C:\recordings\test.mp4",
            CameraName = "Cam",
            RecordingTime = DateTime.UtcNow,
        };

        // Act
        var api = core.ToApiModel();

        // Assert
        api.CameraId.Should().Be(Guid.Empty);
    }
}