using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace TAOM.Features.TroopWeight.Hooks;

/// <summary>
/// Corrects the four vanilla "battle ready / wounded" display surfaces that derive wounded as
/// <c>NumberOfAllMembers - NumberOfHealthyMembers</c>. TAOM's TroopWeight feature weights only the
/// first getter, so the weight surplus (e.g. 23 weight-2 troops counting as 46) renders as phantom
/// wounds. Each method rewrites the displayed numbers to the weighted (Healthy, Wounded) split so
/// battle-ready + wounded equals the weighted member total the rest of the feature shows.
///
/// Display-only: we deliberately do NOT weight <c>PartyBase.NumberOfHealthyMembers</c> itself
/// because it feeds gameplay (battle troop supply, casualty tracking, sacrifice limits, desertion).
/// Every body is try/catch-guarded and leaves vanilla output untouched on any failure.
/// </summary>
public class TroopWeightDisplayHook :
    IOnCampaignUIHelperGetMainPartyHealthTooltip,
    IOnCampaignUIHelperGetPartyHealthTooltip,
    IOnGameMenuPartyItemRefreshCounts,
    IOnPartyBaseHelperGetPartySizeText,
    IOnClanPartyItemUpdateProperties
{
    private readonly ITroopWeightService _troopWeightService;
    private readonly IModLogger _logger;

    // Localized labels, resolved once on first use (loc manager is ready at runtime, not at ctor).
    // Computed from the same TextObjects the engine uses, so matching is language-stable.
    private string _battleReadyStr;
    private string _woundedStr;
    private string _healingRateStr;
    private string _prisonersStr;
    private string _landCapacityStr;

    public TroopWeightDisplayHook(ITroopWeightService troopWeightService, IModLogger logger)
    {
        _troopWeightService = troopWeightService;
        _logger = logger;
    }

    private void EnsureLabels()
    {
        if (_battleReadyStr != null)
            return;

        _battleReadyStr = new TextObject("{=LVmkE2Ow}Battle Ready Troops").ToString();
        _woundedStr = new TextObject("{=TzLtVzdg}Wounded Troops").ToString();
        _healingRateStr = new TextObject("{=tf7301NC}Healing Rate").ToString();
        _prisonersStr = new TextObject("{=N6QTvjMf}Prisoners").ToString();
        _landCapacityStr = new TextObject("{=ZgYAGfbD}Land Troop Capacity").ToString();
    }

    public void OnGetMainPartyHealthTooltip(ref List<TooltipProperty> __result)
    {
        try
        {
            RewriteHealthTooltip(__result, MobileParty.MainParty?.Party);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] GetMainPartyHealthTooltip hook error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void OnGetPartyHealthTooltip(PartyBase party, ref List<TooltipProperty> __result)
    {
        try
        {
            RewriteHealthTooltip(__result, party);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] GetPartyHealthTooltip hook error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void OnGameMenuPartyItemRefreshCounts(GameMenuPartyItemVM partyItem)
    {
        try
        {
            var party = partyItem?.Party;
            if (party?.MemberRoster == null)
                return;

            var (healthy, wounded) = _troopWeightService.GetWeightedHealthAndWounded(party);
            // (0,0) on a non-empty roster means the weighted read failed — leave vanilla values.
            if (healthy == 0 && wounded == 0 && party.MemberRoster.Count > 0)
                return;

            var mobileParty = party.MobileParty;
            bool hidden = mobileParty != null && mobileParty.IsInfoHidden;

            // Vanilla's GameMenuPartyItemVM.PartyWoundedSize setter has a copy-paste bug: it guards
            // `if (value != _partySize)` (should be _partyWoundedSize), so it silently DROPS any
            // wounded value equal to the current PartySize. Nudge PartySize off a collision before
            // writing wounded, then settle PartySize. Verified against the v1.4.5 decompile; the VM is
            // sealed/engine-constructed so this is exercised in-game, not in unit tests.
            if (partyItem.PartySize == wounded)
                partyItem.PartySize = wounded + 1;
            partyItem.PartyWoundedSize = wounded;
            partyItem.PartySize = healthy;
            partyItem.PartySizeLbl = hidden ? "?" : healthy.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] GameMenuPartyItem.RefreshCounts hook error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void OnGetPartySizeText(PartyBase party, ref TextObject __result)
    {
        try
        {
            if (party?.MemberRoster == null)
                return;

            var (healthy, wounded) = _troopWeightService.GetWeightedHealthAndWounded(party);
            if (healthy == 0 && wounded == 0 && party.MemberRoster.Count > 0)
                return;

            if (wounded == 0)
            {
                // Mirrors vanilla's "no wounded" branch: a single number, no HEALTHY/WOUNDED split.
                __result = new TextObject(healthy.ToString());
                return;
            }

            MBTextManager.SetTextVariable("HEALTHY_NUM", healthy);
            MBTextManager.SetTextVariable("WOUNDED_NUM", wounded);
            __result = GameTexts.FindText("str_party_health");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] PartyBaseHelper.GetPartySizeText hook error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public void OnClanPartyItemUpdateProperties(ClanPartyItemVM partyItem)
    {
        try
        {
            var party = partyItem?.Party;
            if (party?.MobileParty == null || party.MemberRoster == null)
                return;

            int weightedTotal = (int)Math.Ceiling(_troopWeightService.CalculateWeightedMemberCount(party));
            // 0 weighted on a non-empty roster means the read failed — leave the vanilla text.
            if (weightedTotal == 0 && party.MemberRoster.Count > 0)
                return;

            // Mirror vanilla UpdateProperties' "X/limit" + "Party Size: X/limit" build, with the weighted
            // current substituted for the raw TotalManCount it used.
            GameTexts.SetVariable("LEFT", weightedTotal);
            GameTexts.SetVariable("RIGHT", party.PartySizeLimit);
            string sizeText = GameTexts.FindText("str_LEFT_over_RIGHT").ToString();
            partyItem.PartySizeText = sizeText;

            GameTexts.SetVariable("LEFT", GameTexts.FindText("str_party_morale_party_size").ToString());
            GameTexts.SetVariable("RIGHT", sizeText);
            partyItem.PartySizeSubTitleText = GameTexts.FindText("str_LEFT_colon_RIGHT").ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TroopWeight] ClanPartyItem.UpdateProperties hook error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Rewrites the "Battle Ready Troops" and "Wounded Troops" entries of a vanilla party-health
    /// tooltip with the weighted split. When weighted wounded is 0, strips the healing-rate block
    /// vanilla added under its phantom <c>(num &gt; 0)</c> branch so the tooltip matches a genuinely
    /// unwounded party. Fail-safe: if the block can't be confidently bounded, the (already corrected)
    /// wounded value still stands.
    /// </summary>
    private void RewriteHealthTooltip(List<TooltipProperty> list, PartyBase party)
    {
        if (list == null || list.Count == 0 || party?.MemberRoster == null)
            return;

        var (healthy, wounded) = _troopWeightService.GetWeightedHealthAndWounded(party);
        if (healthy == 0 && wounded == 0 && party.MemberRoster.Count > 0)
            return;

        EnsureLabels();

        int woundedIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            var prop = list[i];
            if (prop == null)
                continue;

            if (prop.DefinitionLabel == _battleReadyStr)
                prop.ValueLabel = healthy.ToString();
            else if (prop.DefinitionLabel == _woundedStr)
            {
                prop.ValueLabel = wounded.ToString();
                woundedIdx = i;
            }
            else if (prop.DefinitionLabel == _landCapacityStr)
            {
                // Capacity row (main-party tooltip only). Vanilla builds "current/limit" from the RAW
                // MemberRoster.TotalManCount, so it disagrees with the weighted header. Rewrite the
                // current to the weighted member total (healthy + wounded). Vanilla decided the red
                // "over capacity" color on the RAW compare, so a weighted-over / raw-under party shows
                // the over number without the red tint — cosmetic, acceptable for a display-only fix.
                var capText = GameTexts.FindText("str_LEFT_over_RIGHT_no_space");
                capText.SetTextVariable("LEFT", healthy + wounded).SetTextVariable("RIGHT", party.PartySizeLimit);
                prop.ValueLabel = capText.ToString();
            }
        }

        if (wounded != 0 || woundedIdx < 0)
            return;

        // Strip the spurious healing-rate block: everything from just after the Wounded entry up to
        // the next structural boundary (Prisoners or Land Troop Capacity, else end of list).
        // Assumption: the healing-rate entry + its separators/explanation form ONE contiguous block
        // between Wounded and that boundary (true for both GetMainPartyHealthTooltip and
        // GetPartyHealthTooltip in v1.4.5). Fail-safe: if no healing-rate entry is found in the range
        // (hasHealingEntry == false) we skip the removal entirely, leaving the already-corrected
        // "Wounded: 0" value intact — so a future vanilla layout change degrades to a cosmetic stale
        // line, never a crash or a wrong number. RemoveRange bounds are safe: boundary defaults to
        // list.Count and only shrinks to an index > woundedIdx, so the count is always >= 1.
        int boundary = list.Count;
        for (int i = woundedIdx + 1; i < list.Count; i++)
        {
            var lbl = list[i]?.DefinitionLabel;
            if (lbl == _prisonersStr || lbl == _landCapacityStr)
            {
                boundary = i;
                break;
            }
        }

        bool hasHealingEntry = false;
        for (int i = woundedIdx + 1; i < boundary; i++)
        {
            if (list[i]?.DefinitionLabel == _healingRateStr)
            {
                hasHealingEntry = true;
                break;
            }
        }

        if (hasHealingEntry)
        {
            // Preserve the NEXT section's leading spacer: vanilla emits a separator before "Prisoners"
            // and an empty line before "Land Troop Capacity" that belong to those sections, not the
            // healing block. Without this guard, stripping the healing block also swallows that spacer
            // (cosmetic — Codex review 2026-06-07, Suspect 2). Only applies when a real boundary was
            // found (boundary < list.Count); at end-of-list there is no following section to space.
            int removeEnd = boundary;
            if (boundary < list.Count && removeEnd - 1 > woundedIdx
                && string.IsNullOrEmpty(list[removeEnd - 1]?.DefinitionLabel))
                removeEnd--;

            if (removeEnd > woundedIdx + 1)
                list.RemoveRange(woundedIdx + 1, removeEnd - (woundedIdx + 1));
        }
    }
}
