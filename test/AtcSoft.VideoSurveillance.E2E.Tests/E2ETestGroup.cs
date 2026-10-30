namespace AtcSoft.VideoSurveillance.E2E;

/// <summary>
/// One desktop: the smoke tests run one at a time and share the server.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ETestGroup : ICollectionFixture<ServerFixture>
{
    public const string Name = "E2E smoke tests";
}