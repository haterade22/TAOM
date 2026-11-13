#!/usr/bin/env python3
"""Give every young female dwarf skin the adult woman's female meshes in the live LOTRLOME_Armory skins.xml.

WHY
Mike, 2026-11-13: any female dwarf, children included, uses the female meshes. The adult woman skin
already wears sk_dwarf_bm_f1_* (tools/oneoff/restore_adult_woman_dwarf.py). The teenager, tween, child and
toddler female skins still wear the male base mesh sm_dwarf_basemesh_a1_*, so a girl in armour that has no
dwarf variant showed a torn human-shaped head and neck.

WHAT IT CHANGES
In the first <race id="dwarf"> only, in every <skin gender="1"> whose mesh_maturity_type is not adult
(kid_2_female, kid_1_female, kid_3_female, toddler_female):
  1. the <skin> tag: body_meta_mesh, body_meta_mesh_shoulders, legs_mesh, hands_mesh, face_meta_mesh
     (sm_dwarf_basemesh_a1_* -> sk_dwarf_bm_f1_*), underwear_bottom_mesh ("" -> sk_dwarf_underwear_female_a)
     and body_mesh_suffix ("_fem" -> "", the adult woman's value: no _fem dwarf armour ships);
  2. <eyebrow_meshes>: the one empty entry becomes the five adult female brows;
  3. the four <face_texture> name and lod_material: m_dwarf_basemesh_a1 -> m_dwarf_bm_female_a1_head.
The male skins, the adult woman and every other race stay byte-identical. min_scale and the other
attributes stay, so the child scale still applies to the female head.

REFUSES when the dwarf race holds other than 4 young female skins; when a mesh value is neither the
base-mesh one nor the female one; when a skin holds other than 4 face_texture entries or an eyebrow list that
is neither one empty entry nor the five female brows; when the edited file does not parse; on --apply while
the game or the Modding Kit runs; when the dated backup name already exists.

    python restore_young_woman_dwarf.py                # dry run
    python restore_young_woman_dwarf.py --apply        # back up, write, re-read, verify
    python restore_young_woman_dwarf.py --revert --apply   # meshes, brows, textures only; the suffix stays ""
                                                           # (the backup holds the exact original)

I/O is the full binary round trip (tools/README.md "XML I/O convention", idiom B). The backup is
<file>.bak-dwarf-young-woman-<stamp>, never a .xml extension. Idempotent. RE-RUN CONDITION: an Armory update
overwrites skins.xml. Afterwards: python tools/validate_mesh_refs.py --no-rgl-log.
"""
import argparse
import datetime as dt
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
_env = os.environ.get("BANNERLORD_GAME_DIR", "")
LIVE = os.path.join(_env if _env.strip() else DEFAULT_GAME,
                    "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")

# (attribute, base-mesh value, female value). A suffix of "_fem" or "" both read as the base state.
HEADER = (
    ("body_meta_mesh", "sm_dwarf_basemesh_a1_body", "sk_dwarf_bm_f1_body"),
    ("body_meta_mesh_shoulders", "sm_dwarf_basemesh_a1_shoulder", "sk_dwarf_bm_f1_shoulder"),
    ("legs_mesh", "sm_dwarf_basemesh_a1_legs", "sk_dwarf_bm_f1_legs"),
    ("hands_mesh", "sm_dwarf_basemesh_a1_arms", "sk_dwarf_bm_f1_arms"),
    ("face_meta_mesh", "sm_dwarf_basemesh_a1_head", "sk_dwarf_bm_f1_head"),
    ("underwear_bottom_mesh", "", "sk_dwarf_underwear_female_a"),
)
BROWS = ["sk_dwarf_bm_f1_eyebrow_%02d" % i for i in range(1, 6)]
FACE_BASE, FACE_FEMALE = "m_dwarf_basemesh_a1", "m_dwarf_bm_female_a1_head"
YOUNG_COUNT = 4


class Refused(Exception):
    pass


def game_or_kit_running():
    try:
        raw = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True,
                             timeout=20, check=True).stdout
    except (OSError, subprocess.SubprocessError) as exc:
        print("WARNING: could not list processes (%s); refusing to write" % exc)
        return True
    out = (raw or b"").decode("ascii", errors="replace")
    return not out or any(k in out for k in ("TaleWorlds.MountAndBlade", "Bannerlord"))


def _attr_rx(name):
    return re.compile(r'(?<![\w.:-])(%s\s*=\s*")([^"]*)(")' % re.escape(name))


def _set_attr(tag, name, allowed, new):
    """Replace one attribute value in a start tag; refuse a value outside allowed."""
    found = list(_attr_rx(name).finditer(tag))
    if len(found) != 1:
        raise Refused("<skin> carries %s %d time(s), expected once" % (name, len(found)))
    cur = found[0].group(2)
    if allowed is not None and cur not in allowed:
        raise Refused('%s="%s" is neither the base-mesh nor the female value' % (name, cur))
    return tag[:found[0].start(2)] + new + tag[found[0].end(2):]


