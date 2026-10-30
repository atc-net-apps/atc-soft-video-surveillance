namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Tests whether a server answers at an address, before a profile is saved.
/// It asks for the camera list, which every server serves at <c>/cameras</c>.
/// </summary>
public static class ServerConnectionProbe
{
    private const string ProbePath = "cameras";

    public static async Task<string> TestAsync(
        HttpClient httpClient,
        Uri serverAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(serverAddress);

        var address = new Uri($"{serverAddress.AbsoluteUri.TrimEnd('/')}/{ProbePath}");

        try
        {
            using var response = await httpClient
                .GetAsync(address, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? string.Format(CultureInfo.CurrentCulture, Translations.ProbeConnected1, (int)response.StatusCode)
                : string.Format(CultureInfo.CurrentCulture, Translations.ProbeFailedStatus2, (int)response.StatusCode, response.ReasonPhrase);
        }
        catch (TaskCanceledException)
        {
            return Translations.ProbeTimedOut;
        }
        catch (HttpRequestException ex)
        {
            return string.Format(CultureInfo.CurrentCulture, Translations.ProbeFailed1, ex.Message);
        }
    }
}