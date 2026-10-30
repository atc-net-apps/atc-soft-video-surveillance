namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// Native Media Foundation P/Invoke surface used by
/// <see cref="MediaFoundation.MediaFoundationEnumerator"/>.
/// Kept tiny — only the entry points we need to enumerate USB
/// cameras and read their friendly-name + symbolic-link attributes.
/// </summary>
[SuppressMessage("Style", "SA1310:Field names should not contain underscore", Justification = "Mirrors Win32 GUID names")]
[SuppressMessage("Performance", "SYSLIB1054", Justification = "These specific Media Foundation entry points have ABI quirks (mfplat.dll loaded on demand, IMFActivate ref-counting) that the LibraryImport source generator does not yet model correctly. Keeping classic DllImport.")]
[SuppressMessage("Style", "SA1134:Each attribute should be placed on its own line of code", Justification = "COM vtable slot definitions are vastly more readable as one line per slot — splitting [PreserveSig] across two lines triples the file length without aiding comprehension.")]
[SuppressMessage("Style", "ATC202:Multi parameters should be broken down to separate lines", Justification = "Same rationale as SA1134 — COM vtable slot signatures stay readable when kept on a single line per slot.")]
[SuppressMessage("Style", "ATC201:Single parameter should be on a new line when the method declaration exceeds 80 characters", Justification = "Same rationale as SA1134 / ATC202 — COM vtable slot signatures stay readable when kept on a single line per slot.")]
[SuppressMessage("Style", "SA1516:Elements should be separated by blank line", Justification = "COM vtable slot ordering is significant; blank lines between every slot would obscure the layout. The interface clusters them visually instead.")]
internal static class MediaFoundationInterop
{
    public const int S_OK = 0;
    public const uint MF_VERSION = 0x0002_0070;
    public const uint MF_E_ATTRIBUTENOTFOUND = 0xC00D36E6;

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE =
        new("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID =
        new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME =
        new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SYMBOLIC_LINK =
        new("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");

    public static readonly Guid MF_MT_FRAME_SIZE =
        new("1652c33d-d6b2-4012-b834-72030849a37d");

    public static readonly Guid MF_MT_FRAME_RATE =
        new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");

    public static readonly Guid MF_MT_SUBTYPE =
        new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

    public static readonly Guid IID_IMFMediaSource =
        new("279a808d-aec7-40c8-9c6b-a6b492c78a66");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(
        uint version,
        uint flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateAttributes(
        out IMFAttributes ppMFAttributes,
        uint cInitialSize);

    /// <summary>
    /// Raw-pointer overload used by the device probe — keeping the
    /// IMFAttributes COM object as an <see cref="IntPtr"/> avoids the
    /// <c>[ComImport]</c> RCW round-trip that, on .NET 10, has been
    /// observed to drop the <c>MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE</c>
    /// attribute between <c>SetGUID</c> and the subsequent
    /// <see cref="MFEnumDeviceSources(IntPtr, out IntPtr, out uint)"/>
    /// call (manifests as <c>MF_E_ATTRIBUTENOTFOUND</c> from
    /// <c>MFEnumDeviceSources</c>).
    /// </summary>
    [DllImport("mfplat.dll", EntryPoint = "MFCreateAttributes", ExactSpelling = true)]
    public static extern int MFCreateAttributesRaw(
        out IntPtr ppMFAttributes,
        uint cInitialSize);

    [DllImport("mf.dll", ExactSpelling = true)]
    public static extern int MFEnumDeviceSources(
        IntPtr pAttributes,
        out IntPtr pppSourceActivate,
        out uint pcSourceActivate);

    [DllImport("ole32.dll", ExactSpelling = true)]
    public static extern void CoTaskMemFree(IntPtr pv);

    [ComImport]
    [Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFAttributes
    {
        // We only call SetGUID / GetString / GetStringLength slots,
        // but COM requires the entire vtable in declaration order;
        // unused slots are declared with PreserveSig + minimal types
        // so the layout matches.
        [PreserveSig]
        int GetItem(
            ref Guid guidKey,
            IntPtr pValue);

        [PreserveSig]
        int GetItemType(
            ref Guid guidKey,
            out int pType);

        [PreserveSig]
        int CompareItem(
            ref Guid guidKey,
            IntPtr value,
            out bool pbResult);

        [PreserveSig]
        int Compare(
            IMFAttributes theirs,
            int matchType,
            out bool pbResult);

        [PreserveSig]
        int GetUINT32(
            ref Guid guidKey,
            out uint punValue);

        [PreserveSig]
        int GetUINT64(
            ref Guid guidKey,
            out ulong punValue);

        [PreserveSig]
        int GetDouble(
            ref Guid guidKey,
            out double pfValue);

        [PreserveSig]
        int GetGUID(
            ref Guid guidKey,
            out Guid pguidValue);

        [PreserveSig]
        int GetStringLength(
            ref Guid guidKey,
            out uint pcchLength);

        [PreserveSig]
        int GetString(
            ref Guid guidKey,
            [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue,
            uint cchBufSize,
            out uint pcchLength);

        [PreserveSig]
        int GetAllocatedString(
            ref Guid guidKey,
            out IntPtr ppwszValue,
            out uint pcchLength);

        [PreserveSig]
        int GetBlobSize(
            ref Guid guidKey,
            out uint pcbBlobSize);

        [PreserveSig]
        int GetBlob(
            ref Guid guidKey,
            IntPtr pBuf,
            uint cbBufSize,
            out uint pcbBlobSize);

        [PreserveSig]
        int GetAllocatedBlob(
            ref Guid guidKey,
            out IntPtr ppBuf,
            out uint pcbSize);

        [PreserveSig]
        int GetUnknown(
            ref Guid guidKey,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [PreserveSig]
        int SetItem(
            ref Guid guidKey,
            IntPtr value);

        [PreserveSig]
        int DeleteItem(ref Guid guidKey);

        [PreserveSig]
        int DeleteAllItems();

        [PreserveSig]
        int SetUINT32(
            ref Guid guidKey,
            uint unValue);

        [PreserveSig]
        int SetUINT64(
            ref Guid guidKey,
            ulong unValue);

        [PreserveSig]
        int SetDouble(
            ref Guid guidKey,
            double fValue);

        [PreserveSig]
        int SetGUID(
            ref Guid guidKey,
            ref Guid guidValue);

        [PreserveSig]
        int SetString(
            ref Guid guidKey,
            [MarshalAs(UnmanagedType.LPWStr)] string wszValue);

        [PreserveSig]
        int SetBlob(
            ref Guid guidKey,
            IntPtr pBuf,
            uint cbBufSize);

        [PreserveSig]
        int SetUnknown(
            ref Guid guidKey,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);

        [PreserveSig]
        int LockStore();

        [PreserveSig]
        int UnlockStore();

        [PreserveSig]
        int GetCount(out uint pcItems);

        [PreserveSig]
        int GetItemByIndex(
            uint unIndex,
            out Guid pguidKey,
            IntPtr pValue);

        [PreserveSig]
        int CopyAllItems(IMFAttributes pDest);
    }

    [ComImport]
    [Guid("7fee9e9a-4a89-47a6-899c-b6a53a70fb67")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFActivate
    {
        // IMFAttributes vtable (slots 3-37) — we don't call any of
        // them directly on the activate; they exist purely so the
        // vtable layout is correct for the IMFActivate slots that
        // follow.
        [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool pbResult);
        [PreserveSig] int GetUINT32(ref Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(ref Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(ref Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
        [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems(IntPtr pDest);

        [PreserveSig]
        int ActivateObject(
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [PreserveSig] int ShutdownObject();
        [PreserveSig] int DetachObject();
    }

    [ComImport]
    [Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaSource
    {
        [PreserveSig] int GetEvent(uint dwFlags, [MarshalAs(UnmanagedType.IUnknown)] out object ppEvent);
        [PreserveSig] int BeginGetEvent(IntPtr pCallback, IntPtr punkState);
        [PreserveSig] int EndGetEvent(IntPtr pResult, [MarshalAs(UnmanagedType.IUnknown)] out object ppEvent);
        [PreserveSig] int QueueEvent(uint met, ref Guid extType, int hrStatus, IntPtr pvValue);
        [PreserveSig] int GetCharacteristics(out uint pdwCharacteristics);

        [PreserveSig]
        int CreatePresentationDescriptor(
            [MarshalAs(UnmanagedType.IUnknown)] out object ppPresentationDescriptor);

        [PreserveSig] int Start(IntPtr pPresentationDescriptor, ref Guid pguidTimeFormat, IntPtr pvarStartPosition);
        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Shutdown();
    }

    [ComImport]
    [Guid("03cb2711-24d7-4db6-a17f-f3a7a479a536")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFPresentationDescriptor
    {
        [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool pbResult);
        [PreserveSig] int GetUINT32(ref Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(ref Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(ref Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
        [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems(IntPtr pDest);

        [PreserveSig] int GetStreamDescriptorCount(out uint pdwDescriptorCount);

        [PreserveSig]
        int GetStreamDescriptorByIndex(
            uint dwIndex,
            out bool pfSelected,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppDescriptor);

        [PreserveSig] int SelectStream(uint dwDescriptorIndex);
        [PreserveSig] int DeselectStream(uint dwDescriptorIndex);
        [PreserveSig] int Clone([MarshalAs(UnmanagedType.IUnknown)] out object ppPresentationDescriptor);
    }

    [ComImport]
    [Guid("56c03d9c-9dbb-45f5-ab4b-d80f47c05938")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFStreamDescriptor
    {
        [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, out bool pbResult);
        [PreserveSig] int Compare(IntPtr theirs, int matchType, out bool pbResult);
        [PreserveSig] int GetUINT32(ref Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(ref Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString(ref Guid guidKey, IntPtr pwszValue, uint cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(ref Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize(ref Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(ref Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
        [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(ref Guid guidKey, IntPtr pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems(IntPtr pDest);

        [PreserveSig] int GetStreamIdentifier(out uint pdwStreamIdentifier);

        [PreserveSig]
        int GetMediaTypeHandler(
            [MarshalAs(UnmanagedType.IUnknown)] out object ppMediaTypeHandler);
    }

    [ComImport]
    [Guid("e93dcf6c-4b07-4e1e-8123-aa16ed6eadf5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMFMediaTypeHandler
    {
        [PreserveSig] int IsMediaTypeSupported(IntPtr pMediaType, IntPtr ppMediaType);
        [PreserveSig] int GetMediaTypeCount(out uint pdwTypeCount);

        [PreserveSig]
        int GetMediaTypeByIndex(
            uint dwIndex,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppType);

        [PreserveSig] int SetCurrentMediaType(IntPtr pMediaType);
        [PreserveSig] int GetCurrentMediaType([MarshalAs(UnmanagedType.IUnknown)] out object ppMediaType);
        [PreserveSig] int GetMajorType(out Guid pguidMajorType);
    }
}