namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// View model for the notification history view.
/// </summary>
public partial class NotificationHistoryViewModel : ViewModelBase
{
    private readonly NotificationCoordinator coordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHistoryViewModel"/> class.
    /// </summary>
    public NotificationHistoryViewModel(NotificationCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        this.coordinator = coordinator;
    }

    /// <summary>
    /// Gets the notification history entries.
    /// </summary>
    public ObservableCollection<NotificationEntry> Entries
        => coordinator.History;

    [RelayCommand]
    private void ClearHistory()
        => coordinator.History.Clear();
}