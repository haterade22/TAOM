#!/usr/bin/env python3
"""Retarget each culture's lord-party templates to a per-culture max troop sum.

Balance pass 2026-08-14. `PartyTemplateObject`'s max sum is the ceiling of the roster a
lord party receives AT SPAWN: the engine draws a uniform ratio r in [0,1) per party
(`DefaultPartySizeLimitModel.GetInitialPartySizeRatioForMobileParty`, which returns
`party.RandomFloat()` for a lord party) and fills each stack to
`min + (max - min) * r` (`FindAppropriateInitialRosterForMobileParty`). So the expected
spawn roster is the midpoint of the min and max sums, and raising the max sum raises it
linearly.

It is NOT the party's steady-state size. `PartySizeLimit` still governs recruitment and
decay, so a party spawned above its limit bleeds back down. Pairing these numbers with a
party-size-limit change is a separate, deliberate decision.

Scope: every `kingdom_hero_party_..._template` EXCEPT the mercenary and outlaw variants.
Those 34 sit at a flat max_value=50 and nothing in the repo binds them today, so they are
left alone. That leaves 193 templates: 17 culture defaults plus 176 per-clan variants (the
per-clan ones are what most named lords actually use, via `Clan.DefaultPartyTemplate`,
bound in characters/clans.xml or spclans.xslt; the culture default is only the fallback for
a clan that binds nothing). Per-clan ids are usually `<culture>_<clan>_<N>`, but do NOT key
on the trailing digit: Gondor's 14 are `<culture>_<fief>` and have none, with the clan id
absent entirely (clan_empire_west_9 -> kingdom_hero_party_gondor_blackroot_vale_template).
The regex below matches on the mercenary/outlaw exclusion alone, so it covers both shapes.

Method: min_value is never touched. Each stack's SPREAD (max - min) is scaled by the same
factor so the template's max sum lands on the culture target, then rounding drift is
absorbed by the widest stacks. Keeping min fixed guarantees max >= min for every stack,
which the engine relies on: a max below min makes `(max - min) * r` negative and the stack
fills below its floor.

Idempotent: re-running against an already-retargeted file is a no-op, because the target is
absolute rather than a multiplier.

Usage:
    python tools/rebalance_party_template_maxes.py            # dry-run (default)
    python tools/rebalance_party_template_maxes.py --apply
"""

import argparse
import re
import sys
from collections import OrderedDict
from pathlib import Path

TARGET_FILE = (
    Path(__file__).resolve().parent.parent
    / "Main" / "_Module" / "ModuleData" / "taom_partyTemplates.xml"
)

# Culture key -> target max troop sum. Longest key wins on prefix match, so
# `mistymountainorcs` is tested before any shorter key that could shadow it. That only
# disambiguates keys competing for the SAME leading token; it does not help when the id's
# leading token is a different culture entirely. `goblin_bluecraig_1` resolves to `goblin`,
# never `bluecraig`, because the match is on the leading token. Harmless while the two
# targets are equal; diverge them and the five Blue Craig clan templates silently take the
# goblin number.
# Retargeted 2026-09-01 into the vanilla party-size band. These were 4500 / 3500 / 2000 / 1500 / 1000,
# sized for the 10x AI party size cap that shipped at the time. That cap is now neutral by default, so
# a lord's real limit is vanilla's 40-203, and a 3500 template made him spawn ~2,400 men and lose all
# but ~150 of them on the first daily tick. Spawn and cap are independent in the engine:
# FindAppropriateInitialRosterForMobileParty fills each stack to `min + (max - min) * r` and never
# consults PartySizeLimit, so the two have to be kept in the same range by hand.
#
# The ordering is preserved (goblin > orc > dwarf > men > elf) but compressed, because a 4.5:1 spread
# is meaningless once every culture is trimmed to the same 40-203 band anyway.
CULTURE_TARGETS = {
    # Goblin realms: highest, they breed fastest
    "goblin": 320,
    "bluecraig": 320,
    # Orc / uruk kingdoms
    "mordor": 260,
    "isengard": 260,
    "gundabad": 260,
    "dolguldur": 260,
    "mistymountainorcs": 260,
    # Dwarves: still the largest free-peoples host
    "erebor": 220,
    # Men
    "gondor": 200,
    "rohan": 200,
    "dale": 200,
    "dunland": 200,
    "rhun": 200,
    "khand": 200,
    "harad": 200,
    "umbar": 200,
    "shaghana": 200,
    "abanissa": 200,
    # Elves: few, and they stay few
    "rivendell": 150,
    "lothlorien": 150,
    "mirkwood": 150,
    "lindon": 150,
}

