#!/usr/bin/env python3
"""The knowledge store: check it, ask it, and list the procedures of the repository (#1613, ADR-016).

A technique is a way of doing something well, with the reason it is better. Each one is a file,
docs/knowledge/techniques/<id>.toml, and the file stem is its id. An entry says how the technique acts today:

  sentence  only the entry says it (no [[acts]] row)
  instance  it was built once, in one place, and is no shared default; the entry says where
  report    a tool in the repository prints the number
  gate      a check in the repository fails without it
  code      a shared function in the repository builds it

report, gate and code name `path::text`: a file of the repository and a text on one line of it. When either is
gone the check fails, so an entry cannot go on claiming a default that was removed. An entry below a gate says in
`next` what would make it act. There is no date to bump: nothing here turns red because time passed.

Procedures are never stored. `procedures` reads the skills and the workflow docs on each run, so the list cannot
go stale, and `--orphans` names the ones that no index points to.

Why a tool and not a doc: the indexes of this repository that compare themselves with their source stayed true,
and the hand-kept ones that were measured drifted (ADR-016 "Context").

Usage (from any folder):
  python tools/knowledge.py check                     the store's findings; exit 0 clean, 1 findings, 2 not judged
  python tools/knowledge.py check --staged            the commit gate: judges the files the commit records, and only
                                                      when the commit can change the verdict (see `staged_matters`)
  python tools/knowledge.py for <path> [<path> ...]   the entries whose triggers match these files
  python tools/knowledge.py find <word> [<word> ...]  the entries, procedures and task-table rows with these words
  python tools/knowledge.py show <id>                 one entry
  python tools/knowledge.py brief --paths <p> ... --task <phrase> ...  the block for a subagent's spawn brief
  python tools/knowledge.py debt [--json]             counts by area and level; the entries that do not act yet
  python tools/knowledge.py procedures [--orphans] [--json]            the computed procedure catalogue
  python tools/knowledge.py index --out <file>        the lines .claude/hooks/point-knowledge.sh matches against

Globs (`when.paths`, `when.outside`) are the dialect of tools/data_gates.py: lower case, forward slashes, a leading
`*`, matched against the whole path, and `*` crosses folders. The hook matches the same globs in bash with
`[[ $path == $glob ]]`. A bracket class and `?` are refused: bash and Python read them differently (`?` is one byte
in bash and one character here).

`check` reads the working tree: every entry file on disk, and "a file of the repository" is one that git tracks or
would add. `check --staged` reads what the commit records and never the disk: the names and the text both come from
the git index. So nothing that only the work tree holds refuses a commit (a new entry file, another session's
unfinished edit of an entry or of a file an entry names), and a break that is staged is seen though the disk copy
was repaired. It then judges the last commit the same way and refuses only a finding that the last commit did not
have. A finding that reached trunk by another way is printed when the command is run by hand (the hook shows
nothing on exit 0), and it stays for the real-tree test to fail on. For a merge the last commit is the branch's
own, so a clean merge of a trunk that holds a finding is refused until the finding is repaired.

Tests: tools/tests/test_knowledge.py. Standard library only.
"""
from __future__ import annotations

import argparse
import collections
import datetime
import fnmatch
import json
import os
import re
import subprocess
import sys
import tomllib
import traceback
from dataclasses import dataclass, field
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
STORE_REL = "docs/knowledge"
TECH_REL = STORE_REL + "/techniques"
TEARDOWN_REL = STORE_REL + "/teardowns"
LESSONS_REL = "docs/reviews/lessons"
TOOL_REL = "tools/knowledge.py"

#: Areas beside the lesson categories (an area is a lessons file stem or one of these).
EXTRA_AREAS = ("ai-harness", "asset-craft", "coop-server", "release", "review-process", "scenes-map")
STATES = ("instance", "report", "gate", "code")
LEVELS = ("sentence",) + STATES
GRADES = ("measured", "inferred", "maintainer")
SOURCE_KINDS = ("lesson", "teardown", "adopt", "issue", "maintainer")

LINE_MAX = 220
TITLE_MAX = 100
BRIEF_MAX = 12
ID_RE = re.compile(r"[a-z0-9]+(-[a-z0-9]+)*")
ID_MAX = 60
GLOB_CHARS_RE = re.compile(r"[a-z0-9/_.*-]+")
REF_PATH_RE = re.compile(r"[A-Za-z0-9/_.-]+")
DATE_RE = re.compile(r"\d{4}-\d{2}-\d{2}")
DASHES = ("\u2014", "\u2013")

TOP_KEYS = {"title": str, "line": str, "why": str, "area": str, "status": str, "retired": str,
            "superseded_by": str, "ask": str, "see": str, "next": str, "when": dict, "evidence": list,
            "acts": list, "source": list}
TOP_REQUIRED = ("title", "line", "why", "area", "status")
WHEN_KEYS = ("paths", "outside", "tasks")
EVIDENCE_KEYS = ("grade", "value", "method", "date")
ACTS_KEYS = ("state", "ref", "where", "scope")
SOURCE_KEYS = ("lesson", "heading", "teardown", "adopt", "issue", "maintainer", "words")

#: The files that count as an index of procedures.
INDEX_FILES = ("docs/reference/doc-lookup.md", "docs/INDEX.md", "CLAUDE.md", "AGENTS.md",
               "docs/ai-includes/orientation.md", "docs/ai-includes/agent-operating-manual.md",
               "docs/ai-includes/codex-operating-guide.md")
#: Folders under docs/ whose files are records and history, never procedures.
DOC_SKIP = ("docs/reviews/", "docs/archive/", "docs/raw/", "docs/knowledge/", "docs/changelog-archive/",
            "docs/issues-drafts/", "docs/adrs/")
DOC_NAME_RE = re.compile(r"(workflow|-authoring|-process\.md$)")


@dataclass(frozen=True)
class Finding:
    path: str
    code: str
    message: str

    def __str__(self) -> str:
        return "%s: %s: %s" % (self.path, self.code, self.message)


@dataclass
class Entry:
    id: str
    path: str
    title: str = ""
    line: str = ""
    why: str = ""
    area: str = ""
    status: str = "active"
    retired: str = ""
    superseded_by: str = ""
    ask: str = ""
    see: str = ""
    next: str = ""
    paths: list = field(default_factory=list)
    outside: list = field(default_factory=list)
    tasks: list = field(default_factory=list)
    evidence: list = field(default_factory=list)
    acts: list = field(default_factory=list)       # the rows that passed their own shape check
    sources: list = field(default_factory=list)
    level: str = "sentence"

    @property
    def active(self) -> bool:
        return self.status == "active"

    @property
    def globs(self) -> list:
        return list(self.paths) + list(self.outside)


