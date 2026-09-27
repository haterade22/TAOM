#!/usr/bin/env python3
"""Give the adult female dwarf her own meshes back in the live LOTRLOME_Armory skins.xml (crash test).

WHY
The adult woman skin of <race id="dwarf"> has worn the male base mesh set (sm_dwarf_basemesh_a1_*) since
dccbb9a3 (2026-08-11), a stopgap after native facegen CTDs: #403 (an underwear mesh name that did not
resolve) and #385 (TaleWorlds.Native.dll+0x58232c, the LOD0 sub-mesh sk_dwarf_bm_f1_head.eye carried no
morph channels while the head and .mouth carried 101). tools/blender/add_face_morph_channels.py gave the
.eye its 101 channels on 2026-09-25; this script puts the female meshes back so a test build can show
whether she still crashes. Mechanism: docs/features/troll-race.md (the face-morph crash, #684).

WHAT IT CHANGES
The reverse of the three dccbb9a3 hunks, inside the adult woman <skin> of the first <race id="dwarf">
only (located by structure, comments skipped, cross-checked against ElementTree):
  1. the <skin> tag: body_meta_mesh, body_meta_mesh_shoulders, legs_mesh, hands_mesh, face_meta_mesh
     (sm_dwarf_basemesh_a1_* -> sk_dwarf_bm_f1_*) and underwear_bottom_mesh ("" ->
     sk_dwarf_underwear_female_a);
  2. the five <eyebrow_mesh> names ("" -> sk_dwarf_bm_f1_eyebrow_01 .. _05, in order);
  3. the four <face_texture> name and lod_material (m_dwarf_basemesh_a1 -> m_dwarf_bm_female_a1_head).
The teenager, tween, child and toddler female skins, the male skins and every other race stay
byte-identical. --revert swaps the same 19 values back to the base-mesh ones.

REFUSES when the block is not found exactly once; when it holds other than 5 eyebrow_mesh or 4
face_texture entries; when a value is neither all-stopgap nor all-female (a partial or foreign edit);
when an expected current value does not occur exactly once per field in its scope; when the edited file
does not parse or differs from the original by anything but those 19 attribute values; when the dated
backup name already exists; and, on --apply, while the game or the Modding Kit runs.

    python restore_adult_woman_dwarf.py                      # dry run: report and print the diff
    python restore_adult_woman_dwarf.py --diff out.diff      # dry run, also save the diff
    python restore_adult_woman_dwarf.py --apply              # back up, write, re-read, verify
    python restore_adult_woman_dwarf.py --revert --apply     # back to the base-mesh stopgap

I/O is the full binary round trip (tools/README.md "XML I/O convention", idiom B): LF, CRLF and a BOM
survive untouched. The backup is <file>.bak-dwarf-woman-restore-<YYYYMMDD-HHMMSS> (--revert:
.bak-dwarf-woman-revert-<stamp>), never a .xml extension, because the engine globs *.xml in ModuleData.
Idempotent: a second run reports "already restored" and writes nothing.

RE-RUN CONDITION: any LOTRLOME_Armory update overwrites skins.xml and silently undoes this.
Afterwards: python tools/validate_mesh_refs.py --no-rgl-log (exit 0 is clean).
"""
import argparse
import datetime as dt
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import Counter, namedtuple

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
_env_game = os.environ.get("BANNERLORD_GAME_DIR", "")
LIVE = os.path.join(_env_game if _env_game.strip() else DEFAULT_GAME,
                    "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")
BACKUP_TAG = {"restore": ".bak-dwarf-woman-restore-", "revert": ".bak-dwarf-woman-revert-"}

