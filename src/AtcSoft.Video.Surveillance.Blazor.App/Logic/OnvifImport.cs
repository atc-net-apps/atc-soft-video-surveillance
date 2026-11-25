namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// What the camera form says and fills in after an ONVIF search or read.
/// </summary>
public static class OnvifImport
{
    /// <summary>
    /// Gets what a search found, in words.
    /// </summary>
    /// <param name="result">The server's answer.</param>
    /// <returns>The text.</returns>
    public static string Describe(OnvifDiscoveryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return !result.CouldProbe
            ? Translations.OnvifNoMulticast
            : result.Devices.Count == 0
                ? Translations.OnvifNoneFound
                : string.Format(CultureInfo.CurrentCulture, Translations.OnvifFound1, result.Devices.Count);
    }

    /// <summary>
    /// Gets how reading a camera went, in words.
    /// </summary>
    /// <param name="result">The server's answer.</param>
    /// <returns>The text.</returns>
    public static string Describe(OnvifProfilesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.Value switch
        {
            OnvifProfilesResultOutcome.Names.Read => result.SubStreamPath is null ? Translations.OnvifReadMainOnly : Translations.OnvifReadWithSubStream,
            OnvifProfilesResultOutcome.Names.Unauthorized => Translations.OnvifUnauthorized,
            OnvifProfilesResultOutcome.Names.Timeout => Translations.OnvifTimeout,
            OnvifProfilesResultOutcome.Names.NoRtspProfile => Translations.OnvifNoRtspProfile,
            _ => Translations.OnvifNotOnvif,
        };
    }

    /// <summary>
    /// Gets a found device as the form lists it.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <returns>Its name, model and address.</returns>
    public static string Describe(OnvifDiscoveredDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return string.Join(" · ", new[] { device.Name, device.Hardware, device.Address }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Gets what a read fills in: RTSP, the main stream's port and path, the sub-stream's path and
    /// port (0 for the main stream's); nothing when the streams were not read.
    /// </summary>
    /// <param name="result">The server's answer.</param>
    /// <returns>The fields, or <see langword="null"/>.</returns>
    public static (int Port, string Path, string SubStreamPath, int SubStreamPort)? Fields(
        OnvifProfilesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome.Value == OnvifProfilesResultOutcome.Names.Read && result.Path is { } path
            ? (result.Port ?? 554, path, result.SubStreamPath ?? string.Empty, result.SubStreamPort ?? 0)
            : null;
    }
}