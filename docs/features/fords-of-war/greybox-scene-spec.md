# Fords of War: greybox scene spec

For Mike, building the first purpose-built Fords of War scene in the Modding Kit. The mode already
runs on a lane layout it generates from the boundary of an existing field battle scene. A
greybox replaces the generated layout with lanes you place by hand, so lane width, choke points and
tower spacing can be tuned.

## Scene

- **Id:** `taom_fords_greybox_01`. Save it in the live `TAOM_Map/SceneObj/`, like the other TAOM
  scenes. Keep a copy outside the module: `TAOM_Map` is not versioned, so a reinstall removes it.
- **Size:** a flat playable area of about 500 m by 250 m. West base at one short end, East base at
  the other.
- **Terrain:** flat or gently rolling, plain textures. No forests on the lanes. Props only where they
  shape a lane.
- **Boundary:** a battle boundary around the playable area, as on any field battle scene.

## Lanes

Three lanes from the West base to the East base: a straight middle lane and two side lanes that
leave each base together and bow out to about 45 m from the middle. Each lane is 12 to 20 m wide.
Add one or two cross paths between neighbour lanes near the middle.

**Waypoints:** an empty entity per waypoint, about every 12 m, tagged `fow_lane_<lane>_<n>`. Lanes
are numbered 0, 1, 2 from the West side's left; `n` counts from 0 at the West base end. The mode
reads them in order.

## Structures

All of these are entities with a `DestructableComponent` script. For the greybox, use the Native
prefab `siege_barricade_a`, scaled up. The mode sets the hit points from `match.json`, so the
prefab's own value does not matter.

| Tag | Where |
|---|---|
| `fow_tower_west_<lane>_0` | The West outer tower of a lane, about 36 percent of the lane length from the West base |
| `fow_tower_west_<lane>_1` | The West inner tower, about 20 percent from the West base |
| `fow_tower_east_<lane>_0`, `fow_tower_east_<lane>_1` | The same, measured from the East base |
| `fow_keep_west`, `fow_keep_east` | The keeps, close to each base |

Place each tower beside its lane, not across it, so troops can reach it from two sides.

## Other markers

| Tag | Where |
|---|---|
| `fow_capture_<n>` | Capture points, one on each cross path between neighbour lanes |
| `fow_camp_<n>` | Neutral camp positions between lanes (used in phase 3) |

## Navmesh

The navmesh must cover every lane, every cross path, the bases, and a ring of at least 4 m around
each structure, so attackers can surround it. No navmesh on top of structures. Check that a unit can
walk from each base to the other base along each lane.

## Hand-off

Tell me the scene id when it is saved. I then add it to `maps.json` with `"layout": "tags"`, and the
mode reads your markers instead of generating a layout.
