# Creature Mount Authoring — the complete workflow

How to take a non-humanoid creature from raw assets to a **rideable, battle-stable mount** in
TAOM on Bannerlord 1.4.6. Distilled from the two campaigns that proved every step the hard way:
the **war elephant** (2026-06-03 → 06-10, upstream-pack port) and the **giant spider**
(2026-06-04 → 06-12, custom skeleton + custom clips — the maximal case). The warg
(Alliance.Wargs) is the always-on reference implementation: when in doubt, do what the warg does.

**Architecture in one line:** a creature mount is a vanilla cavalry spawn — a rider TROOP with a
Horse-slot ITEM whose `HorseComponent` names a `Monster`; the engine does all mount work
(movement, blows, deaths, rider seating) off that Monster's data surfaces, and TAOM layers
attacks on top with a per-agent behavior tree. **No spawn patches, no detached combatants** —
that architecture was built twice and deleted twice (see spider.md "the three architectures").

Companion docs: [spider.md](../features/spider.md) (architecture + every RCA),
[elephant.md](../features/elephant.md), [spider-skeleton-animation-pipeline.md](../features/spider-skeleton-animation-pipeline.md)
(asset pipeline), [lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
(external-module change ledger), [mount-and-rider-runtime.md](../reference/engine/mount-and-rider-runtime.md)
(engine internals).

---

## ⚠️ REPLACING FBX / TPAC FILES — read before EVERY refine cycle

**Animation refinement is constant; the asset-swap is where a working mount silently breaks.** A
Blender rework → Kit recompile → deploy of new `_anm`/`_geo` tpacs can drop something the
deployed XML still depends on, and most failures are SILENT (riderless spawn, no crash) or only
crash in-mission. **Back up first** (rename the old file to `*.backup` — already standard here),
then know these requirements BEFORE replacing, and run the gate AFTER.

> ## 🛑 IF A REWORK BROKE A WORKING CREATURE: RESTORE THE BACKUP FOLDER FIRST — DO NOT REBUILD
>
> **The single most expensive lesson of the 2026-06-14 spider session.** When a Blender/Kit rework
> breaks a previously-working creature, the FIRST move is to **restore the whole-creature backup
> folder** the user takes whenever a creature works — NOT to surgically rebuild the skeleton / clips
> / physics. That session spent ~a full day reconstructing a skeleton (physics transplant), clips
> (`_anm` regen), and chasing native AVs (launch → thumbnail → battle-spawn `sound_and_collision`),
> when the **proven working bundle already existed** in a dated backup folder. Restoring one
> `spider_correct_geo.tpac` from it fixed in one copy what hours of surgery could not.
>
> **Where the backups live** (ask the user; these are the known TAOM locations as of 2026-06-14):
> - **`E:\LOTRAOMAssets\_tpac_backup_<YYYYMMDD>\<creature>\`** — the user's "whole folder when it
>   worked" backup (has `spider\` + `elephant\` subfolders: the live `Assets/creature/<c>/` mirror —
>   `animations/`, `meshes/`, `textures/`, the `_geo`/`_anm` tpacs). **This is the gold copy.**
> - `E:\LOTRAOMAssets\Elephant\_backups\`, `E:\LOTRAOMAssets\_auto_workspace\_backups\`,
>   `E:\spider_anim_backup_<date>\` — older / workspace backups.
> - My session-made backups (`_spider_rebuild_backup_<date>\`, `*.bak-*` siblings) capture the
>   BROKEN/intermediate states — NOT the working one. The user's `_tpac_backup_<date>` is the working one.
>
> **How to tell the working bundle from a broken one** (before restoring): the working skeleton has
> `Usage='horse'` + per-bone bodies + N-1 D6 constraints (`tools/tpac_skeleton_transplant.py <tpac>
> <skel> --dry-run`), and a **distinct skeleton `owner_guid`** from the raw 5/1 source. A raw skeleton
> (`Usage='other'`, 0 constraints) launch-crashes; a mesh-only re-export goes riderless. Hash-diff
> (`Get-FileHash`) the backup vs the live bundle to confirm they actually differ before copying.
>
> Only rebuild from FBX if NO working backup exists. Even then, clone the **warg's** working
> skeleton+collision setup (a 49-bone single-mesh creature mount that builds cleanly) rather than
> authoring from scratch, and test incrementally: launch → thumbnail → spawn → battle.

### The four ways a re-export breaks a working mount (all observed)

| # | What the swap drops | Symptom | Detect | Fix |
|---|---|---|---|---|
| 1 | **The Skeleton resource** — a mesh/geo re-export ships **mesh-only**, dropping `<creature>_skeleton` | `CreateAgentSkeleton("<creature>_skeleton")` → null → **RIDERLESS mount** (graceful, NO crash — easy to miss) | the skeleton must exist as a `type=Skeleton` resource in a **live (non-`.backup`) loose tpac**, exactly like the working elephant (`elephant_skeleton` **bundled with the mesh** in live loose `mesh/adod_elephant_geo.tpac`) | **Re-BUNDLE it into the new mesh tpac**: `tools/tpac_skeleton_inject.py <new_mesh.tpac> <backup_with_skeleton.tpac> <skel_name> <out.tpac>` → skeleton + meshes in ONE tpac (the proven structure). Do NOT use `tpac_skeleton_extract.py` (a STANDALONE skeleton-only tpac CRASHED the engine — recursive worker-thread native AV; spider 2026-06-14). Do NOT rename the action_set's `skeleton=` to a mesh name; do NOT fully revert (the backup's un-split mesh re-hits #4). RCA: spider 2026-06-13/14 |
| 2 | **The `quad_movement` tag** on a measured-gait clip (Kit recompile loses it) | `Skeleton.TickAnimations` AV (`+0x10`) on the FIRST mission tick, every mount context | byte-scan each `monster_usage_movements`-bound clip's deployed `_anm.tpac` for `quad_movement` | byte-graft the tag from a tagged sibling (spider.md "How-to"), or re-tag in the Kit |
| 3 | **A binding target** (a rename orphans an `animation="X"`) | degenerate record → AV / null when resolved | every `animation=` in `as_<c>` + children + the rider partial must resolve to a real resource in a deployed tpac | re-point the binding to the correct existing clip; never bind a name no tpac carries |
| 4 | ~~**The mesh split**~~ **— RETIRED (corrected 2026-06-13): there is NO per-mesh bone limit** | — | **The only bone cap is `Skeleton.MaxBoneCount = 64` (engine code), a SKELETON-total cap, not per-mesh. Author skeletons ≤63. A single mesh skins the whole skeleton — proven: elephant 59 active bones / chariot 54, both one mesh, both render.** | **Never split a body for bone count.** Keep the whole body in ONE mesh. Split a mesh ONLY for a genuinely separate sub-mesh — e.g. the warg's `warg_low_fur` is split so the FUR can cloth-simulate independently (cloth-driven, not bone-driven). A fully-disjoint full-body `<AdditionalMeshes>` mesh may not render (chariot 2nd horse). See `feedback_no_40_bone_per_mesh_limit` |
| 5 | **HorseHarness on the mount** suppresses the Horse item's `<AdditionalMeshes>` | always-render parts (a chariot's cart) vanish the moment a harness is equipped | native mount-compositing drops Horse-item AdditionalMeshes when the HorseHarness slot is filled (chariot 2026-06-13: barding dropped the cart) | put anything that must ALWAYS render (vehicle/cart/reins) in the **base mesh**, not `<AdditionalMeshes>`; leave only optional geometry (mane) as an AdditionalMesh. See `feedback_custom_mount_harness_rules` |
| 6 | **Custom-skeleton harness** shipped as a standalone FBX | **Kit CRASHES on import** (native, no rgl_log) when the harness skin uses bones outside the stock horse skeleton (`B`-set, cart bones) and Type=horse is picked | the editor binds a standalone `_notused` harness to the built-in horse skeleton, which lacks the custom bones | build the harness mesh INSIDE the creature FBX that defines the skeleton (where `chariot_horse_harness_imperial_b` lives); the HorseHarness item references it by metamesh name. `family_type` on the Horse item + harness must match. See `feedback_custom_mount_harness_rules` |

Two more invariants the swap must preserve: the **skeleton resource NAME must equal the
action_set's `skeleton="…"`** (the Blender armature name becomes the skeleton name; the FBX-export
recipe renames the armature to `<skel>_notused` for *animation* clips so they don't register a 2nd
skeleton — but the *mesh* export must keep the real skeleton name), and **movement clips stay
in-place** (zero net root travel).

### MANDATORY post-deploy gate (run after every tpac/FBX replace, BEFORE battle-testing)

```
python tools/verify_mount_assets.py <spider|elephant>
```

It statically checks all of #1–#3 (skeleton present in a live loose tpac, every measured-gait
clip tagged, no phantom bindings) and exits non-zero on any FAIL. **PASS is necessary but not
sufficient** — it cannot see #4 (mesh bone-palette) or in-game behavior, so a PASS still requires
an **in-game spawn-with-rider check** (the mount must appear *with* its rider, not a lone rider).
Add a new creature to the tool's `CREATURES` config. Skeleton/tag/binding internals:
`tools/tpac_skeleton_scan.py` + `docs/tools/spider-skeleton-tpac-tools.md`. Lesson memory:
`feedback-mesh-reexport-drops-skeleton-resource`.

---

## Phase 0 — Assets (skeleton, meshes, physics)

| Requirement | Rule | Failure mode if violated |
|---|---|---|
| Skeleton bone count | **≤ 63 bones** (engine cap is `Skeleton.MaxBoneCount = 64`; author ≤63 for a 1-bone safety margin) | import failure / corrupt rig |
| ~~Per-mesh bone palette~~ | **NO per-mesh bone limit (corrected 2026-06-13).** A single mesh skins the whole skeleton — elephant 59 active bones / chariot 54, one mesh each, both render. Do NOT split a body for bone count. Split a mesh ONLY for a genuinely separate sub-mesh (the warg splits `warg_low_fur` so the FUR cloth-simulates independently — cloth-driven, not bone-driven). | — (the old ~40 cap was a misdiagnosis; see `feedback_no_40_bone_per_mesh_limit`) |
| Physics | bodies + joints transplanted onto the final skeleton in the same tpac | ragdoll-less corpses, collision holes |
| Movement clips | authored **IN-PLACE** (zero net root travel) — the engine translates the agent | double-speed slide if root motion baked (verified vs warg 2026-06-03) |

## Phase 1 — Animation clips (the Kit, or the byte-patch emergency path)

**THE rule that cost a 6-hour RCA:** every **movement/gait clip** (walks, runs, turns-in-motion,
jump) compiled for a `movement_system="quadrupedal"` action set MUST carry the
**`quad_movement` Clip *usage*** (bottom of the Kit clip panel, below Flags — it is NOT a Flag)
plus **step points**, and gait sound needs the `make_walk_sound` Flag. An untagged gait clip
builds a null native gait structure → AV (`+0x10`) on the FIRST `Skeleton.TickAnimations` in
EVERY mount context: thumbnail, inventory tableau, mission. Detached (non-mount) spawns never
trip it — which is why the bug hides until the creature becomes a mount.

- Run clips serving the gallop pace also need the **`cyclic`** flag (the spider's run regraft).
- Non-gait clips (attacks, deaths, hits) must NOT carry `quad_movement`; their polish flags are
  per-category (attack: `client_prediction, lock_movement, enforce_all`; death:
  `make_bodyfall_sound, client_prediction, do_not_keep_track_of_sound, enforce_all,
  update_bounding_volume`; rear: `lock_movement, enforce_lowerbody`) — full Kit field map in
  spider-skeleton-animation-pipeline.md §3c.
- **Durable path:** set these in the Modding Kit and recompile. **Emergency path:** the
  byte-patch template graft (spider.md "How-to") — structurally validated clean by
  `tools/_scratch`-class verifiers on 2026-06-12, but treat it as a stopgap; recompile when the
  Kit is open anyway.
- tpac filename ≠ resource name. Validate every binding target against the **parsed resource
  inventory** (byte-scan the tpacs), never by name plausibility — a binding to a nonexistent
  animation compiles into a **degenerate record** that AVs when dereferenced (the
  `an_spi_attack_back` phantom, 2026-06-11). Sweep script pattern: scan candidate names against
  the module's tpacs + Alliance.Wargs packs + vanilla `Native/.../animation_clips.tpac`.

## Phase 2 — Monster XML (`Monsters/<file>.xml`, engine-registered via SubModule `<Xmls>`)

Copy the warg's shape (`Alliance.Wargs/ModuleData/Monsters/LOTR/lotr_monster_warg.xml`), then:

| Field | Value | Why (campaign evidence) |
|---|---|---|
| `num_paces` | **6** | every mountable monster is 6; the mount machinery indexes the gallop pace (5) |
| `family_type` | **1** (horse family) | family 1 carries vanilla's complete rider-death / dismount / rider-fall surface. The spider tried 11 — no such surface exists for it. Harness isolation is done in C# (mount-lock), not via family |
| `<Flags>` | **EXACTLY** `Mountable CanRear RunsAwayWhenHit CanCharge CanWander` | **`CanAttack` is forbidden on a mountable monster** — it activates the engine's own attack-AI (`Agent_ai::set_attack_entity`), a code path NO working mount takes, and the site of the 1.4.6 charge-CTD (null `agent+0xAD8`, Event-Log-proven 2026-06-12). The BT bite/trample does NOT need it — the warg proves it. No other extras: no baseline mount declares any |
| Rein surface | `rein_handle_bone` + `rein_handle_left/right_local_pos` + `rein_collision_1/2_bone` | every working mount declares one (warg-minimal is fine) |
| Rider adders | `rider_eye_height_adder`, `rider_body_capsule_height_adder`, `rider_body_capsule_forward_adder` | warg + vanilla horse parity (camera + rider capsule) |
| `rider_sit_bone` | a spine/chest bone | seats the rider; `rider_camera_height_adder` tunes camera |
| Slope block | `front/back_bone_to_detect_ground_slope_index` + `bones_to_modify_on_sloping_ground_*` | quadruped slope pitch (indices verified vs decompiled `MonsterExtensions`) |
| `sound_and_collision_info_class` | an existing class (`horse`) or author your own collision_infos (warg does) | footstep sounds/particles only — cosmetic |

## Phase 3 — action_types (typed verb surface)

The native dispatch is **TYPE-driven**. Declare, exactly:

| Action | Type | Notes |
|---|---|---|
| 12 falls: `act_<c>_fall_{right,left,roll,backwards,slow_right,slow_left}` + `_continue` siblings | `actt_fall` | the blow handler chains `<fall>_continue` on downed mounts; missing/untyped = `HandleBlowAux` AV |
| `act_<c>_rear`, `act_<c>_rear_damaged` | `actt_rear` | fires when the mount takes damage |
| `act_<c>_dash` | `actt_dash` | |
| `act_<c>_kick` | `actt_kick` | |
| `act_<c>_quick_stop`, `act_<c>_quick_stop_when_fast` | `actt_mount_quick_stop` | wire BOTH usage-set slots to these (the spider had an untyped idle in `quick_stop_action` — same lookup-miss class as the jump bug) |
| `act_<c>_hit_object`, `act_<c>_hit_object_while_falling` | `actt_hit_object` | |
| `act_<c>_strike_front`, `act_<c>_strike_back` | `actt_mount_strike` | the HEAVY trample rows |
| `act_<c>_strike_front_while_moving`, `act_<c>_strike_back_while_moving` | **UNTYPED** | the LIGHT (`is_heavy=False`) strike rows — warg-exact shape |
| `act_<c>_idle_1` | `actt_idle` | the upper-body pace-1/front row's dedicated typed idle (warg's `act_warg_idle_1`) |
| **`act_<c>_jump`** (the usage-set `jump_start_action`) | **`actt_dash` — NEVER `actt_jump`** | warg AND elephant both type it dash. A jump-TYPED action absent from the jump ROWS misses `jump_actions_map_` — tolerated on 1.4.5, **AV on 1.4.6** |
| movement/idle/attack/hit/death/taunt actions | untyped | bindings + BT use them |

## Phase 4 — action_sets

1. **The creature's set** (`as_<c>`, `skeleton=`, `movement_system="quadrupedal"`): bind EVERY
   action the usage set references (verbs, falls, strikes incl. while-moving, jumps via the
   global `act_horse_jump_*` family, movements, idles incl. `_1`, turns) to a **real, validated**
   clip. Also bind `act_horse_forward_canter` explicitly — the thumbnail/tableau pose resolves it
   (`as_warg` does the same).
2. **Children**: `as_<c>_town_and_village` + `as_<c>_map` (`base_set="as_<c>"`) — campaign map
   icons + settlement scenes resolve them; missing child = native AV (elephant "Crash #4" class).
3. **The rider partial**: a partial `<action_set id="as_human_warrior">` block mapping every
   creature usage-set action to a RIDER overlay clip (`rider_warg_*` + vanilla `rider_fall_*`).
   **It MUST sit at the TOP of the file** — `base_set` inheritance snapshots at definition time,
   so the partial must precede every race set or riders inherit nothing (the goblin
   "thrust-loop", 2026-06-11). The engine merges same-id sets across modules
   (Alliance.Wargs' own file leads with exactly such a partial).
4. **Registration truth:** native animation XML loads ONLY via `project.mbproj` `<file>` entries
   with the standard ids (`soln_action_sets` / `soln_action_types` / `soln_monster_usage_sets`)
   → the module-root `action_sets.xml` / `action_types.xml` / `monster_usage_sets.xml` are live;
   subfolder copies are superseded decoys. SubModule.xml `<Xmls>` is managed types only.

## Phase 5 — monster_usage_sets (the lookup tables the engine CRASHES on)

> **The 1.4.6 hardening principle, learned three times in one day: the engine's usage-record
> lookups are hash maps whose miss path dereferences the end-sentinel (asserts compiled out of
> shipping builds). A MISSING key crashes; an EXTRA row is inert. Make every table TOTAL over
> the key combinations the engine can produce.** 1.4.5 tolerated misses; 1.4.6 does not.

The set element: all **10 verb attributes** wired to the Phase-3 typed actions
(`rear`, `rear_damaged`, `dash`, `kick`, `fast_quick_stop` → `_when_fast`, `quick_stop`,
`gallop_acceleration_head` (untyped idle is fine — horse/elephant parity), `hit_object`,
`hit_object_falling`, `jump_start` → the `actt_dash`-typed jump). A verb-less set on a Mountable
monster leaves null native entries → spawn AV.

| Table | Coverage rule | Evidence |
|---|---|---|
| `monster_usage_movements` | every pace 0..5 × directions front/right/left/none, `turn_direction` rows at pace 1, **both `is_left_foot` variants at pace 5**; EVERY pace needs its `direction="none" turn_direction="none"` reference row | missing per-pace reference = native ÷0 at CreateAgent (2026-06-04) |
| `monster_usage_upper_body_movements` | warg shape + pace-1 front/back/none rows; pace-1/front uses the typed `_idle_1` | horse/elephant parity |
| `monster_usage_movement_adders` | the global `act_horse_rider_*_adder` rows (they animate the RIDER) — full pace × direction matrix incl. left-foot pace 5 | rider bob/sway |
| `monster_usage_jumps` | **ALL NINE directions** (`front`, `front_left`, `front_right`, `none`, `left`, `right`, `back`, `back_left`, `back_right`) × (start + loop×is_hard + end×is_hard) = **45 rows**. Vanilla/warg ship only front+none — and vanilla riders never produce directional jump queries, but BT-driven creatures turning mid-jump DO | the 1.4.6 riverbank crash: `monster_usage.cpp` sentinel deref (offset `0x634396`), spider caught mid `act_horse_jump_high_loop` with corrupted record memory |
| `monster_usage_falls` | warg-exact 9 rows: heavy front roll; light × (right/left/front/back) × death_type roll/other — on the TYPED fall codes | knockdown machinery |
| `monster_usage_strikes` | warg-exact 4 rows: heavy front/back on TYPED `actt_mount_strike` codes; light front/back on the UNTYPED `_while_moving` codes | trample dispatch |

## Phase 6 — Item + troop + recruitment

- Horse-slot ITEM (`ItemType.Horse`) with `HorseComponent monster="<monster_id>"`; keep the body in
  ONE mesh (no bone-count split — see Phase-4 row 4); `<AdditionalMeshes>` only for genuinely separate
  sub-meshes (accessories, or a cloth-sim mesh like the warg's fur); `horse_harness` optional (family-1
  fits horse harnesses — the player-side mount-lock prevents abuse, Phase 7).
- Rider TROOP equips the item in the Horse slot → vanilla cavalry spawn does everything else.
- Recruitment via the standard pools (`VolunteerRecruitmentService`) or clan-restricted
  (`harad_elephant_rider` precedent).

## Phase 7 — C# (the TAOM layer)

| Piece | Pattern | Notes |
|---|---|---|
| `<C>MissionBehavior : MissionLogic` | elephant wiring verbatim: `BTRegister.RegisterClass` in lazy `Initialize()`, first-tick scan + `OnAgentBuild` late-attach keyed on **`Monster.StringId`** (NEVER character id — the mount agent's Character is the RIDER), dedup shadow list, dead pruning, error-dedup logging | custom-battle deployment spawns AFTER the first tick — late-attach is the main path |
| `<C>BehaviorTree` | elephant shell (has-rider → ai-controlled → attack gate → task + SleepTask pacing; player-ridden and riderless branches sleep) + `On<C>Died` listener (warg parity); `base(10)` ctor (NOT a throttle — int division truncates <1000 to 0) | the BT layers attacks ON TOP of engine mount AI; it needs no Monster flags |
| Attack service | pure, TaleWorlds-free (`ShouldEngage` / cooldowns / damage), boundary nodes hold the raw `Agent` | warg-pattern rider damage attribution |
| Eager `ActionIndexCache` + `AnyUnresolved()` drift guard | resolve attack clips at mission start; log if any → `act_none` | Armory rename = silent `act_none` = the "slide" |
| Mount-lock | `TaomAgentStatCalculateModel`: `CanAgentRideMount=false` + `MountDifficulty=999` for the monster id | players can't steal the mount; the assigned rider's cavalry spawn ignores it |
| **Patch47 `Agent_Die_SpiderDismount_Patch`** | Prefix on `Agent.Die`: a rider dying on the creature is hard-dismounted via cached private `SetMountAgent(null)` → dies the proven on-foot death; a dying creature frees its rider first | the 1.4.6 native mounted-death path still AVs on (at least melee) deaths — Die-path read of float-bits-as-index from a corrupted action record (debugger-proven 2026-06-12). Patch47 routes around it and was wrongly indicted once (the charge crash fired without it too); it is REQUIRED |
| `SpawnMountLogged` (HeroRace) | instrumented replica of the engine's private `SpawnMount` with graceful mount-skip | keep; demote to LogDebug at ship |

## Phase 8 — Validation instruments (run them ALL before battle-testing)

| Instrument | What it catches |
|---|---|
| `python tools/audit_mount_parity.py` | ANY divergence from warg/elephant/horse across Monster XML, usage sets, action types, action-set binding coverage, rider partial — the tool that found 5 of the spider's 7 deltas in one pass. **Extend its `FILES`/`MOUNTS` maps for each new creature** |
| Animation-target sweep | every `animation=` in your sets byte-scanned against the real tpac inventories (module + Alliance.Wargs packs + vanilla `animation_clips.tpac`) — phantoms compile into degenerate records |
| The probe battery (`RunSpiderMountDiagnostics` pattern) | one-shot in-game probes: skeleton × set × usage spawn tests, engine-binding name resolution (compiled truth vs XML-on-disk), rider-partial inheritance per race set |
| Control battles | warg-only (the known-good baseline), then the new creature; on a crash, bisect single-variable — and expect ≥2 independent causes (the spider had FIVE) |
| Native crash triage | Event Log fault offsets (no debugger needed) discriminate crash sites across runs; VS mixed-mode (Managed .NET Framework + Native) gives faulting registers; pdata function bounds + rip-relative string scan + E8 caller-chain naming — the technique that named `set_attack_entity` and `monster_usage.cpp` |

## The engine-bump protocol (what 1.4.5 → 1.4.6 taught)

Steam force-bumped the engine mid-campaign (2026-06-11 17:39) and three latent data quirks became
CTDs because the rewritten native lookups stopped tolerating misses. After ANY engine bump:
run `/verify-bindings`, re-run the parity audit + control battles, and check the Event Log fault
offsets against the previous version's known sites before assuming your last change caused a
crash. Keep the previous decompile as `_shipping_build_vX.Y.Z` (the 1.4.5 baseline lives at
`E:\Decompiled_Bannerlord\_shipping_build_v1.4.5`) — managed diffs scope the blast radius fast
(1.4.6: combat assembly byte-identical; all changes native-internal).

## The complete gotcha index (chronological, both campaigns)

1. Non-humanoid mounts ARE viable — the "never make creatures mounts" verdict was reversed
   2026-06-11 (`quad_movement` was the real killer, not the mount architecture).
2. ~~Per-mesh bone palette → split meshes~~ — **RETIRED: no per-mesh bone limit (corrected 06-13). Keep the body in one mesh; the only cap is the 64-bone skeleton. See `feedback_no_40_bone_per_mesh_limit`.**
3. Per-pace `direction="none"` reference row or ÷0 (spider 06-04).
4. `quad_movement` + step points on gait clips or universal mount-context AV (THE root cause,
   06-10/11).
5. Run clips on the gallop pace need `cyclic` (mid-charge CTD).
6. Typed falls WITH `_continue` siblings + typed verbs or blow-path AVs (06-11).
7. Binding targets must exist — phantom = degenerate record = delayed AV (06-11).
8. Rider partial at the TOP of the file or race sets inherit nothing (06-11).
9. `family_type=1` for the vanilla rider surface (06-11).
10. NO `CanAttack` on mountable monsters — engine attack-AI path, 1.4.6 CTD (06-12).
11. `jump_start_action` typed `actt_dash`, never `actt_jump` (06-12).
12. Jump tables TOTAL over all 9 directions — BT creatures jump sideways (06-12).
13. Quick-stop slots wired to the typed codes, not an idle (06-12).
14. Light strikes on dedicated UNTYPED `_while_moving` codes (06-12).
15. Patch47 dismount-before-death stays — the 1.4.6 native mounted-death path still corrupts
    (06-12, melee-thrust repro).
16. A missing lookup key CRASHES on 1.4.6; an extra row is inert. When in doubt, add rows.
17. **A mesh/geo re-export can ship MESH-ONLY, silently dropping the `<creature>_skeleton`
    resource → `CreateAgentSkeleton` null → RIDERLESS mount (no crash, easy to miss).** After any
    asset swap, the skeleton must live in a live (non-`.backup`) loose tpac like the working
    elephant; run `tools/verify_mount_assets.py <creature>`. **Fix by re-BUNDLING the skeleton into
    the new mesh tpac with `tools/tpac_skeleton_inject.py` — NOT a standalone skeleton tpac.** The
    standalone approach (`tpac_skeleton_extract.py`, since deprecated) CRASHED the engine with a
    recursive worker-thread native AV (spider 2026-06-14): a standalone skeleton tpac is an unproven
    structure, and every shipping creature bundles its skeleton WITH the mesh. (spider rework
    2026-06-13/14). See the "Replacing FBX/TPAC files" section at the top.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-animation-blender-mcp-workflow.md](./creature-animation-blender-mcp-workflow.md)
- [docs/features/elephant.md](../features/elephant.md)
- [docs/features/spider.md](../features/spider.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
