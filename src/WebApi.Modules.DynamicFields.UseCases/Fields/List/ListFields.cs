using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.List;

/// <summary>One page of the tenant's fields by key, optionally matching a text, or only those that can be a post.</summary>
public sealed record ListFieldsQuery(int Page = 1, int PageSize = 20, string? Search = null, bool? CanBeRoot = null)
    : IQuery<Result<FieldPage>>;

public sealed class ListFieldsHandler(IReadRepository<FieldDefinition> fields, IOptions<DynamicFieldsOptions> options)
    : IQueryHandler<ListFieldsQuery, Result<FieldPage>>
{
    public async ValueTask<Result<FieldPage>> Handle(ListFieldsQuery query, CancellationToken ct)
    {
        var paging = FieldResults.CheckPage(query.Page, query.PageSize, options.Value.MaxPageSize);
        if (!paging.IsSuccess) return paging.As<FieldPage>();

        var spec = new FieldsPageSpec(query.Page, query.PageSize, query.Search, query.CanBeRoot);
        var items = await fields.ListAsync(spec, ct);
        var total = await fields.CountAsync(spec, ct); // counts without the paging
        return new FieldPage([.. items.Select(FieldSummaryDto.From)], query.Page, query.PageSize, total);
    }
}
