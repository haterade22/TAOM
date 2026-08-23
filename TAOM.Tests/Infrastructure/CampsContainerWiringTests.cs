using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.Refuge;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Registration guard for the three camps features TOGETHER, the EnlistmentContainerWiringTests
/// shape: DryIoc's Validate() walks the dependency graph without constructing, so it runs with no
/// live Campaign and still proves the production wiring resolves.
///
/// <para>Why together: the three features form a cross-feature graph
/// (CampService consumes the contributor collection, Refuge's contributor consumes IRefugeService,
/// RefugeService consumes ICampService). A per-feature validation cannot see a cycle that only
/// closes when all three register, which is exactly how the Codex round-2 P1 startup cycle
/// escaped a 7,400-test suite: the source-scan gates checked registration STYLE, nothing resolved
/// the finished graph.</para>
/// </summary>
[TestClass]
public class CampsContainerWiringTests
{
    private static IContainer BuildContainer()
    {
        var container = new Container();

        // Cross-feature dependencies owned by other registration modules.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());

        SupplyLinesIoC.RegisterSupplyLinesFeature(container);
        FieldCampIoC.RegisterFieldCampFeature(container);
        RefugeIoC.RegisterRefugeFeature(container);
        return container;
    }

    [TestMethod]
    public void CampsGraph_EveryServiceRoot_ResolvableWithAllThreeFeaturesRegistered()
    {
        var container = BuildContainer();

        var errors = container.Validate(
            typeof(ISupplyOrderService),
            typeof(ICampService),
            typeof(IRefugeService),
            typeof(IRefugeDefenseService));

        Assert.AreEqual(
            0,
            errors.Length,
            "The camps dependency graph does not resolve (this is how the module fails at "
                + "startup, before Harmony registration): "
                + string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
