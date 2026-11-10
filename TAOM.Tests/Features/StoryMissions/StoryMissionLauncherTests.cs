using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.StoryMissions.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StoryMissions;

[TestClass]
[TestCategory("RequiresGame")]
public class StoryMissionLauncherTests
{
    private const string LauncherPath = "Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs";
    private const string LongDefeatLauncherPath = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLauncher.cs";

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static string Source() => RepoPaths.ReadSource(LauncherPath, stripComments: true);

    private static StoryMissionLauncher Launcher(string? json, IModLogger logger) =>
        new(StoryMissionCatalog.FromText(json, logger), logger, Substitute.For<IOutcomeLog>());

    private static string Mission(string side) =>
        "{\"missions\":[{\"id\":\"m1\",\"scene\":\"scene_a\",\"side\":\"" + side + "\",\"defenderCulture\":\"vlandia\",\"attackerCulture\":\"isengard\","
        + "\"garrison\":[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}],"
        + "\"roster\":{\"waves\":[{\"count\":110,\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}]}}]}";

    // The behavior types the engine is given, in order: the new-expressions that start a line of CreateBehaviors.
    private static string[] BuiltNames(string source)
    {
        var body = source.Substring(source.IndexOf("CreateBehaviors(", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("private static List<MissionSiegeWeapon> Weapons(", StringComparison.Ordinal));
        return Regex.Matches(body, @"^\s+new (\w+)\(", RegexOptions.Multiline).Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
    }

    [TestMethod]
    public void Launch_AnUnknownMission_IsRefusedWithoutTouchingTheEngine()
    {
        var logger = Substitute.For<IModLogger>();

        var opened = Launcher(Mission("Defend"), logger).Launch("nowhere", false);

        Assert.IsFalse(opened);
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("nowhere")));
    }

    [TestMethod]
    public void Launch_AnAttackingMission_IsRefusedUntilThePlayerCanTakeThatSide()
    {
        var logger = Substitute.For<IModLogger>();

        var opened = Launcher(Mission("Attack"), logger).Launch("m1", false);

        Assert.IsFalse(opened);
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("m1") && s.Contains("attacking")));
    }

    [TestMethod]
    public void Launch_WithNoCampaignOrNoTroopsTheEngineKnows_IsRefusedAndNeverThrows()
    {
        var logger = Substitute.For<IModLogger>();

        var opened = Launcher(Mission("Defend"), logger).Launch("m1", false);

        Assert.IsFalse(opened);
        logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TheLauncher_NeverAssignsThePlayerTroop_BecauseInACampaignItIsTheMainHero()
    {
        // Campaign.cs:1334 and ChangePlayerCharacterAction.cs:15 keep Game.PlayerTroop on the player's hero; a Long Defeat launch overwrites it with a Custom Game stub.
        var assignment = new Regex(@"PlayerTroop\s*=(?!=)");

        Assert.IsFalse(assignment.IsMatch(Source()), "the story launcher assigns PlayerTroop");
        Assert.IsTrue(assignment.IsMatch("Game.Current.PlayerTroop = player;"), "the pin must see an assignment");
        Assert.IsTrue(assignment.IsMatch("PlayerTroop=player;"), "the pin must see an assignment without spaces");
        Assert.IsFalse(assignment.IsMatch("if (Game.Current.PlayerTroop == null)"), "a comparison is not an assignment");
        Assert.IsTrue(Regex.IsMatch(LongDefeatSource(), assignment.ToString()), "the Long Defeat launcher is the one that assigns it");
    }

    private static string LongDefeatSource() => RepoPaths.ReadSource(LongDefeatLauncherPath, stripComments: true);

    [TestMethod]
    public void TheLauncher_OpensACampaignMissionWithFriendlyDamageOff()
    {
        var source = Source();

        StringAssert.Contains(source, "MissionState.OpenNew(StoryMission.MissionName");
        StringAssert.Contains(source, "PlayingInCampaignMode = true");
        StringAssert.Contains(source, "DamageToFriendsMultiplier = 0f");
        StringAssert.Contains(source, "DamageFromPlayerToFriendsMultiplier = 0f");
    }

    [TestMethod]
    public void TheLauncher_ClearsIsFriendlyMission_SoTabAndTheOrderBarWork()
    {
        var source = Source();

        StringAssert.Contains(source, "IsFriendlyMission = false");
        StringAssert.Contains(source, "SetPlayerCanTakeControlOfAnotherAgentWhenDead()");
    }

    [TestMethod]
    public void TheLauncher_GivesEachLaunchItsOwnSession_AndLeavesByEndingTheMission()
    {
        var source = Source();

        StringAssert.Contains(source, "new LongDefeatSession()");
        StringAssert.Contains(source, ".EndMission");
        Assert.IsFalse(source.Contains("EndGame"), "EndGame drops the player to the main menu and loses the campaign");
        Assert.IsFalse(source.Contains("ILongDefeatMenuAdapter"));
    }

    [TestMethod]
    public void TheLauncher_WritesNoRunRecordsAndTakesNoClass()
    {
        var source = Source();

        Assert.IsFalse(source.Contains("LongDefeatRunRecords"), "no report file and no best-run store for a story run");
        Assert.IsFalse(source.Contains("LongDefeatTroopGroups"), "no boons, so no troop groups");
    }

    [TestMethod]
    public void CreateBehaviors_IsTheLongDefeatSiegeListPlusTheStoryLogic()
    {
        var story = BuiltNames(Source());
        var longDefeat = BuiltNames(LongDefeatSource());

        CollectionAssert.AreEqual(longDefeat.Concat(new[] { "StoryMissionLogic" }).ToArray(), story);
    }

    [TestMethod]
    public void CreateBehaviors_LeaveOutEverythingThatReadsAMapEventOrEndsTheSiege()
    {
        var names = BuiltNames(Source());
        var source = Source();

        foreach (var banned in new[] { "BattleAgentLogic", "CampaignMissionComponent", "BattleEndLogic", "MissionPreloadView", "MountAgentLogic" })
        {
            CollectionAssert.DoesNotContain(names, banned);
            Assert.IsFalse(source.Contains(banned), banned + " reads MapEvent.PlayerMapEvent or ends a siege this mode runs on purpose");
        }

        Assert.IsFalse(source.Contains("GeneralsAndCaptainsAssignmentLogic"), "it throws in OnInitialSpawnCompleted without DefaultBattleMissionAgentSpawnLogic");
    }

    [TestMethod]
    public void TheMissionName_IsTheKeyOfTheClientsViewList()
    {
        Assert.AreEqual("TaomStoryMission", StoryMission.MissionName);
    }
}
