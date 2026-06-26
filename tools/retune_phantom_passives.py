#!/usr/bin/env python3
"""Re-tune the six career "phantom" PassiveEffect types into a 10-15% band.

Context: HorseChargeDamage / HorseHealth / StealthBonus / TroopResistance / Ammo /
HealthRegeneration were authored with no runtime consumer, so their magnitudes were never
applied and drifted (flat HP like 30/60/100, raw ammo counts like 5/8, fractions 0.03-0.20).
Now that consumers are wired, the maintainer wants every one of these to be ~a 10-15% increase,
expressed as a percentage multiplier (magnitude in [0.10, 0.15]). HorseHealth + Ammo consumers
became multiplicative to match, so their flat values must also become fractions.

The value is scaled by the pip's career tier (higher tier = bigger bonus), staying in band:
  root / standalone (tier 0) -> 0.10
  tier 1                     -> 0.10
  tier 2                     -> 0.13
  tier 3                     -> 0.15

Line-oriented + tier-tracked so the 559 KB file's formatting/comments are preserved (one
<PassiveEffect> per line, flat <ChoiceGroup> nesting — both verified). Only the magnitude=/value=
number on a phantom-type PassiveEffect line is rewritten; everything else is byte-identical.

Usage:  python tools/retune_phantom_passives.py [--dry-run | --apply] [--file PATH]
"""
import argparse
import re
import sys
from pathlib import Path

PHANTOM_TYPES = {
    "HorseChargeDamage", "HorseHealth", "StealthBonus",
    "TroopResistance", "Ammo", "HealthRegeneration",
}

TIER_VALUE = {0: "0.10", 1: "0.10", 2: "0.13", 3: "0.15"}

DEFAULT_FILE = Path("Main/_Module/ModuleData/career_system/taom_career_choices.xml")

_GROUP_OPEN = re.compile(r"<ChoiceGroup\b[^>]*\btier=\"(\d+)\"")
_GROUP_CLOSE = re.compile(r"</ChoiceGroup>")
_PASSIVE = re.compile(r"<PassiveEffect\b[^>]*>")
_TYPE = re.compile(r'type="([^"]+)"')
_MAG = re.compile(r'(magnitude=")([^"]*)(")')
_VAL = re.compile(r'(value=")([^"]*)(")')


def retune(text: str):
    """Return (new_text, changes) where changes is a list of (line_no, type, old, new)."""
    out_lines = []
    changes = []
    current_tier = 0  # root / standalone context until a ChoiceGroup opens

    for i, line in enumerate(text.splitlines(keepends=True), start=1):
        g = _GROUP_OPEN.search(line)
        if g:
            current_tier = int(g.group(1))
        elif _GROUP_CLOSE.search(line):
            current_tier = 0

        pe = _PASSIVE.search(line)
        if pe:
            tmatch = _TYPE.search(pe.group(0))
            if tmatch and tmatch.group(1) in PHANTOM_TYPES:
                new_val = TIER_VALUE.get(current_tier, "0.10")
                # magnitude= is the effective attribute (parser precedence); rewrite it if
                # present, else the wrapper-schema value= alias. Never touch both.
                if _MAG.search(line):
                    old = _MAG.search(line).group(2)
                    if old != new_val:
                        line = _MAG.sub(lambda m: m.group(1) + new_val + m.group(3), line, count=1)
                        changes.append((i, tmatch.group(1), old, new_val))
                elif _VAL.search(line):
                    old = _VAL.search(line).group(2)
                    if old != new_val:
                        line = _VAL.sub(lambda m: m.group(1) + new_val + m.group(3), line, count=1)
                        changes.append((i, tmatch.group(1), old, new_val))

        out_lines.append(line)

    return "".join(out_lines), changes


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", help="report changes, write nothing (default)")
    g.add_argument("--apply", action="store_true", help="write changes (.bak backup first)")
    ap.add_argument("--file", type=Path, default=DEFAULT_FILE)
    args = ap.parse_args()

    path = args.file
    if not path.exists():
        print(f"ERROR: {path} not found (run from repo root)", file=sys.stderr)
        return 2

    text = path.read_text(encoding="utf-8")
    new_text, changes = retune(text)

    by_type = {}
    for _, t, old, new in changes:
        by_type.setdefault(t, []).append((old, new))
    print(f"{'APPLY' if args.apply else 'DRY-RUN'}: {len(changes)} PassiveEffect magnitudes re-tuned in {path}")
    for t in sorted(by_type):
        olds = {}
        for old, _new in by_type[t]:
            olds[old] = olds.get(old, 0) + 1
        print(f"  {t:20s} {len(by_type[t]):3d} entries; old values: "
              + ", ".join(f"{o}x{c}" for o, c in sorted(olds.items())))

    if args.apply:
        path.with_suffix(path.suffix + ".bak").write_text(text, encoding="utf-8")
        path.write_text(new_text, encoding="utf-8")
        print(f"WROTE {path} (backup at {path}.bak)")
    else:
        print("(dry-run — pass --apply to write)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
