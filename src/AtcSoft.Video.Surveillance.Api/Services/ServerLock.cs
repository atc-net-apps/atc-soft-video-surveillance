namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A file in the data folder that a running server keeps open for itself, so a command that
/// changes the server's files (<see cref="ServerPasswordReset"/>) can tell it is running: the
/// server keeps its users in memory and would write over the change.
/// </summary>
public sealed class ServerLock : IDisposable
{
    private readonly FileStream stream;

    private ServerLock(FileStream stream)
        => this.stream = stream;

    /// <summary>
    /// Takes the lock; <see langword="null"/> when another process holds it (or it cannot be opened).
    /// </summary>
    /// <param name="path">The lock file (<see cref="ServerDataPaths.LockPath"/>).</param>
    public static ServerLock? TryAcquire(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new ServerLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
        => stream.Dispose();
}