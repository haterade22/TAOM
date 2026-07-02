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
| Lumber clips (staged, pending Kit-compile) | `E:\LOTRAOMAssets\troll_clips_to_import\troll_{walk,run}_lumber.fbx` — **NON-Armory**; user imports + Kit-compiles |
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
- 🟡 Troll-flavored MOVEMENT (first pass, pending Kit-compile): `troll_walk_lumber` + `troll_run_lumber`
  authored on `human_skeleton` (pelvis/spine sway ×1.4 for a heavier gait), staged armature-only at
  `E:\LOTRAOMAssets\troll_clips_to_import\` (**NON-Armory** — the user imports + Kit-compiles). Then 20
  `as_cave_troll_warrior` forward walk/run overrides (`act_{walk,run}_forward_{2h,2h_axe,polearm,1h,unarmed}`
  + each `_left_stance`) bind them; refine the look interactively (`/refine-creature-anim`) after the
  in-game look. **Authored anim FBXs stage OUTSIDE LOTRLOME_Armory until the user imports them** (standing rule).

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

- 2026-06-15 — `fix(troll-anim)`: fixed `rebuild_from_json` bone-offset collapse, authored first-pass lumbering walk/run, reverted the cave_troll skins back to `human_skeleton` + `lotr_troll_*` meshes.
- 2026-06-14 — `feat(troll-race)`: built the no-assimp keyframe pipeline (read tpac keyframes → JSON → rebuild in Blender → retarget → headless `ge_export`), producing `troll_walk_forward`/`troll_run_forward` autonomously.
- 2026-06-14 — `feat(troll-race)`: enabled the `cave_troll` as a live Mordor unit (`is_basic_troop="false"`, added to the Mordor party template) on `human_skeleton` reusing the full human anim set, and proved the bespoke-skeleton retarget pipeline end-to-end.
- 2026-06-13 — `feat(troll-race)`: adopted Auto-Rig Pro, proved the human→troll retargeting pipeline, and scaffolded the troll race (feature doc + authoring recipe).
- 2026-05-14 — Phase 9c: disabled troll content in-place (the `cave_troll` troop + two troll-themed careers) while preserving all artifacts for later re-enable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md)

<!-- backlinks-end -->
