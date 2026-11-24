#!/usr/bin/env python3
"""
Point existing animation clips at another master and frame range, every other byte kept (2026-09-30, the hill
troll's Fab swings).

WHY
The hill troll's two-handed swing clips (ready, release, quick release, blocked, quick blocked; both stances) carry
what the engine needs to fight with them: the melee attack table key (set_clip_balance_name.py), the flags, the
combat parameter whose collision window times the hit, the hand poses, the blend times and the priority. Their
motion was a human swing retargeted onto the troll, and a two-handed human swing puts the troll's off arm through its
low, forward head. The Fab troll's own one-handed attacks read right on this body (Mike, 2026-09-30), so the clips
keep everything and only change WHAT they play: a clip names its master by GUID and plays Source1..Source2 of it
(backward when Source1 > Source2, as every blocked clip does) over Duration seconds.

WHAT IT DOES
Each plan row (clip, master, source1, source2[, duration]) rewrites the clip's master GUID and its timing floats in
place; the item checksum (xxh64 over the metadata region) follows, and nothing else moves. A row that already holds
those values is left alone. The clip's RuntimeDataCache entry is NOT restamped: it holds keyframes cooked from the
old master and range, so it must go stale until the Modding Kit re-cooks the package on load (open the Armory in the
Kit, save, close). Then `python tools/set_clip_balance_name.py --clips-file <names> --check` (the melee key, the
checksum, the stamp) and `python tools/check_rdc_entries.py` confirm it.

A QUADRUPED GAIT RECORD (#1563, the warg's canter and gallop) states three more things, and a plan row may set each:
  "travel"       LoopDisplacement, the metres the planted feet cover in one loop of the range. Measure it on the master
                 (tools/check_gait_stride.py, measure); a typed value that the feet do not cover makes the beast slide
  "limits"       [PaceSwitchLimitMin, PaceSwitchLimitMax], the speeds at model size between which the pace plays
  "step_points"  the four step points, each -1 (none) or a part of the loop from 0 up to 1
Travel and limits live in the record's quad_movement clip usage. The tool writes them into a usage that is there and
makes none: a gait's in-place twin (warg_canter_stand) has no usage, and a row that gives it travel is refused. The
movement node takes a pace's loop time from two Durations: the "none" row's clip at the lower limit and the front
clip at the upper (docs/features/elephant.md, "What the engine does with a clip record"), so the two records of a
pace take different "duration" values when the cadence must follow the speed.

A ROW MAY STATE THE RECORD'S "flags" (#1565): the whole list, in the Kit's order. The rider's two gallop turn clips
lacked synch_with_horse, so each ran at its own Duration beside a warg whose loop time follows its speed. A plan
refuses a flag that no record of the folder carries: a misspelled flag is a name the engine does not know.

A ROW MAY NAME A "template" (#1565, the warg's walk). The warg's walk bound one record on its front and its "none"
row, so the pace had one loop time; its in-place twin did not exist. A clip that is not in the folder is made from
the template record: the template's bytes under the clip's name, with a package GUID and an item GUID derived from
that name (so a reinstall or a second machine makes the same package), then the row's fields. A clip that is there
ignores its template. A new record has no cache entry: never bind it in an action set before the Kit cooks it
(tools/bind_warg_clips.py holds the warg's binding to that).

REFUSES a clip missing from the folder or carried by two packages there, a master whose package is missing or holds
no single SkeletalAnimation, a range touching frame 0 (the rest frame of a Kit import, never played; allowed only on a
master that a record of the folder already plays from frame 0, as the dire wolf's are), a package the
parser refuses (set_clip_balance_name.parse), a row with a field this tool does not know or without one it needs
(a misspelled field would be skipped and --check would pass with that value never compared), a clip named by
two rows (the second would overwrite the first), a missing clip whose template is missing too, and a file of the new
clip's name that holds another clip. Every refusal comes before the first write, in the dry run too.
Exit codes: 0 done; 1 a refusal, or with --check a record that is pending or not cooked; 2 not run (no clips
folder, no xxhash, or --apply while the game or the Kit runs).

    python tools/repoint_anim_clip.py --plan <plan.json>             # dry run: what would change
    python tools/repoint_anim_clip.py --plan <plan.json> --apply     # write (refused while the game or Kit runs)
    python tools/repoint_anim_clip.py --plan <plan.json> --check     # report each clip's master, range, checksum
The plan is a JSON list of {"clip", "master", "source1", "source2", optional "duration", "travel", "limits",
"step_points", "flags", "template"}; a field not given keeps the clip's own value. --apply backs each package
up to `<file>.bak-repoint-<stamp>` (not a .tpac, so no scanner reads it), writes, re-reads and verifies, and restores
on any failure; a failure on a middle row says which clip failed and how many were written before it.
--under must stay inside the module's Assets folder (no absolute path, drive or `..`).
"""
import argparse
import datetime as dt
import glob
import json
import math
import os
import struct
import sys
import uuid
from collections import namedtuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import set_clip_balance_name as scb  # noqa: E402
from _gamedir import game_or_kit_running  # noqa: E402


