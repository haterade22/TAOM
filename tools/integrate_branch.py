#!/usr/bin/env python
"""Merge one branch into a linked integration worktree, resolving append-only files by union.

Usage: python tools/integrate_branch.py [--worktree P] --message-file F [--append-only GLOB ...]
                                        [--dry-run] <branch>

Refuses the main checkout, a merge already in progress, a dirty tree, a revision that is not a
commit, and a message file that is missing, empty or fails tools/check_public_text.py. Runs
`git merge --no-ff --no-commit <branch>` in diff3 conflict style, so a conflict block holds each
side's whole change and the base lines both started from (the default style trims lines common to
both sides out of the block). A conflict in the append-only set (globs whose `*` stays inside one
folder; default docs/reviews/lessons/*.md and docs/reviews/REVIEW-LOG.md) is resolved by union
only when both sides start with the block's base lines, that is when both only appended: ours,
then what theirs adds after the base, BOM and line endings kept. A side that edits a shared line
goes to hand resolution.

Then it checks the whole merge: no conflict marker on a line the merge adds, in any file, always;
in each append-only file the merge changes, a blank line before every `### ` heading (a `## `
entry in REVIEW-LOG.md) outside a code fence that the merge adds or puts after new text, inserted
where missing; no such heading duplicated by the merge. It stages the paths it resolved and prints
`ready to commit: git commit -F "<message file>"`. It never commits: the orchestrator runs that
command through Bash, so every commit gate judges the merge.

Exit 0: merged and staged, not committed. Exit 1: refused, or git failed before a merge started.
Exit 2: the merge is left in progress for a hand resolution (a conflict outside the append-only
set, a union it could not do, a duplicated heading or a leftover marker); every problem is listed.
Exit 3: an unexpected error; the worktree may hold a merge in progress (git status shows it). It
runs only non-destructive git verbs: it never aborts, resets, cleans, stashes or commits. No
recount. --dry-run reports the conflict set through `git merge-tree --write-tree`, touching
neither the index nor the working tree (exit 2 when a path would need a hand resolution). Pure
stdlib.
"""
from __future__ import annotations

import argparse
import fnmatch
import os
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import check_public_text  # noqa: E402

DEFAULT_APPEND_ONLY = ("docs/reviews/lessons/*.md", "docs/reviews/REVIEW-LOG.md")
START, BASE, MID, END = "<" * 7, "|" * 7, "=" * 7, ">" * 7
_HUNK_RE = re.compile(r"^@@ -\S+ \+(\d+)(?:,(\d+))? @@")
_FENCE_RE = re.compile(r"^ {0,3}(`{3,}|~{3,})")


class Stop(Exception):
    def __init__(self, message: str, code: int = 1):
        super().__init__(message)
        self.code = code


def git(wt: str, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", "-c", "core.quotePath=false", "-C", wt, *args],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")


def git_ok(wt: str, *args: str) -> str:
    proc = git(wt, *args)
    if proc.returncode:
        raise Stop(f"git {' '.join(args)} failed: {(proc.stderr or proc.stdout).strip()}")
    return proc.stdout


def _is_marker(line: str, mark: str) -> bool:
    return line == mark or line.startswith(mark + " ")


def _merge_block(ours: list, base: list, theirs: list) -> list:
    """Ours, then what theirs adds: each side must start with the base lines both began from."""
    shared = [line.rstrip("\r\n") for line in base]
    for side in (ours, theirs):
        if [line.rstrip("\r\n") for line in side[:len(base)]] != shared:
            raise ValueError("a side edits a line both branches share, so it is not an append")
    return ours + theirs[len(base):]


def union(data: bytes) -> tuple[bytes, int]:
    """Resolve every conflict block to ours, then what theirs appends. Returns (bytes, blocks)."""
    out, parts, state, blocks = [], {}, None, 0
    for line in data.decode("utf-8").splitlines(keepends=True):
        bare = line.rstrip("\r\n")
        if _is_marker(bare, START):
            if state is not None:
                raise ValueError("nested conflict marker")
            state, blocks, parts = "ours", blocks + 1, {"ours": [], "base": [], "theirs": []}
        elif _is_marker(bare, BASE):
            if state != "ours":
                raise ValueError("base marker outside a conflict")
            state = "base"
        elif bare == MID and state in ("ours", "base"):
            state = "theirs"
        elif _is_marker(bare, END):
            if state != "theirs":
                raise ValueError("end marker without a conflict")
            out += _merge_block(parts["ours"], parts["base"], parts["theirs"])
            state = None
        elif state is None:
            out.append(line)
        else:
            parts[state].append(line)
    if state is not None:
        raise ValueError("unterminated conflict")
    if not blocks:
        raise ValueError("no conflict markers")
    return "".join(out).encode("utf-8"), blocks


