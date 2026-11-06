#!/usr/bin/env python3
"""TAOM doc-health linter.

Walks docs/ and reports doc rot:

- Dead markdown links (relative paths that don't resolve)
- Stale version refs (1.3.15, 1.3.x, Bannerlord 1.3 outside docs/migration/)
- Orphan feature docs (docs/features/<x>.md not referenced anywhere else)
- Prose trapped inside an auto-generated backlinks region (build_backlinks.py deletes it)
- Missing feature docs (Main/Features/<X>/ without a matching docs/features/<x>.md)

Usage:
    python tools/lint_docs.py                   # full lint, prints markdown report to stdout
    python tools/lint_docs.py --quick           # dead-link check only (fastest, hook-friendly)
    python tools/lint_docs.py --report PATH     # write report to PATH instead of stdout
    python tools/lint_docs.py --fail-on-dead    # exit 1 if any dead links found (CI)

Conventions match .claude/hooks/detect-docs-gaps.sh for the feature-slug fuzzy match.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import traceback
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlparse

# Repo root resolved from this script's location: tools/lint_docs.py -> repo root
REPO_ROOT = Path(__file__).resolve().parent.parent
DOCS_DIR = REPO_ROOT / "docs"
FEATURES_DIR = DOCS_DIR / "features"
MIGRATION_DIR = DOCS_DIR / "migration"
ARCHIVE_DIR = DOCS_DIR / "archive"
AUDITS_DIR = DOCS_DIR / "audits"
# CLAUDE.md eager-load budget. CLAUDE.md loads into EVERY session and every agent spawn, so
# every KB is a permanent per-turn tax. Two restructures thinned it: 174 KB -> 91 KB
# (2026-07-12), then 93.6 KB -> ~42 KB (2026-07-18) by moving the four big index tables
# (Key Paths, Harmony, GameModel, Doc Lookup) and the procedure sections (Localization, Codex,
# Hooks, Equipment, PowerShell) out to docs/reference/*.md behind one-line stubs. The rule that
# keeps it lean: CLAUDE.md answers "what must I never do, and where do I look next?" — a section
# that answers "how do I do X" is a skill body; "what are all the X" is a docs/reference/ file
# with a stub. Caps are calibrated to the ~42 KB landing; tighten further if the index is
# slimmed again. NOTE: new features now add their row to docs/reference/feature-map.md, NOT to
# a Key Paths table here (that table moved) — so CLAUDE.md should barely grow per feature.
CLAUDE_MD = REPO_ROOT / "CLAUDE.md"
CLAUDE_MD_MAX_BYTES = 46_000       # hard cap (fails --fail-on-drift); was 100_000 pre-2026-07-18
CLAUDE_MD_WARN_BYTES = 44_000      # report-only early warning; was 95_000
CLAUDE_MD_MAX_TABLE_ROW = 400      # chars; thin rows run ~80-300
CLAUDE_MD_MAX_PROSE_LINE = 600     # chars; catches paragraph bloat outside tables
# Enforcement flipped ON at the end of the decomposition (2026-07-12, Track C8).
CLAUDE_MD_BUDGET_ENFORCE = True

# AGENTS.md eager-load budget (added 2026-07-18). Codex reads AGENTS.md but TRUNCATES it at
# project_doc_max_bytes. Before the rebuild, AGENTS.md was 112 KB with the actual review RULES
# starting at byte ~83.5 K — past even the 64 KB flagged cap — so Codex reviewed without them.
# The `.claude/skills/{codex-verify,review-codex,deep-review}` dispatch commands now pass
# `-c project_doc_max_bytes=65536`; this budget keeps AGENTS.md well under that so the rules
# (which must sit early) never get pushed past the cap again. THIS is the check that would have
# caught the truncation. Historical catch-log lives in docs/reviews/codex-track-record.md.
AGENTS_MD = REPO_ROOT / "AGENTS.md"
AGENTS_MD_MAX_BYTES = 44_000       # hard cap (fails --fail-on-drift)
AGENTS_MD_WARN_BYTES = 40_000      # report-only early warning

# Rolled-out CHANGELOG halves: verbatim historical text whose links/versions were written
# relative to the repo root at the time — never lint them as living docs.
CHANGELOG_ARCHIVE_DIR = DOCS_DIR / "changelog-archive"
# Gitignored raw Codex transcripts (docs/reviews/raw/): on-disk reference only, never curated.
REVIEWS_RAW_DIR = DOCS_DIR / "reviews" / "raw"
# Architecture Decision Records: point-in-time by definition — an ADR names the versions that were
# current when the decision was taken, and rewriting them falsifies the record.
ADRS_DIR = DOCS_DIR / "adrs"
# Staging for pages written to be published on someone else's documentation site. Their links are
# absolute in THAT site's URL space (/guides/foo/, /3d/bar/), not repo-relative paths, so resolving
# them against this repo is meaningless and reports every one as dead.
COMMUNITY_DIR = DOCS_DIR / "community"
MAIN_FEATURES_DIR = REPO_ROOT / "Main" / "Features"

LINK_RE = re.compile(r"\[([^\]]*)\]\(([^)]+)\)")
INLINE_CODE_RE = re.compile(r"`+[^`\n]+?`+")
STALE_VERSION_PATTERNS = [
    (re.compile(r"\bv?1\.3\.15\b"), "1.3.15"),
    (re.compile(r"\bBannerlord\s+1\.3\b(?!\.)"), "Bannerlord 1.3"),
    (re.compile(r"\bv1\.3\.x\b"), "v1.3.x"),
    # agent-operating-manual.md names v1.4.5 and v1.4.6 stale alongside v1.3.15; the checker
    # never looked for them, so the rule and its enforcement disagreed (#405 gap 2).
    #
    # `(?<![-\w.])` keeps the branch name out. The active branch IS `bannerlord-1.4.5`, quoted 53
    # times across 21 docs — "the active branch (currently `bannerlord-1.4.5`)" in
    # TRANSLATOR_GUIDE.md:229 is correct, current prose that a bare \b pattern reads as rot, and no
    # wording rule can tell the two apart because the sentence genuinely says "currently".
    # `(?<![-\w.])` covers two distinct hyphen cases, both real in this tree:
    #   - the branch name. The active branch IS `bannerlord-1.4.5`, quoted 53 times across 21
    #     docs; TRANSLATOR_GUIDE.md:229 — "the active branch (currently `bannerlord-1.4.5`)" — is
    #     correct, current prose that no wording rule can separate from a claim.
    #   - hyphenated compound modifiers. spider.md:262 heads a dated section "(2026-06-12,
    #     post-1.4.6 campaign)"; "post-1.4.6" describes when the campaign ran.
    # Narrowing this to `(?<!bannerlord-)` was tried and re-admitted spider.md:262, so the broad
    # form stays. The cost is disclosed: a claim spelled with a hyphen before the digits is not
    # seen. `test_a_current_claim_about_145_is_reported` pins that the ordinary spelling still is.
    (re.compile(r"(?<![-\w.])v?1\.4\.5\b"), "1.4.5"),
    (re.compile(r"(?<![-\w.])v?1\.4\.6\b"), "1.4.6"),
]
# Paths where v1.3 refs are intentional (migration docs, archived material, ADRs that name old versions historically).
# docs/audits/ is the v1.3.15 migration audit record — every "v1.3.15-unverified" flag and
# "verified against v1.3.15" note is a historical fact about that audit campaign. Exempt the whole dir.
STALE_VERSION_EXEMPT_PREFIXES = (
    str(MIGRATION_DIR).replace("\\", "/"),
    str(ARCHIVE_DIR).replace("\\", "/"),
    str(AUDITS_DIR).replace("\\", "/"),
    str(CHANGELOG_ARCHIVE_DIR).replace("\\", "/"),
    str(REVIEWS_RAW_DIR).replace("\\", "/"),
    # The comment above already claimed "ADRs that name old versions historically" were exempt,
    # but docs/adrs/ was never in this tuple — so adrs/010, the ADR that recorded THIS very
    # problem, was itself reported as rot. Implementing the intent that was already documented.
    str(ADRS_DIR).replace("\\", "/"),
)
DEAD_LINK_EXEMPT_PREFIXES = (
    str(ARCHIVE_DIR).replace("\\", "/"),
    str(CHANGELOG_ARCHIVE_DIR).replace("\\", "/"),
    str(REVIEWS_RAW_DIR).replace("\\", "/"),
    str(COMMUNITY_DIR).replace("\\", "/"),
)
# Some review files document past API surface and intentionally cite 1.3.15.
# codex-prompt-* / codex-result-* are Codex review transcripts — by design they instruct/record
# verification against the version under review, so their v1.3.15 refs are historical, not rot.
STALE_VERSION_EXEMPT_FILENAME_SUBSTRINGS = (
    "rca-",
    "codex-adversarial-",
    "codex-prompt-",
    "codex-result-",
    "doc-lint-",
    # Historical Codex-review essay logs: every version ref is a fact about the review that ran,
    # not current guidance. codex-track-record.md holds the catch-log moved out of AGENTS.md
    # 2026-07-18; review-lessons-archive.md holds the rolled-out older essays.
    "codex-track-record",
    "review-lessons-archive",
    # Historical review records added 2026-08-05 (leftover sweep): REVIEW-LOG rows and the
    # audit-* snapshots are point-in-time facts about the reviews/audits that ran, exactly like
    # rca-*; the lessons/ dir quotes old versions as the historical context of each lesson.
    "REVIEW-LOG",
    "audit-",
    # The docs that DESCRIBE this checker have to quote rot to explain it: doc-health-linter.md
    # lists the shapes it catches, doc-lookup.md summarises the marker set. Same self-reference
    # as agent-operating-manual.md — the rule text that defines staleness reads as stale — and
    # the same reason `doc-lint-` is already here.
    "doc-health-linter",
    "doc-lookup",
)
STALE_VERSION_EXEMPT_DIR_PARTS = ("docs/reviews/lessons",)
# The exemptions above are all whole-FILE switches (directory, filename, path part). That is the
# wrong granularity and is why #397 stayed at 29 findings after the 2026-08-05 exemption sweep:
# the remaining sites live in docs that must stay linted (native-skin-fixes.md alone holds 10),
# and the real distinction is per LINE — "is this naming the CURRENT target, or recording history?"
# So the model flips: a version string alone is not rot; a version string presented as current is.
# Measured against the 29: this clears 26. The wording-marker approach from the issue clears 16.
# The marker set IS the detection surface, so rot phrased outside it is silent (#405). Widened with
# four verb phrases and the bare `Engine:` label — all of them name a current target without using a
# marker word. Measured on the real tree: still 0 findings, so this buys detection at no
# false-positive cost, which is why it lands now rather than behind an exemption pass.
# `Engine\s*:` sits OUTSIDE the \b group deliberately: a word boundary after a colon needs a word
# character next, so `Engine: 1.3.15` can never match from inside it.
# How far from the version string a marker still counts as describing it. See the call site in
# check_stale_versions for the measurement this number comes from.
MARKER_WINDOW = 60
CURRENT_TARGET_RE = re.compile(
    r"\b(current(ly)?|now|supported|"
    r"builds? against|building against|pinned to|"
    r"built for|requires?|compatible with)\b"
    # `target` carries two senses and only one is a claim. "hook targets remain authored",
    # "All 7 targets were re-derived", "Patch targets exist in v1.4.5" are NOUNS — the thing a
    # patch points at — and they sit next to a version in exactly the docs that discuss porting.
    # The claim sense is either the label form (`Target: Bannerlord X`, the CLAUDE.md header this
    # check exists to catch) or the verb followed by a version.
    r"|\bTargets?\s*:"
    r"|\btarget(s|ed|ing)?\s+(?:Bannerlord\b|v?\d)"
    # …and the copula form. "Our target version IS 1.3.15", "the active version IS 1.3.15",
    # "our target REMAINS 1.3.15" are claims the pre-#405 checker caught with a bare marker;
    # narrowing to "marker immediately before a version" dropped them. What separates them from
    # the noun senses is that a version follows the verb: "hook targets REMAIN authored" and
    # "All 7 targets WERE re-derived" do not.
    r"|\b(?:target|active)\w*\s+(?:\w+\s+){0,2}(?:is|are|was|were|remains?)\s+"
    r"(?:the\s+|Bannerlord\s+)*v?\d"
    # Same two senses: `runs on` is ordinary technical English — doc-health-linter.md:69 says a
    # regex "still runs on the raw line".
    r"|\bruns? on\s+(?:Bannerlord\b|v?\d)"
    # `active` has the same noun/adjective split as `target`: "423 missing active action types"
    # (lotrlome-armory-snapshot/README.md:99) is a category of animation, not a claim about an
    # engine version. Only the version-facing sense counts.
    r"|\bactive\s+(?:Bannerlord\b|v?\d)"
    # NOTE: `installed` is deliberately NOT a marker yet. It is the strongest present-tense claim
    # in this vocabulary, and adding it reports 8 findings across the tree — every one genuine.
    # They are not prose defects though: reflection-sites.md:66 ("all 32 resolve against installed
    # v1.4.5") and battle-scenes.md:7 are binding-verification claims that need re-deriving against
    # the pin, not rewording. Adding the marker without that work would invite the wrong fix.
    # Tracked separately; add the marker with the re-verification, not before it.
    # Reverse order: "Bannerlord 1.3.15 is the active engine."
    r"|\bv?[\d.]+\s+is\s+(?:the\s+)?(?:active|current|target)\b"
    # `Engine:` as a LABEL — line start, table cell, bullet or heading. Mid-sentence it is just
    # punctuation: elephant.md:309 says "verified against the engine: `Vec2.LeftVec()` …".
    r"|(?:^|[|>#*-]\s*)Engine\s*:", re.I | re.M)
# Residue after that flip: contrast lines that name an old version AND a present-tense word, where
# the present-tense word belongs to the CURRENT version, also named on the same line — e.g.
# "that mod ships a v1.3.15-only DLL. TAOM tracks the current engine (v1.4.7)", and the rule text
# in agent-operating-manual.md that defines staleness. A line that states the pin is not claiming
# an older version is the target; it is comparing the two. Resolved in the checker, so no doc is
# edited (issue #397: "Do not 'fix' the 29 by editing the docs. They are accurate; the check is
# wrong."). Trade-off: prose that names the pin AND wrongly calls an old version current is missed.
# Codex review *transcripts* (prompts + results) are external-tool snapshots, not curated docs.
# We don't lint their internal links either — they capture historical state and we don't edit them.
DEAD_LINK_EXEMPT_FILENAME_SUBSTRINGS = (
    "codex-adversarial-",
    "codex-prompt-",
    "codex-result-",
    "doc-lint-",
    # Historical review essay-logs — their file refs capture past state; we don't edit them.
    "codex-track-record",
    "review-lessons-archive",
)
DEAD_LINK_EXEMPT_FILENAMES = ("TEMPLATE.md",)
# Top-level dirs we walk for markdown files.
DOC_ROOTS = [DOCS_DIR]


@dataclass
class LintReport:
    dead_links: list[tuple[Path, int, str, str]] = field(default_factory=list)
    untracked_targets: list[tuple[Path, int, str, str]] = field(default_factory=list)
    stale_versions: list[tuple[Path, int, str, str]] = field(default_factory=list)
    orphan_features: list[Path] = field(default_factory=list)
    backlinks_prose: list[tuple[Path, int, str, str]] = field(default_factory=list)
    missing_feature_docs: list[tuple[str, str]] = field(default_factory=list)
    config_drift: list[tuple[Path, int, str, str]] = field(default_factory=list)
    version_mismatches: list[tuple[Path, int, str, str]] = field(default_factory=list)
    budget: list[tuple[Path, int, str, str]] = field(default_factory=list)
    model_registry: list[tuple[Path, int, str, str]] = field(default_factory=list)
    dashes: list[tuple[Path, int, str, str]] = field(default_factory=list)
    rca_followups: list[tuple[Path, int, str, str]] = field(default_factory=list)
    feature_verify: list[tuple[Path, int, str, str]] = field(default_factory=list)
    git_diff_failures: list[tuple[Path, int, str, str]] = field(default_factory=list)
    rule_drift: list[tuple[Path, int, str, str]] = field(default_factory=list)
    adr_symbols: list[tuple[Path, int, str, str]] = field(default_factory=list)

    @property
    def total(self) -> int:
        return (
            len(self.dead_links)
            + len(self.untracked_targets)
            + len(self.stale_versions)
            + len(self.orphan_features)
            + len(self.backlinks_prose)
            + len(self.missing_feature_docs)
            + len(self.config_drift)
            + len(self.version_mismatches)
            + len(self.budget)
            + len(self.model_registry)
            + len(self.dashes)
            + len(self.rca_followups)
            + len(self.feature_verify)
            + len(self.git_diff_failures)
            + len(self.rule_drift)
            + len(self.adr_symbols)
        )


def iter_markdown(root: Path):
    for p in root.rglob("*.md"):
        if p.is_file():
            yield p


def is_external_link(target: str) -> bool:
    if not target:
        return True
    if target.startswith(("http://", "https://", "mailto:", "ftp://", "file://", "file:")):
        return True
    parsed = urlparse(target)
    return bool(parsed.scheme)


def resolve_link(source_file: Path, target: str) -> Path | None:
    """Resolve a relative link from source_file. Returns absolute Path or None if external/anchor-only."""
    if is_external_link(target):
        return None
    # Strip query string and fragment
    target = target.split("#", 1)[0].split("?", 1)[0].strip()
    if not target:
        return None
    # Absolute (rare in our repo) — treat as repo-root relative if it begins with /
    if target.startswith("/"):
        return (REPO_ROOT / target.lstrip("/")).resolve()
    return (source_file.parent / target).resolve()


FENCE_RE = re.compile(r"^\s*(`{3,}|~{3,})")


def iter_lines_outside_code_fences(text: str):
    """Yield (lineno, line) skipping anything inside ``` or ~~~ fenced blocks, at any indent.
    A fence closes only on the opener's character with a run at least as long as the opener's,
    so a three-backtick line inside a four-backtick block stays inside it (nested fences)."""
    fence_marker: str | None = None
    for lineno, line in enumerate(text.splitlines(), start=1):
        m = FENCE_RE.match(line)
        if m:
            marker = m.group(1)
            if fence_marker is None:
                fence_marker = marker
            elif marker[0] == fence_marker[0] and len(marker) >= len(fence_marker):
                fence_marker = None
            continue
        if fence_marker is not None:
            continue
        yield lineno, line


def is_dead_link_exempt(f: Path) -> bool:
    if f.name in DEAD_LINK_EXEMPT_FILENAMES:
        return True
    if any(sub in f.name for sub in DEAD_LINK_EXEMPT_FILENAME_SUBSTRINGS):
        return True
    f_posix = str(f).replace("\\", "/")
    if any(f_posix.startswith(prefix) for prefix in DEAD_LINK_EXEMPT_PREFIXES):
        return True
    return False


def is_never_committed_target(p: Path) -> bool:
    """True for link targets under docs/reviews/raw/, which is gitignored (.gitignore:157 — the
    raw Codex transcripts are 2-4 MB each and stay on the reviewing machine).

    Those files exist for whoever ran the review and for nobody else, so the same rca-*/REVIEW-LOG
    links that resolve on the authoring machine are dead on every fresh clone — 14 of them today.
    That asymmetry is why this check reads as clean locally while every other contributor sees it
    dirty, and it is a property of the TARGET, not of the linking file: exempting the linking files
    instead (they are already exempt from the stale-version check) would switch off dead-link
    coverage for REVIEW-LOG.md's several hundred other links."""
    try:
        return p.is_relative_to(REVIEWS_RAW_DIR)
    except (OSError, ValueError):
        return False


def looks_like_path_target(target: str) -> bool:
    """Heuristic: real path targets don't have unescaped whitespace.
    Filters out things like `[xml](Get-Content $versionXml -Raw)` (PowerShell type accelerator
    inside a unified-diff line — not a markdown link to a file)."""
    return not any(c.isspace() for c in target)


def check_dead_links(files: list[Path]) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    for f in files:
        if is_dead_link_exempt(f):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for lineno, line in iter_lines_outside_code_fences(text):
            scrubbed = INLINE_CODE_RE.sub("", line)
            for match in LINK_RE.finditer(scrubbed):
                label, target = match.group(1), match.group(2).strip()
                if not looks_like_path_target(target):
                    continue
                resolved = resolve_link(f, target)
                if resolved is None:
                    continue
                if is_never_committed_target(resolved):
                    continue
                if not resolved.exists():
                    findings.append((f, lineno, target, label))
    return findings


def _untracked_paths() -> set[Path]:
    """Every untracked, non-ignored path in the repo.

    Fail open: `_git_out` returns None on any failure and that becomes an EMPTY set, so a broken
    or absent git can only make the caller under-report. It can never invent a finding.
    `--exclude-standard` honours .gitignore, so the gitignored `docs/reviews/raw/` transcripts
    stay out of this set and keep their existing dead-link exemption rather than migrating into
    a new one.
    """
    out = _git_out("-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard")
    if not out:
        return set()
    return {REPO_ROOT / name.strip() for name in out.splitlines() if name.strip()}


def check_untracked_link_targets(
    files: list[Path], untracked: set[Path] | None = None
) -> list[tuple[Path, int, str, str]]:
    """Links whose target exists on disk but is NOT tracked by git (#517).

    `check_dead_links` resolves a target with `Path.exists()`, a filesystem stat, so trackedness
    is invisible to it by construction. A link to an untracked file therefore reads clean on the
    machine that authored it and is dead for everyone else. Not hypothetical: on 2026-08-28
    `docs/INDEX.md` and `docs/reference/doc-lookup.md` were pushed carrying links to
    `docs/features/armoury-mesh-cleanup.md` while it was still untracked. This linter reported
    0 dead links; the remote carried 2.

    Deliberately a separate finding rather than folded into `dead_links`, because the fixes
    differ: a dead link wants the link corrected or the target written, this wants `git add`.
    A target that is missing from disk entirely is left to `check_dead_links`, so nothing is
    double-counted.
    """
    if untracked is None:
        untracked = _untracked_paths()
    if not untracked:
        return []
    findings: list[tuple[Path, int, str, str]] = []
    for f in files:
        if is_dead_link_exempt(f):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for lineno, line in iter_lines_outside_code_fences(text):
            scrubbed = INLINE_CODE_RE.sub("", line)
            for match in LINK_RE.finditer(scrubbed):
                label, target = match.group(1), match.group(2).strip()
                if not looks_like_path_target(target):
                    continue
                resolved = resolve_link(f, target)
                if resolved is None or is_never_committed_target(resolved):
                    continue
                # Missing from disk is a dead link, not an untracked one.
                if not resolved.exists():
                    continue
                if resolved in untracked:
                    findings.append((f, lineno, target, label))
    return findings


def _cat_file_batch(specs: list[str]) -> list[str | None]:
    """The text of each object (`:path`, `HEAD:path`) through one git process, because a process per
    file costs about 27 ms and a doc commit reads hundreds. None for anything but a file."""
    if not specs:
        return []
    try:
        proc = subprocess.run(["git", "-C", str(REPO_ROOT), "cat-file", "--batch"],
                              input=("\n".join(specs) + "\n").encode("utf-8"), capture_output=True)
    except OSError:
        return [None] * len(specs)
    if proc.returncode != 0:
        return [None] * len(specs)
    texts: list[str | None] = []
    pos = 0
    for _ in specs:
        end = proc.stdout.index(b"\n", pos)
        fields = proc.stdout[pos:end].rsplit(b" ", 2)
        pos = end + 1
        if len(fields) != 3 or not fields[2].isdigit():      # `<spec> missing`, `<spec> ambiguous`
            texts.append(None)
            continue
        size = int(fields[2])
        texts.append(proc.stdout[pos:pos + size].decode("utf-8", "replace") if fields[1] == b"blob" else None)
        pos += size + 1
    return texts


class CommitView:
    """The tree a commit records, for the gating checks, so another session's unstaged edit in the same
    checkout cannot refuse this commit and a staged breach cannot pass behind a clean working copy.
    `names` are the repo-relative paths the commit changes against `base`. A plain commit records the
    index, and a git hook always sees the final index. A path commit records its named working copies
    on top of HEAD, whatever else is staged."""

    def __init__(self, names: set[str], from_index: bool, base: str = "HEAD"):
        self.names = names
        self.from_index = from_index
        self.base = base
        self._texts: dict[str, str | None] = {}

    @staticmethod
    def name_of(path: Path) -> str:
        return Path(os.path.normpath(path)).relative_to(REPO_ROOT).as_posix()

    def load(self, paths) -> None:
        """Read these files at once, so the reads after it spawn nothing."""
        names = [n for n in dict.fromkeys(self.name_of(p) for p in paths) if n not in self._texts]
        for name in [n for n in names if not self.from_index and n in self.names]:
            path = REPO_ROOT / name
            self._texts[name] = path.read_text(encoding="utf-8", errors="replace") if path.is_file() else None
        from_git = [n for n in names if n not in self._texts]
        prefix = ":" if self.from_index else "HEAD:"
        self._texts.update(zip(from_git, _cat_file_batch([prefix + n for n in from_git])))

    def read(self, path: Path) -> str | None:
        """The file's text as the commit records it; None when it records no such file."""
        self.load([path])
        return self._texts[self.name_of(path)]


def commit_view(staged: bool, only_paths: list[str] | None, base: str) -> CommitView | None:
    """What this run judges: None for the working copies, with neither flag or outside a git tree
    (fail open, as the other git-reading checks do)."""
    if not staged and not only_paths:
        return None
    inside = _git_run("rev-parse", "--is-inside-work-tree")
    if inside is None or inside.returncode != 0:
        return None
    if only_paths:
        listed = _git_out("-c", "core.quotePath=false", "ls-files", "--", *only_paths)
    else:
        against = [] if base == "HEAD" and _is_unborn_head() else [base]
        listed = _git_out("-c", "core.quotePath=false", "diff", "--cached", "--name-only", "--no-renames",
                          "--diff-filter=d", *against)
    return CommitView({n.strip() for n in (listed or "").splitlines() if n.strip()}, not only_paths, base)


def _commit_text(path: Path, commit: CommitView | None) -> str | None:
    """A file's text: as the commit records it, or the working copy with no commit in view. None when
    there is no such file."""
    try:
        return path.read_text(encoding="utf-8", errors="replace") if commit is None else commit.read(path)
    except (OSError, ValueError):
        return None


def _read_pin(commit: CommitView | None = None) -> str:
    """The pinned game version exactly as committed ('v1.4.7'); '' if absent or unreadable.

    Returned raw, not normalised: check_version_consistency quotes it verbatim in its findings.
    """
    return (_commit_text(REPO_ROOT / ".claude" / "pinned-game-version.txt", commit) or "").strip()


CLAUSE_BREAK_RE = re.compile(
    r"(?<=[.!?])\s"          # sentence end
    r"|\s\|\s|\|"            # table cell
    r"|\(see\b",             # cross-reference aside: "(see \"v1.4.6 native port\")" names a
                             # section, it does not claim a target
    re.I)
# A version the sentence explicitly rules OUT is not a claim that it is current:
# elephant.md:117 says the donor pack is "built for Bannerlord ~1.2.12, NOT 1.4.5".
NEGATED_VERSION_RE = re.compile(r"\b(not|never|isn't|is not|rather than|instead of)\s*$", re.I)


def clause_around(line: str, match: re.Match) -> str:
    """The clause the version sits in: same sentence, same table cell, bounded by MARKER_WINDOW.

    A markdown line is a whole paragraph — castle-recruitment.md:117 runs 1,450 characters — so
    "a marker somewhere on this line" pairs words that have nothing to do with each other. Two
    bounds, and both are needed:

    - **Clause.** castle-recruitment.md:117 reads "… returning null on v1.4.6). Current dev data
      has full coverage" — "Current" opens a new sentence about the DATA. Splitting on sentence
      ends and table-cell pipes keeps a marker with the version it actually describes.
    - **Distance.** A clause can still be long; MARKER_WINDOW caps how far the search reaches.
      Measured over the 1.4.5/1.4.6 candidates, real pairs sit within ~51 characters and the
      nearest unrelated one was at 71, so the cap sits in that gap.
    """
    lo = max(0, match.start() - MARKER_WINDOW)
    hi = min(len(line), match.end() + MARKER_WINDOW)
    for b in CLAUSE_BREAK_RE.finditer(line, lo, match.start()):
        lo = b.end()                       # last break before the version wins
    right = CLAUSE_BREAK_RE.search(line, match.end(), hi)
    return line[lo:right.start() if right else hi]


def check_stale_versions(files: list[Path]) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    pin = _norm_ver(_read_pin())
    pin_re = re.compile(r"\bv?" + re.escape(pin) + r"\b") if pin else None
    for f in files:
        f_posix = str(f).replace("\\", "/")
        if any(f_posix.startswith(prefix) for prefix in STALE_VERSION_EXEMPT_PREFIXES):
            continue
        if any(sub in f.name for sub in STALE_VERSION_EXEMPT_FILENAME_SUBSTRINGS):
            continue
        if any(part in f_posix for part in STALE_VERSION_EXEMPT_DIR_PARTS):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for lineno, line in iter_lines_outside_code_fences(text):
            for pattern, label in STALE_VERSION_PATTERNS:
                m = pattern.search(line)
                if m:
                    # Rot is a present-tense claim, not a mention (#397). A line that merely
                    # records when something happened ("ported for TAOM v1.3.15", "v1.3.15
                    # reference RVA, informational only") is correct as written.
                    # Test that on the line with inline code stripped: a present-tense word inside
                    # backticks is an identifier, not a claim — `CampaignTime.Now` in an API-break
                    # note (messengers.md:23) is the whole reason this is scrubbed. The version
                    # match above stays on the raw line, where a `1.3.15` in backticks still counts.
                    #
                    # The marker must also be NEAR the version. A markdown line here is a whole
                    # paragraph — castle-recruitment.md:117 runs 1,450 characters — so "somewhere on
                    # the same line" pairs a marker with a version that has nothing to do with it.
                    # Measured over the 1.4.5/1.4.6 candidate set: 11 findings have the marker within
                    # 51 characters of the version, the next is at 71, and the tail reaches 1,228.
                    # The window sits in that gap. Widening it re-admits paragraph noise; narrowing
                    # it starts dropping ordinary prose like "TAOM is built for Bannerlord 1.3.15."
                    #
                    # These two guards `continue`, not `break`: both judge THIS version match, not
                    # the line. A line can carry a mention of one stale version and a live claim
                    # about another — "Historical: v1.3.15 shipped. The current target is Bannerlord
                    # 1.4.5." — and 1.3.15 sorts first in STALE_VERSION_PATTERNS. Breaking here
                    # retires the whole pattern list on the mention and never evaluates 1.4.5, so
                    # real rot goes silent. The pin guard below is different: naming the pin is a
                    # property of the line, so it correctly retires all patterns.
                    if NEGATED_VERSION_RE.search(line[max(0, m.start() - 24):m.start()]):
                        continue
                    if not CURRENT_TARGET_RE.search(INLINE_CODE_RE.sub("", clause_around(line, m))):
                        continue
                    # ...and a line that also names the pin is contrasting the two, not claiming
                    # the old one is current.
                    if pin_re and pin_re.search(line):
                        break
                    findings.append((f, lineno, label, line.strip()[:140]))
                    break  # one finding per line is enough
    return findings


# --- config-example drift ---------------------------------------------------
# A feature doc that embeds a ```json block mirroring a shipped ModuleData config must
# not drift from the shipped defaults. We compare only keys present in BOTH the doc block
# and the shipped file (partial examples are fine — a doc showing 5 of 7 keys is not
# flagged for the 2 it omits); a shared key whose value differs, or a doc key ABSENT from
# the shipped file (renamed/removed), is drift. Doc blocks that aren't valid JSON (annotated
# with `...` or `//` comments) are skipped — not comparable. This is the enforcement for the
# v1.4.7 finding: flipping banner_color_config.json's EnableLayerLimitTranspiler default left
# the feature doc's example showing the old `true`.
MODULEDATA_JSON_RE = re.compile(r"([\w./\\-]*ModuleData[\w./\\-]*\.json)")
JSON_FENCE_OPEN_RE = re.compile(r"^\s*```json\s*$")
FENCE_CLOSE_RE = re.compile(r"^\s*```\s*$")


