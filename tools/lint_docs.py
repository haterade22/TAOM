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
import re
import subprocess
import sys
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


FENCE_RE = re.compile(r"^\s*(```|~~~)")


def iter_lines_outside_code_fences(text: str):
    """Yield (lineno, line) skipping anything inside ``` or ~~~ fenced blocks."""
    in_fence = False
    fence_marker: str | None = None
    for lineno, line in enumerate(text.splitlines(), start=1):
        m = FENCE_RE.match(line)
        if m:
            marker = m.group(1)
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = None
            continue
        if in_fence:
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


def _read_pin() -> str:
    """The pinned game version exactly as committed ('v1.4.7'); '' if absent or unreadable.

    Returned raw, not normalised: check_version_consistency quotes it verbatim in its findings.
    """
    try:
        return (REPO_ROOT / ".claude" / "pinned-game-version.txt").read_text(encoding="utf-8").strip()
    except OSError:
        return ""


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


def check_config_example_drift(files: list[Path]) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    for f in files:
        if _is_historical_doc(f):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for cfg_path, json_body, lineno in _config_json_blocks(text):
            cfg_file = (REPO_ROOT / cfg_path.replace("\\", "/")).resolve()
            if not cfg_file.exists():
                continue
            try:
                doc_json = json.loads(json_body)
            except ValueError:
                continue  # annotated / partial example — not comparable
            try:
                shipped = json.loads(cfg_file.read_text(encoding="utf-8-sig"))
            except (OSError, ValueError):
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


def check_version_consistency() -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    pin_raw = _read_pin()
    pin = _norm_ver(pin_raw)
    if not pin:
        return findings

    def check_file(path: Path, pattern: re.Pattern, what: str):
        if not path.exists():
            return
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
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


def check_claude_md_budget() -> list[tuple[Path, int, str, str]]:
    """CLAUDE.md + AGENTS.md eager-load budgets: total size (both) + CLAUDE.md per-line caps.

    Findings are (file, lineno, kind, message). lineno 0 = whole-file finding.
    Fenced code blocks are exempt from line caps (commands/JSON wrap awkwardly).
    AGENTS.md gets a size-only budget (Codex-truncation guard, see AGENTS_MD_* constants).
    """
    findings: list[tuple[Path, int, str, str]] = []
    if not CLAUDE_MD.is_file():
        return findings
    size = CLAUDE_MD.stat().st_size
    if size > CLAUDE_MD_MAX_BYTES:
        findings.append((CLAUDE_MD, 0, "size",
                         f"CLAUDE.md is {size:,} B — over the {CLAUDE_MD_MAX_BYTES:,} B budget. "
                         f"Move detail to docs/features/, docs/reference/, or a paths:-scoped rule."))
    elif size > CLAUDE_MD_WARN_BYTES:
        findings.append((CLAUDE_MD, 0, "size-warn",
                         f"CLAUDE.md is {size:,} B — approaching the {CLAUDE_MD_MAX_BYTES:,} B budget "
                         f"(warn threshold {CLAUDE_MD_WARN_BYTES:,} B)."))
    text = CLAUDE_MD.read_text(encoding="utf-8", errors="replace")
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

    # AGENTS.md size budget — Codex truncates it at project_doc_max_bytes; keep it small so the
    # review RULES stay early. The catch that would have caught the 2026-07-18 truncation.
    if AGENTS_MD.is_file():
        asize = AGENTS_MD.stat().st_size
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

MODEL_RULES_DOC = REPO_ROOT / ".claude" / "rules" / "gamemodels.md"
MODEL_REGISTRY_DOC = DOCS_DIR / "reference" / "gamemodel-registry.md"
MODELS_SEARCH_ROOT = REPO_ROOT / "Main"

# A TAOM model is a class named Taom*Model deriving from something whose name ends in Model —
# Default*, Sandbox*, or one of TAOM's own abstract intermediates.
MODEL_CLASS_RE = re.compile(
    r"^[ \t]*(?:public\s+|internal\s+)?(?:sealed\s+|abstract\s+|partial\s+)*"
    r"class\s+(Taom\w*Model)\s*:\s*(\w*Model)\b", re.M)
