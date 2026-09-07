using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Features.TroopWeight;

public interface ITroopWeightService
{
    float GetTroopWeight(string troopStringId);
    float GetTroopWeight(CharacterObject character);
    float CalculateWeightedMemberCount(PartyBase party);
    float CalculateWeightedRosterCount(TroopRoster roster);
    float CalculateWeightedElementCount(TroopRosterElement element);

    /// <summary>
    /// The "elite tax" as a party-size-LIMIT deflation (the 2026-07-11 rework): instead of inflating the
    /// member COUNT (which polluted every count display), heavy troops now SHRINK the party-size limit by
    /// their weight surplus, so counts read raw everywhere while the recruit cap still fills at the troop
    /// weight. Subtracts <c>ceil(weightedCount) − rawCount</c> from <paramref name="limit"/> (clamped so
    /// the limit stays ≥ 1). No-op when <c>EnableTroopWeight</c> is off or the party is light. Gated +
    /// engine-touching (mirrors <see cref="CalculateWeightedMemberCount"/>); the clamp math is the pure,
    /// unit-tested <see cref="TroopWeightService.ComputeSizePenalty"/>.
    /// </summary>
    /// <para>
    /// <paramref name="includeDescriptions"/> is vanilla's display/enforcement discriminator, and this
    /// method skips the penalty entirely when it is <c>true</c> (2026-09-06 usage-frame reframe), so the
    /// tooltip no longer renders a "Heavy troops −N" line. Verified against v1.4.8: only
    /// <c>PartyBase.PartySizeLimit</c> (which passes <c>false</c>) feeds gameplay;
    /// <c>PartyBase.PartySizeLimitExplainer</c> (<c>true</c>) is consumed solely by
    /// <c>CampaignUIHelper.GetPartyTroopSizeLimitTooltip</c> and <c>RecruitmentVM</c>'s capacity hint.
    /// The cap is unchanged — the weight cost is shown on the USED side instead
    /// (see <see cref="TroopWeightDisplay"/>).
    /// </para>
    void ApplyPartySizeWeightPenalty(PartyBase party, ref ExplainedNumber limit, bool includeDescriptions);

    /// <summary>
    /// The party's TRUE (pre-weight-penalty) size limit — what the limit would be without the elite-tax
    /// deflation. The shed-on-upgrade hook needs this to trim a heavy party back to its real cap; it can't
    /// reconstruct it from the deflated <c>PartySizeLimit</c> alone because the penalty clamp is lossy
    /// (a clamped limit floors at 1 regardless of the true base). Returns the current deflated limit as a
    /// fallback when no penalty has been captured (e.g. <c>EnableTroopWeight</c> off).
    /// </summary>
    int GetTrueBaseSizeLimit(PartyBase party);

    /// <summary>
    /// Pure core for the weighted battle-ready / wounded split. Sums (Number-WoundedNumber)*weight
    /// into Healthy and WoundedNumber*weight into Wounded, then ceilings each. This is the
    /// authoritative fix for the phantom-wounded display bug: weighted-healthy + weighted-wounded
    /// equals the weighted member total, so a consumer that does (AllMembers - HealthyMembers) no
    /// longer manufactures wounds out of the weight surplus. Engine-free; unit-tested.
    /// </summary>
    (int Healthy, int Wounded) ComputeWeightedHealthyAndWounded(
        IEnumerable<(string TroopId, int Number, int WoundedNumber)> elements);

    /// <summary>
    /// Reads <paramref name="party"/>'s MemberRoster and returns the weighted (Healthy, Wounded)
    /// split via <see cref="ComputeWeightedHealthyAndWounded"/>. Returns (0,0) on any error
    /// (consistent with the other roster-iterating service methods). Not unit-tested (sealed
    /// PartyBase / TroopRoster) — the math it delegates to is.
    /// </summary>
    (int Healthy, int Wounded) GetWeightedHealthAndWounded(PartyBase party);

    /// <summary>
    /// Pure planner for shed-on-upgrade. Given a party's roster as engine-free
    /// <see cref="WeightedTroopEntry"/> rows and its (vanilla) party-size <paramref name="limit"/>,
    /// returns the troops to remove so the WEIGHTED member total no longer exceeds the limit.
    /// Sheds lowest-value first (ascending Tier, then Weight) so elites are kept and the cheap fodder
    /// that ballooned the party via auto-upgrade is trimmed — the "fewer, better troops" intent.
    /// Never sheds hero entries. Removes only as many bodies as needed to reach the budget (no
    /// over-shed). Returns an empty list when already within budget, when nothing sheddable remains,
    /// or on null/empty input. Engine-free; unit-tested.
    /// </summary>
    IReadOnlyList<ShedInstruction> PlanShed(IReadOnlyList<WeightedTroopEntry> entries, int limit);

    void ClearCache();
}
