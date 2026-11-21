using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Get;

/// <summary>A field with its draft and its published versions.</summary>
public sealed record GetFieldQuery(string Key) : IQuery<Result<FieldDetailDto>>;

public sealed class GetFieldHandler(IReadRepository<FieldDefinition> fields) : IQueryHandler<GetFieldQuery, Result<FieldDetailDto>>
{
    public async ValueTask<Result<FieldDetailDto>> Handle(GetFieldQuery query, CancellationToken ct) =>
        await fields.FirstOrDefaultAsync(new FieldDetailSpec(query.Key), ct) is { } field
            ? FieldDetailDto.From(field)
            : Result<FieldDetailDto>.NotFound(FieldResults.NotFoundMessage(query.Key));
}
