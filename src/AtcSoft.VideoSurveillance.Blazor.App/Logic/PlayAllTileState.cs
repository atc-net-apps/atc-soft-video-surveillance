namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where one tile of *play all from here* is.
/// </summary>
public enum PlayAllTileState
{
    /// <summary>A recording is opening; nothing seen yet.</summary>
    Loading,

    /// <summary>Playing, at a known moment.</summary>
    Playing,

    /// <summary>Paused by the pacer because it ran ahead of the others.</summary>
    Held,

    /// <summary>Between recordings: the next one starts later.</summary>
    Waiting,

    /// <summary>Could not be played; left out of the pace.</summary>
    Failed,

    /// <summary>Nothing more to play.</summary>
    Finished,
}