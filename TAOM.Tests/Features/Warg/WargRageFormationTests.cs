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

    [TestMethod]
    public void CleanIfEnemyDied_AiBranch_FindsTheRiderThroughTheStoredOneWhenTheWargHasNone()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");

        StringAssert.Contains(src, "ResetAiControlledAgent(RiderToRestore(Agent.GetValue(), Rider.GetValue()), AgentsFormation.GetValue())");
    }

    [TestMethod]
    public void RiderToRestore_UsesTheStoredRiderOnlyWhenHeIsTheSlotsOccupantAndOwnedHere()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs");
        var start = src.IndexOf("static Agent RiderToRestore(", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RiderToRestore is gone");
        var body = src.Substring(start);

        var live = body.IndexOf("RiderAgent", System.StringComparison.Ordinal);
        var occupant = body.IndexOf("AgentSlotIdentity.IsCurrentOccupant(stored)", System.StringComparison.Ordinal);
        var owned = body.IndexOf("CoopMissionOwnership.IsOwnedHere(stored)", System.StringComparison.Ordinal);

        Assert.IsTrue(live >= 0, "the warg's live rider is no longer read");
        Assert.IsTrue(occupant > live, "a handle held across frames must be checked against the slot (agent indices recycle)");
        Assert.IsTrue(owned > live, "co-op: only the rider's owner may restore him");
    }

    [TestMethod]
    public void WargBehaviorTree_NoRiderBranch_RestoresARiderWhoLeftMidRage()
    {
        var src = Squash("Main/Features/Warg/WargBehaviorTree.cs");

        StringAssert.Contains(src, "AddSequence(\"no rider\", new HasNoRiderDecorator()) .AddTask(new RestoreDismountedRider()) .AddTask(new SleepTask(");
    }

    [TestMethod]
    public void RestoreDismountedRider_ActsOnlyWhileARageIsOpenAndNeverOnAPlayer()
    {
        var src = Squash("Main/Features/Warg/BehaviorTreeElements/RestoreDismountedRider.cs");

        var open = src.IndexOf("RageAttackStartTime.GetValue() == null", System.StringComparison.Ordinal);
        var player = src.IndexOf("IsPlayerControlled", System.StringComparison.Ordinal);
        var reset = src.IndexOf("CleanIfEnemyDied.ResetAiControlledAgent(", System.StringComparison.Ordinal);
        var clear = src.IndexOf("RageAttackStartTime.SetValue(null)", System.StringComparison.Ordinal);

        Assert.IsTrue(open >= 0, "a warg that never raged must not touch its old rider");
        Assert.IsTrue(player >= 0, "the player's rage swap is not this task's to end");
        Assert.IsTrue(reset > open && reset > player, "the guards must come before the reset");
        Assert.IsTrue(clear > reset, "the rage is closed only once the rider is restored, so it is not restored twice");
    }

    // The rage start stores the rider for every warg now, so a later warg death reaches the reset with a rider who may
    // have moved to another formation since. A closed rage clears the formation: null means "nothing to restore".
    [DataTestMethod]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs", "ResetAiControlledAgent(RiderToRestore(")]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/RestoreDismountedRider.cs", "CleanIfEnemyDied.ResetAiControlledAgent(")]
    public void ClosingARage_ClearsTheStoredFormation_AfterGivingItBack(string path, string resetCall)
    {
        var src = Squash(path);

        var reset = src.IndexOf(resetCall, System.StringComparison.Ordinal);
        var clear = src.IndexOf("AgentsFormation.SetValue(null)", System.StringComparison.Ordinal);

        Assert.IsTrue(reset >= 0, "the reset call moved");
        Assert.IsTrue(clear > reset, "a later warg death would move the rider back to the formation this rage stored");
    }

    private static string Squash(string path) =>
        System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");
}
