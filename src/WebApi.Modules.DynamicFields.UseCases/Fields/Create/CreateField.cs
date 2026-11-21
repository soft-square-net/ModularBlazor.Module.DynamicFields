using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Create;

/// <summary>Define a new field, as a draft; the caller becomes its author. Its key cannot change later.</summary>
public sealed record CreateFieldCommand(string Key, FieldDraft Draft) : ICommand<Result<FieldDetailDto>>;

public sealed class CreateFieldHandler(IRepository<FieldDefinition> fields, IAccessScopes access, TimeProvider clock)
    : ICommandHandler<CreateFieldCommand, Result<FieldDetailDto>>
{
    public async ValueTask<Result<FieldDetailDto>> Handle(CreateFieldCommand command, CancellationToken ct)
    {
        if (access.UserId is not { Length: > 0 } me) return Result<FieldDetailDto>.Unauthorized("Sign in first.");

        var field = FieldDefinition.Create(command.Key, command.Draft, me, clock.GetUtcNow());
        if (await fields.AnyAsync(new FieldByKeySpec(field.Key), ct))
            return Result<FieldDetailDto>.Conflict($"Field '{field.Key}' exists already.");

        await fields.AddAsync(field, ct);
        return FieldDetailDto.From(field);
    }
}
