# Host Review

## Overview

A "Review the host" option on the Field Camp wait sub-menu opens a battle scene on the terrain under the main party, with the healthy part of the party standing in formation and no enemy. The player commands as general with the vanilla order bar, nobody can be hurt or killed, and holding the leave key (Tab by default) leaves back to the camp menu. Nothing the review does is written back to the party.

## Why This Exists

The map shows a party as one icon and a number. The player has no way to see the host they command, or to try orders on it, without a real fight.

- **Vanilla behavior:** a battle scene exists only through a map event or a mission started by a quest, tournament or the like. There is no way to stand the party in formation in peace.
- **TAOM requirement:** a safe, enemy-free scene where the whole host (up to the real battle size) can be seen and ordered about.
- **Without this feature:** the only place to see the host is a real battle, where every mistake costs men.

## Architecture

### Design Challenge

Vanilla field battles lean on a map event at every turn, and a friendly mission loses several of its own features. Each choice below answers one engine fact:

- **Own mission name and view list, no scoreboard.** The scoreboard view marks a mission as not friendly. A friendly mission keeps holding the leave key to leave, so the view list for `TaomHostReview` is the campaign field battle's list (`SandBoxMissionViews.OpenBattleMission`) minus the battle scoreboard, the equipment drop view, `MissionSingleplayerViewHandler` (the campaign hotkey panels), the battle music, the kill notification feed, `MissionCampaignBattleSpectatorView`, the order-of-battle screen and `MissionPreloadView`. `MissionPreloadView` is omitted because it reads `MapEvent.PlayerMapEvent` unguarded. `MissionFaceCacheView` is kept, and the escape menu gets `CampaignOptions.IsIronmanMode` as vanilla passes it, so an ironman run greys "Exit to Main Menu". A `[ViewCreatorModule]` class may hold only `[ViewMethod]` statics, because `ViewCreatorManager` reads attribute [0] unguarded; `ViewCreatorModuleGateTests` pins it.
- **A `DefaultVisualOrderProvider` subclass.** The vanilla order bar hides itself in a friendly mission, so `HostReviewVisualOrderProvider` (in TAOM.Client since ADR-013) inherits the whole order set and overrides only `IsAvailable()` to return true in a Host Review mission.
- **`PartyAgentOrigin` with `isInvincible`.** SandBox models hard-cast the origin's combatant to `PartyBase`, so a plain custom-battle origin crashes them. An invincible party origin satisfies the cast and writes no wound, death or hit points back. No combat XP is awarded because `BattleAgentLogic` is not in the behavior list (it would also read `MapEvent.PlayerMapEvent`), not because of `isInvincible`.
- **`CustomBattleCombatant` for both teams.** A party has no battle side without a map event, so the two sides are custom-battle combatants (the player's lineup as Defender, an empty Attacker).
- **No Order of Battle screen, and no static bypass flag.** `TaomBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux` returns `_service.CanPlayerSideDeployWithOrderOfBattle() ?? base`. `EnlistmentDeploymentService` returns `null` with no map event (pinned by `EnlistmentDeploymentServiceTests`), and `SandboxBattleInitializationModel` then returns false, so the screen never appears on its own. Nothing global is set, which is why the next real battle still shows it. See [enlistment.md](enlistment.md).
- **`DisableDying` set in `OnAfterDeploymentFinished`.** `FinishDeployment` resets the flag to false just before that callback, so it is set there.
- **No reinforcements, by construction.** The lineup is capped at the spawn logic's own battle size and the attacker side is empty (`DefaultBattleMissionAgentSpawnLogic` gives the defender the full size), so there is nobody left to reinforce with. No switch-off code exists.
- **Other TAOM mission behaviors.** Those that join every mission also join a review. One that must not should gate on `HostReviewMissionLogic.IsActiveIn`. TAOM's own synthetic blows go through `Agent.RegisterBlow` to `HandleBlow`, which zeroes damage under `DisableDying`.
- **Career abilities.** Ability state is in memory and cleared at mission end (`CareerAbilityService.ClearAll`, called from `CareerPerkMissionBehavior.OnEndMission`). The saved career data is ids, choices, tiers and flags (`CareerPersistenceBehavior.SyncData`), so a review spends nothing that persists.

### Solution Approach

No Harmony patch. `HostReviewCampaignBehavior` adds the menu option from `OnAfterSessionLaunchedEvent` and handles its condition and consequence; `HostReviewService` decides whether the review can open and who stands in it; `HostReviewMissionLauncher` opens the mission; `HostReviewMissionLogic` makes it harmless.

**What the player sees.** The option "Review the host" (id `taom_fc_review`) is on the Field Camp wait sub-menu `taom_fc_camp`, at index 5, after the slot reserved for the Refuge options and before Leave. It is always listed, and greyed with a tooltip giving the reason when the review cannot open. It is added from `OnAfterSessionLaunchedEvent`, which fires after every `OnSessionLaunched` listener, so index 5 lands after Refuge's index 4 and before Leave whatever the listener order. Far Watch ([far-watch.md](far-watch.md)) inserts its "Send a scout" option at the same index 5 from the same event, and its module is listed after this one, so its listener runs first and this option pushes it down: the order is Refuge, Review the host, Send a scout, Set the men to work (camp work, [field-camp.md](field-camp.md)), Leave. On entry a message reads "Your host stands for review. No one can be hurt. Hold {LEAVE_KEY} to leave.", where the key is the bound Leave key (`HotKeyManager.GetHotKeyId("Generic", 4)`), shown with `MBInformationManager.AddQuickInformation` as vanilla's duel does.

**Block reasons, checked in this order:**

| Order | Block | Tooltip key |
|---|---|---|
| 1 | Enlisted | `taom_fcamp_review_enlisted` |
| 2 | In an encounter (`IEncounterAdapter.HasCurrent` or `IsMainPartyInMapEvent`) | `taom_fcamp_review_busy` |
| 3 | Co-op session active | `taom_fcamp_review_coop` |
| 4 | Main hero wounded or absent from the roster (an empty roster, which is a failed read, lands here too) | `taom_fcamp_review_wounded` |
| 5 | Fewer than 2 healthy men in total | `taom_fcamp_review_few` |

`HostReviewBlock.OpenFailed` is the sixth reason and is not a precheck: `Launch()` returns it for an empty lineup or a launcher failure (`taom_fcamp_review_failed`).

**Lineup rule.** The main hero first, then the other healthy heroes, then healthy troops in roster order. Every roster line counts `Number - WoundedNumber`; for a hero line `Number` is 1 and `WoundedNumber` reads `HeroObject.IsWounded`, so a wounded companion and a stack's wounded men stay out of the scene. The list is cut at the real battle size, which is the smaller of the player's configured real battle size and `DefaultBattleMissionAgentSpawnLogic.MaxNumberOfTroopsForMission`. A stack that crosses the cut is trimmed. An empty lineup returns `OpenFailed` and opens nothing.

**Launch outcome.** `Launch()` returns `None` when the scene opened, otherwise the reason: the block (logged, nothing opened) or `OpenFailed`. Any value but `None` is shown to the player in red through the same reason text as the tooltip; `OpenFailed` reads "The host could not be mustered here."

### Threading

Every callback runs on the main thread: the menu callbacks, mission behavior callbacks and the mission tick. The mission logic keeps no per-agent state, so agent index recycling does not apply.

### Component Diagram

```
Field Camp menu (taom_fc_camp)
        |
HostReviewCampaignBehavior (option, tooltips, failure message)
        |
   HostReviewService (blocks, lineup)
      /          \
HostReviewPartyAdapter   HostReviewMissionLauncher
(roster, battle size)    (record, combatants, behaviors)
IEncounterAdapter               |
(encounter, map event)  HostReviewTroopSupplier (invincible origins)
                        HostReviewMissionLogic (friendly-safe rules)
                        HostReviewMissionViews / HostReviewVisualOrderProvider (TAOM.Client, via IHostReviewOrderProviderRegistrar)
```

## Configuration

There is no config file and no MCM toggle. The player has to pick the menu option to start a review, so a toggle would add a setting and its cost for no gain. The feature saves nothing (`SyncData` is empty). With Field Camp switched off the option is unreachable only because the camp menus are; Host Review has no switch of its own.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HostReview/HostReviewService.cs` | Block order, lineup rule, launch |
| `Main/Features/HostReview/IHostReviewMissionLauncher.cs` | The launcher interface |
| `Main/Adapters/IHostReviewPartyAdapter.cs` | Party adapter interface: battle size and roster |
| `Main/Features/HostReview/HostReviewModule.cs` | Registration; registers the order provider once per process (`ApplyPhase.ProcessLoad`) |
| `Main/Features/HostReview/Domain/HostRosterEntry.cs` | `HostRosterEntry`, `HostReviewSlot`, `HostReviewBlock` |
| `Main/Adapters/HostReviewPartyAdapter.cs` | Reads battle size and the roster (healthy count per line) |
| `Main/Features/HostReview/Hooks/HostReviewCampaignBehavior.cs` | The option (added after the session launch), greyed tooltips, failure message |
| `Main/Features/HostReview/Hooks/HostReviewMissionLauncher.cs` | Opens the mission: scene, combatants, behavior list |
| `Main/Features/HostReview/Hooks/HostReviewTroopSupplier.cs` | Feeds the lineup to the spawn logic in order |
| `Main/Features/HostReview/Hooks/HostReviewMissionLogic.cs` | No friendly fire, no dying, start message |
| `Main/Features/HostReview/Hooks/HostReviewMissionViews.cs` | The `TaomHostReview` view list |
| `Client/Features/HostReview/Hooks/HostReviewVisualOrderProvider.cs` | Makes the vanilla order bar available (TAOM.Client, ADR-013: derives from a `TaleWorlds.MountAndBlade.View` type) |
| `Main/Features/HostReview/IHostReviewOrderProviderRegistrar.cs`, `NullHostReviewOrderProviderRegistrar.cs` | The seam `HostReviewModule.OnPhase(ProcessLoad)` calls once per process; null in core |
| `Client/Features/HostReview/HostReviewOrderProviderRegistrar.cs` | Registers the provider with `VisualOrderFactory` |

## Dependencies

- `IEnlistmentStateQuery` (Enlistment): the Enlisted block.
- `ICoopSessionProvider` (CoopInterop): the co-op block.
- `FieldCampCampaignBehavior.CampSubMenuId` (FieldCamp): the menu the option joins.
- `IEncounterAdapter` (shared, registered by the Enlistment feature): the InEncounter block.
- `IHostReviewPartyAdapter` (`Main/Adapters`): wraps the main party roster and `BannerlordConfig`.

## Tests

49 tests in total.

- `TAOM.Tests/Features/HostReview/HostReviewServiceTests.cs`: 27 tests covering every block and their order (including a main hero who is wounded, absent or the roster empty), the lineup rule (main hero first, heroes before troops, wounded skipped, battle size cuts) and the `Launch()` outcomes.
- `TAOM.Tests/Features/HostReview/HostReviewTroopSupplierTests.cs`: 9 tests covering supply counts, ranks in lineup order, exhaustion and `NumRemovedTroops` being always zero.
- `TAOM.Tests/Features/HostReview/HostReviewWiringTests.cs`: 12 tests covering module listing, the behavior declaration (one `RequiresGame` test builds it), and source pins: option id, menu, index 5 and `OnAfterSessionLaunchedEvent`, `DisableDying` inside `OnAfterDeploymentFinished`, `new HostReviewMissionLogic(` in the launcher, and an explicit `ReasonText` case for every block.
- `TAOM.Tests/Infrastructure/ViewCreatorModuleGateTests.cs`: 1 `RequiresGame` test that every static method on a `[ViewCreatorModule]` class carries `[ViewMethod]`.

The mission, views and menu need the game and are covered by the in-game checks below.

## Localization

Keys in `Main/_Module/ModuleData/taom_module_strings.xml`, in the `taom_fcamp_` prefix:

- `taom_fcamp_opt_review`: the option text
- `taom_fcamp_review_start`: the on-entry message, with the `{LEAVE_KEY}` placeholder
- `taom_fcamp_review_failed`: the launch failure message
- `taom_fcamp_review_enlisted`, `_busy`, `_coop`, `_wounded`, `_few`: the five tooltips

The English of `_start`, `_coop` and `_enlisted` changed before release and the 12 language files were re-seeded with the new English. The paid translation of the 8 keys is owed.

The combatant names ("Host Review", "Host Review (empty)") use the `{=!}` form and are not translated.

## How to Change the Lineup Rule

1. Edit `BuildLineup` in `HostReviewService.cs`.
2. Update the `BuildLineup_*` tests in `HostReviewServiceTests.cs` first.
3. No other code changes are needed.

## In-game checks owed

- **SPIKE gate, first.** No exception from SandBox models with no map event; the empty attacker side neither ends the mission nor breaks deployment or team AI. Run 5 minutes with a hero of Tactics 50+ and read the logs.
- Option placement on the camp sub-menu, and the greyed tooltips for each block.
- The scene opens at the party position, in formation, with a count at most the battle size and no Order of Battle screen.
- Every order works.
- Nobody dies from melee, missiles, falls or horses.
- Holding the leave key leaves back to the camp menu.
- After leaving, the camp sub-menu clock still runs, Leave works, and a save and reload there resumes normally.
- The start message names the bound leave key on keyboard and gamepad.
- A warg, troll or mumak, and a race or career ability used beside friendly troops, harm nobody.
- A wounded companion and a stack's wounded men stay out of the scene.
- Roster, wounded, hit points and experience are unchanged afterwards.
- The next real battle still shows Order of Battle.
- No reinforcements appear with 500 troops in the party.
- Ironman greys "Exit to Main Menu".
- One non-English language shows the language rows after a restart.

## Changelog

- 2026-10-19: Host Review added (#891).

## GitHub Issue

- **Issue:** #891, Host Review
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](./enlistment.md)
- [docs/features/field-camp.md](./field-camp.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/rca-host-review-2026-10-19.md](../reviews/rca-host-review-2026-10-19.md)

<!-- backlinks-end -->
