# ADOD_Beasts — full architecture, lifecycle & TAOM port comparison

> **Purpose.** Understand the ENTIRE `ADOD_Beasts` mod end-to-end — every class, the start-to-finish runtime
> lifecycle, and *why* it was built the way it was — and place TAOM's port inside that full picture (what we
> ported faithfully, what we deliberately scoped out, where we're safer, and the 1.2.12→1.4.5 drifts). So no future
> session re-decompiles ADOD in slices. Built 2026-06-06 from a full `ilspycmd` decompile of `ADOD_Beasts.dll`
> (both builds), line-by-line audited against TAOM and verified against installed v1.4.5 DLLs (`taom-src`).
> **ADOD_Beasts targets Bannerlord ~1.2.12 — it is a behavioral reference only; never a runtime dependency.**
> Companions: [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md) (the howdah in depth),
> [bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md), [elephant.md](../features/elephant.md),
> [spider.md](../features/spider.md) + its RCA.

## 1. What ADOD_Beasts is, and its two design philosophies

ADOD_Beasts ("A Day of Defeat Beasts") adds two creatures, built on **two different engine idioms** — and that
choice is the root of everything else:

| Creature | Engine idiom | Why that idiom |
|---|---|---|
| **War elephant** | a **vanilla horse-class MOUNT** (`agent.MountAgent`) ridden by a human, with a **howdah** `UsableMachine` crew platform bolted on | Reuses the engine's entire ridden-mount pipeline for free (spawn, locomotion, rider attach, mount AI). The only custom code is the auto-trample AI (an `AgentComponent` on the mount) + the crew platform. |
| **Wolf** | a **map-acquired single PET** bound to the main hero, spawned riderless via the public `Mission.SpawnMonster`, driven by a hand-rolled FSM `AgentComponent` | A companion that follows + fights for the player needs persistent campaign state (which wolf you own) + per-frame follow/aggro logic the engine doesn't provide for a free creature. |

**TAOM's relationship to each is different:**
- The **elephant mount-lock + the structural trample/tusk mechanic** are ported from ADOD, re-homed onto a **ridden
  mount** troop (the mahout rides a `taom_war_elephant` Monster, like ADOD's ridden mount) — but the attack **damage**
  (trample 50-100 / tusk 50-75, randomized per victim, 2026-06-15) and the cooldown **cadence** (2026-06-10) are TAOM's
  deliberate rebalance, NOT 1-for-1 with ADOD's fixed-10×2 / 0.001-per-tick roll. The **howdah is not ported** (a
  separate future feature).