def append_only_matcher(globs):
    """A path test for the append-only globs, matched folder by folder: a `*` never crosses a /."""
    split = [g.split("/") for g in globs]

    def match(path: str) -> bool:
        parts = path.split("/")
        return any(len(g) == len(parts) and all(map(fnmatch.fnmatchcase, parts, g)) for g in split)
    return match


def heading_prefix(path: str) -> str:
    return "## " if path.rsplit("/", 1)[-1] == "REVIEW-LOG.md" else "### "


def _paths(text: str) -> list[str]:
    return list(dict.fromkeys(p for p in text.split("\0") if p))


def added_lines(wt: str, path: str) -> set[int]:
    """Line numbers of the staged file that are new against HEAD."""
    added = set()
    for line in git_ok(wt, "diff", "--cached", "--no-color", "--no-ext-diff", "-U0", "HEAD",
                       "--", path).splitlines():
        match = _HUNK_RE.match(line)
        if match:
            start = int(match.group(1))
            added.update(range(start, start + int(match.group(2) or "1")))
    return added


def fix_seams(wt: str, path: str) -> tuple[list[int], list[str]]:
    """Insert a blank line before each heading the merge added (or put after new text) that
    follows a non-blank line; a heading-shaped line inside a code fence is not a heading.
    Returns (line numbers given a blank line, headings the merge duplicated)."""
    full = os.path.join(wt, path)
    with open(full, "rb") as handle:
        lines = handle.read().decode("utf-8").splitlines(keepends=True)
    added, prefix = added_lines(wt, path), heading_prefix(path)
    heads, fence = {}, None
    for n, line in enumerate(lines, 1):
        opener = _FENCE_RE.match(line)
        if opener:
            mark = opener.group(1)[0]
            fence = mark if fence is None else (None if mark == fence else fence)
        elif fence is None and line.startswith(prefix):
            heads[n] = line.rstrip()
    counts = Counter(heads.values())
    duplicated = sorted({text for n, text in heads.items() if counts[text] > 1 and n in added})
    inserted = [n for n in heads if n > 1 and lines[n - 2].strip() and (n in added or n - 1 in added)]
    if inserted:
        out = []
        for n, line in enumerate(lines, 1):
            if n in inserted:
                out.append("\r\n" if line.endswith("\r\n") else "\n")
            out.append(line)
        with open(full, "wb") as handle:
            handle.write("".join(out).encode("utf-8"))
    return inserted, duplicated


def leftover_markers(wt: str) -> list[str]:
    """Conflict start or end markers on any line the merge adds, across the whole staged merge."""
    found, current, previous = [], "", ""
    diff = git_ok(wt, "diff", "--cached", "--no-color", "--no-ext-diff", "-U0", "HEAD")
    for line in diff.splitlines():
        if line.startswith("+++ ") and previous.startswith("--- "):
            current = line[6:] if line.startswith("+++ b/") else line[4:]
        elif line.startswith("+") and (_is_marker(line[1:], START) or _is_marker(line[1:], END)):
            found.append(f"{current}: leftover conflict marker: {line[1:][:60]}")
        previous = line
    return found


def dry_run(wt: str, branch: str, target: str, is_append) -> int:
    proc = git(wt, "merge-tree", "--write-tree", "--name-only", "--no-messages", "-z", "HEAD", target)
    if proc.returncode not in (0, 1):
        raise Stop(f"git merge-tree failed: {(proc.stderr or proc.stdout).strip()}")
    conflicted = _paths(proc.stdout)[1:]
    if not conflicted:
        print(f"dry run: {branch} merges into HEAD with no conflict")
        return 0
    hand = [p for p in conflicted if not is_append(p)]
    for path in conflicted:
        print(f"{'hand-resolve' if path in hand else 'union (append-only)'}: {path}")
    print(f"dry run: {len(conflicted)} conflicted, {len(hand)} would need a hand resolution")
    return 2 if hand else 0


def read_message(path: str) -> str:
    """The message text, refused when missing, empty or failing the public-text check."""
    try:
        with open(path, "rb") as handle:
            text = handle.read().decode("utf-8-sig")
    except OSError:
        text = ""
    if not text.strip():
        raise Stop(f"the message file {path} is missing or empty")
    findings = check_public_text.check_text(text)
    if findings:
        raise Stop(f"the message file fails tools/check_public_text.py:\n"
                   + "\n".join(f"{path}:{n}: {rule}: {raw}" for n, rule, raw in findings))
    return text


