#!/usr/bin/env python3
"""Wire the Black Numenorean tree into Mordor's party templates, troop weights,
and special-resource costs.

The tree is a STANDALONE elite line: taom_spcultures.xml is deliberately not
touched (elite_basic_troop stays mordor_uruk_warrior) and no volunteer pool
gains an entry, so VolunteerRecruitmentServiceTests keeps its current boundary
rows. These troops reach the field through lord parties instead.

Three files, all in the TAOM repo:

  taom_partyTemplates.xml
      Stacks in the culture default + all 15 per-clan lord templates (the ones
      named lords actually use, via Clan.DefaultPartyTemplate), plus one exact
      entry each in the level-3 patrol and the vassal-reward template.

      max_value is a CEILING, not a count: the engine draws one uniform ratio
      per party and fills every stack to min + (max - min) * r. Adding stacks
      therefore raises the template's max sum, so
      `tools/rebalance_party_template_maxes.py --apply` MUST run afterwards to
      pull Mordor back to its 3500 target. That tool only touches
      kingdom_hero_party_* templates, which is why the patrol and vassal-reward
      entries below use exact min == max values it will not disturb.

  TroopWeights/troop_weights.xml
      2.0 for the whole line, matching mordor_uruk_captain.
      troop-weight-system.md already lists "Black Numenoreans" in that band.

  special_resources/troop_resource_costs.xml
      war_spoils costs, scaled off the existing Mordor elite entries.

Usage:
    python tools/wire_black_numenorean_troops.py --dry-run
    python tools/wire_black_numenorean_troops.py --apply
"""
import argparse
import os
import re
import sys
from datetime import date

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(REPO, "Main", "_Module", "ModuleData")
PARTY_XML = os.path.join(MD, "taom_partyTemplates.xml")
WEIGHTS_XML = os.path.join(MD, "TroopWeights", "troop_weights.xml")
COSTS_XML = os.path.join(MD, "special_resources", "troop_resource_costs.xml")

MARKER = "mordor_num_"

# Pre-normalisation ceilings. Only the RATIO between these matters: the
# rebalance tool rescales every spread so the template sum hits 3500.
LORD_STACKS = [
    ("mordor_num_initiate", 24),
    ("mordor_num_cavalry", 18),
    ("mordor_num_infantry", 18),
    ("mordor_num_archer", 18),
    ("mordor_num_vet_cavalry", 12),
    ("mordor_num_vet_infantry", 12),
    ("mordor_num_vet_archer", 12),
    ("mordor_num_knight", 8),
    ("mordor_num_warden", 8),
    ("mordor_num_marksman", 8),
    ("mordor_num_temple_knight", 5),
    ("mordor_num_temple_guard", 5),
    ("mordor_num_shadowbow", 5),
]

LORD_TEMPLATES = ["kingdom_hero_party_mordor_template"] + [
    f"kingdom_hero_party_mordor_empire_south_{n}_template" for n in range(1, 16)
]

# Exact-count templates, outside the rebalance tool's scope.
EXACT = {
    "patrol_party_mordor_template_level_3": [("mordor_num_infantry", 1)],
    "vassal_reward_troops_mordor": [("mordor_num_vet_infantry", 1)],
}

WEIGHTS = {
    # All 2.0: troop-weight-system.md documents the tier set as 1.0/2.0/3.0/4.0
    # and already names "Black Numenoreans" in the 2.0 band. A first draft used
    # 1.5 for T5/T6, which would have been the only non-integer weights in the
    # whole 100-entry file.
    "mordor_num_initiate": "2.0",
    "mordor_num_cavalry": "2.0",
    "mordor_num_infantry": "2.0",
    "mordor_num_archer": "2.0",
    "mordor_num_vet_cavalry": "2.0",
    "mordor_num_vet_infantry": "2.0",
    "mordor_num_vet_archer": "2.0",
    "mordor_num_knight": "2.0",
    "mordor_num_warden": "2.0",
    "mordor_num_marksman": "2.0",
    "mordor_num_temple_knight": "2.0",
    "mordor_num_temple_guard": "2.0",
    "mordor_num_shadowbow": "2.0",
}


