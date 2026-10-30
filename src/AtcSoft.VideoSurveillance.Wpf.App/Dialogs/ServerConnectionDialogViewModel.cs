namespace AtcSoft.VideoSurveillance.Wpf.App.Dialogs;

/// <summary>
/// View model for the server connection dialog.
/// </summary>
public sealed partial class ServerConnectionDialogViewModel : ObservableObject
{
    private readonly ServerProfileService profileService;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedProfileChanged))]
    private ServerProfile? selectedProfile;

    [ObservableProperty(AfterChangedCallback = nameof(OnServerUrlChanged))]
    private string serverUrl = string.Empty;

    [ObservableProperty]
    private string connectionStatus = Translations.Ready;

    [ObservableProperty(AfterChangedCallback = nameof(OnIsTestingChanged))]
    private bool isTesting;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerConnectionDialogViewModel"/> class.
    /// </summary>
    /// <param name="profileService">The server profile service.</param>
    public ServerConnectionDialogViewModel(ServerProfileService profileService)
    {
        ArgumentNullException.ThrowIfNull(profileService);

        this.profileService = profileService;

        Profiles = new ObservableCollection<ServerProfile>(profileService.Profiles);

        // Pre-select last used profile
        var lastUsed = profileService.GetLastUsedProfile();
        if (lastUsed is not null)
        {
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == lastUsed.Id);
        }
    }

    /// <summary>
    /// The collection of saved server profiles.
    /// </summary>
    public ObservableCollection<ServerProfile> Profiles { get; }

    /// <summary>
    /// The resolved server URL to use for connection (set after successful Connect).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "String URL used for DI configuration")]
    public string? ResolvedUrl { get; private set; }

    /// <summary>
    /// Occurs when the dialog requests to be closed.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    private void OnSelectedProfileChanged()
    {
        if (SelectedProfile is not null)
        {
            ServerUrl = SelectedProfile.Url;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnServerUrlChanged()
        => CommandManager.InvalidateRequerySuggested();

    private static void OnIsTestingChanged()
        => CommandManager.InvalidateRequerySuggested();

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        var url = ServerUrl.TrimEnd('/');

        // Find or create a profile for this URL
        var profile = SelectedProfile;
        if (profile is null || !string.Equals(profile.Url, url, StringComparison.OrdinalIgnoreCase))
        {
            // Check if a profile with this URL already exists
            profile = Profiles.FirstOrDefault(p =>
                string.Equals(p.Url, url, StringComparison.OrdinalIgnoreCase));
        }

        if (profile is not null)
        {
            profileService.SetLastUsed(profile.Id);
            profileService.Save();
        }

        ResolvedUrl = url;
        CloseRequested?.Invoke(this, true);
    }

    private bool CanConnect()
        => !IsTesting && IsValidUrl(ServerUrl);

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, false);

    [RelayCommand]
    private void ExitApp()
    {
        CloseRequested?.Invoke(this, false);
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void AddProfile()
    {
        var vm = new ServerProfileEditDialogViewModel();
        var dialog = new ServerProfileEditDialog(vm);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var profile = new ServerProfile
        {
            Name = vm.ProfileName,
            Url = vm.Url.TrimEnd('/'),
            Description = vm.Description,
        };

        profileService.AddOrUpdateProfile(profile);
        profileService.Save();

        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void EditProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var vm = new ServerProfileEditDialogViewModel
        {
            ProfileName = SelectedProfile.Name,
            Url = SelectedProfile.Url,
            Description = SelectedProfile.Description,
        };

        var dialog = new ServerProfileEditDialog(vm);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SelectedProfile.Name = vm.ProfileName;
        SelectedProfile.Url = vm.Url.TrimEnd('/');
        SelectedProfile.Description = vm.Description;

        profileService.AddOrUpdateProfile(SelectedProfile);
        profileService.Save();

        // Refresh list
        var index = Profiles.IndexOf(SelectedProfile);
        if (index >= 0)
        {
            var updated = SelectedProfile;
            Profiles[index] = updated;
            SelectedProfile = updated;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var dialog = new QuestionDialogBox(
            Application.Current.MainWindow!,
            Translations.DeleteProfile,
            string.Format(
                CultureInfo.CurrentCulture,
                Translations.ConfirmDeleteProfile1,
                SelectedProfile.Name))
        {
            Width = 400,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        profileService.DeleteProfile(SelectedProfile.Id);
        profileService.Save();

        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    [RelayCommand("TestConnection", CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        ConnectionStatus = Translations.Testing;

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            ConnectionStatus = await ServerConnectionProbe
                .TestAsync(httpClient, new Uri(ServerUrl))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ConnectionStatus = string.Format(CultureInfo.CurrentCulture, Translations.ProbeFailed1, ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    private bool CanTestConnection()
        => !IsTesting && IsValidUrl(ServerUrl);

    private bool HasSelectedProfile()
        => SelectedProfile is not null;

    private static bool IsValidUrl(string url)
        => !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}