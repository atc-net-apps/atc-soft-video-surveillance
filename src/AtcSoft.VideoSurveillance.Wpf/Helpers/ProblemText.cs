namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

/// <summary>
/// The server's own words for a request it refused.
/// </summary>
public static class ProblemText
{
    /// <summary>
    /// Gets the text to show: the generated problem text (field messages,
    /// detail, a title that says more than the status), else the status in
    /// the client's language.
    /// </summary>
    public static string From(EndpointResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return (response as IEndpointResult)?.Problem?.ToDisplayText()
            ?? string.Format(
                CultureInfo.CurrentCulture,
                Translations.ServerAnswered2,
                (int)response.StatusCode,
                response.StatusCode);
    }
}