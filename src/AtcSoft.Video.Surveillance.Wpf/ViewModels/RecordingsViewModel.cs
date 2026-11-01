namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// View model for the recordings browser view.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public sealed partial class RecordingsViewModel : ViewModelBase
{
    private readonly GatewayService gatewayService;
    private readonly Uri apiBaseUri;
    private readonly ClientSession? session;

    private ObservableCollection<string> selectedCameraFilters = [];

    [ObservableProperty(AfterChangedCallback = nameof(OnFilterChanged))]
    private string selectedDayFilter = "_ALL_";

    [ObservableProperty(AfterChangedCallback = nameof(OnFilterChanged))]
    private string selectedTimeFilter = "_ALL_";

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedRecordingChanged))]
    private RecordingEntryViewModel? selectedRecording;

    [ObservableProperty]
    private ObservableCollection<RecordingEntryViewModel> recordings = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedRecordingChanged))]
    private bool isAnalysing;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingsViewModel"/> class.
    /// </summary>
    public RecordingsViewModel(
        GatewayService gatewayService,
        Uri apiBaseUri,
        ClientSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(apiBaseUri);

        // The language changes live; CultureManager holds the handler weakly.
        CultureManager.UiCultureChanged += OnUiCultureChanged;
        selectedCameraFilters.CollectionChanged += OnSelectedCameraFiltersChanged;

        this.gatewayService = gatewayService;
        this.apiBaseUri = apiBaseUri;
        this.session = session;
    }

    /// <summary>
    /// Gets the cameras that have recordings, by name. A new dictionary each time,
    /// because atc:LabelMultiSelectComboBox takes its items as one.
    /// </summary>
    public IDictionary<string, string> CameraFilters { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the cameras whose recordings are listed; none ticked lists every camera.
    /// </summary>
    public ObservableCollection<string> SelectedCameraFilters
    {
        get => selectedCameraFilters;
        set
        {
            value ??= [];
            if (ReferenceEquals(value, selectedCameraFilters))
            {
                return;
            }

            selectedCameraFilters.CollectionChanged -= OnSelectedCameraFiltersChanged;
            selectedCameraFilters = value;
            selectedCameraFilters.CollectionChanged += OnSelectedCameraFiltersChanged;
            OnPropertyChanged();
            OnFilterChanged();
        }
    }

    /// <summary>
    /// Gets the text the camera filter shows when no camera is ticked.
    /// </summary>
    public string AllCamerasText => Translations.AllCameras;

    /// <summary>
    /// Gets the day filter items.
    /// </summary>
    public IDictionary<string, string> DayFilterItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["_ALL_"] = Translations.AllDays,
        ["TODAY"] = Translations.FilterToday,
        ["YESTERDAY"] = Translations.FilterYesterday,
        ["LAST7"] = Translations.FilterLast7Days,
        ["LAST30"] = Translations.FilterLast30Days,
        ["THISWEEK"] = Translations.FilterThisWeek,
        ["THISMONTH"] = Translations.FilterThisMonth,
    };

    /// <summary>
    /// Gets the time filter items.
    /// </summary>
    public IDictionary<string, string> TimeFilterItems
        => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["_ALL_"] = Translations.AllTimes,
        ["0_6"] = "00:00 - 06:00",
        ["6_12"] = "06:00 - 12:00",
        ["12_18"] = "12:00 - 18:00",
        ["18_24"] = "18:00 - 00:00",
    };

    /// <summary>
    /// Gets the filtered recordings based on selected filters.
    /// </summary>
    /// <remarks>
    /// A list, not a query: the grid's column filters filter the collection view of a list.
    /// </remarks>
    public IReadOnlyList<RecordingEntryViewModel> FilteredRecordings
    {
        get
        {
            var result = Recordings.AsEnumerable();

            if (SelectedCameraFilters.Count > 0)
            {
                var cameras = new HashSet<string>(SelectedCameraFilters, StringComparer.OrdinalIgnoreCase);
                result = result.Where(r => cameras.Contains(r.CameraName));
            }

            result = ApplyDayFilter(result);
            result = ApplyTimeFilter(result);

            return result.ToList();
        }
    }

    /// <summary>
    /// Gets a value indicating whether a recording can be played.
    /// </summary>
    public bool CanPlay => SelectedRecording is not null;

    /// <summary>
    /// Gets a value indicating whether the selected recording can be analysed for its activity
    /// timeline: a finished recording without an index, while no other is being analysed.
    /// </summary>
    public bool CanAnalyse
        => SelectedRecording is { Activity: null, IsInProgress: false } && !IsAnalysing;

    // How often, and how long, the view looks whether the server has finished an analysis.
    private static readonly TimeSpan AnalysisPoll = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AnalysisPatience = TimeSpan.FromMinutes(30);

    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            var apiRecordings = await gatewayService
                .GetRecordingsAsync()
                .ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (apiRecordings is not null && apiRecordings.Length > 0)
                {
                    Recordings = new ObservableCollection<RecordingEntryViewModel>(
                        apiRecordings
                            .Select(r => new RecordingEntryViewModel(r, apiBaseUri, session?.MediaToken))
                            .OrderByDescending(r => r.RecordingTime));
                }
                else
                {
                    Recordings = [];
                }

                UpdateCameraFilters();
                UpdateStatusMessage();
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Recordings = [];
                UpdateCameraFilters();
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.FailedToLoadRecordings1, ex.Message);
            });
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Window self-disposes on close via the Closed event handler.")]
    private void Play()
    {
        if (SelectedRecording is null)
        {
            return;
        }

        var filePath = SelectedRecording.FilePath;
        var cameraId = SelectedRecording.CameraId;
        var vm = new AtcSoft.Video.Surveillance.Wpf.Core.Windows.FullScreenRecordingWindowViewModel(
            SelectedRecording.PlaybackUrl.AbsoluteUri,
            SelectedRecording.FileName,
            recordingStartTime: SelectedRecording.RecordingTime.LocalDateTime,
            activityLoader: (buckets, durationSeconds, cancellationToken) => LoadActivityAsync(filePath, buckets, durationSeconds, cancellationToken),
            zonesLoader: cancellationToken => CameraZones.LoadAsync(gatewayService, cameraId, cancellationToken),
            previewLoader: ServerPreviews.Loader(gatewayService, path => RecordingEntryViewModel.FileUrl(apiBaseUri, path, session?.MediaToken), filePath),
            verdictSaver: gatewayService.VerdictSaver(filePath));

        var window = new AtcSoft.Video.Surveillance.Wpf.Core.Windows.FullScreenRecordingWindow(vm);
        window.Show();
    }

    [RelayCommand]
    private Task Refresh()
        => LoadAsync();

    // The server analyses in the background; the list shows the sparkline once the index is there.
    [RelayCommand("Analyse", CanExecute = nameof(CanAnalyse))]
    private async Task AnalyseAsync()
    {
        if (SelectedRecording is not { } recording || !CanAnalyse)
        {
            return;
        }

        IsAnalysing = true;
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.AnalysingRecording1, recording.FileName);
        try
        {
            await gatewayService.StartRecordingAnalysisAsync(recording.FilePath).ConfigureAwait(true);
            var waited = TimeSpan.Zero;
            while (waited < AnalysisPatience)
            {
                await Task.Delay(AnalysisPoll).ConfigureAwait(true);
                waited += AnalysisPoll;
                var activity = await gatewayService.GetRecordingActivityAsync(recording.FilePath, 1, 0).ConfigureAwait(true);
                if (activity is { HasIndex: true })
                {
                    await LoadAsync().ConfigureAwait(true);
                    StatusMessage = Translations.RecordingAnalysed;
                    return;
                }

                // Neither done nor going on: the analysis failed, or the server forgot it.
                if (activity is { IsBeingAnalysed: false })
                {
                    break;
                }
            }

            StatusMessage = Translations.RecordingNotAnalysed;
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.FailedWithStatus1, ex.Message);
        }
        finally
        {
            IsAnalysing = false;
        }
    }

    // The server keeps the activity index beside the recording; a recording without one has no timeline.
    private async Task<IReadOnlyList<AtcSoft.Video.Surveillance.Models.ActivityBucket>?> LoadActivityAsync(
        string filePath,
        int buckets,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var activity = await gatewayService
            .GetRecordingActivityAsync(filePath, buckets, durationSeconds, cancellationToken)
            .ConfigureAwait(false);

        return activity is { HasIndex: true }
            ? [.. activity.Buckets.Select(b => new AtcSoft.Video.Surveillance.Models.ActivityBucket(b.Level, b.Tracks, b.IsAnalysed) { Zones = b.Zones ?? [] })]
            : null;
    }

    private void OnSelectedCameraFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnFilterChanged();

    private void OnFilterChanged()
    {
        OnPropertyChanged(nameof(FilteredRecordings));
        UpdateStatusMessage();
    }

    private void OnSelectedRecordingChanged()
    {
        OnPropertyChanged(nameof(CanAnalyse));
        OnPropertyChanged(nameof(CanPlay));
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Shows the lists and the status in the language just chosen.
    /// </summary>
    internal void RefreshTexts()
    {
        OnPropertyChanged(nameof(DayFilterItems));
        OnPropertyChanged(nameof(TimeFilterItems));
        UpdateCameraFilters();
        UpdateStatusMessage();
    }

    private void OnUiCultureChanged(
        object? sender,
        UiCultureEventArgs e)
        => RefreshTexts();

    private void UpdateCameraFilters()
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var cameraNames = Recordings
            .Select(r => r.CameraName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var cameraName in cameraNames)
        {
            filters[cameraName] = cameraName;
        }

        CameraFilters = filters;
        OnPropertyChanged(nameof(CameraFilters));
        OnPropertyChanged(nameof(AllCamerasText));

        // A ticked camera that has no recordings any more is unticked; none left means all cameras.
        foreach (var gone in SelectedCameraFilters.Where(c => !filters.ContainsKey(c)).ToList())
        {
            SelectedCameraFilters.Remove(gone);
        }

        OnPropertyChanged(nameof(FilteredRecordings));
    }

    private void UpdateStatusMessage()
    {
        var count = FilteredRecordings.Count;
        StatusMessage = count == 0
            ? Translations.NoRecordingsFound
            : string.Format(CultureInfo.CurrentCulture, Translations.RecordingsFound1, count);
    }

    private IEnumerable<RecordingEntryViewModel> ApplyDayFilter(
        IEnumerable<RecordingEntryViewModel> source)
    {
        var today = DateTime.Today;

        return SelectedDayFilter switch
        {
            "_ALL_" => source,
            "TODAY" => source.Where(r => r.RecordingTime.LocalDateTime.Date == today),
            "YESTERDAY" => source.Where(r => r.RecordingTime.LocalDateTime.Date == today.AddDays(-1)),
            "LAST7" => source.Where(r => r.RecordingTime.LocalDateTime.Date >= today.AddDays(-6)),
            "LAST30" => source.Where(r => r.RecordingTime.LocalDateTime.Date >= today.AddDays(-29)),
            "THISWEEK" => source.Where(r => IsThisWeek(r.RecordingTime.LocalDateTime, today)),
            "THISMONTH" => source.Where(r =>
                r.RecordingTime.LocalDateTime.Year == today.Year &&
                r.RecordingTime.LocalDateTime.Month == today.Month),
            _ => source,
        };
    }

    private IEnumerable<RecordingEntryViewModel> ApplyTimeFilter(
        IEnumerable<RecordingEntryViewModel> source)
        => SelectedTimeFilter switch
        {
            "_ALL_" => source,
            "0_6" => source.Where(r => r.RecordingTime.LocalDateTime.Hour >= 0 && r.RecordingTime.LocalDateTime.Hour < 6),
            "6_12" => source.Where(r => r.RecordingTime.LocalDateTime.Hour >= 6 && r.RecordingTime.LocalDateTime.Hour < 12),
            "12_18" => source.Where(r => r.RecordingTime.LocalDateTime.Hour >= 12 && r.RecordingTime.LocalDateTime.Hour < 18),
            "18_24" => source.Where(r => r.RecordingTime.LocalDateTime.Hour >= 18 && r.RecordingTime.LocalDateTime.Hour < 24),
            _ => source,
        };

    private static bool IsThisWeek(
        DateTime date,
        DateTime today)
    {
        var daysSinceMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        var monday = today.AddDays(-daysSinceMonday);
        var sunday = monday.AddDays(6);

        return date.Date >= monday && date.Date <= sunday;
    }
}