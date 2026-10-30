namespace AtcSoft.VideoSurveillance.Api.Handlers;

/// <summary>
/// Runs a handler's result against an in-memory response, for what only shows
/// once it is written: the headers.
/// </summary>
internal static class ResultExecution
{
    public static async Task<HttpResponse> ExecuteAsync(this IResult result)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };

        await result.ExecuteAsync(context);
        return context.Response;
    }
}