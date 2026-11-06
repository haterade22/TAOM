#!/usr/bin/env python3
"""Classify every git worktree of a repo before cleanup (read-only).

Why: TAOM accumulates many worktrees (E:/repos/taom-*) and nothing tells a merged, clean one from
one holding unpushed work. This prints one row per worktree with a bucket: main, safe, review,
hold-wip (tracked changes) or hold-unmerged (HEAD not in the base branch).

Usage:
    python tools/worktree_audit.py [--repo PATH] [--base BRANCH] [--json]
Defaults: --repo is the current directory, --base is bannerlord-1.4.5.
Exit code 0 on success; 2 with a message on stderr when --repo is not a git repo, --base
does not exist or `git worktree list` fails.

A worktree that contains another worktree, or whose `git status` failed, is `review` with a
`note` saying why.

Read-only guarantee: only `git worktree list`, `rev-parse`, `merge-base`, `rev-list`,
`status --no-optional-locks` (it does not refresh the index) and `log` run. It never fetches, writes a file or deletes anything. The bucket is advice.
"""
import argparse
import json
import os
import subprocess
import sys

DEFAULT_BASE = "bannerlord-1.4.5"
FOOTER = "The bucket is advice, not permission. This tool removes nothing."


def run_git(cwd, *args):
    """Return (returncode, stdout) of a git command; never raises on a nonzero exit."""
    r = subprocess.run(["git", "-C", cwd, *args], capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    return r.returncode, r.stdout


def parse_porcelain(text):
    """Parse `git worktree list --porcelain` into a list of dicts, in order."""
    blocks = []
    cur = None
    for line in text.splitlines():
        if not line.strip():
            cur = None
            continue
        key, _, val = line.partition(" ")
        if key == "worktree":
            cur = {"path": val, "head": "", "branch": "(detached)", "bare": False,
                   "locked": False, "prunable": False}
            blocks.append(cur)
        elif cur is None:
            continue
        elif key == "HEAD":
            cur["head"] = val
        elif key == "branch":
            cur["branch"] = val[len("refs/heads/"):] if val.startswith("refs/heads/") else val
        elif key == "bare":
            cur["bare"] = True
        elif key == "locked":
            cur["locked"] = True
        elif key == "prunable":
            cur["prunable"] = True
    return blocks


def classify(is_main, missing, locked, dirty, merged, untracked, status_ok=True):
    if is_main:
        return "main"
    if missing or locked or not status_ok:
        return "review"
    if dirty > 0:
        return "hold-wip"
    if not merged:
        return "hold-unmerged"
    if untracked > 0:
        return "review"
    return "safe"


def audit(repo, base):
    """Return one dict per worktree. Raises ValueError if repo/base are unusable."""
    rc, _ = run_git(repo, "rev-parse", "--git-dir")
    if rc != 0:
        raise ValueError(f"not a git repository: {repo}")
    rc, _ = run_git(repo, "rev-parse", "--verify", "--quiet", base + "^{commit}")
    if rc != 0:
        raise ValueError(f"base branch not found: {base}")
    rc, out = run_git(repo, "worktree", "list", "--porcelain")
    if rc != 0:
        raise ValueError("git worktree list failed")
    rows = []
    for i, wt in enumerate(parse_porcelain(out)):
        sha = wt["head"]
        exists = os.path.isdir(wt["path"])
        merged = False
        ahead = behind = 0
        last = ""
        if sha:
            merged = run_git(repo, "merge-base", "--is-ancestor", sha, base)[0] == 0
            rc, cnt = run_git(repo, "rev-list", "--left-right", "--count", f"{base}...{sha}")
            parts = cnt.split()
            if rc == 0 and len(parts) == 2:
                behind, ahead = int(parts[0]), int(parts[1])
            last = run_git(repo, "log", "-1", "--format=%cs", sha)[1].strip()
        dirty = untracked = 0
        status_ok = True
        if exists:
            rc, st = run_git(wt["path"], "--no-optional-locks", "status", "--porcelain",
                             "--untracked-files=normal")
            status_ok = rc == 0
            if status_ok:
                for line in st.splitlines():
                    if line.startswith("??"):
                        untracked += 1
                    elif line.strip():
                        dirty += 1
        missing = (not exists) or wt["prunable"]
        rows.append({
            "path": wt["path"], "branch": wt["branch"], "head": sha[:8], "exists": exists,
            "merged": merged, "ahead": ahead, "behind": behind, "dirty": dirty,
            "untracked": untracked, "last_commit": last, "locked": wt["locked"],
            "bucket": classify(i == 0, missing, wt["locked"], dirty, merged, untracked, status_ok),
            "note": "" if status_ok else "git status failed",
        })
    mark_nested(rows)
    return rows


def _norm(path):
    return os.path.normcase(os.path.normpath(path))


def mark_nested(rows):
    """A worktree that contains another one is `review`: removing it deletes the inner one."""
    norms = [_norm(r["path"]) for r in rows]
    for i, row in enumerate(rows):
        if i == 0:
            continue
        prefix = norms[i].rstrip(os.sep) + os.sep
        inner = [os.path.basename(rows[j]["path"]) for j in range(len(rows))
                 if j != i and norms[j].startswith(prefix)]
        if inner:
            row["bucket"] = "review"
            note = "contains worktree " + ", ".join(inner)
            row["note"] = f"{row['note']}; {note}" if row["note"] else note


def format_table(rows):
    header = ("path", "branch", "bucket", "ahead/behind", "dirty", "untracked", "last commit", "note")
    body = [(r["path"], r["branch"], r["bucket"], f"{r['ahead']}/{r['behind']}",
             str(r["dirty"]), str(r["untracked"]), r["last_commit"] or "-", r["note"])
            for r in rows]
    widths = [max(len(x[c]) for x in [header] + body) for c in range(len(header))]
    lines = ["  ".join(x[c].ljust(widths[c]) for c in range(len(header))).rstrip()
             for x in [header] + body]
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Classify git worktrees (read-only).")
    ap.add_argument("--repo", default=".", help="repository path (default: current directory)")
    ap.add_argument("--base", default=DEFAULT_BASE, help=f"base branch (default: {DEFAULT_BASE})")
    ap.add_argument("--json", action="store_true", help="print a JSON list instead of a table")
    args = ap.parse_args(argv)
    try:
        rows = audit(os.path.abspath(args.repo), args.base)
    except ValueError as e:
        print(f"worktree_audit: {e}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(rows, indent=2))
    else:
        print(format_table(rows))
        print(FOOTER)
    return 0


if __name__ == "__main__":
    sys.exit(main())
