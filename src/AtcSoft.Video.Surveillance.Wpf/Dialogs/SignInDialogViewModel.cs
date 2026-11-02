namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// View model for the sign-in dialog: signs in, or on a new server creates the
/// first admin (with the setup code, unless the dialog runs on the server's own
/// machine), and shows the server's reason when it refused.
/// </summary>
public partial class SignInDialogViewModel : ViewModelBase
{
    private readonly Func<string, string, Task<string?>> signIn;
    private readonly Func<string?, string, string, Task<string?>> completeSetup;
    private readonly string setupCodeFile;

    [ObservableProperty]
    private string setupCode = string.Empty;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? problem;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInDialogViewModel"/> class.
    /// </summary>
    /// <param name="setupRequired">The server has no user yet: the dialog creates the first admin.</param>
    /// <param name="signIn">Signs in (user name, password); returns the server's reason when it refused.</param>
    /// <param name="completeSetup">Creates the first admin (setup code or <see langword="null"/>, user name, password); returns the server's reason when it refused.</param>
    /// <param name="setupCodeRequired">The server wants the setup code from this client (not on the server's own machine).</param>
    /// <param name="setupCodeFile">The name of the file with the setup code in the server's data folder.</param>
    public SignInDialogViewModel(
        bool setupRequired,
        Func<string, string, Task<string?>> signIn,
        Func<string?, string, string, Task<string?>> completeSetup,
        bool setupCodeRequired = true,
        string setupCodeFile = "")
    {
        IsSetup = setupRequired;
        IsSetupCodeRequired = setupRequired && setupCodeRequired;
        this.signIn = signIn;
        this.completeSetup = completeSetup;
        this.setupCodeFile = setupCodeFile;
    }

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Gets a value indicating whether the dialog creates the first admin.
    /// </summary>
    public bool IsSetup { get; }

    /// <summary>
    /// Gets a value indicating whether the first admin needs the setup code from the server's data folder.
    /// </summary>
    public bool IsSetupCodeRequired { get; }

    public string SetupCodeHelp
        => string.Format(CultureInfo.CurrentCulture, Translations.SetupCodeHelp1, setupCodeFile);

    public string Title
        => IsSetup ? Translations.CreateTheFirstAdmin : Translations.SignInToTheServer;

    public string Help
        => IsSetup ? Translations.CreateTheFirstAdminHelp : string.Empty;

    [RelayCommand("SignIn")]
    public async Task SignInAsync()
    {
        IsBusy = true;
        Problem = null;

        try
        {
            Problem = IsSetup
                ? await completeSetup(IsSetupCodeRequired ? SetupCode.Trim() : null, UserName.Trim(), Password).ConfigureAwait(true)
                : await signIn(UserName.Trim(), Password).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // The server could not be reached.
            Problem = ex.Message;
        }

        Password = string.Empty;
        IsBusy = false;

        if (Problem is null)
        {
            CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
        }
    }

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: false));
}