@dataclass
class Store:
    root: Path
    entries: dict = field(default_factory=dict)
    findings: list = field(default_factory=list)
    skipped: int = 0
    exists: bool = True

    @property
    def tech_dir(self) -> Path:
        return self.root / TECH_REL

    def active(self) -> list:
        return [e for e in self.entries.values() if e.active]


# --- reading -------------------------------------------------------------------------------------------------------

def _decode(raw: bytes) -> str:
    return raw.decode("utf-8-sig", errors="replace").replace("\r\n", "\n").replace("\r", "\n")


def read_text(path: Path) -> str:
    """A file's text with no BOM and with LF line ends, whatever the checkout wrote."""
    return _decode(path.read_bytes())


def _git(root: Path, *args) -> bytes | None:
    try:
        proc = subprocess.run(["git", "-C", str(root), "-c", "core.quotePath=false", *args],
                              capture_output=True, timeout=60)
    except (OSError, subprocess.SubprocessError):
        return None
    return proc.stdout if proc.returncode == 0 else None


def _git_names(root: Path, *args) -> list | None:
    out = _git(root, *args)
    return None if out is None else [n for n in out.decode("utf-8", errors="replace").split("\0") if n]


def tracked_files(root: Path) -> list | None:
    """The files of the working tree that git knows or would add (tracked and untracked, not ignored), without the
    ones deleted on disk. None when git cannot say: then the check reports itself unchecked."""
    present = _git_names(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
    deleted = _git_names(root, "ls-files", "-z", "--deleted")
    if present is None or deleted is None:
        return None
    gone = set(deleted)
    return sorted(set(n for n in present if n not in gone))


def index_files(root: Path) -> list | None:
    """The files the next commit records (the git index). None when git cannot say."""
    names = _git_names(root, "ls-files", "-z", "--cached")
    return None if names is None else sorted(set(names))


def head_files(root: Path) -> list | None:
    """The files of the last commit. None when there is no commit yet, or when git cannot say."""
    names = _git_names(root, "ls-tree", "-r", "-z", "--name-only", "HEAD")
    return None if names is None else sorted(set(names))


def commit_files(head: list | None, changes: list) -> list:
    """The files the next commit records: the last commit's files with what the commit stages. The index itself
    (`git ls-files --cached`) also lists an intent-to-add entry (`git add -N`), which no commit records."""
    names = set(head or ())
    for status, path in changes:
        if status == "D":
            names.discard(path)
        else:
            names.add(path)
    return sorted(names)


def staged_changes(root: Path) -> list | None:
    """(status letter, path) for each file the commit changes against HEAD. A rename shows as a deletion and an
    addition. None when git cannot say."""
    parts = _git_names(root, "diff", "--cached", "--name-status", "--no-renames", "-z")
    if parts is None:
        return None
    return [(parts[i][:1], parts[i + 1]) for i in range(0, len(parts) - 1, 2)]


def _git_blobs(root: Path, specs: list) -> dict:
    """The contents of several git objects from one process (`git cat-file --batch`): {spec: bytes}. A spec is
    `<rev>:<path>`, and an empty rev is the index. A spec that git does not hold is left out."""
    if not specs:
        return {}
    try:
        proc = subprocess.run(["git", "-C", str(root), "cat-file", "--batch"],
                              input=("\n".join(specs) + "\n").encode("utf-8"), capture_output=True, timeout=60)
    except (OSError, subprocess.SubprocessError):
        return {}
    if proc.returncode != 0:
        return {}
    out, pos, blobs = proc.stdout, 0, {}
    for spec in specs:
        end = out.find(b"\n", pos)
        if end < 0:
            break
        head = out[pos:end].split(b" ")
        pos = end + 1
        if len(head) == 3 and head[2].isdigit():    # "<sha> <type> <size>"; what git lacks prints "<spec> missing"
            size = int(head[2])
            if head[1] == b"blob":
                blobs[spec] = out[pos:pos + size]
            pos += size + 1
    return blobs


@dataclass
class Tree:
    """What the check reads. In the working tree (`commit` false): the entry files on disk, and a file counts when
    git lists it and it is on disk. For a commit (`commit` true): only the files that one tree of git holds, the
    index (`rev` "") or the last commit (`rev` "HEAD"), and their text from git too. The disk is never read there:
    in a shared checkout it holds other sessions' unfinished work, which is no part of the commit. `names` is None
    when git could not say; then only the disk decides."""
    root: Path
    names: set | None = None
    commit: bool = False
    rev: str = ""
    _blobs: dict = field(default_factory=dict, repr=False, compare=False)

    def has(self, rel: str) -> bool:
        if self.names is None:
            return (self.root / rel).is_file()
        if self.commit:
            return rel in self.names
        return rel in self.names and (self.root / rel).is_file()

    def fetch(self, rels) -> None:
        """Read these files of a commit tree with one git process. A text() that was not fetched costs one each."""
        if not self.commit:
            return
        want = [r for r in rels if r not in self._blobs and (self.names is None or r in self.names)
                and "\n" not in r and "\r" not in r]
        got = _git_blobs(self.root, [self.rev + ":" + r for r in want])
        for r in want:
            self._blobs[r] = got.get(self.rev + ":" + r)

    def text(self, rel: str) -> str:
        if self.commit:
            if rel not in self._blobs:
                self.fetch([rel])
            blob = self._blobs.get(rel)
            if blob is None:
                raise OSError("%s is not readable in %s" % (rel, "the last commit" if self.rev else "the git index"))
            return _decode(blob)
        path = self.root / rel
        if path.is_file():
            return read_text(path)
        raise OSError("%s is not a readable file" % rel)

    def listed(self, folder_rel: str, suffix: str) -> list:
        """The files directly in a folder, as repository paths: the commit's, or the ones on disk."""
        if self.commit and self.names is not None:
            prefix = folder_rel + "/"
            return sorted(n for n in self.names
                          if n.startswith(prefix) and n.endswith(suffix) and "/" not in n[len(prefix):])
        folder = self.root / folder_rel
        return sorted("%s/%s" % (folder_rel, p.name) for p in folder.glob("*" + suffix)) if folder.is_dir() else []


def norm_path(path: str, cwd: Path | None = None) -> str:
    """The form every glob is matched against: absolute, forward slashes, lower case. A relative path is read from
    cwd. (The hook also spells a Git Bash /c/ drive as c:/. No glob can tell the two apart: a glob starts with `*`
    and holds no colon.)"""
    p = str(path).replace("\\", "/")
    if not (re.match(r"^[A-Za-z]:/", p) or p.startswith("/")):
        base = str(cwd if cwd is not None else Path.cwd()).replace("\\", "/").rstrip("/")
        p = base + "/" + p
    return p.rstrip("/").lower()


def glob_problem(glob) -> str | None:
    """Why this glob is refused, or None. The rules keep bash and Python reading it the same way and keep a trigger
    from firing on every file of one kind."""
    if not isinstance(glob, str) or not glob:
        return "a glob is a non-empty string"
    if not GLOB_CHARS_RE.fullmatch(glob):
        return "only a-z 0-9 / _ . * - are allowed (lower case; no bracket class, no ?, no space, no line break)"
    if not glob.startswith("*"):
        return "a glob starts with * (it is matched against the whole path)"
    if "**" in glob:
        return "write one * (it crosses folders here)"
    body = re.sub(r"\.[a-z0-9]+$", "", glob)
    if len(re.findall(r"[a-z0-9]", body)) < 3:
        return "too broad: name a folder or a part of the file name, not only an extension"
    return None


def glob_matches(glob: str, path: str, cwd: Path | None = None) -> bool:
    return fnmatch.fnmatchcase(norm_path(path, cwd), glob)


def areas(tree: Tree) -> set:
    """The areas an entry can name: the lesson file stems of the tree that is judged, and the extra ones."""
    stems = {rel.rsplit("/", 1)[1][:-len(".md")] for rel in tree.listed(LESSONS_REL, ".md")}
    return stems | set(EXTRA_AREAS)


def _strings(value, key=""):
    """Every string in a parsed entry, with the key that holds it."""
    if isinstance(value, str):
        yield key, value
    elif isinstance(value, dict):
        for k, v in value.items():
            yield from _strings(v, k)
    elif isinstance(value, list):
        for v in value:
            yield from _strings(v, key)


def _is_str_list(value) -> bool:
    return isinstance(value, list) and all(isinstance(v, str) and v for v in value)


def _words(text: str) -> list:
    return re.findall(r"[a-z0-9]+", text.lower())


def parse_entry(stem: str, rel: str, text: str, known_areas: set) -> tuple:
    """(entry or None, findings). None means the file cannot be served at all: it does not parse, it has no id, or a
    field the hook prints is missing."""
    out: list = []

    def bad(code, message):
        out.append(Finding(rel, code, message))

    if not ID_RE.fullmatch(stem) or len(stem) > ID_MAX:
        bad("id-invalid", "the file stem is the id: lower-case words joined by single hyphens, at most %d characters" % ID_MAX)
        return None, out
    try:
        data = tomllib.loads(text)
    except tomllib.TOMLDecodeError as e:
        bad("toml-invalid", str(e))
        return None, out

    fatal = False
    for key, value in data.items():
        if key not in TOP_KEYS:
            bad("key-unknown", "`%s` is no key of an entry (a mistyped key would switch its field off)" % key)
        elif not isinstance(value, TOP_KEYS[key]):
            bad("value-invalid", "`%s` has the wrong type" % key)
            fatal = fatal or key in TOP_REQUIRED
    for key in TOP_REQUIRED:
        if key not in data or (isinstance(data.get(key), str) and not data[key].strip()):
            bad("key-missing", "`%s` is required" % key)
            fatal = True
    if fatal:
        return None, out

    def text_of(key):
        v = data.get(key, "")
        return v if isinstance(v, str) else ""

    e = Entry(id=stem, path=rel, title=text_of("title"), line=text_of("line"), why=text_of("why"),
              area=text_of("area"), status=text_of("status"), retired=text_of("retired"),
              superseded_by=text_of("superseded_by"), ask=text_of("ask"), see=text_of("see"), next=text_of("next"))

    if e.area not in known_areas:
        bad("value-invalid", "`area` is the stem of a file in %s (was that file renamed or removed?) or one of: %s"
            % (LESSONS_REL, ", ".join(EXTRA_AREAS)))
    if e.status not in ("active", "retired"):
        bad("value-invalid", "`status` is active or retired")

    for key, value in _strings(data):
        if any(d in value for d in DASHES):
            bad("text-dash", "an em or en dash in `%s`: use a comma, a colon or a new sentence" % key)
            break
    for key, limit in (("title", TITLE_MAX), ("line", LINE_MAX), ("ask", LINE_MAX)):
        value = text_of(key)
        if any(ord(c) < 32 for c in value):
            bad("text-control", "`%s` is one line: no tab, no line break" % key)
        elif len(value) > limit:
            bad("line-too-long", "`%s` has %d characters, the limit is %d" % (key, len(value), limit))

    when = data.get("when", {}) if isinstance(data.get("when", {}), dict) else {}
    for key, value in when.items():
        if key not in WHEN_KEYS:
            bad("key-unknown", "`when.%s` is no key ([when] holds paths, outside, tasks)" % key)
        elif not _is_str_list(value):
            bad("value-invalid", "`when.%s` is a list of strings" % key)
        else:
            setattr(e, key, list(value))
    for glob in e.globs:
        problem = glob_problem(glob)
        if problem:
            bad("glob-invalid", "`%s`: %s" % (glob.strip(), problem))

    rows = data.get("evidence", []) if isinstance(data.get("evidence", []), list) else []
    if not rows:
        bad("evidence-missing", "an entry needs at least one [[evidence]] row")
    for row in rows:
        if not isinstance(row, dict):
            bad("evidence-invalid", "an [[evidence]] row is a table")
            continue
        for key in row:
            if key not in EVIDENCE_KEYS:
                bad("key-unknown", "`evidence.%s` is no key ([[evidence]] holds grade, value, method, date)" % key)
        problem = None
        if row.get("grade") not in GRADES:
            problem = "`grade` is one of: %s" % ", ".join(GRADES)
        elif not isinstance(row.get("value"), str) or not row["value"].strip():
            problem = "`value` says what was measured, inferred or said"
        elif row["grade"] == "measured" and not (isinstance(row.get("method"), str) and row["method"].strip()):
            problem = "a measured value names its `method` (the script or command)"
        elif not isinstance(row.get("date"), datetime.date):
            problem = "`date` is a TOML date (2026-11-26, no quotes)"
        if problem:
            bad("evidence-invalid", problem)
        else:
            e.evidence.append(row)

    rows = data.get("acts", []) if isinstance(data.get("acts", []), list) else []
    for row in rows:
        if not isinstance(row, dict):
            bad("acts-invalid", "an [[acts]] row is a table")
            continue
        for key in row:
            if key not in ACTS_KEYS:
                bad("key-unknown", "`acts.%s` is no key ([[acts]] holds state, ref, where, scope)" % key)
        state = row.get("state")
        if state not in STATES:
            bad("acts-invalid", "`state` is one of: %s (a sentence is an entry with no [[acts]] row)" % ", ".join(STATES))
        elif state == "instance":
            if isinstance(row.get("where"), str) and row["where"].strip():
                e.acts.append(row)
            else:
                bad("acts-invalid", "an instance says `where` the one build lives")
        elif not isinstance(row.get("ref"), str) or not row["ref"].strip():
            bad("acts-invalid", "a %s names its `ref` as path::text" % state)
        else:
            path, sep, needle = row["ref"].partition("::")
            if not sep or not needle.strip() or any(ord(c) < 32 for c in row["ref"]):
                bad("ref-invalid", "`%s` is not path::text (a file of the repository, two colons, a text on one "
                                   "line of it)" % row["ref"].strip())
            elif not REF_PATH_RE.fullmatch(path):
                bad("ref-invalid", "the path of `%s` holds only letters, digits and / _ . -" % row["ref"].strip())
            else:
                e.acts.append(dict(row, _path=path, _needle=needle))
    e.level = max((r["state"] for r in e.acts), key=LEVELS.index, default="sentence")

    rows = data.get("source", []) if isinstance(data.get("source", []), list) else []
    if not rows:
        bad("source-missing", "an entry needs at least one [[source]] row")
    for row in rows:
        if not isinstance(row, dict):
            bad("source-invalid", "a [[source]] row is a table")
            continue
        for key in row:
            if key not in SOURCE_KEYS:
                bad("key-unknown", "`source.%s` is no key" % key)
        e.sources.append(row)

    if e.status == "retired":
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}: \S.*", e.retired):
            bad("retired-invalid", "a retired entry says `retired = \"<date>: <reason>\"`")
    else:
        if e.retired or e.superseded_by:
            bad("retired-invalid", "`retired` and `superseded_by` belong to a retired entry")
        if not e.globs and not e.tasks:
            bad("trigger-none", "an active entry needs a path glob or a task phrase, or nothing ever shows it")
        if LEVELS.index(e.level) < LEVELS.index("gate") and not e.next.strip():
            bad("next-missing", "this entry is at `%s`: say in `next` what would make it act" % e.level)
    return e, out


