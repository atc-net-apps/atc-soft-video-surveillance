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
    .AddProject<Projects.AtcSoft_VideoSurveillance_Api>("VideoSurveillance-Api")
    .WithHttpEndpoint(port: 5000, name: "public");

if (useScratch)
{
    api.WithEnvironment("Storage__DataPath", Path.Combine(scratchDataPath!, "server"));
}

builder
    .AddProject<Projects.AtcSoft_VideoSurveillance_Blazor_App>("VideoSurveillance-BlazorApp")
    .WithReference(api, apiReferenceName)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

var wpfApp = builder
    .AddProject<Projects.AtcSoft_VideoSurveillance_Wpf_App>("VideoSurveillance-WpfApp")
    .WithReference(api, apiReferenceName)
    .WaitFor(api);

var cameraWall = builder
    .AddProject<Projects.AtcSoft_CameraWall_Wpf_App>("CameraWall-WpfApp");

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