namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// View model for adding a camera of a camera hub (motionEye or motionEyeOS on a Raspberry Pi):
/// the hub's cameras through the server, one added. The server reads the camera's stream
/// credentials from the hub itself.
/// </summary>
public partial class HubCameraDialogViewModel : ViewModelBase
{
    private readonly Func<HubProbeRequest, Task<HubProbeResult>> probe;
    private readonly Func<AddHubCameraRequest, Task<HubCameraAdded>> add;

    [ObservableProperty(AfterChangedCallback = nameof(OnInputChanged))]
    private string address = string.Empty;

    [ObservableProperty]
    private string userName = "admin";

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? status;

    [ObservableProperty]
    private string? warning;

    [ObservableProperty]
    private string? problem;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubCameraDialogViewModel"/> class.
    /// </summary>
    /// <param name="probe">Lists a hub's cameras through the server.</param>
    /// <param name="add">Adds one through the server.</param>
    public HubCameraDialogViewModel(
        Func<HubProbeRequest, Task<HubProbeResult>> probe,
        Func<AddHubCameraRequest, Task<HubCameraAdded>> add)
    {
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.add = add ?? throw new ArgumentNullException(nameof(add));
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets the hub's cameras, once listed.
    /// </summary>
    public ObservableCollection<HubCameraRowViewModel> Cameras { get; } = [];

    /// <summary>
    /// Gets the name of the camera added, once it is.
    /// </summary>
    public string? AddedName { get; private set; }

    private static void OnInputChanged()
        => CommandManager.InvalidateRequerySuggested();

    private bool CanConnect()
        => !IsBusy && !string.IsNullOrWhiteSpace(Address);

    [RelayCommand("Connect", CanExecute = nameof(CanConnect))]
    public async Task ConnectAsync()
    {
        IsBusy = true;
        Problem = null;
        Warning = null;
        Status = Translations.Connecting;
        Cameras.Clear();
        try
        {
            var listing = await probe(new HubProbeRequest(Address: Address.Trim(), UserName: UserName, Password: Password)).ConfigureAwait(true);
            Status = StatusOf(listing);
            Warning = listing.OlderSignIn ? Translations.CameraHubOlderSignIn : null;
            foreach (var camera in listing.Cameras)
            {
                Cameras.Add(new HubCameraRowViewModel(camera, ProblemOf(camera.Problem)));
            }
        }
        catch (HttpRequestException ex)
        {
            Status = null;
            Problem = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand("Add")]
    public async Task AddAsync(HubCameraRowViewModel? row)
    {
        if (row is null || !row.CanAdd || IsBusy)
        {
            return;
        }

        IsBusy = true;
        Problem = null;
        try
        {
            var added = await add(new AddHubCameraRequest(Address: Address.Trim(), CameraId: row.Camera.Id, UserName: UserName, Password: Password)).ConfigureAwait(true);
            AddedName = added.DisplayName;
            CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
        }
        catch (HttpRequestException ex)
        {
            Problem = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));

    private static string StatusOf(HubProbeResult listing)
        => listing.Outcome.Value switch
        {
            HubProbeResultOutcome.Names.Listed => string.Format(CultureInfo.CurrentCulture, Translations.CameraHubListed1, listing.Cameras.Count),
            HubProbeResultOutcome.Names.SignInRefused => Translations.CameraHubSignInRefused,
            HubProbeResultOutcome.Names.NotThisKind => Translations.CameraHubNotMotionEye,
            _ => Translations.CameraHubUnreachable,
        };

    private static string? ProblemOf(HubCameraInfoProblem problem)
        => problem.Value switch
        {
            HubCameraInfoProblem.Names.None => null,
            HubCameraInfoProblem.Names.Disabled => Translations.HubCameraDisabled,
            HubCameraInfoProblem.Names.StreamingOff => Translations.HubCameraStreamingOff,
            HubCameraInfoProblem.Names.OnAnotherHub => Translations.HubCameraOnAnotherHub,

            // A reason this client does not know yet: it cannot be added from here.
            _ => problem.Value,
        };
}