# `kingdom_hero_party_<rest>_template`, excluding the mercenary and outlaw variants.
TEMPLATE_ID_RE = re.compile(r"^kingdom_hero_party_(?!mercenary_|outlaw_)(.+)_template$")

TEMPLATE_OPEN_RE = re.compile(r'<MBPartyTemplate\s+id="([^"]+)"')
TEMPLATE_CLOSE_RE = re.compile(r"</MBPartyTemplate>")
STACK_RE = re.compile(
    r'<PartyTemplateStack\s+min_value="(\d+)"\s+max_value="(\d+)"\s+troop="NPCCharacter\.([^"]+)"'
)

# Longest first so `mistymountainorcs` is not shadowed by a shorter prefix.
_KEYS_BY_LENGTH = sorted(CULTURE_TARGETS, key=len, reverse=True)


def culture_for(template_id):
    """Return (culture_key, target) for a lord-party template, or (None, None)."""
    m = TEMPLATE_ID_RE.match(template_id)
    if not m:
        return None, None
    rest = m.group(1)
    for key in _KEYS_BY_LENGTH:
        if rest == key or rest.startswith(key + "_"):
            return key, CULTURE_TARGETS[key]
    return None, None


def retarget(mins, maxes, target):
    """Scale each stack's spread so sum(new_maxes) == target. min is never touched."""
    min_sum = sum(mins)
    max_sum = sum(maxes)

    if target < min_sum:
        raise ValueError("target %d below min sum %d" % (target, min_sum))

    spread = max_sum - min_sum
    if spread <= 0:
        # Every stack is already pinned at max == min. Spread the surplus evenly rather
        # than dividing by zero; no template in scope hits this today, but a hand-edit could.
        extra, rem = divmod(target - min_sum, len(mins))
        return [mn + extra + (1 if i < rem else 0) for i, mn in enumerate(mins)]

    scale = (target - min_sum) / spread
    new_maxes = [mn + int(round((mx - mn) * scale)) for mn, mx in zip(mins, maxes)]

    # A stack that HAD a real spread must keep at least one of it.
    #
    # Without this floor a thin stack rounds to max == min, and when min is 0 that deletes the troop
    # type from the template outright: it can never be drawn by the initial roster fill, and vanilla's
    # new-game top-up weights each stack by `(min + max) / 2`, so a 0/0 stack is weight 0 there too.
    # The loss is also permanent, because the next retarget scales from a spread that is now zero and
    # `0 * anything` stays 0 no matter how high the future target.
    #
    # This shipped once. Going 3500 -> 260 on 2026-09-01 zeroed 45 stacks, six Black Numenorean troop
    # types, across 14 of Mordor's 16 lord templates. Mordor is the exposure because it carries 52
    # stacks against the same budget every other culture spends on 12 to 27, so its thinnest stacks
    # sat at 5/3500 and rounded straight to nothing.
    floors = [mn + 1 if mx > mn else mn for mn, mx in zip(mins, maxes)]
    new_maxes = [max(v, f) for v, f in zip(new_maxes, floors)]

    # Absorb rounding drift on the widest stacks, which are least distorted by +/-1.
    drift = target - sum(new_maxes)
    order = sorted(range(len(mins)), key=lambda i: maxes[i] - mins[i], reverse=True)
    step = 1 if drift > 0 else -1
    idx = 0
    while drift != 0:
        i = order[idx % len(order)]
        # Never push a max below its own floor: min for an already-pinned stack, min + 1 for one that
        # started with a spread. Reducing past that is the silent-deletion bug above.
        if step == -1 and new_maxes[i] <= floors[i]:
            idx += 1
            if idx > len(order) * 4:
                break
            continue
        new_maxes[i] += step
        drift -= step
        idx += 1
    return new_maxes


