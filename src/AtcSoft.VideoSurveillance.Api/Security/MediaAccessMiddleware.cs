namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Lets a request for a recording or a thumbnail (<c>/recordings-files</c>)
/// through only for a signed-in caller, an API key, or a media token in the
/// address (<c>?media=</c>, for a player or an image that cannot send a header).
/// Anyone else gets a 401 ProblemDetails, before the files are looked at.
/// </summary>
public sealed class MediaAccessMiddleware(
    RequestDelegate next,
    AccountService accounts)
{
    /// <summary>
    /// The query parameter a media token is sent in.
    /// </summary>
    public const string MediaTokenParameter = "media";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated == true ||
            (await context.AuthenticateAsync(ApiKeyAuthenticationHandler.SchemeName).ConfigureAwait(false)).Succeeded ||
            await accounts.Tokens.IsValidMediaTokenAsync(context.Request.Query[MediaTokenParameter].ToString()).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Results.Problem(
                detail: "Sign in, or add the session's media token to the address (?media=).",
                statusCode: StatusCodes.Status401Unauthorized)
            .ExecuteAsync(context)
            .ConfigureAwait(false);
    }
}