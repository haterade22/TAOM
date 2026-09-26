# Lessons — Animation & Skeleton

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Animation & Skeleton lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### There is NO ~40 per-mesh/per-draw bone limit — never split a creature body for bone count
The "~40 per-draw / per-mesh bone palette" cited across `spider-skeleton-animation-pipeline.md`, the engine-reference docs, the creature-mount authoring guide, and CLAUDE.md is **FALSE** (disproven during the chariot port, issue #279, 2026-06-13). The only bone cap is the engine's `Skeleton.MaxBoneCount = 64` — a cap on TOTAL bones per *skeleton*, NOT per mesh. **Author skeletons ≤63** for a safety margin. Two in-game proofs: the war elephant renders as ONE mesh skinned to **59 active bones** (of a 60-bone skeleton; measured in Blender), and the chariot renders as ONE mesh skinned to **54 active bones** (both horses). A single mesh skins the whole skeleton; keep a creature's whole body in one mesh. Split a mesh ONLY for a genuinely separate sub-mesh — e.g. the warg's `warg_low_fur` is split so the FUR can cloth-simulate independently (cloth-driven, **not** bone-driven; the old "split is bone-driven, not just cloth" claim was exactly backwards). A fully-disjoint full-body `<AdditionalMeshes>` mesh may not render at all (the chariot's 2nd horse as an additional mesh did not — fixed by merging to one mesh).
- **Why missed:** the spider's 2026-06-05 single-mesh `PreloadForRendering` AV was *attributed* to a ~40 cap and that number propagated into 9 docs and became the justification for splitting the chariot's two horses (which then broke). The spider RCA's own evidence already refuted it — "Refuted — AV persisted with the split mesh" — i.e. the split never fixed the spider AV. The spider render-AV's true cause remains unestablished (and is distinct from the spider's separate `TickAnimations` AV, which was the missing `quad_movement` tag).
- **Prevent:** default to ONE body mesh; never split for bone count. The diagnostic that nailed it: an Option-E base/additional swap in the item XML (no Blender) showed the missing mesh follows the *additional slot*, then the elephant's active-bone measurement killed the ~40 theory.
- **Source:** memory/feedback_no_40_bone_per_mesh_limit.md + `docs/features/chariot.md` + `docs/features/spider-skeleton-animation-pipeline.md` (corrected "Mesh-split" section)

### Fix custom-race CC parent visuals in LOTRLOME_Armory's `action_sets.xml`, not TAOM's repo
When custom-race CC parents (Erebor dwarves, Mordor uruks, Mirkwood elves, Gundabad orcs) render sideways / T-posed / invisible on Bannerlord 1.3.x, the fix is in `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` — NOT TAOM's repo. LOTRLOME was authored against 1.2.x CC parent action types (`act_character_creation_male_default_0..6` / `_female_default_0..6`); 1.3 renamed them (`_default_standing`, `_side_to_side_1`, `_mother_front`, `_father_sitting`, `_side_to_side_2`, `_side_to_side_3`, `_hugging`), so 1.3's CC lookup finds no animation for a custom race and the agent spawns but cannot pose. Add the seven 1.3-style alias actions beside the existing `_default_0..6` block (same animation file, new action-type name; female mirror uses `anim_mother_0..6`). The race-sync prefix on `CharacterCreationNarrativeStageView.RefreshAgentVisuals` (in `Patch20_NarrativeHorseGuard`) is ALSO required — it updates `NarrativeMenuCharacter.Race` from 0=human to the current `CharacterObject.PlayerCharacter.Race` so the lookup resolves to `as_<race>_facegen` instead of `as_human_facegen`; without it the aliases are never reached.
- **Why missed:** TAOM's `Main/_Module/ModuleData/action_sets.xml`/`monsters.xml`/`Races/*` are dead duplicates (LOTRLOME provides the loaded versions; removed 2026-05-04) — easy to edit the wrong (inert) copy. ~3 hours were wasted chasing JSON `_urban` mismatches, Spider IoC interference, equipment rosters, missing spawn points, and dwarf-skeleton incompatibility before the action_set was identified.
- **Prevent:** For invisible / broken CC visuals on 1.3 with LOTRLOME-derived custom races, ALWAYS check that `act_character_creation_*_default_standing` exists in the relevant `as_<race>_facegen` action_set in LOTRLOME_Armory FIRST. Keep the per-race facegen checklist in sync with `docs/reference/lotrlome-armory-snapshot/README.md`.
- **Source:** memory/feedback_lotrlome_action_set_aliases.md + `docs/features/character-creation.md`, `docs/reference/lotrlome-armory-snapshot/README.md`

### Adding a new race-bearing culture requires CREATING missing `as_<race>_facegen` sets, not just patching existing ones
Patching existing facegen action_sets with 1.3 aliases only fixes races LOTRLOME already had a set for. The 2026-05-04 fix patched 12 sets (berserker, cave_troll, dg_uruk, dwarf, goblin, hill_troll, nazghul, orc, pale_uruk, saruman, uruk, uruk_hai) but authored NO `as_elf_facegen` — LOTRLOME (a 1.2-era armor mod) never had playable elves. The bug surfaced 18 days later: Mirkwood/Rivendell parents rendered as a horizontally-stretched / contorted mesh because the race-sync prefix told the engine to look up `as_elf_facegen`, the lookup returned nothing, and the silent fallback didn't bind to the human skeleton elves use. The two-step recipe: (1) patch existing `as_<race>_facegen` with 1.3 aliases; (2) CREATE missing `as_<race>_facegen` + `_female_facegen` for any race a TAOM culture/troop uses that LOTRLOME never anticipated. For `monster_usage="human"` races (elf), `base_set="as_human_warrior"` works because they share the human skeleton + `anim_father_0..6`/`anim_mother_0..6`.
- **Why missed:** The 2026-05-04 README said "12 facegen sets (dwarf, dwarf_female, orc, orc_female, … etc.)" — the trailing `etc.` named neither a present nor an absent race, hiding the elf hole for 18 days.
- **Prevent:** Never trust `etc.` when documenting completeness of a fix — list every required `as_<race>_facegen` ID explicitly. When adding a race (`hobbit`, `man_of_the_west`, etc.) without a matching `_facegen` set, repeat step 2 AND update both the snapshot README and the memory.
- **Source:** memory/feedback_lotrlome_action_set_aliases.md (Addendum 2026-05-22) + `docs/reference/lotrlome-armory-snapshot/README.md`

### A new `as_<race>_facegen` must declare the full ~100-action surface verbatim from `as_dwarf_facegen` — slim entries break post-parent CC stages
A slim `as_elf_facegen` declaring only the 14 CC parent action types (`base_set="as_human_warrior"`) fixes the parent menu but the Early Childhood stage and every later CC stage still shows the child lying down / T-posed. Bannerlord 1.3's facegen lookup does NOT fall through `base_set` inheritance for post-parent action types (`act_childhood_*`, `act_character_creation_toddler_*`, `act_inventory_idle*`, `act_stand_*`, `act_sit_*`, `act_rider_story_background_*`, `act_horse_story_background_*`) — it returns null and falls to engine default (lying-down / T-pose). `as_human_warrior` is a combat set, not a facegen set, so it declares none of these. LOTRLOME's `as_dwarf_facegen` works because it declares all ~100 action types DIRECTLY (action_sets.xml lines 16812-17134, ~322 lines), not by inheritance. Recipe: copy `as_dwarf_facegen` (~322 lines) + `as_dwarf_female_facegen` (~97 lines) verbatim; rename `id` and set `base_set` to whatever the race's monster references in monsters.xml; insert before `</action_sets>`; mirror into `docs/reference/lotrlome-armory-snapshot/action_sets.xml`. Full required surface = 14 CC parent actions (1.2 + 1.3 names) + 7 toddler + ~60 `act_childhood_*` + 8 `act_childhood_toddler_*` + inventory/banner/stand/sit poses + 12 rider/horse story-background actions. All dwarf-block anim refs are skeleton-flexible — no re-targeting needed even for non-human-skeleton races.
- **Why missed:** The 14 CC parent action types ARE the only ones consulted at the parent menu, so slim entries genuinely fix that stage — the mistake was assuming `base_set` inheritance covered the next stages.
- **Prevent:** Read the existing working code (`as_dwarf_facegen`) before deciding "minimum viable" — the dwarf block IS the minimum viable form. Test in-game at EVERY CC stage, not just the parent menu; parent-menu success and Early-Childhood failure are separate symptoms of the same root cause.
- **Source:** memory/feedback_lotrlome_action_set_aliases.md (Addendum v2, 2026-05-22) + `docs/reference/lotrlome-armory-snapshot/README.md`

### Diagnose a broken CC pose via "all races vs one race" — all-races breakage is a vanilla engine bug, not an action_set issue
A third CC pose bug surfaced at the Starting Age menu: age 30 ("at your prime") rendered the player horizontally-stretched / lying-down across orc/dwarf/uruk/elf/human (ages 20/40/50 worked). Root cause was vanilla — `CharacterCreationCampaignBehavior.AgeSelectionAdultOptionOnSelect` hard-codes `SetAnimationId("act_childhood_athlete")` at age 30; the other age handlers use `_focus`/`_sharp`/`_tough`. The `as_<race>_facegen` blocks are bit-for-bit identical across races for this action type and it is registered in `Native/ModuleData/action_types.xml` — the fault is a v1.3.15 runtime `anim_childhood_athlete ↔ human_skeleton` binding regression, not LOTRLOME data. Fix shape: a TAOM Harmony Postfix on the vanilla method that re-applies a working anim_id to the `player_*` character (keeping vanilla's ChangeAge/SetEquipment/SetBirthDay/bonuses), appended to an existing patch file in the same feature folder under the sibling `[HarmonyPatchCategory(...)]`. The "all races vs one race" key: one-race-breaks = LOTRLOME data (author/expand the facegen); all-races-break-at-the-same-stage = vanilla engine/code (Harmony Postfix override; data edits would mask it at one site and break it elsewhere); some-races-work = data, but the bug is in WHICH races have the entry.
- **Why missed:** The user proposed "controlled by action set ids youth/adult" as the mechanism — plausible but false (no such IDs exist in LOTRLOME or Native). Two cheap grep cycles refuted it in under a minute and redirected the investigation to the decompiled vanilla method.
- **Prevent:** Before assuming a fix shape, ask the user to test the broken stage on two unrelated races (e.g. dwarf + uruk) — saves a wasted investigation round on the wrong layer.
- **Source:** memory/feedback_lotrlome_action_set_aliases.md (Addendum v3, 2026-05-22)

### Kit-compiled creature MOVEMENT clips MUST carry the `quad_movement` tag + step points or a quadrupedal action set AVs at `+0x10`
Every gait clip (walk/run/strafe/turn) needs the `quad_movement` tag plus `make_walk_sound` + step points in its `_anm.tpac`. An untagged movement clip compiles fine and even plays on a detached agent — then a `movement_system="quadrupedal"` action set measuring it builds a NULL native gait structure, and the first `Skeleton.TickAnimations` / `GetWalkSpeedLimitOfMountable` dereferences it → AccessViolation at `+0x10` in EVERY mount context (thumbnail, inventory tableau, mission deployment). Attack/hit/death clips correctly do NOT carry the tag (ADOD's don't either). In the Modding Kit, `quad_movement` is a CLIP USAGE (collapsed "Clip usages" section under Flags), NOT a checkbox Flag; `make_walk_sound` IS a Flag. A related degenerate-record crash: a binding whose animation TARGET doesn't exist (`act_spider_strike_back -> an_spi_attack_back`, where the clip name was invented by plausibility) compiles into the same degenerate record and AVs on blow/death dereference.
- **Why missed:** Invisible to compile-time checks and to detached-spawn testing — it detonates only on first quadruped engagement. Secondary fingerprint: resolving an UNBOUND action through the poisoned set returns a runtime-synthesized garbage name (`1002467048434979358_0`-style) from `GetAnimationName`.
- **Prevent:** Set `quad_movement` + step points on every gait clip BEFORE saving in the Kit; interim fix without the Kit is a byte-patch onto a working ADOD `_anm` template (recipe in `docs/features/spider.md` "How-to", originals at `*.bak-untagged`) or the parse-based grafter `_chariot_refine\patch_quad_movement.py`. Validate every `animation=` target against the actual `_anm` resource inventory (internal names parsed from tpacs — filename ≠ resource name), NEVER by name pattern. Debug via the instrumented `SpawnMountLogged` replica + one-shot probe battery (action set × usage set cross-pairings on fresh entities; a caught AV doesn't poison later spawns) + byte-diff a working ADOD `_anm`.
- **Source:** memory/feedback_quad_movement_tag_required_for_gait_clips.md + `docs/features/spider-skeleton-animation-pipeline.md` §3c, `docs/features/spider.md`

### A creature's CustomAttack bone-collision must use ITS OWN strike bones + a size-scaled radius — warg placeholders make the bite play but never land
The CustomAttack bite (warg/spider/elephant bone-collision path) only lands when an indexed bone passes within `boneCollisionRadius` of a target bone during the attack window. Two silent failures both make the creature "not kill anything" at high damage: (1) Placeholder bone indices — copying the warg's set (`{23, 37, 43}`) onto a different skeleton hits different (usually rear/internal) bones nowhere near where the new creature strikes; (2) Radius too small — the warg uses ~1.0m on a 10-bone front cone because "a few bones + a small radius can't form a detection volume" (its own code comment), so a 0.3-0.4m sphere on 1-3 bones almost never catches a moving target. For a giant spider the strike bones are the front legs (`joint40-44_r/l` = indices 14-18 / 19-23, outer leg thigh→tip); a biter would use jaw/fang bones (`joint5_r/l`, mouth `joint12_m`). Use outer/striking segments + several bones + a size-scaled radius (warg 1.0m, ~2× giant spider 1.8m), and bump `TargetDetectionRange` so reachable targets aren't pre-filtered.
- **Why missed:** Misattributed as a damage problem. The tell is in the `[Creature][diag]` log: ATTACK-fire count ≫ HIT count (spider 2026-06-15: 75 attacks, 2 hits ≈ 3% connect) — a huge gap is a bone/radius problem, damage only matters once hits land.
- **Prevent:** Get the real bone index↔name map from the tpac (the engine's ground truth, no Blender) via `python tools/tpac_skeleton_dump.py <…_geo.tpac> <skeleton_name>` (index = bone array order; root=0). Pick the creature's actual strike bones from the Monster XML bone map, not a copied set.
- **Source:** memory/feedback_creature_bite_collision_real_bones_not_placeholders.md + `docs/features/spider.md` → "Damage + bite-collision tuning (2026-06-15)"

### For a humanoid RACE, retarget the Bannerlord human animation library onto an Auto-Rig Pro rig — don't hand-author
A humanoid race needing a full animation set gets it by retargeting the Bannerlord human library onto an Auto-Rig Pro (ARP) rig with ARP Remap (proven `human_skeleton` → troll, Blender 5.1.2, ARP 3.78.10, `bl_ext.user_default.auto_rig_pro`, 167 ops: a real human walk retargets with 72 moving fcurves, clean). This is the RACE counterpart to the creature-MOUNT workflow (warg/spider/elephant = hand-authored quadruped gaits with `quad_movement`); a humanoid race is bipedal, `monster_usage="human"`, standalone `as_*_warrior`, NO `quad_movement`, no mount machinery. A *working* troll can ship immediately with zero authoring by reusing `human_skeleton` + `base_set="as_human_warrior"` (the `cave_troll` model); the bespoke-skeleton retarget is the quality refinement. Driver: `tools/blender/arp_retarget.py` (`import_source_fbx` → `retarget`/`retarget_clip`); saved map `tools/blender/bannerlord_human_to_troll.bmap`. ARP's `build_bones_list` MIS-MAPS (spine→empty, spine2→`c_spine_02.x`, `l_foretwist`→`c_foot_ik.l`, fingers→`c_spine_01.x`, pelvis→`c_root_master.x`); `HUMAN_TO_TROLL_FK` corrects them (FK controllers; pelvis→`c_root.x` `set_as_root`; twists/fingers excluded).
- **Why missed:** RCA 2026-06-14 — a flat or crashing retarget is almost always one of four fixes: (1) DROP unmapped `bones_map_v2` entries, don't blank them (`it.name=''` → ARP makes a tweak bone for an empty target → `AttributeError 'NoneType' has no attribute 'name'`); (2) assign the source action's `action_slot` (Blender ≥4.4 slotted actions; without it the source evaluates to REST → a 198-fcurve `_remap` entirely FLAT); (3) `temp_override` with window/area/region ONLY, never pin `active_object` (ARP switches active object internally; pinning edits the WRONG armature → `eb=None` crash + a "Toggle Pose Mode" error that aborts the unbind); (4) clear a stuck `target_rig["arp_retarget_bound"]` flag before binding (an aborted unbind leaves it `True` → "Already bound" → re-bakes a static pose).
- **Prevent:** Verify motion with `action_motion_count()` against a known-good `_remap` control. "72 fcurves" is a COUNT, not a visual check — ALWAYS screenshot the rebuilt pose to confirm a standing/grounded figure (pre-fix the rebuilt clips were collapsed). ARP is GPL-2.0 + a paid Blender Market addon → NEVER commit ARP source (install zip at `E:\LOTRAOMAssets\Auto-Rig Pro v3.78.10\auto_rig_pro.zip`).
- **Source:** memory/feedback_arp_retargeting_humanoid_race.md (RCA 2026-06-14) + project-troll-race-arp-inflight

### Source human clips at the TPAC DATA layer (no assimp) — the assimp FBX-export path AVs headless; ARP GE export works headless
The assimp FBX-export path (concrete `FbxExporter`, used by `tools/extract_human_anims_tpac.ps1`) AVs (`0xC0000005`) headless on BOTH pwsh-7/.NET-10 and WinPS-5.1/.NET-Framework — a few single exports work, then it crashes deterministically. The human `Skeleton` (`human_skeleton`, GUID dd7f3586) lives in `Native/EmAssetPackages/human/human.tpac` (NOT skeletons.tpac); clips in `AssetPackages/animations.tpac` (569MB; all Native tpacs share one package GUID → load human.tpac eager + add animations.tpac as resolver). Bypass assimp at the TPAC data layer (PROVEN 2026-06-14): `tools/read_anim_keyframes_tpac.ps1` reads a SkeletalAnimation's keyframes + skeleton bone order/rest → JSON (TpacTool.Lib, never crashes); `tools/blender/rebuild_anim_from_json.py` rebuilds the clip on the human armature by walking the hierarchy with the JSON local transforms. CALIBRATION: the animated rotation quaternions need NO axis swap/conjugate, BUT the bone REST matrix MUST be `.transposed()` — Bannerlord `RestFrame` is DirectX ROW-major (offset in M41-M43); without the transpose every bone offset reads 0 and the skeleton COLLAPSES onto the root (RCA 2026-06-14). ARP GE FBX export WORKS HEADLESS (corrects the earlier "UI-only" claim, which was the same `active_object`-pin bug): `ge_export()` → `bpy.ops.arp.arp_export_fbx_panel(quick_export=True, check_existing=False)` under `_ovv()` (window/area/region only). Settings: rig-type UNIVERSAL, engine OTHERS, axes Y/X, `arp_ge_force_rest_pose_export` + detach action. Emits deform-only skeleton (30 bones, NO `c_*`/`_ik` — game skeletons have no IK; IK is authoring-only, baked away on export).
- **Prevent:** A MESH (`body_male_a`, `Mesh.Material`/`SecondMaterial` cleared to a dummy `AssetDependence<Material>`) is MANDATORY in the FBX export or Blender imports static empties; skip `Duration==0` clips. TpacTool is FBX-export-only (no importer) so FBX→tpac compile stays Kit-only. The only true hand-offs left are FBX→`.tpac` Kit-compile + in-game test.
- **Source:** memory/feedback_arp_retargeting_humanoid_race.md (RCA 2026-06-14)

### On Blender 5.1.2 `Action.fcurves` is GONE — author via `pose_bone.keyframe_insert`, and wrap FBX import/export in a VIEW_3D `temp_override`
The Blender 5.1.2 slotted-action API removed the legacy fcurve path: `Action.fcurves` no longer exists — fcurves now live at `action.layers[].strips[].channelbags[].fcurves`. The easiest authoring path is `pose_bone.keyframe_insert(...)`, which auto-creates the slot/layer/channelbag. FBX import AND export fail on an internal `mode_set` call unless wrapped in a VIEW_3D context override (`bpy.context.temp_override(window, area=<VIEW_3D area>, region)`). Verified Kit-ready export recipe that round-trips cleanly: armature-only, `primary_bone_axis='Y'`, `axis_forward='-Y'`, `axis_up='Z'`, `add_leaf_bones=False`, `bake_anim=True`, the NLA strip/take = the bare scene/clip name, and the armature root renamed `<skel>_notused`.
- **Prevent:** Do NOT reach for `Action.fcurves` (gone); if you must read fcurves, walk `action.layers[].strips[].channelbags[].fcurves`.
- **Source:** memory/feedback_blender_512_slotted_action_api.md

### Verify the connected Blender's open file + scene BEFORE any save_as/import/mutation over Blender MCP
Before any `save_as_mainfile` / `import_scene.*` / scene mutation over Blender MCP, verify the connected Blender's open file and scene contents (`bpy.data.filepath` + `get_objects_summary` — assert the expected armatures/meshes) match your intent. The MCP connection drops and reconnects across a long session and can return attached to a DIFFERENT Blender instance or open file. On 2026-06-13 the connected Blender silently switched from the troll to the elephant scene between turns; a `save_as` then captured the elephant scene under a `troll_anim_WORK` filename and imported the human source into the wrong scene (the user had two Blender instances open; the MCP was on the elephant one). No source files were harmed because save-as used a new name, but it wasted steps.
- **Prevent:** At the start of any Blender-mutation sequence, read `bpy.data.filepath` + list the scene's armatures/meshes and confirm the intended target (e.g. troll `rig` + `troll_hill_body_a`, NOT `elephant_skeleton`/`elephant_mesh`) BEFORE save/import. If the user mentions multiple Blender windows, confirm which instance the MCP is on. Always work in a saved WORK copy (`save_as <thing>_anim_WORK_<date>.blend`) so the source `.blend` stays pristine. Pairs with the fork-discipline "don't fabricate state — verify it" rule.
- **Source:** memory/feedback_blender_mcp_verify_scene_before_mutate.md

### When a Blender/Kit rework breaks a previously-WORKING creature mount, RESTORE the whole-creature backup folder FIRST — don't surgically rebuild
When a creature mount that WORKED breaks after a Blender/Kit asset rework, the FIRST move is to restore the user's whole-creature backup `E:\LOTRAOMAssets\_tpac_backup_<YYYYMMDD>\<creature>\` (the GOLD copy mirroring live `Modules/LOTRLOME_Armory/Assets/creature/<creature>/`), NOT to rebuild the skeleton/clips/physics. The user takes a full-folder tpac backup whenever a creature works, exactly so a bad rework can be reverted. The 2026-06-14 spider session spent ~a full day rebuilding what already existed working — transplanting physics (`tpac_skeleton_inject`/`transplant`), regenerating `_anm` clips, and chasing a 4-deep native-AV ladder: launch (raw-skeleton parse AV, RVA 0x91397) → thumbnail (TickAnimations gait AV, handled by the existing `[HandleProcessCorruptedStateExceptions]` guard — a first-chance break, not fatal) → battle-spawn (`Agent.BuildAux` → native `sound_and_collision_info` build AV, RVA 0x490E02, fatal). The fix was ONE file copy: restore `spider_correct_geo.tpac` from `_tpac_backup_20260613\spider\` (the proven 6/11 bundle) over the broken 6/13-Kit-rebuild bundle — the broken battle crash was the 6/13 Kit-rebuild MESH, not the skeleton. `_tpac_backup_20260613` holds both `spider\` and `elephant\`; the elephant will need the same restore-not-rebuild treatment.
- **Why missed:** Session-made backups (`_spider_rebuild_backup_<date>\`, `*.bak-kitbroken`/`*.bak-meshonly`/`*.transplanted`) capture BROKEN/intermediate states — never restore from those for a working creature.
- **Prevent:** Identify the WORKING bundle before restoring via `tpac_skeleton_transplant.py <tpac> <skel> --dry-run` + `seg_guids`: working skeleton = `Usage='horse'` + per-bone bodies + N-1 D6 constraints + a DISTINCT `owner_guid` (6/10 working spider = `a9ec7d87…` vs raw 5/1 source `5857baa9…`; raw `Usage='other'`/0 constraints launch-crashes; mesh-only re-export → riderless). `Get-FileHash` backup vs live to confirm they DIFFER before copying. Native-crash-triage gotcha: `native_crash_triage.py --ip <IP> --base <BASE>` needs IP + base from the SAME run — ASLR re-bases every launch (the IP can fall below a relaunched base); get both from the one paused process (Debug→Modules for the base, the exception's `_ip`).
- **Source:** memory/feedback_creature_rework_restore_from_backup_first.md + project-spider-skeleton-animation-pipeline

### A creature mesh re-export can silently SHIP MESH-ONLY, dropping the Skeleton resource → riderless mount (graceful, no crash, easy to miss)
After re-exporting a creature's mesh/skeleton geo tpac (a Kit recompile following a Blender rework), the Skeleton resource can be silently dropped — the new geo tpac ships MESH-ONLY. The action_set still declares `skeleton="<name>"`, which now resolves to nothing → `CreateAgentSkeleton` returns null → the rider spawns with NO mount (graceful degrade, NO crash), so it's easy to miss. Spider 2026-06-13: the rework's `spider_correct_geo.tpac` had `sk_spider_forest_c`/`_c_2` meshes but no `spider_skeleton` (it survived only in the `.backup`). Fix WITHOUT losing the rework: re-BUNDLE, do NOT make a standalone — `tools/tpac_skeleton_inject.py <new_mesh.tpac> <backup_with_skel.tpac> <skel_name> <out.tpac>` injects the skeleton INTO the new mesh tpac (the proven structure every working creature uses; matches the elephant's bundled `adod_elephant_geo.tpac`). Do NOT rename the action_set's `skeleton=` to a mesh name (manufactures the null); do NOT fully revert the geo (the backup's un-split mesh reintroduces the bone-palette `PreloadForRendering` AV). The DEPRECATED standalone approach (`tpac_skeleton_extract.py`) CRASHED the engine (spider 2026-06-14: recursive worker-thread native AV reading null, `…AE001397` in `TaleWorlds.Native.dll`) — WORSE than riderless: it reused the skeleton's `item_guid` as its `package_guid` (every working tpac has a DISTINCT package guid) AND no shipping creature uses a standalone skeleton tpac.
- **Why missed:** Degrades gracefully (riderless goblin, not a CTD); quad_movement/parity/phantom-binding audits all pass (those surfaces were fine); and a stale baked `AssetPackages/pack*.tpac` may STILL contain the old skeleton, misleading a static byte-scan into "it's present" (the engine loads LOOSE Assets in the dev setup — the baked pack is NOT the runtime source).
- **Prevent:** After ANY creature mesh re-export, scan the LIVE loose Assets tree with `python tools/tpac_skeleton_scan.py <tpac>` for the creature's Skeleton resource (TYPE_GUID `d5a335c6...`) — it MUST be in a live (non-`.backup`) loose tpac, exactly like a known-working control (elephant's `elephant_skeleton` in live loose `mesh/adod_elephant_geo.tpac`). Symmetry with a known-working creature is the decisive test. When restoring a dropped resource, match the PROVEN packaging of a working sibling (skeleton bundled with mesh), don't invent a novel container shape just because it's "cleaner."
- **Source:** memory/feedback_mesh_reexport_drops_skeleton_resource.md + project-spider-skeleton-animation-pipeline

### A mounted rider is the 28-bone `human_skeleton`, not the creature skeleton — verify fit composite in Blender via mesh + sit-bone world-matrix screenshot
A mounted rider is NOT the creature skeleton — it is the standard 28-bone `human_skeleton`. Mounted poses are authored as human clips and bound in action set `as_human_warrior` under `act_<mount>_*` names mapping to `rider_<mount>_*` clips; at runtime the rider is parented to the mount's `rider_sit_bone`. The spider REUSES the warg `rider_warg_*` clips (from `Alliance.Wargs`) rather than authoring its own. Rider sit-bones per mount: warg `Spine1_M`, spider `chest_m`, elephant ` Spine1_05`, horse `horsespine2`. Composite-verification technique (proven): in Blender set `rider.matrix_world = Matrix.Translation(mount.matrix_world @ pose_bone[sit_bone].matrix.translation)` then take a VIEWPORT screenshot (armatures do NOT render in Workbench — use the screenshot path, not a render). Mesh the human skeleton via `orc_rider.fbx` to see an actual orc on the 28 bones and judge fit (e.g. the goblin straddling a narrow warg back on the broad spider).
- **Prevent:** A POC spider-rider idle was made; per-clip rider authoring still needs art-direction + in-game verification (NOT done as of 2026-06-13).
- **Source:** memory/feedback_rider_animation_on_mount_composite_verify.md + project-spider-skeleton-animation-pipeline

### v1.4.6+ native usage/AI lookups CRASH on a missed key — make creature data tables TOTAL; parity-audit BEFORE battle-testing
Bannerlord 1.4.6 rewrote native usage/AI lookups so a MISSED KEY is an AV (shipping builds compile out the asserts; the `unordered_map`-style miss path dereferences the end-sentinel or returns garbage that flows into pointer/index slots). 1.4.5 tolerated the same misses, which is why latent data quirks become CTDs only after an engine bump. Three spider sites in one day (2026-06-12): `Agent_ai::set_attack_entity` (CanAttack flag), `monster_usage.cpp` jump map (directional jump query), native `Die` path (corrupted record → float bits as index). The engine's tables are hash maps keyed on runtime state tuples (direction, pace, jump_state, is_hard…); vanilla data only covers the keys vanilla AI produces, but TAOM's BT-driven creatures produce combinations vanilla riders never do (turning mid-jump, strafe-heavy gaits) and the parser accepts MORE key values (9 directions) than any vanilla file uses. Rules: (1) a missing key crashes, an extra row is inert → make every table TOTAL over the parser's key vocabulary (jump tables: all 9 directions × all states = 45 rows, not vanilla's 10); (2) NEVER declare `CanAttack` on a Mountable monster (engine attack-AI path no mount takes); (3) `jump_start_action` is typed `actt_dash`, NEVER `actt_jump` (warg + elephant precedent).
- **Why missed:** Per-crash fixing is the slow path — 5 crash-driven iterations were finding deltas one at a time that a single parity-audit pass found all at once.
- **Prevent:** Run `tools/audit_mount_parity.py` BEFORE battle-testing any creature change (the scripted spider-vs-warg/elephant/horse diff found 5 deltas in one pass; parity-audit-first was the user's call and was right; warg = known-good baseline). After ANY engine bump: Event-Log fault offsets discriminate crash sites across runs without a debugger; keep the previous decompile as `_shipping_build_vX.Y.Z` for managed diffing; re-run `/verify-bindings` + the parity audit + control battles.
- **Source:** memory/feedback_engine_lookup_total_key_coverage.md + `docs/ai-includes/creature-mount-authoring.md` (16-gotcha index), project-spider-skeleton-animation-pipeline

### A standalone race `action_set` (no `base_set`) silently accrues missing action types across an engine bump → CTD on first use
A standalone race action set — one with its own `skeleton=` and NO `base_set`, e.g. LOTRLOME's `as_dwarf_warrior` (`skeleton="dwarf_skeleton_a"`) — inherits nothing, so every action type the engine gains after the set was authored is simply absent. `as_dwarf_warrior` was seeded from **Native 1.3** `as_human_warrior` types (`tools/Generate-ActionSets.ps1` iterates Native's action nodes); by Native **1.4.6** it was missing **423 active types** — water (`act_dive_*`/`act_swim_*`/`act_death_swim_*`), the 32 `act_stagger_*` hit-reactions, flail/dagger/sling/backstab stances, War-Sails naval. A player falling into water requested `act_dive_idle_unarmed`, the set didn't contain it, and the engine CTD'd. The other LOTR races are immune: orc/uruk/goblin are empty stubs with `base_set="as_human_warrior"`, and LOTRLOME's own `as_human_warrior` is a 48-line PARTIAL that field-merges into Native's full set (which carries `act_dive_*`), so they inherit it — only the standalone dwarf set has no merge partner. Fix = explicit full parity via `tools/patch_dwarf_action_parity.py` (adds every missing active Native type; text-splice that leaves the rest of the file byte-identical; comment-safe; idempotent), NOT a `base_set` — the engine has lookup paths that don't traverse it (the facegen lessons above).
- **Why missed:** A standalone set compiles and plays fine for years; the gap detonates only when the engine first requests a never-before-used action (water entry), and an engine bump silently widens it. No compile-time or load-time check covers it. Also: a raw `grep`/awk type diff over-counts (it includes Native's commented-out `<!-- ... -->` actions, ~126 of them) — parse with an XML reader, which ignores comments, to get the true *active* gap (425 raw → 423 real).
- **Prevent:** After every engine bump, run **`python tools/audit_action_set_parity.py`** (wired into `/engine-bump` Phase 4) — it resolves EVERY set's effective surface (own + full `base_set` chain + cross-module merge) and exits non-zero listing any HUMANOID set short of Native's `as_human_warrior`; fix each with `patch_dwarf_action_parity.py --set-id <id> --apply`. **Bound the blast radius by ENUMERATING, not from memory** — the audit found all 1110 humanoid sets complete after the dwarf fix (dwarf was the only gap). The LIVE file has only 5 standalone sets (the `as_human_warrior` merge-partial, `as_dwarf_warrior`, creature mounts spider/elephant/chariot); trolls (`as_cave_troll_warrior`/`as_hill_troll_warrior`) use `base_set="as_human_warrior"` and inherit dive, so are NOT at risk — a "trolls are next" claim written from memory was corrected by the deep-review's enumeration (RCA `docs/reviews/rca-dwarf-action-parity-2026-06-25.md`).
- **Source:** `tools/patch_dwarf_action_parity.py`, `tools/audit_action_set_parity.py`, `docs/reference/lotrlome-armory-snapshot/README.md`, CHANGELOG 2026-06-25

### The all-races/one-race discriminator has a third branch: all races + INTERMITTENT + only-on-other-machines = build/dependency, not data or engine code

- **Symptom:** every race rendered prone in every UI tableau, on users' machines only, varying
  per launch. The existing discriminator in this file ("one-race-breaks = LOTRLOME data;
  all-races-break = vanilla engine/code") correctly pointed away from race data — but its
  all-races branch says *engine/code*, and the real cause was neither: users were running a current
  `TAOM.dll` against a stale `TAOM.Dependencies.dll`, which carries HarmonyLib itself, so the
  preview patches never applied.
- **Why missed:** the symptom is identical to the documented data failure (bind pose = engine
  default when no animation resolves), so the whole first phase of the investigation went into
  `as_<race>_facegen` coverage, action-set parity and the Armory snapshot. All of it came back
  clean — both audits pass, and the release ships the same race data as the dev machine.
- **Prevent:** before auditing race data for a prone/T-pose report, establish two facts that cost
  minutes: **does it reproduce on the dev machine** (if not, suspect the shipped artifacts, not the
  content), and **is it deterministic per launch** (intermittent rules out static data outright —
  missing XML entries break identically every boot). Only then open `action_sets.xml`.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`

### An engine type with an EXPLICIT static cctor bakes its whole table on first touch — touch it too early and every value is a sentinel for the rest of the process

`TaleWorlds.MountAndBlade.ActionIndexCache` holds 215 `static readonly` action indices (v1.4.7) filled by an **explicit** static constructor calling `MBAnimation.GetActionCodeWithName`. An explicit cctor means the type is **not** `beforefieldinit`, so ANY static member access — a field read *or* the `Create()` method — forces the entire table to initialise at that instant. Touch it before the engine has loaded action types and all 215 bake to `-1` permanently: the fields are `readonly`, so the cctor never re-runs. Vanilla `CharacterTableau.GetIdleAction()` returns `ActionIndexCache.act_inventory_idle_start`, `SetAction(-1)` is a no-op, and every character in every UI tableau renders in bind pose. Presents as **all races, intermittent per launch, never on the dev machine** — because it is a load-order race, not a data defect.
- **Why missed:** the symptom is identical to the documented missing-facegen data bug, so the investigation spent a day on action-set coverage (both audits pass, release data matches dev). Nothing in a decompiled type's *appearance* distinguishes an explicit cctor from `beforefieldinit`; you have to look for the `static Type()` block and reason about what its dependencies are.
- **Prevent:** before reading any engine static in early lifecycle code, check whether its declaring type has an explicit cctor and what that cctor depends on. Gate the first touch behind a probe on a **different** type (here `MBAnimation`, a struct with no cctor) — never on the type you are protecting. Repair is possible but must never guess: resolve the value live and **round-trip verify** it (`Create(name).GetName() == name`) before writing, because a wrong index written into a vanilla static is a silent corruption strictly worse than the `-1` it replaces. Do not assume field name == lookup key: v1.4.7 has exactly one divergence (`act_raid_jump = Create("act_raid_jump_1")`), found only by diffing all 214 `Create()` call sites against their target fields.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md` (addendum), `Main/Features/HeroRace/ActionIndexCacheRepair.cs`

### A repair must be wired to the code that READS the corrupted value, not to the code you happen to own

The first cut of the `ActionIndexCache` repair retried from `CharacterSpawnerService.InitWithCharacter` — a TAOM-owned path that resolves its actions with live `Create()` calls and therefore **never reads the poisoned statics at all**, and which `CharacterSpawner_InitWithCharacter_Patch` skips entirely for race 0. The consumers that do read them (`CharacterTableau.GetIdleAction`, `BasicCharacterTableau`) had no backstop, so on a machine where the early attempt deferred, the fault would persist for the whole session — including for human characters, the case players actually reported.
- **Why missed:** "call it from our own service" is the reflex, and that service was already instrumented so it felt like the natural host. The reviewer question that catches it is not *"does the repair run?"* but *"which code reads the broken value, and is the repair upstream of that read on every path?"*
- **Prevent:** for any repair/patch of shared engine state, enumerate the READERS first (decompile them), then place the fix upstream of each. Prefer a **prefix on the reading method** — it runs before the body, so the same invocation consumes the corrected value and already-constructed state self-corrects on its next refresh. Check explicitly whether your own patch gates out any case (here `race <= 0`) that the readers do not.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md` addendum F2

### A VALID action set is not a posed character — check the clip, not the handle

- **Symptom:** every diagnostic written for the prone-tableau bug stopped at `MBActionSet.IsValid`,
  and every one passed. But `CharacterTableau.GetIdleAction()` poses the doll with
  `act_inventory_idle_start`, and TAOM's Patch2 injects `as_<race>_warrior` — which for uruk is a
  **zero-action stub** inheriting via `base_set="as_human_warrior"`. This file already records that
  the engine does NOT fall through `base_set` for `act_inventory_*`. A set can therefore resolve
  valid and still bind no clip, `SetAction` becomes a no-op, and the skeleton stays in bind pose.
- **Why missed:** `IsValid` only means the set index is ≥ 0. It says nothing about whether the
  requested action has an animation in that set, which is the thing that actually moves the
  skeleton.
- **Prevent:** when diagnosing a pose failure, resolve the animation with
  `MBActionSet.GetAnimationName(in ActionIndexCache)` and treat an empty result as the failure — an
  action *index* existing globally (e.g. `idleStartIdx=4008`) is unrelated to that set binding a
  clip to it. Note also that `CharacterTableau` / `CharacterSpawner` / `BodyGeneratorView` live in
  `Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`, which is **absent
  from the `E:\Decompiled_Bannerlord` dump** — decompile that DLL directly with `ilspycmd`.
- **Source:** `docs/reviews/rca-prone-character-tableau-2026-07-31.md`

### A character's meshes live on the SKELETON, not on the entity's MetaMesh components

`AgentVisuals` attaches skin and armour through `_data.AgentVisuals.GetSkeleton()`, so
`GameEntity.MultiMeshComponentCount` / `GetMetaMesh(i)` return **0 / nothing** for a character
tableau or agent. To enumerate what a character actually draws, use
`entity.Skeleton.GetAllMeshes()` (`IEnumerable<Mesh>`), then `Mesh.GetMaterial()` →
`Material.Name` / `GetShader().Name` / `GetShaderFlags()` / `GetTextureWithSlot(0).Name`, plus
`Mesh.Color` / `Color2` (which is where `AddTeamColorToMesh` writes).

- **Why missed:** the entity-component API is the obvious one and compiles fine; it simply returns
  an empty set here. A render census built on it reported `metaMeshCount=0` for **every** character —
  including `main_hero` and a troop known to render correctly — and that only surfaced in-game.
- **Prevent:** always include a **known-good control** in any census or dump, and treat "the control
  returned nothing" as an instrument fault rather than a finding. Zero for everything is never data.
- **Note:** `MBTextureType` is not visible from `Main`'s reference set; use
  `Material.GetTextureWithSlot(0)` (slot 0 == `DiffuseMap`) instead of `GetTexture(MBTextureType.DiffuseMap)`.
- **Source:** #389 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

### Verify an engine action's TYPE and its other drivers, not just that it resolves

`ActionIndexCache` + `AnyUnresolved()` answer "is this name real". They cannot answer "what does the
engine DO when this action is active" or "who else fires it". Before binding any action to a behaviour
tree, establish three things: its `action_types.xml` **type**; whether the creature's `monster_usage`
set names it in a **verb slot or table** (which means the engine fires it too); and whether the engine
**branches on that type** anywhere (`ActionCodeType`, `AgentActionFlag`, `IsInBeingStruckAction`).

- **Why missed:** the war ram's attack clip was chosen for how it reads and verified only for
  existence. It was got wrong TWICE, the second time while fixing the first, which is the reason this
  is a rule and not a note.
  1. `act_horse_rear` is typed `actt_rear` (`ActionCodeType.Rear = 47`). The inherited `horse` usage set
     declares `rear_action="act_horse_rear"`, so the engine fires it on every damaged mount, and
     `Agent.Mount` refuses a mount whose channel-0 type is `Rear`. The mount would go unmountable
     mid-fight.
  2. The replacement `act_horse_strike_front` plays the horse's hit reactions (`horse_hit_from_front`/
     `_back`): the creature flinches as though hit while you emit damage. **Corrected 2026-09-18:** its
     type `actt_mount_strike` (`ActionCodeType.MountStrike = 52`) is NOT read as being struck;
     `Agent.IsInBeingStruckAction` tests `MBMath.IsBetween(type, 48, 52)`, which is half-open, so it reads 48 to 51 (`StrikeLight` .. `StrikeKnockBack`) as being struck and NOT `MountStrike` (52).
- **The fact underneath both:** **the vanilla horse rig's only attack clip is the kick.** They deal damage by
  charge collision, so `monster_usage_strikes` is the mount's hit-REACTION table, not an attack table.
  The horse rig's only genuinely offensive action is `act_horse_kick` (`actt_kick`,
  `ActionCodeType.Kick = 28`). If you need a creature on the horse rig to attack with anything other
  than a kick, the clip does not exist and must be authored.
- **Prevent:** this risk is specific to a mount that **inherits a vanilla `monster_usage`**. A creature
  with a bespoke usage set owns its whole `act_<creature>_*` vocabulary, so nothing else fires it; a
  reskin SHARES the vocabulary with the engine, and "our code never fires this" stops implying "nothing
  fires this". When reviewing a reskin, grep the inherited usage set for every action the feature binds
  and treat any hit as engine-driven. Check the candidate's `ActionCodeType` against the engine's
  classification bands before binding it. Collapse spare profile slots onto the creature's own attack
  rather than parking them on unrelated real actions, or you silently widen an "am I busy" check.
- **Generalises:** a reskin inherits the donor's BEHAVIOUR, not just its animations. The property that
  makes it cheap is the same one that couples it.
- **Source:** #515 / `docs/reviews/rca-war-ram-2026-08-28.md`

### A loose `Assets/` definition and a cooked `AssetPackages/` entry must not both claim one asset name with a reachable source

Absorbing the warg from `Alliance.Wargs` on 2026-08-28 produced a startup crash that took three
attempts to shape correctly. The mechanism generalises to every creature absorbed from another module.

The warg tpacs were imported and cooked in an editor module called `Alliance.Editor`, which exists in
no install, and that name is baked into each tpac as the asset's **source** path. In game this shows
as a modal `RGL WARNING: Unable to locate source file .../Warg_skin_n.png of texture Warg_skin_n to
compile`.

The tempting fix is to make the pointer resolve: `Alliance.Editor` and `LOTRLOME_Armory` are both
exactly 15 characters, so an in-place byte substitution preserves every offset in the container, and
the 111 real source files can simply be copied in. Doing that **crashes the game on startup**:

```
rglAsset_package_item_texture validate_rdc : Warg_skin_d
Compiled image Warg_skin_d(B8G8R8->DXT1)(2048x2048->2048x2048)
rglAsset_manager::signal_package_item_change - Warg_skin_d
Assertion Failed!  rglIntrusive_ptr.h:151  Expression: px != nullptr
```

A loose asset definition whose source is **missing** is harmless: the engine warns and moves on. Make
that source **reachable** and the warning becomes a real compile, of a texture the cooked pack has
already registered under the same name. The package-item swap mid-startup then dereferences null.

Dangling is safe. Cooked-only is safe. Both, resolvable, crashes. The elephant never hit this because
its loose stubs carry a bare texture name (`t_creature_elephant_a1_d`) with no `$BASE/Modules/...`
source path, so nothing can trigger the recompile; the warg stubs are the same 466 to 553 bytes but
carry a full path, which is the whole difference.

**What to do instead.** Keep the cooked pack, keep `AssetSources/` for re-baking in the Kit, and do
not ship a loose `Assets/` tree for that creature. Note the two-sided cost of removing it: `Assets/`
is what the Modding Kit asset browser reads, so the creature disappears from the editor. That is
acceptable when the sources are present to re-bake from, and it is not acceptable to leave the loose
tpacs in place just to keep the browser populated.

When re-baking, do not leave newly cooked loose tpacs beside the old pack: that reproduces the same
duplicate-registration crash. Cook into the pack, or move the old pack aside for the bake.

Full record: [lotrlome-warg-changes.md](../../reference/lotrlome-warg-changes.md) section 10.


### A tpac item's checksum goes stale the moment you add a value, so guid substitution is safe and setting an empty field is not

> **CORRECTED 2026-08-31, and the correction matters more than the original.** The claim below that
> the item checksum is what defeated the Owner Skeleton patch was never demonstrated, and two things
> found since explain those failures without it. First, the Owner Skeleton guid sits at metadata
> offset **21**, not 20; the patch script was writing one byte early, into the wrong field. Second,
> two later attempts that rebuilt a whole rig tpac were blamed on the checksum and were actually
> caused by the writer zeroing the file header's TOC-size field (see the next lesson). Guid-for-guid
> substitution remains proven safe: 170 references, then another 33, both surviving reload. Whether
> the checksum is validated at all is now **unknown**, not established. Treat the rule below as
> "prefer the Kit for content changes", not as a measured fact about checksums.


Absorbing the warg on 2026-08-28 needed two binary repairs to its copied animation assets. One
worked, one silently did nothing, and the difference is a field it is easy to walk straight past.

A tpac item is laid out as `type_guid | item_guid | version | name | meta_size | metadata |
**checksum(8)** | segments | dependencies`. A skeleton animation's metadata is
`int32(1) | GUID_A(16) | GUID_B(16) | trailer(13)`, where GUID_B is the Owner Skeleton.

**Substituting one guid for another is safe.** Repointing 170 references from a donor module's asset
ids to the re-imported ones worked, held across a Kit reload, and fixed 65 clip bindings. The item is
otherwise unchanged, so its existing checksum stays consistent with its content.

**Writing a value into a field that was zero is not.** Setting the Owner Skeleton is a real content
change, so the checksum must be recomputed. Skipping it does not error: the Kit reads the item,
finds the checksum inconsistent, and shows the field as still unset. The maintainer had to set all 48
by hand. Proof, from diffing a hand-set file against the patched one:

```
rider_warg_dash_geo.tpac   11565 -> 11565 bytes   checksum 1347615467139852255 -> -3110447808552502235
```

Same size, different checksum. The algorithm is not known here, so this class of edit belongs in the
Kit, full stop.

**The verification failure is the reusable part.** The patch was checked by re-reading the bytes that
had just been written, which only proves the write happened. It says nothing about whether the
consumer will accept them. For a binary format with any integrity field, verification means a
round trip through the real consumer, or a diff against an artefact that consumer produced.

**What automation can still do** is decide *which* value belongs where, which is the part a human gets
wrong. The 56 source FBX split 34 to `skeleton_warg` and 22 to `human_skeleton`, derived from the
bones present in each file and confirmed independently by the action sets. That caught
`Warg_AnimRider_Idle.fbx`, which is rigged to the human skeleton despite its `Warg_` prefix and would
have been mis-assigned by anyone going off filenames.

Related trap from the same absorption: a `_geo`'s two items (the `.fbx` and the skeleton animation
`<rig>_notused|<clip>`) **are not in a stable order**. The Kit writes the `.fbx` first on import and
the skeleton animation first after a save. Keying a guid map on item 0 therefore mapped the wrong one
and broke 17 correctly-wired clips. Match items by name. And once a pass has written a wrong value,
restore from backup and redo rather than patching the patch: the second pass has nothing to match
because the value it needed is already gone.

Full record: [lotrlome-warg-changes.md](../../reference/lotrlome-warg-changes.md) section 11.
Tool: `tools/remap_creature_asset_guids.py`.


### An FBX re-import restores only the materials the FBX carries, and the editor-assigned ones vanish silently

Absorbing the warg into `LOTRLOME_Armory` on 2026-08-28 ended with a creature that had every XML row,
every material asset, every animation clip and all its Owner Skeletons correct, and that still did
not appear in battle or on the campaign map. It also had a second symptom which read as unrelated:
the three colour variants (`warg_brown`, `warg_dark`, `warg_albino`) all rendered brown.

One defect caused both. **The re-imported rig bound 3 materials where the donor bound 7.**
`Warg_Rig_V5.fbx` carries exactly three material slots (`warg_skin`, `warg_fur`,
`orc_rider_saddle`) for five meshes. The other four (`warg_fur_lod`, `warg_fur_2`, `warg_fur_3`,
`warg_fur_3_lod`) had been assigned by hand in the donor's editor and existed only inside its
compiled tpac. The Kit imported what the FBX held, which is all it can do, and reported nothing
wrong because from its point of view nothing was.

```
                              AFTER RE-IMPORT      DONOR
warg_low_fur               -> warg_fur x4          warg_fur x4 + warg_fur_lod x4
warg_low_fur_with_saddle   -> warg_fur x4          warg_fur x4 + warg_fur_lod x4
warg_low_fur_with_saddle_2 -> warg_fur x4          warg_fur x4 + warg_fur_2 x4
warg_low_fur_with_saddle_3 -> warg_fur x4          warg_fur x4 + warg_fur_3 + warg_fur_3_lod x3
orc_rider_saddle           -> byte-identical to the donor
```

`orc_rider_saddle` coming back byte-identical is the control: the import is faithful, so what
diverged is exactly the part the FBX never carried.

**Why the two symptoms look unrelated and are not.** The colour of a warg variant lives in its fur
mesh's own material, not in the item XML, so every variant bound to plain `warg_fur` is brown. And
the missing bindings appear four times each, one per LOD level, which fits a creature that renders
in a close-up UI preview and is absent in the world where it is drawn at a lower LOD. **A missing
material binding does not present as a missing material. It presents as the wrong colour, or as
nothing at all.**

**It cannot be repaired outside the Kit**, for the reason already recorded above for the Owner
Skeleton. Adding a binding changes the item's metadata length (1,317 bytes against 1,349 on the dark
variant), so there is no slot to overwrite: it is an insert, not a guid substitution, the 8-byte
checksum goes stale, and the edit is discarded without complaint.

**The detection method, since no tool in this repo checks it.** Index every item guid in both trees,
then for each mesh item scan its byte range for 16-byte sequences that resolve to a material item,
and compare the sets per mesh. `validate_mesh_refs.py` does not do this, and neither does
`verify_mount_assets.py`.

**The debugging lesson is separate and worth more.** Before this was found, the cause was confidently
attributed to the warg never having been cooked into `EmAssetPackages`, resting on a correlation
across six creatures: four cooked ones rendered, two uncooked ones did not. The failing side had a
sample of two, and one of them, the war ram, had never actually been checked. It renders. **Do not
build a cause on the half of a correlation you have not verified**, especially when that half is
what makes the pattern look clean.

Full record: [lotrlome-warg-changes.md](../../reference/lotrlome-warg-changes.md) section 12.
Checklist row: [creature-mount-authoring.md](../../ai-includes/creature-mount-authoring.md) #7.


### A tpac's header carries the TOC size, and a rebuild that zeroes it makes the engine read the table of contents as vertex data

Restoring a creature's skeleton physics data needed the rig tpac re-serialised. Two attempts did
that and both ended with the engine asserting during asset load:

```
Loading packages $BASE/Modules/LOTRLOME_Armory/Assets...
Assertion Failed!
C:\BuildAgent\work\mb3\TaleWorlds.Shared\Source\Base\FairyTale.Library\rglBuffer.cpp:899
Expression: (rglMath::nearly_equals(vector->w, 1.0f)) && "Potential read/write miss match for rglVec3"
```

The first attempt was blamed on a rewritten item guid, the second on the item checksum. Both were
wrong. The defect was in `tools/tpac_skeleton_inject.py`, in one expression:

```python
header = struct.pack('<II', MAGIC, 2) + pkg + struct.pack('<III', len(out_items), 0, 0)
```

`parse_items` reads magic, version, package guid and item count, then **skips the 8 bytes at offset
28..35**, and the writer hardcoded them to zero. Those bytes are the **TOC size**, and the engine
derives `data_start = 36 + toc_size` from them. Measured on 250 shipped tpacs across Native,
SandBox, LOTRLOME_Armory and Alliance.Wargs: `header_tail == sum(len(item.toc))` in 250 of 250, and
the first segment's data offset is always exactly `36 + tail`. Of 6,107 tpacs on disk, every one is
version 2 and exactly one has a zero there, a legitimate 0-item file.

With the field zeroed the engine believes the data section starts at offset 36, which is where the
TOC begins. It then reads guids and length-prefixed name strings as `rglVec3`, and the `w` component
is not 1.0. The assert is the engine noticing precisely that.

**The test that would have caught it in seconds, and is now the gate before any tpac surgery:**

```
parse the file, re-serialise it with NO modifications, compare byte for byte with the original
```

Run against the warg rig it differed in exactly two bytes, at offsets 28 and 29, because
`12102 = 0x2F46` fits in two. Everything else round-tripped perfectly: item TOCs, the 69-byte
segment descriptors, the `UnknownDependences` block, blob order, blob bytes, total length. After the
fix the identity rebuild is byte-identical on the warg rig, the donor rig, the war ram and the
chariot.

**The reusable rule.** Before trusting a binary rewriter, make it reproduce its input exactly. A
dry run that prints plausible numbers proves the script ran, not that the format survived. Any field
the parser skips is a field the writer will invent, and the fields a parser skips are exactly the
ones nobody has understood yet.

**A second lesson, about attribution.** Both crashes were reported as caused by the change, and the
first was not: the editor asserted at 20:05 on pristine files and the first patch landed at 20:14.
The signal split by launcher mode rather than by file contents, three editor-mode runs asserting and
three full-game runs of the same assets loading cleanly, one of them running through to MapScreen.
**Establish that a failure signal is absent before the change before treating its presence after the
change as proof.** Check the log timestamp against the edit timestamp.

**Confirmed in game 2026-08-31, against the analysis.** Restoring the skeleton's bodies and
constraints made the creature render on the campaign map. A twelve-agent audit had refuted that
hypothesis using three controls: a vanilla `usage='other'` mount that renders, vanilla map-icon
skeletons with zero bodies and zero joints that render, and the fact that `BoneBodyPartType` has no
managed rendering consumer. All three were sound-looking and none generalised. **A refutation by
analogy to a different asset is weaker than one cheap empirical test.** When the test is a game
launch and the analysis is a dozen agents, run the test first.

Tools: `tools/tpac_skeleton_swap.py` (transplant a Skeleton item from a known-good compile),
`tools/tpac_skeleton_dump.py` (bones, bodies, constraints).
Record: [lotrlome-warg-changes.md](../../reference/lotrlome-warg-changes.md) section 13.


---

### The Kit stores an FBX's bone locals verbatim and yaws the root 180 degrees; author on the engine's frames

**2026-09-17/18, cave troll clips.** A Fab creature's 52 clips retargeted onto `human_skeleton` looked right in
Blender and folded the troll at every joint in the Kit. Instead of trying export settings, the compiled
master was read back with TpacTool and compared bone by bone with the FBX and with the engine rest frames.
Import 1: every child bone's stored rotation equalled the FBX's local rotation to 0.0 degrees, and the
FBX rig (TpacTool's `FixBoneForBlender` export) had frames 90 to 180 degrees off the engine's. Import 2,
on a rig built from the engine's own rest frames: children engine-exact, pelvis 176 degrees off. Import 3,
with the armature node turned 180 degrees to cancel that: pelvis right, character facing away, because
the node transform rides in as well (the package carries a `Geometry <file>.fbx` item). Import 4, node at
identity and the character turned 180 degrees in the keyed pose: correct.

**Addendum (same day, Artem).** The Kit stores the root position track relative to frame 0, and every vanilla
master opens on a rest frame (run: 0.6 deg from rest, root (0,0,0)) with its clip starting at `Source1 = 1`.
A clip opening on a posed frame loses its pelvis offset: the hunched troll stood 9 cm too high, legs posed for
a lower hip, feet skating. The exporter now keys frame 0 as rest (root turned like the clip) and poses from
frame 1, and the export check asserts frame 0 reads 0 deg. Vanilla walk/run carry one root track (pelvis bob,
no travel) and no pelvis position track; the UE pack had two (root travel + pelvis), and dropping the root's
travel reproduces vanilla's layout. The reimport that followed measured two Kit facts: a reimport keeps the
master's GUID (51 of 52), so GUID-linked clips survive and only `Source2` follows the new Duration; and a package
the Kit had also given a junk Skeleton item came back with no animation ("assigned skeleton animation not
found" on the clip). `gen_troll_anim_clips.ps1 -Verify` is the gate after every reimport.


**Rule.** The rig you author on must have the engine's bone frames (`matrix_local` == accumulated
`RestFrame`, transposed), the armature node stays at identity, and a 180 degree world-Z turn goes into
the pose. `tools/blender/retarget_mannequin_to_human.py --engine-skeleton` does all three and asserts the
frames. The two facts are the answer to the war ram's open question in
`docs/reference/bannerlord-skeleton-authoring.md`: reconstruction + `primary_bone_axis='X'` mangled because
X re-expresses the frames the reconstruction had made exact.

**Why the Blender preview cannot catch it.** Skinning is roll-independent, rotations are not: a mesh bound
to wrong frames deforms perfectly. The check that does catch it without the Kit is to re-import the
exported FBX and measure each bone's local rotation against the engine rest local (the tool reports it):
vanilla masters sit at 0 to 12 degrees at their bind frame, the folded export averaged 75 with 13 bones
over 60, the correct one 28 with three (bent thighs, forward head on a horizontal neck, the wrist).

### The Kit stores bone tracks in FBX node order and the engine reads the skeleton's list order

**Context (2026-09-18, the war ram).** The troll procedure, run on `horse_skeleton`, stood the ram on its head
with its hind legs in the air while the pelvis and spine were right. Read-back of the compiled master against
two vanilla horse masters (`anim_horse_stand_4`, `horse_dash_forward`) showed the cause: the Kit writes a
master's bone tracks in FBX node order (Blender exports depth-first) and never remaps them by name; the engine
reads slot i as bone i of the skeleton's own list; `horse_skeleton` lists the rear legs and tail before the
neck. Slots 16 to 31 were scrambled: the neck played the tail, the hind legs the neck. `human_skeleton`'s
list IS depth-first, which is why the troll measurement said "verbatim" and saw nothing.

**Why it was invisible for a month.** TaleWorlds' own horse FBX hangs `horseneck1` off `horsetail3`; the
August work read that as a rig error and "fixed" it, which moved the neck's tracks into other bones' slots.
The one hierarchy that looked wrong was the one that kept the order right.

**Rule.** A measurement made on one skeleton proves the rule for that skeleton's coincidences too. On EVERY
new skeleton, read the compiled master back and check slot i against file-order bone i at frame 0 before
trusting the viewer. When the list order is not a depth-first walk of the hierarchy, export from a hierarchy
that makes it one, node locals kept engine-parent-relative (`transfer_clip_to_engine_rig.py`,
`order_parents`). And keep the FBX take name stable: it is the master's name, and a reimport keeps the
master's GUID only while the name matches (a Blender `.001` collision created `act_war_ram_butt.001`, a new
GUID, an empty skeleton and an orphaned clip).

### Re-skinning a mesh: the Kit keeps 4 influences, and three QA setups that measured nothing

**2026-09-18, LOME cave troll set.** The shipping skins had 115 un-normalised and 9 over-4-influence vertices
on the body and 1,836 un-normalised on the head. The Kit keeps the 4 strongest influences and renormalises,
so Blender's preview of such a mesh was never what the engine played. `tools/blender/reskin_to_human_skeleton.py`
transfers TaleWorlds' weights from the vanilla body parts (nearest surface), limits to 4, normalises, smooths
once, and applies role rules found by measurement: helmet rigid head, body-armour gorget on spine2 (not the
neck), the head's eye and mouth parts rigid head, bracers kept (already clean). Result at TaleWorlds' bar:
body shoulder p99 stretch 1.73 (donor 1.52, old 2.40), head neck 1.12 (donor 1.24, old 2.92).

**Three QA setups produced numbers that meant nothing, in order:** (1) a re-imported clip FBX as the QA rig:
Blender rebuilds that armature's REST from a posed frame, so every bind to it is garbage (the Kit never
reads FBX rests, which is why the same file plays fine there); build the QA rig from the engine JSON instead.
(2) The retargeted walk frame as the test pose: too harsh even for TaleWorlds' body (p99 1.70, max 4.2), so it
cannot separate weights from pose; use single-joint bends with the donor measured alongside. (3) A donor
left posed while transferring: nearest-surface mapping then swaps left and right calves. Also: FBX-imported
meshes carry a compensating parent-inverse, so their world matrix is identity and their coordinates are the
FixBone WORLD space (facing -Y); unparenting keeps that, and only a 180 degree turn puts them on the engine rig.

**Rule.** Judge skinning by edge stretch under standard bends with the donor as the bar, on a rig whose rest
you built yourself, with everything at rest during the transfer. A metric that reads 1.000 on every mesh is
a bind that did not evaluate, not a perfect skin.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/doc-lookup.md](../../reference/doc-lookup.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/lessons/xslt-moduledata.md](./xslt-moduledata.md)

