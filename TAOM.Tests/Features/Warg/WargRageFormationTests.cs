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
}
