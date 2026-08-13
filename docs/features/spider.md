# Spider (Giant Spider — Ridden Mount)

> **Status (2026-06-11): WORKING — mount lane proven in battle.** Full formations of 8-legged
> giant spiders with goblin riders load and fight in Custom Battle (verified in-game 2026-06-11
> 08:36, screenshot in session log; `[SpiderDiag]` probe battery all-green, `[MountSpawn] success`
> for `spider_mount_a`). The detached-combatant architecture documented in earlier revisions of
> this file was **deleted 2026-06-10** (git history preserves it) — the spider is now a plain
> **Mountable Horse-slot mount** ridden by the `taom_spider_creature` goblin (the warg/elephant
> pattern). Remaining items are cosmetic polish + the rest of the in-game ladder (see "Current
> state" below).

## Overview

The Giant Spider is a **rideable mount**: `taom_spider_creature` (goblin, Cavalry, Dol Guldur)
carries `Item.spider_mount_a` in its Horse equipment slot; the vanilla cavalry spawn builds two
agents — the goblin rider (`FromCharacterObj`) and the spider mount (`FromHorseObj`,
`Monster.spider`). No spawn interception, no Harmony patch on the spawn path. The spider
auto-attacks enemies via a per-agent behavior tree (`SpiderBehaviorTree`, attached by
`SpiderMissionBehavior` keyed on `Monster.StringId == "spider"`), mirroring the elephant — a
directional repertoire (priority pounce + left/right swipes by bearing) as of 2026-06-15
(see "Directional attack model" below).

## Why this exists (and the three architectures that led here)

LOTR needs Dol Guldur's giant spiders as a fielded force. Bannerlord has no first-class
non-humanoid roster troop, so three shapes were tried:

1. **Rideable mount (2026-06-03, first attempt)** — abandoned at the time for a map-icon
   `ForceUpdateBoneFrames` AV that was later understood (missing `_map`/`_town_and_village`
   child action sets — the elephant Crash #4 class) and for the thumbnail/tableau AV whose true
   root cause took until 2026-06-11 to find (below).
2. **Detached riderless combatant (2026-06-04 → 06-05)** — `Mission.SpawnAgent` prefix
   hand-building a `FromHorseObj` spider with native wield guards. Worked in battle, then hit a
   `PreloadForRendering` AV from the 58-bone single mesh (*believed then* to be a "per-mesh GPU palette
   ≈ 40" overflow — **that cause is FALSE, corrected 2026-06-13**: no per-mesh cap exists, the elephant
   renders 59 active bones in one mesh; the AV's true cause is unestablished — `feedback_no_40_bone_per_mesh_limit`)
   and was PAUSED. The whole machinery (Patch45, wield guards, spawn service, move task) was **deleted
   2026-06-10**.
3. **Rideable mount, take two (2026-06-10 → 06-11, current)** — after the war elephant proved
   the ridden-mount lane end-to-end, the spider was converted back: mesh split L/R (on the now-refuted
   bone-palette premise — the split was unnecessary for bone count, a single mesh skins the whole
   ≤63-bone skeleton), mount surface authored, and the tableau/mission AVs root-caused to a **missing
   `quad_movement` clip tag** (the actual fix). This file documents that architecture.

## THE ROOT CAUSE (2026-06-10/11 investigation)

**Who:** Mike (in-game repro + debugger dumps) + Claude (instrumentation, probe battery, byte
forensics), one ~6-hour `/investigate` session.
**What crashed:** native `AccessViolationException`, faulting address `0x10` (null + 0x10 field
read), inside `Skeleton.TickAnimations` / `GetWalkSpeedLimitOfMountable`.
**Where:** every mount-context first-touch — Custom Battle thumbnail
(`CharacterSpawner.SpawnMount`), inventory equip (`CharacterTableau.AdjustCharacterForStanceIndex`),
and mission deployment (`Agent.Build → set_Formation → WalkingSpeedLimitOfMountable`) — while
warg and elephant sailed through the identical code paths.
**When:** first hit the moment the spider became a Horse-slot mount (2026-06-10). The detached-era
spider never crashed here because non-mount agents never engage the quadruped gait machinery.

**Why (root cause):** the `an_spi_*` animation clips were Kit-compiled (2026-06-03 loop sessions)
**without the `quad_movement` tag and step points** in their `_anm.tpac` metadata. Every working
quadruped's movement clips carry that tag (verified by byte-diffing ADOD_Beasts's
`elephant_canter_anm.tpac`, which also carries `make_walk_sound`, four step-point fractions, and
movement speed params — ours had empty tag lists and step points `-1,-1,-1,-1`). A
`movement_system="quadrupedal"` action set whose movement-bound clips lack the tag builds a
**null native gait structure** at skeleton creation; the first tick (or the first mount-speed
query) dereferences it → AV at `+0x10`. A secondary fingerprint: resolving any *unbound* action
through such a set returns a runtime-synthesized garbage record (`1002467048434979358_0`)
instead of a real animation name.

**How it was found:** an instrumented replacement for the engine's private `SpawnMount`
(`CharacterSpawnerService.SpawnMountLogged`) with write-ahead logging + `[HandleProcessCorruptedStateExceptions]`
graceful-catch (crash → logged mount-less degradation), plus a one-shot **probe battery**
(`RunSpiderMountDiagnostics`) that tick-tested fresh skeletons under controlled (action set ×
usage set) pairings. The battery's truth table eliminated, in order: the Kit-compiled skeleton
resource (T3/T4: warg data ticks it fine), the Monster record (same object passed to passing
probes — `family_type=11`, `num_paces=6` both fine), the usage-set enrichment (bare 6/4 shape
also failed), the clip *bindings* (all repointed to one intact clip — still failed), the child
action sets, the old-vs-new tpac (both failed), and dangling `_anm→_geo`/skeleton GUIDs (byte
cross-reference clean). The final cross-probes (T5: `as_spider`×warg-usage AV; T6:
`as_warg`×spider-usage OK) convicted the action set's *engagement of our clips*; byte-diffing a
working ADOD_Beasts clip against ours exposed the missing tag in minutes.

**The fix (how):** rebuild the movement clips' `_anm.tpac` on the elephant template — keep ADOD_Beasts's
proven post-name layout (tags `quad_movement` + `make_walk_sound`, step-point fractions, movement
params), substitute each clip's own identity fields (file/resource/curve GUIDs, name, duration
trio, blend float, trailer hash). Patched clips: `an_spi_walk_2`, `an_spi_walk_left`,
`an_spi_walk_right`, `an_spi_run` (the only clips bound to movement actions). Originals preserved
as `*.bak-untagged` beside them. Non-movement clips (attacks/hits/deaths) correctly do NOT carry
`quad_movement` (ADOD_Beasts's attacks don't either).

**THE LESSON (any future creature):** when compiling creature animation clips in the Modding Kit,
movement/gait clips MUST get the `quad_movement` flag (+ step points) in the Kit's animation
editor before saving. An untagged movement clip is a delayed native AV that detonates only when
a `movement_system="quadrupedal"` action set engages it — thumbnails, tableaus, and mounts, not
the detached spawn paths that earlier testing exercised.

## Architecture

### Data plane (all LIVE in `LOTRLOME_Armory` — registration map below)

| Piece | File | Key facts |
|---|---|---|
| Monster | `ModuleData/Monsters/LOTR/lotr_monster_spider.xml` | `id="spider"`, `Mountable="true"`, `rider_sit_bone="chest_m"`, `num_paces="6"` (every mountable monster is 6 — paces 0–5), **`family_type="1"`** (the horse family — was 11 during the bisect, returned to 1 on 06-11 for the vanilla rider-death/dismount surface; deployed is 1), slope block (head + front/back leg roots), `action_set="as_spider"`, `monster_usage="spider"` |
| Action set | `ModuleData/action_sets.xml` → `as_spider` | `skeleton="spider_skeleton"`, `movement_system="quadrupedal"`, 36 bindings. Movement actions bind ONLY tagged clips (walk_2/left/right, run). Idles/turns/jump/taunt currently bind tagged walk clips (pose-correct idles need a tagged Kit recompile). `act_horse_forward_canter` is bound explicitly (the tableau pose; warg precedent — `as_warg` binds it too) |
| Child sets | same file: `as_spider_town_and_village`, `as_spider_map` | `base_set="as_spider"`; absence = map-icon/thumbnail AV class (elephant Crash #4) |
| Usage set | `ModuleData/monster_usage_sets.xml` → `id="spider"` | Full mount surface: 10 verb attrs (rear/dash/kick/quick-stops/jump/hit-object…), upper-body movements, movements (paces 0–5, gallop both foot variants), rider movement-adders (`act_horse_rider_*` — global codes animating the RIDER), jumps (`act_horse_jump_*`, bound in as_spider), falls, strikes. Every pace 0–5 keeps a `direction="none"` reference row (missing one = native ÷0, the 2026-06-04 crash) |
| Mount item | `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` → `spider_mount_a` | `is_mountable`, `<Horse monster="Monster.spider" maneuver=60 speed=40 charge_damage=10 body_length=100>`, `<AdditionalMeshes><Mesh name="sk_spider_forest_c_2"/>` (the L/R split second half) |
| Skeleton + Meshes (bundled) | `Assets/creature/spider/animations/spider_correct_geo.tpac` (2.78MB) | **LIVE = the PROVEN 6/11 working bundle, RESTORED 2026-06-14 from `E:\LOTRAOMAssets\_tpac_backup_20260613\spider\`** (skeleton `owner_guid a9ec7d87…`, `Usage='horse'`; the 6/10 pre-rework mesh). `sk_spider_forest_c` / `_c_2` L/R-split meshes (≤38 bones/half; the unsplit 58-bone mesh AVs `PreloadForRendering`) + the 62-bone **`spider_skeleton`** resource. The action_set's `skeleton="spider_skeleton"` resolves here. **The 2026-06-13 Kit-rebuild bundle (different mesh) caused a FATAL battle-spawn AV** in the native `sound_and_collision_info` agent-build (`Agent.BuildAux`→native `Build`, RVA 0x490E02) — preserved as `.bak-kitbroken-20260614`; my physics-transplant attempts (`.transplanted-20260614`, `.bak-bundled-crashing-20260614`) likewise didn't fix it. **Restoring the backup did, in one copy.** Lesson: `feedback_creature_rework_restore_from_backup_first.md`. *(In-game battle verification owed.)* |
| Skeleton history (DO NOT repeat) | — | The 06-13 Blender-loop mesh re-export shipped `spider_correct_geo.tpac` **mesh-only**, dropping the skeleton → `CreateAgentSkeleton` null → riderless spiders. First fix attempt (2026-06-13) extracted the skeleton into a **STANDALONE** `meshes/spider_skeleton_geo.tpac` via `tpac_skeleton_extract.py` — this **CRASHED** the engine (recursive worker-thread native AV reading null; the standalone reused the skeleton's item_guid as its package_guid + no creature ships a standalone skeleton tpac). Deleted 2026-06-14. Correct fix = re-bundle into the mesh tpac via `tools/tpac_skeleton_inject.py`. Skeleton source of truth: `meshes/sk_spider_forest_c_geo.tpac.backup` (item [2], the un-split a/b/c original). Mesh-only backup of the dropped state: `animations/spider_correct_geo.tpac.bak-meshonly-20260614`. Lesson: `feedback_mesh_reexport_drops_skeleton_resource.md`. |
| Clips | `Assets/creature/spider/animations/an_spi_*_{anm,geo}.tpac` | Loose-pair format (same as ADOD_Beasts elephant). Movement clips tagged (see ROOT CAUSE). The 13 `new_animation_clip*_anm.tpac` files are NOT garbage — Kit default filenames whose internal resource names are real (`an_spi_hit_front`, `an_spi_death_1`, …; quirk: `an_spi_idle2` has no second underscore) |
| Rider troop | TAOM `Main/_Module/ModuleData/characters/spider_creature.xml` | `taom_spider_creature`: goblin, Cavalry, level 20, Dol Guldur, 3 equipment rosters all with `Horse = spider_mount_a` |
| Troop weight | `TroopWeights/troop_weights.xml` | 3.0 (2 mount + 1 rider; elephant precedent 7.0) |
| Recruitment | `VolunteerRecruitmentService` DG settlement pools | weight 1, all Dol Guldur fiefs (intentionally absent from clan pools) |

### Registration map (the `project.mbproj` truth — load-bearing)

Native animation XML loads ONLY from `LOTRLOME_Armory/ModuleData/project.mbproj` `<file>` entries
with **standard ids**: `soln_action_sets` → root `action_sets.xml`, `soln_action_types` → root
`action_types.xml`, `soln_monster_usage_sets` → root `monster_usage_sets.xml`. Custom ids are
silently ignored (2026-06-04 RCA, comment in the mbproj itself). The `Animations/` and
`MonsterUsage/` subfolder copies are superseded reference copies. SubModule.xml `<Xmls>` handles
only the managed types (Monsters, Items). Alliance.Wargs follows the same pattern.

### Rider-side bindings (the thrust-loop fix, 2026-06-11)

When an agent rides a mount, the engine resolves the **mount's usage-set actions against the
RIDER's action set** to pick the rider's lean/sway overlay (this is what the
`as_goblin_warrior does not contain act_spider_*` rgl warnings were). Unresolved rider-side
lookups degrade per-set — the elephant's mahout falls back benignly, the spider's goblin looped
a thrust action. The supported mechanism (Alliance.Wargs precedent — the FIRST set in
`action_sets_warg.xml` is a partial `as_human_warrior`!) is a **partial `as_human_warrior`
block that the engine merges into the global set**, binding every usage-referenced mount action
to a rider animation. The spider's partial lives in the root `action_sets.xml` right above
`as_spider` (24 bindings) and **reuses the globally-registered `rider_warg_*` clips**
(Alliance.Wargs is a hard dependency) until bespoke spider-rider clips exist. `act_horse_*`
codes (rider adders, jumps, canter) are already vanilla-covered rider-side. Race sets like
`as_goblin_warrior` inherit the human-warrior surface (goblin WARG riders work via the same
merge), so one partial covers all riders incl. the player.

The goblin currently straddles the narrow warg back on the broad spider — the warg-straddle fit
problem + the bespoke re-pose path are covered in
[creature-animation-blender-mcp-workflow.md §4a](../ai-includes/creature-animation-blender-mcp-workflow.md).

### Code plane (TAOM `Main/Features/Spider/`)

| Piece | Role |
|---|---|
| `SpiderMissionBehavior` | Elephant-shape: registers `"SpiderTree"`, first-tick scan + `OnAgentBuild` late-attach keyed on `Monster.StringId == "spider"` (never character id — the character is the goblin), dead pruning |
| `SpiderBehaviorTree` | Elephant-mirrored DIRECTIONAL tree (2026-06-15): `main → has rider → ai controlled → engage → [pounce-off-cooldown → SpiderPounceTask] / [side-off-cooldown → SpiderSideAttackTask] → idle`; player-ridden + riderless branches sleep. Blackboard: `PounceLastFired`/`SideAttackLastFired`/`TargetBearing` |
| `BehaviorTreeElements/SpiderEngageDecorator` | engage gate (replaces `SpiderCanBiteDecorator`): anti-chain (`IsSpiderAttack`) + zero-alloc `SpatialGrid` scan (reusable `_scratch` buffer) + cone hit check, then writes the NEAREST enemy's signed bearing (+=LEFT) to `TargetBearing` |
| `BehaviorTreeElements/SpiderAttackOffCooldownDecorator` | per-kind cooldown gate (`SpiderAttackKind.Pounce` ~5s priority / `SideAttack` ~2s gap-filler) via `IsOffCooldown` |
| `BehaviorTreeElements/SpiderAttackTaskBase` + `SpiderPounceTask`/`SpiderSideAttackTask` | stamp the kind's cooldown, then fire `SpiderAttack(kind, bearing)` (bone-collision per clip) |
| `BehaviorTreeElements/SpiderAttackActions` | eager `ActionIndexCache` for the 4 clips (front/charge/left/right) + `ForName` resolve + `IsSpiderAttack` anti-chain + `AnyUnresolved()` drift guard |
| `SpiderAttackService` | pure (TaleWorlds-free): `SelectActionName`/`SelectBones` (pounce=front/charge by speed; side=left/right by bearing), `IsOffCooldown`, warg-pattern rider damage attribution, `IsSpiderMonster()`; `SpiderAttack` fires the bone-collision `CustomAttack` + the `[Spider][diag] ATTACK fire` log |
| `AdvancedCombat/CustomAttacksUtils` (shared warg+spider+elephant) | synthetic-blow damage application. 2026-06-15 hardening: live-state revalidation + `IsBlowGeometrySafe` finiteness gate before the reflected `Mission.RegisterBlow` (defensive; NOT the fix for the dismount crash below) |
| `TaomAgentStatCalculateModel` (CareerSystem) | mount-lock: `CanAgentRideMount=false` + `MountDifficulty=999` for spider mounts (and elephant) — players can't steal the mount; the Horse-slot cavalry spawn ignores the lock for the assigned rider |
| `CharacterSpawnerService.SpawnMountLogged` (HeroRace) | instrumented replica of the engine's private `SpawnMount` with per-step logging + graceful mount-less degradation on failure. **Keep** (strictly better than the old blind reflective call); demote logging to `LogDebug` at ship. The one-shot `RunSpiderMountDiagnostics` probe battery + `TickProbe` are TEMP-DIAG — retire after the ladder |
| `Hooks/Agent_Die_SpiderDismount_Patch` (Patch47) | **rider-death AV mitigation — REQUIRED, exonerated and re-enabled 2026-06-12.** A rider dying while seated AVs inside the native `Agent.Die` path (1.4.5: use-after-free 3× on 06-11; 1.4.6: melee-thrust repro 06-12 — Die-path lookup returned **float bits as a table index** from a corrupted action record, mixed-mode-debugger-proven: faulting `RAX+RCX*4` matched bit-for-bit with RCX = float −0.094). The patch routes around it: Prefix on `Agent.Die` hard-dismounts via the engine's own private `SetMountAgent(null)` (cached `AccessTools` at `Initialize`) so riders die the proven on-foot death (verified: `act_death_by_arrow_head2`, clean sever, 0 dead-linked riders); a dying spider frees its rider first. **The 06-12-morning indictment ("post-sever tick AV") was overturned** — that crash was the `CanAttack`/`set_attack_entity` charge CTD, Event-Log-proven to fire with AND without Patch47. Vanilla mounts untouched; body try/catch'd. Registered after Patch46 |
| `Hooks/Agent_HandleBlowAux_SpiderDismountGuard_Patch` (Patch48) | **non-lethal sibling of Patch47 — APPLIED 2026-06-15, in-game confirmation pending.** A finite real-melee `CanDismount` hit on a *surviving* mounted Spider Rider AVs inside native `Agent.HandleBlowAux` reading `0x3` (debugger-proven 2026-06-15; stack `MeleeHitCallback → Mission.RegisterBlow → Agent.RegisterBlow → HandleBlow → HandleBlowAux`). Same broken non-vanilla mounted-DISMOUNT native path Patch47 routes around on death — but Patch47 only covers death (it hard-dismounts before `Die`), so a non-lethal dismount hit still reaches the crash. Prefix on `Agent.HandleBlowAux` strips `BlowFlags.CanDismount` when the victim's mount is the spider Monster → native dismount never fires, rider stays on the locked mount, damage still applies. Spider-only (matches Patch47); elephant mahout shares the latent fault but hasn't surfaced. Registered after Patch47 |

## The v1.4.6 engine-bump campaign (2026-06-12) — three crashes, three root causes, GREEN

Steam force-bumped the engine **1.4.5 → 1.4.6 on 2026-06-11 17:39, mid-campaign** (Version.xml +
DLL timestamps; the managed combat assembly is byte-identical — every change is native-internal).
1.4.6's rewritten usage/AI lookups stopped tolerating missed keys (shipping builds compile out
the asserts; the miss path dereferences the end-sentinel), which turned three latent spider-data
quirks into CTDs. All three were root-caused by Event-Log fault-offset correlation + offline
disassembly of `TaleWorlds.Native.dll` (pdata bounds, rip-relative string maps, caller chains)
+ live mixed-mode debugger forensics, then fixed in DATA (plus Patch47):

| # | Site (RVA) | Trigger | Root cause | Fix |
|---|---|---|---|---|
| 1 | `Agent_ai::set_attack_entity` (`0x6BAB4E`, null `agent+0xAD8`) | cavalry charge order | `CanAttack="true"` on the Monster — activates the engine attack-AI, a path NO working mount takes (warg/elephant/horse declare no such flag) | flag removed; Flags pruned to the warg-exact 5 |
| 2 | `monster_usage.cpp` jump lookup (`0x634396`, sentinel deref, target `0x3`) | first spider jump (riverbank), caught mid `act_horse_jump_high_loop` with corrupted record strings | `jump_start_action` typed `actt_jump` (warg+elephant use `actt_dash`) + jump rows covering only front/none of the engine's NINE directions — BT creatures turn mid-jump and produce directional queries vanilla riders never do | retype `actt_dash` + **45-row total jump table** (9 directions × all states; applied to the elephant too) |
| 3 | native `Die` path (`0x5FE0C9`, float-bits-as-index) | melee thrust kills a mounted rider | corrupted action record consumed by the mounted-death resolution (the 1.4.5 use-after-free Die crashes are plausibly the same corruption surfacing later) | **Patch47** dismount-before-death (see Code plane) |
| — | full parity audit (`tools/audit_mount_parity.py`) | — | 5 further deltas vs warg/elephant/horse: flag extras, missing rider capsule/eye adders, quick-stop slots miswired to an untyped idle, untyped pace-1 idle row, light strikes reusing the typed heavy action | all closed (warg-exact shapes); byte-patched `_anm` tpacs structurally verified CLEAN (sizes, layout, string tables — donor-exact) |

**Verdict: full river battle on 1.4.6 — charge, bank jumps, river crossing, prolonged melee,
rider deaths, spider deaths — NO CRASH** (user-confirmed). The end-to-end recipe distilled from
this + the elephant campaign: [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md).

## Directional attack model + dismount-on-hit crash (2026-06-15)

Two pieces of work this session: the bite was upgraded to a directional repertoire, and a *separate*
campaign-battle crash was root-caused.

### Directional attacks (✅ confirmed working in-game)

The warg-style single bite was replaced with the **elephant's directional model** (bone-collision
retained, not the elephant's radial AoE). When a live enemy is engageable, the spider fires a priority
**pounce** (`act_spider_attack_front`, or `act_spider_attack_charge` at `vel.Y ≥ 4`) if off its ~5s
cooldown; otherwise a **left/right swipe** (`act_spider_attack_left`/`_right`) chosen by the nearest
enemy's signed bearing, off a ~2s cooldown. All four clips already existed + were bound in `as_spider`
(only `front`+`charge` were used before). AI-ridden only (player keeps manual control). The pure
selection logic (`SelectActionName`/`SelectBones`/`IsOffCooldown`) is unit-tested; the BT nodes mirror
the elephant 1:1. Verified in the 2026-06-15 13:43 campaign log: clean `[Spider][diag] ATTACK fire`
lines, clip matching bearing sign exactly (`bearing>=0 → left`, `<0 → right`).

`SpiderEngageDecorator` was also given a reusable `_scratch` buffer (zero-alloc `SpatialGrid` scan) via
a new additive `SpatialGrid.GetNearAliveAgentsInRange(..., buffer)` overload — elephant-parity allocation
discipline, found by deep-review.

### The dismount-on-hit crash (Patch48 — ✅ fixed, confirmed in-game 2026-06-15)

**Symptom:** ~1 min into a campaign battle with many spiders, a fatal native AV reading `0x3`. Captured
under the debugger 2026-06-15: the victim is a **surviving** mounted Spider Rider (Health 12), hit by a
**real enemy melee weapon** (`Mission.MeleeHitCallback`, NOT our synthetic bite path), the blow geometry
is **finite**, and it carries **`BlowFlags.CanDismount`**. Stack: `MeleeHitCallback → Mission.RegisterBlow
→ Agent.RegisterBlow → Agent.HandleBlow → Agent.HandleBlowAux` → native AV at `HandleBlowAux`.

**Root cause:** the engine's native mounted-**dismount** path for the non-vanilla spider mount — the
*same fault class* as the mounted-**death** path Patch47 routes around (the Patch47 RCA equalized every
data surface and concluded it's unfixable by data). Patch47 only covers death (hard-dismount before
`Die`), so a non-lethal `CanDismount` hit still reaches the broken native dismount in `HandleBlowAux`.
The rider's own animations are complete (`as_goblin_warrior` inherits the full human death/fall surface
via `base_set="as_human_warrior"`) — not a missing-animation bug.

**Diagnosis correction (honesty):** the *first* report of this crash (truncated stack
`TickMissionAux → Mission.Tick`, "[Spider][diag] bite flood before the crash") was misdiagnosed as NaN
geometry corrupting native state from our synthetic blow. It is not — the blow is finite, vanilla, and
on a rider. The bite flood was correlation. The `CustomAttacksUtils` NaN/live-state guard added that
session is valid *defensive hardening* but does **not** fix this crash.

**Fix:** `Patch48_SpiderHitDismountGuard` (Code-plane table above) — prefix on `Agent.HandleBlowAux`
strips `CanDismount` from blows on spider-mounted riders so the native dismount never fires. Also the
correct design (the spider is a locked mount; its rider must not be knocked off). **Confirmed in-game
2026-06-15** — a full campaign battle with enemies meleeing the mounted spider riders, no `0x3` crash
(the hypothesis held: the AV was the `CanDismount` native dismount in `HandleBlowAux`). RCA:
[rca-spider-dismount-on-hit-2026-06-15.md](../reviews/rca-spider-dismount-on-hit-2026-06-15.md).

### Damage + bite-collision tuning (2026-06-15)

**Damage model** (`SpiderAttackService.CalculateSpiderBiteDamage`, per bone-collision hit, applied as Pierce):
`raw = 75 (MaxBaseDamage) + min(velY×25/15, 25)` → 75..100, then `× clamp((100 − armor×1.1)/100, 0.2, 1)`,
then a **per-hit crit** (`CritChance 0.2`, `×CritMultiplier 1.75`). Outcomes: unarmored/light ≈ one-shot,
medium (~35 torso armor) ≈ 2 hits, heavy (~55) ≈ 3 (2 on a crit), and a 20% min-passthrough floor so even
plate always takes a bite. Crit roll is `MBRandom.RandomFloat` at the boundary (`HandleSpiderTargetHit`),
keeping the formula pure; logged as ` CRIT` in `[Spider][diag] HIT`. **Deployed + confirmed** — the
2026-06-15 14:58 log shows 71-75 per bite on Looters. The previous values (35 base + linear armor) chipped
~20 vs armor; the bump was battle feedback ("didn't kill anything").

**Bite-collision fix (the real "didn't kill anything" cause).** The 14:53 log showed **75 ATTACK fires vs
2 HITs (~3% connect)** — the bite *played* constantly but almost never landed, so the (now-lethal) damage
rarely applied. Cause: the bone-collision used the **warg-placeholder bone indices (23/37/43)** — on the
spider's own skeleton those sit on rear / other-side legs — with a tight 0.3-0.4m sphere, so the indexed
bones rarely passed within range of a target (the exact failure the warg's own code comment warns about:
a few bones + a small radius can't form a detection volume). Fix: a giant spider strikes with its **front
legs**, so the bite now uses the real front-leg bones, **verified from the engine skeleton** via
`python tools/tpac_skeleton_dump.py <spider_correct_geo.tpac> spider_skeleton`:

| | shoulder(40) | thigh(41) | knee(42) | tibia(43) | tip(44) |
|---|---|---|---|---|---|
| front-right `joint4X_r` | 14 | **15** | **16** | **17** | **18** |
| front-left `joint4X_l` | 19 | **20** | **21** | **22** | **23** |

Collision uses the outer leg (thigh→tip): **pounce** = both front legs `[15,16,17,18,20,21,22,23]`,
**left/right swipe** = the matching side's leg. **Radius 0.3-0.4 → 1.8 (pounce) / 1.5 (side)** (the warg
used 1.0m with a 10-bone cone; the giant spider is ~2× and strikes with long legs), and detection range
4 → 5. In-game confirmation owed — watch the HIT-vs-ATTACK ratio + `bones=[…]` in the diag log; the radius
consts (`SpiderConfig.PounceCollisionRadius`/`SideCollisionRadius`) are the dials. The real fang bones
(`joint5_r/l` = 26/32, mouth `joint12_m` = 25) are available if a bite-at-the-mouth model is wanted later.

## Current state & known issues (2026-06-12, post-1.4.6 campaign)

| Item | Status |
|---|---|
| Thumbnail / picker | ✅ mounted spider renders, no crash |
| Custom battle deployment | ✅ full formations spawn (riders seated, 8 legs) |
| Spider idle | ✅ pose-correct: `an_spi_idle` (3.5s) tagged + bound (idle_2 → `an_spi_idle2`, the no-underscore resource quirk) |
| Spider idle refinement (2026-06-12/13, Blender-MCP) | ✅ shipped idle was a walk-in-place bug → fixed via `freeze_toward_rest(legs, 0.92)` to a settled braced IN-PLACE idle (zero-degree loop seam, subtle body breathing), exported (workflow: [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)) |
| Turns / jump | ✅ `an_spi_turn_left/right` + `an_spi_jump` tagged + bound |
| Deaths / hits / attacks | ✅ natural clips bound (`an_spi_death_1/2`, `hit_front/right`, `attack_left/right/top`) — no quad tag needed (ADOD_Beasts parity) |
| Rider animation | ✅ partial `as_human_warrior` with 24 spider→`rider_warg_*` bindings (see "Rider-side bindings"); riders seated correctly in the 09:50 battle |
| Vanilla-map battle (full polish data) | ✅ 2026-06-11 09:50 `battle_terrain_biome_092`: playable, formations deployed, idles standing, riders seated, battery all-green, 0 mount failures |
| **v1.4.6 full battle (river map)** | ✅ **2026-06-12: charge + bank jumps + river crossing + prolonged melee + rider deaths + spider deaths, NO CRASH** — all three 1.4.6 crash sites fixed (see the engine-bump campaign section) |
| Rider death while mounted | ✅ Patch47 dismount-before-death re-enabled (exonerated 2026-06-12); riders die clean on-foot deaths; required on 1.4.6 (melee-death Die-path AV proven without it) |
| Rider non-lethal `CanDismount` hit | ✅ **Patch48 (2026-06-15) — confirmed in-game.** A surviving mounted rider taking a dismountable melee hit AV'd in native `HandleBlowAux` (`0x3`); the prefix strips `CanDismount` for spider riders. Sibling of Patch47 (death) on the same broken native dismount path. See "Damage + bite-collision tuning" / RCA |
| `lotrtaom_iron_hills_01_forceatmo` | ❌ **SEPARATE BUG — not spider. The scene has NEVER loaded: 8/8 CTDs** (2026-06-10 20:53→2026-06-12 06:27), all dying at `scene.xscene` load, pre-agent-spawn — incl. runs with all-green spider probes. `taom_gondor_village_001_forceatmo` loads fine, so the forceatmo/Patch16 mechanism is exonerated — it's this scene's assets. Several 6/10 "spider mission CTDs" were this scene, conflated into the spider evidence. **Removed from `custom_battle_scenes.xml` 2026-06-12** so it stops eating test runs; restore once repaired. Own issue/investigation |
| **Rein attributes on v1.4.8 (2026-08-10)** | ⚠️ **UNVERIFIED — ridden-death test owed.** `lotr_monster_spider.xml` declares **5 of the 12** rein attributes the engine reads (`rein_handle_bone`, both `rein_handle_*_local_pos`, `rein_collision_1/2_bone`); every vanilla `Mountable` monster carries all twelve. Warg parity holds — it declares the same five. v1.4.8 fixed a "horse rein visual bug when a mounted agent died", native with no managed diff, in a path that runs on **mounted-agent death** ([v1.4.8-impact.md](../migration/v1.4.8-impact.md) N7). No crash is predicted; the 1.4.6 river battle below covered rider and spider deaths but predates this change. `audit_mount_parity.py` has no rein check (zero occurrences of "rein"; it always exits 0). Kill a ridden spider and a mounted rider and watch. Contract: [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) "The rein-attribute invariant" |
| Walk gait skew | ⚠️ known from the retarget work (pre-existing; polish) |
| Charge visual | 💡 unused 112KB `an_spi_charge` clip exists — possible upgrade over `an_spi_attack_charge` for the pounce; evaluate later |
| Inventory equip | ❓ retest (was the second AV repro; same root cause, expected fixed) |
| Campaign map icon | ❓ ladder step (c) pending (needs campaign) |
| Player riding / slope / conversation+inventory tableaus | ❓ ladder step (d) pending |
| Bite BT in battle | ✅ confirmed — `SpiderTree` fires for AI riders (2026-06-15 campaign log) |
| Directional attacks (pounce + L/R) | ✅ confirmed working in-game (2026-06-15) — clip matches enemy bearing; see "Directional attack model" |
| Bite damage / lethality | ✅ tuned + deployed (2026-06-15) — 75 base + speed, armor curve, 20% crit; 71-75/bite on Looters (one-shot light, ~2 medium, ~3 heavy). Tunable via `SpiderConfig` |
| Bite hit-rate (front-leg collision) | ⚠️ **fixed 2026-06-15, in-game confirm owed** — was ~3% connect (warg-placeholder bones + tight radius); now real front-leg bones (`joint40-44_r/l` = 14-18/19-23) + 1.8/1.5m radius. Watch HIT-vs-ATTACK in the diag log |
| Diagnostics | battery kept as a regression canary until the ladder completes (one-shot, 6 probes, ms-cheap); `docs/_scratch_characterspawner.cs` deleted; retire battery at ship |
| `.bak` inventory | `action_sets.xml.bak-spider-mount`, `monster_usage_sets.xml.bak-spider-mount`, `.bak-usage-enriched`, `lotr_monster_spider.xml.bak-*`, 9× `*_anm.tpac.bak-untagged`, `spider_correct_geo.tpac.backup` — clean up at ship |

## How-to: tag a movement clip

**Durable fix (Kit editor):** open the clip → **Clip usages** section (bottom of the properties
panel, below Flags) → add **`quad_movement`**; check the **`make_walk_sound`** Flag; set step
points. Full editor field map + per-category ADOD_Beasts flag recipes:
[spider-skeleton-animation-pipeline.md §3c](spider-skeleton-animation-pipeline.md).

**Interim byte-patch (what shipped 2026-06-11):** take a working ADOD_Beasts `_anm.tpac` (e.g.
`elephant_canter_anm.tpac`), keep its post-name layout (step points, the `make_walk_sound` flag
list + `quad_movement` usage list, movement params), substitute the target clip's file GUID (@8),
resource GUID (@52), name (+length @72), duration trio (pos+12 where pos=76+namelen), curve GUID
(both occurrences), blend float, trailer hash; fix the content-size u32 @28 (= filesize − 36).

## External-module change ledger

Every LOTRLOME_Armory change (what + why + rollback + make-permanent path) is recorded in
[docs/reference/lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md) —
the module is outside this repo, so that ledger is the only durable record of its state.

## History / references

- Root-cause session (this doc's RCA section): 2026-06-10 → 06-11, `/investigate`.
- Detached-era RCA: [`docs/reviews/rca-spider-troop-2026-06-04.md`](../reviews/rca-spider-troop-2026-06-04.md)
  (its "unsupported shape" verdict is superseded — the supported shape is the ridden mount).
- Mesh split + skeleton/animation authoring: [`spider-skeleton-animation-pipeline.md`](spider-skeleton-animation-pipeline.md).
- Locomotion/rider refinement workflow + theory: [`creature-animation-blender-mcp-workflow.md`](../ai-includes/creature-animation-blender-mcp-workflow.md).
- Elephant (the lane-prover + template donor): [`elephant.md`](elephant.md).
- Memory: `feedback_quad_movement_tag_required_for_gait_clips`,
  `feedback_nonhumanoid_creature_troop_not_mount` (revised — the mount verdict is reversed).

## Changelog

- 2026-06-15 — Lethal-bite tuning: 75 base + speed bonus, armor curve, 20% per-hit crit; front-leg bone-collision (joint40-44) with 1.8/1.5m radius replacing the warg-placeholder bones.
- 2026-06-15 — Directional attack model: priority pounce (`act_spider_attack_front`/`_charge`) + left/right swipes by enemy bearing; new BT nodes mirror the elephant 1:1.
- 2026-06-15 — Patch48 (`SpiderHitDismountGuard`): strips `BlowFlags.CanDismount` on hits to surviving mounted Spider Riders, avoiding the native `HandleBlowAux` AV; sibling of Patch47.
- 2026-06-14 — Restored the proven 6/11 working bundle (`spider_correct_geo.tpac`) from the user's backup, ending the multi-crash Kit-rework dead-end.
- 2026-06-14 — Re-bundled the dropped `spider_skeleton` resource INTO the mesh tpac via `tpac_skeleton_inject.py`; the prior standalone skeleton tpac crashed the engine.
- 2026-06-13 — Restored the loose `spider_skeleton` resource dropped by the Blender-loop mesh re-export (riderless-spider regression).
- 2026-06-13 — Retired the SpiderDiag probe battery (custom-battle NRE) and refined the idle.
- 2026-06-13 — Fixed the spider idle (walk-in-place) and mapped the rider-animation system source-side (Blender-MCP).
- 2026-06-12 — v1.4.6 engine-bump campaign: three native crash sites root-caused and fixed (`CanAttack`, jump lookup, Die path); spider mount GREEN on 1.4.6.
- 2026-06-11 — Giant spider rideable mount WORKING in battle; root cause was a missing `quad_movement` clip tag.

## Migrated notes (from CLAUDE.md, 2026-07-12)

- The L/R mesh split was done **2026-06-05** (on the later-refuted "~40 per-mesh palette" premise — see "Why this exists" #2/#3 above).
- The refuted premise, stated precisely: the only bone cap in the engine is the **64-bone `Skeleton.MaxBoneCount`**, NOT any per-mesh limit — one mesh skins the whole ≤63-bone skeleton, so a body is never split for bone count (memory: `feedback_no_40_bone_per_mesh_limit`).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)
- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/elephant.md](./elephant.md)
- [docs/features/spider-skeleton-animation-pipeline.md](./spider-skeleton-animation-pipeline.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/adod-beasts-architecture-and-taom-port.md](../reference/adod-beasts-architecture-and-taom-port.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md)
- [docs/reference/engine/agent-spawn-and-render-pipeline.md](../reference/engine/agent-spawn-and-render-pipeline.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
- [docs/reviews/rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)

<!-- backlinks-end -->
