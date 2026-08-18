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