- The **wolf is not ported at all.** TAOM's **spider** solves the same "riderless non-humanoid combatant" problem
  with an **independent design** (a behavior tree reusing Warg/AdvancedCombat), not a port of the wolf's FSM. The
  wolf is a *reference* (its public-`SpawnMonster` spawn path is the spider's recommended render fix).

## 2. The full runtime lifecycle — start to finish

This is the end-to-end process. Each step notes ADOD's mechanism and TAOM's equivalent.

```
LOAD TIME
  SubModule.OnSubModuleLoad
    └─ new Harmony("mod.adodbeasts.bannerlord").PatchAll()   → applies ElephantCameraPatches + CharacterObjectPatch
        TAOM: single shared _harmony, explicit per-feature Patch calls; elephant needs ZERO patches.

GAME START (campaign)
  SubModule.OnGameStart
    ├─ ElephantCharacters.LoadMappings()                     → reads additional_elephant_characters.xml (armor→crew→count)
    ├─ campaignStarter.AddBehavior(ADODBeastsCampaignBehavior) → the WOLF acquisition/save behavior
    └─ wrap existing AgentStatCalculateModel in ADODAgentStatCalculateModel  (the mount-lock + pilot buffs)
        TAOM: resolves IElephantAttackService; constructs TaomAgentStatCalculateModel (subclass, not a wrapper)
              via AddModel — the elephant mount-lock rides in the SHARED career stat model. No CampaignBehavior
              (that's the wolf), no LoadMappings (no howdah).

CAMPAIGN MAP (per day)
  ADODBeastsCampaignBehavior.WolfDailyTick
    └─ if near winter coord (Vec2(615,2000), <300f) && rand<wolfAcquireChance → "Take the Wolf" inquiry → _acquiredWolfId
        Persisted by ADODBeastsSaveDefiner (base 301412299) → SaveableField _acquiredWolfId.
        TAOM: NONE. The elephant has no campaign state; the spider is a rostered troop. No SaveDefiner needed.

MISSION INIT
  SubModule.OnBeforeMissionBehaviorInitialize
    └─ if (Campaign && not Arena && not AtmosphereIndoor) → mission.AddMissionBehavior(new ADODBeastsMissionLogic())
        TAOM: SubModule.OnMissionBehaviorInitialize adds ElephantMissionBehavior UNCONDITIONALLY (no arena/indoor
              gate — house pattern shared with Warg+Spider; inert without elephant agents but pays a once-/mission
              AllAgents scan everywhere). ← see §6, a candidate refinement.

PER AGENT SPAWN  — ADODBeastsMissionLogic.OnAgentBuild(agent, banner)
    ├─ elephant: if agent.IsHuman && HasMount && IsElephant(MountAgent):
    │     read rider Equipment[HorseHarness=11] → ArmorToCharacterMap → GameEntity.Instantiate(adod_howdah_{1,2,4}_agent)
    │     → wire ADODHowdahObject.elephantAgent/elephantRider → MountAgent.AddComponent(ADODBeastsElephantAgentComponent)
    │   wolf: if agent==Agent.Main && _acquiredWolfId set → Mission.SpawnMonster(wolfItem) → AddComponent(WolfComponent)
    │     → register proxy "adod_wolf_target" entity → AI humans get AddController(ADODBeastsHumanAIAgentController)
    └─ TAOM: OnAgentBuild only adds the agent to a `_elephants` shadow list if its Monster.StringId==taom_war_elephant.
          No howdah, no component on the mount, no wolf, no AI-controller. (Trample is driven from OnMissionTick.)

PER FRAME  — ADODBeastsMissionLogic.OnMissionTick(dt)
    ├─ if Input.IsPressed(57 = Space) → MainAgentElephantDamageLogic(Agent.Main)   (player manual trample)
    ├─ (0.25s throttle) lift wolf proxy entities, run HumanAIAgentControllers, assign wolf targets
    └─ hand-iterate Mission.Agents, call each WolfComponent.Tick(dt)
       Elephant trample itself: ADODBeastsElephantAgentComponent.OnTickAsAI(dt) on the mount.
       Howdah: ADODHowdah.OnTick → UpdateHowdahMovement (frame-glue to neck) ; each seat OnTick force-spawns+locks crew.
    └─ TAOM: ElephantMissionBehavior.OnMissionTick iterates `_elephants`, per elephant rolls TrampleChancePerTick
          FIRST (perf pre-gate), then ShouldAiTrample(distance<3, dot>0.25) → radial GetNearbyAgents → CustomAttacksUtils.
          NO input trample, NO hand-tick (v1.4.5 Agent.Tick auto-calls component.OnTick), NO wolf, NO howdah.

ON HIT  — ADODBeastsMissionLogic.OnAgentHit → 30% elephantHit SFX; wolf retaliation targeting
    TAOM: none (hit-SFX deferred as cosmetic; no wolf).

MISSION END / TEARDOWN
  ADOD: OnBattleEnded → clear _animals/_targets/_agentControllers, remove wolf proxy entity.
  TAOM: ElephantMissionBehavior.OnRemoveBehavior → clear `_elephants`. (OnRemoveBehavior is broader than
        OnBattleEnded; both valid v1.4.5.)
```

**The one big WHY:** ADOD's elephant is a *mount*, so the engine does spawn/locomotion/rider-attach for free and
ADOD only writes the trample + howdah. TAOM makes the elephant a *Monster-swap troop* (the project's standing rule
that non-humanoid creatures are troops, never rideable mounts — mounts crash on the campaign-map party-icon
`ForceUpdateBoneFrames` path). That single re-homing is why TAOM drops the howdah, the rider camera, the
GetPower patch, and the mount-AI — they're all properties of the ridden-mount idiom TAOM doesn't use.

## 3. Subsystem-by-subsystem — the WHY + the TAOM comparison

### 3A. Elephant trample + mount-lock — **FAITHFULLY PORTED** (Subsystem A)
- **What ADOD does:** `ADODBeastsElephantAgentComponent.OnTickAsAI` — when the AI rider has a target within **3 m**,
  facing it (look-dir dot > **0.25**), rolls **0.001/tick**; on success deals a radial knockdown (base **10**, ×2,
  ¼ vs a shield-blocking victim) to enemies within **2 m**. `ADODAgentStatCalculateModel` locks the mount:
  `CanAgentRideMount=false` + `MountDifficulty=999` so AI can't steal it.
- **Why coded that way:** a low per-tick probability gives an organic, occasional trample rather than a metronome;
  the facing/range gates keep it to the front arc; the mount-lock exists because ADOD's elephant is a real
  rideable horse-class mount that the AI would otherwise commandeer.
- **TAOM port (verdict: HIGH faithfulness on the mechanic, no bugs):** the facing/range **gate** constants are 1-for-1
  (`ElephantConfig.cs`), but the **damage** and **cooldown** constants are TAOM's deliberate rebalance — trample 50-100 /
  tusk 50-75 randomized per victim (2026-06-15) replacing ADOD's fixed base-10×2, and a fixed 10s/4s cooldown model
  (2026-06-10) replacing the 0.001/tick probability. The gate + damage formula are extracted to a unit-tested
  `IElephantAttackService` (ADR-002/007); the mount-lock folds into the shared `TaomAgentStatCalculateModel`. **Safer than ADOD** in two places: `?.` null-guards on
  `targetMount.Monster` (ADOD had a latent NRE), and exact `Monster.StringId == "taom_war_elephant"` identity vs
  ADOD's brittle `Name.ToLower().Contains("elephant")` substring. **Crucially, TAOM moved the tick off the dead
  `OnTickAsAI` onto `MissionLogic.OnMissionTick`** (see §5) — so TAOM's trample actually fires on 1.4.5 where
  ADOD's would not. The one equivalent-but-different choice: TAOM rolls the probability *before* the distance/facing
  native calls as a perf pre-gate, feeding the same roll into `ShouldAiTrample` (identical firing distribution).

### 3B. Howdah crew platform (`UsableMachine`) — **NOT PORTED** (Subsystem B)
- **What/why:** the multi-troop "crew on the elephant's back" is a `UsableMachine` (`ADODHowdah`) whose
  `StandingPoint` children are seats. It is **not bone-parented** — a free scene entity whose world frame is
  copied onto the elephant's neck point **every tick**; each seat **force-spawns** its own AI archer (armor-tier →
  `additional_elephant_characters.xml` → 1/2/4 crew) and locks it. Built as a `UsableMachine` because that is the
  engine's only crew-platform primitive (same as a siege engine / ship deck).
