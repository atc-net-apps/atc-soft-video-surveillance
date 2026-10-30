namespace AtcSoft.VideoSurveillance.Blazor.App;

/// <summary>
/// The tests assert the US English texts, whatever the machine's language is.
/// </summary>
internal static class EnglishTestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }
}