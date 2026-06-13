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
auto-bites enemies via a per-agent behavior tree (`SpiderBehaviorTree`, attached by
`SpiderMissionBehavior` keyed on `Monster.StringId == "spider"`), mirroring the elephant.

## Why this exists (and the three architectures that led here)

LOTR needs Dol Guldur's giant spiders as a fielded force. Bannerlord has no first-class
non-humanoid roster troop, so three shapes were tried:

1. **Rideable mount (2026-06-03, first attempt)** — abandoned at the time for a map-icon
   `ForceUpdateBoneFrames` AV that was later understood (missing `_map`/`_town_and_village`
   child action sets — the elephant Crash #4 class) and for the thumbnail/tableau AV whose true
   root cause took until 2026-06-11 to find (below).
2. **Detached riderless combatant (2026-06-04 → 06-05)** — `Mission.SpawnAgent` prefix
   hand-building a `FromHorseObj` spider with native wield guards. Worked in battle, then hit a
   `PreloadForRendering` AV from the 58-bone single mesh (per-mesh GPU palette ≈ 40) and was
   PAUSED. The whole machinery (Patch45, wield guards, spawn service, move task) was **deleted
   2026-06-10**.
3. **Rideable mount, take two (2026-06-10 → 06-11, current)** — after the war elephant proved
   the ridden-mount lane end-to-end, the spider was converted back: mesh split L/R to fit the
   bone palette, mount surface authored, and the tableau/mission AVs root-caused to a **missing
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
quadruped's movement clips carry that tag (verified by byte-diffing the upstream beasts pack's
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
working upstream pack clip against ours exposed the missing tag in minutes.

**The fix (how):** rebuild the movement clips' `_anm.tpac` on the elephant template — keep the upstream pack's
proven post-name layout (tags `quad_movement` + `make_walk_sound`, step-point fractions, movement
params), substitute each clip's own identity fields (file/resource/curve GUIDs, name, duration
trio, blend float, trailer hash). Patched clips: `an_spi_walk_2`, `an_spi_walk_left`,
`an_spi_walk_right`, `an_spi_run` (the only clips bound to movement actions). Originals preserved
as `*.bak-untagged` beside them. Non-movement clips (attacks/hits/deaths) correctly do NOT carry
`quad_movement` (the upstream pack's attacks don't either).

**THE LESSON (any future creature):** when compiling creature animation clips in the Modding Kit,
movement/gait clips MUST get the `quad_movement` flag (+ step points) in the Kit's animation
editor before saving. An untagged movement clip is a delayed native AV that detonates only when
a `movement_system="quadrupedal"` action set engages it — thumbnails, tableaus, and mounts, not
the detached spawn paths that earlier testing exercised.

## Architecture

### Data plane (all LIVE in `LOTRLOME_Armory` — registration map below)

