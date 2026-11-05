#!/usr/bin/env python3
"""Compare two [SaveLoad] phase=SaveCensus blocks from one taom_debug log.

Run:  python tools/diff_save_census.py <taom_debug.log> [--from N] [--to M] [--top K]

Blocks are numbered from 1 in file order; the default is the last two. Prints the object and
container deltas, then the per-type changes in n, largest absolute change first. It names what a save
gained between two reloads: docs/investigations/save-growth-2026-11.md. Stdlib only, read-only.
"""
import argparse
import re
import sys

MARKER = "phase=SaveCensus "
ROW = re.compile(r"^(obj|ctr) n=(\d+)(?: elements=(\d+))?.*type='(.*)'$")
SUMMARY_COUNT = re.compile(r"\b(objects|containers)=(\d+)")


def parse_blocks(lines):
    """Return one dict per census: {"summary", "objects", "containers", "rows": {(kind, type): n}}."""
    blocks = []
    for line in lines:
        at = line.find(MARKER)
        if at < 0:
            continue
        part = line[at + len(MARKER):].strip()
        if part.startswith("reason=") or " reason=" in part:
            counts = dict(SUMMARY_COUNT.findall(part))
            blocks.append({
                "summary": part,
                "objects": int(counts.get("objects", 0)),
                "containers": int(counts.get("containers", 0)),
                "rows": {},
            })
            continue
        match = ROW.match(part)
        if match and blocks:
            blocks[-1]["rows"][(match.group(1), match.group(4))] = int(match.group(2))
    return blocks


def diff_blocks(first, second, top):
    """Summary deltas plus the top rows by absolute change in n; a type absent from a block counts as 0."""
    rows = []
    for key in set(first["rows"]) | set(second["rows"]):
        before = first["rows"].get(key, 0)
        after = second["rows"].get(key, 0)
        if before != after:
            rows.append({"kind": key[0], "type": key[1], "from": before, "to": after, "delta": after - before})
    rows.sort(key=lambda row: (-abs(row["delta"]), row["kind"], row["type"]))
    return {
        "objects": second["objects"] - first["objects"],
        "containers": second["containers"] - first["containers"],
        "rows": rows[:top],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Diff two [SaveLoad] phase=SaveCensus blocks from a taom_debug log.")
    parser.add_argument("log", help="path to a taom_debug log")
    parser.add_argument("--from", dest="first", type=int, help="first block, numbered from 1 (default: second to last)")
    parser.add_argument("--to", dest="second", type=int, help="second block, numbered from 1 (default: last)")
    parser.add_argument("--top", type=int, default=40, help="rows to print (default 40)")
    args = parser.parse_args(argv)

    with open(args.log, encoding="utf-8", errors="replace") as log:
        blocks = parse_blocks(log)
    if len(blocks) < 2:
        print(f"{args.log}: fewer than two SaveCensus blocks ({len(blocks)} found)", file=sys.stderr)
        return 2

    first_no = args.first if args.first is not None else len(blocks) - 1
    second_no = args.second if args.second is not None else len(blocks)
    for number in (first_no, second_no):
        if not 1 <= number <= len(blocks):
            print(f"block {number} is out of range: the log has {len(blocks)}", file=sys.stderr)
            return 2

    first, second = blocks[first_no - 1], blocks[second_no - 1]
    result = diff_blocks(first, second, args.top)
    print(f"block {first_no}: {first['summary']}")
    print(f"block {second_no}: {second['summary']}")
    print(f"objects {result['objects']:+d}, containers {result['containers']:+d}")
    print(f"{'kind':<5}{'from':>10}{'to':>10}{'delta':>10}  type")
    for row in result["rows"]:
        print(f"{row['kind']:<5}{row['from']:>10}{row['to']:>10}{row['delta']:>+10}  {row['type']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