def load(root: Path, tree: Tree | None = None) -> Store:
    """Every entry of the store under root, with the findings that need no other file. With no tree: the entry
    files on disk."""
    root = Path(root)
    tree = tree if tree is not None else Tree(root)
    store = Store(root=root)
    names = tree.listed(TECH_REL, ".toml")
    if not names and (tree.commit or not store.tech_dir.is_dir()):     # git holds no empty folder
        store.exists = False
        return store
    tree.fetch(names)
    known = areas(tree)
    for rel in names:
        stem = rel.rsplit("/", 1)[1][:-len(".toml")]
        try:
            text = tree.text(rel)
        except OSError as e:
            store.findings.append(Finding(rel, "toml-invalid", "cannot be read: %s" % e))
            store.skipped += 1
            continue
        entry, findings = parse_entry(stem, rel, text, known)
        store.findings.extend(findings)
        if entry is None:
            store.skipped += 1
        else:
            store.entries[entry.id] = entry
    return store


# --- the check -----------------------------------------------------------------------------------------------------

def _md_heading_in(text: str, title: str) -> bool:
    return any(re.match(r"^#{1,6}\s+", ln) and re.sub(r"^#{1,6}\s+", "", ln).strip() == title.strip()
               for ln in text.split("\n"))


def check(root: Path, tracked: list | None = None, staged: bool = False, rev: str = "") -> list:
    """Every finding of the store under root, sorted by code and path. `tracked` is the file list to judge against
    (pass it to judge a tree that is no checkout). `staged` judges a tree of git and never the disk: what the next
    commit records, or with `rev` "HEAD" what the last commit recorded."""
    root = Path(root)
    if tracked is None:
        tracked = (head_files(root) if rev else index_files(root)) if staged else tracked_files(root)
    unchecked = tracked is None
    tree = Tree(root, None if unchecked else set(tracked), commit=staged and not unchecked, rev=rev)
    store = load(root, tree)
    if not store.exists:
        return [Finding(TECH_REL, "store-missing", "the folder does not exist, so nothing was checked")]
    findings = list(store.findings)
    if not store.entries and not store.skipped:
        findings.append(Finding(TECH_REL, "store-empty", "no entry file, so nothing was checked"))
    if unchecked:
        findings.append(Finding(TECH_REL, "unchecked-tracked",
                                "git could not list the files, so triggers and reference paths were NOT checked"))
    lowered = ["/" + n.lower() for n in (tracked or ())]
    teardowns = tree.listed(TEARDOWN_REL, ".md")
    teardown_stems = {rel.rsplit("/", 1)[1][:-len(".md")] for rel in teardowns}
    tree.fetch(sorted(named_files(store) | set(teardowns)))

    cited = set()
    for e in store.entries.values():
        for row in e.sources:               # a retired entry still closes the teardown it came from
            if isinstance(row.get("teardown"), str) and row["teardown"] in teardown_stems:
                cited.add(row["teardown"])

    for e in store.entries.values():
        def bad(code, message, e=e):
            findings.append(Finding(e.path, code, message))

        if e.status == "retired":           # history: what it named may be gone
            target = store.entries.get(e.superseded_by) if e.superseded_by else None
            if e.superseded_by and (target is None or not target.active):
                bad("retired-invalid", "`superseded_by` names `%s`, which is no active entry" % e.superseded_by)
            continue

        for row in e.sources:
            kinds = [k for k in SOURCE_KINDS if k in row]
            if len(kinds) != 1:
                bad("source-invalid", "a [[source]] row names one of: %s" % ", ".join(SOURCE_KINDS))
            elif kinds[0] == "lesson":
                lesson, heading = row.get("lesson"), row.get("heading")
                if not isinstance(lesson, str) or not tree.has(lesson):
                    bad("source-invalid", "the lesson file `%s` is not in the repository" % lesson)
                elif not isinstance(heading, str) or not heading.startswith("### ") or \
                        heading not in tree.text(lesson).split("\n"):
                    bad("source-invalid", "`heading` is not a whole `### ` line of %s" % lesson)
            elif kinds[0] == "teardown":
                if not isinstance(row["teardown"], str) or row["teardown"] not in teardown_stems:
                    bad("source-invalid", "no teardown record %s/%s.md" % (TEARDOWN_REL, row["teardown"]))
            elif kinds[0] == "adopt":
                adopt = row["adopt"]
                if not isinstance(adopt, str) or not re.fullmatch(r"docs/reviews/adopt-[^/]+\.md", adopt) or not tree.has(adopt):
                    bad("source-invalid", "`adopt` names a docs/reviews/adopt-*.md record that exists")
            elif kinds[0] == "issue":
                if not isinstance(row["issue"], int) or isinstance(row["issue"], bool) or row["issue"] < 1:
                    bad("source-invalid", "`issue` is the issue's number")
            elif kinds[0] == "maintainer":
                if not isinstance(row["maintainer"], str) or not DATE_RE.fullmatch(row["maintainer"]) or \
                        not (isinstance(row.get("words"), str) and row["words"].strip()):
                    bad("source-invalid", "the maintainer's `words` come with their date (maintainer = \"2026-11-25\")")

        if e.see:
            see_path, _, see_heading = e.see.partition("#")
            if not tree.has(see_path):
                bad("see-missing", "`see` names `%s`, which is not in the repository" % see_path)
            elif see_heading and not _md_heading_in(tree.text(see_path), see_heading):
                bad("see-missing", "`%s` has no heading `%s`" % (see_path, see_heading))

        for row in e.acts:
            if row["state"] == "instance":
                continue
            if not tree.has(row["_path"]):
                bad("ref-missing", "`%s` is not in the repository, so the entry cannot be at `%s`: restore it, "
                                   "or lower the entry and say what would make it act" % (row["_path"], row["state"]))
            elif row["_needle"] not in tree.text(row["_path"]):
                bad("ref-text-missing", "`%s` no longer holds the text `%s`" % (row["_path"], row["_needle"]))
        if not unchecked:
            for glob in e.paths:
                if glob_problem(glob) is None and not any(fnmatch.fnmatchcase(n, glob) for n in lowered):
                    bad("trigger-dead", "`%s` matches no file of the repository (a path that lies outside it goes "
                                        "in `when.outside`)" % glob)

    for rel in teardowns:
        stem = rel.rsplit("/", 1)[1][:-len(".md")]
        if stem not in cited and not any(ln.startswith("No technique:") for ln in tree.text(rel).split("\n")):
            findings.append(Finding(rel, "teardown-uncited",
                                    "no entry names this teardown as its source: write the entries, or add a "
                                    "line `No technique: <reason>`"))
    return sorted(findings, key=lambda f: (f.code, f.path, f.message))


