namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// A burst of notifications (cameras flapping after a server restart) shows
/// only its first few as toasts; the next window shows toasts again.
/// </summary>
public sealed class NotificationCoordinatorTests : IDisposable
{
    private readonly SurveillanceEventService events = TestEvents.Service();
    private readonly FakeTimeProvider time = new();
    private readonly NotificationCoordinator sut;

    public NotificationCoordinatorTests()
        => sut = new NotificationCoordinator(events, Substitute.For<Atc.Wpf.Notifications.IToastNotificationService>(), time);

    public void Dispose()
    {
        sut.Dispose();
        events.Dispose();
    }

    [Fact]
    public void A_Burst_Shows_Only_Its_First_Toasts()
    {
        var shown = Enumerable.Range(0, 30).Count(_ => sut.TakeToast());

        shown.Should().Be(NotificationCoordinator.MaxToastsPerWindow);
    }

    [Fact]
    public void The_Next_Window_Shows_Toasts_Again()
    {
        Enumerable.Range(0, 10).ToList().ForEach(_ => sut.TakeToast());

        time.Advance(NotificationCoordinator.ToastWindow);

        sut.TakeToast().Should().BeTrue();
    }
}