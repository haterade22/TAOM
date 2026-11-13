using TaleWorlds.Core;

namespace TAOM.Features.BattleHud.UI;

/// <summary>
/// Issue #1095: the ammo capsule below the circular HUD. The count, its visibility and the low ammo alert
/// are the native MissionAgentStatusVM values; this only adds the approved icon for the wielded weapon's
/// current usage and the capsule's place under the circle drawn for the present ring count. Pure apart
/// from the WeaponClass enum, so the rules are unit-testable.
/// </summary>
public static class BattleHudAmmoMapper
{
    /// <summary>The approved art: the leather quiver for bows and crossbows, the single javelin for
    /// javelins. Any other usage (stones, throwing axes and knives, or none) keeps a plain number.</summary>
    public static BattleHudAmmoIcon IconFor(WeaponClass? wielded)
    {
        switch (wielded)
        {
            case WeaponClass.Bow:
            case WeaponClass.Crossbow:
                return BattleHudAmmoIcon.Quiver;
            case WeaponClass.Javelin:
                return BattleHudAmmoIcon.Javelin;
            default:
                return BattleHudAmmoIcon.None;
        }
    }

    /// <summary>Visible only while native shows its counter with a count of zero or more (it leaves -1
    /// when there is nothing to count). A hidden capsule carries no icon, so none outlives its weapon.</summary>
    public static BattleHudAmmo Map(bool nativeShowAmmoCount, int nativeAmmoCount, WeaponClass? wielded, int ringCount)
    {
        var visible = nativeShowAmmoCount && nativeAmmoCount >= 0;
        var icon = visible ? IconFor(wielded) : BattleHudAmmoIcon.None;
        return new BattleHudAmmo(visible, icon, BattleHudGeometry.AmmoCapsuleTop(ringCount),
            BattleHudGeometry.AmmoNumberOffsetX(icon != BattleHudAmmoIcon.None));
    }
}

public enum BattleHudAmmoIcon
{
    None,
    Quiver,
    Javelin,
}

public readonly struct BattleHudAmmo
{
    public BattleHudAmmo(bool isVisible, BattleHudAmmoIcon icon, float top, float numberOffsetX)
    {
        IsVisible = isVisible;
        Icon = icon;
        Top = top;
        NumberOffsetX = numberOffsetX;
    }

    public bool IsVisible { get; }
    public BattleHudAmmoIcon Icon { get; }
    public float Top { get; }
    public float NumberOffsetX { get; }
}
