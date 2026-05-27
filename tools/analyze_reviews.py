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

The integer-only columns (Real Bugs, False Positives, Missed Bugs) sometimes
contain prose like "1 confirmed + 1 valid" — we extract the leading integer.

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
ROW_RE = re.compile(r"^\|\s*(\d+)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|")
LEADING_INT_RE = re.compile(r"^\s*(\d+)")


@dataclass
class Review:
    num: int
    date: str
    feature: str
    codex_verdict: str
    claude_verdict: str
    real_bugs: int
    false_positives: int
    missed_bugs: int
    prompt_version: str

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


def parse_reviews(text: str) -> list[Review]:
    """Walk the markdown tables in REVIEW-LOG.md and yield Review records.
    Skips header + separator rows by requiring (num, date) to be plausibly
    review-shaped: num is an integer, date matches YYYY-MM-DD."""
    reviews: list[Review] = []
    seen_nums: set[int] = set()
    date_re = re.compile(r"^\d{4}-\d{2}-\d{2}$")
    for line in text.splitlines():
        m = ROW_RE.match(line)
        if not m:
            continue
        try:
            num = int(m.group(1))
        except ValueError:
            continue
        date = m.group(2).strip()
        if not date_re.match(date):
            continue
        if num in seen_nums:
            # Some review numbers repeat across the two summary tables; keep first occurrence.
            continue
        seen_nums.add(num)
        reviews.append(Review(
            num=num,
            date=date,
            feature=m.group(3).strip(),
            codex_verdict=m.group(4).strip(),
            claude_verdict=m.group(5).strip(),
            real_bugs=parse_int(m.group(6)),
            false_positives=parse_int(m.group(7)),
            missed_bugs=parse_int(m.group(8)),
            prompt_version=m.group(9).strip(),
        ))
    reviews.sort(key=lambda r: r.num)
    return reviews


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
        key = r.prompt_version.split()[0]  # "v6", "v6-adversarial", etc — first token
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

    nums = [r.num for r in reviews]
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
    for r in reviews:
        v = r.prompt_version.split()[0]
        if v != prev_v:
            ax1.axvline(r.num - 0.5, color="#888", linestyle="--", linewidth=0.5, alpha=0.4)
            ax1.text(r.num - 0.4, ax1.get_ylim()[1] * 0.95, v, fontsize=7, color="#555", rotation=90, va="top")
            prev_v = v

    # Bottom panel: cumulative real-bug line + a thin running miss-rate indicator
    ax2.fill_between(nums, cum_real, color="#27ae60", alpha=0.3, label="Cumulative real bugs found")
    ax2.plot(nums, cum_real, color="#1a7a3a", linewidth=2)
    ax2.set_xlabel("Review #", fontsize=11)
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
    reviews = parse_reviews(text)
    if not reviews:
        print("error: no review rows parsed from REVIEW-LOG.md (table format may have changed)", file=sys.stderr)
        return 1

    metrics = aggregate_metrics(reviews)

    if not args.no_plot:
        render_plot(reviews, metrics, args.out)

    if args.summary:
        sys.stdout.write(render_summary(reviews, metrics) + "\n")
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

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
