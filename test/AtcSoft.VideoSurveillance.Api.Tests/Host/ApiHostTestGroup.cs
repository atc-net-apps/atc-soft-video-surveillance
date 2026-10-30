namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// One server for all host tests: they share its data folder and run one at a time.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiHostTestGroup : ICollectionFixture<ApiHostFixture>
{
    public const string Name = "API host";
}