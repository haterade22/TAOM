#!/usr/bin/env python3
"""Add town and castle <Settlement> rows to the LIVE TAOM_Map/settlements.xml, plus their 12 loc rows.

The sibling of tools/add_map_villages.py for fortifications, sharing its byte discipline and helpers
(binary read, each file's BOM and newline kept, a non-.xml backup, every written document parsed).
Villages bound to these fortifications go in add_map_villages.py's VILLAGES table and are added after
this script has run, because that tool refuses a village whose bound fortification has no row.

Positions come from the saved Main_map scene: posX/posY from the entity's transform, and the gate
from its child tagged `main_map_city_gate`, which is exactly what the editor's own save path reads
(SandBox.View SettlementPositionScript.LoadSettlementData / SaveSettlementPositions; the last
tagged child of GetChildrenRecursive's pre-order wins). The gate's world position is the child's
frame composed through every ancestor, rotation_euler applied in Z, X, Y order with the scale on
the columns. Before using that maths the script recomputes the gate of every fortification the
file already records and refuses to write unless all of them match within GATE_TOLERANCE
(2026-10-07: 231 gates, worst 0.0065; see GATE_TOLERANCE for why the worst is not 0). An entity
missing from the scene, or one with no gate child, is an error, never a placeholder: a
fortification row without its entity NREs SettlementVisual.OnStartup at map load (#269).

New rows are appended at the end of the file, towns first (the save-order rule,
add_map_villages.append_settlements). `after` places only the loc rows.

Idempotent per id: an id already in the master is skipped. `--check` fails on any table id whose
row, loc row or scene entity is missing, or whose position or gate drifted from the scene, and
when this batch's rows (BATCH: the FORTS ids not in RELEASED, plus the villages bound to them) are
not the file's tail under a town.

Usage:
  python tools/add_map_fortifications.py            # dry run
  python tools/add_map_fortifications.py --apply    # writes the live master + 12 loc files (+ backups)
  python tools/add_map_fortifications.py --check
"""
import argparse
import datetime
import math
import os
import re
import sys
from collections import namedtuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_map_villages as amv  # noqa: E402  LIVE, SCENE, LANGS, _read, _write, _loc_path, insert_* helpers

# The scene file stores transforms to 3 decimals. The measured worst recomputation is 0.0065
# (castle_AN10, 2026-10-07), and rounding its stored rotation by +-0.0005 alone could move the gate
# up to about 0.009, so 0.01 sat barely above the noise floor. A wrong rotation order misses by up to 4.8 units: 0.02 still
# tells maths from noise.
GATE_TOLERANCE = 0.02
GATE_TAG = "main_map_city_gate"

Fort = namedtuple("Fort", "id name owner culture kind scene prosperity after")

