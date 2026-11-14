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
| Creature rider rows | Rows given only to `as_human_warrior` miss the dwarf sets (no `base_set`): copy them into TAOM's dwarf block; `DwarfMountRiderRowsTests` | [war ram](../../../docs/features/war-ram.md) |
| Riderless creatures | Soldiers ignore a riderless `Mountable` with no weapon state; fading a mount a soldier holds or chose CTDs | [bandits](../../../docs/features/creature-bandits.md) |
| Animation | Engine skeleton: engine frames, rest pose at frame 0, list order; own skeleton: retarget the clips too | [skeleton](../../../docs/reference/bannerlord-skeleton-authoring.md) |
| Kit clip rename | Corrupts the clip: keep the name, close the Kit, `rename_anim_clip_tpac.py`. A rope-flagged copy lacking `{clip}_weapon` CTDs | [tools](../../../tools/README.md) |
| New race rigs | LOD0 head/eye/mouth: 101 morphs, eyes following their sockets; release/blocked codes: vanilla or self-keyed clip. Else CTD | [troll](../../../docs/features/troll-race.md) |
| UE clip export | The root node carries pelvis height above bind; keep root height, drop only travel and yaw | [pipeline](../../../docs/reference/ue-to-bannerlord-asset-pipeline.md) |
| Kit FBX materials | Bound by name module-wide, reset on every re-import; a same-named older material wins silently | [troll](../../../docs/features/troll-race.md) |
| Override by name | A package that overrides an item by name needs new GUIDs: a registered GUID is silently ignored (yotthani, not checked by TAOM) | [props](../../../docs/reference/tpac-static-prop-authoring.md) |
