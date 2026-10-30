namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The answer to a change that sends nothing back: done, or the server's reason.
/// </summary>
public sealed record GatewayResult(string? Problem)
{
    public static GatewayResult Done { get; } = new(Problem: null);

    public bool IsSuccess
        => Problem is null;

    public static GatewayResult<T> Success<T>(T value)
        where T : class
        => new(value, Problem: null);

    public static GatewayResult Refused(EndpointResponse response)
        => new(ProblemText.From(response));

    public static GatewayResult<T> Refused<T>(EndpointResponse response)
        where T : class
        => new(Value: null, ProblemText.From(response));
}