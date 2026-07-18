using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CulturalFeats.Diagnostics;
using TAOM.Features.TroopWeight;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySizeModel : DefaultPartySizeLimitModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly ITroopWeightService _troopWeight;
    // TEMPORARY (2026-07-17) — only the load-order probe below needs this; remove with the diagnostic.
    private readonly IModLogger _logger;

    public TaomPartySizeModel(
        ICulturalFeatsService feats,
        ICareerPassiveService careerPassives,
        ITroopWeightService troopWeight,
        IModLogger logger)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _troopWeight = troopWeight;
        _logger = logger;
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
        // heavy troops fill the cap at 2× while every COUNT reads raw. No-op when EnableTroopWeight is off.
        // Call position is irrelevant to the arithmetic — ExplainedNumber sums factors and applies them to
        // BaseNumber, so an Add lands in the base frame whenever it runs. The surplus is a result-frame body
        // count, so the service divides the factors back out (SubtractResultFramePenalty); it must run after
        // the feats above only so it reads the boosted ResultNumber as its base.
        _troopWeight.ApplyPartySizeWeightPenalty(party, ref result);
        // TEMPORARY (2026-07-17) — one-shot load-order probe for the "max troop goes over after loading"
        // report; self-guards to the first main-party computation. Strip with the diagnostic class.
        PartySizeLoadOrderDiagnostic.RecordFirstMainPartyLimit(party, result.ResultNumber, _careerPassives, _logger);
        return result;
    }
}
