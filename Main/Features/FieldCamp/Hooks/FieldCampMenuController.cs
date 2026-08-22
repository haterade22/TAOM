using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.UI;
using TAOM.Features.SupplyLines;

namespace TAOM.Features.FieldCamp.Hooks;

/// <summary>
/// Registers and drives the two camp menus (base: choose a camp type; sub: manage the standing
/// camp). Split out of <see cref="FieldCampCampaignBehavior"/> so the behavior stays a thin event
/// fan-out (ADR-002); every decision here is a one-line delegation into <see cref="ICampService"/>.
///
/// <para>Menus are registered UNCONDITIONALLY (FiefHub lesson, Codex review #36): the runtime
/// Enabled gate lives at the overlay button and in <see cref="ICampService.CanEstablish"/>, so an
/// MCM toggle mid-session works without hitting an unregistered menu.</para>
/// </summary>
public sealed class FieldCampMenuController
{
    private readonly ICampService _camps;
    private readonly ICampSettingsProvider _settings;
    private readonly ISupplyLinesSettingsProvider _supplySettings;
    private readonly IGameMenuAdapter _menus;

    // ResolveMany seam (Refuge in Phase 3). Resolved lazily at the boundary because the behavior's
    // SubModule ctor signature is pinned without contributors.
    private IReadOnlyList<ICampOverlayContributor>? _contributors;

    public FieldCampMenuController(
        ICampService camps,
        ICampSettingsProvider settings,
        ISupplyLinesSettingsProvider supplySettings,
        IGameMenuAdapter menus)
    {
        _camps = camps;
        _settings = settings;
        _supplySettings = supplySettings;
        _menus = menus;
    }

