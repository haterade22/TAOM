#!/usr/bin/env python
"""Append new beards and hairs to the dwarf groom lists in the live LOTRLOME_Armory skins.xml.

WHY
A face key stores a hair or beard as a position in its skin's <hair_meshes> / <beard_meshes> list, so an
entry inserted anywhere but the end shifts every lord's groom after it. New dwarf grooms are therefore
APPENDED. A pilot also adds throwaway test beards; they must come out again while they are still the last
entries, which is what --remove checks (it refuses to remove an entry that a kept entry follows).

WHAT IT TOUCHES
Only <race id="dwarf"> (located by structure, comments skipped) and only its four skins that carry groom
lists: man, woman, kid_2_male, kid_2_female. Other races reuse those skin ids and stay byte-identical, as
do the dwarf's younger skins (their <beard_meshes /> is empty).
  --beard NAME            append a beard_mesh at the END of <beard_meshes>, in each of the four skins
  --hair NAME:STYLETAG    append a hair_mesh with that style tag at the END of <hair_meshes>
  --remove NAME           remove that entry from the four skins (beard or hair list)
Both appending flags are repeatable and keep their order. --remove cannot be combined with them.
Each new entry copies the sibling layout: name and cover_type1..4 all carry the entry's own name; a hair
entry adds <style_tags><style_tag name="STYLETAG"/></style_tags>. Entry indentation comes from the closing
tag of its list, and the line ending is the file's majority one.

IDEMPOTENT
A name already in a list is skipped and reported. It must then sit where a fresh append would have put it
(the requested names are the tail of the list, in request order); anywhere else is a refusal, because
appending would not reproduce the same list. --remove of a name that is not in a list is a reported no-op.

REFUSES before writing when any of this fails, and the whole run writes nothing: a missing dwarf race or
target skin, a self-closing target list, a name in the wrong place, a removal that would shift a kept entry,
a result that does not parse with ElementTree, a list whose tail (or contents, after a removal) is not what
was planned, or any change outside the eight lists.

    python append_dwarf_grooms.py --beard sk_dwarf_beard_a_10 --hair dwarf_hair_k:TiedAcrossBack
    python append_dwarf_grooms.py --remove sk_dwarf_beard_test_ch --remove sk_dwarf_beard_test_noch
    python append_dwarf_grooms.py ... --apply      # back up, write, re-read, verify
    python append_dwarf_grooms.py ... --skins PATH # another skins.xml (the tests use this)

Default is a dry run: per skin and list the entry count before and after plus the names, then a compact
unified diff (at most 120 lines). I/O is the full binary round trip (tools/README.md "XML I/O convention",
idiom B), so a BOM and the line endings survive. --apply first writes the write-once backup
<file>.bak-grooms-<YYYYMMDD-HHMMSS>, never a .xml extension (the engine globs *.xml in ModuleData).
Exit codes: 0 ok (including nothing to do), 1 refused or failed, 2 bad arguments.

RE-RUN CONDITION: any LOTRLOME_Armory update overwrites skins.xml and silently undoes this.
"""
import argparse
import copy
import datetime as dt
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import namedtuple

DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
_env_game = os.environ.get("BANNERLORD_GAME_DIR", "")
LIVE = os.path.join(_env_game if _env_game.strip() else DEFAULT_GAME,
                    "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")
BACKUP_TAG = ".bak-grooms-"
SKINS = ("man", "woman", "kid_2_male", "kid_2_female")
KINDS = {"beard": ("beard_meshes", "beard_mesh"), "hair": ("hair_meshes", "hair_mesh")}
MAX_DIFF_LINES = 120
_NAME_OK = re.compile(r"^[A-Za-z0-9_]+$")

_RACE = re.compile(r"<!--.*?-->|<(/?)race\b[^>]*>", re.S)
_TOK = re.compile(r"<!--.*?-->|<(/?)(skin|hair_meshes|beard_meshes|hair_mesh|beard_mesh)\b[^>]*>", re.S)
_ATTR = re.compile(r'(?<![\w.:-])([\w.:-]+)\s*=\s*"([^"]*)"')