def _config_json_blocks(text: str):
    """Yield (config_path, json_body, open_lineno) for each ```json block preceded
    (within 6 lines) by a ModuleData/*.json path reference."""
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        if JSON_FENCE_OPEN_RE.match(lines[i]):
            open_line = i
            j = i + 1
            body: list[str] = []
            while j < len(lines) and not FENCE_CLOSE_RE.match(lines[j]):
                body.append(lines[j])
                j += 1
            cfg_path = None
            for k in range(open_line - 1, max(-1, open_line - 7), -1):
                m = MODULEDATA_JSON_RE.search(lines[k])
                if m:
                    cfg_path = m.group(1)
                    break
            if cfg_path:
                yield cfg_path, "\n".join(body), open_line + 1
            i = j + 1
        else:
            i += 1


def _is_historical_doc(f: Path) -> bool:
    """Migration/archive/audit dirs + rca-/codex-*/doc-lint- transcripts capture point-in-time
    state; their config examples are historical, not living docs. Same exemption set the
    stale-version check uses."""
    f_posix = str(f).replace("\\", "/")
    if any(f_posix.startswith(prefix) for prefix in STALE_VERSION_EXEMPT_PREFIXES):
        return True
    if any(sub in f.name for sub in STALE_VERSION_EXEMPT_FILENAME_SUBSTRINGS):
        return True
    return False


