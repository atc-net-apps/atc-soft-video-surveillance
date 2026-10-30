namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

/// <summary>
/// Extracts vendor-id / product-id from a Windows USB symbolic link
/// such as <c>\\?\usb#vid_046d&amp;pid_085e&amp;mi_00#...</c>.
/// </summary>
internal static class UsbSymbolicLinkParser
{
    public static (string? VendorId, string? ProductId) Parse(
        string symbolicLink)
    {
        if (string.IsNullOrEmpty(symbolicLink))
        {
            return (null, null);
        }

        return (
            Extract(symbolicLink, "vid_"),
            Extract(symbolicLink, "pid_"));
    }

    private static string? Extract(
        string source,
        string prefix)
    {
        var idx = source.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0 || idx + prefix.Length + 4 > source.Length)
        {
            return null;
        }

        var slice = source.AsSpan(idx + prefix.Length, 4);
        for (var i = 0; i < slice.Length; i++)
        {
            if (!IsHexDigit(slice[i]))
            {
                return null;
            }
        }

        return slice.ToString().ToLowerInvariant();
    }

    private static bool IsHexDigit(char c)
        => (c >= '0' && c <= '9') ||
           (c >= 'a' && c <= 'f') ||
           (c >= 'A' && c <= 'F');
}