namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// The answer to a change: the value the server sent back, or, when it
/// refused, its reason in its own words.
/// </summary>
/// <typeparam name="T">What the server sends back.</typeparam>
public sealed record GatewayResult<T>(
    T? Value,
    string? Problem)
    where T : class
{
    public bool IsSuccess
        => Problem is null;
}