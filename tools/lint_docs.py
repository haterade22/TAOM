#!/usr/bin/env python3
"""TAOM doc-health linter.

Walks docs/ and reports doc rot:

- Dead markdown links (relative paths that don't resolve)
- Stale version refs (1.3.15, 1.3.x, Bannerlord 1.3 outside docs/migration/)
- Orphan feature docs (docs/features/<x>.md not referenced anywhere else)
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
MAIN_FEATURES_DIR = REPO_ROOT / "Main" / "Features"

LINK_RE = re.compile(r"\[([^\]]*)\]\(([^)]+)\)")
INLINE_CODE_RE = re.compile(r"`+[^`\n]+?`+")
STALE_VERSION_PATTERNS = [
    (re.compile(r"\bv?1\.3\.15\b"), "1.3.15"),
    (re.compile(r"\bBannerlord\s+1\.3\b(?!\.)"), "Bannerlord 1.3"),
    (re.compile(r"\bv1\.3\.x\b"), "v1.3.x"),
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
)
DEAD_LINK_EXEMPT_PREFIXES = (
    str(ARCHIVE_DIR).replace("\\", "/"),
    str(CHANGELOG_ARCHIVE_DIR).replace("\\", "/"),
    str(REVIEWS_RAW_DIR).replace("\\", "/"),
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
)
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
    stale_versions: list[tuple[Path, int, str, str]] = field(default_factory=list)
    orphan_features: list[Path] = field(default_factory=list)
    missing_feature_docs: list[tuple[str, str]] = field(default_factory=list)
    config_drift: list[tuple[Path, int, str, str]] = field(default_factory=list)
    version_mismatches: list[tuple[Path, int, str, str]] = field(default_factory=list)
    budget: list[tuple[Path, int, str, str]] = field(default_factory=list)

    @property
    def total(self) -> int:
        return (
            len(self.dead_links)
            + len(self.stale_versions)
            + len(self.orphan_features)
            + len(self.missing_feature_docs)
            + len(self.config_drift)
            + len(self.version_mismatches)
            + len(self.budget)
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
                if not resolved.exists():
                    findings.append((f, lineno, target, label))
    return findings


def check_stale_versions(files: list[Path]) -> list[tuple[Path, int, str, str]]:
    findings: list[tuple[Path, int, str, str]] = []
    for f in files:
        f_posix = str(f).replace("\\", "/")
        if any(f_posix.startswith(prefix) for prefix in STALE_VERSION_EXEMPT_PREFIXES):
            continue
        if any(sub in f.name for sub in STALE_VERSION_EXEMPT_FILENAME_SUBSTRINGS):
            continue
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for lineno, line in iter_lines_outside_code_fences(text):
            for pattern, label in STALE_VERSION_PATTERNS:
                m = pattern.search(line)
                if m:
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
    pin_file = REPO_ROOT / ".claude" / "pinned-game-version.txt"
    if not pin_file.exists():
        return findings
    try:
        pin_raw = pin_file.read_text(encoding="utf-8").strip()
    except OSError:
        return findings
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
    if not quick:
        out.append(f"- Stale version refs (outside migration/archive): **{len(report.stale_versions)}**")
        out.append(f"- Orphan feature docs (no inbound references): **{len(report.orphan_features)}**")
        out.append(f"- Missing feature docs (Main/Features/<X> with no docs/features/<x>.md): **{len(report.missing_feature_docs)}**")
        out.append(f"- Config-example drift (doc JSON != shipped ModuleData config): **{len(report.config_drift)}**")
        out.append(f"- Version mismatches (CLAUDE.md / snapshot != pin): **{len(report.version_mismatches)}**")
        out.append(f"- CLAUDE.md budget (size/row/line caps{'' if CLAUDE_MD_BUDGET_ENFORCE else ', warn-only'}): **{len(report.budget)}**")
    out.append("")
    if report.dead_links:
        out.append("## Dead links")
        out.append("")
        for f, lineno, target, label in report.dead_links:
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
    args = ap.parse_args(argv)

    files: list[Path] = []
    for root in DOC_ROOTS:
        if root.is_dir():
            files.extend(iter_markdown(root))

    report = LintReport()
    report.dead_links = check_dead_links(files)
    if not args.quick:
        report.stale_versions = check_stale_versions(files)
        report.orphan_features = check_orphan_features(files)
        report.missing_feature_docs = check_missing_feature_docs(feature_doc_basenames())
        report.config_drift = check_config_example_drift(files)
        report.version_mismatches = check_version_consistency()
        report.budget = check_claude_md_budget()

    if args.summary:
        # Structured grep-friendly block, modeled on autoresearch's train.py final output
        summary = "\n".join([
            "---",
            f"dead_links:        {len(report.dead_links)}",
            f"stale_versions:    {len(report.stale_versions)}",
            f"orphan_features:   {len(report.orphan_features)}",
            f"missing_features:  {len(report.missing_feature_docs)}",
            f"config_drift:      {len(report.config_drift)}",
            f"version_mismatch:  {len(report.version_mismatches)}",
            f"claude_budget:     {len(report.budget)}",
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
    if args.fail_on_drift and (
        report.config_drift
        or report.version_mismatches
        or (CLAUDE_MD_BUDGET_ENFORCE and report.budget)
    ):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