class RepointError(Exception):
    pass


GAIT_USAGE = "quad_movement"


def _travel_at(buf, c):
    """Where the travel float of the clip's quad_movement usage is, or None when the clip has no such usage. The
    usage is the record's last field: its name, an empty list, then travel and the two pace limits (the warg's
    records, read 2026-11-23). Anything else there is not taken for one."""
    if c.usages != 1:
        return None
    try:
        name, p = scb._str(buf, c.usages_at + 4, c.ck_at)
    except scb.ClipError:
        return None
    if name != GAIT_USAGE or p + 16 != c.ck_at or struct.unpack_from("<i", buf, p)[0] != 0:
        return None
    return p + 4


def timing(buf):
    """Duration, source1, source2 and master GUID of a clip package (the first three floats after the metadata
    version, then the master's GUID after the three floats and the priority that follow them), its four step points,
    and the travel and [lower, upper] pace limits of its quad_movement usage (None for a clip with no such usage)."""
    c = scb.parse(buf)
    p = c.m0 + 4
    duration, s1, s2 = struct.unpack_from("<3f", buf, p)
    at = _travel_at(buf, c)
    gait = struct.unpack_from("<3f", buf, at) if at is not None else None
    return {"duration": float(duration), "source1": float(s1), "source2": float(s2),
            "master_guid": str(uuid.UUID(bytes_le=bytes(buf[p + 28:p + 44]))),
            "step_points": [float(v) for v in struct.unpack_from("<4f", buf, p + 44)],
            "travel": float(gait[0]) if gait else None,
            "limits": [float(gait[1]), float(gait[2])] if gait else None}


def _floats(what, values, count):
    try:
        out = [float(v) for v in values]
    except (TypeError, ValueError):
        raise RepointError("%s must be %d number(s), got %r" % (what, count, values))
    if len(out) != count or not all(math.isfinite(v) for v in out):
        raise RepointError("%s must be %d finite number(s), got %r" % (what, count, values))
    return out


def _with_flags(buf, flags):
    """`buf` with its flag list replaced, the metadata length and the package size following. Pure."""
    if not isinstance(flags, (list, tuple)) or len(set(map(str, flags))) != len(flags) or not all(
            isinstance(f, str) and f.isascii() and f.replace("_", "").isalnum() for f in flags):
        raise RepointError("flags must be a list of distinct flag names, got %r" % (flags,))
    c = scb.parse(buf)
    if list(flags) == c.flags:
        return buf
    packed = struct.pack("<i", len(flags)) + b"".join(struct.pack("<i", len(f)) + f.encode("ascii") for f in flags)
    new = bytearray(buf[:c.flags_at] + packed + buf[c.usages_at:])
    delta = len(new) - len(buf)
    struct.pack_into("<q", new, c.mlen_at, struct.unpack_from("<q", buf, c.mlen_at)[0] + delta)
    struct.pack_into("<Q", new, 28, len(new) - scb.HEADER)
    return bytes(new)


