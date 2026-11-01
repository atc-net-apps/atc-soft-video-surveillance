namespace AtcSoft.Video.Surveillance.Windows.Diagnostics;

/// <summary>
/// Counters of this process and the machine that the managed runtime does not give: the GDI and
/// USER objects the process holds (a leak of either ends a WPF app at 10,000) and the physical memory.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class ProcessMetrics
{
    private const uint GdiObjects = 0;
    private const uint UserObjects = 1;

    /// <summary>
    /// Gets the GDI and USER objects a process holds.
    /// </summary>
    /// <param name="process">The process, usually the current one.</param>
    /// <returns>The counts.</returns>
    public static (int Gdi, int User) GuiObjects(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return ((int)GetGuiResources(process.Handle, GdiObjects), (int)GetGuiResources(process.Handle, UserObjects));
    }

    /// <summary>
    /// Gets the machine's physical memory, in total and available.
    /// </summary>
    /// <returns>The bytes, or <see langword="null"/> when Windows does not say.</returns>
    public static (ulong TotalBytes, ulong AvailableBytes)? SystemMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? (status.TotalPhys, status.AvailPhys)
            : null;
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetGuiResources(
        nint process,
        uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}