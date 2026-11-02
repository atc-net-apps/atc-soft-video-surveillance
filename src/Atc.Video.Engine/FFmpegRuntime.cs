namespace Atc.Video.Engine;

/// <summary>
/// Which FFmpeg the engine needs and where it is. The bindings load exactly one major version
/// of each library, so a folder only counts when all of them match; a folder with another
/// version is still reported, so the user is told what was found instead of a loader error.
/// </summary>
public static class FFmpegRuntime
{
    /// <summary>
    /// The FFmpeg release the installer puts in place (and the documentation names).
    /// </summary>
    public const string ReleaseVersion = "9.0.2";

    /// <summary>
    /// The environment variable that names an FFmpeg folder to use first.
    /// </summary>
    public const string PathEnvironmentVariable = "ATCSOFT_FFMPEG_PATH";

    /// <summary>
    /// The environment variable that limits the search: set to <c>explicit</c>, only the
    /// configured folder and <see cref="PathEnvironmentVariable"/> are looked in. The end-to-end
    /// tests use it to show the set-up dialog on a machine that has FFmpeg.
    /// </summary>
    public const string SearchEnvironmentVariable = "ATCSOFT_FFMPEG_SEARCH";

    /// <summary>
    /// Gets the library versions the bindings were built for.
    /// </summary>
    public static FFmpegLibraryVersions Expected { get; } = new(
        (int)LIBAVUTIL_VERSION_MAJOR,
        (int)LIBAVCODEC_VERSION_MAJOR,
        (int)LIBAVFORMAT_VERSION_MAJOR,
        (int)LIBAVDEVICE_VERSION_MAJOR,
        (int)LIBAVFILTER_VERSION_MAJOR,
        (int)LIBSWSCALE_VERSION_MAJOR,
        (int)LIBSWRESAMPLE_VERSION_MAJOR);

