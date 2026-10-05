# Siege Forces

## Overview

Before a wall battle the player chooses which of his troops fight, on vanilla's "Manage Troops" screen
(the one the lord's hall fight already uses). Troops he leaves out stay in reserve: no agent spawns for
them, they take no losses in that battle, and they are still in the party when it ends. Trolls start
unticked, because they cannot climb ladders, ride towers or take wall stairs. The choice is offered as
attacker and as defender.

## Why This Exists

- **Vanilla behavior:** every healthy troop of the player's side can be sent into a wall battle. Hideouts
  and the lord's hall let the player choose; the wall battle does not.
- **TAOM requirement:** cave trolls and hill trolls cannot use siege ladders or towers. In a wall battle
  they stand idle, clog the ladder queues and take spawn slots from troops that can fight. The player
  needs a way to leave them, or anyone else, in camp.
- **Without this feature:** trolls in the player's party or army waste the battle-size budget of every
  assault, and the player can only avoid it by dismissing them before the siege.

[Creature Siege Role](creature-siege-role.md) (#735) is the companion feature: it decides what trolls
do when they do fight in a wall battle. The two share one definition of an oversized creature,
`OversizedCreatureRaces`, so they cannot drift apart.

## What the player sees

1. He clicks Attack in a siege (as attacker), or fights off an assault on a settlement he defends.
2. The "Manage Troops" screen opens with one row per troop type, summed over every party in scope. The
   player himself is ticked and locked. Trolls start at 0. Everyone else healthy starts ticked; the
   wounded are listed but cannot be ticked.
3. **Done** opens the battle with the ticked troops. **Cancel** opens no battle and leaves the same state
   that cancelling vanilla's lord's hall screen leaves (RG-M checks it in game).

Who counts:

| Situation | Parties in scope, in fill order |
|---|---|
| Player's own party, no army | The main party |
| Player leads an army | The main party, then every party of his army |
| Player is in another lord's army | His own party only; the other parties fight as vanilla sends them |
| Player defends a fief his clan owns | The main party (and army, if he leads one), then the garrison (D1). Militia stays out of scope |

The fill order matters when the same troop type comes from more than one party: each party keeps
`min(healthy, still picked)` of that type in that order, so the player's own troops fight first (D2).

## Architecture

### Design Challenge

- The engine builds each side's ready list in `MapEventSide.MakeReady`, while the mission's behaviours
  are created, and the spawn logic then expects every listed troop to spawn. Removing troops after the
  list is built, or after spawn, leaves deployment waiting for troops that never come.
- `InitWithSinglePhase` receives the side's total and initial spawn computed from the involved-men
  count, not from the ready list. If the totals are not shrunk by the troops left out, deployment
  deadlocks (v1.5.3 `DefaultBattleMissionAgentSpawnLogic.CheckDeployment` :539-547).
- `InitWithSinglePhase` runs for every battle, Custom Battle included, where `MobileParty.MainParty`
  throws because there is no campaign. A fault in that prefix would skip the original, leave no spawn
  phases and throw every tick.

### Solution Approach

Three seams, all on the main thread:

| Step | Seam | What it does |
|---|---|---|
| Offer | `Patch102_StartSiegeMissionPicker`, a prefix on `PlayerSiege.StartSiegeMission(Settlement)` | Asks the service to offer the picker. If the screen opened it returns false; vanilla's mission open waits for Done. Any fault returns true, so vanilla runs |
| Done | `SiegeForcesService` | Builds the plan, arms it, then calls the public `PlayerSiege.StartSiegeMission(settlement)` again under a `[ThreadStatic]` latch, so the prefix steps aside and vanilla runs whole; disarms in a `finally`. Nothing vanilla does is copied |
| Exclude | `TaomTroopSupplierProbabilityModel` (campaign `TroopSupplierProbabilityModel` slot) | Notes the ready list's length, calls the base model, then hands the service a window over what the base appended. While armed, the service removes the left-out entries, highest index first, and records how many it dropped and the side's final list size. Of each troop type a party keeps the entries at the END of its window, the highest priority ones (the engine appends lowest priority first, then sorts the side's list highest first) |
| Fit | `Patch102_SpawnTotalsFit`, a prefix on `DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase` | Shrinks the player side's total by the dropped count, caps it at the recorded ready-list size, and clamps the initial spawn to the new total. Applied once, only to the battle and side the record was made for |

The fit's guard order is the contract. The prefix first asks the service whether a record is pending,
a pure read of TAOM state; with none pending, which is every Custom Battle and field battle, no engine
state is read at all. The map event is then read null-safe (`Campaign.Current?.MainParty?.MapEvent`), a
mismatch of map event or side discards the record, and the whole body sits in a try that leaves the
four ints untouched. Neither prefix body names an engine member: the engine work sits in
`[MethodImpl(NoInlining)]` helpers that compile on their first call, inside the try. See the
[Patch102 registry entry](../reference/harmony-patch-registry.md#patch102_siegeforces).

The picker is offered only while the fit is attached. `Patch102_SpawnTotalsFit` is declared first in
`Patch102_SiegeForces.cs`, because `PatchCategoryIndex` applies a category's classes in assembly order
and a class that fails stops the rest, so a failed fit leaves the entry patch unapplied
(`SiegeForcesWiringTests` pins the order). As a second line, the service asks the adapter once per
process, at the first offer, whether TAOM's prefix is attached to `InitWithSinglePhase`
(`Harmony.GetPatchInfo`), and offers nothing when it is not.

### Component Diagram

```
siege/siege_forces.json        MCM "Battle Tactics/Siege Forces"
        |                                  |
SiegeForcesConfigProvider      SiegeForcesSettingsProvider
         \                                /
          +------ SiegeForcesService ----+---- SiegeForcesRules (pure)
          |      (offer, arm, filter, fit)       OversizedCreatureRaces (shared)
          |
   +------+-------------------+-------------------------------+
   |                          |                               |
Patch102_StartSiegeMission  TaomTroopSupplierProbabilityModel  Patch102_SpawnTotalsFit
Picker (prefix)             (GameModel, campaign)              (prefix)
   |                          |
SiegeForcesAdapter          ReadyListWindow (IReadyListWindow)
(snapshot, picker, token)   (one party's slice of the ready list)
```

### State

| What | Where | Lifetime |
|---|---|---|
| `_resuming` | `[ThreadStatic]` in Patch102 | One synchronous re-entry, in a try/finally |
| The armed plan | Service | Set around the vanilla mission open, cleared in a `finally` |
| Dropped count per party, ready-list size per side | Service | Cleared on every arming; written by overwrite, so a second `MakeReady` stays exact |
| The pending fit (map event, side, dropped, ready count) | Service | Consumed by the next `InitWithSinglePhase`; applied only on a match. Every offer drops one still waiting |

Nothing is persisted and nothing is per campaign. A pending fit names its map event by reference, so no
battle of a later session can match a stale record.

## Decisions

| # | Decision |
|---|---|
| D1 | The own-clan garrison is in scope when the player defends his own fief. Militia stays out |
| D2 | Fill order: main party, then army, then garrison |
| D3 | No picker in a live co-op session, when the host decides for this peer (`ShouldDeferToHost`, which also covers co-op mods TAOM cannot probe), or on a dedicated server. Each is read live at the offer |
| D4 | Reserves of a troop type that fought share that type's XP. `MapEventParty.CommitXpGain` works per `CharacterObject` (v1.5.3 :404-465), so "reserves earn nothing" would need a second seam. Accepted (hideout parity) |
| D5 | When even one troop is left out, a lost wall battle continues the siege instead of ending in capture (the hideout and wave-cap behaviour). Accepted |
| D6 | When 20 or more troops are healthy and fewer than 20 are picked, vanilla still opens the order-of-battle screen (`SandboxBattleInitializationModel` :80). Cosmetic, accepted |

## Edge Cases

| Scenario | Handling |
|---|---|
| Custom Battle or Custom Siege, also right after a campaign siege whose mission never reached `AfterStart` | No pending record, so no engine read; a stale record is dropped by the next offer, or discarded on a map-event mismatch |
| Another mod owns the troop supplier model slot ([TOR_Core](../reference/provenance-register.md) registers its own) | Nothing is dropped and nothing subtracted. One WARNING when the mission opens without the model filtering the player's parties |
| The fit prefix is not attached to `InitWithSinglePhase` (it failed to apply, or another mod stripped it) | No picker is offered, one WARNING at the first offer; vanilla opens the mission with every troop |
| The fit throws at runtime (an engine member stopped resolving) | The four totals stay untouched and one WARNING says the spawn totals were not fitted and deployment may stall. The list was already filtered, so this is not vanilla's behaviour |
| A hero healed inside a continued map event (counted in the involved men, absent from the ready list) | The ready-list cap on the total prevents the deadlock, but only when the selection left someone out: with nobody left out no fit is recorded and the totals stay vanilla's |
| Defender presses Escape mid-battle | Vanilla's surrender: the whole side surrenders, reserves included. The MCM hint says so |
| Picker cancelled; saved while open | Cancel arms nothing; vanilla's own lord's hall cancel leaves the same state |
| Nothing to choose (the player alone, or enlisted with no troops) | No picker; vanilla runs |
| Sally-out, relief force, lord's hall | Never reach `StartSiegeMission`'s wall branch; vanilla |
| Troll-race player, Player Switcher | The player row is locked by `!IsPlayerCharacter`, counted once, never dropped |
| Selection larger than the battle size; reinforcement waves | Only the kept troops exist in the ready list; vanilla's wave cap applies |
| The mission open throws after something was left out | The pending fit is still recorded; its map-event token and the next offer's reset neutralise a stale record |
| MCM toggled mid-battle | The switch gates only the offer. A Done after it was switched off still applies the selection |
| Repeated assault | A fresh picker with fresh defaults; nothing is remembered |

## Configuration

### MCM: `Battle Tactics/Siege Forces`

| Setting | Default | Notes |
|---|---|---|
| Choose Troops Before Wall Battles (`EnableSiegeTroopPicker`) | On | Read live at each assault, `RequireRestart = false`. Off: every troop fights as in vanilla |

### Config File: `Main/_Module/ModuleData/siege/siege_forces.json`

| Field | Type | Description |
|---|---|---|
| `startOversizedUnticked` | bool | True: troops of an oversized race (`cave_troll`, `hill_troll`) start unticked. The player can still tick them |

```json
{
  "startOversizedUnticked": true
}
```

This is JSON rather than MCM on purpose: MCM keeps a saved value in its own file, so a default flipped in
a later TAOM release would never reach an existing install (trap index: persisted MCM defaults).

Validation: a missing file, text that is not JSON, a missing key or a value that is not a JSON boolean
(the strings `"false"` and the number `0` are refused) falls back to `true` with one WARNING. The provider
is a singleton that reads the file once per process, so **an edit needs a game restart**; a new campaign
or a reload does not reread it.

### Current Values

`startOversizedUnticked: true`. The oversized race list itself is code, not data:
`OversizedCreatureRaces` takes its names from the `TrollBruteForceConfig` constants.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/SiegeForces/SiegeForcesModule.cs` | Registrations, the `Patch102_SiegeForces` category at GameInit, the model declaration; the overlap census with other mods |
| `Main/Features/SiegeForces/SiegeForcesService.cs` | Gates, offer, arming, filtering, the spawn fit and its state |
| `Main/Features/SiegeForces/SiegeForcesRules.cs` | Pure rules: parties in scope, the picker request, the plan, `Keeps`, `FitTotals` |
| `Main/Features/SiegeForces/Domain/OversizedCreatureRaces.cs` | The shared oversized race set, validate-before-lookup |
| `Main/Features/SiegeForces/Domain/SiegeForces{Snapshot,Picker,Plan}.cs` | Snapshot, picker request and plan records |
| `Main/Features/SiegeForces/Models/TaomTroopSupplierProbabilityModel.cs` | The exclusion seam |
| `Main/Features/SiegeForces/Hooks/Patch102_SiegeForces.cs` | The two prefixes and their `NoInlining` helpers; the fit class is declared first on purpose |
| `Main/Features/SiegeForces/SiegeForces{Config,Settings}Provider.cs` | JSON switch and MCM switch |
| `Main/Adapters/{I,}SiegeForcesAdapter.cs` | Wall battle snapshot, the picker call, the map-event token (null-safe), whether the fit prefix is attached |
| `Main/Adapters/{I,}ReadyListWindow.cs` | One party's appended slice of a side's ready list |
| `Main/_Module/ModuleData/siege/siege_forces.json` | The switch |

## Dependencies

- `IRaceManager` (Core): race ids and names for the oversized test.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the D3 gates.
- `IPathService`, `IModLogger` (Core).

## Tests

All under `TAOM.Tests/Features/SiegeForces/`:

- `SiegeForcesRulesTests.cs`: scope, fill order, request defaults, plan mapping, `Keeps`, `FitTotals`
  with the ready-list cap and the hero-drift case.
- `SiegeForcesServiceTests.cs`: no pending record means no adapter call; map-event or side mismatch
  discards; a token-read fault leaves the totals alone; the WARNING when no filtering happened; a
  plan-build fault opens vanilla; a mission-open fault disarms and rethrows.
- `SiegeForcesConfigProviderTests.cs`, `ShippedSiegeForcesConfigTests.cs`,
  `SiegeForcesSettingsProviderTests.cs`: one test per validation rule; the shipped file parses.
- `OversizedCreatureRacesTests.cs`: the race names, and `ResolveRaceIds` validate-before-lookup, including the
  fallback-to-human regression; `SiegeForcesRulesTests` pins that a request resolves the ids once.
- `ReadyListWindowTests.cs`, `SiegeForcesAdapterTests.cs`, `Patch102PrefixTests.cs`.
- `SiegeForcesBindingTests.cs` (`RequiresGame`): both targets, the prefixes' parameter names (Harmony
  binds the four ints by name), and every engine member the helpers, adapters and model reference.
- `SiegeForcesWiringTests.cs`: module listed, model declared for the campaign, category at GameInit, the
  fit class applied before the entry class in `PatchCategoryIndex`'s order, and an `IlCallScanner` pin that
  the model calls its base before `FilterAppended`.
- `CoopVetoClassificationTests.cs` classifies `Patch102_StartSiegeMissionPicker` as reviewed safe.

## Logging

All lines carry `[SiegeForces]`:

| Level | Line | Means |
|---|---|---|
| INFO | `offering the picker: N party(ies), N row(s), up to N troop(s)` | The screen is about to open |
| INFO | `fit: attacker total X -> Y, initial A -> B (left out N, ready list R)` | The spawn totals were shrunk for this battle |
| WARNING | `the mission opened without the troop supplier model filtering the player's parties` | Another mod owns the model slot; every troop fights |
| WARNING | `picker not offered (...)` | A fault before the screen opened; vanilla opens the mission |
| WARNING | `the spawn totals fit is not attached to InitWithSinglePhase, so the picker is not offered` | The fit prefix is missing; vanilla opens the mission |
| WARNING | `the spawn fit could not be checked (...)` | The attach check faulted; it counts as not attached, so no picker |
| ERROR | `could not read the spawn logic's patches (...)` | The adapter could not read Harmony's patch info; the fit counts as not attached |
| WARNING | `a ready list could not be filtered (...); that party may be only partly filtered` | A fault while filtering: before any removal nothing was removed, part way through some entries were |
| WARNING | `Patch102: spawn totals not fitted (...); deployment may stall` | The fit prefix faulted; the totals are vanilla's, but the list is already filtered |
| DEBUG | `dropped a pending fit that belongs to another battle` | A stale record was discarded |

## Engine compatibility (v1.5.4)

The design was researched on v1.5.3; Steam moved the installed game to v1.5.4 on 2026-10-05. Every
signature Part 1 uses was compared in the v1.5.4 decompile and is unchanged, and every line the code
comments cite sits at the same line number in v1.5.4. `SiegeForcesBindingTests` runs against the
installed v1.5.4 DLLs. The repo still pins v1.5.3 (`.claude/pinned-game-version.txt`); no engine bump
was made for this feature.

## How to change which troops start unticked

1. To stop trolls starting unticked, set `startOversizedUnticked` to `false` in
   `Main/_Module/ModuleData/siege/siege_forces.json` and restart the game.
2. To add a race to the oversized set, add its monster id to `OversizedCreatureRaces.Names`. This also
   changes [Creature Siege Role](creature-siege-role.md), which keys on the same set. Code change and
   tests required.

## How to verify in game

Owed; nothing below has been run yet. Desktop, Debug build, campaign:

1. Lead an assault with trolls in your party and in a vassal's party: one aggregated list, trolls at 0,
   the player locked.
2. A 500+ troop siege with a small selection: deployment completes. The log shows `total X -> Y`.
3. Win: the reserves are intact on the party screen.
4. Lose with reserves left: the battle continues, the siege stays on the walls, no capture, and the
   picker opens again.
5. Defend your own castle: garrison rows are present.
6. Lose a defence with reserves: the battle continues.
7. As defender, press Escape mid-battle: the surrender prompt appears.
8. In an AI lord's army: your own party only; with no troops, no picker.
9. Cancel, then Attack, Send troops and Leave: no stuck menu (RG-M).
10. Pull back into the lord's hall: vanilla's picker.
11. Sally-out and relief force: no picker.
12. MCM off, and toggled mid-battle.
13. A troll-race player.
14. Save and reload; a new campaign in the same process.
15. A Custom Siege after a campaign siege in the same process: no exception.
16. A selection under 20 with 20 or more healthy: the order-of-battle screen opens (cosmetic, D6).
17. The next field battle spawns normally.

## Research gates

| Gate | Question | Status |
|---|---|---|
| RG-K | Does another mod patch the three targets? | Done 2026-10-05, read-only: a byte scan of every module DLL and a source read of Bannerlord Coop found no conflicting patch (the census is in `SiegeForcesModule`'s summary). TOR_Core competes for the model slot only |
| RG-M | The cancel path, and the menu after a defeat | Owed, in game (checks 4 and 9 above) |

## Changelog

- 2026-10-05: first build on `feat/siege-forces` (#734). Picker, exclusion model, spawn fit, MCM switch,
  JSON switch. Not yet seen in game.

## GitHub Issue

- **Issue:** #734, [Siege forces: choose which troops go into a siege wall battle](https://github.com/haterade22/TAOM/issues/734)
- **Status:** Open (in-game check owed)
