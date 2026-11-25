# Spider (Giant Spider — Ridden Mount)

> **Status (2026-09-29): WORKING on KEYforce's new meshes, confirmed in game.** Five spider mounts
> ride one body and one skeleton: the forest skins `sk_spider_forest_a1` / `_a2` for Dol Guldur and
> the Mirkwood broods, and the mountain skins `sk_spider_mountain_a1` / `_a2` for the goblin tree
> (Goblin-town, the Misty Mountain Orcs and Blue Craig). Each is one whole mesh from
> `sk_spiders_a_geo.tpac`; the L/R split halves are retired. In Custom Battle on 2026-09-29 all five
> loaded without a crash, rendered whole, seated their riders, walked, ran and struck
> (`[MountSpawn]` for every item on `spider_skeleton` / `as_spider`). The spider is a plain
> **Mountable Horse-slot mount** ridden by a goblin, the warg and elephant pattern; the
> detached-combatant architecture of earlier revisions was deleted 2026-06-10 (git history keeps it).
> Open items are in "Current state" below.

## Overview

The Giant Spider is a **rideable mount**: a goblin rider carries a spider item in its Horse equipment
slot, and the vanilla cavalry spawn builds two agents, the goblin rider (`FromCharacterObj`) and the
spider mount (`FromHorseObj`, `Monster.spider`). No spawn interception, no Harmony patch on the spawn
path. Two rider lines use it:

| Line | Troops (level) | Mounts |
|---|---|---|
| Dol Guldur Spider Riders (`characters/spider_creature.xml`) | `taom_spider_creature` Spider Rider (21) → `taom_spider_rider_brown` Great Spider Rider (26) → `taom_spider_rider_pale` Pale Spider Rider (31) | Giant Spider (forest_a1, 1.0x) → Great Spider (forest_a1, 1.10x) → Pale Spider (forest_a2, 1.25x) |
| Goblin mountain spider riders (`troops/troops_goblin.xml`, shared by Goblin-town, the Misty Mountain Orcs and Blue Craig) | `goblin_lurker` (16) → `goblin_spider_rider` [Goblin] Spider Rider (21) → `goblin_spider_lord` [Goblin] Spider Lord (31) | Mountain Spider (mountain_a1, 1.0x) → Great Mountain Spider (mountain_a2, 1.25x) |

The Mirkwood spider broods ([creature-bandits.md](creature-bandits.md)) ride the three forest mounts
riderless. The spider auto-attacks enemies via a per-agent behavior tree (`SpiderBehaviorTree`,
attached by `SpiderMissionBehavior` keyed on `Monster.StringId == "spider"`, so every spider item gets
it), mirroring the elephant: a directional repertoire (priority pounce plus left and right swipes by
bearing) dealing damage radially in a front arc (see "Directional attack model" below).

## Why this exists (and the three architectures that led here)

LOTR needs Dol Guldur's giant spiders as a fielded force. Bannerlord has no first-class
non-humanoid roster troop, so three shapes were tried:

