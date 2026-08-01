using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the mission-side console reports: agent inspection, troop spawning, and the battle-scene
/// lookup. All three render from engine-free snapshots so every branch is testable without a live
/// mission — the engine-touching half is a thin boundary that cannot be unit-tested at all.
/// </summary>
[TestClass]
public class MissionReportFormatterTests
{
    private static AgentSnapshot Agent() => new AgentSnapshot
    {
        Index = 7,
        Name = "Gondor Tower Guard",
        CharacterId = "gondor_tower_guard",
        RaceName = "human",
        RaceId = 0,
        MonsterId = "human",
        ActionSetName = "as_human_warrior",
        SkeletonName = "human_skeleton",
        Health = 80f,
        MaxHealth = 100f,
        TeamLabel = "Attacker",
        FormationLabel = "Infantry",
        IsHuman = true,
        EquipmentSlots = new[] { "Weapon0: gondor_sword", "Body: gondor_mail" },
    };

    // ---- agent info -------------------------------------------------------

    [TestMethod]
    public void FormatAgent_FullSnapshot_RendersIdentityRaceAndRig()
    {
        var report = MissionReportFormatter.FormatAgent(Agent());

        StringAssert.Contains(report, "Gondor Tower Guard");
        StringAssert.Contains(report, "gondor_tower_guard");
        StringAssert.Contains(report, "human");
        StringAssert.Contains(report, "as_human_warrior");
        StringAssert.Contains(report, "human_skeleton");
        StringAssert.Contains(report, "80");
    }

