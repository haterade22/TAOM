#!/usr/bin/env python3
"""Schema-driven cross-reference + validation engine for TAOM ModuleData XML.

This is TAOM's port of the IDEAS behind TheOldRealms/TOR_Tools (MIT) —
its `SchemaDefinition`/`CrossReferenceService`/`ValidationService` — into TAOM's
Python tooling world. It does NOT vendor any TOR code; only the architecture is
adopted: declarative JSON schemas as the source of truth, a cross-reference
graph resolved against id registries, and a severity-classified issue model.

Design goals:
  * Decoupled from the game install — `Registries` is injected, so the engine is
    unit-testable with synthetic data (see tools/tests/test_validate_moduledata.py).
  * Reuses the proven scan logic of the existing one-shot validators
    (audit_item_refs.py multi-module item registry, validate_all_troop_refs.py
    Armory scan) instead of reinventing it.
  * Prefix-based ref resolution (Item. / NPCCharacter. / Culture. / PartyTemplate.)
    — the prefixes are globally unambiguous in Bannerlord XML, exactly how the
    existing TAOM validators already match.

CLI entry point lives in tools/validate_moduledata.py.
"""
from __future__ import annotations

import fnmatch
import glob
import json
import os
import re
from collections import defaultdict
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path


# --------------------------------------------------------------------------- #
# Issue model (mirrors TOR's ValidationIssue: severity + location + message)   #
# --------------------------------------------------------------------------- #
class Severity(Enum):
    ERROR = "ERROR"
    WARNING = "WARNING"
    INFO = "INFO"


_SEVERITY_ORDER = {Severity.ERROR: 0, Severity.WARNING: 1, Severity.INFO: 2}


@dataclass
class Issue:
    severity: Severity
    code: str
    file: str
    line: int
    entry_id: str
    message: str

    def sort_key(self):
        return (_SEVERITY_ORDER[self.severity], self.file, self.line, self.code)


# --------------------------------------------------------------------------- #
# Registries (the cross-reference targets; injected so the engine is testable) #
# --------------------------------------------------------------------------- #
@dataclass
class Registries:
    items: set                       # all defined Item ids (no "Item." prefix)
    item_def_files: dict             # Armory item id -> [files] when defined >1x (dup detection)
    npccharacters: set               # all defined NPCCharacter ids
    cultures: set                    # valid culture StringIds
    party_templates: set             # defined PartyTemplate ids


# --------------------------------------------------------------------------- #
# Schema model (the JSON files under tools/schemas/ are the source of truth)   #
# --------------------------------------------------------------------------- #
# Special-rule names that map to a handler in Validator. A schema declaring a
# name outside this set is a developer error (typo / unimplemented rule) and is
# rejected at load time rather than silently ignored (deep-review 2026-05-30).
KNOWN_SPECIAL_RULES = {"civilian_equipment_type"}


@dataclass
class Schema:
    name: str
    applies_to: list                 # globs relative to ModuleData root
    entry_element: str
    id_attribute: str = "id"
    duplicate_code: str = "DUPLICATE_ID"
    enums: dict = field(default_factory=dict)          # attr -> [allowed]
    special_rules: list = field(default_factory=list)
    description: str = ""

    @staticmethod
    def from_json(obj: dict) -> "Schema":
        for key in ("name", "applies_to", "entry_element"):
            if not obj.get(key):
                raise ValueError(f"schema is missing required field '{key}': {obj.get('name', obj)}")
        unknown = set(obj.get("special_rules", [])) - KNOWN_SPECIAL_RULES
        if unknown:
            raise ValueError(
                f"schema '{obj['name']}' declares unknown special_rules {sorted(unknown)}; "
                f"known rules: {sorted(KNOWN_SPECIAL_RULES)}")
        return Schema(
            name=obj["name"],
            applies_to=obj["applies_to"],
            entry_element=obj["entry_element"],
            id_attribute=obj.get("id_attribute", "id"),
            duplicate_code=obj.get("duplicate_code", "DUPLICATE_ID"),
            enums=obj.get("enums", {}),
            special_rules=obj.get("special_rules", []),
            description=obj.get("description", ""),
        )


