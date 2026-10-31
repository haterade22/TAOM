using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
[TestCategory("RequiresGame")]
public class FordsMissionLauncherTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static readonly Type[] SpecList =
    {
        typeof(MissionCombatantsLogic),
        typeof(AgentHumanAILogic),
        typeof(AmmoSupplyLogic),
        typeof(MissionHardBorderPlacer),
        typeof(MissionBoundaryPlacer),
        typeof(MissionOptionsComponent),
        typeof(FordsOfWarMissionLogic),
    };

    private static CustomBattleCombatant Combatant(BattleSideEnum side) =>
        new(new TextObject("{=!}test"), null, null) { Side = side };

    [TestMethod]
    public void BehaviorTypes_AreExactlyTheSpecListInOrder()
    {
        CollectionAssert.AreEqual(SpecList, FordsMissionLauncher.BehaviorTypes().ToArray());
    }

    [TestMethod]
    public void BehaviorTypes_LeaveOutEverythingTheSpecExcludes()
    {
        var names = FordsMissionLauncher.BehaviorTypes().Select(t => t.Name).ToList();

        foreach (var excluded in new[]
                 {
                     "DefaultBattleMissionAgentSpawnLogic", "BattleEndLogic", "BattleDeploymentMissionController", "BattleDeploymentHandler",
                     "AgentMoraleInteractionLogic", "MissionAgentPanicHandler", "AgentVictoryLogic", "AssignPlayerRoleInTeamMissionController",
                     "BattleMissionStarterLogic", "MissionBoundaryCrossingHandler",
                 })
            CollectionAssert.DoesNotContain(names, excluded);
    }

    [TestMethod]
    public void BehaviorTypes_EndWithTheFordsLogic_AndOpenWithTheCombatantsLogic()
    {
        var types = FordsMissionLauncher.BehaviorTypes();

        Assert.AreEqual(typeof(MissionCombatantsLogic), types[0], "the teams must exist before any later behavior starts");
        Assert.AreEqual(typeof(FordsOfWarMissionLogic), types[types.Count - 1], "the match starts after the boundary placers have run");
    }

    [TestMethod]
    public void CreateBehaviors_BuildsOneInstancePerListedType_InOrder()
    {
        var logger = Substitute.For<IModLogger>();
        var launcher = new FordsMissionLauncher(
            FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger), new FordsSession(), logger);

        var behaviors = launcher.CreateBehaviors(
            new FordsMatchSetup("taom_rohan_battle_001_forceatmo", "gondor", "mordor", "normal", 3),
            Combatant(BattleSideEnum.Defender), Combatant(BattleSideEnum.Attacker));

        CollectionAssert.AreEqual(SpecList, behaviors.Select(b => b.GetType()).ToArray());
    }

    [TestMethod]
    public void CreateBehaviors_AmmoIsSuppliedToBothSides()
    {
        var logger = Substitute.For<IModLogger>();
        var launcher = new FordsMissionLauncher(
            FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger), new FordsSession(), logger);

        var ammo = launcher.CreateBehaviors(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3),
            Combatant(BattleSideEnum.Defender), Combatant(BattleSideEnum.Attacker)).OfType<AmmoSupplyLogic>().Single();

        var sides = (System.Collections.Generic.List<BattleSideEnum>)typeof(AmmoSupplyLogic)
            .GetField("_sideList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ammo)!;
        CollectionAssert.AreEquivalent(new[] { BattleSideEnum.Defender, BattleSideEnum.Attacker }, sides);
    }

    [TestMethod]
    public void WestSide_IsTheDefenderAndEastTheAttacker()
    {
        Assert.AreEqual(BattleSideEnum.Defender, FordsMissionLauncher.WestSide);
        Assert.AreEqual(BattleSideEnum.Attacker, FordsMissionLauncher.EastSide);
    }

    [TestMethod]
    public void TheLauncher_SpawnsNoPlayerAgent()
    {
        // The mission keeps no main agent (build-spec 6.1): nothing here may name the player troop or a controller type.
        var source = RepoPaths.ReadSource("Main/Features/FordsOfWar/Hooks/FordsMissionLauncher.cs", stripComments: true);

        foreach (var banned in new[] { "PlayerTroop", "AgentControllerType", "SpawnLogic", "BattleEndLogic" })
            Assert.IsFalse(source.Contains(banned), "the launcher must not name " + banned);
    }

    [TestMethod]
    public void TheLauncher_OpensTheMissionUnderTheFordsNameAsANonCampaignMission()
    {
        var source = RepoPaths.ReadSource("Main/Features/FordsOfWar/Hooks/FordsMissionLauncher.cs", stripComments: true);

        StringAssert.Contains(source, "MissionState.OpenNew(FordsOfWarMission.MissionName");
        StringAssert.Contains(source, "PlayingInCampaignMode = false");
    }
}
