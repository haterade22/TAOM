#!/usr/bin/env python3
"""
Register the hill troll war hammer in the LIVE LOTRLOME_Armory (2026-09-26): two crafting pieces, their
TwoHandedMace registrations, the crafted item and its English names.

The hammer's art is `AssetSources/weapons/Mordor/troll/wm_hill_troll_ws_1.fbx` (tools/oneoff/export_hill_troll_hammer.py:
`wm_hill_troll_2h_hammer_head`, `wm_hill_troll_2h_hammer_handle`, LOD0 to LOD5, and `bo_wm_hill_troll_2h_hammer_head`).
Every block is a copy of the cave troll's two-handed mace (`wm_cave_troll_2h_mace_*`, the troll weapon that ships and
works) with the ids, names, meshes, body, lengths and head weight changed, so its flags and materials stay the proven
ones. Its damage does NOT carry over: the engine simulates a crafted weapon's damage and speed from the pieces'
geometry (TaleWorlds.Core.Crafting), so the copied head weight 1.23 on the longer hammer priced at 23 Blunt and swing
speed 12 against the mace's 86 and 28. HEAD_WEIGHT 0.875 prices it at 86 and 28 (tools/melee_catalogue.price):
- head: length and blade_length 69.63 (the head is 0.6963 m along the shaft), weight 0.875. The seat comes from
  `length`: when it is present the engine sets both distance attributes to length/2 (CraftingPiece.cs:165-179), so the
  copied distance_to_*_piece values are dead and the head's base sits flush on the handle end
  (WeaponDesign.CalculatePivotDistances);
- handle: length 350.96 (3.5096 m), piece_offset 95, which puts the grip 23% up the shaft as the cave mace's 70 on its
  258.58 does.

GATED ON THE KIT IMPORT: `--apply` refuses until `Assets/weapons/Mordor/troll/wm_hill_troll_ws_1_geo.tpac` lists the
two pieces as Metamesh items and the body as a PhysicsShape in its table of contents (package_gaps, exact names; a
byte test passed a mistyped body, since the head's name is inside the body's). A `body_name` no package ships makes
PreloadHelper.WaitForMeshesToBeLoaded spin forever on the first mission that preloads the item (#352), so the XML
must never land before the art does.

Byte-faithful (tools/README.md XML I/O convention): binary read, BOM and line endings kept, every file parsed before it
is written, a timestamped `.bak-hillhammer-<stamp>` beside each (never an .xml extension), idempotent (a file that
already carries the hammer's ids is left alone). Dry run by default; `--apply` also refuses while the game or the Kit
runs.

    python tools/oneoff/add_hill_troll_hammer_items.py            # dry run: what would change
    python tools/oneoff/add_hill_troll_hammer_items.py --apply    # after the Kit import
Then point the `hill_troll` troop (Main/_Module/ModuleData/troops/troops_mordor.xml) at `Item.wm_hill_troll_2h_hammer_a`
and run python tools/validate_moduledata.py, tools/validate_mesh_refs.py --scan-bodies and tools/check_external_xslt.py.
"""
import argparse
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402

ARMORY = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", "LOTRLOME_Armory")
MD = os.path.join(ARMORY, "ModuleData")
TPAC = os.path.join(ARMORY, "Assets", "weapons", "Mordor", "troll", "wm_hill_troll_ws_1_geo.tpac")

HEAD, HANDLE, ITEM = "wm_hill_troll_2h_hammer_head", "wm_hill_troll_2h_hammer_handle", "wm_hill_troll_2h_hammer_a"
BODY = "bo_" + HEAD
# The engine simulates a crafted weapon's damage and speed from its pieces' geometry (TaleWorlds.Core.Crafting);
# the cave head's 1.23 on this head's longer geometry priced at 23 Blunt and speed 12 (tools/melee_catalogue.price).
# 0.875 prices at the cave mace's 86 Blunt and speed 28 (Mike, 2026-09-26).
HEAD_WEIGHT = "0.875"
CAVE_HEAD, CAVE_HANDLE, CAVE_ITEM = "wm_cave_troll_2h_mace_head", "wm_cave_troll_2h_mace_handle", "wm_cave_troll_2h_mace_a"
NAMES = {
    ITEM: "[Mordor] Hill Troll War Hammer I",
    HEAD: "Mordor Hill Troll War Hammer Head A",
    HANDLE: "Mordor Hill Troll War Hammer Handle A",
}


