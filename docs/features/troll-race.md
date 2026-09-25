# Troll Race

## Overview

A **playable/NPC humanoid race** — the troll — built the same way as the human, dwarf, and orc races:
a big bipedal body skinned to a humanoid skeleton, using `monster_usage="human"` and a standard
(bipedal) action set. It is **NOT a rideable mount** — none of the mount machinery (rider sit-bone,
mount-lock, Horse item, `quad_movement` clips) applies. The troll walks, fights, dies, and reacts on foot
exactly like any humanoid, just at troll scale and proportions. The one creature-style layer is the Brute
Force smash (#649): a behaviour tree per troll, `Main/Features/TrollBruteForce/`, on both trolls.

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
- `hill_troll`: since 2026-09-24 on its own skeleton `troll_skeleton_a` with KEYForce's `hill_troll_a_*` meshes
  and a standalone `as_hill_troll_warrior`, the dwarf's layout (the 2026-09-24 entries below). Before that
  `skeleton="troll_skeleton"`, mesh `mordor_hill_troll`.
- TAOM has `cave_troll` (enabled 2026-06-14, after the 2026-05-14 disable) and `hill_troll` (2026-09-25)
  NPCCharacters in `troops_mordor.xml`, both on `BodyProperty.fighter_cave_troll` (`TAOM_bodyproperties.xml`).

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

**The hill troll took the own-skeleton route after all (2026-09-24), without ARP:** KEYForce's rig
`troll_skeleton_a` (the human's 28 bone names) is turned to the human's bone axes on export, so every human clip
bends it correctly; the human ragdoll, IK joints and hit capsules are copied through the bone frames; and the 52
Fab clips are retargeted onto its engine dump with a two-bone leg IK. A human clip on it bends about the right
axes but keeps the human's rest relations, so the head pitches up and the wrists twist (a clip stores
parent-relative rotations); the actions the troll plays are therefore retargeted from the human masters as well,
with the Fab clips reused for the rest. The entries dated 2026-09-24 under Track 1 are the record.

**2026-09-18: the flavour source is the Fab "Cave Troll Lightweight" pack, not hand-authored lumber clips.**
All 52 of its clips are retargeted onto `human_skeleton` by `tools/blender/retarget_mannequin_to_human.py`, and the
two facts that made them play correctly in the Kit (author on the engine's own bone frames; the Kit turns the
root 180 degrees) are in [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md).
The earlier `troll_*_lumber` clips were authored on the TpacTool FBX rig, whose frames are not the engine's;
they were never Kit-proven and would fold, so they are superseded. `as_cave_troll_warrior` is no longer
empty: `tools/bind_troll_action_set.py` owns its 213 overrides.

## Race-authoring recipe

1. **Skeleton:** Kit-import the skeleton WITH its skinned meshes as one FBX
   (`tools/blender/export_rig_for_kit.py --bone-frames <reframe JSON>`, the artist's bones turned to the
   human's axes by `tpac_skeleton_copy_physics.py --reframe`, grip bones added), then copy the human
   physics into the package (`tpac_skeleton_copy_physics.py --reframed <JSON> --fit`). The hill troll is
   the worked example. (Or reuse `human_skeleton` for the `cave_troll` fallback.)
2. **Meshes:** in that same FBX, one object per skin slot, a head as `<mesh>` / `.eye` / `.mouth`, materials
   under the Kit's exact names → `_geo.tpac`.
3. **Animations:** retarget a clip set onto the skeleton's engine dump
   (`retarget_mannequin_to_human.py --engine-skeleton <json> --armature-name <skeleton>_notused`), Kit-import
   the FBX, wire the masters (`wire_anim_master_skeletons.ps1`), then `gen_troll_anim_clips.ps1` with
   `-TravelScale` = the retarget report's `pelvis_scale` → `_anm.tpac`. On a re-framed rig human clips bend
   about the right axes but keep the human's rest relations: retarget the ones the race plays
   (`retarget_mannequin_to_human.py --source-json --source-rig human`).
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
| Hill troll rig (LIVE) | `LOTRLOME_Armory\AssetSources\Race Test\Mordor\Trolls\hill_troll_a\hill_troll_a.fbx` (from KEYForce's `troll_rig_base_01.blend` via `tools/blender/export_rig_for_kit.py`), package `Assets\...\hill_troll_a\hill_troll_a_geo.tpac` (skeleton `troll_skeleton_a`); ledger [lotrlome-hill-troll-changes.md](../reference/lotrlome-hill-troll-changes.md) |
| Hill troll clip masters + clips (LIVE) | `LOTRLOME_Armory\Assets\Race Test\Mordor\Trolls\animations\` (`anim_hill_troll_*_geo.tpac` masters on `troll_skeleton_a`, `anim_hill_troll_*_anm.tpac` clips); sources `AssetSources\Race Test\Mordor\Trolls\animations\*.fbx`, staged from `E:\LOTRAOMAssets\troll_clips_to_import\fab_hill_troll_v5\` |
| Hill troll retarget inputs | `tools/blender/troll_skeleton_a_engine.json` (engine dump of the re-framed skeleton), `tools/blender/fab_hill_troll_clip_names.json`; physics and re-frame: `tools/tpac_skeleton_copy_physics.py`, `tools/skeleton_hit_capsules.py`; race wiring `tools/wire_hill_troll_race.py` |
| Clip + action-set tooling | `tools/gen_troll_anim_clips.ps1` (`-CloneByName`, `-Renames`, `-RetargetReport`), `tools/read_anim_keyframes_tpac.ps1 -ByClip`, `tools/tpac_fix_item_checksums.py`, `tools/check_rdc_entries.py`, `tools/bind_troll_action_set.py` (cave troll), `tools/bind_hill_troll_action_set.py` (hill troll: Fab, retargeted, reused idles), `tools/blender/hill_troll_clip_renames.json` (the 15 names over 63 characters) |
| Retarget work scene | `E:\LOTRAOMAssets\_export\cave_troll_lightweight\cave_troll_retarget_WORK.blend` (all 52 actions on the engine rig) + `retarget_preview\` renders |
| Re-skinned LOME meshes | `LOTRLOME_Armory\AssetSources\Race Test\Mordor\Trolls\Cave Troll\LOME_troll.fbx`, `LOME_troll_armor.fbx`; QA renders `E:\LOTRAOMAssets\_troll_rig_out_20260918\preview\`; originals `E:\LOTRAOMAssets\_troll_rig_backup_20260918\` |
| Lumber work scene | `E:\LOTRAOMAssets\troll_lumber_WORK_20260614.blend` (`human_skeleton` + the 2 lumber actions) |
| Monster / skin / action_set | `<game>\Modules\LOTRLOME_Armory\ModuleData\{monsters,skins,action_sets}.xml` |
| BodyProperty | `Main/_Module/ModuleData/TAOM_bodyproperties.xml` (`fighter_cave_troll`, both trolls) |
| Troop | `Main/_Module/ModuleData/troops/troops_mordor.xml` (`cave_troll`, **ENABLED 2026-06-14**; `hill_troll`, 2026-09-25) |
| Party template | `Main/_Module/ModuleData/taom_partyTemplates.xml` (`kingdom_hero_party_mordor_template`, 0..7 each; the Bolgrûkig, Zarûnik and Brughash templates `..._empire_south_{5,13,15}_template`, 0..2 each) |
| Brute Force smash (#649) | `Main/Features/TrollBruteForce/` (`TrollBruteForceConfig.ActionSetsByMonster`, `TrollBruteForceService.BodySize`); the action in the Armory's `action_types.xml`, bound in both troll sets |
| Health | `Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json` (`baseHitPoints` 200) read by `TaomCharacterStatsModel` (campaign); the Armory Monsters' `hit_points` (Custom Battle) |
| Special resources | `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` (recruit 50 War Spoils, upkeep 5 and 4) |
| Other troop wiring | `Main/Features/SettlementGuards/SettlementGuardService.cs` (no guard duty), `troop_weights.xml` (4.0), `CharacterAvatarPatch.json` (encyclopedia framing), `tools/taom_schema.py` (`_BODYLESS_BY_DESIGN`, `_ARMOUR_LADDER_EXEMPT`), `tools/melee_ladders.json` (exempt) |
| Race C# | `Main/Core/Domain/RaceManager.cs`, `Main/Features/HeroRace/` (no change needed) |

## Tests

| What | Where |
|---|---|
| Brute Force decisions, body size, config, both sets bound in the live Armory | `TAOM.Tests/Features/TrollBruteForce/` (`TrollBruteForceWiringTests` is `LiveInstall`) |
| 200 health: config, resolver, provider bounds; the Monsters agree with the config | `TAOM.Tests/Features/CombatMechanics/` (`RaceCombatModifiersResolverTests`, `CombatMechanicsConfigProviderTests`, `ShippedCombatMechanicsConfigTests`, `TrollHitPointsLiveDataTests` `LiveInstall`) |
| Resource costs, party templates at 260, guard exclusion, face coverage | `TroopResourceCostDataTests`, `ShippedLordPartyTemplateTests`, `SettlementGuardServiceTests`, `CharacterFaceCoverageTests` |
| Armory race wiring (reinstall gate) | `python tools/wire_hill_troll_race.py --check`; `tools/tests/test_wire_hill_troll_race.py` |
| Action set body and the reused idles | `tools/tests/test_bind_hill_troll_action_set.py`; `python tools/audit_action_set_parity.py` |
| Clips on disk | `gen_troll_anim_clips.ps1 -Verify` and `-CloneByName -Verify` (no unit harness) |

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
  inherit human), parity audit OK, snapshot refreshed. OWED: Custom Battle with Mordor trolls. (The provenance
  row is cleared, 2026-09-23: bought on Fab, and Mike records no creator or licence tier for Fab purchases.)
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

- **Cave troll jaw fix (2026-09-24), Kit reimport owed:** Mike saw the mouth and the skin around it stretched open
  in the Kit ("Seems like the mouth is rigged to a neck bone?"). Measured in the re-skinned FBX: 201 skin vertices
  of `lotr_troll_head`, the underside of the jaw 16 to 32 cm in front of the neck joint, were dominated by `spine2`
  (up to 0.75), and nothing on the head or body followed `neck`. `rigid_skull` made only vertices ABOVE the neck
  joint's height rigid on `head`; a troll's jaw hangs at or below it, and the nearest human surface there is the
  chest, so the chin stayed with the chest when the head moved. The 2026-09-18 QA passed it: its one neck bend
  nodded forward (max stretch 2.05 against the donor's 1.77) and nothing judged compression. The tool now makes the
  jaw rigid too (`rigid_jaw`, 14 cm in front of the neck joint) and gives a head mesh only `head` and `neck` weight,
  QA nods both ways, and `.DONE` says "fail" if a head mesh keeps any other weight. Re-run from the original FBX:
  the head carries 7,131 vertices on `head` and 18 throat vertices on `neck`, none on the chest; nod QA max 1.66
  forward and 1.36 back (donor 1.77); body, hands and feet identical to the 2026-09-18 weights. Head material
  remapped again (`fbx_remap_materials.py`). Output `E:\LOTRAOMAssets\_troll_rig_out_20260924\LOME_troll.fbx`,
  copied over `AssetSources\Race Test\Mordor\Trolls\Cave Troll\LOME_troll.fbx` at 11:58 (Mike: "You can update
  the FBX file in the folder"; the old one is `LOME_troll.fbx.bak-jawfix-20260924` beside it). OWED: Mike reimports
  it in the Kit and checks the jaw. Still unexplained in the same screenshots: a pale, bald patch at the back and top of the
  skull, not part of the head mesh's three materials.
- **Hill troll moved onto its own skeleton, `troll_skeleton_a` (2026-09-24), the dwarf model:** KEYForce's final
  delivery (`E:\LOTRAOMAssets\drive-download-20260924T173959Z-1-001\`, `troll_rig_base_01.blend` plus textures) is a
  3.6 m troll skinned to `troll_skeleton_a`: 26 bones with the human's names and order (no `l_finger0`/`r_finger0`),
  each bone along its local +Y, rolled 180 to 280 deg off `human_skeleton`'s, 1.8 to 4.9 times as long. Mike's calls:
  keep the artist's frames (clips get authored for this skeleton, human ones reused where none exist), one mesh per
  skin slot, copy the dwarf, textures at 1K. `tools/blender/export_rig_for_kit.py` wrote `hill_troll_a.fbx` (armature
  under its real name; slots `hill_troll_a_body` 5,562 vertices, `_head` 2,099, `_hands` 1,136, `_legs` 2,094; bones
  back from the round trip within 0.001 mm and 0 deg); six 2048 maps went to 1024 (originals under
  `E:\taom-texture-backup-2026-09-13\`). Mike imported it into `LOTRLOME_Armory\Assets\Race Test\Mordor\Trolls\hill_troll_a\`
  (`hill_troll_a_geo.tpac`, source `AssetSources\...\Trolls\hill_troll_a\`), with Kit materials
  `t_tr_hill_troll_{body,eye,head,cloth,hammer}_a_mtl`. The import left the skeleton `Usage` `other`, 26 empty
  bodies, no joints. `tools/tpac_skeleton_copy_physics.py` then carried the human's physics through the bone frames
  (a verbatim copy, the dwarf's way, would have laid every capsule across its limb) with hit capsules fitted to the
  skin (`skeleton_hit_capsules.py fit --axis bone`) and ragdoll radii sized from them: `Usage` `human`, 26 bodies,
  34 joints (15 d6, 19 ik), 99.3% of the skin inside a hit capsule, every joint at the human's angle to its bone
  except the ankle's ik twist (17 deg off the leg against 6). Checked on a scratch copy first, with the old dump
  parser, a world-space comparison and the coverage re-measure; the live package is byte-identical to that copy
  (backup `hill_troll_a_geo.tpac.bak-physics-20260924-135228`). Procedure and the three engine facts behind it:
  [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md) "Ragdoll, IK and hit capsules
  for a humanoid on its own skeleton".
  **Materials (2026-09-24 pm):** the first import bound the meshes to `m_hilltroll_{body,cloth,eye,head}_a`, the
  lowercased FBX names, and materials by those names already existed: the OLD hill troll's, from March, in
  `Trolls\Hill Troll\textures\`, on the old `T_HillTroll_*` textures. So the new model wore old textures with no
  warning, and the `t_tr_hill_troll_*_a` materials made for it went unused. KEYForce's second `.blend`
  (`E:\LOTRAOMAssets\drive-download-20260924T190230Z-1-001\`) renamed `M_HillTroll_Head_A` to `M_HillTroll_Mouth_A`
  (same `T_HillTroll_Head_A_*` textures, head base and mouth) and deleted 11 unused materials; geometry, weights and
  every bone's rest are unchanged. The re-export names the Kit materials directly
  (`export_rig_for_kit.py --material`: Body to `t_tr_hill_troll_body_a`, Cloth to `_cloth_a`, Mouth to `_head_a`, Eye to
  `_eye_a`), same vertex counts, bones within 0.001 mm; installed over the Armory source (old one
  `hill_troll_a.fbx.bak-kitmaterials-20260924`). Mike re-imported it at 14:29: every metamesh now names a
  `t_tr_hill_troll_*_a` material, and the skeleton physics came through byte-identical, so nothing was re-run. But
  the head came in as `head.0` (head base plus mouth, one material) and `head.1` (eyes), with no mouth sub-mesh to
  tag. The Kit makes one named sub-mesh per FBX object called `<mesh>.<part>` (the dwarf's `_head`, `_head.eye`,
  `_head.mouth`, tagged `face_base_mesh`, `face_eye_mesh`, `face_mouth_mesh`) and `.0`/`.1` only for one object with two
  materials; the export had joined the three head objects. Re-exported at 14:38 as `hill_troll_a_head` (962
  vertices), `hill_troll_a_head.eye` (194) and `hill_troll_a_head.mouth` (943, on the head material, as KEYForce set
  it), installed over the source (previous one `.bak-mouthsplit-20260924`). Mike re-imported, tagged and saved (14:39): the head
  metamesh holds `hill_troll_a_head` (`face_base_mesh`), `.eye` (`face_eye_mesh`) and `.mouth` (`face_mouth_mesh`),
  every mesh names its `t_tr_hill_troll_*_a` material, the skeleton physics is still byte-identical to the 13:52
  write, and the package has a fresh `.rdc` (14:38, a minute before the tag save). Mike's look in the Kit's
  skeleton view: ragdoll capsules inside the body, hit capsules around it, IK joints present, "Looking good". The
  in-game checks still owed are a hit test and a corpse falling. Next: the race wiring (skin on `troll_skeleton_a`, Monster, a standalone
  action set naming it), clips retargeted onto `troll_skeleton_a`, the hammer as an item, then the cave troll.
- **Hill troll race wired to `troll_skeleton_a` (2026-09-24 pm), step 2:** Mike: point the existing `hill_troll`
  race at the new skeleton and meshes, like the dwarf; Monster resized, no riding; all ten skins; grip bones added.
  The rig had no `l_finger0`/`r_finger0`, the bones the Monster hangs held items on (clips do not need them;
  weapons do), so the export adds them, carried from the human hand and lowered 0.22 m by KEYForce to sit in the
  fist. KEYForce's shoulder piece duplicates the body's surface (every vertex 0 mm from it): it is the
  `body_meta_mesh_shoulders` mesh and now exports as `hill_troll_a_shoulder` instead of doubling the shoulders
  inside the body. Physics re-run over the 28-bone skeleton (99.5% of the skin in a hit capsule). Live
  `skins.xml`, `monsters.xml` and `action_sets.xml` edited by the new `tools/wire_hill_troll_race.py` plus
  `patch_dwarf_action_parity.py` (4,700 actions in a standalone `as_hill_troll_warrior`); the Monster's four
  variants renamed to the `hill_troll_*` ids the engine looks up. Validators: engine XSD pass on `monsters.xml`,
  `audit_action_set_parity.py` 0 gaps over 1,304 humanoid sets (the hill troll root now counted), ModuleData 0
  errors. Every edit, backup and redo step: [lotrlome-hill-troll-changes.md](../reference/lotrlome-hill-troll-changes.md);
  the tracked Armory snapshot carries them. OWED: Mike re-imports the FBX with the lowered grips, I re-check the
  physics, a Kit save for the `.rdc`, then the first in-game look (the troll plays human clips on its own skeleton
  until step 3).
- **Re-framed to the human's axes, and the 52 Fab clips retargeted (2026-09-24 evening), step 3:** Mike previewed
  the human `guard_up_2h` on the hill troll in the Kit: arms, shoulders and head twisted. The engine plays a clip's
  joint rotations on a skeleton as they are, and `troll_skeleton_a`'s bones were rolled 180 to 280 deg off the
  human's (the dwarf's are within about 35). Mike chose to re-frame on export over retargeting every human clip:
  `tpac_skeleton_copy_physics.py --reframe` turns every bone to the human's anatomical axes, heads kept (the mesh
  and weights need nothing), and `export_rig_for_kit.py --bone-frames` applies it with the grips (KEYForce's -0.22 m).
  The 16:03 Kit import matches the record exactly (0.000 deg, 0.16 mm), 28 bones in human order, bone axis +X.
  Physics re-run with `--reframed` (maps by the identity; the plain rule would have turned the wrists 24 to 30
  deg because the hands now have off-axis grip children): 99.4% of the skin in a hit capsule, all 34 joints within
  0.03 deg of the human's. `retarget_mannequin_to_human.py` onto `tools/blender/troll_skeleton_a_engine.json`
  (`--armature-name troll_skeleton_a_notused`, `--ref-clip Cave_Troll_free_idle_0.fbx`): all 52 Fab clips as
  `anim_hill_troll_*` in `E:\LOTRAOMAssets\troll_clips_to_import\fab_hill_troll\`, every frame 0 at rest (0.0 deg),
  previews matching the Fab troll pose for pose (idle, attack, walk, hit, run start; the first death crouches a
  little less than the source). Mike imported the 52 into `LOTRLOME_Armory\Assets\Race Test\Mordor\Trolls\animations\`
  (52 masters, all with an EMPTY skeleton); `wire_anim_master_skeletons.ps1 -SkeletonGuid 7516b03c-...
  -BoneNum 28` pointed all 52 at `troll_skeleton_a` (the folder holds only these, since the human is 28 bones too;
  `.bak-preskel` beside each). `gen_troll_anim_clips.ps1` (new `-SkeletonGuid`, `-ClipPrefix`, `-TravelScale`; the
  cave troll's dry run byte-identical after the change) wrote the 52 `anim_hill_troll_*_anm.tpac` clips, strides at
  troll size (walk 2.73 m per loop, 1.74 m/s; run 5.34 m); `-Verify` 52 ok, 0 stale, 0 orphan; checksums refreshed.
  OWED: a Kit load and save (the 52 clips have no `.rdc` entry yet), a Kit preview of a Fab clip and of a human one
  on the re-framed troll, binding into `as_hill_troll_warrior`, then the in-game look.
- **Fab clips re-retargeted: wrists, feet, root height, leg IK (2026-09-24, late):** Mike saw a twisted wrist on
  `run1` and `walk_to_run` in the Kit and asked for a loop over the animations. Four fixes to
  `retarget_mannequin_to_human.py`, each kept only on measurements against the Fab source (the probes live in
  `E:\LOTRAOMAssets\_hill_troll_a_export\review_20260924b\`: feet per bone against the clip's first frame, per-frame
  world rotation steps, loop seams, planted-foot travel, root height per frame). (1) The Mannequin twist bones drive
  the human `*_twist1` helpers: `run1`'s wrist twist fell from 172 to 82 deg with 90 on the forearm, the source's
  own split. (2) The feet keep their own rest pitch (`NO_ALIGN`): aligned to the Fab foot's steeper ankle-to-ball
  line (41 deg down against the troll's 21) they pitched toe-down and the toes sank up to 17 cm under held ankles.
  (3) A two-bone leg IK (`--no-leg-ik` for the A/B) puts each ankle on the source ankle's path scaled by the thigh +
  calf ratio 1.5578, anchored on the Fab's bind stance from the troll's hip: the troll's rest feet stand 35 cm in
  front of its hips and the Fab's 19 cm behind at scale, so anchored on the troll's own rest ankle a forward step
  asked 19% more than the leg's length and the IK missed by up to 37 cm; the knee bends toward its rest pole
  carried by the thigh and the calf follows the thigh's correction before it is aimed (the retargeted knee's plane
  flipped near a straight leg: 63 deg one-frame jumps in `run_to_heavy_attack`, 28 in `hit_right1`, against the
  source's 15 and 14). (4) The UE export clamps the pelvis at its bind height 1.181 m and parks the excess on the
  root node (`danger_run_0` holds the pelvis flat for six frames while the root rises 6.5 cm; `danger_attack_1`
  12.9 cm; 21 of 52 clips), so an in-place retarget that drops root motion flattens every bob and sinks the body by
  the cut: `_root_height_matrix` keeps the root's Z and drops only travel and yaw. Over all 52: feet within 2 cm of
  the source's scaled lowest point (they were 19 to 29 cm under, 21 cm even in idle), no single-frame step beyond
  the source's own, the six loops close at 0 deg and 0 m, frame 0 at rest, the mid-frame previews pose for pose.
  The v5 FBX set replaced the Armory sources in `AssetSources\Race Test\Mordor\Trolls\animations\` (the approved
  first set is backed up at `E:\LOTRAOMAssets\_hill_troll_a_export\anim_fbx_backup_20260924_1610\`). The clip
  generator's `-TravelScale` must be the report's `pelvis_scale`: the planted foot slides 2.71 m per `combat_walk1`
  loop against the source's 1.74, ratio 1.558, so the first run's 1.377 would have skated the feet 12%, and the 52
  `_anm.tpac` clips are regenerated at 1.5578 after the re-import. OWED: Mike's Kit re-import and save,
  `wire_anim_master_skeletons.ps1` (masters may come back EMPTY), delete and regenerate the clips, `-Verify`, the
  Kit look at wrists and feet. The cave troll's shipped Fab set came from the older tool and carries the same
  clipped bobs: a separate decision, not made here.
- **Human clips for the hill troll (2026-09-24, later):** after the re-import Mike found the wrists still twisted
  and a neck "straight up": on the cave troll's `anim_troll_*` clips, which are `human_skeleton` clips. Measured
  against `human_skeleton_engine.json`: the troll's rest bone DIRECTIONS sit 54 deg (spine2), 45 (neck) and 42
  (thigh) off the human's and its head and hand rest ORIENTATIONS 65 and 20 deg off. The re-frame matched axes,
  not relations, and a clip stores parent-relative rotations, so every human clip lands each bone at the human's
  world orientation turned by its parent's rest difference: head up 45 to 65 deg, wrists twisted 20. That is every
  action the Fab set lacks, including the engine's melee. Mike's decision: create the clips the troll needs and
  reuse the Fab clips for specific actions, not 4,700. The pipeline: `read_anim_keyframes_tpac.ps1 -ByClip`
  resolves the action set's clip names through `animation_clips.tpac` to masters (16 prototype clips were 15
  masters; `blocked_slashright_2h` plays `anim_twohanded_slashright_unbalanced` backward 110 to 1; a master named
  `jump_loop` is a 0-frame shell) and writes `clips_index.json`; `retarget_mannequin_to_human.py --source-json
  --source-rig human` builds the source rig from each JSON's skeleton dump and keys it (sparse integer-frame keys
  with holds, `Duration` = the root track's key count, the pelvis bob in `root.pos`), maps by identity, keeps the
  trunk delta-only (`--no-align`), takes the trunk's reference from frame 1 of the two-handed stance
  (`--ref-json`) and its posture from the approved Fab idle (`--posture-clip`: spine2 17 deg, neck 92, pelvis 15,
  where the rig's rest has 52, 53 and 6; two probes and the first posture read compared the rest with itself
  because Blender's importer lands our frame 0 on frame 1, caught by a pixel diff of the previews), and anchors
  the leg IK on the reference pose's own feet (the Fab set re-run this way is within 6 mm of v5, so v5 stays).
  Fifteen prototype masters (`stand_right_twohanded`, `anim_guard_up_twohanded`, the slashright and overswing
  ready and release masters, defend up, `anim_2handedbash`, `anim_2h_stand_idle_1`, kick, jump, strike, turn,
  run) export with every IK goal within 2 cm and frame 0 at rest, and are staged in
  `LOTRLOME_Armory\AssetSources\Race Test\Mordor\Trolls\animations\` beside the 52 Fab masters (a first
  `animations_human` folder was named after the SOURCE and read as human animations, so Mike had it folded into
  the troll's folder before importing) for Mike's Kit look.
  `gen_troll_anim_clips.ps1 -CloneByName -ClipsIndex ... -TravelScale 1.8504` will cut each clip as a copy of its
  own vanilla definition on the new master (range shifted by the rest frame, facial id cleared, displacements
  scaled), and the new `tools/bind_hill_troll_action_set.py` (10 tests) regenerates the standalone set from
  Native's 4,700 active nodes: Fab clip where the cave troll rules bind one (213), retargeted human clip where an
  index lists it (439 with batch 1), the human clip inherited otherwise (4,048). Batch 1 is the two-handed group
  without crouches plus strikes, staggers, falls, jumps and kicks: 429 clips on 255 masters
  (`review_20260924b/batch1_clips.txt`), retargeted in one background run (53,652 frames, 255 of 255 exported,
  frame 0 at rest everywhere, IK misses under 7.3 cm with six clips over 5 cm, all falls, knockbacks and quick
  swings; the 15 prototype masters came out pixel-identical to the prototype run) and staged, all 255, in the
  `animations` source folder beside the Fab set (307 FBX, no name collisions; the generator's two modes each skip
  the other's clips and masters). Mike imported the 255 on 2026-09-25: `wire_anim_master_skeletons.ps1` patched
  all 255 EMPTY masters (the 52 kept theirs); `gen_troll_anim_clips.ps1 -CloneByName` wrote 428 clips and refused
  `aserai_mp_guard_idle_2hperk` (vanilla range 1..551 on a master the Kit reports at 552 frames, one past the last
  key: a multiplayer perk idle, left on the human clip). The Fab verify was clean (52); the clone-by-name verify reported the 428 with that
  clip as missing and exited 1 until the deep review taught it the refusal (2026-09-25: `refused-by-design=1`,
  exit 0). `bind_hill_troll_action_set.py --clips-dir` rewrote the set from the clips ON DISK (its first pass had
  trusted the index and bound the refused clip): 4,700 nodes, Fab 213, retargeted 438, inherited 4,049, every bound
  troll clip present, the cave troll's set untouched, `audit_action_set_parity.py` 0 gaps; the tracked snapshot
  carries the identical body. Mike's Kit look: "all of the animations I tested look amazing". The Kit also warned
  `Could not set fixed-size(64) string` on 15 clips: an AnimationClip's name is a 64-byte engine string, 63 usable
  characters, and `anim_hill_troll_` + the vanilla name ran to 70 (`..._strike_fall_right_heavy_back_rise_left_
  stance_continue`); master names are not fixed-size (the import session logged nothing on the seven long ones).
  `tools/blender/hill_troll_clip_renames.json` shortens those 15 (`left_stance` to `ls`, no collisions, longest 61),
  the generator reads it as `-Renames` and refuses any name still over 63, the bind as `--renames`. OWED: a Kit
  save (the `.rdc` entries; `check_rdc_entries.py --under "Race Test/Mordor/Trolls/animations"` after it), the
  in-game fight.
- **The hill troll troop and the Brute Force tree (2026-09-25):** Mike: "set up the hill troll race according to our
  new animations, mesh and skeleton" and "add them as a troop just like the cave_troll to Mordor". `hill_troll` in
  `troops_mordor.xml` beside `cave_troll` (race `hill_troll`, Infantry, level 51, the cave troll's skills, name
  `{=aom_hill_troll_name}[AMordor] Hill Troll`, face `BodyProperty.fighter_cave_troll`, the cave troll's own, since
  a byte-identical `fighter_hill_troll` copy was merged back in review), a `0..7` stack in `kingdom_hero_party_mordor_template` next to the cave troll's
  (the Mordor culture's default template, which no shipped lord uses: all 15 Mordor clans bind their own, so
  neither troll reached an AI lord's party, a gap that predated the hill troll; three clans field them since, entry below), `TroopWeight` 4.0 (the cave
  troll's row is still inside a stale "WIP" comment from May), `hill_troll` in `SettlementGuardService`'s excluded
  guard races, in `CharacterAvatarPatch.json` with the cave troll's framing (re-check on the taller model), in the
  validator's `_BODYLESS_BY_DESIGN` and the melee ladder's exemptions; the combat-mechanics, banner-bearer, field
  commission and race-age configs already named it. Kit: one battle set and one civilian set, both
  `wm_cave_troll_2h_mace_a` only: the two-handed clips are the retargeted ones, and the `lotr_troll_*` armour is
  skinned to `human_skeleton` and would float on this rig. The Brute Force tree (#649, committed in
  `9354b0b2`) attaches to both trolls: `TrollBruteForceConfig.ActionSetsByMonster`
  (`cave_troll` to `as_cave_troll_warrior`, `hill_troll` to `as_hill_troll_warrior`), `IsBruteForceTroll` replaces
  `IsCaveTroll`, the start-up drift guard checks every set, the wiring tests cover both Monsters and both bindings
  (each set must bind its own troll's clip, found once under `Assets`), and `bind_hill_troll_action_set.py`'s
  `EXTRA_BINDINGS` appends `act_troll_brute_force` to `anim_hill_troll_attack1` (4,701 nodes, parity 0 gaps).
  Validators: XSD pass on the three edited files, `validate_moduledata.py` 0 errors. The translator ran on Mike's key the same morning: `aom_hill_troll_name` in all 12 language files (the
  `[AMordor]` sort prefix now follows the cave troll's row in each language: plain Mordor in RU, JP, KO, CNs and CNt),
  localization tests 39 green. Reinstall gate: `python tools/wire_hill_troll_race.py --check` exits 1 when the
  Armory's hill troll race, Monster or standalone set is no longer wired (a reinstall reverts all three). OWED: a Kit
  save, a Custom Battle with hill trolls on the Mordor side (gait, the two-handed swings, the smash, a death), a
  GitHub issue, the deep review.
- **Health, costs, clans, reach and idles (2026-09-25, after the deep review):** both trolls have 200 health
  (the campaign through `TaomCharacterStatsModel` and the race's `baseHitPoints` in
  `combat_mechanics_config.json`; Custom Battle through the Monster's `hit_points`, the cave troll's cut from 300);
  both are special-resource troops at 50 War Spoils to recruit and 5 (cave) or 4 (hill) a day, charged to the
  player only, which in practice means recruiting captured trolls from prisoners; Bolgrûkig, Zarûnik and Brughash
  (`clan_empire_south_5`, `_13`, `_15`) field 0 to 2 of each, the first shipped lords that can (a companion clan on a Mordor settlement already
  fell back to the culture template's 0 to 7); Brute Force distances
  scale with `AgentScale` times eye height over 1.70, so the cave troll's tuning holds and the hill troll's reach
  follows its height; and the bind's reuse rule puts the inventory, conversation and cheer codes (172) and the
  bodyguard pose on the troll's own Fab idles (Mike: "reuse animations that we already have"). Detail and the
  tests: CHANGELOG "trolls at 200 health, costed, and in three Mordor warbands". OWED: the Custom Battle smoke
  reads the `[TrollBruteForce]` body size and the ring for both trolls. Brute Force has its own doc,
  [troll-brute-force.md](troll-brute-force.md). OPEN for Mike (review of these changes): the reused Fab idles are
  not `cyclic` while the vanilla clips behind those codes are, and the party screen, map conversation and victory
  logic set the action once (per-code clone clips and a Kit save would fix it); the Mordor culture template's 0 to 7
  trolls reach companion clans; the cave troll's `TroopWeight` row is still commented out (1.0 against the hill
  troll's 4.0); a Free-culture player pays the recruit cost in their own resource and loses the troll to alignment
  desertion the next day.
- **Mouth textures (2026-09-25):** Mike asked where `t_hilltroll_mouth` lived. Not in any package or FBX: every
  `hill_troll` skin's `<mouth_textures>` named it (17 entries of the OLD troll's material, the kids the human
  `mouth_mat*`), and the engine puts that material on the head's `face_mouth_mesh`, so the new head's mouth would
  have had none in game. `wire_hill_troll_race.py` now rewrites mouth textures like face textures (39 `mouth_texture` tags, 78 attributes;
  16 tests then, 22 since the review); applied live and to the snapshot, 0 mentions left. The old `Assets/.../Trolls/Hill Troll/` folder
  (March meshes, `troll_skeleton*` packages, `m_hilltroll_*` materials) is unreferenced now; deleting it is a
  separate art-drop decision (`audit_deleted_mesh_impact.py` first).
- **LOD1 to LOD5 on the hill troll (2026-09-25):** the Armoury rule is LOD0 through LOD5 on every mesh
  (`docs/reference/armory-guide.md`, "LODs in the FBX sources"), and `hill_troll_a.fbx` had LOD0 only.
  `tools/lod_fill_batch.py` added 35 levels to the seven parts (70/30/15/7/3%, neck and wrist seams locked,
  bind pose unchanged) and installed it over the source. Mike re-imported it the same day.
  **An `export_rig_for_kit.py` re-export from the `.blend` drops them**, so run
  `python tools/lod_fill_batch.py --fbx "Race Test/Mordor/Trolls/hill_troll_a/hill_troll_a.fbx" --apply` after
  every re-export. The package holds `troll_skeleton_a`, so its tpac was backed up first
  (`E:\Bannerlord_Backups\lod_pass_20260925\originals\Assets\`). After that re-import, `troll_skeleton_a`
  kept its capsule, body and ragdoll segment byte-identical (decompressed), with bone order and parents unchanged
  and rest frames within 8.6e-5 (FBX round-off), so nothing was restored.
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
  (Superseded on 2026-09-24: KEYForce delivered the hill troll on its own `troll_skeleton_a`, with its own
  materials; the entries above.)
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
> `human_skeleton` (no custom troll skeleton); `hill_troll` referenced a custom `troll_skeleton` until
> 2026-09-24 and runs on `troll_skeleton_a` since. One correction to "no IK joints": a game skeleton's
> `SkeletonUserData` does carry `ik`-typed joints (the human has 19 beside 15 `d6`), but they are ragdoll
> constraints, not animation IK; `tpac_skeleton_copy_physics.py` copies them.

## See also
- [troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md) — the HOW.
- [hero-race.md](hero-race.md) — TAOM race system (id mapping, camera/eye-height, persistence).

## Changelog

- 2026-09-25, `feat(troll)`: 200 health for both trolls, special-resource costs, troll stacks in three Mordor
  clans' templates, Brute Force reach scaled by eye height, the hill troll's idles reused for inventory,
  conversation, cheers and the bodyguard pose.
- 2026-09-25, `feat(troll)`: the `hill_troll` troop for Mordor, the Brute Force tree on both trolls, 428 human clips
  cut and bound (`bind_hill_troll_action_set.py`), `wire_hill_troll_race.py --check` as the reinstall gate.
- 2026-09-24, `fix(troll)`: the Fab clips re-retargeted with twist bones, flat feet, leg IK on the Fab stance and
  the root's height kept; feet within 2 cm of the source over all 52; `-TravelScale` is the report's `pelvis_scale`.
- 2026-09-24, `feat(troll)`: `troll_skeleton_a` re-framed to the human's axes on export (human clips bend it right),
  physics re-run, and the 52 Fab clips retargeted onto it as `anim_hill_troll_*` (staged for the Kit).
- 2026-09-24, `feat(troll)`: the `hill_troll` race on `troll_skeleton_a` (all ten skins, standalone action set,
  Monster sized to the model, grip bones, separate shoulder mesh; `wire_hill_troll_race.py`).
- 2026-09-24, `feat(troll)`: the hill troll on KEYForce's `troll_skeleton_a` (`export_rig_for_kit.py`, Kit import by
  Mike), with the human's ragdoll, IK and skin-fitted hit capsules carried through its bone frames
  (`tpac_skeleton_copy_physics.py`).
- 2026-09-24, `fix(troll)`: the cave troll's jaw rides its head instead of its chest (`reskin_to_human_skeleton.py`
  jaw rule, head meshes on head and neck only, a nod each way in QA).
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
