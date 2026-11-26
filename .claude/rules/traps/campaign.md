---
paths:
  - "Main/**/*Behavior*.cs"
  - "Main/**/*Menu*.cs"
  - "Main/**/*Settings*.cs"
  - "Main/**/Cheats/**"
  - "Main/**/*Console*.cs"
  - "Main/SubModule.cs"
  - "Main/Features/Enlistment/**"
  - "Main/Features/FieldCamp/**"
  - "Main/Features/MapEventGuard/**"
  - "Main/Features/Diplomacy/**"
  - "Main/Features/UncapturableHeroes/**"
  - "Main/Features/FiefGranting/**"
  - "Main/Features/MarriageAlignment/**"
  - "Main/Features/PlayerSwitcher/**"
  - "Main/Features/NavalTravel/**"
  - "Main/Features/ShaderPrecompilation/**"
  - "Main/Features/DevConsole/**"
---

# Trap sheet: campaign C#

Campaign behaviors, ticks, menus, map events, capture, marriage, settings, console commands and parked features.

| Trap | Rule | Doc |
|---|---|---|
| Parked features | NavalTravel, ShaderPrecompilation: off in `SubModule.cs`; sweep every file the last toggle touched | [map](../../../docs/reference/feature-map.md) |
| Persisted MCM defaults | json2 keeps saved values: rename (comment why), never flip a default; `check_mcm_default_flips.py` | [shaders](../../../docs/features/shader-precompilation.md) |
| Console commands | Route through `TaomConsole`; a wrongly shaped command throws in unguarded startup discovery | [console](../../../docs/features/dev-console.md) |
| Enlisted service | Only `DischargeService` ends it; parked, or visible in the commander's settlement, is legitimate | [enlistment](../../../docs/features/enlistment.md) |
| Leaving a map event | Clearing `MainParty.AttachedTo` mid-event nulls the upgrade tracker; vanilla CTDs later | [guard](../../../docs/features/map-event-guard.md) |
| Menus stop time | Land on the map after timed work; a false WAIT condition hides every option; no `TickEvent` in a menu | [field camp](../../../docs/features/field-camp.md) |
| Daily tick after load | A save in an elapsed day's first half reruns that day: gate daily work on a saved day | [ticks](../../../docs/reference/engine/campaign-tick-time-and-party-ai.md) |
| Event listener order | Newest first: a listener added after the engine's runs before it on the same event | [ticks](../../../docs/reference/engine/campaign-tick-time-and-party-ai.md) |
| "Frozen" game | Can be a stuck kingdom vote waiting on an `IsKingsDecisionOver` edge: `Patch80` | [diplomacy](../../../docs/features/diplomacy.md) |
| Kill-path listeners | `OnPartyDisbandStarted` and other events a battle death fires run inside the engine's roster loop, which Patch169 snapshots; never `GetTroopRoster()` on that party, read by index | [guard](../../../docs/features/map-event-guard.md) |
| Hero capture | Patch `Hero.CanBecomePrisoner`; denying capture grants escape; both seams share one `DeathMark` guard | [capture](../../../docs/features/uncapturable-heroes.md) |
| Hero states | `Fugitive`, `Released` and `Traveling` end by themselves (one daily line settles the first two, and skips a Special hero): never give one a final verdict, never write "the engine never" from one method | [messengers](../../../docs/features/messengers.md) |
| `Town.LastCapturedBy` | A leader-clan stamp, not participation; score sieges from `IFiefSiegeParticipationService` | [fiefs](../../../docs/features/fief-granting.md) |
| MarriageModel | One slot: `AddModel` does not compose, so every marriage rule goes in `TaomMarriageModel` | [marriage](../../../docs/features/marriage-alignment.md) |
| Player Switcher | Register at priority 1100; reassign the player clan before removing the created hero | [switcher](../../../docs/features/player-switcher.md) |
| Settlement menus | Need an encounter; only `IEncounterAdapter.EnsureSettlementEncounter` places the player | [enlistment](../../../docs/features/enlistment.md) |
| Popups and menu hotkeys | Escape runs an inquiry's option B and a menu hotkey its first leave option: never a saved choice; a popup button rechecks the map | [lessons](../../../docs/reviews/lessons/localization-ui.md) |
| Popup pause | Every `ShowInquiry`, `ShowTextInquiry` and `ShowMultiSelectionInquiry` that can open over the map passes `pauseGameActiveState: true` by name, or the party keeps moving under it; `PopupPauseGateTests` allow-lists the mission and main-menu only calls | [gate](../../../TAOM.Tests/Infrastructure/PopupPauseGateTests.cs) |
| Map path region costs | Keep both region-switch costs at 0: a nonzero cost takes the region-map path search, which faults without NavalDLC | [naval](../../../docs/features/naval-travel.md) |
| Options on a vanilla menu | Add them from an `OnSessionLaunched` listener, never `OnGameStart`: `UnregisterNonReadyObjects` drops the uninitialised menu | [events](../../../docs/reference/engine/campaignevents-and-campaignbehavior.md) |
