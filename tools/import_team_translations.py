#!/usr/bin/env python3
"""Import a translation team's rows into TAOM's language files and translation cache.

    python tools/import_team_translations.py --lang CNs --from <TAOM_CN.zip or folder> --report <out.json>
    python tools/import_team_translations.py --lang CNs --lang CNt --from <drop> --report <out.json> --apply

The Chinese team ships its work as a module of its own (TAOM_CN, #1385). Its text is the reference
for Chinese (docs/localization/TRANSLATOR_GUIDE.md, "Chinese: the team's text is the reference"), so
this tool takes it into the repo: every row of ours whose id the team translated gets the team's
text, in the language file and in tools/translation_cache/<lang>.json, so a rebuild keeps it and the
AI translator never drafts it again (the translator picks up only rows that still equal English).

Where the team's text comes from, first source wins per id:
  1. ModuleData/Languages/<lang>/*.xml rows (their language files);
  2. {=KEY}text values in their RuntimeTranslations*.xml (ModuleData/ for CNs, ModuleData/CNt/ for CNt);
  3. {=KEY}text values in their Overrides/ (CNs) or Overrides.CNt/ (CNt) file copies.

A row is taken only when its id is already a row of ours, its placeholders, conditionals and tags
match our English (translate_with_claude.placeholders_match), and it is not the English text itself.
The tool never adds a row, so it cannot seed a vanilla-key row VanillaKeyOverrideTests forbids.

--module TAOM (the default) writes the repo's files. TAOM_Map and Armory write the live install,
each file backed up first (translate_with_claude.backup_live_file). A dry run (the default) writes
nothing; --report always writes the JSON report to the path given.

The drop is untrusted data: it is read as XML and JSON only, from a zip or a folder, and nothing in it
is run. ElementTree resolves no external entity, and the expat it uses limits entity expansion.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _loc_sources import TAOM_SOURCES  # noqa: E402
from translate_with_claude import (  # noqa: E402
    LANGUAGES,
    REPO_ROOT,
    TAOM_LANG_DIR,
    _parse_string_xml,
    armory_lang_dir,
    backup_live_file,
    game_modules_root,
    load_cache,
    load_overrides,
    placeholders_match,
    save_cache,
    taom_map_lang_dir,
    write_back,
)

KEYED = re.compile(r"^\{=([^}]+)\}(.*)$", re.DOTALL)
TAG = re.compile(r'<tag\s+language="([^"]*)"\s*/>')
MODULES = ("TAOM", "TAOM_Map", "Armory")

# Where each language's runtime and override copies sit in the team's module.
TEAM_LAYOUT = {
    "CNs": {"runtime": "ModuleData", "overrides": "Overrides"},
    "CNt": {"runtime": "ModuleData/CNt", "overrides": "Overrides.CNt"},
}


# ── Reading the drop ───────────────────────────────────────────────────────────

class Drop:
    """The team's module, from a zip or a folder, addressed by paths relative to its root folder."""

    def __init__(self, source: Path):
        self._zip = None
        self._files: dict[str, object] = {}
        if source.is_file() and zipfile.is_zipfile(source):
            self._zip = zipfile.ZipFile(source)
            names = [n for n in self._zip.namelist() if not n.endswith("/")]
            root = _common_root(names)
            self._files = {n[len(root):]: n for n in names}
        elif source.is_dir():
            if not (source / "ModuleData").is_dir():
                inner = [d for d in source.iterdir() if (d / "ModuleData").is_dir()]
                if len(inner) != 1:
                    raise SystemExit(f"--from {source}: no ModuleData folder at its root or one level down")
                source = inner[0]
            self._files = {p.relative_to(source).as_posix(): p for p in source.rglob("*") if p.is_file()}
        else:
            raise SystemExit(f"--from {source}: not a zip file or a folder")

    def names(self, folder: str, suffix: str) -> list[str]:
        prefix = folder.rstrip("/") + "/"
        return sorted(n for n in self._files if n.startswith(prefix) and n.lower().endswith(suffix))

    def names_directly_in(self, folder: str, suffix: str) -> list[str]:
        prefix = folder.rstrip("/") + "/"
        return [n for n in self.names(folder, suffix) if "/" not in n[len(prefix):]]

    def read(self, name: str) -> bytes:
        ref = self._files[name]
        if self._zip is not None:
            return self._zip.read(ref)
        return Path(ref).read_bytes()


