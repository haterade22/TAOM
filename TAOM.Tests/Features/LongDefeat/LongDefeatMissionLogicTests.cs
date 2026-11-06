using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatMissionLogicTests
{
    private const string LogicPath = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs";

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static LongDefeatMissionLogic Logic(FakeLongDefeatMenu? menu = null) =>
        new(new RunPlan("helms", new[] { new GarrisonRow("t", FormationSlot.Infantry, 1) },
                new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
                new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default),
            new LongDefeatSession(), Substitute.For<IModLogger>(), Substitute.For<IOutcomeLog>(), menu ?? new FakeLongDefeatMenu(), "m1");

    // The text of the first block after the marker, by brace depth.
    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " is not in the logic");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }

        Assert.Fail(marker + " has no closing brace");
        return "";
    }

    [TestMethod]
    public void TheLogic_IsALogicBehavior_SoCheckMissionEndedPollsIt()
    {
        Assert.AreEqual(MissionBehaviorType.Logic, Logic().BehaviorType);
    }

    [TestMethod]
    public void TheLogic_DoesNotOverrideOnBehaviorInitialize()
    {
        var method = typeof(LongDefeatMissionLogic).GetMethod("OnBehaviorInitialize", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(method);
    }

    [TestMethod]
    public void TheLogic_OverridesNoMissionEnded_BecauseNothingHereNeedsTheEnginesEndedState()
    {
        var method = typeof(LongDefeatMissionLogic).GetMethod("MissionEnded", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(method);
    }

    [TestMethod]
    public void Ticks_AndRemovals_BeforeAnythingStarted_DoNothingAndDoNotThrow()
    {
        var logic = Logic();

        logic.OnMissionTick(0.1f);
        logic.OnAgentRemoved(null!, null!, AgentState.Killed, default);
        logic.OnAgentFleeing(null!);
    }

    [TestMethod]
    public void OnRetreatMission_EndsTheGameThroughTheMenuAdapter()
    {
        var menu = new FakeLongDefeatMenu();

        Logic(menu).OnRetreatMission();

        Assert.AreEqual(1, menu.EndGames);
    }

    [TestMethod]
    public void OnRetreatMission_Twice_EndsTheGameOnce()
    {
        var menu = new FakeLongDefeatMenu();
        var logic = Logic(menu);

        logic.OnRetreatMission();
        logic.OnRetreatMission();

        Assert.AreEqual(1, menu.EndGames);
    }

    [TestMethod]
    public void TheMoraleHoldIsArmedInAfterStart_AndBothExitsTearDownTheHoldAndTheDirector()
    {
        var source = RepoPaths.ReadSource(LogicPath, stripComments: true);

        StringAssert.Contains(BodyOf(source, "override void AfterStart"), "LongDefeatMorale.Arm(Mission)");
        StringAssert.Contains(BodyOf(source, "override void AfterStart"), "LongDefeatHookState.Arm(Mission)");
        foreach (var exit in new[] { "OnEndMission", "OnRemoveBehavior" })
            StringAssert.Contains(BodyOf(source, "override void " + exit), "TearDown()", exit + " does not tear down");

        var tearDown = BodyOf(source, "private void TearDown()");
        StringAssert.Contains(tearDown, "LongDefeatMorale.Disarm(Mission)");
        StringAssert.Contains(tearDown, "LongDefeatHookState.Disarm(Mission)");
        StringAssert.Contains(tearDown, "_host.ShutDown()");
    }

    [TestMethod]
    public void OnScoreHit_AsksTheDirectorsWantFirst_ThenFiltersTheHit_ThenForwardsTheRidersBlow()
    {
        // OnScoreHit carries the engine's own isBlocked and damagedHp (Mission.cs:5672); OnAgentHit has neither. It may run off the main thread, so the cheap volatile read comes first.
        var body = BodyOf(RepoPaths.ReadSource(LogicPath, stripComments: true), "override void OnScoreHit");

        var wants = body.IndexOf("_host.WantsHits", StringComparison.Ordinal);
        var attacker = body.IndexOf("LongDefeatHitFilter.Attacker(", StringComparison.Ordinal);
        var admits = body.IndexOf("LongDefeatHitFilter.Admits(", StringComparison.Ordinal);
        var forward = body.IndexOf("_host.OnHit(", StringComparison.Ordinal);
        Assert.IsTrue(wants >= 0 && wants < attacker && attacker < admits && admits < forward, body);
        StringAssert.Contains(body, "isBlocked");
        StringAssert.Contains(body, "damagedHp");
        StringAssert.Contains(body, "affectedAgent?.Health", "a killing blow is read from the victim's health, which HandleBlow writes before it raises the hit (Agent.cs:5513, :5527)");
        StringAssert.Contains(body, ".IsMount");
        StringAssert.Contains(body, ".RiderAgent");
    }

    [TestMethod]
    public void OnScoreHit_BeforeAnythingStarted_DoesNothingAndDoesNotThrow()
    {
        var logic = Logic();
        var blow = default(Blow);
        var collision = default(AttackCollisionData);

        logic.OnScoreHit(null!, null!, null!, false, false, in blow, in collision, 5f, 0f, 0f);
        logic.OnScoreHit(null!, null!, null!, true, true, in blow, in collision, 0f, 0f, 0f);
    }

    private const string VolleyHooksPath = "Main/Features/LongDefeat/Hooks/LongDefeatVolleyHooks.cs";

    [TestMethod]
    public void OnAgentHit_AsksTheVolleyWindowFirst_ThenReadsTheMissileByItemIdAndTheVictimsSide_ThenForwards()
    {
        // OnAgentHit carries the missile's weapon, so the item id (Mission.cs:5646, :5657-5659); it may run off the main thread, so the volatile want comes first (A21).
        var body = BodyOf(RepoPaths.ReadSource(VolleyHooksPath, stripComments: true), "static void AgentHit");

        var wants = body.IndexOf("host.WantsVolleyHits", StringComparison.Ordinal);
        var read = body.IndexOf("LongDefeatVolleyHitFilter.Read(", StringComparison.Ordinal);
        var forward = body.IndexOf("host.OnVolleyHit(", StringComparison.Ordinal);
        Assert.IsTrue(wants >= 0 && wants < read && read < forward, body);
        StringAssert.Contains(body, "affectorWeapon.Item?.StringId", "the item id of the missile's weapon");
        StringAssert.Contains(body, "affectedAgent.Team?.Side == BattleSideEnum.Defender", "a friend is told by the victim's side, never the attacker's (A24)");
        StringAssert.Contains(body, "collisionData.AttackBlockedWithShield");
        StringAssert.Contains(body, "affectedAgent.Health", "a kill is the victim's health after the blow, which HandleBlow writes before the hit (E16)");
        StringAssert.Contains(body, "affectorAgent != null", "the engine's own affector is counted when it is null");
        StringAssert.Contains(body, "host.IsVolleyItem");
    }

    [TestMethod]
    public void TheLogic_ForwardsBothVolleyCallbacksToTheHooks_AndStaysSmallEnoughForTheEntryPointGate()
    {
        var source = RepoPaths.ReadSource(LogicPath, stripComments: true);

        StringAssert.Contains(BodyOf(source, "override void OnAgentHit"), "LongDefeatVolleyHooks.AgentHit(_host,");
        StringAssert.Contains(BodyOf(source, "override void OnMissileHit"), "LongDefeatVolleyHooks.MissileHit(_host, Mission,");
    }

    [TestMethod]
    public void OnMissileHit_ReadsOnlyACancelledFriendlyHit_ThroughTheMissilesItem()
    {
        // A cancelled hit never reaches OnAgentHit (A19), and OnMissileHit carries no weapon, so the missile is looked up by the index the hit carries (E28).
        var source = RepoPaths.ReadSource(VolleyHooksPath, stripComments: true);
        var body = BodyOf(source, "static void MissileHit");

        var cancelled = body.IndexOf("!isCanceled", StringComparison.Ordinal);
        var wants = body.IndexOf("host.WantsVolleyHits", StringComparison.Ordinal);
        Assert.IsTrue(cancelled >= 0 && cancelled < wants, "the cheap test first, then the volatile want: " + body);
        StringAssert.Contains(body, "victim.Team?.Side != BattleSideEnum.Defender");
        StringAssert.Contains(body, "LongDefeatVolleyHitFilter.Cancelled(");
        StringAssert.Contains(body, "host.OnVolleyHit(");
        StringAssert.Contains(BodyOf(source, "private static string? MissileItem("), "mission.MissilesList");
    }

    [TestMethod]
    public void TheVolleyHooks_BeforeAnythingStarted_DoNothingAndDoNotThrow()
    {
        var logic = Logic();
        var blow = default(Blow);
        var collision = default(AttackCollisionData);
        var weapon = default(MissionWeapon);

        logic.OnAgentHit(null!, null!, in weapon, in blow, in collision);
        logic.OnMissileHit(null!, null!, true, collision);
        logic.OnMissileHit(null!, null!, false, collision);
    }

    [TestMethod]
    public void IsLoss_IsAKillOrAKnockOut_NeverARoutOrADeletion()
    {
        Assert.IsTrue(LongDefeatMissionLogic.IsLoss(AgentState.Killed));
        Assert.IsTrue(LongDefeatMissionLogic.IsLoss(AgentState.Unconscious));
        Assert.IsFalse(LongDefeatMissionLogic.IsLoss(AgentState.Routed));
        Assert.IsFalse(LongDefeatMissionLogic.IsLoss(AgentState.Deleted));
        Assert.IsFalse(LongDefeatMissionLogic.IsLoss(AgentState.Active));
    }

    [TestMethod]
    public void AfterStart_SpawnsNothing_TheFirstReadyTickStartsTheRun()
    {
        // An agent spawned inside AfterStart lets a SpawnAgent hook add a behavior to the list the engine is walking (rca-fords-of-war-2026-10-31.md).
        var source = RepoPaths.ReadSource(LogicPath, stripComments: true);
        var after = source.IndexOf("override void AfterStart", StringComparison.Ordinal);
        var next = source.IndexOf("override void", after + 10, StringComparison.Ordinal);

        Assert.IsFalse(source.Substring(after, next - after).Contains("StartRun("), "AfterStart must only mark the start as due");
    }
}
