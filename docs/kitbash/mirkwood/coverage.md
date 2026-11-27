# Mirkwood settlement and kit coverage

All 24 installed `Culture.mirkwood` settlements use Battania centre scenes. The
settlement XML includes five castles, two towns and 17 villages. Those rows do not
point to the showcase or to any of the 11 production scenes identified below.

## Scene use

Counted direct mesh components and instances that resolve through TAOM_Map prefab
definitions. The pass parsed all 66 top-level installed TAOM_Map `scene.xscene`
files. The production total is 436 placements of 21 distinct kit meshes.
The campaign map is one of the 11 scenes.

| TAOM_Map scene | Direct placements | Expanded prefab placements | Total |
| --- | ---: | ---: | ---: |
| `Main_map` | 0 | 11 | 11 |
| `mod_taom_mirkwood_kitbash` | 0 | 136 | 136 |
| `taom_dale_castle_001_forceatmo` | 24 | 154 | 178 |
| `taom_dwarf_town_01_forceatmo` | 0 | 32 | 32 |
| `taom_dwarven_moria_e_forceatmo` | 0 | 11 | 11 |
| `taom_dwarven_moria_w_forceatmo` | 0 | 9 | 9 |
| `taom_gondor_town_minas_tirith_forceatmo` | 11 | 2 | 13 |
| `taom_gondor_town_pelargir_forceatmo` | 11 | 0 | 11 |
| `taom_minas_morgul_arena_forceatmo` | 0 | 2 | 2 |
| `taom_minas_morgul_keep_forceatmo` | 48 | 0 | 48 |
| `taom_mordor_town_01_forceatmo` | 0 | 9 | 9 |
| `taom_mordor_town_minas_forceatmo` | 67 | 45 | 112 |

Two instances of `dwarf_anvil_runic` contribute 32 placements in
`taom_dwarf_town_01_forceatmo`. That scene contains no literal kit name.
Dale contains 24 direct placements and 154 through the authored house prefabs.
Minas in Mordor also expands a Gondor prefab with Mirkwood arcade pieces.

## Showcase and prefab budget

The showcase places `a_taom_mirkwood_complete_set` once. The prefab XML
has 136 individual root definitions plus the one palette root with 136 children.
It uses 273 prefab entities. The five prefab folders from the documented editor
module set currently total 124,879 of 131,072, leaving 6,193.

The authored offsets arrange the kit as a palette. Some walks have elevated
bases and quarter-turn rotations. The only pair whose cooked LOD0 bounding boxes
meet the simple 30 mm face-contact filter is two display vines. These positions
can reconstruct the palette. They cannot specify playable seams.

The curved A02 centerpiece column uses the dirty column collision body in both
its individual definition and the palette. Verify the shared shape before using
the piece along a navigable opening.

## Settlement scenes

| Settlement | ID | Centre scene at all authored levels |
| --- | --- | --- |
| Glad Thaw | `castle_M1` | `battania_castle_b` |
| Annon Sirith | `castle_village_M1_1` | `battania_village_k` |
| Celebost | `castle_village_M1_2` | `battania_village_l` |
| Gwígar | `castle_M2` | `battania_castle_a` |
| Glin-edhel | `castle_village_M2_1` | `battania_village_m` |
| Tinnulas | `castle_village_M2_2` | `battania_village` |
| Torech Emel | `castle_M3` | `battania_castle_b` |
| Pelan Erui | `castle_village_M3_1` | `battania_village_e` |
| Eithel Glin | `castle_village_M3_2` | `battania_village_g` |
| Lasgalor | `castle_M4` | `battania_castle_a` |
| Galadbain | `castle_village_M4_1` | `battania_village_h` |
| Tawar Beleg | `castle_village_M4_2` | `battania_village_i` |
| Imnagath | `castle_M5` | `battania_castle_b` |
| Tawar-gloss | `castle_village_M5_1` | `battania_village_j` |
| Pelarivrin | `castle_village_M5_2` | `battania_village_k` |
| Felegoth | `town_M1` | `battania_town_b` |
| Methedloth | `village_M1_1` | `battania_village_l` |
| Ang-fennas | `village_M1_2` | `battania_village_m` |
| Linnenduin | `village_M1_3` | `battania_village` |
| Mithlas | `village_M1_4` | `battania_village_e` |
| Caras Laerolin | `town_M2` | `battania_town_c` |
| Laer Loth | `village_M2_1` | `battania_village_g` |
| Ithillas | `village_M2_2` | `battania_village_h` |
| Dorglan | `village_M2_3` | `battania_village_i` |

The five castles and both towns use the same three Battania keep interiors
and `battania_dungeon_stealth`. The towns also name `arena_battania_a`,
`battania_town_house_a_interior_a_tavern` and
`battania_town_house_b_interior_b_house`. Their alley rows leave the
scene fields empty.

## Settlement components

| Scene | Useful pieces in the kit | Gap to check against the target scene |
| --- | --- | --- |
| Town | Stone arcade runs, carved columns, entries, steps, bridge spans, lamps | A connected civic hall and interior route, with assembled bays, crossings and landings |
| Castle | Town wall segments, gate leaf, stone entrances, large span and arch pieces | Defensive corner and tower families, wall-walk and gate assembly, and siege wiring if requested |
| Village | 3 m floor, wall and window blocks, roof corners, house doors and stairs | Complete house recipes, working entrances and a usable street or path layout |

A first round can compare the existing stone arcade, the main stone bridge and a
root-supported walk against the references. The review marks determine which
pieces enter that round.