Entry = namedtuple("Entry", "name start end")        # token offsets: start tag begin, end tag end
Box = namedtuple("Box", "skin kind close entries")   # close: offset of the </..._meshes> tag
Edit = namedtuple("Edit", "pos end ins")             # replace text[pos:end] by ins; both on line starts
Row = namedtuple("Row", "skin kind before after appended removed skipped absent")
Plan = namedtuple("Plan", "text new_text rows edits")


class GroomError(Exception):
    pass


def _attr(tag, name):
    for m in _ATTR.finditer(tag):
        if m.group(1) == name:
            return m.group(2)
    return None


def _line(text, pos):
    return text.count("\n", 0, pos) + 1


def beard_entry(name, indent, eol):
    i = indent
    return eol.join([i + "<beard_mesh", i + '\tname="%s"' % name] +
                    [i + '\tcover_type%d="%s"%s' % (n, name, ">" if n == 4 else "") for n in range(1, 5)] +
                    [i + "</beard_mesh>"]) + eol


def hair_entry(name, tag, indent, eol):
    i = indent
    return eol.join([i + "<hair_mesh", i + '\tname="%s"' % name] +
                    [i + '\tcover_type%d="%s"%s' % (n, name, ">" if n == 4 else "") for n in range(1, 5)] +
                    [i + "\t<style_tags>", i + "\t\t<style_tag", i + '\t\t\tname="%s" />' % tag,
                     i + "\t</style_tags>", i + "</hair_mesh>"]) + eol


def dwarf_span(text):
    """(start, end) of the one <race id="dwarf"> element, comments skipped."""
    spans, cur = [], None
    for m in _RACE.finditer(text):
        tok = m.group(0)
        if tok.startswith("<!--") or tok.endswith("/>"):
            continue
        if m.group(1):
            if cur is None:
                raise GroomError("</race> with no open <race> (line %d)" % _line(text, m.start()))
            if cur[1]:
                spans.append((cur[0], m.end()))
            cur = None
        else:
            if cur is not None:
                raise GroomError("a <race> opens inside another (line %d)" % _line(text, m.start()))
            cur = (m.start(), _attr(tok, "id") == "dwarf")
    if cur is not None:
        raise GroomError("a <race> is never closed")
    if len(spans) != 1:
        raise GroomError('found %d <race id="dwarf"> elements, expected 1' % len(spans))
    return spans[0]


def scan(text, span):
    """{(skin, kind): Box} for the target skins inside the dwarf race; self-closing lists are absent."""
    boxes, seen = {}, set()
    skin, box, start = None, None, None
    for m in _TOK.finditer(text, span[0], span[1]):
        tok = m.group(0)
        if tok.startswith("<!--"):
            continue
        closing, tag, selfc = m.group(1) == "/", m.group(2), tok.endswith("/>")
        if tag == "skin":
            if closing or selfc:
                skin = box = None
                continue
            skin = _attr(tok, "name")
            if skin in SKINS:
                if skin in seen:
                    raise GroomError("the dwarf race has two <skin name=\"%s\"> elements" % skin)
                seen.add(skin)
            box = None
        elif skin not in SKINS:
            continue
        elif tag.endswith("_meshes"):
            kind = "hair" if tag.startswith("hair") else "beard"
            if closing:
                if box is None:
                    raise GroomError("</%s> with no open list in skin %s" % (tag, skin))
                boxes[(skin, kind)] = Box(skin, kind, m.start(), box)
                box = None
            elif not selfc:
                if (skin, kind) in boxes:
                    raise GroomError("skin %s has two <%s> elements" % (skin, tag))
                box = []
        else:
            if box is None:
                continue
            if closing:
                if start is None:
                    raise GroomError("</%s> with no open entry in skin %s" % (tag, skin))
                box.append(Entry(start[0], start[1], m.end()))
                start = None
            elif selfc:
                box.append(Entry(_attr(tok, "name"), m.start(), m.end()))
            else:
                start = (_attr(tok, "name"), m.start())
    missing = [s for s in SKINS if s not in seen]
    if missing:
        raise GroomError("the dwarf race has no <skin name=\"%s\">" % '", "'.join(missing))
    return boxes


