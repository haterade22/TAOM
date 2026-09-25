#!/usr/bin/env python
"""Generate one CHANGELOG.md release section from the commits since the previous release tag.

`/release` runs this (AGENTS.md "Documentation duty"): the commit body is the changelog entry,
so nobody hand-edits CHANGELOG.md between releases. Pure stdlib.

Every commit subject carries the version label `<type>[(scope)][!]: vX.Y.Z - <description>`
(`.claude/hooks/check-commit-subject-version.sh` gates it). Labelled commits are grouped by type
in a fixed order; commits without the label are listed in their own last group so nothing is lost.

    python tools/changelog_from_commits.py --version v2.0.31            # print the section
    python tools/changelog_from_commits.py --version v2.0.31 --write    # insert it into CHANGELOG.md

Exit codes: 0 success; 2 refused (bad version, no commits in range, no previous tag, git
failure, or CHANGELOG.md already has this version's section or a hand-written section above
the release sections).
"""
import argparse
import datetime
import re
import subprocess
import sys
from collections import namedtuple
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# Same shape as the subject gate in .claude/hooks/check-commit-subject-version.sh, with groups.
LABEL_RE = re.compile(
    r"^(?P<type>[a-z][a-z0-9]*)(?:\((?P<scope>[^)]+)\))?!?: "
    r"(?P<version>v\d+\.\d+\.\d+(?:\.\d+)?) - (?P<description>\S.*)$"
)
VERSION_ARG_RE = re.compile(r"^v\d+\.\d+\.\d+$")
RELEASE_HEADING_RE = re.compile(r"^## v\d+\.\d+\.\d+ \(")

# Known types first, in this order; any other type follows alphabetically under its own name.
TYPE_ORDER = [
    ("feat", "Features"),
    ("fix", "Fixes"),
    ("balance", "Balance"),
    ("data", "Data"),
    ("perf", "Performance"),
    ("refactor", "Refactoring"),
    ("test", "Tests"),
    ("docs", "Documentation"),
    ("chore", "Chores"),
]
UNLABELLED_HEADING = "Commits without the version label"

LOG_FORMAT = "%H%x1f%s%x1f%b%x1e"

Commit = namedtuple("Commit", "sha subject body type scope version description")


def parse_log(raw):
    """Parse `git log --format=%H%x1f%s%x1f%b%x1e` output into Commits, in log order.

    A subject without the version label yields a Commit whose type, scope, version and
    description are None."""
    commits = []
    for record in raw.split("\x1e"):
        record = record.strip("\r\n")
        if not record.strip():
            continue
        sha, subject, body = (record.split("\x1f") + ["", ""])[:3]
        body = body.replace("\r\n", "\n").strip("\n").rstrip()
        m = LABEL_RE.match(subject)
        if m:
            commits.append(Commit(sha, subject, body, m.group("type"), m.group("scope"),
                                  m.group("version"), m.group("description")))
        else:
            commits.append(Commit(sha, subject, body, None, None, None, None))
    return commits


def _groups(commits):
    """(heading, commits) pairs: known types in TYPE_ORDER, other types alphabetically, then
    the unlabelled commits. Empty groups are omitted; log order is kept inside a group."""
    known = dict(TYPE_ORDER)
    groups = []
    for type_name, heading in TYPE_ORDER:
        members = [c for c in commits if c.type == type_name]
        if members:
            groups.append((heading, members))
    others = sorted({c.type for c in commits if c.type is not None and c.type not in known})
    for type_name in others:
        groups.append((type_name, [c for c in commits if c.type == type_name]))
    unlabelled = [c for c in commits if c.type is None]
    if unlabelled:
        groups.append((UNLABELLED_HEADING, unlabelled))
    return groups


