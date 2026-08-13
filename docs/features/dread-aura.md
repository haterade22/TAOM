# Aura of Unnatural Dread

## Overview

The Nazgûl and Sauron drain the morale of enemy troops standing near them on the battlefield.
Hold a line in front of a Ringwraith and it breaks: morale falls, the front rank panics, and
vanilla's own contagion carries the rout outward. Elves and dwarves resist, veterans and heroes
resist, and an elven hero never breaks at all.

Battle scale only. There is no campaign-map effect, by decision (see "Out of scope").

## Why This Exists

TAOM had no battle-scale morale code at all, and `BattleMoraleModel` was an unclaimed Tier 2 item
on [`docs/roadmap.md`](../roadmap.md) ("Racial fearlessness (Undead), cultural bravery"). More
importantly the wraiths fought as ordinary heavy infantry: nothing in the game expressed that
their weapon is fear rather than a sword.

## Architecture

```
SubModule.OnMissionBehaviorInitialize
   └── DreadAuraMissionLogic : MissionLogic      ← gate, schedule, stand down on failure
          ├── DreadMissionGate                    ← campaign field battle / siege / sally-out only
          ├── DreadSourceTracker                  ← who on the field projects dread
          ├── DreadPulseScheduler                 ← round-robin; which sources pulse this frame
          └── DreadPulseRunner                    ← one pulse: query, then drain each target
                 ├── DreadAgentGate               ← who may be affected at all
                 ├── IDreadAuraService            ← the drain arithmetic (pure)
                 ├── IDreadAuraSettingsProvider   ← radius / rate, read LIVE per pulse
                 ├── IDreadRegistry               ← source identity + resist table
                 └── BattleMoraleModel (CALLED, not overridden)
```

**No GameModel override.** This is the design's central choice and it is deliberate. See
"Rejected seams" below.

On a staggered pulse, for each dread source the logic calls
`Mission.GetNearbyEnemyAgents(pos, radius, sourceTeam, buffer)` and then
`agent.ChangeMorale(-drain)` on each eligible target. That is byte-for-byte the shape of vanilla's
own `AgentMoraleInteractionLogic`, which already runs in every campaign battle.

**The aura writes morale and nothing else.** Panic, retreat, the formation flipping to `Flee` past
30% retreating, and the morale contagion that spreads it are all the engine's, reached through the
same `CommonAIComponent.Morale` field vanilla drives. Calling `Panic()` or `Retreat()` directly
would bypass `MissionAgentPanicHandler`'s `OnBatchUnitRemovalStart/End` bracket and thrash
formation arrangement.

## Identifying a dread source: two axes, and why one is not enough

**The Nazgûl are not identifiable by race.** Verified 2026-08-13 against TAOM's shipped data:

| Hero | `race` in TAOM data |
|---|---|
| Sauron (`lord_1_17`) | `sauron` |
| Witch-King (`lord_1_15`), `lord_1_155`, `lord_1_16`, `lord_1_28`, `lord_1_38` | **none**, inherits vanilla race 0 (human) |
| Khamûl (`lord_1_48`) | `orc` |
| `lord_1_48_1` / `_2` / `_3` | `uruk` |

`race="nazghul"` appears **nowhere** in TAOM's data, even though the `nazghul` race exists in the
FaceGen registry (referenced by `banner_bearers_config.json` and `raceage/race_age_config.json`).

So a race-keyed source list would have emitted **zero auras for eight of the Nine**, parsed
cleanly, logged nothing, and looked like a working feature. Adding `orc` to catch Khamûl would
have handed an aura of dread to every orc in the game.

Identity is therefore a two-axis OR, the same shape `TaomCombatMechanicsModel` already uses:

- **Hero StringId**: via `heroSets: ["nazgul_nine"]`, resolving to the existing
  [`INazgulRegistry`](../../Main/Features/NazgulFamily/NazgulRegistry.cs), plus explicit
  `heroIds: ["lord_1_17"]` for Sauron. This is the axis that finds the Nine.
- **FaceGen race**: `races: ["sauron"]`. Sauron is the only dread-bearer this axis can find, and
  he is listed on both so the feature survives a data change that drops his race attribute.

`ShippedDreadAuraConfigTests.ShippedConfig_KeepsTheNazgulHeroSet` pins this, because a
well-meaning "simplify to a race list" refactor is otherwise silent.

