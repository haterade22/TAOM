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

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the
history and the gate. A new trap for this area is a new row here, never a row in the always-loaded
[trap index](../../../docs/ai-includes/orientation.md)
([ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)).

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
| Hero capture | Patch `Hero.CanBecomePrisoner`; denying capture grants escape; both seams share one `DeathMark` guard | [capture](../../../docs/features/uncapturable-heroes.md) |
| `Town.LastCapturedBy` | A leader-clan stamp, not participation; score sieges from `IFiefSiegeParticipationService` | [fiefs](../../../docs/features/fief-granting.md) |
| MarriageModel | One slot: `AddModel` does not compose, so every marriage rule goes in `TaomMarriageModel` | [marriage](../../../docs/features/marriage-alignment.md) |
| Player Switcher | Register at priority 1100; reassign the player clan before removing the created hero | [switcher](../../../docs/features/player-switcher.md) |
| Settlement menus | Need an encounter; only `IEncounterAdapter.EnsureSettlementEncounter` places the player | [enlistment](../../../docs/features/enlistment.md) |
