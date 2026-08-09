using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Modules.Audit;
using Mixology.Modules.Audit.Authorization;
using Mixology.Modules.Audit.Models;
using Xunit;
using CedarEntity = Cedar.Types.Entity;

namespace Mixology.Modules.Audit.Tests;

public sealed class AuditAuthorizationTests
{
    public static TheoryData<Actor, EntityUid, bool> Matrix => new()
    {
        { Actor.Owner, AuditAuthorization.List, true },
        { Actor.Owner, AuditAuthorization.Get, true },
        { Actor.Manager, AuditAuthorization.List, false },
        { Actor.Manager, AuditAuthorization.Get, false },
        { Actor.Sommelier, AuditAuthorization.List, false },
        { Actor.Sommelier, AuditAuthorization.Get, false },
        { Actor.Bartender, AuditAuthorization.List, false },
        { Actor.Bartender, AuditAuthorization.Get, false },
        { Actor.Anonymous, AuditAuthorization.List, false },
        { Actor.Anonymous, AuditAuthorization.Get, false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task AuditReadsAreOwnerOnly(Actor actor, EntityUid action, bool allowed)
    {
        ServiceCollection services = new();
        services.AddAuditModule();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IEntityAuthorizer authorizer = provider.GetRequiredService<IEntityAuthorizer>();
        AuditEntry entry = Entry();

        if (allowed)
        {
            await authorizer.AuthorizeAsync(actor, action, entry.ToCedarEntity());
            return;
        }

        await Assert.ThrowsAsync<PermissionError>(async () =>
            await authorizer.AuthorizeAsync(actor, action, entry.ToCedarEntity()));
    }

    [Fact]
    public void ResourceConversionUsesTheDeclaredEmptyShape()
    {
        AuditEntry entry = Entry();

        CedarEntity entity = entry.ToCedarEntity();

        Assert.Equal(AuditAuthorization.ResourceType, entity.Uid.Type.Value);
        Assert.Equal(entry.Id.Value, entity.Uid.Id.Value);
        Assert.Empty(entity.Parents);
        Assert.Empty(entity.Attributes);
        Assert.Empty(entity.Tags);
    }

    private static AuditEntry Entry() => new(
        AuditEntryId.New(),
        "test",
        null,
        Actor.Owner,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch,
        true,
        null,
        null,
        []);
}