def edit_block(block, to_female):
    """Return the block with its three groups of values moved to the target state."""
    head = re.match(r"<skin\b[^>]*>", block).group(0)
    rest = block[len(head):]
    new_head = head
    for attr, base, fem in HEADER:
        new_head = _set_attr(new_head, attr, (base, fem), fem if to_female else base)
    # base state of the suffix is "_fem" (kids) or "" (toddler); the female state is "".
    if to_female:
        new_head = _set_attr(new_head, "body_mesh_suffix", ("_fem", ""), "")
    # eyebrows
    m = re.search(r"([ \t]*)<eyebrow_meshes>(.*?)</eyebrow_meshes>", rest, re.S)
    if not m:
        raise Refused("a young female skin has no <eyebrow_meshes>")
    names = re.findall(r'name\s*=\s*"([^"]*)"', m.group(2))
    nl = "\r\n" if "\r\n" in block else "\n"
    if names not in ([""], BROWS):
        raise Refused("eyebrow list %r is neither one empty entry nor the five female brows" % (names,))
    want = BROWS if to_female else [""]
    ind = m.group(1)
    body = "".join('%s\t<eyebrow_mesh%s%s\t\tname="%s" />%s' % (ind, nl, ind, n, nl) for n in want)
    brows = "%s<eyebrow_meshes>%s%s%s</eyebrow_meshes>" % (ind, nl, body, ind)
    rest = rest[:m.start()] + brows + rest[m.end():]
    # face textures
    tags = list(re.finditer(r"<face_texture\b[^>]*>", rest))
    if len(tags) != 4:
        raise Refused("a young female skin has %d <face_texture> entries, expected 4" % len(tags))
    src, dst = (FACE_BASE, FACE_FEMALE) if to_female else (FACE_FEMALE, FACE_BASE)
    out, at = [], 0
    for t in tags:
        tag = t.group(0)
        for attr in ("name", "lod_material"):
            tag = _set_attr(tag, attr, (src, dst), dst)
        out += [rest[at:t.start()], tag]
        at = t.end()
    rest = "".join(out) + rest[at:]
    return new_head + rest


def plan(data, to_female):
    """(new bytes or None when already done, skin names changed). Pure."""
    text = data.decode("utf-8")
    race = re.search(r'<race\s+id="dwarf"\s*>.*?</race>', text, re.S)
    if not race:
        raise Refused('no <race id="dwarf"> found')
    pieces, at, names = [], 0, []
    root = ET.fromstring(data)
    et_young = [s.get("name") for r in root.iter("race") if r.get("id") == "dwarf"
                for s in r.findall("skin")
                if s.get("gender") == "1" and s.get("mesh_maturity_type") != "adult"]
    if len(et_young) != YOUNG_COUNT:
        raise Refused("ElementTree sees %d young female dwarf skins, expected %d" % (len(et_young), YOUNG_COUNT))
    hits = 0
    for m in re.finditer(r"<skin\b[^>]*>.*?</skin>", text[race.start():race.end()], re.S):
        block = m.group(0)
        tag = re.match(r"<skin\b[^>]*>", block).group(0)
        if not (re.search(r'gender\s*=\s*"1"', tag) and not re.search(r'mesh_maturity_type\s*=\s*"adult"', tag)):
            continue
        hits += 1
        new = edit_block(block, to_female)
        s = race.start() + m.start()
        pieces += [text[at:s], new]
        at = race.start() + m.end()
        if new != block:
            names.append(re.search(r'\bname\s*=\s*"([^"]*)"', tag).group(1))
    if hits != YOUNG_COUNT:
        raise Refused("the text scan found %d young female skins, ElementTree %d" % (hits, YOUNG_COUNT))
    new_text = "".join(pieces) + text[at:]
    if new_text == text:
        return None, []
    new_root = ET.fromstring(new_text.encode("utf-8"))
    if new_root is None:
        raise Refused("the edited file does not parse")
    return new_text.encode("utf-8"), names


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--file", default=LIVE)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--revert", action="store_true")
    args = ap.parse_args(argv)
    to_female = not args.revert
    if not os.path.isfile(args.file):
        print("ERROR: not found: %s" % args.file, file=sys.stderr)
        return 2
    try:
        data = open(args.file, "rb").read()
        new, names = plan(data, to_female)
        print("file:  %s" % args.file)
        if new is None:
            print("already %s: nothing to do" % ("female" if to_female else "reverted"))
            return 0
        print("plan:  %s %d skins: %s" % ("female" if to_female else "revert", len(names), ", ".join(names)))
        if not args.apply:
            print("DRY RUN: nothing written; pass --apply to write")
            return 0
        if game_or_kit_running():
            print("REFUSED: the game or the Modding Kit runs; close it first", file=sys.stderr)
            return 2
        bak = args.file + ".bak-dwarf-young-woman-" + dt.datetime.now().strftime("%Y%m%d-%H%M%S")
        if os.path.exists(bak):
            raise Refused("backup exists: " + bak)
        with open(bak, "xb") as fh:
            fh.write(data)
        if open(bak, "rb").read() != data:
            raise Refused("the backup does not read back")
        with open(args.file, "wb") as fh:
            fh.write(new)
        again, _ = plan(open(args.file, "rb").read(), to_female)
        if open(args.file, "rb").read() != new or again is not None:
            with open(args.file, "wb") as fh:
                fh.write(data)
            raise Refused("the written file does not re-plan as done; original bytes restored (%s)" % bak)
        print("written: %s\nbackup:  %s" % (args.file, bak))
        return 0
    except Refused as exc:
        print("REFUSED: %s" % exc, file=sys.stderr)
        return 1
    except ET.ParseError as exc:
        print("REFUSED: parse error: %s" % exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
