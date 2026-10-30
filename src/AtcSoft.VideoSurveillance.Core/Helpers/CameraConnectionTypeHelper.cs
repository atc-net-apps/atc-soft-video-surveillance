namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Rules for the camera connection type: which value is displayed, where the
/// connection-type icon goes, and how detected values are applied.
/// </summary>
public static class CameraConnectionTypeHelper
{
    /// <summary>
    /// Gets the connection type to display. USB cameras are always
    /// <see cref="CameraConnectionType.Usb"/>; network cameras use the stored
    /// value, except that a stored <see cref="CameraConnectionType.Usb"/> is
    /// treated as <see cref="CameraConnectionType.None"/>.
    /// </summary>
    public static CameraConnectionType GetEffective(
        ConnectionSettings connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Source == CameraSource.Usb)
        {
            return CameraConnectionType.Usb;
        }

        return connection.ConnectionType == CameraConnectionType.Usb
            ? CameraConnectionType.None
            : connection.ConnectionType;
    }

    /// <summary>
    /// Resolves the tile corner for the connection-type icon. The result is
    /// always adjacent to <paramref name="infoBoxPosition"/>, never the info
    /// box corner itself nor its diagonal (the quick-actions corner).
    /// </summary>
    public static OverlayPosition ResolveIconCorner(
        OverlayPosition infoBoxPosition,
        ConnectionTypeIconPlacement placement)
    {
        var isTop = infoBoxPosition is OverlayPosition.TopLeft or OverlayPosition.TopRight;
        var isLeft = infoBoxPosition is OverlayPosition.TopLeft or OverlayPosition.BottomLeft;

        if (placement == ConnectionTypeIconPlacement.SameColumn)
        {
            isTop = !isTop;
        }
        else
        {
            isLeft = !isLeft;
        }

        return ToCorner(isTop, isLeft);
    }

    /// <summary>
    /// Gets the corner diagonally opposite <paramref name="corner"/>.
    /// </summary>
    public static OverlayPosition GetDiagonalCorner(OverlayPosition corner)
        => corner switch
        {
            OverlayPosition.TopLeft => OverlayPosition.BottomRight,
            OverlayPosition.TopRight => OverlayPosition.BottomLeft,
            OverlayPosition.BottomLeft => OverlayPosition.TopRight,
            _ => OverlayPosition.TopLeft,
        };

    /// <summary>
    /// Applies a detected connection type to the current value.
    /// Implicit detection (on add / on connect) only fills in a value that is
    /// still <see cref="CameraConnectionType.None"/>. Explicit detection (the
    /// user asked for it) may overwrite. A failed detection
    /// (<see cref="CameraConnectionType.None"/>) never clears a value.
    /// </summary>
    public static CameraConnectionType ApplyDetected(
        CameraConnectionType current,
        CameraConnectionType detected,
        bool isExplicit)
    {
        if (detected == CameraConnectionType.None)
        {
            return current;
        }

        return isExplicit || current == CameraConnectionType.None
            ? detected
            : current;
    }

    private static OverlayPosition ToCorner(
        bool isTop,
        bool isLeft)
    {
        if (isTop)
        {
            return isLeft ? OverlayPosition.TopLeft : OverlayPosition.TopRight;
        }

        return isLeft ? OverlayPosition.BottomLeft : OverlayPosition.BottomRight;
    }
}