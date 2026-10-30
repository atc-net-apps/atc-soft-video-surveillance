namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// Reference-counted <c>MFStartup</c> wrapper. Media Foundation is
/// process-wide and idempotent; this class lets multiple consumers in
/// the same process safely call <see cref="Acquire"/> / <see cref="Release"/>
/// pairs without worrying about who shuts MF down.
/// </summary>
internal static class MediaFoundationLifetime
{
    private static readonly Lock SyncRoot = new();
    private static int refCount;

    public static void Acquire()
    {
        lock (SyncRoot)
        {
            if (refCount == 0)
            {
                var hr = MediaFoundationInterop.MFStartup(MediaFoundationInterop.MF_VERSION, 0);
                if (hr < 0)
                {
                    throw new InvalidOperationException(
                        $"MFStartup failed with HRESULT 0x{hr:X8}.");
                }
            }

            refCount++;
        }
    }

    public static void Release()
    {
        lock (SyncRoot)
        {
            if (refCount == 0)
            {
                return;
            }

            refCount--;
            if (refCount == 0)
            {
                _ = MediaFoundationInterop.MFShutdown();
            }
        }
    }
}