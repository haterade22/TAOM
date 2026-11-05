using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatMissionLauncherTests
{
    [TestMethod]
    public void Launch_StampsThePlanWithTheBuildTheModsAndCheatMode()
    {
        var source = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "LongDefeat", "Hooks", "LongDefeatMissionLauncher.cs"));

        StringAssert.Contains(source, "Provenance = plan.Provenance with");
        StringAssert.Contains(source, "BuildStampReport.ReadInformationalVersion");
        StringAssert.Contains(source, "ModuleHelper.GetActiveModules");
        StringAssert.Contains(source, "NativeConfig.CheatMode");
    }

    private const string LauncherPath = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs";

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static readonly Type[] SpecList =
    {
        typeof(BattleSpawnLogic),
        typeof(MissionOptionsComponent),
        typeof(MissionCombatantsLogic),
        typeof(SiegeMissionPreparationHandler),
        typeof(AgentHumanAILogic),
        typeof(AmmoSupplyLogic),
        typeof(AssignPlayerRoleInTeamMissionController),
        typeof(MissionAgentPanicHandler),
        typeof(MissionBoundaryPlacer),
        typeof(MissionBoundaryCrossingHandler),
        typeof(AgentMoraleInteractionLogic),
        typeof(EquipmentControllerLeaveLogic),
        typeof(MissionSiegeEnginesLogic),
        typeof(LongDefeatMissionLogic),
    };

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private static LongDefeatConfigCatalog Catalog(IModLogger logger) =>
        LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), logger);

    private static LongDefeatMissionLauncher Launcher(LongDefeatConfigCatalog catalog, IModLogger logger, LongDefeatSession? session = null) =>
        new(catalog, session ?? new LongDefeatSession(), logger, Substitute.For<IOutcomeLog>(), Substitute.For<TAOM.Adapters.ILongDefeatMenuAdapter>(),
            LongDefeatRunRecords.ForFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-ld-unused"), null));

    private static CustomBattleCombatant Combatant(BattleSideEnum side) =>
        new(new TextObject("{=!}test"), null, null) { Side = side };

    // The siege engine types need a running Game, so the list is read from the source: the new-expressions that start a line in CreateBehaviors.
    private static string[] BuiltNames()
    {
        var source = RepoPaths.ReadSource(LauncherPath, stripComments: true);
        var body = source.Substring(source.IndexOf("CreateBehaviors(", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("private static List<MissionSiegeWeapon> Weapons(", StringComparison.Ordinal));
        return System.Text.RegularExpressions.Regex.Matches(body, @"^\s+new (\w+)\(", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Cast<System.Text.RegularExpressions.Match>().Select(x => x.Groups[1].Value).ToArray();
    }

    [TestMethod]
    public void CreateBehaviors_LeaveOutTheLogicThatNeedsTheSpawnLogic()
    {
        var names = BuiltNames().ToList();

        CollectionAssert.DoesNotContain(names, "GeneralsAndCaptainsAssignmentLogic");
        CollectionAssert.DoesNotContain(names, "BattleEndLogic");
        var source = RepoPaths.ReadSource(LauncherPath, stripComments: true);
        Assert.IsFalse(source.Contains("GeneralsAndCaptainsAssignmentLogic"), "it throws in OnInitialSpawnCompleted without DefaultBattleMissionAgentSpawnLogic");
    }

    [TestMethod]
    public void CreateBehaviors_NamesEachListedTypeOnce_InOrder()
    {
        CollectionAssert.AreEqual(SpecList.Select(t => t.Name).ToArray(), BuiltNames());
    }

    [TestMethod]
    public void EachMissionGetsItsOwnKey_SoTheOutcomeLogDoesNotLatchASecondRun()
    {
        var source = RepoPaths.ReadSource(LauncherPath, stripComments: true);

        StringAssert.Contains(source, "Interlocked.Increment(ref _missions)");
    }

    [TestMethod]
    public void Launch_AnUnknownFortressId_IsRefusedWithoutTouchingTheEngine()
    {
        var logger = Substitute.For<IModLogger>();
        var session = new LongDefeatSession();

        var opened = Launcher(Catalog(logger), logger, session).Launch(new LongDefeatSetup("nowhere", "test", RunOptions.Default));

        Assert.IsFalse(opened);
        Assert.AreEqual("Launch", session.Fault, "a refusal is a fault the autorun driver sees at once, not a 600 second wait");
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("nowhere")));
    }

    [TestMethod]
    public void Launch_ACultureOrTroopTheEngineLacks_IsRefusedAsAFault()
    {
        // No object manager is running in a test, so the shipped fortress's cultures and troops resolve to nothing.
        var logger = Substitute.For<IModLogger>();
        var session = new LongDefeatSession();

        var opened = Launcher(Catalog(logger), logger, session).Launch(new LongDefeatSetup("helms", "test", RunOptions.Default));

        Assert.IsFalse(opened);
        Assert.AreEqual("Launch", session.Fault);
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("cannot load 'helms'")));
    }

    [TestMethod]
    public void Launch_AnExceptionWhileOpening_IsLoggedAndBecomesAFault_NeverThrown()
    {
        var logger = Substitute.For<IModLogger>();
        var session = new LongDefeatSession();

        var opened = Launcher(Catalog(logger), logger, session).Launch(null!);

        Assert.IsFalse(opened);
        Assert.AreEqual("Launch", session.Fault);
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("opening the mission failed")));
    }

    [TestMethod]
    public void TheLauncher_ClearsIsFriendlyMission_SoTabAndTheOrderBarWork()
    {
        // IsFriendlyMission true makes Tab end the mission and leaves the order bar with no orders (phase1-plan.md, "Three findings").
        var source = RepoPaths.ReadSource(LauncherPath, stripComments: true);

        StringAssert.Contains(source, "IsFriendlyMission = false");
        StringAssert.Contains(source, "SetPlayerCanTakeControlOfAnotherAgentWhenDead()");
    }

    [TestMethod]
    public void TheLauncher_OpensTheMissionUnderTheLongDefeatNameAsANonCampaignMission()
    {
        var source = RepoPaths.ReadSource(LauncherPath, stripComments: true);

        StringAssert.Contains(source, "MissionState.OpenNew(LongDefeatMission.MissionName");
        StringAssert.Contains(source, "PlayingInCampaignMode = false");
    }

    [TestMethod]
    public void TheLogic_IsAMissionLogicThatAnswersTeamPower()
    {
        Assert.IsTrue(typeof(MissionLogic).IsAssignableFrom(typeof(LongDefeatMissionLogic)));
        Assert.IsTrue(typeof(IBattlePowerCalculationLogic).IsAssignableFrom(typeof(LongDefeatMissionLogic)));
    }
}