## The arithmetic

```
D_eff = moralePerSecond
      × falloff(distance)                     // 1.0 within innerRadius, linear to 0 at radius
      / max(1, GetMoraleResistance())         // vanilla tier/hero curve, via the model call
      × raceResist[targetRace]                // elf 0.4, dwarf 0.5, otherwise 1.0
```

`CharacterObject.GetMoraleResistance()` is `(IsHero ? 1.5 : 1) * (0.5 * tier + 1)`.

The engine regenerates morale at **+0.4/sec** up to **half** a troop's starting morale
(`CommonAIComponent.OnTickParallel`). Above that ceiling there is no pushback at all; below it the
drain must beat 0.4/sec or the target parks at the ceiling forever.

```
t_rout = R / D_eff  +  R / (D_eff − 0.4)      where R = half of initial morale (about 24.75)
                                              valid only for D_eff > 0.4
```

At the shipped `moralePerSecond = 5.0`, inside the inner radius:

| Target | resistance | D_eff | time to rout |
|---|---|---|---|
| Tier-1 levy | 1.5 | 3.33 | **16 s** |
| Tier-3 line infantry | 2.5 | 2.00 | **28 s** |
| Tier-6 elite | 4.0 | 1.25 | **49 s** |
| Tier-3 elf | 2.5 × 0.4 | 0.80 | **93 s** |
| Tier-5 dwarf | 3.5 × 0.5 | 0.71 | **113 s** |
| Tier-6 elf hero | 6.0 × 0.4 | **0.33** | **never** |

The last row is the point. **Elven heroes are immune to the Witch-King by arithmetic**, with no
special case anywhere in the code: their effective drain falls below the engine's own regeneration
rate. That `D_eff > 0.4` break-even *is* the resistance mechanic, not a defect in it.

A 12 m radius covers most of a shieldwall at once, so "28 seconds" reads in play as the whole front
rank going together rather than as a trickle of individuals.

These numbers are the balance contract. They are asserted three times and all three must move
together: `DreadAuraServiceTests.ComputeDrain_ShippedRate_RoutsAtTheDocumentedTime` simulates the
real engine morale loop against the real service, `ShippedDreadAuraConfigTests` pins the four
config values, and this table documents them.

## Who cannot be affected

`DreadAgentGate.CanAffect` is the single chokepoint. It requires the agent to be non-null,
active, human, not a mount, AI-controlled, to have a `CommonAIComponent`, and to report
`GetMorale() >= 0f`.

**The player is structurally immune, and that is correct.** `AgentCommonAILogic.OnAgentCreated`
attaches `CommonAIComponent` only when the agent is AI-controlled, so a player-controlled agent has
no morale at all and `GetMorale()` returns the sentinel `-1f`. Writing to one of those does not
crash: `AgentComponentExtensions.ChangeMorale` null-checks the component and silently no-ops. The
gate exists to keep the sentinel out of the arithmetic, where it must read as "no morale", never as
"already drained". **Do not "fix" this.** A future change that gives the player morale would need
its own design pass, not a relaxed guard here.

Two clauses in that gate are load-bearing for reasons worth stating. `!agent.IsMount`: mounts ARE
AI-controlled and DO carry the component, so without it a wraith would drain the enemy's horses.
`agent != null`: `Mission.GetNearbyAgentsAux` adds its
`DotNetObject.GetManagedObjectWithId(id) as Agent` result unconditionally, so a reclaimed id
arrives in the caller's buffer as a null entry.

Allies are never candidates: `GetNearbyEnemyAgents` filters them out native-side. That is also why
"a wraith's own orcs are unaffected" needs no explicit guard.

## Mission types

`DreadMissionGate.IsEligible` is an **allowlist**, re-read every tick:

- `Campaign.Current != null` excludes custom battle, main menu, multiplayer maps
- `!GameNetwork.IsSessionActive` excludes multiplayer
- `CombatType == Combat` excludes arenas and tournaments (`ArenaCombat`) and conversations and
  town walkarounds (`NoCombat`)
- `IsFieldBattle || IsSiegeBattle || IsSallyOutBattle`

