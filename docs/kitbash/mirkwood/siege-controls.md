# Mirkwood siege controls

[Mirkwood siege kit issue #1620](https://github.com/haterade22/TAOM/issues/1620).

Mike's scene brief applies to every Mirkwood town and castle siege scene. The scene builder
needs an intact wall, a compatible broken wall, a gatehouse that holds an outer and an
inner gate, and defender siege positions. Study a tower battery and an extra forward
wall with its own battery and access. These measurements informed the
[design language](design-language.md) and the [built siege kit](siege-kit.md).

## Measured examples

| Installed scene or prefab | Observation |
| --- | --- |
| `taom_gondor_town_dolamroth_forceatmo` | Two wall defender positions and two tower positions. Outer and inner gate planes are 6.736 m apart. |
| `taom_gondor_town_pelargir_forceatmo` | Four wall positions. Authored machine and ammunition areas are about 8.0 to 10.34 m wide by 6.0 to 6.54 m deep. Gate planes are 4.290 m apart. |
| `taom_dwarf_town_01_forceatmo` | Two authored defender stations on Erebor `sm_dw_castle_tower_b1` decks. Each deck is 6 x 6 m and the ammunition pile is on the adjacent 20 x 6 m wall deck. Gate planes are 5.480 m apart horizontally. |
| Native `mangonel_b_spawner` | Collision, default crew points and pile points span 5.50 x 5.72 m. The sampled moving arm reaches 2.787 m over the deck. |

`taom_dwarf_town_01_forceatmo` proves the Dwarf asset usage. The installed
Erebor campaign town `town_E1` points to `sturgia_town_a`.

## Requirements for later scene checks

- Put a separate visible `outer_gate` and `inner_gate` in every active siege level.
  Native European outer doors sweep a width of about 5.70 m and reach 6.822 m high.
  Reserve the swing pocket and check the selected gate with actual clips. Reserve the
  outer gate's tactical position beyond the inner gate. The visible throat and sweep
  recess may have different widths.
- Make the intact and broken ends interchangeable. The siege scene needs matching
  `solid_child` and `broken_child` registrations and a usable path through the
  breach. The campaign handler can choose the two sided wall placements relative to
  the active ram.
- For each defender station, check body, crew positions, pile, moveable arm, access and
  field-facing visibility. Reserve ample level deck and test in the game. Defender AI
  checks that the castle-side standing face ID ends in `1`.

Relevant native references are `Native/Prefabs/spawners.xml` under
`mangonel_b_spawner`, `TaleWorlds.MountAndBlade.TeamAISiegeComponent`,
`CastleGate`, `WallSegment`, `UsableMachine` and `RangedSiegeWeapon`
in the installed v1.5.7 client. Scene setup also follows the
[castle gates reference](../../reference/engine/castle-gates.md).