# (attribute, stopgap value, female value): dccbb9a3 hunk 1, the <skin> start tag.
HEADER = (
    ("body_meta_mesh", "sm_dwarf_basemesh_a1_body", "sk_dwarf_bm_f1_body"),
    ("body_meta_mesh_shoulders", "sm_dwarf_basemesh_a1_shoulder", "sk_dwarf_bm_f1_shoulder"),
    ("legs_mesh", "sm_dwarf_basemesh_a1_legs", "sk_dwarf_bm_f1_legs"),
    ("hands_mesh", "sm_dwarf_basemesh_a1_arms", "sk_dwarf_bm_f1_arms"),
    ("face_meta_mesh", "sm_dwarf_basemesh_a1_head", "sk_dwarf_bm_f1_head"),
    ("underwear_bottom_mesh", "", "sk_dwarf_underwear_female_a"),
)
# Hunk 2: the i-th <eyebrow_mesh> name, (stopgap, female).
EYEBROWS = tuple(("", "sk_dwarf_bm_f1_eyebrow_%02d" % i) for i in range(1, 6))
# Hunk 3: every <face_texture>'s name and lod_material.
FACE_ATTRS = ("name", "lod_material")
FACE_STOPGAP, FACE_FEMALE = "m_dwarf_basemesh_a1", "m_dwarf_bm_female_a1_head"
FACE_COUNT = 4

_COMMENT = r"<!--.*?-->"
_ATTR = re.compile(r'(?<![\w.:-])([\w.:-]+)\s*=\s*"([^"]*)"')
_TAGNAME = re.compile(r"</?([\w.:-]+)")

Field = namedtuple("Field", "label tag attr start end current stopgap female scope")
Plan = namedtuple("Plan", "text span fields state new")


class RestoreError(Exception):
    pass


def game_or_kit_running():
    """Copy of tools/_gamedir.py game_or_kit_running: True when the game or the Modding Kit runs, and
    also when the check itself could not run (fail-closed)."""
    try:
        raw = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True,
                             timeout=20, check=True).stdout
    except (OSError, subprocess.SubprocessError) as exc:
        print("WARNING: could not list processes (%s); refusing to write" % exc)
        return True
    if raw is None:
        print("WARNING: the process list came back empty-handed; refusing to write")
        return True
    out = raw.decode("ascii", errors="replace")
    return any(k in out for k in ("TaleWorlds.MountAndBlade", "Bannerlord"))


def _tokens(text, names, start=0, end=None):
    """Start and end tags of the named elements in text[start:end], skipping comments."""
    alts = "|".join(r"</?%s\b[^>]*>" % re.escape(n) for n in names)
    rx = re.compile(_COMMENT + "|" + alts, re.S)
    for m in rx.finditer(text, start, len(text) if end is None else end):
        if not m.group(0).startswith("<!--"):
            yield m


def _tag(m):
    tok = m.group(0)
    return _TAGNAME.match(tok).group(1), tok.startswith("</")


def _attrs(text, m):
    out = {}
    for a in _ATTR.finditer(text, m.start(), m.end()):
        out.setdefault(a.group(1), []).append((a.group(2), a.start(2), a.end(2)))
    return out


def _values(text, m, name):
    return [v for v, _s, _e in _attrs(text, m).get(name, [])]


def _only(attrs, name, where):
    found = attrs.get(name, [])
    if len(found) != 1:
        raise RestoreError("%s carries %s %d time(s), expected once" % (where, name, len(found)))
    return found[0]


def _line(text, pos):
    return text.count("\n", 0, pos) + 1


def locate(text):
    """(skin start, start-tag end, block end) of the adult woman <skin> of the first <race id="dwarf">."""
    race_open, skin, hits = False, None, []
    for m in _tokens(text, ("race", "skin")):
        name, closing = _tag(m)
        if name == "race":
            if closing:
                if race_open:
                    break
                continue
            if race_open:
                raise RestoreError("a <race> opens inside the dwarf race (line %d)" % _line(text, m.start()))
            race_open = _values(text, m, "id") == ["dwarf"]
            continue
        if not race_open:
            continue
        if closing:
            if skin is None:
                raise RestoreError("</skin> with no open <skin> (line %d)" % _line(text, m.start()))
            if skin[2]:
                hits.append((skin[0], skin[1], m.end()))
            skin = None
        else:
            if skin is not None or m.group(0).endswith("/>"):
                raise RestoreError("unexpected <skin> shape at line %d" % _line(text, m.start()))
            target = (_values(text, m, "gender") == ["1"]
                      and _values(text, m, "mesh_maturity_type") == ["adult"])
            skin = (m.start(), m.end(), target)
    else:
        raise RestoreError('no complete <race id="dwarf"> element found')
    if len(hits) != 1:
        raise RestoreError("the dwarf race has %d adult woman <skin> blocks, expected 1" % len(hits))
    return hits[0]