| Piece | File | Key facts |
|---|---|---|
| Monster | `ModuleData/Monsters/LOTR/lotr_monster_spider.xml` | `id="spider"`, `Mountable="true"`, `rider_sit_bone="chest_m"`, `num_paces="6"` (every mountable monster is 6 — paces 0–5), `family_type="11"`, slope block (head + front/back leg roots), `action_set="as_spider"`, `monster_usage="spider"` |
| Action set | `ModuleData/action_sets.xml` → `as_spider` | `skeleton="spider_skeleton"`, `movement_system="quadrupedal"`, 36 bindings. Movement actions bind ONLY tagged clips (walk_2/left/right, run). Idles/turns/jump/taunt currently bind tagged walk clips (pose-correct idles need a tagged Kit recompile). `act_horse_forward_canter` is bound explicitly (the tableau pose; warg precedent — `as_warg` binds it too) |
| Child sets | same file: `as_spider_town_and_village`, `as_spider_map` | `base_set="as_spider"`; absence = map-icon/thumbnail AV class (elephant Crash #4) |
| Usage set | `ModuleData/monster_usage_sets.xml` → `id="spider"` | Full mount surface: 10 verb attrs (rear/dash/kick/quick-stops/jump/hit-object…), upper-body movements, movements (paces 0–5, gallop both foot variants), rider movement-adders (`act_horse_rider_*` — global codes animating the RIDER), jumps (`act_horse_jump_*`, bound in as_spider), falls, strikes. Every pace 0–5 keeps a `direction="none"` reference row (missing one = native ÷0, the 2026-06-04 crash) |
| Mount item | `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` → `spider_mount_a` | `is_mountable`, `<Horse monster="Monster.spider" maneuver=60 speed=40 charge_damage=10 body_length=100>`, `<AdditionalMeshes><Mesh name="sk_spider_forest_c_2"/>` (the L/R split second half) |
| Skeleton + meshes | `Assets/creature/spider/animations/spider_correct_geo.tpac` (2.7MB, split) | 62-bone `spider_skeleton` + `sk_spider_forest_c` / `_c_2` L/R-split meshes (≤38 bones/half; the unsplit 58-bone mesh AVs `PreloadForRendering`). Physics transplanted (62 bodies + 61 D6 joints). Pre-split original: `spider_correct_geo.tpac.backup` |
| Clips | `Assets/creature/spider/animations/an_spi_*_{anm,geo}.tpac` | Loose-pair format (same as the upstream pack elephant). Movement clips tagged (see ROOT CAUSE). The 13 `new_animation_clip*_anm.tpac` files are NOT garbage — Kit default filenames whose internal resource names are real (`an_spi_hit_front`, `an_spi_death_1`, …; quirk: `an_spi_idle2` has no second underscore) |
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
| `SpiderBehaviorTree` | `main → has rider → ai controlled → [bite gate → SpiderAttackTask → Sleep] → idle`; player-ridden and riderless branches sleep (engine mount AI runs) |
| `BehaviorTreeElements/SpiderCanBiteDecorator` | anti-chain gate (`IsSpiderAttack`), SpatialGrid scan, side via `RiderAgent?.Team.Side ?? Team.Side`, cone+range hit check |
| `BehaviorTreeElements/SpiderAttackActions` | eager `ActionIndexCache` for bite clips + `AnyUnresolved()` drift guard |
| `SpiderAttackService` | pure (TaleWorlds-free): bite gates, warg-pattern rider damage attribution, `IsSpiderMonster()` |
| `TaomAgentStatCalculateModel` (CareerSystem) | mount-lock: `CanAgentRideMount=false` + `MountDifficulty=999` for spider mounts (and elephant) — players can't steal the mount; the Horse-slot cavalry spawn ignores the lock for the assigned rider |
| `CharacterSpawnerService.SpawnMountLogged` (HeroRace) | instrumented replica of the engine's private `SpawnMount` with per-step logging + graceful mount-less degradation on failure. **Keep** (strictly better than the old blind reflective call); demote logging to `LogDebug` at ship. The one-shot `RunSpiderMountDiagnostics` probe battery + `TickProbe` are TEMP-DIAG — retire after the ladder |
| `Hooks/Agent_Die_SpiderDismount_Patch` (Patch47) | **rider-death AV mitigation — REQUIRED, exonerated and re-enabled 2026-06-12.** A rider dying while seated AVs inside the native `Agent.Die` path (1.4.5: use-after-free 3× on 06-11; 1.4.6: melee-thrust repro 06-12 — Die-path lookup returned **float bits as a table index** from a corrupted action record, mixed-mode-debugger-proven: faulting `RAX+RCX*4` matched bit-for-bit with RCX = float −0.094). The patch routes around it: Prefix on `Agent.Die` hard-dismounts via the engine's own private `SetMountAgent(null)` (cached `AccessTools` at `Initialize`) so riders die the proven on-foot death (verified: `act_death_by_arrow_head2`, clean sever, 0 dead-linked riders); a dying spider frees its rider first. **The 06-12-morning indictment ("post-sever tick AV") was overturned** — that crash was the `CanAttack`/`set_attack_entity` charge CTD, Event-Log-proven to fire with AND without Patch47. Vanilla mounts untouched; body try/catch'd. Registered after Patch46 |

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

## Current state & known issues (2026-06-12, post-1.4.6 campaign)

| Item | Status |
|---|---|
| Thumbnail / picker | ✅ mounted spider renders, no crash |
| Custom battle deployment | ✅ full formations spawn (riders seated, 8 legs) |
| Spider idle | ✅ pose-correct: `an_spi_idle` (3.5s) tagged + bound (idle_2 → `an_spi_idle2`, the no-underscore resource quirk) |
| Spider idle refinement (2026-06-12/13, Blender-MCP) | ✅ shipped idle was a walk-in-place bug → fixed via `freeze_toward_rest(legs, 0.92)` to a settled braced IN-PLACE idle (zero-degree loop seam, subtle body breathing), exported (workflow: [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)) |
| Turns / jump | ✅ `an_spi_turn_left/right` + `an_spi_jump` tagged + bound |
| Deaths / hits / attacks | ✅ natural clips bound (`an_spi_death_1/2`, `hit_front/right`, `attack_left/right/top`) — no quad tag needed (upstream pack parity) |
| Rider animation | ✅ partial `as_human_warrior` with 24 spider→`rider_warg_*` bindings (see "Rider-side bindings"); riders seated correctly in the 09:50 battle |
| Vanilla-map battle (full polish data) | ✅ 2026-06-11 09:50 `battle_terrain_biome_092`: playable, formations deployed, idles standing, riders seated, battery all-green, 0 mount failures |
| **v1.4.6 full battle (river map)** | ✅ **2026-06-12: charge + bank jumps + river crossing + prolonged melee + rider deaths + spider deaths, NO CRASH** — all three 1.4.6 crash sites fixed (see the engine-bump campaign section) |
| Rider death while mounted | ✅ Patch47 dismount-before-death re-enabled (exonerated 2026-06-12); riders die clean on-foot deaths; required on 1.4.6 (melee-death Die-path AV proven without it) |
| `lotrtaom_iron_hills_01_forceatmo` | ❌ **SEPARATE BUG — not spider. The scene has NEVER loaded: 8/8 CTDs** (2026-06-10 20:53→2026-06-12 06:27), all dying at `scene.xscene` load, pre-agent-spawn — incl. runs with all-green spider probes. `taom_gondor_village_001_forceatmo` loads fine, so the forceatmo/Patch16 mechanism is exonerated — it's this scene's assets. Several 6/10 "spider mission CTDs" were this scene, conflated into the spider evidence. **Removed from `custom_battle_scenes.xml` 2026-06-12** so it stops eating test runs; restore once repaired. Own issue/investigation |
| Walk gait skew | ⚠️ known from the retarget work (pre-existing; polish) |
| Charge visual | 💡 unused 112KB `an_spi_charge` clip exists — possible upgrade over `an_spi_attack_charge` for the pounce; evaluate later |
| Inventory equip | ❓ retest (was the second AV repro; same root cause, expected fixed) |
| Campaign map icon | ❓ ladder step (c) pending (needs campaign) |
| Player riding / slope / conversation+inventory tableaus | ❓ ladder step (d) pending |
| Bite BT in battle | ❓ verify `SpiderTree` fires for AI riders |
| Diagnostics | battery kept as a regression canary until the ladder completes (one-shot, 6 probes, ms-cheap); `docs/_scratch_characterspawner.cs` deleted; retire battery at ship |
| `.bak` inventory | `action_sets.xml.bak-spider-mount`, `monster_usage_sets.xml.bak-spider-mount`, `.bak-usage-enriched`, `lotr_monster_spider.xml.bak-*`, 9× `*_anm.tpac.bak-untagged`, `spider_correct_geo.tpac.backup` — clean up at ship |

## How-to: tag a movement clip

**Durable fix (Kit editor):** open the clip → **Clip usages** section (bottom of the properties
panel, below Flags) → add **`quad_movement`**; check the **`make_walk_sound`** Flag; set step
points. Full editor field map + per-category upstream pack flag recipes:
[spider-skeleton-animation-pipeline.md §3c](spider-skeleton-animation-pipeline.md).

**Interim byte-patch (what shipped 2026-06-11):** take a working upstream pack `_anm.tpac` (e.g.
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
- [docs/reference/lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
- [docs/reviews/rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)

<!-- backlinks-end -->
