#!/usr/bin/env python3
"""Raise max_value to 50 on party-template stacks (bandits + kingdom hero parties).

One-off balance retune (2026-07-02, issue: bandit parties averaged 20-25 on the map):
sets max_value="50" on every PartyTemplateStack in

  * the 8 bandit cultures' {culture}_raider_party_template + {culture}_boss_party_template
  * ALL kingdom_hero_party_* templates (base + mercenary + outlaw + per-clan variants)

in Main/_Module/ModuleData/taom_partyTemplates.xml.

Rules:
  * min_value is never touched.
  * A max_value already >= 50 is never lowered (left as-is, reported).
  * The dedicated hideout-boss hero stack (min_value="1" max_value="1", troop id
    ending "_boss") is left untouched -- one boss per hideout is load-bearing for
    the boss-conversation flow (see docs/features/bandit-management.md).

Usage:
    python tools/raise_party_template_maxes.py            # dry-run (default)
    python tools/raise_party_template_maxes.py --apply
"""

import argparse
import re
import sys
from collections import OrderedDict
from pathlib import Path

TARGET_FILE = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "taom_partyTemplates.xml"

NEW_MAX = 50

BANDIT_CULTURES = (
    "dunland_raiders",
    "rhun_raiders",
    "harad_raiders",
    "gundabad_raiders",
    "umbar_corsairs",
    "gondor_soldiers",
    "erebor_warriors",
    "mirkwood_stalkers",
)

BANDIT_TEMPLATE_RE = re.compile(
    r"^(?:%s)_(?:raider|boss)_party_template$" % "|".join(BANDIT_CULTURES)
)

TEMPLATE_OPEN_RE = re.compile(r'<MBPartyTemplate\s+id="([^"]+)"')
TEMPLATE_CLOSE_RE = re.compile(r"</MBPartyTemplate>")
STACK_RE = re.compile(
    r'<PartyTemplateStack\s+min_value="(\d+)"\s+max_value="(\d+)"\s+troop="NPCCharacter\.([^"]+)"'
)


def template_in_scope(template_id):
    if template_id.startswith("kingdom_hero_party_"):
        return True
    return bool(BANDIT_TEMPLATE_RE.match(template_id))


def process(lines):
    """Return (new_lines, per_template_counts, skipped_boss, skipped_high, malformed)."""
    new_lines = []
    counts = OrderedDict()  # template_id -> changed stack count
    skipped_boss = []
    skipped_high = []
    malformed = []
    current_id = None
    in_scope = False

    for lineno, line in enumerate(lines, start=1):
        m_open = TEMPLATE_OPEN_RE.search(line)
        if m_open:
            current_id = m_open.group(1)
            in_scope = template_in_scope(current_id)
            if in_scope:
                counts.setdefault(current_id, 0)
            new_lines.append(line)
            continue

        if TEMPLATE_CLOSE_RE.search(line):
            current_id = None
            in_scope = False
            new_lines.append(line)
            continue

        if in_scope and "<PartyTemplateStack" in line:
            m = STACK_RE.search(line)
            if not m:
                malformed.append((lineno, current_id, line.strip()))
                new_lines.append(line)
                continue
            min_v, max_v, troop = int(m.group(1)), int(m.group(2)), m.group(3)
            if min_v == 1 and max_v == 1 and troop.endswith("_boss"):
                skipped_boss.append((current_id, troop))
                new_lines.append(line)
                continue
            if max_v >= NEW_MAX:
                if max_v > NEW_MAX:
                    skipped_high.append((current_id, troop, max_v))
                new_lines.append(line)
                continue
            new_lines.append(
                line.replace('max_value="%d"' % max_v, 'max_value="%d"' % NEW_MAX, 1)
            )
            counts[current_id] += 1
            continue

        new_lines.append(line)

    return new_lines, counts, skipped_boss, skipped_high, malformed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    args = parser.parse_args()

    if not TARGET_FILE.exists():
        print("ERROR: target file not found: %s" % TARGET_FILE)
        return 1

    # newline="" preserves the file's CRLF endings through the round-trip
    with open(TARGET_FILE, encoding="utf-8", newline="") as f:
        text = f.read()
    lines = text.splitlines(keepends=True)
    new_lines, counts, skipped_boss, skipped_high, malformed = process(lines)

    bandit_ids = [t for t in counts if BANDIT_TEMPLATE_RE.match(t)]
    hero_ids = [t for t in counts if t.startswith("kingdom_hero_party_")]

    print("Templates in scope: %d bandit, %d kingdom_hero_party_*" % (len(bandit_ids), len(hero_ids)))
    print()
    print("Bandit templates (changed stacks):")
    for t in bandit_ids:
        print("  %-45s %d" % (t, counts[t]))
    bandit_total = sum(counts[t] for t in bandit_ids)
    hero_total = sum(counts[t] for t in hero_ids)
    print("  bandit stacks changed: %d" % bandit_total)
    print()
    print("kingdom_hero_party_* stacks changed: %d (across %d templates)" % (hero_total, len(hero_ids)))
    zero_hero = [t for t in hero_ids if counts[t] == 0]
    if zero_hero:
        print("  templates with 0 changes (all stacks already >= %d):" % NEW_MAX)
        for t in zero_hero:
            print("    %s" % t)
    print()
    print("Boss hero stacks left untouched (1/1): %d" % len(skipped_boss))
    for t, troop in skipped_boss:
        print("  %-45s %s" % (t, troop))
    if skipped_high:
        print()
        print("Stacks already above %d (left as-is): %d" % (NEW_MAX, len(skipped_high)))
        for t, troop, v in skipped_high:
            print("  %-45s %-35s max=%d" % (t, troop, v))
    if malformed:
        print()
        print("WARNING: %d PartyTemplateStack lines in scope did not match the expected single-line shape:" % len(malformed))
        for lineno, t, snippet in malformed:
            print("  line %d (%s): %s" % (lineno, t, snippet))

    total = bandit_total + hero_total
    print()
    if not args.apply:
        print("DRY-RUN: %d stacks would be changed. Re-run with --apply to write." % total)
        return 0

    with open(TARGET_FILE, "w", encoding="utf-8", newline="") as f:
        f.write("".join(new_lines))
    print("APPLIED: %d stacks changed in %s" % (total, TARGET_FILE))
    return 0


if __name__ == "__main__":
    sys.exit(main())