def _parse(data, what):
    try:
        return ET.fromstring(data)
    except ET.ParseError as exc:
        raise RestoreError("%s does not parse: %s" % (what, exc))


def _et_skin(root):
    race = next((r for r in root.iter("race") if r.get("id") == "dwarf"), None)
    skins = [] if race is None else [s for s in race.findall("skin")
                                     if s.get("gender") == "1" and s.get("mesh_maturity_type") == "adult"]
    if len(skins) != 1:
        raise RestoreError("ElementTree sees %d dwarf adult woman skins, expected 1" % len(skins))
    return skins[0]


def _cross_check(root, text, span):
    """The text-located block must be the one ElementTree sees: same start-tag attributes."""
    m = re.compile(r"<skin\b[^>]*>").match(text, span[0])
    by_text = {k: v[0][0] for k, v in _attrs(text, m).items()}
    if by_text != dict(_et_skin(root).attrib):
        raise RestoreError("the text scan and ElementTree disagree on the adult woman <skin> tag")


def _entries(text, start, end, container, entry, expected):
    boxes, items, open_at = [], [], None
    for m in _tokens(text, (container, entry), start, end):
        name, closing = _tag(m)
        if name == entry:
            if not closing:
                items.append(m)
        elif closing:
            if open_at is None:
                raise RestoreError("</%s> with no open <%s>" % (container, container))
            boxes.append((open_at, m.end()))
            open_at = None
        else:
            open_at = m.start()
    if len(boxes) != 1 or open_at is not None:
        raise RestoreError("the adult woman block has %d <%s> elements, expected 1" % (len(boxes), container))
    if len(items) != expected:
        raise RestoreError("the adult woman block has %d <%s> entries, expected %d (the dccbb9a3 hunk)"
                           % (len(items), entry, expected))
    if any(not (boxes[0][0] < m.start() and m.end() <= boxes[0][1]) for m in items):
        raise RestoreError("an <%s> sits outside its <%s>" % (entry, container))
    return boxes[0], items


def fields(text, span):
    """The 19 attribute values the three dccbb9a3 hunks own, with their positions and both values."""
    start, tag_end, end = span
    out = []
    head = re.compile(r"<skin\b[^>]*>").match(text, start)
    attrs = _attrs(text, head)
    for attr, stop, fem in HEADER:
        v, s, e = _only(attrs, attr, "the adult woman <skin> tag")
        out.append(Field(attr, "skin", attr, s, e, v, stop, fem, (start, end)))
    box, items = _entries(text, start, end, "eyebrow_meshes", "eyebrow_mesh", len(EYEBROWS))
    for i, (m, (stop, fem)) in enumerate(zip(items, EYEBROWS), 1):
        v, s, e = _only(_attrs(text, m), "name", "eyebrow_mesh %d" % i)
        out.append(Field("eyebrow_mesh %d name" % i, "eyebrow_mesh", "name", s, e, v, stop, fem, box))
    box, items = _entries(text, start, end, "face_textures", "face_texture", FACE_COUNT)
    for i, m in enumerate(items, 1):
        attrs = _attrs(text, m)
        for attr in FACE_ATTRS:
            v, s, e = _only(attrs, attr, "face_texture %d" % i)
            out.append(Field("face_texture %d %s" % (i, attr), "face_texture", attr, s, e, v,
                             FACE_STOPGAP, FACE_FEMALE, box))
    return out


def block_state(fs):
    if all(f.current == f.stopgap for f in fs):
        return "stopgap"
    if all(f.current == f.female for f in fs):
        return "restored"
    odd = ["%s=%r" % (f.label, f.current) for f in fs if f.current not in (f.stopgap, f.female)]
    raise RestoreError("the block is neither the stopgap nor the female set: "
                       + (", ".join(odd) if odd else "some fields stopgap, some female (a partial edit)"))


