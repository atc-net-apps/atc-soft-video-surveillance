namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// A yes/no question asked inside a dialog (<see cref="DialogQuestionOverlay"/>), never in a
/// window of its own: a dialog opens no other window. <see cref="AskAsync"/> shows it and
/// completes with the answer.
/// </summary>
public sealed partial class DialogQuestion : ObservableObject
{
    private TaskCompletionSource<bool>? answer;

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private bool isOpen;

    /// <summary>
    /// Shows the question and waits for the answer. A question still open is answered no.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns><see langword="true"/> for yes.</returns>
    public Task<bool> AskAsync(string question)
    {
        answer?.TrySetResult(false);
        answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Text = question;
        IsOpen = true;
        return answer.Task;
    }

    [RelayCommand]
    private void Yes()
        => Answer(yes: true);

    [RelayCommand]
    private void No()
        => Answer(yes: false);

    private void Answer(bool yes)
    {
        IsOpen = false;
        var asked = answer;
        answer = null;
        asked?.TrySetResult(yes);
    }
}