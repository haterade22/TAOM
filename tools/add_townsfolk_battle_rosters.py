#!/usr/bin/env python3
"""Add a battle EquipmentRoster (mirroring each civilian one) to civilian-only NPCs.

Why: arena spectators are the settlement culture's townsfolk/notables (e.g. townsman_erebor),
spawned with BATTLE equipment. Every TAOM culture's townsfolk/notables ship a civilian-ONLY
roster (`<EquipmentRoster civilian="true">`, no plain battle roster), so FirstBattleEquipment is
empty -> the arena crowd renders naked while the town walk (civilian equipment) is clothed.
Confirmed in-game: `[MissionDiag][NakedSuspect] char='townsman_erebor' ... Body=empty Leg=empty`.

Fix: for every <NPCCharacter> whose <Equipments> contains ONLY civilian="true" rosters, append a
plain (battle) twin of each civilian roster (same items, `civilian="true"` removed). The character
then renders identically in the arena (battle) and town (civilian). Idempotent: a character that
already has any non-civilian roster is skipped, so re-running is a no-op.

Scope: Main/_Module/ModuleData/characters/npcs_*.xml (all cultures' townsfolk + notables).

I/O convention (tools/README.md): preserve UTF-8 BOM + CRLF + non-ASCII; formatting-preserving
(regex line edits, NOT a reparse). Run with --dry-run first; --apply writes.
"""
import argparse
import glob
import os
import re
import sys

# Each NPCCharacter has exactly one <Equipments>...</Equipments>; processing per-block == per-NPC.
EQUIP_BLOCK_RE = re.compile(r"(<Equipments\s*>)(.*?)(</Equipments>)", re.DOTALL)
ROSTER_RE = re.compile(r"(\s*)<EquipmentRoster\b([^>]*)>(.*?)</EquipmentRoster>", re.DOTALL)
CIVILIAN_ATTR_RE = re.compile(r"""\s*civilian\s*=\s*(["'])true\1""")


def _is_civilian(attrs: str) -> bool:
    return CIVILIAN_ATTR_RE.search(attrs) is not None


def _process_equip_inner(inner: str):
    """Return (new_inner, changed). Appends battle twins iff the block is civilian-only."""
    rosters = list(ROSTER_RE.finditer(inner))
    if not rosters:
        return inner, False
    # If any roster is already a battle (non-civilian) roster, the NPC is fine — skip (idempotent).
    if any(not _is_civilian(r.group(2)) for r in rosters):
        return inner, False

    twins = []
    for r in rosters:
        leading_ws, attrs, body = r.group(1), r.group(2), r.group(3)
        battle_attrs = CIVILIAN_ATTR_RE.sub("", attrs)
        twins.append(f"{leading_ws}<EquipmentRoster{battle_attrs}>{body}</EquipmentRoster>")

    # Insert the twins immediately after the last existing roster (before the trailing indent +
    # </Equipments>), so each twin sits on its own correctly-indented line.
    last_end = rosters[-1].end()
    new_inner = inner[:last_end] + "".join(twins) + inner[last_end:]
    return new_inner, True


def process_text(text: str):
    count = [0]

    def repl(m):
        new_inner, changed = _process_equip_inner(m.group(2))
        if changed:
            count[0] += 1
        return m.group(1) + new_inner + m.group(3)

    return EQUIP_BLOCK_RE.sub(repl, text), count[0]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    ap.add_argument("--glob", default="Main/_Module/ModuleData/characters/npcs_*.xml")
    args = ap.parse_args()

    files = sorted(glob.glob(args.glob))
    if not files:
        print(f"No files matched: {args.glob}", file=sys.stderr)
        return 1

    total = 0
    for path in files:
        raw = open(path, "rb").read()
        had_bom = raw.startswith(b"\xef\xbb\xbf")
        text = raw.decode("utf-8-sig")
        new_text, n = process_text(text)
        total += n
        status = "WROTE" if (args.apply and n) else ("would add" if n else "ok")
        print(f"  {os.path.basename(path):28} {n:3} characters {status}")
        if args.apply and n:
            out = (b"\xef\xbb\xbf" if had_bom else b"") + new_text.encode("utf-8")
            open(path, "wb").write(out)

    print(f"\n{'APPLIED' if args.apply else 'DRY-RUN'}: {total} civilian-only NPCs "
          f"{'given a' if args.apply else 'would get a'} battle roster across {len(files)} files.")
    if not args.apply and total:
        print("Re-run with --apply to write. (idempotent: re-running skips NPCs that already have a battle roster)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
