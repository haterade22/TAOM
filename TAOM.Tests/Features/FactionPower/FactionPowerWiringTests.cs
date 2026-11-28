using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.CoopInterop;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionPower;
using TAOM.Features.FactionPower.Hooks;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
public class FactionPowerWiringTests
{
    private static Container Build()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IFactionConfigProvider>());
        container.RegisterInstance(Substitute.For<IAdvancedStartDataAdapter>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(Substitute.For<IPartySizeCacheInvalidator>());
        FactionPowerIoC.RegisterFactionPowerFeature(container);
        return container;
    }

    [TestMethod]
    public void TheFeature_ResolvesWithoutADependencyCycle()
    {
        using var container = Build();

        Assert.IsNotNull(container.Resolve<IFactionPowerSettings>());
        Assert.IsNotNull(container.Resolve<IFactionPowerCatalog>());
        Assert.IsNotNull(container.Resolve<FactionPowerService>());
        Assert.IsNotNull(container.Resolve<FactionPowerEditor>());
        Assert.IsNotNull(container.Resolve<IFactionPowerPartyHook>());
        Assert.IsNotNull(container.Resolve<IFactionPowerEconomyHook>());
        Assert.IsNotNull(container.Resolve<IFactionPowerSession>());
        Assert.IsNotNull(container.Resolve<IFactionPowerStrikeHook>());
    }

    [TestMethod]
    public void TheSettings_AreOneSharedStore()
    {
        using var container = Build();

        Assert.AreSame(container.Resolve<IFactionPowerSettings>(), container.Resolve<IFactionPowerSettings>());
        Assert.AreSame((object)container.Resolve<IFactionPowerSettings>(), (object)container.Resolve<FactionPowerStore>());
    }

    [TestMethod]
    public void TheSessionBegin_ReloadsTheFactionPowerStore()
    {
        var settings = new FakeFactionPowerSettings();
        var data = new FakeAdvancedStartData { IsAvailable = false };
        var logger = Substitute.For<IModLogger>();
        var store = new CampaignStartOptionsStore(data, logger);
        var session = new CampaignStartSession(data, store, Substitute.For<ICampaignStartDefaults>(), logger,
            new FactionPowerSession(data, new FakeFactionPowerCatalog("gondor"), settings, logger));

        session.Begin();

        Assert.AreEqual(1, settings.Reloads);
    }

    [TestMethod]
    public void SubModuleAndTheContainerFile_NeedNoFactionPowerEdit_ForTheModelsToResolveIt()
    {
        var model = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        StringAssert.Contains(model, "IoC.Resolve<IFactionPowerPartyHook>()");
        Assert.IsFalse(TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("FactionPower"));
    }
}