<!-- backlinks-end -->
### An animation must be authored against the ENGINE skeleton, not a mesh FBX

Skinning uses bind matrices and is roll-independent, so a mesh FBX can carry arbitrary bone
orientations and still deform perfectly in game. Rotations are NOT roll-independent. So "vanilla
animations play correctly on this mesh, therefore its rig is right" is a false inference: it proves
the bone POSITIONS and weights, and says nothing about orientations. An animation authored on that
rig can look flawless in Blender and come out twisted in the engine, and nothing in Blender will ever
show it.

Get the real skeleton with `pwsh tools/dump_engine_skeleton.ps1 -Skeleton horse_skeleton`. For the
war ram it revealed two things no mesh FBX could: the engine parents `horseneck1` to `horsespine3`
(the mesh rig says `horsetail3`), and it has 32 bones with no `_nub_notused` entries.
- **Why missed:** the whole session compared the ram against other FBX files, which are all proxies.
  The engine's own `skeletons.tpac` was never opened until the end. Earlier attempts to read a horse
  skeleton failed on `pack_horse_customrig` and four other packages, and that was taken as "the
  skeleton is unobtainable" rather than "try another package". `skeletons.tpac` parses fine and holds
  every vanilla skeleton.
- **Prevent:** dump the engine skeleton FIRST, before authoring a single keyframe. Full write-up,
  including the rest-frame maths and the ranked dead ends:
  `docs/reference/bannerlord-skeleton-authoring.md`.
