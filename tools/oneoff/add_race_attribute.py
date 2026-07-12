"""Stamp race="elf" onto every Rivendell / Mirkwood / Lothlorien NPCCharacter.

Walks each in-scope XML, finds <NPCCharacter ...> opening-tag spans (single-line
or multi-line), and inserts race="elf" directly after the id="..." attribute when
the entry's culture is one of the three elven cultures and no race attribute is
present. Idempotent. Re-parses every modified file as a well-formedness check.

Usage:
    python tools/add_race_attribute.py           # dry-run (default)
    python tools/add_race_attribute.py --apply   # write changes
"""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"

ELVEN_CULTURES = {
    "Culture.rivendell",
    "Culture.mirkwood",
    "Culture.lothlorien",
}

TARGET_FILES = [
    MODULEDATA / "characters" / "lords.xml",
    MODULEDATA / "characters" / "clans.xml",
    MODULEDATA / "characters" / "npcs_rivendell.xml",
    MODULEDATA / "characters" / "npcs_mirkwood.xml",
    MODULEDATA / "characters" / "npcs_lothlorien.xml",
    MODULEDATA / "troops" / "troops_rivendell.xml",
    MODULEDATA / "troops" / "troops_mirkwood.xml",
    MODULEDATA / "taom_wanderers.xml",
    MODULEDATA / "taom_education_character_templates.xml",
]

NPC_OPEN_RE = re.compile(r"<NPCCharacter(?=[\s>])")
CULTURE_RE = re.compile(r'culture="([^"]+)"')
RACE_RE = re.compile(r'race\s*=\s*"([^"]*)"')
ID_RE = re.compile(r'(id="[^"]*")')
ID_LINE_RE = re.compile(r'^(\s*)id="[^"]*"\s*$')


class Stats:
    def __init__(self) -> None:
        self.added = 0
        self.normalized = 0
        self.skipped_already_set = 0
        self.skipped_non_elven = 0
        self.unhandled = 0


def find_open_tag_end(text: str, start: int) -> int:
    """Return the index of the closing '>' of the opening tag starting at *start*.

    Walks character-by-character, tracking quoted-string state so a '>' inside
    an attribute value is not mistaken for the tag end.
    """
    i = start
    in_quote = False
    quote_char = ""
    while i < len(text):
        ch = text[i]
        if in_quote:
            if ch == quote_char:
                in_quote = False
        else:
            if ch in ('"', "'"):
                in_quote = True
                quote_char = ch
            elif ch == ">":
                return i
        i += 1
    raise ValueError(f"Unterminated <NPCCharacter ...> tag starting at {start}")


def process_text(text: str, stats: Stats) -> str:
    """Return updated text with race="elf" inserted on matching NPCCharacter tags."""
    pieces: list[str] = []
    pos = 0
    for match in NPC_OPEN_RE.finditer(text):
        tag_start = match.start()
        tag_end = find_open_tag_end(text, tag_start)
        # Open-tag span is text[tag_start:tag_end+1] (includes the closing '>').
        open_tag = text[tag_start : tag_end + 1]

        culture_match = CULTURE_RE.search(open_tag)
        if not culture_match or culture_match.group(1) not in ELVEN_CULTURES:
            # Append everything up to and including this unchanged tag.
            pieces.append(text[pos : tag_end + 1])
            pos = tag_end + 1
            if culture_match:
                stats.skipped_non_elven += 1
            continue

        race_match = RACE_RE.search(open_tag)
        if race_match:
            existing = race_match.group(0)  # e.g. 'race ="elf"' or 'race="elf"'
            if existing == 'race="elf"':
                # Already canonical — leave untouched.
                pieces.append(text[pos : tag_end + 1])
                pos = tag_end + 1
                stats.skipped_already_set += 1
                continue
            if race_match.group(1) == "elf":
                # Whitespace-quirk normalization (e.g. 'race ="elf"' -> 'race="elf"').
                new_open_tag = open_tag[: race_match.start()] + 'race="elf"' + open_tag[race_match.end() :]
                pieces.append(text[pos:tag_start])
                pieces.append(new_open_tag)
                pos = tag_end + 1
                stats.normalized += 1
                continue
            # Set to a non-elf race — leave alone, do not overwrite.
            pieces.append(text[pos : tag_end + 1])
            pos = tag_end + 1
            stats.skipped_already_set += 1
            continue

        # No race attribute and culture is elven: insert race="elf" after id="...".
        new_open_tag = insert_race_attribute(open_tag)
        if new_open_tag is None:
            stats.unhandled += 1
            pieces.append(text[pos : tag_end + 1])
            pos = tag_end + 1
            continue
        pieces.append(text[pos:tag_start])
        pieces.append(new_open_tag)
        pos = tag_end + 1
        stats.added += 1

    pieces.append(text[pos:])
    return "".join(pieces)


