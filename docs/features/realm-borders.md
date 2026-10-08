# Realm Borders

## Overview

Draws every kingdom's border on the campaign map and keeps it in step with the campaign: a captured
castle, a clan changing sides, a rebellion, a new or a destroyed kingdom redraws the line within a
frame or two. Each town and castle owns a province grown over the map's own terrain, so a border
follows rivers, coasts and mountain ranges instead of cutting straight between two settlements, and
the wild land between realms stays unclaimed. Each realm's land is tinted in its colour between the
lines. A map-mode key switches between the realms, the Free Peoples against the Shadow, and the
player's allies and enemies; each realm's name is lettered across its land; and the player is told when
the party rides into another realm (#698). At full zoom-out a parchment map of Middle-earth fades in
under the borders. The player's settings, each realm's colour included, are in MCM.

## Why This Exists

- **Vanilla behavior:** Bannerlord draws no borders. Who holds what is readable only settlement by
  settlement, from banners and tooltips.
- **TAOM requirement:** Middle-earth is read by its realms: which bank of the Anduin is Gondor's, how
  far Mordor reaches after a siege, where the Free Peoples' front runs. The Kingdom Borders mod (Nexus
  10699) showed players want this. Its [adoption review](../reviews/adopt-kingdom-borders-2026-09-30.md)
  found its nearest-settlement split cuts across rivers and ridges and hands every patch of wild land
  to someone, and its renderer keeps one entity per strip and touches every one of them each frame.
- **Without this feature:** the political map stays invisible, and a war's front line exists only in
  the player's head.

## Architecture

### Design Challenge

- **Where a province ends.** A nearest-settlement split ignores the land. TAOM grows each province
  outward from its town or castle and its villages, over a cost grid read from the campaign navmesh's
  terrain types, and stops at a maximum claim.
- **Keeping it cheap.** Terrain never changes and fiefs never move, so the province map is computed
  once per map and reused by every campaign on it. A capture only re-selects which province edges are
  realm borders, that repaint runs on a worker thread, and only the map tiles whose content changed
  are rebuilt.
- **Drawing on the map scene.** There is no engine border primitive. The borders are runtime meshes
  of vertex-coloured quads draped on the terrain, one mesh per map tile. The parchment map is the same
  kind of mesh, carrying a picture of the map.

### Solution Approach

| Layer | Types |
|---|---|
| Domain (engine-free, `Domain/`) | `ProvincePartitioner`, `BoundaryTracer`, `RealmBorderSelector`, `PolylineMath`, `StripBuilder`, `BorderPainter`, `BorderStyleSelector`, `RealmFill`, `TileBinner`, `RealmPalette`, `RealmLabelPlacer`, `RealmNameLettering`, `BorderCrossingTracker`, `ProvinceBitmap`, `MapMode`, `RealmRelation`, `AtlasSheet` |
| Services | `RealmTerritoryService` (the per-map province map), `RealmBorderService` (snapshot, repaint on the worker, uploads, fade, notices), `RealmAtlasService` (the parchment map), `RealmBordersSettingsProvider`, `RealmPaletteProvider` |
| Adapters (`Main/Adapters/`) | `MapTerrainAdapter` (bounds, navmesh terrain type, height), `RealmMapAdapter` (fiefs, their realms, each realm's relation to the player, the player's realm and position, the clock), `BorderRenderAdapter` (the border meshes and the parchment map), `RealmNoticeAdapter` (the messages) |
| Entry points | `RealmBordersCampaignBehavior` (events; attaches the map view), `RealmBordersMapView` (keys, the per-frame calls, the names layer), `RealmBordersCheats` and `RealmAtlasCheats` (console) |
| Wiring | `RealmBordersModule` in `FeatureModules.All`. No Harmony patch, no GameModel, no save data |

The domain uses its own `MapPoint` rather than the engine's `Vec2`, so every geometry test runs on
hosted CI without the game.

### Component Diagram

```
navmesh terrain types           fiefs, owners, relations, the player
        |                                   |
 MapTerrainAdapter                    RealmMapAdapter
        \                                  /
     RealmTerritoryService (once per map: sample, partition on a worker, trace)
                          |
     RealmBorderService (game thread: snapshot; worker: select, smooth, paint, tile, names;
                         game thread: adopt, upload a few tiles a frame, fade)
        /                 |                    \
BorderRenderAdapter   Labels -> RealmNamesVM   RealmNoticeAdapter
(one mesh per tile)   (the names layer)        (mode and crossing messages)
        \                 |
         RealmBordersMapView  <- attached on the first map tick by RealmBordersCampaignBehavior
                          |
     RealmAtlasService (the parchment map at full zoom-out, drawn through BorderRenderAdapter)
```

