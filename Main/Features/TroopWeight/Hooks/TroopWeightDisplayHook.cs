using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopWeight.Hooks;

/// <summary>
/// Shows the elite tax as capacity USED instead of a shrunken limit (2026-09-06 usage-frame reframe).
/// Every capacity readout renders <c>weighted-used / true-base</c> (19 / 20) where vanilla would render
/// <c>raw / deflated</c> (10 / 11). Enforcement is untouched — see <see cref="TroopWeightDisplay"/> for
/// the identity that makes the two frames the same cap, and why vanilla's over-capacity warning flags
/// stay correct without being rewritten here.
///
/// It deliberately does NOT touch headcounts (map nameplate, "X vs Y" encounter menu, battle, the
/// Battle Ready / Wounded tooltip rows) — those keep reading raw so the body count always agrees with
/// reality.
///
/// <para>
/// <b>This is not purely cosmetic, and an earlier version of this comment wrongly claimed it was.</b>
/// Two vanilla confirmation prompts read the very properties rewritten here:
/// <c>RecruitmentVM.ExecuteDone</c> gates its "Over Limit" inquiry on
/// <c>CurrentPartySize &lt;= PartyCapacity</c>, and the party screen's done-path reads
/// <c>IsMainTroopsLimitWarningEnabled</c> / <c>IsOtherTroopsLimitWarningEnabled</c>. So those prompts now
/// fire in the weighted frame. That is intended — a warning should key off the same cap the player is
/// looking at — and the booleans are equivalent to vanilla's for every weight the mod ships, because
/// <c>raw &gt; deflated ⟺ weighted &gt; base</c>. `WeightedFrameIdentityTests` pins that equivalence so a
/// future weight-table or clamp change cannot silently move a confirmation threshold. Nothing here
/// changes an actual cap, a roster, or an AI decision.
/// </para>
/// </summary>
public class TroopWeightDisplayHook :
    IOnPartyVMRefreshPartyInformation,
    IOnClanPartyItemUpdateProperties,
    IOnRecruitmentVMRefreshPartyProperties,
    IOnCampaignUIHelperGetPartyHealthTooltip,
    IOnPartyCharacterVMRefreshValues
{
    private readonly ITroopWeightService _troopWeight;
    private readonly IModLogger _logger;

    // The row this hook rewrites is identified by its RENDERED definition label, so this must be resolved
    // the same way and at the same time vanilla resolves it — inside the call. Caching it in a static
    // initialiser would freeze whatever the localisation system happened to return at type-load; if that
    // is the English fallback, it stops matching the localised string vanilla builds at hover time and the
    // capacity row silently never gets rewritten on any non-English install. Vanilla itself constructs
    // this TextObject per tooltip build (CampaignUIHelper, v1.4.8), so per-call is also the cheaper
    // consistency: one small allocation on a hover path.
    private static string LandTroopCapacityLabel()
        => new TextObject("{=ZgYAGfbD}Land Troop Capacity").ToString();

    public TroopWeightDisplayHook(ITroopWeightService troopWeight, IModLogger logger)
    {
        _troopWeight = troopWeight;
        _logger = logger;
    }

    // ---------------------------------------------------------------- shared frame

    /// <summary>
    /// The (used, limit) pair to display for a live party. Returns null when there is nothing to restate —
    /// no weight surplus AND no deflation — so every caller falls through to vanilla's own text untouched.
    /// </summary>
    private (int Used, int Limit)? Frame(PartyBase party)
    {
        if (party?.MemberRoster == null)
            return null;

        int deflated = party.PartySizeLimit;
        int raw = party.MemberRoster.TotalManCount;
        int weighted = (int)Math.Ceiling(_troopWeight.CalculateWeightedMemberCount(party));

        int used = TroopWeightDisplay.DisplayUsed(raw, weighted);
        int limit = TroopWeightDisplay.DisplayLimit(deflated, _troopWeight.GetTrueBaseSizeLimit(party));

        return (used == raw && limit == deflated) ? null : ((int, int)?)(used, limit);
    }

    // ---------------------------------------------------------------- party screen header

    public void OnRefreshPartyInformation(PartyVM partyVm)
    {
        try
        {
            var logic = partyVm?.PartyScreenLogic;
            if (logic == null)
                return;

            var main = BuildLabel(logic.RightOwnerParty, partyVm.MainPartyTroops, logic.RightPartyMembersSizeLimit);
            if (main != null)
            {
                partyVm.MainPartyTroopsLbl = main.Value.Label;
                if (partyVm.IsMainTroopsLimitWarningEnabled && !main.Value.IsOverCapacity)
                    partyVm.IsMainTroopsLimitWarningEnabled = false;
            }

            var other = BuildLabel(logic.LeftOwnerParty, partyVm.OtherPartyTroops, logic.LeftPartyMembersSizeLimit);
            if (other != null)
            {
                partyVm.OtherPartyTroopsLbl = other.Value.Label;
                if (partyVm.IsOtherTroopsLimitWarningEnabled && !other.Value.IsOverCapacity)
                    partyVm.IsOtherTroopsLimitWarningEnabled = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Party-screen header rewrite failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Rebuilds one side's troop header in the weighted frame, plus whether that frame considers the side
    /// over capacity. Returns null to keep vanilla's string untouched.
    ///
    /// The count is summed from the SCREEN's list, not the live roster, so the header tracks pending
    /// transfers while the player drags troops. The limit still comes from the live party, which is
    /// correct: the party-size limit does not move until the transfer is applied.
    ///
    /// <para>
    /// <b>Why the caller also needs <c>IsOverCapacity</c>.</b> Vanilla drives its red over-capacity tint
    /// from <c>RightPartyMembersSizeLimit &lt; MemberRosters[1].TotalManCount</c> — a LIVE numerator over a
    /// denominator frozen at screen-open (`PartyScreenLogic.cs:491` assigns it exactly once). Vanilla's
    /// label used that same frozen denominator, so the two could never visually disagree. This hook swaps
    /// the label's denominator to the true base, which decouples them. Worst case, reachable by the exact
    /// remediation workflow the feature exists to prompt: a party whose surplus exceeded its whole base
    /// limit has its penalty clamped, so its frozen deflated limit is 1 while its true base is (say) 100.
    /// Drag the heavy troops off and the header reads a comfortable "30 / 100" directly beside vanilla's
    /// still-red over-capacity tint. The caller therefore clears a stale tint — downgrade only, never
    /// raising one, so no mode gate vanilla applied can be bypassed and no warning can be fabricated.
    /// </para>
    /// </summary>
    private (string Label, bool IsOverCapacity)? BuildLabel(
        PartyBase party, MBBindingList<PartyCharacterVM> list, int screenLimit)
    {
        if (party?.MemberRoster == null || list == null)
            return null;

        // A quest or custom party screen can pass a limit that is not the party's own. Restating it against
        // this party's true base would print a fraction neither number belongs to, so leave those vanilla.
        if (screenLimit != party.PartySizeLimit)
            return null;

        var rows = new List<(string TroopId, int Number, int WoundedNumber)>(list.Count);
        int rawHealthy = 0;
        int rawWounded = 0;
        foreach (var item in list)
        {
            var character = item?.Character;
            if (character == null || item.Number <= 0)
                continue;

            int wounded = item.WoundedCount < 0 ? 0 : item.WoundedCount;
            if (wounded > item.Number)
                wounded = item.Number;

            rows.Add((character.StringId, item.Number, wounded));
            rawHealthy += item.Number - wounded;
            rawWounded += wounded;
        }

        var (healthy, injured) = _troopWeight.ComputeWeightedHealthyAndWounded(rows);
        int limit = TroopWeightDisplay.DisplayLimit(screenLimit, _troopWeight.GetTrueBaseSizeLimit(party));

        // Nothing to restate: no weight surplus on this side and no deflation to undo.
        if (healthy == rawHealthy && injured == rawWounded && limit == screenLimit)
            return null;

        return (FormatPartyListLabel(healthy, injured, limit), healthy + injured > limit);
    }

    /// <summary>
    /// Mirrors vanilla <c>PartyVM.PopulatePartyListLabel</c>'s four label variants (v1.4.8) with weighted
    /// numbers substituted. Reproduced rather than reused because that builder is <c>private static</c>,
    /// takes no party, and is shared with the PRISONER headers — which must stay raw.
    /// </summary>
    private static string FormatPartyListLabel(int healthy, int wounded, int limit)
    {
        MBTextManager.SetTextVariable("COUNT", healthy);
        MBTextManager.SetTextVariable("WEAK_COUNT", wounded);

        if (limit != 0)
        {
            MBTextManager.SetTextVariable("MAX_COUNT", limit);
            MBTextManager.SetTextVariable("PARTY_LIST_TAG", "");
            if (wounded > 0)
            {
                MBTextManager.SetTextVariable("TOTAL_COUNT", healthy + wounded);
                return GameTexts.FindText("str_party_list_label_with_weak").ToString();
            }
            return GameTexts.FindText("str_party_list_label").ToString();
        }

        return wounded > 0
            ? GameTexts.FindText("str_party_list_label_with_weak_without_max").ToString()
            : healthy.ToString();
    }

    // ---------------------------------------------------------------- clan screen row

    public void OnClanPartyItemUpdateProperties(ClanPartyItemVM item)
    {
        try
        {
            var frame = Frame(item?.Party);
            if (frame == null)
                return;

            GameTexts.SetVariable("LEFT", frame.Value.Used);
            GameTexts.SetVariable("RIGHT", frame.Value.Limit);
            string fraction = GameTexts.FindText("str_LEFT_over_RIGHT").ToString();
            item.PartySizeText = fraction;

            // Vanilla composes the subtitle from the fraction it just built, so rebuild it from ours or the
            // row would show the weighted figure on top and the raw one underneath.
            GameTexts.SetVariable("LEFT", GameTexts.FindText("str_party_morale_party_size").ToString());
            GameTexts.SetVariable("RIGHT", fraction);
            item.PartySizeSubTitleText = GameTexts.FindText("str_LEFT_colon_RIGHT").ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Clan-screen size rewrite failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- recruitment screen

    public void OnRecruitmentRefreshPartyProperties(RecruitmentVM vm)
    {
        try
        {
            var party = PartyBase.MainParty;
            if (vm == null || party?.MemberRoster == null)
                return;

            int deflated = party.PartySizeLimit;
            int limit = TroopWeightDisplay.DisplayLimit(deflated, _troopWeight.GetTrueBaseSizeLimit(party));

            // Vanilla counts the pending cart against capacity, so the cart must be weighted too — otherwise
            // queueing a weight-2 recruit would move the numerator by 1 and the player could overfill.
            float cart = 0f;
            int cartCount = 0;
            if (vm.TroopsInCart != null)
            {
                foreach (var troop in vm.TroopsInCart)
                {
                    cart += _troopWeight.GetTroopWeight(troop?.Character);
                    cartCount++;
                }
            }

            int raw = party.MemberRoster.TotalManCount + cartCount;
            int weighted = (int)Math.Ceiling(_troopWeight.CalculateWeightedMemberCount(party) + cart);
            int used = TroopWeightDisplay.DisplayUsed(raw, weighted);

            if (used == raw && limit == deflated)
                return;

            vm.CurrentPartySize = used;
            vm.PartyCapacity = limit;
            // The same boolean vanilla computed (raw > deflated is equivalent to weighted > base), restated
            // in this frame so the warning tint can never disagree with the numbers printed beside it.
            vm.IsPartyCapacityWarningEnabled = used > limit;

            GameTexts.SetVariable("LEFT", used);
            GameTexts.SetVariable("RIGHT", limit);
            vm.PartyCapacityText = GameTexts.FindText("str_LEFT_over_RIGHT").ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Recruitment capacity rewrite failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- health tooltip capacity row

    public void OnGetPartyHealthTooltip(PartyBase party, List<TooltipProperty> properties)
    {
        try
        {
            if (properties == null)
                return;

            var frame = Frame(party);
            if (frame == null)
                return;

            var text = GameTexts.FindText("str_LEFT_over_RIGHT_no_space");
            text.SetTextVariable("LEFT", frame.Value.Used).SetTextVariable("RIGHT", frame.Value.Limit);
            string rendered = text.ToString();

            // ONLY the capacity row. The Battle Ready / Wounded rows above it are headcounts and stay raw;
            // weighting them is what produced the phantom-wounded bug (RCA 2026-06-07).
            var capacityLabel = LandTroopCapacityLabel();
            foreach (var property in properties)
            {
                if (property != null && property.DefinitionLabel == capacityLabel)
                {
                    property.ValueLabel = rendered;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Capacity-row rewrite failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- per-row weight tag

    public void OnPartyCharacterRefreshValues(PartyCharacterVM character)
    {
        try
        {
            var troop = character?.Character;
            if (troop == null)
                return;

            var multiplier = TroopWeightDisplay.FormatWeightMultiplier(_troopWeight.GetTroopWeight(troop));
            if (multiplier.Length == 0)
                return;

            // Vanilla reassigns Name from the character on every RefreshValues, so this cannot double-append.
            var tagged = new TextObject("{=taom_troop_weight_tag}{NAME} ×{MULT}");
            tagged.SetTextVariable("NAME", character.Name);
            tagged.SetTextVariable("MULT", multiplier);
            character.Name = tagged.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] Row weight tag failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
