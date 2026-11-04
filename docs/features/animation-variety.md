# Animation Variety

## Overview

One feature module, `AnimationVariety` (`Main/Features/AnimationVariety/`), holding two mission-time pieces that share the `[AnimVariety]` log tag. The **swing probe** is a logging-only experiment that asks whether the engine picks a random swing clip among alternative action codes; it is on by default while the swing clips are tested (see "The swing probe"). The **gait variety** feature changes the game: it gives each orc and goblin warrior in a battle its own step size and gait set, once, when the soldier spawns, so a marching line stops walking in lockstep.

## Why This Exists

- **Vanilla behavior:** every troop of one type gets the same step size, `BasicCharacterObject.GetStepSize()`, which is `min(0.8 + 0.2 * Athletics / 300, 1)`. The walk phase advances by speed over clip stride, clip duration and `StepSize` and starts at 0 for every agent (native research on v1.5.5). Nothing randomizes it, and nothing keeps soldiers apart once they stand still: the native locomotion update (0x7496D0) holds a fully idle agent's walk phase at its forward clip's `bip_mov_ik` snapping start, so soldiers on one clip re-align at every halt. The rate is also lifted toward the clip's own rate at low speed (0x74BF60). A step size difference therefore builds up only while the soldiers keep walking.
- **TAOM requirement:** orc and goblin lines are large and dense, and their restyled locomotion clips ([orc-goblin-locomotion.md](orc-goblin-locomotion.md)) are one clip per move. Identical clip, identical stride and identical start means every soldier in a line puts the same foot down on the same frame.
- **Without this feature:** a marching orc line reads as one animation copied a few hundred times.

## Architecture

### Design Challenge

- The phase and the step size live in native code. The only managed lever is `Agent.SetActionSet(ref AnimationSystemData)` (v1.5.5 `Agent.cs:2608`), which natively runs the full human animation init: both action channels are cleared, the walk phase goes back to 0, `StepSize` is re-read and the rider link is re-attached.
- `MBActionSet.GetActionSet(id)` answers an unknown id with set 0 (`as_human_warrior`), and the native `GetIndexWithID` logs `Action set "%s" could not be found, using default action set!` on every miss. TAOM's triage table reads that line as a broken base set, so a per-id probe is not an option. The engine's sets are listed once instead (below).
- `Agent.Index` recycles, so it cannot seed a per-soldier draw.

### Solution Approach

`GaitVarietyMissionBehavior` (a `MissionLogic`, 81 lines) overrides `OnAgentBuild`, which `Mission.SpawnAgent` raises on the spawning thread after `BuildAgent` (v1.5.5 `Mission.cs:4390`). It hands each agent, wrapped in `GaitVarietyAgent`, to `GaitVarietyService`. The service applies two levers in **one** `SetActionSet` call, built the way vanilla builds its own post-spawn swap (`Mission.cs:4531-4534`): `Monster.FillAnimationSystemData(actionSet, stepSize, hasClippingPlane: false)`.

1. **Random step size.** New step = the agent's vanilla `Character.GetStepSize()` times a factor in `[minFactor, 1.0]`. The factor is `min + (1 - min) * u` with `u` in `[0, 1)`. The factor multiplies vanilla's own step size, so the applied value can go below 0.80 (see Configuration).
2. **Random gait set.** The agent's action set is replaced by one of its configured variants, equal weights, base set included.

| Base set | Variants |
|---|---|
| `as_orc_warrior` | `as_orc_warrior_b`, `as_orc_warrior_c` |
| `as_goblin_warrior` | `as_goblin_warrior_b`, `as_goblin_warrior_c` |
| `as_goblin_female_warrior` | `as_goblin_female_warrior_b`, `as_goblin_female_warrior_c` |

Only these three base sets are touched. There is no `as_orc_female_warrior` row: the Armory `orc` Monster declares no `female_action_set` (`LOTRLOME_Armory/ModuleData/monsters.xml:419-420`), so a female orc spawns on `as_orc_warrior` and that row would be unreachable. The goblin Monster does declare one (`monsters.xml:1392`). Villager, tavern and hideout sets, every other race and every other action set are left alone.