    public void AddMenus(CampaignGameStarter starter)
    {
        starter.AddGameMenu(
            FieldCampCampaignBehavior.BaseMenuId,
            "{=taom_fc_menu_base}Your party makes camp.{NEWLINE}{TAOM_FC_STATUS}",
            new OnInitDelegate(OnMenuInit),
            GameMenu.MenuOverlayType.None);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_fc_ambush",
            "{=taom_fc_opt_ambush}Set up an ambush",
            args => EstablishCondition(args, CampType.Ambush),
            args => Establish(CampType.Ambush),
            isLeave: false, index: 0);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_fc_lookout",
            "{=taom_fc_opt_lookout}Create a lookout",
            args => EstablishCondition(args, CampType.Lookout),
            args => Establish(CampType.Lookout),
            isLeave: false, index: 1);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_fc_field",
            "{=taom_fc_opt_field}Establish a field camp",
            args => EstablishCondition(args, CampType.Field),
            args => Establish(CampType.Field),
            isLeave: false, index: 2);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_fc_manage",
            "{=taom_fc_opt_manage}Manage camp",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                return _camps.PlayerCamp != null;
            },
            args => _menus.SwitchTo(FieldCampCampaignBehavior.CampSubMenuId),
            isLeave: false, index: 3);

        // Index 4 on BOTH menus is deliberately unassigned: the Refuge feature (Phase 3) inserts
        // its options there. Do not fill it.

        starter.AddGameMenuOption(FieldCampCampaignBehavior.BaseMenuId, "taom_fc_leave",
            "{=taom_fc_opt_continue}Continue",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            args => _menus.ExitToLast(),
            isLeave: true, index: 5);

        starter.AddGameMenu(
            FieldCampCampaignBehavior.CampSubMenuId,
            "{=!}{TAOM_FC_STATUS}",
            new OnInitDelegate(OnMenuInit),
            GameMenu.MenuOverlayType.None);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_fc_forage",
            "{=!}{TAOM_FC_FORAGE}",
            ForageCondition,
            args =>
            {
                _camps.ToggleForaging();
                // Re-enter the menu so the toggled TAOM_FC_FORAGE / status text re-renders.
                _menus.SwitchTo(FieldCampCampaignBehavior.CampSubMenuId);
            },
            isLeave: false, index: 0);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_fc_fortify",
            "{=taom_fc_opt_fortify}Upgrade to fortified camp",
            FortifyCondition,
            args =>
            {
                if (_camps.Fortify())
                {
                    _menus.ExitToLast();
                    return;
                }
                // The condition showed the option enabled, so the only runtime failure left is gold.
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=taom_fc_no_gold}Not enough gold to fortify the camp.").ToString(),
                    Colors.Red));
            },
            isLeave: false, index: 1);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_fc_supplies",
            "{=taom_fc_opt_supplies}Order supplies",
            SuppliesCondition,
            args => SupplyLines.UI.SupplyOrderScreens.Open(),
            isLeave: false, index: 2);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_fc_break",
            "{=taom_fc_opt_break}Break camp",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                if (_camps.PlayerCamp != null)
                    return true;
                return Disabled(args, new TextObject("{=taom_fc_need_camp}Establish a camp first."));
            },
            args =>
            {
                _camps.BreakPlayerCamp();
                _menus.SwitchTo(FieldCampCampaignBehavior.BaseMenuId);
            },
            isLeave: false, index: 3);

        starter.AddGameMenuOption(FieldCampCampaignBehavior.CampSubMenuId, "taom_fc_camp_leave",
            "{=taom_fc_opt_leave}Leave",
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            args => _menus.ExitToLast(),
            isLeave: true, index: 5);
    }

    private void OnMenuInit(MenuCallbackArgs args)
    {
        RefreshStatusVariables();
    }

    /// <summary>Both menu bodies render from these two text variables (source module pattern);
    /// re-set on every menu init so the raise percent and forage toggle stay current.</summary>
    private void RefreshStatusVariables()
    {
        var camp = _camps.PlayerCamp;
        TextObject status;
        if (camp == null)
        {
            status = new TextObject("{=taom_fc_status_choose}Choose how to make camp here.");
        }
        else if (!camp.IsReady)
        {
            status = new TextObject(
                "{=taom_fc_menu_raising}{RAISING} {PROGRESS}% (its effects start once set up)");
            status.SetTextVariable("RAISING", FieldCampTexts.RaisingLabel(camp.TypeEnum));
            status.SetTextVariable("PROGRESS", (int)(ClampProgress(camp.BuildProgress()) * 100f));
        }
        else if (camp.Foraging)
        {
            status = new TextObject("{=taom_fc_menu_active_forage}Active: {CAMP} (foraging for supplies)");
            status.SetTextVariable("CAMP", FieldCampTexts.TypeLabel(camp.TypeEnum));
        }
        else
        {
            status = new TextObject("{=taom_fc_menu_active}Active: {CAMP}");
            status.SetTextVariable("CAMP", FieldCampTexts.TypeLabel(camp.TypeEnum));
        }

        MBTextManager.SetTextVariable("TAOM_FC_STATUS", status, false);
        MBTextManager.SetTextVariable("TAOM_FC_FORAGE", camp?.Foraging == true
            ? new TextObject("{=taom_fc_forage_stop}Stop foraging")
            : new TextObject("{=taom_fc_forage_start}Forage for supplies"), false);
    }

    private void Establish(CampType type)
    {
        if (_camps.Establish(type))
            _menus.SwitchTo(FieldCampCampaignBehavior.CampSubMenuId);
    }

    private bool EstablishCondition(MenuCallbackArgs args, CampType type)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Wait;
        // Camped already: the establish options vanish and Manage takes over (source behavior).
        if (_camps.PlayerCamp != null)
            return false;

        var reason = _camps.CanEstablish(type);
        if (reason == CampBlockReason.None)
            return true;
        return Disabled(args, ReasonText(reason, type));
    }

    private bool ForageCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
        var camp = _camps.PlayerCamp;
        if (camp == null)
            return Disabled(args, new TextObject("{=taom_fc_need_field}Establish a field camp first."));
        if (camp.TypeEnum != CampType.Field && camp.TypeEnum != CampType.Fortified)
            return Disabled(args, new TextObject("{=taom_fc_forage_field_only}Only a field or fortified camp can forage."));
        if (!camp.IsReady)
            return Disabled(args, new TextObject("{=taom_fc_building}The camp is still being set up."));
        return true;
    }

    private bool FortifyCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Manage;
        var camp = _camps.PlayerCamp;
        if (camp == null)
            return Disabled(args, new TextObject("{=taom_fc_need_field}Establish a field camp first."));
        if (camp.TypeEnum != CampType.Field)
            return Disabled(args, new TextObject("{=taom_fc_fortify_field_only}Only a field camp can be fortified."));
        if (!camp.IsReady)
            return Disabled(args, new TextObject("{=taom_fc_building}The camp is still being set up."));

        args.Tooltip = new TextObject("{=taom_fc_fortify_cost}Cost: {GOLD}{GOLD_ICON}")
            .SetTextVariable("GOLD", _settings.FortifiedUpgradeCost);
        return true;
    }

    private bool SuppliesCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
        // Phase 1 seam is DIRECT (no reflection bridge): the SupplyLines feature ships in the same
        // assembly, so the only gate is its own master toggle.
        if (!_supplySettings.Enabled)
            return Disabled(args, new TextObject("{=taom_fc_supplies_needs}Supply lines are disabled."));
        var camp = _camps.PlayerCamp;
        if (camp == null)
            return Disabled(args, new TextObject("{=taom_fc_need_camp}Establish a camp first."));
        if (camp.TypeEnum == CampType.Ambush || camp.TypeEnum == CampType.Lookout)
            return Disabled(args, new TextObject("{=taom_fc_supplies_stealth}Ambush and lookout camps stay hidden - no supply convoys."));
        if (!camp.IsReady)
            return Disabled(args, new TextObject("{=taom_fc_building}The camp is still being set up."));
        return true;
    }

    private TextObject ReasonText(CampBlockReason reason, CampType type)
    {
        switch (reason)
        {
            case CampBlockReason.FeatureDisabled:
                return new TextObject("{=taom_fc_reason_disabled}Field camps are disabled in the mod options.");
            case CampBlockReason.Moving:
                return new TextObject("{=taom_fc_reason_moving}Halt your party before making camp.");
            case CampBlockReason.InSettlement:
                return new TextObject("{=taom_fc_reason_settlement}You cannot pitch camp inside a settlement.");
            case CampBlockReason.TooCloseToTown:
                return new TextObject("{=taom_fc_too_close}Too close to a town or castle - pitch a field camp farther from settlements.");
            case CampBlockReason.TerrainUnsuitable:
                return type == CampType.Ambush
                    ? new TextObject("{=taom_fc_ambush_terrain}Needs concealing terrain - forest, hills, swamp, canyon, dunes, a bridge or a ford.")
                    : type == CampType.Lookout
                        ? new TextObject("{=taom_fc_lookout_terrain}Needs high ground or open vantage - mountains, hills, forest, steppe or plains.")
                        : new TextObject("{=taom_fc_reason_terrain}The ground here does not suit this camp.");
            case CampBlockReason.Enlisted:
                return new TextObject("{=taom_fc_reason_enlisted}You cannot make camp while serving in another lord's army.");
            case CampBlockReason.External:
                var external = FirstContributorBlockReason();
                // Contributor reasons arrive as finished player-facing strings; {=!} marks the
                // wrapper as not-for-translation.
                return external != null
                    ? new TextObject("{=!}" + external)
                    : new TextObject("{=taom_fc_reason_blocked}You cannot make camp here.");
            default:
                return new TextObject("{=taom_fc_reason_blocked}You cannot make camp here.");
        }
    }

    private string? FirstContributorBlockReason()
    {
        _contributors ??= IoC.ResolveAll<ICampOverlayContributor>().ToList();
        foreach (var contributor in _contributors)
        {
            try
            {
                var reason = contributor.CreationBlockedReason();
                if (!string.IsNullOrEmpty(reason))
                    return reason;
            }
            catch
            {
                // A faulty contributor must not take the menu down; fall through to the generic text.
            }
        }
        return null;
    }

    private static bool Disabled(MenuCallbackArgs args, TextObject reason)
    {
        args.IsEnabled = false;
        args.Tooltip = reason;
        return true;
    }

    private static float ClampProgress(float value)
    {
        if (!(value > 0f))
            return 0f;
        return value > 1f ? 1f : value;
    }
}
