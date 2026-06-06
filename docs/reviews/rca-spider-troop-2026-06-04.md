# RCA — Recruitable Giant Spider Troop (2026-06-04)

> **Status: redesigned mid-day.** The in-place `Mission.SpawnAgent` `Monster`-swap approach reviewed in the appendix below passed deep-review with no *code* bugs — then crashed **in-game** with a native AccessViolation, because letting vanilla `SpawnAgent` run on the swapped agent builds a humanoid skin (`dg_uruk`) on the spider skeleton. It was replaced the same day with a **detached non-humanoid combatant** (`CreationType.FromHorseObj`) spawn that never builds a humanoid skin. This top section is the RCA of that redesign + its five-layer crash journey + the architecture investigation. The appendix is retained as the (now-superseded) swap approach's review.

## Redesign — detached non-humanoid combatant

**Root insight:** the engine has exactly two agent-build shapes — *humanoid combatant* (skin + weapons + formation) and *mount* (no skin, no weapons, no formation). A non-humanoid **combatant** is a category the engine does not natively support. The swap approach tried to use the humanoid build → humanoid skin on a spider skeleton → AV. The detached approach uses the **mount** build (`FromHorseObj`, which skips `AddSkinMeshes`) and then re-adds, by hand, the combatant pieces a troop needs. The cost: every piece the mount build skips (weapon/wield state, formation membership) becomes a separate front, and the engine's formation/team-AI assumes every member is a weapon-carrying humanoid.

### The five-layer crash journey (each fixed in turn; all evidence from `ilspycmd` on installed v1.4.5 DLLs)

| # | Crash | Root cause | Fix |
|---|-------|-----------|-----|
| 1 | `DivideByZeroException` (native `CreateAgent`) | Spider anim files were registered in `LOTRLOME_Armory/ModuleData/project.mbproj` under **custom** `soln_spider_*` ids the runtime silently ignores → the `as_spider` action set was never loaded | Move anim files to top-level under **recognized** ids (`soln_action_sets`/`_types`/`_monster_usage_sets`). *Lesson: the engine loads `project.mbproj` files only for a fixed id allowlist; custom ids are dropped silently. Read `rgl_log` to confirm which files the engine actually opens.* |
| 2 | `AccessViolation` in `BuildAgent → PreloadForRendering` | (a) render data uninitialized — the free-mount path seats a `MountCreationKey` we omitted; (b) a `(0,0)` deploy direction normalized to a NaN frame | Reflected `Agent.SetMountInitialValues(name, MountCreationKey.GetRandomMountKeyString(...))` + a `Vec2.Forward` guard on invalid/zero direction |
| 3 | `AccessViolation` in `PreloadForRendering` (mesh) | `sk_spider_forest_c` is a single **62-bone** mesh that overflows the native per-mesh bone palette (~40–58). `Skeleton.MaxBoneCount=64` is a *separate* skeleton cap | **Open** — needs a Modding-Kit mesh-split into sub-meshes ≤ ~40 bones. Proven via the warg stand-in (warg mesh renders through the identical code path) |
| 4 | `NullReferenceException` in `Agent.GetPrimaryWieldedItemIndex` | Vanilla `MissionBattleSideSpawnContext.SpawnTroops` calls `agent.WieldInitialWeapons()` on every spawned agent; its first line derefs the spider's uninitialized native wield pointer (`0xee0`) | `Agent_WieldInitialWeapons_SpiderSkip_Patch` — skip wielding for the spider (it bites via its Monster, never wields) |
| 5a | `NullReferenceException` in `Agent.IsRangedCached` | Joining a formation → `QueryLibrary.IsInfantry → IsRangedCached → Equipment.ContainsNonConsumableRangedWeaponWithAmmo()` derefs the **null `MissionEquipment`** (the mount build never creates one) | `EnsureMissionEquipment` → `agent.InitializeMissionEquipment(null,null)` builds an empty (weaponless) `MissionEquipment` from the SpawnEquipment → weapon-cache queries return `false` |
| 5b | native `AccessViolation` in `Agent.GetMissileRange` | Formation membership → `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime → BehaviorSkirmish → FormationQuerySystem` reads per-unit `agent.MaximumMissileRange → GetMissileRange()` (native) which walks the **uninitialized native weapon struct** | **Gated off** + investigated root fix `InitializeNativeWeaponState` (see below) |

