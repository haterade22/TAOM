using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// The counts behind the #1361 hits, pierce and multishot checks. The mission logic hands in only hits on an enemy,
/// and every missile removal; the tally decides what each one proves. Native hands a freed missile index to the next
/// missile at once, so an index means one arrow only until its removal.
/// </summary>
[TestClass]
public class HeroRangedTallyTests
{
    private readonly object _victim = new object();
    private readonly object _other = new object();

    private static HeroRangedTally New() => new HeroRangedTally("taom_hero_legolas", 1);

    [TestMethod]
    public void PrimaryHit_CountsContactOncePerMissile()
    {
        var t = New();

        t.MissileHit(_victim, 7, extra: false, blockedShield: false, blockedWeapon: false, shieldBroken: false);
        t.MissileHit(_other, 7, false, false, false, false);
        t.MissileHit(_victim, 8, false, false, false, false);

        Assert.AreEqual(2, t.Contact, "a missile that passes through two bodies is one contact");
    }

    [TestMethod]
    public void PrimaryHit_SameIndexAfterARemoval_IsASecondContact()
    {
        var t = New();

        t.MissileHit(_victim, 7, false, false, false, false);
        t.MissileRemoved(7);
        t.MissileHit(_victim, 7, false, false, false, false);

        Assert.AreEqual(2, t.Contact, "the next arrow took the freed index");
    }

    [TestMethod]
    public void BlockedHits_AreContactToo()
    {
        var t = New();

        t.MissileHit(_victim, 1, false, blockedShield: true, false, false);
        t.MissileHit(_victim, 2, false, false, blockedWeapon: true, false);

        Assert.AreEqual(2, t.Contact);
        Assert.AreEqual(1, t.PrimaryShield);
    }

    [TestMethod]
    public void ExtraHit_IsNoContact_ButAnExtraHit()
    {
        var t = New();

        t.MissileHit(_victim, 3, extra: true, false, false, false);

        Assert.AreEqual(0, t.Contact);
        Assert.AreEqual(1, t.ExtraHits);
    }

    [TestMethod]
    public void Pierce_ShieldThenBodyOfTheSameMissileAndVictim_WithNoRemovalBetween_Passed()
    {
        var t = New();

        t.MissileHit(_victim, 5, false, blockedShield: true, false, shieldBroken: true);
        t.MissileHit(_victim, 5, false, false, false, false);

        Assert.AreEqual(1, t.ShieldHits);
        Assert.AreEqual(1, t.Broken);
        Assert.AreEqual(1, t.Passed);
    }

    [TestMethod]
    public void Pierce_BodyHitOfTheSameIndexAndVictimAfterARemoval_NotPassed()
    {
        var t = New();
        t.MissileHit(_victim, 5, false, true, false, false);
        t.MissileRemoved(5);

        t.MissileHit(_victim, 5, false, false, false, false);

        Assert.AreEqual(0, t.Passed, "the arrow stuck in the shield; the body hit is the next arrow's");
    }

    [TestMethod]
    public void Pierce_OtherVictimOrOtherMissile_NotPassed()
    {
        var t = New();
        t.MissileHit(_victim, 5, false, true, false, false);

        t.MissileHit(_other, 5, false, false, false, false);
        t.MissileHit(_victim, 6, false, false, false, false);

        Assert.AreEqual(0, t.Passed);
    }

    [TestMethod]
    public void Pierce_AWeaponBlockAfterTheShield_NotPassed()
    {
        var t = New();
        t.MissileHit(_victim, 5, false, true, false, false);

        t.MissileHit(_victim, 5, false, false, blockedWeapon: true, false);

        Assert.AreEqual(0, t.Passed);
    }

    [TestMethod]
    public void Pierce_ExtraArrowsCount()
    {
        var t = New();

        t.MissileHit(_victim, 9, extra: true, blockedShield: true, false, false);
        t.MissileHit(_victim, 9, extra: true, false, false, false);

        Assert.AreEqual(1, t.ShieldHits);
        Assert.AreEqual(0, t.PrimaryShield);
        Assert.AreEqual(1, t.Passed);
    }

    [TestMethod]
    public void MissileRemoved_OfAnIndexNeverSeen_ChangesNothing()
    {
        var t = New();
        t.MissileHit(_victim, 4, false, true, false, false);

        t.MissileRemoved(11);
        t.MissileHit(_victim, 4, false, false, false, false);

        Assert.AreEqual(1, t.Contact);
        Assert.AreEqual(1, t.Passed);
    }

    [TestMethod]
    public void NullVictim_CountsNothing()
    {
        var t = New();

        t.MissileHit(null, 1, false, false, false, false);

        Assert.AreEqual(0, t.Contact);
    }

    [TestMethod]
    public void NoteWindow_CountsEachBurstOnce()
    {
        var t = New();

        t.NoteWindow(30f);
        t.NoteWindow(30f);
        t.NoteWindow(95f);
        t.NoteWindow(float.NaN);

        Assert.AreEqual(2, t.Windows);
    }
}
