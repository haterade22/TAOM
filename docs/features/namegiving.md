# Namegiving

## Overview

The ruler of a realm can rename its towns, castles and villages. A Rename button with its influence price sits under the Annex button and its cost on the Fiefs tab of the kingdom screen. It opens a picker of the selected fief and its villages, then a text box prefilled with the current name, then a confirmation. The new name shows in the fief list, the panel title, the village cards, the map nameplate and the encyclopedia, and it survives save and load. Four fiefs also take a lore name when the matching side holds them: Minas Morgul becomes Minas Ithil, Dol Guldur becomes Amon Lanc, Fornost Erain becomes Deadmen's Dike and Orthanc becomes Treegarth of Orthanc. The map name comes back when the other side retakes the fief. A name the player chose is never overwritten.

## Why This Exists

- **Vanilla behavior:** a settlement's name is fixed by the map data. The engine has a custom-name slot on every party (`PartyBase.CustomName`, saved with the settlement, and `Settlement.Name` returns it when set), but no screen uses it for settlements.
- **TAOM requirement:** a player who founds or conquers a realm in a Middle-earth setting wants to name its places, and the lore has older and darker names for four of the great fortresses that belong to whoever holds them.
- **Without this feature:** every place keeps its map name for the whole campaign, so Minas Morgul stays Minas Morgul in the hands of Gondor.

## Architecture

### Design Challenge

- The Fiefs tab is a vanilla view model tree (`KingdomManagementVM` holds `KingdomSettlementVM`, which holds the list of `KingdomSettlementItemVM`) and a vanilla prefab. The button must be added without replacing either, so it rides UIExtenderEx: one view model mixin and one prefab insertion. No Harmony, no reflection.
- The `FiefsPanel` prefab is mounted with `DataSource="{Settlement}"` (the `KingdomSettlementVM`), but the natural home for the mixin is `KingdomManagementVM`: it declares its own `OnFinalize`, while `KingdomSettlementVM` declares none, and a mixin on a target without one makes UIExtenderEx stack a hook on the base `ViewModel.OnFinalize` ([lesson](../reviews/lessons/harmony-il.md)). The inserted block therefore opens with `DataSource="{..}"` to step back up one level.
- `ViewModel.OnPropertyChangedWithValue<T>` raises `PropertyChangedWithValue` only, never `PropertyChanged`, so the selection listener must subscribe to the former.
- `PartyBase.SetCustomName` stamps settlement properties onto the `TextObject` it is given, so every call gets a fresh one.
- Players type free text into a localized text system. `Common.TextContainsSpecialCharacters` allows `{ } [ ]`, which the text engine reads as markup, so TAOM refuses them.

### Solution Approach

`NamegivingService` holds every rule and takes `IFiefNamingAdapter` (reads a settlement as a `FiefNameSnapshot`, lists every name, reads influence, the ruler flag and the map-action reason; writes `ApplyPlayerName`, `ApplyLore` and `Restore`), `IAlignmentService`, the two co-op providers, the settings provider and a logger.

