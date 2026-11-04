#!/usr/bin/env python3
"""TAOM review-log analyzer.

Mirrors karpathy/autoresearch's `analysis.ipynb`: parses the running scorecard
of Codex adversarial reviews in `docs/reviews/REVIEW-LOG.md` and produces:

  - `docs/reviews/progress.png` — visualization of bugs found over time +
    Codex accuracy trend (committed to repo as a teaser, even though the
    underlying TSV equivalent stays in markdown)
  - Optional `--summary` flag emits a structured `---`-delimited block to
    stdout for autonomous loops to grep

Parses the markdown tables under `## Summary` and `## Gap Reviews (post-audit)`.
Rows look like:

    | # | Date | Feature | Codex Verdict | Claude Verdict | Real Bugs | False Positives | Missed Bugs | Prompt Version |

The first cell is a number, a slug ("career-e11") or "(at merge)", and numbers repeat, so a row is
keyed by (first cell, date, feature). The integer-only columns (Real Bugs, False Positives, Missed
Bugs) sometimes contain prose like "1 confirmed + 1 valid" or "2 HIGH, 9 MEDIUM": Real Bugs is the
larger of the leading integer and the sum of the severity counts, which are kept per grade.

Yield is printed by tier (the effort word in the Prompt Version cell), by who found it (`codex` when a
Codex pass ran, else `claude`), and by lens: the `Found by` column of the RCA tables in
`docs/reviews/rca-*.md`, one count per credited lens. False positives are logged only in the
REVIEW-LOG rows, so the per-lens table has none. Cost is not joined: compare totals by hand.

Usage:
    python tools/analyze_reviews.py                    # generate progress.png
    python tools/analyze_reviews.py --summary          # also print --- block to stdout
    python tools/analyze_reviews.py --no-plot          # skip the PNG, just print summary
"""
from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
REVIEW_LOG = REPO_ROOT / "docs" / "reviews" / "REVIEW-LOG.md"
PROGRESS_PNG = REPO_ROOT / "docs" / "reviews" / "progress.png"

# Table row pattern:  | N | date | feature | codex | claude | real | fps | missed | prompt |
ROW_RE = re.compile(r"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|")
LEADING_INT_RE = re.compile(r"^\s*(\d+)")


SEVERITY_RE = re.compile(
    r"(\d+)\s+(?:[A-Za-z-]+\s+)?(CRITICAL|CRIT|HIGH|MEDIUM|MED|LOW|P1|P2|P3)\b")
SEVERITY_NAMES = {"CRIT": "CRITICAL", "MED": "MEDIUM"}
GRADE_RE = re.compile(r"\b(CRITICAL|CRIT|HIGH|MEDIUM|MED|LOW|P1|P2|P3)\b")
TIER_RE = re.compile(r"\b(ultra|xhigh|max|high|medium|low)\b", re.IGNORECASE)
CODEX_NOT_RUN_RE = re.compile(r"^\W*(not run|no codex|n/a|none)", re.IGNORECASE)
NO_MISSED_RE = re.compile(r"^\W*(none|n/a|nil|no\b)", re.IGNORECASE)
CODE_SPAN_RE = re.compile(r"(`+).*?\1")
LENS_HEADERS = ("found by", "lens", "lenses", "finding (lens)")
LENS_WORDS = ("standards", "engine", "efficiency", "completeness", "data flow", "design", "xml", "tooling",
              "codex", "convergence")
LENS_KEY = dict(zip("12345678", LENS_WORDS))  # the deep-review lens numbers: 1 Standards ... 8 Tooling
LENS_KEY_RE = re.compile(r"(?:[^,;/+]*\bagent\s+)?([1-8])(?:fe|cc)?\b")


def normalize_grade(word: str) -> str:
    return SEVERITY_NAMES.get(word.upper(), word.upper())


def parse_severity(cell: str) -> dict[str, int]:
    out: dict[str, int] = {}
    for count, grade in SEVERITY_RE.findall(re.sub(r"\([^)]*\)", " ", cell)):  # not a fix tally in prose
        key = normalize_grade(grade)
        out[key] = out.get(key, 0) + int(count)
    return out


