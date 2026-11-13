using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.AiPartySize;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CulturalFeats.Diagnostics;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.KeepWardens;
using TAOM.Features.TroopWeight;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySizeModel : DefaultPartySizeLimitModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly ITroopWeightService _troopWeight;
    private readonly IAiPartySizeService _aiPartySize;
    // TEMPORARY (2026-07-17) — only the load-order probe below needs this; remove with the diagnostic.
    private readonly IModLogger _logger;
    private readonly IEyeOfSauronHook _eyeOfSauron;
    private readonly ICastlePatrolSizeHook _castlePatrolSize;
    private readonly IPatrolRosterHook _patrolRoster;
    // Built once: a lambda over the method parameter would allocate a closure and a Func on every call, every party.
    private readonly System.Func<object?, float> _weightedCountOf;

    public TaomPartySizeModel(
        ICulturalFeatsService feats,
        ICareerPassiveService careerPassives,
        ITroopWeightService troopWeight,
        IAiPartySizeService aiPartySize,
        IModLogger logger,
        IEyeOfSauronHook eyeOfSauron)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _troopWeight = troopWeight;
        _aiPartySize = aiPartySize;
        _logger = logger;
        _eyeOfSauron = eyeOfSauron;
        // Resolved once here, not as a constructor parameter: that would need an edit to SubModule.cs, where this model is built.
        _castlePatrolSize = IoC.Resolve<ICastlePatrolSizeHook>();
        _patrolRoster = IoC.Resolve<IPatrolRosterHook>();
        _weightedCountOf = p => _troopWeight.CalculateWeightedMemberCount((PartyBase)p!);
    }

    public override ExplainedNumber GetPartyMemberSizeLimit(
        PartyBase party, bool isCurrentlyAtSea, bool includeDescriptions = false)
    {
        var result = base.GetPartyMemberSizeLimit(party, isCurrentlyAtSea, includeDescriptions);
        // Keep Wardens (#888): vanilla's patrol limit reads only the town Guard House and answers 0 for a castle patrol. MUST run
        // right after the base call, which returns the 0 it replaces, and before the culture feats, whose factors then apply to the
        // replaced number. Pinned by CastlePatrolSizeHookTests.
        _castlePatrolSize.ApplyCastlePatrolSize(party, ref result, includeDescriptions);
        // Vanilla PartyBaseHelper.HasFeat precedence — see CultureFeatAdapter.FromOrNull(PartyBase).
        // Replaces the prior `party.Owner?.Culture ?? party.Culture` which skipped LeaderHero.Culture
        // (Codex review 43 caught the same systemic gap in TaomPartySpeedModel).
        _feats.ApplyPartySizeFeats(CultureFeatAdapter.FromOrNull(party), ref result);
        // AI lord scaling (#461): lets AI lords HOLD their spawned roster instead of being trimmed to
        // the vanilla 50-150 cap within a day. MUST run BEFORE the TroopWeight line below — that call
        // snapshots (int)result.ResultNumber as the "true base" the shed later trims a heavy party to,
        // so scaling applied after it would leave the shed trimming to the UNSCALED limit and this
        // whole feature would silently no-op. Pinned by AiPartySizeOrderingTests.
        _aiPartySize.ApplyAiLordScaling(party, ref result);
        // Caravan escort cap. Vanilla's caravan branch is 20 + (10 | 20 | 30) and its clan-tier and
        // Steward branch is guarded !party.IsCaravan, so a caravan is capped far below the roster
        // TAOM's parity templates spawn. MUST run before the TroopWeight line below for the same
        // reason ApplyAiLordScaling does: that call snapshots (int)result.ResultNumber as the "true
        // base" the daily shed later trims to, so a bonus applied after it would leave the shed
        // trimming to the unscaled cap and the caravan would bleed back to 30 men over a week with
        // every unit test still green. Pinned by AiPartySizeOrderingTests.
        _aiPartySize.ApplyCaravanScaling(party, ref result);
        // PartySize passives are authored as flat counts ("+2 party size"), so apply via ApplyFlat
        // (result.Add). ApplyFactor would treat magnitude=2 as +200% (x3 the base) — the "+2 -> +150"
        // bug. Culture party-size feats above remain factor-based (ApplyPartySizeFeats uses AddFactor).
        // Leader only, not CareerPassiveHero.ResolveId (owner first): a +50 pick must not grow every
        // caravan the player owns (#768).
        // MUST run after the last AddFactor (the culture feats and the AI lord scaling above; the caravan bonus adds no factor): ApplyFlat divides
        // out only the factors already on the number, so a later factor would multiply the flat count
        // again. MUST run before the TroopWeight line below so its snapshot includes the career count.
        // Pinned by AiPartySizeOrderingTests.
        _careerPassives.ApplyFlat(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PartySize);
        // #765 M8: the Eye of Sauron hunter's host. Lifts the hunter's limit to its own head count (weighted), so the
        // daily shed never trims the topped-up host. MUST run before the TroopWeight line, for the reason the scaling
        // lines above give; pinned by AiPartySizeOrderingTests. The weighted count is walked for the hunter only.
        // Its place after the career count is free: AddResultFrameBonus adds no factor, and no career hero leads the
        // hunter, so each of the two adds zero wherever the other applies.
        AiPartySizeService.AddResultFrameBonus(ref result, _eyeOfSauron.GetHostSizeBonus(
            party.MobileParty?.StringId, result.ResultNumber, result.SumOfFactors,
            party.MemberRoster?.TotalManCount ?? 0, party, _weightedCountOf));
        // TroopWeight "elite tax" (2026-07-11 rework): shrink the limit by the party's weight surplus so
        // heavy troops fill the cap at 2× while every COUNT reads raw. No-op when EnableTroopWeight is off.
        // Call position is irrelevant to the arithmetic — ExplainedNumber sums factors and applies them to
        // BaseNumber, so an Add lands in the base frame whenever it runs. The surplus is a result-frame body
        // count, so the service divides the factors back out (SubtractResultFramePenalty); it must run after
        // the feats above only so it reads the boosted ResultNumber as its base.
        // includeDescriptions is passed straight through: the service skips the penalty on the display
        // (tooltip) path so the breakdown shows the honest base instead of a "Heavy troops -N" line, while
        // the enforcement path (PartyBase.PartySizeLimit, includeDescriptions: false) still deflates. The
        // branch lives in the service, not here — gamemodels.md rule 4 keeps this override branch-free.
        _troopWeight.ApplyPartySizeWeightPenalty(party, ref result, includeDescriptions);
        // TEMPORARY (2026-07-17) — one-shot load-order probe for the "max troop goes over after loading"
        // report; self-guards to the first main-party computation. Strip with the diagnostic class.
        PartySizeLoadOrderDiagnostic.RecordFirstMainPartyLimit(party, result.ResultNumber, _careerPassives, _logger);
        return result;
    }

    // Keep Wardens (#991): vanilla fills each template stack to its maximum, so a patrol spawns and refills at its template's head
    // count, not its limit, and one under 90 percent of the limit never leaves home. Base FIRST, so TAOM's Patch39 postfix and the
    // co-op postfix on the base still see the roster they patch; the hook then fits a land patrol's roster and leaves every other
    // party's alone. One expression (gamemodels.md rule 4); pinned by PatrolRosterHookTests.
    public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate) =>
        _patrolRoster.FitToLimit(party, base.FindAppropriateInitialRosterForMobileParty(party, partyTemplate));

    public override ExplainedNumber CalculateGarrisonPartySizeLimit(
        Settlement settlement, bool includeDescriptions = false)
    {
        var result = base.CalculateGarrisonPartySizeLimit(settlement, includeDescriptions);
        // Siege balance, not an AI handicap (#461): applies to every garrison, player-owned included.
        // Reached both directly and via base.GetPartyMemberSizeLimit's garrison branch, which dispatches
        // virtually onto this override.
        _aiPartySize.ApplyGarrisonScaling(ref result);
        return result;
    }
}
