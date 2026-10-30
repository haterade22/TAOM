# Aura of Unnatural Dread

## Overview

The Nazgûl and Sauron drain the morale of enemy troops right beside them on the battlefield.
The aura weakens the troops in contact with a Ringwraith, and it never routs them by itself:
morale stops at a floor of 10, and combat finishes them. Elves and dwarves resist, veterans and
heroes resist, and an elven hero of level 16 or more never reaches the floor at all. See "Why the
aura does not rout by itself (2026-10-15)" for the reason.

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

**The aura writes morale and nothing else.** Panic, retreat and the morale contagion that spreads
it are all the engine's, reached through the same `CommonAIComponent.Morale` field vanilla drives.
Calling `Retreat()` directly would bypass `MissionAgentPanicHandler`'s `OnBatchUnitRemovalStart/End`
bracket and thrash formation arrangement; calling `Panic()` directly would skip `CanPanic()`: the
morale model's `CanPanicDueToMorale` veto, and the rule that a siege attacker on a ladder or
siege-weapon face cannot panic (`CommonAIComponent.cs:174-193`).

## Identifying a dread source: two axes, and why one is not enough

**When the aura shipped, the Nazgûl were not identifiable by race.** Until #644 (2026-09-23) six
of the Nine (the Witch-King `lord_1_15`, `lord_1_155`, `lord_1_16`, `lord_1_28`, `lord_1_38` and
Khamûl `lord_1_48`) carried no `race` attribute, so they loaded as vanilla race 0 (human), and
`lord_1_48_1` / `_2` / `_3` were `race="uruk"`. (This section first listed five as carrying none
and Khamûl as `orc`, and counted eight unreachable; `uncapturable-heroes.md` recorded the correction
on 2026-08-26.)

So a race-keyed source list would have emitted **zero auras for the Nine**, parsed cleanly, logged
nothing, and looked like a working feature. Adding `uruk` to catch three of them would have handed
an aura of dread to every uruk lord in the game.

Since #644 all nine are `race="nazghul"` (Sauron stays `race="sauron"`). A race list could reach
them now, but the hero set stays the axis that finds the Nine: it names exactly those nine whatever
their race data says.

Identity is therefore a two-axis OR, the same shape `TaomCombatMechanicsModel` already uses:

- **Hero StringId**: via `heroSets: ["nazgul_nine"]`, resolving to the existing
  [`INazgulRegistry`](../../Main/Features/NazgulFamily/NazgulRegistry.cs), plus explicit
  `heroIds: ["lord_1_17"]` for Sauron. This is the axis that finds the Nine.
