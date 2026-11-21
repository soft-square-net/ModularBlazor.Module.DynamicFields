using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Delete;

/// <summary>
/// Delete a field that was never published. A published one stays: content made with it must keep validating.
/// <paramref name="Version"/> is the version the caller read.
/// </summary>
public sealed record DeleteFieldCommand(string Key, int Version) : ICommand<Result>;

public sealed class DeleteFieldHandler(IRepository<FieldDefinition> fields) : ICommandHandler<DeleteFieldCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteFieldCommand command, CancellationToken ct)
    {
        var field = await fields.FirstOrDefaultAsync(new FieldByKeySpec(command.Key), ct);
        if (field is null) return Result.NotFound(FieldResults.NotFoundMessage(command.Key));
        if (field.Version != command.Version) return Result.Conflict(FieldResults.StaleMessage(command.Key, field.Version, command.Version));
        if (field.LatestVersion > 0)
            return Result.Conflict($"Field '{command.Key}' has published versions, which content may use; it cannot be deleted.");

        await fields.DeleteAsync(field, ct);
        return Result.NoContent();
    }
}
