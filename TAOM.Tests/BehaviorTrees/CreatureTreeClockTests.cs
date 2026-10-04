using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.BehaviorTrees;

/// <summary>
/// The creature trees' sleep, wait, cooldown and rage timers run every frame for a running tree, so they read
/// <see cref="DateTime.UtcNow"/>: <see cref="DateTime.Now"/> converts to local time on every call and steps by an
/// hour at a daylight-saving change. A stamp is written by one node and compared by another, so both sides of a
/// pair must read the same clock (plan 033).
/// </summary>
[TestClass]
public class CreatureTreeClockTests
{
    private static readonly string[] CreatureTreePrefixes =
    {
        "BehaviorTreeWrapper.",
        "BehaviorTrees.",
        "TAOM.Features.AdvancedCombat.BaseBehaviorTree.",
        "TAOM.Features.Animalia.",
        "TAOM.Features.CreatureBandits.BehaviorTreeElements.",
        // The tree classes outside their elements namespace; no trailing dot, so nested closures match too.
        "TAOM.Features.CreatureBandits.CreatureBanditBehaviorTree",
        "TAOM.Features.Elephant.",
        "TAOM.Features.ElephantLike.",
        "TAOM.Features.Elk.",
        "TAOM.Features.Mumakil.",
        "TAOM.Features.Spider.",
        "TAOM.Features.TrollBruteForce.BehaviorTreeElements.",
        "TAOM.Features.TrollBruteForce.TrollBruteForceBehaviorTree",
        "TAOM.Features.Warg.",
        "TAOM.Features.WarRam.",
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void CreatureTreeCode_NeverReadsDateTimeNow()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        List<string> violations = IlCallScanner.FindCallers(
            typeof(global::BehaviorTreeWrapper.Tasks.SleepTask).Assembly,
            m => m.DeclaringType == typeof(DateTime) && m.Name == "get_Now",
            out List<string> unreadable,
            out int scanned);

        Assert.IsTrue(scanned > 0, "No method bodies scanned: the scan failed rather than passed.");
        List<string> unreadableTrees = unreadable.Where(IsCreatureTreeCode).ToList();
        Assert.AreEqual(0, unreadableTrees.Count,
            "Unreadable creature-tree method bodies, so the rule cannot vouch: " + string.Join("; ", unreadableTrees));
        List<string> treeViolations = violations.Where(IsCreatureTreeCode).ToList();
        Assert.AreEqual(0, treeViolations.Count,
            "a creature tree's interval must use DateTime.UtcNow (no time-zone conversion per frame, no DST step), " +
            "and a stamp's writer and reader must use the same clock: " + string.Join("; ", treeViolations));
    }

    [TestMethod]
    public void ClockRule_ADirectNowRead_IsFound()
    {
        MethodInfo method = typeof(ReadsLocalClock).GetMethod(nameof(ReadsLocalClock.Read));

        IEnumerable<MethodBase> calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray());

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(DateTime) && m.Name == "get_Now"),
            "The scanner did not see a direct DateTime.Now read, so the creature-tree rule would pass vacuously.");
    }

    private static bool IsCreatureTreeCode(string name) =>
        CreatureTreePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    private sealed class ReadsLocalClock
    {
        public DateTime Read() => DateTime.Now;
    }
}
