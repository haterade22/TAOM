# Custom Map for Warsails Guide

How to integrate the Warsails naval mod with a custom Bannerlord map.

## 1. Update Cultures

Add all missing attributes from the Warsails cultures XSLT to each of your custom cultures.

## 2. Create Party Templates

Create new fishing, naval caravan, and naval patrol party templates for each culture. Alternatively, reuse the 6 native party templates for your custom cultures.

## 3. Incorporate Warsails Entities

Incorporate all Warsails settlement, clan, kingdom, and lord IDs into your modules. This may work by removing them all with XSLT instead of manually merging.

## 4. Convert Island Castles

Change all island castles to port towns, or add bridges/rocky passageways to make the castles and villages accessible by land.

## 5. Nav Mesh Setup

Configure nav mesh around all shores and water:

| Area | Nav Mesh Tile |
|------|---------------|
| Shore borders | Tile 7 |
| Shallow water (ocean) | Tile 18 |
| Deep water (ocean) | Tile 19 |
| Under bridges | Tile 25 |
| Navigable rivers | Tile 11 |
| Holes / unnavigable gaps | Tile 10 |

Make sure all holes in the nav mesh are filled with an unnavigable tile (e.g. tile 10).

## 6. Add Port Town Entities

For each port town in the world map scene:

1. Add a blockade script to the potential port town entity
2. Add the following child entities to the port town entity:
   - `port_town` (with the `city port` tag)
   - `blockade_start`
   - `blockade_end`

## 7. Iterative Settlement Distance Cache Generation

This process must be done one port at a time:

1. Save the scene and exit the editor tools
2. Delete all port coordinates from your `settlements.xml` **except for one**
3. Start the game in sandbox mode
4. When you reach the world map, run the **LT Settlement Distance Cache Generator** until it reports success
5. Exit the game
6. Remove the `new` prefix from the 3 newly generated SDC (Settlement Distance Cache) files
7. Add the coordinates back for your next port in `settlements.xml`
8. Reopen the game and run the LT SDC script again to incorporate the new port's distance data
9. Repeat until all desired ports are incorporated
