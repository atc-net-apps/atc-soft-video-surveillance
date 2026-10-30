namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Layouts;

/// <summary>
/// Handler business logic for the CreateLayout operation.
/// </summary>
public sealed class CreateLayoutHandler(
    ICameraStorageService storage) : ICreateLayoutHandler
{
    public Task<CreateLayoutResult> ExecuteAsync(
        CreateLayoutParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var layout = parameters.Request.ToCoreModel();
        storage.AddOrUpdateLayout(layout);
        storage.Save();

        return Task.FromResult(CreateLayoutResult.Created(layout.ToApiModel(), location: $"/layouts/{layout.Id}"));
    }
}