# Arthedain (2026-10-07). Mike placed the entities in the editor and approved the names, the
# capital (Fornost, town_AN2) and the fief split: clan 1 the King's, clans 2 and 3 tier 3, clans 4
# and 5 tier 2. A weak realm on purpose: prosperity well under Gondor's (EW towns 4500, castles
# 1000) and NOT on tools/settlement_economy_floor.json, whose 4800/950 floor would lift it.
# Scenes: unused vanilla empire towns (the entities are copies of Gondor's) and Gondor's castles.
ANCHOR = "castle_village_GBC4_4"  # `after` places only the loc rows: Arthedain's follow Blue Craig's
FORTS = [
    Fort("town_AN2", "Fornost Erain", "clan_arthedain_1", "arthedain", "town", "empire_town_c", 3000, ANCHOR),
    Fort("town_AN1", "Annúminas", "clan_arthedain_2", "arthedain", "town", "empire_town_d", 2600, "town_AN2"),
    Fort("town_AN3", "Bree", "clan_arthedain_3", "arthedain", "town", "empire_town_i", 2400, "town_AN1"),
    Fort("castle_AN9", "Barad Forn", "clan_arthedain_1", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "town_AN3"),
    Fort("castle_AN10", "Emyn Uial", "clan_arthedain_2", "arthedain", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_AN9"),
    Fort("castle_AN4", "Amon Sûl", "clan_arthedain_3", "arthedain", "castle", "taom_gondor_castle_003_forceatmo", 800, "castle_AN10"),
    Fort("castle_AN5", "Weather Hills", "clan_arthedain_4", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "castle_AN4"),
    Fort("castle_AN6", "Brandywine Bridge", "clan_arthedain_4", "arthedain", "castle", "taom_gondor_castle_002_forceatmo", 800, "castle_AN5"),
    Fort("castle_AN8", "Emyn Beraid", "clan_arthedain_5", "arthedain", "castle", "taom_gondor_castle_003_forceatmo", 800, "castle_AN6"),
    Fort("castle_AN11", "Forochel", "clan_arthedain_5", "arthedain", "castle", "taom_gondor_castle_001_forceatmo", 800, "castle_AN8"),
    # Blue Craig's second town (Mike placed it 2026-10-07), held by the Crag-spawn (tier 5), who
    # had only Krathol. Prosperity at the 4800 town floor Blue Craig sits on
    # (tools/settlement_economy_floor.json); sturgia_town_e is the one sturgia town scene unused.
    Fort("town_GBC2", "Luinkrag", "clan_bluecraig_2", "bluecraig", "town", "sturgia_town_e", 4800, "village_GBC1_3"),
]

# FORTS ids a release has shipped. The save-order check protects saves made BEFORE a batch, so once
# a release carries a batch, add its ids here and the next batch's rows become the required tail.
# Limit: a village a later batch binds to an already released fortification is not counted as new
# (batch_ids follows the bound fortification), so --check would name it; widen batch_ids first.
RELEASED = set()


def batch_ids(forts=None, villages=None, released=None):
    """This batch's rows: the unreleased FORTS ids plus every village bound to one of them."""
    forts = FORTS if forts is None else forts
    villages = amv.VILLAGES if villages is None else villages
    released = RELEASED if released is None else released
    ids = {f.id for f in forts if f.id not in released}
    return ids | {v.id for v in villages if v.bound in ids}


def append_order(forts):
    """Towns first (a town heads the appended tail, see save_order_findings), else table order."""
    return sorted(forts, key=lambda f: f.kind != "town")

# Low levels: a poor realm's walls and markets. Wall level 1 everywhere, so sieges are short.
TOWN_BUILDINGS = [
    ("building_settlement_fortifications", 1), ("building_settlement_barracks", 1),
    ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 0),
    ("building_settlement_siege_workshop", 1), ("building_settlement_tax_office", 1),
    ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 1),
    ("building_settlement_mason", 1), ("building_settlement_waterworks", 0),
    ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1),
]
CASTLE_BUILDINGS = [
    ("building_castle_fortifications", 1), ("building_castle_barracks", 1),
    ("building_castle_training_fields", 1), ("building_castle_guard_house", 0),
    ("building_castle_siege_workshop", 0), ("building_castle_castallans_office", 1),
    ("building_castle_granary", 1), ("building_castle_craftmans_quarters", 0),
    ("building_castle_farmlands", 1), ("building_castle_mason", 0),
    ("building_castle_roads_and_paths", 1),
]

# Per culture, everything a fortification row needs beyond its own table fields, each copied from a
# live row of that culture: Arthedain from Gondor's (town_EW7, castle_EW1), Blue Craig from its own
# town_GBC1 (sturgia scenes, gate rotation 0.378, its building levels).
STYLE = {
    "arthedain": dict(town_menu="menu_empire_3", castle_menu="menu_empire_1", wait="wait_empire_town",
                      gate_rotation={"town": "0.808", "castle": "0.908"},
                      arena="arena_empire_a", tavern="empire_house_c_tavern_a",
                      keep=("empire_castle_keep_a_l1_interior", "empire_castle_keep_a_l2_interior",
                            "empire_castle_keep_a_l3_interior"),
                      dungeon="empire_dungeon_stealth", house="empire_house_d_interior_house",
                      town_buildings=TOWN_BUILDINGS, castle_buildings=CASTLE_BUILDINGS),
    "bluecraig": dict(town_menu="gui_bg_town_sturgia", castle_menu="gui_bg_castle_sturgia", wait="wait_sturgia_town",
                      gate_rotation={"town": "0.378", "castle": "0.908"},
                      arena="arena_sturgia_a", tavern="sturgia_house_b_interior_tavern",
                      keep=("sturgia_castle_keep_a_l2_interior", "sturgia_castle_keep_a_l2_interior",
                            "sturgia_castle_keep_a_l3_interior"),
                      dungeon="sturgia_dungeon_stealth", house="sturgia_town_house_d1_interior_b_house",
                      town_buildings=[
                          ("building_settlement_fortifications", 2), ("building_settlement_barracks", 2),
                          ("building_settlement_training_fields", 1), ("building_settlement_guard_house", 1),
                          ("building_settlement_siege_workshop", 2), ("building_settlement_tax_office", 1),
                          ("building_settlement_marketplace", 1), ("building_settlement_warehouse", 1),
                          ("building_settlement_mason", 1), ("building_settlement_waterworks", 0),
                          ("building_settlement_courthouse", 0), ("building_settlement_roads_and_paths", 1)],
                      castle_buildings=CASTLE_BUILDINGS),
}


