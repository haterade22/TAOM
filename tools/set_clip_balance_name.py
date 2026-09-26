#!/usr/bin/env python3
"""
Self-key animation clips for the engine's melee attack table, the edit the Modding Kit makes when a clip's
"Blends with animation" box holds the clip's own name (2026-09-26).

WHY
A swing (`act_(quick_)release_*`) or a blocked recoil (`act_(quick_)blocked_*`) looks its clip up in the melee attack
table (TaleWorlds.Native.dll RVA 0xDB0360; reader 0x659030), and a clip with no row reads null+8: the hill troll
swing CTD at +0x6590B9. A clip gets a row at load (Animation_clip_item vtable slot 5, 0x58BEA0) only when its
metadata "Blends with animation" string (editor name `blends_with_animation_`, in-memory +0xD8) equals its own name,
or through the ten blend children the Kit generates between a clip and its "_balanced" twin. Vanilla self-keys 175
swings that have no twin (fists, lances, staff thrusts), and all 175 leave "Blends with action" empty. The 480
`anim_hill_troll_*` clips were generated with both boxes copied or blanked from vanilla templates, so none has a row.
Full mechanism: docs/features/troll-race.md "The swing CTD"; the Kit field map:
docs/reference/bannerlord-animation-system-map.md.

WHAT IT DOES
For each named clip (matched by the item name inside the package, not the filename) it writes the clip's own name
into "Blends with animation" and empties "Blends with action", exactly the shape Mike's two Kit edits produced on
2026-09-26 (anim_hill_troll_release_overswing_2h, anim_hill_troll_blocked_overswing_2h). It keeps the package valid:
the metadata length, the TOC size (file size - 36) and the item checksum (xxh64, seed 0, over the i64 metadata length
and the metadata) are rewritten, and the RuntimeDataCache entry's stamp (RDC[0x68], a copy of the stored checksum
the Kit writes when it cooks) follows the new checksum. The cooked keyframes in the RDC do not depend on these
fields, so nothing else moves. The metadata version, the Kit's user-data entries and every other byte stay as
they were. A clip already self-keyed is left alone.

REFUSES a package that is not one version-2 AnimationClip item, a TOC size that does not match the file, a
segment, metadata version 0, a "Blends with animation" that names ANOTHER clip, a generated blend child
(parent names set or a child index other than -1), a name over the engine's 63-character row buffer or not ASCII,
a name missing or duplicated in the folder, and a package with no RDC entry.

    python tools/set_clip_balance_name.py --clip anim_hill_troll_release_slashleft_2h ...     # dry run
    python tools/set_clip_balance_name.py --clips-file names.txt --apply                     # write
    python tools/set_clip_balance_name.py --clips-file names.txt --check                     # verify
--apply refuses while the game or the Kit runs, backs each package and RDC entry up to `<file>.bak-balancename-
<stamp>` (never a .tpac or .rdc extension, so no scanner picks it up), writes, re-reads and verifies, and restores
both on any failure. A self-keyed clip is harmless until an action binds it; the binder
(tools/bind_hill_troll_action_set.py) decides that.
"""
import argparse
import datetime as dt
import glob
import os
import struct
import sys
import uuid
from dataclasses import dataclass, field as dc_field

import xxhash

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir, game_or_kit_running  # noqa: E402

