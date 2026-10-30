namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// MudBlazor's own texts (a close button, a nav group's toggle, the pager's
/// buttons) from the shared Translations, in the language the app started in.
/// A text not listed here is left to MudBlazor, in English.
/// </summary>
public sealed class TranslationsMudLocalizer : MudLocalizer
{
    private static readonly Dictionary<string, Func<string>> Texts = new(StringComparer.Ordinal)
    {
        ["MudAlert_Close"] = () => Translations.Close,
        ["MudChip_Close"] = () => Translations.Close,
        ["MudDialog_Close"] = () => Translations.Close,
        ["MudSnackbar_Close"] = () => Translations.Close,
        ["MudInput_Clear"] = () => Translations.Clear,
        ["MudInput_Increment"] = () => Translations.Increase,
        ["MudInput_Decrement"] = () => Translations.Decrease,
        ["MudFormComponent_Required"] = () => Translations.Required,
        ["MudNavGroup_ToggleExpand"] = () => Translations.ToggleGroup1,
        ["MudTablePager_FirstPage"] = () => Translations.FirstPage,
        ["MudTablePager_PreviousPage"] = () => Translations.PreviousPage,
        ["MudTablePager_NextPage"] = () => Translations.NextPage,
        ["MudTablePager_LastPage"] = () => Translations.LastPage,
        ["MudTabs_ScrollLeft"] = () => Translations.ScrollLeft,
        ["MudTabs_ScrollRight"] = () => Translations.ScrollRight,
        ["MudTabs_ScrollUp"] = () => Translations.ScrollUp,
        ["MudTabs_ScrollDown"] = () => Translations.ScrollDown,
    };

    public override LocalizedString this[string key]
        => this[key, []];

    public override LocalizedString this[string key, params object[] arguments]
        => Texts.TryGetValue(key, out var text)
            ? new LocalizedString(key, string.Format(CultureInfo.CurrentCulture, text(), arguments))
            : new LocalizedString(key, key, resourceNotFound: true);
}