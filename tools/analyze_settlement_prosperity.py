#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
READ-ONLY starting-prosperity overview for the LIVE TAOM_Map module vs vanilla SandBox (#317).

Companion to rebalance_settlement_prosperity.py (imports it for parsing + the target curve, so the
"proposed" column always agrees with what --apply would write). Run this BEFORE any rebaseline.

Reports to tools/reports/settlement-prosperity/:
    REPORT.md                    the primary artifact
    REPORT.html                  same tables, browser-friendly
    settlement-prosperity.json   full per-fief records for downstream tooling

Sections: per-class counts + distribution vs vanilla (composition-effect callout — TAOM is 65%
castles, so combined medians mislead), per-region table with flat-cluster flags, gold-equilibrium
columns (vanilla 10000 + P*12 and TAOM's shipped 25000 + P*12 — DefaultSettlementEconomyModel.
GetTownGoldChange + settlement_economy_config.json), flags (exact-value clusters, <1000 fiefs,
zero-bound-village fiefs, >6000 guard), hearth distribution (informational — the rebaseline never
touches hearth).

Usage:
    python3 tools/analyze_settlement_prosperity.py                      # write reports
    python3 tools/analyze_settlement_prosperity.py --stdout             # + executive summary
    python3 tools/analyze_settlement_prosperity.py --cluster-threshold 5
"""
import argparse
import json
import os
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_settlement_prosperity as rb  # noqa: E402

REPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "reports", "settlement-prosperity")
VANILLA_GOLD_BASE, TAOM_GOLD_BASE, GOLD_PER_PROSPERITY = 10000, 25000, 12
BUCKET = 500


def median(values):
    v = sorted(values)
    n = len(v)
    if n == 0:
        return 0
    return v[n // 2] if n % 2 else (v[n // 2 - 1] + v[n // 2]) / 2


def stats(values):
    if not values:
        return {"n": 0, "min": 0, "median": 0, "mean": 0, "max": 0}
    return {"n": len(values), "min": min(values), "median": median(values),
            "mean": round(sum(values) / len(values)), "max": max(values)}


def histogram(values, bucket=BUCKET):
    h = Counter((v // bucket) * bucket for v in values)
    return dict(sorted(h.items()))


def md_table(headers, rows):
    out = ["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"]
    out += ["| " + " | ".join(str(c) for c in row) + " |" for row in rows]
    return "\n".join(out)


def build_report(taom, vanilla, targets, fiefs, cluster_threshold):
    lines = ["# Settlement Starting-Prosperity Report (TAOM_Map vs vanilla SandBox)", ""]
    lines.append("Read-only analysis; the *proposed* column comes from `rebalance_settlement_prosperity.py` "
                 "(lift-only vanilla quantile map). Starting prosperity seeds NEW campaigns only; live saves "
                 "are covered by the SettlementEconomy regen knob (#317).")
    lines.append("")

    taom_by_kind = {k: [r for r in taom if r["kind"] == k] for k in ("town", "castle", "village")}
    van_by_kind = {k: [r for r in vanilla if r["kind"] == k] for k in ("town", "castle", "village")}

    # 1. counts + per-class stats
    lines.append("## 1. Counts and per-class prosperity (composition effect: compare per class, never combined)")
    rows = []
    for kind in ("town", "castle"):
        t = stats([r["prosperity"] for r in taom_by_kind[kind]])
        v = stats([r["prosperity"] for r in van_by_kind[kind]])
        rows.append([kind, t["n"], v["n"], t["min"], v["min"], t["median"], v["median"],
                     t["mean"], v["mean"], t["max"], v["max"]])
    rows.append(["village (hearth)", len(taom_by_kind["village"]), len(van_by_kind["village"]),
                 "-", "-", median([r["hearth"] for r in taom_by_kind["village"]]),
                 median([r["hearth"] for r in van_by_kind["village"]]), "-", "-", "-", "-"])
    lines.append(md_table(["class", "TAOM n", "van n", "TAOM min", "van min", "TAOM med", "van med",
                           "TAOM mean", "van mean", "TAOM max", "van max"], rows))
    lines.append("")

    # 2. per-class histograms
    lines.append(f"## 2. Distribution ({BUCKET}-wide buckets)")
    for kind in ("town", "castle"):
        th = histogram([r["prosperity"] for r in taom_by_kind[kind]])
        vh = histogram([r["prosperity"] for r in van_by_kind[kind]])
        buckets = sorted(set(th) | set(vh))
        lines.append(f"### {kind}s")
        lines.append(md_table(["bucket", "TAOM", "vanilla"],
                              [[f"{b}-{b + BUCKET - 1}", th.get(b, 0), vh.get(b, 0)] for b in buckets]))
        lines.append("")

    # 3. per-region table
    lines.append("## 3. Per region (TAOM)")
    by_region = defaultdict(lambda: defaultdict(list))
    for f in fiefs:
        by_region[f["region"]][f["kind"]].append(f)
    rows = []
    for region in sorted(by_region):
        for kind in ("town", "castle"):
            fs = by_region[region][kind]
            if not fs:
                continue
            vals = [f["prosperity"] for f in fs]
            flat = Counter(vals).most_common(1)[0]
            rows.append([region, kind, len(fs), min(vals), median(vals), max(vals),
                         f"{flat[1]}@{flat[0]}" if flat[1] >= cluster_threshold else "-"])
    lines.append(md_table(["region", "class", "n", "min", "med", "max", "flat cluster"], rows))
    lines.append("")

    # 4. flags
    lines.append("## 4. Flags")
    all_fief_values = Counter((f["kind"], f["prosperity"]) for f in fiefs)
    clusters = [(k, p, n) for (k, p), n in all_fief_values.items() if n >= cluster_threshold]
    lines.append("### Exact-value clusters (generator defaults)")
    lines.append(md_table(["class", "value", "count"],
                          [[k, p, n] for k, p, n in sorted(clusters, key=lambda x: -x[2])]))
    low = [f for f in fiefs if f["prosperity"] < 1000]
    lines.append("")
    lines.append(f"### Fiefs below 1000 prosperity: {len(low)} "
                 f"({sum(1 for f in low if f['kind'] == 'castle')} castles, "
                 f"{sum(1 for f in low if f['kind'] == 'town')} towns)")
    zero_v = sorted(f["id"] for f in fiefs if f["n_villages"] == 0)
    lines.append(f"### Zero-bound-village fiefs (no production offset): {', '.join(zero_v) or 'none'}")
    over = [f["id"] for f in fiefs if f["prosperity"] > 6000]
    lines.append(f"### Above 6000 (negative housing-growth zone): {', '.join(over) or 'none'}")
    lines.append("")

    # 5. town gold equilibria + proposed targets
    lines.append("## 5. Towns — gold equilibrium and proposed rebaseline")
    lines.append("`equil.` = starting-prosperity gold-regen target; vanilla base 10000, TAOM shipped base "
                 "25000 (`settlement_economy_config.json`). `proposed` = lift-only quantile-map target.")
    rows = []
    for f in sorted((f for f in fiefs if f["kind"] == "town"), key=lambda x: x["prosperity"]):
        p, t = f["prosperity"], targets[f["id"]]
        rows.append([f["id"], f["culture"], f["n_villages"], p,
                     VANILLA_GOLD_BASE + GOLD_PER_PROSPERITY * p,
                     TAOM_GOLD_BASE + GOLD_PER_PROSPERITY * p,
                     t, f"{t - p:+d}" if t != p else "-"])
    lines.append(md_table(["town", "culture", "villages", "prosperity", "equil. (vanilla base)",
                           "equil. (TAOM base)", "proposed", "delta"], rows))
    lines.append("")

    changed = sum(1 for f in fiefs if targets[f["id"]] != f["prosperity"])
    lines.append(f"**Proposed rebaseline would change {changed}/{len(fiefs)} fiefs** — preview with "
                 "`python tools/rebalance_settlement_prosperity.py` (dry-run), write with `--apply`.")
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description="Read-only TAOM_Map starting-prosperity report (#317).")
    ap.add_argument("--stdout", action="store_true", help="print executive summary to stdout")
    ap.add_argument("--cluster-threshold", type=int, default=5, help="flag values shared by >= N fiefs")
    ap.add_argument("--game-dir", default=None, help="Bannerlord install root")
    args = ap.parse_args()

    taom = rb.parse_settlements(rb.live_settlements_path(args.game_dir))
    vanilla = rb.parse_settlements(rb.vanilla_settlements_path(args.game_dir))
    targets, fiefs = rb.compute_targets(taom, vanilla)

    report = build_report(taom, vanilla, targets, fiefs, args.cluster_threshold)

    os.makedirs(REPORT_DIR, exist_ok=True)
    md_path = os.path.join(REPORT_DIR, "REPORT.md")
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(report + "\n")
    html = ("<!doctype html><meta charset='utf-8'><title>TAOM settlement prosperity</title>"
            "<style>body{font-family:monospace;max-width:1100px;margin:2em auto}"
            "table{border-collapse:collapse}td,th{border:1px solid #999;padding:2px 8px;text-align:right}"
            "th{background:#eee}td:first-child,th:first-child{text-align:left}</style><body><pre>"
            + report.replace("&", "&amp;").replace("<", "&lt;") + "</pre>")
    with open(os.path.join(REPORT_DIR, "REPORT.html"), "w", encoding="utf-8") as f:
        f.write(html)
    with open(os.path.join(REPORT_DIR, "settlement-prosperity.json"), "w", encoding="utf-8") as f:
        json.dump({"fiefs": [dict(f, proposed=targets[f["id"]]) for f in fiefs]}, f, indent=1)

    print(f"Reports written to {REPORT_DIR}")
    if args.stdout:
        taom_towns = [f["prosperity"] for f in fiefs if f["kind"] == "town"]
        taom_castles = [f["prosperity"] for f in fiefs if f["kind"] == "castle"]
        changed = sum(1 for f in fiefs if targets[f["id"]] != f["prosperity"])
        print(f"towns: n={len(taom_towns)} med={median(taom_towns)}  "
              f"castles: n={len(taom_castles)} med={median(taom_castles)}  "
              f"proposed changes: {changed}/{len(fiefs)}")


if __name__ == "__main__":
    sys.exit(main())
