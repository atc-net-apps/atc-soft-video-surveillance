namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// One tile of *play all from here* as its player reports it to the pacer.
/// </summary>
/// <param name="State">Where the tile is.</param>
/// <param name="At">The moment on the picture, for a tile playing or held.</param>
/// <param name="NextStart">When its next recording starts, for a tile waiting.</param>
public sealed record PlayAllTile(
    PlayAllTileState State,
    DateTimeOffset? At = null,
    DateTimeOffset? NextStart = null);