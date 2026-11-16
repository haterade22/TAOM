# Cheat Menu

## Overview

An optional menu of cheats for single player. The MCM switch **Enable Cheat Menu** (TAOM settings, group Cheat Menu, off
by default) adds a **Cheats** button to the campaign map's bottom bar, after the Fiefs button. The button opens three game
menus:

| Page | Buttons |
|---|---|
| TAOM cheat menu | Teleport to a settlement..., Gold +10,000 denars, Special resource +1,000, Influence +100, Renown +100, Troops..., Relations +20 with every lord of a kingdom..., Heal your party and your heroes, Close |
| Teleport | Towns..., Castles..., Villages... (each opens a searchable list), Back |
| Troops | +10 basic recruits, +10 elite recruits (the culture's `basic_troop` and `elite_basic_troop`), +10 of any troop of your faction... (a searchable list, see Troops below), Every troop (party screen)... (the game's own cheat party screen, see below), Back |

Every button calls `CheatMenuService`, the same code as the console commands below, and shows its result as a message. Gold, influence, renown, heal, relation and teleport results name what they did (`CheatGiven`): gold shows the new total in denars, heal the number of soldiers healed (#1297), relation the kingdom's display name and the number of lords changed, and teleport the settlement (#1317).

## Troops

"+10 of any troop of your faction..." opens a searchable list of every regular troop of the faction, from recruit to top
tier, each shown as "Tier N: name", sorted by tier, then name. A pick adds 10 of that troop to the main party.

- The faction's culture is the player's kingdom's culture, or the clan's culture without a kingdom. The two quick entries
  above it still use the main hero's own culture, as before.
- `CheatWorldAdapter.ReadFactionTroops` gathers the sources and `CheatTroopList.Select` (pure) decides. The list starts from:
  - the culture's own lists, whatever culture each troop carries: its basic and elite basic troop, its four militia
    troops, the troops of its vassal reward template, its tavern mercenaries (`basic_mercenary_troops`), its TAOM
    recruitment pool (`IVolunteerRecruitmentService.GetCulturePoolTroopIds`) and its Elite Emissary offers;
  - every troop that carries the culture and that a TAOM recruitment pool offers anywhere (`AllPooledTroopIds`, settlement,
    clan and culture pools, Gondor's JSON pools included) or a clan's party template holds.
- From there it follows every upgrade, and keeps only regular field soldiers: occupation Soldier or Mercenary, not a hero,
  not a template, not obsolete. Lords, wanderers, companions, the custom dev and supporter characters (all heroes),
  villagers, caravan guards and bandits stay out.
- A troop that only carries the culture's tag is not listed: Rohan, Dale, Dunland and Rhun reuse vanilla culture ids
  (vlandia, sturgia, empire, khuzait), and vanilla's own troops of those cultures stay loaded in the campaign; training
  dummies and town guards carry the tag too. Troops the arachnophobia option hides are left out.
- The service checks a pick against the list again before it adds anything, so an id from elsewhere is refused.

"Every troop (party screen)..." opens the game's own cheat party screen (`PartyScreenHelper.OpenScreenAsCheat`, v1.5.7).
In vanilla it replaces the normal party screen (the P key, the party button) when the engine's cheat mode is on
(`cheat_mode` in the engine config). It lists every valid encyclopedia troop, 10 of each, sorted by name, on the left;
the player moves the ones he wants into his party (arrows: 1, shift 5, ctrl all), sees the selected troop's model, and
presses Done. It is a dismiss-mode screen, as in vanilla: troops moved to the left are dismissed, and prisoners moved to
the left are released.
- With the engine's cheat mode on, `CheatWorldAdapter.OpenCheatPartyScreen` calls vanilla's method. Without it (vanilla
  then refuses with "Cheat mode is not enabled!"), it opens the same `PartyState` with the same initialization data, field
  for field, and vanilla's own list from the private `PartyScreenHelper.GetRosterWithAllGameTroops` (read with
  `AccessTools`; the binding gate `ReflectionSiteBindingTests` pins it). Its Done handler does what vanilla's private
  `PartyScreenHelper.DefaultDoneHandler` does: it releases the prisoners moved to the left
  (`EndCaptivityAction.ApplyByReleasedByChoice`). Without that, a lord moved left stays a prisoner with no captor. If a game update renames that method, the entry says there is nothing to do.
- No faction or culture filter: vanilla's call takes no roster (project owner, 2026-11-15: "just look at the games own
  cheat mode"). Going over the party-size limit is allowed there, as in vanilla.

## Map key

**H** on the campaign map opens the same menu as the button. Rebind it under Options > Keybindings > Campaign Map, row
"Open Cheat Menu (TAOM)" (`TaomCheatMenuHotKeyCategory`, game key id 550, after every other TAOM key id; its name rows are
in `global_strings.xml`).

- `CheatMenuMapView` (TAOM.Client) asks `CheatMenuOpener.TryOpenFromHotKey` every map frame. With the switch off or outside
  single player it returns at once and the key is not read. Otherwise a press goes through `TryOpen`, the button's own
  gate, so the key opens nothing while a menu, an encounter or a screen holds the map.
- A press with Ctrl or Alt held does not count.
- Why H: free on the campaign map (`MapHotKeyCategory` and `GenericCampaignPanelsGameKeyCategory` do not bind it, no TAOM
  key uses it). In battle vanilla binds H in `CheatsHotKeyCategory` (Ctrl combinations in game cheat mode), and
  DualWield uses N. TAOM's Fords of War (field a hero) and The Long Defeat (sound the horn) read raw H in their own
  missions, so the battle key does nothing while `FordsCommandView` or `LongDefeatHudView` (the views that read H) is in the
  mission (a story mission has the Long Defeat logic but not its HUD, so the key works there); the Esc menu item still opens the list there. The RTS Camera and Command System defaults could not be
  checked, so the key is rebindable.

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

## Battle

With the same switch on, in single player, the game's single-player Esc menu (`MissionGauntletSingleplayerEscapeMenu`, which
the campaign's battles, sieges and scenes use) gets a **Cheats** item just before Photo Mode, where vanilla adds its gamepad
"Cheat Menu" item. `Patch152_BattleCheatMenu` is a postfix on `MissionGauntletSingleplayerEscapeMenu.GetEscapeMenuItems` in
TAOM.Client. A click closes the Esc menu and opens a single-choice list. **H** in a battle opens the same list: Options >
Keybindings > Action, row "Open Battle Cheat Menu (TAOM)" (game key id 551 in the same category). `BattleCheatMenuView`, a
`[DefaultView]` in TAOM.Client, asks `BattleCheatOpener.TryOpenOnKey` each frame, which reads no key with the switch off or
outside single player. A press does not count with Ctrl or Alt held (vanilla's Ctrl+H cheat), nor while the Esc menu, the order
panel, the radial menu, photo mode or another pop-up is open.

| Entry | Effect |
|---|---|
| Heal yourself | The player's agent and its mount back to full health |
| Heal your side | Every active soldier of the player's team and its ally team, and their mounts |
| God mode: off / on | Switches god mode: the player and any mount they ride take no damage until it is switched off or the battle ends |
| Rout the enemy | Vanilla `mission.flee_enemies`: every active enemy soldier panics |
| Kill all enemies | Vanilla `mission.kill_all_enemies`: every AI-controlled soldier of every enemy team dies |
| +10 allied troops | 10 copies of the non-hero soldiers already on the player's side |
| +10 enemy troops | 10 copies of the non-hero soldiers already on the enemy side |

Every entry calls `BattleCheatService`, the same code as the console commands below, and shows its result as a message.
- Every action refuses in co-op first, then outside a running battle: no mission, no player team, a mission that has ended, or a
  side that has retreated.
- The spawns go round-robin over the distinct characters of that side's active soldiers. Heroes are never copied, so the
  player, companions, lords and the custom dev and supporter characters stay out. A side with nobody to copy (for example
  the enemy side in a town) changes nothing.
- They go through `MissionTroopSpawner`, the same code as `taom.spawn_troops`: its team check, its creature-bandit rule (a
  creature bandit always spawns as an enemy) and its ring of positions around the player.
- God mode sets the engine's own `Agent.MortalityState.Immortal` on the main agent and the mount it rides. In v1.5.7
  `Agent.HandleBlow` zeroes every blow's damage on an Immortal agent, the engine's and TAOM's own creature blows (spider,
  trample) alike, while the blow still lands: knockdowns, knockbacks and falls play as usual, they only take no health.
  `Invulnerable` was not used: the engine reads it only in its melee, missile, charge and fall callbacks, so a blow registered
  directly would still hurt. `BattleCheatMenuView` calls `BattleCheatService.TickGodMode` every frame, which protects a new
  horse after a remount, sets a horse the player left back to Mortal, and once the battle ends or a side retreats turns god
  mode off and sets both back. It touches only agents it made Immortal itself (`BattleCheatPolicy.PlanGodMode`): an agent the
  game made Invulnerable or Immortal is never taken over, and an agent whose state something else changed is let go untouched.
  It does not carry over to the next mission (`BattleCheatState.MissionKey`, the mission's identity hash; two missions with
  the same hash, which .NET does not rule out, would keep it on). Nothing is saved.

| Command | Effect |
|---|---|
| `taom.cheat_battle_heal [self\|side]` | Heals the player (default) or the player's side |
| `taom.cheat_battle_god [on\|off]` | Switches god mode (no argument), or turns it on or off |
| `taom.cheat_battle_rout` | Routs the enemy |
| `taom.cheat_battle_kill` | Kills every AI-controlled enemy |
| `taom.cheat_battle_spawn <1-200> [ally\|enemy]` | Copies of that side's non-hero soldiers (default enemy, as `taom.spawn_troops`) |

The battle commands need cheat mode and a mission (`TaomConsole.RunInMission`), not the MCM switch.

The list opens through `BattleCheatOpener.TryOpen`. `TryOpenOnKey` checks the switch and single player before it reads any key;
`BattleCheatMenuView` calls it for the battle key.

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
| `taom.cheat_relation <kingdom_id> [-100..100]` | Applies a relation change (default +20) with every living lord of the kingdom through `ChangeRelationAction.ApplyPlayerRelation`, so the game adds its Charm and perk bonus; with Personal Relations off, the engine moves each change to the lord's clan leader, who gets it once per lord (clamped at 100) |
| `taom.cheat_teleport <settlement_id>` | Teleports to a town's, castle's or village's gate, with the refusals above |

The special resource has its own command, `taom.add_special_resources` (special-resources.md). The menu's +1,000 uses the same
capped `ISpecialResourceService.GrantAmount`, for the main hero's own resource.

## Key files

| File | Purpose |
|---|---|
| `Main/Features/CheatMenu/CheatMenuPolicy.cs` | Pure rules: when the menu shows, when a teleport is refused |
| `Main/Features/CheatMenu/CheatMenuService.cs` | The cheats, with amount limits and the co-op refusal |
| `Main/Features/CheatMenu/CheatTroopList.cs` | Pure rule: which troops the faction troop list shows, and their order |
| `Main/Adapters/CheatWorldAdapter.cs` | The engine calls (vanilla actions, the roster, the teleport) |
| `Main/Features/CheatMenu/CheatMenuOpener.cs` | Switch, single player and free map; opens the root menu (button and key) |
| `Main/Features/CheatMenu/TaomCheatMenuHotKeyCategory.cs` | The rebindable map key (default H, id 550), registered at process load |
| `Client/Features/CheatMenu/UI/CheatMenuMapView.cs` | Reads the key every map frame (`CheatMenuKeys`); attached by `CheatMenuMapViewAdapter` from the behavior's tick |
| `Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs` | The three game menus and the pick lists |
| `Main/Features/CheatMenu/Hooks/Patch151_CheatMenuNavigation.cs` | Adds the button to the map bar while the menu is shown |
| `Client/Features/CheatMenu/UI/TaomCheatsNavigationElement.cs` | The button (TAOM.Client, ADR-013); icon layer `taom_cheats` in `MapBar.xml`, on the game's `SaveLoad\icon_gold` |
| `Main/Features/CheatMenu/Cheats/CheatMenuCheats.cs` | The console commands |
| `Main/Features/CheatMenu/Battle/` | The battle menu: `BattleCheatPolicy` (pure), `BattleCheatService`, `BattleCheatOpener`, `BattleCheatTexts` |
| `Main/Adapters/BattleCheatAdapter.cs`, `BattleCheatHostAdapter.cs` | The battle cheats' engine calls; the pop-up list and the message |
| `Main/Features/DevConsole/Cheats/MissionTroopSpawner.cs` | The spawner shared with `taom.spawn_troops` |
| `Client/Features/CheatMenu/Hooks/Patch152_BattleCheatEscapeMenu.cs` | The Esc menu's Cheats item |
| `Main/Features/CheatMenu/Cheats/BattleCheatCheats.cs` | The battle console commands |

## How to verify in game

1. Load a single-player save with the switch off: the bottom bar shows no Cheats button.
2. Turn on Enable Cheat Menu in Mod Options, then load the save again: the Cheats button (gold icon) shows after Fiefs.
3. Click it on the free map: the cheat menu opens. Try Gold, Influence, Renown, Heal and both Troops buttons, and check the
   message and the change on the party screen and the map bar.
   In Troops, open "+10 of any troop of your faction...": the list runs from tier 1 recruits to the faction's top troops,
   with cavalry and archers; type a name in the search box, choose it, and 10 of it join the party. As a vassal of
   another culture's kingdom, the list is that kingdom's.
   Then "Every troop (party screen)...": the party screen opens with every troop, 10 of each, on the left; click one to see
   its model, move some to your party, press Done; they stay in the party. Cancel leaves the party as it was.
   With the engine's cheat mode off and a lord in your prison, open it again, move the lord to the left and press Done:
   the release notice shows, and the lord is free afterwards (his encyclopedia page no longer says prisoner).
4. Teleport: Towns..., type a town name in the search box, choose it. The menu closes and the party stands outside that
   town's gate with the camera on it. Enter the town and leave again, and the village or town menus work as usual (the case
   12 crash path).
