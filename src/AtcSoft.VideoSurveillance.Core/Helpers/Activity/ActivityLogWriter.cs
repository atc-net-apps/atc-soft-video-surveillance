namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Writes the activity index of one recording while it is made: a line for every analysed second,
/// written as soon as the second is over, so a reader sees the index grow. Not thread-safe: one
/// camera's pictures are analysed one at a time.
/// </summary>
public sealed class ActivityLogWriter : IDisposable
{
    private readonly TextWriter writer;
    private readonly ActivityAccumulator accumulator;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityLogWriter"/> class and writes the header.
    /// </summary>
    /// <param name="writer">Where the lines go; disposed with this writer.</param>
    /// <param name="start">Where the offsets count from.</param>
    public ActivityLogWriter(
        TextWriter writer,
        DateTimeOffset start)
    {
        ArgumentNullException.ThrowIfNull(writer);

        this.writer = writer;
        accumulator = new ActivityAccumulator(start);
        writer.WriteLine(ActivityLogFormat.Header(start));
        writer.Flush();
    }

    /// <summary>
    /// Gets where the offsets count from.
    /// </summary>
    public DateTimeOffset Start
        => accumulator.Start;

    /// <summary>
    /// Creates the activity index file at <paramref name="path"/>, replacing one that is there.
    /// Others may read and delete it while it is written.
    /// </summary>
    /// <param name="path">The file, from <see cref="RecordingPathHelper.BuildActivityPath"/>.</param>
    /// <param name="start">Where the offsets count from.</param>
    /// <returns>The writer.</returns>
    public static ActivityLogWriter Create(
        string path,
        DateTimeOffset start)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
        }

        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        try
        {
            return new ActivityLogWriter(new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)), start);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Adds the result of one analysed picture; writes the previous second when this one begins a new one.
    /// </summary>
    /// <param name="time">When the picture was taken.</param>
    /// <param name="result">What the detector found in it.</param>
    public void Add(
        DateTimeOffset time,
        MotionDetectorResult result)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (accumulator.Add(time, result) is { } second)
        {
            WriteSecond(second);
        }
    }

    /// <summary>
    /// Writes the second in progress and closes the file.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            if (accumulator.Flush() is { } second)
            {
                WriteSecond(second);
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void WriteSecond(ActivitySecond second)
    {
        writer.WriteLine(ActivityLogFormat.Line(second));
        writer.Flush();
    }
}