namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// What a long test run is measured on: the software's versions, the operating system, the processor,
/// memory, graphics and disks, the power plan's sleep timeouts and FFmpeg. Written at start-up when
/// diagnostics are on, so a log can be read against the machine that wrote it and runs on different
/// machines can be compared. A probe that fails says so instead of failing the report.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemReport
{
    private const string NotAvailable = "not available";

    /// <summary>
    /// Collects the report.
    /// </summary>
    /// <param name="folders">The folders whose disks matter (recordings, snapshots, logs).</param>
    /// <returns>The sections, in order.</returns>
    public static IReadOnlyList<SystemReportSection> Collect(
        IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        return
        [
            new SystemReportSection("Application", Application()),
            new SystemReportSection("Operating system", OperatingSystemEntries()),
            new SystemReportSection("Processor and memory", ProcessorAndMemory()),
            new SystemReportSection("Graphics", Graphics()),
            new SystemReportSection("Disks", Disks(folders)),
            new SystemReportSection("Power", Power()),
            new SystemReportSection("FFmpeg", FFmpeg()),
        ];
    }

    /// <summary>
    /// Writes a report as text: a heading per section and an indented line per entry.
    /// </summary>
    /// <param name="sections">The sections.</param>
    /// <returns>The text.</returns>
    public static string Format(IEnumerable<SystemReportSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var text = new StringBuilder();
        foreach (var section in sections)
        {
            text.Append('[').Append(section.Name).AppendLine("]");
            foreach (var (key, value) in section.Entries)
            {
                text.Append("  ").Append(key).Append(": ").AppendLine(value);
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    private static List<KeyValuePair<string, string>> Application()
    {
        var entry = Assembly.GetEntryAssembly();
        var version = entry?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? entry?.GetName().Version?.ToString()
                      ?? NotAvailable;
        using var process = Process.GetCurrentProcess();

        return
        [
            Entry("Name", entry?.GetName().Name ?? NotAvailable),
            Entry("Version", version),
            Entry("Folder", AppContext.BaseDirectory),
            Entry(".NET", RuntimeInformation.FrameworkDescription),
            Entry("Process", string.Create(CultureInfo.InvariantCulture, $"{RuntimeInformation.ProcessArchitecture}, id {process.Id}, started {process.StartTime:O}")),
            Entry("Garbage collector", $"{(System.Runtime.GCSettings.IsServerGC ? "server" : "workstation")}, {System.Runtime.GCSettings.LatencyMode}"),
            Entry("Culture", $"{CultureInfo.CurrentCulture.Name}, UI {CultureInfo.CurrentUICulture.Name}"),
            Entry("Time zone", TimeZoneInfo.Local.Id),
        ];
    }

    private static List<KeyValuePair<string, string>> OperatingSystemEntries()
    {
        var entries = new List<KeyValuePair<string, string>>
        {
            Entry("Description", RuntimeInformation.OSDescription),
            Entry("Architecture", RuntimeInformation.OSArchitecture.ToString()),
        };

        Try(entries, "Windows", () =>
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key is null
                ? NotAvailable
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{key.GetValue("ProductName")} {key.GetValue("DisplayVersion")} (build {key.GetValue("CurrentBuild")}.{key.GetValue("UBR")}, {key.GetValue("EditionID")})");
        });

        entries.Add(Entry("Up since", DateTimeOffset.Now.AddMilliseconds(-Environment.TickCount64).ToString("O", CultureInfo.InvariantCulture)));
        entries.Add(Entry("Machine", Environment.MachineName));
        return entries;
    }

    private static List<KeyValuePair<string, string>> ProcessorAndMemory()
    {
        var entries = new List<KeyValuePair<string, string>>();

        Try(entries, "Processor", () =>
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{(key?.GetValue("ProcessorNameString") as string)?.Trim() ?? NotAvailable}, {key?.GetValue("~MHz")} MHz");
        });

        entries.Add(Entry("Logical processors", Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)));
        Try(entries, "Memory", () =>
            ProcessMetrics.SystemMemory() is { } memory
                ? $"{Gigabytes(memory.TotalBytes)} total, {Gigabytes(memory.AvailableBytes)} available"
                : NotAvailable);

        Try(entries, "Computer", () => FirstWmi("SELECT Manufacturer, Model FROM Win32_ComputerSystem", o => $"{o["Manufacturer"]} {o["Model"]}"));
        return entries;
    }

    private static List<KeyValuePair<string, string>> Graphics()
    {
        var entries = new List<KeyValuePair<string, string>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverVersion, AdapterRAM, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController");
            var index = 0;
            foreach (var adapter in searcher.Get().Cast<ManagementObject>())
            {
                using (adapter)
                {
                    index++;
                    var memory = adapter["AdapterRAM"] is uint bytes and > 0 ? $", {Gigabytes(bytes)}" : string.Empty;
                    var screen = adapter["CurrentHorizontalResolution"] is { } width
                        ? $", showing {width}x{adapter["CurrentVerticalResolution"]} at {adapter["CurrentRefreshRate"]} Hz"
                        : string.Empty;
                    entries.Add(Entry(
                        string.Create(CultureInfo.InvariantCulture, $"Adapter {index}"),
                        $"{adapter["Name"]}, driver {adapter["DriverVersion"]}{memory}{screen}"));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            entries.Add(Entry("Adapters", $"{NotAvailable}: {ex.Message}"));
        }

        return entries;
    }

    private static List<KeyValuePair<string, string>> Disks(
        IEnumerable<string> folders)
    {
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var folder in folders.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Try(entries, folder, () =>
            {
                var root = Path.GetPathRoot(Path.GetFullPath(folder));
                if (string.IsNullOrEmpty(root))
                {
                    return NotAvailable;
                }

                var drive = new DriveInfo(root);
                return drive.IsReady
                    ? $"{drive.Name} {drive.DriveType} {drive.DriveFormat}, {Gigabytes(drive.AvailableFreeSpace)} free of {Gigabytes(drive.TotalSize)}"
                    : $"{drive.Name} not ready";
            });
        }

        return entries;
    }

    // A PC that sleeps stops recording: the timeouts of the active power plan, on mains power.
    private static List<KeyValuePair<string, string>> Power()
    {
        var entries = new List<KeyValuePair<string, string>>();
        Try(entries, "Power plan", () => RunPowerCfg("/getactivescheme")?.Trim() ?? NotAvailable);
        Try(entries, "Sleep after (plugged in)", () => PowerTimeout("STANDBYIDLE"));
        Try(entries, "Hibernate after (plugged in)", () => PowerTimeout("HIBERNATEIDLE"));
        return entries;
    }

    private static List<KeyValuePair<string, string>> FFmpeg()
    {
        var check = FFmpegRuntime.Check();
        return
        [
            Entry("Needed", FFmpegRuntime.ReleaseVersion),
            Entry("Status", check.Status.ToString()),
            Entry("Found", check.ToString() ?? NotAvailable),
        ];
    }

    private static string PowerTimeout(string setting)
    {
        var output = RunPowerCfg($"/query SCHEME_CURRENT SUB_SLEEP {setting}");
        var match = output is null
            ? null
            : Regex.Match(output, "Current AC Power Setting Index:\\s*0x(?<seconds>[0-9a-fA-F]+)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        if (match is not { Success: true })
        {
            return NotAvailable;
        }

        var seconds = long.Parse(match.Groups["seconds"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return seconds == 0
            ? "never"
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} minutes (recordings stop while the PC sleeps)");
    }

    private static string? RunPowerCfg(string arguments)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        return process.WaitForExit(5000) ? output : null;
    }

    private static string FirstWmi(
        string query,
        Func<ManagementObject, string> describe)
    {
        using var searcher = new ManagementObjectSearcher(query);
        using var item = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        return item is null ? NotAvailable : describe(item);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A probe that fails is reported; it never fails the report.")]
    private static void Try(
        List<KeyValuePair<string, string>> entries,
        string key,
        Func<string> probe)
    {
        try
        {
            entries.Add(Entry(key, probe()));
        }
        catch (Exception ex)
        {
            entries.Add(Entry(key, $"{NotAvailable}: {ex.Message}"));
        }
    }

    private static string Gigabytes(double bytes)
        => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024 * 1024):0.0} GB");

    private static KeyValuePair<string, string> Entry(
        string key,
        string value)
        => new(key, value);
}