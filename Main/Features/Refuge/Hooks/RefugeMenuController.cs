using System;
using System.Collections.Generic;
using Helpers;
using TAOM.Adapters;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.Refuge.Components;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Registers and drives the refuge menu, and inserts the two Refuge options into FieldCamp's
/// menus at their reserved index 4. Split out of <see cref="RefugeCampaignBehavior"/> so the
/// behavior stays a thin event fan-out (ADR-002); every decision is a delegation into
/// <see cref="IRefugeService"/> / <see cref="IWardenService"/>, engine screens open here at the
/// boundary.
///
/// <para>Menus register UNCONDITIONALLY (FiefHub lesson): the Enabled gate lives in
/// <see cref="IRefugeService.CanFound"/>, and a standing refuge stays enterable with the master
/// toggle off so a toggle never strands a garrison.</para>
/// </summary>
public sealed class RefugeMenuController
{
    private readonly IRefugeService _refuges;
    private readonly IWardenService _wardens;
    private readonly IRefugeSettingsProvider _settings;
    private readonly IGameMenuAdapter _menus;
    private readonly IEncounterAdapter _encounters;

    public RefugeMenuController(
        IRefugeService refuges,
        IWardenService wardens,
        IRefugeSettingsProvider settings,
        IGameMenuAdapter menus,
        IEncounterAdapter encounters)
    {
        _refuges = refuges;
        _wardens = wardens;
        _settings = settings;
        _menus = menus;
        _encounters = encounters;
    }

