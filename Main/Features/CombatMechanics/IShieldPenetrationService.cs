namespace TAOM.Features.CombatMechanics;

public interface IShieldPenetrationService
{
    // OR-in CanPenetrateShield (+ MultiplePenetration when configured) for config-listed item ids
    // / weapon classes. Flags cross the boundary as ulong (WeaponFlags is a ulong enum) so the
    // service stays TaleWorlds-free (ADR-007). Returns currentFlags unchanged when not granted.
    ulong ApplyPenetrationFlags(string itemId, string weaponClassName, ulong currentFlags);

    // Native workaround (spec mechanic 8): when TAOM grants penetration at RUNTIME and the item's
    // static flags lack it, divide shield damage by the configured divisor (default 0.3).
    float ApplyRuntimeFlagCorrection(string itemId, string weaponClassName, bool itemHasStaticPenetrateFlag, float baseShieldDamage);
}