@dataclass
class Review:
    label: str
    date: str
    feature: str
    codex_verdict: str
    claude_verdict: str
    real_bugs: int
    false_positives: int
    missed_bugs: int
    prompt_version: str
    severity: dict = field(default_factory=dict)

    @property
    def tier(self) -> str:
        """The Codex effort word in the Prompt Version cell; rows before tiers say `none`."""
        m = TIER_RE.search(self.prompt_version)
        return m.group(1).lower() if m else "none"

    @property
    def found_by(self) -> str:
        """`codex` when a Codex pass ran on the row, else `claude` (the in-house lenses)."""
        return "claude" if CODEX_NOT_RUN_RE.match(self.codex_verdict.strip("() ")) else "codex"

    @property
    def total_findings(self) -> int:
        return self.real_bugs + self.false_positives

    @property
    def accuracy(self) -> float:
        t = self.total_findings
        return self.real_bugs / t if t > 0 else 1.0  # no findings = "approve" verdict, treat as clean


def parse_int(cell: str) -> int:
    m = LEADING_INT_RE.match(cell)
    return int(m.group(1)) if m else 0


def parse_reviews(text: str, stats: dict | None = None) -> list[Review]:
    """Walk the markdown tables in REVIEW-LOG.md and return Review records, oldest first.
    A row is review-shaped when its second cell is a YYYY-MM-DD date; the first cell may be a
    number, a slug or "(at merge)". Numbers repeat across the tables, so the key is (first cell,
    date, feature) and only an exact repeat is dropped. `stats`, when given, receives `short` (dated
    rows with fewer than 9 cells, which are dropped) and `prose_missed` (rows whose Missed Bugs cell
    is prose, which counts 0)."""
    reviews: list[Review] = []
    seen: set[tuple[str, str, str]] = set()
    date_re = re.compile(r"^\d{4}-\d{2}-\d{2}$")
    if stats is not None:
        stats.update(short=0, prose_missed=0)
    for line in text.splitlines():
        m = ROW_RE.match(line)
        if not m:
            cells = split_cells(line) if line.lstrip().startswith("|") else []
            if stats is not None and len(cells) > 1 and date_re.match(cells[1]):
                stats["short"] += 1
            continue
        label, date, feature = m.group(1).strip(), m.group(2).strip(), m.group(3).strip()
        if not date_re.match(date):
            continue
        key = (label, date, feature)
        if key in seen:
            continue
        seen.add(key)
        if stats is not None and not LEADING_INT_RE.match(m.group(8)) and not NO_MISSED_RE.match(m.group(8)):
            stats["prose_missed"] += 1
        severity = parse_severity(m.group(6))
        reviews.append(Review(
            label=label,
            date=date,
            feature=feature,
            codex_verdict=m.group(4).strip(),
            claude_verdict=m.group(5).strip(),
            real_bugs=max(parse_int(m.group(6)), sum(severity.values())),
            false_positives=parse_int(m.group(7)),
            missed_bugs=parse_int(m.group(8)),
            prompt_version=m.group(9).strip(),
            severity=severity,
        ))
    reviews.sort(key=lambda r: r.date)  # stable: same-day rows keep the log's order
    return reviews


def yield_by(reviews: list[Review], attr: str) -> dict[str, dict]:
    """Real bugs, false positives and severity counts grouped by a Review attribute
    (`tier` or `found_by`)."""
    out: dict[str, dict] = {}
    for r in reviews:
        d = out.setdefault(getattr(r, attr), {"reviews": 0, "real": 0, "fps": 0, "severity": {}})
        d["reviews"] += 1
        d["real"] += r.real_bugs
        d["fps"] += r.false_positives
        for grade, n in r.severity.items():
            d["severity"][grade] = d["severity"].get(grade, 0) + n
    return out


def split_cells(line: str) -> list[str]:
    """The cells of a table row; a pipe inside a code span stays in its cell."""
    masked = CODE_SPAN_RE.sub(lambda m: m.group(0).replace("|", "\0"), line.strip().strip("|"))
    return [c.replace("\0", "|").strip() for c in masked.split("|")]