- **Source:** war ram #515, 2026-08-29.

### A verification that cannot fail is not a verification

Three separate hypotheses were "refuted" this session by tests that were structurally incapable of
detecting the fault, and two of them were later shown to be real problems.
- Deleting the `_nub_notused` bones in Blender and seeing no pose change proves nothing: the nubs sit
  at the same position as their child with identity rotation, so removing them is a no-op **by
  construction**. It says nothing about a Kit that drops them from a track list.
- Re-parenting `horseneck1` in Blender and seeing no pose change proves nothing either, for the same
  reason, and the engine really does parent it differently.
- Checking a bone's HEAD position cannot detect a rotation about its own origin. Head positions were
  used to "confirm no yaw", and they are blind to exactly that. A whole sweep of head angles rendered
  identically and was nearly accepted as "this bone does nothing"; the real cause was that the
  assigned action was being re-applied at render time and silently overwriting the manual pose.
- Every render for several hours was a SIDE view, which is close to blind to a yaw.
- **Prevent:** before trusting a negative result, ask what the test would show if the hypothesis were
  true. If the answer is "the same thing", the test is worthless. For pose work specifically: render a
  FRONT view, and assert on bone DIRECTION vectors rather than head positions.
- **Source:** war ram #515, 2026-08-29.

### Pick a reference creature that matches the shape of the thing you are building