**Draw.** `u` and the variant index come from a pure function of the troop `StringId` (FNV-1a) and `Agent.BodyPropertiesSeed`, finalized with a 32-bit mixer; the variant index is a second, independent draw from the same seed. The seed is vanilla's face seed: `(party index * 171 + StringId hash * 6791 + rank * 197) mod 2000` in a campaign, without the party term in a Custom Battle (`CharacterHelper.GetPartyMemberFaceSeed`, `BasicCharacterObject.GetDefaultFaceSeed`). So a soldier gets the same gait whenever it is spawned with the same seed, and two soldiers of one troop type collide only when their seeds do (2000 values).

**Skipped agents** (no engine call, no log line): a non-human agent, a mount, the main agent or any player-controlled agent, a rider (below), an agent whose set is not one of the three, and an agent whose vanilla step size is not a finite number of at least 0.01 (one warning per mission).

**Riders are skipped on purpose.** The managed `SetActionSet` is two statements, the native call and a network broadcast, so nothing in the decompile shows that re-initialising a mounted rider's animation is safe; the native side re-attaches the rider link, which is exactly the part not proven. A rider's walk phase is not visible anyway. The check reads both the cached mount agent and the horse slot (`EquipmentIndex.Horse`) of the spawn equipment, because the mount link is native.

**Variants that do not exist.** `GaitVariantCatalog` asks the engine for one silent listing per process: `IGaitVarietyAdapter.LoadedActionSetNames()` loops `MBActionSet.GetNumberOfActionSets()` and `GetActionSetWithIndex(i).GetName()` (native: the count is 0x6DF8F0 and the name by index 0x6DF890, neither logs). It keeps the names in an ordinal `HashSet` and answers every variant question by membership, so no per-id lookup runs and the engine's "could not be found" line never appears. A missing variant is reported only by the `=missing` flag of the INFO variants line, once per mission; the catalog has no logger and never warns. With no variant installed, lever 2 draws only the base set and makes no engine call.

**Interplay with later set swaps.** `Mission.SpawnTroopWithAgentBuildData` swaps a suffixed set (`specialActionSetSuffix`) after `SpawnAgent` returns, which is after `OnAgentBuild`; a caller that passes a suffix replaces the gait set chosen here. Battle spawns pass none. The SandBox settlement and disguise handlers do the same to every agent they build: `MissionAgentHandler.cs:888-889` replaces the set with the location character's action set, and `DisguiseMissionLogic.cs:494` and `:524` replace it with a suffixed set. All three run after `SpawnAgent`, so a town orc gets one wasted re-init here (its set and step size are then overwritten with vanilla's step size) and is still counted in the `n=` of the mission summary. In battle the DualWield mod does the same to its dual-wield troops: its `Mission.SpawnAgent` postfix runs after `OnAgentBuild` and sets the Monster's set plus `_dw` (`as_orc_warrior_dw`) with the troop's vanilla step size. DualWield_TAOM adds three such gait-race troops (`dw_mordor_orc_cutthroat`, `dw_dolguldur_goblin_cutter`, `dw_goblintown_twinblade`), so they keep the main gait and vanilla step size and still count in `n=` under the variant they drew. Accepted (Mike, 2026-10-18): only these few troops dual-wield; look again only if DualWield ever takes over the gaits of ordinary troops.

**Failure.** The behavior catches every exception, logs one warning and stops for the rest of that mission (`[AnimVariety] gait OnAgentBuild failed, gait variety off for this mission: ...`); the battle goes on. An `OnAgentBuild` that arrives off the main thread (`MissionThreadGuard.NoteCall`) is skipped, never parked, because the soldier would already have played frames with the vanilla gait. The behavior builds its warning delegate once in its constructor.

### Component Diagram