5. Enter a village, then use `taom.cheat_teleport <a town id>` in the console (cheat mode on): it answers "Leave the
   settlement first" and the party does not move.
6. Relations...: choose a kingdom; the encyclopedia shows at least +20 with its lords (more with Charm), with Personal Relations on.
7. Turn the switch off and load again: the button is gone.
8. With the switch on, press H on the free map: the cheat menu opens. Press H inside a town menu: nothing. Rebind the key
   in Options > Keybindings > Campaign Map and check the new key opens it. With the switch off, H does nothing.
9. With the switch on, start a field battle and press Esc: Cheats shows just before Photo Mode. Click it: the Esc menu closes and
   the battle list opens. Try each entry and check the message and the effect (health bars, fleeing enemies, the dead, new
   troops near you, no hero among them).
10. Turn the switch off and press Esc in a battle: no Cheats item.
11. With the switch on, press H in a battle: the battle list opens. Press Ctrl+H with game cheat mode on: vanilla's cheat runs
    and no list opens. Turn the switch off: H in battle does nothing.
12. Mounted in a field battle, pick "God mode: off". Open the list again: it reads "God mode: on". Let enemies hit you and your
    horse, ride off a drop and get hit by a spider or a trampling beast: no health is lost, and you are still knocked down
    and staggered. Dismount, take another horse: the new horse takes no damage, the old one does. Pick "God mode: on": both
    take damage again. Turn it on, win the battle, start another: the list reads "God mode: off".