**Recurring theme (3×):** the uninitialized native wield pointers (`0xee0/4/8`) bit us at WieldInitialWeapons, then aiming, then missile-range. They exist because `FromHorseObj` has no case in `Agent.EquipItemsFromSpawnEquipment` (Agent.cs:4532) — the native `WeaponEquipped` loop that initializes per-slot weapon records never runs for a mount-path agent.

### Architecture investigation (3-agent decompile workflow) — the decisive findings

1. **Formation native-query surface is bounded to ONE method.** Every per-unit query the formation/team-AI makes reads *managed-cached* state (now valid via the `MissionEquipment` fix) **except** `agent.MaximumMissileRange → GetMissileRange()` (native, no managed early-out). A Harmony prefix returning `0f` for the spider is **sufficient** to survive formation membership + `BehaviorSkirmish` planning (HIGH confidence — verified by enumerating all ~45 `FormationQuerySystem` delegates, every `Formation` aggregator, every `QueryLibrary` predicate, and grepping the whole behavior/tactic set). The aiming natives (`CurrentAimingError`, etc.) are **not** on the formation path — they fire only for an actively-aiming ranged agent, which the spider never is.
2. **The native wield state IS fixable without the skin build.** `WeaponEquipped` (native per-slot weapon init) and `AddSkinMeshes` (humanoid skin) are two **distinct, sequential** operations in `EquipItemsFromSpawnEquipment`. The public `agent.RemoveEquippedWeapon(slot)` (Agent.cs:4814) runs the *same* native `WeaponEquipped(InvalidWeaponData)` the normal path runs for empty slots — **without** `AddSkinMeshes`. Looping it over all 5 weapon slots initializes the native weapon struct → `GetMissileRange`/aiming reads stop AV'ing. This is the **root** fix for both 5b *and* future per-frame combat reads (high-confidence-but-unverified: native bodies are unreadable, so it must be confirmed in-game).
3. **The spider BT does NOT drive movement.** `SpiderTree` only detects enemies within ~16m and bites within ~4m; it has *no* move-to-enemy node. The warg's only movement node steers `warg.RiderAgent` (a rider, which the spider lacks). So a **detached** spider is a stationary bite-trap; advancing toward the enemy requires either **formation movement orders** (i.e. formation membership) or a **new BT movement node**. Movement was always intended to come from the formation.

### Decision + checkpoint (committed 2026-06-04)

