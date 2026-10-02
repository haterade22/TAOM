#!/usr/bin/env python3
"""Generate English master localization strings files for troop/lord/clan/kingdom names.

WHY THIS EXISTS
---------------
Troop, lord, clan, and kingdom identity text already carries {=KEY}default syntax inline in its
own source XML (troops/troops_*.xml, characters/lords.xml, characters/clans.xml,
taom_spkingdoms.xml), but the translator's discoverable source-file list
(tools/translate_with_claude.py: english_source_files) never reads those files, so most of those
keys have no registered English row anywhere in the pipeline and every language shows the raw
English default forever, with no error anywhere. Found 2026-09-13 for troops alone (#572); this
generator closes the same gap for troops, lords, clans, and kingdoms
(docs/features/localization.md "Case B").

WHAT IT DOES
------------
For each category, extracts every {=KEY}default occurrence from its source XML(s), excludes any
key that is ALREADY registered in one of the 13 existing TAOM strings sources (idempotent: a lord
key already translated via taom_xslt_strings.xml, or a clan key already in taom_module_strings.xml,
keeps that single registration rather than gaining a duplicate row in a second file), and writes
the remainder into one new generated strings file per category at ModuleData root, in the SAME
bare <strings> root format taom_module_strings.xml and its siblings use (NOT the <base type="string">
wrapper the per-language translation files use).

Run with --apply to write; --dry-run (default) to preview counts only. Re-running after new troops/
lords/clans/kingdoms are authored picks up their keys and leaves existing rows untouched, because
the exclusion set is recomputed from the shipped English source files, not from a snapshot.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"

# Mirrors tools/translate_with_claude.py english_source_files("TAOM") — the files the translator
# already discovers keys from. Kept as a literal list rather than importing that module, because
# importing it pulls in the UTF-8 stdout reconfiguration side effect (see that file's header).
EXISTING_TAOM_SOURCES = [
    MODULE_DATA / "taom_module_strings.xml",
    MODULE_DATA / "global_strings.xml",
    MODULE_DATA / "taom_wanderer_strings.xml",
    MODULE_DATA / "named_companions" / "named_companion_strings.xml",
    MODULE_DATA / "taom_cc_strings.xml",
    MODULE_DATA / "taom_career_strings.xml",
    MODULE_DATA / "taom_messenger_strings.xml",
    MODULE_DATA / "taom_xslt_strings.xml",
    MODULE_DATA / "taom_wotr_strings.xml",
    MODULE_DATA / "taom_lotr_issue_strings.xml",
    MODULE_DATA / "taom_emissary_strings.xml",
    MODULE_DATA / "taom_enlistment_strings.xml",
    MODULE_DATA / "taom_player_switcher_strings.xml",
]

# Any attribute of the shape attr="{=KEY}default text" — the same shape
# translate_with_claude.py's _diff_against_settlement_source uses for TAOM_Map's settlements.xml.
ATTR_KEY_PATTERN = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')
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


def excluded_ids(cat: str, existing_sources: list[Path] = EXISTING_TAOM_SOURCES) -> set[str]:
    """Keys category `cat` must not register: those the hand-kept sources carry, and those any
    OTHER generated file already carries (a language loading two rows for one id keeps the later)."""
    others = [spec["output"] for name, spec in CATEGORIES.items() if name != cat]
    return registered_ids(list(existing_sources) + others)


def registered_ids(existing_sources: list[Path] = EXISTING_TAOM_SOURCES) -> set[str]:
    """Every {=KEY} already registered across the pipeline's existing English source files."""
    ids: set[str] = set()
    for path in existing_sources:
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
        if key not in seen:
            seen[key] = default
    return list(seen.items())


def build_category_entries(cat: str, already_registered: set[str]) -> list[tuple[str, str]]:
    """New (unregistered) entries for one category, sorted by key for a stable diff."""
    spec = CATEGORIES[cat]
    seen: dict[str, str] = {}
    for src in spec["sources"]():
        if not src.exists():
            continue
        for key, default in extract_from_file(src):
            if key in already_registered or key in seen:
                continue
            seen[key] = default
    return sorted(seen.items())


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


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    ap.add_argument("--category", choices=sorted(CATEGORIES), default=None,
                     help="Limit to one category. Omit to process all of them.")
    args = ap.parse_args()

    print(f"  Already-registered keys across {len(EXISTING_TAOM_SOURCES)} existing sources: "
          f"{len(registered_ids())}")

    cats = [args.category] if args.category else sorted(CATEGORIES)
    grand_total = 0
    for cat in cats:
        spec = CATEGORIES[cat]
        entries = build_category_entries(cat, excluded_ids(cat))
        grand_total += len(entries)
        out = spec["output"]
        print(f"\n  [{cat}] -> {out.relative_to(REPO_ROOT)}: {len(entries)} new key(s)")
        if entries[:3]:
            for key, default in entries[:3]:
                preview = default if len(default) <= 50 else default[:47] + "..."
                print(f"      {key} = {preview}")
            if len(entries) > 3:
                print(f"      ... and {len(entries) - 3} more")

        if args.apply:
            content = build_xml(entries, spec["header"])
            out.write_text(content, encoding="utf-8")

    print(f"\n  Total new keys: {grand_total}")
    if args.dry_run:
        print("  (dry run - no files written)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
