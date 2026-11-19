#!/usr/bin/env python3
"""What does a release channel lack against the live install? (#1480)

Compares the live Modules folder with a channel's Modules folder (E:/LOTRAOM_Releases/<channel>/Modules)
file by file and groups every difference by module, directory and kind, so the maintainer approves the
groups that ship and leaves work in progress behind. Live is the ground truth, except for the files
tools/package_release.py never ships (RuntimeDataCache, AssetSources, module-level shader sacks, backup
sidecars, IDE state, runtime logs): its classify() decides, so the two tools cannot drift apart.

Verdicts:
  NEW           only in live; ships when its group is approved
  CHANGED       in both, contents differ (size, or SHA-256 when the size matches and the mtime does not)
  SAME          in both, same contents
  CHANNEL_ONLY  only in the channel; a candidate to set aside
  CHANNEL_JUNK  in the channel, and the packager would never ship it (a .bak, Prefabs_Unused, ...)
  KIT_PACKAGE   an AssetPackages tpac only the channel holds: Modding Kit output, never set aside
  EXCLUDED      in live, never shipped (the packager rule is named)
  UNKNOWN       in live, not on the packager's include-list: reported, never grouped

Read-only by default. --apply --groups <id> ... copies only the named groups. Every channel file it
replaces or removes moves to <releases>/_set-aside/<channel>-<stamp>/Modules/ first; nothing is deleted.
A group under bin/ also needs --allow-bin, after the DLL stamp gate in the /channel-sync skill.
The launcher manifest.json is never touched.

Usage:
  python tools/diff_release_channel.py E:/LOTRAOM_Releases/public/Modules [--json PATH]
  python tools/diff_release_channel.py E:/LOTRAOM_Releases/public/Modules --apply --groups "TAOM/ModuleData/changed"

Exit code: 0 no difference, 1 differences listed (or apply done: 0), 2 bad input, 3 the tool crashed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import time
import traceback
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path, PurePosixPath

sys.path.insert(0, str(Path(__file__).resolve().parent))

import package_release as pr  # noqa: E402

NEW, CHANGED, SAME = "NEW", "CHANGED", "SAME"
CHANNEL_ONLY, CHANNEL_JUNK, KIT_PACKAGE = "CHANNEL_ONLY", "CHANNEL_JUNK", "KIT_PACKAGE"
EXCLUDED, UNKNOWN = "EXCLUDED", "UNKNOWN"
GROUP_DEPTH = 3          # directory components in a group id: Assets/creature/elephant, SceneObj/<scene>
MTIME_SLACK = 2.0        # FAT-style rounding; copy2 keeps the mtime, so a synced file reads SAME
# Channel junk that --delete-junk removes outright (Mike, 2026-11-19); every other set-aside group moves.
DELETE_RULES = frozenset({"BACKUP_SIDECAR", "SCENE_BACKUPS", "PREFABS_UNUSED"})
DEFAULT_LIVE = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")


class InputError(Exception):
    pass


@dataclass
class Entry:
    module: str
    rel: str                       # module-relative POSIX path, live casing when live has the file
    verdict: str
    rule: str = ""
    src: Path | None = None        # the file that ships (live, or the repo stub)
    dst: Path | None = None        # the channel file when it exists
    size: int = 0
    mtime: float = 0.0
    group: str = ""


@dataclass
class Group:
    id: str
    kind: str                      # new, changed, set-aside
    entries: list = field(default_factory=list)

    @property
    def bytes(self) -> int:
        return sum(e.size for e in self.entries)

    @property
    def newest(self) -> float:
        return max((e.mtime for e in self.entries), default=0.0)


@dataclass
class Report:
    live: Path
    channel: Path
    entries: list = field(default_factory=list)
    groups: dict = field(default_factory=dict)
    absent_modules: list = field(default_factory=list)
    notes: list = field(default_factory=list)

    def add(self, entry: Entry, group_id: str = "", kind: str = "") -> None:
        self.entries.append(entry)
        if group_id:
            entry.group = group_id
            self.groups.setdefault(group_id, Group(group_id, kind)).entries.append(entry)


def _walk(root: Path) -> dict:
    """casefolded rel -> (rel, path, stat) for every file under root."""
    out = {}
    if not root.is_dir():
        return out
    for dirpath, _dirs, files in os.walk(root):
        for fn in files:
            p = Path(dirpath) / fn
            rel = p.relative_to(root).as_posix()
            out[rel.casefold()] = (rel, p, p.stat())
    return out


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def _same(a: Path, sa, b: Path, sb) -> bool:
    if sa.st_size != sb.st_size:
        return False
    if abs(sa.st_mtime - sb.st_mtime) <= MTIME_SLACK:
        return True
    return _sha256(a) == _sha256(b)


def _dir_key(rel: str) -> str:
    parts = PurePosixPath(rel).parts[:-1][:GROUP_DEPTH]
    return "/".join(parts) if parts else "(root)"


def _is_kit_package(rel: str) -> bool:
    p = PurePosixPath(rel)
    return len(p.parts) >= 2 and p.parts[0].casefold() == "assetpackages" and p.suffix.casefold() == ".tpac"


def _diff_module(report: Report, module: str, live_root: Path, chan_root: Path) -> None:
    live, chan = _walk(live_root), _walk(chan_root)
    for key in sorted(set(live) | set(chan)):
        if key in live:
            rel, path, st = live[key]
            decision = pr.classify(rel)
            c = chan.get(key)
            if decision.action == pr.EXCLUDE:
                if c:
                    report.add(Entry(module, c[0], CHANNEL_JUNK, decision.rule, dst=c[1], size=c[2].st_size,
                                     mtime=c[2].st_mtime), f"{module}/set-aside/{decision.rule}", "set-aside")
                else:
                    report.add(Entry(module, rel, EXCLUDED, decision.rule, src=path, size=st.st_size,
                                     mtime=st.st_mtime))
                continue
            if decision.action == pr.UNKNOWN:
                report.add(Entry(module, rel, UNKNOWN, decision.reason, src=path, size=st.st_size, mtime=st.st_mtime))
                continue
            if c is None:
                report.add(Entry(module, rel, NEW, src=path, size=st.st_size, mtime=st.st_mtime),
                           f"{module}/{_dir_key(rel)}/new", "new")
            elif _same(path, st, c[1], c[2]):
                report.add(Entry(module, rel, SAME, src=path, dst=c[1], size=st.st_size, mtime=st.st_mtime))
            else:
                report.add(Entry(module, rel, CHANGED, src=path, dst=c[1], size=st.st_size, mtime=st.st_mtime),
                           f"{module}/{_dir_key(rel)}/changed", "changed")
        else:
            rel, path, st = chan[key]
            decision = pr.classify(rel)
            if decision.action == pr.EXCLUDE:
                report.add(Entry(module, rel, CHANNEL_JUNK, decision.rule, dst=path, size=st.st_size,
                                 mtime=st.st_mtime), f"{module}/set-aside/{decision.rule}", "set-aside")
            elif _is_kit_package(rel):
                report.add(Entry(module, rel, KIT_PACKAGE, dst=path, size=st.st_size, mtime=st.st_mtime))
            else:
                report.add(Entry(module, rel, CHANNEL_ONLY, dst=path, size=st.st_size, mtime=st.st_mtime),
                           f"{module}/set-aside/channel-only", "set-aside")


def _diff_stub(report: Report, stub: str, stubs_root: Path, channel: Path) -> None:
    src = stubs_root / stub / "_Module" / "SubModule.xml"
    if not src.is_file():
        raise InputError(f"repo stub missing: {src}")
    dst = channel / stub / "SubModule.xml"
    st = src.stat()
    if not dst.is_file():
        report.add(Entry(stub, "SubModule.xml", NEW, src=src, size=st.st_size, mtime=st.st_mtime),
                   f"{stub}/(stub)/new", "new")
    elif src.read_bytes() == dst.read_bytes():
        report.add(Entry(stub, "SubModule.xml", SAME, src=src, dst=dst, size=st.st_size, mtime=st.st_mtime))
    else:
        report.add(Entry(stub, "SubModule.xml", CHANGED, src=src, dst=dst, size=st.st_size, mtime=st.st_mtime),
                   f"{stub}/(stub)/changed", "changed")


def default_modules(live: Path, channel: Path) -> tuple:
    names = set(pr.DEFAULT_MODULES)
    if channel.is_dir():
        names |= {p.name for p in channel.iterdir() if p.is_dir()}
    names -= set(pr.STUB_MODULES)
    return tuple(sorted(names))


def diff(live: Path, channel: Path, *, modules, stubs=(), stubs_root: Path = pr.STUBS_ROOT) -> Report:
    if not live.is_dir():
        raise InputError(f"live Modules folder not found: {live}")
    if not channel.is_dir():
        raise InputError(f"channel Modules folder not found: {channel}")
    report = Report(live, channel)
    for module in modules:
        live_root, chan_root = live / module, channel / module
        if not live_root.is_dir():
            if chan_root.is_dir():
                report.notes.append(f"{module}: in the channel, not in live; not compared")
                continue
            raise InputError(f"module {module} is in neither live nor the channel")
        if not chan_root.is_dir():
            report.absent_modules.append(module)
        _diff_module(report, module, live_root, chan_root)
    for stub in stubs:
        _diff_stub(report, stub, stubs_root, channel)
    return report


def _resolve_ci(base: Path, rel: str) -> Path:
    """The channel path for rel, reusing the casing of every directory the channel already has."""
    cur = base
    parts = PurePosixPath(rel).parts
    for i, part in enumerate(parts):
        nxt = cur / part
        if not nxt.exists() and cur.is_dir():
            match = next((c for c in cur.iterdir() if c.name.casefold() == part.casefold()), None)
            if match is not None:
                nxt = match
        cur = nxt
    return cur


def apply(report: Report, channel: Path, groups, *, set_aside_root: Path | None = None, stamp: str | None = None,
          allow_bin: bool = False) -> Path:
    if not groups:
        raise InputError("--apply needs --groups: name each group to ship")
    missing = [g for g in groups if g not in report.groups]
    if missing:
        raise InputError(f"no such group: {', '.join(missing)}")
    bin_groups = [g for g in groups if any(PurePosixPath(e.rel).parts[0].casefold() == "bin"
                                           for e in report.groups[g].entries)]
    if bin_groups and not allow_bin:
        raise InputError(f"bin groups need --allow-bin after the DLL stamp gate: {', '.join(bin_groups)}")
    set_aside_root = set_aside_root or channel.parent.parent / "_set-aside"
    stamp = stamp or datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir = set_aside_root / f"{channel.parent.name}-{stamp}"
    aside = run_dir / "Modules"
    log = []
    for gid in groups:
        for e in report.groups[gid].entries:
            if e.dst is not None and e.dst.exists():
                target = aside / e.dst.relative_to(channel)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.move(str(e.dst), str(target))
                log.append({"group": gid, "action": "set-aside", "from": str(e.dst), "to": str(target)})
            if e.verdict in (NEW, CHANGED):
                dst = e.dst if e.dst is not None else _resolve_ci(channel, f"{e.module}/{e.rel}")
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(e.src, dst)
                log.append({"group": gid, "action": "copy", "from": str(e.src), "to": str(dst)})
    run_dir.mkdir(parents=True, exist_ok=True)
    log_path = run_dir / "moves.json"
    log_path.write_text(json.dumps(log, indent=1), encoding="utf-8")
    return log_path


def junk_to_delete(report: Report) -> list:
    return [e for e in report.entries if e.verdict == CHANNEL_JUNK and e.rule in DELETE_RULES]


def delete_junk(report: Report, channel: Path, *, set_aside_root: Path | None = None,
                stamp: str | None = None) -> Path:
    """Delete the channel's backup sidecars, scene backups and Prefabs_Unused files (Mike, 2026-11-19:
    delete, not quarantine), prune the folders that leaves empty, and log each path."""
    set_aside_root = set_aside_root or channel.parent.parent / "_set-aside"
    stamp = stamp or datetime.now().strftime("%Y%m%d-%H%M%S")
    deleted = []
    for e in junk_to_delete(report):
        e.dst.unlink()
        deleted.append(str(e.dst))
        parent = e.dst.parent
        module_root = channel / e.module
        while parent != module_root and parent.is_dir() and not any(parent.iterdir()):
            parent.rmdir()
            parent = parent.parent
    set_aside_root.mkdir(parents=True, exist_ok=True)
    log = set_aside_root / f"{channel.parent.name}-{stamp}-deleted.txt"
    log.write_text("".join(f"{p}\n" for p in deleted), encoding="utf-8")
    return log


def _mb(n: int) -> str:
    return f"{n / 1048576:.1f} MB" if n >= 1048576 else f"{n / 1024:.1f} KB"


def _date(t: float) -> str:
    return time.strftime("%Y-%m-%d %H:%M", time.localtime(t)) if t else "-"


def format_report(report: Report) -> str:
    lines = [f"live:    {report.live}", f"channel: {report.channel}"]
    counts = {}
    for e in report.entries:
        counts[e.verdict] = counts.get(e.verdict, 0) + 1
    lines.append("files: " + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
    for m in report.absent_modules:
        lines.append(f"ABSENT in channel: {m}")
    lines.extend(f"NOTE: {n}" for n in report.notes)
    if report.groups:
        lines.append("")
        lines.append("groups (newest live file first):")
        for g in sorted(report.groups.values(), key=lambda g: (g.kind == "set-aside", -g.newest, g.id)):
            lines.append(f"  {g.id}  {len(g.entries)} files  {_mb(g.bytes)}  newest {_date(g.newest)}")
            for e in g.entries[:5]:
                lines.append(f"      {e.rel}")
            if len(g.entries) > 5:
                lines.append(f"      ... {len(g.entries) - 5} more")
    excluded = {}
    for e in report.entries:
        if e.verdict == EXCLUDED:
            excluded[e.rule] = excluded.get(e.rule, 0) + 1
    if excluded:
        lines.append("")
        lines.append("left out of the copy (package_release rules): "
                     + ", ".join(f"{k} {v}" for k, v in sorted(excluded.items())))
    for verdict, label in ((UNKNOWN, "UNKNOWN (not on the include-list, never copied)"),
                           (KIT_PACKAGE, "KIT_PACKAGE (channel tpac live lacks; Kit output, kept)")):
        rows = [e for e in report.entries if e.verdict == verdict]
        if rows:
            lines.append("")
            lines.append(f"{label}: {len(rows)}")
            lines.extend(f"  {e.module}/{e.rel}" for e in rows[:20])
    return "\n".join(lines)


def to_json(report: Report) -> dict:
    return {
        "live": str(report.live), "channel": str(report.channel),
        "absent_modules": report.absent_modules, "notes": report.notes,
        "groups": {g.id: {"kind": g.kind, "bytes": g.bytes, "newest": _date(g.newest),
                          "files": [e.rel for e in g.entries]} for g in report.groups.values()},
        "unknown": [f"{e.module}/{e.rel}" for e in report.entries if e.verdict == UNKNOWN],
        "kit_packages": [f"{e.module}/{e.rel}" for e in report.entries if e.verdict == KIT_PACKAGE],
    }


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("channel", type=Path, help="The channel's Modules folder")
    ap.add_argument("--live", type=Path, default=DEFAULT_LIVE, help="The live Modules folder (default: the install)")
    ap.add_argument("--modules", nargs="+", help="Modules to compare (default: the release set plus the channel's)")
    ap.add_argument("--no-stubs", action="store_true", help="Skip the four BUTR alias stubs")
    ap.add_argument("--json", type=Path, help="Write the grouped report as JSON")
    ap.add_argument("--apply", action="store_true", help="Copy the named groups into the channel")
    ap.add_argument("--groups", nargs="+", default=[], help="Group ids to apply")
    ap.add_argument("--allow-bin", action="store_true", help="Allow bin/ groups (only after the DLL stamp gate)")
    ap.add_argument("--delete-junk", action="store_true",
                    help="List (with --apply: delete) the channel's backup sidecars, scene backups and Prefabs_Unused")
    args = ap.parse_args(argv)

    if args.apply and not args.groups and not args.delete_junk:
        print("ERROR: --apply needs --groups: name each group to ship.", file=sys.stderr)
        return 2
    if args.delete_junk and args.groups:
        print("ERROR: run --delete-junk on its own, then --groups.", file=sys.stderr)
        return 2
    try:
        modules = tuple(args.modules) if args.modules else default_modules(args.live, args.channel)
        stubs = () if args.no_stubs else pr.STUB_MODULES
        report = diff(args.live, args.channel, modules=modules, stubs=stubs)
        if args.delete_junk:
            junk = junk_to_delete(report)
            by_rule = {}
            for e in junk:
                by_rule[e.rule] = by_rule.get(e.rule, 0) + 1
            summary = ", ".join(f"{k} {v}" for k, v in sorted(by_rule.items())) or "nothing"
            if not args.apply:
                print(f"would delete {len(junk)} file(s) from {args.channel}: {summary}")
                for e in junk:
                    print(f"  {e.module}/{e.rel}")
                return 1 if junk else 0
            log = delete_junk(report, args.channel)
            print(f"deleted {len(junk)} file(s) from {args.channel}: {summary}; list in {log}")
            return 0
        if args.apply:
            log = apply(report, args.channel, args.groups, allow_bin=args.allow_bin)
            print(f"applied {len(args.groups)} group(s); moves logged in {log}")
            print("Re-run without --apply to confirm, then run the channel checks in /channel-sync.")
            return 0
    except InputError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    print(format_report(report))
    if args.json:
        args.json.write_text(json.dumps(to_json(report), indent=1), encoding="utf-8")
        print(f"\njson: {args.json}")
    unknown = any(e.verdict == UNKNOWN for e in report.entries)
    return 1 if (report.groups or report.absent_modules or unknown) else 0


def entry() -> int:
    """main() with a crash turned into exit 3, so a traceback never reads as "differences found"."""
    try:
        return main()
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        print("ERROR: tools/diff_release_channel.py crashed (traceback above); no verdict, exit 3.", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(entry())
