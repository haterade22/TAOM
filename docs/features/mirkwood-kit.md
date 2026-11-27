# Mirkwood settlement kit

## Overview

Mirkwood has an editable Blender kit for castle and town frontages, a civic hall,
housing, village buildings, and the ravine approach. The 26-piece Asset Browser
library shares weathered limestone and dark heartwood trim across these assets.
[#1620](https://github.com/haterade22/TAOM/issues/1620) tracks siege integration.

## Why this exists

The installed Mirkwood kit has architectural pieces used by other kingdoms.
The measured settlement data gives all 24 Mirkwood settlements Battania scenes.
The new kit gives scene builders distinct Mirkwood architecture and shared wall
joins for the castle and town siege requirements.
[Installed catalog and scene coverage](../kitbash/mirkwood/coverage.md)

## Siege and architectural layout

The 20 m intact wall, traversable breach, 16 m gatehouse, towers, curved returns,
and stairs connect to the same wall walk. The 6.2 m gate passage has two seats
8 m apart. The castle layout uses four defender towers. The town uses four forward
batteries with access stairs. The tested defender footprint is 5.50 × 5.72 m.
Both compounds connect their gatehouses at grade to the hall and housing.
[Measured siege kit](../kitbash/mirkwood/siege-kit.md) · [Compound layouts](../kitbash/mirkwood/settlement.md)

## Files and verification

The [delivery record](../kitbash/mirkwood/delivery.md) identifies the editable
master, the Asset Browser catalog, the staged visual FBXs, and the local rerun
commands. The Blender checks covered saved placement origins, wall and stair
joins, static clearance at both gate seats, and 1,728 route samples across the
two compounds. Separate Blender files test distance levels, walk proxies, and
the optional gate leaves. The [build record](../kitbash/mirkwood-kit.md) carries
the dated checks and the [lessons](../kitbash/mirkwood/lessons.md) explain the
failures they caught.

## How to verify in game

Use the pinned Bannerlord v1.5.7 build and Modding Kit. These checks are pending
for the new assets. Prepare one castle test scene and one town test scene after
importing the selected FBXs and binding the native prefabs. Use the
[settlement scene needs](../kitbash/settlement-scene-needs.md),
[castle gate reference](../reference/engine/castle-gates.md), and
[measured defender controls](../kitbash/mirkwood/siege-controls.md)
for the entity names, roles, and scene requirements.

1. In the editor, check that each new mesh has its own collision body, the
   intended physical surface, and the correct texture channels. Inspect the
   import frame, pivot, bounds, and LODs on a wall, tower, gatehouse, and house.
   Check the stair body's `has_steps` flag. Take near and distant screenshots
   of the carved limestone and timber.
2. In each test scene, cross the approach, gate chamber, wall walk, stairs,
   and breach with an agent. Check that the floor, guard collision, and navmesh
   follow the intended route and the openings stay clear. Record where an
   agent stalls, falls, or crosses a closed barrier.
3. Test the native `outer_gate` and `inner_gate` prefabs independently from
   attack and defense at every active siege level. Observe gate movement,
   blocking while closed, open traversal, and the gate navmesh change.
   Check the ram path to the outer seat. Switch the wall's `solid_child`
   and `broken_child` and check that both align to the same wall joins
   and use their intended collision and routes.
4. Place and use the defender machine and ammunition at a tower and at a
   forward battery. Verify spawn, crew access, operator and firing clearance,
   and defender navigation from the town side. Run the attacker approach to
   the same frontage. Capture one result per layout against issue #1620.
