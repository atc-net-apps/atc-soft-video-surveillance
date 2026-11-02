namespace Atc.Video.Engine;

/// <summary>
/// The engine knows which FFmpeg it needs (the library versions the bindings were built for)
/// and finds a folder that has exactly those, so a wrong or missing FFmpeg is said clearly at
/// start instead of failing deep in the loader.
/// </summary>
public sealed class FFmpegRuntimeTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-ffmpeg-" + Guid.NewGuid().ToString("N"));

    public FFmpegRuntimeTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    [Fact]
    public void The_Expected_Libraries_Are_The_Ones_The_Bindings_Were_Built_For()
    {
        var expected = FFmpegRuntime.Expected;

        expected.Avutil.Should().Be((int)LIBAVUTIL_VERSION_MAJOR);
        expected.Avcodec.Should().Be((int)LIBAVCODEC_VERSION_MAJOR);
        expected.Avformat.Should().Be((int)LIBAVFORMAT_VERSION_MAJOR);
        expected.FFmpegMajor.Should().Be(9, "bindings 9.x are for FFmpeg 9");
    }

    [Theory]
    [InlineData("avutil-61.dll", "avutil", 61)]
    [InlineData("libavutil.so.61", "avutil", 61)]
    [InlineData("libavcodec.so.63.4.100", "avcodec", 63)]
    [InlineData("libavutil.61.dylib", "avutil", 61)]
    [InlineData("libswscale.10.1.100.dylib", "swscale", 10)]
    public void A_Library_Is_Read_From_The_File_Name_Each_System_Gives_It(
        string fileName,
        string library,
        int major)
    {
        // Before, only Windows' names were read: FFmpeg on Linux and macOS was never found.
        FFmpegRuntime.TryReadLibraryFile(fileName, out var name, out var version).Should().BeTrue();
        name.Should().Be(library);
        version.Should().Be(major);
    }

    [Theory]
    [InlineData("libavutil.so")]
    [InlineData("avutil.dll")]
    [InlineData("libavutil.dylib")]
    [InlineData("readme.txt")]
    public void A_File_Without_A_Version_Is_Not_A_Library_Found(string fileName)
        => FFmpegRuntime.TryReadLibraryFile(fileName, out _, out _).Should().BeFalse();

    [Fact]
    public void A_Folder_With_The_Expected_Libraries_Is_Ready()
    {
        var folder = Install("ffmpeg9", FFmpegRuntime.Expected);

        var check = FFmpegRuntime.Check([folder]);

        check.Status.Should().Be(FFmpegStatus.Ready);
        check.Folder.Should().Be(folder);
    }

    [Fact]
    public void An_Older_FFmpeg_Is_Reported_As_The_Wrong_Version()
    {
        var folder = Install("ffmpeg8", Older());

        var check = FFmpegRuntime.Check([folder]);

        check.Status.Should().Be(FFmpegStatus.WrongVersion);
        check.Folder.Should().BeNull();
        check.Found.Should().ContainSingle().Which.Should().Be(new FFmpegFolder(folder, Older()));
        check.Found[0].Versions.FFmpegMajor.Should().Be(8);
    }

    [Fact]
    public void The_Folder_With_The_Expected_Version_Wins_Over_An_Earlier_Older_One()
    {
        var older = Install("ffmpeg8", Older());
        var current = Install("ffmpeg9", FFmpegRuntime.Expected);

        var check = FFmpegRuntime.Check([older, current]);

        check.Status.Should().Be(FFmpegStatus.Ready);
        check.Folder.Should().Be(current);
        check.Found.Should().HaveCount(2);
    }

    [Fact]
    public void A_Folder_That_Misses_A_Library_Is_Not_Ready()
    {
        var folder = Install("incomplete", FFmpegRuntime.Expected);
        File.Delete(Path.Combine(folder, $"swresample-{FFmpegRuntime.Expected.Swresample}.dll"));

        var check = FFmpegRuntime.Check([folder]);

        check.Status.Should().Be(FFmpegStatus.WrongVersion);
    }

    [Fact]
    public void No_FFmpeg_At_All_Is_Missing()
    {
        var empty = Directory.CreateDirectory(Path.Combine(directory, "empty")).FullName;

        var check = FFmpegRuntime.Check([Path.Combine(directory, "nowhere"), empty]);

        check.Status.Should().Be(FFmpegStatus.Missing);
        check.Found.Should().BeEmpty();
    }

    [Fact]
    public void The_Managed_Folder_Is_Below_ProgramData_And_Named_After_The_Release()
        => FFmpegRuntime.ManagedFolder.Should().Be(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AtcSoft",
            "ffmpeg",
            FFmpegRuntime.ReleaseVersion,
            "bin"));

    [Fact]
    public void The_Usual_Folders_Are_Looked_In_After_The_Configured_One()
    {
        // Folders of this system: on Linux and macOS a "D:\..." would be split at its colon.
        var configured = Path.Combine(Path.GetTempPath(), "Configured");
        var onPath = Path.Combine(Path.GetTempPath(), "OnPath");
        var candidates = FFmpegRuntime.Candidates(configured, name => name == "PATH" ? onPath : null);

        candidates[0].Should().Be(configured);
        candidates.Should().Contain(FFmpegRuntime.ManagedFolder).And.Contain(onPath);
    }

    [Fact]
    public void Only_The_Named_Folders_Are_Looked_In_When_The_Search_Is_Limited()
    {
        // Lets the end-to-end tests show the set-up dialog on a machine that has FFmpeg.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FFmpegRuntime.SearchEnvironmentVariable] = "explicit",
            [FFmpegRuntime.PathEnvironmentVariable] = @"D:\FromEnvironment",
            ["PATH"] = @"D:\OnPath",
        };

        var candidates = FFmpegRuntime.Candidates(@"D:\Configured", environment.GetValueOrDefault);

        candidates.Should().Equal(@"D:\Configured", @"D:\FromEnvironment");
    }

    [Fact]
    public void A_Folder_The_User_Chose_Is_Remembered_For_Every_App()
    {
        var file = Path.Combine(directory, "notes", "folder.txt");
        var chosen = Install("chosen", FFmpegRuntime.Expected);

        FFmpegRuntime.RememberFolder(chosen, file).Should().BeTrue();

        FFmpegRuntime.ReadRememberedFolder(file).Should().Be(chosen);
    }

    [Fact]
    public void No_Folder_Is_Remembered_When_None_Was_Chosen()
    {
        FFmpegRuntime.ReadRememberedFolder(Path.Combine(directory, "missing.txt")).Should().BeNull();
    }

    private static FFmpegLibraryVersions Older()
        => FFmpegRuntime.Expected with
        {
            Avutil = FFmpegRuntime.Expected.Avutil - 1,
            Avcodec = FFmpegRuntime.Expected.Avcodec - 1,
            Avformat = FFmpegRuntime.Expected.Avformat - 1,
            Avdevice = FFmpegRuntime.Expected.Avdevice - 1,
            Avfilter = FFmpegRuntime.Expected.Avfilter - 1,
            Swscale = FFmpegRuntime.Expected.Swscale - 1,
            Swresample = FFmpegRuntime.Expected.Swresample - 1,
        };

    private string Install(
        string name,
        FFmpegLibraryVersions versions)
    {
        var folder = Directory.CreateDirectory(Path.Combine(directory, name)).FullName;
        foreach (var (library, major) in versions.Libraries)
        {
            File.WriteAllText(Path.Combine(folder, $"{library}-{major}.dll"), string.Empty);
        }

        return folder;
    }
}