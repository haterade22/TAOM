#!/usr/bin/env python3
"""Issue #104 Option B — repurpose 98 dead MaxCharge mutations in taom_career_choices.xml as
CooldownReduction. After the cooldown rework (#103), template.MaxCharge is unread for
CooldownOnly abilities (all 50 careers use cooldown-based gating). Designers editing -20/-30
entries were silently misled. This script:

  - Renames property="MaxCharge" → property="CooldownReduction"
  - Rescales value="-20" → value="6"   (20% × 30s base = 6s reduction per tier-1 keystone)
  - Rescales value="-30" → value="9"   (30% × 30s base = 9s reduction per tier-2/3 keystone)
  - Preserves the 1.5× ratio between the two tiers (was -30/-20 = 1.5, now 9/6 = 1.5)

**Sign convention.** Codex pass-2 (2026-06-02) caught a HIGH bug in the first pass: the
original script emitted negative values (`-6`, `-9`) on the theory that the calculator would
subtract them from the running sum. The `flat` calculator is actually `baseValue + value`
(`BuiltInCalculators.cs:7-8`), so `-6`/`-9` produced negative `CooldownReduction` values, and
`AbilityEffectExecutor` only applies reductions when the property is POSITIVE. Result: the
feature was dead. Re-applying the script after the sign flip makes the 98 mutations
functional. The semantic meaning is "shorten cooldown by N seconds" — a positive number.

Idempotency: the regex now matches either the legacy `MaxCharge` schema OR the broken negative
`CooldownReduction` schema, so re-running on a previously-rewritten file repairs the sign.

Run with --dry-run first to see the diff; --apply mutates the file in place.
"""

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
XML_PATH = ROOT / "Main" / "_Module" / "ModuleData" / "career_system" / "taom_career_choices.xml"

# Match the legacy MaxCharge schema OR the broken negative CooldownReduction schema.
# Whitespace and attribute order pinned to the actual file layout.
PATTERN = re.compile(
    r'property="(?:MaxCharge|CooldownReduction)" calculator="flat" value="(-?20|-?30|-?6|-?9)"'
)


def replace(match):
    raw = match.group(1)
    # Map both the legacy -20/-30 and any existing -6/-9 (from the broken first apply) to
    # positive 6/9. Treat unsigned 6/9/20/30 identically — designer-typed positive is fine.
    abs_v = int(raw.lstrip("-"))
    if abs_v in (20, 6):
        new_value = "6"
    elif abs_v in (30, 9):
        new_value = "9"
    else:
        raise ValueError(f"Unexpected value: {raw}")
    return f'property="CooldownReduction" calculator="flat" value="{new_value}"'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="Write changes to disk")
    parser.add_argument("--dry-run", action="store_true", help="Show what would change (default)")
    args = parser.parse_args()

    if not args.apply and not args.dry_run:
        args.dry_run = True

    if not XML_PATH.exists():
        print(f"ERROR: {XML_PATH} not found", file=sys.stderr)
        sys.exit(1)

    original = XML_PATH.read_text(encoding="utf-8")
    matches = PATTERN.findall(original)
    tier1 = sum(1 for m in matches if int(m.lstrip("-")) in (20, 6))
    tier2 = sum(1 for m in matches if int(m.lstrip("-")) in (30, 9))
    total = tier1 + tier2

    print(f"Found {total} matching mutations: {tier1} × tier-1 (20/6), {tier2} × tier-2/3 (30/9)")
    if total != 98:
        print(f"WARNING: expected 98 mutations, found {total}", file=sys.stderr)

    mutated = PATTERN.sub(replace, original)
    new_count_6 = mutated.count('property="CooldownReduction" calculator="flat" value="6"')
    new_count_9 = mutated.count('property="CooldownReduction" calculator="flat" value="9"')

    print(f"After rewrite: {new_count_6} × value=\"6\", {new_count_9} × value=\"9\"")
    if new_count_6 != tier1:
        print(f"FAIL: tier-1 → 6 count mismatch ({new_count_6} vs {tier1})", file=sys.stderr)
        sys.exit(2)
    if new_count_9 != tier2:
        print(f"FAIL: tier-2/3 → 9 count mismatch ({new_count_9} vs {tier2})", file=sys.stderr)
        sys.exit(2)

    if args.apply:
        XML_PATH.write_text(mutated, encoding="utf-8")
        print(f"OK: wrote {XML_PATH}")
    else:
        print("DRY RUN — pass --apply to commit")


if __name__ == "__main__":
    main()
