#!/usr/bin/env python3
"""Import a translation team's rows into TAOM's language files and translation cache.

    python tools/import_team_translations.py --lang CNs --from <TAOM_CN.zip or folder> --report <out.json>
    python tools/import_team_translations.py --lang CNs --lang CNt --from <drop> --report <out.json> --apply

The Chinese team ships its work as a module of its own (TAOM_CN, #1385). Its text is the reference
for Chinese (docs/localization/TRANSLATOR_GUIDE.md, "Chinese: the team's text is the reference"), so
this tool takes it into the repo: a row of ours whose id the team translated gets the team's text, in
the language file and in tools/translation_cache/<lang>.json, so a rebuild keeps it and the AI
translator never drafts it again (the translator picks up only rows that still equal English).

The team's text comes from its ModuleData/Languages/<lang>/*.xml rows. Their runtime and override
copies are not read: in the v2.0.38 drop, every id of ours they hold is also in the language files.

A row of ours with a translation override takes the override: tools/translation_overrides/<lang>.json
wins in every tool, so a hand correction survives each import, and a different team text is reported.
Any other row takes the team's text only when all of these hold:
  - the id is not in KEEP_OURS (decisions recorded below) and has no --skip-prefix;
  - the text is not empty, is not our English, and holds no tab or line break (written raw into an
    attribute, either reads back as a space);
  - no {...} expression in it holds a character outside ASCII and whitespace, such as a Chinese name
    or a full-width question mark: the engine's tokenizer reads none, and then shows the whole string
    empty (Tokenizer.FindTokenMatchesAndText);
  - its placeholders, conditionals and tags match our English (translate_with_claude.placeholders_match);
  - for TAOM, our English for the id had one text at the drop's base version (the TAOM version its
    SubModule.xml depends on, or --base), in every commit since, and now. Otherwise the team may have
    translated words we have since changed, and the row is reported as stale. An id added after the base
    is taken, and listed, only when its English never changed after it was added. TAOM_Map and the
    Armory are not versioned here, so their rows are taken without this check and counted;
  - in a file of names a player must tell apart (troop and lord names, Armory items), it does not give two ids
    with different English one text. An Armory name whose English ends in a tier (I, IV, II-T1) first
    gets that tier appended when the team's text lacks it, the way the team writes it elsewhere.
The tool never adds a row, so it cannot seed a vanilla-key row VanillaKeyOverrideTests forbids.

--module TAOM (the default) writes the repo's files. TAOM_Map and Armory write the live install, each
file backed up first (translate_with_claude.backup_live_file). A dry run (the default) writes only the
JSON report. The report is also written when a run stops after the drop and the base are read.

Exit codes: 0 done. 2 done, but the drop has problems (a file that does not parse, two texts for one
id, no rows for a language): read the report's "problems". 1 stopped (a bad --from or --base, a file of
ours that does not parse, a row that does not land).

The drop is untrusted data: it is read as XML only, from a zip or a folder, and nothing in it is run or
extracted. ElementTree resolves no external entity, and the expat it uses limits entity expansion.
"""

from __future__ import annotations

