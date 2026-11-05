using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>class</c> verdict (phase2-plan.md section 6): the lord spawned with every kit item read back from his spawn equipment, a foot class has no horse, and a kit that
/// fell back to his own gear or lost a slot to a misfit is a failure the run cannot hide.
/// </summary>
[TestClass]
public class LongDefeatClassChecksTests
{
    private static LdLoadout Archer() => new("archer", false, new[]
    {
        new LdKitSlot("Item0", "bow_a"), new LdKitSlot("Item1", "arrows_a"), new LdKitSlot("Item2", ""), new LdKitSlot("Horse", ""), new LdKitSlot("HorseHarness", ""),
    });

    private static LdKitFacts Found(LdLoadout wanted, bool hasMount = false, string[]? skipped = null) =>
        new(true, wanted.Slots.ToList(), skipped ?? Array.Empty<string>(), hasMount);

    [TestMethod]
    public void Class_EveryWantedItemReadBack_NoMountForAFootClass_Passes()
    {
        var verdict = LongDefeatChecks.Class(Archer(), Found(Archer()));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, "class=archer");
        StringAssert.Contains(verdict.Detail, "mounted=false");
        StringAssert.Contains(verdict.Detail, "Item0=bow_a");
        StringAssert.Contains(verdict.Detail, "has_mount=false");
        StringAssert.Contains(verdict.Detail, "wrong=none");
    }

    [TestMethod]
    public void Class_ASlotThatReadsBackAnotherItem_Fails_NamingIt()
    {
        var found = new LdKitFacts(true, new[]
        {
            new LdKitSlot("Item0", "sword_a"), new LdKitSlot("Item1", "arrows_a"), new LdKitSlot("Item2", ""), new LdKitSlot("Horse", ""), new LdKitSlot("HorseHarness", ""),
        }, Array.Empty<string>(), false);

        var verdict = LongDefeatChecks.Class(Archer(), found);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "wrong=Item0:bow_a>sword_a");
    }

    [TestMethod]
    public void Class_AClearedSlotThatStillHoldsAnItem_Fails()
    {
        var found = new LdKitFacts(true, new[]
        {
            new LdKitSlot("Item0", "bow_a"), new LdKitSlot("Item1", "arrows_a"), new LdKitSlot("Item2", ""), new LdKitSlot("Horse", "horse_a"), new LdKitSlot("HorseHarness", ""),
        }, Array.Empty<string>(), false);

        Assert.AreEqual(CheckResult.Fail, LongDefeatChecks.Class(Archer(), found).Result);
    }

    [TestMethod]
    public void Class_AFootClassWithAMount_Fails()
    {
        var verdict = LongDefeatChecks.Class(Archer(), Found(Archer(), hasMount: true));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "has_mount=true");
    }

    [TestMethod]
    public void Class_AMountedClassWithNoMount_Fails_AndWithOneItPasses()
    {
        var rider = new LdLoadout("cavalry", true, new[] { new LdKitSlot("Item0", "spear_a"), new LdKitSlot("Horse", "horse_a"), new LdKitSlot("HorseHarness", "harness_a") });

        Assert.AreEqual(CheckResult.Fail, LongDefeatChecks.Class(rider, Found(rider, hasMount: false)).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatChecks.Class(rider, Found(rider, hasMount: true)).Result);
    }

    [TestMethod]
    public void Class_ASlotTheEngineRefused_Fails_NamingIt()
    {
        var verdict = LongDefeatChecks.Class(Archer(), Found(Archer(), skipped: new[] { "Item1" }));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "skipped=Item1");
    }

    [TestMethod]
    public void Class_ALordWhoNeverSpawned_Fails()
    {
        var verdict = LongDefeatChecks.Class(Archer(), new LdKitFacts(false, Array.Empty<LdKitSlot>(), Array.Empty<string>(), false));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-lord");
    }

    [TestMethod]
    public void TheCheckId_IsTheManifestsClassRow()
    {
        Assert.AreEqual("taom.long-defeat.class", LongDefeatChecks.ClassId);
    }
}
