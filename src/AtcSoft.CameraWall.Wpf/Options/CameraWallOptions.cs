namespace AtcSoft.CameraWall.Wpf.Options;

/// <summary>
/// Configuration options for the camera wall library.
/// </summary>
[OptionsBinding("CameraWall", ValidateDataAnnotations = true, ValidateOnStart = true)]
public partial class CameraWallOptions
{
    /// <summary>
    /// Gets or sets the theme base (Dark or Light).
    /// </summary>
    [Required]
    public string ThemeBase { get; set; } = "Dark";

    /// <summary>
    /// Gets or sets the theme accent color.
    /// </summary>
    [Required]
    public string ThemeAccent { get; set; } = "Blue";

    /// <summary>
    /// Gets or sets the UI language.
    /// </summary>
    [Required]
    public string Language { get; set; } = "en-US";
}