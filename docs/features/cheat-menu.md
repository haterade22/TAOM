# Cheat Menu

## Overview

An optional menu of cheats for single player. The MCM switch **Enable Cheat Menu** (TAOM settings, group Cheat Menu, off
by default) adds a **Cheats** button to the campaign map's bottom bar, after the Fiefs button. The button opens three game
menus:

| Page | Buttons |
|---|---|
| TAOM cheat menu | Teleport to a settlement..., Gold +10,000 denars, Special resource +1,000, Influence +100, Renown +100, Troops..., Relations +20 with every lord of a kingdom..., Heal your party and your heroes, Close |
| Teleport | Towns..., Castles..., Villages... (each opens a searchable list), Back |
| Troops | +10 basic recruits, +10 elite recruits (the culture's `basic_troop` and `elite_basic_troop`), Back |

Every button calls `CheatMenuService`, the same code as the console commands below, and shows its result as a message.

## Map key

**H** on the campaign map opens the same menu as the button. Rebind it under Options > Keybindings > Campaign Map, row
"Open Cheat Menu (TAOM)" (`TaomCheatMenuHotKeyCategory`, game key id 550, after every other TAOM key id; its name rows are
in `global_strings.xml`).

- `CheatMenuMapView` (TAOM.Client) asks `CheatMenuOpener.TryOpenFromHotKey` every map frame. With the switch off or outside
  single player it returns at once and the key is not read. Otherwise a press goes through `TryOpen`, the button's own
  gate, so the key opens nothing while a menu, an encounter or a screen holds the map.
- A press with Ctrl or Alt held does not count.
- Why H: free on the campaign map (`MapHotKeyCategory` and `GenericCampaignPanelsGameKeyCategory` do not bind it, no TAOM
  key uses it). In battle vanilla binds H only in `CheatsHotKeyCategory` (Ctrl combinations in game cheat mode), and
  DualWield uses N. The RTS Camera and Command System defaults could not be checked, so the key is rebindable.

## Why it exists

Players used the third-party mod TAOM_CheatPanel. Its teleport crashed a game (player case 12, 14 Nov 2026):
- It moved the party out of a village and returned to the village menu, so the menu and the encounter stayed open.
- The next click on "Wait here" ran `LeaveSettlementAction` on a party that was no longer in a settlement, and it threw.

## Teleport

`CheatMenuPolicy.CheckTeleport` moves only a party that stands free on the land map. Its refusals are checked in this order:

1. no campaign;
2. a mission;
3. captivity;
4. a map event or player encounter;
5. a siege (besieging, or a besieger camp);
6. inside a settlement;
7. in an army it does not lead;
8. an inactive party (enlistment parks the party);
9. at sea;
10. a field camp (the camp would stay behind with its visual).

The menu itself opens only from the free map (`MapMenuGate`, as the Fiefs hub), and the teleport list closes the menu
(`GameMenu.ExitToLast`) before the party moves. The move (`CheatWorldAdapter.TeleportToGate`):
- sets `MainParty.Position` to the settlement's `GatePosition` (an army the player leads comes along);
- calls `SetMoveModeHold` and refreshes the party visual;
- stops time and puts the camera on the party.

The party arrives outside the gate, and the player enters as usual, so every settlement-entered listener runs through the
normal encounter.

## Co-op

Single player only. The button is added only while `CoopSessionPolicy.IsSinglePlayerAuthority`, every menu button except
Close hides without it, and every cheat refuses without it, read live at each call. TAOM has no message that carries a cheat
to the host, and a cheat on one peer would desync the campaign. The switch is on `CoopSettingsRelevance`'s `PlayerLocal` list.

## Console commands

Each command needs cheat mode and a running campaign (`TaomConsole.RunInCampaign`), not the MCM switch. Each is tier B
(dev-console.md): it echoes the result and changes only what it names.

| Command | Effect |
|---|---|
| `taom.cheat_gold [1-1000000]` | Gives gold to the main hero (default 10,000) |
| `taom.cheat_influence [1-10000]` | Influence for the player's clan in its kingdom (default 100) |
| `taom.cheat_renown [1-10000]` | Renown for the player's clan (default 100) |
| `taom.cheat_troops basic\|elite [1-500]` | The culture's basic or elite basic troop into the main party (default 10) |
| `taom.cheat_heal` | Heals every wounded soldier and every hero in the main party |
| `taom.cheat_relation <kingdom_id> [-100..100]` | Changes the player's relation with every living lord of the kingdom (default +20) |
| `taom.cheat_teleport <settlement_id>` | Teleports to a town's, castle's or village's gate, with the refusals above |

The special resource has its own command, `taom.add_special_resources` (special-resources.md). The menu's +1,000 uses the same
capped `ISpecialResourceService.GrantAmount`, for the main hero's own resource.

## Key files

| File | Purpose |
|---|---|
| `Main/Features/CheatMenu/CheatMenuPolicy.cs` | Pure rules: when the menu shows, when a teleport is refused |
| `Main/Features/CheatMenu/CheatMenuService.cs` | The cheats, with amount limits and the co-op refusal |
| `Main/Adapters/CheatWorldAdapter.cs` | The engine calls (vanilla actions, the roster, the teleport) |
| `Main/Features/CheatMenu/CheatMenuOpener.cs` | Switch, single player and free map; opens the root menu (button and key) |
| `Main/Features/CheatMenu/TaomCheatMenuHotKeyCategory.cs` | The rebindable map key (default H, id 550), registered at process load |
| `Client/Features/CheatMenu/UI/CheatMenuMapView.cs` | Reads the key every map frame (`CheatMenuKeys`); attached by `CheatMenuMapViewAdapter` from the behavior's tick |
| `Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs` | The three game menus and the pick lists |
| `Main/Features/CheatMenu/Hooks/Patch151_CheatMenuNavigation.cs` | Adds the button to the map bar while the menu is shown |
| `Client/Features/CheatMenu/UI/TaomCheatsNavigationElement.cs` | The button (TAOM.Client, ADR-013); icon layer `taom_cheats` in `MapBar.xml`, on the game's `SaveLoad\icon_gold` |
| `Main/Features/CheatMenu/Cheats/CheatMenuCheats.cs` | The console commands |

## How to verify in game

1. Load a single-player save with the switch off: the bottom bar shows no Cheats button.
2. Turn on Enable Cheat Menu in Mod Options, then load the save again: the Cheats button (gold icon) shows after Fiefs.
3. Click it on the free map: the cheat menu opens. Try Gold, Influence, Renown, Heal and both Troops buttons, and check the
   message and the change on the party screen and the map bar.
4. Teleport: Towns..., type a town name in the search box, choose it. The menu closes and the party stands outside that
   town's gate with the camera on it. Enter the town and leave again, and the village or town menus work as usual (the case
   12 crash path).
