using System.Collections.Generic;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.Roles;

/// <summary>
/// Equipment-based combat-role classifier. Pure function of equipment + mount status.
/// Caches results by hero StringId + equipment fingerprint to avoid repeated weapon-class
/// lookups during per-frame UI refresh paths.
/// </summary>
public sealed class CompanionRoleService : ICompanionRoleService
{
    // Cache: heroStringId -> (signature, role). Signature is computed from the four
    // weapon-slot WeaponClass values + has-shield + has-mount; on equipment change,
    // signature changes, cache miss, recompute.
    private readonly Dictionary<string, (long sig, CombatRole role)> _cache = new();

    public CombatRole GetPrimaryRole(IHeroCombatAdapter hero)
    {
        if (hero == null) return CombatRole.Unknown;
        var equipment = hero.Equipment;
        if (equipment == null) return CombatRole.Unknown;

        var sig = ComputeSignature(equipment, hero.HasMount);
        var id = hero.StringId ?? string.Empty;
        if (_cache.TryGetValue(id, out var cached) && cached.sig == sig)
            return cached.role;

        var role = ComputeRole(equipment, hero.HasMount);
        _cache[id] = (sig, role);
        return role;
    }

    public bool IsMounted(IHeroCombatAdapter hero) => hero?.HasMount ?? false;

    public string GetRoleShortText(CombatRole role) => role switch
    {
        CombatRole.Archer => "BOW",
        CombatRole.Crossbow => "XBW",
        CombatRole.ShieldInfantry => "INF",
        CombatRole.TwoHanded => "2H",
        CombatRole.Polearm => "POL",
        CombatRole.Cavalry => "CAV",
        CombatRole.HorseArcher => "H.AR",
        CombatRole.Skirmisher => "SKR",
        CombatRole.OneHanded => "1H",
        CombatRole.Slinger => "SLG",
        _ => string.Empty,
    };

    public bool IsRangedRole(CombatRole role) =>
        role == CombatRole.Archer || role == CombatRole.Crossbow || role == CombatRole.Slinger;

    // -- Internal --

    private CombatRole ComputeRole(IBattleEquipmentSnapshot equipment, bool hasMount)
    {
        var hasShield = equipment.HasShield;

        var role = CombatRole.Unknown;
        for (var i = 0; i < 4; i++)
        {
            var wc = equipment.GetWeaponClass(i);
            if (wc == null) continue;
            role = ClassifyWeapon(wc.Value, hasShield);
            if (role != CombatRole.Unknown) break;
        }

        if (hasMount)
        {
            // Mount overrides: ranged → HorseArcher, anything else (including Unknown) → Cavalry
            if (IsRangedRole(role)) return CombatRole.HorseArcher;
            return CombatRole.Cavalry;
        }

        return role;
    }

    private static CombatRole ClassifyWeapon(WeaponClass wc, bool hasShield) => wc switch
    {
        WeaponClass.Bow => CombatRole.Archer,
        WeaponClass.Crossbow => CombatRole.Crossbow,
        WeaponClass.Sling => CombatRole.Slinger,
        WeaponClass.Stone or WeaponClass.ThrowingAxe or WeaponClass.ThrowingKnife or WeaponClass.Javelin
            => CombatRole.Skirmisher,
        WeaponClass.TwoHandedSword or WeaponClass.TwoHandedAxe or WeaponClass.TwoHandedMace
            => CombatRole.TwoHanded,
        WeaponClass.OneHandedPolearm or WeaponClass.TwoHandedPolearm or WeaponClass.LowGripPolearm
            => CombatRole.Polearm,
        WeaponClass.OneHandedSword or WeaponClass.OneHandedAxe or WeaponClass.Mace or WeaponClass.Dagger
            or WeaponClass.Pick // P4-2 fix (Codex review #36): Pick is a real melee class, was falling to Unknown.
            => hasShield ? CombatRole.ShieldInfantry : CombatRole.OneHanded,
        _ => CombatRole.Unknown,
    };

    private static long ComputeSignature(IBattleEquipmentSnapshot equipment, bool hasMount)
    {
        // Pack the four weapon-class ints (0..32) into a long with shield + mount bits.
        // 6 bits per slot is plenty (WeaponClass.NumClasses ~= 32). 4*6 = 24 bits + 2 flag bits = 26.
        long sig = 0;
        for (var i = 0; i < 4; i++)
        {
            var wc = equipment.GetWeaponClass(i);
            var v = wc.HasValue ? (int)wc.Value + 1 : 0; // 0 means empty, 1..32 means WeaponClass + 1
            sig = (sig << 6) | (uint)(v & 0x3F);
        }
        if (equipment.HasShield) sig |= 1L << 30;
        if (hasMount) sig |= 1L << 31;
        return sig;
    }
}