def process(lines):
    """Return (new_lines, report_rows, unmapped)."""
    # Pass 1: collect each in-scope template's stack line numbers and values.
    templates = OrderedDict()
    unmapped = []
    current = None
    for lineno, line in enumerate(lines):
        m_open = TEMPLATE_OPEN_RE.search(line)
        if m_open:
            tid = m_open.group(1)
            key, target = culture_for(tid)
            if TEMPLATE_ID_RE.match(tid) and key is None:
                unmapped.append(tid)
            current = tid if key else None
            if current:
                templates[current] = {"target": target, "culture": key, "stacks": []}
            continue
        if TEMPLATE_CLOSE_RE.search(line):
            current = None
            continue
        if current and "<PartyTemplateStack" in line:
            m = STACK_RE.search(line)
            if not m:
                raise ValueError("line %d: unparsable stack in %s: %s"
                                 % (lineno + 1, current, line.strip()))
            templates[current]["stacks"].append(
                (lineno, int(m.group(1)), int(m.group(2))))

    # Pass 2: compute and rewrite.
    new_lines = list(lines)
    rows = []
    for tid, info in templates.items():
        stacks = info["stacks"]
        if not stacks:
            continue
        mins = [s[1] for s in stacks]
        maxes = [s[2] for s in stacks]
        old_sum = sum(maxes)
        new_maxes = retarget(mins, maxes, info["target"])
        changed = 0
        for (lineno, mn, mx), new_mx in zip(stacks, new_maxes):
            if new_mx == mx:
                continue
            new_lines[lineno] = new_lines[lineno].replace(
                'max_value="%d"' % mx, 'max_value="%d"' % new_mx, 1)
            changed += 1
        rows.append((tid, info["culture"], sum(mins), old_sum,
                     sum(new_maxes), info["target"], changed))
    return new_lines, rows, unmapped


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true",
                        help="write changes (default: dry-run)")
    args = parser.parse_args()

    if not TARGET_FILE.exists():
        print("ERROR: target file not found: %s" % TARGET_FILE)
        return 1

    # Binary round-trip: the file carries a UTF-8 BOM and CRLF endings, and both must
    # survive (.claude/rules/moduledata-validation.md, XML I/O convention idiom B).
    raw = TARGET_FILE.read_bytes()
    text = raw.decode("utf-8")
    lines = text.splitlines(keepends=True)

    new_lines, rows, unmapped = process(lines)

    print("%-58s %-18s %6s %7s %7s %7s %5s"
          % ("template", "culture", "min", "old", "new", "target", "chg"))
    for tid, culture, min_sum, old_sum, new_sum, target, changed in rows:
        flag = "" if new_sum == target else "  <-- MISS"
        print("%-58s %-18s %6d %7d %7d %7d %5d%s"
              % (tid, culture, min_sum, old_sum, new_sum, target, changed, flag))

    print()
    print("templates retargeted: %d" % len(rows))
    print("stacks changed:       %d" % sum(r[6] for r in rows))
    misses = [r for r in rows if r[4] != r[5]]
    if misses:
        print("WARNING: %d template(s) did not land exactly on target" % len(misses))
    if unmapped:
        print()
        print("WARNING: %d kingdom_hero_party template(s) matched no culture key "
              "and were SKIPPED:" % len(unmapped))
        for tid in unmapped:
            print("  %s" % tid)

    print()
    if not args.apply:
        print("DRY-RUN: re-run with --apply to write.")
        return 0

    TARGET_FILE.write_bytes("".join(new_lines).encode("utf-8"))
    print("APPLIED: %s" % TARGET_FILE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
