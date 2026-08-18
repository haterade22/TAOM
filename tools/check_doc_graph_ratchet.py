#!/usr/bin/env python3
"""Ratchet on the documentation graph's structural health.

`/doc-graph` shipped 2026-06-08 with a measured baseline and was then run by
nothing: zero hooks, zero CI jobs. Ten weeks later the isolated-doc count had gone
from 64 to 153 and the component count from 70 to 156, and no test failed, because
`tools/tests/test_graph_query.py` tests the ALGORITHM against synthetic fixtures.
It never looks at the real `docs/` tree, and it was green the whole time.

That is the lesson this script exists to encode: a diagnostic nobody runs is not a
check. The unit tests prove `metrics` computes the right numbers; this proves the
numbers have not got worse.

Ratcheted on ORPHANS and COMPONENTS only. Node and edge counts grow legitimately
with every doc that lands, so gating them would just be noise. Orphans (a doc with
no inbound AND no outbound `.md` link) and components (disconnected islands) should
never grow: a new doc that nobody links and that links nobody is exactly the defect.

Usage:
    python tools/check_doc_graph_ratchet.py            # check against the baseline
    python tools/check_doc_graph_ratchet.py --update   # accept the current numbers

Exit codes: 0 within baseline, 1 regression, 2 bad input.

Lowering the baseline is the point. Run --update after linking isolates in, and
commit the lowered file. Raising it needs a reason in the commit message.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import doc_graph as dg  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
BASELINE = Path(__file__).resolve().parent / "doc_graph_baseline.json"

#: Metrics that must never grow. Everything else in `metrics` is informational.
RATCHETED = ("orphans", "components")


def current_metrics() -> dict:
    """The two ratcheted counts, from the live docs tree."""
    nodes, edges, inbound, adj = dg.load_doc_graph()
    metrics = dg.graph_metrics(nodes, edges, inbound, adj, top=1)
    return {
        "orphans": len(metrics["orphans"]),
        "components": int(metrics["component_count"]),
    }


def compare(current: dict, baseline: dict) -> list:
    """Regressions, as human-readable strings. Empty list means within baseline."""
    problems = []
    for key in RATCHETED:
        if key not in baseline:
            problems.append(f"{key}: missing from the baseline file, cannot check")
            continue
        if current[key] > baseline[key]:
            problems.append(
                f"{key}: {current[key]} exceeds the baseline {baseline[key]} "
                f"(+{current[key] - baseline[key]})")
    return problems


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--update", action="store_true",
                    help="write the current numbers as the new baseline")
    ap.add_argument("--baseline", default=str(BASELINE))
    ap.add_argument("--json", dest="json_out", action="store_true")
    args = ap.parse_args()

    baseline_path = Path(args.baseline)
    current = current_metrics()

    if args.update:
        baseline_path.write_text(json.dumps(current, indent=2) + "\n", encoding="utf-8")
        print(f"baseline updated: {current}")
        return 0

    if not baseline_path.exists():
        print(f"ERROR: no baseline at {baseline_path}. Create one with --update.",
              file=sys.stderr)
        return 2

    try:
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"ERROR: baseline is not valid JSON: {exc}", file=sys.stderr)
        return 2

    problems = compare(current, baseline)

    if args.json_out:
        print(json.dumps({"current": current, "baseline": baseline,
                          "problems": problems}, indent=2))
    elif problems:
        print("FAIL: the documentation graph got structurally worse.")
        for p in problems:
            print(f"  {p}")
        print("\nAn orphan is a doc with no inbound AND no outbound .md link, so nothing\n"
              "will ever find it. Link it from INDEX.md or a sibling, or delete it.\n"
              "Inspect with: python tools/graph_query.py metrics --top 15\n"
              "If the growth is genuinely intended, run --update and say why in the commit.")
    else:
        print(f"PASS: orphans {current['orphans']} <= {baseline['orphans']}, "
              f"components {current['components']} <= {baseline['components']}")

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
