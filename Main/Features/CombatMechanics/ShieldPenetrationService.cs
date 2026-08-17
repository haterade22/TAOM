using System;
using System.Collections.Generic;

namespace TAOM.Features.CombatMechanics;

// Spec mechanic 8 (the combat-mechanics spec): config-granted shield
// penetration flags in DecideMissileWeaponFlags + the runtime-flag shield-damage correction in
// CalculateShieldDamage.
//
// SHIPS OFF since 2026-08-17, with empty grant lists. The correction was originally a workaround
// for a "native underestimation" of shield damage on runtime-granted flags (TW forums
// 470085/470117); that premise was disproved against v1.4.8 and is NOT a live justification. See
// ShieldPenetrationConfig in CombatMechanicsConfig.cs for the engine trace. Both methods below are
// identity functions under the shipped config; the class stays for opt-in per item id or class.
//
// Per-hit HOT PATH: grant lists are precomputed into HashSets in the constructor; the only per-call
// provider read is the MCM-backed settings toggle (changes mid-session by design). No LINQ, no
// per-call allocation. TaleWorlds-free (ADR-007) — flags cross the boundary as ulong.
public class ShieldPenetrationService : IShieldPenetrationService
{
    // Verified against installed 1.4.6 TaleWorlds.Core.WeaponFlags via ilspycmd (spec "Engine
    // ground truth", 2026-07-02): CanPenetrateShield = 0x20000, MultiplePenetration = 0x40000000.
    private const ulong CanPenetrateShieldFlag = 0x20000UL;
    private const ulong MultiplePenetrationFlag = 0x40000000UL;

    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly HashSet<string> _itemIds;
    private readonly HashSet<string> _weaponClasses;
    private readonly bool _addMultiplePenetration;
    private readonly bool _correctionEnabled;
    private readonly float _correctionDivisor;

    public ShieldPenetrationService(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings)
    {
        _settings = settings;

        // WeaponClasses entries are already enum-validated by the config provider; item ids and
        // class names are matched ordinal (Bannerlord ids + enum member names are case-sensitive).
        var config = configProvider.GetConfig()?.ShieldPenetration ?? new ShieldPenetrationConfig();
        _itemIds = BuildSet(config.ItemIds);
        _weaponClasses = BuildSet(config.WeaponClasses);
        _addMultiplePenetration = config.AddMultiplePenetration;
        _correctionEnabled = config.RuntimeShieldDamageCorrectionEnabled;
        _correctionDivisor = config.RuntimeShieldDamageCorrectionDivisor;
    }

    public ulong ApplyPenetrationFlags(string itemId, string weaponClassName, ulong currentFlags)
    {
        if (!_settings.ShieldPenetrationEnabled || !IsGranted(itemId, weaponClassName))
        {
            return currentFlags;
        }

        var flags = currentFlags | CanPenetrateShieldFlag;
        if (_addMultiplePenetration)
        {
            flags |= MultiplePenetrationFlag;
        }

        return flags;
    }

    public float ApplyRuntimeFlagCorrection(string itemId, string weaponClassName, bool itemHasStaticPenetrateFlag, float baseShieldDamage)
    {
        // Correction applies ONLY when TAOM granted penetration at runtime and the item's own
        // static flags lack it — statically-flagged items get correct native shield damage.
        if (itemHasStaticPenetrateFlag
            || !_correctionEnabled
            || !_settings.ShieldPenetrationEnabled
            || !IsGranted(itemId, weaponClassName))
        {
            return baseShieldDamage;
        }

        return baseShieldDamage / _correctionDivisor;
    }

    private bool IsGranted(string itemId, string weaponClassName)
    {
        return (itemId != null && _itemIds.Contains(itemId))
            || (weaponClassName != null && _weaponClasses.Contains(weaponClassName));
    }

    private static HashSet<string> BuildSet(List<string> values)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (values == null)
        {
            return set;
        }

        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] != null)
            {
                set.Add(values[i]);
            }
        }

        return set;
    }
}
