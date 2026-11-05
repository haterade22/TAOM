using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CoopInterop.Missions;

/// <summary>
/// Source pins for the co-op ownership checks inside TAOM mission behaviors (#967, C7). Each check sits
/// BEFORE the action it guards, because a check after the write protects nothing. The seams run engine
/// code that no unit test reaches, so the order is pinned in the source text (comments stripped).
/// Single-player stays identical: <c>CoopMissionOwnership</c> answers "owned" with no session.
/// </summary>
[TestClass]
public class CoopMissionSeamPinTests
{
    private const string Owned = "CoopMissionOwnership.IsOwnedHere(";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    // The text of one member, from its signature to the next member at the same indent.
    private static string Member(string source, string signatureFragment)
    {
        var start = source.IndexOf(signatureFragment, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"'{signatureFragment}' is gone; update this pin.");
        var next = Regex.Match(source.Substring(start + signatureFragment.Length), @"\n    (?:public|private|protected|internal)\b");
        return next.Success ? source.Substring(start, signatureFragment.Length + next.Index) : source.Substring(start);
    }

    private static void CheckPrecedes(string body, string check, string action, string what)
    {
        var c = body.IndexOf(check, StringComparison.Ordinal);
        var a = body.IndexOf(action, StringComparison.Ordinal);
        Assert.IsTrue(c >= 0, $"{what}: '{check}' is missing.");
        Assert.IsTrue(a >= 0, $"{what}: '{action}' is gone; update this pin.");
        Assert.IsTrue(c < a, $"{what}: the ownership check must come before '{action}'.");
    }

    [TestMethod]
    public void AuthorityAndOwnershipAreRegistered()
    {
        var ioc = Source("Main/Features/CoopInterop/CoopInteropIoC.cs");
        StringAssert.Contains(ioc, "Register<Missions.ICoopMissionAuthority, Missions.CoopMissionAuthority>(Reuse.Singleton)");
    }

    [TestMethod]
    public void Ownership_NoContainerOrNoSession_AnswersOwnedWithoutTheRegistry()
    {
        var src = Source("Main/Features/CoopInterop/Missions/CoopMissionOwnership.cs");
        StringAssert.Contains(src, "authority == null || authority.IsLocallyControlled(agent)");

        var authority = Source("Main/Features/CoopInterop/Missions/CoopMissionAuthority.cs");
        var owned = Member(authority, "public bool IsLocallyControlled(Agent? agent)");
        CheckPrecedes(owned, "if (!_session.IsSessionActive) return true;", "LinkFor(", "IsLocallyControlled");
        // The mission is read from the argument, never captured in a closure (no allocation per call).
        Assert.IsFalse(owned.Contains("() => mission"), "LinkFor takes a static lambda over the mission");
    }

    [TestMethod]
    public void Authority_UsesCompiledDelegatesAndNeverReflectionInvoke()
    {
        var src = Source("Main/Features/CoopInterop/Missions/CoopMissionAuthority.cs");
        StringAssert.Contains(src, ".Compile()");
        Assert.IsFalse(Regex.IsMatch(src, @"\.Invoke\("), "a per-agent call must go through a compiled delegate, not MethodInfo.Invoke");
    }

    [TestMethod]
    public void BehaviorTreeAgentComponent_RunsATreeOnlyForAnOwnedAgent()
    {
        var src = Source("Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs");
        var tick = Member(src, "internal void TickOnMissionThread(float dt)");
        CheckPrecedes(tick, Owned + "Agent)", "Tree.RunTree()", "tree tick");
        // The skipped tick restarts the cadence, so a non-owner peer is throttled only by a delay of 1000 ms or more (a creature tree still asks every frame).
        CheckPrecedes(tick, Owned + "Agent)", "timeSinceLastEvaluation = 0f;", "tree tick");
        // Never decide in the build callback: a mount's controller is AI there whatever co-op will make it.
        Assert.IsFalse(src.Contains("OnAgentBuild"), "ownership is read each tick, never at build time");
    }