MODEL_MENTION_RE = re.compile(r"`(Taom\w*Model)`")
MODEL_COUNT_CLAIM_RES = (
    re.compile(r"TAOM has (\d+) GameModel overrides"),
    re.compile(r"Existing Overrides \((\d+) total\)"),
)


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

    for doc in (MODEL_RULES_DOC, MODEL_REGISTRY_DOC):
        if not doc.is_file():
            findings.append((doc, 0, "missing-doc", f"{rel(doc)} does not exist"))
            continue
        text = doc.read_text(encoding="utf-8", errors="replace")
        listed = set(MODEL_MENTION_RE.findall(text))

        for name in sorted(set(in_code) - listed):
            findings.append((doc, 0, "unlisted-model",
                             f"`{name}` ({rel(in_code[name])}) is not in {rel(doc)}"))
        for name in sorted(listed - set(in_code)):
            findings.append((doc, _line_of(text, f"`{name}`"), "phantom-model",
                             f"`{name}` is listed in {rel(doc)} but no such class exists"))

        for pattern in MODEL_COUNT_CLAIM_RES:
            for m in pattern.finditer(text):
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
_FENCE_RE = re.compile(r"^\s{0,3}(`{3,}|~{3,})")
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

    Fence state is tracked across the WHOLE file even when only_lines restricts
    what is reported, because an added line inside a code block is only
    identifiable from the lines above it.
    """
    findings: list[tuple[Path, int, str, str]] = []
    fence: str | None = None
    for lineno, raw in enumerate(text.splitlines(), 1):
        opener = _FENCE_RE.match(raw)
        if opener:
            marker = opener.group(1)[0]
            if fence is None:
                fence = marker
            elif marker == fence:
                fence = None
            continue
        if fence is not None:
            continue
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


def _git_out(*args: str) -> str | None:
    """Run git in REPO_ROOT. Returns None on any failure (fail open)."""
    try:
        proc = subprocess.run(
            ["git", "-C", str(REPO_ROOT), *args],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
        )
    except Exception:
        return None
    return proc.stdout if proc.returncode == 0 else None


_HUNK_RE = re.compile(r"^@@ -\S+ \+(\d+)(?:,(\d+))? @@")


def _changed_markdown_lines(base: str) -> dict[Path, set[int] | None]:
    """Added line numbers per markdown file. None means 'the whole file is new'."""
    changed: dict[Path, set[int] | None] = {}
    diff = _git_out("-c", "core.quotePath=false", "diff", "-U0", base, "--", "*.md")
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
    untracked = _git_out(
        "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard", "--", "*.md"
    )
    if untracked:
        for name in untracked.splitlines():
            name = name.strip()
            if name:
                changed[REPO_ROOT / name] = None
    return changed


def check_ai_dashes(base: str = "HEAD") -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    for path, lines in sorted(_changed_markdown_lines(base).items(), key=lambda kv: str(kv[0])):
        if not path.is_file():
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except Exception:
            continue
        findings.extend(scan_text_for_dashes(path, text, lines))
    return findings


def rel(p: Path) -> str:
    try:
        return str(p.relative_to(REPO_ROOT)).replace("\\", "/")
    except ValueError:
        return str(p)


def format_report(report: LintReport, quick: bool) -> str:
    out: list[str] = []
    out.append("# Doc lint report")
    out.append("")
    out.append(f"- Dead links: **{len(report.dead_links)}**")
    out.append(f"- Link targets present but UNTRACKED (dead on the remote): "
               f"**{len(report.untracked_targets)}**")
    if not quick:
        out.append(f"- Stale version refs (outside migration/archive): **{len(report.stale_versions)}**")
        out.append(f"- Orphan feature docs (no inbound references): **{len(report.orphan_features)}**")
        out.append(f"- Prose trapped in an auto-generated backlinks region: **{len(report.backlinks_prose)}**")
        out.append(f"- Missing feature docs (Main/Features/<X> with no docs/features/<x>.md): **{len(report.missing_feature_docs)}**")
        out.append(f"- Config-example drift (doc JSON != shipped ModuleData config): **{len(report.config_drift)}**")
        out.append(f"- Version mismatches (CLAUDE.md / snapshot != pin): **{len(report.version_mismatches)}**")
        out.append(f"- CLAUDE.md budget (size/row/line caps{'' if CLAUDE_MD_BUDGET_ENFORCE else ', warn-only'}): **{len(report.budget)}**")
        out.append(f"- GameModel registry drift (code vs the two catalogues): **{len(report.model_registry)}**")
        out.append(f"- Em/en dashes in newly written prose: **{len(report.dashes)}**")
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
            out.append("`.claude/rules/gamemodels.md` is `paths:`-scoped to the model files, so it is the "
                       "briefing whoever edits a model actually receives — a model missing from its table "
                       "never gets the cross-entity-propagation rule. `docs/reference/gamemodel-registry.md` "
                       "claims to list every override. Add the model to both, or correct the claim.")
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
    if report.total == 0:
        out.append("**Clean — no findings.**")
        out.append("")
    return "\n".join(out)


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
                    help="Exit 1 if any config-example drift OR version mismatch found (pre-commit gate)")
    ap.add_argument("--summary", action="store_true", help="Emit a --- delimited grep-friendly summary block instead of the full markdown report")
    ap.add_argument("--dash-base", default="HEAD", metavar="REF",
                    help="Base ref for the em/en dash check (default HEAD, i.e. uncommitted writing). "
                         "Pass a branch point to scan a whole branch.")
    args = ap.parse_args(argv)

    files: list[Path] = []
    for root in DOC_ROOTS:
        if root.is_dir():
            files.extend(iter_markdown(root))

    report = LintReport()
    report.dead_links = check_dead_links(files)
    report.untracked_targets = check_untracked_link_targets(files)
    if not args.quick:
        report.stale_versions = check_stale_versions(files)
        report.orphan_features = check_orphan_features(files)
        report.backlinks_prose = check_backlinks_region_prose(files)
        report.missing_feature_docs = check_missing_feature_docs(feature_doc_basenames())
        report.config_drift = check_config_example_drift(files)
        report.version_mismatches = check_version_consistency()
        report.budget = check_claude_md_budget()
        report.model_registry = check_model_registry()
        report.dashes = check_ai_dashes(args.dash_base)

    if args.summary:
        # Structured grep-friendly block, modeled on autoresearch's train.py final output
        summary = "\n".join([
            "---",
            f"dead_links:        {len(report.dead_links)}",
            f"stale_versions:    {len(report.stale_versions)}",
            f"orphan_features:   {len(report.orphan_features)}",
            f"backlinks_prose:   {len(report.backlinks_prose)}",
            f"missing_features:  {len(report.missing_feature_docs)}",
            f"config_drift:      {len(report.config_drift)}",
            f"version_mismatch:  {len(report.version_mismatches)}",
            f"claude_budget:     {len(report.budget)}",
            f"model_registry:    {len(report.model_registry)}",
            f"ai_dashes:         {len(report.dashes)}",
            f"total_findings:    {report.total}",
            "---",
            "",
        ])
        sys.stdout.write(summary)
    else:
        rendered = format_report(report, quick=args.quick)
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
    ):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