### Realms

A fief's realm is its owner's map faction, as the engine reads it: the kingdom, or the owner clan
itself when that clan serves no kingdom. v1.5.3 raises a rebellion by creating a clan, not a kingdom,
so a rebel town shows as a realm of its own (keyed "clan:" and the clan id inside the adapter only)
and takes a reserve colour; so does an independent player's castle, which also takes the gold cord.
MCM's Your Realm colour goes to the player's own realm when the palette names none for it: their clan's
land while it serves no kingdom, then a kingdom they found. It follows the player from the one to the
other. Setting it hands the realm's reserve colour back for a rebel to take; clearing it gives the
realm a free reserve colour again. In a kingdom the palette names, that kingdom's own field applies.

### The province map

`RealmTerritoryService` samples a grid of 512 cells along the longer side of the map's bounds (so
at most 262,144 cells; `taom.print_realm_borders` prints the real size), one navmesh terrain query per
cell, 256 at a time within 3 ms of each map frame. TAOM_Map's settlements alone span 1,423 by 1,331
units, so a cell covers about 3 units. The flood and the tracing then run on a worker thread over
plain arrays. The result is kept per fief layout (ids, positions and village counts): a reload or a
second campaign on the same map reuses it, and a different layout recomputes it. Bounds that are not
finite fail the build rather than send a query to an infinite position.

| Terrain class | Navmesh terrain types (v1.5.3) | Cost to cross one cell |
|---|---|---|
| Open | Plain, Desert, Steppe, RuralArea, Dune, Beach, anything unknown | 1 |
| Rough | Snow, Forest, Swamp | 2 |
| River | River, NonNavigableRiver, UnderBridge | 12 |
| Crossing | Fording, Bridge | 1 |
| Wall | Mountain, Canyon, Cliff, LandRestriction | never crossed |
| Water | Lake, Water, CoastalSea, OpenSea, SeaRestriction, and cells with no navmesh face | never crossed |

The table holds while War Sails (NavalDLC) is not loaded, which TAOM refuses today: with it, the
navmesh lookup keeps only land-navigable faces, so mountains, rivers and lakes would read as water
(#120).

Every fief seeds its province from its town or castle and from each of its villages, with a head
start (town 30, castle 18, village 6), and a multi-source Dijkstra flood (8 neighbours, no diagonal
slip between two walls or two water cells) gives each cell to the cheapest seed within a claim of 190.
Unclaimed land patches smaller than 150 cells join the province that surrounds most of them, so a
stray hollow does not show as wild land. A seed sitting on a wall or on water starts from the nearest
open cell within 5 cells. The boundaries between provinces are then traced once as oriented chains.

### Choosing and drawing the borders

When anything may have changed (an owner, a kingdom, a war or an alliance, a setting, the map mode),
the game thread takes a snapshot: each province's realm, its group in the current map mode, each
group's colour and the look settings. If the snapshot equals the one last painted, nothing happens,
so a mercenary contract or a re-grant inside one realm costs a comparison. Otherwise the worker
paints: each boundary chain takes the groups of its two sides; a chain with one group on both sides
is not a border; the rest are joined into the longest lines they form, simplified (Ramer-Douglas-Peucker
at 0.9 of a cell), smoothed with four Chaikin passes, resampled every 1.2 map units, painted into
quads and binned into 128-unit map tiles with a content hash each. The realm names are placed again
only when an owner changed. Back on the game thread, a tile whose hash is unchanged is not rebuilt,
and uploads stop after 4 ms each frame. A repaint that was overtaken (the drawing was cleared while
it ran) is dropped and painted again.

| Look | When | Drawn as |
|---|---|---|
| Atlas | default | a watercolour wash inside each realm's edge in its colour, fading out, and a dark dash-dot ink line |
| Gold cord | political mode, the player's own frontier, "Gold Cord on Your Realm" on | the gold cord replaces the ink |
| Heraldic | "Heraldic Bands Instead of Atlas Look" on | two solid bands in each realm's colour, a gap on the line, thin dark keylines |
| War front | allies and enemies mode, where the player's side (own or allied land) meets an enemy | an ember glow with a bright core, 1.6 times wider and hotter where the player's own land stands on it |
| Land tint | "Colour Realm Lands" on (the default) | each realm's land between its borders in its colour at "Realm Colour Strength" (0.5), on a grid two cells apart, fading out beside another realm (where the border's wash carries the edge) and beside wild land and water (where the fade is the edge); drawn before the lines in each tile, so the lines sit on top. The allies and enemies mode leaves neutral land clear |

