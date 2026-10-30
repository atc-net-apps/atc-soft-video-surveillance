namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// The language changes live in the WPF client (CultureManager raises
/// UiCultureChanged and the view models call RefreshTexts), so the lists a
/// view model builds follow it. The tests change only this thread's culture.
/// </summary>
public sealed class LanguageSwitchTests
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    [Fact]
    public void The_Recordings_Filters_Follow_A_Language_Change()
    {
        using var content = new StringContent("[]");
        using var server = new GatewayTestServer(HttpStatusCode.OK, content, "application/json");
        var sut = new RecordingsViewModel(server.Gateway, new Uri("http://server.test"));
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        InDanish(() =>
        {
            sut.RefreshTexts();

            sut.DayFilterItems["TODAY"].Should().Be(Translations.ResourceManager.GetString(nameof(Translations.FilterToday), Danish));
            sut.TimeFilterItems["_ALL_"].Should().Be(Translations.ResourceManager.GetString(nameof(Translations.AllTimes), Danish));
        });

        changed.Should().Contain([nameof(RecordingsViewModel.DayFilterItems), nameof(RecordingsViewModel.TimeFilterItems)]);
    }

    private static void InDanish(Action assert)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = Danish;
            assert();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}