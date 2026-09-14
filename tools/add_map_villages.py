#!/usr/bin/env python3
"""Add village <Settlement> rows to the LIVE TAOM_Map/settlements.xml, plus their 12 loc rows.

The map author places village entities in the worldmap scene (Main_map/scene.xscene). Each one
needs a matching <Settlement> row in settlements.xml or it is inert, and the editor's own save path
(SandBox.View SettlementPositionScript.OnSceneSave) only writes positions back for ids it finds in
that XML. This script writes the rows; the VILLAGES table below is the batch.

Positions: taken from the scene entity's transform when the entity is already saved (first two
numbers of `<transform position="x, y, z"`). When it is not, the bound settlement's position plus a
small per-parent offset is written and the row is printed as PLACEHOLDER. That is safe only because
the editor overwrites posX/posY from the entity on the next scene save; DO NOT start a campaign
while a PLACEHOLDER row has no scene entity, SettlementVisual.OnStartup NREs at map load (#269).
`--check` is the gate for that: it fails on any table id whose master row, loc row or scene entity
is missing, or whose master position drifted from the scene transform.

Rows are inserted right after the row's own `after` anchor (default INSERT_AFTER) so a region's
block stays contiguous; loc rows go after that anchor's last row in each language, same display
name in all 12 (Tolkien proper nouns do not translate). `hearth` is per row too (default HEARTH):
the #597 Gondor rows sit at 350 like their EW10/EW11 castle-village neighbours; the Isengard and
Gundabad rows at those cultures' 500 floor. Idempotent per id: an id already in the master is skipped. Byte discipline per
.claude/rules/moduledata-validation.md: binary read, each file's own BOM and newline preserved
(the master carries a BOM and CRLF, the loc files no BOM and CR CR LF; both are detected, not assumed), a non-.xml backup before the
write, and every written document is parsed first.

Precedent: tools/add_bluecraig_castles.py (#270). Next batch: append rows to VILLAGES.

Usage:
  python tools/add_map_villages.py            # dry run: prints the plan and each position's source
  python tools/add_map_villages.py --apply    # writes the live master + 12 loc files (+ backups)
  python tools/add_map_villages.py --check    # exit 1 on any missing row / entity / drifted position
"""
import argparse
import datetime
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import namedtuple
from xml.sax.saxutils import quoteattr

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord")
MAP = GAME + r"/Modules/TAOM_Map"
LIVE = MAP + r"/ModuleData/settlements.xml"
SCENE = MAP + r"/SceneObj/Main_map/scene.xscene"
LANGS = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]

# Every VillageType id in the live file (21) plus the ones DefaultVillageTypes registers that TAOM
# does not use yet. add_bluecraig_castles.py's copy omitted the five horse ranches.
VALID_VILLAGE_TYPES = {
    "swine_farm", "cattle_farm", "sheep_farm", "wheat_farm", "vineyard", "fisherman", "lumberjack",
    "iron_mine", "silver_mine", "clay_mine", "salt_mine", "flax_plant", "date_farm", "olive_trees",
    "silk_plant", "trapper", "steppe_horse_ranch", "vlandian_horse_ranch", "europe_horse_ranch",
    "desert_horse_ranch", "sturgian_horse_ranch",
}

# culture -> (background_mesh, wait_mesh, castle_background_mesh): the panel meshes every village
# of that culture already uses in the live file.
MESHES = {
    "isengard": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "gondor": ("gui_bg_village_empire", "wait_empire_village", "gui_bg_castle_empire"),
    "gundabad": ("gui_bg_village_sturgia", "wait_sturgia_village", "gui_bg_castle_sturgia"),
}
HEARTH = "500"  # the SETTLEMENT_ECONOMY_FLOOR for isengard (tools/settlement_economy_floor.json)

# hearth and after are optional per row: hearth defaults to HEARTH, after to INSERT_AFTER (anchor_of).
Village = namedtuple("Village", "id name bound village_type culture scene hearth after", defaults=(HEARTH, None))

