namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// How renewing a stream lease went.
/// </summary>
public enum StreamLeaseRenewal
{
    /// <summary>The stream goes on.</summary>
    Renewed,

    /// <summary>The server no longer has the lease (it ran out, or the server restarted): the stream is started again.</summary>
    Gone,

    /// <summary>The server could not be reached: the lease is tried again next time.</summary>
    Unreachable,
}