def integrate(args) -> int:
    wt = git_ok(args.worktree, "rev-parse", "--show-toplevel").strip()
    git_dir, common = git_ok(wt, "rev-parse", "--path-format=absolute", "--git-dir",
                             "--git-common-dir").splitlines()[:2]
    if os.path.normcase(os.path.realpath(git_dir)) == os.path.normcase(os.path.realpath(common)):
        raise Stop(f"refusing: {wt} is the main checkout; run in a linked integration worktree")
    if not git(wt, "rev-parse", "-q", "--verify", "MERGE_HEAD").returncode:
        raise Stop(f"refusing: a merge is already in progress in {wt}; conclude it first")
    dirty = git_ok(wt, "status", "--porcelain")
    if dirty:
        raise Stop(f"refusing: the working tree at {wt} is not clean:\n{dirty.rstrip()[:800]}")
    message = os.path.abspath(args.message_file)
    read_message(message)
    proc = git(wt, "rev-parse", "-q", "--verify", "--end-of-options", args.branch + "^{commit}")
    if proc.returncode:
        raise Stop(f"refusing: {args.branch} is not a commit")
    target = proc.stdout.strip()
    is_append = append_only_matcher(args.append_only or DEFAULT_APPEND_ONLY)

    if args.dry_run:
        return dry_run(wt, args.branch, target, is_append)

    proc = git(wt, "-c", "merge.conflictStyle=diff3", "merge", "--no-ff", "--no-commit",
               "--end-of-options", args.branch)
    merging = git(wt, "rev-parse", "-q", "--verify", "MERGE_HEAD").stdout.strip()
    if merging != target:
        raise Stop(f"git merge started no merge of {args.branch} (exit {proc.returncode}, "
                   f"MERGE_HEAD {merging or 'absent'}):\n{(proc.stdout + proc.stderr).strip()}")
    problems = []
    for path in _paths(git_ok(wt, "diff", "--name-only", "--diff-filter=U", "-z")):
        if not is_append(path):
            problems.append(f"{path}: conflict outside the append-only set")
            continue
        try:
            with open(os.path.join(wt, path), "rb") as handle:
                data, blocks = union(handle.read())
        except (OSError, ValueError) as exc:
            problems.append(f"{path}: not resolved by union ({exc})")
            continue
        with open(os.path.join(wt, path), "wb") as handle:
            handle.write(data)
        git_ok(wt, "add", "--", path)
        print(f"resolved by union: {path} ({blocks} block{'s' if blocks > 1 else ''})")
    unresolved = {p.split(": ", 1)[0] for p in problems}
    for path in _paths(git_ok(wt, "diff", "--cached", "--name-only", "-z", "HEAD")):
        if is_append(path) and path not in unresolved and os.path.isfile(os.path.join(wt, path)):
            inserted, duplicated = fix_seams(wt, path)
            if inserted:
                git_ok(wt, "add", "--", path)
                print(f"blank line inserted in {path} before line(s) {', '.join(map(str, inserted))}")
            problems += [f"{path}: heading duplicated by the merge: {h}" for h in duplicated]
    problems += leftover_markers(wt)
    command = f'git commit -F "{message.replace(os.sep, "/")}"'
    if problems:
        print("HAND-RESOLVE (the merge is left in progress):")
        for problem in problems:
            print(f"  {problem}")
        print(f"Resolve these, stage them, then: {command}")
        return 2
    print(f"ready to commit: {command}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--worktree", default=os.getcwd(), help="integration worktree (default: cwd)")
    parser.add_argument("--message-file", required=True, help="the merge commit message")
    parser.add_argument("--append-only", action="append", metavar="GLOB",
                        help="a path glob resolved by union; repeat for more (default: "
                        + " ".join(DEFAULT_APPEND_ONLY) + ")")
    parser.add_argument("--dry-run", action="store_true", help="report the conflict set only")
    parser.add_argument("branch")
    args = parser.parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="backslashreplace")
    try:
        return integrate(args)
    except Stop as exc:
        print(f"integrate_branch: {exc}", file=sys.stderr)
        return exc.code
    except Exception as exc:  # a distinct exit code and one line, never a traceback read as exit 1
        print(f"integrate_branch: unexpected error: {exc!r}; the worktree may hold a merge in "
              "progress (git status shows it)", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
