namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// Follows blobs from picture to picture (SORT-like: a constant-velocity guess, then matching by
/// overlap or nearness). A track shows only when it has been seen for a while <b>and</b> has
/// travelled (moved, or grown or shrunk by coming closer or going away): a branch swaying on the
/// spot never does, a person walking by or up to the camera does. A track keeps its id while it
/// is followed.
/// </summary>
internal sealed class MotionTracker
{
    // A track that has gone this many times the travel that confirms it is an object for certain
    // (a walker, or someone coming closer); a false track on a swaying branch jitters about where
    // it began.
    private const double CertainTravel = 3;

    private readonly List<Track> tracks = [];
    private int nextId = 1;

    /// <summary>
    /// Takes the blobs of a picture and gets the confirmed tracks seen in it, each with whether it
    /// has travelled far enough to be an object for certain. With <paramref name="seen"/>, every
    /// track seen in the picture is added to it with what became of it, for the diagnostics.
    /// </summary>
    public List<(Blob Box, int Id, bool Certain)> Update(
        List<Blob> blobs,
        double time,
        MotionDetectorOptions options,
        int width,
        List<(Blob Box, int Id, MotionCandidateState State, double Travel, bool Certain)>? seen = null)
    {
        var minTravel = options.MinTravelShare * width;
        var pairs = new List<(int Track, int Blob, double Score)>();
        for (var t = 0; t < tracks.Count; t++)
        {
            var predicted = tracks[t].Predict(time);
            for (var b = 0; b < blobs.Count; b++)
            {
                var score = MatchScore(predicted, blobs[b]);
                if (score > 0)
                {
                    pairs.Add((t, b, score));
                }
            }
        }

        var trackTaken = new bool[tracks.Count];
        var blobTaken = new bool[blobs.Count];
        foreach (var (t, b, _) in pairs.OrderByDescending(p => p.Score))
        {
            if (trackTaken[t] || blobTaken[b])
            {
                continue;
            }

            trackTaken[t] = blobTaken[b] = true;
            tracks[t].See(blobs[b], time);
        }

        for (var b = 0; b < blobs.Count; b++)
        {
            if (!blobTaken[b])
            {
                tracks.Add(new Track(nextId++, blobs[b], time));
            }
        }

        tracks.RemoveAll(t => time - t.LastSeen > options.TrackLostSeconds);

        var shown = new List<(Blob Box, int Id, bool Certain)>();
        foreach (var track in tracks)
        {
            if (!track.Confirmed && time - track.FirstSeen >= options.ConfirmSeconds && track.Travel >= minTravel)
            {
                track.Confirmed = true;
            }

            var isSeen = Math.Abs(track.LastSeen - time) < 1e-9;
            var certain = track.Travel >= CertainTravel * minTravel;
            if (track.Confirmed && isSeen)
            {
                shown.Add((track.Box, track.Id, certain));
            }

            if (seen is not null && isSeen)
            {
                var state = track.Confirmed
                    ? MotionCandidateState.Shown
                    : time - track.FirstSeen < options.ConfirmSeconds
                        ? MotionCandidateState.TooNew
                        : MotionCandidateState.NotTravelled;
                seen.Add((track.Box, track.Id, state, minTravel > 0 ? track.Travel / minTravel : 0, track.Confirmed && certain));
            }
        }

        return shown;
    }

    public void Reset()
        => tracks.Clear();

    // Overlap scores above 1; a near box without overlap between 0 and 1; a far one 0.
    private static double MatchScore(
        (double MinX, double MinY, double MaxX, double MaxY) predicted,
        Blob blob)
    {
        var width = Math.Min(predicted.MaxX, blob.MaxX + 1) - Math.Max(predicted.MinX, blob.MinX);
        var height = Math.Min(predicted.MaxY, blob.MaxY + 1) - Math.Max(predicted.MinY, blob.MinY);
        var predictedArea = (predicted.MaxX - predicted.MinX) * (predicted.MaxY - predicted.MinY);
        var blobArea = (double)blob.Width * blob.Height;
        if (width > 0 && height > 0)
        {
            var shared = width * height;
            return 1 + (shared / (predictedArea + blobArea - shared));
        }

        var dx = ((predicted.MinX + predicted.MaxX) / 2) - blob.CenterX;
        var dy = ((predicted.MinY + predicted.MaxY) / 2) - blob.CenterY;
        var reach = Math.Max(Math.Max(predicted.MaxX - predicted.MinX, predicted.MaxY - predicted.MinY), Math.Max(blob.Width, blob.Height));
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        return distance < reach ? 1 - (distance / reach) : 0;
    }

    private sealed class Track(
        int id,
        Blob box,
        double time)
    {
        private const double GrowthRatio = 1.3;

        private readonly double originX = box.CenterX;
        private readonly double originY = box.CenterY;
        private readonly double originSide = Math.Max(box.Width, box.Height);
        private double smoothedSide = Math.Max(box.Width, box.Height);
        private double velocityX;
        private double velocityY;

        public int Id { get; } = id;

        public Blob Box { get; private set; } = box;

        public double FirstSeen { get; } = time;

        public double LastSeen { get; private set; } = time;

        public double Travel { get; private set; }

        public bool Confirmed { get; set; }

        public (double MinX, double MinY, double MaxX, double MaxY) Predict(
            double time)
        {
            var dt = time - LastSeen;
            var dx = velocityX * dt;
            var dy = velocityY * dt;
            return (Box.MinX + dx, Box.MinY + dy, Box.MaxX + 1 + dx, Box.MaxY + 1 + dy);
        }

        public void See(
            Blob blob,
            double time)
        {
            var dt = time - LastSeen;
            if (dt > 0)
            {
                // Smoothed, so one odd blob does not throw the guess off.
                velocityX = (0.5 * velocityX) + (0.5 * (blob.CenterX - Box.CenterX) / dt);
                velocityY = (0.5 * velocityY) + (0.5 * (blob.CenterY - Box.CenterY) / dt);
            }

            Box = blob;
            LastSeen = time;
            var tx = blob.CenterX - originX;
            var ty = blob.CenterY - originY;

            // Coming towards the camera or going away changes the size more than the place. A
            // swaying or flickering blob changes size too, back and forth, so only a sustained
            // change counts: the smoothed size must have grown or shrunk by 30 %.
            smoothedSide = (0.7 * smoothedSide) + (0.3 * Math.Max(blob.Width, blob.Height));
            var ratio = smoothedSide / Math.Max(1, originSide);
            var grown = ratio is >= GrowthRatio or <= 1 / GrowthRatio ? Math.Abs(smoothedSide - originSide) : 0;
            Travel = Math.Max(Travel, Math.Max(Math.Sqrt((tx * tx) + (ty * ty)), grown));
        }
    }
}