# Kingdom coverage: which kingdom has settlement art of its own

Phase 1 output of the [kingdom kits prompt](kingdom-kits-prompt.md). It records what settlement art
each kingdom has, what it borrows, and which kit is its nearest donor.

**State on 2026-11-24:** only Rivendell is reviewed, because Mike chose it as the pilot before the
review ran. The other seven kingdoms of the prompt (Rohan, Rhûn, the Orcs, the Goblins, Harad, Khand
and Dale) are still owed. Every count below names the command that prints it, or its date.

## Kits on disk

`TAOM_Map\AssetSources\Scenes\` on 2026-11-24. The command, in PowerShell with `$s` set to that
folder: `Get-ChildItem -LiteralPath $s -Directory`, then for each folder
`(Get-ChildItem -LiteralPath $_.FullName -Recurse -File | Measure-Object).Count` and the same with
`-Filter *.fbx`.

| Folder | Files | FBX | Folder date | What it holds |
|---|---|---|---|---|
| `comprehend` | 61 | 5 | 2026-11-24 | Buildings, root walls, scaffolding, a floor and a round stair (files dated 2026-10-17), with eight ground texture sets and object textures |
| `Decals` | 17 | 0 | 2026-09-13 | Decal textures |
| `erebor` | 87 | 15 | 2026-10-15 | The Erebor kit ([catalog](erebor/)) |
| `Gondor` | 410 | 112 | 2026-10-15 | The Gondor kit ([catalog](gondor/)); `walls\` holds the castle walls, towers and the gatehouse |
| `Mirkwood` | 86 | 1 | 2026-04-08 | One FBX; not catalogued |
| `Mordor` | 104 | 25 | 2026-10-17 | Not catalogued |
| `Nature` | 120 | 25 | 2026-10-05 | Rocks and cliffs shared by the kits |
| `Rohan` | 134 | 7 | 2026-11-06 | Two landmarks, not a modular kit: the Golden Hall of Edoras (`edoras\sm_golden_hall_edoras_taom.fbx`) and Helm's Deep (`helms_deep\`, five FBX, and `helms_deep_wall_key_01.fbx`) |
| `Skyboxes` | 4 | 0 | 2026-11-06 | Sky textures |

Two differences from the kit table in [README.md](README.md):

- **`Rohan` exists and the table did not list it.** It holds landmark meshes for two scenes. It has
  no wall, gate or house family that another settlement can reuse.
- **`Rivendell` is not there.** The table says the converted kit (458 meshes) is in
  `Scenes/Rivendell/`. No such folder exists, and no cooked package holds a kit mesh:
  `python tools/validate_mesh_refs.py --check-name sm_rivendell_gate_wall --check-name sm_rivendell_tower`
  reports both names free. What exists is the raw Unreal export at
  `E:\LOTRAOMAssets\_export\rivendell` (460 FBX in `meshes\`, 725 TGA in `textures\`, with the
  original `SM_` names and centimetre units) and the Unreal project at
  `E:\LOTRAOMAssets\Environment 3D Cosmos\ElvenForestCity`. The converter
  (`tools/oneoff/blender_normalize_rivendell.py`) can run again from that export. No commit under
  `docs` or `tools` records a removal (`git log -S"Scenes/Rivendell" -- docs tools` shows only the
  commits that added the pipeline), and the live module is not versioned, so the cause is unknown.

## Rivendell

Culture id `rivendell`, kingdom id `rivendell`, shown as Imladris
(`Main/_Module/ModuleData/taom_spkingdoms.xml:88-99`, `taom_spcultures.xml:349`,
[id-cheatsheet.md](../modding/id-cheatsheet.md)). The live `TAOM_Map\ModuleData\settlements.xml`
holds 21 Rivendell settlements at lines 10814 to 11160
(`grep -c 'culture="Culture.rivendell"'` prints 21).

| Settlement | Id | Scene it loads (levels 1 to 3 are the same scene) | Scene module |
|---|---|---|---|
| Rivendell (town) | `town_R1` | `battania_town_d` | SandBoxCore |
| Hithaegrist (castle) | `castle_R2` | `battania_castle_a` | SandBoxCore |
| Nan Tornaeth (castle) | `castle_R3` | `battania_castle_b` | SandBoxCore |
| Baracharn (castle) | `castle_R4` | `battania_castle_a` | SandBoxCore |
| Fennas Drúnin (castle) | `castle_R5` | `battania_castle_b` | SandBoxCore |
| 16 villages | `village_R1_1` to `_4`, `castle_village_R2_1` to `R5_3` | `battania_village` and its variants `_e`, `_g`, `_h` to `_m` | SandBox and SandBoxCore |

Interiors are vanilla Battania too: the keep `battania_castle_keep_a_l1` to `_l3_interior`, the
tavern, the arena `arena_battania_a`, the dungeon and the houses.

**What the scenes are built from.** Counts of `<game_entity prefab="<prefix>` in each
`scene.xscene`, not capped: `battania_castle_a` has `battania_` 267, `sturgia_` 16, `aserai_` 2,
`empire_` 1; `battania_castle_b` has `battania_` 152; `battania_town_d` has `battania_` 468. Every
TAOM kit prefix (`gd_`, `gondor`, `dw_`, `erebor`, `mirkwood`, `taom_`) counts 0 in all three. The
counts cover entities placed in the scene file, not the meshes inside nested prefabs.

**Siege content today.** `grep -c '<script name="WallSegment"'` prints 43, 56 and 87 for the two
castles and the town, and `CastleGate` prints 6 in each. So every Rivendell siege is fought on
vanilla Battania timber and stone.

**The gap.** Rivendell has no settlement art of its own in any module. No Rivendell or elven scene
exists, and no kit showcase scene (`mod_taom_<kit>_kitbash` exists for Erebor, Gondor, Mirkwood and
Mordor only). The approved Rivendell art is armour, shields, weapons and one map-icon set.

**Nearest donors.**

| Need | Donor | Why |
|---|---|---|
| Siege wall dimensions, wall walk, collision, LOD chain | Gondor `gondor_castle_wall_20m_l3_a` and its `_dest` twin | The only TAOM wall set that a working siege scene uses (`taom_gondor_castle_001_forceatmo`) |
| Gatehouse dimensions | Gondor `gondor_castle_gatehouse_l1_a` | Its deck is at the wall's deck height; the moving gate inside it is a vanilla gate prefab |
| Elven forms (arches, pillars, railings, a tower, a gate, a garden wall) | The raw `ElvenForestCity` export | Bought kit in the right style; it has no wall walk, no parapet and no broken wall |
| Colour | Rivendell armour and shield textures in `LOTRLOME_Armory`, and the map-icon set | The only approved Rivendell art |

The measured record of each donor piece is in the work folder
`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\` and will be written up here when the
pilot's records are approved (Gate 2 of the prompt).

## Still owed

- The same review for Rohan, Rhûn, the Orcs, the Goblins, Harad, Khand and Dale.
- A catalog of the Mirkwood and Mordor kits, as far as a kingdom needs them.
- The use count of each kit piece in the scenes and prefabs (the Gondor catalog has it for Gondor).
- The filterable picture page of donor pieces.
