using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The filter the logic applies to a missile hit before the run hears of it (phase3-plan.md section 4 and A24). A hit counts when its missile's item id is a volley item. It is friendly by the victim's
/// side, never the attacker's (the owner may be dead or his slot reused while the arrow flew), a shield block is kept apart, and a kill is a victim under 1 health after the blow (E16).
/// </summary>
[TestClass]
public class LongDefeatVolleyHitFilterTests
{
    private static readonly HashSet<string> Volley = new() { "ld_elven_arrow", "fow_tower_arrow" };

    private static LdVolleyHit? Read(string? item = "ld_elven_arrow", bool missile = true, bool victimIsDefender = false, bool blocked = false, float health = 40f,
        bool affectorKnown = true, bool affectorIsDefender = true) =>
        LongDefeatVolleyHitFilter.Read(missile, item, Volley.Contains, victimIsDefender, blocked, health, affectorKnown, affectorIsDefender, 45f, 30f);

    [TestMethod]
    public void AnArrowOfAVolleyItem_OnAnAttacker_IsAFoeHit()
    {
        var hit = Read();

        Assert.IsNotNull(hit);
        Assert.AreEqual(LdVolleyHitKind.Foe, hit.Value.Kind);
        Assert.IsFalse(hit.Value.Kill);
        Assert.AreEqual("ld_elven_arrow", hit.Value.ItemId);
        Assert.AreEqual(45f, hit.Value.StartingBaseSpeed);
        Assert.AreEqual(30f, hit.Value.ImpactSpeed);
    }

    [TestMethod]
    public void AnythingThatIsNotAVolleyArrow_IsNotRead()
    {
        Assert.IsNull(Read(item: "wm_elven_arrow_q1v3"), "a real elven arrow shot by a soldier");
        Assert.IsNull(Read(item: null));
        Assert.IsNull(Read(missile: false), "a sword blow carries no volley item");
    }

    [TestMethod]
    public void AHitOnADefender_IsFriendly_ByTheVictimsSide_WhateverTheAffectorIs()
    {
        Assert.AreEqual(LdVolleyHitKind.Friendly, Read(victimIsDefender: true)!.Value.Kind);
        Assert.AreEqual(LdVolleyHitKind.Friendly, Read(victimIsDefender: true, affectorKnown: false)!.Value.Kind, "a null affector does not make a friend a foe");
    }

    [TestMethod]
    public void AShieldBlock_IsKeptApart_AndIsNeverAKill()
    {
        var hit = Read(blocked: true, health: 0f)!.Value;

        Assert.AreEqual(LdVolleyHitKind.Blocked, hit.Kind);
        Assert.IsFalse(hit.Kill);
    }

    [TestMethod]
    public void AShieldBlockOnADefender_IsABlock_NotAFriendlyHit_ForTheEngineNeverCancelsAFriendlyBlock()
    {
        // Mission.cs:5823 (the shield branch) runs before the friendly-fire cancel at :5866-5888, so a defender's block is not cancelled and does him no harm: R6 is about a defender hurt.
        var hit = Read(victimIsDefender: true, blocked: true)!.Value;

        Assert.AreEqual(LdVolleyHitKind.Blocked, hit.Kind);
        Assert.IsFalse(hit.Kill);
        Assert.AreEqual(LdVolleyHitKind.Friendly, Read(victimIsDefender: true, blocked: false)!.Value.Kind, "an unblocked hit on a defender is still friendly");
    }

    [TestMethod]
    public void TheHook_TreatsAWeaponBlockLikeAShieldBlock()
    {
        var hooks = RepoPaths.ReadSource("Main/Features/LongDefeat/Hooks/LongDefeatVolleyHooks.cs", stripComments: true);

        StringAssert.Contains(hooks, "collisionData.AttackBlockedWithShield || collisionData.MissileBlockedWithWeapon");
    }

    [TestMethod]
    public void AVictimUnderOneHealthAfterTheBlow_IsAKill_AndOneAtOrOverIsNot()
    {
        Assert.IsTrue(Read(health: 0.5f)!.Value.Kill);
        Assert.IsTrue(Read(health: 0f)!.Value.Kill);
        Assert.IsFalse(Read(health: 1f)!.Value.Kill);
        Assert.IsFalse(Read(health: float.NaN)!.Value.Kill, "a health that is not a number is not a kill");
    }

    [TestMethod]
    public void ANullAffector_IsCounted_AndAnAffectorOnTheWrongSide_IsCounted()
    {
        Assert.IsTrue(Read(affectorKnown: false)!.Value.AffectorNull);
        Assert.IsFalse(Read()!.Value.AffectorNull);
        Assert.IsTrue(Read(affectorIsDefender: false)!.Value.AffectorSideWrong);
        Assert.IsFalse(Read(affectorKnown: false)!.Value.AffectorSideWrong, "a missing affector has no side to get wrong");
    }

    [TestMethod]
    public void ACancelledFriendlyHit_IsItsOwnKind_AndOnlyForAVolleyItem()
    {
        var hit = LongDefeatVolleyHitFilter.Cancelled("fow_tower_arrow", Volley.Contains);

        Assert.AreEqual(LdVolleyHitKind.Cancelled, hit!.Value.Kind);
        Assert.IsNull(LongDefeatVolleyHitFilter.Cancelled("wm_elven_arrow_q1v3", Volley.Contains));
        Assert.IsNull(LongDefeatVolleyHitFilter.Cancelled(null, Volley.Contains));
    }

    [TestMethod]
    public void TheMissileTheHitCarries_IsKept_SoATowersUseCanBeToldFromAnothers()
    {
        var hit = LongDefeatVolleyHitFilter.Read(true, "fow_tower_arrow", Volley.Contains, false, false, 40f, true, true, 45f, 30f, 217);
        var cancelled = LongDefeatVolleyHitFilter.Cancelled("fow_tower_arrow", Volley.Contains, 218);

        Assert.AreEqual(217, hit!.Value.Missile);
        Assert.AreEqual(218, cancelled!.Value.Missile);
        Assert.AreEqual(-1, Read()!.Value.Missile, "a hit read with no missile names none");
    }
}
