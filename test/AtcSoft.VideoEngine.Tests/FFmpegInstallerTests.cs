namespace AtcSoft.VideoEngine;

/// <summary>
/// The installer puts the FFmpeg the engine needs in place: the pinned download, checked
/// against its SHA-256, only its bin folder, and only when the libraries are the right version.
/// A failed install leaves nothing behind.
/// </summary>
public sealed class FFmpegInstallerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-ffinstall-" + Guid.NewGuid().ToString("N"));

    private string Target
        => Path.Combine(directory, "ffmpeg", "9.0.2", "bin");

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
    public async Task The_Bin_Folder_Of_The_Download_Is_Installed()
    {
        var zip = Zip(FFmpegRuntime.Expected);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);
        var progress = new List<FFmpegInstallProgress>();

        var installed = await FFmpegInstaller.InstallAsync(http, Package(zip), Target, new Recorder(progress), TestContext.Current.CancellationToken);

        installed.Should().Be(Target);
        FFmpegRuntime.ReadVersions(Target).Should().Be(FFmpegRuntime.Expected);
        File.Exists(Path.Combine(Target, "ffmpeg.exe")).Should().BeTrue("the server runs the command-line tool for live streams");
        Directory.GetFiles(Target).Should().NotContain(f => f.EndsWith("README.txt", StringComparison.Ordinal), "only the bin folder is installed");
        progress.Select(p => p.Stage).Should().Contain(FFmpegInstallStage.Downloading).And.EndWith(FFmpegInstallStage.Done);
        progress.Last(p => p.Stage == FFmpegInstallStage.Downloading).BytesDone.Should().Be(zip.Length);
    }

    [Fact]
    public async Task A_Download_That_Does_Not_Match_Its_Checksum_Is_Refused_And_Leaves_Nothing()
    {
        var zip = Zip(FFmpegRuntime.Expected);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);
        var package = Package(zip) with { Sha256 = new string('0', 64) };

        var install = () => FFmpegInstaller.InstallAsync(http, package, Target, progress: null, TestContext.Current.CancellationToken);

        (await install.Should().ThrowAsync<FFmpegInstallException>()).Which.Message.Should().Contain("checksum");
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public async Task A_Download_With_Another_FFmpeg_Version_Is_Refused()
    {
        var older = FFmpegRuntime.Expected with { Avutil = FFmpegRuntime.Expected.Avutil - 1 };
        var zip = Zip(older);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);

        var install = () => FFmpegInstaller.InstallAsync(http, Package(zip), Target, progress: null, TestContext.Current.CancellationToken);

        await install.Should().ThrowAsync<FFmpegInstallException>();
        Directory.Exists(Target).Should().BeFalse();
    }

    [Fact]
    public async Task An_Earlier_Install_Is_Replaced()
    {
        Directory.CreateDirectory(Target);
        await File.WriteAllTextAsync(Path.Combine(Target, "left-over.dll"), "x", TestContext.Current.CancellationToken);
        var zip = Zip(FFmpegRuntime.Expected);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);

        await FFmpegInstaller.InstallAsync(http, Package(zip), Target, progress: null, TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(Target, "left-over.dll")).Should().BeFalse();
        FFmpegRuntime.ReadVersions(Target).Should().Be(FFmpegRuntime.Expected);
    }

    [Fact]
    public async Task An_Install_In_Use_Is_Left_As_It_Was()
    {
        // The server may run from the shared folder while a client installs.
        Directory.CreateDirectory(Target);
        var inUse = Path.Combine(Target, "avutil-60.dll");
        await File.WriteAllTextAsync(inUse, "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(Target, "avcodec-62.dll"), "old", TestContext.Current.CancellationToken);
        var zip = Zip(FFmpegRuntime.Expected);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);

        await using (new FileStream(inUse, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var install = () => FFmpegInstaller.InstallAsync(http, Package(zip), Target, progress: null, TestContext.Current.CancellationToken);

            await install.Should().ThrowAsync<FFmpegInstallException>();
        }

        Directory.GetFiles(Target).Select(Path.GetFileName).Should().BeEquivalentTo("avutil-60.dll", "avcodec-62.dll");
        (await File.ReadAllTextAsync(inUse, TestContext.Current.CancellationToken)).Should().Be("old");
        Directory.GetDirectories(Path.GetDirectoryName(Target)!).Should().Equal([Target], "no work folder is left behind");
    }

    [Fact]
    public async Task An_Install_Another_App_Has_Just_Finished_Is_Kept()
    {
        // Two apps started together may both offer to install.
        var zip = Zip(FFmpegRuntime.Expected);
        using var handler = new Serve(zip);
        using var http = new HttpClient(handler, disposeHandler: false);
        await FFmpegInstaller.InstallAsync(http, Package(zip), Target, progress: null, TestContext.Current.CancellationToken);
        var marker = Path.Combine(Target, "in-use.marker");
        await File.WriteAllTextAsync(marker, "x", TestContext.Current.CancellationToken);

        var installed = await FFmpegInstaller.InstallAsync(http, Package(zip), Target, progress: null, TestContext.Current.CancellationToken);

        installed.Should().Be(Target);
        File.Exists(marker).Should().BeTrue();
    }

    [Fact]
    public void The_Package_Is_The_Pinned_Release_With_Its_Checksum()
    {
        var package = FFmpegInstaller.Package;

        package.ReleaseVersion.Should().Be(FFmpegRuntime.ReleaseVersion);
        package.DownloadUri.Scheme.Should().Be("https");
        package.DownloadUri.AbsoluteUri.Should().Contain(FFmpegRuntime.ReleaseVersion).And.EndWith("-full_build-shared.zip");
        package.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    private static FFmpegPackage Package(byte[] zip)
        => new(
            new Uri("https://downloads.test/ffmpeg-9.0.2-full_build-shared.zip"),
            Convert.ToHexStringLower(SHA256.HashData(zip)),
            zip.Length,
            FFmpegRuntime.ReleaseVersion);

    private static byte[] Zip(FFmpegLibraryVersions versions)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write("x"u8);
            }

            foreach (var (library, major) in versions.Libraries)
            {
                Add($"ffmpeg-9.0.2-full_build-shared/bin/{library}-{major}.dll");
            }

            Add("ffmpeg-9.0.2-full_build-shared/bin/ffmpeg.exe");
            Add("ffmpeg-9.0.2-full_build-shared/bin/ffprobe.exe");
            Add("ffmpeg-9.0.2-full_build-shared/README.txt");
            Add("ffmpeg-9.0.2-full_build-shared/include/libavutil/avutil.h");
        }

        return stream.ToArray();
    }

    private sealed class Serve(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    private sealed class Recorder(List<FFmpegInstallProgress> progress) : IProgress<FFmpegInstallProgress>
    {
        public void Report(FFmpegInstallProgress value)
            => progress.Add(value);
    }
}