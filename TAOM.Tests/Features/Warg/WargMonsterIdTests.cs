using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Warg;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Pins which Monster ids count as a warg.
///
/// This matters more than it looks. <c>AgentAdapter.IsWarg()</c> is the only gate on
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
    public void BothFamilyMembers_ArePresentInTheSet()
    {
        CollectionAssert.AreEquivalent(
            new[] { "warg", "fell_warg" },
            WargConfig.WargMonsterIds.ToArray(),
            "a future edit dropping one of these would silently disable the bite behaviour tree");
    }
}