def repoint(buf, master_guid, source1, source2, duration=None, travel=None, limits=None, step_points=None,
            flags=None, rest_frame_played=False):
    """New package bytes playing `master_guid` from source1 to source2 (either way round) over `duration`
    seconds, with the gait fields and the flag list a row gave; a field that is None keeps the clip's own value.
    `rest_frame_played`: a record already plays this master from frame 0, so frame 0 is a key of the loop. Pure."""
    source1, source2 = _floats("source1/source2", [source1, source2], 2)
    if min(source1, source2) < 1 and not (rest_frame_played and min(source1, source2) == 0):
        raise RepointError("a range must not touch frame 0, the master's rest frame")
    before = scb.parse(buf)
    if flags is not None:
        buf = _with_flags(buf, flags)
    c = scb.parse(buf)
    p = c.m0 + 4
    new = bytearray(buf)
    old_duration = struct.unpack_from("<f", buf, p)[0]
    if duration is not None and not _floats("duration", [duration], 1)[0] > 0:
        raise RepointError("duration must be above 0, got %r" % (duration,))
    struct.pack_into("<3f", new, p, old_duration if duration is None else float(duration), float(source1),
                     float(source2))
    g = master_guid if isinstance(master_guid, uuid.UUID) else uuid.UUID(str(master_guid))
    new[p + 28:p + 44] = g.bytes_le
    if step_points is not None:
        points = _floats("step_points", step_points, 4)
        if not all(v == -1.0 or 0.0 <= v < 1.0 for v in points):
            raise RepointError("each step point is -1 (none) or a part of the loop from 0 up to 1, got %r" % (step_points,))
        struct.pack_into("<4f", new, p + 44, *points)
    if travel is not None or limits is not None:
        at = _travel_at(buf, c)
        if at is None:
            raise RepointError("%s has no quad_movement usage to hold travel or pace limits; this tool writes into a "
                               "usage that is there and makes none" % c.name)
        if travel is not None:
            if not _floats("travel", [travel], 1)[0] >= 0:
                raise RepointError("travel must not be negative, got %r" % (travel,))
            struct.pack_into("<f", new, at, float(travel))
        if limits is not None:
            low, high = _floats("limits", limits, 2)
            if low > high:
                raise RepointError("the lower pace limit %g is above the upper %g" % (low, high))
            struct.pack_into("<2f", new, at + 4, low, high)
    new[c.ck_at:c.ck_at + 8] = struct.pack("<Q", scb._xxh64(bytes(new[c.mlen_at:c.ck_at])))
    new = bytes(new)
    a, b = before, scb.parse(new)
    if (a.name, a.field, a.blends_with_action, a.flags if flags is None else list(flags), a.usages, a.userdata) != \
            (b.name, b.field, b.blends_with_action, b.flags, b.usages, b.userdata):
        raise RepointError("%s: the rewritten package does not re-parse as the same clip" % a.name)
    return new


# The namespace of the GUIDs a made clip gets. Changing it gives every such clip a new identity and a new cache entry.
CLONE_NAMESPACE = uuid.UUID("6f0a4c1e-9a53-4d1b-8a55-1565c11be5a7")


def clone(buf, name):
    """New package bytes: the clip in `buf` under `name`, with a package GUID and an item GUID of its own. Both come
    from the name, so every machine makes one package and its cache entry keeps its file name. Pure."""
    c = scb.parse(buf)
    if not name or not name.isascii() or len(name) > scb.MAX_NAME or not name.replace("_", "").isalnum():
        raise RepointError("%r: a clip name is 1 to %d ASCII letters, digits and underscores" % (name, scb.MAX_NAME))
    if c.field or c.src1 or c.src2 or c.gen_index != -1:
        raise RepointError("%s: the template %s is keyed to a clip (%r) or generated from two (%r, %r); its copy "
                           "would carry that" % (name, c.name, c.field, c.src1, c.src2))
    new = bytearray(buf[:72] + struct.pack("<i", len(name)) + name.encode("ascii") + buf[c.mlen_at:])
    struct.pack_into("<Q", new, 28, len(new) - scb.HEADER)
    new[8:24] = uuid.uuid5(CLONE_NAMESPACE, "package:" + name).bytes_le
    new[52:68] = uuid.uuid5(CLONE_NAMESPACE, "clip:" + name).bytes_le
    new = bytes(new)
    made = scb.parse(new)
    if (made.name, made.field, made.blends_with_action, made.flags, made.usages, made.userdata) != \
            (name, c.field, c.blends_with_action, c.flags, c.usages, c.userdata) or not scb.checksum_ok(new):
        raise RepointError("%s: the copy of %s does not re-parse as that clip under a new name" % (name, c.name))
    return new


