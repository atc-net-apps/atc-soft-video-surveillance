using CoreSettings = AtcSoft.VideoSurveillance.Models.Settings;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models.Settings;

/// <summary>
/// Wraps <see cref="CoreSettings.AuthenticationSettings"/> with change notification for WPF binding.
/// </summary>
public partial class AuthenticationSettings : ObservableObject
{
    internal CoreSettings.AuthenticationSettings Core { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationSettings"/> class.
    /// </summary>
    public AuthenticationSettings()
        : this(new CoreSettings.AuthenticationSettings())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationSettings"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal AuthenticationSettings(CoreSettings.AuthenticationSettings core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string? UserName
    {
        get => Core.UserName;
        set
        {
            if (string.Equals(Core.UserName, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.UserName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string? Password
    {
        get => Core.Password;
        set
        {
            if (string.Equals(Core.Password, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.Password = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Creates a deep copy of this instance.
    /// </summary>
    public AuthenticationSettings Clone()
        => new(Core.Clone());

    /// <summary>
    /// Copies values from another instance.
    /// </summary>
    public void CopyFrom(AuthenticationSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        UserName = source.UserName;
        Password = source.Password;
    }

    /// <summary>
    /// Determines whether the specified instance has the same values.
    /// </summary>
    public bool ValueEquals(AuthenticationSettings? other)
        => other is not null && Core.ValueEquals(other.Core);
}