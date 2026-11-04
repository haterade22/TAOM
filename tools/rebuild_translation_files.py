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
import os
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

import name_tags
from _gamedir import DEFAULT_GAME_ROOT, ensure_exists, game_modules
from _loc_sources import TAOM_SOURCES
from translate_with_claude import key_owners, override_ids_to_skip

REPO_ROOT = Path(__file__).resolve().parent.parent
# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME_ROOT = game_modules(DEFAULT_GAME_ROOT)

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


def taom_sources(locale):
    """The TAOM English sources and their language file names, from the table every localization tool shares."""
    return [(src, tpl.format(locale=locale)) for src, tpl in TAOM_SOURCES]


def source_owners():
    """Each key's owning source: the first table row that declares it (key_owners)."""
    return key_owners([REPO_ROOT / "Main" / "_Module" / "ModuleData" / src for src, _ in TAOM_SOURCES])


def owned_elsewhere(owners, key, src_path):
    """True when another source owns the key: a row here would load after the owner's and replace its curated row."""
    return owners.get(key, src_path) != src_path


def escape(text):
    return (text.replace("&", "&amp;").replace('"', "&quot;")
                .replace("<", "&lt;").replace(">", "&gt;"))


def write_text_file(path, content, dry_run, backup=False):
    """Write `content` to `path` unless dry_run. With backup, an existing file whose bytes would change is copied aside first."""
    if dry_run:
        return
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if backup and path.exists() and path.read_bytes() != content.replace("\n", os.linesep).encode("utf-8"):
        shutil.copy2(path, str(path) + ".bak_nametags_" + datetime.now().strftime("%Y%m%d_%H%M%S"))
    path.write_text(content, encoding="utf-8")


def _rows_of(xml_text):
    root = ET.fromstring(xml_text.encode("utf-8"))
    return {el.get("id"): el.get("text") or "" for el in root.iter("string")}


def summarize_map_diff(current_xml, new_xml):
    """How `new_xml` differs from `current_xml`: rows, tag-only changes, other changes, row-set differences."""
    cur, new = _rows_of(current_xml), _rows_of(new_xml)
    summary = {"rows": len(new), "tag_only": 0, "unchanged": 0, "other_changes": [],
               "only_in_new": [i for i in new if i not in cur], "only_in_current": [i for i in cur if i not in new]}
    for sid, text in new.items():
        if sid not in cur:
            continue
        if cur[sid] == text:
            summary["unchanged"] += 1
        elif name_tags.strip_leading_tags(cur[sid]) == name_tags.strip_leading_tags(text):
            summary["tag_only"] += 1
        else:
            summary["other_changes"].append((sid, cur[sid], text))
    return summary


def report_dry_run(label, path, content):
    """Print how a dry-run rebuild of `path` would differ from the file on disk (read only)."""
    if not Path(path).exists():
        print(f"  {label} dry run: {path} does not exist yet")
        return
    s = summarize_map_diff(Path(path).read_text(encoding="utf-8"), content)
    print(f"  {label} dry run vs the current file: {s['rows']} rows, {s['tag_only']} tag-only changes, "
          f"{s['unchanged']} unchanged, {len(s['other_changes'])} other changes, "
          f"{len(s['only_in_new'])} only in the rebuild, {len(s['only_in_current'])} only in the current file")


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


