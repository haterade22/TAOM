using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// Null-safe replacement for the <c>party.Owner ?? party.LeaderHero</c> pattern when resolving
/// the hero whose career passives govern a party. <c>PartyBase.get_Owner</c> THROWS for a
/// settlement party whose <c>OwnerClan</c> is null — <c>Settlement.Owner</c> is
/// <c>OwnerClan.Leader</c> with no guard, and <c>OwnerClan</c> is null for any settlement that is
/// neither Village, Town, nor Hideout (TAOM_Map's <c>retirement_retreat</c>, crash 0b462fd8: the
/// settlement daily tick CTD'd every campaign on v2.0.8.0). A <c>?.</c> on the result cannot
/// guard a getter that throws internally (.claude/rules/adapters.md).
///
/// <c>MobileParty.Owner</c> (<c>=&gt; _partyComponent?.PartyOwner</c>) is the safe owner accessor
/// and keeps the owner-first precedence — player-owned caravans/garrisons led by non-career
/// companions still resolve to the player. Settlement parties resolve to null and the passive
/// skips (<c>ApplyFactor</c>/<c>ApplyFlat</c> no-op on a null id), matching intent: career
/// passives are authored for hero-run parties, not a fief's static settlement party. The engine
/// getter's remaining limb, the private <c>_customOwner</c>, has no public accessor and is only
/// set on rebel/quest edge parties whose heroes have no careers.
///
/// Enforced by <c>PartyOwnerGetterBanTests</c>: no method in TAOM.dll may call
/// <c>PartyBase.get_Owner</c> — route through this resolver instead.
/// </summary>
public static class CareerPassiveHero
{
    public static string? ResolveId(PartyBase? party)
        => (party?.MobileParty?.Owner ?? party?.LeaderHero)?.StringId;
}