def master_guid(folder, master):
    import creature_fit
    path = os.path.join(folder, master + "_geo.tpac")
    if not os.path.isfile(path):
        raise RepointError("master %s: no %s" % (master, path))
    with open(path, "rb") as fh:
        try:
            name, guid = creature_fit.master_item(fh.read())
        except ValueError as exc:
            raise RepointError("master %s: %s" % (master, exc))
    return uuid.UUID(guid)


Row = namedtuple("Row", "clip path buf new changed master made_from")     # buf None: the clip is not there yet
REQUIRED = ("clip", "master", "source1", "source2")
OPTIONAL = ("duration", "travel", "limits", "step_points", "flags")      # each one a keyword of repoint()
MAKES = ("template",)                                           # the record a missing clip is made from


def _refuse_bad_rows(rows):
    """A plan is typed by hand and its --check is a gate. A field this tool does not know would be skipped in
    silence and the record would read DONE with that value never compared; of two rows for one clip the second
    would overwrite the first, because each is built from the file as it is. Both are refused before anything
    is planned. So is a plan with no row: its --check would pass having compared nothing."""
    if not isinstance(rows, list) or not rows or not all(isinstance(r, dict) for r in rows):
        raise RepointError("the plan must be a JSON list of at least one row object")
    for r in rows:
        name = r.get("clip", "(a row with no clip)")
        missing = [k for k in REQUIRED if k not in r]
        if missing:
            raise RepointError("%s: the row lacks %s" % (name, ", ".join(missing)))
        unknown = sorted(set(r) - set(REQUIRED) - set(OPTIONAL) - set(MAKES))
        if unknown:
            raise RepointError("%s: unknown field(s) %s; a row takes %s" % (
                name, ", ".join(unknown), ", ".join(REQUIRED + OPTIONAL + MAKES)))
    names = [r["clip"] for r in rows]
    twice = sorted({n for n in names if names.count(n) > 1})
    if twice:
        raise RepointError("a clip is named by two rows: %s" % ", ".join(twice))


def plan(rows, folder):
    _refuse_bad_rows(rows)
    index, known_flags, from_zero = {}, set(), set()
    for path in glob.glob(os.path.join(folder, "*_anm.tpac")):
        with open(path, "rb") as fh:
            try:
                data = fh.read()
                c = scb.parse(data)
            except (scb.ClipError, struct.error):
                continue
        index.setdefault(c.name, []).append(path)
        known_flags.update(c.flags)
        t = timing(data)
        if min(t["source1"], t["source2"]) == 0:
            from_zero.add(uuid.UUID(t["master_guid"]))
    out = []
    for r in rows:
        strange = [f for f in r.get("flags") or () if isinstance(f, str) and f not in known_flags]
        if strange:
            raise RepointError("%s: no record under %s carries the flag(s) %s; a misspelled flag is a name the "
                               "engine does not know" % (r["clip"], folder, ", ".join(strange)))
        paths = index.get(r["clip"], [])
        if not paths and r.get("template") is not None:
            buf, base, path = None, _from_template(r, index, folder), os.path.join(folder, r["clip"] + "_anm.tpac")
        elif len(paths) != 1:
            raise RepointError("%s: %d packages carry that clip under %s" % (r["clip"], len(paths), folder))
        else:
            path = paths[0]
            with open(path, "rb") as fh:
                buf = base = fh.read()
        g = master_guid(folder, r["master"])
        new = repoint(base, g, r["source1"], r["source2"], rest_frame_played=g in from_zero,
                      **{k: r[k] for k in OPTIONAL if k in r})
        out.append(Row(r["clip"], path, buf, new, new != buf, r["master"], r.get("template") if buf is None else None))
    return out


