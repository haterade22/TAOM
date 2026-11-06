# LOTRLOME_Armory changes: dwarf head butt and drop kick (#1087)

Every edit TAOM made to the live, unversioned `LOTRLOME_Armory` for the dwarf attacks, 2026-11-06. A reinstall of the
Armory drops all of it; the tools below rebuild each file, and their checks fail until they do.

| File (under `LOTRLOME_Armory/`) | Change | Rebuilt and checked by |
|---|---|---|
| `AssetSources/Race Test/dwarf/animations/dwarf_head_butt.fbx`, `dwarf_drop_kick.fbx` | New folder. The exports of `tools/blender/author_dwarf_attack_clips.py`; the source copies stay in `E:\LOTRAOMAssets\dwarf_clips_to_import\` | the authoring tool |
| `Assets/Race Test/dwarf/animations/dwarf_head_butt_geo.tpac`, `dwarf_drop_kick_geo.tpac` | The Kit's import of the FBX: masters `dwarf_head_butt` (37 frames) and `dwarf_drop_kick` (55 frames) on `dwarf_skeleton_a` (`0099bfb2-09c4-4291-b225-3d24bdb14715`) | a Kit import of the FBX |
| `Assets/Race Test/dwarf/animations/dwarf_head_butt_anm.tpac`, `dwarf_drop_kick_anm.tpac` | The clips, cloned from vanilla `kick_right_leg` by `tools/gen_dwarf_attack_clips.ps1`, then saved in the Kit so each has its `RuntimeDataCache` entry | `gen_dwarf_attack_clips.ps1 -Verify`; `python tools/check_rdc_entries.py --under "Race Test/dwarf"` |
| `ModuleData/action_types.xml` | `act_dwarf_head_butt` and `act_dwarf_drop_kick`, both `type="actt_kick"`, after `act_troll_brute_force` | `python tools/bind_dwarf_attack_actions.py --check` |
| `ModuleData/action_sets.xml` | The two rows at the end of `as_dwarf_warrior`, on the clips above | the same `--check` |

The two XML edits are mirrored byte for byte in `docs/reference/lotrlome-armory-snapshot/`; `--check` reads all four
files. Backups of the pre-edit Armory files: `action_types.xml.bak-dwarf-attacks-20261106-124630` and
`action_sets.xml.bak-dwarf-attacks-20261106-124630` beside them.

## The clip recipe

Both clips keep the vanilla kick's flags, priority 34 and blend in 0.2, and change what the war ram's head butt clip
changed to play in battle ([war-ram.md](../features/war-ram.md), "The head-butt clip"):

- `enforce_all` added. The ram's clip showed nothing in battle on priority alone.
- No `ContinueWithAction`: the kick's `act_kick_right_leg_continue` would play a human follow-through after ours.
- No `CombatParameterId`: the engine's own kick collision stays off, and TAOM applies the damage.
- Blend out 0.2 instead of 0: the clips end in the ready stance and hand back to locomotion.
- The kick's swing sound, its sound point moved to the frame the swing starts.

The drop kick also carries `displace_position` and a `DisplacementUsage` of (0, 2.0, 0, 1) ending at progress 31/53,
frame 32 of 1 to 54, the travel the authoring tool keyed the feet against. The flag without the usage is a native
null read; `gen_dwarf_attack_clips.ps1` refuses a clip with one and not the other.

## Why `actt_kick`

It is the vanilla kick's type. Its one managed consumer, `StandingPoint.DoesActionTypeStopUsingGameObject`, makes an
agent that starts a kick, jump or weapon bash let go of the machine it is using. An untyped action would keep a
dwarf on a machine through a 2 m drop kick. What native code does with the type beyond that is unverified.

## Rollback

1. Restore the two `.bak-dwarf-attacks-*` files over `action_types.xml` and `action_sets.xml`.
2. Delete `Assets/Race Test/dwarf/animations/` and `AssetSources/Race Test/dwarf/animations/`.
3. Revert the snapshot files with git.
