# Codex pre-review: the harness item of the black Rhûn chariot set (2026-11-26)

You are an independent, adversarial reviewer. Work READ ONLY: change no file, run no command that writes into the
repo or the game install. Report defects with evidence; propose no rewrite beyond the one line that fixes a
defect. Never name the upstream chariot pack or its author: write "the upstream chariot pack".

## The change

The maintainer's word: "add it into the XMLs, new item. We will make a special troop for Khamul's line in Rhun".
A new chariot cart with horse armour for both horses (one skinned mesh, `rhun_war_chariot_f`, on
`horse_skeleton_chariot`) was imported by the maintainer in the Modding Kit that morning. This change makes it
an item.

1. Live, unversioned module `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory`:
   - `ModuleData\LOTRLOME_items\LOTRAOM_horses.xml`: ONE new `<Item id="taom_chariot_armour_rhun_black"`
     `Type="HorseHarness" mesh="rhun_war_chariot_f">`, with a comment above it, inserted between the items
     `taom_chariot_armour_rhun` and `taom_chariot_armour_imperial`. Its values are those of
     `taom_chariot_armour_rhun`. Every other row of the file is out of scope.
   - `ModuleData\Languages\loc_LOTRAOM_horses.xml`: ONE new `<string id="taom_chariot_armour_rhun_black">` row,
     written by `python tools/register_armory_names.py --apply --prefix "^taom_chariot_armour_rhun_black$"`.
2. This checkout (a sibling worktree, branch `feat/rhun-chariot-variants`; `git diff -- tools` shows it):
   `tools/armour_cover_flags.json` (+1 entry), `tools/data/armour_colour_families.json` (+1 entry, the record
   `tools/scan_armour_colours.py --apply` writes for the item: family `black`, how `word black`, texture ``),
   `tools/external-loc-coverage-baseline.json` (`armory` +1 in each of 12 languages).
3. Listed only (binary, written by the Kit): `Assets\creature\chariot\mesh\rhun_war_chariot_f_geo.tpac` and twelve
   packages in `Assets\creature\chariot\textures` (nine `_tex`, three `_mtl`).

The scripts that wrote the live files are in
`E:\Temp\claude\1d198fa1037d6973\b8435c17-f812-4316-b2a1-047ee581f28e\scratchpad\mine\`: `add_black_item.py` (the
item row) and `install_set_f.py` (the ten source files into `AssetSources`).

## Facts read by the session (test them, do not trust them)

- `python tools/data_gates.py <the two live files>` from this checkout: 22 PASS, 2 FAIL, 1 NOT RUN. The two
  failing gates (`armory-refs`, `armour-colours`) failed before the edit with the same content; `loc-tests` is a
  dotnet run that the gate runner does not start.
- `python tools/validate_mesh_refs.py --check-name rhun_war_chariot_f`: TAKEN, one package (the mesh package
  above). `python tools/check_rdc_entries.py`: none of the thirteen new packages lacks a cache entry.
- No troop, lord or kit references the new item.

## Known suspects: check each against the code and the data

1. **The item row against the engine and against its siblings.** Read how the game reads a `HorseHarness` item
   (the installed DLLs; `pwsh tools/taom-src.ps1 path ItemObject` gives the decompile path) and compare the new
   row attribute by attribute with `taom_chariot_armour_rhun`, `taom_chariot_armour_imperial` and the two dwarf
   harness rows. Is any attribute wrong, missing or inert for this item type? `family_type="4"` is shared with
   the dwarf ram chariot (`taom_ram_chariot_a`): can a Rhûn cart harness now land on rams, and through which
   path (a troop row, a random equipment roll, a market, a smithy, the encyclopedia)? Is that new, or already
   true of the two existing Rhûn harness items?
2. **Who picks up a new Armory item without being told.** Search `tools/` for generators and audits that walk
   every item of the Armory or every `HorseHarness` (lord armour variants, armour band clones, armour classes,
   the restat, the starter kit, the armour colour scanner, the Armory audit) and say for each whether the new
   row changes its output, and whether a later `--apply` of that tool would hand this harness to a lord, a
   troop or a market. `is_merchandise="false"` and `culture="Culture.khuzait"`: read both from the data and say
   what each does for this row.
3. **The dwarf chariot applier.** `tools/apply_dwarf_chariot_armory.py` anchors its item block on
   `taom_chariot_armour_imperial`. Does a new item directly before that anchor change what `--check`,
   `--apply` or a removal does? Read its guard and its tests.
4. **The name row and the translation pipeline.** Is `loc_LOTRAOM_horses.xml` the file the Armory's items of
   `LOTRAOM_horses.xml` belong to (read the routing in `tools/register_armory_names.py`)? Does the key collide
   with a key of TAOM, TAOM_Map or the Armory? Is +1 per language in the coverage baseline the right delta, and
   does the baseline hold any other change?
5. **The colour family record.** `how: "word black"` with an empty `texture`: read `tools/scan_armour_colours.py`
   and say why the scanner did not measure a texture for a mesh that has three materials with maps, and whether
   a consumer of the table (`tools/generate_armour_band_clones.py`, `tools/taom_schema.py`, a gate) breaks or
   misbehaves on an empty texture.
6. **Reinstall and other machines.** The Armory is unversioned. Name every place where this row is lost or
   breaks something: a reinstall of the Armory, the laptop with a partial install, a release channel build
   (`docs/reference/release-process.md`, the channel sync), a dedicated server, a co-op guest. Which repo check
   goes red when the row is missing, today, and which would once a troop references the item?
7. **The two writing scripts.** Do they do what their docstrings say: stop on an existing target, keep BOM and
   line endings, back up before the write, verify after it? Find a path where they write a broken file.

## Output

A list of findings, most severe first. For each: severity (P1 breaks a load, a save, a server or a player's
game; P2 wrong data or a gate that lies; P3 hygiene), the file and line, the evidence you read (quote it), and
the smallest fix. Then a list "checked and found right" with one line each. If you could not check something,
say UNVERIFIED and why.