def block(text, tag, ident):
    """The full `<tag ... id="ident" ...> ... </tag>` element text (its leading indentation included)."""
    m = re.search(r'[ \t]*<%s\s[^>]*?\bid="%s"' % (tag, re.escape(ident)), text)
    if not m:
        raise SystemExit("%s id=%s not found" % (tag, ident))
    end = text.index("</%s>" % tag, m.end()) + len("</%s>" % tag)
    return m.start(), end, text[m.start():end]


def sub_attr(xml, attr, value):
    new, n = re.subn(r'(\b%s=")[^"]*(")' % re.escape(attr), lambda m: m.group(1) + value + m.group(2), xml, count=1)
    if n != 1:
        raise SystemExit("attribute %s not found in the template block" % attr)
    return new


def head_block(text):
    _, end, cave = block(text, "CraftingPiece", CAVE_HEAD)
    b = cave.replace('id="%s"' % CAVE_HEAD, 'id="%s"' % HEAD)
    b = sub_attr(b, "name", "{=aom_%s_name}%s" % (HEAD, NAMES[HEAD]))
    b = sub_attr(b, "mesh", HEAD)
    b = sub_attr(b, "length", "69.63")
    b = sub_attr(b, "blade_length", "69.63")
    b = sub_attr(b, "body_name", BODY)
    b = sub_attr(b, "weight", HEAD_WEIGHT)
    return end, b


def handle_block(text):
    _, end, cave = block(text, "CraftingPiece", CAVE_HANDLE)
    b = cave.replace('id="%s"' % CAVE_HANDLE, 'id="%s"' % HANDLE)
    b = sub_attr(b, "name", "{=aom_%s_name}%s" % (HANDLE, NAMES[HANDLE]))
    b = sub_attr(b, "mesh", HANDLE)
    b = sub_attr(b, "length", "350.96")
    b = sub_attr(b, "piece_offset", "95")
    return end, b


def item_block(text):
    _, end, cave = block(text, "CraftedItem", CAVE_ITEM)
    b = cave.replace('id="%s"' % CAVE_ITEM, 'id="%s"' % ITEM)
    b = sub_attr(b, "name", "{=aom_%s_name}%s" % (ITEM, NAMES[ITEM]))
    b = b.replace('id="%s"' % CAVE_HEAD, 'id="%s"' % HEAD).replace('id="%s"' % CAVE_HANDLE, 'id="%s"' % HANDLE)
    if HEAD not in b or HANDLE not in b:
        raise SystemExit("the cave mace item did not carry both pieces")
    return end, b


def insert_after_line(text, anchor_re, lines, nl):
    r"""Insert `lines` after the (single) line matching anchor_re, with that line's indentation, before the line's
    own terminator: `[^\r\n]` rather than `.`, which takes the `\r` of a CRLF line and put the first run's lines
    between `\r` and `\n` (2026-09-26, three live files)."""
    m = list(re.finditer(r"^([ \t]*)[^\r\n]*%s[^\r\n]*(?=\r?\n|\Z)" % anchor_re, text, flags=re.M))
    if len(m) != 1:
        raise SystemExit("anchor %r matched %d lines" % (anchor_re, len(m)))
    indent, pos = m[0].group(1), m[0].end()
    return text[:pos] + "".join(nl + indent + ln for ln in lines) + text[pos:]


