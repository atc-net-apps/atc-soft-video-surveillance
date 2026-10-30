namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Writes every change a caller made into the <see cref="IAuditLog"/>: a request
/// that is not a read, to one of the API's operations, that succeeded. An operation
/// the specification marks <c>x-audit: false</c> is left out.
/// </summary>
public sealed class AuditMiddleware(
    RequestDelegate next,
    IAuditLog audit)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await next(context).ConfigureAwait(false);

        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) ||
            context.Response.StatusCode >= StatusCodes.Status400BadRequest ||
            context.GetEndpoint() is not { } endpoint ||
            endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName is not { Length: > 0 } name ||
            global::VideoSurveillance.Generated.ApiOperationExtension.Find(endpoint, "x-audit") == "false")
        {
            return;
        }

        var caller = CallerIdentity.From(context.User);
        audit.Record(
            caller?.UserName ?? "anonymous",
            char.ToLowerInvariant(name[0]) + name[1..],
            context.Request.Path + context.Request.QueryString);
    }
}