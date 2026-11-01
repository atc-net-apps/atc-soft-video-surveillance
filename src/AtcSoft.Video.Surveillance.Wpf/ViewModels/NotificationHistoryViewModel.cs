namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// View model for the notification history view.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class NotificationHistoryViewModel : ViewModelBase
{
    private readonly NotificationCoordinator coordinator;
    private ObservableCollection<string> selectedEventTypes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHistoryViewModel"/> class.
    /// </summary>
    public NotificationHistoryViewModel(NotificationCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        this.coordinator = coordinator;

        // A view of its own: the history itself stays whole for whoever else reads it.
        EntriesView = new System.Windows.Data.ListCollectionView(coordinator.History)
        {
            Filter = item => item is NotificationEntry entry && IsShown(entry),
        };

        coordinator.History.CollectionChanged += OnHistoryChanged;
        selectedEventTypes.CollectionChanged += OnSelectedEventTypesChanged;

        // The language changes live; CultureManager holds the handler weakly.
        CultureManager.UiCultureChanged += OnUiCultureChanged;
    }

    /// <summary>
    /// Gets the notification history entries.
    /// </summary>
    public ObservableCollection<NotificationEntry> Entries
        => coordinator.History;

    /// <summary>
    /// Gets the entries of the kinds ticked in the filter, newest first.
    /// </summary>
    public System.Windows.Data.ListCollectionView EntriesView { get; }

    /// <summary>
    /// Gets the kinds of notification, for the filter chips.
    /// </summary>
    public IDictionary<string, string> EventTypes
        => NotificationEventTypeText.All.ToDictionary(
            t => t.ToString(),
            NotificationEventTypeText.For,
            StringComparer.Ordinal);

    /// <summary>
    /// Gets how many notifications of each kind the history has, shown on the chips.
    /// </summary>
    public IDictionary<string, int> EventTypeCounts
        => NotificationEventTypeText.All.ToDictionary(
            t => t.ToString(),
            t => coordinator.History.Count(e => e.EventType == t),
            StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the kinds of notification shown; none ticked shows every kind.
    /// </summary>
    public ObservableCollection<string> SelectedEventTypes
    {
        get => selectedEventTypes;
        set
        {
            value ??= [];
            if (ReferenceEquals(value, selectedEventTypes))
            {
                return;
            }

            selectedEventTypes.CollectionChanged -= OnSelectedEventTypesChanged;
            selectedEventTypes = value;
            selectedEventTypes.CollectionChanged += OnSelectedEventTypesChanged;
            OnPropertyChanged();
            EntriesView.Refresh();
        }
    }

    private bool IsShown(NotificationEntry entry)
        => selectedEventTypes.Count == 0 ||
           selectedEventTypes.Contains(entry.EventType.ToString(), StringComparer.Ordinal);

    private void OnSelectedEventTypesChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
        => EntriesView.Refresh();

    private void OnHistoryChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(EventTypeCounts));

    private void OnUiCultureChanged(
        object? sender,
        UiCultureEventArgs e)
    {
        OnPropertyChanged(nameof(EventTypes));
        EntriesView.Refresh();
    }

    [RelayCommand]
    private void ClearHistory()
        => coordinator.History.Clear();
}