```
MCM (TaomSettings, group "Animation Variety")
        |
  GaitVarietySettingsProvider (lazy)        AnimVarietyProbeSettingsProvider (lazy)
        |                                              |
  GaitVarietyService  ---- GaitVariantCatalog     AnimVarietyService
  (per mission)            (process singleton)    (per mission)
        |                        |                      |
  IGaitVarietyAgent        IGaitVarietyAdapter    IAnimVarietyAgent, IAnimVarietyAdapter
  (GaitVarietyAgent)       (GaitVarietyAdapter)   (AnimVarietyAgent, AnimVarietyAdapter)
        ^                                               ^
        |                                               |
  GaitVarietyMissionBehavior                     AnimVarietyMissionBehavior
  OnAgentBuild, OnEndMission                     OnMissionTick, OnAgentHit (DeferredCallbackQueue),
                                                 OnEndMission
        ^                                               ^
        +----------------- AnimationVarietyModule -------+
                    (one MissionBehaviorDecl each)
```

### Co-op

The decision: gait variety applies on every peer and is not gated on `IsAuthority`, `ShouldDeferToHost` or `IsDedicatedServer`. The three co-op questions ([coop-interop.md](coop-interop.md)) answer as follows.

| Question | Answer | Evidence |
|---|---|---|
| Is a co-op module loaded? | Irrelevant. The feature reads no co-op signal. | None needed. |
| Does this peer own the simulation? | Irrelevant. Nothing shared or saved changes: the walk phase and the clip choice of an agent this peer built. | The levers write only `Agent.SetActionSet`. |
| Is there a real player in this process? | Irrelevant. Nothing is credited to `Hero.MainHero`. | No campaign state is read. |

The managed `SetActionSet` broadcasts a `SetAgentActionSet` message only under `GameNetwork.IsServerOrRecorder` (v1.5.5 `Agent.cs:2611-2616`), the native multiplayer server or a recording. BannerlordCoop moves mission agents with its own position and action packets (`docs/research/bannerlordcoop-internals.md`, "Missions and combat") and its source has no `GameNetwork.IsServer` use; it calls `Agent.SetActionSet` itself when it spawns location puppets (`LocationPuppetSpawner.cs:482`). A puppet (`Controller = None`) is built through `Mission.SpawnAgent`, so `OnAgentBuild` fires for it and each peer draws for its own copy. The three gait settings are classified as presentation in `CoopSettingsRelevance`, so they stay out of the settings fingerprint.

The swing probe is co-op neutral too: it reads and logs only, on whichever agents this process holds, and writes nothing to the game or the network. `EnableSwingAlternativeProbe` is classified as instrumentation in `CoopSettingsRelevance`. A peer with it on pays the per-tick sweep alone.

Not verified: whether Coop's puppet spawner passes the owning peer's `BodyPropertiesSeed`. If it does not, two peers show different gaits for the same soldier, which changes pixels only.

## Configuration

MCM group **Animation Variety** (`TaomSettings`, all live):

| Setting | Property | Default | Range | Read |
|---|---|---|---|---|
| Random step size per soldier | `EnableRandomStepSize` | on | bool | on every spawn |
| Step size minimum factor | `GaitStepSizeMinFactor` | 0.92 | 0.80 to 1.00 | on every spawn |
| Random gait set per soldier | `EnableRandomGaitSet` | on | bool | on every spawn |
| Swing alternative probe (diagnostic) | `EnableSwingAlternativeProbe` | **on** (for testing; see below) | bool | once per mission, at the probe's first tick |

The first three apply to soldiers spawned after the change. The probe switch is read at the start of each mission, so a change waits for the next mission.

**The factor is not the final step size.** It multiplies vanilla's step size, `min(0.8 + 0.2 * Athletics / 300, 1)`, which is 0.80 at Athletics 0 and 1.00 at Athletics 300. At the default 0.92 an Athletics 0 soldier ends between 0.736 and 0.80, and an Athletics 300 soldier between 0.92 and 1.00. At 0.80 the lowest applied step size is 0.64. The MCM hint states this; an earlier hint claimed 0.92 "stays inside what the game shows", which was wrong for low-Athletics soldiers. The default did not change, so no persisted-default note is needed.

The minimum factor is validated in `GaitVarietyService` with `FiniteFloatValidator.IsFiniteInRange`: NaN, an infinity or a value outside 0.80 to 1.00 falls back to 0.92 with one warning per mission, but only while the step lever is on. With the lever off the factor is unused: nothing warns, and the settings line prints the raw value. `GaitVarietySettingsProvider` reads the settings object through a lazy accessor, because it is read on every spawn (`HotPathSettingsProvidersTests`); `AnimVarietyProbeSettingsProvider` does the same and joins the same tests.