- **FaceGen race**: `races: ["sauron"]`. Sauron is the only race listed (the Nine are race
  `nazghul` since #644 but come in through the hero set), and he is listed on both so the feature
  survives a data change that drops his race attribute.

`ShippedDreadAuraConfigTests.ShippedConfig_KeepsTheNazgulHeroSet` pins this, because a
well-meaning "simplify to a race list" refactor is otherwise silent.

## The arithmetic

```
D_eff = moralePerSecond
      × falloff(distance)                     // 1.0 within innerRadius, linear to 0 at radius
      / max(1, GetMoraleResistance())         // vanilla tier/hero curve, via the model call
      × raceResist[targetRace]                // elf 0.4, dwarf 0.5, otherwise 1.0
```

`CharacterObject.GetMoraleResistance()` is `(IsHero ? 1.5 : 1) * (0.5 * n + 1)`, where `n` is the
tier, or `Level / 4 + 1` for a hero (v1.5.5 `CharacterObject.cs:619-623`).

The engine regenerates morale at **+0.4/sec** up to **half** a troop's starting morale
(`CommonAIComponent.OnTickParallel`). Above that ceiling there is no pushback at all; below it the
drain must beat 0.4/sec or the target parks at the ceiling forever.

```
t_floor = R / D_eff  +  (R - F) / (D_eff - 0.4)   where R = half of initial morale (about 24.75)
                                                  and F = moraleFloor (10); valid only for D_eff > 0.4
```

Initial morale is `35 + RandomInt(30)` plus every agent component's `GetMoraleAddition()` (in a
campaign battle, the party's morale and the side's strength share), then the morale model (perks,
traits and TAOM's culture doctrine), clamped to [15, 100] (v1.5.5 `CommonAIComponent.InitializeMorale`).
The table uses the mean 49.5, which is an even fight at party morale 50. A party at morale 80 starts
near 64.5, so R is about 32 and a tier-3 troop takes about 30 s to reach the floor.

At the shipped `moralePerSecond = 5.0` and `moraleFloor = 10`, inside the inner radius (2 m):

| Target | resistance | D_eff | time to reach the floor of 10 |
|---|---|---|---|
| Tier-1 levy | 1.5 | 3.33 | **12.5 s** |
| Tier-3 line infantry | 2.5 | 2.00 | **21.75 s** |
| Tier-6 elite | 4.0 | 1.25 | **37.25 s** |
| Tier-3 elf | 2.5 × 0.4 | 0.80 | **67.5 s** |
| Tier-5 dwarf | 3.5 × 0.5 | 0.71 | **81.25 s** |
| Elf hero, level 16 to 19 | 5.25 × 0.4 | **0.38** | **never** |
| Elf hero, level 20 to 23 | 6.0 × 0.4 | **0.33** | **never** |

The target then sits on the floor for as long as it stays in contact; the engine's regeneration
lifts it a little and the aura pushes it back down, so it never goes lower and never panics.

The last two rows still hold. **Elven heroes of level 16 or more are untouched by the Witch-King by
arithmetic**, with no special case anywhere in the code: their effective drain falls below the
engine's own regeneration rate, so they never get near the floor. A lower-level elven hero does
reach it, slowly (resistance 4.5 at levels 12 to 15 gives 0.44/sec). That `D_eff > 0.4`
break-even *is* the resistance mechanic, not a defect in it.

A 5 m radius covers only the troops in contact with the wraith, so the effect reads in play as the
men fighting him going soft, not as a whole shieldwall going together.

These numbers are the balance contract. They are asserted three times and all three must move
together: `DreadAuraServiceTests.ComputeDrain_ShippedFloor_ReachesTheFloorAtTheDocumentedTime` and
`ComputeDrain_ShippedFloorElfHero_NeverReachesTheFloor` simulate the real engine morale loop against
the real service, `ShippedDreadAuraConfigTests.ShippedConfig_BalanceValuesMatchTheDocumentedTable`
pins the four config values, and this table documents them.
`ComputeDrain_ShippedFloor_AuraAloneNeverRouts` proves the floor holds up to the highest rate any
configuration reaches (20/sec on a resistance-1 target). The floor-0 rout times (16 s, 28 s and
49 s for the first three rows) stay in `ComputeDrain_ShippedRate_RoutsAtTheDocumentedTime` with the
floor set to 0 explicitly, because they document the mechanics a lower floor would bring back.

## Why the aura does not rout by itself (2026-10-15)

A troop that routs raises `AgentMoraleInteractionLogic.OnAgentFleeing`. Vanilla gives a morale loss
to up to 10 allies of the fleeing agent: first the AI allies within 4 m, then, for every slot still
empty, random members of the fleeing agent's whole formation at any distance (v1.5.5
`AgentMoraleInteractionLogic.cs:79-84`, `SelectRandomAgentsFromFormationToAgentSet`). The loss
scales with `CalculateCasualtiesFactor` (1 + 2 x the side's removed ratio).

So an aura that routs troops is not local. In a dense block a single rout stays among its
neighbours, but those neighbours stood inside the aura too and break in turn; once the line thins,
each rout reaches random troops anywhere in the formation. The 12 m aura drained morale to 0 inside
its radius, so it routed troops by itself, and Mike saw dread act across the whole battlefield.
With a floor of 10 the aura starts no rout of its own, and the radius is cut to 5 m (inner 2 m) so
it only touches the troops in contact.

Three other fear effects have no floor and can still rout troops the aura has weakened:

- the Nazgûl Scream (a signature strike, 6 m, 25 morale every 15 s). At full strength it takes
  exactly 10 from a tier-3 troop (25 / 2.5). Regeneration lifts a troop on the floor to about 10.1
  between pulses, so a Scream leaves a tier-3 troop at the edge of panic, where any nearby death
  breaks it, and it breaks tier-1 and tier-2 troops (16.7 and 12.5) outright;
- Sauron's overhead Slam (4 m, 15 morale every 20 s);
- race-ability fear, such as the Dol Guldur 8 m fear aura and the fear some races cause on a kill.

Such a rout spreads by the vanilla rule above. Mike kept the floor at 10 on 2026-10-15: these
effects reach 4 to 8 m, so their routs start next to the source.

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

**Career counter (#769 E10).** The pulse runner wraps the morale model's answer in
`CareerTroopHooks.ScaleFearDrain(<career service>, target, ...)` (the stat service `DreadAuraMissionLogic` hands the runner), so a victim troop whose party leader is a player with a
`TroopFearResistance` pip loses that fraction less morale (see [career-system.md](career-system.md)). It
never touches the player or a hero, and it does not override the morale model, which also scales kill-morale gains.

## Mission types

`DreadMissionGate.IsEligible` is an **allowlist**, re-read every tick:

- `Campaign.Current != null` excludes custom battle, main menu, multiplayer maps
- `!GameNetwork.IsSessionActive` excludes multiplayer
- `CombatType == Combat` excludes arenas and tournaments (`ArenaCombat`) and conversations and
  town walkarounds (`NoCombat`)
- `Mode == MissionMode.Battle` excludes the pre-battle **Deployment** phase. This one is not
  redundant with the team-AI check below: `MissionTeamAIType` is assigned *before* deployment
  (`DeploymentHandler.cs:41` sets `Mode = Deployment`; `MissionCombatantsLogic.cs:225` flips it to
  `Battle` afterwards), so without this clause a wraith drains morale while the player is still
  positioning formations, and a player who lingers there starts the battle already down. Caught by
  Codex 2026-08-13; same gate `BannerBearerAssignmentMissionLogic` uses.
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

A wraith entrant draining a Minas Morgul tournament could rout the other entrants (at
`moraleFloor` 0; at the shipped floor it would only weaken them, because the tournament mission
carries no `AgentMoraleInteractionLogic`) inside an arena with no retreat position, which is the
failure family
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
it: a fearless agent clamps at `0.01f` and never panics, so if it leaves the field it leaves as an
ordered retreat (see the roster section): it keeps its roster place, but `MapEventSide.OnTroopRouted`
still counts it in `TroopCasualties` (v1.5.5 `:642-643`).

**The radius clamp** was designed in, then removed after reading `Mission.GetNearbyAgentsAux`
(v1.4.8 Mission.cs:2314), then restored on 2026-10-15 at 15 m. The managed side is pure native paging, but
the native side has the fallback: `IMBMission.GetNearbyAgentsAux` searches the proximity grid only
up to the value `ProximityMapMaxSearchRadius` returns (15.0 on v1.5.5, `.rdata` RVA 0xB2E120) and
walks every agent in the mission beyond it (`python tools/native_decompile.py --engine-method
IMBMission.GetNearbyAgentsAux`). `AgentProximityMap.BeginSearch` switches at the same 15 m. So the
MCM slider and both providers cap the radius at 15, and a value above it costs a full agent scan on
every pulse.

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
| `profile.radius` | 5.0 | [0.1, 15] accepted at load, but the radius in play is always [4, 15]: with MCM loaded the slider value replaces it, and without MCM the settings provider clamps it to the slider's range too |
| `profile.innerRadius` | 2.0 | [0, radius], ordering invariant |
| `profile.moralePerSecond` | 5.0 | [0, 50] accepted at load, but the rate in play is always [0, 20], the slider's range, as for the radius |
| `profile.moraleFloor` | 10.0 | [0, 50] |
| `raceResist[*]` | elf 0.4, dwarf 0.5 | [0, 1] per entry, above 1 dropped |

`moraleFloor: 10` keeps the aura from routing anyone on its own, because the engine panics a troop
only below 0.01. Setting it to 0 lets the aura alone drive a troop to the panic threshold, and each
such rout passes a morale loss to up to 10 allies, across the formation when fewer than 10 stand
within 4 m (see "Why the aura does not rout by itself"). It is a data change, no code. The floor
has no MCM knob.

Above 1.0 a resist value would *amplify* dread on a race the author meant to protect, so those rows
are dropped with a warning rather than clamped.

Race names and hero ids are **not** checked for existence at load: the FaceGen registry is not
populated then. At load a null or blank entry is removed with a warning and a padded one trimmed
(since the Codex review 130 follow-up).
`DreadRegistry` validates race names lazily on first resolve and skips + warns per entry; hero ids
are pinned instead by `ShippedDreadAuraConfigTests`.

**MCM** (`Aura of Dread` group): `EnableDreadAura`, `DreadAuraCloseRadius` (4-15, default 5),
`DreadAuraMoralePerSecond` (0-20), `DreadAuraAffectsPlayerTroops`. The radius property was renamed
from `DreadAuraRadius` when its default dropped from 12 to 5: MCM keeps a saved value per property
name, so an existing `TAOM.json` would have kept 12 under the old name, and a new name lets the new
default reach everyone. `DreadAuraSettingsProviderTests.McmDefaults_MatchTheCompiledProfileDefaults`
keeps the MCM defaults equal to the compiled JSON defaults. The radius ceiling is enforced
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

177 tests (`dotnet test TAOM.Tests --filter FullyQualifiedName~TAOM.Tests.Features.DreadAura`, 2026-10-15).

- `DreadAuraServiceTests` (58): falloff geometry, the golden time-to-floor table at the shipped floor
  of 10, the proof that the aura alone never routs up to the 20/sec rate ceiling, the floor-0 rout
  times, the morale floor (a null profile included), the
  `-1f` no-component sentinel, and a NaN / ±Infinity case for every float that reaches a decision.
- `DreadAuraConfigProviderTests` (47): one test per validation rule (blank list entries removed
  and padded ones trimmed since Codex review 130), plus an every-field round-trip and the
  `ObjectCreationHandling.Replace` regression.
- `DreadRegistryTests` (25): both identity axes, `PlainOrcTroop_IsNotSource`, unknown config keys
  skipped and warned, the lazy map built once.
- `DreadPulseSchedulerTests` (16): the rotation starves nobody, the budget holds, a selected source
  is not picked twice in a frame, a NaN interval selects nothing, and a long skipped window is
  clamped to the catch-up ceiling instead of arriving as one burst.
- `ShippedDreadAuraConfigTests` (12): the shipped file parses with zero rejections and keeps the
  balance contract, the hero-set link, and a floor above the 0.01 panic threshold.
- `DreadAuraBindingTests` (8): engine members resolve; `MissionLogic` is the base class.
- `DreadSourceTrackerTests` (7): null handling, the dedup contract, and the mission-clock seed.
- `DreadAuraSettingsProviderTests` (4): with no MCM every getter falls back to the JSON, a live MCM
  edit reaches its getter through the cached settings reference, the MCM defaults equal the compiled
  defaults, and an MCM radius above 15 clamps to 15.

**Mutation-checked.** When the feature shipped (2026-08-13), disabling each load-bearing rule
reddened tests: the morale floor (2), the radius NaN gate written as a positive requirement (2), the
race-resist multiplier (3), the per-source elapsed integration (6). On 2026-10-15, setting the floor
to 0 inside `ComputeDrain` reddened 7, both never-routs rows among them. An earlier `IsValidRaceId` guard reddened **zero** and was
removed rather than shipped: the lookup tables are keyed by race *id*, so `GetRaceNameFromId`'s
"human" fallback is structurally unreachable and the guard defended a path that does not exist.
The `DidNotReceive().GetRaceNameFromId` assertions pin that design against a name-keyed rewrite.

## Engine constants this feature is balanced against

Signature drift is caught offline by `DreadAuraBindingTests`. **Behaviour** drift in these constants
is not, so they are recorded here and the control battle is an `/engine-bump` checklist item.
Verified against v1.4.8:

| Constant | Value | Source |
|---|---|---|
| Initial morale | `35 + rand[0..29]` plus each component's `GetMoraleAddition()`, then the morale model, clamped [15, 100] (re-read on v1.5.5) | `CommonAIComponent.InitializeMorale` |
| Recovery ceiling | `_initialMorale * 0.5f` | `CommonAIComponent.cs:74` |
| Morale regeneration | +0.4/sec, only below the ceiling | `CommonAIComponent.OnTickParallel` |
| Panic threshold | `_morale < 0.01f`, tested **before** regeneration | `CommonAIComponent.OnTickParallel` |
| Formation flee (watched, not balanced against) | `Flee` above 30% of a formation retreating; its only reader, the order panel's view model, stores it in a property nothing reads, so it changes no behaviour (re-read on v1.5.5) | `Formation.GetMovementTypeOfUnits` (`Formation.cs:1266-1291`), `MissionOrderTroopControllerVM.cs:875`; `Mission.GetMovementTypeOfAgents` (`Mission.cs:5280`, no caller) |
| Morale resistance | `(IsHero ? 1.5 : 1) * (0.5 * n + 1)`, `n` = tier, or `Level / 4 + 1` for a hero (re-read on v1.5.5) | `CharacterObject.GetMoraleResistance` |

The panic-before-regeneration ordering is load-bearing and easy to get backwards: reversing it lets
+0.4/sec lift a broken agent back over the threshold every frame so nothing ever routs. That is a
real bug this feature's own test simulation shipped for one iteration before it was caught.

## Routed troops leave the roster permanently

This is the consequence a player most needs to understand, and it is inherited from vanilla rather
than invented here: **a routed troop is not a scared troop, it is a lost one.** Since 2026-10-15
the aura alone no longer routs anyone; the routs below come from combat on troops it has weakened.

A troop that panics and flees off the field leaves the mission neither killed nor wounded, so
`BattleAgentLogic.OnAgentRemoved` calls `Origin.SetRouted(isOrderRetreat)` with `isOrderRetreat`
true only when its morale is at or above 0.01 (v1.5.5 `BattleAgentLogic.cs:176-179`). When the
enemy side retreats, `BattleEndLogic.OnEndMission` does the same for every agent of the player's
enemy team still on the field (`BattleEndLogic.cs:224-235`). `PartyGroupAgentOrigin.SetRouted` (`:130-137`) passes it to
`MapEventSide.OnTroopRouted` (`:633-644`), which, for a panicked troop, calls
`MapEventParty.OnTroopRouted` (`:316-328`) and does exactly this:

```csharp
Party.MemberRoster.AddToCounts(_roster[troopSeed].Troop, -1);   // gone from the party
RoutedInBattle.AddToCounts(_roster[troopSeed].Troop, 1);        // feeds DesertersCampaignBehavior
```

So a formation that routs removes those troops from their party's roster and, on the defeated
side, can seed deserter parties on the map (`DesertersCampaignBehavior.MapEventEnded`). Four
carve-outs, each read on v1.5.5:

| Case | Outcome |
|---|---|
| Ordered retreat (morale at or above 0.01 as it leaves) | **Not lost.** `MapEventSide.cs:636` removes the troop only when `isOrderRetreat` is false, and `isOrderRetreat` is true when the troop's morale is at or above 0.01 as it leaves (`BattleAgentLogic.cs:178-179`). A troop the aura holds at its floor of 10 that leaves on a retreat order keeps its place, unless something else takes it below 0.01 first. It still counts in its side's casualty tally (`MapEventSide.cs:640-643`). |
| Heroes and companions | **Exempt.** `MapEventParty.cs:324-327` wounds them instead of removing them. |
| Siege defenders | **Exempt** in a siege assault: `MapEventSide.cs:636` requires `MissionSide == Attacker` only when the map event is `BattleTypes.Siege`. In a sally-out, routed troops on both sides are removed. |
| `PartyAgentOrigin` (ungrouped) | **Exempt.** Its `SetRouted` is an empty method (`PartyAgentOrigin.cs:202-204`). |

One consequence is worth being deliberate about: **`DreadAuraAffectsPlayerTroops` defaults to
true**, so an enemy wraith weakens the player's troops and they rout sooner in combat, which can
permanently cost the player troops. The aura does not rout them itself, but the cost is real and the
MCM hint says so.

## Known Limitations

- **Auto-resolve does not see the aura.** `TaomCombatSimulationModel` and
  `DefaultMilitaryPowerModel` govern auto-resolve and know nothing about dread, so fighting a
  wraith manually is harsher than auto-resolving one.
- **No campaign-map effect.** Deferred, see below.
- **No visual effect.** The audible cue is `SkinVoiceManager.VoiceType.Fear`, which falls silent
  rather than erroring for a race with no clip bound.
- **The morale write races the engine's regeneration, and that is accepted.**
  `CommonAIComponent.OnTickParallel` regenerates `_morale` on a parallel job while the pulse writes
  it from `OnMissionTick` on the main thread. Neither side synchronises, so one side's contribution
  can be lost. A 32-bit float store is atomic on x64, so there is no tearing, and the drain
  integrates from `LastPulseTime` rather than from the previous morale value, so a lost update does
  not accumulate error: the next pulse reads whatever the current value is and carries on. Vanilla's
  own `AgentMoraleInteractionLogic` writes the same field the same way from a main-thread event.
  Buffering the drains to a post-parallel sync point was considered and rejected: the win is a rare
  sub-0.01 perturbation, the cost is a new accumulation buffer plus a guess about which callback is
  reliably post-parallel, and guessing that wrong is worse than the race. Flagged by Codex
  2026-08-13 as RACE-REAL; recorded here rather than fixed.

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
- [signature-strikes.md](signature-strikes.md): Sauron's and the Nine's strikes, whose fear burst reuses `DreadAgentGate.CanAffect` and `IDreadRegistry.ResolveResist`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/signature-strikes.md](./signature-strikes.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/reference/engine/formations-and-team-ai.md](../reference/engine/formations-and-team-ai.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/roadmap.md](../roadmap.md)

<!-- backlinks-end -->
## Source pruning and slot reuse (2026-09-13, #595)

2026-09-13 (#595): `DreadSourceTracker.Prune` requires `AgentSlotIdentity.IsCurrentOccupant` as well as the
engine's `IsActive()`, which answers for a recycled slot's new tenant; the pulse runner's null-`Team` early-out
was the only thing masking that.
