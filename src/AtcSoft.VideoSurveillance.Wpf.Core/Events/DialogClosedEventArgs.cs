namespace AtcSoft.VideoSurveillance.Wpf.Core.Events;

/// <summary>
/// Event arguments for when a dialog closes.
/// </summary>
public sealed class DialogClosedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialogClosedEventArgs"/> class.
    /// </summary>
    /// <param name="dialogResult">The dialog result.</param>
    public DialogClosedEventArgs(bool dialogResult)
        => DialogResult = dialogResult;

    /// <summary>
    /// Gets the dialog result.
    /// </summary>
    public bool DialogResult { get; }
}