Two rounds of work went into a 90-degree bone-convention gap measured against the chariot. `as_chariot`
uses `chariot_skeleton`: two horses plus a cart, on its own skeleton asset, whose horse-NAMED bones
are not the horse skeleton. Measured against the warg and the elephant, single-creature mounts with
BT-driven attacks, a creature's animation FBX uses the SAME bone convention as its own mesh FBX
(median 6.79 and 16.83 degrees, i.e. rest-pose differences), not a 90-degree flip.
- **Prevent:** for a creature mount, compare against warg / spider / elephant. Never the chariot.
- **Source:** war ram #515, 2026-08-29.

### A mesh tpac holds bone indices, not bone names, so scanning for bone names is not a skinning test

Restoring the fell warg meant proving whether the Kit had kept the skin weights on import. The test
used was a byte scan of the compiled tpac for the warg's bone names, decompressing every segment
first. It returned zero every time, against a control where the warg's own rig returned `Root_M` 12,
`Head_M` 17, `Hip_R` 8. That was read as "still unskinned" and reported three times across three
separate attempts, each followed by a different theory about why the Kit had dropped the binding.

All three were wrong. The maintainer opened the model viewer, played `as_warg` clips against the fell
warg, and it animated correctly.

**A skinned mesh stores bone INDICES into its skeleton's bone array. The names live in the Skeleton
item.** The warg's rig tpac contains bone names because it *bundles* `skeleton_warg` alongside its
meshes. A mesh that binds to a skeleton living in a different tpac has no reason to carry a single
bone name, and correctly does not.

