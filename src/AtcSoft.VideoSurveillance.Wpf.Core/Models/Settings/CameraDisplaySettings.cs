using CoreSettings = AtcSoft.VideoSurveillance.Models.Settings;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models.Settings;

/// <summary>
/// Wraps <see cref="CoreSettings.CameraDisplaySettings"/> with change notification for WPF binding.
/// </summary>
public partial class CameraDisplaySettings : ObservableObject
{
    internal CoreSettings.CameraDisplaySettings Core { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraDisplaySettings"/> class.
    /// </summary>
    public CameraDisplaySettings()
        : this(new CoreSettings.CameraDisplaySettings())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraDisplaySettings"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal CameraDisplaySettings(CoreSettings.CameraDisplaySettings core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Required(ErrorMessageResourceType = typeof(Translations), ErrorMessageResourceName = nameof(Translations.DisplayNameRequired))]
    [StringLength(256, ErrorMessageResourceType = typeof(Translations), ErrorMessageResourceName = nameof(Translations.DisplayNameTooLong))]
    public string DisplayName
    {
        get => Core.DisplayName;
        set
        {
            if (string.Equals(Core.DisplayName, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.DisplayName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string? Description
    {
        get => Core.Description;
        set
        {
            if (string.Equals(Core.Description, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.Description = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the overlay position.
    /// </summary>
    public OverlayPosition OverlayPosition
    {
        get => Core.OverlayPosition;
        set
        {
            if (Core.OverlayPosition == value)
            {
                return;
            }

            Core.OverlayPosition = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the clockwise rotation applied to this camera's stream.
    /// </summary>
    public CameraRotation Rotation
    {
        get => Core.Rotation;
        set
        {
            if (Core.Rotation == value)
            {
                return;
            }

            Core.Rotation = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Creates a deep copy of this instance.
    /// </summary>
    public CameraDisplaySettings Clone()
        => new(Core.Clone());

    /// <summary>
    /// Copies values from another instance.
    /// </summary>
    public void CopyFrom(CameraDisplaySettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        DisplayName = source.DisplayName;
        Description = source.Description;
        OverlayPosition = source.OverlayPosition;
        Rotation = source.Rotation;
    }

    /// <summary>
    /// Determines whether the specified instance has the same values.
    /// </summary>
    public bool ValueEquals(CameraDisplaySettings? other)
        => other is not null && Core.ValueEquals(other.Core);
}