#!/usr/bin/env python3
"""
Rebuild language XML files from translation cache + overrides + English source.

The translate_with_claude.py script does in-place text substitution which fails when
the target file is an empty stub (no <string> elements to substitute). This script
builds the target files from scratch by walking the English source structure and
injecting translations from override/cache.

Usage: python tools/rebuild_translation_files.py --lang RU
       python tools/rebuild_translation_files.py --all
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
GAME_ROOT = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")

LANGUAGES = {
    "BR":  ("por-BR", "Portuguese (Brazilian)"),
    "CNs": ("zho-CN", "Simplified Chinese"),
    "CNt": ("zho-HK", "Traditional Chinese"),
    "DE":  ("deu-DE", "German"),
    "FR":  ("fre-FR", "French"),
    "IT":  ("ita-IT", "Italian"),
    "JP":  ("jpn-JP", "Japanese"),
    "KO":  ("kor-KO", "Korean"),
    "PL":  ("pol-PL", "Polish"),
    "RU":  ("rus-RU", "Russian"),
    "SP":  ("spa-LA", "Spanish (LA)"),
    "TR":  ("tur-TR", "Turkish"),
}


def escape(text):
    return (text.replace("&", "&amp;").replace('"', "&quot;")
                .replace("<", "&lt;").replace(">", "&gt;"))


def parse_source_with_keys(path):
    """Parse a source XML with text='{=KEY}default' entries. Returns dict id->english."""
    tree = ET.parse(path)
    result = {}
    for el in tree.iter("string"):
        text = el.get("text", "")
        m = re.match(r"^\{=([^}]+)\}(.*)$", text, re.DOTALL)
        if m:
            result[m.group(1)] = m.group(2)
        else:
            sid = el.get("id", "")
            if sid:
                result[sid] = text
    return result


def parse_settlements_xml(path):
    """Extract inline {=KEY}default from settlements.xml. Returns dict id->english."""
    with open(path, encoding="utf-8") as f:
        content = f.read()
    pattern = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')
    result = {}
    for m in pattern.finditer(content):
        k, v = m.groups()
        if k not in result:
            result[k] = v
    return result


def parse_armory_english(path):
    """Parse an Armory English source XML — direct id+text mapping (no {=KEY} prefix)."""
    tree = ET.parse(path)
    result = {}
    for el in tree.iter("string"):
        sid = el.get("id", "")
        text = el.get("text", "")
        if sid:
            result[sid] = text
    return result


def build_translation_xml(language_tag, ordered_entries):
    """Build XML content. ordered_entries: list of (id, text)."""
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
    for sid, text in ordered_entries:
        lines.append(f'    <string id="{escape(sid)}" text="{escape(text)}" />')
    lines.append('  </strings>')
    lines.append('</base>')
    lines.append('')
    return "\r\n".join(lines)


def rebuild_language(lang):
    locale, lang_name = LANGUAGES[lang]
    print(f"\n[{lang}] {lang_name}")

    # Load overrides + cache
    overrides_path = REPO_ROOT / "tools" / "translation_overrides" / f"{lang.lower()}.json"
    cache_path = REPO_ROOT / "tools" / "translation_cache" / f"{lang.lower()}.json"
    overrides = json.loads(overrides_path.read_text(encoding="utf-8")) if overrides_path.exists() else {}
    cache = json.loads(cache_path.read_text(encoding="utf-8")) if cache_path.exists() else {}
    # Remove non-id metadata keys (e.g., "_comment")
    overrides = {k: v for k, v in overrides.items() if not k.startswith("_")}

    print(f"  Overrides: {len(overrides)}, Cache: {len(cache)}")

    def resolve(sid, english):
        if sid in overrides:
            return overrides[sid], "override"
        if sid in cache:
            return cache[sid], "cache"
        return english, "english_fallback"

    # ─── TAOM module: 6 source XMLs ─────────────────────────────────────────────
    taom_sources = [
        ("taom_module_strings.xml",                       f"std_taom_module_strings_{locale}.xml"),
        ("taom_wanderer_strings.xml",                     f"std_taom_wanderer_strings_{locale}.xml"),
        ("named_companions/named_companion_strings.xml",  f"std_taom_named_companion_strings_{locale}.xml"),
        ("taom_cc_strings.xml",                           f"std_taom_cc_strings_{locale}.xml"),
        ("taom_career_strings.xml",                       f"std_taom_career_strings_{locale}.xml"),
        ("taom_messenger_strings.xml",                    f"std_taom_messenger_strings_{locale}.xml"),
    ]
    taom_target_dir = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages" / lang
    for src_rel, tgt_name in taom_sources:
        src_path = REPO_ROOT / "Main" / "_Module" / "ModuleData" / src_rel
        if not src_path.exists():
            print(f"  [skip] {src_rel} not found")
            continue
        src_map = parse_source_with_keys(src_path)
        tgt_path = taom_target_dir / tgt_name
        ordered = []
        stats = {"override": 0, "cache": 0, "english_fallback": 0}
        for sid, english in src_map.items():
            text, source = resolve(sid, english)
            ordered.append((sid, text))
            stats[source] += 1
        content = build_translation_xml(lang_name, ordered)
        tgt_path.write_text(content, encoding="utf-8")
        print(f"  TAOM {tgt_name}: {len(ordered)} entries (override={stats['override']} cache={stats['cache']} en_fallback={stats['english_fallback']})")

    # ─── TAOM_Map: settlements.xml ──────────────────────────────────────────────
    settlements_xml = GAME_ROOT / "TAOM_Map" / "ModuleData" / "settlements.xml"
    map_target = GAME_ROOT / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    if settlements_xml.exists():
        src_map = parse_settlements_xml(settlements_xml)
        ordered = []
        stats = {"override": 0, "cache": 0, "english_fallback": 0}
        for sid, english in src_map.items():
            text, source = resolve(sid, english)
            ordered.append((sid, text))
            stats[source] += 1
        content = build_translation_xml(lang_name, ordered)
        map_target.parent.mkdir(parents=True, exist_ok=True)
        map_target.write_text(content, encoding="utf-8")
        print(f"  TAOM_Map loc_settlements.xml: {len(ordered)} entries (override={stats['override']} cache={stats['cache']} en_fallback={stats['english_fallback']})")

    # ─── LOTRLOME_Armory: 19 source XMLs at root of Languages/ ─────────────────
    armory_root = GAME_ROOT / "LOTRLOME_Armory" / "ModuleData" / "Languages"
    armory_target = armory_root / lang
    armory_target.mkdir(parents=True, exist_ok=True)
    for src_path in sorted(armory_root.glob("loc_*.xml")):
        src_map = parse_armory_english(src_path)
        ordered = []
        stats = {"override": 0, "cache": 0, "english_fallback": 0}
        for sid, english in src_map.items():
            text, source = resolve(sid, english)
            ordered.append((sid, text))
            stats[source] += 1
        content = build_translation_xml(lang_name, ordered)
        tgt = armory_target / src_path.name
        tgt.write_text(content, encoding="utf-8")
        print(f"  Armory {src_path.name}: {len(ordered)} entries (override={stats['override']} cache={stats['cache']} en_fallback={stats['english_fallback']})")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--lang", choices=list(LANGUAGES.keys()))
    p.add_argument("--all", action="store_true")
    args = p.parse_args()

    if args.all:
        targets = list(LANGUAGES.keys())
    elif args.lang:
        targets = [args.lang]
    else:
        p.error("Specify --lang LANG or --all")

    for lang in targets:
        rebuild_language(lang)

    print("\nDone.")


if __name__ == "__main__":
    main()
