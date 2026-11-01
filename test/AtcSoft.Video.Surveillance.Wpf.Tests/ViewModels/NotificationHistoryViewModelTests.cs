namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

using AtcSoft.Video.Surveillance.Wpf.Models;

/// <summary>
/// The history is filtered by kind with chips: none ticked shows every kind, and each chip counts its kind.
/// </summary>
public sealed class NotificationHistoryViewModelTests
{
    [Fact]
    public void No_Kind_Ticked_Shows_Every_Notification_And_Ticked_Kinds_Show_Only_Theirs()
    {
        var (sut, history) = Create();
        history.Add(Entry(NotificationEventType.MotionDetected));
        history.Add(Entry(NotificationEventType.RecordingStarted));
        history.Add(Entry(NotificationEventType.CameraDisconnected));

        sut.EntriesView.Count.Should().Be(3);

        sut.SelectedEventTypes.Add(nameof(NotificationEventType.MotionDetected));
        sut.SelectedEventTypes.Add(nameof(NotificationEventType.CameraDisconnected));

        sut.EntriesView.Cast<NotificationEntry>().Select(e => e.EventType).Should().Equal(
            NotificationEventType.MotionDetected,
            NotificationEventType.CameraDisconnected);
        history.Should().HaveCount(3, "the history itself is not filtered");
    }

    [Fact]
    public void Each_Kind_Has_A_Chip_With_Its_Words_And_Its_Count()
    {
        var (sut, history) = Create();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        history.Add(Entry(NotificationEventType.MotionDetected));
        history.Add(Entry(NotificationEventType.MotionDetected));

        sut.EventTypes.Should().HaveCount(Enum.GetValues<NotificationEventType>().Length);
        sut.EventTypes[nameof(NotificationEventType.MotionDetected)].Should().Be(Translations.Motion);
        sut.EventTypeCounts[nameof(NotificationEventType.MotionDetected)].Should().Be(2);
        sut.EventTypeCounts[nameof(NotificationEventType.RecordingStopped)].Should().Be(0);
        changed.Should().Contain(nameof(NotificationHistoryViewModel.EventTypeCounts));
    }

    [Fact]
    public void The_Type_Column_Shows_The_Kind_In_Words()
        => Entry(NotificationEventType.RecordingStopped).TypeText
            .Should().Be(Translations.RecordingStoppedNotification);

    private static (NotificationHistoryViewModel Sut, System.Collections.ObjectModel.ObservableCollection<NotificationEntry> History) Create()
    {
        var eventService = new SurveillanceEventService(
            Substitute.For<IListEventsEndpoint>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SurveillanceEventService>.Instance,
            new SurveillanceEventServiceOptions());
        var coordinator = new NotificationCoordinator(eventService, Substitute.For<Atc.Wpf.Notifications.IToastNotificationService>());
        return (new NotificationHistoryViewModel(coordinator), coordinator.History);
    }

    private static NotificationEntry Entry(NotificationEventType eventType)
        => new(DateTimeOffset.UnixEpoch, "Title", "Message", eventType);
}