#!/usr/bin/env python
"""Public-text gate: check an issue body, an issue comment or a commit message before it is posted.

Usage: python tools/check_public_text.py FILE...

Exit 1 with one `file:line: rule: text` line per finding, exit 0 when every file is clean, exit 2
when a file cannot be read. Rules:

- em-dash, en-dash: through lint_docs.scan_text_for_dashes, so code spans, fenced blocks, link
  targets and a `<!-- lint-allow-dash -->` line are exempt (AGENTS.md "Human prose").
- local-path: a drive-letter path with either slash (`X:\\`, `X:/`, so `C:\\Users` too) or a Git
  Bash drive path (`/x/...`), anywhere on the line, code spans included: a local path leaks either
  way. A URL (`https://host/a/b`) and a PowerShell drive (`Env:\\TEMP`) pass.
- placeholder: a leftover token such as `MERGE_HASH` (four or more capitals, then `_ISSUE`,
  `_HASH`, `_TOTALS`, `_RESULT` or `_DOCS`), or a line that is only `TODO`; code spans are
  exempt, so a constant quoted from the code passes.

The words Claude and Codex are not flagged: issues legitimately discuss Claude Code hooks.
Pure stdlib.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from lint_docs import INLINE_CODE_RE, scan_text_for_dashes  # noqa: E402

LOCAL_PATH_RE = re.compile(r"(?<![A-Za-z0-9])[A-Za-z]:[\\/](?!/)|(?<![\w./-])/[a-z]/[A-Za-z0-9_.-]+")
PLACEHOLDER_RE = re.compile(r"[A-Z]{4,}_(?:ISSUE|HASH|TOTALS|RESULT|DOCS)|^\s*TODO\s*$")


def check_text(text: str) -> list[tuple[int, str, str]]:
    """Every finding in text as (line number, rule, stripped line), in line order."""
    findings = [(line, kind, raw) for _, line, kind, raw in scan_text_for_dashes(Path(), text)]
    for number, raw in enumerate(text.splitlines(), 1):
        if LOCAL_PATH_RE.search(raw):
            findings.append((number, "local-path", raw.strip()[:120]))
        if PLACEHOLDER_RE.search(INLINE_CODE_RE.sub(" ", raw)):
            findings.append((number, "placeholder", raw.strip()[:120]))
    return sorted(findings, key=lambda f: f[0])


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("files", nargs="+", metavar="FILE")
    args = parser.parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="backslashreplace")
    found = 0
    for name in args.files:
        try:
            text = Path(name).read_text(encoding="utf-8-sig")
        except (OSError, UnicodeDecodeError) as exc:
            print(f"{name}: cannot read: {exc}", file=sys.stderr)
            return 2
        for line, rule, raw in check_text(text):
            print(f"{name}:{line}: {rule}: {raw}")
            found += 1
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
