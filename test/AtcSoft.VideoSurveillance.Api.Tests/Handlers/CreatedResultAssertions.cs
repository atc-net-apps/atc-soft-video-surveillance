namespace AtcSoft.VideoSurveillance.Api.Handlers;

/// <summary>
/// A create answers 201 with the created value and its address in Location.
/// </summary>
internal static class CreatedResultAssertions
{
    public static T ShouldBeCreated<T>(
        this global::VideoSurveillance.Generated.IApiResult result,
        string locationPrefix)
    {
        result.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Location.Should().StartWith(locationPrefix);
        return result.Value.Should().BeOfType<T>().Subject;
    }
}