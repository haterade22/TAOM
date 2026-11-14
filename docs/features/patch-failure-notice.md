# Patch Failure Notice

## Overview

When a group of Harmony patches fails to apply, TAOM tells the player. This feature adds the case that
notice cannot see: a fix that applied, then switched itself off. A transpiler that found no anchor in the
engine's IL, or a feature latch that tripped after an exception, now leaves one entry in a ledger. A
main-thread drain shows it as one red chat line and writes it to the TAOM log (#1185).

## Why This Exists

- **Vanilla behavior:** Harmony counts a transpiler that returns the method unchanged as applied.
  `PatchCategoryApplier.TryApply` returns true, so `ReportPatchFailures` stays silent.
- **TAOM requirement:** the player and a bug reporter must learn that a fix is off. Several transpilers
  logged only a warning in the log file, and three of them (Patch23 scene notifications, Patch93, Patch66)
  logged nothing. The feature latches (Marshal's Word, the building UI hook, the marriage service
  resolve, the Hidden Hosts fault budget) switch a feature off for the mission or until the game
  restarts, long after any apply notice has run.
- **Without this feature:** an engine update that moves one IL anchor turns a fix off with no sign on
  screen, and the report arrives as a gameplay complaint.

## Architecture

### Design Challenge

The existing notice runs at three points, each once per process: the first main menu, game
initialization and the first mission start (`SubModule.ReportPatchFailures`). A latch that trips later never
reaches any of them. A report can also come from the AI thread or a native callback, where nothing may
write a log line or touch the UI.

### Solution Approach

- `SelfDisables.Report(id, reason)` (`Main/Core/Diagnostics/SelfDisables.cs`) records the fix. It takes only the
  short internal lock of `ConcurrentDictionary.TryAdd` and runs no other code while it holds it, writes nothing
  and works before IoC exists. An id is kept once for the process, so a per-mission latch shows its first fault
  only.
- `SelfDisableNotice.Tick()` (`Main/Core/Diagnostics/SelfDisableNotice.cs`) runs from one line in
  `SubModule.OnApplicationTick`. While nothing waits it reads one integer and one list count. It works in two
  steps:
  1. **Record**, on the first tick after a report: one `[SelfDisable]` warning per entry, so the log has it even
     when the session ends at the main menu or during a load. The entries then wait in the ledger's `Unshown`
     list.
  2. **Show**, once the chat can show the line: one red chat line that names every waiting id, then one
     `[Check] id=taom.patch-notice.self-disable result=pass` line per id.
- The chat can show the line when a game is running, no loading window is up, and the screen is the campaign map
  or a mission. The engine's chat filter (v1.5.7 `MPChatVM.IsChatAllowedByOptions`) drops a line, and keeps no
  copy, in a mission with Hide Battle UI on, so the line waits for the map then. With the single-player chat box
  off the notice writes `result=skip reason=chat-off` and shows nothing. On character creation and the intro
  video the line waits too, because it would fade before the campaign map appears.
- On a dedicated server Record writes the warning and a `result=skip reason=dedicated-server` line and shows
  nothing (ADR-013).
- A log that throws costs only its line. A chat line that throws leaves its entries waiting, and the tick records
  the throw as `[Swallowed] feature=PatchNotice site=SelfDisableNotice.Tick`. The next new entry tries the line
  again with every waiting id; the notice does not try again every frame.
- `PatchCategoryApplier` and `ReportPatchFailures` are unchanged.

### Component Diagram

```
site (transpiler anchor missing, or a latch trips; any thread)
        |
  SelfDisables.Report(id, reason)  -> SelfDisableLedger (ConcurrentQueue, once per id)
                                              |
  SubModule.OnApplicationTick (main thread)   |
        |                                     |
  SelfDisableNotice.Tick -- Record: [SelfDisable] warning -> Unshown
                         -- Gate (map or mission, chat options)? -- Show: red chat line, [Check] line
```

## Sites

The id is the feature's patch category, or `<category>.<part>` where one category holds two fixes, so a
player report greps straight to its `## <category>` heading in the
[patch registry](../reference/harmony-patch-registry.md). The Marshal's Word thrower patch applies in the
category `Patch_MissionTime_MarshalsWord` and reports under the feature's `Patch141_MarshalsWord`.

| Site | Id | What is lost |
|---|---|---|
| Marshal's Word soldier callback latch | `Patch141_MarshalsWord.SoldierCallback` | the archers' draw hold and the held line's slot clamp, until the process ends |
| Marshal's Word array prefix latch | `Patch141_MarshalsWord.ArrayLayout` | several companies laid out as one body, for the mission |
| Marshal's Word thrower latch | `Patch141_MarshalsWord.ThrowerStand` | the throwers' stand, for the mission; until the game restarts when its field does not bind |
| Marshal's Word spacing transpiler | `Patch141_MarshalsWord.SpacingRepair` | the spacing repair in one formation method |
| Building UI hook latch | `Patch108_TaomBuildings.UiHook` | the TAOM building icon and effect line on the town screen |
| Marriage service resolve latch and clan draw transpiler | `Patch81_MarriageAlignment` | the partner steering for AI marriages |
| Hidden Hosts fault budget (32 hook faults), and an engine field that does not bind in the nameplate or tooltip hook | `Patch105_HiddenHosts` | the hidden troop counts: vanilla counts show until the game restarts |
| Bard town guard transpiler | `Patch110_BardTownGuard` | the musician fix |
| Party icon colour transpiler | `Patch23_BannerColorPersistence.PartyIcon` | a clan's own colours on its map party icon |
| Scene notification transpiler | `Patch23_BannerColorPersistence.SceneNotification` | the clan faction on scene-notification characters |
| Castle recruitment AI transpiler | `Patch42_CastleRecruitment` | AI lords scoring castles for recruitment |
| Character body generator transpiler | `Late_Transpiler` | the race `_facegen` action set in the body-generator preview |
| Creature bandit weapon state transpiler | `Patch93_CreatureBandits.WeaponState` | route A's weapon state for the riderless creature (the loss in play is not verified) |
| Order banner transpiler | `Patch66_Enlistment.OrderBanner` | the banner reads "Men! TaleWorlds.MountAndBlade.BehaviorStop!" again |
| Mission start guard transpiler | `Patch103_MissionStartGuard` | the guard around the mission start calls |
| Party icon scale transpilers | `Patch53_PartyIconScale` | the Map Figure Scale slider for one or more figures |
| Seat of Seeing order panel transpiler | `Patch140_SeatOfSeeing` | a hero who fights on can open the order panel |
| Banner layer limit transpiler | `Patch15_BannerLayerLimit` | nothing today: the setting is off by default and v1.5.7 has no layer cap |

Left out on purpose: transpilers whose loss costs the player nothing (the RaceAge assert noise, the tick and
map profilers, which are off by default), the Shadowfax clan and inventory fast paths (a degrade builds the
screen as vanilla does and the install is already logged), the diagnostic latches, and the polygon
emblem latch. A log-once flag that does not skip the next call is not a disable and is not reported.

Not reported yet (#1223): four feature latches that sit outside a patch class, in the custom attack blow
binding, the action index repair, the Player Switcher session latch and the map view release.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Core/Diagnostics/SelfDisables.cs` | the ledger and its static door |
| `Main/Core/Diagnostics/SelfDisableNotice.cs` | the main-thread notice: the log line, the chat gate, the chat line and the `[Check]` lines |
| `Main/SubModule.cs` | one line in `OnApplicationTick` |
| `Main/_Module/ModuleData/taom_module_strings.xml` | the English row for `taom_patch_self_disabled` |
| `tools/ingame_checks/1185.toml` | the in-game check |

## Dependencies

- `IModLogger` and `IOutcomeLog` (Core logging): the notice resolves them only when an entry waits.
- `ClientComposition.IsDedicatedServer`: the server branch.
- `GameStateManager.Current.ActiveState`, `LoadingWindow.IsLoadingWindowActive`, `BannerlordConfig.HideBattleUI`
  and `BannerlordConfig.EnableSingleplayerChatBox`: the chat gate, read in a separate method a server never calls.

## Tests

- `TAOM.Tests/Core/Diagnostics/SelfDisableLedgerTests.cs`: once per id, report order, drain once, two threads
  reporting the same ids.
- `TAOM.Tests/Core/Diagnostics/SelfDisableNoticeTests.cs`: Record on a client and a server, a log that throws,
  Show for each gate answer, a chat line that throws and the next report, the gate's answers, the order of the
  steps in `Tick`, the reads of the client gate, the registered text and its red colour, the one call in
  `OnApplicationTick`, and the `[Check]` and `[Swallowed]` lines rendered through the real `OutcomeLog`.
- `tools/tests/test_ingame_verdict.py` (`Issue1185Tests`): the manifest judged on fixtures that hold those
  rendered lines.
- `TAOM.Tests/Core/Diagnostics/SelfDisableTranspilerSiteTests.cs` and the degrade tests beside the existing
  transpiler tests (Bard town guard, Seat of Seeing, party icon scale): each feeds a stream with no anchor and
  expects one ledger entry with the site's id.
- `MarshalPatchTests`, `TaomBuildingUiHooksTests`, `Patch81SelfDisableTests` and `HiddenHostsFaultBudgetTests`
  (with `HiddenHostsBindMissSelfDisableTests`) cover the latches.
- `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` and the feature binding tests run the transpilers over
  the installed engine's IL: on a healthy v1.5.7 install no site reports.

## How to verify in game

**Preconditions:** the desktop install, a campaign that loads, and a log in `Modules/TAOM` (the `taom` log).

1. Close the game. In `Modules/TAOM/ModuleData/configs/banner_color_config.json` set
   `"EnableLayerLimitTranspiler": true` (the file is read once per launch).
2. Start the game and load a saved campaign. The v1.5.7 `Banner.TryGetBannerDataFromCode` has no `RemoveRange`,
   so Patch15 reports at the first campaign load of the launch. One red line naming `Patch15_BannerLayerLimit`
   appears once the campaign map shows. The log holds `[SelfDisable] Patch15_BannerLayerLimit is off:` and
   `[Check] id=taom.patch-notice.self-disable result=pass feature=Patch15_BannerLayerLimit shown=chat`.
3. Start a battle and load again in the same session. The line does not repeat.
4. Optional, for a new campaign: start one with the flag on. The line waits through character creation and
   shows on the campaign map.
5. Close the game and set the flag back to `false`.

`python tools/ingame_verdict.py --issue 1185` reads the `[Check]` line. `shown=chat` means the engine took the
line while the chat could show it; that the player saw it is a human claim in the manifest. The timing, read in
the v1.5.7 code: the game-init reports queue under the loading window, the chat is cleared only before a game
exists, and `MapScreen` lowers the loading window a few frames after its scene is ready.

## How to add a site

1. Put one `SelfDisables.Report(<id>, <reason>)` on the failure branch, beside the existing log line. Build the
   reason only on that branch.
2. Write a test that takes the branch and expects one ledger entry with the id.
3. Add a row to the table above.

## Changelog

- #1185: the ledger, the drain and a report at every site in the table above.