def clean_lenses(raw: str) -> list[str]:
    """Lens names from a cell, only the ones in LENS_WORDS: `XML lens, Data flow 4 (O1)` reads `xml`,
    `data flow`. A lens key (`2fe F1, 3 F1, 6 P1`, `Agent 5`) credits the lens it numbers; anything
    else, such as `this session`, gets no credit."""
    s = re.sub(r"\([^)]*\)", " ", raw).casefold()
    s = re.sub(r"data-?flow", "data flow", s).replace("elegance", "design")
    keyed, listing = [], False  # after a lens word, a bare number is a finding id: `completeness 2, 3, 9`
    for piece in re.split(r"[,;/+]", s):
        key = None if listing else LENS_KEY_RE.match(piece.strip())
        if key:
            keyed.append(LENS_KEY[key.group(1)])
        elif any(re.search(rf"\b{w}\b", piece) for w in LENS_WORDS):
            listing = True
    return list(dict.fromkeys([w for w in LENS_WORDS if re.search(rf"\b{w}\b", s)] + keyed))


def parse_rca_findings(text: str) -> list[tuple[list[str], str]]:
    """(lenses, severity) for each row of an RCA findings table with a `Found by`, `Lens`, `Lenses`
    or `Finding (lens)` column; the last holds the lenses in the parenthesis ending the cell.
    `XML, Data flow, Codex (O1)` credits three lenses; the severity is the first grade in the Sev
    cell (`HIGH (XML) / MED (Data flow)` reads HIGH), or `?` when there is none."""
    rows: list[tuple[list[str], str]] = []
    found_col = sev_col = None
    in_finding = False
    for line in text.splitlines():
        if not line.lstrip().startswith("|"):
            found_col = sev_col = None
            continue
        cells = split_cells(line)
        lowered = [c.lower() for c in cells]
        header = next((i for i, c in enumerate(lowered) if c in LENS_HEADERS), None)
        if header is not None:
            # A table with no Sev column is a summary, not findings.
            found_col = header if "sev" in lowered else None
            in_finding = lowered[header] == "finding (lens)"
            sev_col = lowered.index("sev") if "sev" in lowered else None
            continue
        if found_col is None or set("".join(cells)) <= set("-: ") or found_col >= len(cells):
            continue
        raw = cells[found_col]
        if in_finding:
            tail = re.search(r"\(([^()]*)\)\s*$", raw)
            raw = tail.group(1) if tail else ""
        lenses = clean_lenses(raw)
        grade = GRADE_RE.search(cells[sev_col]) if sev_col is not None and sev_col < len(cells) else None
        rows.append((lenses, normalize_grade(grade.group(1)) if grade else "?"))
    return rows


def yield_by_lens(findings: list[tuple[list[str], str]]) -> dict[str, dict]:
    out: dict[str, dict] = {}
    for lenses, grade in findings:
        for lens in lenses:
            d = out.setdefault(lens, {"real": 0, "sole": 0, "severity": {}})
            d["real"] += 1
            d["sole"] += len(lenses) == 1
            d["severity"][grade] = d["severity"].get(grade, 0) + 1
    return out


def format_severity(severity: dict) -> str:
    return ", ".join(f"{n} {g}" for g, n in sorted(severity.items(), key=lambda kv: (-kv[1], kv[0]))) or "-"


def aggregate_metrics(reviews: list[Review]) -> dict:
    total_real = sum(r.real_bugs for r in reviews)
    total_fps = sum(r.false_positives for r in reviews)
    total_missed = sum(r.missed_bugs for r in reviews)
    total_findings = total_real + total_fps
    accuracy = total_real / total_findings if total_findings > 0 else 0.0
    miss_rate = total_missed / (total_real + total_missed) if (total_real + total_missed) > 0 else 0.0
    fp_rate = total_fps / total_findings if total_findings > 0 else 0.0
    # Per-prompt-version accuracy
    by_prompt: dict[str, dict[str, int]] = {}
    for r in reviews:
        key = (r.prompt_version.split() or ["?"])[0]  # "v6", "v6-adversarial", etc — first token
        d = by_prompt.setdefault(key, {"real": 0, "fps": 0, "missed": 0})
        d["real"] += r.real_bugs
        d["fps"] += r.false_positives
        d["missed"] += r.missed_bugs
    return {
        "reviews": len(reviews),
        "total_real": total_real,
        "total_fps": total_fps,
        "total_missed": total_missed,
        "accuracy": accuracy,
        "miss_rate": miss_rate,
        "fp_rate": fp_rate,
        "by_prompt": by_prompt,
    }