def _line_start(text, pos, what):
    ls = text.rfind("\n", 0, pos) + 1
    if text[ls:pos].strip():
        raise GroomError("%s (line %d) does not start its own line" % (what, _line(text, pos)))
    return ls


def _line_end(text, end, what):
    j = text.find("\n", end)
    if j < 0 or text[end:j].strip():
        raise GroomError("%s (line %d) does not end its own line" % (what, _line(text, end)))
    return j + 1


def _eol(text):
    crlf = text.count("\r\n")
    return "\r\n" if crlf > text.count("\n") - crlf else "\n"


def _plan_list(text, box, kind, adds, removes, eol):
    """(edit or None, appended, removed, skipped, absent) for one list. Refuses unsafe requests."""
    where = "skin %s %s_meshes" % (box.skin, kind)
    cur = [e.name for e in box.entries]
    edit, appended, removed, skipped = None, [], [], []
    if adds:
        names = [a[0] for a in adds]
        for n in names:
            if cur.count(n) > 1:
                raise GroomError("%s lists %s %d times" % (where, n, cur.count(n)))
        ranks = [i for i, n in enumerate(names) if n in cur]
        m = len(ranks)
        if ranks != list(range(m)):
            raise GroomError("%s: %s is present but an earlier requested name is not, so a fresh append "
                             "would not keep the requested order" % (where, names[ranks[-1]]))
        if m and cur[-m:] != names[:m]:
            n = names[0]
            raise GroomError("%s: %s is already at index %d, but a fresh append would put it at index %d"
                             % (where, n, cur.index(n), len(cur)))
        skipped, appended = names[:m], names[m:]
        if appended:
            at = _line_start(text, box.close, "</%s>" % KINDS[kind][0])
            indent = text[at:box.close] + "\t"
            body = "".join(beard_entry(n, indent, eol) if kind == "beard" else hair_entry(n, t, indent, eol)
                           for n, t in adds[m:])
            edit = Edit(at, at, body)
    if removes:
        gone = {i for i, n in enumerate(cur) if n in removes}
        for i in sorted(gone):
            if i + 1 < len(cur) and i + 1 not in gone:
                raise GroomError("%s: removing %s would shift the index of %s"
                                 % (where, cur[i], cur[i + 1]))
        removed = [cur[i] for i in sorted(gone)]
        if gone:
            first, last = box.entries[min(gone)], box.entries[max(gone)]
            edit = Edit(_line_start(text, first.start, "<%s>" % KINDS[kind][1]), _line_end(
                text, last.end, "</%s>" % KINDS[kind][1]), "")
    absent = [n for n in removes if n not in cur]
    return edit, appended, removed, skipped, absent


def _fingerprint(root):
    """Everything outside the eight target lists, as serialised text per race and skin."""
    out = []
    for r, race in enumerate(root.findall("race")):
        for s, sk in enumerate(race.findall("skin")):
            if race.get("id") == "dwarf" and sk.get("name") in SKINS:
                sk = copy.copy(sk)
                for child in list(sk):
                    if child.tag in ("hair_meshes", "beard_meshes"):
                        sk.remove(child)
            out.append(((r, s), ET.tostring(sk)))
    return out


def _names(root, skin, kind):
    race = next(r for r in root.findall("race") if r.get("id") == "dwarf")
    sk = next(s for s in race.findall("skin") if s.get("name") == skin)
    return [e.get("name") for e in sk.find(KINDS[kind][0])]


def _parse(data, what):
    try:
        return ET.fromstring(data)
    except ET.ParseError as exc:
        raise GroomError("%s does not parse: %s" % (what, exc))


def apply_edits(text, edits):
    for e in sorted(edits, key=lambda e: e.pos, reverse=True):
        text = text[:e.pos] + e.ins + text[e.end:]
    return text


