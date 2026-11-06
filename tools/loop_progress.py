#!/usr/bin/env python3
"""No-progress breaker for work-driving loops (#998).

A loop calls this once per work tick. It fingerprints what the files show (git HEAD, status,
diff, untracked files, and any out-of-repo watch paths), compares with the previous tick's
fingerprint kept in a state file, and counts consecutive ticks with no change. The count lives on
disk, so it survives a compaction.

Exit codes: 0 progress or under the limit, 3 limit reached, 1 git failed, 2 usage.

Usage:
    python tools/loop_progress.py --state <file> [--repo <dir>] [--watch-path <path>]... [--limit 3]
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import tempfile

PARTS = ("head", "status", "diff", "untracked", "watch")


class GitError(Exception):
    pass


def run_git(repo, *args, allow_fail=False):
    try:
        proc = subprocess.run(["git", "-C", repo, *args], capture_output=True)
    except OSError as exc:
        raise GitError("git could not run: %s" % exc)
    if proc.returncode != 0 and not allow_fail:
        msg = proc.stderr.decode("utf-8", "replace").strip()
        raise GitError("git %s failed: %s" % (" ".join(args), msg))
    return proc.returncode, proc.stdout


def sha(data):
    return hashlib.sha256(data).hexdigest()


def stat_text(path):
    st = os.stat(path)
    return "%d:%d" % (st.st_size, st.st_mtime_ns)


def hash_watch(paths):
    lines = []
    for raw in paths:
        path = os.path.abspath(raw)
        if os.path.isdir(path):
            entries = []
            for dirpath, _dirs, files in os.walk(path):
                for name in files:
                    full = os.path.join(dirpath, name)
                    try:
                        entries.append((os.path.relpath(full, path), stat_text(full)))
                    except OSError:
                        entries.append((os.path.relpath(full, path), "gone"))
            lines.append("dir %s" % path)
            lines.extend("%s|%s" % e for e in sorted(entries))
        elif os.path.exists(path):
            lines.append("file %s|%s" % (path, stat_text(path)))
        else:
            lines.append("missing %s" % path)
    return sha("\n".join(lines).encode("utf-8", "surrogateescape"))


def compute_parts(repo, watch_paths):
    run_git(repo, "rev-parse", "--git-dir")
    code, head = run_git(repo, "rev-parse", "--verify", "-q", "HEAD", allow_fail=True)
    has_head = code == 0
    _, status = run_git(repo, "status", "--porcelain=v1", "-z")
    if has_head:
        _, diff = run_git(repo, "diff", "HEAD")
    else:
        _, unstaged = run_git(repo, "diff")
        _, staged = run_git(repo, "diff", "--cached")
        diff = unstaged + b"\0--cached--\0" + staged
    _, listing = run_git(repo, "ls-files", "--others", "--exclude-standard", "-z")
    top = run_git(repo, "rev-parse", "--show-toplevel")[1].decode("utf-8", "surrogateescape").strip()
    entries = []
    for rel in sorted(p for p in listing.decode("utf-8", "surrogateescape").split("\0") if p):
        try:
            entries.append("%s|%s" % (rel, stat_text(os.path.join(top, rel))))
        except OSError:
            entries.append("%s|gone" % rel)
    return {
        "head": sha(head if has_head else b"no-commit"),
        "status": sha(status),
        "diff": sha(diff),
        "untracked": sha("\n".join(entries).encode("utf-8", "surrogateescape")),
        "watch": hash_watch(watch_paths),
    }


def load_state(path):
    if not os.path.exists(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        if not isinstance(data.get("parts"), dict):
            raise ValueError("no parts")
        return {
            "fingerprint": str(data["fingerprint"]),
            "parts": data["parts"],
            "no_progress": int(data["no_progress"]),
            "ticks": int(data["ticks"]),
        }
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as exc:
        print("loop_progress: unreadable state %s (%s); starting over" % (path, exc), file=sys.stderr)
        return None


def write_state(path, state):
    directory = os.path.dirname(os.path.abspath(path))
    os.makedirs(directory, exist_ok=True)
    fd, tmp = tempfile.mkstemp(dir=directory, prefix=".loop_progress.", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as fh:
            json.dump(state, fh, indent=2)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise


def main(argv=None):
    ap = argparse.ArgumentParser(description="Count consecutive loop ticks with no file progress.")
    ap.add_argument("--state", required=True, help="JSON state file (kept between ticks)")
    ap.add_argument("--repo", default=".", help="git repository to fingerprint (default: cwd)")
    ap.add_argument("--watch-path", action="append", default=[],
                    help="out-of-repo file or directory the task edits (repeatable)")
    ap.add_argument("--limit", type=int, default=3, help="ticks without progress that trip the breaker")
    args = ap.parse_args(argv)

    try:
        parts = compute_parts(args.repo, args.watch_path)
    except GitError as exc:
        print("loop_progress: %s" % exc, file=sys.stderr)
        return 1

    fingerprint = sha("\n".join("%s=%s" % (k, parts[k]) for k in PARTS).encode("ascii"))
    prev = load_state(args.state)
    if prev is None:
        count, ticks, changed = 0, 1, "first tick"
    else:
        ticks = prev["ticks"] + 1
        if prev["fingerprint"] == fingerprint:
            count, changed = prev["no_progress"] + 1, "none"
        else:
            count = 0
            diff = [k for k in PARTS if prev["parts"].get(k) != parts[k]]
            changed = ",".join(diff) or "none"
    write_state(args.state, {"fingerprint": fingerprint, "parts": parts,
                             "no_progress": count, "ticks": ticks})
    print("loop_progress: tick %d, no progress %d of %d (changed: %s)"
          % (ticks, count, args.limit, changed))
    return 3 if count >= args.limit else 0


if __name__ == "__main__":
    sys.exit(main())
