namespace AtcSoft.VideoSurveillance.Events;

public class CameraConnectionChangedEventArgsTests
{
    [Fact]
    public void Constructor_Sets_Properties()
    {
        // Arrange
        var camera = new CameraConfiguration();
        var previousState = ConnectionState.Disconnected;
        var newState = ConnectionState.Connected;

        // Act
        var args = new CameraConnectionChangedEventArgs(camera, previousState, newState);

        // Assert
        args.Camera.Should().BeSameAs(camera);
        args.PreviousState.Should().Be(previousState);
        args.NewState.Should().Be(newState);
        args.ErrorMessage.Should().BeNull();
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WithErrorMessage_Sets_ErrorMessage()
    {
        // Arrange
        var camera = new CameraConfiguration();
        var errorMessage = "Connection timed out";

        // Act
        var args = new CameraConnectionChangedEventArgs(
            camera,
            ConnectionState.Connecting,
            ConnectionState.Error,
            errorMessage);

        // Assert
        args.ErrorMessage.Should().Be(errorMessage);
        args.NewState.Should().Be(ConnectionState.Error);
    }

    [Fact]
    public void Constructor_WithNullCamera_Throws()
    {
        // Act
        var act = () => new CameraConnectionChangedEventArgs(
            null!,
            ConnectionState.Disconnected,
            ConnectionState.Connected);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}