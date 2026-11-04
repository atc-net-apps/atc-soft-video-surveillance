namespace AtcSoft.Video.Surveillance.Wpf.App.Dialogs;

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

    // The profile being added or edited, over the dialog; null when none is.
    [ObservableProperty(DependentPropertyNames = [nameof(IsEditingProfile)])]
    private ServerProfileEditorViewModel? profileEditor;

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
    /// Gets the yes/no question shown inside the dialog: a dialog opens no other window.
    /// </summary>
    public AtcSoft.Video.Surveillance.Wpf.Core.Dialogs.DialogQuestion Question { get; } = new();

    /// <summary>
    /// Gets a value indicating whether a profile is being added or edited, over the dialog.
    /// </summary>
    public bool IsEditingProfile
        => ProfileEditor is not null;

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
        => OpenEditor(new ServerProfileEditorViewModel(), vm =>
        {
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
        });

    // The editor shows over the dialog until saved or cancelled; on save the change is applied.
    private void OpenEditor(
        ServerProfileEditorViewModel editor,
        Action<ServerProfileEditorViewModel> apply)
    {
        editor.CloseRequested += (_, saved) =>
        {
            ProfileEditor = null;
            if (saved)
            {
                apply(editor);
            }
        };
        ProfileEditor = editor;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void EditProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var profile = SelectedProfile;
        OpenEditor(
            new ServerProfileEditorViewModel
            {
                ProfileName = profile.Name,
                Url = profile.Url,
                Description = profile.Description,
            },
            vm =>
            {
                profile.Name = vm.ProfileName;
                profile.Url = vm.Url.TrimEnd('/');
                profile.Description = vm.Description;

                profileService.AddOrUpdateProfile(profile);
                profileService.Save();

                // Refresh list
                var index = Profiles.IndexOf(profile);
                if (index >= 0)
                {
                    Profiles[index] = profile;
                    SelectedProfile = profile;
                }
            });
    }

    [RelayCommand("DeleteProfile", CanExecute = nameof(HasSelectedProfile))]
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        var question = string.Format(CultureInfo.CurrentCulture, Translations.ConfirmDeleteProfile1, profile.Name);
        if (!await Question.AskAsync(question).ConfigureAwait(true))
        {
            return;
        }

        profileService.DeleteProfile(profile.Id);
        profileService.Save();

        Profiles.Remove(profile);
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