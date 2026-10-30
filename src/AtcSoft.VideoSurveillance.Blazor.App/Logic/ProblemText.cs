namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

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

        var result = response as IEndpointResult;
        if (result?.IsTransportFailure == true)
        {
            return Translations.ServerCouldNotBeReached;
        }

        return result?.Problem?.ToDisplayText()
            ?? string.Format(
                CultureInfo.CurrentCulture,
                Translations.ServerAnswered2,
                (int)response.StatusCode,
                response.StatusCode);
    }

    /// <summary>
    /// Puts the page's own sentence and the server's reason together.
    /// </summary>
    public static string WithReason(
        string sentence,
        string? reason)
        => string.IsNullOrWhiteSpace(reason) ? sentence : $"{sentence} {reason}";
}