def _check_unique(text, fs, which):
    """Each expected current value occurs exactly as often in its scope as the fields that hold it."""
    need = Counter((f.scope, f.attr, getattr(f, which)) for f in fs)
    for (scope, attr, value), n in sorted(need.items()):
        body = re.sub(_COMMENT, "", text[scope[0]:scope[1]], flags=re.S)
        got = len(re.findall(r'(?<![\w.:-])%s\s*=\s*"%s"' % (re.escape(attr), re.escape(value)), body))
        if got != n:
            raise RestoreError('%s="%s" occurs %d time(s) in its scope (line %d), expected %d'
                               % (attr, value, got, _line(text, scope[0]), n))


def _verify(text, new_text, old_root, span, fs, src, dst):
    start, _tag_end, end = span
    if new_text[:start] != text[:start] or new_text[len(new_text) - (len(text) - end):] != text[end:]:
        raise RestoreError("the edit reaches outside the adult woman block")
    new_root = _parse(new_text.encode("utf-8"), "the edited file")
    old_elems, new_elems = list(old_root.iter()), list(new_root.iter())
    if len(old_elems) != len(new_elems):
        raise RestoreError("the edited file has a different element count")
    skin = _et_skin(new_root)
    first = next(i for i, e in enumerate(new_elems) if e is skin)
    last = first + sum(1 for _ in skin.iter())
    got = Counter()
    for i, (a, b) in enumerate(zip(old_elems, new_elems)):
        if (a.tag, a.text, a.tail, sorted(a.keys())) != (b.tag, b.text, b.tail, sorted(b.keys())):
            raise RestoreError("element %d (<%s>) changed shape" % (i, a.tag))
        for k in a.keys():
            if a.get(k) != b.get(k):
                if not first <= i < last:
                    raise RestoreError("an attribute outside the adult woman skin changed: <%s %s>" % (a.tag, k))
                got[(a.tag, k, a.get(k), b.get(k))] += 1
    want = Counter((f.tag, f.attr, getattr(f, src), getattr(f, dst)) for f in fs)
    if got != want:
        raise RestoreError("the edited tree differs from the plan: %r" % (got - want or want - got))


def plan(data, mode):
    """Plan(text, span, fields, state, new bytes or None when already in the target state). Pure."""
    text = data.decode("utf-8")          # idiom B: binary read, BOM and line endings stay in the string
    span = locate(text)
    old_root = _parse(data, "the current file")
    _cross_check(old_root, text, span)
    fs = fields(text, span)
    state = block_state(fs)
    target, src, dst = ("restored", "stopgap", "female") if mode == "restore" else ("stopgap", "female", "stopgap")
    if state == target:
        return Plan(text, span, fs, state, None)
    _check_unique(text, fs, src)
    pieces, at = [], 0
    for f in sorted(fs, key=lambda f: f.start):
        pieces += [text[at:f.start], getattr(f, dst)]
        at = f.end
    new_text = "".join(pieces + [text[at:]])
    _verify(text, new_text, old_root, span, fs, src, dst)
    return Plan(text, span, fs, state, new_text.encode("utf-8"))


def unified_diff(p, label, context=3):
    """Unified diff, line by line. The edit changes values inside lines and never the line count, so
    this is exact; difflib mis-aligns repeated skin structure and reports untouched lines as changed."""
    old = p.text.splitlines(keepends=True)
    new = p.new.decode("utf-8").splitlines(keepends=True)
    if len(old) != len(new):
        raise RestoreError("the edit changed the line count")
    groups = []
    for i in (i for i, (a, b) in enumerate(zip(old, new)) if a != b):
        if groups and i - groups[-1][-1] - 1 <= 2 * context:
            groups[-1].append(i)
        else:
            groups.append([i])
    out = ["--- a/%s\n" % label, "+++ b/%s\n" % label]
    for g in groups:
        lo, hi = max(0, g[0] - context), min(len(old), g[-1] + context + 1)
        out.append("@@ -%d,%d +%d,%d @@\n" % (lo + 1, hi - lo, lo + 1, hi - lo))
        i = lo
        while i < hi:
            j = i
            while j < hi and old[j] != new[j]:
                j += 1
            if j == i:
                out.append(" " + old[i])
                i += 1
            else:
                out += ["-" + s for s in old[i:j]] + ["+" + s for s in new[i:j]]
                i = j
    return out


