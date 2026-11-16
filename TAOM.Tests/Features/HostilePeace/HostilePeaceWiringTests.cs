using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.Diplomacy;
using TAOM.Features.HostilePeace;
using TAOM.Features.HostilePeace.Hooks;
using TAOM.Features.HostilePeace.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HostilePeace;

/// <summary>
/// Wiring for Hostile Peace. Each piece fails silently when unwired: drop the module line and Patch156 never applies; drop the
/// SubModule lines and blocks B and C never run; rename a postfix argument and Harmony binds nothing; drop a switch and the
/// player cannot turn a block off. Also the access answers against the base game's war answers, the JSON file and its defaults.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HostilePeaceWiringTests
{
    // ---- The module ----

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HostilePeaceModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.HostilePeace.HostilePeaceModule()"));
    }

    [TestMethod]
    public void TheModule_OwnsNoSaveData_AndAppliesOneProcessLoadCategory()
    {
        var module = new HostilePeaceModule();

        Assert.AreEqual("HostilePeace", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch156_HostilePeace", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void TheModule_ResolvesOneSharedService_FromItsOwnRegistrations()
    {
        using var container = new Container();
        new HostilePeaceModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IDiplomacyService>());
        container.RegisterInstance(Substitute.For<TAOM.Features.Execution.IAlignmentService>());

        Assert.IsNotNull(container.Resolve<IHostilePeaceSettingsProvider>());
        Assert.AreSame(container.Resolve<HostilePeaceService>(), container.Resolve<HostilePeaceService>());
    }

    [TestMethod]
    public void SubModule_AddsBothModels_WithTheSharedService()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(src, "IoC.Resolve<Features.HostilePeace.HostilePeaceService>()");
        StringAssert.Contains(src, "campaignStarter.AddModel(new TaomTradeAgreementModel(hostilePeace, coopSession))");
        StringAssert.Contains(src, "campaignStarter.AddModel(new TaomSettlementAccessModel(hostilePeace))");
        StringAssert.Contains(src, "campaignStarter.AddModel(new TaomCrimeModel(hostilePeace))");
    }

    [TestMethod]
    public void SubModule_HandsTheServiceToAllThreePlayerAlliancePaths()
    {
        // Block D: a path built without the service keeps the old full freedom, silently.
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(src, "new PlayerAllianceProposalBehavior(diplomacyService, coopSession, diplomacyLogger, hostilePeace)");
        StringAssert.Contains(src, "new TaomAllianceModel(diplomacyService, hostilePeace)");
        StringAssert.Contains(src, "IoC.Resolve<IOnAllianceAction>(), coopSession, hostilePeace)");
    }

    [TestMethod]
    public void EveryPlayerAlliancePath_AsksTheSameGate()
    {
        foreach (var file in new[]
                 {
                     "Main/Features/Diplomacy/Models/TaomAllianceModel.cs",
                     "Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs",
                     "Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs",
                 })
        {
            var src = RepoPaths.ReadSource(file, stripComments: true);
            StringAssert.Contains(src, "PlayerAllianceGate.Blocks(_hostilePeace,", $"{file} does not ask block D");
        }
    }

    [TestMethod]
    public void TheAllianceModel_GivesACrossSidePlayerPairTheHostilePenalty()
    {
        Assert.AreEqual(-10000f, TAOM.Features.Diplomacy.Models.TaomAllianceModel.CrossSidePlayerPenalty);
    }

    [TestMethod]
    public void NoCrossSideAlliance_HasAReasonForTheKingdomScreen()
    {
        StringAssert.Contains(HostilePeaceTexts.NoCrossSideAlliance().ToString(), "opposite sides");
    }

    // ---- Block A: the postfix binds to the engine method ----

    [TestMethod]
    public void Patch156_TargetsCanTradeWith_AndItsArgumentNamesMatch()
    {
        var target = AccessTools.Method(typeof(CaravansCampaignBehavior), "CanTradeWith", new[] { typeof(IFaction), typeof(IFaction) });
        Assert.IsNotNull(target, "CaravansCampaignBehavior.CanTradeWith(IFaction, IFaction) is gone; Patch156 has no target");
        Assert.AreEqual(typeof(bool), target!.ReturnType);

        // Harmony binds a postfix argument to the target's parameter by name.
        var targetNames = target.GetParameters().Select(p => p.Name).ToArray();
        var postfix = typeof(Patch156_CaravanTrade_HostilePeace).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)!;
        foreach (var name in postfix.GetParameters().Select(p => p.Name).Where(n => !n!.StartsWith("__", StringComparison.Ordinal)))
            CollectionAssert.Contains(targetNames, name, $"the postfix argument '{name}' matches no parameter of CanTradeWith");

        var category = typeof(Patch156_CaravanTrade_HostilePeace).GetCustomAttribute<HarmonyPatchCategory>();
        Assert.AreEqual(HostilePeaceModule.PatchCategory, category?.info.category);
    }

    // ---- Blocks B and C: the models override the right members ----

    [TestMethod]
    public void TheModels_OverrideTheBaseGamesDefaultModels()
    {
        Assert.AreEqual(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultTradeAgreementModel), typeof(TaomTradeAgreementModel).BaseType);
        Assert.AreEqual(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementAccessModel), typeof(TaomSettlementAccessModel).BaseType);
        Assert.AreEqual(typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultCrimeModel), typeof(TaomCrimeModel).BaseType);
        Assert.AreEqual(typeof(TaomCrimeModel), typeof(TaomCrimeModel).GetMethod("GetEffectiveCrimeChange")!.DeclaringType);
        Assert.AreEqual(typeof(TaomTradeAgreementModel), typeof(TaomTradeAgreementModel).GetMethod("CanMakeTradeAgreement")!.DeclaringType);
        Assert.AreEqual(typeof(TaomSettlementAccessModel), typeof(TaomSettlementAccessModel).GetMethod("CanMainHeroEnterSettlement")!.DeclaringType);
        Assert.AreEqual(typeof(TaomSettlementAccessModel), typeof(TaomSettlementAccessModel).GetMethod("CanMainHeroDoSettlementAction")!.DeclaringType);
        Assert.AreEqual(typeof(TaomSettlementAccessModel), typeof(TaomSettlementAccessModel).GetMethod("CanMainHeroEnterLordsHall")!.DeclaringType,
            "a castle's 'Request entry' asks the lords hall answer, so it must be overridden too");
    }

    // ---- Block C: the base game's war answers ----

    [TestMethod]
    public void AsAtWar_Town_IsTheBaseGamesWarAnswer_SoTheSneakInOptionShows()
    {
        var access = HostilePeaceAccess.AsAtWar(isTown: true);

        Assert.AreEqual(SettlementAccessModel.AccessLevel.LimitedAccess, access.AccessLevel);
        Assert.AreEqual(SettlementAccessModel.LimitedAccessSolution.Disguise, access.LimitedAccessSolution);
        Assert.AreEqual(SettlementAccessModel.AccessLimitationReason.HostileFaction, access.AccessLimitationReason);
    }

    [TestMethod]
    public void AsAtWar_Castle_IsTheBaseGamesWarAnswer()
    {
        var access = HostilePeaceAccess.AsAtWar(isTown: false);

        Assert.AreEqual(SettlementAccessModel.AccessLevel.NoAccess, access.AccessLevel);
        Assert.AreEqual(SettlementAccessModel.AccessMethod.ByRequest, access.AccessMethod);
        Assert.AreEqual(SettlementAccessModel.AccessLimitationReason.HostileFaction, access.AccessLimitationReason);
    }

    [TestMethod]
    public void KeepAsAtWar_IsTheBaseGamesWarAnswerForTheKeep_SoRequestEntryIsNotGranted()
    {
        // DefaultSettlementAccessModel.CanMainHeroEnterKeepInternal, war branch: limited access by disguise, reason Disguised.
        // The castle menu grants entry only for FullAccess or a Bribe solution, so this answer lands on "entry denied".
        var access = HostilePeaceAccess.KeepAsAtWar();

        Assert.AreEqual(SettlementAccessModel.AccessLevel.LimitedAccess, access.AccessLevel);
        Assert.AreEqual(SettlementAccessModel.LimitedAccessSolution.Disguise, access.LimitedAccessSolution);
        Assert.AreEqual(SettlementAccessModel.AccessLimitationReason.Disguised, access.AccessLimitationReason);
    }

    [DataTestMethod]
    [DataRow(SettlementAccessModel.SettlementAction.Trade, "trade with a hostile village")]
    [DataRow(SettlementAccessModel.SettlementAction.RecruitTroops, "recruit troops from a hostile village")]
    [DataRow(SettlementAccessModel.SettlementAction.WaitInSettlement, "wait in a hostile village")]
    public void VillageRefusal_TheThreeActionsWarRefuses_UseTheWarText(SettlementAccessModel.SettlementAction action, string words)
    {
        var text = HostilePeaceAccess.VillageRefusal(action);

        Assert.IsNotNull(text, $"{action} is refused at war, so it must be refused here too");
        StringAssert.Contains(text!.ToString(), words);
    }

    [DataTestMethod]
    [DataRow(SettlementAccessModel.SettlementAction.Craft)]
    [DataRow(SettlementAccessModel.SettlementAction.JoinTournament)]
    [DataRow(SettlementAccessModel.SettlementAction.ManageTown)]
    public void VillageRefusal_ActionsWarAllows_AreLeftAlone(SettlementAccessModel.SettlementAction action)
    {
        Assert.IsNull(HostilePeaceAccess.VillageRefusal(action));
    }

    [TestMethod]
    public void NoTradeAgreement_HasAReasonForTheKingdomScreen()
    {
        StringAssert.Contains(HostilePeaceTexts.NoTradeAgreement().ToString(), "will not trade");
    }

    // ---- The switches ----

    [TestMethod]
    public void Settings_McmWinsOverJson()
    {
        var config = Substitute.For<IHostilePeaceConfigProvider>();
        config.Config.Returns(new HostilePeaceConfig { BlockCaravanTrade = true, BlockTradeAgreements = true, LimitTownAccess = true });
        var settings = new TaomSettings
        {
            HostilePeaceBlockCaravanTrade = false,
            HostilePeaceBlockTradeAgreements = true,
            HostilePeaceLimitTownAccess = false,
            AllowPlayerAlliancesWithAnyone = true,
        };

        var provider = new HostilePeaceSettingsProvider(config, settings);

        Assert.IsFalse(provider.BlockCaravanTrade);
        Assert.IsTrue(provider.BlockTradeAgreements);
        Assert.IsFalse(provider.LimitTownAccess);
        Assert.IsTrue(provider.AllowPlayerAlliancesWithAnyone);
    }

    [TestMethod]
    public void Settings_McmDefaults_BlockEverything()
    {
        var settings = new TaomSettings();

        Assert.IsTrue(settings.HostilePeaceBlockCaravanTrade);
        Assert.IsTrue(settings.HostilePeaceBlockTradeAgreements);
        Assert.IsTrue(settings.HostilePeaceLimitTownAccess);
        Assert.IsFalse(settings.AllowPlayerAlliancesWithAnyone, "the player's cross-side alliances are blocked by default");
    }

    [TestMethod]
    public void Json_ShippedFile_TurnsEveryBlockOn()
    {
        var json = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "diplomacy", "hostile_peace.json"));
        var config = HostilePeaceConfigProvider.Parse(json);

        Assert.IsTrue(config.BlockCaravanTrade);
        Assert.IsTrue(config.BlockTradeAgreements);
        Assert.IsTrue(config.LimitTownAccess);
        Assert.IsFalse(config.AllowPlayerAlliancesWithAnyone);
    }

    [TestMethod]
    public void Json_OneSwitchOff_LeavesTheOthersOn()
    {
        var config = HostilePeaceConfigProvider.Parse("{ \"blockTradeAgreements\": false }");

        Assert.IsTrue(config.BlockCaravanTrade);
        Assert.IsFalse(config.BlockTradeAgreements);
        Assert.IsTrue(config.LimitTownAccess);
    }

    [TestMethod]
    public void Json_NullDocument_FallsBackToEveryBlockOn()
    {
        var config = HostilePeaceConfigProvider.Parse("null");

        Assert.IsTrue(config.BlockCaravanTrade && config.BlockTradeAgreements && config.LimitTownAccess);
    }
}
