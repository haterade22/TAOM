using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleIoCTests
{
    private static Container Build()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IWarOfTheRingService>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPathService>());
        WarChronicleIoC.RegisterWarChronicleFeature(container);
        return container;
    }

    [TestMethod]
    public void EveryWarChronicleService_ResolvesFromTheContainerWithItsDependencies()
    {
        using var container = Build();

        Assert.IsNotNull(container.Resolve<IWarEffectService>());
        Assert.IsNotNull(container.Resolve<WarBaselineService>());
        Assert.IsNotNull(container.Resolve<IKingdomWarSnapshotAdapter>());
        Assert.IsNotNull(container.Resolve<WarLedgerService>());
        Assert.IsNotNull(container.Resolve<WarChronicleStateService>());
        Assert.IsNotNull(container.Resolve<WarChronicleTickService>());
        Assert.IsNotNull(container.Resolve<IWarChronicleSettingsProvider>());
    }

    [TestMethod]
    public void TheEscapePass_ResolvesWithItsAdapterAndService_AsSingletons()
    {
        using var container = Build();

        Assert.IsNotNull(container.Resolve<IPrisonerEscapeAdapter>());
        Assert.IsNotNull(container.Resolve<WarEscapeService>());
        Assert.AreSame(container.Resolve<WarEscapeDailyPass>(), container.Resolve<WarEscapeDailyPass>());
    }

    [TestMethod]
    public void Services_AreSingletons()
    {
        using var container = Build();

        Assert.AreSame(container.Resolve<IWarEffectService>(), container.Resolve<IWarEffectService>());
        Assert.AreSame(container.Resolve<WarBaselineService>(), container.Resolve<WarBaselineService>());
        Assert.AreSame(container.Resolve<RallyTierStore>(), container.Resolve<RallyTierStore>());
    }

    [TestMethod]
    public void TheRally_ResolvesWithItsConfigProvider_AsSingletons()
    {
        using var container = Build();

        Assert.IsInstanceOfType(container.Resolve<IRallyConfigProvider>(), typeof(RallyConfigProvider));
        Assert.AreSame(container.Resolve<IRallyConfigProvider>(), container.Resolve<IRallyConfigProvider>());
        Assert.AreSame(container.Resolve<RallyService>(), container.Resolve<RallyService>());
    }

    [TestMethod]
    public void TheRallyWritesTheSameStoreTheSaveReads()
    {
        using var container = Build();
        var store = container.Resolve<RallyTierStore>();

        store.SetTier("empire_w", 2);

        var saved = string.Concat(container.Resolve<WarChronicleStateService>().CaptureChunks());
        StringAssert.Contains(saved, "\"empire_w\":2");
    }
}