- **Detached + positioned, formation membership GATED OFF** (`SpiderConfig.EnableFormationMembership = false`). This is the verified non-crashing state: spiders spawn in the deployment zone, render (with the warg stand-in), and do not crash — but are passive.
- **Warg render stand-in committed** (`SpiderMonsterId="warg"`, `SpiderMountItemId="warg_brown"`) — the real spider mesh AVs at render (#3) until the mesh-split, so the warg (which renders cleanly and exercises the entire spawn path) is the committed test vehicle. Real values `"spider"` / `"spider_mount_a"` documented inline.
- The investigated root fix (`InitializeNativeWeaponState`, #2) ships **behind the same flag** so the next-session re-enable is a one-line flip + an in-game test.

### Next-session plan

1. Flip `EnableFormationMembership = true`, test in-game. If the formation `GetMissileRange` AV is gone → membership + advance-with-army works. If not → add the `Agent.GetMissileRange→0` prefix (finding #1, bounded + HIGH confidence).
2. (Asset) split `sk_spider_forest_c` into `<AdditionalMeshes>` sub-meshes ≤ ~40 bones; revert `SpiderConfig` to the real spider.
3. If membership stays off by design, add a spider-self BT movement node (model on `WargAiControlledGetToEnemy`, retargeted off `RiderAgent`).
4. Strip `[Spider][diag]` logging once shipping.

### Preventive lessons

- **A clean deep-review is not an in-game pass.** The swap approach (appendix) passed 5 agents with zero code bugs and still hard-crashed natively. Engine-coupled agent-build code MUST be smoke-tested in-game before "done."
- **Non-humanoid combatant = mount-build + manual combatant re-add.** When you reuse `FromHorseObj` to dodge the skin build, you inherit *every* gap the mount path has (weapon/wield state, formation membership) — audit the full troop-spawn contract (`Mission.SpawnTroop`) and re-add each piece, in order, or the engine's humanoid assumptions AV one layer at a time.
- Extends memory `feedback_nonhumanoid_creature_troop_not_mount` (troop-not-mount) with the build-shape detail: the troop is recruited as a humanoid anchor but *spawned* via the mount build, not the humanoid build.

## Update 2026-06-05 — architecture resolved (riderless autonomous) + mesh-split done

The next-session plan above was executed in-game. What actually happened (full detail in the 2026-06-05 CHANGELOG entry + [spider.md](../features/spider.md) + [spider-skeleton-animation-pipeline.md](../features/spider-skeleton-animation-pipeline.md)):

- **Formation membership = dead end.** Flipping `EnableFormationMembership=true` crashed *before* the `GetMissileRange` read: `InitializeNativeWeaponState`'s `RemoveEquippedWeapon` → native `WeaponEquipped` AV (the native wield struct can't be **written** either). A 3-agent decompile workflow also found formation membership adds a *second* crash — a null-`HumanAIComponent` NRE in the per-agent formation tick (`Agent.cs:4726`) — for no benefit a detached spider needs. **Rejected.**
- **The native-wield surface is a CLOSED set of 3** — `GetMissileRange` + `Get{Primary,Offhand}WieldedItemIndex`; every red debugger property funnels through them. `Agent_SpiderNativeWieldGuard_Patch` guards all 3 (→ `0`/`None`, the correct answer for a weaponless biter). This is the *complete* fix, not whack-a-mole — the reframe that resolved the user's (correct) "we'll just error into the next one" concern.
- **Chosen architecture: detached + autonomous BT** (riderless, matches the monster vision). `SpiderMoveToEnemyTask` advances the spider via the wield-free `SetScriptedPositionAndDirection`. Mount+rider was the only zero-native-wield alternative (the map-icon crash that shelved the old mount design does NOT fire for a non-leader rider troop) but ships a *visible rider*. In-game the spiders spawn + render (warg stand-in) with **no crash**; the wander was root-caused to a 16m engage gate (enemies far at battle start) and fixed with a mission-wide nearest-enemy search.
- **Mesh-split done** (`spider_correct.fbx`): the warg authors every mesh ≤40 bones over a 49-bone skeleton; the spider mesh weights to **58** (skeleton 62 — the "62-bone mesh" was a count conflation). Each `sk_spider_forest_{a,b,c}` split L/R into base (33) + `_2` additional (30), skeleton byte-identical. Pending Modding-Kit compile + `<AdditionalMeshes>` wiring.

**New preventive lessons (2026-06-05):**
- **The native wield state of a `FromHorseObj` agent is unfixable but bounded.** Can't read it (NRE), can't write it (`WeaponEquipped` AV) — so don't try to *initialize* it; *guard the small closed set* of managed methods that read it, enumerated by decompile (don't assume whack-a-mole).
- **A warg stand-in (`Mountable=true`) is a good RENDER proxy but a bad BEHAVIOUR proxy** — its mount-AI wanders and masks movement. Validate non-humanoid behaviour only with the real `Mountable=false` creature.
- **Skeleton + meshes must ship in one FBX** — a mesh-only export drops skin weights (Blender stores skin in the armature deformer; the editor also rejects duplicate skeleton names across two imports).

---

## Update 2026-06-05 (later) — render AV refuted at the data level → feature PAUSED → pivot to a ridden elephant

The mesh-split landed and the real spider (`Mountable="false"`) was tested in-game. It **deterministically AccessViolated in native `Agent.PreloadForRendering`** — straight to desktop, every battle entry. The session that followed was an exhaustive attempt to move that wall at the data level; it failed, and the feature was **paused by the project owner** (`SpiderConfig.Enabled = false`).

### What was refuted (every data-level hypothesis)

| Hypothesis | How tested | Verdict |
|---|---|---|
| Mesh > per-mesh bone palette | L/R split each `sk_spider_forest_*` ≤40 bones, rebuilt FBX | **Refuted** — AV persisted with the split mesh |
| Build/visuals failure | A render-diag Harmony prefix (`[HandleProcessCorruptedStateExceptions]`) logged the agent's state on the way into `PreloadForRendering` | **Refuted** — log showed `AgentVisuals=ok skel.bones=62`; visuals + skeleton **build fine** |
| Skeleton integrity / IK / >4 influences / physics | `tpac_skeleton_*` dumps + transplant re-applied; influences clamped | **Refuted** — all clean, AV unchanged |
| Missing/corrupt shader (`pbr_metallic ×385` "Missing shader from sack") | Renamed `compressed_shader_cache.sack` → engine recompiled live | **Refuted** — same AV; cache restored byte-identical (1,594,448,832 B) |
| Material binding | Verified the spider material/texture refs resolve | **Refuted** |

**Decisive evidence:** the render-diag patch proved the managed side is healthy — `AgentVisuals=ok`, `skel.bones=62` — and the crash is the **native GPU render-preload** of that skeleton, reached only through the **detached / non-mountable mount-render sub-path**. (The patch was deleted when the feature paused — it would spam on warg battles.) A GPU "device removed" error seen separately on the campaign map was a driver TDR, not caused by any cache edit (the `.sack` was proven unchanged).

### Root cause (architectural, not a bug)

**The engine supports exactly two agent shapes — a humanoid combatant (skin + weapons + formation) and a ridden mount.** There is no "non-humanoid riderless combatant." The detached spider hacked a mount (`FromHorseObj`) into a riderless fighter, and the **non-mountable detached path through `Agent.PreloadForRendering`** is the one we cannot satisfy with data. This is the *same* root insight as the original redesign ("a non-humanoid combatant is a category the engine does not natively support") — now confirmed to extend all the way down to native GPU render-preload, not just the managed skin/weapon/formation layers.

### The decisive contrast — why the elephant works where the spider didn't

ADOD's war-elephant (deep-dive `w21npmp7s`, 2026-06-05) is a **60-bone** non-humanoid creature that **renders fine in battle** — because it is a `Mountable="true"` **ridden mount** (a normal humanoid rider sits on it; the horse/warg path), never the detached render path. So:

- **The spider's wall is the detached / `Mountable="false"` path — NOT the bone count.** A 60-bone skeleton renders fine ridden; a 62-bone one AVs detached. Bone count was refuted twice (split mesh + ADOD's working 60-bone elephant).
- **The supported way to ship a non-humanoid creature is as a ridden mount that auto-attacks** — exactly the warg pattern. The elephant takes that lane and touches none of the five crash layers the spider fought (humanoid-skin AV, native-wield NRE/AV, formation null-`HumanAIComponent` NRE, map-icon crash, render-preload AV).

