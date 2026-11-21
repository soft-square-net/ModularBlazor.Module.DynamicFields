using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.EditDraft;

/// <summary>
/// Replace a field's draft. Published versions do not change; the next publish makes the draft a new version.
/// <paramref name="Version"/> is the version the caller read.
/// </summary>
public sealed record EditFieldDraftCommand(string Key, int Version, FieldDraft Draft) : ICommand<Result<FieldDetailDto>>;

public sealed class EditFieldDraftHandler(IRepository<FieldDefinition> fields, TimeProvider clock)
    : ICommandHandler<EditFieldDraftCommand, Result<FieldDetailDto>>
{
    public async ValueTask<Result<FieldDetailDto>> Handle(EditFieldDraftCommand command, CancellationToken ct)
    {
        var field = await fields.FirstOrDefaultAsync(new FieldByKeySpec(command.Key), ct);
        if (field is null) return Result<FieldDetailDto>.NotFound(FieldResults.NotFoundMessage(command.Key));
        if (field.Version != command.Version) return Result<FieldDetailDto>.Conflict(FieldResults.StaleMessage(command.Key, field.Version, command.Version));

        field.EditDraft(command.Draft, clock.GetUtcNow());
        await fields.UpdateAsync(field, ct);
        return FieldDetailDto.From(field);
    }
}