def exit_code(findings: list) -> int:
    if any(not f.code.startswith("unchecked") for f in findings):
        return 1
    return 2 if findings else 0


def named_files(store: Store) -> set:
    """Every file of the repository that an entry names: what makes it act, its lesson file, its adoption record and
    its `see` target. A change of one of them can change the store's verdict."""
    out = set()
    for e in store.entries.values():
        out.update(r["_path"] for r in e.acts if r["state"] != "instance")
        for row in e.sources:
            out.update(row[k] for k in ("lesson", "adopt") if isinstance(row.get(k), str))
        if e.see:
            out.add(e.see.partition("#")[0])
    return out


def staged_matters(store: Store, changes: list) -> bool:
    """Whether a commit can change the store's verdict: it stages a file of the store, the tool, a file an entry
    names, or a deletion (a deleted file can be the last one a trigger matched)."""
    names = {path for _, path in changes}
    if any(n.startswith(STORE_REL + "/") or n == TOOL_REL for n in names):
        return True
    if any(status == "D" for status, _ in changes):
        return True
    return bool(names & named_files(store))


# --- queries -------------------------------------------------------------------------------------------------------

def _rank(e: Entry) -> tuple:
    return (LEVELS.index(e.level), e.id)


def matching(store: Store, path: str, cwd: Path | None = None, include_retired: bool = False) -> list:
    """The entries whose globs match this file. The ones that do not act yet come first, because a gate fails
    without the worker's help."""
    target = norm_path(path, cwd)
    hits = [e for e in store.entries.values() if (include_retired or e.active)
            and any(fnmatch.fnmatchcase(target, g) for g in e.globs)]
    return sorted(hits, key=_rank)


