namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// View model for the dialog the signed-in user changes their own password in.
/// </summary>
public partial class ChangePasswordDialogViewModel : ViewModelBase
{
    private readonly Func<string, string, Task<string?>> changePassword;

    [ObservableProperty]
    private string currentPassword = string.Empty;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string? problem;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordDialogViewModel"/> class.
    /// </summary>
    /// <param name="changePassword">Changes the password (current, new); returns the server's reason when it refused.</param>
    public ChangePasswordDialogViewModel(
        Func<string, string, Task<string?>> changePassword)
        => this.changePassword = changePassword;

    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    [RelayCommand("Save")]
    public async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            Problem = await changePassword(CurrentPassword, NewPassword).ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            // The server could not be reached.
            Problem = ex.Message;
        }

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