The ImagineArt paintings in [`tools/realm_border_art/`](../../tools/realm_border_art/) set the look; the
game draws the borders in vertex colours with no texture (the parchment map is the one textured layer).
A painted, textured wash is still an open spike.

`BorderRenderAdapter` builds each tile's mesh from a copy of an engine vertex-colour material with
`NoModifyDepthBuffer`, plus `NoDepthTest` while "Draw Borders Through Hills" is on, and in the late pass
after post effects: bit 0x20000000, which v1.5.3 names `AlwaysDepthTest` in C# and `render_after_postfx`
natively. Every border material draws there, so the parchment map never covers the borders, whichever
material MCM picks. A material that culls back faces gets every triangle in both windings; a two-sided
one (`vertex_color_blend_after_postfx_mat` is) gets one, since a second winding blends each pixel twice.
The tiles fade with `GameEntity.SetAlpha`; a new tile takes the current opacity. That drawing recipe was
learned from Kingdom Borders ([provenance](../reference/provenance-register.md)). "Automatic" tries
`vertex_color_blend_after_postfx_mat` first, which blends normally so the dark ink shows, then
`vertex_color_mat` and `vertex_color_lighting` (both in Native's core material packages); the log line
`[RealmBorders] drawing with material '<name>'` says which, with its blend and flags, and
`taom.realm_borders_material` tries another. Vertex heights come from the terrain directly (`Scene.GetTerrainHeight`, the heightfield vanilla drapes its hover
outline on; over water it is the bed), one query per distinct vertex, kept across repaints on the same map scene (up to 300,000) and
let go when the map screen closes. The first build used `MapScene.GetHeightAtPoint`, a physics query,
and one tile took 286 ms to build in the first look session. The engine blend mode is the material's own
unless MCM's "Border Blend Mode" or `taom.realm_borders_blend` picks another; the first look showed
glowing lines with no dark ink: `vertex_color_mat` blends with `AddAlpha`, which adds its colour onto
the map (logged as "its own blend AddAlpha"). `Modulate` is normal alpha blending; `Factor` turns
blending off.

No release ever drew the borders on the two-sided material: `vertex_color_mat` and
`vertex_color_lighting` were the only choices until the 2026-10-01 look session made the late-pass
material Automatic's first. Those session builds carried both windings on it, so everything in a tile
blended twice: the 0.3 land tint judged there drew at about 0.51 (1 - 0.7²), and the lines a little
darker than they now draw. The tint's default moved to 0.5 with the fix, to keep what was judged.

### Parchment map

As the camera nears its furthest zoom, a parchment map of Middle-earth fades in over the campaign map,
and the realm tint, borders and names draw on it. It starts to show at 0.8 of the furthest zoom
(`Campaign.MapMaximumHeight`, the height `MapCameraView` zooms out to) and is fully drawn from 0.95,
only while MCM's "Parchment Map at Full Zoom-Out" is on and the borders are showing, so the M key and
"Show Realm Borders" hide it too.

The picture, `ModuleData/realm_borders/atlas_parchment.png` (2048 by 2048), was generated with
ImagineArt from a guide made of TAOM_Map's own vista, heightmap and water mask, so the borders run along
its drawn coasts and rivers: from the real shore to the nearest ink, the median is 0 and 97% are within
2.3 of the terrain's 1600 units. Its generation record is the `atlas_parchment` entry in
[`tools/realm_border_art/provenance.json`](../../tools/realm_border_art/provenance.json).

`RealmAtlasService` builds the sheet once per map scene, on the first frame that needs it: an 8 by 8
grid of tile meshes over the 1600-unit terrain square, each 16 by 16 quads of 12.5 units, draped on the
terrain like the borders. Each tile has two layers:

| Layer | Material | Render order | Draws |
|---|---|---|---|
| Ink underlay | `vertex_color_blend_after_postfx_mat` | 126 | its corners' colour, the ink (`#3D281B`) |
| Picture | a copy of Native's banner-icon material `custom_banner_icons_09`, the picture as its diffuse map, blended with `Modulate` | 127 | the picture's brightness as opacity in the mesh's factor colour (`Mesh.Color`), the paper (`#FFF0D8`): paper where the picture is bright, see-through where it is dark, so the ink shows through the strokes |

Both layers draw with no depth test in the late pass, over land, water, smoke and map figures, under the
borders at the engine's default render order, 128 (v1.5.3's native mesh constructor sets it). The
picture's material also turns texture streaming off: a picture loaded from a file is not in the texture
streamer, and with streaming on the material drew it plain white. The paper's colour is set before the
entity's `SetAlpha`, which writes the alpha of the same native colour. The ink layer is a copy of the
picture's mesh, so the terrain is draped once per tile. The two layers and the paper colour have not
been seen in game yet; the look session saw the picture as one layer, in a neutral white with the 3D
map showing through its strokes.