# ---------------------------------------------------------------- scene transforms

_TOK = re.compile(r'<game_entity\b[^>]*>|</game_entity>|<transform\b[^>]*/>|<tag name="([^"]+)"/>')


def entity_subtree(scene_text, name):
    m = re.search(r'<game_entity name=' + re.escape(f'"{name}"') + r'[^>]*>', scene_text)
    if not m:
        return None
    depth = 0
    for t in re.finditer(r'<game_entity\b[^>]*>|</game_entity>', scene_text[m.start():]):
        depth += 1 if t.group(0).startswith("<game_entity") else -1
        if depth == 0:
            return scene_text[m.start():m.start() + t.end()]
    return None


def _parse_tree(txt):
    stack, root = [], None
    for t in _TOK.finditer(txt):
        s = t.group(0)
        if s.startswith("<game_entity"):
            node = {"tf": None, "tags": [], "kids": []}
            (stack[-1]["kids"] if stack else []).append(node)
            root = root or node
            stack.append(node)
        elif s == "</game_entity>":
            stack.pop()
        elif s.startswith("<transform"):
            if stack and stack[-1]["tf"] is None:
                stack[-1]["tf"] = s
        elif t.group(1) and stack:
            stack[-1]["tags"].append(t.group(1))
    return root


def _vec(tf, key, default):
    m = re.search(key + r'="([^"]+)"', tf or "")
    return [float(x) for x in m.group(1).split(",")] if m else list(default)


