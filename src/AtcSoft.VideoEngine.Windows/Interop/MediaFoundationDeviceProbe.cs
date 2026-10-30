namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// Real <see cref="IMfDeviceProbe"/> backed by Media Foundation's
/// <c>MFEnumDeviceSources</c>. Holds an MF lifetime reference for the
/// duration of each probe call so callers don't have to coordinate
/// startup/shutdown.
/// </summary>
internal sealed class MediaFoundationDeviceProbe : IMfDeviceProbe
{
    public IReadOnlyList<MfDeviceRow> EnumerateVideoCaptureDevices()
    {
        // Media Foundation's device-source enumeration (and the
        // IMFActivate / IMFMediaSource objects it spawns) misbehaves
        // on a WPF STA thread: MFEnumDeviceSources returns
        // MF_E_ATTRIBUTENOTFOUND because the cross-apartment proxy
        // through which the attribute store is accessed loses the
        // SetGUID write. Run the whole probe on a dedicated MTA worker
        // thread; the resulting MfDeviceRow values are pure-managed so
        // marshalling them back is trivial.
        IReadOnlyList<MfDeviceRow>? result = null;
        ExceptionDispatchInfo? capturedError = null;

        var worker = new Thread(() =>
        {
            try
            {
                MediaFoundationLifetime.Acquire();
                try
                {
                    result = EnumerateInternal();
                }
                finally
                {
                    MediaFoundationLifetime.Release();
                }
            }
            catch (Exception ex)
            {
                capturedError = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = "MF.DeviceProbe",
        };

        worker.SetApartmentState(ApartmentState.MTA);
        worker.Start();
        worker.Join();

        capturedError?.Throw();
        return result ?? [];
    }

    private static IReadOnlyList<MfDeviceRow> EnumerateInternal()
    {
        // Keep the attribute COM object as a raw IntPtr — the
        // [ComImport] RCW path drops the source-type attribute on
        // .NET 10 (SetGUID returns S_OK but a subsequent read returns
        // MF_E_ATTRIBUTENOTFOUND). Direct vtable invocation for
        // SetGUID and an IntPtr hand-off to MFEnumDeviceSources sidestep
        // the marshaller entirely.
        var hr = MediaFoundationInterop.MFCreateAttributesRaw(out var pAttrs, 1);
        if (hr < 0 || pAttrs == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"MFCreateAttributes failed with HRESULT 0x{hr:X8}.");
        }

        try
        {
            var sourceTypeKey = MediaFoundationInterop.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE;
            var vidcap = MediaFoundationInterop.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
            hr = SetGuidViaVtable(pAttrs, ref sourceTypeKey, ref vidcap);
            if (hr < 0)
            {
                throw new InvalidOperationException(
                    $"IMFAttributes.SetGUID(VIDCAP) failed with HRESULT 0x{hr:X8}.");
            }

            hr = MediaFoundationInterop.MFEnumDeviceSources(pAttrs, out var pActivates, out var count);
            if (hr < 0)
            {
                throw new InvalidOperationException(
                    $"MFEnumDeviceSources failed with HRESULT 0x{hr:X8}.");
            }

            if (count == 0 || pActivates == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                return ReadActivateArray(pActivates, count);
            }
            finally
            {
                MediaFoundationInterop.CoTaskMemFree(pActivates);
            }
        }
        finally
        {
            _ = Marshal.Release(pAttrs);
        }
    }

    /// <summary>
    /// Calls <c>IMFAttributes::SetGUID</c> by reading the COM vtable
    /// directly. <c>IMFAttributes</c> inherits from <c>IUnknown</c>
    /// (3 slots), and <c>SetGUID</c> is the 22nd <c>IMFAttributes</c>
    /// method — vtable index 24 from the start of the object.
    /// </summary>
    private static unsafe int SetGuidViaVtable(
        IntPtr pAttrs,
        ref Guid guidKey,
        ref Guid guidValue)
    {
        const int SetGuidVtableIndex = 24;
        var vtable = *(IntPtr**)pAttrs;
        var pSetGuid = vtable[SetGuidVtableIndex];
        var setGuid = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, Guid*, int>)pSetGuid;

        fixed (Guid* pKey = &guidKey)
        {
            fixed (Guid* pValue = &guidValue)
            {
                return setGuid(pAttrs, pKey, pValue);
            }
        }
    }