## Limits

- The map bar is built once per map screen. A switch turned on or off shows on the next map screen (loading a save).
- A teleport next to an enemy party starts an encounter on the next tick, the same as the game's own Ctrl+click cheat.
- Special resources are per hero and resolved by kingdom, then culture. A hero whose kingdom and culture grant none gets
  "Nothing changed".
- Not tested in game (14 Nov 2026): the button, the map key, the menus, the lists and the camera after a teleport;
  the Esc item, the battle key, the battle list and every battle cheat. God mode (15 Nov 2026) is not tested in game either.
- The faction troop list (15 Nov 2026) is not tested in game: its content was checked only on the repo's data, for Gondor.
- God mode is kept by `BattleCheatMenuView`'s tick. A mission without the game's default views would get god mode only at the
  moment it is switched, with no remount follow-up and no switch-off at the battle's end; the next mission still starts with
  it off.
- In a field battle or siege, god mode turns off when the game declares the battle won or lost (the last enemy falls or
  flees, or your side is beaten), so you walk the field after a victory without it: `BattleEndLogic` sets
  `Mission.MissionEnded` before that walk. In a mission without the game's battle-end check it stays on until the mission
  ends. `Mission.RetreatSide` is never written in v1.5.7, so an AI retreat counts only through `MissionEnded`.
