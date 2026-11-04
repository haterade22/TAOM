using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// A warg's mount is built before its rider (Mission.SpawnAgent builds the mount first, and the rider's Formation is set only in the
/// rider's own build), so the tree's blackboard captures a null formation for every warg spawned after the first scan. The rage
/// start must store the rider's live formation before it clears it, or <c>CleanIfEnemyDied</c> and <c>OnWargDied</c> write the
/// stored null back and the rider never rejoins its formation. Agent is sealed, so the behaviour itself has no unit seam; this pins
/// the order in the source and the in-game check is owed (see the fords-of-war build spec).
/// A warg that dies without raging never reaches the rage start, so its stored formation is still null and the reset must not write
/// that null over the rider's own formation; this is pinned in source for the same reason (Agent is sealed).
/// </summary>
[TestClass]
public class WargRageFormationTests
{
    [TestMethod]
    public void PrepareOnFirstCall_AiBranch_StoresTheLiveFormationBeforeClearingIt()
    {
        var src = RepoPaths.ReadSource("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs", stripComments: true);

        var store = src.IndexOf("AgentsFormation.SetValue(rider.Formation)", System.StringComparison.Ordinal);
        var clear = src.IndexOf("rider.Formation = null", System.StringComparison.Ordinal);

        Assert.IsTrue(store >= 0, "the live formation is never stored");
        Assert.IsTrue(clear >= 0, "the rage start no longer clears the rider's formation");
        Assert.IsTrue(store < clear, "the formation must be stored before it is cleared");
    }

    [TestMethod]
    public void ResetAiControlledAgent_WritesTheFormationOnlyWhenOneWasStored()
    {
        var src = RepoPaths.ReadSource("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs", stripComments: true);

        var write = src.IndexOf("rider.Formation = formation", System.StringComparison.Ordinal);
        var guard = src.IndexOf("formation != null", System.StringComparison.Ordinal);

        Assert.IsTrue(write >= 0, "the reset no longer restores the stored formation");
        Assert.IsTrue(guard >= 0, "the reset writes the formation without a null check, so a warg that died without raging unsets its rider's formation");
        Assert.IsTrue(guard < write, "the null check must come before the write");
    }

    // A rider who leaves a living warg mid-rage is no longer its RiderAgent, so the reset must find him through the rider the
    // rage start stored, or his formation is never given back (check 12 "stranded"). Agent is sealed: pinned in source.
    [TestMethod]
    public void PrepareOnFirstCall_AiBranch_StoresTheRiderBesideTheFormation()
    {
        var src = RepoPaths.ReadSource("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs", stripComments: true);

        var store = src.IndexOf("Rider.SetValue(rider)", System.StringComparison.Ordinal);
        var clear = src.IndexOf("rider.Formation = null", System.StringComparison.Ordinal);

        Assert.IsTrue(store >= 0, "the rage start never stores the rider, so a warg he leaves cannot find him");
        Assert.IsTrue(store < clear, "the rider must be stored before his formation is cleared");
    }

    // A hit in the second after a rage's last attack starts the next rage before the first has closed. The rider has no
    // formation by then, so a plain store would write null over the real one and he would never rejoin.
    [TestMethod]
    public void PrepareOnFirstCall_ASecondRageOnTheSameRider_KeepsTheFormationTheFirstStored()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs");

        var guard = src.IndexOf("rider.Formation != null", System.StringComparison.Ordinal);
        var store = src.IndexOf("AgentsFormation.SetValue(rider.Formation)", System.StringComparison.Ordinal);