**Preventive lesson (the "never again"):** to put a non-humanoid creature in a TAOM battle, make it a **`Mountable="true"` ridden mount** (warg/horse machinery) — do **not** spawn it as a detached `FromHorseObj` riderless agent. The detached path is unsupported and its native render-preload AV cannot be moved with data. If the spider is ever revived, re-shape it as a ridden mount. Full elephant design: [elephant.md](../features/elephant.md).

### Tooling built this session (kept)

- `tools/spider_render_triage.py` — one-command crash-triage (auto-finds latest `taom_debug` + `rgl_log` + crash dump; reports intercepts/fail-opens, render-acted lines, missing-shaders, formation-crash signature; prints a VERDICT). Pure stdlib, read-only, fail-soft.
- `tools/tpac_skeleton_transplant.py --usage horse|other|human` — the skeleton-`Usage` flag (was hardcoded `'horse'`), so a creature skeleton can be written as `'horse'` (mount), `'other'`, or `'human'` for render-path experiments.

---

## Update 2026-06-06 — ADOD wolf comparison: the spider's "impossible" verdict is likely WRONG

While porting the elephant, the user pointed out ADOD ships **wolves** — and the wolf is the true analog to the
spider (the elephant is a `Mountable=true` *ridden* mount, so it never touches the riderless render path the spider
died on). A decompile + asset comparison of ADOD's wolf overturns this RCA's core conclusions:

