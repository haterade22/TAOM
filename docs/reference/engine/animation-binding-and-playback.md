# Bannerlord animation binding + action playback (Phase 2)

> **One process, traced end-to-end from the decompile** (v1.4.5): how an agent gets its animation rig (skeleton +
> action set) from its `Monster`, and how an action actually plays. This is the runtime flow that *consumes* the
> clip flags documented in [bannerlord-animation-clip-flags.md](../bannerlord-animation-clip-flags.md), and the
> successor to [agent-spawn-and-render-pipeline.md](agent-spawn-and-render-pipeline.md) (Phase 1 — `CreateAgent`
> calls `FillAnimationSystemData`; `BuildAgent` calls `SetActionChannel`). Part of the phased engine study.

## WHAT it is

Two linked sub-processes:
- **Binding** — at spawn, the agent's whole animation rig (which **action set** maps `act_*` → clips, which
  **monster_usage** governs movement/mount actions, the **skeleton bone indices**, the **gait step size + speed
  limits**) is assembled from the agent's **`Monster`** and handed to the native engine.
- **Playback** — at runtime, code asks the agent to play an action on a body channel; the native engine resolves
  it to a clip via the action set, arbitrates priority, applies the clip's `AnimFlags`, blends, and plays.

**The `Monster` is the single source of the animation rig.** Everything a creature animates flows from its
`monsters.xml` definition.

## HOW it works — binding

### `Monster.FillAnimationSystemData(...)` (MountAndBlade.cs:101588 / overload :101595) ⭐
Builds an `AnimationSystemData` from the Monster:
```
ActionSet            = monster.MonsterMissionData.ActionSet  (or FemaleActionSet)   // from Monster.ActionSetCode
MonsterUsageSetIndex = Agent.GetMonsterUsageIndex(monster.MonsterUsage)             // from Monster.MonsterUsage
WalkingSpeedLimit / CrouchWalkingSpeedLimit                                          // from Monster
StepSize             = stepSize                                                      // the gait stride datum
Bones = { HeadLookDirection, SpineLower/Upper, NeckRoot, Pelvis, R/LUpperArm,
          FallBlowDamage, TerrainDecal0/1, ragdoll bones, ... }                      // the bone-index map
Biped = { ragdoll stationary-check bones, foot-IK bones, ... }
```
So `AnimationSystemData` = **action set + monster usage + speed limits + step size + the full bone-index map**.

- `ActionSetCode` (the `action_set="…"` attribute, e.g. `as_spider`/`as_elephant`/`as_adod_wolf`) is read from
  `monsters.xml` (`TaleWorlds.Core` Monster, Core.cs:19126/19395) and resolved to an `MBActionSet` via
  `MBActionSet.GetActionSet(code)`. The action set lists `<action type="act_*" animation="<clip>"/>` (the
  action_sets.xml the clip-flags doc covers).
- `MonsterUsage` (the `monster_usage="…"` attribute) → `MonsterUsageSetIndex`: governs mount/dismount + which
  movement actions apply (the 1.4.X-native pattern for animals is `monster_usage="horse"`).
- The **bone indices** are resolved by name from the skeleton — they're why `monsters.xml` lists
  `head_look_direction_bone`, `pelvis_bone`, the IK end-effectors, ragdoll bones, etc. (a wrong/missing bone name
  here breaks look-at, ragdoll, IK).

This `AnimationSystemData` is passed to **`CreateAgent`** (Phase 1, Mission.cs:4042) → native `CreateAgentInternal`,
which creates the agent's native animation system. So the rig is bound **at agent creation**.

### `Agent.SetActionSet(ref AnimationSystemData)` (Agent.cs:2574)
Swaps the agent's action set at runtime — native `IMBAgent.SetActionSet` + a network broadcast. Used for **suffixed
action sets**: `MBGlobals.GetActionSetWithSuffix(monster, isFemale, "_villager"/"_lord"/"_facegen")` (e.g.
`SpawnTroop`'s `specialActionSetSuffix`, Mission.cs:4480). This is the mechanism behind TAOM's
`as_<race>_facegen` / `_villager` action-set requirements — the engine appends the suffix to the Monster's base
`ActionSetCode` and resolves a distinct set.

### `Agent.ActionSet` (Agent.cs:696)
`=> new MBActionSet(MBAPI.IMBAgent.GetActionSetNo(GetPtr()))` — the agent's *current* action set, read from native.
TAOM's spider/elephant gate on `agent.ActionSet.IsValid` before driving actions (an unbound/invalid action set =
no animations).

## HOW it works — playback

