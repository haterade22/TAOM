using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.BattleHud.UI;

namespace TAOM.Tests.Features.BattleHud;

// Issue #1095: the ammo counter attached below the circular HUD. The count, its visibility and the low
// ammo alert are native (MissionAgentStatusVM.AmmoCount, ShowAmmoCount, IsAmmoCountAlertEnabled); TAOM
// only picks the approved icon for the wielded weapon's current usage and places the capsule under the
// circle for the present ring count. Approved art: a leather quiver for bows and crossbows, a single
// javelin for javelins. Every other ammo weapon keeps a plain number, never a borrowed icon.
[TestClass]
public class BattleHudAmmoMapperTests
{
    private const float Touch = 0.01f;

    // ── Icon choice ──────────────────────────────────────────────────────────────────────────

    [DataTestMethod]
    [DataRow(WeaponClass.Bow, BattleHudAmmoIcon.Quiver)]
    [DataRow(WeaponClass.Crossbow, BattleHudAmmoIcon.Quiver)]
    [DataRow(WeaponClass.Javelin, BattleHudAmmoIcon.Javelin)]
    [DataRow(WeaponClass.ThrowingAxe, BattleHudAmmoIcon.None)]
    [DataRow(WeaponClass.ThrowingKnife, BattleHudAmmoIcon.None)]
    [DataRow(WeaponClass.Arrow, BattleHudAmmoIcon.None)]
    [DataRow(WeaponClass.Bolt, BattleHudAmmoIcon.None)]
    public void IconFor_WieldedUsage_PicksOnlyTheApprovedArt(WeaponClass wielded, BattleHudAmmoIcon expected)
    {
        Assert.AreEqual(expected, BattleHudAmmoMapper.IconFor(wielded));
    }

    [TestMethod]
    public void IconFor_NoWieldedUsage_HasNoIcon()
    {
        Assert.AreEqual(BattleHudAmmoIcon.None, BattleHudAmmoMapper.IconFor(null));
    }

    [TestMethod]
    public void IconFor_EveryOtherWeaponClass_HasNoIcon()
    {
        // Stones, slings, throwing axes and knives and anything an engine bump adds keep a plain number.
        foreach (WeaponClass wielded in Enum.GetValues(typeof(WeaponClass)))
        {
            if (wielded == WeaponClass.Bow || wielded == WeaponClass.Crossbow || wielded == WeaponClass.Javelin)
                continue;
            Assert.AreEqual(BattleHudAmmoIcon.None, BattleHudAmmoMapper.IconFor(wielded), wielded.ToString());
        }
    }

    // ── Visibility follows the native counter ──────────────────────────────────────────────────

    [TestMethod]
    public void Map_NativeCounterShown_ShowsTheQuiverForABow()
    {
        var ammo = BattleHudAmmoMapper.Map(true, 24, WeaponClass.Bow, 3);

        Assert.IsTrue(ammo.IsVisible);
        Assert.AreEqual(BattleHudAmmoIcon.Quiver, ammo.Icon);
    }

    [TestMethod]
    public void Map_NativeCounterHidden_HidesTheCapsuleAndItsIcon()
    {
        // A javelin held for melee, a sword, or no main agent: native ShowAmmoCount is false, and no
        // icon from the previous weapon may linger.
        var ammo = BattleHudAmmoMapper.Map(false, 4, WeaponClass.Javelin, 3);

        Assert.IsFalse(ammo.IsVisible);
        Assert.AreEqual(BattleHudAmmoIcon.None, ammo.Icon);
    }

