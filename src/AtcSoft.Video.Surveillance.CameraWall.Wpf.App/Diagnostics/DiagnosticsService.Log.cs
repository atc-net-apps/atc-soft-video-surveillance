namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App.Diagnostics;

public sealed partial class DiagnosticsService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Diagnostics on: a system report in {ReportPath}, health samples every {Seconds} s in {Folder}")]
    private partial void LogDiagnosticsStarted(string reportPath, int seconds, string folder);

    [LoggerMessage(Level = LogLevel.Information, Message = "System report:\n{Report}")]
    private partial void LogSystemReport(string report);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The system report could not be written")]
    private partial void LogSystemReportFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Health: CPU {Cpu:0.0} %, working set {WorkingSetMb} MB, private {PrivateMb} MB, managed {ManagedMb} MB, GC {Gen0}/{Gen1}/{Gen2} (pause {GcPauseMs} ms), handles {Handles}, GDI {Gdi}, USER {User}, threads {Threads}, free memory {AvailableMb} MB, slowest UI answer {UiDelayMs} ms, recordings {Recordings}, recording disk free {DiskFreeGb} GB")]
    private partial void LogHealth(double cpu, long workingSetMb, long privateMb, long managedMb, int gen0, int gen1, int gen2, long gcPauseMs, int handles, int gdi, int user, int threads, long availableMb, long uiDelayMs, int recordings, string diskFreeGb);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recording '{Camera}': {State}, {File} {SizeMb:0.0} MB (+{GrowthKb} KB), {Fps:0.0} fps, {Frames} frames, last packet {PacketAgeSeconds:0.0} s ago")]
    private partial void LogRecordingSample(string camera, RecordingGrowthState state, string file, double sizeMb, long growthKb, double fps, long frames, double packetAgeSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recording '{Camera}' has not grown for {Minutes:0.0} minutes: {File} ({SizeMb:0.0} MB, last packet {PacketAgeSeconds:0.0} s ago)")]
    private partial void LogRecordingStalled(string camera, double minutes, string file, double sizeMb, double packetAgeSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording '{Camera}' grows again: {File}")]
    private partial void LogRecordingResumed(string camera, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No video from '{Camera}' for {Seconds:0} s while it records")]
    private partial void LogNoPackets(string camera, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The window did not answer for {Seconds:0.0} s")]
    private partial void LogUiFrozen(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The window has not answered for {Seconds:0} s")]
    private partial void LogUiStillFrozen(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A health sample failed")]
    private partial void LogSampleFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "The recording worker has not passed for {Minutes:N1} min: segments, the disk space guard and restarting recordings are stuck")]
    private partial void LogRecordingWorkerStale(double minutes);
}