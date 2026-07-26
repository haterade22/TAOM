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

Idempotent -- rerunning skips copies that already exist. Invariants pinned by
TAOM.Tests/Features/TroopProgression/TavernMercenaryDataTests.cs.
"""

from __future__ import annotations

import re
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
    "mistymountainorcs": [
        ("mistymountainorcs_fighter", "mistymountainorcs", "[Misty Mountains] Hired Orc Raider"),
    ],
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


def append_copies(stem: str, entries: list[tuple[str, str]]) -> int:
    """entries: [(source id, hired name)] -> append merc copies to troops_<stem>.xml."""
    path = TROOPS_DIR / f"troops_{stem}.xml"
    xml = read(path)
    new_blocks = []

    for source_id, hired_name in entries:
        if f'id="{source_id}{MERC_SUFFIX}"' in xml:
            continue
        new_blocks.append(make_mercenary_copy(find_troop_block(xml, source_id), source_id, hired_name))

    if not new_blocks:
        return 0

    closing = xml.rindex("</NPCCharacters>")
    addition = BANNER + "\n" + "\n\n".join(new_blocks) + "\n\n"
    write(path, xml[:closing] + normalize_newlines(addition, xml) + xml[closing:])
    return len(new_blocks)


def repoint_cultures() -> int:
    xml = read(SPCULTURES)
    culture_pattern = re.compile(r'<Culture\s+id="(?P<id>[^"]+)"')
    changed = 0

    for match in list(culture_pattern.finditer(xml)):
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

    write(SPCULTURES, xml)
    return changed


def main() -> None:
    by_file: dict[str, list[tuple[str, str]]] = {}
    for picks in PICKS.values():
        for source_id, stem, hired_name in picks:
            entries = by_file.setdefault(stem, [])
            if (source_id, hired_name) not in entries:
                entries.append((source_id, hired_name))

    total = 0
    for stem, entries in sorted(by_file.items()):
        added = append_copies(stem, entries)
        total += added
        print(f"troops_{stem}.xml: +{added} mercenary copies")

    print(f"{total} mercenary troops added")
    print(f"{repoint_cultures()} culture <basic_mercenary_troops> blocks repointed")


if __name__ == "__main__":
    main()