1. **Rideable mount (2026-06-03, first attempt)** — abandoned at the time for a map-icon
   `ForceUpdateBoneFrames` AV that was later put down to a missing `_map` child action set (a cause now UNVERIFIED, see the Child sets row; **corrected 2026-08-28**, this used to say
   `_map`/`_town_and_village` and the second half is wrong, see the Child sets row) and for the thumbnail/tableau AV whose true
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
4. **Whole meshes (2026-09-29).** KEYforce re-exported the spiders as whole meshes on the same
   `spider_skeleton` (`sk_spiders_a_geo.tpac`: two forest and two mountain skins). They loaded and
   rendered in battle on the first try, so the split is retired. Nothing here establishes why the
   June single mesh crashed `PreloadForRendering`; the new meshes simply do not.

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
| Monster | `ModuleData/Monsters/LOTR/lotr_monster_spider.xml` | `id="spider"`, `Mountable="true"`, `rider_sit_bone="chest_m"`, `num_paces="6"` (every mountable monster is 6; paces 0 to 5), **`family_type="1"`** (the horse family; was 11 during the bisect, returned to 1 on 06-11 for the vanilla rider-death/dismount surface; deployed is 1, and TAOM's Patch154 sets 13 in memory after the load, [creature-saddles.md](creature-saddles.md)), slope block (head + front/back leg roots), `action_set="as_spider"`, `monster_usage="spider"` |
| Action set | `ModuleData/action_sets.xml` → `as_spider` | `skeleton="spider_skeleton"`, `movement_system="quadrupedal"`, 69 rows since 2026-11-22 (#1541; 36 at the June fix). Every front, strafe, turn and stand row plays a `spider_*` clip with the `quad_movement` usage; the four in-place twins on the `none` rows and `spider_idle` carry none, as the vanilla horse's do ("Locomotion generated on its own rig" below). `act_horse_forward_canter` is bound explicitly (the tableau pose; warg precedent: `as_warg` binds it too) |
| Child sets | same file: `as_spider_town_and_village`, `as_spider_map` | `base_set="as_spider"`. **Only `_map` is load-bearing**: `SandBox.View` looks it up by name (`MBGlobals.GetActionSet(monster.ActionSetCode + "_map")`), and a miss returns the default set 0, a human set, not a throw ([animation system map](../reference/bannerlord-animation-system-map.md) section 7). Whether that caused the 2026-06-03 map-icon AV (above) is UNVERIFIED; elephant Crash #4 had another cause, its whole set family unregistered in native ([elephant.md](elephant.md) "Crash #4"). **CORRECTED 2026-08-28:** `_town_and_village` was listed here as equally required and is not. No managed code appends `_town_and_village` (0 hits, v1.4.8), and the rideable vanilla `as_camel` and `as_horse_2` ship without one. Keeping the spider's is harmless |
| Usage set | `ModuleData/monster_usage_sets.xml` → `id="spider"` | Full mount surface: 10 verb attrs (rear/dash/kick/quick-stops/jump/hit-object…), upper-body movements, movements (paces 0-5, gallop both foot variants), rider movement-adders (`act_horse_rider_*`: global codes animating the RIDER), jumps (`act_horse_jump_*`, bound in as_spider), falls, strikes. Every pace 0-5 keeps a `direction="none"` reference row (missing one = native ÷0, the 2026-06-04 crash). Since 2026-11-22 (#1541) the movement rows are the warg's table on the spider's actions, written by `tools/bind_spider_clips.py` |
| Mount items | `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` → `spider_mount_a`, `spider_mount_brown`, `spider_mount_pale`, `spider_mount_mountain_a1`, `spider_mount_mountain_a2` | Five items on one body (2026-09-29), each `is_mountable`, `<Horse monster="Monster.spider" maneuver=85 speed=65 charge_damage=15>` (the #615 retune) with `body_length` as the tier: 100 / 110 / 125 becomes `SetInitialAgentScale` 1.0 / 1.10 / 1.25 at build (`Mission.cs:4056`, the mumakil precedent). Forest: `spider_mount_a` "Giant Spider" on `sk_spider_forest_a1` (100), `spider_mount_brown` "Great Spider" on `sk_spider_forest_a1` (110; the id stays for saves, the brown skin is gone), `spider_mount_pale` "Pale Spider" on `sk_spider_forest_a2` (125). Mountain: `spider_mount_mountain_a1` "Mountain Spider" on `sk_spider_mountain_a1` (100), `spider_mount_mountain_a2` "Great Mountain Spider" on `sk_spider_mountain_a2` (125). Every mesh is whole, so no item has `<AdditionalMeshes>`. **No `<Materials>` block, on purpose:** `MountVisualCreator.SetMaterialProperties` applies it with `SetMaterialToSubMeshesWithTag(mat, "horse_body")` on the base mesh only, and the spider meshes carry no `horse_body` tag (Byak0's `warg_low` does), so the vanilla-horse recolour is a silent no-op here; each skin is its own mesh instead. Every C# seam keys on `Monster.spider`, never the item id, so a new spider item needs no code |
| Meshes (live) | `Assets/creature/spider/meshes/sk_spiders_a_geo.tpac` (8.9 MB, KEYforce, 2026-09-29) | Four whole metameshes: `sk_spider_forest_a1`, `sk_spider_forest_a2`, `sk_spider_mountain_a1`, `sk_spider_mountain_a2`, LOD0 to LOD5, skinned to `spider_skeleton`. The package bundles no skeleton of its own (`tpac_skeleton_scan.py` finds none), so it cannot collide with the one in `spider_correct_geo.tpac`, and it has its `RuntimeDataCache` entry (`check_rdc_entries.py`) |
| Skin variants (retired) | `Assets/creature/spider/animations/spider_variants_geo.tpac` (5.56MB, generated 2026-09-17) | Four metamesh items, `sk_spider_forest_a`/`_a_2` (a1) and `sk_spider_forest_b`/`_b_2` (a2): the #616 clones of the split c halves, built by `python tools/tpac_clone_metamesh.py` and cooked by one Kit save (the client renders meshes from `RuntimeDataCache/<package GUID>.rdc`, which only the editor writes). **No item references them since 2026-09-29.** The package stays on disk until the new meshes have shipped a release, as the rollback: restoring the three forest items' old `mesh` and `<AdditionalMeshes>` lines brings them back. Rebuild: the command in [lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md), 2026-09-17 section |
| Skeleton + old meshes (bundled) | `Assets/creature/spider/animations/spider_correct_geo.tpac` (2.78MB) | **Load-bearing: it carries the 62-bone `spider_skeleton` resource** that `as_spider`'s `skeleton="spider_skeleton"` resolves and every spider mesh rides; never delete or re-export it. LIVE = the proven 6/11 bundle, restored 2026-06-14 from `E:\LOTRAOMAssets\_tpac_backup_20260613\spider\` (skeleton `owner_guid a9ec7d87…`, `Usage='horse'`). It also holds the old `sk_spider_forest_c` / `_c_2` L/R-split meshes, unreferenced since 2026-09-29. (The split was made for the June 58-bone single mesh that AV'd `PreloadForRendering`; KEYforce's whole meshes on this skeleton do not, and that crash's cause is still unestablished.) **The 2026-06-13 Kit-rebuild bundle (different mesh) caused a FATAL battle-spawn AV** in the native `sound_and_collision_info` agent-build (`Agent.BuildAux`→native `Build`, RVA 0x490E02), preserved as `.bak-kitbroken-20260614`; my physics-transplant attempts (`.transplanted-20260614`, `.bak-bundled-crashing-20260614`) likewise didn't fix it. **Restoring the backup did, in one copy.** Lesson: `feedback_creature_rework_restore_from_backup_first.md`. Every battle since June ran on this skeleton, the 2026-09-29 ones included. |
| Skeleton history (DO NOT repeat) | — | The 06-13 Blender-loop mesh re-export shipped `spider_correct_geo.tpac` **mesh-only**, dropping the skeleton → `CreateAgentSkeleton` null → riderless spiders. First fix attempt (2026-06-13) extracted the skeleton into a **STANDALONE** `meshes/spider_skeleton_geo.tpac` via `tpac_skeleton_extract.py` — this **CRASHED** the engine (recursive worker-thread native AV reading null; the standalone reused the skeleton's item_guid as its package_guid + no creature ships a standalone skeleton tpac). Deleted 2026-06-14. Correct fix = re-bundle into the mesh tpac via `tools/tpac_skeleton_inject.py`. Skeleton source of truth: `meshes/sk_spider_forest_c_geo.tpac.backup` (item [2], the un-split a/b/c original). Mesh-only backup of the dropped state: `animations/spider_correct_geo.tpac.bak-meshonly-20260614`. Lesson: `feedback_mesh_reexport_drops_skeleton_resource.md`. |
| Clips | `Assets/creature/spider/animations/an_spi_*_{anm,geo}.tpac` | Loose-pair format (same as ADOD_Beasts elephant). Movement clips tagged (see ROOT CAUSE). The 13 `new_animation_clip*_anm.tpac` files are NOT garbage: Kit default filenames whose internal resource names are real (`an_spi_hit_front`, `an_spi_death_1`, …; quirk: `an_spi_idle2` has no second underscore). Since 2026-11-22 (#1541) the 15 `spider_*` pairs beside them carry the locomotion; the June gait clips are unbound and stay as the way back |
| Rider line | TAOM `Main/_Module/ModuleData/characters/spider_creature.xml` | Three rungs, the warg line's shape: `taom_spider_creature` Spider Rider (L21, Giant Spider, light uruk kit) upgrades to `taom_spider_rider_brown` Great Spider Rider (L26, Great Spider at 1.10, medium kit: a one-handed sword in every roster, and in Item1 a shield or, in one roster, a two-handed axe, never both, #1312; TwoHanded skill 130; "Brown" until 2026-09-29, id kept for saves) upgrades to `taom_spider_rider_pale` Pale Spider Rider (L31, Pale Spider at 1.25, heavy kit in the same shape, TwoHanded skill 170), the warg line's levels (16/21/26/31) from its second rung up. Every roster of a rung carries that rung's mount. Only the base is pooled (Dol Guldur fiefs, weight 1); the rungs cost 2 / 3 War Spoils on the party screen (`troop_resource_costs.xml`, player only) at the creature upkeep of 1; all three sit in `_HARNESSLESS_BY_DESIGN` with the base |
| Goblin rider line | TAOM `Main/_Module/ModuleData/troops/troops_goblin.xml` | Added 2026-09-29 for the mountain skins. `goblin_lurker` (L16) gains a second upgrade, `goblin_spider_rider` [Goblin] Spider Rider (L21, Mountain Spider), which upgrades to `goblin_spider_lord` [Goblin] Spider Lord (L31, Great Mountain Spider). One tree serves Goblin-town, the Misty Mountain Orcs and Blue Craig, so all three field them. Skills mirror the Dol Guldur rungs at 21 and 31, clamped up to the Lurker's so the upgrade costs no stat (`UPGRADE_SKILL_REGRESSION`); kit is the goblin tree's own L21 and L31 gear, every set with a one-handed weapon, a shield and the spider. No resource cost or Elite Emissary row (the goblin cultures have no special resource); both sit in `_HARNESSLESS_BY_DESIGN` |
| Troop weight | `TroopWeights/troop_weights.xml` | 4.0 for all five riders: the three Dol Guldur rungs and the two goblin ones. It was 3.0 (2 mount + 1 rider) until `c9fae4dc` (2026-07-24), which also set the Harad elephant rider to 10.0 (12.0 since `43dada47`) |
| Recruitment | `VolunteerRecruitmentService` DG settlement pools | weight 1, all Dol Guldur fiefs (intentionally absent from clan pools) |

> **The backup files named in this table moved on 2026-09-01.** Every `.bak*` and `.backup` sidecar
> in the three shipped modules was relocated to
> `E:\Bannerlord_Backups\module_bak_sweep_2026-09-01\`, same relative paths, because `.bak` breaks
> the Cloudflare distribution. That covers `spider_correct_geo.tpac.bak-kitbroken-20260614`,
> `.bak-meshonly-20260614`, `.bak-bundled-crashing-20260614`, `.transplanted-20260614` and the 24
> `an_spi_*_geo.tpac.bak-preskel` clips (30 files from the spider folder in all). **`meshes/sk_spider_forest_c_geo.tpac.backup` is the one to
> watch**: it has no live `.tpac` sibling and never did, so it is the sole copy of the
> `spider_skeleton` source above rather than a backup of anything. Copy it back before any restore
> step. See [module-backup-sweep](../reference/module-backup-sweep.md).

### Registration map (the `project.mbproj` truth — load-bearing)

Native animation XML loads ONLY from `LOTRLOME_Armory/ModuleData/project.mbproj` `<file>` entries
with **standard ids**: `soln_action_sets` → root `action_sets.xml`, `soln_action_types` → root
`action_types.xml`, `soln_monster_usage_sets` → root `monster_usage_sets.xml`. Custom ids are
silently ignored (2026-06-04 RCA, comment in the mbproj itself). The `Animations/` and
`MonsterUsage/` subfolder copies are superseded reference copies. SubModule.xml `<Xmls>` handles
only the managed types (Monsters, Items). The warg followed the same pattern before its data was
absorbed into LOTRLOME_Armory on 2026-08-28.

### Rider-side bindings (the thrust-loop fix, 2026-06-11)

When an agent rides a mount, the engine resolves the **mount's usage-set actions against the
RIDER's action set** to pick the rider's lean/sway overlay (this is what the
`as_goblin_warrior does not contain act_spider_*` rgl warnings were). Unresolved rider-side
lookups degrade per-set — the elephant's mahout falls back benignly, the spider's goblin looped
a thrust action. The supported mechanism (the warg precedent: the FIRST set in
`action_sets_warg.xml` is a partial `as_human_warrior`!) is a **partial `as_human_warrior`
block that the engine merges into the global set**, binding every usage-referenced mount action
to a rider animation. The spider's partial lives in the root `action_sets.xml` right above
`as_spider` (24 bindings in June, 55 spider rows since #1541) and **reuses the globally-registered `rider_warg_*` clips**
(they ship in LOTRLOME_Armory since the 2026-08-28 warg absorption; before that they made
Alliance.Wargs an undeclared hard dependency of the spider) until bespoke spider-rider clips
exist. `act_horse_*`
codes (rider adders, jumps, canter) are already vanilla-covered rider-side. Race sets like
`as_goblin_warrior` inherit the human-warrior surface (goblin WARG riders work via the same
merge), so one partial covers all riders incl. the player.

The goblin currently straddles the narrow warg back on the broad spider — the warg-straddle fit
problem + the bespoke re-pose path are covered in
[creature-animation-blender-mcp-workflow.md §4a](../ai-includes/creature-animation-blender-mcp-workflow.md).

### Code plane (TAOM `Main/Features/Spider/`)

| Piece | Role |
|---|---|
| `SpiderMissionBehavior` | Elephant-shape: registers `"SpiderTree"`, first-tick scan + `OnAgentBuild` late-attach keyed on `Monster.StringId == "spider"` (never character id, since the character is the goblin), dead pruning; skips a creature bandit (`!CreatureBanditAgents.Is(a)`), which gets its own tree, `CreatureBanditBehaviorTree`, from `CreatureBanditMissionBehavior`, so no spider carries two trees |
| `SpiderBehaviorTree` | Elephant-mirrored DIRECTIONAL tree (2026-06-15): `main → has rider → ai controlled → engage → [pounce-off-cooldown → SpiderPounceTask] / [side-off-cooldown → SpiderSideAttackTask] → idle`; player-ridden + riderless branches sleep. Blackboard: `PounceLastFired`/`SideAttackLastFired`/`TargetBearing` |
| `BehaviorTreeElements/SpiderEngageDecorator` | engage gate (replaces `SpiderCanBiteDecorator`): anti-chain (`IsSpiderAttack`) + zero-alloc `SpatialGrid` scan (reusable `_scratch` buffer) + cone hit check, then writes the NEAREST enemy's signed bearing (+=LEFT) to `TargetBearing` |
| `BehaviorTreeElements/SpiderAttackOffCooldownDecorator` | per-kind cooldown gate (`SpiderAttackKind.Pounce` ~5s priority / `SideAttack` ~2s gap-filler) via `IsOffCooldown` |
| `BehaviorTreeElements/SpiderAttackTaskBase` + `SpiderPounceTask`/`SpiderSideAttackTask` | stamp the kind's cooldown, then fire `SpiderAttack(kind, bearing)` (the clip plays; damage lands radially in that attack's arc) |
| `BehaviorTreeElements/SpiderAttackActions` | eager `ActionIndexCache` for the 4 clips (front/charge/left/right) + `ForName` resolve + `IsSpiderAttack` anti-chain + `AnyUnresolved()` drift guard |
| `SpiderAttackService` | pure (TaleWorlds-free): `SelectActionName` (pounce=front/charge by speed; side=left/right by bearing), `IsOffCooldown`, warg-pattern rider damage attribution, `IsSpiderMonster()`. The attacks take strike rules: a `SpiderStrikeProfile` per attack (max targets, damage multiplier, crit-only knockdown) grouped into a `SpiderStrikeSet` (bite, pounce, swipe); the ridden spider uses `SpiderStrikeSet.Ridden` (every enemy in the arc, full damage, knockdown at the damage threshold). `SpiderAttack` plays the clip and strikes radially through the adapter's `RadialStrike` (`SpiderConfig.StrikeRadius` 3.5 m; pounce arc ±70° about straight ahead, swipes centred ±45° with a 50° half-angle), logs `[Spider][diag] ATTACK fire`, and returns a `SpiderStrikeOutcome`; the capped path is testable through the internal `StrikeArc` seam |
| `SpiderStrikes.cs` | `SpiderStrikeProfile` (a per-attack rule: max targets, damage multiplier, crit-only knockdown), `SpiderStrikeSet` (the bite/pounce/swipe profiles, plus `SpiderStrikeSet.Ridden` for the mounted spider), `SpiderStrikes` (the pure selection and capping logic `SpiderAttackService` calls), `SpiderStrikeOutcome` (the result `SpiderAttack` returns), `SpiderStrikeCandidate` and `SpiderStrikeTargets` (the scanned-and-capped target set) |
| `AdvancedCombat/CustomAttacksUtils` (shared warg+spider+elephant) | synthetic-blow damage application. 2026-06-15 hardening: live-state revalidation + `IsBlowGeometrySafe` finiteness gate before the reflected `Mission.RegisterBlow` (defensive; NOT the fix for the dismount crash below) |
| `TaomAgentStatCalculateModel` (CareerSystem) | mount-lock: `CanAgentRideMount=false` + `MountDifficulty=999` for spider mounts (and elephant) — players can't steal the mount; the Horse-slot cavalry spawn ignores the lock for the assigned rider |
| `CharacterSpawnerService.SpawnMountLogged` (HeroRace) | instrumented replica of the engine's private `SpawnMount` with per-step logging + graceful mount-less degradation on failure. **Keep** (strictly better than the old blind reflective call); demote logging to `LogDebug` at ship. The one-shot `RunSpiderMountDiagnostics` probe battery and `TickProbe` were retired on 2026-06-13 and are gone from the source |
| `Hooks/Agent_Die_SpiderDismount_Patch` (Patch47) | **rider-death AV mitigation — REQUIRED, exonerated and re-enabled 2026-06-12.** A rider dying while seated AVs inside the native `Agent.Die` path (1.4.5: use-after-free 3× on 06-11; 1.4.6: melee-thrust repro 06-12 — Die-path lookup returned **float bits as a table index** from a corrupted action record, mixed-mode-debugger-proven: faulting `RAX+RCX*4` matched bit-for-bit with RCX = float −0.094). The patch routes around it: Prefix on `Agent.Die` hard-dismounts via the engine's own private `SetMountAgent(null)` (cached `AccessTools` at `Initialize`) so riders die the proven on-foot death (verified: `act_death_by_arrow_head2`, clean sever, 0 dead-linked riders); a dying spider frees its rider first. **The 06-12-morning indictment ("post-sever tick AV") was overturned** — that crash was the `CanAttack`/`set_attack_entity` charge CTD, Event-Log-proven to fire with AND without Patch47. Vanilla mounts untouched; body try/catch'd. Registered after Patch46 |
| `Hooks/Agent_HandleBlowAux_SpiderDismountGuard_Patch` (Patch48) | **non-lethal sibling of Patch47, applied and confirmed in game 2026-06-15** (see "The dismount-on-hit crash"). A finite real-melee `CanDismount` hit on a *surviving* mounted Spider Rider AVs inside native `Agent.HandleBlowAux` reading `0x3` (debugger-proven 2026-06-15; stack `MeleeHitCallback → Mission.RegisterBlow → Agent.RegisterBlow → HandleBlow → HandleBlowAux`). Same broken non-vanilla mounted-DISMOUNT native path Patch47 routes around on death, but Patch47 only covers death (it hard-dismounts before `Die`), so a non-lethal dismount hit still reaches the crash. Prefix on `Agent.HandleBlowAux` strips `BlowFlags.CanDismount` when the victim's mount is the spider Monster → native dismount never fires, rider stays on the locked mount, damage still applies. Covers every creature mount since 2026-11-08, as Patch47 does. Registered after Patch47 |

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
4 → 5. The real fang bones (`joint5_r/l` = 26/32, mouth `joint12_m` = 25) are available if a
bite-at-the-mouth model is wanted later.

**Superseded the same day by a radial strike.** Even on the right bones with a 1.8 m sphere, bone
collision connected only about 6% of the time on this big, fast mount, so damage is now dealt radially in
a front arc when the spider attacks, the elephant's model (`SpiderConfig`: `StrikeRadius` 3.5 m, pounce
arc ±70°, swipe arcs centred ±45° with a 50° half-angle). The front-leg clip still plays and the arc keeps
it a directed strike; the bone indices stay in `SpiderConfig` as a verified reference, wired to nothing.
The dials are those arc constants.

## Hit capsules fitted to the mesh (2026-09-18)

The spider's per-bone hit capsules (what weapons and missiles strike) were the Modding Kit's defaults, thin rods
along each bone: 40 of 62 bodies, and 49.9% of the skin sat inside any hit capsule. `tools/skeleton_hit_capsules.py` fitted them to the skinned mesh (both halves of the split body, `sk_spider_forest_c` and `sk_spider_forest_c_2`), a little bigger
than the skin and never more than 20 cm proud of it, and patched `Assets/creature/spider/animations/spider_correct_geo.tpac` in the live Armory (backup
`spider_correct_geo.tpac.bak-hitcapsules-20260918-201114`): 38 capsules refit, 97.2% of the skin now inside one. Legs, head, chest and fangs are covered fully, the abdomen bulb 87%. Read back through TpacTool.Lib with zero mismatches and every segment hash intact.
Method and format: [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md) "Hit capsules".

**Since 2026-09-29** the capsules are unchanged but the skins are new: they live on `spider_skeleton`, so all five
spider items (KEYforce's whole forest and mountain meshes) get them, yet they were fitted to the old
`sk_spider_forest_c` body. Nobody has measured how well they hug the new meshes; re-run the fit against a new
mesh if hits feel wrong.
**Owed:** a Custom Battle hit test (strike and shoot legs, head and abdomen) on the new meshes. The
2026-09-29 battles showed spiders taking and dealing damage, but no one aimed at body parts.

## Locomotion generated on its own rig (2026-11-22, #1541)

**What was wrong.** The June gait records were byte-grafts of elephant records (the fix for the missing
`quad_movement` usage), so their travel per loop and pace limits described another animal, and the clips came from
a retarget across two rigs with different bone axes. Read from the installed files with the gate's reader:

| June clip | Record said | Leg tips did |
|---|---|---|
| `an_spi_walk_2` | 3.60 m a loop, limits 4.8 to 7.2 (the elephant canter's) | 2.93 m; tips 0.20 to 0.30 m under the ground; the left front leg worked behind its rest point and the right one ahead of it |
| `an_spi_run` | 3.00 m, limits 4.3 to 13.0 | 6.45 m; the third leg pair never lifted; 0.59 m between the first and last key at the front tips |
| `an_spi_walk_left`, `_right` | 3.60 m | not mirror images: left lifted all eight tips together, right kept six tips under the ground |
| `an_spi_turn_left`, `_right` | 3.60 m | no loop: first and last key 2 to 4.5 m apart at the tips |
| `an_spi_idle`, `an_spi_jump` | 3.60 m | 0 m |

The spider had no backward walk. Pace 0 played the forward walk, the stand pace's row was an idle whose record
carried the canter's limits, and trot, canter and gallop all played `an_spi_run`.

**The method** (`tools/spider_gait.py`, pure Python; its header has the full account). No retarget: the tool places
each leg tip on a path and solves the five-bone leg to reach it, on `spider_skeleton`'s own rest pose
(`tools/tests/fixtures/spider_rig.json`, dumped from the installed skeleton).

- The gait is an alternating tetrapod: L1, R2, L3, R4 step together and R1, L2, R3, L4 half a loop later, each pair
  0.03 of a loop after the pair in front of it. A faster gait keeps that order and plants each tip for a smaller
  share of the loop.
- A planted tip moves back at the ground's speed and nowhere else. The swing is a quintic that joins the stance at
  both ends with no step in speed and no acceleration; a cubic scuffed the tip 6 cm near the ground.
- The leg bends at the knee to the target's distance (the upper bone, and knee to tip as one stiff piece), then
  turns about the upper bone's joint, first about the vertical and then in the vertical plane, so the knee stays up.
- Front tips sweep about a point 0.25 m ahead of rest and rear tips 0.25 m behind it, so neighbours do not meet.

| Clip | Keys | Loop | Travel | Pace limits | Plays on |
|---|---|---|---|---|---|
| `spider_walk` | 54 | 0.90 s | 2.00 m | 0.2 to 2.22 | `act_spider_walk_forward`, the `act_horse_jump_*` rows |
| `spider_trot` | 30 | 0.50 s | 2.30 m | 2.0 to 4.6 | `act_spider_trot_forward` (new) |
| `spider_canter` | 24 | 0.40 s | 2.80 m | 4.14 to 7.0 | `act_spider_canter_forward` (new), `act_horse_forward_canter` |
| `spider_gallop` | 16 | 0.267 s | 3.00 m | 6.3 to 13.0 | `act_spider_run_forward` |
| `spider_walk_backwards` | 54 | 0.90 s | 1.40 m | -1.56 to -0.2 | `act_spider_walk_backward` (new) |
| `spider_strafe_left`, `_right` | 48 | 0.80 s | 1.20 m sideways | the template's 0.2 to 2.5 | `act_spider_walk_left`, `_right` |
| `spider_turn_left`, `_right` | 48 | 0.80 s | 40 degrees | 0 to 0 | `act_spider_turn_left`, `_right` |
| `spider_stand` | 1 | 4.0 s | 0 | -0.4 to 0.4 | `act_spider_stand` (new); the two quick stops until 2026-11-23, when they took `spider_quick_stop` |
| `spider_idle` | 120 | 4.0 s | 0 | none | the four idle actions, the inventory and conversation rows, the taunt |
| `spider_<gait>_inplace` (four) | as the gait | as the gait | 0 | none | `act_spider_walk_stand`, `_trot_stand`, `_canter_stand`, `_run_stand` (new) |

A gait's upper limit is its travel divided by its loop time, the speed at which the full stride is right, and the
next pace starts at 0.9 of that. The vanilla horse's walk does the same: 1.52 m in 0.8 s, upper limit 1.9
(`Native/AssetPackages/animation_clips.tpac`, read 2026-11-22). A first gallop of 3.8 m a loop drew the front
legs out to 99% of their length; 3.0 m in 16 keys keeps them at 94%.

**The pace table** takes the warg's and the elephant's shape: pace 0 the backward walk, pace 1 the stand, paces 2 to
5 walk, trot, canter and gallop. The `direction="none"` row of paces 2 to 5 plays that gait's in-place twin: same
timing and lift, no sweep. Pace 0 played the walk's twin until 2026-11-23 and plays its own since,
`spider_walk_backwards_inplace` (#1550). The engine blends the twin with the gait by speed
([elephant.md](elephant.md), "What the engine does with a clip record"), so a slow spider takes short steps at the
gait's cadence. A twin's record is its gait's own with no clip usage, the shape of the vanilla `gait_walkfast` beside
`horse_walkfast`. The node reads a clip usage only from a pace's front, strafe-right and turn-right slots, and from
the `none` slot only the clip's Duration (native read of the movement node on v1.5.7, the #1541 review), so a twin
with no usage is safe at every pace. Eight new actions carry the table; each has a rider row in the partial
`as_human_warrior` and in the dwarf copy. A walk, trot, canter or backward row takes the clip the warg's rider
plays for that role; the gallop's twin takes `rider_warg_canter`, the clip the June `act_spider_run_forward` row has.
The spider's table repeats an action across rows (`act_spider_walk_left` and `_right` in every pace, as since June,
and now the walk's and the gallop's twins twice each), where the game's schema declares the action unique per table
and the other mounts keep that; two battles ran clean on it, and the native read (2026-11-23, under "Still open"
below) shows why it is safe.

**How the set is built.** No Kit import; one Kit start.

```
python tools/spider_gait.py --rig tools/tests/fixtures/spider_rig.json --out <set>
python tools/elephant_write_masters.py --clip <set>/<clip>.json --master <spider animations>/an_spi_walk_2_geo.tpac --new --out <spider animations>
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& tools\gen_elephant_anim_clips.ps1 -Reports @('<set>\report.json') -Prefixes spider_ -Folder <spider animations> -Templates @('<elephant animations>', '<warg animations>') -Apply"
(start the Modding Kit once with the Armory loaded, then close it)
python tools/bind_spider_clips.py --apply
python tools/check_gait_stride.py --creature spider
python tools/verify_mount_assets.py spider
```

The game and the Kit must be closed for every step that writes. The Kit cooks a clip's cache entry when it loads the
module, before any save. `bind_spider_clips.py` refuses while a clip it binds has no current entry, and it changes
the live `action_types.xml`, `action_sets.xml` and `monster_usage_sets.xml` and the three snapshot copies together.
Check sheets on the mesh: `tools/blender/elephant_apply_clip.py --arm spider_skeleton --harness spider_cfg.py
--rest-from-clip` on `E:\LOTRAOMAssets\_auto_workspace\spider_anim_WORK_20260612.blend`, which holds the engine's rest
pose in centimetres. Work folder of the first set: `E:\LOTRAOMAssets\Spider\gait_20261122\`.

**The way back.** The June `an_spi_*` clips are on disk and their records are unchanged. Copy the three files of
`E:\LOTRAOMAssets\Spider\bind_backup_20261122-184045\` over the live ones and the June binding is back.

**Two traps met on the way.**

- The editor refuses a clip whose sound point is at or past (Duration - BlendOutPeriod) / Duration, with a "Sound
  Point Error" dialog. Two observations stand behind the rule, no read of the editor: the refused twin (0.9 s, both
  blends 0.8 s, a point at 0.2, "should be smaller than 0.111111"), and `spider_gallop`, which loads with a
  blend-in of 0.3 s on a 0.267 s loop, so the blend-in is not in it. The first twins cloned `elephant_stand_2`, an idle record with sound points at 0.2 and 0.5, a
  `horse_eating` sound and a 0.8 s blend-out on loops of 0.27 to 0.9 s. `gen_elephant_anim_clips.ps1` now fails such a
  row before it writes.
- A record written again gets a new package GUID, and the entry the Kit cooked for the old one stays in
  `RuntimeDataCache` with no owner. Move it out with the record.

**Still open.**

- The turn records claim 0.873 (40 degrees in radians, divided by 0.8 s). The node advances a turn clip by turn
  speed / (Duration x LoopDisplacement) of the turn-RIGHT record, for both directions (native read, 0x74E9C0), so
  Duration x LoopDisplacement is the turn per loop in the unit of the turn-speed input. That unit is still not
  read. It is radians a second by inference only: the vanilla horse's and the mumak's product is 1.8, which is
  about one loop a second at a horse's turn rate, and fifty a second if the input were in degrees. Two ways to read
  it failed on 2026-11-23: the vanilla `animation_clips.tpac` (6,177 items) is in a cooked form `parse_anim` cannot
  read, and the trial cannot turn one spider on command (its input drives the view). A turn in place in a battle
  shows a wrong factor as slipping feet.
- The quick stops play `spider_quick_stop` since 2026-11-23, 1.5 s, the warg's stop length. Nobody has watched a
  hard stop. Whether the engine holds the mount for the clip's length is still not known.
- The spider in the party screen, the encyclopedia and a conversation plays `spider_idle`, which has no
  `quad_movement` usage. The vanilla horse's `horse_inventory_idle` has none either, and the June note named those
  screens as the first to crash on an untagged MOVEMENT clip; nobody has opened them on the new idle.
- `verify_mount_assets.py` exempts the four twins from its `quad_movement` check, as it does the dire wolf's. The
  vanilla horse, the warg and the elephant run that shape, and the spider ran it in two custom battles on 2026-11-22
  with no fault (the runs under "The spider gait step of the command trial").
- The seven attack clips (on twelve actions) and the rider's clips are still June's (#1550, slices B and C). The
  attacks wait because their timing must match `SpiderStrikes.cs`, which applies the damage.
- The table repeats an action across rows, where the game's schema declares it unique. That is safe: the loader
  (0x621E40, native read 2026-11-23) files each row in a map keyed on foot, pace, direction and turn direction
  (`foot | pace << 7 | direction * 8 + 8 | turn * 2 + 2`), with the action as the stored value, and the first row
  of a key wins. The row parser is 0x624B90.
- **A `none` row is a wildcard, so a pace's `none/none` row goes last.** The loader files a `direction="none"` row
  under "none" and all eight directions, a `turn_direction="none"` row under "none" and both turns, and an
  `is_left_foot="True"` row under both feet, each only where no earlier row holds the key. From #1541 to
  2026-11-23 the stand pace listed its `none/none` stand row first, so its two turn-in-place rows and its two
  strafe rows were never stored: a spider that turned in place played the stand clip. The warg lists the stand
  row last, and so does the spider now. `bind_spider_clips.py` (`hidden_rows`) fails a table with a row the
  loader never stores. Nobody has watched a turn in place since.

### The one-shot clips: hits, deaths, the stop and the jump start (2026-11-23, #1550)

`tools/spider_gait.py` writes 13 more clips. A one-shot is a list of keyed poses (`kind="keyed"`): body height,
pitch, roll, yaw, shift, abdomen, palps, a curl of the legs toward the body, and a move of each leg tip. Between
keys every value eases with a smoothstep, and each leg is solved on its target as in a gait. The first key is the
stand pose, so the clip blends in from any gait; a hit, the stop and the jump also end on it.

| Clip | Length | Record cloned from | Plays on |
|---|---|---|---|
| `spider_hit_front`, `_back`, `_left`, `_right` | 0.6 s | `elephant_hit` (no flags) | the four hits; hit object and rear damaged play the front one |
| `spider_death`, `_right`, `_left` | 1.5 s | `warg_death_backward`, `warg_death_right_side`, `warg_death_left_side` (`client_prediction`, `make_bodyfall_sound`) | `act_spider_death`, `_death_2` and the six falls |
| `spider_dead`, `_right`, `_left` | one pose, 2.0 s | the wargs' `*_continue` records | the six `*_continue` actions, which had replayed the whole fall |
| `spider_quick_stop` | 1.5 s | `warg_quick_stop` | both quick stops |
| `spider_jump_start` | 1.25 s, in place | `horse_jump_start` | `act_spider_jump`; the stride gate's `pending` entry for the June clip is gone |
| `spider_walk_backwards_inplace` | 0.9 s | the backward walk's own record, no usage | the new `act_spider_walk_backward_stand`, on pace 0's `none` row |

The abdomen's tip is 2.6 m behind the body's centre and 0.30 m off the ground, so each degree of nose-up pitch
lowers it about 4.5 cm. A pose that pitches up raises the abdomen, and a test fails any clip that puts a body bone
under the ground. A folded leg tip stays 1.3 m from its hip: the hind legs cannot fold below 1.12 m.

`act_spider_walk_backward_stand` is a ninth new action. Its rider row brings the dwarf set to 232 creature rider
rows (`DwarfMountRiderRowsTests`).

### Key times (2026-11-23, #1558)

A clip record plays the keys from its `Source1` to its `Source2`, and the editor accepts a track only when its key
times hold both. The first writer keyed a master 1 to N under sources 0 to N, and the Kit printed "ipo does not
fit" once per bone per clip at each cook. Now frame f sits at key time f, a loop gets a closing key at time N that
repeats frame 0 (so sources 0 to N hold N steps), and a one-shot has n keys under sources 0 to n-1.
`tools/elephant_write_masters.py` holds the rule and its source in its header, `--rekey loop|shot` migrates an
older master, and `tools/check_gait_stride.py` fails a bound clip whose sources reach outside its master's keys.
All 28 spider masters were migrated in place. `--rekey` takes the record's sources (`--sources 0:<N>`), always, and
migrates one form only, keys 1 to N under sources 0 to N. The sources are the caller's word that one record plays
the whole master: the tool sees no record, and a second record on the same master would be shifted. [RCA](../reviews/rca-spider-key-times-slide-capsule-2026-11-23.md).

### The overlay rows, and why the spiders slid (2026-11-23, #1559)

The engine lays the clip of a `monster_usage_upper_body_movement` row, and of the usage set's
`gallop_acceleration_head_action`, over the WHOLE body at a weight of up to 1. Since June the spider's rows had
played the turn-in-place actions at every moving pace and the idle on the gallop's acceleration, so in a charge
the idle replaced the gallop and the spiders slid. Measured over 11 spiders above 4 m/s, the leg tips moved 6.5 to
6.8 m/s against the body in the gait, 1.0 to 1.3 under the idle and 2.1 to 3.8 under the turn clip. The back legs
slowed as much as the front ones, so the overlay is not held to the bones under `neck_root_bone` (`spine2_m`).

Each moving pace's two rows now play that pace's own gait action, as the warg's rows do (`warg_trot_r`, 0.73 s,
beside `warg_trot`, 0.73 s), and the set names no gallop acceleration action, as the warg's names none.
`tools/bind_spider_clips.py` owns the 13 rows (`OVERLAYS`) and `--check` holds them. There are still no turning
variants of the gaits: a spider that turns while it runs plays the straight gait.

### The body capsule (2026-11-23, #1560)

The body capsule is what keeps two mounts, or a soldier and a mount, out of each other. June's was a pill around
the chest (radius 0.50, y 0.45 to -0.45), and the spiders ran into each other's abdomens. It is now radius 0.70,
pos1 `0.0, 0.25, 0.90`, pos2 `0.0, -1.95, 0.90`: from the head to the abdomen's tip, 3.6 m long and 1.4 m wide,
1.75 times a horse's width. The legs are outside it, as every mount's are. `python tools/verify_mount_assets.py
spider` fails a capsule that stops more than 0.10 m short of the foremost or rearmost bone on the centre line, or
whose radius does not reach the widest hip, that sits beside or above the centre line, or whose numbers are not
finite. Those are checks from above and of position; the gate does not prove that every bone is inside the capsule
(the abdomen's tip is 0.22 m outside its rear cap). `tools/data_gates.py` runs it as `spider-body`. Not tested: a spider at a gate or on a narrow path.

Two managed readers take the capsule's numbers. The rider's camera height is `rider_camera_height_adder` + pos1 z +
radius, times the mount's scale (`MissionScreen.cs:1263`, and the lock-on look height in
`MissionMainAgentController.cs:288`), so the wider capsule alone raised the camera from 2.0 to 2.5 m. The adder
went from 0.9 to 0.4 in the same file and the camera stays at 2.0. An AI agent's retreat distance is 15 times the
radius (`CommonAIComponent.cs:55`): 10.5 m now, 7.5 m before. What native collision and pathing do with the capsule,
and whether it scales with the mount, is not read.

### The spider gait step of the command trial

The rebuilt locomotion clips are bound in the live data and nobody had seen them in the game. The command trial
(`tools/run_command_trial.py`, [unattended runs](../reference/unattended-game-runs.md)) has a step for them, `spider_gaits`
(#1541). It runs only when named, with `enter` and no other step, in a battle of its own: twelve `fow_goblin_spider_rider`
troops join the player's army against the crew battle's 300 melee orcs. That is 413 soldiers; at the battle size
setting of 400 the engine holds 13 back as reinforcements, one of them a spider rider on the runs below, so the
line read `riders=11`.

Run it from a checkout built with `./build.ps1`, with no game running: `python tools/run_command_trial.py --steps enter,spider_gaits`.

The step waits up to 20 s for a rider on a mount whose Monster id is `spider`, holds the whole army with the Stop order and keeps
the Seat's view on one rider. It then reads the highest mount speed (the length of `Agent.MovementVelocity`) in three phases:
the last 2 s of 6 s held (`stand`), 14 s after the Charge order (`top`) and the last 2 s of 6 s held again (`stop`). It saves
four pictures, `hud_spider_gaits_1.png` to `_4.png`: the end of the stand, 3 s and 8 s into the charge, the end of the stop.
The view stays at the keep's own distance. A person reads the pictures.

The line is `[SeatTrial] step=spider_gaits result=<PASS|FAIL> riders=<n> mounted=<n> stand=<m/s> top=<m/s> stop=<m/s> pictures=<n> slide=<s> judged=<s>`,
then `kind`, `chosen`, `letgo`, `seated` and `faulted`. It passes with at least one rider, `top` of 3.0 or more, `stand` of 1.5 or
less, four pictures, `slide` of 2.0 or less, `judged` of 2.0 or more for each rider and the keep facts. `stop` and `mounted` are not in the rule, because the riders may be
fighting at the end; they are there for the reader. The limits 3.0 and 1.5 were set before the first run; the runs below read 9.1
to 11.4 at the charge and 0.0 standing.

`slide` (#1559) is the seconds, summed over all the spiders, in which a spider faster than 4 m/s moved its eight leg tips less
than 0.3 of its own way through a window of 0.5 s (`SpiderSlideMeter`; the host reads the tip bones' frames in the mount's own
space each mission tick). A gait reads 0.7 and more, the slide of 2026-11-23 read 0.17. A reading under 4 m/s is left out of a
window, and ends it only when the mount has been slow for more than 0.2 s; two readings more than 0.2 s apart end it too. A mount whose tip bones cannot be read is not judged.
`judged` is the seconds of fast movement the meter judged, slid or not: a `slide` of 0 from a run
with no skeleton or tip bone to read, readings too far apart, no fast spider or one spider read out of many measured too
little, and under 2 s of `judged` for each rider the step fails.
Not read: whether the bone frames carry the mount's scale (the share was measured on 1.0 mounts; at 1.25 a gait still reads
0.56), and whether a spider off the screen still animates.

Runs. The two of 2026-11-22 were on build `84f42bc85` with the #1541 changes not yet committed; the five of 2026-11-23 on `435e5644a` with this change not yet committed:

| Run folder | Line | What it showed |
|---|---|---|
| `E:\Temp\command-trial-runs\20261122-191545` | FAIL `riders=11 mounted=11 stand=2.7 top=10.3 stop=0.0 pictures=4` | No crash, clean unload. The rule then judged the whole stand, and the riders were still walking into their ranks when the Stop order came. One frame of 29.6 s at the spawn tick, in a session that compiled 1,067 shaders; earlier custom battle runs had 0.7 to 0.9 s. |
| `E:\Temp\command-trial-runs\20261122-192218` | PASS `riders=11 mounted=11 stand=0.0 top=10.0 stop=0.0 pictures=4` | The stand judged on its last 2 s and the view kept close. Worst frame 1.09 s, so the long frame of the first run did not come back. |
| `E:\Temp\command-trial-runs\20261123-103046` | FAIL `stand=0.0 top=9.1 stop=0.0 pictures=4 slide=3.5` | The first run with the slide meter, on the June overlay rows: the fault the maintainer had seen. |
| `E:\Temp\command-trial-runs\20261123-103422` | PASS `stand=0.0 top=10.6 stop=0.0 pictures=4 slide=0.0` | The overlay rows on each pace's gait, the key times migrated and cooked, the one-shot clips bound. |
| `E:\Temp\command-trial-runs\20261123-104155` | PASS `stand=0.0 top=10.8 stop=0.0 pictures=4 slide=0.0` | The same with the wider body capsule. |
| `E:\Temp\command-trial-runs\20261123-110756` | PASS `stand=0.0 top=11.4 stop=0.0 pictures=4 slide=0.0 judged=147.4` | After the review: the stand pace's `none/none` row last, the camera adder at 0.4, and the meter's `judged` fact (147 mount-seconds above 4 m/s). |
| `E:\Temp\command-trial-runs\20261123-112345` | PASS `stand=0.0 top=11.8 stop=0.0 pictures=4 slide=0.0 judged=152.9` | The final code of the day: a halt over 0.2 s ends a window, and the judged floor is 2 s for each rider (22 s for 11). |

What the runs prove: the new action sets and the pace table load, 11 riders spawn seated on `as_spider`, the spiders stand
still when held, reach 10 m/s at the charge (the gallop pace, limits 6.3 to 13.0, full stride at 11.25) and stop again. What they
do not prove: how any gait looks. The pictures are stills from above. The walk, trot and canter paces, a step back, a strafe and a
turn in place were not held long enough to see, and a person's eye in a battle decides those.

## Current state & known issues (updated 2026-11-23)

| Item | Status |
|---|---|
| **Own clips, key times, slide and capsule (2026-11-23)** | Gates and three custom battles pass (`slide=0.0`). Owed, a person's eye: a charge, a turn in place and a sidestep from a stand, the rider's camera, spiders side by side, a gate with the wider capsule, a hit, a death, a hard stop, a jump, a slow step back. The attacks and the rider's clips are still June's (#1550). |
| **KEYforce's whole meshes (2026-09-29, v1.5.3)** | ✅ Custom Battle, one change at a time: `spider_mount_a` on `sk_spider_forest_a1` first, then all five items. No crash at load, the troop picker or deployment; every spider renders whole (both sides, eight legs); riders sit on the back; walk, run and the strikes work. The TAOM log shows `[MountSpawn]` for all five items on `spider_skeleton` / `as_spider` and no exception |
| **Goblin mountain spider riders (2026-09-29)** | ✅ [Goblin] Spider Rider and [Goblin] Spider Lord ride the mountain skins in Custom Battle; the Dol Guldur rungs ride the forest skins, the higher tiers visibly bigger. Owed: the campaign upgrade from `goblin_lurker` on the party screen |
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
| **Rein attributes on v1.4.8 (2026-08-10)** | ✅ **crash-free on v1.5.3 (2026-09-29), rein visual not inspected.** Ridden spiders died in numbers in the 2026-09-29 Custom Battles (one ended with 36 of 84 alive) and every battle ran to the end. Original note: `lotr_monster_spider.xml` declares **5 of the 12** rein attributes the engine reads (`rein_handle_bone`, both `rein_handle_*_local_pos`, `rein_collision_1/2_bone`); every vanilla `Mountable` monster carries all twelve. Warg parity holds: it declares the same five. v1.4.8 fixed a "horse rein visual bug when a mounted agent died", native with no managed diff, in a path that runs on **mounted-agent death** ([v1.4.8-impact.md](../migration/v1.4.8-impact.md) N7). No crash is predicted; the 1.4.6 river battle below covered rider and spider deaths but predates this change. `audit_mount_parity.py` has no rein check (zero occurrences of "rein"); it always exits 0. Kill a ridden spider and a mounted rider and watch. Contract: [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) "The rein-attribute invariant" |
| Side steps from a stand (2026-11-24, #1591) | Open in #1550. The two side-step records hold a travel of 1.20 m, the legs' stride, and a `Duration` of 0.80 s. While the loop time is inside its clamp (a sideways speed of 1.4 to 2.7 m/s at model size) the node moves the body travel / `Duration` = 1.50 m in a loop ([elephant.md](elephant.md), "Standing pace"), so the legs slip by a fifth of the way. Under that band the clip's weight falls with the speed and the legs fit. A travel of 0.96 m fits inside the band and not under it, and it also changes the side step's blend weight at every moving pace, whose strafe rows use the same two actions. The sideways speed of a real side step is not measured, so no value is set. `tools/check_gait_stride.py --creature spider` prints both records on a `side` line, gives no verdict on the two numbers, and holds the legs' stance and slip |
| Walk gait skew | Replaced 2026-11-22 (#1541): the gait, turn, idle and stand clips are generated on the rig, see "Locomotion generated on its own rig". The rows above that name `an_spi_idle`, the turns and the walk describe the June clips |
| Charge visual | 💡 unused 112KB `an_spi_charge` clip exists — possible upgrade over `an_spi_attack_charge` for the pounce; evaluate later |
| Inventory equip | ❓ retest (was the second AV repro; same root cause, expected fixed) |
| Campaign map icon | ❓ ladder step (c) pending (needs campaign) |
| Player riding / slope / conversation+inventory tableaus | ❓ ladder step (d) pending |
| Bite BT in battle | ✅ confirmed — `SpiderTree` fires for AI riders (2026-06-15 campaign log) |
| Directional attacks (pounce + L/R) | ✅ confirmed working in-game (2026-06-15) — clip matches enemy bearing; see "Directional attack model" |
| Bite damage / lethality | ✅ tuned + deployed (2026-06-15) — 75 base + speed, armor curve, 20% crit; 71-75/bite on Looters (one-shot light, ~2 medium, ~3 heavy). Tunable via `SpiderConfig` |
| Bite hit-rate | ✅ moot: bone collision connected about 3% (placeholder bones), then about 6% (front-leg bones, 1.8 m sphere), so on 2026-06-15 damage moved to the radial front-arc strike (see "Damage + bite-collision tuning") |
| Diagnostics | the SpiderDiag probe battery was retired 2026-06-13 (`0e08df4c`) and is gone from the source; `docs/_scratch_characterspawner.cs` deleted |
| `.bak` inventory | every spider backup sidecar moved to `E:\Bannerlord_Backups\module_bak_sweep_2026-09-01\` in the 2026-09-01 sweep (see the note under the data-plane table) |

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

## Release pairing (2026-09-29)

Package the Armory in the same release as the TAOM build that names its new spider items. The goblin tree's
`goblin_spider_rider` and `goblin_spider_lord` (`troops/troops_goblin.xml`) ride `spider_mount_mountain_a1` and
`_a2`, which exist only in the Armory, and players get the Armory only from Mike's editor package. What the engine does
with a missing Horse item is unverified (a missing HorseHarness leaves the slot empty, `elephant.md`), so treat a TAOM
build without the matching Armory as broken for those two troops. `SpiderMountItemTests` pins the five items in the
live Armory on the desktop.

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

- 2026-11-23: own clips for the hits, the deaths with a held last pose, the quick stop and the jump start, and a twin for the backward walk (#1550, slice A); clip key times that the editor accepts, for the spider, the mumak and the war elephant (#1558); overlay rows on each pace's own gait, so a charging spider no longer slides (#1559); a body capsule that holds the body (#1560). The spider step of the command trial measures the slide. [RCA](../reviews/rca-spider-key-times-slide-capsule-2026-11-23.md).
- 2026-11-19: a bite spares a riderless mount on the spider's side (#594). The engine gives a mount no team, so `MountRiderSides` records a rider's team against his mount when he spawns on it or mounts it, and `CreatureBiteSides.Spares`, shared with the warg, compares that side; a riderless mount whose side is unknown is spared. The creature bandits' capped strike still skips every loose mount.
- 2026-11-09: Arachnophobia Mode (#1110) swaps every non-hero spider rider to a warg when it is on; see [arachnophobia-mode.md](arachnophobia-mode.md).
- 2026-09-29: KEYforce's whole spider meshes (`sk_spiders_a_geo.tpac`) replace the split halves: the Giant, Great (was Brown, now forest_a1 at 1.10x) and Pale Spiders on forest_a1/_a2, and two new mountain mounts on mountain_a1/_a2 for a goblin-tree rider line (`goblin_spider_rider` L21, `goblin_spider_lord` L31, off `goblin_lurker`). Confirmed in game; the split was never needed for these meshes.
- 2026-09-28: strike rules added for the creature bandits (#692), via `SpiderStrikeProfile` and `SpiderStrikeSet`; the ridden spider keeps `SpiderStrikeSet.Ridden` and is unchanged.
- 2026-09-17: Brown and Pale Spider mounts (#616): the a1/a2 skins ship as clones of the split c halves in `spider_variants_geo.tpac`, cooked by one Kit save; the Spider Rider becomes a three-rung line (L21 / L26 / L31) with the mount growing 1.0 / 1.10 / 1.25 per rung. The engine's `<Materials>` recolour needs a `horse_body` sub-mesh tag the spider lacks.
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
- 2026-09-29: the split is retired. KEYforce's whole meshes on the same skeleton load and render in battle.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)
- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/elephant.md](./elephant.md)
- [docs/features/spider-skeleton-animation-pipeline.md](./spider-skeleton-animation-pipeline.md)
- [docs/features/spider/wolf-parity-and-render-tests.md](spider/wolf-parity-and-render-tests.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/adod-beasts-architecture-and-taom-port.md](../reference/adod-beasts-architecture-and-taom-port.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md)
- [docs/reference/engine/agent-spawn-and-render-pipeline.md](../reference/engine/agent-spawn-and-render-pipeline.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/lotrlome-soln-id-fix.md](../reference/lotrlome-soln-id-fix.md)
- [docs/reference/lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
- [docs/research/creature-bandits-roadmap.md](../research/creature-bandits-roadmap.md)
- [docs/reviews/rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)

<!-- backlinks-end -->