The picture is loaded once per process, the way vanilla's 2D resource context loads a texture (always
valid, then preloaded); the log line gives its load time, size and memory. A build that fails (no map
scene is only a wait; no picture, no material, or an engine exception) logs once and is not tried again
until `taom.realm_atlas_rebuild` or the next map screen. The one-layer build the look session saw
cost 203 to 599 ms on the game thread; the two-layer build has not been timed (copying the picture's mesh
for the ink keeps it from doubling). The log line and `taom.print_realm_atlas` give its time.

**What the look session found (2026-10-01)**, so nobody tries these again:

| Tried | Result |
|---|---|
| `show_texture_2d`, `editmode_icons`, `editor_map_border` on the sheet | drew nothing on the map |
| `vertex_color_blend_mat`, `default_alpha` on the sheet | a plain sheet, ignoring the picture |
| `custom_banner_icons_09` | the picture, the only one of six that reads a texture set on a copy |
| texture row 0 at the map's south edge, first try | the picture upside down: the engine counts a texture's rows from the bottom |
| the sheet in the normal pass | the map's water, rivers and Mordor's smoke drew over it |
| the borders in the normal pass | the late-pass sheet covered them |
| a cream vertex colour on the picture's corners | ignored: the banner material colours through the factor colour, hence the ink underlay |

### Map modes

The map-mode key (G) cycles the modes and names the new one in the message log. Every session starts
in the political mode.

| Mode | Lines | Colours |
|---|---|---|
| Political | every pair of neighbouring realms | each realm's palette colour, the player's frontier gilded |
| Free Peoples and the Shadow | the one front line where the two sides meet; realms are grouped by `IAlignmentService.ResolveSide`, and a neutral realm (the `neutral` rows of `alignment.json`: Khand, Shaghâna, Abanissa and Umbar) stands on neither side, so it draws no line | pale blue-white for the Free Peoples, red for the Shadow |
| Allies and enemies | between realms of different standing towards the player: own, allied, enemy, neutral, by the settlement nameplates' own rule (at war, same faction, allied, else neutral) | the nameplates' frame colours (`NameplateRelationPalette`), so the map and the nameplates agree; a war front burns where the player's side meets an enemy |

### Realm names

Each realm's name is lettered at the deepest point (chamfer distance) of its largest piece of land,
when that piece has at least 400 cells. The name is written in spaced capitals in TAOM's aniron font
when aniron has every glyph (Latin and Cyrillic; `RealmNameLetteringTests` holds the table to
`aniron.fnt`), and as written in the language's own font otherwise, so a Chinese, Japanese or Korean
name never shows empty boxes. The names appear once the borders are 35% opaque and fade in with them
(through `Brush.GlobalAlphaFactor`, which is how Gauntlet fades text), are centred on the screen's
usable area, and never take a click: every widget ignores events and the layer sets no input
restrictions.

### Crossing notices

Every in-game hour the province under the player's party gives its realm. The first reading of a
session is silent; entering another realm then shows "You enter the lands of {REALM}." at most once
per realm every 12 in-game hours, so riding along a border does not repeat it. A party position that
is not finite reads as wild land.

### Where it does not run

- **A dedicated server:** the behavior registers nothing, so no map view is attached and no border
  is computed (`IDedicatedServerProvider`).
- **Beside the Kingdom Borders mod:** with module `KingdomBorders` active the behavior registers
  nothing and logs that TAOM's borders stand aside, so the map never carries two sets of lines.
- **Co-op:** every setting is presentation, listed as such in `CoopSettingsRelevance`; each client
  draws its own borders from its own campaign state.

### Departures from the approved design

Recorded so they can be re-weighed; each is the build's choice, not an oversight.