def _from_template(r, index, folder):
    """The bytes a missing clip starts from: its template under the clip's name. Row.buf is None for such a clip."""
    donors = index.get(r["template"], [])
    if len(donors) != 1:
        raise RepointError("%s: %d packages carry its template %s under %s" % (
            r["clip"], len(donors), r["template"], folder))
    path = os.path.join(folder, r["clip"] + "_anm.tpac")
    if os.path.exists(path):
        raise RepointError("%s: %s is there and does not carry that clip; it is never overwritten" % (r["clip"], path))
    with open(donors[0], "rb") as fh:
        return clone(fh.read(), r["clip"])


def apply(rows, stamp=None):
    stamp = stamp or dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    todo = [r for r in rows if r.changed]
    for r in todo:
        if r.buf is not None and os.path.exists(r.path + ".bak-repoint-" + stamp):
            raise RepointError("backup exists: %s.bak-repoint-%s" % (r.path, stamp))
    written = []
    for r in todo:
        try:
            _write_row(r, stamp)
        except Exception as exc:
            raise RepointError("%s failed (%s: %s). %d of %d changed record(s) were written before it%s; this one is "
                               "restored and the rest are untouched" % (
                                   r.clip, type(exc).__name__, exc, len(written), len(todo),
                                   " (" + ", ".join(written) + ")" if written else "")) from exc
        written.append(r.clip)
    return len(todo)


def _write_row(r, stamp):
    if r.buf is None:
        _make(r)
        return
    with open(r.path + ".bak-repoint-" + stamp, "wb") as fh:
        fh.write(r.buf)
    try:
        with open(r.path, "wb") as fh:
            fh.write(r.new)
        with open(r.path, "rb") as fh:
            if fh.read() != r.new or not scb.checksum_ok(r.new):
                raise RepointError("%s: read-back or checksum differs" % r.clip)
    except Exception:
        with open(r.path, "wb") as fh:
            fh.write(r.buf)
        raise
    print("written %s -> %s %s (backup .bak-repoint-%s)" % (r.clip, r.master, _range(r.new), stamp))


def _make(r):
    """Write a clip that was not there. Mode x: a file that appeared since the plan is never overwritten. A new
    file has nothing to back up, and a failed write leaves none behind."""
    try:
        fh = open(r.path, "xb")
    except FileExistsError:
        raise RepointError("%s: %s appeared since the plan; it is never overwritten" % (r.clip, r.path))
    try:                        # from here the file is this call's own, so any failure takes it away again
        with fh:
            fh.write(r.new)
        with open(r.path, "rb") as back:
            if back.read() != r.new or not scb.checksum_ok(r.new):
                raise RepointError("%s: read-back or checksum differs" % r.clip)
    except Exception:
        os.remove(r.path)
        raise
    print("made    %s from %s -> %s %s (a new file: no backup)" % (r.clip, r.made_from, r.master, _range(r.new)))


def under_folder(module, under):
    """The clips folder `under` names inside the module's Assets folder. An absolute path, a drive, or a `..` part
    would resolve outside it, so each is refused (#1566)."""
    parts = under.replace("\\", "/").split("/")
    if os.path.isabs(under) or under.startswith(("/", "\\")) or ":" in under or ".." in parts \
            or not any(p not in ("", ".") for p in parts):
        raise RepointError("--under %r must be a folder inside the module's Assets folder: no absolute path, no "
                           "drive and no '..'" % under)
    return os.path.join(module, "Assets", *[p for p in parts if p not in ("", ".")])


def _range(buf):
    t = timing(buf)
    gait = "" if t["travel"] is None else ", travel %.2f m, limits %g..%g" % (t["travel"], t["limits"][0], t["limits"][1])
    return "%g..%g over %.2f s%s" % (t["source1"], t["source2"], t["duration"], gait)