    [TestMethod]
    public void OnWargDied_ActsOnlyForAnOwnedRider()
    {
        var src = Source("Main/Features/Warg/BehaviorTreeElements/OnWargDied.cs");
        var notify = Member(src, "public override void Notify(object[] data)");
        // ValidStoredRider holds the ownership test, pinned in WargRageFormationTests.
        CheckPrecedes(notify, "CleanIfEnemyDied.ValidStoredRider(", "FinishRageMode.Reset", "OnWargDied");
        CheckPrecedes(notify, "CleanIfEnemyDied.ValidStoredRider(", "CleanIfEnemyDied.ResetAiControlledAgent", "OnWargDied");
    }

    [TestMethod]
    public void BehaviorTreeNotifyAll_StaysUngated_BecauseOnlyOnWargDiedActs()
    {
        var src = Source("Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs");
        var notifyAll = Member(src, "private void NotifyAll(List<BannerlordBTListener> listeners, object[] data)");
        Assert.IsFalse(notifyAll.Contains("CoopMission"), "gate the one listener that acts (OnWargDied), not the dispatcher");
        // The other event listeners only write their own blackboard or log.
        StringAssert.Contains(Source("Main/Features/Spider/BehaviorTreeElements/OnSpiderDied.cs"), "LogDebug(");
        var rage = Member(Source("Main/Features/Warg/BehaviorTreeElements/WargTryToGoRage.cs"), "public override void Notify(object[] data)");
        Assert.IsFalse(rage.Contains("TakeDamage") || rage.Contains("RegisterBlow") || rage.Contains("ChangeMorale"));
    }

    [TestMethod]
    public void BoneCheck_RegistersABlowOnlyForAnOwnedAttacker()
    {
        var src = Source("Main/Features/AdvancedCombat/BoneCheck.cs");
        CheckPrecedes(src, Owned, "_onCollisionCallback?.Invoke(", "bone hit");
        StringAssert.Contains(src, "is AgentAdapter");
    }

    [TestMethod]
    public void SignatureStrikes_AttackerAndVictimAreChecked()
    {
        var logic = Source("Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs");
        var hit = Member(logic, "public override void OnMeleeHit(");
        CheckPrecedes(hit, Owned + "attacker)", "_buffer.Enqueue(", "OnMeleeHit");

        var runner = Source("Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs");
        var run = Member(runner, "public void Run(Mission mission, in StrikeRequest request)");
        CheckPrecedes(run, Owned + "attacker)", "_sound.Play(", "ring attacker");
        // The victim's morale is gated by DreadAgentGate (an AI controller), not by a registry check.
        CheckPrecedes(run, "DreadAgentGate.CanAffect(victim)", "victim.ChangeMorale(", "ring victim morale");
        Assert.IsFalse(run.Contains(Owned + "victim)"), "the ring victim is gated by DreadAgentGate alone");
    }

    [TestMethod]
    public void CrewSpawners_SpawnOnlyForAnOwnedRider()
    {
        var howdah = Source("Main/Features/Elephant/HowdahCrewSpawner.cs");
        CheckPrecedes(Member(howdah, "private void SpawnQueued("), Owned + "mahout)", "Spawn(machine, mahout, mission)", "howdah crew");

        var mumak = Source("Main/Features/Mumakil/MumakilCrewSpawner.cs");
        CheckPrecedes(Member(mumak, "private void SpawnQueued("), Owned + "rider)", "Spawn(platform, rider, mission)", "mumakil crew");
    }

