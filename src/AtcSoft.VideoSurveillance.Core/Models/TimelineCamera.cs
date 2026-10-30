namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A camera as a timeline of every camera knows it: its lane, and the folder its recordings are in.
/// </summary>
/// <param name="Id">The camera.</param>
/// <param name="Name">Its name, which also names its recording folder.</param>
public sealed record TimelineCamera(
    Guid Id,
    string Name);