`MissionTeamAITypeEnum` is `{ NoTeamAI, FieldBattle, Siege, SallyOut, NavalBattle, NavalRaid }`, so
**hideouts and arenas are `NoTeamAI`** and the allowlist excludes them by construction.
`DreadAuraBindingTests.MissionTeamAiTypes_StillLackAHideoutOrArenaMember` pins that reasoning, so
if a future engine adds a dedicated member the allowlist gets re-derived rather than silently
losing its exclusion.

**Never cache the gate at init.** `MissionTeamAIType` is assigned in
`MissionCombatantsLogic.EarlyStart`, which runs *after* every `OnBehaviorInitialize`, so an
init-time snapshot reads `NoTeamAI` and disables the feature in every mission. The same trap is
documented at `SmartCavalryAIMissionBehavior.cs:63-68`.

A wraith entrant draining a Minas Morgul tournament would have routed the other entrants inside an
arena with no retreat position, which is the failure family
[`rca-tournament-exit-hang-2026-07-06.md`](../reviews/rca-tournament-exit-hang-2026-07-06.md)
already covers.

## Rejected seams

**`GetEffectiveInitialMorale`** is the obvious hook, and it is wrong. It fires once inside
`CommonAIComponent.Initialize()`, when every agent is still in its deployment box, so it cannot be
positional. Worse, it sets `_initialMorale`, and `_recoveryMorale = _initialMorale * 0.5f` derives
from it: a wraith killed thirty seconds in would leave the enemy army permanently capped at a
lowered recovery ceiling for the rest of the battle, invisible and unattributable. It is also
unnecessary, since the tick drain clears the +0.4/sec regeneration on its own.

**`CalculateMoraleChangeToCharacter`** is not overridden, but **called**. The caller applies the
sign (`AgentMoraleInteractionLogic.cs:110` negates it, `:115` does not), so an override shaping it
for "elves resist dread" would equally shrink the morale elves *gain* from kills, and would apply
to every unrelated vanilla morale event. Calling it instead is how tier and hero resistance reach
the aura at exact parity with how the engine scales kill-morale, and it means no TAOM code can
recurse into itself.

**`CanPanicDueToMorale`** (fearless undead) is out of v1, not asked for. Worth knowing before adding
it: a fearless agent clamps at `0.01f`, and `BattleEndLogic` routs on `< 0.01f`, so fearless agents
would never be counted as routed in campaign casualty accounting.

**The `AgentProximityMap` radius clamp** was designed in, then removed after reading
`Mission.GetNearbyAgentsAux` (Mission.cs:2314). It is pure native paging with no proximity-map
fallback, so the silent degradation to a full agent scan applies only to
`AgentProximityMap.BeginSearch`, which this feature does not use.

**`IBattlefieldQueryAdapter.GetNearbyAgents`**: its `NearbyAgentSnapshot` carries no race, no
morale, and no write path, and it wraps `GetNearbyAgents` rather than the native-filtered
`GetNearbyEnemyAgents`. Extending a read-only snapshot type into a write-back handle for one caller
was the worse trade.

## Configuration

`Main/_Module/ModuleData/dread_aura/dread_aura_config.json`, validated per field by
`DreadAuraConfigProvider` (revert + warn, plus one summary warning). Every float goes through
`FiniteFloatValidator` before its range check.

| Field | Default | Valid range |
|---|---|---|
| `enabled` | true | |
| `pulseIntervalSeconds` | 0.25 | [0.1, 5.0] |
| `maxSourcesPerFrame` | 2 | [1, 16] |
| `fearVoiceChancePerPulse` | 0.02 | [0, 1] |
| `profile.radius` | 12.0 | [0.1, 30] in JSON; the MCM slider is [4, 30] and its value is re-clamped to that |
| `profile.innerRadius` | 4.0 | [0, radius], ordering invariant |
| `profile.moralePerSecond` | 5.0 | [0, 50] in JSON; an MCM value is re-clamped to the slider's [0, 20] |
| `profile.moraleFloor` | 0.0 | [0, 50] |
| `raceResist[*]` | elf 0.4, dwarf 0.5 | [0, 1] per entry, above 1 dropped |

`moraleFloor: 0` lets the aura alone drive a troop to the engine's 0.01 panic threshold. Raise it
to make dread soften troops while leaving the kill to combat, without a code change.

