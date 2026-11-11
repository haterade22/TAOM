using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>The whole win and loss table of a story mission, for the side the player takes. A fault, a missing result and a stalemate decide nothing.</summary>
[TestClass]
public class StoryVerdictTests
{
    private static RunResult Ended(RunEndReason reason) => new(reason, 1, 1, 0);

    private static readonly Dictionary<RunEndReason, StoryOutcome> Defending = new()
    {
        [RunEndReason.TargetReached] = StoryOutcome.Won,
        [RunEndReason.DefendersDead] = StoryOutcome.Lost,
        [RunEndReason.KeepLost] = StoryOutcome.Lost,
        [RunEndReason.Abandoned] = StoryOutcome.Lost,
        [RunEndReason.Stalemate] = StoryOutcome.Undecided,
    };

    private static readonly Dictionary<RunEndReason, StoryOutcome> Attacking = new()
    {
        [RunEndReason.KeepLost] = StoryOutcome.Won,
        [RunEndReason.DefendersDead] = StoryOutcome.Won,
        [RunEndReason.TargetReached] = StoryOutcome.Lost,
        [RunEndReason.Abandoned] = StoryOutcome.Lost,
        [RunEndReason.Stalemate] = StoryOutcome.Undecided,
    };

    [TestMethod]
    public void Of_Defending_MapsEveryEndReason()
    {
        foreach (var pair in Defending)
            Assert.AreEqual(pair.Value, StoryVerdict.Of(StorySide.Defending, Ended(pair.Key), false), "defending, " + pair.Key);
    }

    [TestMethod]
    public void Of_Attacking_MapsEveryEndReason()
    {
        foreach (var pair in Attacking)
            Assert.AreEqual(pair.Value, StoryVerdict.Of(StorySide.Attacking, Ended(pair.Key), false), "attacking, " + pair.Key);
    }

    [TestMethod]
    public void TheTables_NameEveryEndReasonTheRunCanGive()
    {
        var all = Enum.GetValues(typeof(RunEndReason)).Cast<RunEndReason>().OrderBy(r => r).ToArray();

        CollectionAssert.AreEqual(all, Defending.Keys.OrderBy(r => r).ToArray(), "a new end reason needs a decision for the defending side");
        CollectionAssert.AreEqual(all, Attacking.Keys.OrderBy(r => r).ToArray(), "a new end reason needs a decision for the attacking side");
    }

    [TestMethod]
    public void Of_Stalemate_IsUndecidedForBothSides()
    {
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, Ended(RunEndReason.Stalemate), false));
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Attacking, Ended(RunEndReason.Stalemate), false));
    }

    [TestMethod]
    public void Of_NoResult_IsUndecidedForBothSides()
    {
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, null, false));
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Attacking, null, false));
    }

    [TestMethod]
    public void Of_AFault_IsUndecided_EvenWhenTheAbandonedRunLeftALossBehindIt()
    {
        // A faulted host abandons its run, and Abandoned alone reads as a loss for either side.
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, Ended(RunEndReason.Abandoned), true));
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Attacking, Ended(RunEndReason.Abandoned), true));
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, null, true));
    }

    [TestMethod]
    public void Of_AFault_BeatsAWinToo()
    {
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, Ended(RunEndReason.TargetReached), true));
    }

    [TestMethod]
    public void Of_AnEndReasonOutsideTheEnum_IsUndecided()
    {
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Defending, Ended((RunEndReason)99), false));
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of(StorySide.Attacking, Ended((RunEndReason)99), false));
    }

    [TestMethod]
    public void Of_ASideOutsideTheEnum_IsUndecided()
    {
        Assert.AreEqual(StoryOutcome.Undecided, StoryVerdict.Of((StorySide)99, Ended(RunEndReason.TargetReached), false));
    }
}