def render_plot(reviews: list[Review], metrics: dict, out_path: Path) -> None:
    """Generate the progress.png teaser image. Two panels: bugs-per-review + cumulative."""
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib not available - skipping plot. Install with: pip install matplotlib", file=sys.stderr)
        return

    nums = list(range(1, len(reviews) + 1))  # the log's own numbers repeat
    real = [r.real_bugs for r in reviews]
    fps = [r.false_positives for r in reviews]
    missed = [r.missed_bugs for r in reviews]
    cum_real = []
    running = 0
    for v in real:
        running += v
        cum_real.append(running)

    fig, (ax1, ax2) = plt.subplots(2, 1, figsize=(14, 9), sharex=True)

    # Top panel: stacked bars per review
    ax1.bar(nums, real, color="#2ecc71", label="Real bugs (caught)", edgecolor="black", linewidth=0.5)
    ax1.bar(nums, fps, bottom=real, color="#e67e22", label="False positives", edgecolor="black", linewidth=0.5)
    ax1.bar(nums, missed, bottom=[r + f for r, f in zip(real, fps)], color="#c0392b", label="Bugs Codex missed", edgecolor="black", linewidth=0.5)
    ax1.set_ylabel("Findings per review", fontsize=11)
    ax1.set_title(f"TAOM Codex Adversarial Reviews — {metrics['reviews']} reviews, {metrics['total_real']} real bugs found", fontsize=13)
    ax1.legend(loc="upper left", fontsize=9)
    ax1.grid(True, alpha=0.2, axis="y")

    # Annotate prompt-version transitions
    prev_v = None
    for pos, r in zip(nums, reviews):
        v = (r.prompt_version.split() or ["?"])[0]
        if v != prev_v:
            ax1.axvline(pos - 0.5, color="#888", linestyle="--", linewidth=0.5, alpha=0.4)
            ax1.text(pos - 0.4, ax1.get_ylim()[1] * 0.95, v, fontsize=7, color="#555", rotation=90, va="top")
            prev_v = v

    # Bottom panel: cumulative real-bug line + a thin running miss-rate indicator
    ax2.fill_between(nums, cum_real, color="#27ae60", alpha=0.3, label="Cumulative real bugs found")
    ax2.plot(nums, cum_real, color="#1a7a3a", linewidth=2)
    ax2.set_xlabel("Review (oldest first)", fontsize=11)
    ax2.set_ylabel("Cumulative real bugs", fontsize=11)
    ax2.legend(loc="upper left", fontsize=9)
    ax2.grid(True, alpha=0.2)

    # Summary text on the figure
    accuracy_pct = metrics["accuracy"] * 100
    miss_pct = metrics["miss_rate"] * 100
    fp_pct = metrics["fp_rate"] * 100
    summary_text = (
        f"Codex accuracy: {accuracy_pct:.0f}%   "
        f"miss rate: {miss_pct:.0f}%   "
        f"FP rate: {fp_pct:.0f}%   "
        f"(total findings: {metrics['total_real'] + metrics['total_fps']})"
    )
    fig.text(0.5, 0.02, summary_text, ha="center", fontsize=10, color="#333", style="italic")

    plt.tight_layout(rect=(0, 0.04, 1, 1))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    plt.savefig(out_path, dpi=120, bbox_inches="tight")
    plt.close(fig)
    print(f"Wrote {out_path.relative_to(REPO_ROOT).as_posix()}", file=sys.stderr)


def render_summary(reviews: list[Review], metrics: dict) -> str:
    """Structured grep-friendly block, modeled on autoresearch's train.py final output."""
    lines = ["---"]
    lines.append(f"reviews:             {metrics['reviews']}")
    lines.append(f"real_bugs:           {metrics['total_real']}")
    lines.append(f"false_positives:     {metrics['total_fps']}")
    lines.append(f"missed_bugs:         {metrics['total_missed']}")
    lines.append(f"codex_accuracy_pct:  {metrics['accuracy'] * 100:.1f}")
    lines.append(f"codex_miss_rate_pct: {metrics['miss_rate'] * 100:.1f}")
    lines.append(f"codex_fp_rate_pct:   {metrics['fp_rate'] * 100:.1f}")
    lines.append("---")
    return "\n".join(lines)