import argparse
import functools
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _loc_sources import TAOM_SOURCES  # noqa: E402
from translate_with_claude import (  # noqa: E402
    CACHE_DIR,
    LANGUAGES,
    REPO_ROOT,
    TAOM_LANG_DIR,
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
MODULE_ROOT = re.compile(r"^(?:([^/]+)/)?ModuleData/", re.IGNORECASE)
XML_UNSAFE = re.compile(r"[\x00-\x1f\ud800-\udfff￾￿]")
# A character no token of the engine's tokenizer reads inside a {...} expression (Tokenizer's token table).
UNREADABLE = re.compile(r"[^\x00-\x7f\s]")
TIER = re.compile(r"\s((?:[IVXL]+|\d+)(?:-T\d+)?)$")
MODULES = ("TAOM", "TAOM_Map", "Armory")
TEAM_LANGS = ("CNs", "CNt")
ENGLISH_DIR = "Main/_Module/ModuleData"
# Our sources whose rows are names a player picks between, so two of them may never share a text.
DISTINCT_NAME_SOURCES = {"taom_troop_name_strings.xml", "taom_lord_name_strings.xml"}

# Ids whose team text names another entity than our English does: a Chinese player would meet another
# lord, clan or name than an English one. Our row stays, and the report lists the id with the reason for
# the team (#1385). Each pattern must match the whole id.
KEEP_OURS = (
    (r"aom_mordor_(?:male|female)_name_\d+", "the team's Mordor name pools are another list (Gûlsha is 格拉斯卡)"),
    (r"aom_rohan_clan_name_\d+", "the team's Rohan clans are other houses (Harolding is 埃奥尔一族, the Eorlings)"),
    (r"aom_shaghana_clan_\d+", "the team's Shaghana clans are other names (Alâranin is 阿兹拉克氏族, the Azrak clan)"),
    (r"aom_abanissa_clan_\d+", "the team's Âbanissa houses are other names (House of Phaxsharân is 乌洛家族)"),
    (r"aom_clan_dolguldur_1_name", "Hîondrûs is 黑暗獠牙 (Dark Fang) in the team's text"),
    (r"aom_clan_gundabad_1_name", "Ashmog is 贡达巴德之子 (Sons of Gundabad) in the team's text"),
    (r"aom_lord_1_8_name", "Húrioneth is 文德丽娅 in the team's text"),
)


# ── Reading the drop ───────────────────────────────────────────────────────────

class Drop:
    """The team's module, from a zip or a folder, addressed by paths relative to its root folder."""

    def __init__(self, source: Path):
        self._zip = None
        self._files: dict[str, object] = {}
        if source.is_file() and zipfile.is_zipfile(source):
            self._zip = zipfile.ZipFile(source)
            names = [n for n in self._zip.namelist() if not n.endswith("/") and not n.startswith("__MACOSX/")]
            roots = {m.group(1) or "" for n in names if (m := MODULE_ROOT.match(n))}
            if len(roots) != 1:
                self._zip.close()
                raise SystemExit(f"--from {source}: want one module folder holding ModuleData/, found "
                                 f"{sorted(roots) or 'none'}")
            prefix = (roots.pop() + "/").lstrip("/")
            self._files = {n[len(prefix):]: n for n in names if n.startswith(prefix)}
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
        """The files under folder (any depth) whose names end in suffix, both matched without case."""
        prefix = folder.rstrip("/").lower() + "/"
        return sorted(n for n in self._files if n.lower().startswith(prefix) and n.lower().endswith(suffix))

    def find(self, name: str) -> str | None:
        """The drop's own spelling of a root-relative path, matched without case."""
        return next((n for n in self._files if n.lower() == name.lower()), None)

    def read(self, name: str) -> bytes:
        ref = self._files[name]
        if self._zip is not None:
            return self._zip.read(ref)
        return Path(ref).read_bytes()

    def close(self) -> None:
        if self._zip is not None:
            self._zip.close()

    def __enter__(self) -> Drop:
        return self

    def __exit__(self, *exc) -> None:
        self.close()


def _drop_xml(drop: Drop, name: str, problems: list[str]):
    try:
        return ET.fromstring(drop.read(name))
    except ET.ParseError as exc:
        problems.append(f"{name}: does not parse ({exc})")
        return None


def team_rows(drop: Drop, lang: str, problems: list[str]) -> dict[str, tuple[str, str]]:
    """id -> (text, file) from the team's ModuleData/Languages/<lang>/*.xml.

    An id the drop gives two different texts is a problem, and neither text is taken: the engine shows
    whichever file it loads last, so the drop itself does not say which one the team means."""
    rows: dict[str, tuple[str, str]] = {}
    clashing: set[str] = set()
    for name in drop.names(f"ModuleData/Languages/{lang}", ".xml"):
        if name.lower().endswith("/language_data.xml"):
            continue
        root = _drop_xml(drop, name, problems)
        if root is None:
            continue
        for el in root.iter("string"):
            sid, text = el.get("id", ""), el.get("text", "")
            if not sid:
                continue
            if sid in rows and rows[sid][0] != text:
                clashing.add(sid)
            rows.setdefault(sid, (text, name))
    for sid in sorted(clashing):
        problems.append(f"{lang} {sid}: the drop holds two different texts for this id; neither is imported")
        del rows[sid]
    return rows


def base_version(drop: Drop) -> str | None:
    """The TAOM version the drop's SubModule.xml depends on (its DependedModuleMetadata), or None."""
    name = drop.find("SubModule.xml")
    if name is None:
        return None
    try:
        root = ET.fromstring(drop.read(name))
    except ET.ParseError:
        return None
    for el in root.iter("DependedModuleMetadata"):
        if el.get("id") == "TAOM" and el.get("version"):
            return el.get("version")
    return None


# ── Our side ───────────────────────────────────────────────────────────────────

def _rows(data: bytes, where: str, strip_keys: bool) -> dict[str, str]:
    """The <string id text> rows; with strip_keys, text="{=KEY}value" is the row KEY. Stops on a file that
    does not parse, which would otherwise read as empty and file every team row under the wrong list."""
    try:
        root = ET.fromstring(data)
    except ET.ParseError as exc:
        raise SystemExit(f"{where} does not parse ({exc}); fix it before an import") from None
    rows = {}
    for el in root.iter("string"):
        sid, text = el.get("id", ""), el.get("text", "")
        if strip_keys and (m := KEYED.match(text)):
            sid, text = m.group(1), m.group(2)
        if sid:
            rows[sid] = text
    return rows


def _file_rows(path: Path, strip_keys: bool) -> dict[str, str]:
    return _rows(path.read_bytes(), str(path), strip_keys)


def ref_exists(ref: str) -> bool:
    return subprocess.run(["git", "-C", str(REPO_ROOT), "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}"],
                          capture_output=True).returncode == 0


def english_at(ref: str) -> Callable[[str], dict[str, str]]:
    """A reader of one English source of ours as it was at git ref: {} when the file did not exist then."""
    @functools.cache
    def read(src_name: str) -> dict[str, str]:
        shown = subprocess.run(["git", "-C", str(REPO_ROOT), "show", f"{ref}:{ENGLISH_DIR}/{src_name}"],
                               capture_output=True)
        return _rows(shown.stdout, f"{ref}:{src_name}", strip_keys=True) if shown.returncode == 0 else {}
    return read


def english_after(ref: str) -> Callable[[str], dict[str, list[str]]]:
    """A reader of each text every row of one English source had in the commits after git ref that changed
    the file, in log order, oldest first. A row added after the team's base can change again before now."""
    @functools.cache
    def read(src_name: str) -> dict[str, list[str]]:
        path = f"{ENGLISH_DIR}/{src_name}"
        commits = subprocess.run(["git", "-C", str(REPO_ROOT), "log", "--reverse", "--format=%H", f"{ref}..HEAD",
                                  "--", path], capture_output=True, text=True, check=True).stdout.split()
        texts: dict[str, list[str]] = defaultdict(list)
        for commit in commits:
            shown = subprocess.run(["git", "-C", str(REPO_ROOT), "show", f"{commit}:{path}"], capture_output=True)
            if shown.returncode:  # this commit deleted the file
                continue
            for sid, text in _rows(shown.stdout, f"{commit}:{src_name}", strip_keys=True).items():
                if not texts[sid] or texts[sid][-1] != text:
                    texts[sid].append(text)
        return dict(texts)
    return read


@dataclass
class Target:
    path: Path
    english: dict[str, str]
    current: dict[str, str]
    live: bool
    english_then: dict[str, str] | None = None  # our English at the drop's base version; None: no history
    english_later: dict[str, list[str]] = field(default_factory=dict)  # its texts in the commits since
    distinct_names: bool = False                 # names a player picks between: no two may share a text
    tiers: bool = False                          # Armory item names: append the English tier the team left out


def taom_targets(lang: str, lang_root: Path = TAOM_LANG_DIR, source_root: Path | None = None,
                 base: Callable[[str], dict[str, str]] | None = None,
                 later: Callable[[str], dict[str, list[str]]] | None = None) -> list[Target]:
    locale, _ = LANGUAGES[lang]
    source_root = source_root or REPO_ROOT / ENGLISH_DIR
    targets = []
    for src_name, template in TAOM_SOURCES:
        target = lang_root / lang / template.format(locale=locale)
        src = source_root / src_name
        if target.exists() and src.exists():
            targets.append(Target(target, _file_rows(src, strip_keys=True), _file_rows(target, strip_keys=False),
                                  live=False, english_then=base(src_name) if base else None,
                                  english_later=later(src_name) if later else {},
                                  distinct_names=src_name in DISTINCT_NAME_SOURCES))
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
        return [Target(target, settlement_english(source), _file_rows(target, strip_keys=False), live=True)]
    root = armory_lang_dir()
    if not (root / lang).is_dir():
        raise SystemExit(f"Armory: not found: {root / lang}")
    return [Target(root / lang / src.name, _file_rows(src, strip_keys=False),
                   _file_rows(root / lang / src.name, strip_keys=False), live=True, distinct_names=True, tiers=True)
            for src in sorted(root.glob("loc_*.xml")) if (root / lang / src.name).exists()]


# ── The plan ───────────────────────────────────────────────────────────────────

@dataclass
class Plan:
    writes: dict[Path, dict[str, str]] = field(default_factory=dict)
    cache: dict[str, str] = field(default_factory=dict)
    imported: int = 0
    already_equal: int = 0
    unchecked: int = 0
    stale: list[dict] = field(default_factory=list)
    collisions: list[dict] = field(default_factory=list)
    kept_ours: list[dict] = field(default_factory=list)
    override_conflicts: list[dict] = field(default_factory=list)
    rejected: list[dict] = field(default_factory=list)
    new_since_base: list[str] = field(default_factory=list)
    tier_added: list[str] = field(default_factory=list)
    english_in_drop: list[str] = field(default_factory=list)
    skipped_prefix: list[str] = field(default_factory=list)
    orphans: list[str] = field(default_factory=list)
    theirs_only: list[str] = field(default_factory=list)
    ours_untranslated_they_lack: list[str] = field(default_factory=list)
    ours_drafted_they_lack: list[str] = field(default_factory=list)


def keep_ours_reason(sid: str) -> str | None:
    return next((reason for pattern, reason in KEEP_OURS if re.fullmatch(pattern, sid)), None)


def with_tier(english: str, text: str) -> str:
    """text with the tier that ends english (II, IV, 3, II-T1) appended as " II", unless text holds it."""
    m = TIER.search(english)
    if not m or re.search(r"(?<![A-Za-z0-9])" + re.escape(m.group(1)) + r"(?![A-Za-z0-9])", text):
        return text
    return f"{text} {m.group(1)}"


def unreadable_expressions(text: str) -> list[str]:
    """Each {...} expression in text that holds an UNREADABLE character. The expression ends where its
    braces balance, as Tokenizer.FindExpressionEnd cuts it."""
    found, start = [], text.find("{")
    while start != -1:
        depth, end = 1, start + 1
        while end < len(text) and depth:
            depth += {"{": 1, "}": -1}.get(text[end], 0)
            end += 1
        if UNREADABLE.search(text, start, end):
            found.append(text[start:end])
        start = text.find("{", end)
    return found


def _accepted_text(sid: str, current: str, team: dict[str, tuple[str, str]], target: Target,
                   overrides: dict[str, str], skip_prefixes: tuple[str, ...], plan: Plan) -> str | None:
    """The text this row of ours takes from the team, or None with the reason filed in the plan."""
    english = target.english.get(sid)
    if sid not in team:
        if english is not None:
            (plan.ours_untranslated_they_lack if current == english else plan.ours_drafted_they_lack).append(sid)
        return None
    if english is None:
        plan.orphans.append(sid)  # a row with no English source: check_orphan_loc_rows.py owns it
        return None
    if skip_prefixes and sid.startswith(skip_prefixes):
        plan.skipped_prefix.append(sid)
        return None
    text, origin = team[sid]
    if sid in overrides:
        if overrides[sid] != text:
            plan.override_conflicts.append({"id": sid, "override": overrides[sid], "theirs": text})
        return overrides[sid]
    reason = keep_ours_reason(sid)
    if reason:
        plan.kept_ours.append({"id": sid, "reason": reason})
        return None
    if not text.strip() or text == english:
        plan.english_in_drop.append(sid)
        return None
    unsafe = set(XML_UNSAFE.findall(text))
    unreadable = unreadable_expressions(text)
    if unsafe:
        ok, why = False, f"characters an attribute cannot carry: {sorted(hex(ord(c)) for c in unsafe)}"
    elif unreadable:
        ok, why = False, f"the game shows the whole string empty for these {{...}} expressions: {unreadable}"
    else:
        ok, why = placeholders_match(english, text)
    if not ok:
        plan.rejected.append({"id": sid, "file": target.path.name, "origin": origin, "reason": why,
                              "english": english, "theirs": text})
        return None
    if target.english_then is not None:
        then = target.english_then.get(sid)
        texts = ([] if then is None else [then]) + target.english_later.get(sid, []) + [english]
        if len(set(texts)) > 1:
            plan.stale.append({"id": sid, "file": target.path.name, "english_then": texts[0],
                               "english_now": english, "theirs": text})
            return None
        if then is None:
            plan.new_since_base.append(sid)
    return with_tier(english, text) if target.tiers else text


def _drop_collisions(target: Target, accepted: dict[str, str], plan: Plan) -> None:
    """Keeps our text wherever the team's text would give ids with different English one name."""
    while True:
        holders: dict[str, list[str]] = defaultdict(list)
        for sid, current in target.current.items():
            if sid in target.english:
                holders[accepted.get(sid, current)].append(sid)
        clashing = [sid for ids in holders.values() if len({target.english[s] for s in ids}) > 1
                    for sid in ids if sid in accepted and accepted[sid] != target.current[sid]]
        if not clashing:
            return
        for sid in clashing:
            plan.collisions.append({"id": sid, "file": target.path.name, "english": target.english[sid],
                                    "theirs": accepted.pop(sid)})


def make_plan(team: dict[str, tuple[str, str]], targets: list[Target], overrides: dict[str, str],
              skip_prefixes: tuple[str, ...] = ()) -> Plan:
    plan = Plan()
    ours: set[str] = set()
    for target in targets:
        accepted: dict[str, str] = {}
        for sid, current in target.current.items():
            ours.add(sid)
            text = _accepted_text(sid, current, team, target, overrides, skip_prefixes, plan)
            if text is not None:
                accepted[sid] = text
        if target.distinct_names:
            _drop_collisions(target, accepted, plan)
        for sid, text in accepted.items():
            plan.cache[sid] = text
            if target.current[sid] == text:
                plan.already_equal += 1
                continue
            plan.writes.setdefault(target.path, {})[sid] = text
            plan.imported += 1
            if target.english_then is None:
                plan.unchecked += 1
            if target.tiers and sid not in overrides and text != team[sid][0]:
                plan.tier_added.append(sid)
    plan.theirs_only = sorted(set(team) - ours)
    for name in ("new_since_base", "tier_added", "english_in_drop", "skipped_prefix", "orphans",
                 "ours_untranslated_they_lack", "ours_drafted_they_lack"):
        getattr(plan, name).sort()
    for name in ("stale", "collisions", "kept_ours", "override_conflicts", "rejected"):
        getattr(plan, name).sort(key=lambda r: r["id"])
    return plan


def apply_plan(plan: Plan, lang: str, targets: list[Target], cache_dir: Path = CACHE_DIR,
               backups: list[str] | None = None) -> list[str]:
    """Saves the cache, then writes the language files, each live one backed up first.

    The cache goes first: its rows passed every check, and a rebuild writes them into any file a failed
    write below leaves behind. Each backup path is added to backups as it is made, so a caller can still
    report them after a failure."""
    backups = [] if backups is None else backups
    cache = load_cache(lang, cache_dir)
    cache.update(plan.cache)
    save_cache(lang, cache, cache_dir)
    live = {t.path for t in targets if t.live}
    for path, rows in sorted(plan.writes.items()):
        if path in live:
            backups.append(str(backup_live_file(path)))
        before = path.read_bytes()
        tag = TAG.search(before.decode("utf-8-sig"))
        _, unplaced = write_back(path, rows, tag.group(1) if tag else LANGUAGES[lang][1])
        try:  # write_back writes in place, so a document that no longer parses is put back (data-scripts.md)
            ET.parse(path)
        except ET.ParseError as exc:
            path.write_bytes(before)
            raise SystemExit(f"{path}: the written file does not parse ({exc}); it is put back") from None
        if unplaced:
            raise SystemExit(f"{path}: {len(unplaced)} rows did not land, e.g. {unplaced[:3]}")
    return backups


def summary(lang: str, plan: Plan) -> dict:
    return {
        "lang": lang,
        "imported": plan.imported,
        "already_equal": plan.already_equal,
        "unchecked": plan.unchecked,
        "files_written": len(plan.writes),
        "writes": {str(path): sorted(rows) for path, rows in sorted(plan.writes.items())},
        "stale": plan.stale,
        "new_since_base": plan.new_since_base,
        "kept_ours": plan.kept_ours,
        "collisions": plan.collisions,
        "tier_added": plan.tier_added,
        "override_conflicts": plan.override_conflicts,
        "rejected": plan.rejected,
        "english_in_drop": plan.english_in_drop,
        "skipped_prefix": plan.skipped_prefix,
        "orphans": plan.orphans,
        "theirs_only": plan.theirs_only,
        "ours_untranslated_they_lack": plan.ours_untranslated_they_lack,
        "ours_drafted_they_lack": plan.ours_drafted_they_lack,
    }


def _line(lang: str, team_count: int, plan: Plan) -> str:
    return (f"[{lang}] team rows {team_count}; imported {plan.imported} into {len(plan.writes)} files "
            f"({plan.unchecked} with no base to check); already equal {plan.already_equal}; "
            f"stale {len(plan.stale)}; kept ours {len(plan.kept_ours)}; collisions {len(plan.collisions)}; "
            f"tier added {len(plan.tier_added)}; override conflicts {len(plan.override_conflicts)}; "
            f"rejected {len(plan.rejected)}; English in drop {len(plan.english_in_drop)}; "
            f"skipped by prefix {len(plan.skipped_prefix)}; orphans {len(plan.orphans)}; "
            f"theirs only {len(plan.theirs_only)}; ours they lack {len(plan.ours_untranslated_they_lack)} "
            f"untranslated and {len(plan.ours_drafted_they_lack)} drafted")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    p.add_argument("--lang", action="append", required=True, choices=TEAM_LANGS)
    p.add_argument("--from", dest="source", required=True, type=Path, help="the team's module: a .zip or a folder")
    p.add_argument("--module", action="append", choices=MODULES,
                   help="our module(s) to write; default TAOM (TAOM_Map and Armory are live installs)")
    p.add_argument("--base", help="the git ref of TAOM the team translated (default: the TAOM version the "
                                  "drop's SubModule.xml depends on)")
    p.add_argument("--skip-prefix", action="append", default=[],
                   help="ids to leave alone, e.g. realms_ while another session owns them")
    p.add_argument("--report", required=True, type=Path, help="where to write the JSON report")
    p.add_argument("--apply", action="store_true", help="write the files (default: a dry run)")
    args = p.parse_args(argv)

    langs = list(dict.fromkeys(args.lang))
    modules = list(dict.fromkeys(args.module or ["TAOM"]))
    problems: list[str] = []
    with Drop(args.source) as drop:
        base = args.base or base_version(drop)
        teams = {lang: team_rows(drop, lang, problems) for lang in langs}
    if "TAOM" in modules:
        if not base:
            raise SystemExit(f"--from {args.source}: its SubModule.xml names no TAOM version; pass --base <tag>")
        if not ref_exists(base):
            raise SystemExit(f"--base {base}: not a commit or tag in this repo (git fetch --tags?)")
    report = {"source": str(args.source), "modules": modules, "base": base, "applied": args.apply,
              "problems": problems, "languages": []}
    then, later = english_at(base), english_after(base)  # read only for TAOM, once per source file (cached)
    try:
        for lang in langs:
            team = teams[lang]
            if not team:
                problems.append(f"{lang}: the drop holds no rows under ModuleData/Languages/{lang}")
            targets = []
            for module in modules:
                targets.extend(taom_targets(lang, base=then, later=later) if module == "TAOM"
                               else live_targets(lang, module))
            plan = make_plan(team, targets, load_overrides(lang), tuple(args.skip_prefix))
            entry = summary(lang, plan)
            entry["team_rows"] = len(team)
            report["languages"].append(entry)
            if args.apply:
                entry["backups"] = []
                apply_plan(plan, lang, targets, backups=entry["backups"])
            print(_line(lang, len(team), plan))
    except BaseException as exc:
        report["error"] = f"{type(exc).__name__}: {exc}"
        raise
    finally:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for problem in report["problems"]:
        print(f"  PROBLEM: {problem}", file=sys.stderr)
    print(f"report: {args.report}" + ("" if args.apply else "  (dry run: nothing else written)"))
    return 2 if report["problems"] else 0


if __name__ == "__main__":
    sys.exit(main())
