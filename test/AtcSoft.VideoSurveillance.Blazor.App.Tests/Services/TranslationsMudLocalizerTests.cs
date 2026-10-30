namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// MudBlazor's own texts (a close button, a nav group's toggle) come from the
/// shared Translations, in the language the app started in.
/// </summary>
public sealed class TranslationsMudLocalizerTests
{
    private readonly TranslationsMudLocalizer sut = new();

    [Fact]
    public void A_MudBlazor_Text_Is_The_Shared_Translation()
        => sut["MudAlert_Close"].Value.Should().Be(Translations.Close);

    [Fact]
    public void A_MudBlazor_Text_With_A_Name_In_It_Gets_The_Name()
        => sut["MudNavGroup_ToggleExpand", "Cameras"].Value.Should().Be("Toggle Cameras");

    [Fact]
    public void A_MudBlazor_Text_Is_In_The_Chosen_Language()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");

            sut["MudAlert_Close"].Value.Should().Be("Luk");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void A_Text_Without_A_Translation_Is_Left_To_MudBlazor()
        => sut["MudColorPicker_SpectrumView"].ResourceNotFound.Should().BeTrue();
}