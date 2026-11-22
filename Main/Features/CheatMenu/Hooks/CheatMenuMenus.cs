using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>
/// The cheat menu's pages (docs/features/cheat-menu.md): three game menus whose buttons call <see cref="CheatMenuService"/>,
/// the same code as the <c>taom.cheat_*</c> console commands. Registered unconditionally at session launch, so a switch
/// flipped mid-session works; every button but Close hides while the menu is not available (switch off, or co-op).
/// </summary>
internal static class CheatMenuMenus
{
    internal const int GoldStep = 10_000;
    internal const int ResourceStep = 1_000;
    internal const int InfluenceStep = 100;
    internal const int RenownStep = 100;
    internal const int TroopStep = 10;
    internal const int RelationStep = 20;

    internal static void Register(CampaignGameStarter starter, CheatMenuService service, ICheatMenuSettingsProvider settings,
        ICheatWorldAdapter world)
    {
        bool Shown() => CheatMenuPolicy.IsMenuAvailable(settings.EnableCheatMenu, service.IsSinglePlayer);

        starter.AddGameMenu(CheatMenuIds.Root,
            "{=taom_cheat_menu_text}TAOM cheat menu. Cheats change your save, and most cannot be undone.",
            null, GameMenu.MenuOverlayType.None);
        var i = 0;
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_teleport", "{=taom_cheat_opt_teleport}Teleport to a settlement...", i++,
            Shown, () => GameMenu.SwitchToMenu(CheatMenuIds.Teleport));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_gold", "{=taom_cheat_opt_gold}Gold: +10,000 denars", i++,
            Shown, () => CheatMenuTexts.Show(service.GiveGold(GoldStep)));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_resource", "{=taom_cheat_opt_resource}Special resource: +1,000", i++,
            Shown, () => CheatMenuTexts.Show(service.AddSpecialResource(ResourceStep)));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_influence", "{=taom_cheat_opt_influence}Influence: +100", i++,
            Shown, () => CheatMenuTexts.Show(service.AddInfluence(InfluenceStep)));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_renown", "{=taom_cheat_opt_renown}Renown: +100", i++,
            Shown, () => CheatMenuTexts.Show(service.AddRenown(RenownStep)));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_troops", "{=taom_cheat_opt_troops}Troops...", i++,
            Shown, () => GameMenu.SwitchToMenu(CheatMenuIds.Troops));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_relation", "{=taom_cheat_opt_relation}Relations: +20 with every lord of a kingdom...", i++,
            Shown, () => Pick("taom_cheat_opt_relation", "{=taom_cheat_pick_kingdom}Choose a kingdom", world.Kingdoms(),
                id => CheatMenuTexts.Show(service.AddRelation(id, RelationStep))));
        Option(starter, CheatMenuIds.Root, "taom_cheat_opt_heal", "{=taom_cheat_opt_heal}Heal your party and your heroes", i++,
            Shown, () => CheatMenuTexts.Show(service.Heal()));
        UnlimitedPartyOption(starter, service, Shown, i++);
        Leave(starter, CheatMenuIds.Root, "taom_cheat_opt_close", "{=taom_cheat_opt_close}Close", i, GameMenu.ExitToLast);

        starter.AddGameMenu(CheatMenuIds.Teleport,
            "{=taom_cheat_teleport_text}Teleport to a settlement's gate. You arrive outside and enter as usual.",
            null, GameMenu.MenuOverlayType.None);
        i = 0;
        foreach (var (kind, id, label) in new[]
        {
            (CheatSettlementKind.Town, "taom_cheat_tp_towns", "{=taom_cheat_tp_towns}Towns..."),
            (CheatSettlementKind.Castle, "taom_cheat_tp_castles", "{=taom_cheat_tp_castles}Castles..."),
            (CheatSettlementKind.Village, "taom_cheat_tp_villages", "{=taom_cheat_tp_villages}Villages..."),
        })
        {
            var k = kind;
            Option(starter, CheatMenuIds.Teleport, id, label, i++, Shown,
                () => Pick(id, "{=taom_cheat_pick_settlement}Choose where to teleport", world.Settlements(k), TeleportTo(service)));
        }
        Leave(starter, CheatMenuIds.Teleport, "taom_cheat_tp_back", "{=taom_cheat_opt_back}Back", i,
            () => GameMenu.SwitchToMenu(CheatMenuIds.Root));

        starter.AddGameMenu(CheatMenuIds.Troops,
            "{=taom_cheat_troops_text}Add recruits of your culture to your party.",
            null, GameMenu.MenuOverlayType.None);
        Option(starter, CheatMenuIds.Troops, "taom_cheat_troops_basic", "{=taom_cheat_troops_basic}+10 basic recruits", 0,
            Shown, () => CheatMenuTexts.Show(service.AddTroops(elite: false, TroopStep)));
        Option(starter, CheatMenuIds.Troops, "taom_cheat_troops_elite", "{=taom_cheat_troops_elite}+10 elite recruits", 1,
            Shown, () => CheatMenuTexts.Show(service.AddTroops(elite: true, TroopStep)));
        Option(starter, CheatMenuIds.Troops, "taom_cheat_troops_any", "{=taom_cheat_troops_any}+10 of any troop of your faction...", 2,
            Shown, () => PickFactionTroop(service));
        Option(starter, CheatMenuIds.Troops, "taom_cheat_troops_screen", "{=taom_cheat_troops_screen}Every troop (party screen)...", 3,
            Shown, () =>
            {
                var outcome = service.OpenCheatPartyScreen();
                if (!outcome.Ok) CheatMenuTexts.Show(outcome);
            });
        Leave(starter, CheatMenuIds.Troops, "taom_cheat_troops_back", "{=taom_cheat_opt_back}Back", 4,
            () => GameMenu.SwitchToMenu(CheatMenuIds.Root));
    }

    // Every regular troop of the faction, lowest tier first (CheatTroopList); the list's search box finds one by name.
    private static void PickFactionTroop(CheatMenuService service)
    {
        var troops = service.FactionTroops();
        if (troops.Count == 0)
        {
            CheatMenuTexts.Show(new CheatOutcome(CheatOutcomeKind.NothingToDo));
            return;
        }
        Pick("taom_cheat_troops_any", "{=taom_cheat_pick_troop}Choose a troop: 10 of it join your party",
            troops.Select(t => (t.Id, TroopLabel(t))).ToList(),
            id => CheatMenuTexts.Show(service.AddFactionTroop(id, TroopStep)));
    }

    internal static string TroopLabel(CheatTroopFacts t) =>
        new TextObject("{=taom_cheat_troop_row}Tier {TIER}: {NAME}")
            .SetTextVariable("TIER", t.Tier)
            .SetTextVariable("NAME", t.Name)
            .ToString();

    // The menu closes BEFORE the party moves: case 12 crashed on a settlement menu left open behind a teleport.
    internal static Action<string> TeleportTo(CheatMenuService service) => id =>
    {
        GameMenu.ExitToLast();
        CheatMenuTexts.Show(service.Teleport(id));
    };

    private static void Option(CampaignGameStarter starter, string menu, string id, string text, int index, Func<bool> shown, Action act) =>
        starter.AddGameMenuOption(menu, id, text,
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Continue; return shown(); },
            _ => Guarded(id, act), isLeave: false, index: index);

    // A cheat that throws shows a warning and is logged, instead of escaping the menu click (Bannerlord Cheats' lesson:
    // a cheat that fails must not take the campaign down).
    internal static void Guarded(string id, Action act)
    {
        try { act(); }
        catch (Exception ex)
        {
            try
            {
                IoC.Resolve<TAOM.Core.Logging.IModLogger>().LogError($"[CheatMenu] {id} failed: {ex}");
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    new TextObject("{=taom_cheat_failed}That cheat failed. The TAOM log has the details.").ToString(),
                    TaleWorlds.Library.Colors.Red));
            }
            catch (Exception)
            {
                // swallow-ok: the warning itself must not be what crashes the menu
            }
        }
    }

    // "Unlimited party: off / on" (CheatPartyRules). The label shows the current state; the hint says both things it does. A
    // click flips it, says what changed, and redraws the page so the label follows.
    private static void UnlimitedPartyOption(CampaignGameStarter starter, CheatMenuService service, Func<bool> shown, int index) =>
        starter.AddGameMenuOption(CheatMenuIds.Root, "taom_cheat_opt_unlimited_party",
            "{=taom_cheat_opt_unlimited_party}Unlimited party: {STATE}",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                args.Text.SetTextVariable("STATE", new TextObject(service.UnlimitedParty
                    ? "{=taom_cheat_state_on}on" : "{=taom_cheat_state_off}off"));
                args.Tooltip = new TextObject("{=taom_cheat_unlimited_party_hint}While on, your party has no troop limit, and troops of enemy or opposite-alignment cultures in it do not desert because of their culture. Turning it off keeps every troop, but the usual rules apply again: men over the troop limit start to desert, morale drops and the party is slower.");
                return shown();
            },
            _ => Guarded("taom_cheat_opt_unlimited_party", () =>
            {
                var outcome = service.SetUnlimitedParty(!service.UnlimitedParty);
                if (!outcome.Ok)
                {
                    CheatMenuTexts.Show(outcome);
                    return;
                }
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(new TextObject(service.UnlimitedParty
                    ? "{=taom_cheat_unlimited_party_on}Unlimited party is on: no size limit, and enemy troops in your party do not desert because of their culture."
                    : "{=taom_cheat_unlimited_party_off}Unlimited party is off: the size limit and desertion apply again.").ToString()));
                GameMenu.SwitchToMenu(CheatMenuIds.Root);
            }),
            isLeave: false, index: index);

    private static void Leave(CampaignGameStarter starter, string menu, string id, string text, int index, Action act) =>
        starter.AddGameMenuOption(menu, id, text,
            args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
            _ => act(), isLeave: true, index: index);

    private static void Pick(string optionId, string title, IReadOnlyList<(string Id, string Name)> choices, Action<string> onPicked)
    {
        var elements = choices.Select(c => new InquiryElement(c.Id, c.Name, null)).ToList();
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: new TextObject(title).ToString(),
            descriptionText: string.Empty,
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_cheat_pick_ok}Choose").ToString(),
            negativeText: new TextObject("{=taom_cheat_pick_cancel}Cancel").ToString(),
            affirmativeAction: chosen => Guarded(optionId, () =>
            {
                if (chosen != null && chosen.Count > 0 && chosen[0].Identifier is string id)
                    onPicked(id);
            }),
            negativeAction: _ => { },
            isSeachAvailable: true), pauseGameActiveState: true);
    }
}
