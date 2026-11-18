using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole.Cheats;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// #1434: the entrance audit judged only the navmesh island, so an entrance on a face land parties may not use, or one
/// no path reaches, passed. Silas He's report named seven such entrances. These rules add the terrain and path checks
/// vanilla's own distance cache applies (SandBoxNavigationCache :23-25, :89).
/// </summary>
[TestClass]
public class SettlementEntranceRulesTests
{
    private static readonly int[] Excluded = { 7, 9 };

    private static SettlementEntranceRules.Verdict Classify(bool onMesh = true, int island = 2, int group = 1, bool? path = true)
        => SettlementEntranceRules.Classify(onMesh, island, mainIsland: 2, group, Excluded, path);

    [TestMethod]
    public void AnEntranceOnTheMainIslandThatAPathReaches_IsOk()
        => Assert.AreEqual(SettlementEntranceRules.Verdict.Ok, Classify());

    [TestMethod]
    public void OffMeshAndOtherIsland_KeepTheirOldVerdicts()
    {
        Assert.AreEqual(SettlementEntranceRules.Verdict.OffMesh, Classify(onMesh: false));
        Assert.AreEqual(SettlementEntranceRules.Verdict.OtherIsland, Classify(island: 5));
    }

    [TestMethod]
    public void AFaceLandPartiesMayNotUse_IsReported_EvenOnTheMainIsland()
        => Assert.AreEqual(SettlementEntranceRules.Verdict.ExcludedTerrain, Classify(group: 9));

    [TestMethod]
    public void AnEntranceNoReferenceReaches_IsReported()
        => Assert.AreEqual(SettlementEntranceRules.Verdict.NoPath, Classify(path: false));

    [TestMethod]
    public void NoReferenceToTestFrom_IsNotAFinding()
        => Assert.AreEqual(SettlementEntranceRules.Verdict.Ok, Classify(path: null));

    [TestMethod]
    public void NearestReferences_AreTheClosestUsableOthers_InDistanceOrder()
    {
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (5, 0), (2, 0), (3, 0) };
        var usable = new[] { true, true, true, false, true };

        var refs = SettlementEntranceRules.NearestReferences(points, self: 0, usable, count: 3);

        CollectionAssert.AreEqual(new[] { 1, 4, 2 }, refs.ToArray());
    }

    // Lens 5 (#1434 review): two entrances 2.7 apart (Orthanc and Isengard) were each the other's nearest reference,
    // so a path between them passed both while neither connects to the rest of the map.
    [TestMethod]
    public void APocketWhoseMembersReachOnlyEachOther_IsNotConnected()
    {
        // 0..3 the main map, 4 and 5 a pocket next to each other.
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0), (3, 0), (50, 0), (51, 0) };
        var all = Enumerable.Repeat(true, points.Count).ToList();
        Func<int, int, bool> path = (a, b) => (a >= 4) == (b >= 4);

        var connected = SettlementEntranceRules.ConnectedToMain(points, all, all, 3, path);

        CollectionAssert.AreEqual(new[] { true, true, true, true, false, false }, connected.ToArray());
    }

    [TestMethod]
    public void AClusterWhoseReferencesAreAllInsideIt_GetsOneTryFromTheMainGroup()
    {
        // 0..4 the main map; 5..8 a far cluster, so each member's three nearest references are cluster members.
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (100, 0), (101, 0), (102, 0), (103, 0) };
        var all = Enumerable.Repeat(true, points.Count).ToList();
        var tries = new List<(int From, int To)>();

        var connected = SettlementEntranceRules.ConnectedToMain(points, all, all, 3, (a, b) => { tries.Add((a, b)); return true; });

        Assert.IsTrue(connected.All(c => c));
        CollectionAssert.Contains(tries, (4, 5));
    }

    [TestMethod]
    public void AGroupAMainMemberAlreadyFailedToReach_GetsNoExtraTry()
    {
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0), (3, 0), (50, 0), (51, 0) };
        var all = Enumerable.Repeat(true, points.Count).ToList();
        var tries = new List<(int From, int To)>();

        SettlementEntranceRules.ConnectedToMain(points, all, all, 3, (a, b) => { tries.Add((a, b)); return (a >= 4) == (b >= 4); });

        Assert.AreEqual(tries.Count, tries.Distinct().Count());
    }

    [TestMethod]
    public void AnEntranceThatFailedACheapCheck_IsNeverPathTestedNorConnected()
    {
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0) };
        var candidate = new[] { true, true, false };
        var tested = new List<int>();
        Func<int, int, bool> path = (a, b) => { tested.Add(a); tested.Add(b); return true; };

        var connected = SettlementEntranceRules.ConnectedToMain(points, candidate, candidate, 3, path);

        CollectionAssert.AreEqual(new[] { true, true, false }, connected.ToArray());
        CollectionAssert.DoesNotContain(tested, 2);
    }

    [TestMethod]
    public void AVillageNoReferenceReaches_IsNotConnected()
    {
        // 3 is a village (a candidate, never a reference); every path to it fails.
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0), (1, 1) };
        var candidate = new[] { true, true, true, true };
        var usable = new[] { true, true, true, false };

        var connected = SettlementEntranceRules.ConnectedToMain(points, candidate, usable, 3, (a, b) => b != 3);

        CollectionAssert.AreEqual(new[] { true, true, true, false }, connected.ToArray());
    }

    // Lens 5 re-review: with no successful path at all, the answer depended on whether a village or a castle came
    // first in the file, and could read "every entrance is reachable".
    [TestMethod]
    public void NoPathSucceedsAnywhere_NothingIsConnected_WhateverComesFirst()
    {
        var points = new List<(float X, float Y)> { (0, 0), (1, 0), (2, 0), (3, 0) };
        var all = new[] { true, true, true, true };
        foreach (var usable in new[] { new[] { false, true, true, true }, new[] { true, true, true, true } })
        {
            var connected = SettlementEntranceRules.ConnectedToMain(points, all, usable, 3, (a, b) => false);
            Assert.IsFalse(connected.Any(c => c));
        }
    }

    [TestMethod]
    public void NearestReferences_NeverIncludeTheSettlementItself()
    {
        var points = new List<(float X, float Y)> { (0, 0), (0, 0) };
        var refs = SettlementEntranceRules.NearestReferences(points, self: 0, new[] { true, true }, count: 3);

        CollectionAssert.AreEqual(new[] { 1 }, refs.ToArray());
    }
}