def read(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    stamp = date.today().strftime("%Y%m%d")
    bak = f"{path}.bak-blacknum-{stamp}"
    if not os.path.exists(bak):
        with open(bak, "w", encoding="utf-8", newline="") as f:
            f.write(read(path))
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def eol_of(text):
    """Dominant line ending, by majority rather than presence.

    A single stray CRLF in an otherwise-LF file would flip a presence test, and
    mixed-ending files demonstrably exist in this codebase.
    """
    crlf = text.count("\r\n")
    return "\r\n" if crlf > text.count("\n") - crlf else "\n"


def add_stacks(text, template_id, stacks, exact=False):
    """Insert PartyTemplateStack lines before a template's </stacks>."""
    m = re.search(r'<MBPartyTemplate\s+id="%s"\s*>(.*?)</MBPartyTemplate>'
                  % re.escape(template_id), text, re.S)
    if not m:
        return text, "TEMPLATE NOT FOUND"
    body = m.group(1)
    todo = [(t, v) for t, v in stacks if f'troop="NPCCharacter.{t}"' not in body]
    if not todo:
        return text, "already wired"
    eol = eol_of(text)
    indent = "\t\t\t"
    lines = "".join(
        f'{indent}<PartyTemplateStack min_value="{v if exact else 0}" '
        f'max_value="{v}" troop="NPCCharacter.{t}" />{eol}'
        for t, v in todo)
    close = body.rfind("</stacks>")
    line_start = body.rfind(eol, 0, close) + len(eol)
    new_body = body[:line_start] + lines + body[line_start:]
    return text[:m.start(1)] + new_body + text[m.end(1):], f"+{len(todo)}"


def do_party(dry):
    text = read(PARTY_XML)
    notes = []
    for tid in LORD_TEMPLATES:
        text, note = add_stacks(text, tid, LORD_STACKS)
        notes.append((tid, note))
    for tid, stacks in EXACT.items():
        text, note = add_stacks(text, tid, stacks, exact=True)
        notes.append((tid, note))
    ok = sum(1 for _, n in notes if n.startswith("+"))
    bad = [t for t, n in notes if n == "TEMPLATE NOT FOUND"]
    print(f"  taom_partyTemplates.xml: {ok}/{len(notes)} templates wired"
          + (f"  NOT FOUND: {bad}" if bad else ""))
    if bad:
        sys.exit(1)
    if not dry and ok:
        write(PARTY_XML, text)
    return ok


def do_weights(dry):
    text = read(WEIGHTS_XML)
    todo = {k: v for k, v in WEIGHTS.items() if f'id="{k}"' not in text}
    if not todo:
        print("  troop_weights.xml: already wired")
        return 0
    eol = eol_of(text)
    close = text.rfind("</TroopWeights>")
    if close < 0:
        print("  ERROR: </TroopWeights> not found", file=sys.stderr)
        sys.exit(1)
    line_start = text.rfind(eol, 0, close) + len(eol)
    indent = re.match(r"[ \t]*", text[line_start:]).group(0) or "    "
    block = (f'{indent}<!-- Black Numenorean line: the 2.0 elite band -->{eol}'
             + "".join(f'{indent}<TroopWeight id="{k}" weight="{v}" />{eol}'
                       for k, v in todo.items()))
    print(f"  troop_weights.xml: +{len(todo)}")
    if not dry:
        write(WEIGHTS_XML, text[:line_start] + block + text[line_start:])
    return len(todo)


def do_costs(dry):
    text = read(COSTS_XML)
    todo = [k for k in WEIGHTS if f'id="{k}"' not in text]
    if not todo:
        print("  troop_resource_costs.xml: already wired")
        return 0
    # Scaled off the shipped Mordor elites, NOT invented: mordor_uruk_captain
    # (level 36) is upgrade 4 / upkeep 0.2 and mordor_uruk_baraddurguard (36) is
    # 5 / 0.3. The Black Numenorean T8/T9 sit at level 41 and 46, above both, so
    # they take the top of the ladder.
    #
    # merchant_cost is deliberately omitted on every row. It only has meaning for
    # troops listed in elite_emissary_config.xml <CultureOffers>, and this is a
    # lord-party-only line that is not offered there. Writing a price for an
    # unreachable offer would be dead data, and the file's own header band
    # (L36 about 10-14, L41 about 18, L46 about 28) would make the obvious
    # guesses wrong anyway. If these are ever added to <CultureOffers>, add
    # merchant_cost then, using that band.
    #   level -> (upgrade_cost, daily_upkeep)
    tiers = {26: (1, "0.05"), 31: (2, "0.1"), 36: (3, "0.15"),
             41: (4, "0.2"), 46: (5, "0.3")}
    levels = {"mordor_num_initiate": 26, "mordor_num_cavalry": 31,
              "mordor_num_infantry": 31, "mordor_num_archer": 31,
              "mordor_num_vet_cavalry": 36, "mordor_num_vet_infantry": 36,
              "mordor_num_vet_archer": 36, "mordor_num_knight": 41,
              "mordor_num_warden": 41, "mordor_num_marksman": 41,
              "mordor_num_temple_knight": 46, "mordor_num_temple_guard": 46,
              "mordor_num_shadowbow": 46}
    eol = eol_of(text)
    close = text.rfind("</TroopResourceCosts>")
    if close < 0:
        m = re.search(r"</(\w+)>\s*$", text.strip())
        print(f"  ERROR: closing tag not found (root looks like {m.group(1) if m else '?'})",
              file=sys.stderr)
        sys.exit(1)
    line_start = text.rfind(eol, 0, close) + len(eol)
    # Derive the indent from the last CONTENT line, not from the closing tag:
    # a root close tag sits at column 0, so reading its indent always yields ""
    # and silently falls back to a width the file may not use.
    existing = re.findall(r"\n([ \t]+)<Troop\b", text)
    indent = existing[-1] if existing else "  "
    rows = []
    rows.append(f'{indent}<!-- Black Numenorean line -->{eol}')
    for k in todo:
        up, keep = tiers[levels[k]]
        rows.append(f'{indent}<Troop id="{k}" resource_id="war_spoils" '
                    f'upgrade_cost="{up}" daily_upkeep="{keep}" />{eol}')
    print(f"  troop_resource_costs.xml: +{len(todo)}")
    if not dry:
        write(COSTS_XML, text[:line_start] + "".join(rows) + text[line_start:])
    return len(todo)


def main():
    ap = argparse.ArgumentParser(description="Wire the Black Numenorean tree")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    dry = not args.apply
    print("DRY RUN" if dry else "APPLYING")
    do_party(dry)
    do_weights(dry)
    do_costs(dry)
    if not dry:
        print("\nNEXT (required): python tools/rebalance_party_template_maxes.py --apply")
        print("  Adding stacks raised each template's max sum off Mordor's 3500 target.")


if __name__ == "__main__":
    main()