def cook_fault(module, buf, master_path):
    """None when the Kit cooked the clip as it is now, else why not. The game plays the keys in the clip's
    RuntimeDataCache entry, so a clip repointed since its last cook still plays its old master. Two signals, because
    neither covers the other: the entry carries the record's stored checksum (a changed record), and the entry is no
    older than the master's file (a master whose keys changed under one GUID leaves record and stamp alike; the
    dates are then the only signal, as tools/check_gait_stride.py cache_state reads them)."""
    c = scb.parse(buf)
    entry = scb.rdc_path(module, c.package_guid)
    if not os.path.isfile(entry):
        return "it has no cache entry (%s)" % entry
    with open(entry, "rb") as fh:
        if not scb.rdc_current(fh.read(), c, buf):
            return "its cache entry does not carry the record's checksum"
    if os.path.getmtime(entry) < os.path.getmtime(master_path):
        return "its cache entry is older than its master %s" % os.path.basename(master_path)
    return None


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--plan", required=True)
    ap.add_argument("--clips-dir", default=scb.CLIPS_DIR)
    ap.add_argument("--module", default=scb.MODULE, help="the module root, which holds RuntimeDataCache/")
    ap.add_argument("--under", help="the clips folder as a subfolder of the module's Assets, e.g. "
                                    "creature/warg/animations; replaces --clips-dir")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    try:
        clips_dir = under_folder(args.module, args.under) if args.under is not None else args.clips_dir
    except RepointError as exc:
        print("refused: %s" % exc)
        return 1
    # Exit 2 is NOT RUN, as tools/data_gates.py reads it: a machine without the module or without xxhash has no
    # finding to report. A plan the tool refuses is a finding: exit 1 and the reason, no traceback.
    if not os.path.isdir(clips_dir):
        print("NOT RUN: no clips folder %s" % clips_dir)
        return 2
    try:
        with open(args.plan, encoding="utf-8-sig") as fh:
            wanted = json.load(fh)
    except (OSError, ValueError) as exc:
        print("refused: cannot read the plan %s: %s" % (args.plan, exc))
        return 1
    try:
        rows = plan(wanted, clips_dir)
    except ModuleNotFoundError as exc:
        if exc.name != "xxhash":            # any other import fault is a fault of this tool, and must fail
            raise
        print("NOT RUN: %s (the item checksum needs xxhash)" % exc)
        return 2
    except RepointError as exc:
        print("refused: %s" % exc)
        return 1
    if args.check:
        bad = raw = 0
        for r in rows:
            if r.buf is None:
                bad += 1
                print("%-8s %-60s is not there: --apply makes it from %s" % ("PENDING", r.clip, r.made_from))
                continue
            done = not r.changed
            fault = cook_fault(args.module, r.buf, os.path.join(os.path.dirname(r.path), r.master + "_geo.tpac"))
            bad += not done
            raw += done and fault is not None
            print("%-8s %-60s %s %s checksum=%s cooked=%s%s" % (
                "DONE" if done else "PENDING", r.clip, r.master, _range(r.buf), scb.checksum_ok(r.buf), fault is None,
                " (%s)" % fault if done and fault else ""))
        if raw:
            print("%d record(s) are not cooked as they are now: the game still plays their old keys. Start the "
                  "Modding Kit once with the module loaded, close it, and run this check again" % raw)
        return 1 if bad or raw else 0
    for r in rows:
        print("%-8s %-60s -> %s %s" % ("MAKE" if r.buf is None else "CHANGE" if r.changed else "same", r.clip, r.master,
                                       _range(r.new)))
    if not args.apply:
        print("dry run: %d of %d would change; --apply writes" % (sum(r.changed for r in rows), len(rows)))
        return 0
    if game_or_kit_running():
        print("refused: the game or the Modding Kit is running")
        return 2
    try:
        n = apply(rows)
    except RepointError as exc:
        print("refused: %s. A record written before it stays written, with its backup beside it; --check shows "
              "what is left, and --apply again writes it" % exc)
        return 1
    print("%d written. Next: open the Armory in the Modding Kit, save, close (the Kit re-cooks their RuntimeDataCache "
          "entries), then set_clip_balance_name.py --check and check_rdc_entries.py" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
