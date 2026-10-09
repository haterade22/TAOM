// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// Pins the Harmony 2.4.2 rule Patch104 relies on, without the game: when a bool prefix returns false the original is
/// skipped AND the bool prefixes after it never run (Harmony guards every prefix that can affect the original: a bool
/// return, or a by-ref, out or reference-type argument other than <c>__instance</c>, <c>__originalMethod</c> or
/// <c>__state</c>; a void observer prefix still runs). That is why the cull's prefix returns false only after the culled
/// update ran to the end, and why it does not take <c>__runOriginal</c> (docs/reference/harmony-patch-registry.md,
/// "Patch104_NameplateCull"): another mod's later replacing prefix does not run on a frame the cull handled.
/// </summary>
[TestClass]
public class NameplateCullPrefixOrderTests
{
    private static readonly List<string> Ran = new List<string>();
    private static bool _firstResult;

    private sealed class Subject
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Original() => Ran.Add("original");
    }

    private static bool First() { Ran.Add("first"); return _firstResult; }

    private static bool Second() { Ran.Add("second"); return true; }

    private static void Run(bool firstReturns)
    {
        Ran.Clear();
        _firstResult = firstReturns;
        var id = "taom.tests.nameplatecull.prefixorder";
        var harmony = new Harmony(id);
        var target = typeof(Subject).GetMethod(nameof(Subject.Original))!;
        try
        {
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(NameplateCullPrefixOrderTests).GetMethod(nameof(First), BindingFlags.Static | BindingFlags.NonPublic)!) { priority = Priority.First });
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(NameplateCullPrefixOrderTests).GetMethod(nameof(Second), BindingFlags.Static | BindingFlags.NonPublic)!) { priority = Priority.Last });

            new Subject().Original();
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void TwoBoolPrefixes_TheFirstReturnsFalse_TheSecondNeverRunsAndTheOriginalIsSkipped()
    {
        Run(firstReturns: false);

        CollectionAssert.AreEqual(new[] { "first" }, Ran);
    }

    [TestMethod]
    public void TwoBoolPrefixes_TheFirstReturnsTrue_BothRunThenTheOriginal()
    {
        Run(firstReturns: true);

        CollectionAssert.AreEqual(new[] { "first", "second", "original" }, Ran);
    }
}