# The batch. Bindings follow the sibling ids already in the file (castle_village_isengard_a is on
# castle_orthanc_gate, village_isengard_a on town_isengard, castle_village_I2_1..3 on castle_I2).
# Names are Sindarin in the Angrenost style (docs/reference/taom-map-settlement-naming.md, region I)
# and renameable later through tools/Apply-MapVillageNames.py.
VILLAGES = [
    Village("castle_village_isengard_b", "Parth Angren",   "castle_orthanc_gate", "wheat_farm", "isengard", "empire_village_a"),
    Village("castle_village_isengard_c", "Tawarlad",       "castle_orthanc_gate", "swine_farm", "isengard", "empire_village_b"),
    Village("castle_village_isengard_d", "Eryn Methed",    "castle_orthanc_gate", "lumberjack", "isengard", "empire_village_c"),
    Village("village_isengard_b",        "Talath Curunír", "town_isengard",       "wheat_farm", "isengard", "empire_village_d"),
    Village("village_isengard_c",        "Athrad Angren",  "town_isengard",       "fisherman",  "isengard", "empire_village_e"),
    Village("village_isengard_d",        "Amon Thôn",      "town_isengard",       "sheep_farm", "isengard", "empire_village_f"),
    Village("village_isengard_e",        "Nan Gwath",      "town_isengard",       "swine_farm", "isengard", "empire_village_g"),
    Village("village_isengard_f",        "Groth Morn",     "town_isengard",       "iron_mine",  "isengard", "empire_village_h"),
    Village("castle_village_I2_4",       "Angroth",        "castle_I2",           "iron_mine",  "isengard", "empire_village_i"),
    # #597 (2026-09-13): Serelond (town_EW10) and Methir (town_EW11) held no village at all. The
    # map author placed the entities first; village_EW11_3 had none at authoring time and lands as
    # a PLACEHOLDER until the scene is saved. Sindarin names per the EW row of
    # docs/reference/taom-map-settlement-naming.md; hearth 350 matches castle_village_EW10/EW11_*;
    # scenes are the four taom_gondor_village_00N_forceatmo folders every EW village already uses.
    # The two villages rebound from castle_EW7 (village_EW10_1/_2) belong to rename_map_settlements.py.
    Village("village_EW10_3", "Aerlond",      "town_EW10", "fisherman",   "gondor", "taom_gondor_village_001_forceatmo", hearth="350", after="town_EW10"),
    Village("village_EW11_1", "Parth Mallen", "town_EW11", "wheat_farm",  "gondor", "taom_gondor_village_003_forceatmo", hearth="350", after="town_EW11"),
    Village("village_EW11_2", "Nan Laeg",     "town_EW11", "sheep_farm",  "gondor", "taom_gondor_village_004_forceatmo", hearth="350", after="town_EW11"),
    Village("village_EW11_3", "Emyn Caran",   "town_EW11", "olive_trees", "gondor", "taom_gondor_village_002_forceatmo", hearth="350", after="town_EW11"),
    # #597, same evening: Framsburg (castle_G4) was the third and last fortification with no village.
    # Gundabad hearth is that culture's 500 economy floor (the HEARTH default); panel meshes and
    # sturgia_village_* scenes as every G-region village; Black Speech names per the naming doc's G row.
    # Each Gundabad fief's villages share a stem (Düglar-, Mazūg-, Shôrd-, Gund-, Gram-); Framsburg's is
    # the Northman name the orcs kept. -bosh is what this map's swine farms carry (Bagmosh, Gundbosh).
    Village("castle_village_G4_1", "Fram-bûrz", "castle_G4", "wheat_farm", "gundabad", "sturgia_village_g", after="castle_G4"),
    Village("castle_village_G4_2", "Fram-bosh", "castle_G4", "swine_farm", "gundabad", "sturgia_village_h", after="castle_G4"),
]
INSERT_AFTER = "village_isengard_a"   # default anchor: last settlement of the Isengard block, and its last loc row

