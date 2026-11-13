using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.KingdomAllegiance;
using TAOM.Features.KingdomAllegiance.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.KingdomAllegiance;

/// <summary>
/// Wiring guards for Kingdom Allegiance (#1111). Each piece fails silently when unwired: drop the module line and the patch never
/// applies; drop the SubModule arguments and the model cannot be built; drop the statics handshake and the postfix has no service.
/// Also the postfix itself, the MCM switch and its default.
/// </summary>
[TestClass]
public class KingdomAllegianceWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new KingdomAllegianceModule().RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance<IOutcomeLog>(new OutcomeLog(Substitute.For<IModLogger>()));
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<KingdomAllegianceModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.KingdomAllegiance.KingdomAllegianceModule()"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndAppliesOneProcessLoadCategory()
    {
        var module = new KingdomAllegianceModule();

        Assert.AreEqual("KingdomAllegiance", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch145_KingdomAllegiance", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations_AsOneSharedInstance()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<IKingdomAllegianceSettingsProvider>());
        Assert.IsNotNull(container.Resolve<TAOM.Features.KingdomAllegiance.Models.AllegianceFactsReader>());
        Assert.IsNotNull(container.Resolve<IAllegianceRelationAdapter>());
        Assert.AreSame(container.Resolve<KingdomAllegianceService>(), container.Resolve<KingdomAllegianceService>());
    }

    [TestMethod]
    public void TheModule_DoesNotRegisterWhatOtherFeaturesOwn()
    {
        var src = RepoPaths.ReadSource("Main/Features/KingdomAllegiance/KingdomAllegianceModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Register<IAlignmentService"));
        Assert.IsFalse(src.Contains("Register<IPlayerControlQuery"));
        Assert.IsFalse(src.Contains("Register<IOutcomeLog"));
    }

    [TestMethod]
    public void SubModule_HandsTheModelTheServiceAndTheAdapter()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var line = src.Substring(src.IndexOf("new TaomDiplomacyModel(", StringComparison.Ordinal), 400);

        StringAssert.Contains(line, "IoC.Resolve<Features.KingdomAllegiance.KingdomAllegianceService>()");
        StringAssert.Contains(line, "IoC.Resolve<Features.KingdomAllegiance.Models.AllegianceFactsReader>()");
    }

    [TestMethod]
    public void TheModelOverrides_DelegateToTheServiceFirst()
    {
        var src = RepoPaths.ReadSource("Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs", stripComments: true);

        StringAssert.Contains(src, "_allegiance.JoinVeto(_allegianceFacts.Read(clan, kingdom)) ?? base.GetScoreOfClanToJoinKingdom(clan, kingdom)");
        StringAssert.Contains(src, "_allegiance.JoinVeto(_allegianceFacts.Read(mercenaryClan, kingdom))");
        StringAssert.Contains(src, "?? base.GetScoreOfMercenaryToJoinKingdom(mercenaryClan, kingdom)");
    }

    // --- Patch145 -------------------------------------------------------------------------------------------------------

    private sealed class ThrowingLords : IAllegianceRelationAdapter
    {
        public IReadOnlyList<SideIds> ReadLords() => throw new InvalidOperationException("boom");
        public int ReadRelation(int first, int second) => 0;
        public void WriteRelation(int first, int second, int value) { }
        public void Release() { }
    }

    private static KingdomAllegianceService Service()
    {
        var settings = Substitute.For<IKingdomAllegianceSettingsProvider>();
        settings.Enabled.Returns(true);
        return new KingdomAllegianceService(settings, Substitute.For<IAlignmentService>(), Substitute.For<ICoopSessionProvider>(),
            new OutcomeLog(Substitute.For<IModLogger>()));
    }

    [TestMethod]
    public void Postfix_RunsTheFlipOverTheLordsItWasGiven()
    {
        var lords = Substitute.For<IAllegianceRelationAdapter>();
        lords.ReadLords().Returns(new List<SideIds>());
        Patch145_FriendshipSeeding_Postfix.Initialize(Service(), lords, Substitute.For<IModLogger>());

        Patch145_FriendshipSeeding_Postfix.Postfix();

        lords.Received(1).ReadLords();
    }

    [TestMethod]
    public void Postfix_AFault_KeepsVanillasRelations_AndIsLogged()
    {
        var logger = Substitute.For<IModLogger>();
        Patch145_FriendshipSeeding_Postfix.Initialize(Service(), new ThrowingLords(), logger);

        Patch145_FriendshipSeeding_Postfix.Postfix();

        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.Contains("[KingdomAllegiance]") && s.Contains("Patch145") && s.Contains("boom")));
    }

    [TestMethod]
    public void ThePatchClass_IsThinAndNamesItsCategoryByTheModulesConstant()
    {
        var src = RepoPaths.ReadSource("Main/Features/KingdomAllegiance/Hooks/Patch145_FriendshipSeeding_Postfix.cs", stripComments: true);

        Assert.IsTrue(src.Split('\n').Length < 150, "ADR-002: entry points stay under 150 lines");
        StringAssert.Contains(src, "[HarmonyPatchCategory(KingdomAllegianceModule.PatchCategory)]");
        StringAssert.Contains(src, "\"UpdateFriendshipAndEnemies\"");
    }

    // --- the MCM switch -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefault_On()
    {
        Assert.IsTrue(KingdomAllegianceSettingsProvider.DefaultEnabled);
        Assert.AreEqual(KingdomAllegianceSettingsProvider.DefaultEnabled, KingdomAllegianceSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefault_MatchesTheNoSettingsFallback()
    {
        Assert.AreEqual(KingdomAllegianceSettingsProvider.From(null), KingdomAllegianceSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void Enabled_ReadsTheSettingsItWasGiven_LiveAfterTheFirstRead()
    {
        var settings = new TaomSettings { KingdomAllegianceEnabled = true };
        var provider = new KingdomAllegianceSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.KingdomAllegianceEnabled = false;
        Assert.IsFalse(provider.Enabled);
    }
}
