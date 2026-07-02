# LOTRLOME_Armory changes for the Spider Mount (2026-06-10 → 06-14)

The giant-spider mount's **data plane lives entirely in the external `LOTRLOME_Armory` module**
(`E:\Steam\...\Modules\LOTRLOME_Armory\`), which is NOT in this repo. This ledger records every
change made there during the spider-mount conversion + RCA, **why** each exists, and how to roll
back or make permanent.

> ## 2026-06-14 RESOLUTION — restored the working bundle from backup (read this first)
>
> The 2026-06-13 Blender/Kit rework broke the spider across 4 native-AV layers (launch parse →
> thumbnail TickAnimations[HCSE-handled] → **fatal battle-spawn `sound_and_collision_info` build**,
> RVA 0x490E02). All the surgery below (skeleton transplant, `_anm` regen, standalone-extract,
> inject re-bundle) was a **dead-end rebuild** — the proven 6/11 working `spider_correct_geo.tpac`
> already existed in the user's whole-folder backup **`E:\LOTRAOMAssets\_tpac_backup_20260613\spider\`**
> (skeleton `owner_guid a9ec7d87…`, `Usage='horse'`, 6/10 pre-rework mesh). **Restoring that one file
> fixed it.** Live bundle is now that restored copy; the broken Kit-rebuild bundle is preserved as
> `animations/spider_correct_geo.tpac.bak-kitbroken-20260614`. **Rule going forward: when a rework
> breaks a creature, RESTORE the `_tpac_backup_<date>` folder first — don't rebuild.** See
> `feedback_creature_rework_restore_from_backup_first.md` +
> `docs/ai-includes/creature-mount-authoring.md` → "IF A REWORK BROKE A WORKING CREATURE". Companion docs: [spider.md](../features/spider.md) (architecture + the
full RCA) and [spider-skeleton-animation-pipeline.md](../features/spider-skeleton-animation-pipeline.md)
(asset pipeline + the `quad_movement` requirement).

> **Registration context (unchanged, but load-bearing):** native animation XML loads ONLY via
> `ModuleData/project.mbproj` `<file>` entries with standard ids — `soln_action_sets` → root
> `action_sets.xml`, `soln_action_types` → root `action_types.xml`, `soln_monster_usage_sets` →
> root `monster_usage_sets.xml`. The `Animations/` and `MonsterUsage/` subfolder copies
> (`action_sets_spider.xml`, `action_types_spider.xml`, `lotr_monster_usage_spider.xml`) are
> **superseded and unused** — do not edit them expecting effects. SubModule.xml `<Xmls>` carries
> only the managed types (Monsters, Items).

## 1. `ModuleData/action_sets.xml`

| Change | Why |
|---|---|
| `as_spider` rewritten — now 58 bindings: every `act_spider_*` action bound to its pose-correct clip (idles → `an_spi_idle`/`an_spi_idle2`, turns → `an_spi_turn_*`, deaths → `an_spi_death_1/2`, hits → `an_spi_hit_*`, directional attacks → their own clips); engine-fallback codes (8× `act_horse_jump_*`, conversation, inventory idles) → `an_spi_walk_2` | A ridden mount reaches bindings a detached walker never did. Every binding must resolve to a clip with real animation data — resolving an *unbound* action through `as_spider` returns a degenerate runtime record (`1002467048434979358_0`-class) and ticking it AVs |
| Explicit `act_horse_forward_canter → an_spi_walk_2` binding | The tableau/thumbnail pose (`PoseActionForHorse`) resolves canter against the mount's set; `as_warg` binds it explicitly too (the WOTS author hit the same issue — there's a commented-out earlier attempt in their file) |
| +12 `act_spider_fall_*` (+`_continue`) bindings → death clips | Mount knockdown machinery (see §2) |
| +10 typed verb bindings (`act_spider_rear`, `_rear_damaged`, `_dash`, `_kick`, `_quick_stop`, `_quick_stop_when_fast`, `_hit_object`, `_hit_object_while_falling`, `_strike_front`, `_strike_back`) → existing proven clips | Mount verb machinery (see §2) |
| Children added: `as_spider_town_and_village` (idle → `an_spi_idle`) + `as_spider_map` (4 `act_map_mount_attack_*` → `an_spi_attack_front`), both `base_set="as_spider"` | The engine resolves `<base>_map` / `<base>_town_and_village` for campaign map icons and settlement scenes; a missing child = native AV (the elephant's "Crash #4" class) |
| **Partial `as_human_warrior` block added** (MOVED TO TOP OF FILE 2026-06-11 — load-order critical: `base_set` inheritance snapshots at definition, so the partial must precede every race set; the goblin thrust-loop was this): 47 bindings (44 + 3 parity rows 2026-06-12) mapping every spider usage-set action to a RIDER animation (reusing the globally-registered `rider_warg_*` clips + vanilla `rider_fall_*` incl. `_continue` variants) | When an agent rides, the engine resolves the MOUNT's usage actions against the RIDER's action set for the rider's lean/sway/fall overlay. Unresolved = garbage rider channel (the goblin "thrust loop"). The mechanism is the Alliance.Wargs precedent — the FIRST set in `action_sets_warg.xml` is exactly such a partial, and the engine merges same-id sets across modules. Race sets (as_goblin_warrior etc.) inherit the human-warrior surface, so one partial covers all riders incl. the player |
| 2026-06-12 parity additions: `as_spider` +3 bindings (`act_spider_idle_1` → `an_spi_idle`, `act_spider_strike_front/back_while_moving` → `an_spi_attack_front`) = 61 total; rider partial +3 mirror rows (`idle_1` → `rider_warg_idle`, while-moving strikes → `rider_warg_hit_object`) = 47 total | New actions from the parity audit (§2/§3) need both mount-side and rider-side bindings |

Backup: `action_sets.xml.bak-spider-mount` (pre-2026-06-10 state).

## 2. `ModuleData/action_types.xml`

| Change | Why |
|---|---|
| +12 fall codes: `act_spider_fall_{right,left,roll,backwards,slow_right,slow_left}` + `_continue` siblings, all **`type="actt_fall"`** | **The native action dispatch is TYPE-driven.** Every working mount (warg, elephant, vanilla horses) registers typed fall codes WITH `_continue` siblings — the blow handler chains `<fall>_continue` on downed mounts. The spider's falls previously pointed at untyped `act_spider_death/death_2` → `Agent.HandleBlowAux` AV (`+0x3`) when a missile hit triggered the knockdown path |
| +10 verb codes typed `actt_rear` / `actt_dash` / `actt_kick` / `actt_mount_quick_stop` / `actt_hit_object` / `actt_mount_strike` | Same type-dispatch rule: `actt_rear` fires when a mount takes damage (mounts rear when shot), `actt_mount_strike` on tramples, etc. Untyped actions in the usage-set verb slots feed the wrong native handler. Mirrors the elephant's type table exactly |

Untouched: the original untyped `act_spider_*` codes (idle/walk/run/turn/attack/hit/death/
taunt) remain for the action-set bindings + the BT bite.

**2026-06-12 (v1.4.6 + parity audit) additions to this file:**

| Change | Why |
|---|---|
| `act_spider_jump` retyped **`actt_jump` → `actt_dash`** | Warg (`act_warg_jump_start`) AND elephant (`act_elephant_jump_start`) both type their usage-set `jump_start_action` as `actt_dash`. A jump-TYPED action absent from the `monster_usage_jump` rows misses the engine's `jump_actions_map_` — tolerated on 1.4.5, **AV on 1.4.6** (`monster_usage.cpp` sentinel deref at the first spider jump, ~34s into battle; the function self-identifies via its assert string) |
| +`act_spider_idle_1` **`type="actt_idle"`** | The warg's upper-body pace-1/front row uses `act_warg_idle_1` TYPED `actt_idle`; the spider's row pointed at the untyped `act_spider_idle`. Dedicated mirror action, kept separate from the untyped idle that feeds verb slots |
| +`act_spider_strike_front_while_moving`, `act_spider_strike_back_while_moving` (UNTYPED) | Warg's `is_heavy=False` strike rows use dedicated untyped `*_while_moving` actions; the spider reused the TYPED heavy action for light rows |

## 3. `ModuleData/monster_usage_sets.xml`

| Change | Why |
|---|---|
| Spider set gained the full **mount surface**: 10 verb attributes (now pointing at the §2 typed codes), `monster_usage_upper_body_movements`, `monster_usage_movement_adders` (global `act_horse_rider_*` codes — they animate the RIDER), `monster_usage_jumps` (`act_horse_jump_*`), `monster_usage_falls` (the typed fall codes, elephant row shape) | Every mountable monster's set carries this surface (horse/camel/warg/elephant); the spider's detached-era set had only movements + strikes — the shape of vanilla's NON-mountable sets (cow/sheep) |
| Pace 5 (gallop) rows added — movements with BOTH `is_left_foot` variants + gallop rider-adders; every pace 0–5 keeps a `direction="none"` reference row | `num_paces="6"` (§4) requires data per pace; a missing per-pace `direction="none"` reference row = native ÷0 at CreateAgent (the 2026-06-04 crash class) |
| Strikes matrix rebuilt on the elephant shape: 4 rows (heavy/light × front/back), `body_part="none"`, `impact="1"`, on the typed `act_spider_strike_front/back` codes | The old matrix had nonstandard left/right direction rows, `body_part="chest"`, `impact="2/3"` and untyped attack codes — none of which any working mount uses. Directional biting is the behavior tree's job, not the strike matrix's |

Backups: `monster_usage_sets.xml.bak-spider-mount` (pre-effort) and `.bak-usage-enriched`
(the enriched pre-strip state from the RCA bisect).

**2026-06-12 (parity audit) changes to the spider set:**

| Change | Why |
|---|---|
| Quick-stop slots REWIRED: `quick_stop_action` was `act_spider_idle` (untyped!) → now `act_spider_quick_stop`; `fast_quick_stop_action` was `act_spider_quick_stop` → now `act_spider_quick_stop_when_fast` | The typed verbs existed but sat in the wrong slots — `quick_stop_action` fed an untyped idle into a typed-verb slot (same lookup-miss bug class as the jump type). Warg/elephant/horse all wire both slots to `actt_mount_quick_stop`-typed actions |
| Upper-body pace-1/front row → `act_spider_idle_1` (typed `actt_idle`) | Warg parity (see §2) |
| Light strike rows (`is_heavy="False"`) → the new untyped `*_while_moving` actions | Warg-exact strike matrix shape: heavy=typed `actt_mount_strike`, light=untyped while-moving |

## 4. `ModuleData/Monsters/LOTR/lotr_monster_spider.xml`

| Change | Why |
|---|---|
| `num_paces` 5 → **6** | Every mountable monster in the game is 6 — the mount machinery indexes the gallop pace (5). The detached-era 5 left no pace-5 data for a `Mountable` monster |
| `family_type` 1 → 11 → **1** (final) | First moved to 11 to isolate horse harnesses (elephant precedent = 10), then **returned to 1 during the 2026-06-11 rider-death bisect**: family 1 (the horse family) carries vanilla's complete rider-death / dismount / rider-fall surface, which the warg relies on. Family 11 had no such surface anywhere. The harness-isolation concern is handled by the mount-lock in `TaomAgentStatCalculateModel` instead |
| Ground-slope block added: `front/back_bone_to_detect_ground_slope_index` (2/4 — indices into the modify list) + `bones_to_modify_on_sloping_ground_0..4` (`head_m`, `joint40_l/r`, `joint22_l/r`) | Lets the quadruped slope system pitch the body on hills (warg/elephant parity); indices verified against decompiled `MonsterExtensions` |
| Rein surface added (2026-06-11): `rein_handle_bone="head_m"`, `rein_handle_left/right_local_pos`, `rein_collision_1/2_bone="chest_m"` | Warg-minimal rein surface — every working mount declares one; part of the rider-death bisect equalization |
| **Flags pruned to the warg's EXACT set** (2026-06-12): `CanCharge`, `CanRear`, `CanWander`, `Mountable`, `RunsAwayWhenHit` — `CanAttack` and 8 other detached-era extras REMOVED | `CanAttack` on a mountable monster activates the engine's own attack-AI (`Agent_ai::set_attack_entity`) — a path NO working mount takes, and the site of the v1.4.6 charge-crash (null `agent+0xAD8` deref, Event-Log-proven with and without Patch47). The warg's BT bite works without it; so does ours. The other extras (`CanBeCharged`/`CanSprint`/`CanBeInGroup` + explicit-false rows) are declared by no baseline mount (parity audit) |
| Rider capsule/eye adders added (2026-06-12): `rider_eye_height_adder="1.7"`, `rider_body_capsule_height_adder="0"`, `rider_body_capsule_forward_adder="0"` | Warg + vanilla horse both declare them; spider was the only mount without (parity audit) |

(`Mountable="true"` + `rider_sit_bone="chest_m"` predate this effort — 2026-06-03.)

## 5. `Assets/creature/spider/animations/` (tpacs)

| Change | Why |
|---|---|
| `spider_correct_geo.tpac` replaced with the Kit-recompiled **L/R split-mesh** build (2.7MB): `sk_spider_forest_c` + `_c_2` each ≤38 bones, physics re-transplanted (62 bodies + 61 D6 joints) | ~~The unsplit 58-bone single mesh exceeds the ~40-bone per-mesh GPU palette~~ → `Agent.PreloadForRendering` AV (the 2026-06-05 detached-era killer). **CORRECTION 2026-06-13: the ~40 per-mesh palette is FALSE** (elephant renders 59 active bones in one mesh; the only cap is the 64-bone skeleton). The AV's true cause is unestablished; the split was unnecessary for bone count — see `feedback_no_40_bone_per_mesh_limit`. `spider_mount_a` consumes the second half via `<AdditionalMeshes>` |
| **9 `_anm.tpac` files byte-patched onto the upstream pack's elephant templates** — `an_spi_walk_2`, `walk_left`, `walk_right`, `idle` (file `new_animation_clip_3`), `idle2` (4), `turn_left` (5), `turn_right` (6), `jump` (7) on the *canter* template; `an_spi_run` on the *gallop* template (adds `cyclic` + gallop speed params, since run serves paces 3–5) | **THE ROOT CAUSE of every mount-context AV:** the clips were Kit-compiled without the `quad_movement` clip usage + step points that every working quadruped's gait clips carry. A `movement_system="quadrupedal"` action set measuring untagged gait clips builds a null native gait structure → AV (`+0x10`) on the first `Skeleton.TickAnimations` in thumbnails, inventory tableaus, AND missions. The patch grafts the upstream pack's proven post-name layout (tags, step points, params) while keeping each clip's own GUIDs/name/duration — full recipe in spider.md "How-to"; Kit-editor field locations in the pipeline doc §3c |
| NOT modified: the 12 remaining clip pairs (deaths, hits, directional attacks, charge…) | Byte-diff proved them structurally identical to the playback-proven files; non-gait clips correctly do NOT carry `quad_movement` (the upstream pack's attacks/deaths don't either — they carry `lock_movement`/`make_bodyfall_sound`-class polish flags, a Kit-recompile polish item) |

The Kit filename quirk: `new_animation_clip_N_anm.tpac` files are NOT garbage — the resource
*names inside* are real (`an_spi_idle`, `an_spi_death_1`, …; one quirk: `an_spi_idle2`, no second
underscore). tpac filename ≠ resource name.

## Rollback / backup inventory

| File | Backup |
|---|---|
| `action_sets.xml` | `.bak-spider-mount`, `.bak-parity-146` (pre-2026-06-12 parity round) |
| `action_types.xml` | `.bak-jumptype-146` (pre jump retype), `.bak-parity-146` |
| `monster_usage_sets.xml` | `.bak-spider-mount`, `.bak-usage-enriched`, `.bak-parity-146` |
| `lotr_monster_spider.xml` | `.bak-spider-mount`, `.bak`, `.bak-canattack-146` (pre CanAttack removal), `.bak-parity-146` |
| 9 patched `*_anm.tpac` | `*.bak-untagged` beside each (+ `an_spi_run_anm.tpac.bak-canter-template`) |
| `spider_correct_geo.tpac` | `meshes/sk_spider_forest_c_geo.tpac.backup` (7.8MB pre-split build; ALSO the source of `spider_skeleton` for the 06-13 fix below) |
| `SubModule.xml` | `.bak-elephant` (predates this effort) |

## 2026-06-13 — Blender-loop rework regression + the skeleton fix

The user reworked the spider in a Blender loop and Kit-recompiled (backing up files first). A
post-rework audit found everything sound EXCEPT a HIGH regression: the new
`Assets/creature/spider/animations/spider_correct_geo.tpac` (2.78MB) shipped **mesh-only**
(`sk_spider_forest_c` + `_c_2`), dropping the `spider_skeleton` Skeleton resource. The 06-12
working build's geo tpac had carried the skeleton; the re-export did not. With no live loose tpac
providing it (it survived only in `meshes/sk_spider_forest_c_geo.tpac.backup`), the action_set's
`skeleton="spider_skeleton"` resolved to nothing → `CreateAgentSkeleton` null → riderless
spiders. Root-caused by symmetry with the working elephant (`elephant_skeleton` lives in its live
loose `mesh/adod_elephant_geo.tpac`; the engine loads loose Assets, not the stale 6/10 baked
`AssetPackages/pack6.tpac`).

| Fix (2026-06-13, REVERTED — CRASHED) | `Assets/creature/spider/meshes/spider_skeleton_geo.tpac` (4.5KB) — a STANDALONE skeleton-only tpac extracted from the backup via `tpac_skeleton_extract.py`. **This crashed the engine** on spawn: a recursive worker-thread native AV reading null (`0x00007FFDAE001397` in `TaleWorlds.Native.dll`). Two defects — (1) it reused the skeleton's item_guid as the package_guid (every working tpac has a DISTINCT package guid; the collision drove a recursive resolver into null), and (2) no shipping creature uses a standalone skeleton tpac at all. Deleted 2026-06-14. |
| Fix (2026-06-14, CORRECT) | Re-bundled the skeleton **into** the new mesh tpac with the new repo tool `tools/tpac_skeleton_inject.py`: `spider_correct_geo.tpac` is now a **4-item** tpac (`spider_correct.fbx` + `sk_spider_forest_c` + `_c_2` + `spider_skeleton`), keeping its own distinct package guid `f544…`. The skeleton data is **bit-identical** (sha256-verified) to `sk_spider_forest_c_geo.tpac.backup` item [2]. This is the PROVEN bundled structure — skeleton WITH mesh in one tpac, exactly like the working elephant's `adod_elephant_geo.tpac`. Post-deploy gate `verify_mount_assets.py spider` = PASS. Mesh-only state backed up as `animations/spider_correct_geo.tpac.bak-meshonly-20260614`. **In-game spawn-with-rider verification owed (deployed 2026-06-14).** |

**Make permanent (the durable path):** recompile the 9 patched clips in the Modding Kit with the
fields set in the editor — `quad_movement` in **Clip usages**, `make_walk_sound` Flag, step
points — then the byte-patched files (and their `.bak-untagged` siblings) can be deleted. Apply
the per-category flag recipes from the pipeline doc §3c to the attack/death clips at the same
time. After the spider ships, sweep all `.bak-*` files.

## Verification trail (what each change fixed, in order)

1. Mesh split + children + mount surface → thumbnail still AV'd (led to the probe battery).
2. `quad_movement` byte-patch (walk_2) → **all 6 battery probes flipped green; thumbnail +
   deployment work** (2026-06-11 08:22).
3. Remaining gait tags + pose-correct rebinds + rider partial → battle verified, idles stand,
   riders seated (09:50).
4. Gallop-template run regraft → **charge-to-contact works** (15:19 — crash moved to blow
   handling).
5. Typed falls + verbs + elephant strike matrix → engine-binding probes proved every typed code
   loaded EXCEPT `act_spider_strike_back`, which resolved to the degenerate record — because its
   binding target **`an_spi_attack_back` does not exist** (a name invented by plausibility when
   authoring the typed-verb map; `act_spider_kick` had the same phantom). **A binding to a
   nonexistent animation compiles into the degenerate record** — the same class as an unbound
   action — and the spider's rear-strike/blow path dereferencing it caused both arrow-hit
   crashes (mount `HandleBlowAux` AV, then the rider `Die()` use-after-free). Both rebound to
   `an_spi_attack_front`; a phantom-target sweep (every `animation="an_spi_*"` checked against
   the actual `_anm` resource inventory) now reports NONE. **Rule: validate binding targets
   against the resource inventory, never by name plausibility.**
6. **2026-06-11 17:39: Steam force-bumped the game 1.4.5 → 1.4.6 mid-campaign** (Version.xml +
   DLL timestamps). On 1.4.6 every spider battle CTD'd at native `Agent_ai::set_attack_entity`
   (fault offset `0x6BAB4E`, null `agent+0xAD8`) right after a cavalry charge — Event Log proved
   the identical offset with AND without Patch47, including a run with ZERO deaths. Root cause:
   `CanAttack="true"` (§4) put the spider on an engine attack-AI path no working mount takes;
   1.4.6's rewrite of that path dereferences unguarded. Removal verified: next battle ran 4,200+
   ticks through charge + melee + rider deaths.
7. Second 1.4.6 site: `monster_usage.cpp` `jump_actions_map_` sentinel deref (offset `0x634396`,
   target `0x3`) at the first spider jump — root cause the `actt_jump` typing of the
   `jump_start_action` (§2). Retyped `actt_dash` (warg + elephant parity). Same battle also
   proved **1.4.6 fixed the original mounted-death AV natively**: 7 riderless spiders existed
   with Patch47 disabled and no `Agent.Die` crash — Patch47 is disabled in `SubModule.cs` and
   slated for deletion if the post-parity retest stays green.
8. Full parity audit (`tools/audit_mount_parity.py`, repo) — spider vs warg vs elephant vs
   vanilla horse across Monster XML / usage sets / action types / action-set bindings / rider
   partial: 5 further deltas fixed (Flags prune, rider adders, quick-stop rewire, typed idle_1
   row, while-moving light strikes — §2/§3/§4). BT code parity separately verified clean
   (mission behavior = elephant wiring; tree = elephant shell + warg pacing + warg died-listener).
9. Post-parity battle still hit the jump-map site at ~14,512 ticks (3.4× longer than
   pre-parity) — live debugger caught a spider mid `act_horse_jump_high_loop` with **corrupted
   action-name strings** (interleaved across record boundaries). Root cause completed: the
   engine's jump lookup accepts NINE directions (the parser's own vocabulary); front+none-only
   rows leave directional mid-jump queries unanswerable. **45-row total jump table** (§3) —
   riverbank jumps confirmed the trigger environment (user: river map, spiders in/near it).
10. Final 1.4.6 site: melee thrust killing a MOUNTED rider → native `Die`-path AV at RVA
    `0x5FE0C9` — mixed-mode debugger + disassembly proved the death resolution consumed a
    corrupted action record (**float bits −0.094 used as a table index**; faulting address =
    `RAX+RCX*4` bit-for-bit). The 9 byte-patched `_anm` tpacs were structurally verified CLEAN
    (donor-exact layout/strings/sizes) and every animation target our sets reference exists in
    the real tpac inventories (incl. vanilla `animation_clips.tpac`) — no phantoms. **Patch47
    exonerated** (its earlier indictment was crash site #1, which fired without it) **and
    re-enabled**: severed riders die clean on-foot deaths; this routes around the engine's
    corrupt-record death path.
11. **2026-06-12 FINAL: full river battle on v1.4.6 — charge, bank jumps, river crossing,
    prolonged melee, rider deaths, spider deaths — NO CRASH** (user-confirmed). The elephant's
    jump table got the same 45-row expansion proactively (its set shared the template; its
    jump_start was already `actt_dash`; it has no `CanAttack`). End-to-end workflow distilled to
    `docs/ai-includes/creature-mount-authoring.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/spider.md](../features/spider.md)

<!-- backlinks-end -->
