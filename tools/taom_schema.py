#!/usr/bin/env python3
"""Schema-driven cross-reference + validation engine for TAOM ModuleData XML.

Schema-driven validation engine —
its `SchemaDefinition`/`CrossReferenceService`/`ValidationService` — into TAOM's
Python tooling world. No external code is vendored; the architecture is
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
import itertools
import json
import os
import re
from collections import defaultdict
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path


# --------------------------------------------------------------------------- #
# Issue model (severity + location + message)                                  #
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
    # Optional (defaulted) so existing constructions stay valid.
    harness_family_types: dict = field(default_factory=dict)  # HorseHarness id -> (family_type|None, def file)
    mount_family_types: dict = field(default_factory=dict)    # Type="Horse" id -> Monster family_type (None = unknown)
    body_properties: set = field(default_factory=set)         # defined BodyProperty ids (face_key_template targets)
    suspect_registries: list = field(default_factory=list)    # human-readable "this registry looks too small" warnings


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
    # face_key_template targets. An undefined one is not an XML error: the engine
    # registers a placeholder, MBObjectManager.UnregisterNonReadyObjects drops it
    # ("Null object reference found with ID: fighter_umbar"), and the character
    # silently loses its authored face. Nothing else cross-checked these.
    RefKind("body_property", re.compile(r'="BodyProperty\.([A-Za-z0-9_.\-]+)"'),
            "body_properties", Severity.ERROR, "BROKEN_BODY_PROPERTY_REF", "body property", "BodyProperty."),
]


def _lineno(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1


# --------------------------------------------------------------------------- #
# Validator                                                                    #
# --------------------------------------------------------------------------- #
class Validator:
    # extra_ref_roots: ModuleData folders of OTHER modules TAOM authors into
    # (LOTRLOME_Armory). They are swept for broken cross-references ONLY -- the
    # schema contracts (duplicate ids, civilian equipmentType, enums) describe
    # TAOM's own files, and applying them to a foreign module would report
    # defects against a repo this validator does not own.
    def __init__(self, moduledata, schemas: list, registries: Registries, extra_ref_roots=None):
        self.moduledata = Path(moduledata)
        self.schemas = schemas
        self.reg = registries
        requested = [Path(r) for r in (extra_ref_roots or [])]
        self.extra_ref_roots = [r for r in requested if r.exists()]
        # A root that silently vanished is the dangerous case: the sweep quietly
        # shrinks back to TAOM-only and the run still prints PASS, which is exactly
        # the under-coverage state this sweep was added to end. Record it so the
        # caller can say so out loud -- never drop it on the floor.
        self.missing_ref_roots = [str(r) for r in requested if not r.exists()]

    # -- public ----------------------------------------------------------- #
    def run(self) -> list:
        issues = []
        issues += self._ref_sweep()
        issues += self._schema_checks()
        issues += self._duplicate_item_defs()
        issues += self._education_coverage()
        issues += self._harness_family_types()
        issues.sort(key=lambda i: i.sort_key())
        return issues

    # -- helpers ---------------------------------------------------------- #
    def _xml_files(self):
        yield from sorted(self.moduledata.rglob("*.xml"))

    def _extra_ref_files(self):
        for root in self.extra_ref_roots:
            yield from sorted(root.rglob("*.xml"))

    def _rel(self, path: Path) -> str:
        try:
            return path.relative_to(self.moduledata).as_posix()
        except ValueError:
            pass
        # Foreign module: prefix with the module name. A bare
        # "LOTRLOME_items/rhun/head_armors.xml" reads as a TAOM path and sends
        # the reader to the wrong repo -- and that file is not even in git.
        for root in self.extra_ref_roots:
            try:
                inner = path.relative_to(root).as_posix()
            except ValueError:
                continue
            # `or root.name` covers a root sitting at a drive root, where
            # pathlib gives the parent an empty name and the label would
            # degrade to a bare "/inner/path" naming no module at all.
            return f"{root.parent.name or root.name}/{inner}"
        return str(path)

    def _schema_for(self, rel: str):
        for s in self.schemas:
            if any(fnmatch.fnmatch(rel, g) for g in s.applies_to):
                return s
        return None

    @staticmethod
    def _read(path: Path) -> str:
        # utf-8-sig, not utf-8: the Armory sweep newly reads files authored by
        # other tools, 2 of which carry a BOM. Per tools/README.md "XML I/O
        # convention". Harmless on non-BOM files; keeps a stray U+FEFF out of
        # the first match for any future anchored pattern.
        return path.read_text(encoding="utf-8-sig", errors="ignore")

    # -- pass 1: cross-reference sweep ------------------------------------ #
    def _ref_sweep(self) -> list:
        issues = []
        active = [k for k in REF_KINDS if getattr(self.reg, k.registry_attr)]
        for path in itertools.chain(self._xml_files(), self._extra_ref_files()):
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

    # -- pass 4: age-8 education template coverage ------------------------- #
    # The v1.4.7 engine resolves child_education_templates_stage_2_page_0_
    # branch_{0-5}_{culture.StringId} at the age-8 education stage and
    # dereferences the result WITHOUT a null guard (EducationCampaignBehavior.
    # GetSpecialCharacterPropertiesForOption). A main culture missing any of the
    # six templates is a guaranteed CTD when a child of that culture turns 8
    # (#354: lothlorien, umbar, goblin, mistymountainorcs all shipped without).
    _EDUCATION_BRANCHES = 6

    def _education_coverage(self) -> list:
        if not self.reg.npccharacters:
            return []  # degraded mode (no game install): registry unavailable
        path = self.moduledata / "taom_spcultures.xml"
        if not path.exists():
            return []
        # Mask comments but keep newlines so line attribution stays accurate.
        raw = self._read(path)
        text = _COMMENT_RE.sub(lambda m: "\n" * m.group(0).count("\n"), raw)
        rel = self._rel(path)
        issues = []
        for m in re.finditer(r"<Culture\b([^>]*?)/?>", text, re.S):
            attrs = m.group(1)
            if not re.search(r'\bis_main_culture="true"', attrs):
                continue
            idm = re.search(r'\bid="([A-Za-z0-9_.\-]+)"', attrs)
            if not idm:
                continue
            cid = idm.group(1)
            missing = [
                b for b in range(self._EDUCATION_BRANCHES)
                if f"child_education_templates_stage_2_page_0_branch_{b}_{cid}"
                not in self.reg.npccharacters
            ]
            if missing:
                issues.append(Issue(
                    severity=Severity.ERROR, code="MISSING_EDUCATION_TEMPLATES",
                    file=rel, line=_lineno(text, m.start()), entry_id=cid,
                    message=(
                        f'main culture "{cid}" is missing stage_2 education character '
                        f'template(s) for branch(es) {", ".join(map(str, missing))} '
                        f'(child_education_templates_stage_2_page_0_branch_N_{cid}) — '
                        f"age-8 child education CTDs on a null tutor template (#354)"
                    ),
                ))
        return issues

    # -- pass 5: horse-harness family_type integrity ----------------------- #
    # A HorseHarness whose <Armor> omits family_type deserializes to FamilyType 0
    # — the HUMAN family (ArmorComponent.cs:153, monsters.xml legend). The v1.4.7
    # inventory screen compares it against the equipped mount's
    # Monster.FamilyType and returns false with NO user-visible message
    # (SPInventoryVM.IsItemEquipmentPossible, :4112), so the harness is silently
    # unequippable on every mount; a harness placed by an equipment-set XML (which
    # bypasses the VM) is force-unequipped on the next inventory transfer (:3923).
    # Shipped once: starter_cavalry_gondor_horse_armor_a, 2026-05-21 -> 2026-07-29.
    # Scanned as two independent sweeps, NOT one alternation: a single finditer
    # pass is non-overlapping, so an outer <EquipmentRoster> match would swallow
    # its nested <EquipmentSet> blocks and they'd never be checked.
    _EQSET_BLOCK_RE = re.compile(r"<EquipmentSet\b[^>]*?>(.*?)</EquipmentSet>", re.S)
    _EQROSTER_BLOCK_RE = re.compile(r"<EquipmentRoster\b[^>]*?>(.*?)</EquipmentRoster>", re.S)
    _EQ_TAG_RE = re.compile(r"<[Ee]quipment\b[^>]*?/?>", re.S)
    _SLOT_ATTR_RE = re.compile(r'\bslot="([^"]+)"')
    _ITEM_REF_ATTR_RE = re.compile(r'\bid="Item\.([A-Za-z0-9_.\-]+)"')

    def _harness_family_types(self) -> list:
        if not self.reg.harness_family_types:
            return []  # degraded mode (no game install): registry unavailable
        issues = []
        for item_id, (family_type, def_file) in sorted(self.reg.harness_family_types.items()):
            if family_type is not None:
                continue
            issues.append(Issue(
                severity=Severity.ERROR, code="MISSING_HARNESS_FAMILY_TYPE",
                file=def_file, line=0, entry_id=item_id,
                message=(
                    f'harness "{item_id}" has no <Armor family_type> — it defaults to 0 '
                    f"(human family), so the inventory screen silently refuses it on every "
                    f"mount; set it to the mount's Monster.family_type (1 for horses)"
                ),
            ))
        return issues + self._harness_pairings()

    def _harness_pairings(self) -> list:
        """Every Horse + HorseHarness pair inside one EquipmentSet (or one troop
        EquipmentRoster) must agree on family type, or the engine strips the
        harness the first time the player touches the inventory."""
        if not self.reg.mount_family_types:
            return []
        issues = []
        for path in self._xml_files():
            raw = self._read(path)
            # Mask comments but keep newlines so line attribution stays accurate.
            text = _COMMENT_RE.sub(lambda m: "\n" * m.group(0).count("\n"), raw)
            rel = self._rel(path)
            blocks = [(m.start(), m.group(1)) for m in self._EQSET_BLOCK_RE.finditer(text)]
            # A roster is paired as a unit only when it holds equipment directly
            # (the troop-roster shape); one that wraps EquipmentSets is covered by
            # the sweep above, and pairing it whole would cross-pair set 1's mount
            # with set 2's harness.
            blocks += [(m.start(), m.group(1)) for m in self._EQROSTER_BLOCK_RE.finditer(text)
                       if "<EquipmentSet" not in m.group(1)]
            for start, body in blocks:
                slots = {}
                for tag in self._EQ_TAG_RE.finditer(body):
                    ms = self._SLOT_ATTR_RE.search(tag.group(0))
                    mi = self._ITEM_REF_ATTR_RE.search(tag.group(0))
                    if ms and mi and ms.group(1) in ("Horse", "HorseHarness"):
                        slots[ms.group(1)] = mi.group(1)
                mount_id, harness_id = slots.get("Horse"), slots.get("HorseHarness")
                if not mount_id or not harness_id:
                    continue
                mount_ft = self.reg.mount_family_types.get(mount_id)
                harness_ft = (self.reg.harness_family_types.get(harness_id) or (None, ""))[0]
                # None on either side = unknown (undefined item, unresolved or
                # ambiguous monster, or a harness already reported above).
                if mount_ft is None or harness_ft is None or mount_ft == harness_ft:
                    continue
                issues.append(Issue(
                    severity=Severity.ERROR, code="HARNESS_FAMILY_MISMATCH",
                    file=rel, line=_lineno(text, start), entry_id=harness_id,
                    message=(
                        f'harness "{harness_id}" (family_type={harness_ft}) is paired with '
                        f'mount "{mount_id}" (family_type={mount_ft}) — the engine silently '
                        f"unequips a mismatched harness on the next inventory transfer"
                    ),
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
# TAOM_bodyproperties.xml puts the id on its OWN line after the element name, so
# this must span newlines (re.S + \b[^>]*?) exactly like the others — an
# id-on-the-same-line pattern registers nothing and turns every lord broken.
_BODYPROP_DEF_RE = re.compile(r'<BodyProperty\b[^>]*?\bid="([^"]+)"', re.S)

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
        # utf-8-sig per tools/README.md "XML I/O convention" — a BOM'd definition
        # file must not leave a stray U+FEFF glued to the first captured id.
        text = xml.read_text(encoding="utf-8-sig", errors="ignore")
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


# --------------------------------------------------------------------------- #
# Weapon-class registry (item id -> skill class)                               #
# --------------------------------------------------------------------------- #
# Vanilla items carry <Item Type="...">; TAOM/Armory weapons are
# <CraftedItem crafting_template="..."> with NO Type attribute. The entire
# install has zero Type="TwoHandedWeapon" items — every two-hander everywhere
# is a CraftedItem — so a class resolver MUST read both attributes.
_ITEM_OPEN_RE = re.compile(r"<(?:Item|CraftedItem)\b[^>]*?>", re.S)
_ATTR_ID_RE = re.compile(r'\bid="([^"]+)"')
_ATTR_TYPE_RE = re.compile(r'\bType="([^"]+)"')
_ATTR_TEMPLATE_RE = re.compile(r'\bcrafting_template="([^"]+)"')

# Measured vocabulary across all item roots (2026-07-13): Type values
# {OneHandedWeapon, Polearm, Bow, Crossbow, Thrown, SlingStones, Arrows, Bolts,
# Shield, + armor/goods types} and crafting_template values {Dagger, Mace,
# OneHandedAxe, OneHandedSword, TwoHandedAxe, TwoHandedMace, TwoHandedSword,
# Pike, TwoHandedPolearm, Javelin, ThrowingAxe, ThrowingKnife}.
WEAPON_CLASS_TO_SKILL = {
    # <Item Type="..."> (vanilla)
    "OneHandedWeapon": "OneHanded",
    "TwoHandedWeapon": "TwoHanded",   # unused in current data; kept for safety
    "Polearm": "Polearm",
    "Bow": "Bow",
    "Crossbow": "Crossbow",
    "Thrown": "Throwing",
    "SlingStones": "Throwing",
    "Arrows": "Arrows",
    "Bolts": "Bolts",
    "Shield": "Shield",
    # <CraftedItem crafting_template="..."> (TAOM / Armory / vanilla crafted)
    "Dagger": "OneHanded",
    "Mace": "OneHanded",
    "OneHandedAxe": "OneHanded",
    "OneHandedSword": "OneHanded",
    "TwoHandedAxe": "TwoHanded",
    "TwoHandedMace": "TwoHanded",
    "TwoHandedSword": "TwoHanded",
    "Pike": "Polearm",
    "TwoHandedPolearm": "Polearm",
    "Javelin": "Throwing",
    "ThrowingAxe": "Throwing",
    "ThrowingKnife": "Throwing",
}


def build_item_class_registry(moduledata, game_modules) -> dict:
    """Map item id -> skill class ('OneHanded'/'TwoHanded'/'Polearm'/'Bow'/
    'Crossbow'/'Throwing'/'Arrows'/'Bolts'/'Shield') for every weapon-classed
    item across the same item roots as build_registries. Non-weapon items
    (armor, horses, goods) are omitted."""
    moduledata = Path(moduledata)
    game_modules = Path(game_modules) if game_modules else None

    item_roots = []
    if game_modules:
        for name in ("SandBoxCore", "SandBox", "Native", "StoryMode", "CustomBattle",
                     "LOTRLOME_Armory", "Alliance.Wargs", "ADOD_Beasts", "NavalDLC"):
            item_roots.append(game_modules / name / "ModuleData")
    item_roots.append(moduledata)

    classes = {}
    for root in item_roots:
        if not root.exists():
            continue
        for xml in root.rglob("*.xml"):
            text = _read_stripped(xml)
            for m in _ITEM_OPEN_RE.finditer(text):
                tag = m.group(0)
                mid = _ATTR_ID_RE.search(tag)
                if not mid:
                    continue
                mtype = _ATTR_TYPE_RE.search(tag)
                mtmpl = _ATTR_TEMPLATE_RE.search(tag)
                raw = (mtype and mtype.group(1)) or (mtmpl and mtmpl.group(1))
                skill = WEAPON_CLASS_TO_SKILL.get(raw) if raw else None
                if skill:
                    classes[mid.group(1)] = skill
    return classes


# --------------------------------------------------------------------------- #
# Harness / mount family-type registries                                       #
# --------------------------------------------------------------------------- #
# Mount-side family type comes ONLY from the monsters XML: HorseComponent.
# Deserialize (v1.4.7) never reads family_type off the <Horse> element, so the
# family_type attributes written on <Horse> in LOTRAOM_horses.xml are dead data —
# only Monster.<id> family_type (with base_monster inheritance) is authoritative.
_ITEM_OPEN_ONLY_RE = re.compile(r"<Item\b[^>]*?>")
_MONSTER_OPEN_RE = re.compile(r"<Monster\b[^>]*?>")
_ARMOR_OPEN_RE = re.compile(r"<Armor\b[^>]*?>")
_HORSE_OPEN_RE = re.compile(r"<Horse\b[^>]*?>")
_ATTR_FAMILY_TYPE_RE = re.compile(r'\bfamily_type="([^"]+)"')
_ATTR_MONSTER_RE = re.compile(r'\bmonster="(?:Monster\.)?([A-Za-z0-9_.\-]+)"')
_ATTR_BASE_MONSTER_RE = re.compile(r'\bbase_monster="(?:Monster\.)?([A-Za-z0-9_.\-]+)"')


def _as_int(text):
    try:
        return int(text)
    except (TypeError, ValueError):
        return None


def _monster_family_type(mid, decls, memo, seen=()):
    """Resolve a monster's family_type, following base_monster. Returns None when
    unknown OR when competing declarations disagree (ADOD_Beasts redeclares ids
    Native/LOTRLOME also define, and engine resolution is load-order dependent —
    guessing would emit false mismatches)."""
    if mid in memo:
        return memo[mid]
    if mid in seen or mid not in decls:
        return None
    values = set()
    for family_type, base in decls[mid]:
        if family_type is not None:
            values.add(family_type)
        elif base:
            inherited = _monster_family_type(base, decls, memo, seen + (mid,))
            if inherited is not None:
                values.add(inherited)
    result = values.pop() if len(values) == 1 else None
    memo[mid] = result
    return result


def build_harness_registries(item_roots) -> tuple:
    """(harness id -> (family_type|None, def file), mount item id -> family_type|None)

    A harness with NO family_type registers as None rather than being omitted —
    that absence is the bug being detected (v1.4.7 defaults it to 0 = human)."""
    monster_decls = defaultdict(list)
    harness, mount_monster = {}, {}
    for root in item_roots:
        root = Path(root)
        if not root.exists():
            continue
        for xml in sorted(root.rglob("*.xml")):
            text = _read_stripped(xml)
            if "<Monster" in text:
                for m in _MONSTER_OPEN_RE.finditer(text):
                    tag = m.group(0)
                    mid = _ATTR_ID_RE.search(tag)
                    if not mid:
                        continue
                    mft = _ATTR_FAMILY_TYPE_RE.search(tag)
                    mbase = _ATTR_BASE_MONSTER_RE.search(tag)
                    monster_decls[mid.group(1)].append((
                        _as_int(mft.group(1)) if mft else None,
                        mbase.group(1) if mbase else None,
                    ))
            if 'Type="HorseHarness"' not in text and 'Type="Horse"' not in text:
                continue
            # Walk <Item> opens rather than regexing <Item>...</Item> blocks: a
            # self-closing <Item ... /> would otherwise swallow a later item's body.
            opens = list(_ITEM_OPEN_ONLY_RE.finditer(text))
            for i, m in enumerate(opens):
                tag = m.group(0)
                if tag.rstrip().endswith("/>"):
                    continue
                mid, mtype = _ATTR_ID_RE.search(tag), _ATTR_TYPE_RE.search(tag)
                if not mid or not mtype or mtype.group(1) not in ("HorseHarness", "Horse"):
                    continue
                limit = opens[i + 1].start() if i + 1 < len(opens) else len(text)
                close = text.find("</Item>", m.end())
                body = text[m.end():min(limit, close if close != -1 else limit)]
                if mtype.group(1) == "HorseHarness":
                    armor = _ARMOR_OPEN_RE.search(body)
                    mft = _ATTR_FAMILY_TYPE_RE.search(armor.group(0)) if armor else None
                    harness.setdefault(mid.group(1),
                                       (_as_int(mft.group(1)) if mft else None, xml.as_posix()))
                else:
                    horse = _HORSE_OPEN_RE.search(body)
                    mmonster = _ATTR_MONSTER_RE.search(horse.group(0)) if horse else None
                    if mmonster:
                        mount_monster.setdefault(mid.group(1), mmonster.group(1))

    memo = {}
    mounts = {item_id: _monster_family_type(monster_id, monster_decls, memo)
              for item_id, monster_id in mount_monster.items()}
    return harness, mounts


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

    # Body properties come from the *_bodyproperties.xml files only, for the same
    # reason cultures do: a directory rglob would sweep character-creation and
    # feature configs that reuse the <BodyProperty> shape for non-referenceable
    # entries, polluting the registry and masking the broken refs we want caught.
    bodyprop_files = [moduledata / "TAOM_bodyproperties.xml"]
    if game_modules:
        for name, fname in (("SandBoxCore", "sandboxcore_bodyproperties.xml"),
                            ("SandBox", "sandbox_bodyproperties.xml"),
                            ("NavalDLC", "naval_bodyproperties.xml")):
            bodyprop_files.append(game_modules / name / "ModuleData" / fname)

    items, item_def_files = _scan(item_roots, _ITEM_DEF_RE, want_files=True)
    npccharacters = _scan(npc_roots, _NPC_DEF_RE)
    cultures = _scan_files(culture_files, _CULTURE_DEF_RE) | VANILLA_CULTURES
    party_templates = _scan(pt_roots, _PARTYTEMPLATE_DEF_RE)
    body_properties = _scan_files(bodyprop_files, _BODYPROP_DEF_RE)

    # Only flag duplicate item defs inside the LOTRLOME_Armory item folders,
    # where the multi-folder duplicate-id bug actually occurs.
    armory_marker = "LOTRLOME_items"
    armory_dups = {
        iid: [f for f in files if armory_marker in f]
        for iid, files in item_def_files.items()
    }
    armory_dups = {iid: fs for iid, fs in armory_dups.items() if len(set(fs)) > 1}

    harness_family_types, mount_family_types = build_harness_registries(item_roots)

    if game_modules is None:
        # Without the game install the item / troop / party-template registries
        # are TAOM-only and therefore INCOMPLETE — every reference to a vanilla
        # or Armory entity would false-positive. Mark them unavailable (empty)
        # so the engine's empty-registry guard skips those ref checks entirely;
        # the CLI reports the skip. Culture validity, duplicate-id, civilian-type
        # and enum checks remain reliable from TAOM data + the vanilla-culture
        # floor set, so they still run. (deep-review 2026-05-30)
        # The harness/mount family-type registries need the Armory + monsters XML
        # from the install, so they are unavailable too.
        items, npccharacters, party_templates, armory_dups = set(), set(), set(), {}
        harness_family_types, mount_family_types = {}, {}
        # TAOM's 30 body properties are only a quarter of the 121 defined; the
        # rest are vanilla, and TAOM characters reference them freely.
        body_properties = set()

    # Size floors. A registry built from an explicit file list shrinks silently when
    # a source file is renamed (a game patch, a typo'd --game-modules that still
    # points at a real directory). Full shrinkage trips the empty-registry guard and
    # SKIPS the check -- a clean-looking PASS; partial shrinkage floods false
    # positives. Neither is distinguishable from a healthy run without a floor, so
    # say so rather than letting the number speak for itself. Floors are deliberately
    # far below today's real counts (121 body properties, 38 cultures) -- this catches
    # "the file list broke", not "the data changed a bit".
    suspect = []
    if game_modules:
        for label, value, floor in (("body_properties", body_properties, 50),
                                    ("cultures", cultures, 20)):
            if len(value) < floor:
                suspect.append(f"{label} registry has only {len(value)} entries "
                               f"(expected >={floor}) - a source path may be wrong")

    return Registries(
        items=items, item_def_files=armory_dups,
        npccharacters=npccharacters,
        cultures=cultures, party_templates=party_templates,
        harness_family_types=harness_family_types,
        mount_family_types=mount_family_types,
        body_properties=body_properties,
        suspect_registries=suspect,
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
