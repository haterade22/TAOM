"""Generate tavern-mercenary troop copies and repoint every culture's <basic_mercenary_troops>.

Vanilla's RecruitmentCampaignBehavior draws the town's tavern offer from
`town.Culture.BasicMercenaryTroops`, and every TAOM culture shipped vanilla's Calradian list
verbatim -- so Minas Morgul sold "Hired Pike". This script:

1. Copies each culture's RAREST recruitment-pool troops (lowest VolunteerChance weight) into
   dedicated `<source>_merc` entries: occupation="Mercenary", no upgrade_targets, same skills and
   equipment. The originals are untouched, so notable recruitment and AI party wages are unchanged.
2. Rewrites the 14 town-owning cultures' <basic_mercenary_troops> blocks to point at those copies.

Leaf copies matter: the engine randomly walks UpgradeTargets from whatever root it draws, so an
upgrade target would let the offer drift back onto a normal Soldier line troop.

Dry run by default; pass --apply to write. Every new text is parsed before any file is written, and
only files whose bytes changed are written. Idempotent -- rerunning skips copies that already
exist. Invariants pinned by
TAOM.Tests/Features/TroopProgression/TavernMercenaryDataTests.cs.
"""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

MODULE_DATA = Path(__file__).resolve().parents[2] / "Main" / "_Module" / "ModuleData"
TROOPS_DIR = MODULE_DATA / "troops"
SPCULTURES = MODULE_DATA / "taom_spcultures.xml"

MERC_SUFFIX = "_merc"

# culture id -> [(source troop id, troop file stem, hired display name)]
# Sources are the weight-1 entries of each culture's CultureMap pool in
# Main/Features/TroopProgression/RecruitmentPools/ (weight 2 for Lothlorien, 3 for Umbar/Harad --
# those pools have no weight-1 entry). Excluded by rule: creature mounts (taom_spider_creature)
# and level-51 legendaries (rivendell_knight_golden_flower).
PICKS: dict[str, list[tuple[str, str, str]]] = {
    "mordor": [
        ("mordor_uruk_grunt", "mordor", "[Mordor] Hired Black Uruk Grunt"),
        ("mordor_orc_impaler", "mordor", "[Mordor] Hired Orc Impaler"),
        ("mordor_orc_hunter", "mordor", "[Mordor] Hired Orc Hunter"),
        ("mordor_warg_tamer", "mordor", "[Mordor] Hired Nurn Warg Tamer"),
    ],
    "gondor": [
        ("gondor_bel_recruit", "gondor", "[Gondor] Hired Belfalas Recruit"),
        ("gondor_lam_clansman", "gondor", "[Gondor] Hired Lamedon Clansman"),
        ("gondor_loss_lumberman", "gondor", "[Gondor] Hired Lossarnach Lumberman"),
    ],
    "erebor": [("erebor_oathsworn", "erebor", "[Erebor] Hired Oathsworn")],
    "rivendell": [("rivendell_noble", "rivendell", "[Rivendell] Hired Noble")],
    "lothlorien": [("imladris_bowman", "rivendell", "[Rivendell] Hired Imladris Bowman")],
    "mirkwood": [("mirkwood_recruit", "mirkwood", "[Mirkwood] Hired Silvan Levy")],
    "isengard": [
        ("urukhai_warrior", "isengard", "[Isengard] Hired Uruk-Hai Warrior"),
        ("urukhai_scout", "isengard", "[Isengard] Hired Uruk-Hai Scout"),
        ("orthanc_chosen", "isengard", "[Isengard] Hired Orthanc Chosen"),
    ],
    "gundabad": [
        ("gundabad_fighter", "gundabad", "[Gundabad] Hired Pale Uruk Raider"),
        ("gundabad_scout", "gundabad", "[Gundabad] Hired Scout"),
    ],
    "goblin": [("goblin_fighter", "goblin", "[Goblin] Hired Goblin Raider")],
    # troops_mistymountainorcs.xml is gone (f577bd547): the culture fields the goblin tree and
    # taom_spcultures.xml binds its tavern to goblin_fighter_merc, the same copy goblin uses.
    "mistymountainorcs": [("goblin_fighter", "goblin", "[Goblin] Hired Goblin Raider")],
    "dolguldur": [("dg_orc_scout", "dolguldur", "[Dol Guldur] Hired Orc Scout")],
    "umbar": [("umbar_elite", "umbar", "[Umbar] Hired Adûnaim Recruits")],
    "shaghana": [("harad_noble", "harad", "[Harad] Hired Youngblood of the Serpent")],
    "abanissa": [("harad_noble", "harad", "[Harad] Hired Youngblood of the Serpent")],
}

BANNER = (
    "\n    <!-- ===== TAVERN MERCENARIES =====\n"
    "         Dedicated Mercenary-occupation copies of this culture's rarest recruitment-pool\n"
    "         troops, referenced from <basic_mercenary_troops> in taom_spcultures.xml. Leaves by\n"
    "         design: RecruitmentCampaignBehavior walks UpgradeTargets when picking a town's offer.\n"
    "         Do NOT add upgrade_targets here. -->\n"
)


def read(path: Path) -> str:
    # newline="" on both sides: keep the file's existing CRLF/LF endings out of the diff.
    with open(path, "r", encoding="utf-8", newline="") as handle:
        return handle.read()