def enforced_by(store: Store, path: str, cwd: Path | None = None) -> list:
    """The active entries that name this file as what makes them act (the `ref` of a report, gate or code row), in
    any checkout. The hook's watch note gives the same answer at an Edit or a Write."""
    target = norm_path(path, cwd)
    hits = [e for e in store.active()
            if any(r["state"] != "instance" and target.endswith("/" + r["_path"].lower()) for r in e.acts)]
    return sorted(hits, key=_rank)


def find(store: Store, words: list) -> list:
    """The active entries that hold the words as whole words, most words first."""
    wanted = set(w for word in words for w in _words(word))
    scored = []
    for e in store.active():
        have = set(_words(" ".join([e.id, e.title, e.line, e.why, e.area, " ".join(e.tasks)])))
        score = len(wanted & have)
        if score:
            scored.append((-score, _rank(e), e))
    return [e for _, _, e in sorted(scored, key=lambda t: (t[0], t[1]))]


def brief_entries(store: Store, paths: list, tasks: list, cwd: Path | None = None) -> list:
    """The entries for a spawn brief: the ones whose globs match a path, and the ones with a task phrase whose every
    word is among the words given. A single common word of a longer phrase does not match it. An entry that matches
    a path and a task phrase comes first, then the ones a task phrase alone brings, then the ones a path alone
    brings, so the cap of the brief cuts the entries the lead said least about."""
    by_path, by_task = {}, {}
    for p in paths:
        for e in matching(store, p, cwd):
            by_path[e.id] = e
    given = set(w for t in tasks for w in _words(t))
    for e in store.active():
        if any(words and set(words) <= given for words in (_words(phrase) for phrase in e.tasks)):
            by_task[e.id] = e

    def order(e: Entry) -> tuple:
        return (0 if e.id in by_path and e.id in by_task else 1 if e.id in by_task else 2, _rank(e))

    return sorted({**by_path, **by_task}.values(), key=order)


