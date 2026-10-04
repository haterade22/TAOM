using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The stand-aside rule of Patch99 (deep review of plan 042): which Harmony patches make the fast path run the engine's
/// own code. TAOM's own owners never count; on the target only the patches that run before or instead of the original
/// count (its postfixes and finalizers still run after the fast path); on a method the fast path bypasses, every patch
/// counts. Harmony's <c>Patches</c> and <c>Patch</c> are built directly, so this runs on hosted CI without the game.
/// </summary>
[TestClass]
public class XmlMergeForeignPatchFilterTests
{
    private const string Target = "MBObjectManager.CreateMergedXmlFile";
    private const string Watched = "MBObjectManager.ToXDocument";
    private const string Foreign = "com.other";

    private static readonly MethodInfo PatchMethod =
        typeof(XmlMergeForeignPatchFilterTests).GetMethod(nameof(PatchBody), BindingFlags.Static | BindingFlags.NonPublic)!;

    [TestMethod]
    public void TaomsOwnOwners_AreNeverForeign_InAnyCollection()
    {
        foreach (var owner in new[] { "com.taom.mod", "TAOM.Dependencies.Foundation.PatchShield" })
        {
            var found = Collect(Every(owner), Watched, includeAfterOriginal: true);

            Assert.AreEqual(0, found.Count, owner + " is TAOM's own owner: " + string.Join(", ", found));
        }
    }

    [TestMethod]
    public void Target_ForeignPatchesBeforeOrInsteadOfTheOriginal_AreForeign()
    {
        var found = Collect(Every(Foreign), Target, includeAfterOriginal: false);

        CollectionAssert.AreEqual(new[]
        {
            "com.other inner-postfix " + Target,
            "com.other inner-prefix " + Target,
            "com.other prefix " + Target,
            "com.other transpiler " + Target,
        }, found);
    }

    [TestMethod]
    public void Target_ForeignPostfixAndFinalizer_AreNotForeign()
    {
        var info = new Patches(None(), One(Foreign), None(), One(Foreign), None(), None());

        Assert.AreEqual(0, Collect(info, Target, includeAfterOriginal: false).Count,
            "a postfix or finalizer on the target still runs after the fast path");
    }

    [TestMethod]
    public void WatchedMethod_EveryForeignPatch_IsForeign()
    {
        var found = Collect(Every(Foreign), Watched, includeAfterOriginal: true);

        CollectionAssert.AreEqual(new[]
        {
            "com.other finalizer " + Watched,
            "com.other inner-postfix " + Watched,
            "com.other inner-prefix " + Watched,
            "com.other postfix " + Watched,
            "com.other prefix " + Watched,
            "com.other transpiler " + Watched,
        }, found);
    }

    [TestMethod]
    public void ForeignPatches_CountsTheTargetsPatchesBeforeTheOriginal_AndEveryPatchOnAWatchedMethod()
    {
        var target = typeof(XmlMergeForeignPatchFilterTests).GetMethod(nameof(TargetBody), BindingFlags.Static | BindingFlags.NonPublic)!;
        var watched = typeof(XmlMergeForeignPatchFilterTests).GetMethod(nameof(WatchedBody), BindingFlags.Static | BindingFlags.NonPublic)!;

        // Each method gets its own owner, so a seam that read one method's patch info for another fails here.
        var found = XmlMergeEngineAdapter.ForeignPatches(target, new[] { watched },
            m => m == target ? Every("com.target") : m == watched ? Every("com.watched") : null);

        const string OnTarget = "MBObjectManager." + nameof(TargetBody);
        const string OnWatched = "MBObjectManager." + nameof(WatchedBody);
        CollectionAssert.AreEqual(new[]
        {
            "com.target inner-postfix " + OnTarget,
            "com.target inner-prefix " + OnTarget,
            "com.target prefix " + OnTarget,
            "com.target transpiler " + OnTarget,
            "com.watched finalizer " + OnWatched,
            "com.watched inner-postfix " + OnWatched,
            "com.watched inner-prefix " + OnWatched,
            "com.watched postfix " + OnWatched,
            "com.watched prefix " + OnWatched,
            "com.watched transpiler " + OnWatched,
        }, found.ToList(), "the target's postfixes and finalizers never count; a watched method's always do");
    }

    [TestMethod]
    public void NoPatchInfo_FindsNothing()
    {
        Assert.AreEqual(0, Collect(null, Watched, includeAfterOriginal: true).Count);
    }

    private static List<string> Collect(Patches? info, string where, bool includeAfterOriginal)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        XmlMergeEngineAdapter.CollectForeign(found, info, where, includeAfterOriginal);
        return found.ToList();
    }

    private static Patches Every(string owner) =>
        new Patches(One(owner), One(owner), One(owner), One(owner), One(owner), One(owner));

    private static Patch[] One(string owner) => new[] { new Patch(PatchMethod, 0, owner, Priority.Normal, new string[0], new string[0], false) };

    private static Patch[] None() => new Patch[0];

    private static void PatchBody()
    {
    }

    private static void TargetBody()
    {
    }

    private static void WatchedBody()
    {
    }
}
