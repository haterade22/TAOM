"""Comment-line ratchet for production C# (ADR-009).

Counts the lines that hold a // or /* */ comment in each .cs file under Main, Client,
CoopCompat and Dependencies, with the rule RepoPaths.CommentLineCount uses in
TAOM.Tests. CommentRatchetGateTests fails when a file's count moves away from
tools/comment-ratchet-baseline.txt in either direction.

    python tools/comment_ratchet.py --regen     write the baseline after a sweep
    python tools/comment_ratchet.py --report    totals per feature folder
"""

import argparse
import codecs
import os
import re
import sys
from collections import Counter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOTS = ("Main", "Client", "CoopCompat", "Dependencies")
BASELINE = os.path.join(REPO, "tools", "comment-ratchet-baseline.txt")
COMMENT = re.compile(r"/\*.*?\*/|//[^\n]*", re.S)
HEADER = (
    "# ADR-009 comment-line ratchet: <path><TAB><count>, counts may only fall.\n"
    "# Written by python tools/comment_ratchet.py --regen; read by CommentRatchetGateTests.\n"
)


def comment_line_count(source):
    text = source.replace("\r\n", "\n")
    lines = set()
    position = 0
    line = 0
    for match in COMMENT.finditer(text):
        line += text.count("\n", position, match.start())
        position = match.start()
        value = match.group(0)
        if value.startswith("///") and not value.startswith("////"):
            continue
        last = line + value.count("\n")
        lines.update(range(line, last + 1))
    return len(lines)


def read_text(path):
    with open(path, "rb") as handle:
        data = handle.read()
    if data.startswith(codecs.BOM_UTF8):
        return data[3:].decode("utf-8", errors="replace")
    if data.startswith(codecs.BOM_UTF16_LE) or data.startswith(codecs.BOM_UTF16_BE):
        return data.decode("utf-16", errors="replace")
    return data.decode("utf-8", errors="replace")


def scan(repo=REPO):
    counts = {}
    for root in ROOTS:
        for folder, dirs, files in os.walk(os.path.join(repo, root)):
            dirs[:] = [d for d in dirs if d not in ("obj", "bin")]
            for name in files:
                if not name.lower().endswith(".cs"):
                    continue
                path = os.path.join(folder, name)
                relative = os.path.relpath(path, repo).replace("\\", "/")
                counts[relative] = comment_line_count(read_text(path))
    return counts


def write_baseline(counts, out):
    rows = sorted((path, count) for path, count in counts.items() if count > 0)
    with open(out, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(HEADER)
        for path, count in rows:
            handle.write(f"{path}\t{count}\n")
    return len(rows)


def folder_of(path):
    parts = path.split("/")
    return "/".join(parts[:3]) if len(parts) > 3 and parts[1] == "Features" else "/".join(parts[:2])


def report(counts, top):
    totals = Counter()
    for path, count in counts.items():
        totals[folder_of(path)] += count
    for folder, count in totals.most_common(top or None):
        print(f"{count:7d}  {folder}")
    print(f"{sum(counts.values()):7d}  total comment lines in {sum(1 for c in counts.values() if c)} of {len(counts)} files")


def main(argv=None, repo=REPO):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--regen", action="store_true", help="write the baseline")
    parser.add_argument("--report", action="store_true", help="print totals per folder")
    parser.add_argument("--top", type=int, default=30, help="folders to list in the report; 0 lists all")
    parser.add_argument("--out", default=BASELINE, help="baseline path for --regen")
    args = parser.parse_args(argv)
    if not (args.regen or args.report):
        parser.error("give --regen or --report")

    counts = scan(repo)
    if len(counts) < 1000:
        print(f"only {len(counts)} .cs files found under {', '.join(ROOTS)}: wrong repo root?", file=sys.stderr)
        return 2
    if args.regen:
        rows = write_baseline(counts, args.out)
        print(f"wrote {rows} entries to {os.path.relpath(args.out, REPO)}")
    if args.report:
        report(counts, args.top)
    return 0


if __name__ == "__main__":
    sys.exit(main())
