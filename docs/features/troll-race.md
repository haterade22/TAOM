# Troll Race

## Overview

A **playable/NPC humanoid race** — the troll — built the same way as the human, dwarf, and orc races:
a big bipedal body skinned to a humanoid skeleton, using `monster_usage="human"` and a standard
(bipedal) action set. It is **NOT a rideable mount** — none of the mount machinery (rider sit-bone,
mount-lock, Horse item, behavior tree, `quad_movement` clips) applies. The troll walks, fights, dies,
and reacts on foot exactly like any humanoid, just at troll scale and proportions.

## Why this exists

LOTR's trolls are foot combatants/race units. Bannerlord already models non-human humanoids (orcs,
dwarves, etc.) as **races**, so the troll fits that mold rather than the creature-mount mold used for
the warg/spider/elephant. The animation challenge — a full combat/movement set on troll proportions — is
solved by **retargeting the human animation library** onto the troll's Auto-Rig Pro rig (see
[troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md)),
not by hand-authoring a quadruped gait set.

## The race data chain

```
race string id (engine FaceGen.GetRaceNames)
  -> Monster  (monster_usage="human", action_set=as_troll_warrior, female_action_set=...)
  -> skin     (skeleton + body/legs/hands/face meshes)
  -> action_set (as_troll_warrior, skeleton="<troll skeleton>", movement_system="bipedal")
  -> BodyProperty (build/age/height ranges)
  -> NPCCharacter troop(s) (race="troll")
```
TAOM's `RaceManager` (`Main/Core/Domain/RaceManager.cs`) caches the engine's race id↔name list via
`FaceGenAdapter` — **no TAOM C# change is needed** to use a race the engine already lists. Per-race
tweaks (e.g. eye-height) hook through `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs` +
`FaceGen_GetBaseMonsterFromRace_Patch` if the troll needs a height offset.

## Existing template: `cave_troll` / `hill_troll` (LOTRLOME_Armory)

LOTRLOME_Armory already ships two troll races — **the ready-made data template**:
- `cave_troll`: Monster `monster_usage="human"`, `action_set="as_cave_troll_warrior"`
  (`base_set="as_human_warrior"`), `skin skeleton="human_skeleton"`, mesh `lotr_troll_body`
  (+ feet/hands/head). Reuses human clips directly.
- `hill_troll`: same pattern but `skeleton="troll_skeleton"`, mesh `mordor_hill_troll`.
- TAOM has a `cave_troll` NPCCharacter (`troops_mordor.xml`) + `BodyProperty.fighter_cave_troll`
  (`TAOM_bodyproperties.xml`), **currently disabled** (2026-05-14).

> ⚠ Verify these entries against the live files before editing — paths/keys must be confirmed, not
> assumed. They are the copy-from template, not a spec.

## Skeleton + animation approach (this project)

**Current direction (updated 2026-06-14): the SHIPPING troll is `cave_troll` on `human_skeleton`** with
`as_cave_troll_warrior` (`base_set="as_human_warrior"`) — it inherits the full human animation set
(walk/run/attacks/death) for free, confirmed working in battle. Troll *flavor* is layered on top as
**movement overrides only**: lumbering walk/run clips authored on `human_skeleton` (so they play directly
— NO ARP retarget) and bound to the cave_troll's forward walk/run `act_*` codes. Attacks stay
engine-driven — Bannerlord battle melee is pose-blend, not standalone clips (the vanilla 2h attack clips
extract as 0-keyframe shells) — so custom clips can only ever change *movement*.

**The bespoke OWN-skeleton path (`skeleton_troll` + a full retargeted clip set, decided 2026-06-13) is
PARKED.** It works in principle (ARP retarget → `ge_export`; see the workflow doc) but adds a large clip
set + multiple Kit hand-offs for a humanoid whose attacks are engine-driven regardless. Revisit only if
the troll ever needs movement that human-skeleton overrides can't express.

