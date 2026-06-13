# Bannerlord agent combat stats — AgentStatCalculateModel / AgentDrivenProperties (Phase 15)

> **One process, traced from the decompile** (v1.4.5): the per-agent combat-stat pipeline — how an agent's movement
> speed, weapon handling, accuracy, armor, and mounted stats are computed from its skills/equipment/mount and handed to
> the native combat sim as a bag of **`AgentDrivenProperties`**, via the **`AgentStatCalculateModel`** GameModel. This
> is the model TAOM's `TaomAgentStatCalculateModel` overrides for **career passives + the war-elephant mount-lock**.
> Closes the in-mission stack (Phases 1,3,4,12,13,14). Part of the phased engine study.

## WHAT it is

Every agent has an **`AgentDrivenProperties`** — a flat bag of ~99 `float` "driven properties" (`DrivenProperty` enum,
`None=-1` … `98`) that the **native combat simulation reads every tick** (move speed, swing speed, accuracy, armor,
mounted charge, etc.). The **`AgentStatCalculateModel`** (a mission-side GameModel) is what **fills** that bag: once at
spawn, and again whenever the agent's state changes (mount, weapon, perk).

## HOW it works

### `AgentDrivenProperties` (AgentDrivenProperties.cs:6) — the stat bag
A `class` holding the float array, addressed two ways:
- **`GetStat(DrivenProperty)` / `SetStat(DrivenProperty, value)`** — raw indexed access.
- **~90 named accessor properties** that wrap a specific `DrivenProperty`: e.g. `SwingSpeedMultiplier`
  (`DrivenProperty.SwingSpeedMultiplier`, :16), `HandlingMultiplier` (:40), `ReloadSpeed` (:52),
  `WeaponInaccuracy` (:76), `MaxSpeedMultiplier`, and the mount stats **`MountSpeed`** (=93), **`MountManeuver`** (=92),
  **`MountChargeDamage`** (=52), **`MountDifficulty`** (=53). (`DrivenProperty.cs` lists them; values run 0..98.)

### `AgentStatCalculateModel : MBGameModel<AgentStatCalculateModel>` (AgentStatCalculateModel.cs:8) — the filler
A **mission-side** GameModel (Phase 7 registry: `GetModel<T>` last-added-wins). Contract:
- **`InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData)`** (:14, abstract) — fill the
  **base** ADP at spawn (from skills, armor, weapons, mount).
- **`UpdateAgentStats(agent, agentDrivenProperties)`** (:28, abstract) — **recompute** the ADP whenever state changes.
  Called from many places: the `MountAgent` setter calls it (Phase 14 — mounting changes speed/charge), weapon wield,
  perk application. **This is the hot method TAOM overrides.**
- Other abstracts: `GetDifficultyModifier` (:30), `CanAgentRideMount(agent, targetMount)` (:32),
  `GetWeaponDamageMultiplier` (:119), `GetEquipmentStealthBonus` (:121), `GetSneakAttackMultiplier` (:123),
  `GetKnockBack/KnockDown/DismountResistance` (:125-129), `GetBreatheHoldMaxDuration` (:131).
- Virtuals (override-optional): `GetEffectiveMaxHealth` (:44), `GetEnvironmentSpeedFactor` (:49), `GetEffectiveSkill`
  (:109), `GetEffectiveSkillForWeapon` (:114), `HasHeavyArmor` (:34), `GetWeaponInaccuracy` (:76).

### Flow
```
spawn: BuildAgent (Phase 1) → InitializeAgentStats(agent, spawnEquipment, ADP, buildData)   [base stats]
  state change (mount/weapon/perk) → UpdateAgentStats(agent, ADP)                            [recompute]
  every tick → native combat sim reads ADP.GetStat(DrivenProperty.X)                         [consume: speed/accuracy/charge…]
```

## WHY it's shaped this way

A flat float bag (`AgentDrivenProperties`) is the **ABI between managed stat-logic and the native combat sim**: the
engine reads fixed `DrivenProperty` slots each tick at native speed, while all the *computation* of those slots lives in
a swappable managed GameModel. `Initialize` (once) vs `Update` (on change) avoids recomputing ~99 stats every frame —
only when something actually changed. Making it a GameModel lets mods retune any stat without touching the sim.

