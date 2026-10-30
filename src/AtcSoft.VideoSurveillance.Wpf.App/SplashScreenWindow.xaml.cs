namespace AtcSoft.VideoSurveillance.Wpf.App;

/// <summary>
/// Splash screen window displayed during application startup.
/// </summary>
[DependencyProperty<string>("Header")]
[DependencyProperty<string>("VersionText")]
public partial class SplashScreenWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplashScreenWindow"/> class.
    /// </summary>
    public SplashScreenWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}