No new setting reuses an existing name. A saved `TAOM.json` from before this feature has no values for the four, so MCM supplies the defaults above.

### Log lines

Tag `[AnimVariety]`. Nothing at all is written when no orc or goblin warrior spawned and the probe is off.

At the first eligible soldier of a mission, two lines:

```
[AnimVariety] gait settings randomStepSize=True minFactor=0.920 randomGaitSet=True
[AnimVariety] gait variants as_orc_warrior_b=present as_orc_warrior_c=present as_goblin_warrior_b=missing as_goblin_warrior_c=missing as_goblin_female_warrior_b=missing as_goblin_female_warrior_c=missing
```

At mission end, only when a lever acted, one line per base set:

```
[AnimVariety] gait set=as_orc_warrior n=212 variants as_orc_warrior=212 step n=212 min=0.736 max=0.999 mean=0.880
[AnimVariety] gait set=as_orc_warrior n=212 variants as_orc_warrior=71 as_orc_warrior_b=70 as_orc_warrior_c=71 step off
```

`n` counts soldiers a lever acted on, including those that drew the base set and settlement orcs whose set is swapped again after spawn; the `step` part is the resulting step size (`n`, `min`, `max`, `mean`, invariant culture, three decimals). The second line is what a run looks like once the variants exist and the step lever is off. Warnings:

```
[AnimVariety] gait step size minimum factor NaN is not a number from 0.800 to 1.000; using 0.920
[AnimVariety] gait vanilla step size NaN is not a usable number; soldiers that carry it are left alone
```

### Variant sets in the Armory

The orc sets `as_orc_warrior_b` and `as_orc_warrior_c` are in the live Armory since 2026-10-18: Mike imported the masters, the 36 clips (16 main, 10 B, 10 C) were generated and Kit-saved, and `bind_orc_goblin_locomotion.py --apply` wrote the two sets ([orc-goblin-locomotion.md](orc-goblin-locomotion.md), the Kit Import Checklist). Their in-game look is owed. In the variants line above, the two orc sets now read `=present`. Goblin variants have no clips yet, so the four goblin sets still read `=missing`, and only the step size lever acts on goblins. Goblin variants come after the orc ones; the code needs no change when a set appears, it picks it up on the next game start (the listing is read once per process). Each variant must be a full copy of its base set's rows, because the soldier fights, rides and dies with that set.

## The swing probe

`AnimVarietyMissionBehavior` and `AnimVarietyService` test the action-set `alternative_group` attribute: does the engine play a random swing clip among alternative codes? It is **on by default while the swing clips are being tested** (Mike, 2026-10-18; it shipped off in `d13e91658`, which no build ever deployed). On, with no `act_taom_*` code loaded, it makes 256 silent action-code lookups and writes one INFO line per mission; with such codes loaded it sweeps the soldiers every frame. With `EnableSwingAlternativeProbe` off, the first tick reads the switch and stops: no `ActionCodeExists` lookup runs, no agent is swept and no log line is written. **Turning it off for players later means renaming the setting** (and its `CoopSettingsRelevance` entry), not flipping the default: MCM writes `TAOM.json` when a player saves the Mod Options, and a saved value outlives a later default, so a player who saved them on such a build keeps `true` (orientation.md, "Persisted MCM defaults"). No saved `false` exists to worry about today: no build with the off default was deployed or released, and this machine's `TAOM.json` (2026-09-17) has no probe key. **Owed:** rename the setting with the default off before the next release or before any `act_taom_*` code reaches the Armory, whichever comes first. Before the switch existed, any loaded `act_taom_*` code armed a per-frame sweep in every mission.

With the switch on, the service looks in each mission for the base codes `act_<family>_<direction>_1h` and `..._1h_left_stance` (families `release_`, `quick_release_`, `blocked_`, `quick_blocked_`; directions `overswing`, `slashleft`, `slashright`, `thrust`) and for alternatives named `act_taom_<base without "act_">_<b..h>`. With none it logs `[AnimVariety] no alternative groups found; swing tracking off` and stops. Otherwise, for each action set once, it logs a `group set=... base=... alt=...` line with `alternatives`, clip names, durations and blend-out starts, and warns about the setups that crash or mistime:

