namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

public sealed class LiveStreamChoiceTests
{
    [Fact]
    public void One_Camera_Filling_The_Page_Plays_The_Main_Stream()
        => LiveStreamChoice.For(rows: 1, columns: 1).Should().Be(CameraStream.Main);

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 2)]
    [InlineData(3, 1)]
    public void A_Grid_Of_Several_Cameras_Plays_The_Sub_Streams(
        int rows,
        int columns)
        => LiveStreamChoice.For(rows, columns).Should().Be(CameraStream.Sub);
}