**ADOD's wolf is a WORKING riderless non-humanoid creature.** `adod_wolf_*` Monster: no `Mountable` flag,
`monster_usage="horse"`, `action_set="as_adod_wolf"`. Spawned via `Mission.SpawnMonster(item, …)` →
`CreateHorseAgentFromRosterElements` → `CreateAgent(…, FromHorseObj)` + `agent.SetMountInitialValues(name, MountCreationKey)`;
AI via `ADODBeastsWolfAgentComponent`. It renders + fights riderless in-game.

| | ADOD wolf (works) | Our spider (AV'd) |
|---|---|---|
| Spawn | `SpawnMonster` → `CreateAgent(FromHorseObj)` + `SetMountInitialValues` | hand-rolled `CreateAgent(FromHorseObj)` + `SetMountInitialValues` — **already equivalent** |
| Skeleton bones | **57** | 62 (only 5 more) |
| **Skeleton `Usage`** | **`'other'`** | **`'horse'`** |

**Three conclusions of this RCA are refuted or in serious doubt:**
1. **"A non-humanoid riderless combatant is a shape the engine doesn't support" — REFUTED.** ADOD's wolves are exactly that and work.
2. **"The 62-bone / 58-bone mesh overflows the per-mesh palette → `PreloadForRendering` AV" — in serious doubt.** The wolf renders at a **57-bone** skeleton (5 fewer than the spider). A 5-bone gap doesn't flip render→AV. The mesh-split we chased was likely a red herring. (Wolf per-*mesh* palette not directly measured — but the skeleton count alone undercuts the theory.)
3. **The spawn path was not the cause** — our `SpiderDetachedAgentSpawner` already replicated `SetMountInitialValues` + the MountCreationKey (the one genuinely-subtle piece).

**Leading hypothesis (UNTESTED) — the spider skeleton `Usage`.** The wolf's skeleton is **`Usage='other'`**; the
spider's is **`Usage='horse'`**. This RCA repeatedly described the AV as *"specific to the mount-render path"* — and
`Usage='horse'` is precisely what routes a skeleton onto that path. The wolf avoids it with `Usage='other'` while
*still* spawning via `FromHorseObj`. We added a `--usage` flag to `tpac_skeleton_transplant` this session but the
recipe always kept `'horse'`, so **`Usage='other'` was very likely never tested in-game.**

**The cheap experiment to revive the spider:** `python tools/tpac_skeleton_transplant.py <spider tpac> spider_skeleton --usage other`,
re-import, flip `SpiderConfig.Enabled=true`, and test — keeping the existing `FromHorseObj`/`SetMountInitialValues`
spawn. If the AV disappears, the spider was never "impossible"; it was a one-field skeleton-Usage bug, and the entire
detached-vs-ridden agonising + the mesh-split were chasing the wrong cause. Framed as a hypothesis (the spider is
paused; this is the most promising untried lead, not a confirmed fix). **Lesson: when comparing to a working
reference mod, compare the WORKING ANALOG (wolf = riderless), not the convenient one (elephant = ridden) — and check
the skeleton `Usage` field, not just bone counts + spawn code.**

## Appendix — deep-review of the SUPERSEDED in-place `Monster`-swap approach

> Retained for history. This reviewed the approach that returned `true` from the `Mission.SpawnAgent` prefix after `agentBuildData.Monster(spider)` — which crashed in-game (humanoid skin on the spider skeleton). The detached redesign above replaced it.

### Summary

Deep-review (5 agents) of the recruitable-spider-troop feature found **no correctness bugs** and **one minor style finding** (an ADR-002 entry-point line-count overage), fixed in-session. The high-blast-radius risks were all verified clean: the `Mission.SpawnAgent` chokepoint prefix is fail-safe (always returns `true`, null-guarded), it coexists with Patch23_BannerColorPersistence (both return `true`), bites work in real campaign battles via the shared `IBoneCollisionService` singleton that `AdvancedCombatBehavior` ticks, and the level-21 spider (Tier 4) is below MaxVolunteerTier 6 so it is genuinely offerable as a volunteer. Build + 3030/3032 tests green. **(Note: "no correctness bugs" was true of the managed code reviewed; the design itself was the bug — it crashed natively in-game, which the redesign above fixes.)**

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW | `SpiderMissionBehavior.cs` 153 lines > ADR-002 "<150" ceiling | Style / Critical-Rule ceiling | Gutting the scatter-spawn path took the file 201→153; I verified behavior + delegation, not `wc -l` against the 150 ceiling | After editing an entry-point class (MissionBehavior / Patch / GameModel / CampaignBehavior), `wc -l` it against 150. One-off; no new rule needed. |
| 2 | (overstated) | Agent 4 rated "LOTRLOME_Armory absent from `SubModule.xml` DependedModules" as CRITICAL ("Monster load fails at runtime") | Review-process / false-severity | Agent 4 assumed declared load-order affects a runtime lookup | Resolved by evidence-over-claims — see Notes. Not a spider bug; pre-existing whole-mod matter, not bundled into this feature. |

## Notes

- **Finding 2 is the valuable process datapoint.** The deep-review's own "when two agents disagree, that's signal" rule fired: Agent 4 (Completeness) called the missing LOTRLOME dependency CRITICAL, while Agent 5 (Data Flow) traced the Monster null-path as a graceful fail-open. The disagreement forced a mechanism check — the spider Monster is resolved by a **runtime** `MBObjectManager.GetObject<Monster>("spider")` at agent-spawn time, by which point every enabled module is loaded regardless of declared order; and the service already fail-opens (logs + spawns the humanoid anchor) if it is absent. So the declared dependency is load-order-irrelevant for the spider. The general lesson: "module X owns entity Y" does **not** imply a load-time cross-module dependency when the consumer is a runtime lookup. TAOM has never declared LOTRLOME_Armory despite using its items everywhere, and ships fine — a pre-existing whole-mod observation, surfaced for the user, deliberately not fixed inside this surgical feature.
- **No systemic / repeat-offender pattern.** Finding 1 is a one-off ceiling miss, not a recurring class (unlike the NaN-gate trio). No new feedback memory warranted.

## Prior design dead-end — the rideable-mount crash (the real "never again" lesson)

Before the troop approach, the spider was built as a **rideable mount** (creature-as-HorseItem, `dg_giant_spider_rider`). It was fully reversed because it failed two ways:

1. **Campaign-map crash (game-breaking):** a mounted creature gets a map party icon; building it calls `Skeleton.ForceUpdateBoneFrames()` (via `MobilePartyVisual.AddMobileIconComponents` → `RefreshPartyIcon`), which threw `AccessViolationException` on entering the open world. This path exists ONLY for mounts.
2. **Per-mesh bone-render limit:** a single skinned Mesh has a native ~40–58 per-draw-call bone cap; the 8-leg spider mesh exceeded it (only 4 legs rendered). `Skeleton.MaxBoneCount=64` is a separate (skeleton) cap; the fix would be a multi-mesh split (warg `<AdditionalMeshes>`).

**Preventive (cross-session):** memory `feedback_nonhumanoid_creature_troop_not_mount` — any future non-humanoid creature is a recruitable troop via the `Mission.SpawnAgent` monster-swap, never a mount. The troop path never gets a map icon (no `ForceUpdateBoneFrames`) and spawns on the live-agent path. Also captured in `docs/features/spider.md` "Why This Exists". This is the systemic lesson; the troop feature's own deep-review (above) was clean.

## Human-seam items (not review findings — require in-game verification)

- **Live-agent render:** whether the spider shows all 8 legs. The per-mesh bone-render limit was the *mount-path* symptom (4 of 8 legs); it is unproven on the *live-agent* spawn path and can only be confirmed by recruiting a spider and taking it to battle.
- **Fang bone indices (23/37/43)** are documented placeholders copied from the warg — bites may land on the wrong contact points until a runtime bone dump resolves the real `joint5_l/r` / `joint12_m` indices on `as_spider`.
