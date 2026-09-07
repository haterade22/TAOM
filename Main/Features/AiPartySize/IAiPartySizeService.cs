using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.AiPartySize;

/// <summary>
/// Lets AI lord parties actually HOLD the roster their party template spawns them with.
///
/// Spawn size and sustained size are set by two systems that never agreed. A lord party is filled
/// from its <c>PartyTemplateObject</c> by <c>FindAppropriateInitialRosterForMobileParty</c>, which
/// draws one uniform ratio per party and never consults <c>PartySizeLimit</c>; the sustained cap is
/// <c>DefaultPartySizeLimitModel</c>'s pure-additive 20 + 25/tier + Steward, which lands at 50-150
/// for a typical lord. Everything above that cap is then removed, fast: TAOM's own TroopWeight shed
/// runs off <c>DailyTickPartyEvent</c> and trims the WHOLE overflow in a single tick.
///
/// Raising the cap is necessary but NOT sufficient. Two further mechanisms ignore it entirely and
/// are driven by morale instead: vanilla's morale desertion (up to 14.87%/day below morale 10) and
/// the garrison dump. Both are fed by starvation (-30 morale) and unpaid wages (-20), which a large
/// party incurs automatically. That is why this service also carries food and wage relief: without
/// them the parties still bleed out, just through a different path. See docs/features/ai-party-size.md.
/// </summary>
public interface IAiPartySizeService
{
    /// <summary>
    /// Scales an AI lord party's size limit. MUST be called BEFORE
    /// <c>ITroopWeightService.ApplyPartySizeWeightPenalty</c>, which snapshots
    /// <c>(int)limit.ResultNumber</c> and caches it as the "true base" the shed later trims to.
    /// Called after, the shed still trims to the unscaled limit and this whole feature no-ops.
    /// Skips the main party and anything that is not a leader-run lord party.
    /// </summary>
    void ApplyAiLordScaling(PartyBase party, ref ExplainedNumber limit);

    /// <summary>
    /// Scales a settlement garrison's size limit. Applies to EVERY garrison, player-owned included:
    /// this is siege balance, not an AI handicap. Without it, lords fielding thousands walk over
    /// garrisons still capped near vanilla's 200.
    /// </summary>
    /// <summary>
    /// Raises a caravan's member cap so it can hold the roster its party template spawns.
    ///
    /// Vanilla caps a caravan at 20 + (10 | 20 | 30) by notable Power, and its clan-tier and
    /// Steward branch is guarded <c>!party.IsCaravan</c>, so nothing else moves the number. TAOM's
    /// caravan templates are sized for parity with a bandit warband, which is well above that, and
    /// an over-cap caravan is actively drained: <c>DesertionCampaignBehavior</c> accepts
    /// <c>IsCaravan</c> and sheds a quarter of the excess daily with no morale condition, while
    /// <c>GetOverPartySizeEffect</c> costs it half its speed at twice the cap.
    ///
    /// Unlike every other member of this interface this is NOT gated by the feature toggle, and has
    /// no MCM knob. The cap and the shipped template maxima are two halves of one balance change, so
    /// a switch that reverted one while the other stayed put would ship precisely the shed and the
    /// speed penalty it exists to prevent. Pinned by CaravanPartySizeTests.
    /// </summary>
    void ApplyCaravanScaling(PartyBase party, ref ExplainedNumber limit);

    void ApplyGarrisonScaling(ref ExplainedNumber limit);

    /// <summary>
    /// Reduces an AI lord party's daily food consumption. Consumption is NEGATIVE, so the relief
    /// factor moves it toward zero. Deliberately not done by overriding
    /// <c>NumberOfMenOnMapToEatOneFood</c>, which is global and would silently retune the player.
    /// </summary>
    void ApplyAiFoodRelief(MobileParty party, ref ExplainedNumber consumption);

    /// <summary>
    /// Reduces an AI lord party's total wage bill, so the clan stays inside a wage bracket it can
    /// actually pay and <c>HasUnpaidWages</c> never pins to 1.0 (a flat -20 morale, which opens the
    /// morale-desertion path this feature exists to close). Player parties are untouched.
    /// </summary>
    void ApplyAiWageRelief(MobileParty party, ref ExplainedNumber wage);
}