def plan(text, beards, hairs, removes):
    """Plan(text, new_text, rows, edits). Pure: reads nothing, writes nothing. `beards` is a list of
    names, `hairs` a list of (name, style tag), `removes` a list of names. Raises GroomError."""
    span = dwarf_span(text)
    old_root = _parse(text.encode("utf-8"), "the current file")
    boxes = scan(text, span)
    eol = _eol(text)
    wanted = {"beard": [(n, None) for n in beards], "hair": list(hairs)}
    rows, edits, expect = [], [], {}
    for skin in SKINS:
        for kind in ("beard", "hair"):
            adds, rem = wanted[kind], list(removes)
            if not adds and not rem:
                continue
            box = boxes.get((skin, kind))
            if box is None:
                if adds:
                    raise GroomError("skin %s has no usable <%s_meshes> (missing or self-closing)"
                                     % (skin, kind))
                rows.append(Row(skin, kind, 0, 0, [], [], [], rem))
                continue
            cur = [e.name for e in box.entries]
            edit, appended, removed, skipped, absent = _plan_list(text, box, kind, adds, rem, eol)
            if edit:
                edits.append(edit)
            after = [n for n in cur if n not in removed] + appended
            expect[(skin, kind)] = (after, [a[0] for a in adds])
            rows.append(Row(skin, kind, len(cur), len(after), appended, removed, skipped, absent))
    new_text = apply_edits(text, edits)
    new_root = _parse(new_text.encode("utf-8"), "the result")
    for (skin, kind), (after, req) in expect.items():
        got = _names(new_root, skin, kind)
        if got != after:
            what = "tail" if req else "contents"
            raise GroomError("skin %s %s_meshes: the %s is %r, expected %r"
                             % (skin, kind, what, got[-len(req) - 1:] if req else got, after))
        if req and got[-len(req):] != req:
            raise GroomError("skin %s %s_meshes: the tail is not the requested names in order" % (skin, kind))
    if _fingerprint(old_root) != _fingerprint(new_root):
        raise GroomError("the edit changed something outside the eight groom lists")
    return Plan(text, new_text, rows, edits)


def _lines(s):
    parts = s.split("\n")
    return [p + "\n" for p in parts[:-1]] + ([parts[-1]] if parts[-1] else [])


def unified_diff(p, label, context=3):
    """Unified diff built from the edits (all whole-line insertions or deletions), so it is exact and
    fast on a multi-megabyte file."""
    old = _lines(p.text)
    marks = []
    for e in sorted(p.edits, key=lambda e: e.pos):
        first = p.text.count("\n", 0, e.pos)
        marks.append((first, first + p.text.count("\n", e.pos, e.end), _lines(e.ins)))
    groups = []
    for mk in marks:
        if groups and mk[0] - groups[-1][-1][1] <= 2 * context:
            groups[-1].append(mk)
        else:
            groups.append([mk])
    out, delta = ["--- a/%s\n" % label, "+++ b/%s\n" % label], 0
    for g in groups:
        lo, hi = max(0, g[0][0] - context), min(len(old), g[-1][1] + context)
        body, cur = [], lo
        for first, last, ins in g:
            body += [" " + x for x in old[cur:first]] + ["-" + x for x in old[first:last]]
            body += ["+" + x for x in ins]
            cur = last
        body += [" " + x for x in old[cur:hi]]
        grown = sum(len(m[2]) - (m[1] - m[0]) for m in g)
        out.append("@@ -%d,%d +%d,%d @@\n" % (lo + 1, hi - lo, lo + 1 + delta, hi - lo + grown))
        out += body
        delta += grown
    return out


def _stamp():
    return dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def write(path, old, new):
    """Write-once backup, then the file; read back; restore the original bytes on a failure."""
    bak = path + BACKUP_TAG + _stamp()
    if os.path.exists(bak):
        raise GroomError("backup exists: " + bak)
    if open(path, "rb").read() != old:
        raise GroomError("the file changed on disk since it was read")
    with open(bak, "xb") as fh:
        fh.write(old)
    if open(bak, "rb").read() != old:
        raise GroomError("the backup does not read back: " + bak)
    try:
        open(path, "wb").write(new)
        if open(path, "rb").read() != new:
            raise GroomError("the written file does not read back")
    except Exception:
        open(path, "wb").write(old)
        raise
    return bak