- **Gates, in order** (`Check`): the target-independent gates first, the MCM switch, single-player (`CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and `IsAuthority`)), `Hero.MainHero.IsKingdomLeader` and the engine's map-action gate (`CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`, as vanilla's kingdom-rename button uses it: not a prisoner, not on a raft or ferry, not in a mission, an encounter, a raid, a siege or a map event); then the gates of the target itself, that the fief belongs to the player's kingdom and `influence >= cost`, written so a NaN purse fails. So a fief outside the realm reports the map action first when both fail (a test pins it). The target-independent gates are read once for a whole picker (`Targets`, `ButtonState`), and `Check` hands back the engine's reason text beside the block. `Apply` runs every gate again, plus the name rules against a fresh names snapshot, because the three prompts take time.
- **Prices** are constants: 2000 for a town, 1000 for a castle, 500 for a village.
- **Name rules** (`ValidateName`, the first one broken wins): 1 to 50 characters; no `{ } [ ]`; letters, digits, single spaces and punctuation only (a tab, a no-break space, a symbol or an emoji is refused); no leading or trailing space; no two spaces in a row; not the current name; not another settlement's current name, compared with `InvariantCultureIgnoreCase`. Data names are not unique, so the check is a list scan against a snapshot taken once per prompt, with the target excluded by id.
- **Writing a name.** `FiefNamingAdapter.ApplyPlayerName` calls `SetCustomName(new TextObject("{=!}" + name))` first and `ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -cost)` second. The `{=!}` prefix is the engine's marker for text that is not looked up in the language tables, so a player's name is never translated. Name first means a throw in the charge leaves a named fief, never a charge for nothing. Influence changes through the action so `OnClanInfluenceChanged` fires.
- **The button.** `FiefNamingMixin` (`[ViewModelMixin("RefreshValues")]` on `KingdomManagementVM`) subscribes to `Settlement.PropertyChangedWithValue` in its constructor and filters on `CurrentSelectedSettlement`; `OnFinalize` unsubscribes. It never calls `RefreshSettlementList`, which clears the list and resets the selection. `ButtonState` folds over the picker's rows: it hides the button unless the switch is on, the game is single-player and the player rules a realm, greys it with a reason while the map action is blocked or no row of the picker can be renamed, and shows the selected fief's price. The constructor does not refresh: UIExtenderEx replays the view model constructor's `RefreshValues` call on the mixin as `OnRefresh`. After a rename the mixin writes `item.Name = settlement.Name.ToString()` into the fief's row and calls `item.RefreshValues()`, which re-reads the village names.
- **The prompts** (`NamegivingPrompts`): `MBInformationManager.ShowMultiSelectionInquiry` (each `InquiryElement` shows its price; a row that cannot be renamed is disabled with its reason as the hint), then `InformationManager.ShowTextInquiry` with a `textCondition` that runs on open and on every keystroke (OK is greyed with the reason while the name is invalid; the prefilled current name starts greyed with "That is already its name."), then `InformationManager.ShowInquiry`. The result of `Apply` carries only the outcome (`Success`, `Block`, `Problem`, `Detail`); the done message is built from the picked row's name and the typed text. Every callback runs inside the mixin's `ScreenFaultLog`, which logs one line per site per screen.
- **Placement and the hint shape.** The block is inserted with `InsertType.Child` at `Index = int.MaxValue` into `descendant::ListPanel[@Id='ActionButtonsContainer']/Children`, so it is the last child of the vertical action column and inside `FiefsActionButtonsScope`, the gamepad scope. `Append` would insert a sibling of the container (UIExtenderEx `PrefabComponent`), in a horizontal row outside that scope. The shape is vanilla's governor-button shape (as Reeve's Ledger), not the Annex block: a fixed, always-enabled wrapper `Widget` holds the disabled-reason `HintWidget` beside the `ButtonWidget`, because Gauntlet never hovers a disabled widget ([Reeve's Ledger RCA](../reviews/rca-reeves-ledger-2026-10-19.md) finding 1). The button's own hint sits inside it for the enabled state. A test pins that the disabled hint's parent is not a button and that the wrapper carries no `IsEnabled`, `IsDisabled`, `DoNotAcceptEvents` or `DoNotPassEventsToChildren`.
- **Lore names.** `NamegivingBehavior` listens to `CampaignEvents.OnSettlementOwnerChangedEvent` (fired after the new owner is set), `OnClanChangedKingdomEvent` and `OnSessionLaunchedEvent`. A clan that joins, defects from, founds or rebels against a kingdom keeps its fiefs and raises no owner event ([lesson](../reviews/lessons/campaign-mechanics.md)), so the kingdom-change handler reconciles that clan's own settlements (`clan.Settlements`) and nothing else. The full pass runs on `OnSessionLaunchedEvent`, which fires for a new campaign and a loaded one; `OnGameLoadedEvent` fires for saved games only. `NamegivingService.ReconcileLore` resolves the holder's side with `IAlignmentService.ResolveSide(mapFaction.StringId, mapFaction.Culture.StringId)` and `DecideLore(row, side, origin, enabled)` returns Keep, Set or Restore. A custom name is classified from its raw text: empty is None, a `{=taom_namegiving_lore_` prefix is Lore, anything else is Player and is never touched. With the switch on, the row's side sets the lore name (unless another place already bears it, which is skipped and logged), the other side restores the map name (`SetCustomName(null)`), Neutral keeps whatever is there. With the switch off, a fief with a lore name gets its map name back whoever holds it (a player name stays), at the next capture, load or change of realm. `MayWriteWorld` is checked before the switch, so a co-op peer restores nothing. The session pass catches per fief and once around the table, and logs one line for the pass: a throw in a campaign-start handler loops the loading screen ([lesson](../reviews/lessons/campaign-mechanics.md)), so the service never lets one out.
- **Co-op.** Single-player only; the gated row is in [coop-interop.md](coop-interop.md). The switch is counted in the settings fingerprint: the lore half reacts to captures with no click, like `EnableFromTheAshes`, so two peers holding different values could reach different names.

### Component Diagram

```
TaomSettings.EnableNamegiving ─ NamegivingSettingsProvider
                                         │
KingdomManagementVM ── FiefNamingMixin ──┼── NamegivingService ◄── ICoopSessionProvider, IDedicatedServerProvider, IAlignmentService
  (RefreshValues)        │               │         ▲
  Settlement ◄─ PropertyChangedWithValue │         └── IFiefNamingAdapter ── Settlement, PartyBase.SetCustomName,
                         └── NamegivingPrompts ─────────┘                      ChangeClanInfluenceAction, CampaignUIHelper
FiefsPanel.xml ◄── FiefActionsRenamePrefab (last child of ActionButtonsContainer, DataSource {..})
CampaignEvents.OnSettlementOwnerChangedEvent, OnClanChangedKingdomEvent, OnSessionLaunchedEvent ── NamegivingBehavior ── NamegivingService.ReconcileLore / ReconcileAllLore ── LoreNames
```

## Configuration

There is no JSON file: the prices are constants in `NamegivingService` and the four lore rows are in `LoreNames`.

| MCM setting | Group | Default | Effect |
|---|---|---|---|
| `EnableNamegiving` | Fief Management/Namegiving (GroupOrder 3) | on | the Rename button and the lore names; the button follows when the kingdom screen next opens, and switching it off gives the lore fiefs their map names back at the next capture, load or change of realm |

The switch is counted in the co-op settings fingerprint (`CoopSettingsRelevance` does not exclude it): the lore half reacts to captures on its own, like `EnableFromTheAshes`. The writes themselves run behind `MayWriteWorld`.

### The lore names

| Settlement | Map name | Lore name | Held by | Key |
|---|---|---|---|---|
| `town_ES2` | Minas Morgul | Minas Ithil | Free | `taom_namegiving_lore_minas_ithil` |
| `town_DG1` | Dol Guldur | Amon Lanc | Free | `taom_namegiving_lore_amon_lanc` |
| `town_AN2` | Fornost Erain | Deadmen's Dike | Evil | `taom_namegiving_lore_deadmens_dike` |
| `town_isengard` | Orthanc | Treegarth of Orthanc | Free | `taom_namegiving_lore_treegarth` |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Namegiving/NamegivingModule.cs` | registers the services and the one behavior; saves nothing |
| `Main/Features/Namegiving/NamegivingService.cs` | gates, prices, name rules, classify, lore decision and reconcile |
| `Main/Features/Namegiving/NamegivingTypes.cs` | the plain value types in and out of the service |
| `Main/Features/Namegiving/LoreNames.cs` | the four lore rows and the key prefix |
| `Main/Features/Namegiving/NamegivingSettingsProvider.cs` | the MCM switch, cached lazily (a concrete class) |
| `Main/Features/Namegiving/Hooks/NamegivingBehavior.cs` | the capture, kingdom-change and session-launch listeners |
| `Client/Features/Namegiving/UI/FiefNamingMixin.cs` | the button's state and click |
| `Main/Features/Namegiving/UI/NamegivingPrompts.cs` | picker, text box, confirmation |
| `Main/Features/Namegiving/UI/NamegivingTexts.cs` | every player text |
| `Main/Core/Logging/ScreenFaultLog.cs` | the guard and the once-per-site log (shared with the Reeve's Ledger) |
| `Client/Features/Namegiving/UI/FiefActionsRenamePrefab.cs` | the insertion into the `FiefsPanel` prefab |
| `Main/Adapters/IFiefNamingAdapter.cs`, `FiefNamingAdapter.cs` | settlement reads and the three writes |
| `Main/Features/TaomSettings.cs` | `EnableNamegiving` |
| `Main/Composition/FeatureModules.cs` | the module line |
| `Main/_Module/ModuleData/taom_module_strings.xml` | the 22 `taom_namegiving_*` rows |

## Dependencies

- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player write gate.
- `IAlignmentService` (Execution): the Free, Evil or Neutral side of a holder.
- The vanilla `FiefsPanel` prefab and `KingdomSettlementVM` / `KingdomSettlementItemVM`; the vanilla key `str_warning_you_dont_have_enough_influence`, reused for the influence reason.
- UIExtenderEx 2.13.3: `[ViewModelMixin]`, `[PrefabExtension]`, `PrefabExtensionInsertPatch`.
- `ScreenFaultLog` (Core/Logging): the mixin builds one and passes it to the prompts.

## Tests

- `TAOM.Tests/Features/Namegiving/NamegivingServiceTests.cs` (97): every name rule (braces, square brackets, tab, newline, no-break space, symbol, emoji, 50 and 51 characters, edge and double spaces, same name, case-insensitive taken, accented letters), prices, each gate and the gate order (including a fief outside the realm while the map action is blocked), NaN influence, the button state, the picker rows, the map-action gate read once per picker, apply (stale realm, stale influence, stale co-op state for each of the four co-op states, a map action blocked since the prompt, bad name, taken), classify, `DecideLore` for each side and origin and with the switch off, `ReconcileLore` (switch off restores a lore name and leaves a player name, nothing in a co-op state) and `ReconcileAllLore` including a throwing adapter.
- `LoreNamesTests.cs` (6): the four decided pairs, keys and prefix, every lore name passes the name rules.
- `NamegivingSettingsProviderTests.cs` (5): defaults, the group and order, no digits in the hint, the switch counted for co-op.
- `TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs` (9): one line per site, the tag, the empty results on a throw, the guarded condition, every throw counted in the session summary while the error line stays latched.
- `NamegivingWiringTests.cs` (18): the module line, the container graph, the 150-line cap on every UI and hook file, the three behavior listeners (owner change, clan kingdom change, session launch) and the clan handler's shape, the guarded session path, the latch, the subscribe and unsubscribe pair, no refresh in the mixin constructor, no settings read in the mixin, no write in the UI.
- `NamegivingPrefabTests.cs` (7): the anchor and the child insertion at `int.MaxValue`, the `{..}` root, the disabled hint outside the button, every binding resolving on the mixin (read from attribute metadata, constructing nothing).
- `FiefNamingAdapterPinTests.cs` (11): the name written before the charge, the `{=!}` prefix, a fresh `TextObject`, `PropertyChangedWithValue` rather than `PropertyChanged`, no `RefreshSettlementList`.
- `NamegivingLiveDataTests.cs` (3, `LiveInstall`): the XPath matches one node in the installed `FiefsPanel.xml`, the panel's `DataSource` is `{Settlement}`, and the four lore ids exist in the live TAOM_Map with no place already bearing a lore name.
- `NamegivingTextsTests.cs` (11, `RequiresGame`: it builds `TextObject`s).
- `CampaignHotPathSettingsProvidersTests`, `SettingsFingerprintTests`: the lazy accessor and the setting counts.

## How to add a lore name or change a price

1. Add a `LoreRow` to `LoreNames.All` with the live TAOM_Map settlement id and the full literal `{=taom_namegiving_lore_<name>}English` (spelled out, so `tools/harvest_literal_loc_keys.py` can find it), and the side that earns it. `LoreNamesTests` pins the exact set of rows, keys and sides, so a new row breaks it on purpose: update the expected sets in the same change.
2. Run `python tools/harvest_literal_loc_keys.py --apply`, then seed the new key into every language for free: `python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --seed-only --apply` per language. The paid translation of the seeded rows is a separate step on the maintainer's word ([#931](https://github.com/haterade22/TAOM/issues/931)).
3. `NamegivingLiveDataTests` checks the id exists and the name is free.
4. **Save contract.** A save stores the raw `{=taom_namegiving_lore_*}English` text of a lore name. Never rename `LoreNames.KeyPrefix` or a key, and never remove a row: a saved lore name would then read as a player's name, or lose its row, and could never be restored. Never park the module either.
5. A price is a constant in `NamegivingService` (`TownCost`, `CastleCost`, `VillageCost`); the MCM hint and the strings carry no number. A price change touches the `CostFor` test, the prices in this doc and the [feature-map](../reference/feature-map.md) row.

## Known Limits

- Only the realm's ruler can rename (Mike's decision). A clan leader who is not the ruler gets no button.
- Renaming is single-player only. Under co-op, with a co-op mod TAOM cannot probe, or on a dedicated server the button is hidden and the lore names do not change.
- Villages are renamed through their town or castle's picker; there is no per-card button.
- A player-chosen name is stored as plain text and does not translate. A lore name translates with the language.
- Hideouts and any other settlement kind cannot be renamed.
- A name that another place bears is refused even when that place is a hideout or in another realm.
- If a lore name collides with a name already in use, the fief keeps its map name; the collision is logged at each capture, change of realm and session start while it lasts.
- After a language switch the open kingdom screen keeps the old language until it reopens.
- The text box's reason hint can be missing when the box is reopened (the vanilla popup view model keeps its `InputText`).
- A renamed row in a name-sorted Fiefs list keeps its place until the list is re-sorted or the screen reopens.
- Uniqueness compares names in the current language, and `Restore` has no collision check, so two places can rarely end up with the same name after a lore name is taken back.

### In-game checklist (owed)

Unseen in game. `{..}` reaching `KingdomManagementVM` across the `FiefsPanel` boundary and the nameplate refresh after a rename are verified in source; the in-game confirmation is still owed.

- The Rename block appears as the last child of the action column, under the Annex button and its cost, and its bindings work.
- A gamepad reaches Rename inside the action buttons' navigation scope, with Right from Annex.
- The look and position of the button and the cost row.
- Rename a town, a castle and a village: the list row, the panel title, the village cards, the encyclopedia and the influence update at once, and the map nameplate shows the new name.
- Realm-border labels show realm names (a rename cannot reach them).
- The disabled hint on hover: not the ruler is hidden, in an encounter shows the engine's reason, short of influence shows the vanilla warning.
- A picker row for a village that cannot be afforded is disabled with its reason.
- The OK button is greyed with the reason while the typed name is invalid, including on open.
- Save and load keep the name; switching language keeps a player name as typed and translates a lore name.
- Console: capture Minas Morgul for a Free realm (becomes Minas Ithil) and retake it for an Evil one (restored); a player-renamed fief is not overwritten by either.
- Switch the MCM option off, then load: Minas Ithil goes back to Minas Morgul, and a player-chosen name stays.
- A lore fief whose clan defects, or rebels, across the Free and Evil line takes the other side's name.
- A new campaign starts with the four map names and no Namegiving error in the log.

## Changelog

- 2026-10-20: Namegiving added (#858). Review fixes: the block is a child of the action column (gamepad scope), a switched-off feature gives the lore fiefs their map names back, a clan's kingdom change reconciles its fiefs, the pass runs at session launch, and the switch is counted for co-op.

## GitHub Issue

- **Issue:** [#858](https://github.com/haterade22/TAOM/issues/858) Rename towns, castles and villages from the kingdom fiefs screen
- **Status:** Shipped on `bannerlord-1.5.x` in `d0e6725ec` (v2.0.35); the review follow-ups (the prefab insert gate and the
  shared `ScreenFaultLog`) landed in `69b47d00b`. Issue open: the in-game check is owed; the paid translation of the 22 new
  strings is tracked in [#931](https://github.com/haterade22/TAOM/issues/931).

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