| Approved | Built | Why |
|---|---|---|
| Heights from a grid cached with the terrain sample, no native calls during a rebuild | One terrain query per distinct vertex, cached across repaints on the same scene | Measured offline on TAOM_Map's heightmap: a grid at the sampling resolution hides 3.1% of the strip under the terrain against 0.59% per vertex, and matching per-vertex accuracy needs about 4.2 million samples |
| A new realm takes the reserve colour farthest from its neighbours | Farthest from every colour in use | Neighbours are not known when a colour is handed out; the farthest-from-all choice maximises the smallest distance to every colour in use, neighbours included |
| Map modes in a second release | Shipped with the first | Built in one pass on Mike's word |
| Strip textures from the ImagineArt art | Vertex colours, no texture | The art set the look; a textured wash is the open spike |
| Widths banded by zoom | One world width, scaled by the "Border Width" setting | To judge in the look session |
| Care at three-realm junctions | Line ends meet at the junction point | To judge in the look session |

## Configuration

### MCM: group "Realm Borders"

| Setting | Default | Controls |
|---|---|---|
| Show Realm Borders | on | the whole feature; off clears the map, and the two keys do nothing while it is off |
| Heraldic Bands Instead of Atlas Look | off | the Heraldic look |
| Gold Cord on Your Realm | on | the player's frontier in gold (political mode) |
| Border Width | 1 | a width multiplier, 0.5 to 3 |
| Fade In From Camera Distance | 45 | hidden when the camera is closer than this |
| Full Opacity Distance | 110 | fully drawn from this distance out |
| Draw Borders Through Hills | on | off lets ridges hide the lines |
| Realm Names | on | the names layer |
| Border-Crossing Notices | on | the hourly crossing message |
| Colour Realm Lands | on | the land tint between the borders |
| Realm Colour Strength | 0.5 | the tint's strength, 0.05 to 0.8 |
| Parchment Map at Full Zoom-Out | on | the parchment map under the borders at the furthest zoom |
| Border Blend Mode | Material default | the engine blend mode the borders are drawn with (advanced; for the look) |
| Border Material | Automatic | the engine material the borders are drawn from (advanced): Automatic, `vertex_color_mat`, `vertex_color_lighting` or `vertex_color_blend_after_postfx_mat`; Automatic tries the last first |
| Realm Colours (sub-group) | blank | one `#RRGGBB` field per realm; blank keeps the palette colour its tooltip names |
| Your Realm (in Realm Colours) | blank | the player's realm when the palette names none for it: their clan's land while it serves no kingdom, then a kingdom they found; blank takes a free colour |

`RealmBordersSettingsProvider` re-validates what it reads: a width that is not a number or outside
0.5 to 3 becomes 1, and a fade pair with either value not a number, outside 0 to 5000, or a start not
below the full distance reverts both to 45 and 110. Each reversion logs one warning naming the
setting, repeated only when the value changes; a tint strength outside 0.05 to 0.8 becomes 0.5, and a
realm colour that is not `#RRGGBB` keeps the palette's. Every setting applies on the next map frame. The
two dropdowns persist by index, so their lists are pinned by tests and never reordered; a console
command stands until its dropdown is changed. The
defaults live once, in the provider, and `TaomSettings` reads them from there; changing a shipped
default means renaming the setting, since MCM keeps a player's saved value. Realm Colour Strength moved
from 0.3 to 0.5 before any release carried Realm Borders, so no player holds the old value; a `TAOM.json`
from a development build keeps 0.3 until the group is reset.

### Keys: Options > Keybindings > Campaign Map

| Key | Default | Game key id |
|---|---|---|
| Show Realm Borders (TAOM) | M | 520 |
| Realm Borders Map Mode (TAOM) | G | 521 |

`TaomRealmBordersHotKeyCategory` registers them in `OnSubModuleLoad` like the time controls; their
Options names are rows in `ModuleData/global_strings.xml`.

### Palette: `Main/_Module/ModuleData/realm_borders/palette.json`

| Field | Type | Description |
|---|---|---|
| `realms` | object | kingdom id to `#RRGGBB` |
| `reserve` | array | colours for realms that appear in play, the one farthest from the colours in use first |
| `minimumDeltaE` | number | the smallest CIE76 distance any two colours, reserve included, may have (15) |
| `minimumLightness` | number | the darkest L* allowed (25), so no realm reads as black |

The palette is separate from banner colours on purpose: banner colours left 9 of the 22 realms of
the time near black and made Gundabad and the Misty Mountain Orcs look alike. `RealmPaletteTests` fails when a
kingdom is missing, two colours (reserve included) sit closer than `minimumDeltaE`, or one is darker
than `minimumLightness`. A malformed colour is skipped with a warning and that realm takes a reserve
colour, or the Your Realm colour when it is the player's. The file is read once per process, so an edit
needs a full game restart; each campaign then builds its own palette, so the reserve is handed out from
its best colour again in every campaign.

