using TAOM.Adapters;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.Roles;

/// <summary>
/// Equipment-based combat-role classification for heroes. Pure function of equipment +
/// mount status — no skill thresholds, no random factors. Cached by hero StringId +
/// equipment fingerprint.
/// </summary>
public interface ICompanionRoleService
{
    /// <summary>Detect the primary combat role from the hero's first equipped weapon.</summary>
    CombatRole GetPrimaryRole(IHeroCombatAdapter hero);

    /// <summary>True when the hero has a horse equipped (slot 10 non-empty).</summary>
    bool IsMounted(IHeroCombatAdapter hero);

    /// <summary>Short text label for HUD ("BOW", "INF", "1H", ...). Empty for Unknown.</summary>
    string GetRoleShortText(CombatRole role);

    /// <summary>True for ranged classes (Archer, Crossbow, Slinger).</summary>
    bool IsRangedRole(CombatRole role);
}
