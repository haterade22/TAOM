# Kingdom kit layout

Where a kingdom's settlement kit lives in the map module, how its files are named and how the state of a file is tracked. Decided on 2026-11-26 for the kingdom kits job (#1600) on the maintainer's word: "I would structure and organize it better than Rivendell\kit preview. Organize it for the long term project we are doing." Rivendell is the first kit in this shape. The older kits (Gondor, Erebor, Mirkwood, Mordor, Rohan) keep their folders: their imported assets depend on those paths.

## The tree

The kingdom folder is `TAOM_Map\AssetSources\Scenes\<Kingdom>`. The Modding Kit mirrors the source tree in its resource browser (`Assets\Scenes\Gondor` holds the same folders as `AssetSources\Scenes\Gondor`; listed 2026-11-26), so this tree is what a scene builder sees.

```
Scenes\Rivendell\
  README.md       the manifest: every file, its pieces, its state (generated, never typed)
  walls\          the castle family: rivendell_castle_*
    rivendell_castle_kit.fbx              walls of 13, 20 and 27 m
    rivendell_castle_breach_kit.fbx       the broken wall
    rivendell_castle_towers_kit.fbx       wall towers, the corner tower, the keep
    rivendell_castle_gatehouses_kit.fbx   gatehouses
    rivendell_castle_stairs_kit.fbx       stairs to the wall walk
  buildings\      rivendell_house_* and rivendell_hall_*
    rivendell_house_kit.fbx
    rivendell_hall_kit.fbx
  village\        rivendell_village_*: cottage, long house, barn, store, low walls, well
    rivendell_village_kit.fbx
  structures\     rivendell_open_*: terraces, railings, stairs, slabs, gate arch, arcade, pavilion, bridge
    rivendell_open_kit.fbx
  props\          rivendell_small_*: fountain, lamp post, bench, statue plinth
    rivendell_small_kit.fbx
  parts\          rivendell_part_*: the part layer on the tile grid
    rivendell_part_kit.fbx
  textures\       the shared texture sets t_rivendell_* and their materials
  _variants\      looks and readings that wait for the owner's choice; deleted after it
    rivendell_variants_kit.fbx
```

## Rules

1. **One table decides the folder.** The family word of a piece name gives its folder: `castle` to `walls`, `house` and `hall` to `buildings`, `village` to `village`, `open` to `structures`, `small` to `props`, `part` to `parts`. `walls`, `buildings` and `textures` are the names that the Gondor and Erebor folders already use.
2. **A kit file is named `<kingdom>_<family>[_<kind>]_kit.fbx`** and holds only pieces whose names start with `<kingdom>_<family>_`. The castle family is split by kind, because a wall with its levels and bodies is large. The one exception is the `_variants` file.
3. **A piece has its final name from its first build.** A later import of the same file in the same folder replaces the piece in place, so a scene that uses it keeps working.
4. **Nothing temporary gets a folder or a file name.** No `preview`, `trial`, `tmp` or date in a name under the kingdom folder. The state of a file (a first build at level 0, or an approved look with levels and bodies) is a column of the manifest.
5. **`_variants` holds only choices that are open:** a second look (`_looka`), a second reading (`_readingb`, `_solid`), a second break look (`_dest_masonry`). After the choice the owner deletes the folder and its imported assets.
6. **Lower case** for every folder and file name under the kingdom folder. The Kit turns mesh names to lower case, and a prefab's mesh lookup is case sensitive.
7. **Textures** are `t_<kingdom>_<set>_<variant>_<d|n|s>.png`, 1024 px, in `textures`. Every family uses the shared sets; a family adds a set only when no shared set serves.
8. **A copy into the module adds new files, or replaces our own unchanged file.** A later build of a kit file may replace the file in the module only when the file there still has the hash of our last copy (the mode `--replace-own`); the old file is first kept in a backup folder outside the module. The tool never deletes, checks the hash of each file after the copy and writes an undo list. After a replacement the owner imports that file again in the Kit, and the check proves it by the hash of the source that the Kit stores in the package. Until the kit code is in the repo, the tool is `kit_full\editor_set\tools\copy_kit_set_to_module.py` in the work folder.
9. **Outside the source tree** the names follow the Gondor ones: the prefab file `Prefabs\taom_<kingdom>_kitbash.xml` and the showcase scene `mod_taom_<kingdom>_kitbash`.
10. **One asset a piece.** A piece that a scene builder places as one thing is one asset in the Kit. Its parts are sub-objects of one mesh, named `<piece>.<part>`: the Kit joins every object that shares the prefix before the first dot ([gondor/README.md](gondor/README.md), "A piece is a part family"). A part is its own mesh, with a suffix such as `_m1`, only where the engine needs its own entity: a parapet part of a wall, a tower or a gatehouse (the siege code hides and breaks them one by one) and a stair inside a tower (one body carries one set of flags). The prefab then places the whole piece, so the prefab file goes to the scene builder together with the meshes. The maintainer, 2026-11-26, on a castle stair that came as three assets: "These should be made all together, no one is going to need stair railings. So stairs come with the railings."

## State on 2026-11-26

- In the module since 06:41: `walls\rivendell_castle_kit.fbx` and 12 maps in `textures`. Imported in the Kit by the maintainer at 10:38 to 10:40 (12 texture packages, 4 materials, the mesh package); the read-only check passed textures and mesh at 10:45, and four material flags were still to set.
- In the module since 10:53: ten more kit files of first builds (50 pieces, level 0, no lower levels, no bodies), six maps of two new texture sets and the manifest `README.md`. An independent checker passed the files before the copy. Imported by the maintainer by 11:01; a read of the packages found all 99 meshes bound to their materials.
- In the module since 17:08 (kit set 3, by the replace mode): five kit files written again (the stairs, the open structures, the towers with the new corner tower, the gatehouses, the variants) with the parts of 13 pieces as sub-objects (rule 10), and two blue trim colour maps. Not imported yet. The prefab file `Prefabs\taom_rivendell_kitbash.xml` is in the module since 16:24 and still names the part meshes of kit set 2: it is made again after the import, and the 20 old part assets are deleted only after that.