So the scan was measuring "does this tpac contain a Skeleton item", dressed up as a skinning test,
and every creature that shares an existing skeleton would fail it.

**What to test instead.** The FBX, before it reaches the Kit, where the answer is unambiguous:

```
                 LimbNode  Deformer  Cluster  Skin  BindPose
skinned export     98        1001      490     10      20
unrigged source     0           0        0      0       0
```

Then confirm in the model viewer with an actual animation. The cheap empirical check beat three
rounds of binary analysis, and it beat them in one click.

**The generalisable failure.** A test that returns the same answer for "broken" and for "correct but
structured differently" is not a test. Before trusting a negative result, establish that the check
can distinguish the two, ideally by running it against a known-good example of the *same shape*, not
merely a known-good example. The warg was a bad control precisely because it bundles its skeleton and
the fell warg does not.

### The Modding Kit refusing a duplicate asset name is the protection working, not an obstacle

Sharing an existing skeleton raises an obvious worry: will importing a second FBX that contains the
same armature mint a second `skeleton_warg` and shadow the live one that `as_warg` resolves? The
spider's 2026-06-14 guid collision and recursive native access violation is what that fear is built
on.

Two workarounds were tried. Renaming the exported armature to `skeleton_warg_notused`, following the
convention animation clips use, produced a tpac with no Skeleton item, which looked like success.
Exporting with the real name `Skeleton_Warg` produced this in the editor console:

```
Unable to import skeleton_warg(Skeleton). Item with same name already exists in
Warg_Rig_V5_geo. Asset names are required to be unique within the same module.
```

**That message is the correct outcome.** The Kit enforces per-module name uniqueness itself, skips
the duplicate, imports the meshes, and lets them bind to the skeleton already present. No workaround
is needed and the `_notused` rename is unnecessary for a mesh that shares an existing skeleton. Both
exports produced a working creature; only one of them said so out loud.

Related, and measured on the same job: `primary_bone_axis='Y'` with `secondary_bone_axis='X'` is not
optional on FBX export. At `X`/`Y` the bone *heads* and the bounding box stay correct to 1e-06 while
the *tails* drift 0.575 m, putting `Hip_R` 88% of its reference motion out of place. A gate that
checks head positions or bounding boxes passes the broken file. Check tails.
`tools/blender/harness.py` and `arp_retarget.py` use the correct pair;
**`tools/blender/creature_anim_ops.py` lines 288, 338 and 427 carry `X`/`Y` and are wrong.**

### Texture rules the Modding Kit does not enforce loudly

Three constraints found the hard way while importing 28 inherited textures, none of which produces a
clear error message.

**Dimensions must be divisible by 4, and should be a power of two.** BC block compression works on
4x4 blocks. Eight of the fell warg's maps were 5689x5689, which is odd, so the Kit silently fell back
to uncompressed `R8G8B8A8_UNORM` at **164 MiB each**. Six of them accounted for essentially the whole
940 MB RuntimeDataCache payload for one creature. Nothing warned; the assets simply were not
compressible. At 2048 with DXT5 and mips the same map is 5.33 MiB.

The formula worth remembering: `bytes = width x height x bpp x 4/3`, where the `4/3` is the mip chain
and bpp is 4.0 uncompressed, 1.0 for BC3/DXT5 and BC5, 0.5 for BC1 and BC4. **Halving the dimension
quarters the memory.**

**Palette-mode PNGs compile to single-channel BC4.** Four maps were 8-bit palette, including two
normal maps, and a single-channel normal map is worthless. Re-saving them as RGB fixed it. Note that
a resize pass will silently skip files that are already the target size, so they keep their palette;
the de-palette has to be a separate forced re-encode.

**Uppercase in a texture filename crashed the editor** when the texture was assigned to a material.
Observed by the maintainer across repeated attempts; every one of the 28 inherited files carried
uppercase, and renaming them all to lowercase resolved it. The rule may be narrower than "any
uppercase", because the warg's own shipped `Warg_skin_d` carries a capital W and works, so the
trigger could be the mixed `T_GD_` pattern or a case-only mismatch somewhere in the chain. Lowercase
is safe either way. Worth knowing that mesh names are lowercased automatically on import
(`SK_GD_Fellwarg` becomes `sk_gd_fellwarg`) while texture names keep their case, and that asymmetry
is exactly the shape that produces a lookup miss.

**And when resizing data maps, force the colour management off.** Normal, AO and metallic are data,
not colour. Set `colorspace_settings.name = 'Non-Color'` and the scene view transform to `Standard`
with gamma 1.0 before saving, or Blender applies a filmic transform to the buffer and produces a
creature with subtly wrong lighting that nobody can explain later.

### A mount's clip needs a priority and `enforce_lowerbody`: the Kit's default clip plays in the viewer and not in battle

**Context (2026-09-18, the war ram).** The head-butt played correctly in the Kit viewer and the TAOM log recorded it
firing 1,300 times in a Custom Battle, yet Mike never saw the head drop. The clip carried the Kit's defaults, priority
0 and no flags. At priority 34 it was still not visible (1,005 butts, next battle). Every vanilla horse clip read
(`horse_kick` 34, `horse_rear` 74, even the hit reaction `horse_hit_from_front` at priority 2) carries
`enforce_lowerbody`, and `horse_kick` and the warg's `warg_attack_stand` share priority 34 + `enforce_lowerbody` +
`enforce_all` + blends 0.2 / 0.4; the ram now carries that recipe, in-game result pending.

**Why missed.** The viewer plays a clip alone, with nothing competing for channel 0; a battle is the first place
locomotion does. And the damage count proved nothing: the attack task plays the action and applies damage in the
same tick, so damage lands whether or not the animation survives.

**Rule.** Give a mount's action clip the recipe of the nearest vanilla clip of its kind, read from the tpac (table in
`docs/reference/bannerlord-animation-clip-flags.md`), and judge it in battle, never in the viewer.

### A hand-built tpac package is invisible to the game until the Modding Kit has cooked its RuntimeDataCache entry

