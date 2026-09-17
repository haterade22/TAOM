using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #613: the mask carries two axes, delivery (Melee / Ranged) and damage kind (Cut / Pierce /
/// Blunt), and a pip matches a hit per axis: its delivery bits (if any) must overlap the hit's
/// delivery, and its kind bits (if any) must overlap the hit's kind. One shared "any bit" test
/// would let a "melee blunt" pip fire on a blunt arrow through the Blunt bit. Kinds come from
/// <c>AttackCollisionData.DamageType</c> (Cut 0, Pierce 1, Blunt 2, Invalid -1).
/// </summary>
[TestClass]
public class AttackTypeMaskTests
{
    [TestMethod]
    public void Matches_KindOnlyPip_FiresOnThatKindByAnyDelivery()
    {
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Blunt, AttackTypeMask.Melee | AttackTypeMask.Blunt));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Blunt, AttackTypeMask.Ranged | AttackTypeMask.Blunt));
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.Blunt, AttackTypeMask.Melee | AttackTypeMask.Cut));
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.Cut, AttackTypeMask.Ranged | AttackTypeMask.Pierce));
    }

    [TestMethod]
    public void Matches_DeliveryOnlyPip_FiresOnThatDeliveryByAnyKind()
    {
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Melee, AttackTypeMask.Melee | AttackTypeMask.Blunt));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Melee, AttackTypeMask.Melee));
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.Melee, AttackTypeMask.Ranged | AttackTypeMask.Blunt));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Ranged, AttackTypeMask.Ranged | AttackTypeMask.Pierce));
    }

    [TestMethod]
    public void Matches_BothAxesPip_NeedsBothToOverlap()
    {
        var pip = AttackTypeMask.Melee | AttackTypeMask.Blunt;
        Assert.IsTrue(AttackTypeMaskMatch.Matches(pip, AttackTypeMask.Melee | AttackTypeMask.Blunt));
        Assert.IsFalse(AttackTypeMaskMatch.Matches(pip, AttackTypeMask.Ranged | AttackTypeMask.Blunt), "a blunt arrow is not a melee blunt hit");
        Assert.IsFalse(AttackTypeMaskMatch.Matches(pip, AttackTypeMask.Melee | AttackTypeMask.Cut));
    }

    [TestMethod]
    public void Matches_AllPip_KeepsItsOldMeaning_AnyDeliveryAnyKind()
    {
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.All, AttackTypeMask.Melee | AttackTypeMask.Cut));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.All, AttackTypeMask.Ranged | AttackTypeMask.Pierce));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.All, AttackTypeMask.Melee));
    }

    [TestMethod]
    public void Matches_KindPipAgainstHitWithUnknownKind_DoesNotFire()
    {
        // A hit whose kind the engine reports as Invalid carries no kind bit; a kind-specific pip
        // has nothing to match and stays out, while a delivery pip still fires.
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.Blunt, AttackTypeMask.Melee));
        Assert.IsTrue(AttackTypeMaskMatch.Matches(AttackTypeMask.Melee, AttackTypeMask.Melee));
    }

    [TestMethod]
    public void Matches_NonePip_NeverFires()
    {
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.None, AttackTypeMask.Melee | AttackTypeMask.Blunt));
        Assert.IsFalse(AttackTypeMaskMatch.Matches(AttackTypeMask.None, AttackTypeMask.All));
    }

    [TestMethod]
    public void ForHit_MapsDeliveryAndEngineDamageType()
    {
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Cut, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: 0));
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Pierce, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: 1));
        Assert.AreEqual(AttackTypeMask.Ranged | AttackTypeMask.Blunt, AttackTypeMaskMatch.ForHit(isMissile: true, damageType: 2));
        Assert.AreEqual(AttackTypeMask.Melee, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: -1), "Invalid carries no kind");
        Assert.AreEqual(AttackTypeMask.Ranged, AttackTypeMaskMatch.ForHit(isMissile: true, damageType: 7), "an unknown code carries no kind");
    }

    [TestMethod]
    public void ForHit_BluntByRule_OverridesTheReportedKind()
    {
        // Vanilla's own damage math (MissionCombatMechanicsHelper.GetAttackCollisionResults:200)
        // treats a bare-hand hit, a hit off the weapon's attach bone (haft, not blade), a kick or
        // bash, fall damage and a horse charge as Blunt, through a local it never writes back into
        // AttackCollisionData.DamageType. The mask must mirror the rule or a "blunt resistance" pip
        // misses a trample.
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Blunt, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: 0, bluntByRule: true));
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Blunt, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: -1, bluntByRule: true));
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Cut, AttackTypeMaskMatch.ForHit(isMissile: false, damageType: 0, bluntByRule: false));
    }

    [TestMethod]
    public void TryParse_AcceptsNamesInAnyCaseAndCommaOrPipeLists()
    {
        Assert.IsTrue(AttackTypeMaskMatch.TryParse("Melee", out var m) && m == AttackTypeMask.Melee);
        Assert.IsTrue(AttackTypeMaskMatch.TryParse("blunt", out var b) && b == AttackTypeMask.Blunt);
        Assert.IsTrue(AttackTypeMaskMatch.TryParse("Melee, Blunt", out var mb) && mb == (AttackTypeMask.Melee | AttackTypeMask.Blunt));
        Assert.IsTrue(AttackTypeMaskMatch.TryParse("Ranged|Pierce", out var rp) && rp == (AttackTypeMask.Ranged | AttackTypeMask.Pierce));
        Assert.IsTrue(AttackTypeMaskMatch.TryParse("All", out var all) && all == AttackTypeMask.All);
    }

    [TestMethod]
    public void TryParse_RejectsUnknownNamesDigitsAndEmpty()
    {
        // Enum.TryParse would accept "1" and "Melee, Fire" partially; a typo must fail, not widen.
        Assert.IsFalse(AttackTypeMaskMatch.TryParse("Fire", out _));
        Assert.IsFalse(AttackTypeMaskMatch.TryParse("Melee, Fire", out _));
        Assert.IsFalse(AttackTypeMaskMatch.TryParse("1", out _));
        Assert.IsFalse(AttackTypeMaskMatch.TryParse("", out _));
        Assert.IsFalse(AttackTypeMaskMatch.TryParse(null, out _));
        Assert.IsFalse(AttackTypeMaskMatch.TryParse("None", out _), "None is the inert sentinel, not an authorable mask");
    }
}
