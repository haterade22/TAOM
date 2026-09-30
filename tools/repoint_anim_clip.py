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

REFUSES a clip missing from the folder or carried by two packages there, a master whose package is missing or holds
no single SkeletalAnimation, a range touching frame 0 (a master's rest frame, never played), and a package the
parser refuses (set_clip_balance_name.parse).

    python tools/repoint_anim_clip.py --plan <plan.json>             # dry run: what would change
    python tools/repoint_anim_clip.py --plan <plan.json> --apply     # write (refused while the game or Kit runs)
    python tools/repoint_anim_clip.py --plan <plan.json> --check     # report each clip's master, range, checksum
The plan is a JSON list of {"clip", "master", "source1", "source2", optional "duration"}. --apply backs each package
up to `<file>.bak-repoint-<stamp>` (not a .tpac, so no scanner reads it), writes, re-reads and verifies, and restores
on any failure.
"""
import argparse
import datetime as dt
import glob
import json
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


def timing(buf):
    """(duration, source1, source2, master GUID) of a clip package: the first three floats after the metadata
    version, then the master's GUID after the three floats and the priority that follow them."""
    c = scb.parse(buf)
    p = c.m0 + 4
    duration, s1, s2 = struct.unpack_from("<3f", buf, p)
    return {"duration": float(duration), "source1": float(s1), "source2": float(s2),
            "master_guid": str(uuid.UUID(bytes_le=bytes(buf[p + 28:p + 44])))}


def repoint(buf, master_guid, source1, source2, duration=None):
    """New package bytes playing `master_guid` from source1 to source2 (either way round) over `duration`
    seconds (the clip's own when None). Pure."""
    if min(source1, source2) < 1:
        raise RepointError("a range must not touch frame 0, the master's rest frame")
    c = scb.parse(buf)
    p = c.m0 + 4
    new = bytearray(buf)
    old_duration = struct.unpack_from("<f", buf, p)[0]
    struct.pack_into("<3f", new, p, old_duration if duration is None else float(duration), float(source1),
                     float(source2))
    g = master_guid if isinstance(master_guid, uuid.UUID) else uuid.UUID(str(master_guid))
    new[p + 28:p + 44] = g.bytes_le
    new[c.ck_at:c.ck_at + 8] = struct.pack("<Q", scb._xxh64(bytes(new[c.mlen_at:c.ck_at])))
    new = bytes(new)
    a, b = scb.parse(buf), scb.parse(new)
    if (a.name, a.field, a.blends_with_action, a.flags, a.usages, a.userdata) != \
            (b.name, b.field, b.blends_with_action, b.flags, b.usages, b.userdata):
        raise RepointError("%s: the rewritten package does not re-parse as the same clip" % a.name)
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


Row = namedtuple("Row", "clip path buf new changed master")


def plan(rows, folder):
    index = {}
    for path in glob.glob(os.path.join(folder, "*_anm.tpac")):
        with open(path, "rb") as fh:
            try:
                index.setdefault(scb.parse(fh.read()).name, []).append(path)
            except (scb.ClipError, struct.error):
                continue
    out = []
    for r in rows:
        paths = index.get(r["clip"], [])
        if len(paths) != 1:
            raise RepointError("%s: %d packages carry that clip under %s" % (r["clip"], len(paths), folder))
        with open(paths[0], "rb") as fh:
            buf = fh.read()
        g = master_guid(folder, r["master"])
        new = repoint(buf, g, r["source1"], r["source2"], r.get("duration"))
        out.append(Row(r["clip"], paths[0], buf, new, new != buf, r["master"]))
    return out


def apply(rows, stamp=None):
    stamp = stamp or dt.datetime.now().strftime("%Y%m%d-%H%M%S")
    todo = [r for r in rows if r.changed]
    for r in todo:
        if os.path.exists(r.path + ".bak-repoint-" + stamp):
            raise RepointError("backup exists: %s.bak-repoint-%s" % (r.path, stamp))
    for r in todo:
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
    return len(todo)


def _range(buf):
    t = timing(buf)
    return "%g..%g over %.2f s" % (t["source1"], t["source2"], t["duration"])


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--plan", required=True)
    ap.add_argument("--clips-dir", default=scb.CLIPS_DIR)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    with open(args.plan, encoding="utf-8-sig") as fh:
        rows = plan(json.load(fh), args.clips_dir)
    if args.check:
        bad = 0
        for r in rows:
            done = not r.changed
            bad += not done
            print("%-8s %-60s %s %s checksum=%s" % ("DONE" if done else "PENDING", r.clip, r.master, _range(r.buf),
                                                   scb.checksum_ok(r.buf)))
        return 1 if bad else 0
    for r in rows:
        print("%-8s %-60s -> %s %s" % ("CHANGE" if r.changed else "same", r.clip, r.master, _range(r.new)))
    if not args.apply:
        print("dry run: %d of %d would change; --apply writes" % (sum(r.changed for r in rows), len(rows)))
        return 0
    if game_or_kit_running():
        print("refused: the game or the Modding Kit is running")
        return 2
    n = apply(rows)
    print("%d written. Next: open the Armory in the Modding Kit, save, close (the Kit re-cooks their RuntimeDataCache "
          "entries), then set_clip_balance_name.py --check and check_rdc_entries.py" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