MODULE = os.path.join(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"), "Modules", "LOTRLOME_Armory")
CLIPS_DIR = os.path.join(MODULE, "Assets", "Race Test", "Mordor", "Trolls", "animations")
ANIMATION_CLIP = bytes.fromhex("c809655063e5a44cb166a53b92e913a7")
HEADER = 36          # magic, version, package guid, item count, u64 TOC size
MAX_NAME = 63        # 0x5682B0 copies the key into a 64-byte buffer
SEGMENT, USERDATA = 69, 48


class ClipError(Exception):
    pass


@dataclass
class Clip:
    name: str
    package_guid: bytes
    item_guid: bytes
    mlen_at: int
    m0: int
    ck_at: int
    meta_version: int
    blends_action_at: int
    blends_with_action: str
    field_at: int
    field: str
    src1: str
    src2: str
    gen_index: int
    flags: list = dc_field(default_factory=list)
    usages: int = 0
    userdata: int = 0


def _str(buf, at, end):
    if at + 4 > end:
        raise ClipError("truncated string length at %d" % at)
    n = struct.unpack_from("<i", buf, at)[0]
    if n < 0 or at + 4 + n > end:
        raise ClipError("bad string length %d at %d" % (n, at))
    return buf[at + 4:at + 4 + n].decode("utf-8", "replace"), at + 4 + n


def parse(buf):
    """The fields this tool touches and checks, located in the engine's metadata order (0x58C500)."""
    if len(buf) < HEADER + 40 or buf[:4] != b"TPAC" or struct.unpack_from("<I", buf, 4)[0] != 2:
        raise ClipError("not a version-2 TPAC package")
    if struct.unpack_from("<I", buf, 24)[0] != 1:
        raise ClipError("not a single-item package")
    if struct.unpack_from("<Q", buf, 28)[0] != len(buf) - HEADER:
        raise ClipError("TOC size does not match the file size")
    if buf[36:52] != ANIMATION_CLIP:
        raise ClipError("the item is not an AnimationClip")
    name, o = _str(buf, 72, len(buf))
    mlen_at = o
    mlen = struct.unpack_from("<q", buf, o)[0]
    m0 = o + 8
    ck_at = m0 + mlen
    if mlen < 4 or ck_at + 16 > len(buf):
        raise ClipError("metadata length runs past the file")
    segs = struct.unpack_from("<i", buf, ck_at + 8)[0]
    if segs != 0:
        raise ClipError("the package carries %d segment(s); this tool edits metadata-only clips" % segs)
    ud = struct.unpack_from("<i", buf, ck_at + 12)[0]
    if ud < 0 or ck_at + 16 + ud * USERDATA != len(buf):
        raise ClipError("user data does not end the file")
    end = ck_at
    p = m0
    version = struct.unpack_from("<I", buf, p)[0]
    if version < 1:
        raise ClipError("metadata version %d has no Blends with animation field" % version)
    p += 4 + 28 + 16 + 16                     # version, 6 floats + int, animation guid, step points
    for _ in range(3):                         # sound, voice, facial
        _, p = _str(buf, p, end)
    blends_action_at = p
    blends_action, p = _str(buf, p, end)
    _, p = _str(buf, p, end)                   # continue to action
    p += 8                                     # hand poses
    _, p = _str(buf, p, end)                   # combat parameter id
    p += 4 + 4 + 1 + 4                         # blend in, blend out, do not interpolate, int
    field_at = p
    fld, p = _str(buf, p, end)
    src1, p = _str(buf, p, end)
    src2, p = _str(buf, p, end)
    gen = struct.unpack_from("<b", buf, p)[0]
    p += 1 + 4 + 2
    nflags = struct.unpack_from("<i", buf, p)[0]
    p += 4
    flags = []
    for _ in range(max(0, nflags)):
        f, p = _str(buf, p, end)
        flags.append(f)
    usages = struct.unpack_from("<i", buf, p)[0]
    return Clip(name, buf[8:24], buf[52:68], mlen_at, m0, ck_at, version, blends_action_at, blends_action,
                field_at, fld, src1, src2, gen, flags, usages, ud)


def _check_name(name):
    if len(name) > MAX_NAME or not name.isascii():
        raise ClipError("%s: the name must be ASCII and at most %d characters" % (name, MAX_NAME))


def self_key(buf):
    """(new bytes, changed): Blends with animation = own name, Blends with action empty. Pure; writes nothing."""
    c = parse(buf)
    _check_name(c.name)
    if c.src1 or c.src2 or c.gen_index != -1:
        raise ClipError("%s is a generated blend child (parents %r/%r, index %d)" % (c.name, c.src1, c.src2, c.gen_index))
    if c.field and c.field != c.name:
        raise ClipError("%s: Blends with animation already names %r; a twin-keyed clip is the Kit's to change"
                        % (c.name, c.field))
    if c.field == c.name and c.blends_with_action == "":
        return buf, False
    own = c.name.encode("ascii")
    old_action_len = 4 + len(c.blends_with_action.encode("utf-8"))
    old_field_len = 4 + len(c.field.encode("utf-8"))
    new = bytearray()
    new += buf[:c.blends_action_at] + struct.pack("<i", 0)
    new += buf[c.blends_action_at + old_action_len:c.field_at]
    new += struct.pack("<i", len(own)) + own
    new += buf[c.field_at + old_field_len:]
    delta = len(new) - len(buf)
    mlen = struct.unpack_from("<q", buf, c.mlen_at)[0] + delta
    struct.pack_into("<q", new, c.mlen_at, mlen)
    struct.pack_into("<Q", new, 28, len(new) - HEADER)
    ck_at = c.ck_at + delta
    new[ck_at:ck_at + 8] = struct.pack("<Q", xxhash.xxh64(bytes(new[c.mlen_at:ck_at]), seed=0).intdigest())
    new = bytes(new)
    after = parse(new)
    if (after.field, after.blends_with_action) != (c.name, "") or \
            (after.src1, after.src2, after.gen_index, after.flags, after.usages, after.userdata) != \
            (c.src1, c.src2, c.gen_index, c.flags, c.usages, c.userdata):
        raise ClipError("%s: the rewritten metadata does not re-parse as intended" % c.name)
    return new, True


def checksum_ok(buf):
    c = parse(buf)
    return buf[c.ck_at:c.ck_at + 8] == struct.pack("<Q", xxhash.xxh64(buf[c.mlen_at:c.ck_at], seed=0).intdigest())


def rdc_path(module, package_guid):
    return os.path.join(module, "RuntimeDataCache", str(uuid.UUID(bytes_le=bytes(package_guid))).upper() + ".rdc")


def stamp_rdc(entry, item_guid, checksum):
    if len(entry) < 0x70 or entry[:4] != b"RDC0":
        raise ClipError("not an RDC0 entry")
    if entry[0x14:0x24] != item_guid or entry[0x24:0x34] != item_guid:
        raise ClipError("the RDC entry belongs to another item")
    return entry[:0x68] + bytes(checksum) + entry[0x70:]


def keyed_clips(folder, names):
    """The subset of `names` whose `<name>_anm.tpac` in `folder` is self-keyed: the item is that clip, its "Blends
    with animation" holds its own name and it is not a generated child. Those clips have a melee attack table row, so
    a swing or blocked code may play them (the binder's rule 0 and wire_hill_troll_race.py --check read this). A
    missing or unreadable package counts as unkeyed."""
    out = set()
    for name in names:
        try:
            with open(os.path.join(folder, name + "_anm.tpac"), "rb") as fh:
                c = parse(fh.read())
        except (OSError, ClipError, struct.error):
            continue
        if c.name == name and c.field == name and not c.src1 and not c.src2 and c.gen_index == -1:
            out.add(name)
    return out


def index_clips(folder):
    """{item name: [paths]} for every *_anm.tpac under `folder` that parses."""
    out = {}
    for path in glob.glob(os.path.join(folder, "**", "*_anm.tpac"), recursive=True):
        try:
            name = parse(open(path, "rb").read()).name
        except ClipError:
            continue
        out.setdefault(name, []).append(path)
    return out


def plan(names, folder, module):
    index = index_clips(folder)
    rows = []
    for name in names:
        paths = index.get(name, [])
        if len(paths) != 1:
            raise ClipError("%s: %d packages carry that clip under %s" % (name, len(paths), folder))
        buf = open(paths[0], "rb").read()
        new, changed = self_key(buf)
        rdc = rdc_path(module, parse(buf).package_guid)
        if not os.path.isfile(rdc):
            raise ClipError("%s: no RuntimeDataCache entry %s; save the package in the Kit first" % (name, rdc))
        rows.append((name, paths[0], buf, new, changed, rdc))
    return rows


def check(rows):
    bad = 0
    for name, path, buf, _new, _changed, rdc in rows:
        c = parse(buf)
        stamp = open(rdc, "rb").read()[0x68:0x70]
        ok = c.field == name and c.blends_with_action == "" and checksum_ok(buf) and stamp == buf[c.ck_at:c.ck_at + 8]
        bad += not ok
        print("%-6s %-58s field=%r action=%r checksum=%s stamp=%s" % ("OK" if ok else "STALE", name, c.field,
              c.blends_with_action, checksum_ok(buf), stamp == buf[c.ck_at:c.ck_at + 8]))
    return 1 if bad else 0


def apply(rows):
    stamp = dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    for name, path, buf, new, changed, rdc in rows:
        if not changed:
            continue
        entry = open(rdc, "rb").read()
        new_entry = stamp_rdc(entry, parse(new).item_guid, new[parse(new).ck_at:parse(new).ck_at + 8])
        backups = [(path, buf, path + ".bak-balancename-" + stamp), (rdc, entry, rdc + ".bak-balancename-" + stamp)]
        for _src, _data, bak in backups:
            if os.path.exists(bak):
                raise ClipError("backup exists: " + bak)
        for _src, data, bak in backups:
            open(bak, "wb").write(data)
        try:
            open(path, "wb").write(new)
            open(rdc, "wb").write(new_entry)
            if open(path, "rb").read() != new or open(rdc, "rb").read() != new_entry:
                raise ClipError("read-back differs")
            got = parse(new)
            if got.field != name or not checksum_ok(new):
                raise ClipError("the written package does not verify")
        except Exception:
            for src, data, _bak in backups:
                open(src, "wb").write(data)
            raise
        print("written %s (+ its RDC stamp), backups .bak-balancename-%s" % (name, stamp))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--clip", action="append", default=[], help="clip item name (repeatable)")
    ap.add_argument("--clips-file", help="a file with one clip item name per line")
    ap.add_argument("--clips-dir", default=CLIPS_DIR)
    ap.add_argument("--module", default=MODULE, help="the module root holding RuntimeDataCache/")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true", help="report field, checksum and RDC stamp; exit 1 if stale")
    args = ap.parse_args(argv)
    names = list(args.clip)
    if args.clips_file:
        names += [l.strip() for l in open(args.clips_file, encoding="utf-8") if l.strip() and not l.startswith("#")]
    if not names:
        ap.error("name at least one clip")
    try:
        rows = plan(names, args.clips_dir, args.module)
        if args.check:
            return check(rows)
        for name, _p, buf, new, changed, _r in rows:
            print("%-58s %s" % (name, "self-key (%d -> %d bytes)" % (len(buf), len(new)) if changed else "already self-keyed"))
        if not any(r[4] for r in rows):
            print("no change")
            return 0
        if not args.apply:
            print("DRY RUN: nothing written")
            return 0
        if game_or_kit_running():
            print("REFUSED: the game or the Modding Kit is running; close it first", file=sys.stderr)
            return 2
        apply(rows)
        return check(plan(names, args.clips_dir, args.module))
    except ClipError as exc:
        print("REFUSED: %s" % exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