def _store_label(store: Store) -> str:
    return str(store.tech_dir).replace("\\", "/")


def _printed(e: Entry) -> str:
    return e.line + ((" (see %s)" % e.see) if e.see else "")


def _entry_line(e: Entry) -> str:
    return "- %s [%s]: %s" % (e.id, e.level, _printed(e))


def brief_text(store: Store, paths: list, tasks: list, cwd: Path | None = None) -> str:
    """The block a lead pastes into a subagent's spawn brief. A miss says what was searched."""
    hits = brief_entries(store, paths, tasks, cwd)
    total = len(store.active())
    if not hits:
        return "No entry matched (%d entries scanned in %s)." % (total, _store_label(store))
    lines = ["Knowledge entries for this work (detail: python tools/knowledge.py show <id>). The bracket says how "
             "each acts today. Read each before the first edit, and say in your report what you did about it:"]
    lines += [_entry_line(e) for e in hits[:BRIEF_MAX]]
    if len(hits) > BRIEF_MAX:       # the cut is named, never silent
        lines.append("(%d of %d matches shown; not shown: %s. Detail: python tools/knowledge.py show <id>.)" % (
            BRIEF_MAX, len(hits), ", ".join(e.id for e in hits[BRIEF_MAX:])))
    lines.append("(%d of %d entries matched; store: %s)" % (len(hits), total, _store_label(store)))
    return "\n".join(lines)


def debt(store: Store) -> dict:
    """Counts by level and by area, and the active entries below a gate with what would make each act. Computed on
    every run and stored nowhere."""
    active = store.active()
    by_level = {lv: sum(1 for e in active if e.level == lv) for lv in LEVELS}
    by_area: dict = {}
    for e in active:
        by_area.setdefault(e.area, {lv: 0 for lv in LEVELS})[e.level] += 1
    owed = [{"id": e.id, "area": e.area, "level": e.level, "next": e.next}
            for e in sorted(active, key=_rank) if LEVELS.index(e.level) < LEVELS.index("gate")]
    return {"total": len(active), "retired": len(store.entries) - len(active), "by_level": by_level,
            "by_area": {a: by_area[a] for a in sorted(by_area)}, "owed": owed}


def index_text(store: Store) -> str:
    """The hook's index: one `t` line per active entry and glob (kind, id, level, glob, line, ask) and one `w` line
    per file an entry names as what makes it act. Tab-separated. The hook builds this without running the check,
    so a glob the check refuses gets no row and no field keeps a tab or a line break."""
    def clean(s: str) -> str:
        return re.sub(r"[\x00-\x1f]+", " ", s).strip()

    t_rows, w_rows = [], []
    for e in sorted(store.active(), key=_rank):
        for glob in sorted(set(g for g in e.globs if glob_problem(g) is None)):
            t_rows.append(("t", e.id, e.level, glob, clean(_printed(e)), clean(e.ask)))
        for path in sorted(set(r["_path"] for r in e.acts if r["state"] != "instance")):
            w_rows.append(("w", e.id, e.level, "*/" + path.lower(), path, ""))
    w_rows.sort(key=lambda r: (r[1], r[3]))
    head = "#knowledge-index v1\tentries=%d\tskipped=%d\tstore=%s" % (len(store.active()), store.skipped, _store_label(store))
    return "\n".join([head] + ["\t".join(r) for r in t_rows + w_rows]) + "\n"


# --- procedures ----------------------------------------------------------------------------------------------------

def _front_matter(text: str) -> dict:
    """The `key: value` lines between the first two `---` lines. A folded value takes the indented lines under it."""
    lines = text.split("\n")
    if not lines or lines[0].strip() != "---":
        return {}
    out, key = {}, None
    for ln in lines[1:]:
        if ln.strip() == "---":
            break
        m = re.match(r"^([A-Za-z_][\w-]*):\s*(.*)$", ln)
        if m:
            key, value = m.group(1), m.group(2).strip()
            out[key] = "" if value in (">", "|", ">-", "|-") else value.strip("\"'")
        elif key and ln.startswith((" ", "\t")):
            out[key] = (out[key] + " " + ln.strip()).strip()
    return out


def _first_heading(text: str) -> str:
    for ln in text.split("\n"):
        if ln.startswith("# "):
            return ln[2:].strip()
    return ""


def _link_targets(index_rel: str, text: str) -> set:
    base = os.path.dirname(index_rel)
    out = set()
    for target in re.findall(r"\]\(([^)\s]+)\)", text):
        target = target.split("#", 1)[0]
        if not target or re.match(r"^[a-z]+:", target):
            continue
        out.add(os.path.normpath(os.path.join(base, target)).replace("\\", "/"))
    return out


def _lookup_rows(root: Path) -> list:
    """(first cell, link targets) of each table row of the task-to-doc table."""
    rel = "docs/reference/doc-lookup.md"
    path = root / rel
    if not path.is_file():
        return []
    rows = []
    for ln in read_text(path).split("\n"):
        if ln.startswith("|") and not re.match(r"^\|\s*-", ln):
            cells = [c.strip() for c in ln.strip().strip("|").split("|")]
            if len(cells) >= 2:
                rows.append((cells[0], _link_targets(rel, ln)))
    return rows


