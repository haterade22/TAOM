using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment.Equipment;

/// <summary>
/// Fallback-chain contract for <c>enlist_{culture}_{assignment}_{rank}</c> (#525).
///
/// The chain walks CULTURE first, then assignment, then rank, because issuing another faction's
/// kit is the defect players actually report (#427, #431). Keeping the culture and losing the role
/// gives a soldier the wrong job in his own army's gear; keeping the role and losing the culture
/// dresses him as somebody else's soldier. Note this is NOT a rendering argument: the roster is
/// keyed on the COMMANDER's culture, so it cannot know the player's race under either ordering.
///
/// Culture tokens are RUNTIME StringIds — the vlandia-not-rohan pin guards the #1 TAOM data bug
/// (lore names leaking into runtime ids).
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
            "gondor", ServiceAssignment.Archer, EnlistmentRank.Veteran,
            Exists("enlist_gondor_archer_veteran"));

        Assert.AreEqual("enlist_gondor_archer_veteran", result);
    }

    [TestMethod]
    public void Resolve_VlandiaCulture_BuildsVlandiaId_NeverRohan()
    {
        var queried = new List<string>();
        Func<string, bool> spy = id =>
        {
            queried.Add(id);
            return id == "enlist_vlandia_cavalry_sergeant";
        };

        var result = EnlistmentRosterResolver.Resolve(
            "vlandia", ServiceAssignment.Cavalry, EnlistmentRank.Sergeant, spy);

        Assert.AreEqual("enlist_vlandia_cavalry_sergeant", result);
        foreach (var id in queried)
            Assert.IsFalse(id.Contains("rohan"), $"runtime id must use 'vlandia', not lore name: {id}");
    }

    [TestMethod]
    public void Resolve_ExactMissing_FallsToNextLowerRank_KeepingTheAssignment()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "vlandia", ServiceAssignment.Archer, EnlistmentRank.Sergeant,
            Exists("enlist_vlandia_archer_veteran"));

        Assert.AreEqual("enlist_vlandia_archer_veteran", result);
    }

    [TestMethod]
    public void Resolve_DescendsFullRankChain_ToRecruit()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "vlandia", ServiceAssignment.Archer, EnlistmentRank.Sergeant,
            Exists("enlist_vlandia_archer_recruit"));

        Assert.AreEqual("enlist_vlandia_archer_recruit", result);
    }

    [TestMethod]
    public void Resolve_RightRoleAtALowerRank_BeatsWrongRoleAtTheRightRank()
    {
        // Rank is the innermost loop, so a lower-rank kit of the chosen role wins. A lesser
        // version of what you asked for is a smaller disappointment than the wrong thing.
        var result = EnlistmentRosterResolver.Resolve(
            "gondor", ServiceAssignment.Cavalry, EnlistmentRank.Sergeant,
            Exists("enlist_gondor_cavalry_recruit", "enlist_gondor_infantry_sergeant"));

        Assert.AreEqual("enlist_gondor_cavalry_recruit", result);
    }

    [TestMethod]
    public void Resolve_NoAssignmentRosterAnywhere_FallsToInfantryInTheSameCulture()
    {
        // goblin has no Cavalry-grouped troop, so enlist_goblin_cavalry_* is never authored.
        var result = EnlistmentRosterResolver.Resolve(
            "goblin", ServiceAssignment.Cavalry, EnlistmentRank.Soldier,
            Exists("enlist_goblin_infantry_soldier", "enlist_default_cavalry_soldier"));

        Assert.AreEqual("enlist_goblin_infantry_soldier", result,
            "the culture must outrank the assignment: the neutral cavalry default is Rohan militia "
            + "gear, and issuing another faction's kit is the complaint #427 and #431 both are");
    }

    [TestMethod]
    public void Resolve_CultureOutranksAssignment_EvenAtALowerRank()
    {
        // The sharp form of the ordering: the same culture at recruit still beats the correct
        // assignment from the neutral default at the requested rank.
        var result = EnlistmentRosterResolver.Resolve(
            "mistymountainorcs", ServiceAssignment.Archer, EnlistmentRank.Sergeant,
            Exists("enlist_mistymountainorcs_infantry_recruit", "enlist_default_archer_sergeant"));

        Assert.AreEqual("enlist_mistymountainorcs_infantry_recruit", result);
    }

    [TestMethod]
    public void Resolve_CultureWithNoRosterAtAll_FallsToDefaultKeepingTheAssignment()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "unrostered_culture", ServiceAssignment.Archer, EnlistmentRank.Veteran,
            Exists("enlist_default_infantry_veteran", "enlist_default_archer_veteran"));

        Assert.AreEqual("enlist_default_archer_veteran", result,
            "inside the default culture the assignment is preferred again");
    }

    [TestMethod]
    public void Resolve_DefaultCulture_FallsToInfantryWhenTheAssignmentIsAbsent()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "unrostered_culture", ServiceAssignment.Cavalry, EnlistmentRank.Soldier,
            Exists("enlist_default_infantry_soldier"));

        Assert.AreEqual("enlist_default_infantry_soldier", result);
    }

    [TestMethod]
    public void Resolve_Lothlorien_PrefersItsOwnRosterOverTheDefault()
    {
        // lothlorien binds to the Rivendell tree (taom_spcultures.xml: basic_troop=
        // imladris_recruit), so enlist_lothlorien_* ships and must beat the default.
        var result = EnlistmentRosterResolver.Resolve(
            "lothlorien", ServiceAssignment.Infantry, EnlistmentRank.Veteran,
            Exists("enlist_lothlorien_infantry_veteran", "enlist_default_infantry_veteran"));

        Assert.AreEqual("enlist_lothlorien_infantry_veteran", result);
    }

    [TestMethod]
    public void Resolve_BattaniaKhand_PrefersItsOwnRosterOverTheDefault()
    {
        // Khand's runtime id is battania (XSLT culture); it binds to the Rhun tree
        // (spcultures.xslt: basic_troop=loke_rim_initiate), so enlist_battania_* ships.
        var result = EnlistmentRosterResolver.Resolve(
            "battania", ServiceAssignment.Infantry, EnlistmentRank.Recruit,
            Exists("enlist_battania_infantry_recruit", "enlist_default_infantry_recruit"));

        Assert.AreEqual("enlist_battania_infantry_recruit", result);
    }

    [TestMethod]
    public void Resolve_InfantryRequest_DoesNotProbeInfantryTwice()
    {
        // The assignment fallback IS infantry, so an infantry request must dedupe rather than
        // walk the same four ids a second time.
        var queried = new List<string>();
        Func<string, bool> spy = id => { queried.Add(id); return false; };

        EnlistmentRosterResolver.Resolve(
            "gondor", ServiceAssignment.Infantry, EnlistmentRank.Sergeant, spy);

        CollectionAssert.AllItemsAreUnique(queried);
    }

    [TestMethod]
    public void Resolve_UndefinedAssignmentOrdinal_DoesNotProbeInfantryTwiceEither()
    {
        // This is the case the dedupe actually turns on, and the Infantry test above does NOT
        // cover it. AssignmentChain compares the two steps by TOKEN, not by enum value. Swap it
        // for an ordinal comparison and the Infantry test still passes (99 != 0 is only reached
        // for an out-of-range value), while an out-of-range ordinal yields twice and walks the
        // identical four infantry ids a second time, because AssignmentToken maps both to
        // "infantry". Without this test the comment on AssignmentChain is an unpinned claim.
        var queried = new List<string>();
        Func<string, bool> spy = id => { queried.Add(id); return false; };

        EnlistmentRosterResolver.Resolve(
            "gondor", (ServiceAssignment)99, EnlistmentRank.Sergeant, spy);

        CollectionAssert.AllItemsAreUnique(queried);
    }

    [TestMethod]
    public void Resolve_NothingExists_ReturnsNull()
    {
        var result = EnlistmentRosterResolver.Resolve(
            "gondor", ServiceAssignment.Infantry, EnlistmentRank.Sergeant, _ => false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Resolve_NullCulture_SkipsCultureChain_UsesDefault()
    {
        var queried = new List<string>();
        Func<string, bool> spy = id =>
        {
            queried.Add(id);
            return id == "enlist_default_support_soldier";
        };

        var result = EnlistmentRosterResolver.Resolve(
            null, ServiceAssignment.Support, EnlistmentRank.Soldier, spy);

        Assert.AreEqual("enlist_default_support_soldier", result);
        foreach (var id in queried)
            Assert.IsTrue(id.StartsWith("enlist_default_", StringComparison.Ordinal),
                $"null culture must not probe culture rosters: {id}");
    }

    [TestMethod]
    public void Resolve_NullRosterExists_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => EnlistmentRosterResolver.Resolve(
                "gondor", ServiceAssignment.Infantry, EnlistmentRank.Recruit, null));
    }

    [TestMethod]
    public void Resolve_UndefinedAssignmentOrdinal_StillResolves()
    {
        // A save carrying an ordinal outside the enum (a future assignment, or a corrupt record)
        // must not throw and must not silently take another assignment's kit. AssignmentToken
        // falls back to infantry, which is the one assignment every culture authors.
        var result = EnlistmentRosterResolver.Resolve(
            "gondor", (ServiceAssignment)99, EnlistmentRank.Soldier,
            Exists("enlist_gondor_infantry_soldier"));

        Assert.AreEqual("enlist_gondor_infantry_soldier", result);
    }
}
