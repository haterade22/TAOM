using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.TroopWeight;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySizeModel : DefaultPartySizeLimitModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly ITroopWeightService _troopWeight;

    public TaomPartySizeModel(
        ICulturalFeatsService feats,
        ICareerPassiveService careerPassives,
        ITroopWeightService troopWeight)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _troopWeight = troopWeight;
    }

    public override ExplainedNumber GetPartyMemberSizeLimit(
        PartyBase party, bool includeDescriptions = false)
    {
        var result = base.GetPartyMemberSizeLimit(party, includeDescriptions);
        // Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
        // Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
        // (Codex review 43 caught the same systemic gap in TaomPartySpeedModel).
        _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
        // PartySize passives are authored as flat counts ("+2 party size"), so apply via ApplyFlat
        // (result.Add). ApplyFactor would treat magnitude=2 as +200% (x3 the base) — the "+2 -> +150"
        // bug. Culture party-size feats above remain factor-based (ApplyPartySizeFeats uses AddFactor).
        _careerPassives.ApplyFlat(CareerPassiveHero.ResolveId(party), ref result, PassiveEffectType.PartySize);
        // TroopWeight "elite tax" (2026-07-11 rework): shrink the limit by the party's weight surplus so
        // heavy troops fill the cap at 2× while every COUNT reads raw. Applied last so the surplus is
        // clamped against the feat/career-boosted limit. No-op when EnableTroopWeight is off.
        _troopWeight.ApplyPartySizeWeightPenalty(party, ref result);
        return result;
    }
}