def load_schemas(schema_dir) -> list:
    schema_dir = Path(schema_dir)
    schemas = []
    for p in sorted(schema_dir.glob("*.json")):
        if p.name.startswith("_"):
            continue
        with open(p, encoding="utf-8") as f:
            schemas.append(Schema.from_json(json.load(f)))
    return schemas


# --------------------------------------------------------------------------- #
# Cross-reference kinds (prefix-based, globally unambiguous)                   #
# --------------------------------------------------------------------------- #
@dataclass
class RefKind:
    name: str
    pattern: re.Pattern
    registry_attr: str
    severity: Severity
    code: str
    label: str
    prefix: str
    allow: set = field(default_factory=set)


# Patterns are ATTRIBUTE-AGNOSTIC — they match the prefixed value on ANY
# attribute (`id="Item.x"`, `troop="NPCCharacter.x"`, `culture="Culture.x"`,
# `*_party_template="PartyTemplate.x"`). Anchoring on a specific attribute (e.g.
# `id=`) silently misses real refs: party-template stacks reference troops via
# `troop="NPCCharacter.x"`, not `id=` (deep-review 2026-05-30, HIGH). The prefix
# itself is unambiguous, so the attribute name is irrelevant. Definitions never
# carry the prefix in their value (`<NPCCharacter id="x">`, not `"NPCCharacter.x"`),
# so a def is never mistaken for a ref.
REF_KINDS = [
    RefKind("item", re.compile(r'="Item\.([A-Za-z0-9_.\-]+)"'),
            "items", Severity.ERROR, "BROKEN_ITEM_REF", "item", "Item.", {"None"}),
    RefKind("npccharacter", re.compile(r'="NPCCharacter\.([A-Za-z0-9_.\-]+)"'),
            "npccharacters", Severity.ERROR, "BROKEN_TROOP_REF", "troop/character", "NPCCharacter."),
    RefKind("culture", re.compile(r'="Culture\.([A-Za-z0-9_.\-]+)"'),
            "cultures", Severity.ERROR, "UNKNOWN_CULTURE", "culture", "Culture."),
    RefKind("party_template", re.compile(r'="PartyTemplate\.([A-Za-z0-9_.\-]+)"'),
            "party_templates", Severity.WARNING, "BROKEN_PARTY_TEMPLATE_REF", "party template", "PartyTemplate."),
]


