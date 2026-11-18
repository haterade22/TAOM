using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.AiPartySize;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.HostilePeace;
using TAOM.Features.MarriageAlignment;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.Diplomacy;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.CampaignStartOptions;

[TestClass]
public class CampaignStartOptionsWiringTests
{
    private static Container Build()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<TAOM.Adapters.IPartySizeCacheInvalidator>());
        CampaignStartOptionsIoC.RegisterCampaignStartOptionsFeature(container);
        container.Register<ITaomSettingsProvider, TaomSettingsProvider>(Reuse.Singleton);
        container.Register<IWarOfTheRingConfigProvider, WarOfTheRingConfigProvider>(Reuse.Singleton);
        container.Register<ITroopWeightSettingsProvider, TroopWeightSettingsProvider>(Reuse.Singleton);

        // The defaults read the real War of the Ring service, which reads the store: a cycle through it would close here.
        container.RegisterInstance(Substitute.For<TAOM.Features.Diplomacy.IDiplomacyService>());
        container.RegisterInstance(Substitute.For<TAOM.Adapters.IAllianceAdapter>());
        container.Register<TAOM.Features.Diplomacy.IWarOfTheRingService, TAOM.Features.Diplomacy.WarOfTheRingService>(Reuse.Singleton);

        // The six more providers the defaults read, real ones over stubbed JSON config providers: each reads the
        // store, so a cycle through any of them would close here.
        container.RegisterInstance(Substitute.For<IMomentumConfigProvider>());
        container.Register<IMomentumSettingsProvider, MomentumSettingsProvider>(Reuse.Singleton);
        container.Register<IEyeOfSauronSettings, EyeOfSauronSettingsProvider>(Reuse.Singleton);
        container.Register<IAiPartyScalingSettingsProvider, AiPartyScalingSettingsProvider>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IRecruitmentAlignmentConfigProvider>());
        container.Register<IRecruitmentAlignmentSettingsProvider, RecruitmentAlignmentSettingsProvider>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IMarriageAlignmentConfigProvider>());
        container.Register<IMarriageAlignmentSettingsProvider, MarriageAlignmentSettingsProvider>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IAlignmentDesertionConfigProvider>());
        container.Register<IAlignmentDesertionSettingsProvider, AlignmentDesertionSettingsProvider>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IHostilePeaceConfigProvider>());
        container.Register<IHostilePeaceSettingsProvider, HostilePeaceSettingsProvider>(Reuse.Singleton);
        return container;
    }

    // Defaults read the Troop Weight provider, which read the store: no cycle may close.
    [TestMethod]
    public void TheStoreAndTheDefaultsResolve_WithoutADependencyCycle()
    {
        using var container = Build();

        Assert.IsNotNull(container.Resolve<ICampaignStartDefaults>());
        Assert.IsNotNull(container.Resolve<ITroopWeightSettingsProvider>());
        Assert.IsNotNull(container.Resolve<CampaignOptionEditor>());
        Assert.IsNotNull(container.Resolve<CampaignStartMismatchNotice>());
    }

    [TestMethod]
    public void TheReadersAndTheSession_ShareOneStore()
    {
        using var container = Build();

        // The session refreshes the concrete store; the readers must be handed that same object.
        Assert.AreSame((object)container.Resolve<ICampaignStartOptions>(), (object)container.Resolve<CampaignStartOptionsStore>());
    }

    [TestMethod]
    public void BeginningASession_WithNoCampaign_DoesNothing()
    {
        using var container = Build();

        container.Resolve<ICampaignStartSession>().Begin();

        Assert.IsFalse(container.Resolve<ICampaignStartOptions>().IsPinned);
    }

    // The session pins the start screen's choices before any reader runs, so it must be the first statement
    // of the CampaignGameStarter block in SubModule.OnGameStart. Source text, because the engine cannot run here.
    [TestMethod]
    public void OnGameStart_BeginsTheStartSession_AsTheFirstStatementOfTheCampaignBlock()
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "SubModule.cs"));
        var lines = System.IO.File.ReadAllLines(path);

        int block = System.Array.FindIndex(lines, l => l.Contains("if (gameStarterObject is CampaignGameStarter campaignStarter)"));
        Assert.IsTrue(block >= 0, "the CampaignGameStarter block in OnGameStart moved");

        var first = lines.Skip(block + 2) // past the `if` line and its opening brace
            .Select(l => l.Trim())
            .First(l => l.Length > 0 && !l.StartsWith("//", System.StringComparison.Ordinal));
        StringAssert.Contains(first, "ICampaignStartSession>().Begin();");
    }
}
