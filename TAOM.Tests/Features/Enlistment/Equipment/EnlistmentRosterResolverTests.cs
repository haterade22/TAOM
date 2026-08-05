using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment.Equipment;

/// <summary>
/// Fallback-chain contract: exact -> lower ranks (descending) -> enlist_default_{rank}
/// -> null. Culture tokens are RUNTIME StringIds — the vlandia-not-rohan pin guards
/// the #1 TAOM data bug (lore names leaking into runtime ids).
/// </summary>
[TestClass]
public class EnlistmentRosterResolverTests
{
    private static Func<string, bool> Exists(params string[] ids)
    {
        var set = new HashSet<string>(ids, StringComparer.Ordinal);
        return set.Contains;
    }

    [TestMethod]
    public void Resolve_ExactRosterExists_ReturnsExactId()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "gondor", EnlistmentRank.Veteran, Exists("enlist_gondor_veteran"));

        Assert.AreEqual("enlist_gondor_veteran", result);
    }

    [TestMethod]
    public void Resolve_VlandiaCulture_BuildsVlandiaId_NeverRohan()
    {
        var queried = new List<string>();
        Func<string, bool> spy = id => { queried.Add(id); return id == "enlist_vlandia_sergeant"; };

        var result = EnlistmentRosterResolver.Resolve("vlandia", EnlistmentRank.Sergeant, spy);

        Assert.AreEqual("enlist_vlandia_sergeant", result);
        foreach (var id in queried)
            Assert.IsFalse(id.Contains("rohan"), $"runtime id must use 'vlandia', not lore name: {id}");
    }

    [TestMethod]
    public void Resolve_ExactMissing_FallsToNextLowerRank()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "vlandia", EnlistmentRank.Sergeant, Exists("enlist_vlandia_veteran"));

        Assert.AreEqual("enlist_vlandia_veteran", result);
    }

    [TestMethod]
    public void Resolve_DescendsFullRankChain_ToRecruit()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "vlandia", EnlistmentRank.Sergeant, Exists("enlist_vlandia_recruit"));

        Assert.AreEqual("enlist_vlandia_recruit", result);
    }

    [TestMethod]
    public void Resolve_LothlorienNoRosters_FallsToDefaultForRequestedRank()
    {
        // Lothlorien has no troop tree, hence no culture rosters — the default must
        // match the REQUESTED rank, not a lower one.
        var result = EnlistmentRosterResolver.Resolve(
            "lothlorien", EnlistmentRank.Veteran,
            Exists("enlist_default_recruit", "enlist_default_veteran"));

        Assert.AreEqual("enlist_default_veteran", result);
    }

    [TestMethod]
    public void Resolve_BattaniaKhandNoRosters_FallsToDefault()
    {
        // Khand's runtime id is battania (XSLT culture) — no tree, default fallthrough.
        var result = EnlistmentRosterResolver.Resolve(
            "battania", EnlistmentRank.Recruit, Exists("enlist_default_recruit"));

        Assert.AreEqual("enlist_default_recruit", result);
    }

    [TestMethod]
    public void Resolve_NothingExists_ReturnsNull()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "gondor", EnlistmentRank.Sergeant, _ => false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_NullCulture_SkipsCultureChain_UsesDefault()
    {
        var queried = new List<string>();
        Func<string, bool> spy = id => { queried.Add(id); return id == "enlist_default_soldier"; };

        var result = EnlistmentRosterResolver.Resolve(null, EnlistmentRank.Soldier, spy);

        Assert.AreEqual("enlist_default_soldier", result);
        Assert.AreEqual(1, queried.Count, "null culture must not probe culture rosters");
    }

    [TestMethod]
    public void Resolve_NullRosterExists_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => EnlistmentRosterResolver.Resolve("gondor", EnlistmentRank.Recruit, null));
    }
}
