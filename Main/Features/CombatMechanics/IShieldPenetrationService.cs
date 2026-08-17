namespace TAOM.Features.CombatMechanics;

public interface IShieldPenetrationService
{
    // OR-in CanPenetrateShield (+ MultiplePenetration when configured) for config-listed item ids
    // / weapon classes. Flags cross the boundary as ulong (WeaponFlags is a ulong enum) so the
    // service stays TaleWorlds-free (ADR-007). Returns currentFlags unchanged when not granted.
    ulong ApplyPenetrationFlags(string itemId, string weaponClassName, ulong currentFlags);

    // Opt-in, and OFF under the shipped config (RuntimeShieldDamageCorrectionEnabled = false), so
    // this returns baseShieldDamage unchanged. When enabled, and when TAOM granted penetration at
    // RUNTIME while the item's static flags lack it, divides shield damage by the configured
    // divisor. It was justified as correcting a native underestimation; that premise does not hold
    // on v1.4.8 (see ShieldPenetrationConfig in CombatMechanicsConfig.cs).
    float ApplyRuntimeFlagCorrection(string itemId, string weaponClassName, bool itemHasStaticPenetrateFlag, float baseShieldDamage);
}
