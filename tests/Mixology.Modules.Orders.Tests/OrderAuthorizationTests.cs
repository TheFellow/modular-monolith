using Cedar.Types;
using Microsoft.Extensions.DependencyInjection;
using Mixology.Application.Authentication;
using Mixology.Authorization.Cedar;
using Mixology.Kernel.Entities;
using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Mixology.Modules.Orders.Authorization;
using Mixology.Modules.Orders.Models;
using Xunit;
using KernelEntityUid = Mixology.Kernel.Entities.EntityUid;

namespace Mixology.Modules.Orders.Tests;

public sealed class OrderAuthorizationTests
{
    private static readonly Order Resource = new(
        OrderId.New(),
        MenuId.New(),
        [new OrderItem(DrinkId.New(), 1, string.Empty)],
        [],
        [],
        OrderStatus.Pending,
        DateTimeOffset.UtcNow,
        null,
        string.Empty,
        null,
        TagCollection.Empty);

    public static TheoryData<Actor, KernelEntityUid, bool> Matrix
    {
        get
        {
            TheoryData<Actor, KernelEntityUid, bool> matrix = new();
            KernelEntityUid[] reads = [OrderAuthorization.List, OrderAuthorization.Get];
            KernelEntityUid[] writes =
            [
                OrderAuthorization.Place,
                OrderAuthorization.Complete,
                OrderAuthorization.Cancel,
                OrderAuthorization.Tag,
                OrderAuthorization.Untag,
            ];
            foreach (KernelEntityUid action in reads.Concat(writes))
            {
                matrix.Add(Actor.Owner, action, true);
                matrix.Add(Actor.Manager, action, true);
                matrix.Add(Actor.Bartender, action, true);
                matrix.Add(Actor.Sommelier, action, reads.Contains(action));
                matrix.Add(Actor.Anonymous, action, false);
            }

            return matrix;
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task PoliciesPreserveTheExactStaffReadAndOrderManagementMatrix(
        Actor actor,
        KernelEntityUid action,
        bool allowed)
    {
        ServiceCollection services = new();
        services.AddOrdersModule();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IEntityAuthorizer authorizer = provider.GetRequiredService<IEntityAuthorizer>();

        if (allowed)
        {
            await authorizer.AuthorizeAsync(actor, action, Resource.ToCedarEntity());
            return;
        }

        Exception denied = await Assert.ThrowsAsync<PermissionError>(async () =>
            await authorizer.AuthorizeAsync(actor, action, Resource.ToCedarEntity()));
        Assert.True(AppError.IsPermission(new InvalidOperationException("wrapped", denied)));
    }

    [Fact]
    public void ResourceMapsMenuStatusAndCanonicalOrderType()
    {
        Entity entity = Resource.ToCedarEntity();

        Assert.Equal(OrderAuthorization.ResourceType, entity.Uid.Type.Value);
        Assert.Equal(Resource.Id.Value, entity.Uid.Id.Value);
        Assert.Equal(
            Resource.MenuId.Value,
            Assert.IsType<Cedar.Types.EntityUid>(entity.Attributes[new CedarString("MenuID")]).Id.Value);
        Assert.Equal(
            OrderStatus.Pending.Value,
            Assert.IsType<CedarString>(entity.Attributes[new CedarString("Status")]).Value);
    }
}