- **TAOM:** zero howdah. The full line-level port spec + the 4 v1.4.5 drifts it must fix live in
  [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md). This is the bigger future sub-feature.

### 3C. Wolf creature-AI — **NOT PORTED; spider is independent** (Subsystem C)
- **What/why ADOD:** a 6-state event-driven FSM in one ~600-line `AgentComponent` (Idle/Follow/Aggro/Attack/
  Retreat/Turning) that follows the owner, chases enemies, and hand-builds melee `Blow`s via `RegisterBlow` on a
  timer; plus a custom `AgentController` on enemy **humans** + proxy "wolf-target" `GameEntity`s so the AI knows to
  attack the pet; plus a `MissionView` HP bar. Owner-bound because it's a pet.
- **TAOM spider (independent, better-fit):** a **behavior tree** (Selector: no-enemy→sleep / engage→move→bite→sleep)
  reusing `BehaviorTreeWrapper` + `AdvancedCombat` (`SpatialGrid`, `BoneCollision`, real `CustomAttack` bite
  instead of a timer-Blow). An independent team combatant (scans all agents for nearest enemy), no owner, no proxy
  entities, no human-AI-controller. **The spider's design is the more maintainable pattern; do not adopt the
  FSM-in-a-component.**
- **The one reference that matters:** the wolf spawns via the **public `Mission.SpawnMonster(mountItem, …)`** with a
  single un-split mesh — exactly the spider's recommended render fix (the spider's *reflected `FromHorseObj`* chain
  is what AccessViolates). See §6.