def procedures(root: Path) -> list:
    """The procedure catalogue, computed from the files: every skill, every Codex skill, every workflow doc, and
    every file that a knowledge entry names with `see`. Each record holds the trigger its file already states (for a
    `see` target: the entry's line), the indexes that name it, and the entries that point at it."""
    root = Path(root)
    records = []

    def add(kind, ident, rel, title, trigger):
        records.append({"id": ident, "kind": kind, "home": rel, "title": title, "trigger": trigger,
                        "bytes": (root / rel).stat().st_size, "indexed_by": [], "entries": []})

    for folder, kind, prefix in ((".claude/skills", "skill", "skill:"), (".agents/skills", "codex-skill", "codex-skill:")):
        for path in sorted((root / folder).glob("*/SKILL.md")):
            fm = _front_matter(read_text(path))
            name = fm.get("name") or path.parent.name
            add(kind, prefix + path.parent.name, "%s/%s/SKILL.md" % (folder, path.parent.name), name, fm.get("description", ""))

    lookup = _lookup_rows(root)
    docs = set()
    for pattern in ("docs/ai-includes/*.md", "docs/modding/recipe-*.md", ".ai/*.md", ".ai/roles/*.md"):
        docs.update(p.relative_to(root).as_posix() for p in root.glob(pattern))
    docs_dir = root / "docs"
    if docs_dir.is_dir():
        for path in docs_dir.rglob("*.md"):
            rel = path.relative_to(root).as_posix()
            if DOC_NAME_RE.search(path.name) and not rel.startswith(DOC_SKIP):
                docs.add(rel)
    for rel in sorted(docs):
        trigger = next((cell for cell, targets in lookup if rel in targets), "")
        add("doc", "doc:" + rel, rel, _first_heading(read_text(root / rel)) or Path(rel).stem, trigger)

    by_home = {rec["home"]: rec for rec in records}
    for e in sorted(load(root).active(), key=lambda e: e.id):
        home = e.see.partition("#")[0] if e.see else ""
        if not home:
            continue
        if home not in by_home and (root / home).is_file():
            add("doc", "doc:" + home, home, _first_heading(read_text(root / home)) or Path(home).stem, e.line)
            by_home[home] = records[-1]
        if home in by_home:
            by_home[home]["entries"].append(e.id)

    for index_rel in INDEX_FILES:
        path = root / index_rel
        if not path.is_file():
            continue
        text = read_text(path)
        targets = _link_targets(index_rel, text)
        for rec in records:
            if rec["home"] == index_rel:
                continue
            name = rec["id"].split(":", 1)[1]
            if rec["kind"] == "skill":
                named = re.search(r"(?<![\w/-])/%s(?![\w-])" % re.escape(name), text) or ("skills/%s/" % name) in text
            elif rec["kind"] == "codex-skill":
                named = re.search(r"(?<![\w-])%s(?![\w-])" % re.escape(name), text)
            else:
                named = rec["home"] in targets or rec["home"] in text
            if named or rec["home"] in targets:
                rec["indexed_by"].append(index_rel)
    return sorted(records, key=lambda r: r["id"])


def find_procedures(root: Path, words: list) -> tuple:
    """(procedure records, task-table rows) that hold the words as whole words, most words first. A row is the task
    cell of docs/reference/doc-lookup.md with the files it links: the hand-kept answer to "which doc for this
    task". A row that names a skill and links no file is a row too, with no file."""
    wanted = set(w for word in words for w in _words(word))
    scored = []
    for rec in procedures(root):
        score = len(wanted & set(_words(" ".join([rec["id"], rec["title"], rec["trigger"]]))))
        if score:
            scored.append((-score, rec["id"], rec))
    rows = []
    for cell, targets in _lookup_rows(Path(root)):
        score = len(wanted & set(_words(cell)))
        if score:
            rows.append((-score, cell, sorted(targets)))
    return ([rec for _, _, rec in sorted(scored, key=lambda t: (t[0], t[1]))],
            [(cell, targets) for _, cell, targets in sorted(rows, key=lambda t: (t[0], t[1]))])


# --- command line --------------------------------------------------------------------------------------------------

def _show(e: Entry) -> str:
    out = ["%s [%s, %s, %s]" % (e.id, e.level, e.area, e.status), e.title, "", "Do: " + e.line, "Why: " + e.why]
    if e.ask:
        out.append("A reviewer asks: " + e.ask)
    if e.see:
        out.append("See: " + e.see)
    if e.globs or e.tasks:
        out.append("When: " + "; ".join(filter(None, [", ".join(e.globs), ", ".join(e.tasks)])))
    for row in e.acts:
        where = row.get("ref") or row.get("where")
        out.append("Acts as %s: %s%s" % (row["state"], where, (" (%s)" % row["scope"]) if row.get("scope") else ""))
    if not e.acts:
        out.append("Acts as: a sentence only")
    if e.next:
        out.append("Next: " + e.next)
    for row in e.evidence:
        out.append("Evidence (%s, %s): %s%s" % (row["grade"], row["date"], row["value"],
                                                (" [method: %s]" % row["method"]) if row.get("method") else ""))
    for row in e.sources:
        out.append("Source: " + "; ".join("%s = %s" % (k, row[k]) for k in SOURCE_KEYS if k in row))
    if e.retired:
        out.append("Retired: " + e.retired + ((" (superseded by %s)" % e.superseded_by) if e.superseded_by else ""))
    out.append("File: " + e.path)
    return "\n".join(out)


def _parser() -> argparse.ArgumentParser:
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--root", default=str(REPO), help="the repository to read (default: the one this tool is in)")
    p = argparse.ArgumentParser(prog="knowledge.py", description="Check and query the knowledge store (docs/knowledge).")
    sub = p.add_subparsers(dest="command")
    s = sub.add_parser("check", parents=[common], help="the store's findings")
    s.add_argument("--staged", action="store_true",
                   help="the commit gate: judge the files the commit records, and only when it can change the verdict")
    s = sub.add_parser("for", parents=[common], help="the entries whose triggers match these files")
    s.add_argument("paths", nargs="+")
    s.add_argument("--all", action="store_true", help="retired entries too")
    s = sub.add_parser("find", parents=[common], help="the entries, procedures and task-table rows that hold these words")
    s.add_argument("words", nargs="+")
    s = sub.add_parser("show", parents=[common], help="one entry")
    s.add_argument("id")
    s = sub.add_parser("brief", parents=[common], help="the block for a subagent's spawn brief")
    s.add_argument("--paths", nargs="*", default=[])
    s.add_argument("--task", nargs="*", default=[])
    s = sub.add_parser("debt", parents=[common], help="counts by area and level, and what does not act yet")
    s.add_argument("--json", action="store_true")
    s = sub.add_parser("procedures", parents=[common], help="the computed procedure catalogue")
    s.add_argument("--orphans", action="store_true", help="only the ones no index names")
    s.add_argument("--json", action="store_true")
    s = sub.add_parser("index", parents=[common], help="write the hook's index")
    s.add_argument("--out", required=True)
    return p


#: How many findings of the last commit a commit is shown.
OLD_SHOWN = 5
#: What a refused commit is told last: its committer may never have heard of the store.
STAGED_HINT = ("knowledge: these lines come from the knowledge store (docs/knowledge/README.md). In the named .toml, "
               "make `see`, `ref` or `heading` name the file as it is now, or retire the entry: status = \"retired\" "
               "and retired = \"<date>: <reason>\". Detail: python tools/knowledge.py show <id>.")


