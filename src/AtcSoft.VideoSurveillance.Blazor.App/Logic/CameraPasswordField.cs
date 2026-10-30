namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The password box of the camera form. The server never sends a camera's
/// password back, so the box starts empty when a camera is edited.
/// </summary>
public static class CameraPasswordField
{
    /// <summary>
    /// What to send for the password box: an empty box is left out, so the
    /// stored password is kept.
    /// </summary>
    public static string? ToSend(string? typed)
        => string.IsNullOrEmpty(typed) ? null : typed;
}