namespace AtcSoft.VideoEngine;

/// <summary>
/// Puts the FFmpeg the engine needs in place: downloads the pinned build, checks its SHA-256,
/// unpacks only its <c>bin</c> folder, checks the library versions, and only then replaces the
/// target folder. A failed install leaves the target folder as it was.
/// </summary>
public static class FFmpegInstaller
{
    private const int BufferSize = 81920;
    private const long ProgressStep = 512 * 1024;

    private static readonly char[] EntrySeparators = ['/', '\\'];

    /// <summary>
    /// Gets the build the installer downloads: the shared "full" build of
    /// <see cref="FFmpegRuntime.ReleaseVersion"/> from the gyan.dev release mirror on GitHub.
    /// </summary>
    public static FFmpegPackage Package { get; } = new(
        new Uri($"https://github.com/GyanD/codexffmpeg/releases/download/{FFmpegRuntime.ReleaseVersion}/ffmpeg-{FFmpegRuntime.ReleaseVersion}-full_build-shared.zip"),
        "8d31e162f1616e37aab3fa2db991b97e1b8dbeb1c8465fd81a81be16ff91b328",
        99899672,
        FFmpegRuntime.ReleaseVersion);

    /// <summary>
    /// Installs <see cref="Package"/> into <see cref="FFmpegRuntime.ManagedFolder"/>.
    /// </summary>
    /// <param name="http">The client to download with.</param>
    /// <param name="progress">Told how far the install has come.</param>
    /// <param name="cancellationToken">Stops the install.</param>
    /// <returns>The folder FFmpeg was installed in.</returns>
    public static Task<string> InstallAsync(
        HttpClient http,
        IProgress<FFmpegInstallProgress>? progress,
        CancellationToken cancellationToken)
        => InstallAsync(http, Package, FFmpegRuntime.ManagedFolder, progress, cancellationToken);

    /// <summary>
    /// Installs a package into a folder, replacing what is there.
    /// </summary>
    /// <param name="http">The client to download with.</param>
    /// <param name="package">The build to install.</param>
    /// <param name="targetFolder">The folder that ends up holding the libraries and tools.</param>
    /// <param name="progress">Told how far the install has come.</param>
    /// <param name="cancellationToken">Stops the install.</param>
    /// <returns>The folder FFmpeg was installed in.</returns>
    /// <exception cref="FFmpegInstallException">The download failed, did not match its checksum, or held another FFmpeg.</exception>
    public static async Task<string> InstallAsync(
        HttpClient http,
        FFmpegPackage package,
        string targetFolder,
        IProgress<FFmpegInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFolder);

        targetFolder = Path.GetFullPath(targetFolder);
        var parent = Path.GetDirectoryName(targetFolder)
            ?? throw new ArgumentException("The target folder needs a parent folder.", nameof(targetFolder));

        // The work folder sits next to the target, so the final move stays on one volume.
        var work = Path.Combine(parent, ".install-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            var zip = Path.Combine(work, "ffmpeg.zip");
            var hash = await DownloadAsync(http, package, zip, progress, cancellationToken).ConfigureAwait(false);

            progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Verifying, package.Size, package.Size));
            if (!string.Equals(hash, package.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new FFmpegInstallException(
                    $"The download from '{package.DownloadUri}' does not match its checksum (SHA-256 {hash}, expected {package.Sha256}); it was not installed");
            }

            progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Extracting, package.Size, package.Size));
            var bin = Path.Combine(work, "bin");
            ExtractBin(zip, bin, cancellationToken);

            var versions = FFmpegRuntime.ReadVersions(bin);
            if (versions != FFmpegRuntime.Expected)
            {
                var found = versions is null
                    ? "no FFmpeg libraries"
                    : $"FFmpeg {versions.FFmpegMajor} (libavutil {versions.Avutil})";
                throw new FFmpegInstallException(
                    $"The download from '{package.DownloadUri}' holds {found}, not FFmpeg {FFmpegRuntime.Expected.FFmpegMajor} (libavutil {FFmpegRuntime.Expected.Avutil}); it was not installed");
            }

            cancellationToken.ThrowIfCancellationRequested();
            Replace(bin, targetFolder, Path.Combine(work, "old"));
            progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Done, package.Size, package.Size));
            return targetFolder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new FFmpegInstallException($"FFmpeg could not be installed into '{targetFolder}': {ex.Message}", ex);
        }
        finally
        {
            TryDelete(work);
        }
    }

    /// <summary>
    /// Puts the new folder in place of the target. The old one is moved aside first, which fails
    /// before anything changes when one of its files is in use (the server may run from it), and
    /// is moved back when the new one cannot be put in place. A target that already holds the
    /// FFmpeg the engine needs (another app installed it meanwhile) is kept.
    /// </summary>
    private static void Replace(
        string newFolder,
        string targetFolder,
        string asideFolder)
    {
        if (FFmpegRuntime.ReadVersions(targetFolder) == FFmpegRuntime.Expected)
        {
            return;
        }

        var hadTarget = Directory.Exists(targetFolder);
        if (hadTarget)
        {
            Directory.Move(targetFolder, asideFolder);
        }

        try
        {
            Directory.Move(newFolder, targetFolder);
        }
        catch (Exception ex) when (hadTarget && ex is IOException or UnauthorizedAccessException)
        {
            Directory.Move(asideFolder, targetFolder);
            throw;
        }
    }

    private static async Task<string> DownloadAsync(
        HttpClient http,
        FFmpegPackage package,
        string file,
        IProgress<FFmpegInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http
                .GetAsync(package.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? package.Size;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                var target = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                await using (target.ConfigureAwait(false))
                {
                    var buffer = new byte[BufferSize];
                    long done = 0;
                    long reported = 0;
                    progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, 0, total));
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        done += read;
                        if (done - reported >= ProgressStep)
                        {
                            reported = done;
                            progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, done, total));
                        }
                    }

                    progress?.Report(new FFmpegInstallProgress(FFmpegInstallStage.Downloading, done, total));
                }
            }

            return Convert.ToHexStringLower(sha.GetHashAndReset());
        }
        catch (HttpRequestException ex)
        {
            throw new FFmpegInstallException($"FFmpeg could not be downloaded from '{package.DownloadUri}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unpacks the files directly in a <c>bin</c> folder of the zip (<c>top/bin/name</c> or
    /// <c>bin/name</c>), by file name only, so no entry can write outside the folder.
    /// </summary>
    private static void ExtractBin(
        string zip,
        string bin,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(bin);
        var root = Path.GetFullPath(bin) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zip);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parts = entry.FullName.Split(EntrySeparators);
            if (parts.Length is < 2 or > 3 ||
                !string.Equals(parts[^2], "bin", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = parts[^1];
            if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(bin, name));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A work folder left behind is harmless; the next install uses a new one.
        }
    }
}