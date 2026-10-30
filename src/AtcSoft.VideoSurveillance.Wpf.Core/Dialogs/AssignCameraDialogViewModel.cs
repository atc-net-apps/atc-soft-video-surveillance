namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// View model for the Assign Camera to Layout dialog.
/// Uses <see cref="DualListSelector"/> for dual-list management.
/// </summary>
public partial class AssignCameraDialogViewModel : ViewModelDialogBase
{
    private readonly List<Guid> originalAssignedCameraIds;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignCameraDialogViewModel"/> class.
    /// </summary>
    /// <param name="layoutName">The name of the layout being edited.</param>
    /// <param name="availableCameras">Cameras not currently in the layout.</param>
    /// <param name="assignedCameras">Cameras currently in the layout (in order).</param>
    public AssignCameraDialogViewModel(
        string layoutName,
        IEnumerable<CameraConfiguration> availableCameras,
        IEnumerable<CameraConfiguration> assignedCameras)
    {
        ArgumentNullException.ThrowIfNull(layoutName);
        ArgumentNullException.ThrowIfNull(availableCameras);
        ArgumentNullException.ThrowIfNull(assignedCameras);

        LayoutName = layoutName;

        AvailableItems = new ObservableCollection<DualListSelectorItem>(
            availableCameras
                .OrderBy(c => c.Display.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(c => ToSelectorItem(c)));

        var assignedList = assignedCameras.ToList();
        SelectedItems = new ObservableCollection<DualListSelectorItem>(
            assignedList.Select((c, i) => ToSelectorItem(c, (int?)i)));

        originalAssignedCameraIds = assignedList
            .Select(c => c.Id)
            .ToList();
    }

    /// <summary>
    /// Occurs when the dialog requests to be closed.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public static string DialogTitle => Translations.AssignCameraToLayout;

    /// <summary>
    /// Gets the name of the layout being edited.
    /// </summary>
    public string LayoutName { get; }

    /// <summary>
    /// Gets the header text for the available cameras list.
    /// </summary>
    public static string AvailableHeaderText => Translations.AvailableCameras;

    /// <summary>
    /// Gets the header text for the assigned cameras list.
    /// </summary>
    public static string SelectedHeaderText => Translations.AssignedToLayout;

    /// <summary>
    /// Gets the items for the available list in the <see cref="DualListSelector"/>.
    /// </summary>
    public ObservableCollection<DualListSelectorItem> AvailableItems { get; }

    /// <summary>
    /// Gets the items for the selected list in the <see cref="DualListSelector"/>.
    /// </summary>
    public ObservableCollection<DualListSelectorItem> SelectedItems { get; }

    /// <summary>
    /// Gets a value indicating whether there are actual changes compared to the original state.
    /// </summary>
    /// <returns><c>true</c> if the assigned cameras list differs from the original; otherwise, <c>false</c>.</returns>
    public bool HasActualChanges()
    {
        var currentIds = SelectedItems
            .Select(item => ((CameraConfiguration)item.Tag!).Id)
            .ToList();
        return !originalAssignedCameraIds.SequenceEqual(currentIds);
    }

    /// <summary>
    /// Gets the assigned cameras from the selected items.
    /// </summary>
    /// <returns>A list of camera configurations in their current order.</returns>
    public IReadOnlyList<CameraConfiguration> GetAssignedCameras()
        => SelectedItems
            .Select(item => (CameraConfiguration)item.Tag!)
            .ToList();

    [RelayCommand]
    private void Ok()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));

    private static DualListSelectorItem ToSelectorItem(
        CameraConfiguration camera,
        int? sortOrder = null)
        => new()
        {
            Identifier = camera.Id.ToString(),
            Name = camera.Display.DisplayName,
            Description = camera.Display.Description,
            Tag = camera,
            SortOrderNumber = sortOrder,
        };
}