def _hair_arg(value):
    name, sep, tag = value.partition(":")
    if not sep or not _NAME_OK.match(name) or not _NAME_OK.match(tag):
        raise argparse.ArgumentTypeError("%r is not NAME:STYLETAG (letters, digits, underscore)" % value)
    return name, tag


def _name_arg(value):
    if not _NAME_OK.match(value):
        raise argparse.ArgumentTypeError("%r is not a name (letters, digits, underscore)" % value)
    return value


def report(rows):
    for skin in SKINS:
        mine = [r for r in rows if r.skin == skin]
        if not mine:
            continue
        print("skin %s" % skin)
        for r in mine:
            bits = ["%s: %d -> %d" % (r.kind, r.before, r.after)]
            for label, names in (("appended", r.appended), ("removed", r.removed),
                                 ("skipped, already present", r.skipped), ("not present", r.absent)):
                if names:
                    bits.append("%s %s" % (label, ", ".join(names)))
            print("  " + "; ".join(bits))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--skins", default=LIVE, help="skins.xml to edit (default: the live Armory's)")
    ap.add_argument("--beard", action="append", default=[], type=_name_arg, metavar="NAME")
    ap.add_argument("--hair", action="append", default=[], type=_hair_arg, metavar="NAME:STYLETAG")
    ap.add_argument("--remove", action="append", default=[], type=_name_arg, metavar="NAME")
    ap.add_argument("--apply", action="store_true", help="write (default is a dry run)")
    args = ap.parse_args(argv)
    if not (args.beard or args.hair or args.remove):
        ap.error("give at least one of --beard, --hair, --remove")
    if args.remove and (args.beard or args.hair):
        ap.error("--remove cannot be combined with --beard or --hair")
    if len(set(args.beard)) != len(args.beard) or len({h[0] for h in args.hair}) != len(args.hair) \
            or len(set(args.remove)) != len(args.remove):
        ap.error("a name is given twice")
    if not os.path.isfile(args.skins):
        print("ERROR: not found: %s" % args.skins, file=sys.stderr)
        return 2
    try:
        data = open(args.skins, "rb").read()
        p = plan(data.decode("utf-8"), args.beard, args.hair, args.remove)
        print("file: %s" % args.skins)
        report(p.rows)
        if p.new_text == p.text:
            print("nothing to do: the file already matches" if not args.remove
                  else "nothing to do: no listed name is present")
            return 0
        diff = unified_diff(p, os.path.basename(args.skins))
        print("diff: %d lines%s" % (len(diff), "" if len(diff) <= MAX_DIFF_LINES
                                    else ", first %d shown" % MAX_DIFF_LINES))
        sys.stdout.write("".join(diff[:MAX_DIFF_LINES]).replace("\r", ""))
        if len(diff) > MAX_DIFF_LINES:
            print("... %d more diff lines not shown" % (len(diff) - MAX_DIFF_LINES))
        if not args.apply:
            print("DRY RUN: nothing written; pass --apply to write")
            return 0
        if os.path.normcase(os.path.abspath(args.skins)) == os.path.normcase(os.path.abspath(LIVE)):
            sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
            from _gamedir import game_or_kit_running
            if game_or_kit_running():
                raise GroomError("the game or the Modding Kit is running; close it first")
        new = p.new_text.encode("utf-8")
        bak = write(args.skins, data, new)
        try:
            again = open(args.skins, "rb").read().decode("utf-8")
            ok = plan(again, args.beard, args.hair, args.remove).new_text == again
        except GroomError:
            ok = False
        if not ok:
            open(args.skins, "wb").write(data)
            raise GroomError("the written file does not re-plan as done; the original bytes are back "
                             "(backup: %s)" % bak)
        print("written: %s\nbackup:  %s" % (args.skins, bak))
        return 0
    except GroomError as exc:
        print("REFUSED: %s" % exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