def _run_check(root: Path, staged: bool) -> int:
    if not staged:
        findings = check(root)
        for f in findings:
            print(f)
        code = exit_code(findings)
        print("knowledge: %d entries, %d findings (%s)%s" % (
            len(load(root).entries), len(findings), TECH_REL,
            "" if code != 2 else "; NOT fully judged, see the unchecked line"))
        return code
    return _run_staged(root)


def _run_staged(root: Path) -> int:
    """The commit gate. Exit 1 only for a finding that the last commit did not already have."""
    changes = staged_changes(root)
    if changes is None:                     # never the work tree in its place: the commit does not record it
        print("knowledge: git could not list what the commit records, so the store was NOT judged")
        return 2
    head = head_files(root)                 # None before the first commit
    names = commit_files(head, changes)
    store = load(root, Tree(root, set(names), commit=True))
    if store.exists and not staged_matters(store, changes):
        print("knowledge: this commit touches nothing the store names, so the store was not judged")
        return 0
    findings = check(root, tracked=names, staged=True)
    # Two bad rows of one kind print the same line, so the last commit's findings are counted, not only held.
    base = collections.Counter(check(root, tracked=head, staged=True, rev="HEAD")) if findings and head is not None \
        else collections.Counter()
    new, old = [], []
    for f in findings:
        if base[f] > 0:
            base[f] -= 1
            old.append(f)
        else:
            new.append(f)
    code = exit_code(new)
    # The old ones first and cut short: .githooks/pre-commit shows the last 30 lines of a refusal.
    if old:
        print("knowledge: %d finding%s already in the last commit and refuse%s nothing here%s:" % (
            len(old), " was" if len(old) == 1 else "s were", "s" if len(old) == 1 else "",
            "" if len(old) <= OLD_SHOWN else " (the first %d)" % OLD_SHOWN))
        for f in old[:OLD_SHOWN]:
            print("  %s" % f)
    for f in new:
        print(f)
    print("knowledge: %d entries, %d new finding%s (%s, as the commit records it)%s" % (
        len(store.entries), len(new), "" if len(new) == 1 else "s", TECH_REL,
        "" if code != 2 else "; NOT fully judged, see the unchecked line"))
    if code == 1:
        print(STAGED_HINT)
    return code


def main(argv: list) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    parser = _parser()
    try:
        args = parser.parse_args(argv)
    except SystemExit as e:
        return 2 if e.code else 0
    if not args.command:
        parser.print_usage(sys.stderr)
        return 2
    root = Path(args.root)

    if args.command == "check":
        try:
            return _run_check(root, args.staged)
        except Exception:   # a crash of the check is "not judged": never a finding, never a clean run
            traceback.print_exc(limit=6)
            print("knowledge: the check itself failed, so the store was NOT judged", file=sys.stderr)
            return 2

    store = load(root)
    if not store.exists:
        print("knowledge: no store at %s (unchecked, not absent)" % _store_label(store), file=sys.stderr)
        return 2

    if args.command == "for":
        for p in args.paths:
            hits = matching(store, p, include_retired=args.all)
            watched = enforced_by(store, p)
            if not hits and not watched:
                print("no entry matched %s (%d entries scanned in %s)" % (p, len(store.active()), _store_label(store)))
                continue
            print(p)
            for e in hits:
                print("  " + _entry_line(e)[2:])
            for e in watched:
                print("  %s [%s] acts through this file: when you change it, update %s in the same change"
                      % (e.id, e.level, e.path))
        return 0
    if args.command == "find":
        hits = find(store, args.words)
        for e in hits[:15]:
            print(_entry_line(e)[2:])
        if not hits:
            print("no entry holds %s (%d entries scanned in %s)" % (" ".join(args.words), len(store.active()), _store_label(store)))
        records, rows = find_procedures(root, args.words)
        for r in records[:10]:
            print("procedure %s: %s (%s)" % (r["id"], r["trigger"] or r["title"], r["home"]))
        for cell, targets in rows[:10]:
            print("task table: %s -> %s" % (cell, ", ".join(targets) or "no file linked: read the row in docs/reference/doc-lookup.md"))
        if not records and not rows:
            print("no procedure or task-table row holds %s" % " ".join(args.words))
        return 0
    if args.command == "show":
        e = store.entries.get(args.id)
        if e is None:
            print("knowledge: no entry `%s` in %s" % (args.id, _store_label(store)), file=sys.stderr)
            return 2
        print(_show(e))
        return 0
    if args.command == "brief":
        print(brief_text(store, args.paths, args.task))
        return 0
    if args.command == "debt":
        d = debt(store)
        if args.json:
            print(json.dumps(d, indent=1, ensure_ascii=False))
            return 0
        print("knowledge debt: %d active entries (%d retired) in %s" % (d["total"], d["retired"], _store_label(store)))
        print("by level: " + ", ".join("%s %d" % (lv, d["by_level"][lv]) for lv in LEVELS))
        for area, counts in d["by_area"].items():
            print("  %-24s %s" % (area, ", ".join("%s %d" % (lv, counts[lv]) for lv in LEVELS if counts[lv])))
        print("%d entries do not act yet (below a gate):" % len(d["owed"]))
        for row in d["owed"]:
            print("  %s [%s]: next: %s" % (row["id"], row["level"], row["next"]))
        return 0
    if args.command == "procedures":
        records = procedures(root)
        if args.orphans:
            records = [r for r in records if not r["indexed_by"]]
        if args.json:
            print(json.dumps(records, indent=1, ensure_ascii=False))
            return 0
        for r in records:
            print("%s | %s | %s" % (r["id"], r["title"], r["trigger"] or "(no trigger stated)"))
            print("    home %s (%d B); indexed by: %s%s" % (
                r["home"], r["bytes"], ", ".join(r["indexed_by"]) or "NONE",
                ("; entries: " + ", ".join(r["entries"])) if r["entries"] else ""))
        print("%d procedures%s (computed from the files; nothing is stored)" % (
            len(records), " that no index names" if args.orphans else ""))
        return 0
    if args.command == "index":
        out = Path(args.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        tmp = out.with_name(out.name + ".part-%d" % os.getpid())
        tmp.write_text(index_text(store), encoding="utf-8", newline="\n")
        try:
            os.replace(tmp, out)
        except OSError:
            # Several first calls of the hook build the same index at once, and on Windows a rename fails while
            # another process holds the target open. The other writer's file is the same index.
            tmp.unlink(missing_ok=True)
            if not out.is_file():
                raise
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