def check_config_example_drift(files: list[Path],
                               commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    files = [f for f in files if not _is_historical_doc(f)]
    if commit is not None:
        commit.load(files)
    for f in files:
        text = _commit_text(f, commit)
        if text is None:
            continue
        for cfg_path, json_body, lineno in _config_json_blocks(text):
            shipped_text = _commit_text(REPO_ROOT / cfg_path.replace("\\", "/"), commit)
            if shipped_text is None:
                continue
            try:
                doc_json = json.loads(json_body)
            except ValueError:
                continue  # annotated / partial example — not comparable
            try:
                shipped = json.loads(shipped_text.lstrip("\ufeff"))
            except ValueError:
                continue
            if not isinstance(doc_json, dict) or not isinstance(shipped, dict):
                continue
            for key, dval in doc_json.items():
                if key not in shipped:
                    findings.append((f, lineno, cfg_path,
                                     f"doc example key '{key}' is not in shipped {cfg_path}"))
                elif shipped[key] != dval:
                    findings.append((f, lineno, cfg_path,
                                     f"doc shows \"{key}\": {json.dumps(dval)} but shipped {cfg_path} has {json.dumps(shipped[key])}"))
    return findings


# --- version consistency ----------------------------------------------------
# The canonical committed game-version markers must agree with the pin
# (.claude/pinned-game-version.txt): CLAUDE.md's "Target: Bannerlord X" line(s) and the
# API-snapshot headers. Catches "pin bumped but a doc/snapshot left stale" — the exact
# drift this repo was in at the start of the v1.4.7 bump (pin v1.4.6, snapshot v1.4.5).
def _norm_ver(s: str) -> str:
    return s.strip().lower().lstrip("v")


def check_version_consistency(commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    pin_raw = _read_pin(commit)
    pin = _norm_ver(pin_raw)
    if not pin:
        return findings

    def check_file(path: Path, pattern: re.Pattern, what: str):
        text = _commit_text(path, commit)
        if text is None:
            return
        for lineno, line in enumerate(text.splitlines(), start=1):
            m = pattern.search(line)
            if m and _norm_ver(m.group(1)) != pin:
                findings.append((path, lineno, m.group(1),
                                 f"{what} says {m.group(1)} but pin is {pin_raw}"))

    check_file(REPO_ROOT / "CLAUDE.md",
               re.compile(r"Target:\s*Bannerlord\s+v?([0-9]+(?:\.[0-9]+)+)"),
               "CLAUDE.md target")
    # AGENTS.md (Codex reviewer instructions) — anchor on its stable declarative line so
    # incidental version mentions don't false-positive. The historical review essays that DID
    # cite old versions moved to docs/reviews/codex-track-record.md (stale-version-exempt).
    check_file(REPO_ROOT / "AGENTS.md",
               re.compile(r"mod for Bannerlord\s+v?([0-9]+(?:\.[0-9]+)+)"),
               "AGENTS.md target")
    snap_dir = DOCS_DIR / "reference" / "taleworlds-api-snapshot"
    for name in ("gamemodel-bases.md", "patch-targets.md"):
        check_file(snap_dir / name,
                   re.compile(r"\(v?([0-9]+(?:\.[0-9]+)+)\s+snapshot\)"),
                   f"snapshot header ({name})")
    return findings


def pascal_to_kebab(name: str) -> str:
    """InitialChildGeneration -> initial-child-generation. Matches detect-docs-gaps.sh."""
    s = re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", name)
    s = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1-\2", s)
    return s.lower()


def feature_doc_basenames() -> set[str]:
    if not FEATURES_DIR.is_dir():
        return set()
    return {
        p.stem
        for p in FEATURES_DIR.glob("*.md")
        if p.name != "TEMPLATE.md"
    }


def check_missing_feature_docs(existing_docs: set[str]) -> list[tuple[str, str]]:
    """For each Main/Features/<X>/, ensure a docs/features/<x>.md exists (fuzzy match)."""
    findings: list[tuple[str, str]] = []
    if not MAIN_FEATURES_DIR.is_dir():
        return findings
    for d in sorted(MAIN_FEATURES_DIR.iterdir()):
        if not d.is_dir():
            continue
        name = d.name
        kebab = pascal_to_kebab(name)
        # Fuzzy match: exact, suffix -system, prefix, or substring either direction
        found = any(
            doc == kebab
            or doc == f"{kebab}-system"
            or doc.startswith(kebab)
            or kebab in doc
            or doc in kebab
            for doc in existing_docs
        )
        if not found:
            findings.append((name, f"docs/features/{kebab}.md"))
    return findings


def build_inbound_reference_index(files: list[Path]) -> dict[Path, set[Path]]:
    """For each markdown file, the set of files that link to it."""
    index: dict[Path, set[Path]] = {}
    for f in files:
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for _lineno, line in iter_lines_outside_code_fences(text):
            scrubbed = INLINE_CODE_RE.sub("", line)
            for match in LINK_RE.finditer(scrubbed):
                target = match.group(2).strip()
                if not looks_like_path_target(target):
                    continue
                resolved = resolve_link(f, target)
                if resolved is None or not resolved.exists():
                    continue
                if resolved.suffix.lower() != ".md":
                    continue
                index.setdefault(resolved, set()).add(f)
    return index


def check_backlinks_region_prose(files: list[Path]) -> list[tuple[Path, int, str, str]]:
    """Hand-written prose trapped inside the auto-generated backlinks region.

    build_backlinks.py's splice_footer keeps only `content[:start] + footer + content[end:]`, so
    ANYTHING between the markers is destroyed on its next run — silently, with no error and no
    conflict. Found 2026-08-09 in docs/features/enlistment.md, which had 51 lines of a live-session
    record sitting below the start marker with a regeneration already armed (the file had gained a
    4th inbound reference while the footer still listed 3).

    The region may legitimately contain only: blank lines, the `## Referenced by` heading, and the
    generated `- [label](path)` list. Anything else is someone's writing about to be deleted.
    """
    findings: list[tuple[Path, int, str, str]] = []
    start_re = re.compile(r"<!--\s*backlinks-start")
    end_re = re.compile(r"<!--\s*backlinks-end")

    for f in files:
        # docs/reviews/raw/ is gitignored verbatim tool output (see is_never_committed_target).
        # Nobody hand-writes prose there to lose, and the transcripts routinely QUOTE a backlinks
        # footer — 11 false positives came from exactly that, and switching to the generator's
        # rfind semantics did not clear them because the quoted pair IS the last pair in those
        # files. The right answer is that this check is about authored docs.
        if is_never_committed_target(f):
            continue

        try:
            lines = f.read_text(encoding="utf-8").splitlines()
        except (OSError, UnicodeDecodeError):
            continue

        # LAST marker pair, not the first — build_backlinks.py's splice_footer uses rfind for
        # exactly this reason ("so footer-shaped examples inside fenced code blocks don't shadow
        # the real footer"). The linter has to identify the SAME region the generator will
        # rewrite, or it reports on text nothing is going to touch. First-match cost 11 false
        # positives on raw Codex transcripts that merely quote a footer.
        start = end = None
        for i, line in enumerate(lines):
            if start_re.search(line):
                start = i
        if start is not None:
            for i in range(len(lines) - 1, start, -1):
                if end_re.search(lines[i]):
                    end = i
                    break
        if start is None or end is None:
            continue

        for i in range(start + 1, end):
            stripped = lines[i].strip()
            if not stripped:
                continue
            if stripped == "## Referenced by":
                continue
            if stripped.startswith("- [") and "](" in stripped:
                continue
            findings.append((f, i + 1, "backlinks-prose", stripped[:120]))

    return findings


def check_orphan_features(files: list[Path]) -> list[Path]:
    """Feature docs that no other doc references."""
    inbound = build_inbound_reference_index(files)
    orphans: list[Path] = []
    if not FEATURES_DIR.is_dir():
        return orphans
    for f in sorted(FEATURES_DIR.glob("*.md")):
        if f.name == "TEMPLATE.md":
            continue
        # Self-references don't count
        refs = {r for r in inbound.get(f.resolve(), set()) if r != f}
        if not refs:
            orphans.append(f)
    return orphans


def _crlf_size(text: str) -> int:
    """Size of a text as a CRLF worktree holds it, so the index and the disk agree (the cap is stated for the disk)."""
    norm = text.replace("\r\n", "\n")
    return len(norm.encode("utf-8")) + norm.count("\n")


def check_claude_md_budget(commit: "CommitView | None" = None) -> list[tuple[Path, int, str, str]]:
    """CLAUDE.md + AGENTS.md eager-load budgets: total size (both) + CLAUDE.md per-line caps, on what
    the commit records with one in view.

    Findings are (file, lineno, kind, message). lineno 0 = whole-file finding.
    Fenced code blocks are exempt from line caps (commands/JSON wrap awkwardly).
    AGENTS.md gets a size-only budget (Codex-truncation guard, see AGENTS_MD_* constants).
    """
    findings: list[tuple[Path, int, str, str]] = []
    text = _commit_text(CLAUDE_MD, commit)
    if text is None:
        return findings
    size = _crlf_size(text) if commit is not None else CLAUDE_MD.stat().st_size
    if size > CLAUDE_MD_MAX_BYTES:
        findings.append((CLAUDE_MD, 0, "size",
                         f"CLAUDE.md is {size:,} B — over the {CLAUDE_MD_MAX_BYTES:,} B budget. "
                         f"Move detail to docs/features/, docs/reference/, or a paths:-scoped rule."))
    elif size > CLAUDE_MD_WARN_BYTES:
        findings.append((CLAUDE_MD, 0, "size-warn",
                         f"CLAUDE.md is {size:,} B — approaching the {CLAUDE_MD_MAX_BYTES:,} B budget "
                         f"(warn threshold {CLAUDE_MD_WARN_BYTES:,} B)."))
    for lineno, line in iter_lines_outside_code_fences(text):
        stripped = line.rstrip("\n")
        if stripped.lstrip().startswith("|"):
            if len(stripped) > CLAUDE_MD_MAX_TABLE_ROW:
                findings.append((CLAUDE_MD, lineno, "table-row",
                                 f"table row is {len(stripped)} chars (cap {CLAUDE_MD_MAX_TABLE_ROW}) — "
                                 f"a row is an index entry; the prose belongs in the linked doc."))
        elif len(stripped) > CLAUDE_MD_MAX_PROSE_LINE:
            findings.append((CLAUDE_MD, lineno, "prose-line",
                             f"line is {len(stripped)} chars (cap {CLAUDE_MD_MAX_PROSE_LINE}) — "
                             f"move the detail to a doc and keep a pointer."))

    # AGENTS.md size budget: Codex truncates it at project_doc_max_bytes; keep it small so the
    # review RULES stay early. The catch that would have caught the 2026-07-18 truncation.
    atext = _commit_text(AGENTS_MD, commit)
    if atext is not None:
        asize = _crlf_size(atext) if commit is not None else AGENTS_MD.stat().st_size
        if asize > AGENTS_MD_MAX_BYTES:
            findings.append((AGENTS_MD, 0, "size",
                             f"AGENTS.md is {asize:,} B — over the {AGENTS_MD_MAX_BYTES:,} B budget. "
                             f"Codex truncates at project_doc_max_bytes; move worked-examples to "
                             f"docs/reviews/codex-track-record.md and keep the RULES early."))
        elif asize > AGENTS_MD_WARN_BYTES:
            findings.append((AGENTS_MD, 0, "size-warn",
                             f"AGENTS.md is {asize:,} B — approaching the {AGENTS_MD_MAX_BYTES:,} B "
                             f"budget (warn {AGENTS_MD_WARN_BYTES:,} B)."))
    return findings

    return findings


# --------------------------------------------------------------------------- rule text drift
#
# Three checks on the text that instructs agents (rules, agents, skills, ai-includes). They gate
# --fail-on-drift, because each names something an agent would then act on and that is not there.
# With a commit in view they judge only the files it changes, as it records them. The commit hooks'
# trigger lists must cover each folder scanned here (.claude/hooks/check-doc-config-drift.sh).

DEAD_MEMORY_DIRS = (".claude/rules", ".claude/agents", ".claude/skills", "docs/ai-includes", "docs/reference/engine")
RULES_DIR_REL = ".claude/rules"
# Memory files are machine-local, so no other client or machine can read a citation of one.
# Lessons, RCAs and ADRs are history and are not scanned.
_DEAD_MEMORY_RE = re.compile(r"feedback[_-]\w")
SYMBOL_SUFFIXES = ("Service", "Adapter", "Factory", "Provider", "Tests", "Hook")
_SYMBOL_RE = re.compile(r"`([A-Z][A-Za-z0-9]*(?:" + "|".join(SYMBOL_SUFFIXES) + r"))`")
# A line with this marker names a type on purpose as an example, so it need not exist.
_ILLUSTRATIVE_RE = re.compile(r"<!--\s*illustrative-name\s*-->")
DOTNET_FLAG_DIRS = ("docs/modding", "docs/reference", "docs/ai-includes", ".claude/skills",
                    ".claude/rules", ".claude/agents", ".ai", ".agents")
_DOTNET_TARGET_RE = re.compile(
    r"\bdotnet\s+(?:test|build)\s+(?:-\S+\s+(?:[^-\s]\S*\s+)?)*[\"']?(?:\S*TAOM\S*|Main\b\S*)")
_LINE_CONTINUATIONS = ("\\", "`", "^")


def _md_files_under(rels) -> list[Path]:
    out: list[Path] = []
    for d in rels:
        base = REPO_ROOT / d
        if base.is_dir():
            out.extend(sorted(p for p in base.rglob("*.md") if p.is_file()))
    return out


def _md_texts_under(rels, commit: CommitView | None) -> list[tuple[Path, str]]:
    """(file, text) of the markdown under rels to judge: every working copy with no commit in view,
    else only the files the commit changes, as it records them. A deleted file is skipped."""
    if commit is None:
        return [(p, p.read_text(encoding="utf-8", errors="replace")) for p in _md_files_under(rels)]
    prefixes = tuple(d + "/" for d in rels)
    paths = [REPO_ROOT / n for n in sorted(commit.names) if n.endswith(".md") and n.startswith(prefixes)]
    commit.load(paths)
    return [(p, t) for p, t in ((p, commit.read(p)) for p in paths) if t is not None]


def check_dead_memory_refs(commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    for path, text in _md_texts_under(DEAD_MEMORY_DIRS, commit):
        for lineno, line in enumerate(text.splitlines(), start=1):
            if _DEAD_MEMORY_RE.search(line):
                findings.append((path, lineno, "dead-memory-ref",
                                 "cites a machine-local memory file; delete the citation and keep "
                                 "the rule text."))
    return findings


def check_rule_symbols(adrs: bool = False,
                       commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    """Backticked names ending in Service, Adapter, Factory, Provider, Tests or Hook, outside code
    fences, that no tracked .cs file or tools .py file contains. adrs=True scans docs/adrs/ instead
    of .claude/rules/ and tags the findings `adr-symbol` (report-only: an ADR keeps history). A name
    resolves against what git tracks, the index for a plain commit, so a file staged in the same
    commit counts, and an untracked one does not. Outside a git tree it searches the files on disk."""
    kind = "adr-symbol" if adrs else "rule-symbol"
    hits: list[tuple[Path, int, str]] = []
    if adrs:
        texts = [(p, p.read_text(encoding="utf-8", errors="replace")) for p in sorted(ADRS_DIR.glob("*.md"))]
    else:
        texts = _md_texts_under((RULES_DIR_REL,), commit)
    for path, text in texts:
        for lineno, line in iter_lines_outside_code_fences(text):
            if _ILLUSTRATIVE_RE.search(line):
                continue
            for m in _SYMBOL_RE.finditer(line):
                hits.append((path, lineno, m.group(1)))
    pending = {name for _, _, name in hits}
    if pending:
        args = ("-h", "-o", "-w", "-F", *(x for n in sorted(pending) for x in ("-e", n)),
                "--", "*.cs", "tools/*.py")
        found = _git_run("grep", *(("--cached",) if commit is not None and commit.from_index else ()), *args)
        if found is not None and found.returncode > 1:
            found = _git_run("grep", "--no-index", *args)
        if found is None or found.returncode > 1:     # a tool fault is no finding
            return []
        pending -= set(found.stdout.split())
    return [(path, lineno, kind,
             f"`{name}` is in no tracked .cs file or tools .py file; name a real symbol, or mark "
             f"an example line with <!-- illustrative-name -->.")
            for path, lineno, name in hits if name in pending]


def check_unflagged_dotnet(commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    """`dotnet test|build` of a TAOM target with no `-p:ModuleId=` in the command, a command wrapped
    with a line continuation read whole: the post-build step deploys into a game install that may be
    running."""
    findings: list[tuple[Path, int, str, str]] = []
    for path, text in _md_texts_under(DOTNET_FLAG_DIRS, commit):
        lines = text.splitlines()
        for i, line in enumerate(lines):
            if not _DOTNET_TARGET_RE.search(line):
                continue
            command, j = line, i
            while command.rstrip().endswith(_LINE_CONTINUATIONS) and j + 1 < len(lines):
                j += 1
                command = lines[j]
                line += " " + command
            if "ModuleId" not in line:
                findings.append((path, i + 1, "unflagged-dotnet",
                                 "add -p:DisableModuleCopy=true -p:ModuleId= so the command does "
                                 "not deploy into the game install."))
    return findings


# --------------------------------------------------------------------------- model registry
#
# Two files catalogue TAOM's GameModel overrides, and neither is ordinary documentation.
# `.claude/rules/gamemodels.md` carries `paths: Main/Features/**/Models/*.cs`, so it is loaded
# automatically whenever a model is edited — its table IS the briefing the next person gets,
# including the cross-entity-propagation rule written after the NavalTravel #296 RCA. A model
# missing from it is a model that rule never reaches. `docs/reference/gamemodel-registry.md`
# opens with "Every TAOM GameModel override", which is a claim a checker can hold it to.
#
# Both directions matter, and so does the count in the prose: a hardcoded "TAOM has N overrides"
# is exactly the shape that rots, and it rots silently because nobody recounts 40 table rows.

MODEL_REGISTRY_DOC = DOCS_DIR / "reference" / "gamemodel-registry.md"
MODELS_SEARCH_ROOT = REPO_ROOT / "Main"

# A TAOM model is a class named Taom*Model deriving from something whose name ends in Model —
# Default*, Sandbox*, or one of TAOM's own abstract intermediates.
MODEL_CLASS_RE = re.compile(
    r"^[ \t]*(?:public\s+|internal\s+)?(?:sealed\s+|abstract\s+|partial\s+)*"
    r"class\s+(Taom\w*Model)\s*:\s*(\w*Model)\b", re.M)
MODEL_MENTION_RE = re.compile(r"`(Taom\w*Model)`")
MODEL_COUNT_CLAIM_RE = re.compile(r"TAOM has (\d+) GameModel overrides")


def discover_model_classes() -> dict[str, Path]:
    """Every Taom*Model in Main/, mapped to the file that declares it."""
    found: dict[str, Path] = {}
    if not MODELS_SEARCH_ROOT.is_dir():
        return found
    for path in MODELS_SEARCH_ROOT.rglob("*.cs"):
        parts = set(path.parts)
        if "obj" in parts or "bin" in parts:
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for name, _base in MODEL_CLASS_RE.findall(text):
            found.setdefault(name, path)
    return found


def _line_of(text: str, needle: str) -> int:
    idx = text.find(needle)
    return text.count("\n", 0, idx) + 1 if idx >= 0 else 0


def check_model_registry() -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    in_code = discover_model_classes()
    if not in_code:
        # No models found at all means the search root moved, not that the catalogues are clean.
        # Staying silent here would turn a broken checker into a green one.
        findings.append((MODELS_SEARCH_ROOT, 0, "no-models",
                         f"found no Taom*Model classes under {rel(MODELS_SEARCH_ROOT)} — "
                         "the search root moved, so this check cannot vouch for anything"))
        return findings

    doc = MODEL_REGISTRY_DOC
    if not doc.is_file():
        findings.append((doc, 0, "missing-doc", f"{rel(doc)} does not exist"))
        return findings
    text = doc.read_text(encoding="utf-8", errors="replace")
    listed = set(MODEL_MENTION_RE.findall(text))

    for name in sorted(set(in_code) - listed):
        findings.append((doc, 0, "unlisted-model",
                         f"`{name}` ({rel(in_code[name])}) is not in {rel(doc)}"))
    for name in sorted(listed - set(in_code)):
        findings.append((doc, _line_of(text, f"`{name}`"), "phantom-model",
                         f"`{name}` is listed in {rel(doc)} but no such class exists"))

    for m in MODEL_COUNT_CLAIM_RE.finditer(text):
        claimed = int(m.group(1))
        if claimed != len(in_code):
            findings.append((doc, text.count("\n", 0, m.start()) + 1, "model-count",
                             f"claims {claimed} GameModel overrides; {len(in_code)} exist"))
    return findings


# --- AI-writing tell: em / en dashes in produced prose ----------------------
# .claude/rules/output-style.md Part 2 bans U+2014 and U+2013 from prose Claude
# produces (commits, CHANGELOG, issues, docs). Hyphens stay legal: `--RunTests`,
# `v1.4.8` and `check-freeze.sh` are everywhere and matching them would make the
# check unusable.
#
# Scope is NEW WRITING ONLY. 40,476 em dashes already sit in the tree (CHANGELOG
# 1,604 / docs 37,448 / .claude 1,424, counted 2026-08-11) from the years the
# house style kept them deliberately. A whole-tree check would report all of them
# and be ignored within a day, so this one reads git: added lines vs a base ref,
# plus untracked markdown in full.
DASH_CHARS = {"—": "em-dash", "–": "en-dash"}
_INLINE_CODE_RE = re.compile(r"(`+).*?\1")
_LINK_TARGET_RE = re.compile(r"\]\([^)]*\)")
_BARE_URL_RE = re.compile(r"<?https?://\S+")
# Escape hatch for text quoted verbatim from outside TAOM (a vanilla engine
# string, an upstream README). Rewriting a quote to strip a dash falsifies it.
_DASH_ALLOW_RE = re.compile(r"<!--\s*lint-allow-dash\s*-->")


def scan_text_for_dashes(
    path: Path, text: str, only_lines: set[int] | None = None
) -> list[tuple[Path, int, str, str]]:
    """Report em/en dashes in prose. only_lines=None scans the whole file.

    Fence state is tracked across the WHOLE file (iter_lines_outside_code_fences) even when
    only_lines restricts what is reported, because an added line inside a code block is only
    identifiable from the lines above it.
    """
    findings: list[tuple[Path, int, str, str]] = []
    for lineno, raw in iter_lines_outside_code_fences(text):
        if only_lines is not None and lineno not in only_lines:
            continue
        if _DASH_ALLOW_RE.search(raw):
            continue
        line = _INLINE_CODE_RE.sub(" ", raw)
        line = _LINK_TARGET_RE.sub(" ", line)
        line = _BARE_URL_RE.sub(" ", line)
        for ch, kind in DASH_CHARS.items():
            if ch in line:
                findings.append((path, lineno, kind, raw.strip()[:120]))
                break  # one finding per line keeps the report readable
    return findings


_EMPTY_TREE = "4b825dc642cb6eb9a060e54bf8d69288fbee4904"


def _git_run(*args: str) -> subprocess.CompletedProcess | None:
    """Run git in REPO_ROOT. None when git cannot be run at all (not installed, not executable):
    the new-lines checks then fail open, with a note on stderr, because a machine without git has
    nothing to diff. A git that ran and failed comes back with its return code (#987)."""
    try:
        return subprocess.run(
            ["git", "-C", str(REPO_ROOT), *args],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    except Exception as exc:
        print(f"lint_docs: git could not be run ({exc}); the new-lines checks (dashes, RCA "
              f"FOLLOW-UP) read nothing.", file=sys.stderr)
        return None


def _git_out(*args: str) -> str | None:
    """Run git in REPO_ROOT. Returns None on any failure (fail open)."""
    proc = _git_run(*args)
    return proc.stdout if proc is not None and proc.returncode == 0 else None


def _is_unborn_head() -> bool:
    """True when the repository has no commit yet (`rev-parse HEAD^{commit}` exits 1, silent)."""
    proc = _git_run("rev-parse", "--verify", "-q", "HEAD^{commit}")
    return proc is not None and proc.returncode == 1 and not proc.stdout.strip()


def _diff_failure(path: Path, base: str, proc: subprocess.CompletedProcess) -> tuple[Path, int, str, str]:
    detail = (proc.stderr or "").strip().splitlines()
    why = detail[0][:100] if detail else f"exit {proc.returncode}"
    return (path, 0, "git-diff-failed",
            f"git diff against {base!r} failed ({why}); the new-lines checks cannot run")


_HUNK_RE = re.compile(r"^@@ -\S+ \+(\d+)(?:,(\d+))? @@")


def _changed_markdown_lines(
    base: str, staged: bool = False, pathspec: str | list[str] = "*.md",
    failures: list[tuple[Path, int, str, str]] | None = None,
) -> dict[Path, set[int] | None]:
    """Added line numbers per file matching `pathspec` (one or several). None means 'the whole
    file is new'. A git diff that ran and failed (an unresolvable base) appends a git-diff-failed
    finding to `failures`; git not runnable at all appends nothing (fail open).

    staged=True diffs the index against `base` (`git diff --cached`) and skips the untracked
    listing: what a commit would record, not what is on disk. A file renamed into the pathspec
    from outside it has no visible source, so it reads as new and is reported in full.
    """
    changed: dict[Path, set[int] | None] = {}
    cached = ["--cached"] if staged else []
    specs = [pathspec] if isinstance(pathspec, str) else list(pathspec)
    proc = _git_run("-c", "core.quotePath=false", "diff", *cached, "-U0", base, "--", *specs)
    if proc is not None and proc.returncode != 0 and base == "HEAD" and _is_unborn_head():
        # The first commit of a repository is read in full, against the empty tree.
        base = _EMPTY_TREE
        proc = _git_run("-c", "core.quotePath=false", "diff", *cached, "-U0", base, "--", *specs)
    diff = proc.stdout if proc is not None and proc.returncode == 0 else None
    if proc is not None and proc.returncode != 0 and failures is not None:
        inside = _git_run("rev-parse", "--is-inside-work-tree")
        if inside is not None and inside.returncode == 0:
            failure = _diff_failure(REPO_ROOT, base, proc)
            if failure not in failures:
                failures.append(failure)
        else:  # no repository (an export, a tarball): nothing to diff, like no git at all
            print(f"lint_docs: {REPO_ROOT} is not a git work tree; the new-lines checks (dashes, "
                  f"RCA FOLLOW-UP) read nothing.", file=sys.stderr)
    if diff:
        current: Path | None = None
        prev = ""
        for line in diff.splitlines():
            # A `+++ b/path` header is always preceded by `--- a/path`. Checking
            # that guards against an ADDED CONTENT line reading `++ foo`, which
            # the `+` diff prefix turns into a convincing `+++ foo`.
            if line.startswith("+++ ") and prev.startswith("--- "):
                target = line[4:].strip()
                if target == "/dev/null":
                    current = None
                else:
                    current = REPO_ROOT / target[2:] if target.startswith("b/") else REPO_ROOT / target
                    changed.setdefault(current, set())
            elif line.startswith("diff --git "):
                current = None
            elif current is not None:
                hunk = _HUNK_RE.match(line)
                if hunk:
                    start = int(hunk.group(1))
                    count = 1 if hunk.group(2) is None else int(hunk.group(2))
                    lines = changed[current]
                    if lines is not None:
                        lines.update(range(start, start + count))
            prev = line
    untracked = None if staged else _git_out(
        "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard", "--", *specs
    )
    if untracked:
        for name in untracked.splitlines():
            name = name.strip()
            if name:
                changed[REPO_ROOT / name] = None
    return changed


def check_ai_dashes(
    base: str = "HEAD", failures: list[tuple[Path, int, str, str]] | None = None
) -> list[tuple[Path, int, str, str]]:
    """Em and en dashes in the new lines of the markdown docs. A diff that failed appends a
    git-diff-failed finding to `failures`, not to the result."""
    findings: list[tuple[Path, int, str, str]] = []
    failures = [] if failures is None else failures
    changed = _changed_markdown_lines(base, failures=failures)
    for path, lines in sorted(changed.items(), key=lambda kv: str(kv[0])):
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except Exception:
            continue
        findings.extend(scan_text_for_dashes(path, text, lines))
    return findings


# --- RCA FOLLOW-UP lines must cite an issue or a reason ---------------------
#
# A deferred finding written as a bare FOLLOW-UP is lost (#984): three times a
# real defect sat in an RCA with no owner until an issue was filed. The uppercase
# word is the project's marker for a deferral; lowercase "follow-up" is used
# loosely and is ignored. Like the dash check, only NEW lines are reported.
#
# Matching runs on the line with inline code spans and link targets removed (as
# the dash check does), so a passing mention in backticks is exempt and a link
# anchor never counts as an issue. A link target that points at /issues/<n> is
# kept for the issue match: a Markdown link to the issue is a citation.
_FOLLOWUP_RE = re.compile(r"\bFOLLOW-UPS?\b")
# A `#<number>` that is a real reference: not C#7, not an HTML entity (&#39;).
_ISSUE_REF_RE = re.compile(r"(?<![\w&])#\d+\b|/issues/\d+\b")
# `#<number>` after a label that numbers something else, and a hex colour.
_NOT_AN_ISSUE_RE = re.compile(
    r"\b(?:finding|crash|case|step|item|row|line)s?\s+#\d+\b|(?<![\w&])#[0-9a-fA-F]{6}\b",
    re.IGNORECASE,
)
# The house phrasings: "no issue: <reason>", "no issue filed, <reason>", "no issues filed,
# since <reason>", "no issue filed (public action)", "no issue filed from this review lead, since
# <reason>", and the deep-review template's "none is filed because <reason>". Up to eight plain
# words may sit between the phrase and its reason marker (`:` `;` `,` `(` because since); a
# sentence end does not count as a marker, so "with no issue." stays bare. The marker must be
# followed by text that is not a cell edge.
_NO_ISSUE_RE = re.compile(
    r"\b(?:no issues?(?: filed)?|none is filed)(?:\s+[^\s|.:;,()]+){0,8}?\s*"
    r"(?:[:;,(]|\s(?:because|since)\b)\s*[^\s|)]",
    re.IGNORECASE,
)
_ISSUE_LINK_TARGET_RE = re.compile(r"/issues/\d+\b")
_RCA_PATHSPEC = "docs/reviews/rca-*.md"


def _is_rca_doc(path: Path) -> bool:
    return path.parent == REPO_ROOT / "docs" / "reviews" and path.name.startswith("rca-") \
        and path.suffix == ".md"


def _followup_owner_prose(raw: str) -> str:
    prose = _INLINE_CODE_RE.sub(" ", raw)
    # A link target is dropped, except an /issues/<n> inside it.
    prose = _LINK_TARGET_RE.sub(
        lambda m: " " + " ".join(_ISSUE_LINK_TARGET_RE.findall(m.group(0))) + " ", prose)
    return _NOT_AN_ISSUE_RE.sub(" ", prose)


def _followup_has_issue(raw: str) -> bool:
    return bool(_ISSUE_REF_RE.search(_followup_owner_prose(raw)))


def _followup_has_owner(raw: str) -> bool:
    prose = _followup_owner_prose(raw)
    return bool(_ISSUE_REF_RE.search(prose) or _NO_ISSUE_RE.search(prose))


def scan_text_for_followups(
    path: Path, text: str, only_lines: set[int] | None = None
) -> list[tuple[Path, int, str, str]]:
    """Report FOLLOW-UP lines with neither an issue reference nor `no issue: <reason>`.

    A table row is one line, so an issue in another cell satisfies it. Fenced blocks
    are skipped by iter_lines_outside_code_fences; the whole file is walked even when
    only_lines restricts what is reported, because an added line inside a code block is
    only identifiable from the lines above it.
    """
    findings: list[tuple[Path, int, str, str]] = []
    for lineno, raw in iter_lines_outside_code_fences(text):
        if only_lines is not None and lineno not in only_lines:
            continue
        prose = _INLINE_CODE_RE.sub(" ", raw)
        prose = _LINK_TARGET_RE.sub(" ", prose)
        if not _FOLLOWUP_RE.search(prose):
            continue
        if _followup_has_owner(raw):
            continue
        findings.append((path, lineno, "followup-no-owner", raw.strip()[:120]))
    return findings


def list_unfiled_followups() -> list[tuple[Path, int, str, str]]:
    """Every FOLLOW-UP line in docs/reviews/rca-*.md that cites no issue number: the backlog the
    gate lets through with `no issue: <reason>`. The gate itself is check_rca_followups."""
    rows: list[tuple[Path, int, str, str]] = []
    reviews = REPO_ROOT / "docs" / "reviews"
    if not reviews.is_dir():
        return rows
    for path in sorted(reviews.glob("rca-*.md")):
        text = path.read_text(encoding="utf-8-sig", errors="replace")
        for lineno, raw in iter_lines_outside_code_fences(text):
            prose = _LINK_TARGET_RE.sub(" ", _INLINE_CODE_RE.sub(" ", raw))
            if _FOLLOWUP_RE.search(prose) and not _followup_has_issue(raw):
                kind = "reason" if _followup_has_owner(raw) else "bare"
                rows.append((path, lineno, kind, raw.strip()[:160]))
    return rows


def check_rca_followups(
    base: str = "HEAD", staged: bool = False, only_paths: list[str] | None = None,
    failures: list[tuple[Path, int, str, str]] | None = None,
) -> list[tuple[Path, int, str, str]]:
    """Bare FOLLOW-UP lines in the new lines of docs/reviews/rca-*.md. staged=True reads the
    index (`git diff --cached`, `git show :<path>`) instead of the working tree. only_paths
    narrows the check to those paths' working copies (`git commit -- <paths>` records them, not
    the index), whatever staged says; only the RCA docs among them are diffed. An unresolvable
    base appends a git-diff-failed finding to `failures`, not to the result."""
    findings: list[tuple[Path, int, str, str]] = []
    failures = [] if failures is None else failures
    pathspec: str | list[str] = _RCA_PATHSPEC
    if only_paths:
        staged = False
        listed = _git_out("-c", "core.quotePath=false", "ls-files", "--", *only_paths) or ""
        pathspec = [n for n in dict.fromkeys(x.strip() for x in listed.splitlines())
                    if n and _is_rca_doc(REPO_ROOT / n)]
        if not pathspec:
            return findings
    changed = _changed_markdown_lines(base, staged=staged, pathspec=pathspec, failures=failures)
    for path, lines in sorted(changed.items(), key=lambda kv: str(kv[0])):
        if not _is_rca_doc(path):
            continue
        if staged:
            text = _git_out("show", f":{path.relative_to(REPO_ROOT).as_posix()}")
            if text is None:
                continue
            text = text.lstrip("\ufeff")
        else:
            if not path.is_file():
                continue
            try:
                text = path.read_text(encoding="utf-8-sig")
            except Exception:
                continue
        findings.extend(scan_text_for_followups(path, text, lines))
    return findings


VERIFY_HEADING = "## How to verify in game"
_FEATURE_DOC_SKIP = {"TEMPLATE.md", "README.md"}
_FEATURE_DOC_PATHSPEC = "docs/features/*.md"


def _has_verify_section(text: str) -> bool:
    """True when `## How to verify in game` sits outside a code fence and its section (up to the
    next `## ` heading or the end of the file) holds at least one non-blank line."""
    text = text.lstrip("\ufeff")
    lines = text.splitlines()
    outside = dict(iter_lines_outside_code_fences(text))
    for lineno, line in outside.items():
        if line.rstrip() != VERIFY_HEADING:
            continue
        for i in range(lineno + 1, len(lines) + 1):
            if i in outside and outside[i].startswith("## "):
                break
            if lines[i - 1].strip():
                return True
        return False
    return False


def _merge_heads() -> list[str]:
    """The commits an in-progress merge brings in (MERGE_HEAD, one per line), or none."""
    path = _git_out("rev-parse", "--git-path", "MERGE_HEAD")
    if not path:
        return []
    merge_head = Path(path.strip())
    if not merge_head.is_absolute():
        merge_head = REPO_ROOT / merge_head
    try:
        return [ln.strip() for ln in merge_head.read_text(encoding="utf-8").splitlines() if ln.strip()]
    except OSError:
        return []


def check_feature_doc_verify_section(
    base: str = "HEAD", staged: bool = False, also_new: list[str] | None = None,
    only_paths: list[str] | None = None,
) -> list[tuple[Path, int, str, str]]:
    """A NEW docs/features/*.md with no `## How to verify in game` section that has a body.

    New means ADDED against `base` (`git diff --name-status --diff-filter=A`; staged=True diffs the
    index and reads the file from it). Working-tree mode also counts untracked files. A modified
    existing doc is never checked, and neither are TEMPLATE.md, README.md and a subdirectory doc.
    `also_new` names paths a pending `git add` in the same command will add: each one absent from
    HEAD and present in the working tree is checked from the working tree. `only_paths` narrows the
    check to those paths' working copies, whatever `staged` says: what `git commit -- <paths>` records.
    """
    names: list[str] = []
    if only_paths:
        staged = False
        listed = _git_out("-c", "core.quotePath=false", "ls-files", "--", *only_paths) or ""
        known = set((_git_out("-c", "core.quotePath=false", "ls-tree", "-r", "--name-only", base,
                              "--", "docs/features/") or "").splitlines())
        names = [n for n in dict.fromkeys(x.strip() for x in listed.splitlines())
                 if n.startswith("docs/features/") and n not in known]
    else:
        cached = ["--cached"] if staged else []
        listing = _git_out("-c", "core.quotePath=false", "diff", *cached, "--name-status",
                           "--diff-filter=A", base, "--", _FEATURE_DOC_PATHSPEC)
        for line in (listing or "").splitlines():
            parts = line.split("\t")
            if len(parts) >= 2:
                names.append(parts[-1].strip())
        if not staged:
            untracked = _git_out("-c", "core.quotePath=false", "ls-files", "--others",
                                 "--exclude-standard", "--", _FEATURE_DOC_PATHSPEC)
            names.extend(n.strip() for n in (untracked or "").splitlines() if n.strip())
    # During a merge, a doc the merged branch already has is not new to this change.
    merge_parents = _merge_heads()
    names = [n for n in names
             if not any(_git_out("cat-file", "-e", f"{sha}:{n}") is not None for sha in merge_parents)]
    pending: set[str] = set()
    for name in also_new or []:
        name = name.strip().replace("\\", "/")
        path = REPO_ROOT / name
        if path.parent != REPO_ROOT / "docs" / "features" or path.suffix != ".md" \
                or not path.is_file() or _git_out("cat-file", "-e", f"HEAD:{name}") is not None:
            continue
        pending.add(name)
    findings: list[tuple[Path, int, str, str]] = []
    for name in sorted(set(names) | pending):
        path = REPO_ROOT / name
        if path.parent != REPO_ROOT / "docs" / "features" or path.name in _FEATURE_DOC_SKIP:
            continue
        if staged and name not in pending:
            text = _git_out("show", f":{name}")
            if text is None:
                continue
        else:
            try:
                text = path.read_text(encoding="utf-8-sig")
            except Exception:
                continue
        if _has_verify_section(text):
            continue
        findings.append((path, 1, "no-verify-section",
                         f"the doc lacks a `{VERIFY_HEADING}` section with a body (see "
                         "docs/features/TEMPLATE.md); "
                         'write the steps, or write "Not applicable:" with a reason under the heading'))
    return findings


def rel(p: Path) -> str:
    try:
        return str(p.relative_to(REPO_ROOT)).replace("\\", "/")
    except ValueError:
        return str(p)


def format_report(report: LintReport, quick: bool, drift_only: bool = False) -> str:
    out: list[str] = []
    out.append("# Doc lint report")
    out.append("")
    gating = [
        f"- Config-example drift (doc JSON != shipped ModuleData config): **{len(report.config_drift)}**",
        f"- Version mismatches (Target line / snapshot != pin): **{len(report.version_mismatches)}**",
        f"- CLAUDE.md budget (size/row/line caps{'' if CLAUDE_MD_BUDGET_ENFORCE else ', warn-only'}): **{len(report.budget)}**",
        f"- RCA FOLLOW-UP lines with no issue or reason (new lines only): **{len(report.rca_followups)}**",
        f"- New feature docs with no `{VERIFY_HEADING}` section: **{len(report.feature_verify)}**",
        f"- Git diff failed, so the new-lines checks could not run: **{len(report.git_diff_failures)}**",
        f"- Rule text drift (dead memory citations, missing symbols, unflagged dotnet): **{len(report.rule_drift)}**",
    ]
    if drift_only:
        out.append("Only the checks `--fail-on-drift` gates on ran (`--drift-only`); run without it for the rest.")
        out.append(f"- Config-example drift (doc JSON != shipped ModuleData config): **{len(report.config_drift)}**")
        out.append(f"- Version mismatches (CLAUDE.md / snapshot != pin): **{len(report.version_mismatches)}**")
        out.append(f"- CLAUDE.md budget (size/row/line caps{'' if CLAUDE_MD_BUDGET_ENFORCE else ', warn-only'}): **{len(report.budget)}**")
        out.append(f"- RCA FOLLOW-UP lines with no issue or reason (new lines only): **{len(report.rca_followups)}**")
        out.append(f"- New feature docs with no `{VERIFY_HEADING}` section: **{len(report.feature_verify)}**")
        out.append(f"- Git diff failed, so the new-lines checks could not run: **{len(report.git_diff_failures)}**")
    else:
        out.append(f"- Dead links: **{len(report.dead_links)}**")
        out.append(f"- Link targets present but UNTRACKED (dead on the remote): "
                   f"**{len(report.untracked_targets)}**")
    if not quick and not drift_only:
        out.append(f"- Stale version refs (outside migration/archive): **{len(report.stale_versions)}**")
        out.append(f"- Orphan feature docs (no inbound references): **{len(report.orphan_features)}**")
        out.append(f"- Prose trapped in an auto-generated backlinks region: **{len(report.backlinks_prose)}**")
        out.append(f"- Missing feature docs (Main/Features/<X> with no docs/features/<x>.md): **{len(report.missing_feature_docs)}**")
        out.extend(gating)
        out.append(f"- GameModel registry drift (code vs the registry): **{len(report.model_registry)}**")
        out.append(f"- Em/en dashes in newly written prose: **{len(report.dashes)}**")
        out.append(f"- Missing symbols in ADRs (report-only): **{len(report.adr_symbols)}**")
    out.append("")
    if report.dead_links:
        out.append("## Dead links")
        out.append("")
        for f, lineno, target, label in report.dead_links:
            out.append(f"- `{rel(f)}:{lineno}` — `[{label}]({target})`")
        out.append("")
    if report.untracked_targets:
        out.append("## Link targets present but untracked")
        out.append("")
        out.append("These resolve on this machine and are dead for everyone else: the target "
                   "exists on disk but git is not tracking it, so a push publishes the link "
                   "without the file. `git add` the target, or drop the link. (2026-08-28: two "
                   "such links reached the remote while this check did not exist.)")
        out.append("")
        for f, lineno, target, label in report.untracked_targets:
            out.append(f"- `{rel(f)}:{lineno}` — `[{label}]({target})`")
        out.append("")
    if not quick:
        if report.stale_versions:
            out.append("## Stale version refs")
            out.append("")
            out.append("Outside `docs/migration/` and `docs/archive/` (rca-*/codex-adversarial-* files exempted as historical record):")
            out.append("")
            for f, lineno, label, line in report.stale_versions:
                out.append(f"- `{rel(f)}:{lineno}` — `{label}` — `{line}`")
            out.append("")
        if report.backlinks_prose:
            out.append("## Prose trapped in an auto-generated backlinks region")
            out.append("")
            out.append("`build_backlinks.py` replaces EVERYTHING between the markers on its next "
                       "run. These lines will be silently deleted — move them above the marker.")
            out.append("")
            for f, lineno, _kind, text in report.backlinks_prose:
                out.append(f"- `{rel(f)}:{lineno}` — {text}")
            out.append("")

        if report.orphan_features:
            out.append("## Orphan feature docs")
            out.append("")
            out.append("These feature docs have no inbound references from other docs. Either link them into INDEX.md / a feature doc / an RCA, or delete them.")
            out.append("")
            for f in report.orphan_features:
                out.append(f"- `{rel(f)}`")
            out.append("")
        if report.missing_feature_docs:
            out.append("## Missing feature docs")
            out.append("")
            out.append("These `Main/Features/<X>/` directories have no matching `docs/features/<x>.md`. Author from `docs/features/TEMPLATE.md`.")
            out.append("")
            for name, target in report.missing_feature_docs:
                out.append(f"- `{name}` → `{target}`")
            out.append("")
        if report.config_drift:
            out.append("## Config-example drift")
            out.append("")
            out.append("A feature doc's `json` example disagrees with the shipped `ModuleData` config it mirrors. "
                       "Update the doc example (or the shipped config) so they match.")
            out.append("")
            for f, lineno, _cfg, msg in report.config_drift:
                out.append(f"- `{rel(f)}:{lineno}` — {msg}")
            out.append("")
        if report.version_mismatches:
            out.append("## Version mismatches")
            out.append("")
            out.append("A committed game-version marker disagrees with `.claude/pinned-game-version.txt`. "
                       "Update it to the pin (run `/engine-bump` if the pin itself is behind the installed game).")
            out.append("")
            for f, lineno, _v, msg in report.version_mismatches:
                out.append(f"- `{rel(f)}:{lineno}` — {msg}")
            out.append("")
        if report.budget:
            out.append("## CLAUDE.md budget")
            out.append("")
            out.append("CLAUDE.md is the eager per-session context load — it stays an INDEX (thin table rows "
                       "+ doc links). Detail belongs in docs/features/, docs/reference/, or a paths:-scoped rule."
                       + ("" if CLAUDE_MD_BUDGET_ENFORCE else " (Warn-only during the decomposition migration.)"))
            out.append("")
            for f, lineno, kind, msg in report.budget:
                loc = f"`{rel(f)}:{lineno}`" if lineno else f"`{rel(f)}`"
                out.append(f"- {loc} — [{kind}] {msg}")
            out.append("")
        if report.model_registry:
            out.append("## GameModel registry drift")
            out.append("")
            out.append("`docs/reference/gamemodel-registry.md` claims to list every override and states "
                       "their total. Add the model to it, or correct the claim.")
            out.append("")
            for f, lineno, kind, msg in report.model_registry:
                loc = f"`{rel(f)}:{lineno}`" if lineno else f"`{rel(f)}`"
                out.append(f"- {loc} — [{kind}] {msg}")
            out.append("")
        if report.dashes:
            out.append("## Em/en dashes in newly written prose")
            out.append("")
            out.append("An em or en dash is the loudest AI-writing tell, so produced prose does "
                       "not use them (`.claude/rules/output-style.md` Part 2). Replace with a "
                       "comma, a colon, a semicolon, parentheses, or a sentence break. Only NEW "
                       "lines are reported; existing prose is left alone. Quoting an outside "
                       "source verbatim? Mark the line `<!-- lint-allow-dash -->`.")
            out.append("")
            for f, lineno, kind, snippet in report.dashes:
                out.append(f"- `{rel(f)}:{lineno}` — [{kind}] `{snippet}`")
            out.append("")
        if report.rca_followups:
            out.append("## RCA FOLLOW-UP lines with no issue or reason")
            out.append("")
            out.append("A deferred finding with no owner is lost (#984). Rule: a newly written "
                       "`FOLLOW-UP` line in `docs/reviews/rca-*.md` must cite an issue (`#<number>`) "
                       "or say `no issue:` and why. Only NEW lines are reported.")
            out.append("")
            for f, lineno, kind, snippet in report.rca_followups:
                out.append(f"- `{rel(f)}:{lineno}` — [{kind}] `{snippet}`")
            out.append("")
        if report.feature_verify:
            out.append("## Feature docs with no in-game verification section")
            out.append("")
            out.append(f"A newly added `docs/features/*.md` needs a `{VERIFY_HEADING}` heading "
                       "(docs/features/TEMPLATE.md). Existing docs are not checked.")
            out.append("")
            for f, lineno, kind, snippet in report.feature_verify:
                out.append(f"- `{rel(f)}:{lineno}` [{kind}] {snippet}")
            out.append("")
    if report.rule_drift:
        out.append("## Rule text drift")
        out.append("")
        out.append("Rules, agents, skills and ai-includes may not cite memory files (`feedback_*`), "
                   "name a `...Service`, `...Adapter`, `...Factory`, `...Provider`, `...Tests` or "
                   "`...Hook` that exists in no tracked .cs or tools .py file, or give a `dotnet` "
                   "command for a TAOM project without `-p:ModuleId=`.")
        out.append("")
        for f, lineno, kind, msg in report.rule_drift:
            out.append(f"- `{rel(f)}:{lineno}` [{kind}] {msg}")
        out.append("")
    if report.adr_symbols and not quick:
        out.append("## Missing symbols in ADRs (report-only)")
        out.append("")
        out.append("An ADR keeps its historical names, so these do not gate. Read them when you edit the ADR.")
        out.append("")
        for f, lineno, kind, msg in report.adr_symbols:
            out.append(f"- `{rel(f)}:{lineno}` {msg}")
        out.append("")
    if report.git_diff_failures:
        out.append("## Git diff failed")
        out.append("")
        out.append("The new-lines checks (dashes and RCA FOLLOW-UP lines) read `git diff` against a base, "
                   "and git could not produce it, so they did not run. The base was verified first, so "
                   "this means repository damage (a broken index or a missing object): run `git status` "
                   "and `git fsck`. This gates like a finding.")
        out.append("")
        for f, lineno, kind, snippet in report.git_diff_failures:
            out.append(f"- `{rel(f)}:{lineno}` — [{kind}] `{snippet}`")
        out.append("")
    if report.total == 0:
        out.append("**Clean — no findings.**")
        out.append("")
    return "\n".join(out)


def _dash_base_resolves(ref: str) -> bool:
    """False only when git ran inside a work tree and cannot resolve REF (a commit, a tag or a tree
    id such as the empty tree). No git, no work tree, and the unborn HEAD of a fresh repository
    stay True: those fail open in the checks themselves, with their own notes."""
    proc = _git_run("rev-parse", "--verify", "-q", f"{ref}^{{tree}}")
    if proc is None or proc.returncode == 0:
        return True
    if ref == "HEAD" and _is_unborn_head():
        return True
    inside = _git_run("rev-parse", "--is-inside-work-tree")
    return inside is None or inside.returncode != 0


def _rule_drift(commit: CommitView | None = None) -> list[tuple[Path, int, str, str]]:
    return check_dead_memory_refs(commit) + check_rule_symbols(commit=commit) + check_unflagged_dotnet(commit)


def main(argv: list[str]) -> int:
    # Force UTF-8 stdout on Windows so em-dashes / unicode arrows don't garble.
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--quick", action="store_true", help="Only run the dead-link check (fastest)")
    ap.add_argument("--report", type=Path, help="Write report to this path instead of stdout (atomic via .tmp+rename)")
    ap.add_argument("--fail-on-dead", action="store_true", help="Exit 1 if any dead links found")
    ap.add_argument("--fail-on-drift", action="store_true",
                    help="Exit 1 on config-example drift, a version mismatch, a CLAUDE.md or "
                         "AGENTS.md budget breach (size-warn findings excepted), a newly written RCA FOLLOW-UP "
                         "line with no issue or reason, a new feature doc with no "
                         "'## How to verify in game' section, or rule text drift.")
    ap.add_argument("--drift-only", action="store_true",
                    help="Run only the checks --fail-on-drift gates on, and gate on them.")
    ap.add_argument("--staged", action="store_true",
                    help="Judge the tree a commit would record, read from the index, not the working "
                         "copies: every gating check. The rule text and path-rule size checks judge only "
                         "the files it changes. The commit hooks' mode.")
    ap.add_argument("--clean-merge", action="store_true",
                    help="The index is a clean merge's (.githooks/pre-merge-commit). Git writes "
                         "MERGE_HEAD only after that hook, so a doc the merged branch holds looks new: "
                         "skip the new-feature-doc check, which that branch's own commit passed.")
    ap.add_argument("--also-new", action="append", default=[], metavar="PATH",
                    help="A docs/features/*.md that a pending `git add` will add (repeatable): "
                         "checked from the working tree if HEAD lacks it.")
    ap.add_argument("--followups", action="store_true",
                    help="List every RCA FOLLOW-UP line that cites no issue number, then the "
                         "count on the last line. Report only: the gate is unchanged.")
    ap.add_argument("--summary", action="store_true", help="Emit a --- delimited grep-friendly summary block instead of the full markdown report")
    ap.add_argument("--only-paths", nargs="+", metavar="PATH", default=None,
                    help="Judge these paths' working copies on top of HEAD, not the index: what `git "
                         "commit -- PATH...` records. Give it last. Limits (the hook, not this tool): `git commit "
                         "-i/--include` and `--pathspec-from-file` are judged as index commits, and "
                         "more than ~450 named paths exceed the Windows argument limit and fall back "
                         "to the index.")
    ap.add_argument("--dash-base", default="HEAD", metavar="REF",
                    help="Base ref for the new-lines-only checks (em/en dashes, RCA FOLLOW-UP "
                         "lines; default HEAD, i.e. uncommitted writing). Pass a branch point to "
                         "scan a whole branch; a tree id such as the empty tree "
                         "counts every file as added. A ref git cannot resolve is an error: "
                         "exit 2, no report.")
    args = ap.parse_args(argv)

    if args.followups:
        rows = list_unfiled_followups()
        for f, lineno, kind, text in rows:
            sys.stdout.write(f"{rel(f)}:{lineno} [{kind}] {text}\n")
        sys.stdout.write(f"unfiled FOLLOW-UP lines: {len(rows)}\n")
        return 0

    if not _dash_base_resolves(args.dash_base):
        print(f"lint_docs: --dash-base {args.dash_base} does not resolve", file=sys.stderr)
        return 2

    files: list[Path] = []
    for root in DOC_ROOTS:
        if root.is_dir():
            files.extend(iter_markdown(root))

    report = LintReport()
    commit = commit_view(args.staged, args.only_paths, args.dash_base)
    if args.drift_only:
        args.fail_on_drift = True
    else:
        report.dead_links = check_dead_links(files)
        report.untracked_targets = check_untracked_link_targets(files)
    if not args.quick and not args.drift_only:
        report.stale_versions = check_stale_versions(files)
        report.orphan_features = check_orphan_features(files)
        report.backlinks_prose = check_backlinks_region_prose(files)
        report.missing_feature_docs = check_missing_feature_docs(feature_doc_basenames())
        report.model_registry = check_model_registry()
        report.dashes = check_ai_dashes(args.dash_base, failures=report.git_diff_failures)
        report.adr_symbols = check_rule_symbols(adrs=True)
    if args.drift_only or not args.quick:
        report.config_drift = check_config_example_drift(files, commit)
        report.version_mismatches = check_version_consistency(commit)
        report.budget = check_claude_md_budget(commit)
        report.rca_followups = check_rca_followups(
            args.dash_base, staged=args.staged, only_paths=args.only_paths,
            failures=report.git_diff_failures)
        if not args.clean_merge:
            report.feature_verify = check_feature_doc_verify_section(args.dash_base, staged=args.staged,
                                                                      also_new=args.also_new,
                                                                      only_paths=args.only_paths)
        report.rule_drift = _rule_drift(commit)

    if args.summary:
        # Structured grep-friendly block, modeled on autoresearch's train.py final output
        counters = [
            ("dead_links", report.dead_links), ("stale_versions", report.stale_versions),
            ("orphan_features", report.orphan_features), ("backlinks_prose", report.backlinks_prose),
            ("missing_features", report.missing_feature_docs), ("config_drift", report.config_drift),
            ("version_mismatch", report.version_mismatches), ("claude_budget", report.budget),
            ("model_registry", report.model_registry), ("ai_dashes", report.dashes),
            ("rca_followups", report.rca_followups), ("feature_verify", report.feature_verify),
            ("git_diff_failed", report.git_diff_failures),
            ("rule_drift", report.rule_drift), ("adr_symbols", report.adr_symbols),
        ]
        if args.drift_only:
            counters = [c for c in counters if c[0] in ("config_drift", "version_mismatch", "claude_budget", "rca_followups",
                                                  "feature_verify", "git_diff_failed", "rule_drift")]
        summary = "\n".join(["---"]
                            + [f"{(name + ':').ljust(19)}{len(items)}" for name, items in counters]
                            + [f"total_findings:    {report.total}", "---", ""])
        sys.stdout.write(summary)
    else:
        rendered = format_report(report, quick=args.quick, drift_only=args.drift_only)
        if args.report:
            # Atomic write: write to .tmp then rename. Prevents partial-write
            # corruption if interrupted mid-write (autoresearch prepare.py pattern).
            args.report.parent.mkdir(parents=True, exist_ok=True)
            tmp = args.report.with_suffix(args.report.suffix + ".tmp")
            tmp.write_text(rendered, encoding="utf-8")
            tmp.replace(args.report)
            print(f"Wrote report to {rel(args.report)} ({report.total} findings)", file=sys.stderr)
        else:
            sys.stdout.write(rendered)

    if args.fail_on_dead and report.dead_links:
        return 1
    # size-warn findings are report-only early warnings (per the WARN_BYTES constants);
    # only hard violations (size / table-row / prose-line) gate the pre-commit hook.
    gating_budget = [b for b in report.budget if b[2] != "size-warn"]
    if args.fail_on_drift and (
        report.config_drift
        or report.version_mismatches
        or (CLAUDE_MD_BUDGET_ENFORCE and gating_budget)
        or report.rca_followups
        or report.feature_verify
        or report.git_diff_failures
        or report.rule_drift
    ):
        return 1
    return 0


def entry(argv: list[str]) -> int:
    """main, with a crash as exit 3: exit 1 is a finding, and the commit hooks refuse on it."""
    try:
        return main(argv)
    except Exception:
        traceback.print_exc()
        print("lint_docs: crashed (traceback above); no verdict.", file=sys.stderr)
        return 3


if __name__ == "__main__":
    raise SystemExit(entry(sys.argv[1:]))
