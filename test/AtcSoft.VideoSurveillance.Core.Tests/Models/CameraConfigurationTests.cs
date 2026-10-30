namespace AtcSoft.VideoSurveillance.Models;

public class CameraConfigurationTests
{
    [Fact]
    public void New_Instance_Has_Default_Values()
    {
        // Act
        var camera = new CameraConfiguration();

        // Assert
        camera.Id.Should().NotBeEmpty();
        camera.Connection.Should().NotBeNull();
        camera.Connection.IpAddress.Should().BeEmpty();
        camera.Connection.Port.Should().Be(554);
        camera.Connection.Protocol.Should().Be(CameraProtocol.Rtsp);
        camera.Authentication.Should().NotBeNull();
        camera.Display.Should().NotBeNull();
        camera.Display.DisplayName.Should().BeEmpty();
        camera.Stream.Should().NotBeNull();
        camera.Overrides.Should().NotBeNull();
    }

    [Fact]
    public void Properties_Can_Be_Set()
    {
        // Arrange
        var id = Guid.NewGuid();
        var camera = new CameraConfiguration
        {
            Id = id,
        };

        camera.Connection.IpAddress = "192.168.1.100";
        camera.Connection.Port = 8554;
        camera.Connection.Protocol = CameraProtocol.Http;
        camera.Display.DisplayName = "Test Camera";

        // Assert
        camera.Id.Should().Be(id);
        camera.Connection.IpAddress.Should().Be("192.168.1.100");
        camera.Connection.Port.Should().Be(8554);
        camera.Connection.Protocol.Should().Be(CameraProtocol.Http);
        camera.Display.DisplayName.Should().Be("Test Camera");
    }

    [Fact]
    public void BuildUri_Returns_Correct_Uri_For_Rtsp()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "192.168.1.10";
        camera.Connection.Port = 554;
        camera.Connection.Protocol = CameraProtocol.Rtsp;
        camera.Connection.Path = "stream1";

        // Act
        var uri = camera.BuildUri();

        // Assert
        uri.ToString().Should().Be("rtsp://192.168.1.10:554/stream1");
    }

    [Fact]
    public void BuildUri_Includes_Credentials_When_Set()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "10.0.0.1";
        camera.Connection.Port = 554;
        camera.Connection.Protocol = CameraProtocol.Rtsp;
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "pass123";

        // Act
        var uri = camera.BuildUri();

        // Assert
        uri.ToString().Should().Contain("admin:pass123@");
    }

    [Fact]
    public void Clone_Creates_Deep_Copy()
    {
        // Arrange
        var original = new CameraConfiguration();
        original.Connection.IpAddress = "192.168.1.1";
        original.Connection.Port = 8080;
        original.Display.DisplayName = "Camera 1";

        // Act
        var clone = original.Clone();

        // Assert
        clone.Id.Should().Be(original.Id);
        clone.Connection.IpAddress.Should().Be("192.168.1.1");
        clone.Display.DisplayName.Should().Be("Camera 1");

        // Verify it's a deep copy
        clone.Connection.IpAddress = "10.0.0.1";
        original.Connection.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public void ToString_Contains_DisplayName_And_IpAddress()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Front Door";
        camera.Connection.IpAddress = "192.168.1.50";

        // Act
        var result = camera.ToString();

        // Assert
        result.Should().Contain("Front Door");
        result.Should().Contain("192.168.1.50");
        result.Should().StartWith("CameraConfiguration");
    }

    [Fact]
    public void ValueEquals_Returns_True_For_Same_Values()
    {
        // Arrange
        var camera1 = new CameraConfiguration();
        camera1.Connection.IpAddress = "192.168.1.1";
        camera1.Connection.Port = 554;
        camera1.Display.DisplayName = "Camera";

        var camera2 = new CameraConfiguration();
        camera2.Connection.IpAddress = "192.168.1.1";
        camera2.Connection.Port = 554;
        camera2.Display.DisplayName = "Camera";

        // Act & Assert
        camera1.ValueEquals(camera2).Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_Returns_False_For_Null()
    {
        // Arrange
        var camera = new CameraConfiguration();

        // Act & Assert
        camera.ValueEquals(null).Should().BeFalse();
    }
}