**2026-09-18: the flavour source is the Fab "Cave Troll Lightweight" pack, not hand-authored lumber clips.**
All 52 of its clips are retargeted onto `human_skeleton` by `tools/blender/retarget_mannequin_to_human.py`, and the
two facts that made them play correctly in the Kit (author on the engine's own bone frames; the Kit turns the
root 180 degrees) are in [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md).
The earlier `troll_*_lumber` clips were authored on the TpacTool FBX rig, whose frames are not the engine's;
they were never Kit-proven and would fold, so they are superseded. `as_cave_troll_warrior` is no longer
empty: `tools/bind_troll_action_set.py` owns its 213 overrides.

## Race-authoring recipe

1. **Skeleton** — Kit-import the troll deform-skeleton FBX (ARP GE export of `troll_rig_01`) → the
   troll's game skeleton tpac. (Or reuse `human_skeleton` for the `cave_troll` fallback.)
2. **Meshes** — Kit-import `troll_hill_body_a` + cloth, skinned to that skeleton → `_geo.tpac` + materials.
3. **Animations** — retarget the human set → troll (workflow doc) → compile each → `_anm.tpac`.
4. **Action set** (`action_sets.xml`, LOTRLOME_Armory): `as_troll_warrior` (+ female / child / villager
   variants) with `skeleton="<troll skeleton>"`, `movement_system="bipedal"`, binding each standard
   human `act_*` to the troll clip. **No `quad_movement`, no mount/`act_horse_*` codes.**
5. **Monster** (`monsters.xml`): `id="troll"`, `monster_usage="human"`, `action_set="as_troll_warrior"`,
   `female_action_set=...`, humanoid bone block, troll weight/HP. Register via SubModule.xml XmlNode.
6. **Skin** (`skins.xml`): `<race id="troll">` → skeleton + the troll meshes.
7. **BodyProperty** (`TAOM_bodyproperties.xml`): troll build/height ranges (copy `fighter_cave_troll`).
8. **Troop(s)** (`troops_<culture>.xml`): NPCCharacter `race="troll"`, culture, equipment, level.
9. **Localization**: `{=race_troll}Troll` etc. through the 12-language pipeline.
10. **Validate**: `python tools/validate_moduledata.py`; enable in a Custom Battle.