**2026-09-17, spider skin variants (#616).** A tool cloned the proven split spider meshes under new
names with a different material: geometry verbatim, names and material GUID rewritten, fresh item,
segment and package GUIDs. Every validator passed, the catalogue parsed the clones, the engine logged
no `Unable to find` and no dependency error, and the inventory tableau drew nothing. Two rounds of
file-level suspicion followed (the item checksum and the per-segment hash, both cracked as xxHash64
seed 0, both recomputed, both irrelevant to the symptom) before a probe package that redefined the
live `sk_spider_forest_c` produced no `Overriding item` line. The client had never registered a
single item from either hand-built package. The August native-commit audit had already settled why:
the shipping client renders from `<module>/RuntimeDataCache/<package GUID>.rdc` and carries no code
to write one; every write string lives in the editor binary. A census made it concrete: of 526
mesh-bearing Armoury packages, the only two without an entry were the hand-built ones. Opening the
Armoury in the Modding Kit and saving cooked the entry and the meshes appeared.

**Rule.** A tpac written or patched outside the Kit is not done until the Kit has saved the module
and `RuntimeDataCache/<package GUID>.rdc` exists for it; check the entry by name before any in-game
test, because the failure mode has no log line and looks exactly like broken bytes. The package
GUID is the key, so a rebuild under a fresh GUID needs a fresh cook. Ship the entry with the module
(the mirror tracks `RuntimeDataCache/`). And when a hand-built package fails, probe the loader before
the bytes: redefine an existing item name in a throwaway package and look for `Overriding item` in
`rgl_log`; its absence means the package was skipped whole, and no amount of byte work will change
that.

**Format facts that came out of it, still true.** Each segment entry carries xxHash64 (seed 0) of its
decompressed payload at offset 56, and the item checksum is xxHash64 over the int64 metadata length
plus the metadata; `tools/tpac_clone_metamesh.py` and its tests carry the reference implementation,
and the Kit's own resave of the clones rewrote the checksums to the same values. The 2026-06-11
byte-patched clips with copied hashes loaded because their packages already had cache entries from
the Kit; that precedent says nothing about hash validation either way.

**Addendum, same evening, the cave troll clips.** TpacTool.Lib's `Package.Save` writes a ZERO item
checksum, and every generated `_anm.tpac` in the Armory carries one (73 warg, 24 spider, 24 chariot,
31 of 33 elephant) while the ram's 6 Kit-authored clips carry real hashes; all of them have `.rdc`
entries and all load, so the entry is the load-bearing half and the hash is hygiene.
`tools/tpac_fix_item_checksums.py` recomputes the field (proven on Kit output first) and
`tools/check_rdc_entries.py` is the entry gate; 925 `_mtl` packages have no entry and render, so the gate
checks `_geo` and `_anm` by default. **Corrected the same day:** the 52 troll masters without an entry were
NOT invisible. The Kit never writes an entry for a skeletal-animation master, and masters play without one:
warg 56 of 56, elephant 31 of 31, chariot 3 of 3 and spider 24 of 26 animation masters have no entry, every one of their clips has one, and all of those creatures animate in game. The gate now recognises a master by its item type (SkeletalAnimation, no Metamesh) and
counts it separately; reading "masters lack entries" as a failure had put a Kit save that could never
satisfy it on the owed list for a day. The same census habit that proved zero checksums harmless (count
the shipping packages that work before calling a difference a defect) would have caught it at once.

### A creature's hit capsules are the Kit's defaults until someone fits them to the mesh
Players reported the war elephant's collision as too small (2026-09-18). A creature has three collision layers: the
Monster's `body_capsule` (what agents bump into), a hit capsule per bone (what blows and missiles strike) and a ragdoll
capsule per bone (the corpse). 41 of the elephant's 60 hit capsules were still the Kit's defaults, thin rods along each
bone with a radius about a ninth of the capsule's length: the neck was 0.03 to 0.05 m wide inside 0.6 m of neck and
only 48% of the skin sat inside any hit capsule. The chariot (57 of 60, 8.8% of its skin hittable) and the spider (40
of 62, 49.9%) carried the same defaults. Fitted to the skinned mesh the three reach 98.2%, 98.0% and 97.2%.
- **Why missed:** the creature renders, animates and takes damage, so nothing looks broken; the Kit shows the capsules
  only in its skeleton editor and no gate reads them. The first answer to "too small" went to the body capsule, a
  different layer, and a doubled radius would have enclosed the howdah's physics floor, the known slide cause.
- **Prevent:** once a creature's skeleton and mesh are final, run `python tools/skeleton_hit_capsules.py show --tpac
  <geo.tpac>` and fit what is still default (`fit`, then `patch --apply`, then a Kit load). Before changing any
  collision, name the layer the complaint is about: bumping is the body capsule, blows passing through are the hit
  capsules, a corpse is the ragdoll.
- **Source:** `docs/reference/bannerlord-skeleton-authoring.md` "Hit capsules", `docs/features/elephant.md` "Collision".

### Fitting a foreign rig onto an engine skeleton: measure the joints first, fit segments only, and never aim a pivot
The Animalia elk and moose (Fab, #646) ship their own quadruped rig. Measured before building: after one uniform scale their legs and spine sit 2 to 11 cm from `horse_skeleton`'s joints, so the mesh can be BENT onto the horse rest by its own weights instead of re-weighted from a donor. The first fit still treated every mapped joint pair as a segment. `horsepelvis` is a pivot 8 cm straight above `horsespine1`, so aiming the pack's pelvis at it swung the hips 90 deg and tore the rump; the elk's short tail was stretched 5 to 6 times into a horse's; the pack's "collarbone" is a midline chest bone, so fitting it to the moose's `horselleg1` swung it 73 deg; and the moose's neck, half a horse's, came out as a horse neck with no hump.
- **Why missed:** every one showed in the fit report (`swing_deg` 90.0 on the pelvis, `stretch` 5.06 and 6.36 on the tail, 2.19 and 73 deg on the moose collarbone, 2.72 on its neck), but the report read as numbers until the side render showed the tear and the tail.
- **Prevent:** read every segment with a swing over about 30 deg or a stretch outside 0.5 to 2 before trusting a fit, and render it. A root or pivot joint goes to the global fit only; a part that must keep its own proportions gets an anchor-only chain (`"stretch": false, "swing": false, "anchor_first_only": true`, and a per-animal profile when only one animal needs it). Its clips must use the same profile as its mesh.
- **Source:** `docs/features/animalia-elk-moose.md` "What the first runs got wrong", `tools/blender/animalia_to_horse_map.json`.

### A mesh bent onto the target's rest needs its clips carried through the fit, not re-aligned to the source's stance
The troll retarget (`retarget_mannequin_to_human.py`) swings each target bone's rest onto the source's rest direction (`S_align`), so the target stands in the source's stance at the source's rest. That suits a human body kept at human proportions. A mesh that was already bent onto the TARGET's rest (the Animalia reskin) would be posed a second time and deform off its rest. `retarget_animalia_to_horse.py` instead carries each pack bone's world-space motion through the rotation the mesh was bent by: `R_t = C . D_s . C^-1 . R_t_rest`, `C` = the engine turn times the fit rotation, so a clip's rest frame is exactly the horse rest.
- **Why missed:** not shipped; caught at design time. The troll tool is the proven path and its docstring presents `S_align` as the method, so reusing it unchanged is the natural mistake.
- **Prevent:** before reusing a retarget, ask what the target mesh's rest is. If the mesh was fitted to the target skeleton, retarget through the same fit (the reskin exports its rotations: `bone_transforms` returns them). Check with a side-by-side render of the retarget and the source clip at the same frames.
- **Source:** `docs/features/animalia-elk-moose.md` "Solution Approach", `tools/blender/retarget_animalia_to_horse.py` docstring.

### A rigid-skull rule keyed on the neck joint's height misses a jaw that hangs below it (2026-09-24)
The cave troll's re-skin (2026-09-18) made a head mesh's vertices rigid on `head` only above the neck joint's height.
A troll's jaw hangs forward at or below that height, so its underside kept the nearest-human-surface weights, the
chest (`spine2`), and the mouth tore open whenever the head moved (Mike's screenshots). The QA passed it: its one
neck bend nodded forward, the chin compressed rather than stretched, and nothing judged compression.
- **Why missed:** the rule was written for a human head, where everything in front of the neck is above it; the
  QA was a set of numbers nobody failed on, and the worst of them (2.05 against the donor's 1.77) read as close.
- **Prevent:** on a creature on a human rig, a head mesh carries only `head` and `neck` weight; the tool asserts it
  and fails its completion flag otherwise. Nod the head both ways in QA, and look at the jaw in the Kit before
  calling a re-skin done.
- **Source:** `docs/features/troll-race.md` "Cave troll jaw fix (2026-09-24)".

### Every animation the Modding Kit imports may name no skeleton: census the masters after each import
The Kit imported all 97 Animalia masters (2026-09-23) with their Skeleton reference EMPTY: the 16 zero bytes before BoneNum and Duration in the SkeletalAnimation metadata. The troll's 52 masters (2026-09-17) and the war ram's re-import (2026-09-18, a `.001` take) came in the same way. Nothing in the Kit or its log says so, every clip still plays in the Kit's viewer, and a master that names no skeleton is not tied to the rig its action set plays it on.
- **Why missed:** each time it was found by reading a master back with TpacTool while chasing something else, and fixed by a one-off patch inside a pack-specific generator (`gen_troll_anim_clips.ps1`, `wire_anim_master_clip.ps1`), so there was no step in the workflow that looked for it.
- **Prevent:** after every animation import, run `powershell.exe -File tools\wire_anim_master_skeletons.ps1 -Masters <Assets folder>` (census: ok / EMPTY / WRONG RIG / OTHER, plus packages with no animation and stray Skeleton copies), then `-Apply` with the Kit closed, which patches EMPTY in place and re-reads each file. It is stage 7 of `docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md`.
- **Source:** #646, `docs/features/animalia-elk-moose.md` "Owed" item 1; the troll and ram notes in the headers of `tools/gen_troll_anim_clips.ps1` and `tools/wire_anim_master_clip.ps1`.

### Copy a human's physics onto a Blender-authored rig through the bone frames, never verbatim (2026-09-24)
The dwarf's physics is `human_skeleton`'s byte for byte (28 bodies, 34 joints), and that works because its bones keep
the human's axes. KEYForce's `troll_skeleton_a` runs every bone along +Y instead of +X, rolled 180 to 280 deg off the
human's: a verbatim copy would lay each capsule across its limb and hinge each joint about the wrong axis, and nothing
would say so before a corpse fell wrong in game.
- **Why missed:** not shipped; caught before the copy by measuring both skeletons' bone axes from the rest frames.
  The dwarf precedent reads as "copy the human's", which holds only for a rig with the human's frames.
- **Prevent:** before copying physics between skeletons, compare the local axis the child offsets lie along and the
  rolls; when they differ, use `tools/tpac_skeleton_copy_physics.py`, then check in world space that each joint keeps
  the donor's angle to its bone and each capsule sits at the same fraction along its bone.
- **Source:** `docs/reference/bannerlord-skeleton-authoring.md` "Ragdoll, IK and hit capsules for a humanoid on its
  own skeleton".

### TaleWorlds' own packages store 0 under a rest frame's translation: never multiply rest frames as a raw 4x4 (2026-09-24)
`human.tpac` stores (x, y, z, 0) in each rest frame's translation row, with stray denormals above it; Kit output (the
troll, the dwarf, the elephant) stores 1. `skeleton_hit_capsules.world_matrices` multiplied the raw 4x4, so on
`human.tpac` every parent's position dropped out and the head landed at 0.107 m. It had only ever read Kit output, so
nothing it wrote was wrong.
- **Why missed:** its tests built rest frames with 1 there, and every package it had read came from the Kit.
- **Prevent:** compose rest frames as a rotation plus an offset, or force the fourth column to (0, 0, 0, 1), as
  `world_matrices` does since (a test pins the 0 case). The copy tool's geometry gate (head above pelvis, toes
  ahead of feet) caught it on first contact: give any tool that reads a new source of skeletons such a gate.
- **Source:** `tools/skeleton_hit_capsules.py` `world_matrices`; `check_facing` in `tools/tpac_skeleton_copy_physics.py`.

### A "Kit default" axis is a rig convention: a fixed local x lies across every limb of a +Y rig (2026-09-24)
The hit-capsule fitter fell back to the bone's local x, "the Kit default", wherever a bone's skin was not elongated.
On `troll_skeleton_a`, whose bones run along +Y, that laid the capsules of the left thigh, the calves, the upper arms,
the forearms and the hands exactly 90.0 deg across their limbs; the left thigh covered 83% of its skin, 95% once the
fallback followed the bone.
- **Why missed:** the skeletons it had fitted (elephant, chariot, spider) have mostly elongated skins, so the
  fallback rarely fired, and no check compared a fitted capsule's axis with its bone's direction.
- **Prevent:** take a bone's direction from its child's position along the skeleton's detected bone axis
  (`bone_axis`, `bone_directions`), never from a fixed local axis; after a fit, compare each capsule's axis with its
  bone's. For a humanoid, `--axis bone` pins every capsule to its bone.
- **Source:** `tools/skeleton_hit_capsules.py`; `docs/reference/bannerlord-skeleton-authoring.md`.

### An FBX material name that matches an older Kit material anywhere in the module binds to it silently (2026-09-24)
The Kit binds each imported mesh to the material its FBX names, lowercased, looked up across the whole module. The
new hill troll's FBX named `M_HillTroll_Body_A`, `_Cloth_A`, `_Eye_A` and `_Head_A`; the old hill troll's March
materials `m_hilltroll_*_a` still sit in `Trolls\Hill Troll\textures\`, so the new meshes bound to them and wore
the old textures (the head's showed at the mouth), while the `t_tr_hill_troll_*_a` materials made for the model went
unused. A name with no match warns "Unable to find material"; a name with a stale match warns nothing.
- **Why missed:** the import raised no warning, and the check after it looked at the skeleton, never at which
  material each metamesh names.
- **Prevent:** export with the Kit material's exact name (`export_rig_for_kit.py --material OLD=NEW`,
  `fbx_remap_materials.py` for an existing FBX), and after any import read the material names the metamesh items
  carry against the materials meant for them.
- **Source:** `docs/features/troll-race.md` "Materials (2026-09-24 pm)".

### The Kit makes a named sub-mesh per FBX object `<mesh>.<part>`: never join a head's eyes and mouth into one object (2026-09-24)
A race head needs its parts as separate sub-meshes, each tagged in the Kit: the dwarf's FBX carries
`SM_Dwarf_Basemesh_A1_head`, `_head.eye` and `_head.mouth`, and its package tags them `face_base_mesh`,
`face_eye_mesh` and `face_mouth_mesh`. The hill troll's export joined KEYForce's `head.base`, `head.eyes` and
`head.mouth` into one object, so the Kit split it by material into `head.0` and `head.1`: the mouth, sharing the
head's material, vanished into `head.0` and could not be tagged.
- **Why missed:** the export gate checked bones, weights and slot names, and the one-mesh-per-skin-slot rule was
  read as one OBJECT per slot, when a slot is one metamesh that may hold several named objects.
- **Prevent:** export a head as `<mesh>`, `<mesh>.eye`, `<mesh>.mouth` (`export_rig_for_kit.py --slot` per part),
  and after the import compare the metamesh's sub-mesh names with the dwarf's.
- **Source:** `docs/features/troll-race.md` "Materials (2026-09-24 pm)".

### A fit over a package fitted before keeps most bodies: take the kept capsules too, or the copy undoes the fit (2026-09-24)
`skeleton_hit_capsules.py fit` keeps a body whose existing capsule already covers more of its skin. Run over the hill
troll's package after its first fit, it refit 2 bodies and kept 26, and `tpac_skeleton_copy_physics.py --fit` took
capsules only from refit bodies, so applying it would have written the thin height-scaled copy over 26 fitted hit
capsules. Caught in the dry run's counts ("2 refit, 26 kept") before any write.
- **Why missed:** both tools were first run on a Kit-fresh package, where every body with skin gets refit, so
  "refit" and "sized by the fit" were the same set.
- **Prevent:** the copy takes every body the fit sized (refit, or kept with a real capsule) and falls back to the
  copy only for a kept body without one; re-measure coverage on a scratch copy before applying (99.5% held).
- **Source:** `tools/tpac_skeleton_copy_physics.py` docstring, `test_a_fit_writes_hit_capsules_and_sizes_the_ragdoll_radius`.

### A helper bone carried by the donor's proportions can miss the target's anatomy: check it against the skin (2026-09-24)
The hill troll's grip bones (`r_finger0`, `l_finger0`) were carried from the human hand scaled by the height ratio,
0.19 m from the wrist. A troll's fist hangs far below its wrist: the hand's own skin centres 0.17 to 0.19 m below
that point, and KEYForce put the grips 0.22 m lower (`--offset`).
- **Why missed:** the carry checks only that the bone keeps the donor's frame relative to its parent; nothing
  compared the result with the mesh the bone serves.
- **Prevent:** after carrying a bone that must sit inside the mesh (a grip, a mount point), compare it with the
  centroid of the skin its parent drives, and let the artist place it where proportions differ.
- **Source:** `docs/reference/lotrlome-hill-troll-changes.md`, `tools/tpac_skeleton_copy_physics.py` `--offset`.

### A custom skeleton's bone rolls twist every human clip: the engine plays joint rotations as they are (2026-09-24)
Human clips store each bone's rotation relative to its parent. On `troll_skeleton_a`, whose bones were rolled 180 to
280 deg off the human's, `guard_up_2h` twisted the arms, shoulders and head in the Kit; the dwarf gets away with the
human set because its bones are within about 35 deg. Proportions do not matter, frames do.
- **Why missed:** "keep the artist's frames" was decided on the plan to author troll clips, before anyone checked
  what the inherited human clips (every action the Fab set does not cover) would do on those frames.
- **Prevent:** before binding human clips to a custom skeleton, preview one in the Kit. For a humanoid, re-frame the
  rig to the human's axes on export (`tpac_skeleton_copy_physics.py --reframe`, `export_rig_for_kit.py
  --bone-frames`); positions stay, so the mesh and weights need nothing.
- **Source:** `docs/features/troll-race.md` "Re-framed to the human's axes".

### A rule that re-derives a mapping from the rig can disagree with how the rig was built (2026-09-24)
After the re-frame every troll bone maps from the human by the identity by construction, but re-running the
physics copy re-derived the maps with its axis-child rule: the hands, leaves when re-framed, now had grip children
KEYForce had moved 0.22 m off their axis, so the rule aimed them 24 to 30 deg away and would have turned the wrist
joints. Caught by checking the maps before the write.
- **Why missed:** the rule was right on every rig it had seen; nothing recorded that this rig was built by a
  re-frame, so the tool could not know its own earlier answer.
- **Prevent:** a tool that builds a rig writes a record of it, and the tool that reads the rig later checks against
  that record and uses the construction's answer (`--reframed <record>`) instead of re-deriving it.
- **Source:** `tools/tpac_skeleton_copy_physics.py` `bone_maps(identity=...)`, `--reframed`.

### Map the source's twist helpers: an unmapped forearm twist leaves the whole roll at the wrist (2026-09-24)
The Fab danger run turns the right hand 136 to 178 deg about the forearm, 89 of them on `lowerarm_twist_01_r`. With
the twist bones unmapped, the human `*_foretwist1` stayed at rest and the hand took the whole roll against it: the
hill troll's wrist twisted into a ribbon in the Kit (Mike). Mapped, the wrist carries 82 deg and the forearm 90, the
source's own split.
- **Why missed:** the map was written from the anatomical chain (upper arm, forearm, hand); helpers sit in no
  chain, and the previews render a mid frame where the roll is small.
- **Prevent:** map every helper the source animates to the target's equivalent, and measure the twist per joint
  (swing-twist decomposition) against the source before calling a retarget done.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `MANNEQUIN_TO_HUMAN`; `docs/features/troll-race.md`
  "Fab clips re-retargeted".

### A UE FBX export can park pelvis height above bind on the root node: an in-place retarget must keep the root's height (2026-09-24)
The Fab export clamps the pelvis at its bind height (1.181 m) and moves any height above it onto the root:
`danger_run_0` holds the pelvis at 1.181 for six frames while the root rises 6.5 cm, `danger_attack_1` 12.9 cm, 21 of
52 clips. Dropping the root with the travel cut the top of every bob and sank the body by the cut on those frames;
the leg IK then put the feet 8 cm under, exactly as asked.
- **Why missed:** "root motion" was read as travel and turn; the height component was never scanned, and the feet
  probe measured the source in world space, where the clamp is invisible, against the target in armature space.
- **Prevent:** scan the source object's Z per frame before retargeting; keep the root's height in the source pose
  (`_root_height_matrix`) and drop only its horizontal travel and yaw.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `_root_height_matrix`;
  `docs/reference/ue-to-bannerlord-asset-pipeline.md` "The retarget stage".

### Anchor a retarget's foot goals on the source's stance from the target's hip, never on the target's own rest foot (2026-09-24)
The hill troll's bind pose stands its feet 35 cm in front of its hips; the Fab troll's stand 19 cm behind (at
hill-troll scale). Goals built as "the troll's rest ankle plus the Fab ankle's scaled move" kept the troll's stance
and added the Fab's stride, so a forward step asked the leg for 19% more than its length and the IK missed by up to
37 cm. Anchored on the Fab stance from the troll's hip (`_stance_ankle`), the misses fell to 0 to 5 cm and the IK's
corrections from 48 to 89 deg to 5 to 23.
- **Why missed:** the rest-relative anchor is exact for planted feet (no slide) and right when both rests share a
  stance; nobody compared the two bind stances.
- **Prevent:** print both rigs' hip-to-ankle rest vectors (scaled) before wiring an IK; a difference is a stance,
  and the source's stance is the one the animation was made for.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `_stance_ankle`.

### Take a two-bone IK's bend plane from a rest pole carried by the thigh, never from the retargeted knee (2026-09-24)
The knee's offset from the hip-to-goal line shrinks to nothing as the leg straightens and its direction then flips
frame to frame: `run_to_heavy_attack`'s left thigh and calf jumped 58 and 63 deg in one frame where the source moved
13 and 15, `hit_right1` 28 against 14. The rest knee's forward offset, kept in the thigh's frame and turned with the
retargeted thigh, is a plane that holds; carrying the calf with the thigh's correction before aiming it keeps the
knee a hinge.
- **Why missed:** the IK was judged on the feet, which it fixed, and on mid-frame previews; per-frame rotation
  steps were not compared with the source's until the seam probe ran.
- **Prevent:** after any IK, compare each bone's largest per-frame world rotation step with the source's; a step
  well above the source's is a solver artefact, whatever the still frames show.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `_knee_pole`, `_leg_ik`.

### A foot's rest pitch is its stance: no swing alignment for feet (2026-09-24)
Both rigs stand flat-footed in bind, but the Fab foot bone points 41 deg down from ankle to ball and the hill troll's
21 (a low ankle and a long foot). Aligned to the Fab's line the troll's toes pitched down and sank 9 to 17 cm while
its ankles held. Delta-only (`NO_ALIGN`), the toes stay where the troll's own rest puts them.
- **Why missed:** alignment was applied to every chain bone alike; a foot was treated as a limb segment rather than
  a stance.
- **Prevent:** measure feet per bone (ankle and toe) against the clip's first frame; a toe that sinks under a held
  ankle is a pitch problem, not a height problem.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `NO_ALIGN`.

### A clip generator's travel scale is the retarget's stride scale, read from its report, never a ratio picked by hand (2026-09-24)
`gen_troll_anim_clips.ps1 -TravelScale` sizes each loop's displacement; the planted foot slides back by exactly the
factor the retarget scaled the stride (`pelvis_scale` in its report, 1.5578 for the hill troll, the thigh + calf
ratio). The first run used the pelvis height ratio 1.377, 12% short, which would have skated the feet by that much:
the planted foot travels 2.71 m per `combat_walk1` loop against the source's 1.74, ratio 1.558.
- **Why missed:** the two numbers were the same while the retarget scaled by pelvis height; the retarget changed its
  scale and the generator's documented value did not follow.
- **Prevent:** the generator's header names the report field; measure the planted foot's travel per loop in the
  exported clip and divide by the source's before writing clips.
- **Source:** `tools/gen_troll_anim_clips.ps1` header; `docs/features/troll-race.md` "Fab clips re-retargeted".

### Measure an exported clip against its own rest frame: an armature-only FBX re-import has no bind pose (2026-09-24)
Blender re-imports an armature-only FBX with the pose at the export-time current frame as `matrix_local`, so
heights measured against `head_local` were offsets from a mid-clip preview frame: "rest calf 58.8 deg from
vertical" and the first foot numbers were artefacts. The exports open on the engine rest at frame 0 (pelvis
1.6266 m), which is the reference.
- **Why missed:** with a meshed FBX the bind pose survives, and the source clips are measured that way; the exports
  differ only in having no mesh.
- **Prevent:** for an armature-only export, set the frame to the clip's first frame and take rest positions from
  `pose.bones[].head` there, never from `data.bones[].head_local`.
- **Source:** `docs/features/troll-race.md` "Fab clips re-retargeted" (the probes in
  `E:\LOTRAOMAssets\_hill_troll_a_export\review_20260924b\`).

### A re-framed rig matches a clip's bone AXES, not its rest RELATIONS: another rest pose's clips still land each bone at the donor's orientation turned by its parent's rest difference (2026-09-24)
The hill troll's rig was re-framed to the human's bone axes, so human clips bent its joints the right way and the
Fab clips retargeted onto it looked right. But every `human_skeleton` clip (the cave troll's `anim_troll_*`, and
every vanilla action the Fab set does not cover, the engine's melee attacks and blocks among them) put the head 45
to 65 deg up and twisted the wrists 20 deg. A clip stores parent-relative rotations, and the troll's hunched rest
keeps its own relations (spine2 54 deg and neck 45 off the human's, the hand 20), so each bone lands at (parent's
rest difference) x (the human's world orientation).
- **Why missed:** the re-frame was judged on the Fab retarget, which compensates for rest differences, while the
  human `guard_up_2h` preview stayed owed; "clip = relations" was never written against "re-frame = axes".
- **Prevent:** before binding another skeleton's clips to a re-framed rig, print both rigs' rest bone directions and
  orientations per bone; pairs beyond a few degrees mean those clips need a retarget (or the rig needs the donor's
  rest relations, which gives it the donor's posture on those clips). The hill troll takes the retarget route:
  `retarget_mannequin_to_human.py --source-json --source-rig human`.
- **Source:** `docs/features/troll-race.md` "Human clips for the hill troll".

### An action set names AnimationClip definitions, and a master named like a clip can be an empty shell: resolve clip names through animation_clips.tpac (2026-09-24)
`stand_2h`, `ready_slashright_2h` and the rest of `as_human_warrior`'s `animation=` values are AnimationClip names in
`animation_clips.tpac`; their SkeletalAnimation masters carry other names (`stand_right_twohanded`,
`anim_twohanded_slashright_ready`), several clips share one master by sub-range (`blocked_slashright_2h` plays
`anim_twohanded_slashright_unbalanced` backward, 110 to 1), and a master that happens to carry a clip's name can be a
0-frame shell (`jump_loop`). Looking masters up by clip name found one of six.
- **Why missed:** the 2026-06-14 extraction used master names that happened to match, and the cave troll's clips were
  named by us.
- **Prevent:** `read_anim_keyframes_tpac.ps1 -ByClip` resolves clip names through the clips package to the master GUID
  and writes `clips_index.json` (master, Source1, Source2) for `gen_troll_anim_clips.ps1 -CloneByName`.
- **Source:** `tools/read_anim_keyframes_tpac.ps1` header.

### Vanilla masters key sparsely and their Duration is the root track's key count: sample by interpolation and take the length from the last key (2026-09-24)
`anim_kick_stanceswitch_right` keys frames 0 to 182 and then 195 (a hold), `anim_jump_loop` about 100 keys over 600
frames, and its `Duration` reads 5, the number of root position keys. Stepping to the nearest key and trusting
`Duration` would have frozen holds into steps and cut the jump to five frames.
- **Why missed:** the Fab masters are dense (one key per frame) and Kit-written masters store the frame count in
  `Duration`, so both assumptions held on every master seen before.
- **Prevent:** length = last key + 1 over every track; slerp and lerp between the bracketing keys
  (`key_source_from_json`); on a new source, compare keys per bone and the last key time with `Duration` first.
- **Source:** `tools/blender/retarget_mannequin_to_human.py` `key_source_from_json`.

### Blender's FBX importer lands an export's frame 0 on the action's first frame: address frames from `action.frame_range[0]` (2026-09-24)
Our exports carry the rest at frame 0; re-imported, that key sits on Blender frame 1. A probe comparing "frame 0"
with "frame 1" compared the rest with itself and reported every trunk unchanged, and the tool's posture reader made
the same mistake and returned the rest as the posture, so a run meant to change the hunch changed nothing (a pixel
diff of the previews showed a few hundred pixels).
- **Why missed:** `measure_feet_f0.py` already used `frame_range[0]` for the rest; two later probes and the posture
  reader hard-coded 0 and 1.
- **Prevent:** every read of an exported clip takes `f0 = action.frame_range[0]` and addresses frames as `f0 + n`;
  when two runs are expected to differ, pixel-diff the previews before drawing a conclusion.
- **Source:** `posture_from_clip` in `tools/blender/retarget_mannequin_to_human.py`.

### Before calling an action set's inherited clips "rarely played", list the engine's other consumers (2026-09-25)
The hill troll's set left 4,049 codes on human clips, called "rarely played" because the names looked like swimming,
ladders and cutscenes. The engine also plays that set off the battlefield: `CharacterTableau` idles party-screen,
encyclopedia and inventory previews on `act_inventory_idle*`, `ConversationMissionLogic` spawns the two highest-level
troops as map-conversation bodyguards on the `_poses` set, and `AgentVictoryLogic` plays `act_cheer_*` after a won
battle. On a hunched rig each shows the human rest relations (head up, wrists twisted). 49 derived `as_hill_troll_*`
sets also carried 2,376 overrides of their own, all human clips.
- **Why missed:** frequency was judged from code names, not from the engine's callers of the set or the derived sets.
- **Prevent:** before binding a race's set, grep the installed engine for `GetActionSetWithSuffix` and the idle and
  victory actions, list the derived `<race>_*` sets that override, and retarget what those paths play.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 4.

### A clip reused for another code must carry the flags that code's consumers rely on (2026-09-25)
The hill troll's Fab idles were bound to 172 inventory, conversation, pose and cheer codes. The idles are
priority 1 with no `cyclic` flag; the vanilla clips behind those codes are `cyclic`, some continue into a loop
code, the cheers are priority 64, and the party screen, the map conversation and the victory logic set the
action once, so the reused clip plays five seconds and stops.
- **Why missed:** the reuse was judged by motion (an idle for an idle), not by the clip definition the engine
  reads.
- **Prevent:** before binding one clip to another code, read the code's vanilla clip (flags, priority,
  `ContinueWithAction`) and the consumer that plays it; where they differ, clone the code's own vanilla clip onto
  the reused master, as `gen_troll_anim_clips.ps1 -CloneByName` does.
- **Source:** `docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md` finding 45.

### Compare a tpac skeleton after a Kit re-import by its decompressed segments, not its stored bytes (2026-09-25)
After the Armoury LOD re-import, four of the five skeleton-carrying packages (chariot, hill troll, warg,
`keyforce_dwarf`) looked changed, and the chariot's fitted hit capsules looked lost, because the stored segment
bytes differed. Decompressed, the capsule, body and ragdoll segment was byte-identical in all five, and the bone
segment kept order and parents with rest frames moved by at most 8.6e-5 (FBX round-off). The LZ4 output differs on
every compile, so a stored-byte hash reports a change that is not there, and a restore on that evidence would have
put the old bones under meshes just compiled against the new ones.
- **Why missed:** the first check hashed the segments as stored in the file.
- **Prevent:** compare `read_segment_data` output (`tools/tpac_skeleton_dump.py`); where the bone segment differs,
  parse the rest frames and restore with `tpac_skeleton_swap.py` only past the bind-pose tolerance (axes 1e-3,
  offsets 1e-4 of the rig extent) or on a changed bone order.
- **Source:** `docs/reference/armory-guide.md` "LODs in the FBX sources".

### A race head with no face morph channels crashes the first agent built from it (2026-09-25)
KEYForce's hill troll head, eye and mouth carried no shape keys. The Kit imported them, every Kit look
passed, and the first Custom Battle with a hill troll crashed to desktop at `TaleWorlds.Native.dll`
+0x57070C: the engine's static face morph read a null morph buffer (address 0x168C, row 962, the head's
vertex count), because the Kit gives such a mesh an empty morph record that slips past its null check.
- **Why missed:** the wiring copied the dwarf's skin layout and the face tags, not the dwarf's shape keys;
  the Kit preview never runs a face morph, and no hill troll had spawned in a mission before.
- **Prevent:** a new race head gets the 101 LOD0 channels every working head carries before its first
  battle (`tools/blender/add_face_morph_channels.py` when the art has none); read a native crash's dump
  for the faulting address, which named the mesh by its vertex count here.
- **Source:** `docs/features/troll-race.md` "Face morph channels, the first Custom Battle CTD".

### A melee release bound to a custom clip crashes the first swing (2026-09-25)
The hill troll's generated set bound 32 `act_release_*` and `act_quick_release_*` codes to human clips
retargeted onto its skeleton. Every Kit look passed; the first swing in battle crashed to desktop at
`TaleWorlds.Native.dll` +0x6590B9. Melee is engine pose-blend: the swing plays per-clip pose data looked up
by the vanilla clip's index, and a new clip has none, so the lookup returned a null entry.
- **Why missed:** the pose-blend fact was written down (`troll-race.md`, the clip-flags reference) as "custom
  attack clips cannot drive melee", a visual limit, not as a crash; the retarget batch then took the
  two-handed group by name, releases included, and no gate knew which codes the engine drives itself.
- **Prevent:** a swing code keeps the vanilla clip (`bind_hill_troll_action_set.py` rule 0, tested); for a
  native crash that is a hash-map miss, read the key register from the dump and resolve it in game (the
  trace's clip-index scan named the clip in one run).
- **Source:** `docs/features/troll-race.md` "The swing CTD, retargeted melee releases".
