// MA0048 (file name must match type name) is suppressed via NoWarn in the
// .csproj — Aspire convention uses AppHost.cs for the top-level program.
var builder = DistributedApplication.CreateBuilder(args);

// The clients find the server through services__api__* (Blazor.App Program.cs,
// Wpf.App App.xaml.cs), so each reference keeps the name "api" whatever the
// resource is called in the dashboard.
const string apiReferenceName = "api";

// Optional scratch folder (AtcSoft__DataPath=... or --AtcSoft:DataPath=...): the server, both WPF
// apps and all their files then live there instead of in ProgramData / LocalAppData.
var scratchDataPath = builder.Configuration["AtcSoft:DataPath"];
var useScratch = !string.IsNullOrWhiteSpace(scratchDataPath);

var api = builder
    .AddDotnetProject("VideoSurveillance-Api", "../AtcSoft.Video.Surveillance.Api/AtcSoft.Video.Surveillance.Api.csproj")
    .WithHttpEndpoint(port: 8585, name: "public");

if (useScratch)
{
    api.WithEnvironment("Storage__DataPath", Path.Combine(scratchDataPath!, "server"));
}

builder
    .AddDotnetProject("VideoSurveillance-BlazorApp", "../AtcSoft.Video.Surveillance.Blazor.App/AtcSoft.Video.Surveillance.Blazor.App.csproj")
    .WithReference(api, apiReferenceName)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

var wpfApp = builder
    .AddDotnetProject("VideoSurveillance-WpfApp", "../AtcSoft.Video.Surveillance.Wpf.App/AtcSoft.Video.Surveillance.Wpf.App.csproj")
    .WithReference(api, apiReferenceName)
    .WaitFor(api);

var cameraWall = builder
    .AddDotnetProject("CameraWall-WpfApp", "../AtcSoft.Video.Surveillance.CameraWall.Wpf.App/AtcSoft.Video.Surveillance.CameraWall.Wpf.App.csproj");

if (useScratch)
{
    // ApplicationPaths.DataPathEnvironmentVariable
    wpfApp.WithEnvironment("ATCSOFT_DATA_PATH", Path.Combine(scratchDataPath!, "wpf-app"));
    cameraWall.WithEnvironment("ATCSOFT_DATA_PATH", Path.Combine(scratchDataPath!, "camera-wall"));
}

await builder
    .Build()
    .RunAsync()
    .ConfigureAwait(false);