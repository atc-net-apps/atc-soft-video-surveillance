namespace AtcSoft.VideoSurveillance.Blazor.App.Shared;

public sealed partial class MainLayout
{
    private bool drawerOpen = true;
    private bool initialized;

    [CascadingParameter]
    private App? AppInstance { get; set; }

    [Inject]
    private AuthService Auth { get; set; } = default!;

    [Inject]
    private BrowserSession Session { get; set; } = default!;

    public void Dispose()
    {
        Session.Changed -= OnSessionChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // The pages are shown once this browser is signed in (a session of an
        // earlier visit counts); the session ending shows the sign-in again.
        Session.Changed += OnSessionChanged;
        await Auth.InitializeAsync();
        initialized = true;
    }

    private void OnSessionChanged()
        => _ = InvokeAsync(StateHasChanged);

    private void ToggleDrawer()
    {
        drawerOpen = !drawerOpen;
    }

    private void ToggleDarkMode()
    {
        AppInstance?.ToggleDarkMode();
    }
}