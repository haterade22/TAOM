using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over <c>HeroCreator</c>/<c>Hero</c>/<c>HeroDeveloper</c>/<c>AddCompanionAction</c>/
/// <c>AddHeroToPartyAction</c>/<c>ClanTierModel</c> for turning a promoted troop into a companion
/// (ADR-007), and over <c>KillCharacterAction</c> for turning one back (#540). Companion-only by
/// design (#376): TAOM dropped the donor's Occupation.Lord/Family path, which raw-mutated
/// <c>Clan.Heroes</c> (bug fix (e)) — this adapter never touches <c>Clan.Heroes</c> directly, only
/// <c>AddCompanionAction.Apply</c>.
/// </summary>
public interface IHeroCommissionAdapter
{
    /// <summary>Current companion count for the player's clan and the clan-tier limit (perk
    /// bonuses already folded in by <c>ClanTierModel.GetCompanionLimit</c>).</summary>
    CompanionRoomInfo GetCompanionRoomInfo();

    /// <summary>
    /// Builds a hero from <paramref name="troopId"/>'s template, equips it from the template's
    /// default gear, applies <paramref name="skillPlan"/> via
    /// <c>HeroDeveloper.SetInitialLevel</c>/<c>SetInitialSkillLevel</c>/<c>AddFocus</c>/
    /// <c>AddAttribute</c>, names it <paramref name="chosenName"/>, adds it as a companion via
    /// <c>AddCompanionAction.Apply</c>, and places it in the main party via
    /// <c>AddHeroToPartyAction.Apply</c>. Does NOT touch the roster — the caller removes the
    /// promoted troop via <see cref="ITroopRosterQueryAdapter.RemoveOneFromRoster"/> only after
    /// this returns a non-null id (never partially-apply on a failed creation).
    /// </summary>
    /// <returns>The new hero's StringId, or null when <paramref name="troopId"/> doesn't resolve
    /// or there is no main party to add the hero to.</returns>
    string CreateCompanionFromTroop(string troopId, CommissionSkillPlan skillPlan, string chosenName);

    /// <summary>True when the given hero StringId resolves to a hero that is currently alive.
    /// Used to prune <c>_taom_fc_promotedHeroes</c> on load.</summary>
    bool IsHeroAliveAndValid(string heroId);

    /// <summary>What the dismissal verdict needs to know about the hero, or
    /// <see cref="PromotedHeroSnapshot.Missing"/> when no living hero carries the id.</summary>
    PromotedHeroSnapshot GetPromotedHeroSnapshot(string heroId);

    /// <summary>
    /// Removes the hero from the game with <c>KillCharacterAction.ApplyByRemove</c>: the same
    /// single call vanilla's own fire line ends on, and the Refuge warden rollback ships. Returns
    /// false, touching nothing, when the id resolves to no living hero or the hero's party is in a
    /// map event or siege (there the engine would only mark the hero and defer). Returns true only
    /// when the hero is dead on return.
    /// </summary>
    bool RemoveCompanionFromGame(string heroId);
}