    /// <summary>
    /// The validate-before-lookup contract surfaced in the output. `GetRaceNameFromId` coerces an
    /// unknown id to "human", so the boundary passes a null RaceName instead — and the report must
    /// say the id is invalid rather than confidently printing "human". A diagnostic that lies about
    /// the thing it exists to diagnose is worse than no diagnostic.
    /// </summary>
    [TestMethod]
    public void FormatAgent_InvalidRaceId_SaysInvalidRatherThanNamingARace()
    {
        var agent = Agent();
        agent.RaceName = null;
        agent.RaceId = 99;

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "INVALID");
        StringAssert.Contains(report, "99");
    }

    [TestMethod]
    public void FormatAgent_NullActionSetOrMonster_RendersPlaceholdersNotNullRefs()
    {
        var agent = Agent();
        agent.ActionSetName = null;
        agent.SkeletonName = null;
        agent.MonsterId = null;
        agent.EquipmentSlots = null;

        var report = MissionReportFormatter.FormatAgent(agent);

        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
        StringAssert.Contains(report, "?");
    }

    [TestMethod]
    public void FormatAgent_MountedAgent_RendersTheMount()
    {
        var agent = Agent();
        agent.MountMonsterId = "taom_warg";

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "taom_warg");
        StringAssert.Contains(report, "mount");
    }

    /// <summary>
    /// Asserts the KIND as well as the rider. An earlier version of this test set IsMount/IsHuman and
    /// then only asserted on RiderName — it would have passed against a formatter that ignored both
    /// flags entirely, which is exactly what the formatter did. Caught by the data-flow review.
    /// </summary>
    [TestMethod]
    public void FormatAgent_MountItself_RendersKindAndItsRider()
    {
        var agent = Agent();
        agent.IsMount = true;
        agent.IsHuman = false;
        agent.RiderName = "Goblin Rider";

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "Goblin Rider");
        StringAssert.Contains(report, "kind=mount");
    }

    [TestMethod]
    public void FormatAgent_NonHumanNonMount_IsLabelledDistinctlyFromHuman()
    {
        var agent = Agent();
        agent.IsHuman = false;
        agent.IsMount = false;

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "kind=non-human");
    }

    /// <summary>
    /// Zero is a plausible health value (a downed agent), so a failed read must never render as 0 —
    /// a reader would take "the read threw" for "this agent is dead".
    /// </summary>
    [TestMethod]
    public void FormatAgent_UnreadableHealth_RendersUnknownNotZero()
    {
        var agent = Agent();
        agent.Health = null;
        agent.MaxHealth = null;

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "hp=?/?");
        Assert.IsFalse(report.Contains("hp=0"));
    }

    /// <summary>The equipment block is SpawnEquipment, not the live loadout — the label must say so.</summary>
    [TestMethod]
    public void FormatAgent_Equipment_IsLabelledAsSpawnTime()
    {
        var report = MissionReportFormatter.FormatAgent(Agent());

        StringAssert.Contains(report, "at spawn");
    }

    [TestMethod]
    public void FormatAgent_NoEquipment_SaysSoRatherThanRenderingAnEmptyBlock()
    {
        var agent = Agent();
        agent.EquipmentSlots = new string[0];

        var report = MissionReportFormatter.FormatAgent(agent);

        StringAssert.Contains(report, "no equipment");
    }

    // ---- spawn ------------------------------------------------------------

    [TestMethod]
    public void FormatSpawn_AllSpawned_ReportsTheCount()
    {
        var report = MissionReportFormatter.FormatSpawn(new SpawnOutcome
        { TroopId = "taom_troll", Requested = 5, Spawned = 5, TeamLabel = "Defender" });

        StringAssert.Contains(report, "5");
        StringAssert.Contains(report, "taom_troll");
        StringAssert.Contains(report, "Defender");
    }

    /// <summary>
    /// Partial success is normal — one bad equipment roll should not read as total failure, but it
    /// must not read as success either.
    /// </summary>
    [TestMethod]
    public void FormatSpawn_PartialSuccess_ReportsBothNumbers()
    {
        var report = MissionReportFormatter.FormatSpawn(new SpawnOutcome
        { TroopId = "taom_troll", Requested = 5, Spawned = 3, TeamLabel = "Defender" });

        StringAssert.Contains(report, "3/5");
        StringAssert.Contains(report, "taom_debug");
    }

    /// <summary>
    /// The branch the review found unpinned: every spawn threw, so Spawned is 0 but no FailureReason
    /// was ever set (only the two pre-flight gates set that). The report must not read as success.
    /// </summary>
    [TestMethod]
    public void FormatSpawn_EveryTroopFailedWithNoGateFailure_DoesNotReadAsSuccess()
    {
        var report = MissionReportFormatter.FormatSpawn(new SpawnOutcome
        { TroopId = "taom_troll", Requested = 5, Spawned = 0, TeamLabel = "Defender" });

        StringAssert.Contains(report, "0/5");
        StringAssert.Contains(report, "5 failed");
    }

    [TestMethod]
    public void FormatSpawn_FailureReason_IsReportedVerbatimAndNoCountIsClaimed()
    {
        var report = MissionReportFormatter.FormatSpawn(new SpawnOutcome
        {
            TroopId = "taom_troll", Requested = 5, Spawned = 0,
            FailureReason = "This mission has no enemy team.",
        });

        StringAssert.Contains(report, "This mission has no enemy team.");
        Assert.IsFalse(report.Contains("0/5 spawned"));
    }

    // ---- battle scene -----------------------------------------------------

    [TestMethod]
    public void FormatBattleScene_WithCandidates_ListsEveryScene()
    {
        var report = MissionReportFormatter.FormatBattleScene(new BattleSceneQuery
        {
            X = 120.5f, Y = 340.25f, MapIndex = 42,
            CandidateSceneIds = new[] { "battle_terrain_a", "battle_terrain_b" },
        });

        StringAssert.Contains(report, "42");
        StringAssert.Contains(report, "battle_terrain_a");
        StringAssert.Contains(report, "battle_terrain_b");
    }

    /// <summary>
    /// The money output. A map index no scene claims means a battle there falls back or asserts —
    /// exactly the stale-scene-reference class that an engine bump introduces, and the reason the
    /// command exists at all. It must be unmissable, not an empty list.
    /// </summary>
    [TestMethod]
    public void FormatBattleScene_NoCandidates_WarnsLoudlyAndNamesTheIndex()
    {
        var report = MissionReportFormatter.FormatBattleScene(new BattleSceneQuery
        {
            X = 120.5f, Y = 340.25f, MapIndex = 187,
            CandidateSceneIds = new string[0],
        });

        StringAssert.Contains(report, "NO battle scene");
        StringAssert.Contains(report, "187");
    }

    [TestMethod]
    public void FormatBattleScene_NullCandidateList_DoesNotThrow()
    {
        var report = MissionReportFormatter.FormatBattleScene(new BattleSceneQuery
        { X = 1f, Y = 2f, MapIndex = 3, CandidateSceneIds = null });

        Assert.IsFalse(string.IsNullOrWhiteSpace(report));
    }

    // ---- mission scene ----------------------------------------------------

    [TestMethod]
    public void FormatMissionScene_WithPosition_RendersSceneNameAndPosition()
    {
        var report = MissionReportFormatter.FormatMissionScene("battle_terrain_a", 10.5f, 20.25f, 3f, true);

        StringAssert.Contains(report, "battle_terrain_a");
        StringAssert.Contains(report, "10.5");
    }

    /// <summary>
    /// With the player dead there is no main agent, so the position comes from the camera. The report
    /// must say which, or a reader will mistake a camera position for the player's.
    /// </summary>
    [TestMethod]
    public void FormatMissionScene_NoMainAgent_LabelsThePositionAsCamera()
    {
        var report = MissionReportFormatter.FormatMissionScene("battle_terrain_a", 10.5f, 20.25f, 3f, false);

        StringAssert.Contains(report, "camera");
    }
}
