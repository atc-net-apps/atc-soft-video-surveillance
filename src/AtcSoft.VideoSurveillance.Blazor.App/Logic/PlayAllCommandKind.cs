namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What a tile of *play all from here* asks its video element to do.
/// </summary>
public enum PlayAllCommandKind
{
    /// <summary>Open a recording and play it from a place.</summary>
    Open,

    /// <summary>Play.</summary>
    Play,

    /// <summary>Pause.</summary>
    Pause,

    /// <summary>Stop and let go of the recording.</summary>
    Close,
}