def _common_root(names: list[str]) -> str:
    """The single top folder every name shares ("TAOM_CN/"), or "" when the drop has none."""
    tops = {n.split("/", 1)[0] for n in names if "/" in n}
    if len(tops) == 1 and all("/" in n for n in names):
        top = tops.pop()
        if not any(n.startswith("ModuleData/") for n in names):
            return top + "/"
    return ""


def _xml(drop: Drop, name: str, problems: list[str]):
    try:
        return ET.fromstring(drop.read(name))
    except ET.ParseError as exc:
        problems.append(f"{name}: does not parse ({exc})")
        return None


def _keyed_values(value) -> list[tuple[str, str]]:
    """Every {=KEY}text string inside a JSON value."""
    found = []
    if isinstance(value, str):
        m = KEYED.match(value)
        if m:
            found.append((m.group(1), m.group(2)))
    elif isinstance(value, dict):
        for v in value.values():
            found.extend(_keyed_values(v))
    elif isinstance(value, list):
        for v in value:
            found.extend(_keyed_values(v))
    return found


def _keyed_attributes(root) -> list[tuple[str, str]]:
    found = []
    for el in root.iter():
        for value in el.attrib.values():
            m = KEYED.match(value)
            if m:
                found.append((m.group(1), m.group(2)))
    return found


def team_rows(drop: Drop, lang: str, problems: list[str]) -> dict[str, tuple[str, str]]:
    """id -> (text, origin), first source wins (see the module docstring)."""
    rows: dict[str, tuple[str, str]] = {}

    def take(sid: str, text: str, origin: str):
        if sid and sid != "!" and sid not in rows:
            rows[sid] = (text, origin)

    for name in drop.names(f"ModuleData/Languages/{lang}", ".xml"):
        if name.endswith("/language_data.xml"):
            continue
        root = _xml(drop, name, problems)
        if root is None:
            continue
        for el in root.iter("string"):
            take(el.get("id", ""), el.get("text", ""), name)

    layout = TEAM_LAYOUT.get(lang)
    if layout is None:
        return rows
    for name in drop.names_directly_in(layout["runtime"], ".xml"):
        if not Path(name).name.startswith("RuntimeTranslations"):
            continue
        root = _xml(drop, name, problems)
        if root is None:
            continue
        for el in root.iter("string"):
            m = KEYED.match(el.get("text", ""))
            if m:
                take(m.group(1), m.group(2), name)
    for name in drop.names(layout["overrides"], ""):
        lower = name.lower()
        if lower.endswith(".xml"):
            root = _xml(drop, name, problems)
            pairs = _keyed_attributes(root) if root is not None else []
        elif lower.endswith(".json"):
            try:
                pairs = _keyed_values(json.loads(drop.read(name).decode("utf-8-sig")))
            except (ValueError, UnicodeDecodeError) as exc:
                problems.append(f"{name}: not JSON ({exc})")
                pairs = []
        else:
            continue
        for sid, text in pairs:
            take(sid, text, name)
    return rows


# ── Our side ───────────────────────────────────────────────────────────────────

@dataclass
class Target:
    path: Path
    english: dict[str, str]
    current: dict[str, str]
    live: bool


