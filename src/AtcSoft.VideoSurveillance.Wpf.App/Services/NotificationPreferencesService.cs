namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Manages notification preferences persistence to a local JSON file.
/// Not DI-registered — instantiated directly in App.xaml.cs.
/// </summary>
public sealed class NotificationPreferencesService : JsonFileServiceBase<NotificationPreferences>
{
    private static readonly string PreferencesFilePath = AtcSoft.VideoSurveillance.Helpers.ApplicationPaths.GetLocalDataPath("VideoSurveillance", "notifications.json");

    public NotificationPreferencesService()
        : base(PreferencesFilePath)
    {
    }

    /// <summary>
    /// Gets the loaded preferences.
    /// </summary>
    public NotificationPreferences Preferences
        => Data;
}