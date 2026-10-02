#!/usr/bin/env python3
"""Generate the English master strings files for text the TAOM data XML carries inline.

WHY THIS EXISTS
---------------
Troop, lord, clan and kingdom names, culture text and name lists, hero biographies, career data, NPC
names and Custom Battle scene names carry {=KEY}default inline in their own data XML. The translator
reads only registered English sources (tools/_loc_sources.py), so an unregistered key shows its English
default in every language, with no error anywhere. #572 (2026-09-17) registered the four name families;
the 2026-10-01 run added the five data-text families (docs/features/localization.md "Case B").

WHAT IT DOES
------------
One pass over CATEGORIES in name order. Each category takes every {=KEY}default attribute from its
sources, skips a key a hand-kept English source already registers or an earlier category already
took, and writes the rest, sorted, into its own file at ModuleData root (bare <strings> root, CRLF,
no BOM). The output is a function of the sources alone.

    --dry-run   print each category's size
    --apply     write every category file (or one, with --category)
    --check     exit 1 naming each category file that differs from what the sources produce;
                tools/tests runs the same comparison, so an unregenerated source fails CI

A missing source file or an empty source glob stops the run with exit 2 and writes nothing: it would
otherwise write an empty file over a registered one. The repo settlements.xml (a stale copy of
TAOM_Map's) and custom_settlements.xml (registered nowhere in SubModule.xml) are never sources.
"""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from _loc_sources import TAOM_SOURCES

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"

# Any attribute of the shape attr="{=KEY}default text", the same shape
# translate_with_claude.py's _diff_against_settlement_source uses for TAOM_Map's settlements.xml.
# The engine sentinels {=!} and {=*} are not translatable rows and are skipped.
ATTR_KEY_PATTERN = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')
SENTINEL_KEYS = {"!", "*"}
ID_PATTERN = re.compile(r'<string\s+id="([^"]+)"')

