namespace AtcSoft.VideoSurveillance.Blazor.App;

[SuppressMessage(
    "Naming",
    "MA0049:Type name should not match containing namespace",
    Justification = "Blazor convention: root component is named App in the .App namespace.")]
[SuppressMessage(
    "Naming",
    "CA1724:Type names should not match namespaces",
    Justification = "Blazor convention: root component is named App in the .App namespace.")]
public partial class App
{
    private MudThemeProvider mudThemeProvider = null!;
    private bool isDarkMode = true;

    public bool IsDarkMode
    {
        get => isDarkMode;
        set
        {
            if (isDarkMode != value)
            {
                isDarkMode = value;
                StateHasChanged();
            }
        }
    }

    public void ToggleDarkMode()
        => IsDarkMode = !IsDarkMode;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isDarkMode = await mudThemeProvider.GetSystemDarkModeAsync();
            await InvokeAsync(StateHasChanged);
        }
    }
}