| Warning | Meaning |
|---|---|
| `CRASH RISK: no clip for the base code` | The base code has no clip. The only crash-risk warning. |
| `no clip for the alt code: group disabled for that code (not a crash risk)` | The parser writes the `{clip, group}` slot only after the clip resolves, so the group stays -1 and the random pick never returns that code. |
| `TIMING RISK: ...` | Base and alt durations differ by more than 0.01 s, or their blend-out starts (`MBActionSet.GetActionBlendOutStartProgress`) differ by more than 0.01. The hit timing is computed from the original clip's duration minus its blend-out period. The blend-out start is read only when both clips exist, because its native path uses the logging slot lookup. |
| `group disabled: one member, or the base row lacks alternative_group` | `AreActionsAlternatives` answered false. |

Every group line ends with a note that the melee table row of the alt code cannot be checked from managed code. A missing row there is the real first-swing crash, and nothing in the probe can see it.

The probe counts which code each swing plays and where in the clip a hit lands, and writes `swings set=...` and `hits <code> n=... progress mean=... min=... max=...` lines every 30 s and at mission end. `OnAgentHit` ignores missile blows (`blow.IsMissile`, checked before any allocation, and again in `RecordHit`): a projectile lands long after the shot, while channel 1 may already play a later swing. It changes nothing in the game.

**It has never tracked anything yet.** No `act_taom_*` swing code ships, so the probe has only ever logged "no alternative groups found" or nothing; its summary format is unproven in a real battle. Its internals key the agent map by reference identity (`ReferenceIdentity.Instance`), and the behavior empties its agent list as soon as the service's tick returns, so no `Agent` reference outlives the tick.

## Performance

