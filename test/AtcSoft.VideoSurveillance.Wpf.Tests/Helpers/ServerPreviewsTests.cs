namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

using ApiPreviewSheet = global::VideoSurveillance.Generated.Recordings.Models.RecordingPreviewSheet;
using ApiPreviewTile = global::VideoSurveillance.Generated.Recordings.Models.RecordingPreviewTile;

/// <summary>
/// The server's answer about a recording's previews becomes the layout the timeline looks tiles up
/// in; an answer without previews becomes none.
/// </summary>
public sealed class ServerPreviewsTests
{
    [Fact]
    public void The_Answer_Becomes_The_Layout_Of_The_Tiles()
    {
        var answer = new ApiPreviewSheet(
            HasPreviews: true,
            IsBeingMade: false,
            ImagePath: "Garden/Garden_20261019_101500_000.previews.jpg",
            IntervalSeconds: 1,
            TileWidth: 160,
            TileHeight: 90,
            Columns: 20,
            GridCount: 11,
            Events: [new ApiPreviewTile(Seconds: 6.5, Tile: 11)]);

        var layout = ServerPreviews.ToLayout(answer)!;

        (layout.IntervalSeconds, layout.TileWidth, layout.TileHeight, layout.Columns, layout.GridCount).Should().Be((1d, 160, 90, 20, 11));
        layout.Events.Should().Equal(new AtcSoft.VideoSurveillance.Models.RecordingPreviewEvent(6.5, 11));
    }

    [Fact]
    public void An_Answer_Without_Previews_Is_No_Layout()
    {
        var answer = new ApiPreviewSheet(
            HasPreviews: false,
            IsBeingMade: true,
            ImagePath: null,
            IntervalSeconds: 0,
            TileWidth: 0,
            TileHeight: 0,
            Columns: 0,
            GridCount: 0,
            Events: []);

        ServerPreviews.ToLayout(answer).Should().BeNull();
        ServerPreviews.ToLayout(null).Should().BeNull();
    }
}