## TAOM relevance + gotchas
- **`TaomAgentStatCalculateModel`** (`Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`, registered
  `campaignStarter.AddModel<AgentStatCalculateModel>(...)` at SubModule.cs:447). It **inherits
  `SandboxAgentStatCalculateModel`** — note: **there is no `DefaultAgentStatCalculateModel`**; the abstract base is
  `AgentStatCalculateModel` and the SandBox concrete is `SandboxAgentStatCalculateModel`, so TAOM inherits the
  most-derived *vanilla* model (keeping all SP stat logic) rather than a `Default*` (a documented exception to
  gamemodels.md rule 2). Overrides:
  - `UpdateAgentStats` → `base` → `ICareerAgentStatService.ApplyAgentStatModifiers(...)` (career passives mutate the
    ADP) → set `ADP.MountDifficulty = ElephantConfig.MountDifficulty` for the elephant Monster (near-infinite → non-rider
    AI can't take the elephant — 1-for-1 upstream-pack mount-lock).
  - `CanAgentRideMount` → `false` for an elephant Monster.
  - `GetEffectiveMaxHealth` → `+ career Health passive` for heroes.
- **One override per type → consolidation** (Phase 7 last-added-wins): the **single** `AgentStatCalculateModel` slot
  carries **both** career passives **and** the elephant lock. You cannot register two — combine them in one model (the
  career model gained the elephant branch via `IElephantAttackService`).
- **Thin boundary** (gamemodels.md rule 4): the model only extracts primitives from the sealed `Agent` and delegates;
  all branching/stat math is in `ICareerAgentStatService`, the elephant id-check in `IElephantAttackService`. The body
  uses only ternaries (no inline `if`/`foreach`/`switch`).
- **Research scale + downstream consumer before touching any ADP** (`feedback_engine_scale_research`): a driven
  property may be a *multiplier* or *additive*, may be *clamped*, and is consumed by a specific native path — a change
  with the wrong scale has "no effect or wrong magnitude." Decompile both the property's range and its consumer first.
- **NaN/Infinity guard** (`feedback_clamp_nan_infinity_propagates`): a NaN written into the ADP propagates through the
  native sim (and any `5f * (1f - NaN)` style math) and freezes/breaks the agent — validate finite before `SetStat`.

## The native boundary
`AgentDrivenProperties` is a **managed** object, but it is the **interface to the native combat simulation** — the
engine reads `GetStat(DrivenProperty)` every tick for movement, weapon handling, accuracy, and mounted charge.
`AgentStatCalculateModel.Initialize/UpdateAgentStats` are **managed** (called *from* native at spawn/state-change). So
the *stat computation* is yours (managed, overridable); the *per-tick consumption* is native. This is why a bad ADP
value surfaces as wrong in-sim behavior rather than a managed exception.

## Evidence (file:line, v1.4.5)
- `AgentStatCalculateModel.cs`:8 (`abstract : MBGameModel<AgentStatCalculateModel>`), :14 (`InitializeAgentStats`), :28 (`UpdateAgentStats`), :32 (`CanAgentRideMount`), :44 (`GetEffectiveMaxHealth`), :49 (`GetEnvironmentSpeedFactor`), :109 (`GetEffectiveSkill`), :119-131 (damage/stealth/resistance abstracts).
- `AgentDrivenProperties.cs`:6 (`class`), :16/:40/:52/:76 (named accessors → `GetStat`/`SetStat`). `DrivenProperty.cs`: enum `None=-1`..`98`; mount slots `MountChargeDamage=52`, `MountDifficulty=53`, `MountManeuver=92`, `MountSpeed=93`, `MountDashAccelerationMultiplier=94`.
- TAOM: `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` (`: SandboxAgentStatCalculateModel`; `UpdateAgentStats`/`CanAgentRideMount`/`GetEffectiveMaxHealth`), `Main/SubModule.cs`:447 (`AddModel<AgentStatCalculateModel>`). Gotcha memories: `feedback_engine_scale_research`, `feedback_clamp_nan_infinity_propagates`; `.claude/rules/gamemodels.md` (thin-boundary rule 4). Linked: gamemodel-system.md (Phase 7), mount-and-rider-runtime.md (Phase 14, the `UpdateAgentStats`-on-mount call).
