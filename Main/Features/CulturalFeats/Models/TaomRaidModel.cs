using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomRaidModel : DefaultRaidModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomRaidModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateHitDamage(
        MapEventSide attackerSide, float settlementHitPoints)
    {
        var result = base.CalculateHitDamage(attackerSide, settlementHitPoints);
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared chokepoint (LeaderHero-first,
        // null-safe). MapEventSide.LeaderParty is a PartyBase, so pass it straight to
        // FromOrNull(PartyBase). Replaces the prior Owner-only inline that skipped LeaderHero.Culture
        // (Codex review 43). The careerPassives call below keys on the owning Hero's StringId (a
        // per-hero passive, not a culture feat) — owner-first via CareerPassiveHero, never the
        // throwing PartyBase.get_Owner (crash 0b462fd8). TroopDamage is class-filtered (#769 E9), and a raid has no
        // troop class, so ApplyFactor reads the army-wide pips only: a class-only pip never speeds a raid.
        _feats.ApplyRaidDamageFeats(
            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
            ref result);
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(attackerSide?.LeaderParty), ref result, PassiveEffectType.TroopDamage);
        return result;
    }

    // Career RaidLoot (#769 E11): vanilla's multiplier is 1 plus a trait bonus of the army leader. It is read four ways
    // (RaidEventComponent v1.5.5): :338 for the village gold, with AttackerSide.LeaderParty, x hearth lost x
    // GoldRewardForEachLostHearth, paid to that party's LeaderHero; :419 once per drawn item with the party that
    // drew it, where the item is kept with chance 0.5 x multiplier (the item is removed from the village either way); :445
    // for the village's production goods; and :464 for the common-loot roll, at 0.25 x multiplier. So a multiplier of 2
    // or more makes every drawn item certain, and the pip's cap should stop there. Keyed on the
    // receiving party's own LeaderHero, as the payout is: a player inside an AI army gets no gold bonus (the army leader's
    // party is the gold's receiver) but does get the bonus on the items his own party draws. No null-argument caller.
    public override float GetRaidLootMultiplier(PartyBase receivingParty)
        => _careerPassives.ScaleByPassive(receivingParty?.LeaderHero?.StringId, base.GetRaidLootMultiplier(receivingParty), PassiveEffectType.RaidLoot);
}