    /// <summary>
    /// Gets the folder the installer puts FFmpeg in: shared by every app and the server on the
    /// machine (the server may run under another account), one folder per release, so an
    /// FFmpeg installed by other means is never overwritten.
    /// </summary>
    public static string ManagedFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AtcSoft",
        "ffmpeg",
        ReleaseVersion,
        "bin");

    /// <summary>
    /// Gets the file that names the folder a user chose in the set-up dialog, for every app and
    /// the server on the machine.
    /// </summary>
    public static string RememberedFolderFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AtcSoft",
        "ffmpeg",
        "folder.txt");

    /// <summary>
    /// Gets the folders looked in, in order: the configured one, <see cref="PathEnvironmentVariable"/>,
    /// the folder a user chose (<see cref="RememberedFolderFile"/>), next to the app, <see cref="ManagedFolder"/>, the usual install folders, the app's own folder, and the PATH.
    /// </summary>
    /// <param name="configuredPath">A folder the app was configured with, looked in first.</param>
    /// <returns>The folders, each once.</returns>
    public static IReadOnlyList<string> Candidates(
        string? configuredPath = null)
        => Candidates(configuredPath, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Gets the folders looked in, reading the environment through <paramref name="environment"/>.
    /// </summary>
    /// <param name="configuredPath">A folder the app was configured with, looked in first.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The folders, each once.</returns>
    public static IReadOnlyList<string> Candidates(
        string? configuredPath,
        Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var candidates = new List<string>();
        void Add(string? folder)
        {
            if (!string.IsNullOrWhiteSpace(folder) && !candidates.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(folder);
            }
        }

        Add(configuredPath);
        Add(environment(PathEnvironmentVariable));
        if (IsSearchLimited(environment))
        {
            return candidates;
        }

        Add(ReadRememberedFolder(RememberedFolderFile));
        Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg"));
        Add(ManagedFolder);
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ffmpeg", "bin"));
        if (OperatingSystem.IsWindows())
        {
            Add(@"C:\ffmpeg\bin");
        }
        else
        {
            // Where Linux and macOS keep shared libraries: the image's own folder first, then the
            // loader's path and the system's folders.
            Add("/opt/ffmpeg/lib");
            foreach (var folder in (environment("LD_LIBRARY_PATH") ?? string.Empty).Split(Path.PathSeparator))
            {
                Add(folder.Trim());
            }

            Add(OperatingSystem.IsMacOS() ? "/opt/homebrew/lib" : "/usr/lib/x86_64-linux-gnu");
            Add(OperatingSystem.IsMacOS() ? "/usr/local/lib" : "/usr/lib/aarch64-linux-gnu");
            Add("/usr/local/lib");
            Add("/usr/lib");
        }

        Add(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var folder in (environment("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            Add(folder.Trim());
        }

        return candidates;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="SearchEnvironmentVariable"/> limits the search.
    /// </summary>
    public static bool IsSearchLimited()
        => IsSearchLimited(Environment.GetEnvironmentVariable);

    /// <summary>
    /// Remembers a folder the user chose, so every app and the server look in it.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns><see langword="false"/> when the note could not be written (no access).</returns>
    public static bool RememberFolder(string folder)
        => RememberFolder(folder, RememberedFolderFile);

    /// <summary>
    /// Remembers a folder in the given file.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="file">The file to write.</param>
    /// <returns><see langword="false"/> when the file could not be written.</returns>
    public static bool RememberFolder(
        string folder,
        string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            File.WriteAllText(file, folder.Trim());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the folder a user chose, or <see langword="null"/> when none was.
    /// </summary>
    /// <param name="file">The file the folder is named in.</param>
    /// <returns>The folder, or <see langword="null"/>.</returns>
    public static string? ReadRememberedFolder(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var folder = File.ReadAllText(file).Trim();
            return folder.Length == 0 ? null : folder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a library's name and major version from its file name, as each system names it:
    /// <c>avutil-61.dll</c> (Windows), <c>libavutil.so.61</c> or <c>libavutil.so.61.12.100</c>
    /// (Linux), <c>libavutil.61.dylib</c> or <c>libavutil.61.12.100.dylib</c> (macOS).
    /// </summary>
    /// <param name="fileName">The file name, without its folder.</param>
    /// <param name="library">The library (<c>avutil</c>).</param>
    /// <param name="major">Its major version.</param>
    /// <returns><see langword="true"/> when the name is that of a versioned library.</returns>
    public static bool TryReadLibraryFile(
        string fileName,
        out string library,
        out int major)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        library = string.Empty;
        major = 0;

        string name;
        string version;
        if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var stem = fileName[..^4];
            var dash = stem.LastIndexOf('-');
            if (dash <= 0)
            {
                return false;
            }

            name = stem[..dash];
            version = stem[(dash + 1)..];
        }
        else if (fileName.StartsWith("lib", StringComparison.Ordinal) &&
                 fileName.IndexOf(".so.", StringComparison.Ordinal) is > 3 and var so)
        {
            name = fileName[3..so];
            version = fileName[(so + 4)..].Split('.')[0];
        }
        else if (fileName.StartsWith("lib", StringComparison.Ordinal) &&
                 fileName.EndsWith(".dylib", StringComparison.Ordinal) &&
                 fileName.IndexOf('.', StringComparison.Ordinal) is > 3 and var dot)
        {
            name = fileName[3..dot];
            version = fileName[(dot + 1)..].Split('.')[0];
        }
        else
        {
            return false;
        }

        if (name.Length == 0 ||
            !int.TryParse(version, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out major))
        {
            major = 0;
            return false;
        }

        library = name;
        return true;
    }

    private static bool IsSearchLimited(Func<string, string?> environment)
        => string.Equals(environment(SearchEnvironmentVariable), "explicit", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Looks for the FFmpeg the engine needs in the usual places.
    /// </summary>
    /// <param name="configuredPath">A folder the app was configured with, looked in first.</param>
    /// <returns>What was found.</returns>
    public static FFmpegCheck Check(string? configuredPath = null)
        => Check(Candidates(configuredPath));

    /// <summary>
    /// Looks for the FFmpeg the engine needs in the given folders, in order.
    /// </summary>
    /// <param name="candidates">The folders to look in.</param>
    /// <returns>What was found.</returns>
    public static FFmpegCheck Check(IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var found = new List<FFmpegFolder>();
        foreach (var folder in candidates)
        {
            if (ReadVersions(folder) is { } versions)
            {
                found.Add(new FFmpegFolder(folder, versions));
            }
        }

        var ready = found.FirstOrDefault(f => f.Versions == Expected);
        var status = ready is not null
            ? FFmpegStatus.Ready
            : found.Count > 0 ? FFmpegStatus.WrongVersion : FFmpegStatus.Missing;

        return new FFmpegCheck(status, ready?.Folder, Expected, found);
    }

    /// <summary>
    /// Reads the library versions in a folder from the file names; <see langword="null"/> when
    /// the folder holds no FFmpeg. A library that is not there counts as version 0.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The versions, or <see langword="null"/>.</returns>
    public static FFmpegLibraryVersions? ReadVersions(string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            var majors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                if (TryReadLibraryFile(Path.GetFileName(file), out var library, out var major))
                {
                    majors[library] = Math.Max(major, majors.GetValueOrDefault(library));
                }
            }

            if (!majors.ContainsKey("avutil") && !majors.ContainsKey("avcodec"))
            {
                return null;
            }

            int Major(string library) => majors.GetValueOrDefault(library);
            return new FFmpegLibraryVersions(
                Major("avutil"),
                Major("avcodec"),
                Major("avformat"),
                Major("avdevice"),
                Major("avfilter"),
                Major("swscale"),
                Major("swresample"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}