Above 1.0 a resist value would *amplify* dread on a race the author meant to protect, so those rows
are dropped with a warning rather than clamped.

Race names and hero ids are **not** validated at load: the FaceGen registry is not populated then.
`DreadRegistry` validates race names lazily on first resolve and skips + warns per entry; hero ids
are pinned instead by `ShippedDreadAuraConfigTests`.

**MCM** (`Aura of Dread` group): `EnableDreadAura`, `DreadAuraRadius` (4-30),
`DreadAuraMoralePerSecond` (0-20), `DreadAuraAffectsPlayerTroops`. The radius ceiling is enforced
in **both** the JSON provider and the settings provider, because MCM's slider bounds are UI-only
metadata and its deserializer assigns without a range check. CombatMechanics shipped exactly that
drift on 2026-07-02.

Toggling the MCM off mid-battle stops further drain immediately. It does **not** restore morale
already lost; the hint text says so. Re-enabling mid-battle resumes at the normal rate: the
scheduler caps how much elapsed time one pulse may carry, so an off-window is never delivered as a
single catch-up burst.

**The MCM sliders are live.** Radius and rate are read once per pulse from the settings provider,
never snapshotted onto a tracked source, so dragging a slider affects wraiths already on the field
rather than only ones that spawn afterwards.

**Editing the JSON needs a full Bannerlord restart.** `DreadAuraConfigProvider` is
`Reuse.Singleton` behind a `Lazy`, so the file is read once per process. A new campaign or a
save-load will not pick up an edit. The MCM knobs are the ones that take effect immediately.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/DreadAura/DreadAuraService.cs` | The drain arithmetic. Pure, engine-free, 100% tested |
| `Main/Features/DreadAura/DreadRegistry.cs` | Two-axis source identity + the resist table, one lazily-built id-keyed map. Deliberately does NOT consult the master toggle |
| `Main/Features/DreadAura/DreadAuraConfigProvider.cs` | Validating boundary loader |
| `Main/Features/DreadAura/DreadAuraSettingsProvider.cs` | MCM over validated JSON |
| `Main/Features/DreadAura/Hooks/DreadAuraMissionLogic.cs` | Entry point: gate, schedule, lifecycle |
| `Main/Features/DreadAura/Hooks/DreadMissionGate.cs` | Mission-type allowlist |
| `Main/Features/DreadAura/Hooks/DreadAgentGate.cs` | Per-agent eligibility, incl. the `-1f` no-morale sentinel |
| `Main/Features/DreadAura/Hooks/DreadSourceTracker.cs` | Which agents on the field project dread |
| `Main/Features/DreadAura/Hooks/DreadPulseScheduler.cs` | Round-robin, budget, per-source elapsed time (pure) |
| `Main/Features/DreadAura/Hooks/DreadPulseRunner.cs` | Executes one pulse: reads live geometry, proximity query, morale write |
| `Main/Features/DreadAura/Domain/` | `DreadAuraConfig`, `DreadDrainContext` |
| `Main/_Module/ModuleData/dread_aura/dread_aura_config.json` | Shipped config |
| `Main/IoC.cs` | `DreadAuraIoC.RegisterDreadAuraFeature` |
| `Main/SubModule.cs` | `AddTaomBehavior(new DreadAuraMissionLogic())`, no `AddModel` counterpart |

## Tests

161 tests, all green as part of a 6,615-test suite.

- `DreadAuraServiceTests` (51): falloff geometry, the golden rout-time table, the morale floor, the
  `-1f` no-component sentinel, and a NaN / ±Infinity case for every float that reaches a decision.
- `DreadAuraConfigProviderTests` (43): one test per validation rule, plus an every-field round-trip
  and the `ObjectCreationHandling.Replace` regression.
- `DreadRegistryTests` (25): both identity axes, `PlainOrcTroop_IsNotSource`, unknown config keys
  skipped and warned, the lazy map built once.
- `DreadPulseSchedulerTests` (16): the rotation starves nobody, the budget holds, a selected source
  is not picked twice in a frame, a NaN interval selects nothing, and a long skipped window is
  clamped to the catch-up ceiling instead of arriving as one burst.
- `ShippedDreadAuraConfigTests` (11): the shipped file parses with zero rejections and keeps the
  balance contract and the hero-set link.
- `DreadAuraBindingTests` (8): engine members resolve; `MissionLogic` is the base class.
- `DreadSourceTrackerTests` (7): null handling, the dedup contract, and the mission-clock seed.

**Mutation-checked.** Disabling each load-bearing rule reddens tests: the morale floor (2), the
radius NaN gate written as a positive requirement (2), the race-resist multiplier (3), the
per-source elapsed integration (6). An earlier `IsValidRaceId` guard reddened **zero** and was
removed rather than shipped: the lookup tables are keyed by race *id*, so `GetRaceNameFromId`'s
"human" fallback is structurally unreachable and the guard defended a path that does not exist.
The `DidNotReceive().GetRaceNameFromId` assertions pin that design against a name-keyed rewrite.

## Engine constants this feature is balanced against

Signature drift is caught offline by `DreadAuraBindingTests`. **Behaviour** drift in these constants
is not, so they are recorded here and the control battle is an `/engine-bump` checklist item.
Verified against v1.4.8:

| Constant | Value | Source |
|---|---|---|
| Initial morale | `35 + rand[0..29]`, clamped [15, 100] | `CommonAIComponent.InitializeMorale` |
| Recovery ceiling | `_initialMorale * 0.5f` | `CommonAIComponent.cs:74` |
| Morale regeneration | +0.4/sec, only below the ceiling | `CommonAIComponent.OnTickParallel` |
| Panic threshold | `_morale < 0.01f`, tested **before** regeneration | `CommonAIComponent.OnTickParallel` |
| Formation flee | above 30% of a formation retreating | `Formation.cs:1146`, `Mission.cs:5237` |
| Morale resistance | `(IsHero ? 1.5 : 1) * (0.5 * tier + 1)` | `CharacterObject.GetMoraleResistance` |

The panic-before-regeneration ordering is load-bearing and easy to get backwards: reversing it lets
+0.4/sec lift a broken agent back over the threshold every frame so nothing ever routs. That is a
real bug this feature's own test simulation shipped for one iteration before it was caught.

## Known Limitations

- **Auto-resolve does not see the aura.** `TaomCombatSimulationModel` and
  `DefaultMilitaryPowerModel` govern auto-resolve and know nothing about dread, so fighting a
  wraith manually is harsher than auto-resolving one. This is also a new confound for the
  `AutoResolveDiagnostics` census (#430), which compares manual and auto-resolved outcomes.
- **No campaign-map effect.** Deferred, see below.
- **No visual effect.** The audible cue is `SkinVoiceManager.VoiceType.Fear`, which falls silent
  rather than erroring for a race with no clip bound.

## Out of scope

| Deferred | Why | Shape of the follow-up |
|---|---|---|
| Campaign-map dread | `MobileParty.Morale` recomputes `GetEffectivePartyMorale` **uncached** and is read per-party-per-tick by about ten consumers, so it needs an hourly-tick cache, not a live query. It also crosses the desertion (10) and army-leave (25) thresholds, which delete troops and dissolve armies with no battle | One line in the existing `TaomPartyMoraleModel` (never a second `PartyMoraleModel`, only one can register), plus `/localize` for the tooltip string |
| Fearless undead | Not asked for | `CanPanicDueToMorale` override; mind the `SetRouted` note above |
| Cowering via `Agent.Defensiveness` | Not asked for. The setter calls `UpdateAgentProperties()` on any change above 0.0001, so it needs hysteresis rather than a per-pulse write | Set once on entering cowering, restore once on exit |
| Trolls, Mumakil, Saruman as sources | The mechanism ships; only the data is withheld. Trolls and Saruman are race rows (pure JSON). Mumakil needs a `monsterIds` axis off `agent.Monster?.StringId` | JSON rows, or one extra dictionary |
| Ally-side morale gain near your own wraith | Doubles the query count for an effect with no HUD | Same loop, opposite sign, `GetNearbyAllyAgents` |

## Related

- [combat-mechanics.md](combat-mechanics.md): the two-axis race/monster identity convention this follows.
- [nazgul-family.md](nazgul-family.md): the wraith roster in `NazgulRegistry`, shared with this feature.
- [banner-bearers.md](banner-bearers.md): the MissionLogic + validated-config feature this mirrors.
- [coop-interop.md](coop-interop.md): the MCM settings fingerprint the four new knobs join.