def insert_race_attribute(open_tag: str) -> str | None:
    """Insert race="elf" into the open tag string at the canonical position.

    For single-line tags: place ` race="elf"` right after the id="..." attribute.
    For multi-line tags: insert a new line containing `race="elf"` directly below
    the id="..." line, mirroring its indentation.

    Returns None if the tag has no recognisable id attribute (caller treats as
    unhandled and leaves the tag untouched).
    """
    lines = open_tag.split("\n")
    # Multi-line: find the id="..." line and insert below it.
    if len(lines) > 1:
        for idx, line in enumerate(lines):
            id_line_match = ID_LINE_RE.match(line)
            if id_line_match:
                indent = id_line_match.group(1)
                lines.insert(idx + 1, f'{indent}race="elf"')
                return "\n".join(lines)
        # Multi-line tag but id is on the same line as <NPCCharacter — fall through
        # to single-line treatment on the first line.

    # Single-line tag: insert after id="..." attribute.
    new, n = ID_RE.subn(r'\1 race="elf"', open_tag, count=1)
    if n == 1:
        return new
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--apply", action="store_true", help="Write changes (default is dry-run).")
    parser.add_argument("--dry-run", action="store_true", help="Dry-run (default behaviour).")
    args = parser.parse_args()

    apply_changes = args.apply and not args.dry_run
    mode = "APPLY" if apply_changes else "DRY-RUN"

    grand = Stats()
    failures: list[str] = []

    for file_path in TARGET_FILES:
        if not file_path.exists():
            print(f"  SKIP (missing): {file_path}")
            continue
        original = file_path.read_text(encoding="utf-8")
        stats = Stats()
        updated = process_text(original, stats)

        # Roll into grand totals
        grand.added += stats.added
        grand.normalized += stats.normalized
        grand.skipped_already_set += stats.skipped_already_set
        grand.skipped_non_elven += stats.skipped_non_elven
        grand.unhandled += stats.unhandled

        rel = file_path.relative_to(REPO_ROOT)
        print(
            f"  {rel}: +{stats.added} added, {stats.normalized} normalized, "
            f"{stats.skipped_already_set} already-set, "
            f"{stats.skipped_non_elven} non-elven, {stats.unhandled} unhandled"
        )

        if updated == original:
            continue
        if apply_changes:
            file_path.write_text(updated, encoding="utf-8")
            # Re-parse self-check.
            try:
                ET.fromstring(updated)
            except ET.ParseError as exc:
                failures.append(f"{rel}: XML parse error after edit: {exc}")
        else:
            # Dry-run: still validate the would-be output parses.
            try:
                ET.fromstring(updated)
            except ET.ParseError as exc:
                failures.append(f"{rel}: would produce malformed XML: {exc}")

    print()
    print(f"=== {mode} TOTAL ===")
    print(
        f"  +{grand.added} added, {grand.normalized} normalized, "
        f"{grand.skipped_already_set} already-set, "
        f"{grand.skipped_non_elven} non-elven, {grand.unhandled} unhandled"
    )

    if failures:
        print()
        print("FAILURES:")
        for f in failures:
            print(f"  {f}")
        return 2

    if grand.unhandled:
        print()
        print(f"WARNING: {grand.unhandled} elven NPCCharacter tag(s) lacked a recognisable id attribute and were left untouched.")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
