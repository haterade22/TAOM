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
    public void Resolve_CultureWithNoRoster_FallsToDefaultForRequestedRank()
    {
        // A culture with no roster of its own takes the default for the REQUESTED rank,
        // not a lower one. (This used to be spelled with "lothlorien" on the claim that it
        // had no troop tree; it borrows Rivendell's and now ships enlist_lothlorien_* —
        // see the two tests below.)
        var result = EnlistmentRosterResolver.Resolve(
            "unrostered_culture", EnlistmentRank.Veteran,
            Exists("enlist_default_recruit", "enlist_default_veteran"));

        Assert.AreEqual("enlist_default_veteran", result);
    }

    [TestMethod]
    public void Resolve_Lothlorien_PrefersItsOwnRosterOverTheDefault()
    {
        // lothlorien binds to the Rivendell tree (taom_spcultures.xml: basic_troop=
        // imladris_recruit), so enlist_lothlorien_* ships and must beat the default.
        var result = EnlistmentRosterResolver.Resolve(
            "lothlorien", EnlistmentRank.Veteran,
            Exists("enlist_lothlorien_veteran", "enlist_default_veteran"));

        Assert.AreEqual("enlist_lothlorien_veteran", result);
    }

    [TestMethod]
    public void Resolve_BattaniaKhand_PrefersItsOwnRosterOverTheDefault()
    {
        // Khand's runtime id is battania (XSLT culture); it binds to the Rhun tree
        // (spcultures.xslt: basic_troop=loke_rim_initiate), so enlist_battania_* ships.
        var result = EnlistmentRosterResolver.Resolve(
            "battania", EnlistmentRank.Recruit,
            Exists("enlist_battania_recruit", "enlist_default_recruit"));

        Assert.AreEqual("enlist_battania_recruit", result);
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
