using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Publish;

/// <summary>
/// Publish a field's draft as its next version, pinning each child field to that child's newest published version.
/// <paramref name="Version"/> is the version the caller read. Lookup properties must name lookups the catalog has
/// (<see cref="LookupBindings"/>). A root field's URL prefix must not be another published root field's.
/// </summary>
public sealed record PublishFieldCommand(string Key, int Version) : ICommand<Result<FieldVersionDto>>;

public sealed class PublishFieldHandler(IRepository<FieldDefinition> fields, IAccessScopes access, TimeProvider clock, ILookupClient lookups)
    : ICommandHandler<PublishFieldCommand, Result<FieldVersionDto>>
{
    public async ValueTask<Result<FieldVersionDto>> Handle(PublishFieldCommand command, CancellationToken ct)
    {
        if (access.UserId is not { Length: > 0 } me) return Result<FieldVersionDto>.Unauthorized("Sign in first.");

        var field = await fields.FirstOrDefaultAsync(new FieldByKeySpec(command.Key), ct);
        if (field is null) return Result<FieldVersionDto>.NotFound(FieldResults.NotFoundMessage(command.Key));
        if (field.Version != command.Version) return Result<FieldVersionDto>.Conflict(FieldResults.StaleMessage(command.Key, field.Version, command.Version));

        var lookupErrors = await LookupBindings.CheckAsync(field.Draft, lookups, ct);
        if (lookupErrors.Count > 0) return Result<FieldVersionDto>.Invalid(lookupErrors);

        var childKeys = field.Draft.Children.Select(c => c.FieldKey).ToList();
        var published = childKeys.Count == 0
            ? new Dictionary<string, int>()
            : (await fields.ListAsync(new FieldsByKeysSpec(childKeys), ct))
                .Where(c => c.LatestVersion > 0)
                .ToDictionary(c => c.Key, c => c.LatestVersion);

        var taken = field.Draft.CanBeRoot ? await fields.ListAsync(new TakenUrlPrefixesSpec(field.Key), ct) : [];

        var version = field.Publish(published, me, clock.GetUtcNow(), taken);
        await fields.UpdateAsync(field, ct);
        return FieldVersionDto.From(version);
    }
}
