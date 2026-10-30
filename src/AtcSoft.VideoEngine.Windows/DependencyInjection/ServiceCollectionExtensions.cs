namespace AtcSoft.VideoEngine.Windows.DependencyInjection;

/// <summary>
/// Registers the Windows-specific USB-camera implementations into a
/// DI container. Hosts on non-Windows platforms can skip this call —
/// the <c>NullUsbCameraEnumerator</c> / <c>NullUsbCameraWatcher</c>
/// fallbacks remain registered so downstream code still composes.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsUsbCameraSupport(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace any prior IUsbCameraEnumerator / IUsbCameraWatcher
        // registration with the Windows implementation. Singleton —
        // both types are stateless apart from MF lifetime / WMI watcher
        // ownership, and DI lifetime should match.
        services.RemoveAll<IUsbCameraEnumerator>();
        services.RemoveAll<IUsbCameraWatcher>();

        services.AddSingleton<IUsbCameraEnumerator, MediaFoundation.MediaFoundationEnumerator>();
        services.AddSingleton<IUsbCameraWatcher, Watchers.WindowsUsbWatcher>();

        return services;
    }

    /// <summary>
    /// Registers the recording thumbnail generator and the builder of a
    /// recording's previews. They draw with GDI+, so a host on another
    /// platform skips this call and records without thumbnails or previews.
    /// </summary>
    public static IServiceCollection AddWindowsRecordingThumbnails(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IThumbnailGeneratorService, Thumbnails.ThumbnailGeneratorService>();
        services.TryAddSingleton<IRecordingPreviewBuilder, Previews.RecordingPreviewBuilder>();

        return services;
    }

    /// <summary>
    /// Registers the reader that turns a captured picture into the
    /// grayscale picture motion detection compares. It decodes with GDI+,
    /// so a host on another platform skips this call and runs without
    /// motion detection.
    /// </summary>
    public static IServiceCollection AddWindowsMotionFrameReader(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMotionFrameReader, Motion.GdiMotionFrameReader>();

        return services;
    }
}