        Assert.IsTrue(guard >= 0, "the store has no guard, so a second rage overwrites the real formation with null");
        Assert.IsTrue(guard < store, "the guard must come before the store");
    }

    // A rage that took the player's controller stores no formation. The no-rider branch and the warg-death listener need
    // the rider to know whose rage it is.
    [TestMethod]
    public void PrepareOnFirstCall_PlayerBranch_StoresTheRider()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs");

        var swap = src.IndexOf("SwitchMainAgentController(true)", System.StringComparison.Ordinal);
        var store = src.IndexOf("Rider.SetValue(", System.StringComparison.Ordinal);
        var elseBranch = src.IndexOf("else {", System.StringComparison.Ordinal);

        Assert.IsTrue(swap >= 0 && elseBranch > swap, "the player branch moved");
        Assert.IsTrue(store > swap && store < elseBranch, "the player branch must store the rider too");
    }

    [TestMethod]
    public void CleanIfEnemyDied_GivesTheFormationBackThroughTheStoredRider()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");

        StringAssert.Contains(src, "GiveFormationBack(warg, Rider.GetValue(), AgentsFormation.GetValue())");
    }

    // The formation is the stored rider's, so he comes first. A rider who took his place mid-rage lost his own too
    // (WargAiControlledGetToEnemy clears it) and has no store, so he joins it only if he has none.
    [TestMethod]
    public void GiveFormationBack_TheStoredRiderComesFirst_AndAReplacementOnlyFillsAGap()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");
        var start = src.IndexOf("static void GiveFormationBack(", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "GiveFormationBack is gone");
        var body = src.Substring(start);

        var owner = body.IndexOf("ValidStoredRider(stored)", System.StringComparison.Ordinal);
        var reset = body.IndexOf("ResetAiControlledAgent(owner, formation)", System.StringComparison.Ordinal);
        var replacement = body.IndexOf("live.Formation == null ? formation : null", System.StringComparison.Ordinal);

        Assert.IsTrue(owner >= 0, "the stored rider is no longer read");
        Assert.IsTrue(reset > owner, "the stored rider must get his formation back");
        Assert.IsTrue(replacement > reset, "a replacement rider must take the formation only if he has none");
    }

    [TestMethod]
    public void ValidStoredRider_NeedsTheSlotsOccupantAndOwnedHere()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");
        var start = src.IndexOf("static Agent ValidStoredRider(", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "ValidStoredRider is gone");
        var body = src.Substring(start);

        StringAssert.Contains(body, "AgentSlotIdentity.IsCurrentOccupant(stored)", "a handle held across frames must be checked against the slot (agent indices recycle)");
        StringAssert.Contains(body, "CoopMissionOwnership.IsOwnedHere(stored)", "co-op: only the rider's owner may restore him");
    }

    // Agent.OnFleeing clears a panicked rider's formation on purpose. Writing it back makes him ride away inside it.
    [TestMethod]
    public void ResetAiControlledAgent_LeavesAFleeingRiderOutOfTheFormation()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");

        var guard = src.IndexOf("!rider.IsRunningAway", System.StringComparison.Ordinal);
        var write = src.IndexOf("rider.Formation = formation", System.StringComparison.Ordinal);

        Assert.IsTrue(guard >= 0, "a fleeing rider is written back into the formation");
        Assert.IsTrue(guard < write, "the guard must come before the write");
    }

    // With no enemy within 60 m the "no enemy close" branch wins every pass, so "enter rage mode", which holds the only
    // other close, never runs: the rider keeps no formation until an enemy returns.
    [TestMethod]
    public void WargBehaviorTree_NoEnemyClose_ClosesAnOpenRageBeforeItSleeps()
    {
        var src = Squash("Main/Features/Warg/WargBehaviorTree.cs");

        StringAssert.Contains(src, "AddSequence(\"no enemy close\", new NoEnemyCloseDecorator()) .AddTask(new CloseOpenRage()) .AddTask(new SleepTask(");
    }

    [TestMethod]
    public void CloseOpenRage_ClosesOnlyAnOpenRage()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CloseOpenRage.cs");

        StringAssert.Contains(src, ": CleanIfEnemyDied");
        StringAssert.Contains(src, "RageAttackStartTime.GetValue() != null");
    }

    // A rider long gone, or a warg that never raged, holds nothing to give back: the stored formation is stale or null,
    // and the handle may be a recycled slot.
    [TestMethod]
    public void OnWargDied_ChecksTheSlot_AndGivesBackOnlyWhileARageIsOpen()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/OnWargDied.cs");

        var slot = src.IndexOf("AgentSlotIdentity.IsCurrentOccupant(rider)", System.StringComparison.Ordinal);
        var open = src.IndexOf("RageAttackStartTime.GetValue() != null", System.StringComparison.Ordinal);
        var reset = src.IndexOf("ResetAiControlledAgent(rider, AgentsFormation.GetValue())", System.StringComparison.Ordinal);

        Assert.IsTrue(slot >= 0, "a stored handle must be checked against the slot (agent indices recycle)");
        Assert.IsTrue(open >= 0, "a warg with no open rage must not touch its old rider");
        Assert.IsTrue(slot < reset && open < reset, "the guards must come before the reset");
    }

    [TestMethod]
    public void WargBehaviorTree_NoRiderBranch_RestoresARiderWhoLeftMidRage()
    {
        var src = Squash("Main/Features/Warg/WargBehaviorTree.cs");

        StringAssert.Contains(src, "AddSequence(\"no rider\", new HasNoRiderDecorator()) .AddTask(new RestoreDismountedRider()) .AddTask(new SleepTask(");
    }

    [TestMethod]
    public void RestoreDismountedRider_ClosesAnOpenRage_ButLeavesOneHoldingThePlayersController()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/RestoreDismountedRider.cs");

        StringAssert.Contains(src, ": CleanIfEnemyDied");
        var open = src.IndexOf("RageAttackStartTime.GetValue() == null", System.StringComparison.Ordinal);
        var player = src.IndexOf("rider.IsPlayerControlled", System.StringComparison.Ordinal);
        var stored = src.IndexOf("AgentsFormation.GetValue()", System.StringComparison.Ordinal);

        Assert.IsTrue(open >= 0, "a warg that never raged must not touch its old rider");
        Assert.IsTrue(player > open, "a rage that took the player's controller is FinishRageMode's to end");
        Assert.IsTrue(stored > player, "a rage that took only the player's formation still gives it back");
    }

    // A closed rage clears what it stored. Otherwise a later warg death reaches the reset with a rider who may have moved
    // to another formation or another warg, and a handle whose slot may be recycled.
    [TestMethod]
    public void ClosingARage_ClearsTheStoredFormationAndRider_AfterGivingItBack()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");

        var reset = src.IndexOf("GiveFormationBack(warg,", System.StringComparison.Ordinal);
        var formation = src.IndexOf("AgentsFormation.SetValue(null)", System.StringComparison.Ordinal);
        var rider = src.IndexOf("Rider.SetValue(null)", System.StringComparison.Ordinal);

        Assert.IsTrue(reset >= 0, "the reset call moved");
        Assert.IsTrue(formation > reset, "a later warg death would move the rider back to the formation this rage stored");
        Assert.IsTrue(rider > reset, "a later warg death would cancel scripted movement on a rider who left long ago");
    }

    private static string Squash(string path) =>
        System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");
}