def write(path: Path, text: str) -> None:
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text)


def normalize_newlines(text: str, like: str) -> str:
    """Re-line-end authored text to match the target file (these XMLs are CRLF)."""
    newline = "\r\n" if "\r\n" in like else "\n"
    return text.replace("\r\n", "\n").replace("\n", newline)


def find_troop_block(xml: str, troop_id: str) -> str:
    """Return the verbatim <NPCCharacter> block (with leading indentation) for troop_id."""
    pattern = re.compile(
        r"([ \t]*)<NPCCharacter\b[^>]*\bid=\"" + re.escape(troop_id) + r"\"[^>]*>.*?</NPCCharacter>",
        re.DOTALL,
    )
    match = pattern.search(xml)
    if not match:
        raise SystemExit(f"troop not found: {troop_id}")
    return match.group(0)


def make_mercenary_copy(block: str, source_id: str, hired_name: str) -> str:
    copy = block.replace(f'id="{source_id}"', f'id="{source_id}{MERC_SUFFIX}"', 1)
    copy = re.sub(
        r'name="[^"]*"',
        f'name="{{=aom_merc_{source_id}_name}}{hired_name}"',
        copy,
        count=1,
    )
    copy = copy.replace('occupation="Soldier"', 'occupation="Mercenary"', 1)
    # Leaf copy: strip the upgrade chain (and the blank line it leaves behind).
    copy = re.sub(r"[ \t]*<upgrade_targets>.*?</upgrade_targets>\r?\n", "", copy, flags=re.DOTALL)
    if 'occupation="Mercenary"' not in copy:
        raise SystemExit(f"{source_id}: occupation attribute missing or not Soldier")
    return copy


def plan_copies(xml: str, entries: list[tuple[str, str]]) -> tuple[str, int]:
    """entries: [(source id, hired name)] -> (troops file text with the missing merc copies, count)."""
    new_blocks = []

    for source_id, hired_name in entries:
        if f'id="{source_id}{MERC_SUFFIX}"' in xml:
            continue
        new_blocks.append(make_mercenary_copy(find_troop_block(xml, source_id), source_id, hired_name))

    if not new_blocks:
        return xml, 0

    closing = xml.rindex("</NPCCharacters>")
    addition = BANNER + "\n" + "\n\n".join(new_blocks) + "\n\n"
    return xml[:closing] + normalize_newlines(addition, xml) + xml[closing:], len(new_blocks)


def plan_repoint(xml: str) -> tuple[str, int]:
    """Returns (spcultures text with every picked culture repointed, blocks changed)."""
    culture_pattern = re.compile(r'<Culture\s+id="(?P<id>[^"]+)"')
    changed = 0

    # Last to first: a replacement changes the text's length, so later match offsets would go stale.
    for match in reversed(list(culture_pattern.finditer(xml))):
        culture_id = match.group("id")
        if culture_id not in PICKS:
            continue

        end = xml.find("</Culture>", match.start())
        block = re.search(
            r"([ \t]*)<basic_mercenary_troops>.*?</basic_mercenary_troops>",
            xml[match.start():end],
            re.DOTALL,
        )
        if not block:
            raise SystemExit(f"{culture_id}: no <basic_mercenary_troops> block")

        indent = block.group(1)
        templates = "\n".join(
            f'{indent}  <template name="NPCCharacter.{source}{MERC_SUFFIX}" />'
            for source, _stem, _name in PICKS[culture_id]
        )
        replacement = normalize_newlines(
            f"{indent}<basic_mercenary_troops>\n{templates}\n{indent}</basic_mercenary_troops>", xml
        )

        start = match.start() + block.start()
        stop = match.start() + block.end()
        if xml[start:stop] == replacement:
            continue

        xml = xml[:start] + replacement + xml[stop:]
        changed += 1

    return xml, changed


def check_parses(path: Path, text: str) -> None:
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as exc:
        raise SystemExit(f"{path.name}: generated text does not parse ({exc}); nothing written")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--apply", action="store_true",
                        help="write the changed files (default is a dry run)")
    args = parser.parse_args(argv)

    by_file: dict[str, list[tuple[str, str]]] = {}
    for picks in PICKS.values():
        for source_id, stem, hired_name in picks:
            entries = by_file.setdefault(stem, [])
            if (source_id, hired_name) not in entries:
                entries.append((source_id, hired_name))

    # Build and parse every new text first: a missing source troop or a malformed result
    # exits here, before any file is touched.
    pending: list[tuple[Path, str, str]] = []
    total = 0
    for stem, entries in sorted(by_file.items()):
        path = TROOPS_DIR / f"troops_{stem}.xml"
        old = read(path)
        new, added = plan_copies(old, entries)
        total += added
        print(f"troops_{stem}.xml: +{added} mercenary copies")
        pending.append((path, old, new))

    old = read(SPCULTURES)
    new, repointed = plan_repoint(old)
    pending.append((SPCULTURES, old, new))
    print(f"{total} mercenary troops to add")
    print(f"{repointed} culture <basic_mercenary_troops> blocks to repoint")

    changed = [(path, new) for path, old, new in pending if new != old]
    for path, new in changed:
        check_parses(path, new)

    if args.apply:
        for path, new in changed:
            write(path, new)
    print(f"{len(changed)} files changed" + ("" if args.apply else " (dry run, pass --apply to write)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