    public void AddMenus(CampaignGameStarter starter)
    {
        // ---- Insertions into FieldCamp's menus. INDEX 4 on both is reserved for Refuge; the
        // FieldCamp controller deliberately leaves it unassigned. ----

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_rf_found",
            "{=taom_rf_opt_found}Establish a refuge here",
            FoundCondition,
            args => FoundConsequence(),
            isLeave: false, index: 4);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_rf_enter",
            "{=taom_rf_opt_enter}Enter refuge",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return _refuges.NearestManageable() != null;
            },
            args => _menus.SwitchTo(RefugeCampaignBehavior.MenuId),
            isLeave: false, index: 4);

        // ---- The refuge menu itself. ----

        starter.AddGameMenu(
            RefugeCampaignBehavior.MenuId,
            "{=!}{TAOM_RF_STATUS}",
            new OnInitDelegate(OnMenuInit),
            GameMenu.MenuOverlayType.None);

        starter.AddGameMenuOption(RefugeCampaignBehavior.MenuId, "taom_rf_manage",
            "{=taom_rf_opt_manage}Manage garrison (troops and prisoners)",
            args => InReachCondition(args, GameMenuOption.LeaveType.Manage),
            args =>
            {
                var party = NearestParty();
                if (party != null)
                    PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners(party);
            },
            isLeave: false, index: 0);

        starter.AddGameMenuOption(RefugeCampaignBehavior.MenuId, "taom_rf_items",
            "{=taom_rf_opt_items}Store goods",
            args => InReachCondition(args, GameMenuOption.LeaveType.Trade),
            args =>
            {
                var party = NearestParty();
                if (party?.ItemRoster != null)
                    InventoryScreenHelper.OpenScreenAsStash(party.ItemRoster);
            },
            isLeave: false, index: 1);

        starter.AddGameMenuOption(RefugeCampaignBehavior.MenuId, "taom_rf_upgrade",
            "{=taom_rf_opt_upgrade}Upgrade to stronghold",
            UpgradeCondition,
            args => UpgradeConsequence(),
            isLeave: false, index: 2);

        starter.AddGameMenuOption(RefugeCampaignBehavior.MenuId, "taom_rf_break",
            "{=taom_rf_opt_break}Dismantle refuge",
            args => InReachCondition(args, GameMenuOption.LeaveType.Leave),
            args =>
            {
                var refuge = _refuges.NearestManageable();
                if (refuge == null)
                    return;
                _refuges.Dismantle(refuge);
                CloseMenu();
            },
            isLeave: false, index: 3);

        starter.AddGameMenuOption(RefugeCampaignBehavior.MenuId, "taom_rf_leave",
            "{=taom_rf_opt_leave}Leave",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            args => CloseMenu(),
            isLeave: true, index: 4);
    }

    private void OnMenuInit(MenuCallbackArgs args)
    {
        var refuge = _refuges.NearestManageable();
        var party = refuge != null ? ResolveParty(refuge.PartyId) : null;
        TextObject status;
        if (party == null)
        {
            // Reachable when the player rides out of manage range with the menu still open.
            status = new TextObject("{=taom_rf_menu_gone}No refuge stands within reach.");
        }
        else
        {
            var warden = (party.PartyComponent as RefugePartyComponent)?.Warden;
            if (warden != null)
            {
                status = new TextObject(
                    "{=taom_rf_menu_status_warden}Your {TIER} stands here - garrison: {COUNT}, warden: {WARDEN}.");
                status.SetTextVariable("WARDEN", warden.Name);
            }
            else
            {
                status = new TextObject(
                    "{=taom_rf_menu_status}Your {TIER} stands here - garrison: {COUNT}.");
            }
            status.SetTextVariable("TIER", TierLabel(refuge!.TierEnum));
            status.SetTextVariable("COUNT", party.MemberRoster?.TotalManCount ?? 0);
        }
        MBTextManager.SetTextVariable("TAOM_RF_STATUS", status, false);
    }

    // ---- Found (from the camp manage sub-menu) ----

    private bool FoundCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Manage;
        var reason = _refuges.CanFound();
        if (reason != RefugeBlockReason.None)
            return Disabled(args, ReasonText(reason, _settings.FoundCost));

        args.Tooltip = new TextObject(
                "{=taom_rf_found_tooltip}Cost: {GOLD} denars. Choose a warden (a companion, or a soldier promoted to officer), then garrison it.")
            .SetTextVariable("GOLD", _settings.FoundCost);
        return true;
    }

    private void FoundConsequence()
    {
        var candidates = _wardens.Candidates();
        if (candidates.Count == 0)
        {
            Info(new TextObject("{=taom_rf_no_warden}You need a companion - or a soldier you can promote - to lead a refuge."));
            return;
        }

        var elements = new List<InquiryElement>(candidates.Count);
        foreach (var candidate in candidates)
        {
            string title = candidate.IsCompanion
                ? candidate.DisplayName
                : new TextObject("{=taom_rf_promote_entry}{NAME} (promote to officer)")
                    .SetTextVariable("NAME", candidate.DisplayName).ToString();
            elements.Add(new InquiryElement(candidate, title, null));
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: new TextObject("{=taom_rf_warden_title}Assign a Warden").ToString(),
            descriptionText: new TextObject(
                "{=taom_rf_warden_desc}Choose who commands the refuge - a companion, or promote one of your soldiers into an officer. They leave your party to lead it.").ToString(),
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_rf_confirm}Confirm").ToString(),
            negativeText: new TextObject("{=taom_rf_cancel}Cancel").ToString(),
            affirmativeAction: OnWardenChosen,
            negativeAction: _ => { }));
    }

    private void OnWardenChosen(List<InquiryElement> selected)
    {
        if (selected == null || selected.Count == 0 || !(selected[0].Identifier is WardenCandidate candidate))
            return;

        // Re-check the gate before resolving: ResolveWarden may PROMOTE a soldier into a
        // companion, and founding must not be able to fail (gold spent elsewhere while the picker
        // was open) after that irreversible step. The source promoted first and could strand the
        // new officer when DoFound bailed.
        var precheck = _refuges.CanFound();
        if (precheck != RefugeBlockReason.None)
        {
            Warn(ReasonText(precheck, _settings.FoundCost));
            return;
        }

        var wardenHeroId = _wardens.ResolveWarden(candidate, out bool promoted, out string promotedFromTroopId);
        if (wardenHeroId == null)
        {
            Warn(new TextObject("{=taom_rf_promote_failed}Could not assign that warden."));
            return;
        }

        var refuge = _refuges.Found(wardenHeroId, out var reason);
        if (refuge == null)
        {
            Warn(ReasonText(reason, _settings.FoundCost));
            return;
        }

        refuge.WardenPromoted = promoted;
        refuge.PromotedFromTroopId = promotedFromTroopId;

        Info(new TextObject("{=taom_rf_founded}Refuge founded - garrison it, then it will be raised."));

        // Deposit screen straight after founding (source flow). The service already started the
        // raise; the party screen pauses campaign time, so the build clock does not run while the
        // player deposits - behaviourally identical to the source's build-starts-on-close
        // callback without needing one.
        var party = ResolveParty(refuge.PartyId);
        if (party != null)
            PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners(party);
    }

    // ---- Upgrade ----

    private bool UpgradeCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Manage;
        var refuge = _refuges.NearestManageable();
        if (refuge == null)
            return Disabled(args, ReasonText(RefugeBlockReason.NoRefugeInReach, 0));

        var reason = _refuges.CanUpgrade(refuge);
        if (reason != RefugeBlockReason.None)
            return Disabled(args, ReasonText(reason, _settings.StrongholdUpgradeCost));

        args.Tooltip = new TextObject(
                "{=taom_rf_upgrade_tooltip}Cost: {GOLD} denars. Your company stays while it is rebuilt into a stronghold.")
            .SetTextVariable("GOLD", _settings.StrongholdUpgradeCost);
        return true;
    }

    private void UpgradeConsequence()
    {
        var refuge = _refuges.NearestManageable();
        if (refuge == null)
            return;

        if (!_refuges.Upgrade(refuge))
        {
            // The condition showed the option enabled, so the only runtime failure left is gold.
            Warn(new TextObject("{=taom_rf_upgrade_no_gold}Not enough gold to rebuild the refuge into a stronghold."));
            return;
        }

        Info(new TextObject("{=taom_rf_upgrading}Rebuilding the refuge into a stronghold - your company must stay until it is done."));
        // Leave the menu so the map hold-nearby rule (service FrameTick) takes over, as in the
        // source, which finished the encounter here for the same reason.
        CloseMenu();
    }

    // ---- Shared plumbing ----

    private bool InReachCondition(MenuCallbackArgs args, GameMenuOption.LeaveType leaveType)
    {
        args.optionLeaveType = leaveType;
        if (_refuges.NearestManageable() != null)
            return true;
        return Disabled(args, ReasonText(RefugeBlockReason.NoRefugeInReach, 0));
    }

    private void CloseMenu()
    {
        // Inside a refuge encounter (the DoMeeting intercept) the encounter must be finished or
        // the menu re-opens on the next meeting tick; outside one, plain menu exit. A refuge is
        // never a settlement, so the player cannot be held inside anything: false matches the
        // source and avoids a spurious LeaveSettlement.
        if (_encounters.HasCurrent)
            _encounters.Finish(forcePlayerOutFromSettlement: false);
        else
            _menus.ExitToLast();
    }

    private MobileParty? NearestParty()
    {
        var refuge = _refuges.NearestManageable();
        return refuge == null ? null : ResolveParty(refuge.PartyId);
    }

    private static MobileParty? ResolveParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return null;
        try
        {
            foreach (var party in MobileParty.All)
            {
                if (party != null && string.Equals(party.StringId, partyId, StringComparison.Ordinal))
                    return party;
            }
        }
        catch
        {
            // Campaign mid-teardown; the option consequence just no-ops.
        }
        return null;
    }

    private TextObject ReasonText(RefugeBlockReason reason, int cost)
    {
        switch (reason)
        {
            case RefugeBlockReason.FeatureDisabled:
                return new TextObject("{=taom_rf_reason_disabled}Refuges are disabled in the mod options.");
            case RefugeBlockReason.NoReadyCampHere:
                return new TextObject("{=taom_rf_reason_no_camp}Establish a camp here first, then raise a refuge from it.");
            case RefugeBlockReason.WrongCampType:
                return new TextObject("{=taom_rf_reason_camp_type}Only a field or fortified camp can be raised into a refuge.");
            case RefugeBlockReason.AtRefugeLimit:
                return new TextObject("{=taom_rf_reason_limit}Your clan already holds the most refuges it can support ({LIMIT}).")
                    .SetTextVariable("LIMIT", CurrentRefugeLimit());
            case RefugeBlockReason.TooCloseToTown:
                return new TextObject("{=taom_rf_reason_town}Too close to a town or castle - raise a refuge farther from settlements.");
            case RefugeBlockReason.NoWardenAvailable:
                return new TextObject("{=taom_rf_no_warden}You need a companion - or a soldier you can promote - to lead a refuge.");
            case RefugeBlockReason.NotEnoughGold:
                return new TextObject("{=taom_rf_reason_gold}You cannot afford this - it costs {GOLD} denars.")
                    .SetTextVariable("GOLD", cost);
            case RefugeBlockReason.NoRefugeInReach:
                return new TextObject("{=taom_rf_reason_no_refuge}No refuge within reach.");
            case RefugeBlockReason.StillBuilding:
                return new TextObject("{=taom_rf_reason_building}The refuge is still being raised.");
            case RefugeBlockReason.AlreadyTopTier:
                return new TextObject("{=taom_rf_reason_top_tier}Already a stronghold - the highest tier.");
            case RefugeBlockReason.RefugeAlreadyHere:
                return new TextObject("{=taom_rf_reason_already_here}A refuge already stands here - manage it instead.");
            case RefugeBlockReason.Enlisted:
                return new TextObject("{=taom_rf_reason_enlisted}You cannot raise a refuge while serving in another lord's army.");
            default:
                return new TextObject("{=taom_rf_reason_blocked}You cannot do that here.");
        }
    }

    private int CurrentRefugeLimit()
    {
        try
        {
            return _refuges.RefugeLimit(Clan.PlayerClan?.Tier ?? 0);
        }
        catch
        {
            return _settings.MaxRefugesCap;
        }
    }

    private static TextObject TierLabel(RefugeTier tier)
        => tier == RefugeTier.Stronghold
            ? new TextObject("{=taom_rf_tier_stronghold}stronghold")
            : new TextObject("{=taom_rf_tier_refuge}refuge");

    private static bool Disabled(MenuCallbackArgs args, TextObject reason)
    {
        args.IsEnabled = false;
        args.Tooltip = reason;
        return true;
    }

    private static void Info(TextObject text)
        => InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));

    private static void Warn(TextObject text)
        => InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
}