def taom_targets(lang: str, lang_root: Path = TAOM_LANG_DIR, source_root: Path | None = None) -> list[Target]:
    locale, _ = LANGUAGES[lang]
    source_root = source_root or REPO_ROOT / "Main" / "_Module" / "ModuleData"
    targets = []
    for src_name, template in TAOM_SOURCES:
        target = lang_root / lang / template.format(locale=locale)
        src = source_root / src_name
        if target.exists() and src.exists():
            targets.append(Target(target, _parse_string_xml(src, strip_keys=True),
                                  _parse_string_xml(target, strip_keys=False), live=False))
    return targets


def settlement_english(settlements_xml: Path) -> dict[str, str]:
    """The {=KEY}default pairs inline in TAOM_Map's settlements.xml, first default per key."""
    content = settlements_xml.read_text(encoding="utf-8-sig")
    english: dict[str, str] = {}
    for key, default in re.findall(r'\w+="\{=([^}]+)\}([^"]*)"', content):
        english.setdefault(key, default.replace("&quot;", '"').replace("&lt;", "<")
                           .replace("&gt;", ">").replace("&amp;", "&"))
    return english


def live_targets(lang: str, module: str) -> list[Target]:
    if module == "TAOM_Map":
        target = taom_map_lang_dir() / lang / "loc_settlements.xml"
        source = game_modules_root() / "TAOM_Map" / "ModuleData" / "settlements.xml"
        if not (target.exists() and source.exists()):
            raise SystemExit(f"TAOM_Map: not found: {target if not target.exists() else source}")
        return [Target(target, settlement_english(source), _parse_string_xml(target, strip_keys=False), live=True)]
    root = armory_lang_dir()
    if not (root / lang).is_dir():
        raise SystemExit(f"Armory: not found: {root / lang}")
    return [Target(root / lang / src.name, _parse_string_xml(src, strip_keys=False),
                   _parse_string_xml(root / lang / src.name, strip_keys=False), live=True)
            for src in sorted(root.glob("loc_*.xml")) if (root / lang / src.name).exists()]


# ── The plan ───────────────────────────────────────────────────────────────────

@dataclass
class Plan:
    writes: dict[Path, dict[str, str]] = field(default_factory=dict)
    cache: dict[str, str] = field(default_factory=dict)
    imported: int = 0
    already_equal: int = 0
    rejected: list[dict] = field(default_factory=list)
    english_in_drop: list[str] = field(default_factory=list)
    skipped_prefix: list[str] = field(default_factory=list)
    override_conflicts: list[dict] = field(default_factory=list)
    theirs_only: list[str] = field(default_factory=list)
    ours_untranslated_they_lack: list[str] = field(default_factory=list)


def make_plan(team: dict[str, tuple[str, str]], targets: list[Target], overrides: dict[str, str],
              skip_prefixes: tuple[str, ...] = ()) -> Plan:
    plan = Plan()
    ours: set[str] = set()
    for target in targets:
        for sid, current in target.current.items():
            ours.add(sid)
            english = target.english.get(sid)
            if sid not in team:
                if english is not None and current == english:
                    plan.ours_untranslated_they_lack.append(sid)
                continue
            if skip_prefixes and sid.startswith(skip_prefixes):
                plan.skipped_prefix.append(sid)
                continue
            text, origin = team[sid]
            if english is None:
                # A row with no English source is an orphan; leave it to check_orphan_loc_rows.py.
                continue
            if not text.strip() or text == english:
                plan.english_in_drop.append(sid)
                continue
            ok, reason = placeholders_match(english, text)
            if not ok:
                plan.rejected.append({"id": sid, "file": target.path.name, "origin": origin,
                                      "reason": reason, "english": english, "theirs": text})
                continue
            if sid in overrides and overrides[sid] != text:
                plan.override_conflicts.append({"id": sid, "override": overrides[sid], "theirs": text})
            plan.cache[sid] = text
            if current == text:
                plan.already_equal += 1
                continue
            plan.writes.setdefault(target.path, {})[sid] = text
            plan.imported += 1
    plan.theirs_only = sorted(set(team) - ours)
    for name in ("rejected", "english_in_drop", "skipped_prefix", "ours_untranslated_they_lack"):
        value = getattr(plan, name)
        value.sort(key=(lambda r: r["id"]) if name == "rejected" else None)
    return plan


