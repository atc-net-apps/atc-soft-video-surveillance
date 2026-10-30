namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the layout edit dialog.
/// </summary>
public partial class LayoutEditDialogViewModel : ViewModelBase
{
    private readonly Guid? layoutId;

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    [ObservableProperty(AfterChangedCallback = nameof(OnFormChanged))]
    private string layoutName = string.Empty;

    [ObservableProperty]
    private int rows = 2;

    [ObservableProperty]
    private int columns = 2;

    /// <summary>
    /// Gets whether this is an edit operation.
    /// </summary>
    public bool IsEdit { get; }

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string DialogTitle
        => IsEdit ? Translations.EditLayout : Translations.AddLayout;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutEditDialogViewModel"/> class for adding a new layout.
    /// </summary>
    public LayoutEditDialogViewModel()
    {
        IsEdit = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutEditDialogViewModel"/> class for editing an existing layout.
    /// </summary>
    public LayoutEditDialogViewModel(Layout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        IsEdit = true;
        layoutId = layout.Id;
        layoutName = layout.Name;
        rows = layout.Rows;
        columns = layout.Columns;
    }

    /// <summary>
    /// Builds a <see cref="CreateLayoutRequest"/> from the form fields.
    /// </summary>
    public CreateLayoutRequest BuildCreateRequest()
        => new(
            Name: LayoutName,
            Rows: Rows,
            Columns: Columns);

    /// <summary>
    /// Builds an <see cref="UpdateLayoutRequest"/> from the form fields.
    /// </summary>
    public UpdateLayoutRequest BuildUpdateRequest()
        => new(
            Name: LayoutName,
            Rows: Rows,
            Columns: Columns,
            Cameras: null!);

    /// <summary>
    /// Gets the layout ID for update operations.
    /// </summary>
    public Guid? LayoutId => layoutId;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));

    private bool CanSave()
        => !string.IsNullOrWhiteSpace(LayoutName);

    private static void OnFormChanged()
        => CommandManager.InvalidateRequerySuggested();
}