5. Enter a village, then use `taom.cheat_teleport <a town id>` in the console (cheat mode on): it answers "Leave the
   settlement first" and the party does not move.
6. Relations...: choose a kingdom; the encyclopedia shows +20 with its lords.
7. Turn the switch off and load again: the button is gone.
8. With the switch on, press H on the free map: the cheat menu opens. Press H inside a town menu: nothing. Rebind the key
   in Options > Keybindings > Campaign Map and check the new key opens it. With the switch off, H does nothing.

## Limits

- The map bar is built once per map screen. A switch turned on or off shows on the next map screen (loading a save).
- A teleport next to an enemy party starts an encounter on the next tick, the same as the game's own Ctrl+click cheat.
- Special resources are per hero and resolved by kingdom, then culture. A hero whose kingdom and culture grant none gets
  "Nothing changed".
- Not tested in game (14 Nov 2026): the button, the map key, the menus, the lists and the camera after a teleport.
- A clash of H with the RTS Camera or Command System defaults is unknown; rebind either key if both act.

## Changelog

- 2026-11-14: Initial feature: MCM switch, map button, campaign cheat menu, `taom.cheat_*` console commands. The battle cheat
  menu follows separately.
- 2026-11-14: Rebindable map key "Open Cheat Menu (TAOM)", default H, with the button's gates.
