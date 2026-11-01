namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// View model for the dashboard view showing live server stats.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly GatewayService gatewayService;
    private readonly SurveillanceEventService eventService;
    private readonly CameraStateCounts counts = new();
    private bool loadedOnce;

    [ObservableProperty]
    private int totalCameras;

    [ObservableProperty]
    private int connectedCameras;

    [ObservableProperty]
    private int totalLayouts;

    [ObservableProperty]
    private int activeRecordings;

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewModel"/> class.
    /// </summary>
    public DashboardViewModel(
        GatewayService gatewayService,
        SurveillanceEventService eventService)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(eventService);

        this.gatewayService = gatewayService;
        this.eventService = eventService;

        this.eventService.OnConnectionStateChanged += OnConnectionStateChanged;
        this.eventService.OnRecordingStateChanged += OnRecordingStateChanged;

        // The server could not send again what came meanwhile: the counts are read again.
        this.eventService.OnMissedEvents += () =>
        {
            if (loadedOnce)
            {
                _ = Application.Current?.Dispatcher.InvokeAsync(() => LoadCommand.Execute(parameter: null));
            }
        };
    }

    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        loadedOnce = true;
        IsLoading = true;

        try
        {
            var cameras = await gatewayService
                .GetCamerasAsync()
                .ConfigureAwait(false);

            var layouts = await gatewayService
                .GetLayoutsAsync()
                .ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                counts.Load(cameras ?? []);
                TotalCameras = cameras?.Length ?? 0;
                ShowCounts();

                TotalLayouts = layouts?.Length ?? 0;
            });
        }
        catch (HttpRequestException)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalCameras = 0;
                ConnectedCameras = 0;
                TotalLayouts = 0;
                ActiveRecordings = 0;
            });
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() => IsLoading = false);
        }
    }

    private void OnConnectionStateChanged(ConnectionStateChangedEvent e)
    {
        counts.Apply(e);
        _ = Application.Current?.Dispatcher.InvokeAsync(ShowCounts);
    }

    private void OnRecordingStateChanged(RecordingStateChangedEvent e)
    {
        counts.Apply(e);
        _ = Application.Current?.Dispatcher.InvokeAsync(ShowCounts);
    }

    private void ShowCounts()
    {
        ConnectedCameras = counts.Connected;
        ActiveRecordings = counts.Recording;
    }
}