- **Extractable polish (optional, §6):** the distance→speed ladder, the `Mission.Mode==Battle/Deployment` attack
  gate, the vision-cone gate, the 0.1s tick throttle.

### 3D. Infra — orchestration / save / settings / UI / patches / SubModule / NativeHook (Subsystem D)
The lifecycle skeleton (§2) plus the support classes. TAOM's relevant ports are faithful; the rest is correctly
scoped out:
- **`ADODBeastsSaveDefiner`** persists the **wolf** (`_acquiredWolfId`), NOT the elephant → **the elephant feature
  needs no SaveDefiner**, and TAOM correctly ships none. (If the wolf is ever ported, it needs its own
  `SaveableTypeDefiner` with a unique base id — `feedback_saveable_typedefiner_localid_offset.md` — never reuse
  ADOD's `301412299`.)
- **`ADODBeastsSettings`** (MCM): `elephantCameraDistance` (rider camera, moot for a troop) + `wolfAcquireChance`
  (wolf). TAOM uses compile-time `ElephantConfig.cs` consts; no MCM knob for the elephant.
- **`ADODBeastsMissionView`/VM**: the **wolf HP bar**, not elephant. Not ported.
- **Patches:** `ElephantCameraPatches` (rider camera pull-back — no rider in TAOM) + **`CharacterObjectPatch`**
  (adds howdah-crew power to the elephant's `GetPower` — tied to the howdah TAOM doesn't have). Both correctly
  absent.
- **`NativeHook` (EasyHook) — DEAD IMPORT.** `using NativeHook;` appears but is **never used** anywhere in the
  decompiled body. **The elephant does NOT need native movement hooks to move** — it's a vanilla mount (ADOD) /
  engine-driven Monster-swap troop (TAOM). No NativeHook port required. *(This corrects an earlier impression that
  the native hooks were load-bearing for movement; they are not — they're an unused reference.)*

## 4. Port verdict

TAOM's elephant port is **faithful where it matters and safer in several places**: the trample (facing/range gates
1-for-1; damage + cadence deliberately rebalanced — trample 50-100 / tusk 50-75, fixed-cooldown model), the mount-lock (`CanAgentRideMount`/`MountDifficulty=999`), and the `: MissionLogic` lifecycle
are all correct, with added null-guards, exact-id matching, the dead-`OnTickAsAI`→`OnMissionTick` fix, and pure
logic extracted to a tested service. Every **non-port** (howdah, rider camera, `GetPower` patch, MCM, manual-Space
trample, hit-SFX, the entire wolf subsystem + its SaveDefiner/MissionView) is **correctly scoped out** because
TAOM's elephant is a non-rideable creature-troop, not ADOD's ridden mount + howdah. **No bugs found in the port.**

## 5. The 1.2.12 → 1.4.5 API drift catalogue (apply to any future ADOD port)

| ADOD API (1.2.12) | v1.4.5 reality | Fix |
|---|---|---|
| `AgentComponent.OnTickAsAI(float)` | **does not exist** (only `OnTick`/`OnTickParallel`) | use `OnTick(dt)` (auto-called by `Agent.Tick`). TAOM's elephant already does (`MissionLogic.OnMissionTick`); a verbatim component port would be a dead override / compile error. |
| `Agent.SetMovementDirection(ref Vec2)` | `SetMovementDirection(in Vec2)` | drop the `ref` |
| `StandingPoint.OnUse(Agent)` | `OnUse(Agent, sbyte agentBoneIndex)` | add the param (howdah) |
| `MissionObject.SetDisabled(bool)` toggle | bool is `isParentObject`; call **always disables** | re-architect seat enable/disable (howdah) |
| `UsableMachine.GetDescriptionText(GameEntity)→string` | `(WeakGameEntity)→TextObject` | re-sign (howdah) |
| `ActionIndexCache.Name` | `GetName()` | TAOM already migrated (elephant) |

**Verified PRESENT (no drift), so these ADOD patterns ARE portable:** `Mission.SpawnMonster` (both overloads),
`Agent.AddComponent`/`AddController`/`GetController<T>`, `AgentController` base, `Agent.SetScriptedPosition[AndDirection]`,
`SetMaximumSpeedLimit`, `SetWatchState`, `SetIsAIPaused`, `GetTargetAgent`, `ImmediateEnemy`, `RegisterBlow`,
`Mission.GetNearby[Enemy]Agents`, `GameEntity.Instantiate/SetFrame`, `AgentVisuals.GetGlobalStableNeckPoint`,
`Agent.Tick` auto-calling `component.OnTick`.

## 6. Actionable findings (beyond "the port is faithful")

1. **Spider render fix (high value):** the wolf proves the **public `Mission.SpawnMonster`** + single **un-split**
   mesh path renders a riderless creature. The spider's **reflected `FromHorseObj`** chain is what AccessViolates in
   `PreloadForRendering`. Switching the spider to the wolf's public path is the RCA's recommended cheapest fix.
2. **Elephant SubModule gate (low-risk refinement):** TAOM adds `ElephantMissionBehavior` to *every* mission; ADOD
   gated it to `campaign && !arena && !indoor`. Inert without elephant agents, but consider the gate to skip the
   once-/mission `AllAgents` scan where no elephant can spawn (note: Warg+Spider are also unconditional — a
   house-pattern decision, not an elephant-specific miss).
3. **Optional spider polish from the wolf:** a distance→speed ladder (`SetMaximumSpeedLimit` graduated) so it
   settles into bite range instead of orbiting; a `Mission.Mode==Battle/Deployment` guard on the bite; a
   vision-cone gate; the 0.1s tick throttle. None required; all cheap.
4. **Verify:** does `CustomAttacksUtils.TakeDamage` honor shield blocks the way ADOD's manual `Blow` path does
   (¼ damage + BlowFlag 256)? If not, blocking players take full spider-bite damage.

**ADOD anti-patterns NOT to copy:** the dead `Turning` FSM state; the `OnIdle?.Invoke` event-Action indirection
over a plain switch; `UpdateMovementDirection`'s ignored `turnSpeed` param (hardcodes 0.1f); `Name.Contains("elephant")`
identity; the hand-iterate-and-`Tick` loop (redundant — `Agent.Tick` auto-ticks components in 1.4.5).

## 7. Cross-references
- Howdah line-level port spec: [howdah-crew-mechanism.md](../features/elephant/howdah-crew-mechanism.md)
- Engine/toolchain + decompiled-source layout: [bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md)
- Animation clip flags (for the creature clip authoring): [bannerlord-animation-clip-flags.md](bannerlord-animation-clip-flags.md)
- Elephant feature: [elephant.md](../features/elephant.md) · Spider feature + render RCA: [spider.md](../features/spider.md), [rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)
- Full decompiled source (both builds): `E:\Decompiled_Bannerlord\{_shipping_build,_editor_build}\` (regen: `tools/decompile_bannerlord.ps1`)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/agent-spawn-and-render-pipeline.md](engine/agent-spawn-and-render-pipeline.md)
- [docs/reference/engine/gamemodel-system.md](engine/gamemodel-system.md)
- [docs/reference/engine/mount-and-rider-runtime.md](engine/mount-and-rider-runtime.md)
- [docs/reference/engine/save-system.md](engine/save-system.md)

<!-- backlinks-end -->