def rebuild_language(lang, dry_run=False, allow_live_write=False):
    locale, lang_name = LANGUAGES[lang]
    print(f"\n[{lang}] {lang_name}{' (dry run: nothing is written)' if dry_run else ''}")

    # Load overrides + cache
    overrides_path = REPO_ROOT / "tools" / "translation_overrides" / f"{lang.lower()}.json"
    cache_path = REPO_ROOT / "tools" / "translation_cache" / f"{lang.lower()}.json"
    overrides = json.loads(overrides_path.read_text(encoding="utf-8")) if overrides_path.exists() else {}
    cache = json.loads(cache_path.read_text(encoding="utf-8")) if cache_path.exists() else {}
    # Remove non-id metadata keys (e.g., "_comment")
    overrides = {k: v for k, v in overrides.items() if not k.startswith("_")}

    print(f"  Overrides: {len(overrides)}, Cache: {len(cache)}")
    # Loaded once per run, and only for a language that has tags.
    tag_table = name_tags.load_table() if lang in name_tags.TAGGED_LANGUAGES else None

    def resolve(sid, english):
        if sid in overrides:
            return overrides[sid], "override"
        if sid in cache:
            return cache[sid], "cache"
        return english, "english_fallback"

    # ─── TAOM module: the sources of tools/_loc_sources.py ──────────────────────
    owners = source_owners()
    taom_target_dir = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages" / lang
    for src_rel, tgt_name in taom_sources(locale):
        src_path = REPO_ROOT / "Main" / "_Module" / "ModuleData" / src_rel
        if not src_path.exists():
            print(f"  [skip] {src_rel} not found")
            continue
        src_map = parse_source_with_keys(src_path)
        tgt_path = taom_target_dir / tgt_name
        # A vanilla key TAOM rewords in English only gets no language row (VanillaKeyOverrideTests),
        # unless the file already holds one.
        english_only = override_ids_to_skip(src_path, tgt_path)
        ordered = []
        stats = {"override": 0, "cache": 0, "english_fallback": 0}
        for sid, english in src_map.items():
            if sid in english_only or owned_elsewhere(owners, sid, src_path):
                continue
            text, source = resolve(sid, english)
            ordered.append((sid, text))
            stats[source] += 1
        content = build_translation_xml(lang_name, name_tags.tag_entries(lang, ordered, tag_table) if tag_table else ordered)
        if dry_run:
            report_dry_run(f"TAOM {tgt_name}", tgt_path, content)
        write_text_file(tgt_path, content, dry_run)
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
        # The map file is outside the repo: its names get their grammar tag only when --allow-live-write says so.
        content = build_translation_xml(lang_name, name_tags.tag_entries(lang, ordered, tag_table) if tag_table and allow_live_write else ordered)
        if dry_run:
            report_dry_run("TAOM_Map loc_settlements.xml", map_target, content)
        write_text_file(map_target, content, dry_run, backup=True)
        print(f"  TAOM_Map loc_settlements.xml: {len(ordered)} entries (override={stats['override']} cache={stats['cache']} en_fallback={stats['english_fallback']})")

    # ─── LOTRLOME_Armory: 19 source XMLs at root of Languages/ ─────────────────
    armory_root = GAME_ROOT / "LOTRLOME_Armory" / "ModuleData" / "Languages"
    # mkdir(parents=True) under a root that does not exist quietly builds the
    # whole Modules/LOTRLOME_Armory/ModuleData/Languages/<lang> chain there,
    # finds no loc_*.xml to read, and the run still ends on "Done." (#404).
    ensure_exists(armory_root, what="the Armory Languages folder")
    armory_target = armory_root / lang
    if not dry_run:
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
        write_text_file(tgt, content, dry_run)
        print(f"  Armory {src_path.name}: {len(ordered)} entries (override={stats['override']} cache={stats['cache']} en_fallback={stats['english_fallback']})")


def build_parser():
    p = argparse.ArgumentParser()
    p.add_argument("--lang", choices=list(LANGUAGES.keys()))
    p.add_argument("--all", action="store_true")
    p.add_argument("--dry-run", action="store_true",
                   help="build every file in memory and write none; for the live TAOM_Map file print the diff")
    p.add_argument("--allow-live-write", action="store_true",
                   help="add the grammar tags to the live TAOM_Map loc_settlements.xml too (outside the repo; it is backed up first)")
    return p


def main():
    p = build_parser()
    args = p.parse_args()

    if args.all:
        targets = list(LANGUAGES.keys())
    elif args.lang:
        targets = [args.lang]
    else:
        p.error("Specify --lang LANG or --all")

    for lang in targets:
        rebuild_language(lang, dry_run=args.dry_run, allow_live_write=args.allow_live_write)

    print("\nDone.")


if __name__ == "__main__":
    main()