- A clash of H with the RTS Camera or Command System defaults is unknown; rebind either key if both act.
- A teleport or relation pick runs inside the same crash guard as a menu button (`Guarded` wraps the list's choice).
- Whether the battle pauses while the list is open has not been checked; the list is opened without the pause option.
- Kill all enemies counts every agent it hands to `KillAgentCheat`, which does nothing in a cutscene, conversation or barter.

## Changelog

- 2026-11-14: Initial feature: MCM switch, map button, campaign cheat menu, `taom.cheat_*` console commands. The battle cheat
  menu follows separately.
- 2026-11-14: Rebindable map key "Open Cheat Menu (TAOM)", default H, with the button's gates.
- 2026-11-14: Battle cheat menu: a Cheats item in the single-player Esc menu (Patch152), `taom.cheat_battle_*` console commands,
  and `taom.spawn_troops`' spawner moved to `MissionTroopSpawner` to be shared.
- 2026-11-15: Rebindable battle key "Open Battle Cheat Menu (TAOM)", default H, through the Esc item's gate.
- 2026-11-15: God mode in the battle list ("God mode: off / on") and `taom.cheat_battle_god [on|off]`: the player and
  any mount they ride take no damage (engine Immortal state) until it is switched off or the battle ends.
- 2026-11-15: Troops page: "+10 of any troop of your faction...", a searchable list of every regular troop of the
  faction's culture (kingdom, else clan), sorted by tier then name (`CheatTroopList`).
- 2026-11-15: Troops page: "Every troop (party screen)...", the game's own cheat party screen (every troop, 10 of each),
  opened without the engine's cheat mode.
