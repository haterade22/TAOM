---
paths:
  - "Main/Features/Animalia/**"
  - "Main/Features/Arachnophobia/**"
  - "Main/Features/CreatureBandits/**"
  - "Main/Features/CreatureSiegeRole/**"
  - "Main/Features/Elephant/**"
  - "Main/Features/ElephantLike/**"
  - "Main/Features/Elk/**"
  - "Main/Features/Mumakil/**"
  - "Main/Features/MonsterSize/**"
  - "Main/Features/MountDespawn/**"
  - "Main/Features/Spider/**"
  - "Main/Features/Warg/**"
  - "Main/Features/WarRam/**"
  - "Main/Features/TrollBruteForce/**"
  - "Main/Features/HeroRace/**"
  - "Main/Features/SkeletonBuffer/**"
  - "Main/Features/AnimationVariety/**"
  - "Main/_Module/ModuleData/action_sets.xml"
  - "Main/_Module/ModuleData/skins.xml"
  - "docs/ai-includes/creature-*.md"
  - "docs/reference/bannerlord-skeleton-authoring.md"
  - "docs/reference/ue-to-bannerlord-asset-pipeline.md"
  - "tools/rename_anim_clip_tpac.py"
  - "tools/spider_gait.py"
  - "tools/bind_spider_clips.py"
  - "tools/check_gait_stride.py"
  - "tools/gen_elephant_anim_clips.ps1"
---

# Trap sheet: creatures and assets

Mounts, creatures, race rigs, skeletons, animation clips, Modding Kit and Unreal imports.

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the
history and the gate. A new trap for this area is a new row here, never a row in the always-loaded
[trap index](../../../docs/ai-includes/orientation.md)
([ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)).

| Trap | Rule | Doc |
|---|---|---|
| Horse-skeleton mounts | `act_horse_kick` is the engine's rear kick; own attack, own action; a HorseHarness per Horse slot (gated) | [war ram](../../../docs/features/war-ram.md) |
| Mount size, seat | Resize on the Monster (`taom_body_length`); the usage's rider clip seats a rider, never `rider_sit_bone` | [size](../../../docs/features/monster-size.md) |
| Creature rider rows | Rows given only to `as_human_warrior` miss the dwarf sets (no `base_set`): copy them into the Armory's `as_dwarf_warrior` (live file and repo snapshot together); `DwarfMountRiderRowsTests` | [war ram](../../../docs/features/war-ram.md) |
| Riderless creatures | Soldiers ignore a riderless `Mountable` with no weapon state; fading a mount a soldier holds or chose CTDs | [bandits](../../../docs/features/creature-bandits.md) |
| Animation | Engine skeleton: engine frames, rest pose at frame 0, list order; own skeleton: retarget the clips too | [skeleton](../../../docs/reference/bannerlord-skeleton-authoring.md) |
| Gait stride | A gait master's feet must cover its clip's `LoopDisplacement`, or it skates; never type the value, and never reimport the elephant's June FBX: `check_gait_stride.py` | [elephant](../../../docs/features/elephant.md) |
| Cooked clip keys | The game plays a clip from the cache entry the Kit cooked, not from its master: a master or clip record written by a tool shows only after a Kit start; never bind a clip with no entry | [elephant](../../../docs/features/elephant.md) |
| Quadruped clip records | The mount node takes cadence from `Duration` and pace from the limits, and a cloned record keeps its template's: backward limits negative, strafe `Duration` = keys / the master's key rate (30; the spider's 60), each faster pace's minimum above the walk's: `check_gait_stride.py` | [elephant](../../../docs/features/elephant.md) |
| Cloned clip records | A clone keeps its template's sound points, sound and blends: a sound point at or past (Duration - BlendOut) / Duration is an editor error; a gait's in-place twin is the gait's record with no usage: `gen_elephant_anim_clips.ps1` | [spider](../../../docs/features/spider.md) |
| Spider clips | Generated, never retargeted or typed: `spider_gait.py`, then `bind_spider_clips.py --check` and `check_gait_stride.py --creature spider`; keep the June `an_spi_*` gaits on disk and unbound as the rollback, never bind one as a fix | [spider](../../../docs/features/spider.md) |
| Kit clip rename | Corrupts the clip: keep the name, close the Kit, `rename_anim_clip_tpac.py`. A rope-flagged copy lacking `{clip}_weapon` CTDs | [tools](../../../tools/README.md) |
| New race rigs | LOD0 head/eye/mouth: 101 morphs, eyes following their sockets; release/blocked codes: vanilla or self-keyed clip. Else CTD | [troll](../../../docs/features/troll-race.md) |
| UE clip export | The root node carries pelvis height above bind; keep root height, drop only travel and yaw | [pipeline](../../../docs/reference/ue-to-bannerlord-asset-pipeline.md) |
| Kit FBX materials | Bound by name module-wide, reset on every re-import; a same-named older material wins silently | [troll](../../../docs/features/troll-race.md) |
| Override by name | A package that overrides an item by name needs new GUIDs: a registered GUID is silently ignored (yotthani, not checked by TAOM) | [props](../../../docs/reference/tpac-static-prop-authoring.md) |