- **Gait variety:** one `SetActionSet` per orc or goblin spawn when a lever acts (the native animation init), plus a few dictionary lookups. A gait draw that picks the base set with the step lever off makes no engine call. The variant catalog lists the engine's action sets once per process.
- **Swing probe:** on by default while the swing clips are tested; off, it is one flag check per tick. When on and no alternative code is loaded, the first tick looks up the 32 base codes, plus seven alternatives for each base that exists, and switches itself off. When on with codes loaded, every tick builds a short-lived wrapper per human agent and sweeps them, which is the cost the switch exists to avoid.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/AnimationVariety/GaitVarietyService.cs` | All gait logic: eligibility, seed, draw, validation, counting, summary text |
| `Main/Features/AnimationVariety/GaitVariantCatalog.cs` | The three base sets, variant existence from one silent action-set listing per process |
| `Main/Features/AnimationVariety/GaitVarietySettingsProvider.cs` | Lazy MCM reads; `IGaitVarietySettingsProvider.cs` is its seam |
| `Main/Features/AnimationVariety/AnimVarietyProbeSettingsProvider.cs` | Lazy read of the probe switch; `IAnimVarietyProbeSettingsProvider.cs` is its seam |
| `Main/Features/AnimationVariety/Hooks/GaitVarietyMissionBehavior.cs` | Thin `MissionLogic`: `OnAgentBuild`, `OnEndMission`, `OnRemoveBehavior` |
| `Main/Features/AnimationVariety/AnimationVarietyModule.cs` | Registers both pieces; one `MissionBehaviorDecl` each |
| `Main/Adapters/IGaitVarietyAdapter.cs`, `GaitVarietyAdapter.cs` | The silent action-set name listing |
| `Main/Adapters/IGaitVarietyAgent.cs`, `GaitVarietyAgent.cs` | The agent reads and the one `SetActionSet` call |
| `Main/Adapters/IAnimVarietyAdapter.cs`, `AnimVarietyAdapter.cs` | Probe reads: action code, alternatives, clip name, duration, blend-out start |
| `Main/Adapters/IAnimVarietyAgent.cs`, `AnimVarietyAgent.cs` | Probe reads of one agent: channel-1 action, stage, progress, action set key |
| `Main/Features/AnimationVariety/AnimVarietyService.cs`, `Hooks/AnimVarietyMissionBehavior.cs` | The swing probe |
| `Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs` | Parks the probe's `OnAgentHit` write when native raises it off the main thread |
| `Main/Features/AdvancedCombat/MissionThreadGuard.cs` | Main-thread tripwire for the gait `OnAgentBuild` and the probe's tick |
| `Main/Features/AdvancedCombat/AgentSlotIdentity.cs` | Slot-occupant check in `AnimVarietyAgent.IsActive` |
| `Main/Features/TaomSettings.cs` | The four settings, group "Animation Variety" |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | Classifies the three gait settings as presentation and the probe switch as instrumentation |

## Dependencies

- `IGaitVarietyAdapter`, `IGaitVarietyAgent`, `IAnimVarietyAdapter`, `IAnimVarietyAgent` (Adapters): `MBActionSet`, `ActionIndexCache`, `Agent`, `Monster.FillAnimationSystemData`.
- `IModLogger` (Core): the `[AnimVariety]` lines.
- `FiniteFloatValidator` (Core.Validation): every gate on the factor, the vanilla step size and the probe's frame time, progress and timing comparisons.
- `ReferenceIdentity` (Core.Collections): the probe's agent map comparer.

## Tests

- `TAOM.Tests/Features/AnimationVariety/GaitVarietyServiceTests.cs`: 77 tests; every skip condition, both levers, the factor range, the draw, every log line, invariant culture, NaN and out-of-range gates, no min factor warning with the step lever off.
- `TAOM.Tests/Features/AnimationVariety/GaitVariantCatalogTests.cs`: 28 tests; the three base sets, the exact-name rule, one engine listing per process, never a per-id probe, never a warning.
- `TAOM.Tests/Features/AnimationVariety/AnimVarietyServiceTests.cs`: 49 tests; the off switch, every group warning, the blend-out comparison, swing counting, hit recording including the missile and early or late hit cases, the summary clock.
- `TAOM.Tests/Features/AnimationVariety/AnimVarietyWiringTests.cs`: 9 tests; module declaration, entry point size, the missile gate and the agent list clear (source order), probe default on, the hint, and `MBActionSet.GetActionSetWithIndex(7).GetHashCode() == 7` (tagged `RequiresGame`) because the probe keys action sets on that hash.
- `TAOM.Tests/Features/AnimationVariety/GaitVarietyWiringTests.cs`: 10 tests; module declaration, entry point under 150 lines, singleton catalog and container resolution, settings defaults and slider bounds, the step size hint, `EquipmentIndex.Horse`, the cached warning delegate.
- `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs`: both providers join the lazy-accessor, counting and container tests. `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`: the `TaomSettings` split is 474 reflected, 331 simulation-relevant.
- Measured with coverlet (`--collect:"XPlat Code Coverage"`, 2026-10-18): 100% of lines and branches of `AnimVarietyService`, `GaitVarietyService` and `GaitVariantCatalog`.

## How to Test in Game

1. Custom Battle: pick an orc or goblin culture for one side with at least a few dozen infantry, start, let the line march at the enemy.
2. Settings on (default): soldiers should step out of phase and the line should look uneven. `[AnimVariety] gait ...` lines appear in the TAOM log at the first spawn and at the end of the battle. Expect the spread to show while the line walks and to shrink at halts.
3. Turn **Random step size per soldier** off, start a new battle and compare the same line: the lockstep returns. Turn it on again and set **Step size minimum factor** to 0.80 for a wider spread.
4. Turn **Random gait set per soldier** off and on. On, the orc summary line lists the three sets with about a third each; off, only `as_orc_warrior`. Goblins have no variants yet (their `=missing` entries).
5. Check a mounted orc (warg rider), the player's own orc character and a human troop: none of them should log or change.
6. Visit a town with an orc or goblin resident: the log may count them in `n=`, and they should look the same as before the feature.
7. Look at the log of a normal battle: it must hold no `Action set "..." could not be found` line from the variant check.
8. With **Swing alternative probe (diagnostic)** on (the default) and no `act_taom_` code loaded, each mission logs `no alternative groups found; swing tracking off` once. Turn it off and confirm no `[AnimVariety] group`, `swings` or `no alternative groups found` line appears.

**Result, 2026-10-18 (Mike's Custom Battle at 09:41, `taom_debug_2026-10-18_09-27-38.log`):** `as_orc_warrior_b=present as_orc_warrior_c=present`; the mission summary `gait set=as_orc_warrior n=125 variants as_orc_warrior=38 as_orc_warrior_b=37 as_orc_warrior_c=50 step n=125 min=0.765 max=0.860 mean=0.814`; the probe's one line; no missing clip, missing set or duplicate name in the rgl log. Mike's verdict on the look is owed.

## Unverified

- The in-game look of the step size spread. The numbers of one run are in How to Test ("Result, 2026-10-18"); the look is Mike's call.
- Whether native `SetActionSet` on a freshly built agent drops a spawn-time action on channel 0 that matters. `BuildAgent` restarts the current channel-0 action if there is one (`Mission.cs:4056-4060`), and the native init clears it; vanilla's own suffix swap does the same, and battle spawns have no such action.
- Rider safety, which is why riders are skipped.
- Whether Coop's puppet spawner passes the owning peer's body seed.
- The over-cap spine blend into attacks: the main orc's spine is 40 degrees (cap 30) and the clips were restyled with `--allow-over-cap`; how an attack blends from a walk with it is an in-game check ([orc-goblin-locomotion.md](orc-goblin-locomotion.md)).
- Clip preload: TAOM swaps the set before `AgentVisuals.CheckResources` (`Mission.cs:4392`, after the `OnAgentBuild` loop at `:4390`), where vanilla's suffix swap runs after it (`:4531-4534`). Whether `CheckResources` queues the replaced set's clips, and whether three gait sets fit the on-demand clip budget, is not checked; the `[AnimMem]` probe is the tool for it.
- The probe has never counted a real swing, and no `act_taom_*` code is shipped to count. Its 0.01 blend-out tolerance is a guess, and a missing melee table row (the real first-swing crash) cannot be checked from managed code.
- The look of town and disguise orcs after their set is swapped twice.
- Whether the native reads of the attacker's channel-1 action and progress (`Channel1ActionName`, `Channel1Progress`,
  now in `RecordMeleeHit`) are safe on every thread that raises `OnAgentHit`. They run before the write is deferred, and
  the managed getters give no thread guarantee. The deferred closure itself is safe: it holds captured values and the
  agent map is reference-keyed, so a recycled slot cannot receive the sample (Codex S4, 2026-10-18).

## Changelog

- 2026-10-18: the orc variant sets `as_orc_warrior_b` and `as_orc_warrior_c` are installed in the live Armory with the regenerated main orc clips; the in-game check is owed.
- 2026-10-18: the swing probe is on by default for the swing tests (Mike); turning it off later needs a renamed setting.
- 2026-10-18: review fixes. The variant catalog lists the engine's action sets once instead of probing each id (the probe made the engine log "could not be found"); `as_orc_female_warrior` left the base sets; the swing probe got an MCM off switch (`EnableSwingAlternativeProbe`, default off), skips missile hits, compares blend-out starts and words an alt code without a clip as a disabled group instead of a crash risk; the step size hint states the real range; no minimum factor warning while the step lever is off.
- 2026-10-17: gait variety added (random step size, random gait set) beside the swing probe.
- 2026-10-17: the orc gait sets got content (main, B, C), staged in the Armory; not imported, so the variants are still missing in game.

## GitHub Issue

#817, filed on 2026-10-18 at Mike's word (he had deferred it on 2026-10-17) and closed with the install, labelled `triage-needs-ingame` while his verdict on the look is owed. Owed checks are in Unverified and How to Test; the clip half is tracked in #791.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/bannerlord-animation-system-map.md](../reference/bannerlord-animation-system-map.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/lotrlome-armory-snapshot/README.md](../reference/lotrlome-armory-snapshot/README.md)
- [docs/reviews/rca-animation-variety-2026-10-17.md](../reviews/rca-animation-variety-2026-10-17.md)

<!-- backlinks-end -->
