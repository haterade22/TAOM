#!/usr/bin/env python3
"""Audit Claude Code's machine-local memory for this checkout (read-only).

Reports:
  - MEMORY.md over ADR-011's budget (40 lines, 4 KB)
  - index links to files that do not exist
  - memory files that nothing links to (index, markdown link or [[name]])
  - In-Flight resume cards whose commits are all on trunk and whose issues are all closed:
    CLAUDE.md "Where new knowledge goes" item 8 says delete those

It never edits or deletes. Exit 1 when the index is over budget or has a broken link, else 0.

Usage: python tools/memory_audit.py [--memory-dir DIR] [--no-gh]
"""
import argparse
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

LIMIT_LINES = 40
LIMIT_BYTES = 4096
TRUNK = "origin/bannerlord-1.5.x"
JUDGED_SECTION = "In-Flight"

LINK_RE = re.compile(r"\[([^\]]+)\]\(([^)\s#]+)(?:#[^)]*)?\)")
WIKI_RE = re.compile(r"\[\[([^\]]+)\]\]")
COMMIT_RE = re.compile(r"(?<![\\/\w-])[0-9a-f]{7,40}\b")   # not a path segment such as a scratchpad id
UUID_RE = re.compile(r"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")
ISSUE_RE = re.compile(r"(?<![\w./])#(\d{3,5})\b")
NAME_RE = re.compile(r"^name:\s*(\S+)", re.MULTILINE)
UNPUSHED_RE = re.compile(r"not pushed|unpushed|uncommitted|not yet sent|not deployed|not trunk", re.IGNORECASE)


@dataclass
class Entry:
    name: str
    path: str
    line: str
    section: str


@dataclass
class Verdict:
    name: str
    verdict: str
    reasons: list = field(default_factory=list)


def parse_index(text):
    entries, section = [], ""
    for line in text.splitlines():
        if line.startswith("## "):
            section = line[3:].strip()
            continue
        if line.lstrip().startswith("- "):
            link = LINK_RE.search(line)
            if link:
                entries.append(Entry(link.group(1), link.group(2), line, section))
    return entries


def index_budget(text):
    lines = len(text.splitlines())
    size = len(text.encode("utf-8"))
    return lines, size, lines > LIMIT_LINES or size > LIMIT_BYTES


def extract_refs(text):
    commits = {c for c in COMMIT_RE.findall(text) if re.search(r"\d", c) and re.search(r"[a-f]", c)}
    issues = {int(n) for n in ISSUE_RE.findall(text)}
    return commits, issues


def card_body(text):
    """A card's text without session ids, whose hex groups read as commit hashes."""
    return UUID_RE.sub("", text)


def broken_links(memdir, entries):
    return [e.path for e in entries if not (memdir / e.path).is_file()]


def _rel(memdir, path):
    return path.resolve().relative_to(memdir.resolve()).as_posix()


def orphans(memdir):
    files = sorted(p for p in memdir.rglob("*.md") if p.name != "MEMORY.md")
    linked, wiki = set(), set()
    for p in [memdir / "MEMORY.md", *files]:
        if not p.is_file():
            continue
        text = p.read_text(encoding="utf-8", errors="replace")
        for _, target in LINK_RE.findall(text):
            t = (p.parent / target)
            if t.resolve() != p.resolve() and t.is_file():
                linked.add(_rel(memdir, t))
        wiki.update(w.strip() for w in WIKI_RE.findall(text))
    out = []
    for p in files:
        rel = _rel(memdir, p)
        name = NAME_RE.search(p.read_text(encoding="utf-8", errors="replace"))
        names = {p.stem, name.group(1)} if name else {p.stem}
        if rel not in linked and not names & wiki:
            out.append(rel)
    return out


