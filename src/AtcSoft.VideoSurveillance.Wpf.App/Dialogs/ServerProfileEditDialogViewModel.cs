namespace AtcSoft.VideoSurveillance.Wpf.App.Dialogs;

/// <summary>
/// View model for the server profile add/edit dialog.
/// </summary>
public sealed partial class ServerProfileEditDialogViewModel : ObservableObject
{
    [ObservableProperty(AfterChangedCallback = nameof(OnFieldChanged))]
    private string profileName = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnFieldChanged))]
    private string url = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    /// <summary>
    /// Occurs when the dialog requests to be closed.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    private static void OnFieldChanged()
        => CommandManager.InvalidateRequerySuggested();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
        => CloseRequested?.Invoke(this, true);

    private bool CanSave()
        => !string.IsNullOrWhiteSpace(ProfileName)
        && Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    [RelayCommand]
    private void Cancel()
        => CloseRequested?.Invoke(this, false);
}