#!/usr/bin/env python3
"""List the closed issues this release's commits name that still owe an in-game check.

Why: a closed issue whose code shipped without anyone seeing it work in game keeps the
`triage-needs-ingame` label, but nothing tells the testers which of a release's changes those
are. `/release` runs this in Phase 1 (the count) and Phase 5 (the testers' checklist). It never
blocks a release: the exit code is 0 however many
checks are owed.

Usage:
    python tools/ingame_ledger.py [--since TAG] [--format text|md] [--count]
Defaults: --since is the latest `v[0-9]*` tag reachable from HEAD. An issue counts when a commit
message in <tag>..HEAD names it (#N); the other labelled issues are the backlog. A close date
would be wrong on the 1.4.x line: a ported 1.5.x fix closed months earlier is new here, and a
1.5.x-only issue closed yesterday is not on this line at all.

Exit codes: 0 success; 2 bad arguments or an unknown --since tag; 3 git or gh failed or is
missing (the message says what failed; this tool reports, it never installs or logs in).

Read-only: runs `git describe`, `git log` and `gh issue list`. gh infers the repository from
the current directory. A list of exactly 1000 rows may be truncated and warns on stderr.
"""
import argparse
import json
import re
import subprocess
import sys
from datetime import datetime, timezone

LABEL = "triage-needs-ingame"
LIMIT = 1000
HINT = "run gh auth status"


def run_command(args):
    """Return (returncode, stdout); 127 when the program is missing. Never raises."""
    try:
        r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8",
                           errors="replace")
    except OSError:
        return 127, ""
    return r.returncode, r.stdout


def parse_date(text):
    """Parse an ISO 8601 stamp (trailing Z allowed) into an aware datetime."""
    dt = datetime.fromisoformat(text.strip().replace("Z", "+00:00"))
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt


def escape_md(title):
    """Escape what would break a markdown link label: brackets and a leading #."""
    title = title.replace("[", "\\[").replace("]", "\\]")
    if title.startswith("#"):
        title = "\\" + title
    return title


class LedgerError(Exception):
    def __init__(self, message, code):
        super().__init__(message)
        self.code = code


def resolve_cutoff(runner, since):
    """Return (tag, cutoff datetime). Raises LedgerError (2 unknown tag, 3 git failure)."""
    explicit = since is not None
    tag = since
    if not explicit:
        rc, out = runner(["git", "describe", "--tags", "--abbrev=0", "--match", "v[0-9]*", "HEAD"])
        if rc != 0 or not out.strip():
            raise LedgerError("git describe found no v* tag" + (" (git missing)" if rc == 127 else "")
                              + f"; {HINT}", 3)
        tag = out.strip()
    rc, out = runner(["git", "log", "-1", "--format=%cI", tag])
    if rc == 127:
        raise LedgerError(f"git is missing; {HINT}", 3)
    if rc != 0 or not out.strip():
        if explicit:
            raise LedgerError(f"unknown tag: {tag}", 2)
        raise LedgerError(f"git log failed for tag {tag}; {HINT}", 3)
    try:
        return tag, parse_date(out)
    except ValueError:
        raise LedgerError(f"git log gave an unreadable date for {tag}; {HINT}", 3)


def referenced_issue_numbers(runner, tag):
    """Every issue number a commit message in <tag>..HEAD names. Raises LedgerError (3)."""
    rc, out = runner(["git", "log", "--format=%B", f"{tag}..HEAD"])
    if rc != 0:
        raise LedgerError(f"git log failed for {tag}..HEAD (exit {rc}); {HINT}", 3)
    return {int(n) for n in re.findall(r"#(\d+)\b", out)}


def fetch_issues(runner):
    rc, out = runner(["gh", "issue", "list", "--state", "closed", "--label", LABEL,
                      "--limit", str(LIMIT), "--json", "number,title,closedAt,url"])
    if rc != 0:
        raise LedgerError(f"gh issue list failed (exit {rc}); {HINT}", 3)
    try:
        rows = json.loads(out)
        for row in rows:
            int(row["number"])
    except (ValueError, TypeError, KeyError):
        raise LedgerError(f"gh returned unreadable output; {HINT}", 3)
    return rows


def render(tag, cutoff, new, older, fmt):
    lines = []
    if fmt == "md":
        lines.append("## Please check in game")
        if new:
            lines += [f"- [ ] [#{i['number']}]({i['url']}) {escape_md(i['title'])}" for i in new]
        else:
            lines.append(f"Nothing named since {tag} owes an in-game check.")
        lines.append(f"Other owed checks: {older} (label {LABEL}).")
    else:
        lines.append(f"in-game ledger: {len(new)} issues named by commits since {tag} "
                     f"({cutoff.date().isoformat()}) still owe an in-game check; "
                     f"{older} other labelled issues are in the backlog")
        lines += [f"#{i['number']} {i['title']}" for i in new]
    return "\n".join(lines) + "\n"


def main(argv=None, runner=run_command, out=None, err=None):
    out = out or sys.stdout
    err = err or sys.stderr
    ap = argparse.ArgumentParser(description="List closed issues named by commits since the last "
                                             f"release tag that still carry {LABEL}.")
    ap.add_argument("--since", metavar="TAG", help="release tag (default: latest v* tag from HEAD)")
    ap.add_argument("--format", choices=("text", "md"), default="text",
                    help="text (default) or a markdown testers' checklist")
    ap.add_argument("--count", action="store_true", help="print only the number of owed checks")
    args = ap.parse_args(argv)
    try:
        tag, cutoff = resolve_cutoff(runner, args.since)
        named = referenced_issue_numbers(runner, tag)
        rows = fetch_issues(runner)
    except LedgerError as e:
        print(f"ERROR: {e}", file=err)
        return e.code
    if len(rows) == LIMIT:
        print(f"WARNING: gh returned {LIMIT} rows; the list may be truncated", file=err)
    new = sorted((r for r in rows if r["number"] in named), key=lambda r: r["number"],
                 reverse=True)
    older = len(rows) - len(new)
    if args.count:
        out.write(f"{len(new)}\n")
    else:
        out.write(render(tag, cutoff, new, older, args.format))
    return 0


if __name__ == "__main__":
    sys.exit(main())
