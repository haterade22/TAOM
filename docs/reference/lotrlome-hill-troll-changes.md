# LOTRLOME_Armory changes for the hill troll on `troll_skeleton_a` (2026-09-24)

The hill troll's data plane lives in the external `LOTRLOME_Armory` module
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`), which this repo does not track.
A module reinstall silently reverts every change below, and nothing in CI sees them. This ledger records each edit,
why it exists, and how to redo it. Feature doc: [troll-race.md](../features/troll-race.md) (the 2026-09-24 entries).
The model to copy was the dwarf: a humanoid race on its own skeleton, `Usage` `human`, the human's ragdoll and IK,
every skin and a standalone action set naming that skeleton.

## Assets (imported by Mike in the Modding Kit)

| Path | Note |
|---|---|
| `AssetSources/Race Test/Mordor/Trolls/hill_troll_a/hill_troll_a.fbx` | Exported by `tools/blender/export_rig_for_kit.py` from KEYForce's `troll_rig_base_01.blend` (second delivery, `E:\LOTRAOMAssets\drive-download-20260924T190230Z-1-001\`). Earlier versions beside it: `.bak-kitmaterials-20260924` (first export), `.bak-mouthsplit-20260924` (Kit material names), `.bak-gripbones-20260924` (split head) |
| `AssetSources/.../hill_troll_a/textures/` | KEYForce's 15 textures, six resized from 2048 to 1024 (originals under `E:\taom-texture-backup-2026-09-13\`) |
| `Assets/Race Test/Mordor/Trolls/hill_troll_a/hill_troll_a_geo.tpac` | Package GUID `4F2B61AC-E823-426A-9A6D-4833FAF76957`. Items: skeleton `troll_skeleton_a` (28 bones, the human's names and order), metameshes `hill_troll_a_body` (`.0` body, `.1` cloth), `hill_troll_a_shoulder`, `hill_troll_a_head` (sub-meshes `hill_troll_a_head`, `.eye`, `.mouth`, tagged `face_base_mesh`, `face_eye_mesh`, `face_mouth_mesh`), `hill_troll_a_hands`, `hill_troll_a_legs` |
| `Assets/.../hill_troll_a/textures/t_tr_hill_troll_{body,cloth,head,eye,hammer}_a_mtl.tpac` | The materials the FBX names. The old hill troll's March materials `m_hilltroll_*_a` (in `Trolls/Hill Troll/textures/`) must never match an FBX name again: the first import bound to them silently |
| `RuntimeDataCache/4F2B61AC-E823-426A-9A6D-4833FAF76957.rdc` | Present; cooked by the Kit before the last physics write, so a Kit load and save is owed |
| `AssetSources/Race Test/Mordor/Trolls/animations/anim_hill_troll_*.fbx` (52) | The Fab cave troll clips retargeted onto `troll_skeleton_a` by `tools/blender/retarget_mannequin_to_human.py` (armature `troll_skeleton_a_notused`, frame 0 rest). The set Mike imported first (16:10) is backed up at `E:\LOTRAOMAssets\_hill_troll_a_export\anim_fbx_backup_20260924_1610\`; the installed set is the v5 re-retarget (twist bones, flat feet, leg IK on the Fab stance, root height kept; staged copy `E:\LOTRAOMAssets\troll_clips_to_import\fab_hill_troll_v5\`). Redo: the command in `tools/gen_troll_anim_clips.ps1`'s header and the feature doc's "Fab clips re-retargeted" entry |
| `AssetSources/Race Test/Mordor/Trolls/animations/anim_hill_troll_*.fbx`, the 255 human-sourced ones beside the 52 Fab (a first `animations_human` folder was folded in before import, 2026-09-25) | Human clips retargeted onto `troll_skeleton_a` (`retarget_mannequin_to_human.py --source-json --source-rig human --ref-json <the 2h stance master> --posture-clip <the v5 Fab idle> --no-align spine spine1 spine2 neck head`), masters named `anim_hill_troll_<vanilla master minus anim_>`; sources are `read_anim_keyframes_tpac.ps1 -ByClip` JSON (`E:\LOTRAOMAssets\_hill_troll_a_export\human_json_proto\`, `human_json_batch1\`). All 255 of batch 1 staged 2026-09-24 late (429 two-handed and reaction clips, `review_20260924b/batch1_clips.txt`; the 15 prototypes are among them) for one Kit import. Clips: `gen_troll_anim_clips.ps1 -CloneByName -ClipsIndex <clips_index.json> -Renames tools/blender/hill_troll_clip_renames.json -TravelScale 1.8504` |
| `Assets/Race Test/Mordor/Trolls/animations/anim_hill_troll_*_{geo,anm}.tpac` | 307 masters, all Kit-imported by Mike and wired to `troll_skeleton_a` by `wire_anim_master_skeletons.ps1 -SkeletonGuid 7516b03c-... -BoneNum 28` (`.bak-preskel` beside each): the 52 Fab ones and, since 2026-09-25, the 255 human-sourced ones. 480 clips: the Fab 52 (`gen_troll_anim_clips.ps1 -TravelScale 1.5578`, regenerated after the v5 re-import; the first cut used 1.377) and 428 clone-by-name clips (`-CloneByName -ClipsIndex <human_json_batch1>/clips_index.json -Renames tools/blender/hill_troll_clip_renames.json -TravelScale 1.8504`; `aserai_mp_guard_idle_2hperk` refused, its vanilla range one frame past the master). Both `-Verify` modes exit 0 (the clone-by-name one lists the refused clip as `refused-by-design`, since the 2026-09-25 review). The `.rdc` entries for the 255 masters and 428 clips wait on a Kit save |

**Two bones the artist's rig does not have.** `l_finger0` and `r_finger0` are added on export
(`tpac_skeleton_copy_physics.py --missing-bones`, then `export_rig_for_kit.py --bone-frames`): the grip bones the
Monster's `main_hand_item_bone` / `off_hand_item_bone` hang held items on, each carried from the human's hand
into the troll's with its axes, then moved 0.22 m down by KEYForce (`--offset r_finger0=0,0,-0.22 --offset
l_finger0=0,0,-0.22`): the troll's fist hangs far below its wrist, and the human-scaled grip sat 17 to 19 cm above
the centre of the hand's own skin. A re-export without `--bone-frames` drops them and every held weapon loses its bone.
**Re-framed (Mike, after the human `guard_up_2h` twisted the rig):** the installed FBX (15:51, previous one
`.bak-reframe-20260924`) also turns every bone to the human's anatomical axes, heads kept
(`tpac_skeleton_copy_physics.py --reframe`, record `E:\LOTRAOMAssets\_hill_troll_a_export\reframe_frames.json`;
`export_rig_for_kit.py --bone-frames`). Kit import 16:03: the package matches the record (0.000 deg, 0.16 mm),
bone axis +X like the human's. The engine skeleton, re-dumped for clip work: `tools/blender/troll_skeleton_a_engine.json`
(item GUID `7516b03c-1c28-4b4a-87ab-8df6f047bf9c`).

**Skeleton physics** (`SkeletonUserData` inside the package): written by `tools/tpac_skeleton_copy_physics.py
--reframed <record> --fit` over a `skeleton_hit_capsules.py fit --axis bone` of the skin (made against a scratch copy
whose capsules were reset first, so every body was refit): `Usage` `human`, 28 bodies, 34 joints (15 d6, 19 ik), all
within 0.03 deg of the human's, 99.4% of the skin inside a hit capsule (backup `.bak-physics-20260924-160744`). Backups `hill_troll_a_geo.tpac.bak-physics-*` beside the package. A
Kit re-import that keeps the skeleton kept the physics byte for byte; one that changes the skeleton (the grip bones)
kept the old bodies and added empty ones, so re-run the procedure after any skeleton change.

## ModuleData edits

Written by `tools/wire_hill_troll_race.py --apply` (backups `*.bak-hilltroll-race-20260924-151607`) and
`tools/patch_dwarf_action_parity.py --set-id as_hill_troll_warrior --apply` (backup
`action_sets.xml.bak-parity-20260924-151620`).

| File | Change |
|---|---|
| `skins.xml`, `<race id="hill_troll">` | All ten skins (adult, teen, child, toddler, both genders): `skeleton="troll_skeleton_a"`, `body_meta_mesh="hill_troll_a_body"`, `body_meta_mesh_shoulders="hill_troll_a_shoulder"`, `legs_mesh`, `hands_mesh`, `face_meta_mesh` on the `hill_troll_a_*` meshes, underwear meshes empty. Hair, eyebrow and beard lists copied from the adult male (bald, no brow, clean-shaven; an empty `<beard_meshes />` stays), because a human hair mesh would hang at a human head's height. The children's `default_hair_meshes` / `default_beard_meshes` (human hair under a helmet) removed. Every face texture outside a comment names `t_tr_hill_troll_head_a`, and since 2026-09-25 every mouth texture too (the old `t_hilltroll_mouth` never existed in the Kit, "Unable to find material" every session; the engine puts the skin's mouth material on the head's `face_mouth_mesh`, which carries the head material in KEYForce's model; 39 `mouth_texture` tags, 78 attributes, backup `.bak-hilltroll-race-20260925-091436`). Before: the adult male on the old `troll_skeleton`, the adult female and the teen male on `human_skeleton` with the old troll meshes, the rest fully human |
| `monsters.xml`, `hill_troll` | Sizes measured from the 3.6 m model: `standing_eye_height` 3.58 (the eye mesh's centre), `crouch_eye_height` 2.32, `eye_offset_wrt_head` "-0.069, 0.421, 0.0" (the eyes in the head bone's frame), `first_person_camera_offset_wrt_head` "-0.069, 0.434, 0.0", `arm_length` 2.79 (0.9 times the shoulder-to-wrist ratio 3.10); body capsule radius 0.82 from z 1.77 to 3.43, crouched to 1.33 (the human's times the height ratio 2.215). `CanRide="false"` (Mike: no riding) |
| `monsters.xml`, variants | `troll_child`, `troll_settlement`, `troll_settlement_slow`, `troll_settlement_fast` renamed `hill_troll_child` and so on: `FaceGen.GetMonsterWithSuffix` builds the id from the race name (`TaleWorlds.MountAndBlade/FaceGen.cs:44`), so a settlement or conversation spawn got null. The child's eye heights and arm length scaled like the adult's (2.53, 1.47, 1.86); the settlement variant's `CanRide` off too |
| `action_sets.xml`, `as_hill_troll_warrior` | Standalone: `skeleton="troll_skeleton_a" movement_system="bipedal"`, no `base_set`, filled with Native `as_human_warrior`'s 4,700 actions. An agent's skeleton comes from its action set, and the dwarf's `as_dwarf_warrior` is the working precedent. The 84 other `as_hill_troll_*` sets inherit from it; 49 of them carry their own overrides, which the binder does not rewrite except `as_hill_troll_poses`' `act_stand_1` (since 2026-09-25 on the troll's idle); the other 48 hold 2,375 human-clip nodes. `audit_action_set_parity.py` now counts `as_hill_troll_warrior` as a humanoid root (1,304 humanoid sets, 0 gaps). **Body rebound 2026-09-25** by `tools/bind_hill_troll_action_set.py --clips-index <human_json_batch1>/clips_index.json --clips-dir <Assets/.../Trolls/animations> --apply` (backups `action_sets.xml.bak-hilltroll-bind-20260925-083636` and `-083736`): the same 4,700 nodes, each `animation=` now the Fab clip where the cave troll rules bind one (213), the retargeted human clip where its `_anm.tpac` is on disk (438, the 15 over-long names through `--renames tools/blender/hill_troll_clip_renames.json`), the human clip otherwise (4,049), plus TAOM's own `act_troll_brute_force` to `anim_hill_troll_attack1` for the Brute Force tree (#649): 4,701. **Idles reused 2026-09-25 13:04** (Mike: "reuse animations that we already have. the idle animation is fine"; backup `-130423`): 172 of those 4,049 now play the troll's own Fab idles, the party-screen and encyclopedia idle (`act_inventory_idle`, `_start`) and the 148 map-conversation codes (`act_conversation_*`) on `anim_hill_troll_idle1`, and the 22 victory cheers (`act_cheer_*`, `act_cheering_*`) alternating `anim_hill_troll_combat_idle1` and `_2` by number; the binder's REUSE rule, tried after the Fab and retargeted-human rules. 3,877 codes stay on the human clip (swimming, ladders, cutscenes, crafting, sitting). Redo: `python tools/bind_hill_troll_action_set.py --clips-index <human_json_batch1>/clips_index.json --apply` (`--clips-dir` defaults to the install's `Trolls/animations`), then the parity audit |
| `action_sets.xml`, `as_hill_troll_poses` | Its one override, `act_stand_1` (the map-conversation bodyguard pose), rebound from the human `stand_1` to `anim_hill_troll_idle1` in the same run (the binder's `POSE_BINDINGS`) |

The plain `action_sets.xml.bak` in the folder was overwritten by this run's parity backup (the tool then wrote a
fixed name); whatever older state it held is gone. The tool writes timestamped, write-once backups since.

## Redo after a reinstall

**Detect:** `python tools/wire_hill_troll_race.py --check` exits 1 when the race, the Monster or the standalone set
is no longer wired, or when the set is empty, binds no `anim_hill_troll_*` clip or lacks the one
`act_troll_brute_force` binding; `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter
"FullyQualifiedName~TrollBruteForceWiringTests|FullyQualifiedName~TrollHitPointsLiveDataTests"` covers the Brute
Force action and both trolls' health.

**Restore**, game and Kit closed, either way:

1. Copy `skins.xml`, `monsters.xml`, `action_sets.xml` and `action_types.xml` from
   `docs/reference/lotrlome-armory-snapshot/` (all four equal live on 2026-09-25; `action_types.xml` declares
   `act_troll_brute_force`, which both trolls' sets bind for #649). Check `git log` on the snapshot first: another
   session's later edits to those files ride along.
2. Or re-run, in order: `python tools/wire_hill_troll_race.py --apply`; `python tools/patch_dwarf_action_parity.py
   --target <Armory>/ModuleData/action_sets.xml --set-id as_hill_troll_warrior --apply`; if the clip packages were
   lost too, re-import the FBX in the Kit, wire the masters the import leaves EMPTY (`wire_anim_master_skeletons.ps1
   -SkeletonGuid 7516b03c-1c28-4b4a-87ab-8df6f047bf9c -BoneNum 28` on the animations folder; the generator refuses
   unwired masters) and re-cut the clips (`gen_troll_anim_clips.ps1` with `-Renames
   tools\blender\hill_troll_clip_renames.json -RetargetReport <report>` for the clone-by-name clips; without
   `-Renames` the 15 long names are refused and fall back to human clips); then `python
   tools/bind_hill_troll_action_set.py --clips-index <human_json_batch1>/clips_index.json --apply`. The Brute Force
   action's declaration in `action_types.xml` has no generator: copy its line from the snapshot.
3. Then `python tools/audit_action_set_parity.py`, `--check` above and the test filter above, all clean.

The assets need the FBX re-imported in the Kit and the physics procedure in
[bannerlord-skeleton-authoring.md](bannerlord-skeleton-authoring.md) "Ragdoll, IK and hit capsules for a humanoid on
its own skeleton". The cave troll's Monster `hit_points` (200 since 2026-09-25) is in the snapshot's `monsters.xml`.

## Open

- A Kit load and save for the package's `.rdc`, then the first in-game look.
- Animation: the v5 Fab set (52) and the 255 human-sourced masters are imported, wired, cut into 480 clips and bound
  (rows above). Human clips did NOT play right on the rig, re-frame or not: Mike's Kit look at the cave troll's
  `anim_troll_*` (human-skeleton clips) showed the head 45 to 65 deg up and the wrists twisted 20 deg, because a
  clip stores parent-relative rotations and the troll's rest relations differ from the human's (spine2 54 deg,
  neck 45); the actions the troll plays were therefore retargeted from the human masters and bound, the rest
  reuse the Fab clips. The codes the engine plays often while the troop stands still (the party-screen and
  encyclopedia idle, the map-conversation idles and bodyguard pose, the victory cheers) reuse the Fab idles since
  2026-09-25 13:04; 3,877 codes still name the human clip, and 48 derived sets carry human-clip overrides of their
  own. OWED: a Kit save for the `.rdc`
  entries (then `check_rdc_entries.py --under "Race Test/Mordor/Trolls/animations"`), Mike's Kit look, the
  in-game fight.
- The hammer (`SM_TR_Hammer_*` in KEYForce's file, at the world origin) is not an item yet.
- Tattoo materials in the skins are still the human ones.