## Console

| Command | Tier | What it does |
|---|---|---|
| `taom.print_realm_borders` | A | The state line: enabled, visible, mode, the province build state with its sampling and partition times, grid size and chains, the last repaint's worker time, lines and quads, whether one is running, tiles drawn and queued, the slowest single tile upload, fade, labels, material and blend mode |
| `taom.print_realm_province_map` | A | Writes `Logs/taom_realm_provinces.bmp`: each realm in its colour with its edges dark, a fief without an owner white, wild land parchment, water slate blue, north up |
| `taom.realm_borders_rebuild` | B | Samples the terrain again, recomputes every province and redraws; nothing saved changes |
| `taom.realm_borders_material <name>` | B | Redraws the borders from another engine material; refused when no material has that name |
| `taom.realm_borders_blend <mode>` | B | Redraws the borders with another engine blend mode (NoAlphaBlend, Modulate, AddAlpha, Multiply, Add, Max, Factor and the rest of the engine's list); the status line shows the mode in use |
| `taom.print_realm_atlas` | A | The parchment map's state: MCM's switch, built or not and how long it took, its material and picture, opacity, the camera's zoom against its furthest, the fade band and colours |
| `taom.realm_atlas_rebuild` | B | Draws the parchment map again the next time the camera is zoomed out to it, after a failed build too |
| `taom.realm_atlas_tint <paper> [ink]` | B | Redraws the parchment map in another paper colour and, if given, ink colour (`#RRGGBB`), until the game restarts |
| `taom.realm_atlas_fade <start> <full>` | B | Moves the fade band, as fractions of the furthest zoom (0 to 1, start below full), until the game restarts |

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/RealmBorders/RealmTerritoryService.cs` | Terrain sampling, the worker partition, the per-layout cache |
| `Main/Features/RealmBorders/RealmBorderService.cs` | Snapshot, the worker repaint, tile uploads, fade, modes, labels, crossing notices, console hooks |
| `Main/Features/RealmBorders/Domain/ProvincePartitioner.cs` | The multi-source cost flood and the pocket merge |
| `Main/Features/RealmBorders/Domain/BoundaryTracer.cs`, `RealmBorderSelector.cs` | Province boundary chains, then borders from the grouping |
| `Main/Features/RealmBorders/Domain/BorderPainter.cs`, `BorderStyleSelector.cs`, `BorderLook.cs` | The looks, as coloured quads |
| `Main/Features/RealmBorders/Domain/MapMode.cs`, `RealmRelation.cs` | The map modes and the allies and enemies mode's relation groups |
| `Main/Features/RealmBorders/RealmBordersModule.cs`, `RealmBordersIoC.cs` | Module wiring and registrations |
| `Main/Features/RealmBorders/Hooks/RealmBordersCampaignBehavior.cs` | Events, the dedicated-server and Kingdom Borders gates, the map view |
| `Main/Features/RealmBorders/UI/` | The map view, the names view models, the key category |
| `Main/Features/RealmBorders/RealmAtlasService.cs`, `Domain/AtlasSheet.cs` | The parchment map: when to build and show it, its quads and its fade |
| `Main/Features/RealmBorders/Cheats/RealmBordersCheats.cs` | The borders' five console commands |
| `Main/Features/RealmBorders/Cheats/RealmAtlasCheats.cs` | The parchment map's four console commands |
| `Main/Adapters/BorderRenderAdapter.cs` | Tile meshes and the parchment map on the map scene |
| `Main/Adapters/MapTerrainAdapter.cs`, `RealmMapAdapter.cs`, `RealmNoticeAdapter.cs` | Terrain, campaign state, messages |
| `Main/_Module/GUI/PreFabs/RealmBorders/TaomRealmNames.xml`, `GUI/Brushes/TaomRealmBorders.xml` | The names layer and its two fonts |
| `Main/_Module/ModuleData/realm_borders/palette.json` | The realm palette |
| `Main/_Module/ModuleData/realm_borders/atlas_parchment.png` | The parchment map's picture; its record is in `tools/realm_border_art/provenance.json` |
| `tools/realm_borders_preview.py` | The offline prototype and tuning tool (`compare`, `capture`, `looks`, `tiles`) |

## Dependencies

- `IAlignmentService` (Execution): the side of each realm in the Free Peoples mode.
- `IDedicatedServerProvider` (CoopInterop): the dedicated-server gate.
- `NameplateRelationPalette` (SettlementNameplateRelation): the allies and enemies mode's colours,
  read from its constants.
- `HexColorParser` (SceneScripts): palette and relation colours.
- `IPathService` (Core): where the palette file and the parchment picture live.
- `IModLogger` (Core): state, timings and warnings under `[RealmBorders]`.

## Tests

258 test methods in `TAOM.Tests/Features/RealmBorders/`:

- `ProvincePartitionerTests` (16), `BoundaryTracerTests` (7), `RealmBorderSelectorTests` (7): the flood,
  walls, rivers, diagonal ridges, pockets, water, NaN seeds and positions, chain tracing (saddles and
  four-province corners included) and joining.
- `PolylineMathTests` (7), `StripBuilderTests` (7), `BorderPainterTests` (9), `BorderSupportTests` (22):
  smoothing, strips, every look, resampling, terrain classes keyed by the engine's own enum, tiling,
  modes, relation groups, labels, the crossing tracker.
- `RealmPaletteTests` (20): the shipped palette's gates, reserve included, colour assignment, the
  player's colours (Your Realm included) and the reserve colour they hand back, the strict `#RRGGBB`
  parse, and the provider's missing-file, malformed-file and per-campaign paths.
- `RealmTerritoryServiceTests` (12), `RealmBorderServiceTests` (60): the real pipeline over fake
  adapters, including a capture moving the line, skipped repaints, a repaint overtaken on the worker or
  failing there,
  the fade, the toggle off and on, the keys while off, every mode, the gold cord, notices and their
  guards, session reset, the map screen closing, the material and blend choices, the land tint, the
  MCM colours with Your Realm, the province picture, and no opacity while MCM switches the feature off.
- `RealmFillTests` (7): each realm in its own colour, the fade beside another realm, wild land, water
  and uncoloured groups left bare, zero strength, full strength deep inside, one colour per quad.
- `RealmAtlasServiceTests` (23), `AtlasSheetTests` (6): the parchment map built once from the module's
  picture, only while MCM's switch is on and the borders show, faded with the zoom; a build with no map
  scene waits, one that fails or throws (partway included) leaves nothing drawn and is not retried every frame, and the next map screen or a rebuild
  tries again; the fade band refuses NaN, infinity and an inverted pair; the tiles cover the square with
  the picture north up.
- `RealmBordersProviderTests` (27): the settings' fallbacks and colour fields, the dropdown lists pinned in order, every
  border material in the late pass, the second winding only for a material that culls back faces, the
  parchment map's flags and render orders, and its switch on with no settings. `RealmNameLetteringTests`
  (7), `RealmNamesVMTests` (4), `RealmNamesPrefabTests` (4), `ProvinceBitmapTests` (3).
- `RealmBordersWiringTests` (10): the module listed once, no hand registration, the behavior's gates and
  events, the key category and its Options names, the map view driving the parchment map every
  frame and on close, and the renderer's call sites for the winding, the late pass and the render orders.

`python -m pytest tools/tests/test_realm_borders_preview.py` covers the prototype.

## How to recolour a realm

1. Edit its `#RRGGBB` in `Main/_Module/ModuleData/realm_borders/palette.json`. A new kingdom id goes
   under `realms`.
2. Run `dotnet test TAOM.Tests --filter FullyQualifiedName~RealmPaletteTests`; it names any pair that
   became too close or a colour that became too dark.
3. Restart the game. No code change is needed.

## Performance

- **Once per map:** one navmesh query per grid cell (at most 262,144), at most 3 ms of them a frame,
  then the flood and tracing on a worker.
- **Per change:** a snapshot on the game thread (one realm lookup per fief, one relation or side per
  realm), then, when something on screen changes, the repaint on the worker and at most 4 ms of tile
  uploads a frame.
- **Per frame otherwise:** a settings comparison, the fade value quantized to 1/50 and pushed to the
  meshes only when it changes, the names projected onto the screen, and the behavior's check that its
  map view is attached. The parchment map adds its own quantized fade, pushed only when it changes.
- **The parchment map, once per map scene:** 64 entities, each a mesh of 256 quads and its copy for the
  ink, built on the game thread in one frame the first time the camera reaches 0.8 of its furthest zoom,
  plus the picture's load the first time in a process. The look session measured 203 to 599 ms for the
  one-layer build; the two-layer build is untimed, and the log line gives the time.
- **Measured offline** by the review's harness (2026-09-30), running this C# on TAOM_Map's heightmap
  standing in for the navmesh: a repaint takes about 37 ms (63 ms the first time in a process) with the
  Debug build TAOM ships, now off the game thread; the partition and trace take about 50 ms on the
  worker; a capture re-uploads a median of 6 tiles. The game's own times are logged and printed by
  `taom.print_realm_borders`.

## Not yet run in game

Unit tests cover the geometry and the service over fake adapters. The look sessions of 2026-09-30 and
2026-10-01 saw the borders, the land tint, the realm names and the one-layer parchment map drawn; the
boxes below are still owed.

- [ ] A new campaign: borders appear within seconds of the map opening; `taom.print_realm_borders`
      reports `territory=Ready` with its sampling, partition and repaint times.
- [ ] The log names the material drawn with; if none exists, find one with `taom.realm_borders_material`.
- [ ] The wash fades (the material honours vertex alpha; Kingdom Borders drew one colour per
      triangle, so it never proved this) rather than showing as solid bands.
- [ ] "Draw Borders Through Hills" on and off, at the near and far camera range: off must still hide
      the lines behind ridges now that every border material draws in the late pass.
- [ ] With one winding on the two-sided material, the tint at 0.5 looks as the 0.3 did, and the ink
      line is still dark enough; a development `TAOM.json` holds 0.3 until reset.
- [ ] The parchment map's two layers: cream paper, sepia ink through the strokes, no 3D terrain or
      water showing through, and the borders, tint and names on top of it.
- [ ] The parchment map fades in from 0.8 to 0.95 of the zoom rather than popping in, and the build's
      log time with the copied ink mesh.
- [ ] MCM's "Parchment Map at Full Zoom-Out" off hides it at once; on shows it again without a rebuild.
- [ ] Every MCM Border Material keeps the borders over the parchment map.
- [ ] MCM Border Material `vertex_color_mat` and `vertex_color_lighting` at mid zoom, against v2.0.32:
      both now draw in the late pass at every zoom, without the map's fog and colour grading.
- [ ] "Draw Borders Through Hills" off: the `drawing with material` log line must not list
      `NoDepthTest`; if the material carries it itself, the setting cannot take it away.
- [ ] The fade distances 45 and 110 against the camera's full range (a changed default needs a
      renamed setting).
