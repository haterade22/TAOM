#!/usr/bin/env python3
"""Inventory probe for docs/raw/<topic>/ — preparatory context for /knowledge-compile.

The script does NOT generate the research node itself. It collects:
1. The file inventory of docs/raw/<topic>/ (recursive).
2. Candidate cross-references in docs/features/, docs/reviews/, docs/ai-includes/
   that contain the topic keywords.
3. A skeleton outline for docs/research/<topic>.md.

The /knowledge-compile skill consumes this output as its briefing material, then
writes the actual research node using Claude. The script is the *boring* deterministic
half (inventory + grep); the skill is the *interesting* LLM-driven half (synthesis).

Usage:
    python tools/compile_research.py <topic>
    python tools/compile_research.py tolkien/middle-earth-geography
    python tools/compile_research.py bannerlord-engine/mission-lifecycle --depth 2
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

# Re-use shared paths from lint_docs.
sys.path.insert(0, str(Path(__file__).resolve().parent))
import lint_docs as ld  # noqa: E402

RAW_DIR = ld.DOCS_DIR / "raw"
RESEARCH_DIR = ld.DOCS_DIR / "research"

# Keep keyword search bounded so candidate lists don't explode.
DEFAULT_KEYWORD_HITS_PER_FILE_CAP = 3
DEFAULT_CANDIDATE_FILE_CAP = 20

# Word splitter for topic-derived keywords.
WORD_RE = re.compile(r"[A-Za-z][A-Za-z0-9]+")
STOPWORDS = {
    "the", "a", "an", "and", "or", "of", "to", "for", "in", "on", "at",
    "with", "from", "by", "into", "out", "this", "that", "these", "those",
    "is", "are", "was", "were", "be", "been", "being", "as", "it", "its",
    "topic", "notes", "draft", "raw", "source",
}


def extract_keywords(topic: str) -> list[str]:
    """Derive search keywords from a topic path like 'tolkien/middle-earth-geography'."""
    words: list[str] = []
    for chunk in re.split(r"[/\-_]+", topic):
        for w in WORD_RE.findall(chunk):
            lw = w.lower()
            if len(lw) <= 2 or lw in STOPWORDS:
                continue
            words.append(lw)
    # Deduplicate preserving order
    seen: set[str] = set()
    out: list[str] = []
    for w in words:
        if w not in seen:
            out.append(w)
            seen.add(w)
    return out


def inventory(topic_dir: Path) -> list[Path]:
    return sorted(p for p in topic_dir.rglob("*") if p.is_file() and not p.name.startswith("."))


def first_nonempty_line(p: Path) -> str:
    try:
        for line in p.read_text(encoding="utf-8", errors="replace").splitlines():
            stripped = line.strip()
            if stripped:
                return stripped[:140]
    except OSError:
        pass
    return ""


def find_candidates(keywords: list[str]) -> dict[Path, list[str]]:
    """Search docs/features/, docs/reviews/, docs/ai-includes/ for any keyword match.
    Returns {doc -> [keywords hit]}."""
    if not keywords:
        return {}
    search_roots = [
        ld.DOCS_DIR / "features",
        ld.DOCS_DIR / "reviews",
        ld.DOCS_DIR / "ai-includes",
        ld.DOCS_DIR / "adrs",
    ]
    candidates: dict[Path, list[str]] = {}
    for root in search_roots:
        if not root.is_dir():
            continue
        for md in root.rglob("*.md"):
            if md.name == "TEMPLATE.md":
                continue
            try:
                text = md.read_text(encoding="utf-8", errors="replace").lower()
            except OSError:
                continue
            hits = [kw for kw in keywords if kw in text]
            if hits:
                candidates[md] = hits
    return candidates


def render_briefing(topic: str, topic_dir: Path, files: list[Path], candidates: dict[Path, list[str]]) -> str:
    out: list[str] = []
    out.append(f"# /knowledge-compile briefing — `{topic}`")
    out.append("")
    out.append(f"Raw directory: `{ld.rel(topic_dir)}`")
    out.append(f"Research output target: `docs/research/{Path(topic).name}.md`")
    out.append("")
    out.append(f"## Source inventory ({len(files)} files)")
    out.append("")
    if not files:
        out.append("_(no files in raw dir — add source material first)_")
    else:
        for f in files:
            first = first_nonempty_line(f)
            size = f.stat().st_size
            size_label = f"{size}B" if size < 1024 else f"{size//1024}KB"
            preview = f" — {first}" if first else ""
            out.append(f"- `{ld.rel(f)}` ({size_label}){preview}")
    out.append("")
    keywords = extract_keywords(topic)
    out.append(f"## Cross-reference probe — keywords: {', '.join(keywords) or '_(none derived)_'}")
    out.append("")
    if not candidates:
        out.append("_(no existing TAOM doc matched these keywords — research node will have an empty cross-references section unless the skill finds additional connections.)_")
    else:
        ordered = sorted(candidates.items(), key=lambda kv: -len(kv[1]))[:DEFAULT_CANDIDATE_FILE_CAP]
        out.append(f"Top {len(ordered)} matching docs (by keyword overlap):")
        out.append("")
        for path, hits in ordered:
            out.append(f"- `{ld.rel(path)}` — matched: {', '.join(sorted(hits))}")
    out.append("")
    out.append("## Skeleton for `docs/research/" + Path(topic).name + ".md`")
    out.append("")
    out.append("```markdown")
    out.append(f"# {Path(topic).name.replace('-', ' ').title()}")
    out.append("")
    out.append(f"> Source materials: `docs/raw/{topic}/` — {len(files)} files, last compiled <DATE>")
    out.append("")
    out.append("## Summary")
    out.append("")
    out.append("<1-2 paragraphs synthesizing the raw material>")
    out.append("")
    out.append("## Sources")
    out.append("")
    if files:
        for f in files:
            out.append(f"- `{ld.rel(f)}` — <one-line description>")
    else:
        out.append("- _(none yet)_")
    out.append("")
    out.append("## Cross-references")
    out.append("")
    out.append("<list TAOM features / RCAs / ADRs / memory entries that connect to this topic>")
    out.append("")
    out.append("## Key claims")
    out.append("")
    out.append("<non-obvious propositions extracted from the sources, each citing its source file>")
    out.append("")
    out.append("## Open questions")
    out.append("")
    out.append("<gaps, contradictions, follow-up research>")
    out.append("```")
    out.append("")
    return "\n".join(out)


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("topic", help="Topic path under docs/raw/, e.g. 'tolkien/middle-earth-geography'")
    args = ap.parse_args(argv)

    topic = args.topic.strip().replace("\\", "/").strip("/")
    if not topic:
        print("error: topic argument is empty", file=sys.stderr)
        return 2

    topic_dir = RAW_DIR / topic
    if not topic_dir.is_dir():
        print(f"error: {ld.rel(topic_dir)} does not exist. Create it and add source material first.", file=sys.stderr)
        return 2

    files = inventory(topic_dir)
    keywords = extract_keywords(topic)
    candidates = find_candidates(keywords)
    briefing = render_briefing(topic, topic_dir, files, candidates)
    sys.stdout.write(briefing)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
