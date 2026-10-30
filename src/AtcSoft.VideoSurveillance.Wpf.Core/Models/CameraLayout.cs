using CoreModels = AtcSoft.VideoSurveillance.Models;

namespace AtcSoft.VideoSurveillance.Wpf.Core.Models;

/// <summary>
/// Wraps <see cref="CoreModels.CameraLayout"/> with change notification for WPF binding.
/// </summary>
public partial class CameraLayout : ObservableObject
{
    internal CoreModels.CameraLayout Core { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraLayout"/> class.
    /// </summary>
    public CameraLayout()
        : this(new CoreModels.CameraLayout())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraLayout"/> class
    /// wrapping the specified Core instance.
    /// </summary>
    internal CameraLayout(CoreModels.CameraLayout core)
    {
        Core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Gets or sets the unique identifier for the layout.
    /// </summary>
    public Guid Id
    {
        get => Core.Id;
        set => Core.Id = value;
    }

    /// <summary>
    /// Gets or sets the name of the layout.
    /// </summary>
    public string Name
    {
        get => Core.Name;
        set
        {
            if (string.Equals(Core.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            Core.Name = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the list of camera positions in this layout.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Required for JSON deserialization")]
    public List<CameraLayoutItem> Items
    {
        get => Core.Items;
        set => Core.Items = value;
    }

    /// <summary>
    /// Gets or sets the date and time when the layout was created.
    /// </summary>
    public DateTime CreatedAt
    {
        get => Core.CreatedAt;
        set => Core.CreatedAt = value;
    }

    /// <summary>
    /// Gets or sets the date and time when the layout was last modified.
    /// </summary>
    public DateTime? ModifiedAt
    {
        get => Core.ModifiedAt;
        set => Core.ModifiedAt = value;
    }

    /// <summary>
    /// Returns the name of the layout.
    /// </summary>
    /// <returns>The layout name.</returns>
    public override string ToString()
        => Name;
}