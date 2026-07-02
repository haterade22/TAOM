using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
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
        // throwing PartyBase.get_Owner (crash 0b462fd8).
        _feats.ApplyRaidDamageFeats(
            CultureFeatAdapter.FromOrNull(attackerSide?.LeaderParty),
            ref result);
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(attackerSide?.LeaderParty), ref result, PassiveEffectType.TroopDamage);
        return result;
    }
}
