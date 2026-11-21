using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.GetVersion;

/// <summary>A published version of a field; <paramref name="Number"/> 0 means the newest.</summary>
public sealed record GetFieldVersionQuery(string Key, int Number) : IQuery<Result<FieldVersionDto>>;

public sealed class GetFieldVersionHandler(IReadRepository<FieldDefinition> fields)
    : IQueryHandler<GetFieldVersionQuery, Result<FieldVersionDto>>
{
    public async ValueTask<Result<FieldVersionDto>> Handle(GetFieldVersionQuery query, CancellationToken ct)
    {
        var field = await fields.FirstOrDefaultAsync(new FieldDetailSpec(query.Key), ct);
        if (field is null) return Result<FieldVersionDto>.NotFound(FieldResults.NotFoundMessage(query.Key));

        var number = query.Number == 0 ? field.LatestVersion : query.Number;
        return field.GetVersion(number) is { } version
            ? FieldVersionDto.From(version)
            : Result<FieldVersionDto>.NotFound(field.LatestVersion == 0
                ? $"Field '{query.Key}' has not been published."
                : $"Field '{query.Key}' has no version {number} (the newest is {field.LatestVersion}).");
    }
}
