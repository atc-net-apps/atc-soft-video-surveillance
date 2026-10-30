namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog displaying application information.
/// </summary>
public partial class AboutDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialog"/> class.
    /// </summary>
    /// <param name="version">The application version.</param>
    /// <param name="year">The copyright year.</param>
    public AboutDialog(
        string version,
        int year)
    {
        InitializeComponent();

        VersionRun.Text = version;
        YearRun.Text = year.ToString(GlobalizationConstants.EnglishCultureInfo);
    }

    private void OnOkClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}