namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

public sealed class HubStatesTests
{
    public static TheoryData<string, Atc.Network.ConnectionState> ConnectionStates
        => new()
        {
            { global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Connected, Atc.Network.ConnectionState.Connected },
            { global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Connecting, Atc.Network.ConnectionState.Connecting },
            { global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Reconnecting, Atc.Network.ConnectionState.Reconnecting },
            { global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Error, Atc.Network.ConnectionState.ConnectionFailed },
            { global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Disconnected, Atc.Network.ConnectionState.Disconnected },
            { "rebooting", Atc.Network.ConnectionState.Disconnected },
        };

    [Theory]
    [MemberData(nameof(ConnectionStates))]
    public void A_Camera_State_Is_The_Tile_State(
        string state,
        Atc.Network.ConnectionState expected)
        => HubStates.ToConnectionState(new global::VideoSurveillance.Generated.Models.CameraConnectionState(state)).Should().Be(expected);

    [Theory]
    [InlineData(global::VideoSurveillance.Generated.Models.RecordingStatusState.Names.Recording, true)]
    [InlineData(global::VideoSurveillance.Generated.Models.RecordingStatusState.Names.RecordingMotion, true)]
    [InlineData(global::VideoSurveillance.Generated.Models.RecordingStatusState.Names.RecordingPostMotion, true)]
    [InlineData(global::VideoSurveillance.Generated.Models.RecordingStatusState.Names.Idle, false)]
    [InlineData("paused", false)]
    public void A_Camera_Is_Recording_Until_It_Is_Idle_Again(
        string state,
        bool expected)
        => HubStates.IsRecording(new global::VideoSurveillance.Generated.Models.RecordingStatusState(state)).Should().Be(expected);

    [Theory]
    [InlineData(global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Connected, "Connected")]
    [InlineData(global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.Connecting, "Connecting...")]
    [InlineData(global::VideoSurveillance.Generated.Models.CameraConnectionState.Names.DeviceUnplugged, "Device unplugged")]
    public void A_Camera_State_Is_Shown_In_Words(
        string state,
        string expected)
        => HubStates.ToText(new global::VideoSurveillance.Generated.Models.CameraConnectionState(state)).Should().Be(expected);

    [Fact]
    public void A_Camera_State_Is_Shown_In_The_Chosen_Language()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");

            HubStates.ToText(global::VideoSurveillance.Generated.Models.CameraConnectionState.Disconnected)
                .Should().Be(Translations.ResourceManager.GetString(nameof(Translations.Disconnected), CultureInfo.GetCultureInfo("da-DK")));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("rebooting", "Rebooting")]
    public void A_Camera_State_The_Client_Does_Not_Know_Is_Shown_With_A_Capital(
        string state,
        string expected)
        => HubStates.ToText(new global::VideoSurveillance.Generated.Models.CameraConnectionState(state)).Should().Be(expected);

    [Fact]
    public void No_Camera_State_Is_Shown_As_Disconnected()
        => HubStates.ToText(state: null).Should().Be("Disconnected");

    [Theory]
    [InlineData(true, "Live updates: on")]
    [InlineData(false, "Live updates: connecting...")]
    public void The_Connection_To_The_Server_Is_Shown_In_Words(
        bool connected,
        string expected)
        => HubStates.ServerText(connected).Should().Be(expected);
}