    [TestMethod]
    public void Map_ZeroAmmo_StillShowsTheCounter()
    {
        // Native shows 0 with its alert; the capsule must not vanish at the moment ammo runs out.
        var ammo = BattleHudAmmoMapper.Map(true, 0, WeaponClass.Javelin, 2);

        Assert.IsTrue(ammo.IsVisible);
        Assert.AreEqual(BattleHudAmmoIcon.Javelin, ammo.Icon);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public void Map_NegativeCount_HidesTheCapsule(int count)
    {
        // Native leaves AmmoCount at -1 when the wielded weapon has no ammo to count.
        var ammo = BattleHudAmmoMapper.Map(true, count, WeaponClass.Bow, 3);

        Assert.IsFalse(ammo.IsVisible);
        Assert.AreEqual(BattleHudAmmoIcon.None, ammo.Icon);
    }

    [TestMethod]
    public void Map_UnsupportedAmmoWeapon_ShowsTheNumberCentredWithNoIcon()
    {
        var ammo = BattleHudAmmoMapper.Map(true, 5, WeaponClass.ThrowingAxe, 3);

        Assert.IsTrue(ammo.IsVisible, "the count must not disappear for a weapon without approved art");
        Assert.AreEqual(BattleHudAmmoIcon.None, ammo.Icon);
        Assert.AreEqual(0f, ammo.NumberOffsetX, Touch, "with no icon the number is centred in the whole capsule");
    }

    [TestMethod]
    public void Map_WithAnIcon_MovesTheNumberIntoTheSpaceBesideIt()
    {
        var ammo = BattleHudAmmoMapper.Map(true, 24, WeaponClass.Bow, 3);

        Assert.AreEqual(BattleHudGeometry.AmmoNumberOffsetX(true), ammo.NumberOffsetX, Touch);
        Assert.IsTrue(ammo.NumberOffsetX > 0f, "the icon sits on the left, the number to its right");
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Map_EveryRingCount_PlacesTheCapsuleUnderThatCircle(int ringCount)
    {
        var ammo = BattleHudAmmoMapper.Map(true, 24, WeaponClass.Bow, ringCount);

        Assert.AreEqual(BattleHudGeometry.AmmoCapsuleTop(ringCount), ammo.Top, Touch);
    }

    // ── Placement: centred at 6 o'clock, tucked under the outer rim only ───────────────────────
    // AmmoCapsuleTop is measured from the top of the 160 unit HUD root; the circle's centre is at its
    // middle for every ring count.

    private static float BelowCentre(int ringCount) => BattleHudGeometry.AmmoCapsuleTop(ringCount) - BattleHudGeometry.Diameter * 0.5f;

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void AmmoCapsuleTop_EveryRingCount_TucksUnderTheShellRimButNotTheHealthRing(int ringCount)
    {
        var top = BelowCentre(ringCount);

        Assert.IsTrue(top < BattleHudGeometry.HudRadius(ringCount), $"{ringCount} rings: the capsule top {top:F2} must tuck under the shell rim");
        Assert.IsTrue(BattleHudGeometry.HudRadius(ringCount) - top <= 2.5f, $"{ringCount} rings: the capsule overlaps the shell by more than a sliver");
        Assert.IsTrue(top >= BattleHudGeometry.BandOuterRadius(0, ringCount) + BattleHudGeometry.ChannelRimWidth - Touch,
            $"{ringCount} rings: the capsule covers the health ring or its gold rim");
    }

    [TestMethod]
    public void AmmoCapsuleTop_FewerRings_FollowsTheSmallerCircle()
    {
        Assert.IsTrue(BattleHudGeometry.AmmoCapsuleTop(1) < BattleHudGeometry.AmmoCapsuleTop(2));
        Assert.IsTrue(BattleHudGeometry.AmmoCapsuleTop(2) < BattleHudGeometry.AmmoCapsuleTop(3));
        var tuck = BattleHudGeometry.HudRadius(3) - BelowCentre(3);
        Assert.AreEqual(tuck, BattleHudGeometry.HudRadius(1) - BelowCentre(1), Touch, "the same tuck under every circle");
    }

    [TestMethod]
    public void AmmoCapsule_FullHud_StaysOnScreenInsideTheBottomMargin()
    {
        // The HUD root keeps its 28 unit bottom margin; the capsule hangs below the root into it.
        const float bottomMargin = 28f;
        var bottom = BattleHudGeometry.AmmoCapsuleTop(3) + BattleHudGeometry.AmmoCapsuleHeight;

        Assert.IsTrue(bottom <= BattleHudGeometry.Diameter + bottomMargin, $"capsule bottom {bottom:F1} below the screen edge");
    }

    // ── Size: a small capsule, not a panel ─────────────────────────────────────────────────────

    [TestMethod]
    public void AmmoCapsule_IsTheApprovedSmallSize()
    {
        Assert.IsTrue(BattleHudGeometry.AmmoCapsuleWidth >= 95f && BattleHudGeometry.AmmoCapsuleWidth <= 110f,
            $"capsule width {BattleHudGeometry.AmmoCapsuleWidth}");
        Assert.IsTrue(BattleHudGeometry.AmmoCapsuleHeight >= 26f && BattleHudGeometry.AmmoCapsuleHeight <= 30f,
            $"capsule height {BattleHudGeometry.AmmoCapsuleHeight}");
        Assert.IsTrue(BattleHudGeometry.AmmoIconSize >= 24f && BattleHudGeometry.AmmoIconSize <= 28f,
            $"icon {BattleHudGeometry.AmmoIconSize}");
        Assert.IsTrue(BattleHudGeometry.AmmoIconSize <= BattleHudGeometry.AmmoCapsuleHeight, "the icon fits the capsule's height");
    }

    // ── The number sits in the space the icon leaves ───────────────────────────────────────────
    // Offsets are from the capsule's centre; the icon is inset from its left end, and the number area
    // ends the same inset from its right end.

    [TestMethod]
    public void AmmoNumberOffsetX_NoIcon_IsTheCapsuleCentre()
    {
        Assert.AreEqual(0f, BattleHudGeometry.AmmoNumberOffsetX(false), Touch);
    }

    [TestMethod]
    public void AmmoNumberOffsetX_WithIcon_CentresTheNumberBetweenTheIconAndTheRightEnd()
    {
        var half = BattleHudGeometry.AmmoCapsuleWidth * 0.5f;
        var iconRight = -half + BattleHudGeometry.AmmoIconInset + BattleHudGeometry.AmmoIconSize;
        var areaRight = half - BattleHudGeometry.AmmoIconInset;
        var centre = BattleHudGeometry.AmmoNumberOffsetX(true);
        var numberLeft = centre - BattleHudGeometry.AmmoNumberWidth * 0.5f;
        var numberRight = centre + BattleHudGeometry.AmmoNumberWidth * 0.5f;

        Assert.IsTrue(numberLeft >= iconRight - Touch, $"number box {numberLeft:F1} overlaps the icon ending at {iconRight:F1}");
        Assert.IsTrue(numberRight <= areaRight + Touch, $"number box {numberRight:F1} runs into the capsule's right end {areaRight:F1}");
        Assert.AreEqual((iconRight + areaRight) * 0.5f, centre, 0.5f, "centred in the remaining area");
    }
}