def plan():
    """[(path, new_text or None, note)] for the six files."""
    out = []

    def load(name):
        path = os.path.join(MD, *name.split("/"))
        raw = open(path, "rb").read()
        text = raw.decode("utf-8-sig")
        nl = "\r\n" if text.count("\r\n") * 2 > text.count("\n") else "\n"
        return path, raw, text, nl

    # 1. crafting pieces: the head after the cave head, the handle after the cave handle
    path, raw, text, nl = load("LOTRLOME_crafting_pieces.xml")
    if 'id="%s"' % HEAD in text:
        out.append((path, raw, None, "already has the hammer pieces"))
    else:
        end_h, head = head_block(text)
        end_g, handle = handle_block(text)
        parts = sorted([(end_h, head), (end_g, handle)], reverse=True)
        new = text
        for end, b in parts:
            new = new[:end] + nl + nl + b + new[end:]
        out.append((path, raw, new, "2 CraftingPiece"))
    # 2 and 3. the TwoHandedMace registrations, right after the cave mace's
    for name, fmt in (("weapon_descriptions.xslt", '<AvailablePiece id="%s"/>'),
                      ("crafting_templates.xslt", '<UsablePiece piece_id="%s"/>')):
        path, raw, text, nl = load(name)
        if HEAD in text:
            out.append((path, raw, None, "already registered"))
            continue
        new = insert_after_line(text, re.escape(fmt % CAVE_HANDLE), [fmt % HEAD, fmt % HANDLE], nl)
        out.append((path, raw, new, "2 registrations in TwoHandedMace"))
    # 4. the crafted item after the cave mace
    path, raw, text, nl = load("LOTRLOME_items/LOTRAOM_weapons.xml")
    if 'id="%s"' % ITEM in text:
        out.append((path, raw, None, "already has the item"))
    else:
        end, b = item_block(text)
        out.append((path, raw, text[:end] + nl + nl + b + text[end:], "1 CraftedItem"))
    # 5 and 6. English names beside the cave mace's rows
    for name, rows in (("Languages/loc_LOTRAOM_weapons.xml", [ITEM]),
                       ("Languages/loc_LOTRLOME_crafting_pieces.xml", [HEAD, HANDLE])):
        path, raw, text, nl = load(name)
        if "aom_%s_name" % rows[0] in text:
            out.append((path, raw, None, "already has the names"))
            continue
        anchor = "aom_%s_name" % (CAVE_ITEM if rows == [ITEM] else CAVE_HANDLE)
        lines = ['<string id="aom_%s_name" text="%s"/>' % (r, NAMES[r]) for r in rows]
        out.append((path, raw, insert_after_line(text, re.escape('id="%s"' % anchor), lines, nl), "%d name row(s)" % len(rows)))
    return out


def package_gaps(path):
    """The names the package must ship and does not, read from its table of contents by exact name (the two visual
    pieces as Metamesh items, the body as a PhysicsShape). Raw bytes will not do: the head's name is a substring of
    the body's, so a package holding only a mistyped body passed a byte test."""
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    import validate_mesh_refs as vm
    if not os.path.isfile(path):
        return ["the package %s" % path]
    res = vm.scan_tpac_metameshes(path)
    if not res.parsed_ok:
        return ["a readable table of contents (%s)" % res.error]
    return [n for n in (HEAD, HANDLE) if n not in res.metamesh_names] + \
        ([BODY] if BODY not in res.physicsshape_names else [])


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    changes = plan()
    for path, raw, new, note in changes:
        if new is not None:
            ET.fromstring(new.encode("utf-8"))   # refuse a document that no longer parses
        print("%-70s %s" % (os.path.relpath(path, MD), note if new is not None else "unchanged: " + note))
    todo = [c for c in changes if c[2] is not None]
    if not todo:
        print("no change: the hammer is already registered")
        return 0
    if not args.apply:
        print("DRY RUN: nothing written")
        return 0
    missing = package_gaps(TPAC)
    if missing:
        print("REFUSED: the package lacks %s; import wm_hill_troll_ws_1.fbx in the Modding Kit first (a body_name no "
              "package ships hangs the preload, #352)" % ", ".join(missing), file=sys.stderr)
        return 2
    if game_or_kit_running():
        print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
        return 2
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    for path, raw, new, note in todo:
        open(path + ".bak-hillhammer-" + stamp, "wb").write(raw)
        bom = raw.startswith(b"\xef\xbb\xbf")
        open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + new.encode("utf-8"))
        ET.fromstring(open(path, "rb").read().decode("utf-8-sig").encode("utf-8"))
        print("written %s (backup .bak-hillhammer-%s)" % (os.path.relpath(path, MD), stamp))
    return 0


if __name__ == "__main__":
    sys.exit(main())
