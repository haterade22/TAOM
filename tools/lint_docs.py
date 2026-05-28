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
)
DEAD_LINK_EXEMPT_PREFIXES = (
    str(ARCHIVE_DIR).replace("\\", "/"),
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
)
# Codex review *transcripts* (prompts + results) are external-tool snapshots, not curated docs.
# We don't lint their internal links either — they capture historical state and we don't edit them.
DEAD_LINK_EXEMPT_FILENAME_SUBSTRINGS = (
    "codex-adversarial-",
    "codex-prompt-",
    "codex-result-",
    "doc-lint-",
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

    @property
    def total(self) -> int:
        return (
            len(self.dead_links)
            + len(self.stale_versions)
            + len(self.orphan_features)
            + len(self.missing_feature_docs)
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

    if args.summary:
        # Structured grep-friendly block, modeled on autoresearch's train.py final output
        summary = "\n".join([
            "---",
            f"dead_links:        {len(report.dead_links)}",
            f"stale_versions:    {len(report.stale_versions)}",
            f"orphan_features:   {len(report.orphan_features)}",
            f"missing_features:  {len(report.missing_feature_docs)}",
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
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
