namespace AtcSoft.VideoSurveillance.Wpf.Core.ValueConverters;

using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;
using MotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using MotionZoneKind = AtcSoft.VideoSurveillance.Enums.MotionZoneKind;

public class CameraConfigurationJsonValueConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
            new CameraConfigurationJsonValueConverter(),
        },
    };

    [Fact]
    public void Deserialize_NestedOverrides_RecordingOnly_EnableRecordingOnConnect()
    {
        // Arrange - this is the exact shape from cameras.json that was failing
        const string json = """
        {
            "id": "8cb3d71d-8f6b-4f06-ac2e-a3f2cd8a2566",
            "connection": { "ipAddress": "192.168.1.43", "protocol": "Rtsp", "port": 554, "path": "stream1" },
            "authentication": { "userName": "user", "password": "pass" },
            "display": { "displayName": "TAPO-C210-03" },
            "stream": {},
            "overrides": {
                "recording": {
                    "enableRecordingOnConnect": true
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides.Should().NotBeNull();
        camera.Overrides!.Recording.EnableRecordingOnConnect.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_NestedOverrides_RecordingOnly_EnableRecordingOnMotion()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000001",
            "connection": { "ipAddress": "10.0.0.1", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {},
            "overrides": {
                "recording": {
                    "enableRecordingOnMotion": true
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides!.Recording.EnableRecordingOnMotion.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_NestedOverrides_ConnectionOnly()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000002",
            "connection": { "ipAddress": "10.0.0.2", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {},
            "overrides": {
                "connection": {
                    "connectionTimeoutSeconds": 30
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides!.Connection.ConnectionTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Deserialize_NestedOverrides_MotionDetectionOnly()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000003",
            "connection": { "ipAddress": "10.0.0.3", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {},
            "overrides": {
                "motionDetection": {
                    "sensitivity": 50
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides!.MotionDetection.Sensitivity.Should().Be(50);
    }

    [Fact]
    public void Deserialize_NestedOverrides_PerformanceOnly()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000004",
            "connection": { "ipAddress": "10.0.0.4", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {},
            "overrides": {
                "performance": {
                    "hardwareAcceleration": false
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides!.Performance.HardwareAcceleration.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_NestedOverrides_MultipleSections()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000005",
            "connection": { "ipAddress": "10.0.0.5", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {},
            "overrides": {
                "recording": {
                    "enableRecordingOnConnect": true
                },
                "connection": {
                    "autoReconnectOnFailure": false
                }
            }
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides!.Recording.EnableRecordingOnConnect.Should().BeTrue();
        camera.Overrides!.Connection.AutoReconnectOnFailure.Should().BeFalse();
    }

    [Fact]
    public void Deserialize_NoOverrides_ReturnsDefaults()
    {
        // Arrange
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000006",
            "connection": { "ipAddress": "10.0.0.6", "protocol": "Rtsp", "port": 554 },
            "authentication": { "userName": "u", "password": "p" },
            "display": { "displayName": "Test" },
            "stream": {}
        }
        """;

        // Act
        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        camera.Should().NotBeNull();
        camera!.Overrides.Should().NotBeNull();
        camera.Overrides!.HasAnyOverride().Should().BeFalse();
        camera.Overrides.Recording.EnableRecordingOnConnect.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_NestedOverrides_PreservesValues()
    {
        // Arrange
        var original = new CameraConfiguration();
        original.Connection.IpAddress = "192.168.1.100";
        original.Connection.Port = 554;
        original.Display.DisplayName = "RoundTrip Test";
        original.Overrides!.Recording.EnableRecordingOnConnect = true;
        original.Overrides.Recording.RecordingFormat = "mkv";

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Display.DisplayName.Should().Be("RoundTrip Test");
        deserialized.Overrides!.Recording.EnableRecordingOnConnect.Should().BeTrue();
        deserialized.Overrides.Recording.RecordingFormat.Should().Be("mkv");
    }

    [Fact]
    public void RoundTrip_ConnectionType_And_Icon_Overrides_PreservesValues()
    {
        // Arrange
        var original = new CameraConfiguration();
        original.Connection.IpAddress = "192.168.1.100";
        original.Connection.ConnectionType = CameraConnectionType.Wifi;
        original.Display.DisplayName = "Wi-Fi camera";
        original.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon = true;
        original.Overrides.CameraDisplay.ConnectionTypeIconPlacement = ConnectionTypeIconPlacement.SameColumn;

        // Act
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Connection.ConnectionType.Should().Be(CameraConnectionType.Wifi);
        deserialized.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon.Should().BeTrue();
        deserialized.Overrides.CameraDisplay.ConnectionTypeIconPlacement.Should().Be(ConnectionTypeIconPlacement.SameColumn);
    }

    [Fact]
    public void Serialize_ConnectionType_None_Is_Omitted()
    {
        // Network cameras without a connection type keep the pre-existing on-disk shape.
        var original = new CameraConfiguration();
        original.Connection.IpAddress = "192.168.1.100";

        var json = JsonSerializer.Serialize(original, JsonOptions);

        json.Should().NotContain("connectionType");
    }

    [Fact]
    public void Deserialize_Without_ConnectionType_Defaults_To_None()
    {
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000002",
            "connection": { "ipAddress": "10.0.0.2", "protocol": "Rtsp", "port": 554 },
            "authentication": {},
            "display": { "displayName": "Old camera" },
            "stream": {}
        }
        """;

        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        camera!.Connection.ConnectionType.Should().Be(CameraConnectionType.None);
        camera.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon.Should().BeNull();
    }

    [Fact]
    public void A_Cameras_Own_Detector_Settings_Go_Both_Ways()
    {
        var original = new CameraConfiguration();
        original.Overrides!.MotionDetection.SmallestObjectPercent = 0.5;
        original.Overrides.MotionDetection.StoppedObjectSeconds = 300;
        original.Overrides.MotionDetection.IgnoreLightingChanges = false;

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var read = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions)!;

        read.Overrides!.MotionDetection.ValueEquals(original.Overrides.MotionDetection).Should().BeTrue();
        (read.Overrides.MotionDetection.SmallestObjectPercent, read.Overrides.MotionDetection.StoppedObjectSeconds, read.Overrides.MotionDetection.IgnoreLightingChanges)
            .Should().Be((0.5, 300, false));
    }

    [Fact]
    public void The_Motion_Zones_Go_Both_Ways()
    {
        var original = new CameraConfiguration
        {
            MotionZones =
            [
                new MotionZone { Name = "Tree", Kind = MotionZoneKind.Ignore, Points = [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.9)], Color = "Green" },
                new MotionZone { Name = "Gate", Kind = MotionZoneKind.Watch, Points = [new(0.5, 0.5), new(0.9, 0.5), new(0.9, 0.9)], Sensitivity = 80, SmallestObjectPercent = 0.1 },
            ],
        };

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var read = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions)!;

        json.Should().Contain("\"motionZones\"");
        read.ValueEquals(original).Should().BeTrue();
        read.MotionZones[1].Kind.Should().Be(MotionZoneKind.Watch);
        (read.MotionZones[1].Sensitivity, read.MotionZones[1].SmallestObjectPercent).Should().Be((80, 0.1));
    }

    [Fact]
    public void A_Camera_Without_Zones_Writes_None_And_Reads_None()
    {
        var json = JsonSerializer.Serialize(new CameraConfiguration(), JsonOptions);

        json.Should().NotContain("motionZones");
        JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions)!.MotionZones.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_Legacy_Flat_Overrides_Reads_ConnectionTypeIcon()
    {
        const string json = """
        {
            "id": "00000000-0000-0000-0000-000000000003",
            "connection": { "ipAddress": "10.0.0.3", "protocol": "Rtsp", "port": 554, "connectionType": "Lan" },
            "authentication": {},
            "display": { "displayName": "Flat" },
            "stream": {},
            "overrides": { "showOverlayConnectionTypeIcon": true, "connectionTypeIconPlacement": "SameColumn" }
        }
        """;

        var camera = JsonSerializer.Deserialize<CameraConfiguration>(json, JsonOptions);

        camera!.Connection.ConnectionType.Should().Be(CameraConnectionType.Lan);
        camera.Overrides!.CameraDisplay.ShowOverlayConnectionTypeIcon.Should().BeTrue();
        camera.Overrides.CameraDisplay.ConnectionTypeIconPlacement.Should().Be(ConnectionTypeIconPlacement.SameColumn);
    }
}