def load_rca_findings() -> tuple[list[tuple[list[str], str]], int, int]:
    """(findings, RCA files that contributed one, RCA files read)."""
    findings: list[tuple[list[str], str]] = []
    paths = sorted(REVIEW_LOG.parent.glob("rca-*.md"))
    contributing = 0
    for path in paths:
        rows = parse_rca_findings(path.read_text(encoding="utf-8", errors="replace"))
        contributing += bool(rows)
        findings.extend(rows)
    return findings, contributing, len(paths)


def render_yield(reviews: list[Review], rca_findings: list[tuple[list[str], str]],
                 rca_files: tuple[int, int] = (0, 0)) -> str:
    """Real, false positive and severity per tier, per finder and per lens. No cost join: the
    harness retro compares totals by hand. `rca_files` is (contributing, read)."""
    lines = []
    for title, attr in (("By tier (Prompt Version cell)", "tier"), ("By finder (Codex ran or not)", "found_by")):
        lines.append(title + ":")
        for key, d in sorted(yield_by(reviews, attr).items()):
            lines.append(f"  {key:10s} reviews={d['reviews']:3d}  real={d['real']:3d}  "
                         f"fps={d['fps']:3d}  severity: {format_severity(d['severity'])}")
        lines.append("")
    uncredited = sum(not lenses for lenses, _ in rca_findings)
    lines.append(f"By lens (RCA lens column, {len(rca_findings)} findings from {rca_files[0]} of {rca_files[1]} RCAs, "
                 f"{uncredited} findings with no lens credit, no false positives logged; sole = that lens alone):")
    for key, d in sorted(yield_by_lens(rca_findings).items(), key=lambda kv: (-kv[1]["real"], kv[0])):
        lines.append(f"  {key:22s} real={d['real']:3d}  sole={d['sole']:3d}  severity: {format_severity(d['severity'])}")
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass

    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--summary", action="store_true", help="Print --- delimited summary block to stdout")
    ap.add_argument("--no-plot", action="store_true", help="Skip generating progress.png")
    ap.add_argument("--out", type=Path, default=PROGRESS_PNG, help="Output path for progress.png")
    args = ap.parse_args(argv)

    assert REVIEW_LOG.is_file(), f"REVIEW-LOG not found at {REVIEW_LOG}. Run from repo root."
    text = REVIEW_LOG.read_text(encoding="utf-8", errors="replace")
    stats: dict = {}
    reviews = parse_reviews(text, stats)
    if not reviews:
        print("error: no review rows parsed from REVIEW-LOG.md (table format may have changed)", file=sys.stderr)
        return 1
    if stats["short"] or stats["prose_missed"]:
        print(f"analyze_reviews: {stats['short']} row(s) skipped for too few cells; {stats['prose_missed']} row(s) "
              f"hold Missed Bugs as prose and count 0, so missed_bugs runs low", file=sys.stderr)

    metrics = aggregate_metrics(reviews)

    if not args.no_plot:
        render_plot(reviews, metrics, args.out)

    if args.summary:
        sys.stdout.write(render_summary(reviews, metrics) + "\n")
        findings, contributing, total = load_rca_findings()
        sys.stdout.write(render_yield(reviews, findings, (contributing, total)) + "\n")
    else:
        # Human-readable summary by default
        print(f"Parsed {metrics['reviews']} reviews from REVIEW-LOG.md")
        print(f"  Real bugs:           {metrics['total_real']}")
        print(f"  False positives:     {metrics['total_fps']}")
        print(f"  Codex missed:        {metrics['total_missed']}")
        print(f"  Accuracy:            {metrics['accuracy'] * 100:.1f}%")
        print(f"  Miss rate:           {metrics['miss_rate'] * 100:.1f}%")
        print(f"  False-positive rate: {metrics['fp_rate'] * 100:.1f}%")
        print()
        print("By prompt version:")
        for prompt, d in sorted(metrics["by_prompt"].items()):
            total = d["real"] + d["fps"]
            acc = d["real"] / total * 100 if total > 0 else 0
            print(f"  {prompt:25s}  real={d['real']:3d}  fps={d['fps']:3d}  missed={d['missed']:3d}  acc={acc:.0f}%")
        print()
        findings, contributing, total = load_rca_findings()
        print(render_yield(reviews, findings, (contributing, total)))

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