    [TestMethod]
    public void BannerBearers_StayUngated_BecauseThePromotionModelCallsBaseFirst()
    {
        // A puppet is never promoted to bearer: the vanilla gate (IsAIControlled) runs first in both branches
        // of CanAgentBecomeBannerBearer, and a formation of puppets is banner-assigned by its owner.
        var model = Source("Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs");
        var can = Member(model, "public override bool CanAgentBecomeBannerBearer(Agent agent)");
        var disabled = can.IndexOf("? base.CanAgentBecomeBannerBearer(agent)", StringComparison.Ordinal);
        var enabled = can.IndexOf(": base.CanAgentBecomeBannerBearer(agent)", StringComparison.Ordinal);
        Assert.IsTrue(disabled >= 0 && enabled > disabled, "both branches call base");
        Assert.IsTrue(enabled < can.IndexOf("_service.IsRaceAllowed(", StringComparison.Ordinal), "base runs before TAOM's gates");

        var logic = Source("Main/Features/BannerBearers/Hooks/BannerBearerAssignmentMissionLogic.cs");
        Assert.IsFalse(logic.Contains("CoopMission"), "the assignment logic reads no co-op state");
    }

    [TestMethod]
    public void DreadAgentGate_CanAffect_RequiresAnAIController()
    {
        // The gate is also the co-op ownership test for dread, race fear, the signature ring and the aura
        // pulse: a puppet is Controller None, so it is not AI-controlled and carries no CommonAIComponent.
        var gate = Member(Source("Main/Features/DreadAura/Hooks/DreadAgentGate.cs"), "public static bool CanAffect(Agent agent)");
        StringAssert.Contains(gate, "agent.IsAIControlled");
        StringAssert.Contains(gate, "agent.CommonAIComponent != null");
        foreach (var path in new[]
                 {
                     "Main/Features/DreadAura/Hooks/DreadPulseRunner.cs",
                     "Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs",
                     "Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs",
                 })
        {
            var src = Source(path);
            StringAssert.Contains(src, "DreadAgentGate.CanAffect(", path);
            Assert.IsFalse(src.Contains("CoopMission"), path + " relies on the gate, not a registry check");
        }
    }

    [TestMethod]
    public void CareerAbilities_BuffAndInspireOnlyOwnedAllies()
    {
        var src = Source("Main/Features/CareerSystem/Abilities/MissionAbilityExecutionContext.cs");
        // The burst needs the ally's CommonAIComponent, which only an AI-controlled agent carries.
        var burst = Member(src, "public void ApplyMoraleBurst(");
        CheckPrecedes(burst, "GetComponent<CommonAIComponent>()", "ai.Morale =", "morale burst");
        Assert.IsFalse(burst.Contains("CoopMission"), "the burst is gated by the AI component");
        CheckPrecedes(Member(src, "private void ApplyAoeBuff("), Owned + "ally)", "CareerAbilityBuffTracker.AddAllyContribution", "aoe buff");
    }

    [TestMethod]
    public void CreatureBanditBackstop_StaysUngated()
    {
        // A puppet's origin has no supplier, so SetRouted does nothing there, and co-op never replicates
        // creature bandits (SpawnMonster bypasses SpawnAgent): the backstop needs no ownership test.
        var src = Source("Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs");
        var removed = Member(src, "public override void OnAgentRemoved(");
        Assert.IsFalse(removed.Contains("Controller"), "the backstop reads no controller");
        Assert.IsFalse(removed.Contains("CoopMission"), "the backstop reads no co-op state");
    }

    [TestMethod]
    public void PlayerTeamBehaviors_StayUngated()
    {
        // Co-op keeps other owners' puppets off the player team (PuppetSpawner.cs:1031-1053), and both behaviors
        // read only Mission.Current?.PlayerTeam, so neither needs an ownership test.
        foreach (var path in new[]
                 {
                     "Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs",
                     "Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs",
                 })
        {
            var src = Source(path);
            StringAssert.Contains(src, "PlayerTeam", path);
            Assert.IsFalse(src.Contains("CoopMission"), path + " reads only the player team");
        }
    }

    [TestMethod]
    public void CreatureSiegeRole_ExcludesLaddersOnlyForAIControlledAgents()
    {
        var src = Source("Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs");
        // Already safe: a puppet is Controller None, so IsAIControlled is false and it is never excluded.
        CheckPrecedes(src, "var isAIControlled = agent.IsAIControlled;", "agent.ExcludeFace(", "siege role");
        StringAssert.Contains(src, "if (isAIControlled && !route.Excluded)");
    }
}