CATEGORIES: dict[str, dict] = {
    "troop": {
        "sources": lambda: sorted((MODULE_DATA / "troops").glob("troops_*.xml")),
        "output": MODULE_DATA / "taom_troop_name_strings.xml",
        "header": "Generated troop-name strings: extracted from troops/troops_*.xml name= "
                  "attributes by tools/generate_name_localization_strings.py. Do not hand-edit; "
                  "re-run the generator after adding troops.",
    },
    "lord": {
        "sources": lambda: [MODULE_DATA / "characters" / "lords.xml"],
        "output": MODULE_DATA / "taom_lord_name_strings.xml",
        "header": "Generated lord-name strings: extracted from characters/lords.xml name= "
                  "attributes by tools/generate_name_localization_strings.py. Keys already "
                  "registered via taom_xslt_strings.xml are excluded on purpose - see the "
                  "generator's registered_ids(). Do not hand-edit; re-run the generator instead.",
    },
    "clan": {
        "sources": lambda: [MODULE_DATA / "characters" / "clans.xml"],
        "output": MODULE_DATA / "taom_clan_name_strings.xml",
        "header": "Generated clan-name strings: extracted from characters/clans.xml name= "
                  "attributes by tools/generate_name_localization_strings.py. Keys already "
                  "registered via taom_module_strings.xml (abanissa/shaghana/bandit clusters) are "
                  "excluded on purpose. Do not hand-edit; re-run the generator instead.",
    },
    "kingdom": {
        "sources": lambda: [MODULE_DATA / "taom_spkingdoms.xml"],
        "output": MODULE_DATA / "taom_kingdom_name_strings.xml",
        "header": "Generated kingdom-identity strings (name/short_name/title/ruler_title/text): "
                  "extracted from taom_spkingdoms.xml by "
                  "tools/generate_name_localization_strings.py. Do not hand-edit; re-run the "
                  "generator after adding a kingdom.",
    },
    # The data-text families (2026-10-01). Each consumer builds a TextObject from the raw attribute,
    # so a registered row is what the other twelve languages display: CultureObject.Deserialize
    # (culture names, descriptions, name lists), Hero.Deserialize (EncyclopediaText),
    # BasicCharacterObject.Deserialize (NPC names), the career VMs, CustomGame (battle scenes).
    # The repo settlements.xml (a stale copy of TAOM_Map's) and custom_settlements.xml (registered
    # nowhere in SubModule.xml) never reach the engine and are never sources.
    "culture_text": {
        "sources": lambda: [MODULE_DATA / "taom_spcultures.xml"],
        "output": MODULE_DATA / "taom_culture_text_strings.xml",
        "header": "Generated culture text strings (names, descriptions, male and female name "
                  "lists): extracted from taom_spcultures.xml by "
                  "tools/generate_name_localization_strings.py. Do not hand-edit; re-run the "
                  "generator instead.",
    },
    "hero_text": {
        "sources": lambda: [MODULE_DATA / "characters" / "heroes.xml"],
        "output": MODULE_DATA / "taom_hero_text_strings.xml",
        "header": "Generated hero biography strings: extracted from characters/heroes.xml text= "
                  "attributes by tools/generate_name_localization_strings.py. Do not hand-edit; "
                  "re-run the generator instead.",
    },
    "career_data": {
        "sources": lambda: sorted((MODULE_DATA / "career_system").glob("*.xml")),
        "output": MODULE_DATA / "taom_career_data_strings.xml",
        "header": "Generated career data strings (careers, choices, ability templates, quests): "
                  "extracted from career_system/*.xml by "
                  "tools/generate_name_localization_strings.py. Do not hand-edit; re-run the "
                  "generator instead.",
    },
    "character_name": {
        "sources": lambda: sorted(
            p for p in (MODULE_DATA / "characters").glob("*.xml")
            if p.name not in ("lords.xml", "clans.xml", "heroes.xml")
        ) + [MODULE_DATA / "taom_wanderers.xml",
             MODULE_DATA / "named_companions" / "named_companions.xml"],
        "output": MODULE_DATA / "taom_character_name_strings.xml",
        "header": "Generated character name strings (notables, townsfolk, wanderers, named "
                  "companions, creatures): extracted from characters/*.xml (lords, clans and "
                  "heroes have their own files), taom_wanderers.xml and "
                  "named_companions/named_companions.xml by "
                  "tools/generate_name_localization_strings.py. Do not hand-edit; re-run the "
                  "generator instead.",
    },
    "battle_scene": {
        "sources": lambda: [MODULE_DATA / "custom_battle_scenes.xml"],
        "output": MODULE_DATA / "taom_battle_scene_strings.xml",
        "header": "Generated Custom Battle scene names: extracted from custom_battle_scenes.xml by "
                  "tools/generate_name_localization_strings.py. Do not hand-edit; re-run the "
                  "generator instead.",
    },
}


class SourceMissing(Exception):
    """A category's source file is gone or its glob matched nothing."""


def hand_kept_sources() -> list[Path]:
    """The English sources this tool does not write: the table minus every CATEGORIES output."""
    outputs = {spec["output"].resolve() for spec in CATEGORIES.values()}
    rows = [MODULE_DATA / src for src, _ in TAOM_SOURCES]
    return [p for p in rows if p.resolve() not in outputs]


def registered_ids(existing_sources: list[Path] | None = None) -> set[str]:
    """Every id the hand-kept English sources register (the keys no category may take)."""
    ids: set[str] = set()
    for path in hand_kept_sources() if existing_sources is None else existing_sources:
        if not path.exists():
            continue
        text = path.read_text(encoding="utf-8")
        ids.update(ID_PATTERN.findall(text))
    return ids


def extract_from_file(path: Path) -> list[tuple[str, str]]:
    """(key, default_text) pairs from one source XML, first occurrence wins per key."""
    text = path.read_text(encoding="utf-8")
    seen: dict[str, str] = {}
    for key, default in ATTR_KEY_PATTERN.findall(text):
        if key not in seen and key not in SENTINEL_KEYS:
            seen[key] = default
    return list(seen.items())