### `Agent.SetActionChannel(channelNo, actionIndexCache, ignorePriority, additionalFlags, blend…)` (Agent.cs:2368) ⭐
```
int index = actionIndexCache.Index;
return MBAPI.IMBAgent.SetActionChannel(GetPtr(), channelNo, index + actionShift,
        (ulong)additionalFlags, ignorePriority, blendWithNextActionFactor, actionSpeed,
        blendInPeriod, blendOutPeriodToNoAnim, startProgress, …);
```
A thin native pass-through. The managed side supplies the **action index** (an `ActionIndexCache` resolved from an
`act_*` code via `ActionIndexCache.Create("act_…")`) + the **`AnimFlags`** (cast to `ulong` — priority in the low
byte + behavior bits) + blend params. **Everything else is native:** the engine looks up the action in the agent's
action set → the bound clip, arbitrates priority against the channel's current action (≥ wins, `ignorePriority`
bypasses — see the clip-flags doc Cat 1), applies the clip's authored `AnimFlags` ∪ the passed `additionalFlags`,
blends over `blendInPeriod`, and advances the clip (unless `disable_auto_increment_progress`).

- **Channels:** `0` = lower body / locomotion, `1` = upper body / action. Independent priority arbitration per
  channel (so an upper-body attack overlays a running lower body).
- `GetCurrentAction(channelNo)` (Agent.cs:2748) reads the channel's current action; `GetCurrentAnimationFlag` /
  `GetCurrentActionPriority` read the live flags/priority (used by e.g. shield-block + ladder-climb detection).

## The native boundary (what's native)

`FillAnimationSystemData`/`SetActionSet`/`SetActionChannel`/`GetActionSetNo`/`GetCurrentAction` are managed
*orchestration*; the **actual animation system — clip resolution, priority arbitration, `AnimFlags` interpretation,
blending, the gait builder (speed-synched locomotion via `synch_with_movement` + the stride reference clip),
root-motion (`GetDisplacementVector`)** — runs in `TaleWorlds.Native.dll` behind the `IMBAgent`/`IMBAnimation`
`[EngineMethod]` callbacks. The managed side picks *what* to play; the engine decides *how* it looks.

## What a CREATURE needs to animate (the dependency chain)

For a custom creature (spider/elephant/warg) to animate correctly, its `Monster` must supply a consistent rig:
1. **`action_set="<as_creature>"`** — a valid action set bound to the creature's skeleton, mapping `act_*` types to
   `an_*` clips. Invalid/missing → no animations (the agent T-poses or freezes).
2. **`monster_usage="<set>"`** — governs movement/mount actions (1.4.X animals use `"horse"`).
3. **Correct bone names** in `monsters.xml` (`head_look_direction_bone`, `pelvis_bone`, foot-IK bones, ragdoll
   bones) — resolved by name against the skeleton; wrong names break look-at/IK/ragdoll.
4. **Clips with the right `AnimFlags`** baked in (the per-type recipe — movement: `synch_with_movement`+`cyclic`;
   attack: `lock_movement`+`enforce_all`; priority in the low byte). The spider's `an_spi_*` clips currently ship
   with **zero flags** → even with a valid action set, locomotion would slide / not loop.
5. **A compiled clip (`_anm.tpac`) for every `animation="…"` referenced** — a missing `_anm` → the gait builder
   divides by a 0 duration → DivideByZero at spawn (the spider's `TEMP-ANM-UNBLOCK` substitutions).

So "the creature won't animate" decomposes into: action set invalid? monster_usage wrong? bone names wrong? clip
missing `_anm`? clip flags unset? — check in that order.

## Evidence (file:line, v1.4.5 shipping decompile)
- `Monster.FillAnimationSystemData` — `_shipping_build/TaleWorlds.MountAndBlade.cs`:101588 (+ overload :101595): bundles ActionSet/MonsterUsageSetIndex/speed limits/StepSize/bone map into `AnimationSystemData`.
- `Monster.ActionSetCode`/`FemaleActionSetCode` — `_shipping_build/TaleWorlds.Core.cs`:19126/19128 (read from XML :19395/19400).
- `Agent.ActionSet` — `Agent.cs`:696 (native `GetActionSetNo`); `SetActionSet` — `Agent.cs`:2574 (native + suffixed-set swap, used by `SpawnTroop` Mission.cs:4480); `SetActionChannel` — `Agent.cs`:2368-2372 (native pass-through; index + `(ulong)AnimFlags`); `GetCurrentAction` — `Agent.cs`:2748.
- Consumed at creation: `CreateAgent` — `Mission.cs`:4042 (`monster.FillAnimationSystemData(stepSize, false, isFemale)`); initial action played in `BuildAgent` — `Mission.cs`:4026-4030 (`SetActionChannel`).
- Native: `IMBAgent` `[EngineMethod]` `set_action_channel` / `set_action_set` / `get_action_set_no`; `IMBAnimation` `get_animation_flags` (the clip-flags doc).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/engine/monster-model.md](./monster-model.md)

<!-- backlinks-end -->
