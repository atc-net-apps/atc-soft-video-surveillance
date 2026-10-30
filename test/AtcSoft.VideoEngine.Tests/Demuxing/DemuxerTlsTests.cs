namespace AtcSoft.VideoEngine.Demuxing;

/// <summary>
/// FFmpeg 9 verifies a TLS peer's certificate by default. Most cameras that offer HTTPS or RTSPS
/// use a self-signed one, so the engine does not verify unless asked. Runs against an HTTPS test
/// stream served by the FFmpeg command-line tool with a self-signed certificate; skips without it.
/// </summary>
public sealed class DemuxerTlsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-tls-" + Guid.NewGuid().ToString("N"));
    private readonly List<Process> servers = [];

    public void Dispose()
    {
        foreach (var server in servers)
        {
            Stop(server);
            server.Dispose();
        }

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
    public async Task A_Camera_With_A_Self_Signed_Certificate_Opens_By_Default()
    {
        var uri = await StartHttpsStreamAsync();
        if (uri is null)
        {
            return;
        }

        using var demuxer = new Demuxer();
        var open = () => demuxer.Open(uri, new StreamOptions { UseLowLatencyMode = false }, TestContext.Current.CancellationToken);

        open.Should().NotThrow();
    }

    [Fact]
    public async Task A_Camera_With_A_Self_Signed_Certificate_Is_Refused_When_Verification_Is_Asked_For()
    {
        var uri = await StartHttpsStreamAsync();
        if (uri is null)
        {
            return;
        }

        using var demuxer = new Demuxer();
        var open = () => demuxer.Open(uri, new StreamOptions { UseLowLatencyMode = false, VerifyTlsCertificate = true }, TestContext.Current.CancellationToken);

        open.Should().Throw<AtcSoft.VideoEngine.FFmpeg.FFmpegException>();
    }

    private async Task<Uri?> StartHttpsStreamAsync()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        Directory.CreateDirectory(directory);
        var certFile = Path.Combine(directory, "cert.pem");
        var keyFile = Path.Combine(directory, "key.pem");
        using (var key = RSA.Create(2048))
        {
            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            await File.WriteAllTextAsync(certFile, certificate.ExportCertificatePem(), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(keyFile, key.ExportPkcs8PrivateKeyPem(), TestContext.Current.CancellationToken);
        }

        var port = GetFreePort();

        // The server verifies its client too in FFmpeg 9, and the client has no certificate.
        var server = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i testsrc=size=320x240:rate=10 -t 30 " +
                "-c:v mpeg2video -f mpegts -listen 1 -tls_verify 0 " +
                $"-cert_file \"{certFile}\" -key_file \"{keyFile}\" https://127.0.0.1:{port}/live.ts",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test stream could not be started.");
        servers.Add(server);

        // Wait for the listener without connecting: the server takes one client only.
        for (var i = 0; i < 50 && !IsListening(port); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.SkipUnless(IsListening(port), "The HTTPS test stream did not start.");
        return new Uri($"https://127.0.0.1:{port}/live.ts");
    }

    private static bool IsListening(int port)
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }
}