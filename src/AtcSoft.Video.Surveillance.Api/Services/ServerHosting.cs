namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// How the server is hosted: as a console app, or on Windows as the service the server MSI
/// installs (<see cref="ServiceName"/>). A service starts in the system folder, so its content
/// root (the web client, <c>appsettings.json</c>) is set to the folder of the exe.
/// </summary>
public static class ServerHosting
{
    /// <summary>
    /// The name of the Windows service, and of its virtual account (<c>NT SERVICE\AtcSoftVideoSurveillance</c>).
    /// </summary>
    public const string ServiceName = "AtcSoftVideoSurveillance";

    /// <summary>
    /// Gets the options the server's builder is made with.
    /// </summary>
    /// <param name="args">The command line, without the server's own switches.</param>
    public static WebApplicationOptions Options(string[] args)
        => new()
        {
            Args = args,
            ContentRootPath = RunsAsWindowsService() ? AppContext.BaseDirectory : null,
        };

    /// <summary>
    /// Makes the server a Windows service when the service control manager started it; otherwise
    /// it stays a console app.
    /// </summary>
    /// <param name="builder">The server's builder.</param>
    public static void UseWindowsServiceWhenStartedAsOne(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
#if WINDOWS
        builder.Services.AddWindowsService(options => options.ServiceName = ServiceName);
#endif
    }

    private static bool RunsAsWindowsService()
#if WINDOWS
        => Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService();
#else
        => false;
#endif
}