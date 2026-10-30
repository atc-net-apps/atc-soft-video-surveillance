namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// A blob the detector found in a picture and what became of it (<see cref="MotionDiagnostics"/>).
/// </summary>
/// <param name="Box">The blob's box in analysed pixels; after the merging of near blobs, except one too small.</param>
/// <param name="State">What became of it.</param>
/// <param name="TrackId">The track following it; <see langword="null"/> for one too small.</param>
/// <param name="Travel">How far its track has travelled, as a share of the travel that confirms it (1 confirms).</param>
/// <param name="Held">Whether it is shown and has travelled far enough to be held out of the background's learning.</param>
public sealed record MotionCandidate(
    BoundingBox Box,
    MotionCandidateState State,
    int? TrackId,
    double Travel,
    bool Held);