def _mm(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def _mv(a, v):
    return [sum(a[i][k] * v[k] for k in range(3)) for i in range(3)]


def _rot(axis, ang):
    c, s = math.cos(ang), math.sin(ang)
    return {"x": [[1, 0, 0], [0, c, -s], [0, s, c]],
            "y": [[c, 0, s], [0, 1, 0], [-s, 0, c]],
            "z": [[c, -s, 0], [s, c, 0], [0, 0, 1]]}[axis]


def _frame(tf):
    e = dict(zip("xyz", _vec(tf, "rotation_euler", (0, 0, 0))))
    sc = _vec(tf, "scale", (1, 1, 1))
    r = _mm(_mm(_rot("z", e["z"]), _rot("x", e["x"])), _rot("y", e["y"]))
    return [[r[i][j] * sc[j] for j in range(3)] for i in range(3)], _vec(tf, "position", (0, 0, 0))


def _find_gate(node, rot=None, pos=None):
    """World position of the LAST gate-tagged descendant in pre-order, as the editor resolves it
    (GetChildrenRecursive, overwriting on every match), or None."""
    rl, pl = _frame(node["tf"])
    if rot is None:
        rg, pg = rl, pl
    else:
        rg, pg = _mm(rot, rl), [a + b for a, b in zip(_mv(rot, pl), pos)]
    found = pg if rot is not None and GATE_TAG in node["tags"] else None
    for kid in node["kids"]:
        found = _find_gate(kid, rg, pg) or found
    return found


def scene_gate(scene_text, sid):
    """(x, y) of the fortification's gate child in world space, or None."""
    sub = entity_subtree(scene_text, sid)
    if not sub:
        return None
    g = _find_gate(_parse_tree(sub))
    return (g[0], g[1]) if g else None


def validate_gate_maths(scene_text, master_text):
    """Recompute every recorded gate. Returns (checked, worst error)."""
    worst, n = 0.0, 0
    for m in re.finditer(r'<Settlement id="((?:town|castle)_[^"]+)"[^>]*\sgate_posX="([^"]+)"\s+gate_posY="([^"]+)"',
                         master_text):
        g = scene_gate(scene_text, m.group(1))
        if g is None:
            continue
        n += 1
        worst = max(worst, math.hypot(g[0] - float(m.group(2)), g[1] - float(m.group(3))))
    return n, worst


# ---------------------------------------------------------------- rows


def fort_block(f, pos, gate, nl):
    if f.culture not in STYLE:
        raise ValueError(f"{f.id}: no STYLE recorded for culture {f.culture!r}")
    st = STYLE[f.culture]
    castle = f.kind == "castle"
    key = f.id.split("_", 1)[1]
    comp = f"castle_comp_{key}" if castle else f"town_comp_{key}"
    buildings = "".join(f'          <Building id="{b}" level="{lv}" />\n'
                        for b, lv in (st["castle_buildings"] if castle else st["town_buildings"]))
    sc = f.scene
    center = f'      <Location id="center" scene_name="{sc}" scene_name_1="{sc}" scene_name_2="{sc}" scene_name_3="{sc}" />\n'
    keep = (f'      <Location id="lordshall" scene_name_1="{st["keep"][0]}" '
            f'scene_name_2="{st["keep"][1]}" scene_name_3="{st["keep"][2]}" />\n'
            f'      <Location id="prison" scene_name="{st["dungeon"]}" />\n')
    if castle:
        locations, template, common = center + keep, "castle_complex", ""
    else:
        locations = (center + f'      <Location id="arena" scene_name="{st["arena"]}" />\n'
                     f'      <Location id="tavern" scene_name="{st["tavern"]}" />\n' + keep
                     + "".join(f'      <Location id="house_{i}" scene_name="{st["house"]}" />\n'
                               for i in (1, 2, 3))
                     + '      <Location id="alley" />\n')
        template = "town_complex"
        common = ('    <CommonAreas>\n'
                  '      <Area type="Backstreet" name="{=a0MVffcN}Backstreet" />\n'
                  '      <Area type="Clearing" name="{=LWHIVshb}Clearing" />\n'
                  '      <Area type="Waterfront" name="{=Rr1cy5Sk}Waterfront" />\n'
                  '    </CommonAreas>\n')
    return (
        f'  <Settlement id="{f.id}" name="{{=Settlements.Settlement.name.{f.id}}}{f.name}" owner="Faction.{f.owner}" '
        f'posX="{pos[0]}" posY="{pos[1]}" culture="Culture.{f.culture}" gate_posX="{gate[0]:.4f}" gate_posY="{gate[1]:.4f}">\n'
        f'    <Components>\n'
        f'      <Town id="{comp}" is_castle="{"true" if castle else "false"}" background_crop_position="0.0" '
        f'background_mesh="{st["castle_menu"] if castle else st["town_menu"]}" wait_mesh="{st["wait"]}" '
        f'gate_rotation="{st["gate_rotation"][f.kind]}" '
        f'prosperity="{f.prosperity}">\n'
        f'        <Buildings>\n{buildings}        </Buildings>\n'
        f'      </Town>\n'
        f'    </Components>\n'
        f'    <Locations complex_template="LocationComplexTemplate.{template}">\n{locations}    </Locations>\n'
        f'{common}'
        f'  </Settlement>\n'
    ).replace("\n", nl)


def master_gate(master_text, sid):
    m = re.search(r'<Settlement id=' + re.escape(f'"{sid}"') + r'[^>]*?\sgate_posX="([^"]*)"\s+gate_posY="([^"]*)"', master_text)
    return (float(m.group(1)), float(m.group(2))) if m else None


def check(forts, master_text, scene_text, loc_texts, villages=(), released=frozenset()):
    findings = amv.save_order_findings(master_text, batch_ids(forts, villages, released))
    for f in forts:
        pos = amv.settlement_position(master_text, f.id)
        if not pos:
            findings.append(f"{f.id}: no <Settlement> row in settlements.xml")
        for lang, text in loc_texts.items():
            if f'id="Settlements.Settlement.name.{f.id}"' not in text:
                findings.append(f"{f.id}: no loc row in Languages/{lang}/loc_settlements.xml")
        scene = amv.scene_position(scene_text, f.id)
        gate = scene_gate(scene_text, f.id)
        if not scene:
            findings.append(f"{f.id}: no entity in scene.xscene (a campaign load would NRE SettlementVisual.OnStartup)")
            continue
        if gate is None:
            findings.append(f"{f.id}: entity has no child tagged {GATE_TAG}")
        if pos and (abs(float(pos[0]) - float(scene[0])) > GATE_TOLERANCE or abs(float(pos[1]) - float(scene[1])) > GATE_TOLERANCE):
            findings.append(f"{f.id}: settlements.xml posX={pos[0]} posY={pos[1]} but the scene has {scene[0]}, {scene[1]}")
        mg = master_gate(master_text, f.id)
        if pos and gate and (not mg or math.hypot(mg[0] - gate[0], mg[1] - gate[1]) > GATE_TOLERANCE):
            findings.append(f"{f.id}: gate in settlements.xml {mg} does not match the scene's {gate[0]:.4f}, {gate[1]:.4f}")
    return findings


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    for p in (amv.LIVE, amv.SCENE):
        if not os.path.isfile(p):
            print(f"ERROR: not found: {p}", file=sys.stderr)
            return 2
    master_bom, master = amv._read(amv.LIVE)
    scene = open(amv.SCENE, "rb").read().decode("utf-8", errors="replace")
    locs = {lang: amv._read(amv._loc_path(lang)) for lang in amv.LANGS if os.path.isfile(amv._loc_path(lang))}
    if len(locs) != len(amv.LANGS):
        print(f"ERROR: expected {len(amv.LANGS)} loc_settlements.xml files, found {len(locs)}", file=sys.stderr)
        return 2
    loc_texts = {lang: text for lang, (_, text) in locs.items()}

    if args.check:
        findings = check(FORTS, master, scene, loc_texts, amv.VILLAGES, RELEASED)
        for f in findings:
            print("FAIL " + f)
        if findings:
            print(f"\n{len(findings)} finding(s) across {len(FORTS)} table id(s).")
            return 1
        print(f"OK: all {len(FORTS)} fortifications present with loc rows; positions and gates match the scene; "
              f"the batch's {len(batch_ids())} rows are the file's tail under a town.")
        return 0

    n, worst = validate_gate_maths(scene, master)
    print(f"gate maths: {n} recorded gates recomputed, worst error {worst:.5f}")
    if n == 0 or worst > GATE_TOLERANCE:
        print("REFUSING: the gate computation no longer reproduces the recorded gates", file=sys.stderr)
        return 1

    todo = append_order([f for f in FORTS if f'id="{f.id}"' not in master])
    loc_plan = {lang: [(f.id, f.name) for f in FORTS if f'id="Settlements.Settlement.name.{f.id}"' not in text]
                for lang, text in loc_texts.items()}
    if not todo and not any(loc_plan.values()):
        print(f"all {len(FORTS)} fortifications already present; nothing to do. Try --check.")
        return 0

    nl = amv.detect_newline(master)
    errors, blocks = [], []
    print(f"Plan: +{len(todo)} fortification(s) ({len(FORTS) - len(todo)} already present)")
    for f in todo:
        pos = amv.scene_position(scene, f.id)
        gate = scene_gate(scene, f.id)
        if not pos or gate is None:
            errors.append(f"{f.id}: {'no scene entity' if not pos else 'no gate child'}")
            continue
        blocks.append((f, fort_block(f, pos, gate, nl)))
        print(f"  {f.id:12} {f.name:18} {f.kind:6} owner {f.owner:17} at {pos[0]}, {pos[1]}  gate {gate[0]:.3f}, {gate[1]:.3f}  {f.scene}")
    if errors:
        print("REFUSING:\n  " + "\n  ".join(errors), file=sys.stderr)
        return 1
    for lang in amv.LANGS:
        print(f"  loc {lang:3}: +{len(loc_plan[lang])} row(s)")
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files.")
        return 0

    tag = "mapforts_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    amv._write(amv.LIVE, master_bom, amv.append_settlements(master, [block for _, block in blocks]), tag)
    by_id = {f.id: f for f in FORTS}
    for lang, (bom, ltext) in locs.items():
        if loc_plan[lang]:
            for sid, name in loc_plan[lang]:
                ltext = amv.insert_loc_rows(ltext, by_id[sid].after, [(sid, name)])
            amv._write(amv._loc_path(lang), bom, ltext, tag)
    print(f"\nApplied: +{len(blocks)} fortification(s), loc rows in {sum(1 for l in loc_plan if loc_plan[l])} "
          f"language(s); backups *.bak_{tag}; every written file re-parsed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
