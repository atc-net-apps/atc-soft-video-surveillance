namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// A Windows job object that ends every process in it when its last handle closes: when it is
/// disposed, and when the process that holds it ends in any way (a crash, being killed in Task
/// Manager). A helper process (FFmpeg) put in it can never outlive its owner.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class KillOnCloseJob : IDisposable
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private readonly SafeJobHandle handle;

    private KillOnCloseJob(SafeJobHandle handle)
        => this.handle = handle;

    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name="error">Why it could not be created.</param>
    /// <returns>The job; <c>null</c> when Windows refused it.</returns>
    public static KillOnCloseJob? TryCreate(out string? error)
    {
        var job = CreateJobObjectW(nint.Zero, null);
        if (job.IsInvalid)
        {
            error = new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()).Message;
            job.Dispose();
            return null;
        }

        var limits = new JobObjectExtendedLimitInformationData
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };

        if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<JobObjectExtendedLimitInformationData>()))
        {
            error = new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()).Message;
            job.Dispose();
            return null;
        }

        error = null;
        return new KillOnCloseJob(job);
    }

    /// <summary>
    /// Puts a process in the job.
    /// </summary>
    /// <param name="process">A process that was just started.</param>
    /// <param name="error">Why it could not be put in the job.</param>
    /// <returns><c>false</c> when Windows refused (the process may then outlive its owner).</returns>
    public bool TryAdd(
        Process process,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            if (AssignProcessToJobObject(handle, process.Handle))
            {
                error = null;
                return true;
            }

            error = new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()).Message;
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ObjectDisposedException)
        {
            // The process ended already, or the job was closed.
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Closes the job, which ends every process still in it.
    /// </summary>
    public void Dispose()
        => handle.Dispose();

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial SafeJobHandle CreateJobObjectW(
        nint jobAttributes,
        string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
        SafeJobHandle job,
        int infoClass,
        ref JobObjectExtendedLimitInformationData info,
        uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(
        SafeJobHandle job,
        nint process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformationData
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
            => CloseHandle(handle);
    }
}