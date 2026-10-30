var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with API base address
// For Aspire: uses services__api__https__0 or services__api__http__0 environment variable
// For standalone: uses ApiBaseAddress from wwwroot/appsettings.json (the server's address for a
// deployment; the E2E smoke tests answer that request with their test server) or the default
var apiBaseAddress = builder.Configuration["services:api:https:0"]
    ?? builder.Configuration["services:api:http:0"]
    ?? builder.Configuration["ApiBaseAddress"]
    ?? "http://localhost:5000";

// Register HttpClient factory with named client for the API, with the
// resilience the specification declares (x-retry-*): two minutes per attempt
// for the calls that are slow by nature, and only reads are repeated.
// The signed-in session; every call carries its access token (outermost, so a
// retry sends the same one).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BrowserSession>();
builder.Services
    .AddHttpClient("VideoSurveillance-ApiClient", client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
    })
    .AddApiCredentials((sp, credentials) => SessionCredentials.Configure(credentials, sp.GetRequiredService<BrowserSession>()))
    .AddApiResilience();

// Register all generated endpoints (includes IHttpMessageFactory registration)
builder.Services.AddVideoSurveillanceEndpoints();

// Register MudBlazor services
builder.Services.AddMudServices();
builder.Services.AddTransient<MudLocalizer, TranslationsMudLocalizer>();

// Register Gateway service with API base URL for static file URL building
builder.Services.AddScoped(sp =>
{
    var svc = ActivatorUtilities.CreateInstance<GatewayService>(sp);
    svc.ApiBaseUrl = apiBaseAddress;
    return svc;
});

// Sign-in and sign-out, kept across a page reload
builder.Services.AddScoped<AuthService>();

// The server's event stream (GET /events), one reader per page that runs it
builder.Services.AddTransient<EventStreamService>();

var host = builder.Build();

// Blazor WebAssembly fixes the culture at startup: the language chosen in this
// browser, else the browser's own (see LanguageChoice). A change reloads the page.
var js = host.Services.GetRequiredService<IJSRuntime>();
var culture = CultureInfo.GetCultureInfo(LanguageChoice.Resolve(
    await js.InvokeAsync<string?>("atcLanguage.get").ConfigureAwait(false),
    await js.InvokeAsync<string?>("atcLanguage.browser").ConfigureAwait(false)));
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host
    .RunAsync()
    .ConfigureAwait(false);