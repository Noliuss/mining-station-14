using System.Threading.Tasks;
using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo.Prototypes;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class CargoTest
{
    [Test]
    public async Task NoStaticPriceAndStackPrice()
    {
        await using var pairTracker = await PoolManager.GetServerClient(new PoolSettings{NoClient = true});
        var server = pairTracker.Pair.Server;

        var testMap = await PoolManager.CreateTestMap(pairTracker);

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var mapId = testMap.MapId;
            var grid = mapManager.CreateGrid(mapId);
            var coord = new EntityCoordinates(grid.Owner, 0, 0);

            var protoIds = protoManager.EnumeratePrototypes<EntityPrototype>()
                .Where(p=>!p.Abstract)
                .Select(p => p.ID)
                .ToList();

            foreach (var proto in protoIds)
            {
                var ent = entManager.SpawnEntity(proto, coord);

                if (entManager.TryGetComponent<StackPriceComponent>(ent, out var stackpricecomp)
                    && stackpricecomp.Price > 0)
                {
                    if (entManager.TryGetComponent<StaticPriceComponent>(ent, out var staticpricecomp))
                    {
                        Assert.That(staticpricecomp.Price, Is.EqualTo(0),
                            $"The prototype {proto} have a StackPriceComponent and StaticPriceComponent whose values are not compatible with each other.");
                    }
                }
                entManager.DeleteEntity(ent);
            }
            mapManager.DeleteMap(mapId);
        });
        await pairTracker.CleanReturnAsync();
    }
}