- [ ] Line legibility at the farthest zoom, a three-realm junction up close, Isengard's pale wash over
      snow.
- [ ] Capture a castle: the border moves and only nearby tiles rebuild (`slowestUpload` stays low).
- [ ] Each map mode's look; the allies and enemies mode's colours match the settlement nameplates.
- [ ] A rebellion's town gets a border of its own in a reserve colour; a kingdomless player's castle
      takes the gold cord.
- [ ] With Your Realm set, a kingdomless player's castle takes that colour and keeps it when the
      player founds a kingdom; clearing the field gives the realm a free colour.
- [ ] M and G work; Options > Keybindings > Campaign Map shows both names, in English and one
      translated language.
- [ ] Realm names at a far zoom fade in with the borders; a Chinese game shows them in the plain font;
      a click under a name still reaches the map.
- [ ] Riding over a border shows the crossing notice once.
- [ ] Save and load: the borders redraw and no second `sampling terrain` line appears for the same map;
      a second campaign in the same process starts its reserve colours afresh.
- [ ] `taom.print_realm_province_map` writes a picture whose provinces match the map.

## Changelog

- 2026-09-30: first release (#698). Terrain-aware provinces, live borders in the Atlas and Heraldic
  looks with the player's gold cord, three map modes (the third by relation to the player, in the
  nameplate colours), rebel clans as realms of their own, realm names, crossing notices, the repaint on
  a worker thread, console commands. The eight new strings are drafted in all twelve languages.
- 2026-09-30: each realm's land tinted between its borders; new colours for Harad, Rhûn, Khand,
  Rivendell, Rohan, Dunland, Isengard and Umbar; every look control in MCM, with a colour field per
  realm and a Your Realm colour for the player's realm when the palette names none; heights read from
  the terrain directly.
- 2026-10-01: the parchment map at full zoom-out, under the borders, with its MCM switch and four
  console commands; every border material in the late pass, so the parchment never covers the borders;
  one winding on a two-sided material, which had blended every tile twice in the look session's
  builds, with the tint's default moved from 0.3 to 0.5 to keep the judged look; new colours for Rohan, Harad, Gundabad, Rivendell
  and Mirkwood.

## GitHub Issue

- **Issue:** #698, [Realm Borders](https://github.com/haterade22/TAOM/issues/698)
- **Status:** Open until the in-game checks above pass