def render_section(version, date, since, commits):
    """The Markdown section for one release, ending in exactly one newline."""
    labelled = sum(1 for c in commits if c.type is not None)
    lines = [
        f"## {version} ({date})",
        "",
        f"Commits since {since}: {len(commits)} ({labelled} with the version label, "
        f"{len(commits) - labelled} without).",
        "",
    ]
    for heading, members in _groups(commits):
        lines += [f"### {heading}", ""]
        for c in members:
            lines += [f"#### {c.subject}", "", f"`{c.sha[:8]}`", ""]
            if c.body:
                lines += [c.body, ""]
    return "\n".join(lines).rstrip("\n") + "\n"


def insert_section(changelog, section, version):
    """Insert `section` above the first `## ` heading of `changelog`, or append it after the
    header when there is none. Keeps the file's line endings (CRLF if it has any).

    Raises ValueError if a `## <version> ` heading already exists, or if the first `## `
    heading is not a generated release heading (`## vX.Y.Z (`): that is a hand-written
    section, and only /release writes this file."""
    newline = "\r\n" if "\r\n" in changelog else "\n"
    text = changelog.replace("\r\n", "\n")
    if re.search(r"^## " + re.escape(version) + r"(?: |$)", text, re.M):
        raise ValueError(f"CHANGELOG.md already has a section for {version}")
    m = re.search(r"^## .*$", text, re.M)
    if m and not RELEASE_HEADING_RE.match(m.group(0)):
        raise ValueError(f"CHANGELOG.md has a hand-written section above the releases: "
                         f"{m.group(0)!r}. Only /release writes this file; move those entries "
                         f"into commit bodies or the release note, then run again")
    if m:
        result = text[:m.start()] + section + "\n" + text[m.start():]
    else:
        result = text.rstrip("\n") + "\n\n" + section
    return result.replace("\n", newline)


def _git(repo, *args):
    proc = subprocess.run(["git", "-C", str(repo), *args], capture_output=True,
                          encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {proc.stderr.strip()}")
    return proc.stdout


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--version", required=True, help="the release being cut, e.g. v2.0.31")
    ap.add_argument("--since", help="previous release tag (default: git describe --tags "
                                    "--abbrev=0 --match 'v[0-9]*' <until>)")
    ap.add_argument("--until", default="HEAD", help="end of the range (default: HEAD)")
    ap.add_argument("--date", default=None, help="section date YYYY-MM-DD (default: today)")
    ap.add_argument("--write", action="store_true", help="insert into <repo>/CHANGELOG.md "
                                                         "instead of printing")
    ap.add_argument("--repo", default=str(REPO_ROOT), help="repository root (default: this "
                                                           "script's repository)")
    args = ap.parse_args(argv)

    if not VERSION_ARG_RE.match(args.version):
        sys.stderr.write(f"changelog_from_commits: --version must look like v2.0.31, got "
                         f"{args.version!r}\n")
        return 2
    date = args.date or datetime.date.today().isoformat()
    repo = Path(args.repo)
    try:
        since = args.since or _git(repo, "describe", "--tags", "--abbrev=0",
                                   "--match", "v[0-9]*", args.until).strip()
        raw = _git(repo, "log", "--no-merges", f"--format={LOG_FORMAT}",
                   f"{since}..{args.until}")
    except RuntimeError as exc:
        sys.stderr.write(f"changelog_from_commits: {exc}\n")
        return 2
    commits = parse_log(raw)
    if not commits:
        sys.stderr.write(f"changelog_from_commits: no commits in {since}..{args.until}\n")
        return 2
    section = render_section(args.version, date, since, commits)
    labelled = sum(1 for c in commits if c.type is not None)
    summary = (f"changelog_from_commits: {since}..{args.until}: {len(commits)} commits, "
               f"{labelled} with the version label, {len(commits) - labelled} without\n")

    if args.write:
        path = repo / "CHANGELOG.md"
        with open(path, encoding="utf-8-sig", newline="") as fh:
            current = fh.read()
        try:
            updated = insert_section(current, section, args.version)
        except ValueError as exc:
            sys.stderr.write(f"changelog_from_commits: {exc}\n")
            return 2
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(updated)
    else:
        sys.stdout.buffer.write(section.encode("utf-8"))
        sys.stdout.flush()
    sys.stderr.write(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())