    private static IReadOnlyList<MfDeviceRow> ReadActivateArray(
        IntPtr pActivates,
        uint count)
    {
        var results = new List<MfDeviceRow>(checked((int)count));

        for (var i = 0; i < count; i++)
        {
            var activatePtr = Marshal.ReadIntPtr(pActivates, i * IntPtr.Size);
            if (activatePtr == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                var attrs = (MediaFoundationInterop.IMFAttributes)Marshal.GetObjectForIUnknown(activatePtr);
                var activate = (MediaFoundationInterop.IMFActivate)attrs;
                try
                {
                    var symbolicLink = ReadString(attrs, MediaFoundationInterop.MF_DEVSOURCE_ATTRIBUTE_SYMBOLIC_LINK);
                    var friendlyName = ReadString(attrs, MediaFoundationInterop.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME);

                    if (!string.IsNullOrWhiteSpace(symbolicLink))
                    {
                        var capabilities = TryReadCapabilities(activate);
                        results.Add(new MfDeviceRow(
                            SymbolicLink: symbolicLink,
                            FriendlyName: friendlyName ?? string.Empty,
                            Capabilities: capabilities));
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(attrs);
                }
            }
            finally
            {
                Marshal.Release(activatePtr);
            }
        }

        return results;
    }

    /// <summary>
    /// Activates the device source long enough to walk its
    /// <see cref="MediaFoundationInterop.IMFMediaTypeHandler"/> and
    /// harvest the (size × frame-rate × pixel-format) tuples it
    /// advertises. Best-effort — a device that throws or refuses to
    /// activate yields an empty capability list rather than failing
    /// the whole enumeration.
    /// </summary>
    private static IReadOnlyList<MfCapability> TryReadCapabilities(
        MediaFoundationInterop.IMFActivate activate)
    {
        try
        {
            var iidMediaSource = MediaFoundationInterop.IID_IMFMediaSource;
            var hr = activate.ActivateObject(ref iidMediaSource, out var sourceObj);
            if (hr < 0 || sourceObj is null)
            {
                return [];
            }

            var source = (MediaFoundationInterop.IMFMediaSource)sourceObj;
            try
            {
                hr = source.CreatePresentationDescriptor(out var pdObj);
                if (hr < 0 || pdObj is null)
                {
                    return [];
                }

                var pd = (MediaFoundationInterop.IMFPresentationDescriptor)pdObj;
                try
                {
                    return ReadCapabilitiesFromPresentationDescriptor(pd);
                }
                finally
                {
                    Marshal.ReleaseComObject(pd);
                }
            }
            finally
            {
                _ = source.Shutdown();
                Marshal.ReleaseComObject(source);
                _ = activate.ShutdownObject();
            }
        }
        catch (COMException)
        {
            return [];
        }
        catch (InvalidCastException)
        {
            return [];
        }
    }

    private static IReadOnlyList<MfCapability> ReadCapabilitiesFromPresentationDescriptor(
        MediaFoundationInterop.IMFPresentationDescriptor pd)
    {
        var hr = pd.GetStreamDescriptorCount(out var streamCount);
        if (hr < 0 || streamCount == 0)
        {
            return [];
        }

        var harvested = new List<MfCapability>();

        for (uint i = 0; i < streamCount; i++)
        {
            hr = pd.GetStreamDescriptorByIndex(i, out _, out var sdObj);
            if (hr < 0 || sdObj is null)
            {
                continue;
            }

            var sd = (MediaFoundationInterop.IMFStreamDescriptor)sdObj;
            try
            {
                hr = sd.GetMediaTypeHandler(out var handlerObj);
                if (hr < 0 || handlerObj is null)
                {
                    continue;
                }

                var handler = (MediaFoundationInterop.IMFMediaTypeHandler)handlerObj;
                try
                {
                    HarvestMediaTypes(handler, harvested);
                }
                finally
                {
                    Marshal.ReleaseComObject(handler);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(sd);
            }
        }

        return harvested;
    }

    private static void HarvestMediaTypes(
        MediaFoundationInterop.IMFMediaTypeHandler handler,
        List<MfCapability> harvested)
    {
        var hr = handler.GetMediaTypeCount(out var typeCount);
        if (hr < 0 || typeCount == 0)
        {
            return;
        }

        for (uint t = 0; t < typeCount; t++)
        {
            hr = handler.GetMediaTypeByIndex(t, out var mtObj);
            if (hr < 0 || mtObj is null)
            {
                continue;
            }

            var mt = (MediaFoundationInterop.IMFAttributes)mtObj;
            try
            {
                var capability = TryBuildCapability(mt);
                if (capability is not null)
                {
                    harvested.Add(capability);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(mt);
            }
        }
    }

    private static MfCapability? TryBuildCapability(
        MediaFoundationInterop.IMFAttributes mt)
    {
        var subtypeKey = MediaFoundationInterop.MF_MT_SUBTYPE;
        var hr = mt.GetGUID(ref subtypeKey, out var subtypeGuid);
        if (hr < 0)
        {
            return null;
        }

        var pixelFormat = AtcSoft.VideoEngine.Windows.MediaFoundation.PixelFormatGuidMapper.Map(subtypeGuid);
        if (pixelFormat is null)
        {
            return null;
        }

        var sizeKey = MediaFoundationInterop.MF_MT_FRAME_SIZE;
        hr = mt.GetUINT64(ref sizeKey, out var packedSize);
        if (hr < 0)
        {
            return null;
        }

        var width = (int)(packedSize >> 32);
        var height = (int)(packedSize & 0xFFFFFFFF);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var rateKey = MediaFoundationInterop.MF_MT_FRAME_RATE;
        hr = mt.GetUINT64(ref rateKey, out var packedRate);
        if (hr < 0)
        {
            // Some media types (especially compressed) omit a frame
            // rate. Treat as 0 — the enumerator caller can default.
            packedRate = 0;
        }

        var numerator = (uint)(packedRate >> 32);
        var denominator = (uint)(packedRate & 0xFFFFFFFF);
        var frameRate = denominator > 0
            ? (double)numerator / denominator
            : 0.0;

        return new MfCapability(width, height, frameRate, pixelFormat);
    }

    private static string? ReadString(
        MediaFoundationInterop.IMFAttributes attrs,
        Guid attributeKey)
    {
        var key = attributeKey;
        var hr = attrs.GetStringLength(ref key, out var length);
        if (unchecked((uint)hr) == MediaFoundationInterop.MF_E_ATTRIBUTENOTFOUND)
        {
            return null;
        }

        if (hr < 0)
        {
            return null;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(checked((int)length) + 1);
        hr = attrs.GetString(ref key, buffer, (uint)buffer.Capacity, out _);
        return hr < 0 ? null : buffer.ToString();
    }
}