def category_sources(cat: str) -> list[Path]:
    """The category's sources; raises SourceMissing when one is gone or the glob is empty."""
    sources = list(CATEGORIES[cat]["sources"]())
    missing = [p for p in sources if not p.exists()]
    if not sources or missing:
        raise SourceMissing(f"[{cat}] source missing or glob empty: {missing or 'no files'}")
    return sources


def build_category_entries(cat: str, already_registered: set[str]) -> list[tuple[str, str]]:
    """Unregistered entries for one category, sorted by key for a stable diff."""
    seen: dict[str, str] = {}
    for src in category_sources(cat):
        for key, default in extract_from_file(src):
            if key in already_registered or key in seen:
                continue
            seen[key] = default
    return sorted(seen.items())


def build_all(already_registered: set[str]) -> dict[str, list[tuple[str, str]]]:
    """Every category's entries in one pass over CATEGORIES in name order: a key two categories'
    sources declare goes to the first only (a language loading two rows for one id keeps the later)."""
    owned, built = set(already_registered), {}
    for cat in sorted(CATEGORIES):
        built[cat] = build_category_entries(cat, owned)
        owned.update(key for key, _ in built[cat])
    return built


def stale_categories(built: dict[str, list[tuple[str, str]]]) -> list[str]:
    """Categories whose file on disk is not byte for byte what the sources produce."""
    stale = []
    for cat, entries in sorted(built.items()):
        out = CATEGORIES[cat]["output"]
        expected = build_xml(entries, CATEGORIES[cat]["header"]).encode("utf-8")
        if not out.exists() or out.read_bytes() != expected:
            stale.append(cat)
    return stale


def write_output(cat: str, entries: list[tuple[str, str]]) -> None:
    """Write a category file as bytes (no newline translation), after proving it parses."""
    content = build_xml(entries, CATEGORIES[cat]["header"])
    ET.fromstring(content.encode("utf-8"))
    CATEGORIES[cat]["output"].write_bytes(content.encode("utf-8"))


def build_xml(entries: list[tuple[str, str]], header: str) -> str:
    """Bare <strings> root, matching taom_module_strings.xml / taom_enlistment_strings.xml -
    NOT the <base type="string"><tags>...<strings> wrapper the per-language files use."""
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<strings>',
        '',
        f'\t<!-- {header} -->',
    ]
    for key, default in entries:
        lines.append(f'\t<string id="{key}" text="{{={key}}}{default}" />')
    lines.append('')
    lines.append('</strings>')
    lines.append('')
    return "\r\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    g.add_argument("--check", action="store_true")
    ap.add_argument("--category", choices=sorted(CATEGORIES), default=None,
                    help="Limit --apply or --check to one category (ownership is still computed over all).")
    args = ap.parse_args(argv)

    try:
        built = build_all(registered_ids())
    except SourceMissing as exc:
        print(f"ERROR: {exc}; nothing written.", file=sys.stderr)
        return 2
    cats = [args.category] if args.category else sorted(built)

    if args.check:
        stale = [c for c in stale_categories({c: built[c] for c in cats})]
        for cat in stale:
            print(f"STALE: {CATEGORIES[cat]['output'].name} differs from its sources ([{cat}])")
        if stale:
            print("Run: python tools/generate_name_localization_strings.py --apply")
            return 1
        print(f"OK: {len(cats)} generated file(s) match their sources")
        return 0

    stale = set(stale_categories({c: built[c] for c in cats}))
    for cat in cats:
        out = CATEGORIES[cat]["output"]
        state = "changes" if cat in stale else "unchanged"
        print(f"  [{cat}] -> {out.name}: {len(built[cat])} key(s), {state}")
        if args.apply and cat in stale:
            write_output(cat, built[cat])
    if args.dry_run:
        print("  (dry run - no files written)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
