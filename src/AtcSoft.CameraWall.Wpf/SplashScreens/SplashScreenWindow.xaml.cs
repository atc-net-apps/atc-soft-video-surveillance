namespace AtcSoft.CameraWall.Wpf.SplashScreens;

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
        Messenger.Default.Register<SplashScreenMessage>(this, OnSplashScreenMessageHandler);
    }

    private void OnSplashScreenMessageHandler(SplashScreenMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Close)
        {
            Close();
        }
        else
        {
            SetCurrentValue(HeaderProperty, message.Header);
            SetCurrentValue(VersionTextProperty, $"Version {message.Version}");
        }
    }
}