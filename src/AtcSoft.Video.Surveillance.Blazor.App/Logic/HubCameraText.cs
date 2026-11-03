namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// What the camera hub dialog says: how the listing went, why a camera cannot be added, a
/// camera's size and frame rate, and its picture as an image address.
/// </summary>
public static class HubCameraText
{
    public static string Status(HubProbeResult listing)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return listing.Outcome.Value switch
        {
            HubProbeResultOutcome.Names.Listed => string.Format(CultureInfo.CurrentCulture, Translations.CameraHubListed1, listing.Cameras.Count),
            HubProbeResultOutcome.Names.SignInRefused => Translations.CameraHubSignInRefused,
            HubProbeResultOutcome.Names.NotThisKind => Translations.CameraHubNotMotionEye,
            _ => Translations.CameraHubUnreachable,
        };
    }

    public static string? Problem(HubCameraInfoProblem problem)
        => problem.Value switch
        {
            HubCameraInfoProblem.Names.None => null,
            HubCameraInfoProblem.Names.Disabled => Translations.HubCameraDisabled,
            HubCameraInfoProblem.Names.StreamingOff => Translations.HubCameraStreamingOff,
            HubCameraInfoProblem.Names.OnAnotherHub => Translations.HubCameraOnAnotherHub,

            // A reason this client does not know yet: it cannot be added from here.
            _ => problem.Value,
        };

    public static string Details(HubCameraInfo camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        return string.Join(
            ", ",
            new[]
            {
                camera.Resolution,
                camera.FramesPerSecond is { } fps ? fps.ToString(CultureInfo.CurrentCulture) + " fps" : null,
                camera.IsUsb ? "USB" : null,
            }.Where(s => !string.IsNullOrEmpty(s)));
    }

    public static string? PictureAddress(byte[]? picture)
        => picture is { Length: > 0 }
            ? "data:image/jpeg;base64," + Convert.ToBase64String(picture)
            : null;
}