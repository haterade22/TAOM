#!/usr/bin/env python3
"""
Generate translation template files from TAOM English source strings.

Parses the 3 English XML string files and writes per-language translation
templates with all string IDs pre-populated with the English text, so
translators can see what needs translating and replace each entry.

Usage:
    python tools/generate_translation_template.py --dry-run          # preview counts
    python tools/generate_translation_template.py --apply PL         # write Polish files
    python tools/generate_translation_template.py --apply --all      # write all languages
"""

import argparse
import re
import xml.etree.ElementTree as ET
from pathlib import Path

TAOM_BASE = Path("Main/_Module/ModuleData")
LANG_DIR = TAOM_BASE / "Languages"

# ORDERING TRAP: --apply OVERWRITES each per-language file with a fresh ENGLISH template.
# Any translation already in that file is discarded. The cache
# (tools/translation_cache/<lang>.json) makes recovery free — re-run translate_with_claude.py
# and everything comes back from cache — but the correct order still saves a round trip:
#
#   1. register EVERY key in the source XML first (incl. generated ones, e.g.
#      tools/generate_enlistment_duty_strings.py)
#   2. THEN run this generator
#   3. THEN translate
#
# Hit on 2026-08-08: templates were generated, 12 languages translated, and then 84
# runtime-built duty keys were registered — regenerating to pick them up blanked all 12.

# (source file, target filename template, description)
SOURCES = [
    (TAOM_BASE / "taom_module_strings.xml",
     "std_taom_module_strings_{locale}.xml",
     "module strings"),
    (TAOM_BASE / "taom_wanderer_strings.xml",
     "std_taom_wanderer_strings_{locale}.xml",
     "wanderer strings"),
    (TAOM_BASE / "named_companions" / "named_companion_strings.xml",
     "std_taom_named_companion_strings_{locale}.xml",
     "named companion strings"),
    (TAOM_BASE / "taom_cc_strings.xml",
     "std_taom_cc_strings_{locale}.xml",
     "character creation strings"),
    (TAOM_BASE / "taom_career_strings.xml",
     "std_taom_career_strings_{locale}.xml",
     "career system strings"),
    (TAOM_BASE / "taom_enlistment_strings.xml",
     "std_taom_enlistment_strings_{locale}.xml",
     "enlistment + field-commission strings"),
]

# lang_dir -> (locale_suffix, language_tag)
LANGUAGES = {
    "BR":  ("por-BR", "Portugu\u00eas (BR)"),
    "CNs": ("zho-CN", "\u7b80\u4f53\u4e2d\u6587"),
    "CNt": ("zho-HK", "\u7e41\u9ad4\u4e2d\u6587"),
    "DE":  ("deu-DE", "Deutsch"),
    "FR":  ("fre-FR", "Fran\u00e7ais"),
    "IT":  ("ita-IT", "Italiano"),
    "JP":  ("jpn-JP", "\u65e5\u672c\u8a9e"),
    "KO":  ("kor-KO", "\ud55c\uad6d\uc5b4"),
    "PL":  ("pol-PL", "Polski"),
    "RU":  ("rus-RU", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439"),
    "SP":  ("spa-LA", "Espa\u00f1ol (LA)"),
    "TR":  ("tur-TR", "T\u00fcrk\u00e7e"),
}

KEY_PATTERN = re.compile(r"^\{=([^}]+)\}")


def extract_entries(source_path):
    """Extract (localization_key, english_text) pairs from a source XML file."""
    tree = ET.parse(source_path)
    root = tree.getroot()
    entries = []
    for el in root.iter("string"):
        text_attr = el.get("text", "")
        m = KEY_PATTERN.match(text_attr)
        if m is None:
            continue
        key = m.group(1)
        if key in ("!", "*"):
            continue
        english = KEY_PATTERN.sub("", text_attr)
        entries.append((key, english))
    return entries


def escape_xml_attr(text):
    """Escape text for use inside an XML attribute value (double-quoted)."""
    return (text
            .replace("&", "&amp;")
            .replace('"', "&quot;")
            .replace("<", "&lt;")
            .replace(">", "&gt;"))


def build_xml(entries, language_tag):
    """Build the translation XML file content as a string."""
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"',
        '      xmlns:xsd="http://www.w3.org/2001/XMLSchema"',
        '      type="string">',
        '  <tags>',
        f'    <tag language="{language_tag}" />',
        '  </tags>',
        '  <strings>',
    ]
    for key, text in entries:
        lines.append(f'    <string id="{escape_xml_attr(key)}" text="{escape_xml_attr(text)}" />')
    lines.append('  </strings>')
    lines.append('</base>')
    lines.append('')
    return "\r\n".join(lines)


def process_language(lang_code, dry_run):
    """Generate all 3 template files for a single language."""
    locale, language_tag = LANGUAGES[lang_code]
    lang_path = LANG_DIR / lang_code
    total = 0

    print(f"\n  [{lang_code}] {language_tag}")
    for source_path, filename_template, desc in SOURCES:
        target_filename = filename_template.format(locale=locale)
        target_path = lang_path / target_filename

        entries = extract_entries(source_path)
        total += len(entries)
        print(f"    {source_path.name} -> {target_filename} ({len(entries)} entries)")

        if not dry_run:
            content = build_xml(entries, language_tag)
            target_path.write_text(content, encoding="utf-8")

    return total


def main():
    parser = argparse.ArgumentParser(
        description="Generate TAOM translation template files from English source strings.")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--dry-run", action="store_true",
                       help="Preview what would be generated without writing files")
    group.add_argument("--apply", action="store_true",
                       help="Write the translation template files")
    parser.add_argument("lang", nargs="?", default=None,
                        help="Language code (e.g. PL, DE, FR). Omit for --all.")
    parser.add_argument("--all", action="store_true",
                        help="Generate templates for all languages")
    args = parser.parse_args()

    if not args.all and args.lang is None:
        parser.error("Specify a language code (e.g. PL) or use --all")

    if args.lang and args.lang not in LANGUAGES:
        parser.error(f"Unknown language code '{args.lang}'. Valid: {', '.join(sorted(LANGUAGES))}")

    targets = sorted(LANGUAGES.keys()) if args.all else [args.lang]
    mode = "DRY RUN" if args.dry_run else "WRITING"

    print(f"[{mode}] Generating translation templates...")
    grand_total = 0
    for lang_code in targets:
        grand_total += process_language(lang_code, args.dry_run)

    print(f"\n  Total: {grand_total} entries across {len(targets)} language(s)")
    if args.dry_run:
        print("  (dry run - no files written)")


if __name__ == "__main__":
    main()