def card_verdicts(memdir, entries, commit_state, issue_state):
    out = []
    for e in entries:
        if not e.section.startswith(JUDGED_SECTION):
            continue
        card = memdir / e.path
        text = e.line + "\n" + (card_body(card.read_text(encoding="utf-8", errors="replace")) if card.is_file() else "")
        commits, issues = extract_refs(text)
        reasons = []
        for c in sorted(commits):
            state = commit_state(c) or "unknown"
            if state != "trunk":
                reasons.append(f"{c} {state}")
        for n in sorted(issues):
            state = issue_state(n) or "unknown"
            if state != "closed":
                reasons.append(f"#{n} {state}")
        for phrase in sorted({p.lower() for p in UNPUSHED_RE.findall(text)}):
            reasons.append(f"says {phrase}")
        if not commits and not issues:
            reasons.append("no commit or issue named")
        out.append(Verdict(e.name, "keep" if reasons else "prune", reasons))
    return out


def find_memory_dir(home, checkout_name):
    projects = home / ".claude" / "projects"
    if not projects.is_dir():
        return None
    for d in sorted(projects.iterdir()):
        if d.name.lower().endswith("-" + checkout_name.lower()) and (d / "memory").is_dir():
            return d / "memory"
    return None


def _run(args, cwd=None):
    try:
        r = subprocess.run(args, cwd=cwd, capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.TimeoutExpired):
        return None
    return r


def git_commit_state(repo):
    def state(sha):
        r = _run(["git", "rev-parse", "--verify", "-q", sha + "^{commit}"], repo)
        if not r or r.returncode:
            return "missing"
        r = _run(["git", "merge-base", "--is-ancestor", sha, TRUNK], repo)
        if r and r.returncode == 0:
            return "trunk"
        r = _run(["git", "branch", "-r", "--contains", sha], repo)
        return "remote" if r and r.stdout.strip() else "local"
    return state


def gh_issue_state(repo):
    cache = {}

    def state(number):
        if number not in cache:
            r = _run(["gh", "api", f"repos/{{owner}}/{{repo}}/issues/{number}", "--jq", ".state"], repo)
            cache[number] = r.stdout.strip() if r and r.returncode == 0 else None
        return cache[number]
    return state


def main_checkout_name(repo):
    r = _run(["git", "rev-parse", "--path-format=absolute", "--git-common-dir"], repo)
    common = Path(r.stdout.strip()) if r and r.returncode == 0 else Path(repo).resolve() / ".git"
    return common.parent.name


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--memory-dir", type=Path)
    ap.add_argument("--repo", type=Path, default=Path.cwd())
    ap.add_argument("--no-gh", action="store_true", help="do not ask GitHub; every issue reads unknown")
    a = ap.parse_args(argv)

    memdir = a.memory_dir or find_memory_dir(Path.home(), main_checkout_name(a.repo))
    if not memdir or not (memdir / "MEMORY.md").is_file():
        print(f"memory folder not found ({memdir}); pass --memory-dir")
        return 2
    text = (memdir / "MEMORY.md").read_text(encoding="utf-8")
    entries = parse_index(text)
    lines, size, over = index_budget(text)
    broken = broken_links(memdir, entries)

    print(f"Memory: {memdir}")
    print(f"Index: {lines} lines, {size} bytes (budget {LIMIT_LINES} lines, {LIMIT_BYTES} bytes){' OVER' if over else ''}")
    for b in broken:
        print(f"BROKEN LINK: {b}")
    stray = orphans(memdir)
    print(f"Unlinked files: {len(stray)}" + ("".join(f"\n  {s}" for s in stray)))

    issue_state = (lambda n: None) if a.no_gh else gh_issue_state(a.repo)
    live = [e for e in entries if e.path not in broken]
    verdicts = card_verdicts(memdir, live, git_commit_state(a.repo), issue_state)
    print(f"In-Flight cards: {len(verdicts)}")
    for v in verdicts:
        why = "all commits on trunk, all issues closed" if v.verdict == "prune" else "; ".join(v.reasons[:6])
        if len(v.reasons) > 6:
            why += f"; and {len(v.reasons) - 6} more"
        print(f"  {v.verdict.upper():5} {v.name}: {why}")
    return 1 if over or broken else 0


if __name__ == "__main__":
    sys.exit(main())