## Key files
| Component | Path |
|---|---|
| ARP authoring rig | `E:\LOTRAOMAssets\troll_rig_01.blend` (rig + `troll_hill_body_a` + cloth; IK rig) |
| Retarget work scene | `E:\LOTRAOMAssets\troll_anim_WORK_20260613.blend` (has proven `troll_walk_forward`) |
| Retarget driver + map | `tools/blender/arp_retarget.py`, `tools/blender/bannerlord_human_to_troll.bmap` |
| Human-clip extractor | `tools/extract_human_anims_tpac.ps1` (tpac→FBX; **run under Windows PowerShell 5.1**) |
| Extracted source clips | `E:\LOTRAOMAssets\_troll_extract\` (e.g. `core\anim_walk_forward_unarmed.fbx`) |
| No-assimp clip pipeline | `tools/read_anim_keyframes_tpac.ps1` (tpac→JSON) + `tools/blender/rebuild_anim_from_json.py` (JSON→Blender; **transpose-fixed 2026-06-14**) |
| Lumber clips (SUPERSEDED 2026-09-18, authored on the mesh rig, would fold in the Kit) | `E:\LOTRAOMAssets\troll_clips_to_import\troll_{walk,run}_lumber.fbx` |
| Fab clip masters + clips (LIVE) | `LOTRLOME_Armory\Assets\creature\troll\animations\` (`troll_*_geo.tpac` masters on `human_skeleton`, `anim_troll_*_anm.tpac` clips); sources `AssetSources\creature\troll\animations\*.fbx` |
| Fab retarget tooling | `tools/blender/retarget_mannequin_to_human.py`, `tools/blender/human_skeleton_engine.json`, `tools/blender/fab_cave_troll_clip_names.json`, `tools/blender/fab_cave_troll_clip_measure.json`, `tools/blender/measure_fab_clip_roots.py` |
| Clip + action-set tooling | `tools/gen_troll_anim_clips.ps1`, `tools/tpac_fix_item_checksums.py`, `tools/check_rdc_entries.py`, `tools/bind_troll_action_set.py` |
| Retarget work scene | `E:\LOTRAOMAssets\_export\cave_troll_lightweight\cave_troll_retarget_WORK.blend` (all 52 actions on the engine rig) + `retarget_preview\` renders |
| Re-skinned LOME meshes | `LOTRLOME_Armory\AssetSources\Race Test\Mordor\Trolls\Cave Troll\LOME_troll.fbx`, `LOME_troll_armor.fbx`; QA renders `E:\LOTRAOMAssets\_troll_rig_out_20260918\preview\`; originals `E:\LOTRAOMAssets\_troll_rig_backup_20260918\` |
| Lumber work scene | `E:\LOTRAOMAssets\troll_lumber_WORK_20260614.blend` (`human_skeleton` + the 2 lumber actions) |
| Monster / skin / action_set | `<game>\Modules\LOTRLOME_Armory\ModuleData\{monsters,skins,action_sets}.xml` |
| BodyProperty | `Main/_Module/ModuleData/TAOM_bodyproperties.xml` (`fighter_cave_troll`) |
| Troop | `Main/_Module/ModuleData/troops/troops_mordor.xml` (`cave_troll`, **ENABLED 2026-06-14**) |
| Party template | `Main/_Module/ModuleData/taom_partyTemplates.xml` (`kingdom_hero_party_mordor_template`) |
| Race C# | `Main/Core/Domain/RaceManager.cs`, `Main/Features/HeroRace/` (no change needed) |

## Status / pending (updated 2026-06-14)

### Track 1 — working troll (SHIPPING)
- ✅ `cave_troll` race ENABLED: NPCCharacter uncommented in `troops_mordor.xml`
  (`is_basic_troop="false"` so vanilla `GetBasicVolunteer` can't pick the L51 troll as a Mordor
  *basic* recruit — the sole reason it was disabled 2026-05-14), added to
  `kingdom_hero_party_mordor_template` (0–2 per army). `validate_moduledata.py` PASS (NPC 4521→4522).
- ✅ Uses the full human animation library via `as_cave_troll_warrior` (`base_set="as_human_warrior"`)
  on `human_skeleton`. ALL assets (Monster/skin/action_set/meshes/items) already live in
  LOTRLOME_Armory — **nothing to import.** Zero C# changes (engine auto-lists the race via skins.xml).
- ✅ CONFIRMED in battle (2026-06-14): trolls spawn at ~1.9× scale, armored, wield 2h maces/spears/
  hammers/axes, fight + die correctly via the inherited human anim set — no T-pose, no underwear, no crash.
- ❌ SUPERSEDED (2026-09-18, see the Fab bullets below; authored on the mesh rig, would fold in the Kit): `troll_walk_lumber` + `troll_run_lumber`
  authored on `human_skeleton` (pelvis/spine sway ×1.4 for a heavier gait), staged armature-only at
  `E:\LOTRAOMAssets\troll_clips_to_import\` (**NON-Armory** — the user imports + Kit-compiles). Then 20
  `as_cave_troll_warrior` forward walk/run overrides (`act_{walk,run}_forward_{2h,2h_axe,polearm,1h,unarmed}`
  + each `_left_stance`) bind them; refine the look interactively (`/refine-creature-anim`) after the
  in-game look. **Authored anim FBXs stage OUTSIDE LOTRLOME_Armory until the user imports them** (standing rule).

- 🟡 **Fab clip set (2026-09-17, pending Kit-compile):** all 52 clips of the Fab "Cave Troll Lightweight"
  pack retargeted onto `human_skeleton` by `tools/blender/retarget_mannequin_to_human.py` and staged at
  `E:\LOTRAOMAssets\troll_clips_to_import\fab_cave_troll\` (`troll_<clip>.fbx`, in place) plus
  `fab_cave_troll_rootyaw\` (the 12 turn clips turning in place). Walk, run, two idles, 8 hit reactions,
  3 deaths, 8 turns, transitions and 6 roars are bindable to `as_cave_troll_warrior` codes; the 6 attack
  clips are not (melee is engine pose-blend). Visual check: side-by-side renders in
  `_export\cave_troll_lightweight\retarget_preview\`. Method and gotchas:
  [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) § The retarget stage.

- 🟡 **Fab clips COMPILED + clip metadata authored (2026-09-17 evening):** the 52 masters are in
  `LOTRLOME_Armory\Assets\creature\troll\animations\` (`troll_*_geo.tpac`, all on `human_skeleton`; 37 were
  wired by `tools/gen_troll_anim_clips.ps1`, 15 in the Kit) with 52 `anim_troll_*_anm.tpac` clips beside them,
  cloned from vanilla human clips per type: walk/run (`make_walk_sound` + `bip_mov_ik`, measured loop
  displacement 1.37 / 3.01 m, step points), idles (priority 1), 8 hits (priority 80, `strike_<dir>`), 3 deaths
  (priority 95, displacement +1.15 / -0.61 m), 7 attacks (`enforce_all` + `lock_movement`, priority 60), turns,
  transitions and 6 interactives. Names: `tools/blender/fab_cave_troll_clip_names.json`. NEXT: bind them in
  `as_cave_troll_warrior` (`action_sets.xml`, LOTRLOME_Armory) and Custom Battle smoke; the attack clips cannot
  replace melee, bind movement, idle, hit and death codes. **BEFORE the smoke:** none of the 104 troll tpacs
  has a `RuntimeDataCache` entry yet (`python tools/check_rdc_entries.py --under creature/troll`), so the
  client would skip them all; open the Armory in the Kit and save, then rerun the check until it prints 0.

- OK **Fab clips PLAY in the Kit (2026-09-18) and are bound:** three measured Kit rounds fixed the bone frames
  (author on the engine's own `human_skeleton` frames) and the root yaw (turn the pose, not the node), see
  [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md) 2026-09-18. `tools/bind_troll_action_set.py`
  wrote 213 overrides into `as_cave_troll_warrior` (walk/run/idle/strike/death codes; turns, strafes, attacks
  inherit human), parity audit OK, snapshot refreshed. OWED: Custom Battle with Mordor trolls; provenance row.
  (The Kit save "for the 52 masters' RDC entries" owed here was a false target: the Kit never writes an entry
  for an animation master and masters play without one; `check_rdc_entries.py` now counts them separately and
  prints 0 for `creature/troll`. The junk `human_skeleton_notused.00x` skeletons are gone.)
  **2026-09-18 pm (Artem):** frame 0 of every export is now the REST frame (the Kit zeroes the root position
  track at frame 0; posed frame 0 sat the troll 9 cm too high, feet skating). The 52 FBX in the Armory sources
  are the frame-0 build: REIMPORT them, then regenerate the clips (`gen_troll_anim_clips.ps1`, masters are one
  frame longer); the Kit cooks the new clips' RDC entries when it next loads.
  **Reimport done 10:55 to 10:58:** 51 masters kept their GUIDs (Duration +1, so `-Verify` shows 51 STALE clips);
  `troll_danger_idle_hit_front_0_geo.tpac` came back as Skeleton (`human_skeleton_notused.001`) + Geometry with
  no animation, and the Kit reports "assigned skeleton animation not found" on `anim_troll_combat_hit_front1`.
  Fixed 11:09 (junk skeleton deleted, FBX imported again as an animation, new master GUID `c9368a32`, Skeleton
  empty until `-Apply` patched it). 11:12, Kit closed: the 52 old clips moved to
  `E:\LOTRAOMAssets\_troll_clips_backup_20260918_1112\`, `-Apply` wrote 52 new ones, `-Verify` prints
  `ok=52 stale=0 orphan=0`, checksums 0 stale. All 104 troll packages lack RDC entries until the next Kit save.

- OK **LOME cave troll set RE-SKINNED (2026-09-18):** `tools/blender/reskin_to_human_skeleton.py` transferred
  TaleWorlds' body weights onto `lotr_troll_body/feet/hands/head` and `lotr_troll_armor/bracers/helmet` (42 meshes
  with LODs). Before: 115 un-normalised + 9 over-4-influence vertices on the body, 1,836 un-normalised on the head.
  After: 0/0/0 everywhere. Edge-stretch QA (p99, donor / old / new): body shoulder 1.52 / 2.40 / 1.73, knee 1.13 /
  1.47 / 1.26, head neck 1.24 / 2.92 / 1.12, feet knee 1.09 / 1.21 / 1.11, helmet and gorget rigid. Files in
  `LOTRLOME_Armory\AssetSources\Race Test\Mordor\Trolls\Cave Troll\` (originals + compiled tpacs backed up under
  `E:\LOTRAOMAssets\_troll_rig_backup_20260918\`). OWED: Kit reimport of `LOME_troll.fbx`, import of
  `LOME_troll_armor.fbx`, in-game look. Hill troll: 19.5 cm mean joint offset and 1.3 to 2.3x proportions vs the
  human rest, so it stays on `troll_skeleton` unless Mike accepts a conformed silhouette; Fab troll on its own
  proportions is a separate job. The hill troll's head and mouth materials are missing in the Kit
  (`mordor_hill_troll_head`, `t_hilltroll_mouth`, "Unable to find material" in every Kit session of 2026-09-18).
  **In game (2026-09-18, 15:47):** cave trolls fought on `as_cave_troll_warrior`, 2,982 blows taken, 19 deaths, no
  clip or material warning; Mike confirmed the Fab animations play.
  **Materials (2026-09-18 pm):** the reimport of `LOME_troll.fbx` warned "Unable to find material lotr_troll_head"
  on the six head meshes. The original FBX already named that material and the Kit never had it; the March import
  had been reassigned to `base_body_olog` by hand, which a reimport resets. `tools/blender/fbx_remap_materials.py
  --map lotr_troll_head=base_body_olog --apply` fixed it in the source (6 slots, weights and bones unchanged; old
  file `LOME_troll.fbx.bak-matremap`), so every reimport now binds `base_body_olog`. **Armor textures:**
  `LOME_troll_armor.fbx` names `lotr_troll_chains`, `lotr_troll_helmet`, `lotr_troll_pants`,
  `lotr_troll_plate_armour`, none of which the live Armory had. The source PNGs were found in an older Armory
  copy, `E:\Safee\LOTRLOME_Armory\AssetSources\troll\` (2024-12-11, d/n/s for all four, 2048). Copied into
  the Cave Troll `textures\` folder and resized to diffuse 1024, normal 512, specular 512 (Mike's sizes) with
  `E:\taom-texture-backup-2026-09-13\resize_textures.py`; 2K originals in that backup tree.
  The old compiled `_tex`/`_mtl` tpacs in `Safee` were NOT copied (about 480 bytes each, no pixel data, GUIDs from
  another tree). Mike creates the four materials in the Kit (names must match the FBX slots), then imports the armor.

### Track 2 — bespoke retargeted set (pipeline proven; source step is a Kit/UI hand-off)

> ⚠ **CORRECTION (2026-06-14):** the "PROVEN / 72 fcurves" claims below were validated by an fcurve
> COUNT, not a visual check. `rebuild_from_json` had a row-major/column-major bug that read every bone
> offset as 0 and **collapsed the rebuilt clip onto the root** (on both `human_skeleton` and
> `skeleton_troll`). FIXED 2026-06-14 — `.transposed()` on the rest matrix in `rebuild_anim_from_json.py`;
> post-fix a rebuild yields a clean grounded standing gait. ALWAYS screenshot the rebuilt pose. The clips
> ge_exported to the Hill Troll `clips/` folder before the fix are collapsed garbage — don't compile them.

- ✅ Extraction PROVEN: human clip `animations.tpac` → FBX via `tools/extract_human_anims_tpac.ps1`
  (the human skeleton lives in `EmAssetPackages/human/human.tpac`, NOT skeletons.tpac; meshed export
  with materials cleared so Blender builds an animated armature). One walk imported with 269 fcurves.
- ✅ Retarget PROVEN: human walk → troll ARP rig, 72 moving fcurves, clean run (`troll_walk_forward`
  in `troll_anim_WORK_20260613.blend`). `arp_retarget.py` carries 4 hard-won fixes (see workflow doc).
- ⚠ Headless *extraction* is UNRELIABLE: TpacTool's assimp `ExportSceneToBlob` access-violates
  (`0xC0000005`) when driven from pwsh / PowerShell (both .NET 10 and .NET Framework). It worked
  for a few single exports then crashed deterministically. **Source clips via the Modding Kit's
  resource exporter** (stable — runs assimp in its own process), or retry TpacTool when it cooperates.
- ✅ **No-assimp clip pipeline WORKS (PROVEN 2026-06-14)** — the bypass for the assimp crash. Read clip
  keyframes via `tools/read_anim_keyframes_tpac.ps1` (TpacTool.Lib → JSON, no assimp) → rebuild on the
  human armature via `tools/blender/rebuild_anim_from_json.py` → retarget → `ge_export`. End-to-end:
  rebuilt walk → 72 fcurves (= FBX path); `troll_run_forward` produced from JSON alone (it crashed assimp).
  Exported `troll_walk_forward.fbx` + `troll_run_forward.fbx` to
  `LOTRLOME_Armory/AssetSources/Race Test/Mordor/Trolls/Hill Troll/clips/`. Repeatable per-clip, autonomous.
- ✅ Skeleton GE export PROVEN HEADLESS (`ge_export()` in `arp_retarget.py`): exported `skeleton_troll`
  (**30 deform bones, no IK/control bones**) + skinned body to
  `E:\LOTRAOMAssets\_troll_extract\troll_skeleton_only.fbx` (rest pose, for the skeleton definition)
  and `troll_skeleton_export.fbx` (skeleton + the `troll_walk_forward` clip). **Ready for Kit import.**
- ⏳ Remaining (Kit GUI + data): Kit-compile the skeleton FBX → `skeleton_troll` tpac (+ clips) →
  author `as_troll_warrior` (`skeleton="skeleton_troll"`, `movement_system="bipedal"`) + a `<race>` skin
  → enable a troop with that race → Custom-Battle test. (IK editing stays on the Blender ARP rig.)

> **Game skeletons have NO IK joints.** IK lives only in the Blender ARP authoring rig
> (`troll_rig_01.blend`: `c_foot_ik.l`, `c_hand_ik`, …) and is baked into the animation on export;
> ARP GE export strips control/IK bones, leaving a deform-only skeleton. `cave_troll` uses the stock
> `human_skeleton` (no custom troll skeleton); `hill_troll` references a custom `troll_skeleton`
> (deform skeleton; bone structure not yet inspected).

## See also
- [troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md) — the HOW.
- [hero-race.md](hero-race.md) — TAOM race system (id mapping, camera/eye-height, persistence).

## Changelog

- 2026-09-18, `feat(troll-anim)`: the Fab clips play in the Kit (author on the engine frames + pose-baked root yaw, three measured Kit rounds), `bind_troll_action_set.py` wrote 213 `as_cave_troll_warrior` overrides, `gen_troll_anim_clips.ps1` authored the 52 `anim_troll_*` clips from vanilla templates, LOME cave troll set re-skinned from TaleWorlds' weights (`reskin_to_human_skeleton.py`). Hill troll measured (19.5 cm joint offsets) and left on `troll_skeleton`.
- 2026-09-17, `feat(troll-anim)`: Fab "Cave Troll Lightweight" pack exported from UE 5.4 (`ue_export_cave_troll.py`), all 52 clips retargeted onto `human_skeleton` (`retarget_mannequin_to_human.py`), staged for Kit import.
- 2026-06-15 — `fix(troll-anim)`: fixed `rebuild_from_json` bone-offset collapse, authored first-pass lumbering walk/run, reverted the cave_troll skins back to `human_skeleton` + `lotr_troll_*` meshes.
- 2026-06-14 — `feat(troll-race)`: built the no-assimp keyframe pipeline (read tpac keyframes → JSON → rebuild in Blender → retarget → headless `ge_export`), producing `troll_walk_forward`/`troll_run_forward` autonomously.
- 2026-06-14 — `feat(troll-race)`: enabled the `cave_troll` as a live Mordor unit (`is_basic_troop="false"`, added to the Mordor party template) on `human_skeleton` reusing the full human anim set, and proved the bespoke-skeleton retarget pipeline end-to-end.
- 2026-06-13 — `feat(troll-race)`: adopted Auto-Rig Pro, proved the human→troll retargeting pipeline, and scaffolded the troll race (feature doc + authoring recipe).
- 2026-05-14 — Phase 9c: disabled troll content in-place (the `cave_troll` troop + two troll-themed careers) while preserving all artifacts for later re-enable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md)
- [docs/modding/body-properties.md](../modding/body-properties.md)
- [docs/modding/recipe-add-a-race-or-creature.md](../modding/recipe-add-a-race-or-creature.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
