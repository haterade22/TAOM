using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Warg;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Pins which Monster ids count as a warg.
///
/// This matters more than it looks. <c>WargConfig.IsWargMonster</c> is the only gate on
/// <c>WargMissionBehavior</c> (the bite attack, both the first-tick scan and OnAgentBuild) and on
/// <c>WargRiderHandManager.Tick()</c> (rider hand posing). It was exact string equality against
/// "warg", so a second warg-family Monster silently got no bite and no rider posing: no crash, no
/// log line, nothing to notice in play until someone wondered why the new mount never attacked.
///
/// The fell warg is that second Monster. It reuses skeleton_warg and as_warg but needs its own
/// Monster row for weight, which drives TAOM's charge-knockdown model, and for base hit points.
/// </summary>
[TestClass]
public class WargMonsterIdTests
{
    [TestMethod]
    public void OrdinaryWarg_IsRecognised()
    {
        Assert.IsTrue(WargConfig.IsWargMonster("warg"),
            "the original id must never regress; every existing warg troop depends on it");
    }

    [TestMethod]
    public void FellWarg_IsRecognised()
    {
        Assert.IsTrue(WargConfig.IsWargMonster("fell_warg"),
            "the fell warg has its own Monster and must still bite and pose its rider");
    }

    [TestMethod]
    public void DireWolf_IsRecognised()
    {
        Assert.IsTrue(WargConfig.IsWargMonster("dire_wolf"),
            "the dire wolf runs the warg tree and poses its rider on a skeleton of its own (#1081)");
    }

    [TestMethod]
    [DataRow("warg")]
    [DataRow("fell_warg")]
    public void ImpactBoneIds_SkeletonWargMonster_ReturnsTheWargCone(string monsterId)
    {
        CollectionAssert.AreEqual(
            new sbyte[] { 17, 20, 22, 23, 32, 36, 37, 38, 42, 43 },
            WargConfig.ImpactBoneIds(monsterId),
            "both Monsters share skeleton_warg, so they share its bone indices");
    }

    [TestMethod]
    public void ImpactBoneIds_DireWolf_ReturnsTheWolfSkeletonsOwnBones()
    {
        // adod_wolf_armature_unused: Spine2, Head, Jaw, Tongue03, L Clavicle, L Hand, L Finger0,
        // R Clavicle, R Hand, R Finger0. The warg's indices on this skeleton are an ear, eye bones, a
        // lip and the paws, with no chest or jaw: lessons/animation-skeleton.md, "ITS OWN strike bones".
        CollectionAssert.AreEqual(
            new sbyte[] { 3, 5, 6, 9, 34, 37, 38, 39, 42, 43 },
            WargConfig.ImpactBoneIds("dire_wolf"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Dire_Wolf")]
    public void ImpactBoneIds_UnknownMonster_ReturnsTheWargCone(string monsterId)
    {
        CollectionAssert.AreEqual(
            new sbyte[] { 17, 20, 22, 23, 32, 36, 37, 38, 42, 43 },
            WargConfig.ImpactBoneIds(monsterId),
            "only the dire wolf changes the set; anything else keeps the behaviour it had");
    }

    [TestMethod]
    public void ImpactBoneIds_CalledTwice_ReturnsSeparateLists()
    {
        var first = WargConfig.ImpactBoneIds("dire_wolf");
        first.Clear();
        Assert.AreEqual(10, WargConfig.ImpactBoneIds("dire_wolf").Count,
            "a caller that changes its list must not change the next caller's");
    }

    [TestMethod]
    public void NonWargMounts_AreNotRecognised()
    {
        Assert.IsFalse(WargConfig.IsWargMonster("horse"));
        Assert.IsFalse(WargConfig.IsWargMonster("camel"));
        Assert.IsFalse(WargConfig.IsWargMonster("taom_war_elephant"));
        Assert.IsFalse(WargConfig.IsWargMonster("spider"));
    }

    [TestMethod]
    public void MissingOrEmptyId_IsNotAWarg()
    {
        Assert.IsFalse(WargConfig.IsWargMonster(null), "a null StringId must not throw or match");
        Assert.IsFalse(WargConfig.IsWargMonster(string.Empty));
    }

    [TestMethod]
    public void MatchIsCaseSensitive_MirroringTheOriginalEquality()
    {
        Assert.IsFalse(WargConfig.IsWargMonster("Warg"),
            "Monster ids are lowercase in XML; the original was ordinal equality and stays so");
    }

    [TestMethod]
    public void EveryFamilyMember_IsPresentInTheSet()
    {
        CollectionAssert.AreEquivalent(
            new[] { "warg", "fell_warg", "dire_wolf" },
            WargConfig.WargMonsterIds.ToArray(),
            "a future edit dropping one of these would silently disable the bite behaviour tree");
    }
}
