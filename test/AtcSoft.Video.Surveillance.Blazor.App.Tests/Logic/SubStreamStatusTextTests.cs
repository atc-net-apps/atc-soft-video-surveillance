namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

public sealed class SubStreamStatusTextTests
{
    [Fact]
    public void A_Camera_Without_A_Sub_Stream_Says_Nothing()
        => SubStreamStatusText.For(CameraSubStreamStatus.NotConfigured).Should().BeEmpty();

    [Fact]
    public void A_Sub_Stream_Says_Whether_It_Is_Used()
    {
        SubStreamStatusText.For(CameraSubStreamStatus.InUse).Should().Be(Translations.SubStreamInUse);
        SubStreamStatusText.For(CameraSubStreamStatus.Unavailable).Should().Be(Translations.SubStreamUnavailable);
    }

    [Fact]
    public void A_Status_From_A_Newer_Server_Says_Nothing()
        => SubStreamStatusText.For(new CameraSubStreamStatus("croppedSomehow")).Should().BeEmpty();
}