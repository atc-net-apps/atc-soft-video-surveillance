namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The sub-stream the camera form sends: a low-resolution stream for the live grid and motion
/// detection, given as a path (and a port, 0 for the main stream's). Only a network camera has
/// one. On a new camera an empty path is left out; on an update it is sent as <c>""</c>, which
/// removes the sub-stream on the server.
/// </summary>
public static class SubStreamFields
{
    public static string? Path(
        bool isUsb,
        string path,
        bool isUpdate)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (isUsb)
        {
            return null;
        }

        var trimmed = path.Trim();
        return trimmed.Length > 0 || isUpdate ? trimmed : null;
    }

    public static int? Port(
        bool isUsb,
        string path,
        int port,
        bool isUpdate)
    {
        if (isUsb)
        {
            return null;
        }

        var value = string.IsNullOrWhiteSpace(path) ? 0 : Math.Max(port, 0);
        return value > 0 || isUpdate ? value : null;
    }
}