def apply_plan(plan: Plan, lang: str, targets: list[Target], cache_dir: Path | None = None) -> list[str]:
    """Writes the language files (live ones backed up first) and the cache; returns the backups made."""
    live = {t.path for t in targets if t.live}
    backups = []
    for path, rows in sorted(plan.writes.items()):
        if path in live:
            backups.append(str(backup_live_file(path)))
        content = path.read_text(encoding="utf-8")
        tag = TAG.search(content)
        written, unplaced = write_back(path, rows, tag.group(1) if tag else LANGUAGES[lang][1])
        if unplaced:
            raise SystemExit(f"{path}: {len(unplaced)} rows did not land, e.g. {unplaced[:3]}")
        ET.parse(path)  # refuse to leave a file that no longer parses unnoticed
    cache = load_cache(lang, cache_dir) if cache_dir else load_cache(lang)
    cache.update(plan.cache)
    if cache_dir:
        save_cache(lang, cache, cache_dir)
    else:
        save_cache(lang, cache)
    return backups


def summary(lang: str, plan: Plan) -> dict:
    return {
        "lang": lang,
        "imported": plan.imported,
        "already_equal": plan.already_equal,
        "files_written": len(plan.writes),
        "rejected_placeholders": plan.rejected,
        "english_in_drop": plan.english_in_drop,
        "skipped_prefix": plan.skipped_prefix,
        "override_conflicts": plan.override_conflicts,
        "theirs_only": plan.theirs_only,
        "ours_untranslated_they_lack": plan.ours_untranslated_they_lack,
    }


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    p.add_argument("--lang", action="append", required=True, choices=sorted(TEAM_LAYOUT))
    p.add_argument("--from", dest="source", required=True, type=Path, help="the team's module: a .zip or a folder")
    p.add_argument("--module", action="append", choices=MODULES,
                   help="our module(s) to write; default TAOM (TAOM_Map and Armory are live installs)")
    p.add_argument("--skip-prefix", action="append", default=[],
                   help="ids to leave alone, e.g. realms_ while another session owns them")
    p.add_argument("--report", required=True, type=Path, help="where to write the JSON report")
    p.add_argument("--apply", action="store_true", help="write the files (default: a dry run)")
    args = p.parse_args(argv)

    drop = Drop(args.source)
    modules = args.module or ["TAOM"]
    report = {"source": str(args.source), "modules": modules, "applied": args.apply, "problems": [], "languages": []}
    for lang in args.lang:
        team = team_rows(drop, lang, report["problems"])
        targets = []
        for module in modules:
            targets.extend(taom_targets(lang) if module == "TAOM" else live_targets(lang, module))
        plan = make_plan(team, targets, load_overrides(lang), tuple(args.skip_prefix))
        entry = summary(lang, plan)
        entry["team_rows"] = len(team)
        if args.apply:
            entry["backups"] = apply_plan(plan, lang, targets)
        report["languages"].append(entry)
        print(f"[{lang}] team rows {len(team)}; imported {plan.imported} into {len(plan.writes)} files; "
              f"already equal {plan.already_equal}; rejected (placeholders) {len(plan.rejected)}; "
              f"English in drop {len(plan.english_in_drop)}; skipped by prefix {len(plan.skipped_prefix)}; "
              f"override conflicts {len(plan.override_conflicts)}; theirs only {len(plan.theirs_only)}; "
              f"ours untranslated and they lack {len(plan.ours_untranslated_they_lack)}")
    for problem in report["problems"]:
        print(f"  PROBLEM: {problem}", file=sys.stderr)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"report: {args.report}" + ("" if args.apply else "  (dry run: nothing else written)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