def changed_old_lines(diff):
    """Old-file line numbers of every '-' line in a unified diff."""
    out, line = [], 0
    for d in diff:
        if d.startswith("@@"):
            line = int(re.match(r"@@ -(\d+)", d).group(1))
        elif d.startswith("---") or d.startswith("+++"):
            continue
        elif d.startswith("-"):
            out.append(line)
            line += 1
        elif d.startswith(" "):
            line += 1
    return out


def _stamp():
    return dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def write(path, old, new, tag):
    """Back up, then write; read back; restore the original on any failure. Returns the backup path."""
    bak = path + tag + _stamp()
    if os.path.exists(bak):
        raise RestoreError("backup exists: " + bak)
    if open(path, "rb").read() != old:
        raise RestoreError("the file changed on disk since it was read")
    with open(bak, "xb") as fh:
        fh.write(old)
    if open(bak, "rb").read() != old:
        raise RestoreError("the backup does not read back: " + bak)
    try:
        with open(path, "wb") as fh:
            fh.write(new)
        if open(path, "rb").read() != new:
            raise RestoreError("the written file does not read back")
    except Exception:
        with open(path, "wb") as fh:
            fh.write(old)
        raise
    return bak


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--file", default=LIVE, help="skins.xml to edit (default: the live Armory's)")
    ap.add_argument("--apply", action="store_true", help="write (default is a dry run)")
    ap.add_argument("--revert", action="store_true", help="swap the same fields back to the base-mesh stopgap")
    ap.add_argument("--diff", metavar="PATH", help="also save the unified diff to PATH")
    args = ap.parse_args(argv)
    mode = "revert" if args.revert else "restore"
    if not os.path.isfile(args.file):
        print("ERROR: not found: %s" % args.file, file=sys.stderr)
        return 2
    try:
        data = open(args.file, "rb").read()
        p = plan(data, mode)
        first, last = _line(p.text, p.span[0]), _line(p.text, p.span[2])
        print("file:  %s" % args.file)
        print('block: <race id="dwarf"> <skin gender="1" mesh_maturity_type="adult">, lines %d-%d' % (first, last))
        print("state: %s" % ("stopgap (base-mesh values)" if p.state == "stopgap" else "restored (female values)"))
        if p.new is None:
            print("already restored: nothing to do" if mode == "restore"
                  else "already reverted to the base-mesh values: nothing to do")
            return 0
        diff = unified_diff(p, os.path.basename(args.file))
        lines = changed_old_lines(diff)
        hunks = sum(1 for d in diff if d.startswith("@@"))
        inside = all(first <= n <= last for n in lines)
        print("plan:  %s, %d attribute values on %d lines in %d hunks, lines %d-%d (%s the block)"
              % (mode, len(p.fields), len(lines), hunks, min(lines), max(lines),
                 "all inside" if inside else "NOT all inside"))
        if not inside:
            raise RestoreError("a changed line falls outside the adult woman block")
        if args.diff:
            with open(args.diff, "wb") as fh:
                fh.write("".join(diff).encode("utf-8"))
            print("diff:  %s" % args.diff)
        sys.stdout.write("".join(diff))
        if not args.apply:
            print("DRY RUN: nothing written; pass --apply to write")
            return 0
        if game_or_kit_running():
            print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
            return 2
        bak = write(args.file, data, p.new, BACKUP_TAG[mode])
        try:
            done = plan(open(args.file, "rb").read(), mode).new is None
        except RestoreError:
            done = False
        if not done:
            with open(args.file, "wb") as fh:  # the check failed: put the original bytes back
                fh.write(data)
            raise RestoreError("the written file does not re-plan as done; the original bytes are back "
                               "(backup: %s)" % bak)
        print("written: %s\nbackup:  %s" % (args.file, bak))
        return 0
    except RestoreError as exc:
        print("REFUSED: %s" % exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