def _lineno(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


# --------------------------------------------------------------------------- #
# Validator                                                                    #
# --------------------------------------------------------------------------- #
class Validator:
    def __init__(self, moduledata, schemas: list, registries: Registries):
        self.moduledata = Path(moduledata)
        self.schemas = schemas
        self.reg = registries

    # -- public ----------------------------------------------------------- #
    def run(self) -> list:
        issues = []
        issues += self._ref_sweep()
        issues += self._schema_checks()
        issues += self._duplicate_item_defs()
        issues.sort(key=lambda i: i.sort_key())
        return issues

    # -- helpers ---------------------------------------------------------- #
    def _xml_files(self):
        yield from sorted(self.moduledata.rglob("*.xml"))

    def _rel(self, path: Path) -> str:
        try:
            return path.relative_to(self.moduledata).as_posix()
        except ValueError:
            return str(path)

    def _schema_for(self, rel: str):
        for s in self.schemas:
            if any(fnmatch.fnmatch(rel, g) for g in s.applies_to):
                return s
        return None

    @staticmethod
    def _read(path: Path) -> str:
        return path.read_text(encoding="utf-8", errors="ignore")

    # -- pass 1: cross-reference sweep ------------------------------------ #
    def _ref_sweep(self) -> list:
        issues = []
        active = [k for k in REF_KINDS if getattr(self.reg, k.registry_attr)]
        for path in self._xml_files():
            rel = self._rel(path)
            text = self._read(path)
            schema = self._schema_for(rel)
            entry_el = schema.entry_element if schema else None
            id_attr = schema.id_attribute if schema else "id"
            line_entry = self._entry_by_line(text, entry_el, id_attr) if entry_el else {}
            for kind in active:
                registry = getattr(self.reg, kind.registry_attr)
                for m in kind.pattern.finditer(text):
                    ref = m.group(1)
                    if ref in kind.allow or ref in registry:
                        continue
                    line = _lineno(text, m.start())
                    issues.append(Issue(
                        severity=kind.severity, code=kind.code, file=rel, line=line,
                        entry_id=line_entry.get(line, ""),
                        message=f'{kind.label} "{kind.prefix}{ref}" does not resolve to any known {kind.label}',
                    ))
        return issues

    def _entry_by_line(self, text: str, entry_el: str, id_attr: str) -> dict:
        """Best-effort map of line number -> owning entry id, for attribution.

        Handles both single-line and multi-line element opens (TAOM troop files
        spread NPCCharacter attributes across many lines)."""
        result = {}
        open_re = re.compile(r"<" + re.escape(entry_el) + r"\b")
        id_re = re.compile(r'\b' + re.escape(id_attr) + r'="([A-Za-z0-9_.\-]+)"')
        current = ""
        awaiting = False
        for i, line in enumerate(text.splitlines(), start=1):
            if open_re.search(line):
                awaiting = True
            if awaiting:
                mid = id_re.search(line)
                if mid:
                    current = mid.group(1)
                    awaiting = False
            result[i] = current
        return result

    # -- pass 2: per-schema dup-id / enum / special rules ----------------- #
    def _schema_checks(self) -> list:
        issues = []
        for schema in self.schemas:
            files = self._files_for(schema)
            issues += self._duplicate_ids(schema, files)
            issues += self._enums(schema, files)
            if "civilian_equipment_type" in schema.special_rules:
                issues += self._civilian_rule(files)
        return issues

    def _files_for(self, schema: Schema):
        out = []
        for path in self._xml_files():
            if any(fnmatch.fnmatch(self._rel(path), g) for g in schema.applies_to):
                out.append(path)
        return out

    def _duplicate_ids(self, schema: Schema, files) -> list:
        entry_re = re.compile(
            r"<" + re.escape(schema.entry_element) + r"\b[^>]*?\b"
            + re.escape(schema.id_attribute) + r'="([^"]+)"', re.S)
        seen = defaultdict(list)
        for path in files:
            text = self._read(path)
            for m in entry_re.finditer(text):
                seen[m.group(1)].append((self._rel(path), _lineno(text, m.start())))
        issues = []
        for entry_id, locs in seen.items():
            if len(locs) > 1:
                where = "; ".join(f"{f}:{ln}" for f, ln in locs)
                issues.append(Issue(
                    severity=Severity.ERROR, code=schema.duplicate_code,
                    file=locs[0][0], line=locs[0][1], entry_id=entry_id,
                    message=f'{schema.entry_element} id "{entry_id}" defined {len(locs)} times ({where})',
                ))
        return issues

    def _enums(self, schema: Schema, files) -> list:
        issues = []
        for attr, allowed in schema.enums.items():
            allowed_set = set(allowed)
            attr_re = re.compile(r'\b' + re.escape(attr) + r'="([^"]+)"')
            for path in files:
                text = self._read(path)
                line_entry = self._entry_by_line(text, schema.entry_element, schema.id_attribute)
                for m in attr_re.finditer(text):
                    val = m.group(1)
                    if val not in allowed_set:
                        line = _lineno(text, m.start())
                        issues.append(Issue(
                            severity=Severity.WARNING, code="INVALID_ENUM",
                            file=self._rel(path), line=line,
                            entry_id=line_entry.get(line, ""),
                            message=f'{attr}="{val}" is not one of {sorted(allowed_set)}',
                        ))
        return issues

    _ROSTER_RE = re.compile(
        r'<EquipmentRoster\b[^>]*\bid="([^"]+)"[^>]*>(.*?)</EquipmentRoster>', re.S)
    _EQSET_OPEN_RE = re.compile(r"<EquipmentSet\b[^>]*?>")

    @staticmethod
    def _is_civilian_roster(roster_id: str) -> bool:
        """Demonstrably-civilian roster id markers. `_civ` catches `_civilian_`;
        `child_template_` catches the child rosters (taom_child_equipment_templates.xml
        is 114/114 Civilian-tagged). The education templates (`child_education_*`,
        0/784 tagged) use a different, unconfirmed convention and are deliberately
        NOT treated as civilian, to avoid 784 false positives (Codex review
        2026-05-30 -- documented scope gap in docs/features/moduledata-validation.md)."""
        if "child_education" in roster_id:
            return False
        return "_civ" in roster_id or "child_template" in roster_id

    def _civilian_rule(self, files) -> list:
        """Civilian standalone rosters must tag EVERY EquipmentSet as Civilian.
        Mirrors .claude/rules/xml-data.md 'EquipmentRosters Schema'."""
        issues = []
        for path in files:
            text = self._read(path)
            for m in self._ROSTER_RE.finditer(text):
                roster_id, body = m.group(1), m.group(2)
                if not self._is_civilian_roster(roster_id):
                    continue
                untagged = [s for s in self._EQSET_OPEN_RE.findall(body)
                            if 'equipmentType="Civilian"' not in s]
                if untagged:
                    issues.append(Issue(
                        severity=Severity.WARNING, code="MISSING_CIVILIAN_TYPE",
                        file=self._rel(path), line=_lineno(text, m.start()),
                        entry_id=roster_id,
                        message=f'civilian roster "{roster_id}" has {len(untagged)} EquipmentSet(s) missing equipmentType="Civilian"',
                    ))
        return issues

    # -- pass 3: duplicate item definitions across folders ---------------- #
    def _duplicate_item_defs(self) -> list:
        issues = []
        for item_id, files in self.reg.item_def_files.items():
            uniq = sorted(set(files))
            if len(uniq) > 1:
                issues.append(Issue(
                    severity=Severity.WARNING, code="DUPLICATE_ITEM_DEF",
                    file=uniq[0], line=0, entry_id=item_id,
                    message=f'item "{item_id}" is defined in {len(uniq)} files ({"; ".join(uniq)}) — engine silently shadows one',
                ))
        return issues


# --------------------------------------------------------------------------- #
# Registry builders (real data) — reuse the existing validators' proven logic  #
# --------------------------------------------------------------------------- #
_ITEM_DEF_RE = re.compile(r'<(?:Item|CraftedItem)\s+[^>]*?\bid="([^"]+)"', re.S)
_NPC_DEF_RE = re.compile(r'<NPCCharacter\b[^>]*?\bid="([^"]+)"', re.S)
_CULTURE_DEF_RE = re.compile(r'<Culture\b[^>]*?\bid="([^"]+)"', re.S)
# Party templates are defined as <MBPartyTemplate id="..."> (TAOM + vanilla
# SandBoxCore) — NOT <PartyTemplate>; the container is <partyTemplates>. Match
# both MBPartyTemplate and any legacy PartyTemplate element, never the container.
_PARTYTEMPLATE_DEF_RE = re.compile(r'<(?:MB)?PartyTemplate\b[^>]*?\bid="([^"]+)"', re.S)

# Vanilla culture StringIds that exist at runtime but are produced via XSLT /
# live only in vanilla SandBoxCore (kept as a floor so refs to them never
# false-positive even if the vanilla file can't be read).
VANILLA_CULTURES = {
    "empire", "aserai", "sturgia", "vlandia", "khuzait", "battania", "neutral_culture",
    "looters", "sea_raiders", "mountain_bandits", "forest_bandits",
    "desert_bandits", "steppe_bandits",
}


# Definitions and refs inside XML comments must never enter a registry — a
# commented `<Culture id="...">` example or an `<Item id="...">` placeholder
# would otherwise be accepted as real and mask a typo'd reference
# (Codex review 2026-05-30, HIGH/MED).
_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


def _read_stripped(xml: Path) -> str:
    try:
        text = xml.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""
    return _COMMENT_RE.sub("", text)


def _scan(roots, pattern, want_files=False):
    found = set()
    files = defaultdict(list)
    for root in roots:
        root = Path(root)
        if not root.exists():
            continue
        for xml in root.rglob("*.xml"):
            text = _read_stripped(xml)
            for m in pattern.finditer(text):
                found.add(m.group(1))
                if want_files:
                    files[m.group(1)].append(xml.as_posix())
    return (found, files) if want_files else found


def _scan_files(file_list, pattern):
    """Scan an explicit list of files (not a directory tree). Used for the
    culture registry, which must come ONLY from authoritative culture-definition
    files (taom_spcultures.xml + vanilla spcultures.xml), never from feature
    config files that reuse the <Culture id="..."> shape for other purposes
    (career eligibility groups, per-culture resource configs)."""
    found = set()
    for xml in file_list:
        xml = Path(xml)
        if not xml.exists():
            continue
        for m in pattern.finditer(_read_stripped(xml)):
            found.add(m.group(1))
    return found


def build_registries(moduledata, game_modules, armory_root=None) -> Registries:
    """Build cross-reference registries from the real game install + TAOM repo.

    `game_modules` is .../Mount & Blade II Bannerlord/Modules. Missing roots are
    skipped (reported by the caller, per environment-failures.md)."""
    moduledata = Path(moduledata)
    game_modules = Path(game_modules) if game_modules else None

    item_roots, npc_roots, pt_roots = [], [moduledata], [moduledata]
    if game_modules:
        for name in ("SandBoxCore", "SandBox", "Native", "StoryMode", "CustomBattle",
                     "LOTRLOME_Armory", "Alliance.Wargs", "ADOD_Beasts", "NavalDLC"):
            item_roots.append(game_modules / name / "ModuleData")
        for name in ("SandBoxCore", "SandBox", "Native", "StoryMode", "CustomBattle"):
            npc_roots.append(game_modules / name / "ModuleData")
            pt_roots.append(game_modules / name / "ModuleData")
    item_roots.append(moduledata)

    # Cultures come ONLY from authoritative culture-definition files, never from
    # a directory rglob — feature config files (taom_careers.xml career groups,
    # cc_body_properties.xml, *_resources_config.xml) reuse the <Culture id="...">
    # shape with kingdom ids / placeholders that would pollute the registry and
    # mask invalid culture refs (Codex review 2026-05-30, HIGH).
    culture_files = [moduledata / "taom_spcultures.xml"]
    if game_modules:
        for name in ("SandBoxCore", "SandBox"):
            culture_files.append(game_modules / name / "ModuleData" / "spcultures.xml")

    items, item_def_files = _scan(item_roots, _ITEM_DEF_RE, want_files=True)
    npccharacters = _scan(npc_roots, _NPC_DEF_RE)
    cultures = _scan_files(culture_files, _CULTURE_DEF_RE) | VANILLA_CULTURES
    party_templates = _scan(pt_roots, _PARTYTEMPLATE_DEF_RE)

    # Only flag duplicate item defs inside the LOTRLOME_Armory item folders,
    # where the multi-folder duplicate-id bug actually occurs.
    armory_marker = "LOTRLOME_items"
    armory_dups = {
        iid: [f for f in files if armory_marker in f]
        for iid, files in item_def_files.items()
    }
    armory_dups = {iid: fs for iid, fs in armory_dups.items() if len(set(fs)) > 1}

    if game_modules is None:
        # Without the game install the item / troop / party-template registries
        # are TAOM-only and therefore INCOMPLETE — every reference to a vanilla
        # or Armory entity would false-positive. Mark them unavailable (empty)
        # so the engine's empty-registry guard skips those ref checks entirely;
        # the CLI reports the skip. Culture validity, duplicate-id, civilian-type
        # and enum checks remain reliable from TAOM data + the vanilla-culture
        # floor set, so they still run. (deep-review 2026-05-30)
        items, npccharacters, party_templates, armory_dups = set(), set(), set(), {}

    return Registries(
        items=items, item_def_files=armory_dups,
        npccharacters=npccharacters,
        cultures=cultures, party_templates=party_templates,
    )


# --------------------------------------------------------------------------- #
# Reporting                                                                    #
# --------------------------------------------------------------------------- #
def format_report(issues: list) -> str:
    if not issues:
        return "PASS: no validation issues found."
    by_code = defaultdict(int)
    lines = []
    for i in issues:
        by_code[i.code] += 1
        loc = f"{i.file}:{i.line}" if i.line else i.file
        eid = f" [{i.entry_id}]" if i.entry_id else ""
        lines.append(f"  {i.severity.value:<7} {i.code:<26} {loc}{eid}\n            {i.message}")
    n_err = sum(1 for i in issues if i.severity is Severity.ERROR)
    n_warn = sum(1 for i in issues if i.severity is Severity.WARNING)
    summary = ["", "=== SUMMARY ===", f"  {n_err} error(s), {n_warn} warning(s)"]
    for code, n in sorted(by_code.items(), key=lambda kv: -kv[1]):
        summary.append(f"    {code:<28} {n}")
    return "\n".join(lines + summary)