PLACEHOLDER_STEP = (1.5, 1.0)  # map units per placeholder on the same parent


def comp_id(vid):
    """castle_village_X -> castle_village_comp_X, village_X -> village_comp_X (live convention)."""
    if vid.startswith("castle_village_"):
        return "castle_village_comp_" + vid[len("castle_village_"):]
    if vid.startswith("village_"):
        return "village_comp_" + vid[len("village_"):]
    raise ValueError(f"not a village id: {vid!r}")


def anchor_of(v):
    """The settlement whose block, and whose last loc row, this row lands after."""
    return v.after or INSERT_AFTER


def group_by_anchor(rows):
    """{anchor: [rows]} in first-seen anchor order, rows in table order within each anchor."""
    groups = {}
    for v in rows:
        groups.setdefault(anchor_of(v), []).append(v)
    return groups


def village_block(v, px, py, nl):
    if v.village_type not in VALID_VILLAGE_TYPES:
        raise ValueError(f"{v.id}: invalid VillageType id {v.village_type!r}")
    if v.culture not in MESHES:
        raise ValueError(f"{v.id}: no panel meshes recorded for culture {v.culture!r}; add it to MESHES")
    bg, wait, castle_bg = MESHES[v.culture]
    return (
        f'  <Settlement id="{v.id}" name="{{=Settlements.Settlement.name.{v.id}}}{v.name}" posX="{px}" posY="{py}" culture="Culture.{v.culture}">\n'
        f'    <Components>\n'
        f'      <Village id="{comp_id(v.id)}" village_type="VillageType.{v.village_type}" hearth="{v.hearth}" bound="Settlement.{v.bound}" background_crop_position="0.0" background_mesh="{bg}" wait_mesh="{wait}" castle_background_mesh="{castle_bg}" />\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.village_complex">\n'
        f'      <Location id="village_center" scene_name="{v.scene}" />\n'
        f'    </Locations>\n'
        f'    <CommonAreas>\n'
        f'      <Area type="Pasture" name="{{=fOUsLdZR}}Pasture" />\n'
        f'      <Area type="Thicket" name="{{=66Mzk0NZ}}Thicket" />\n'
        f'      <Area type="Bog" name="{{=iXA5SttU}}Bog" />\n'
        f'    </CommonAreas>\n'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def scene_position(scene_text, entity_id):
    """(x, y) strings from the entity's own transform, or None when the entity is not in the scene.

    The search window ends at the next `<game_entity` open tag (a child or the next sibling), so an
    entity with no transform of its own resolves to None instead of borrowing its neighbour's.
    """
    open_tag = re.search(r'<game_entity name=' + re.escape(f'"{entity_id}"') + r'[^>]*>', scene_text)
    if not open_tag:
        return None
    end = scene_text.find("<game_entity", open_tag.end())
    window = scene_text[open_tag.end():end if end != -1 else len(scene_text)]
    m = re.search(r'<transform position="\s*([-\d.]+)\s*,\s*([-\d.]+)\s*,', window)
    return (m.group(1), m.group(2)) if m else None


def settlement_position(master_text, sid):
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\sposX="([^"]*)"\s+posY="([^"]*)"', master_text)
    return (m.group(1), m.group(2)) if m else None


def resolve_position(v, scene_text, master_text, k):
    """(posX, posY, source). source is 'scene' or 'PLACEHOLDER' (parent + offset for the k-th one)."""
    pos = scene_position(scene_text, v.id)
    if pos:
        return pos[0], pos[1], "scene"
    parent = settlement_position(master_text, v.bound)
    if not parent:
        raise ValueError(f"{v.id}: bound settlement {v.bound!r} is not in settlements.xml, nothing to place it near")
    dx, dy = PLACEHOLDER_STEP
    return f"{float(parent[0]) + dx * (k + 1):.3f}", f"{float(parent[1]) + dy * (k + 1):.3f}", "PLACEHOLDER"


def detect_newline(text):
    if "\r\r\n" in text:
        return "\r\r\n"
    return "\r\n" if "\r\n" in text else "\n"


def missing_ids(master_text, villages):
    return [v for v in villages if f'id="{v.id}"' not in master_text]


def plan_loc_rows(villages, loc_texts):
    """{lang: [(id, name), ...]} of the rows each language file still lacks, from the WHOLE table.

    Planned per language rather than from the ids missing in the master, so a master-present /
    loc-missing state (a run that failed part-way through the languages, a reverted file) is
    repaired by a plain re-run instead of being reported as nothing to do.
    """
    return {lang: [(v.id, v.name) for v in villages if f'id="Settlements.Settlement.name.{v.id}"' not in text]
            for lang, text in loc_texts.items()}


def nothing_to_do(villages, master_text, loc_texts):
    return not missing_ids(master_text, villages) and not any(plan_loc_rows(villages, loc_texts).values())


def _settlement_close_after(text, sid):
    """Offset just past the newline that ends the </Settlement> closing settlement `sid`, or -1."""
    open_m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"'), text)
    if not open_m:
        return -1
    close = text.find("</Settlement>", open_m.end())
    if close == -1:
        return -1
    end = close + len("</Settlement>")
    nl_end = text.find("\n", end)
    return nl_end + 1 if nl_end != -1 else end


def insert_after_settlement(master_text, anchor_id, blocks):
    at = _settlement_close_after(master_text, anchor_id)
    if at == -1:
        at = master_text.rfind("</Settlements>")
        if at == -1:
            raise RuntimeError("</Settlements> close tag not found")
    return master_text[:at] + "".join(blocks) + master_text[at:]


def loc_row(sid, name):
    return f'    <string id="Settlements.Settlement.name.{sid}" text={quoteattr(name)} />'


def insert_loc_rows(loc_text, anchor_id, rows):
    nl = detect_newline(loc_text)
    block = "".join(loc_row(sid, name) + nl for sid, name in rows)
    last = None
    for m in re.finditer(r'<string id="Settlements\.Settlement\.[a-z]+\.' + re.escape(anchor_id) + r'"[^>]*/>', loc_text):
        last = m
    if last is not None:
        at = loc_text.find("\n", last.end()) + 1
    else:
        at = loc_text.rfind("</strings>")
        if at == -1:
            raise RuntimeError("</strings> close tag not found")
        # back up over the indentation that precedes </strings> so the row lands on its own line
        while at > 0 and loc_text[at - 1] in " \t":
            at -= 1
    return loc_text[:at] + block + loc_text[at:]


def check(villages, master_text, scene_text, loc_texts, tolerance=0.01):
    """Findings (strings) for any table id whose data is missing or whose position drifted."""
    findings = []
    for v in villages:
        pos = settlement_position(master_text, v.id)
        if not pos:
            findings.append(f"{v.id}: no <Settlement> row in settlements.xml")
        for lang, text in loc_texts.items():
            if f'id="Settlements.Settlement.name.{v.id}"' not in text:
                findings.append(f"{v.id}: no loc row in Languages/{lang}/loc_settlements.xml")
        scene = scene_position(scene_text, v.id)
        if not scene:
            findings.append(f"{v.id}: no entity in scene.xscene (a campaign load would NRE SettlementVisual.OnStartup)")
        elif pos and (abs(float(pos[0]) - float(scene[0])) > tolerance or abs(float(pos[1]) - float(scene[1])) > tolerance):
            findings.append(f"{v.id}: settlements.xml has posX={pos[0]} posY={pos[1]} but scene.xscene has {scene[0]}, {scene[1]} (save the scene, or re-run --apply)")
    return findings


def _read(path):
    raw = open(path, "rb").read()
    return raw.startswith(b"\xef\xbb\xbf"), raw.decode("utf-8-sig")


def _write(path, had_bom, text, tag):
    ET.fromstring(text.encode("utf-8"))  # refuse to write a document that no longer parses
    backup = path + f".bak_{tag}"
    open(backup, "wb").write(open(path, "rb").read())
    open(path, "wb").write((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))
    ET.parse(path)


def _loc_path(lang):
    return MAP + f"/ModuleData/Languages/{lang}/loc_settlements.xml"


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # the names carry accents
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the live files (default: dry run)")
    ap.add_argument("--check", action="store_true", help="verify every VILLAGES id is fully present and positioned; exit 1 otherwise")
    args = ap.parse_args()

    for p in (LIVE, SCENE):
        if not os.path.isfile(p):
            print(f"ERROR: not found: {p} (set BANNERLORD_GAME_DIR, see tools/_gamedir.py)", file=sys.stderr)
            return 2
    master_bom, master = _read(LIVE)
    scene = open(SCENE, "rb").read().decode("utf-8", errors="replace")
    locs = {lang: _read(_loc_path(lang)) for lang in LANGS if os.path.isfile(_loc_path(lang))}
    if len(locs) != len(LANGS):
        print(f"ERROR: expected {len(LANGS)} loc_settlements.xml files, found {len(locs)}", file=sys.stderr)
        return 2

    if args.check:
        findings = check(VILLAGES, master, scene, {lang: text for lang, (_, text) in locs.items()})
        for f in findings:
            print("FAIL " + f)
        if findings:
            print(f"\n{len(findings)} finding(s) across {len(VILLAGES)} table id(s).")
            return 1
        print(f"OK: all {len(VILLAGES)} villages present in settlements.xml, all {len(locs)} loc files and scene.xscene; positions match.")
        return 0

    loc_texts = {lang: text for lang, (_, text) in locs.items()}
    if nothing_to_do(VILLAGES, master, loc_texts):
        print(f"all {len(VILLAGES)} villages already present in settlements.xml and all {len(locs)} loc files; nothing to do (idempotent). Try --check.")
        return 0
    todo = missing_ids(master, VILLAGES)
    loc_plan = plan_loc_rows(VILLAGES, loc_texts)

    nl = detect_newline(master)
    blocks, placeholders, per_parent = {}, 0, {}
    print(f"Plan: +{len(todo)} village(s) ({len(VILLAGES) - len(todo)} already present)")
    for v in todo:
        k = per_parent.get(v.bound, 0)
        px, py, source = resolve_position(v, scene, master, k)
        if source == "PLACEHOLDER":
            per_parent[v.bound] = k + 1
            placeholders += 1
        blocks.setdefault(anchor_of(v), []).append(village_block(v, px, py, nl))
        print(f"  {v.id:28} {v.name:15} -> bound {v.bound:20} {v.village_type:11} at {px}, {py}  [{source}]  after {anchor_of(v)}")
    if placeholders:
        print(f"\n{placeholders} PLACEHOLDER position(s): the entity is not in the saved scene yet. Save the scene "
              f"before any campaign load; the editor writes the real posX/posY back. Then run --check.")
    for lang in LANGS:
        rows = loc_plan[lang]
        print(f"  loc {lang:3}: +{len(rows)} row(s)" + ("" if rows else " (all present, skipped)"))
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files.")
        return 0

    tag = "mapvillages_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    n_blocks = sum(len(group) for group in blocks.values())
    if blocks:
        text = master
        for anchor, group in blocks.items():
            text = insert_after_settlement(text, anchor, group)
        _write(LIVE, master_bom, text, tag)
    written = []
    by_id = {v.id: v for v in VILLAGES}
    for lang, (bom, text) in locs.items():
        if loc_plan[lang]:
            for anchor, group in group_by_anchor([by_id[sid] for sid, _ in loc_plan[lang]]).items():
                text = insert_loc_rows(text, anchor, [(v.id, v.name) for v in group])
            _write(_loc_path(lang), bom, text, tag)
            written.append(lang)
    print(f"\nApplied: +{n_blocks} settlement(s) in settlements.xml, +{sum(len(loc_plan[l]) for l in written)} loc row(s) "
          f"across {len(written)} of {len(locs)} languages ({', '.join(written) or 'none'}); "
          f"backups *.bak_{tag}; every written file re-parsed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
