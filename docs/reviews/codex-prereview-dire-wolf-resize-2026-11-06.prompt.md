Adversarial pre-review of an uncommitted change in E:\repos\TAOM (branch bannerlord-1.5.x, HEAD 97d8cb72b). Issue #1081. Read-only: do not edit any file. Return the full report as your FINAL MESSAGE; do not write it to a file.

CHANGE: the dire wolf (purchased ADOD_Beasts wolf rig, ridden as a warg-style mount) was rebuilt at 1.73 times the pack's size (RIG_SCALE), because portraits (CharacterSpawnerService) and the inventory (CharacterTableau, item scale_factor) draw the mesh at its own size and taom_body_length only reaches the battle agent. The rig mesh, rest skeleton, skeleton physics (capsules, joint positions), animation masters (location keys), clip gait displacement ($GAIT_SCALE = 1.73 * 1.10) and the Monster's lengths (heights, eye, rein, body capsule) all scale by 1.73; taom_body_length goes back to 110 (the warg's). The Kit refused 5 of 6 animation imports because every export carried the take name "Scene"; the fix names each take after the pack's take and gates it.

Maintainer decisions (not open): rebuild the rig at warg size rather than use item scale_factor; RIG_SCALE 1.73; taom_body_length 110.

READ FIRST: docs/features/dire-wolf.md ("Size", "Traps"), docs/reference/lotrlome-dire-wolf-changes.md ("Resize to warg size", "Replay"), docs/features/monster-size.md (the dire_wolf row and the paragraph after the table).

CHANGED FILES (this change only):
tools/blender/rig_scale.py (new), tools/blender/fbx_takes.py (new), tools/blender/scale_dire_wolf_anims.py (new), tools/blender/prep_dire_wolf_meshes.py
tools/apply_dire_wolf_skeleton.py (--scale, --replace), tools/tpac_skeleton_copy_physics.py (replace_segment, verify_segment), tools/gen_dire_wolf_anim_clips.ps1 (Resolve-Master by name, $GAIT_SCALE), tools/apply_dire_wolf_armory.py (RIG_SCALE lengths, 110)
tools/tests/test_rig_scale.py, test_fbx_takes.py (new), test_apply_dire_wolf_skeleton.py, test_gen_dire_wolf_anim_clips.py, test_apply_dire_wolf_armory.py
Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac (rebuilt by tools/build_ds_skeleton_pack.py --apply)
Docs: docs/features/dire-wolf.md, docs/features/monster-size.md, docs/reference/lotrlome-dire-wolf-changes.md, tools/README.md (the dire wolf rows only), docs/reviews/lessons/animation-skeleton.md (last entry)
Live (unversioned): E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\Monsters\LOTR\lotr_monster_dire_wolf.xml (backup .bak-direwolf-x173-20261106-160216 beside it).
Other sessions' uncommitted files in the tree (dwarf attacks, troll brute force, quad remesh, Tripo prop, C# under Main/Core/Combat) are OUT OF SCOPE.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. Monster lengths. Are standing_chest_height, standing_pelvis_height, standing_eye_height, rider_eye_height_adder, rider_camera_height_adder, eye_offset_wrt_head, rein_handle_*_local_pos and the body_capsule in model units that the engine multiplies by the agent scale (so x1.73 is right with body_length 110), or in metres? Quote the installed engine reader (Monster deserialisation, AgentVisuals or MBAPI) or say UNVERIFIED.
2. Gait displacement. $GAIT_SCALE = RIG_SCALE * 1.10: does a clip's LoopDisplacement / displacement get multiplied by the agent scale at run time, which would double-count the 1.10? docs/features/animalia-elk-moose.md says this is not established.
3. Skeleton scale (apply_dire_wolf_skeleton.py scale_definition / scale_userdata): which userdata fields are lengths (capsule ends, radii, joint pos) and which are not (masses, joint rot, limits, -1 sentinels)? Any length field left unscaled, or a non-length scaled? The idempotence check _at_size and the --replace path.
4. tpac_skeleton_copy_physics.replace_segment / verify_segment: byte faithfulness, the segment hash refresh, a failed verify.
5. rig_scale.scale_rig: the world-origin pivot, the edit-mode round trip, mesh transform with the old world matrix, location-key scaling (co and both handles), and channels it misses (scale keys, constraints, object-level location keys).
6. scale_dire_wolf_anims.py: the -1 frame shift, the scene frame range, the export axes, the re-import gate tolerances, the take-name gate (fbx_takes.take_names: can it misread a non-AnimStack string or a name over 1024 bytes?).
7. gen_dire_wolf_anim_clips.ps1 Resolve-Master: name collisions between masters, a missing name, the OUT scan.
8. apply_dire_wolf_armory.py: the writer leaves an existing Monster file alone. Does a reinstall replay produce the same file the live hand edit produced?
9. DS skeleton pack: does the dedicated server need the scaled skeleton, and does it hold it?

REQUIRED SECTIONS: per suspect verdict with evidence; ENGINE (quote the installed v1.5.6 TaleWorlds members you rely on via pwsh tools/taom-src.ps1 path <Type> or python tools/native_decompile.py); FINDINGS (severity CRITICAL/HIGH/MEDIUM/LOW, file:line, failure scenario); OBSERVATIONS.

QUALITY GATES: every claim cites a file and line you read; no invented API; missing evidence is UNVERIFIED.
