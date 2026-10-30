namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the dialog an admin manages users, API keys and reads the audit log in.
/// </summary>
public partial class UsersDialogViewModel : ViewModelBase
{
    private const int AuditEntriesShown = 200;

    private readonly IUserAdministration administration;
    private readonly Func<string, bool> confirm;

    [ObservableProperty(DependentPropertyNames = [nameof(IsNewUser)], AfterChangedCallback = nameof(OnSelectedUserChanged))]
    private UserRow? selectedUser;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string selectedRoleKey = UserRole.Names.Viewer;

    [ObservableProperty]
    private ApiKeyRow? selectedApiKey;

    [ObservableProperty]
    private string apiKeyName = string.Empty;

    [ObservableProperty]
    private string selectedApiKeyRoleKey = UserRole.Names.Viewer;

    [ObservableProperty(DependentPropertyNames = [nameof(HasMadeApiKey)])]
    private string? madeApiKey;

    [ObservableProperty]
    private string? message;

    [ObservableProperty]
    private string? problem;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersDialogViewModel"/> class.
    /// </summary>
    /// <param name="administration">The server's users, API keys and audit log.</param>
    /// <param name="confirm">Asks the admin a yes/no question.</param>
    public UsersDialogViewModel(
        IUserAdministration administration,
        Func<string, bool> confirm)
    {
        this.administration = administration;
        this.confirm = confirm;
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    public ObservableCollection<UserRow> Users { get; } = [];

    /// <summary>
    /// Gets a value indicating whether saving adds a user (no user is chosen); a user's name cannot change.
    /// </summary>
    public bool IsNewUser
        => SelectedUser is null;

    /// <summary>
    /// Gets a value indicating whether a key was just made: it is shown this once.
    /// </summary>
    public bool HasMadeApiKey
        => MadeApiKey is not null;

    public ObservableCollection<ApiKeyRow> ApiKeys { get; } = [];

    public ObservableCollection<AuditRow> AuditEntries { get; } = [];

    /// <summary>
    /// Gets the roles to choose from, least first: the wire value and its name in words.
    /// </summary>
    public IDictionary<string, string> RoleItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [UserRole.Names.Viewer] = Translations.RoleViewer,
        [UserRole.Names.Operator] = Translations.RoleOperator,
        [UserRole.Names.Admin] = Translations.RoleAdmin,
    };

    /// <summary>
    /// Gets a role in words; one the client does not know is shown as it is.
    /// </summary>
    public string RoleText(UserRole role)
        => RoleItems.TryGetValue(role.Value, out var text)
            ? text
            : role.Value;

    [RelayCommand("Load")]
    public Task LoadAsync()
        => RunAsync(sentence: null, ReloadAsync);

    [RelayCommand]
    public void NewUser()
    {
        SelectedUser = null;
        UserName = string.Empty;
        Password = string.Empty;
        SelectedRoleKey = UserRole.Names.Viewer;
    }

    [RelayCommand("SaveUser")]
    public Task SaveUserAsync()
        => RunAsync(
            Translations.UserCouldNotBeSaved,
            async () =>
            {
                var role = new UserRole(SelectedRoleKey);
                if (SelectedUser is { } user)
                {
                    var newPassword = string.IsNullOrEmpty(Password) ? null : Password;
                    await administration.UpdateUserAsync(user.Id, role, newPassword).ConfigureAwait(true);
                }
                else
                {
                    await administration.CreateUserAsync(UserName, Password, role).ConfigureAwait(true);
                }

                Password = string.Empty;
                await ReloadAsync().ConfigureAwait(true);
                return Translations.UserSaved;
            });

    [RelayCommand("RemoveUser")]
    public async Task RemoveUserAsync()
    {
        if (SelectedUser is not { } user ||
            !confirm(string.Format(CultureInfo.CurrentCulture, Translations.RemoveUser1, user.UserName)))
        {
            return;
        }

        await RunAsync(
                Translations.UserCouldNotBeRemoved,
                async () =>
                {
                    await administration.DeleteUserAsync(user.Id).ConfigureAwait(true);
                    NewUser();
                    await ReloadAsync().ConfigureAwait(true);
                    return Translations.UserRemoved;
                })
            .ConfigureAwait(true);
    }

    [RelayCommand("CreateApiKey")]
    public Task CreateApiKeyAsync()
        => RunAsync(
            Translations.ApiKeyCouldNotBeMade,
            async () =>
            {
                var made = await administration
                    .CreateApiKeyAsync(ApiKeyName, new UserRole(SelectedApiKeyRoleKey))
                    .ConfigureAwait(true);

                MadeApiKey = made.Key;
                ApiKeyName = string.Empty;
                await ReloadAsync().ConfigureAwait(true);
                return Translations.ApiKeyMade;
            });

    [RelayCommand("RemoveApiKey")]
    public async Task RemoveApiKeyAsync()
    {
        if (SelectedApiKey is not { } key ||
            !confirm(string.Format(CultureInfo.CurrentCulture, Translations.RemoveApiKey1, key.Name)))
        {
            return;
        }

        await RunAsync(
                Translations.ApiKeyCouldNotBeRemoved,
                async () =>
                {
                    await administration.DeleteApiKeyAsync(key.Id).ConfigureAwait(true);
                    await ReloadAsync().ConfigureAwait(true);
                    return Translations.ApiKeyRemoved;
                })
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private void Close()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));

    private void OnSelectedUserChanged()
    {
        if (SelectedUser is not { } user)
        {
            return;
        }

        UserName = user.UserName;
        Password = string.Empty;
        SelectedRoleKey = DropDownKey.For(user.Account.Role.Value, RoleItems, UserRole.Names.Viewer);
    }

    private async Task<string?> ReloadAsync()
    {
        var selectedUserId = SelectedUser?.Id;

        var users = await administration.GetUsersAsync().ConfigureAwait(true);
        var apiKeys = await administration.GetApiKeysAsync().ConfigureAwait(true);
        var audit = await administration.GetAuditEntriesAsync(AuditEntriesShown).ConfigureAwait(true);

        Replace(Users, users.Select(x => new UserRow(x, RoleText(x.Role))));
        Replace(ApiKeys, apiKeys.Select(x => new ApiKeyRow(x, RoleText(x.Role))));
        Replace(AuditEntries, audit.Select(x => new AuditRow(x)));

        SelectedUser = Users.FirstOrDefault(x => x.Id == selectedUserId);
        return null;
    }

    // Runs one change; what went wrong is shown with the server's reason after the sentence.
    private async Task RunAsync(
        string? sentence,
        Func<Task<string?>> work)
    {
        IsBusy = true;
        Problem = null;
        Message = null;
        try
        {
            Message = await work().ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            Problem = sentence is null
                ? ex.Message
                : $"{sentence} {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(
        ObservableCollection<T> target,
        IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}