#!/usr/bin/env python3
"""Hold the cover and visibility attributes of the live Armory's armour in a repo spec, and re-apply them.

WHY THIS EXISTS

LOTRLOME_Armory is not in this repo. Mike corrects <Armor> attributes in it by hand (the beard shown under a helmet,
a crown that must show all hair and beard, gloves and boots that hide the bare skin), and a reinstall, a sync from the
mirror or an art drop puts the old values back. A ledger alone was redone by hand and forgotten
(docs/reference/lotrlome-beard-cover-changes.md). The spec, tools/armour_cover_flags.json, holds two layers:

  rules     a reason and a date each, selecting items by id, by Item Type or by mesh. A rule wins.
  snapshot  the value of every covered attribute of every armour item on the day it was taken, so a reset shows as
            drift. An attribute the item lacks is recorded by leaving it out ("absent").

The covered attributes are the ones ArmorComponent.Deserialize reads to decide what the game shows or hides (see
ATTRS). An absent attribute means the engine default, so absent and the default value compare equal, and an item
that lacks an attribute does not get one unless a rule sets it to something else.

    python tools/apply_armour_cover_flags.py                          # dry run: every change it would make
    python tools/apply_armour_cover_flags.py --apply --backup-tag T   # write; one .bak-T copy per file, first write only
    python tools/apply_armour_cover_flags.py --check                  # exit 1 on drift or a missing id, 2 if it cannot run
    python tools/apply_armour_cover_flags.py --snapshot --expect N    # rewrite the spec's snapshot from the live Armory

--snapshot writes only the spec file, never the Armory. It first lists every value it would change against the old
snapshot (SNAPSHOT-CHANGE, SNAPSHOT-ADDED, SNAPSHOT-REMOVED) and writes only when the total equals --expect (none
needed for 0). Run --check first and correct the Armory with --apply when it reports drift: after an Armory
reinstall or a mirror sync, a snapshot taken before --apply would record the reset as the truth.
--armory-path names a LOTRLOME_Armory folder (tests use a temp one); --spec names another spec file.

Edits are byte-faithful (tools/README.md XML I/O convention): BOM and line endings kept, only the attribute token
inside the item's own <Armor> tag changes, an added attribute reuses the tag's own line layout, every output is parsed
before any file is written.

Every covered attribute of every armour item is checked against the values the engine reads (an INVALID line), new
items included; --apply refuses to write while one exists.

Exit: 0 clean; 1 drift, a missing id, a file that does not parse, a value the engine would reject, an unusable spec,
two rules that disagree, or a snapshot refused; 2 the Armory is not installed or holds no armour (a check that cannot
run is not a pass; tools/data_gates.py reads 2 as NOT RUN).
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
DEFAULT_SPEC = REPO / "tools" / "armour_cover_flags.json"
BOM = b"\xef\xbb\xbf"
BOM_CHAR = "\ufeff"   # the same mark once the bytes are decoded


@dataclass(frozen=True)
class Attr:
    values: tuple          # every value the engine understands, lower case
    default: str           # what an absent attribute means
    exact: tuple = ()      # only these exact strings do anything; any other text means the default


_COVER = ("none", "type1", "type2", "type3", "type4", "all")   # HairCoverTypes, BeardCoverTypes (ArmorComponent.cs)
_BOOL = ("true", "false")

# Every <Armor> attribute that decides what the game shows or hides, from ArmorComponent.Deserialize (v1.5.7).
# Left out on purpose: armour stats, weight, material_type and modifier_group (balance, not looks), reins_mesh (a mesh
# reference, checked by audit_armory_refs.py) and has_gender_variations (a property of the mesh asset, so an art drop
# has to be able to change it with the mesh).
ATTRS = {
    "hair_cover_type": Attr(_COVER, "none"),        # which hair the helmet hides: none shows all, all hides all
    "beard_cover_type": Attr(_COVER, "none"),       # the same for the beard
    "mane_cover_type": Attr(("none", "type1", "type2", "all"), "none"),   # horse harness: hides the mane
    "tail_cover_type": Attr(("none", "all"), "none"),   # read and stored; no consumer found in the dump
    "covers_head": Attr(_BOOL, "false"),            # hides the bare head skin; also switches off facegen head scaling
    "covers_body": Attr(_BOOL, "false"),            # hides the bare torso skin
    "covers_hands": Attr(_BOOL, "false"),           # hides the bare hand skin
    "covers_legs": Attr(_BOOL, "false"),            # hides the bare leg skin of the whole outfit
    "no_slim": Attr(_BOOL, "false"),                # read; no consumer found
    "body_mesh_type": Attr(("normal", "upperbody", "shoulders"), "normal", exact=("upperbody", "shoulders")),
    "body_deform_type": Attr(("medium", "large", "skinny"), "medium", exact=("large", "skinny")),
}
SELECT_KEYS = ("ids", "type", "mesh", "not_mesh")
DATE = re.compile(r"\d{4}-\d{2}-\d{2}")
TAG = re.compile(r"[A-Za-z0-9][A-Za-z0-9_-]*")


class SpecError(Exception):
    pass


def effective(name: str, raw: str | None) -> str:
    """What the engine reads from an attribute value: parsed ignoring case, the default when absent."""
    attr = ATTRS[name]
    if raw is None:
        return attr.default
    if attr.exact:
        return raw if raw in attr.exact else attr.default
    return raw.strip().lower()


@dataclass
class Spec:
    comment: list
    rules: list     # dicts: date, reason, select, set
    taken: str
    files: dict     # ModuleData-relative path -> {item id: {attribute: value}}


def _check_values(where: str, values) -> dict:
    if not isinstance(values, dict):
        raise SpecError(f"{where}: expected an object of attribute values")
    for name, value in values.items():
        if name not in ATTRS:
            raise SpecError(f"{where}: {name!r} is not a covered attribute ({', '.join(ATTRS)})")
        if value not in ATTRS[name].values:
            raise SpecError(f"{where}: {name}={value!r} is not a value the engine reads "
                            f"(allowed: {', '.join(ATTRS[name].values)})")
    return values


def _check_rule(index: int, rule) -> dict:
    where = f"rule {index}"
    if not isinstance(rule, dict):
        raise SpecError(f"{where}: expected an object")
    if not str(rule.get("reason", "")).strip():
        raise SpecError(f"{where}: a rule needs a reason")
    if not DATE.fullmatch(str(rule.get("date", ""))):
        raise SpecError(f"{where}: a rule needs a date as YYYY-MM-DD")
    select = rule.get("select")
    if not isinstance(select, dict) or not select or set(select) - set(SELECT_KEYS):
        raise SpecError(f"{where}: select needs one or more of {', '.join(SELECT_KEYS)}")
    for key in ("ids", "mesh", "not_mesh"):
        if key in select:
            names = select[key]
            if not isinstance(names, list) or not names or not all(isinstance(n, str) and n for n in names):
                raise SpecError(f"{where}: select.{key} needs a non-empty list of names")
            if len(set(names)) != len(names):
                raise SpecError(f"{where}: select.{key} lists a name twice")
    if "type" in select and not (isinstance(select["type"], str) and select["type"]):
        raise SpecError(f"{where}: select.type needs an item type")
    if not rule.get("set"):
        raise SpecError(f"{where}: a rule needs a non-empty set")
    _check_values(f"{where} set", rule["set"])
    return rule


def load_spec(path) -> Spec:
    try:
        data = json.loads(Path(path).read_bytes().decode("utf-8-sig"))
    except (OSError, ValueError) as exc:
        raise SpecError(f"{path}: cannot read ({exc})")
    if not isinstance(data, dict) or not isinstance(data.get("rules"), list) or not isinstance(data.get("snapshot"), dict):
        raise SpecError(f"{path}: expected an object with rules and snapshot")
    rules = [_check_rule(i, r) for i, r in enumerate(data["rules"])]
    snap = data["snapshot"]
    taken = str(snap.get("taken", ""))
    if not DATE.fullmatch(taken) or not isinstance(snap.get("files"), dict):
        raise SpecError(f"{path}: snapshot needs taken (YYYY-MM-DD) and files")
    seen: dict = {}
    for rel, items in snap["files"].items():
        if not isinstance(items, dict):
            raise SpecError(f"snapshot {rel}: expected an object of items")
        for item_id, values in items.items():
            if item_id in seen:
                raise SpecError(f"snapshot: {item_id} is in both {seen[item_id]} and {rel}")
            seen[item_id] = rel
            _check_values(f"snapshot {rel} {item_id}", values)
    return Spec(list(data.get("_comment", [])), rules, taken, snap["files"])


def _ordered(values: dict) -> dict:
    return {name: values[name] for name in ATTRS if name in values}


def dump_spec(spec: Spec) -> str:
    """The canonical text: rules as indented JSON, the snapshot one item per line, sorted by file and id."""
    rules = [{"date": r["date"], "reason": r["reason"],
              "select": {k: r["select"][k] for k in SELECT_KEYS if k in r["select"]},
              "set": _ordered(r["set"])} for r in spec.rules]

    def block(value) -> str:
        return json.dumps(value, indent=2, ensure_ascii=False).replace("\n", "\n  ")

    lines = ["{", f'  "_comment": {block(spec.comment)},', f'  "rules": {block(rules)},',
             '  "snapshot": {', f'    "taken": {json.dumps(spec.taken)},']
    if not spec.files:
        lines.append('    "files": {}')
    else:
        lines.append('    "files": {')
        rels = sorted(spec.files)
        for i, rel in enumerate(rels):
            lines.append(f"      {json.dumps(rel)}: {{")
            ids = sorted(spec.files[rel])
            for j, item_id in enumerate(ids):
                comma = "," if j < len(ids) - 1 else ""
                lines.append(f"        {json.dumps(item_id)}: "
                             f"{json.dumps(_ordered(spec.files[rel][item_id]), ensure_ascii=False)}{comma}")
            lines.append("      }" + ("," if i < len(rels) - 1 else ""))
        lines.append("    }")
    lines += ["  }", "}"]
    return "\n".join(lines) + "\n"


# --- reading the Armory ---------------------------------------------------------------------------------------------

_TAG_TAIL = r"""(?:[^>"']|"[^"]*"|'[^']*')*>"""   # up to the closing >, skipping quoted values that hold a >
ITEM_OPEN = re.compile(r"<Item\b" + _TAG_TAIL)
ARMOR_OPEN = re.compile(r"<Armor\b" + _TAG_TAIL)
ATTR = re.compile(r"""([A-Za-z_][\w:.-]*)(\s*=\s*)("[^"]*"|'[^']*')""")
COMMENT = re.compile(r"<!--.*?-->", re.S)


@dataclass
class Entry:
    rel: str
    item_id: str
    item_type: str
    mesh: str
    attrs: dict     # covered attribute -> the value as written
    start: int      # span of the <Armor ...> tag in the file text
    end: int


@dataclass
class ArmoryFile:
    path: Path
    rel: str
    raw: bytes
    text: str
    entries: list


def scan_text(rel: str, text: str) -> list:
    """Every Item that has an <Armor> tag, located by token. A commented-out item is skipped; the text is
    never reparsed and rewritten, so offsets in `text` stay valid for the byte-faithful edit."""
    masked = COMMENT.sub(lambda m: " " * len(m.group()), text)
    entries = []
    for item in ITEM_OPEN.finditer(masked):
        if item.group().rstrip().endswith("/>"):
            continue
        close = masked.find("</Item>", item.end())
        armor = ARMOR_OPEN.search(masked, item.end(), close) if close >= 0 else None
        if armor is None:
            continue
        head = {m.group(1): m.group(3)[1:-1] for m in ATTR.finditer(item.group())}
        attrs = {m.group(1): m.group(3)[1:-1] for m in ATTR.finditer(armor.group()) if m.group(1) in ATTRS}
        entries.append(Entry(rel, head.get("id", ""), head.get("Type", ""), head.get("mesh", ""), attrs,
                             armor.start(), armor.end()))
    return entries


def scan_armory(module_data: Path) -> tuple:
    """(files that hold armour, error lines). Every *.xml under ModuleData except Languages, so an item defined in
    a file other than head_armors.xml is found; a backup (.xml.bak-...) is not an .xml file and is not read."""
    files, errors = [], []
    for path in sorted(module_data.rglob("*.xml")):
        rel = path.relative_to(module_data).as_posix()
        if rel.startswith("Languages/"):
            continue
        raw = path.read_bytes()
        if b"<Armor" not in raw:
            continue
        try:
            text = raw.decode("utf-8")
            ET.fromstring(raw[len(BOM):] if raw.startswith(BOM) else raw)
        except (UnicodeDecodeError, ET.ParseError) as exc:
            errors.append(f"ERROR {rel} does not parse ({exc})")
            continue
        entries = scan_text(rel, text)
        if entries:
            files.append(ArmoryFile(path, rel, raw, text, entries))
    return files, errors


def invalid_values(files: list) -> list:
    """(rel, id, attr, raw) for each covered attribute the engine would reject: ArmorComponent.Deserialize parses
    the cover types with Enum.Parse, so an unknown name throws, a new item included."""
    return [(entry.rel, entry.item_id, attr, raw) for file in files for entry in file.entries
            for attr, raw in entry.attrs.items() if effective(attr, raw) not in ATTRS[attr].values]


# --- deciding what each attribute should be ---------------------------------------------------------------------------

class Conflict(Exception):
    pass


def _matches(select: dict, entry: Entry) -> bool:
    return (("ids" not in select or entry.item_id in select["ids"])
            and ("type" not in select or entry.item_type == select["type"])
            and ("mesh" not in select or entry.mesh in select["mesh"])
            and ("not_mesh" not in select or entry.mesh not in select["not_mesh"]))


def _why(rule: dict) -> str:
    return f"rule {rule['date']}: {rule['reason'].split('. ')[0][:70]}"


@dataclass
class Change:
    file: ArmoryFile
    entry: Entry
    attr: str
    old: str | None     # as written, None when the item lacks it
    want: str
    why: str


@dataclass
class Evaluation:
    changes: list
    already: list       # (rel, id, attr, value) held by an id-list rule
    rule_lines: list    # one summary line per rule
    missing: dict       # id -> where the spec names it
    new_ids: list       # armour items the snapshot does not know
    invalid: list = field(default_factory=list)    # (rel, id, attr, raw) the engine would reject


def evaluate(spec: Spec, files: list) -> Evaluation:
    snapshot = {item_id: values for items in spec.files.values() for item_id, values in items.items()}
    rule_stats = [[0, 0, 0] for _ in spec.rules]    # items matched, attributes changed, attributes already set
    changes, already, new_ids, seen = [], [], [], set()
    for file in files:
        for entry in file.entries:
            seen.add(entry.item_id)
            if entry.item_id not in snapshot:
                new_ids.append(entry.item_id)
            wanted: dict = {}       # attr -> (value, why)
            for index, rule in enumerate(spec.rules):
                if not _matches(rule["select"], entry):
                    continue
                rule_stats[index][0] += 1
                for attr, value in rule["set"].items():
                    if attr in wanted and wanted[attr][0] != value:
                        raise Conflict(f"{entry.item_id}: {attr} is {wanted[attr][0]} by {wanted[attr][1]} "
                                       f"but {value} by {_why(rule)}")
                    wanted[attr] = (value, _why(rule), index)
            for attr in ATTRS:
                if attr in wanted:
                    value, why, index = wanted[attr]
                elif entry.item_id in snapshot:
                    value, why, index = snapshot[entry.item_id].get(attr, ATTRS[attr].default), "snapshot", None
                else:
                    continue
                if effective(attr, entry.attrs.get(attr)) != value:
                    changes.append(Change(file, entry, attr, entry.attrs.get(attr), value, why))
                    if index is not None:
                        rule_stats[index][1] += 1
                elif index is not None:
                    rule_stats[index][2] += 1
                    if "ids" in spec.rules[index]["select"]:
                        already.append((entry.rel, entry.item_id, attr, value))
    missing: dict = {}
    for rule in spec.rules:
        for item_id in rule["select"].get("ids", []):
            if item_id not in seen:
                missing.setdefault(item_id, []).append(_why(rule))
    for rel, items in spec.files.items():
        for item_id in items:
            if item_id not in seen:
                missing.setdefault(item_id, []).append(f"snapshot of {rel}")
    lines = [f"{_why(rule)} ({', '.join(f'{k}={v}' for k, v in rule['set'].items())}): {s[0]} item(s), "
             f"{s[1]} to change, {s[2]} already set" for rule, s in zip(spec.rules, rule_stats)]
    return Evaluation(changes, already, lines, missing, new_ids, invalid_values(files))


# --- editing ----------------------------------------------------------------------------------------------------------

def edit_tag(tag: str, wanted: dict) -> str:
    """The <Armor> tag with each attribute in `wanted` set: an existing value is replaced in place, a missing
    attribute is added after the last one, behind the same gap the last one has (a newline and indent, or a space)."""
    found = list(ATTR.finditer(tag))
    by_name = {m.group(1): m for m in found}
    edits = []
    added = [(name, value) for name, value in wanted.items() if name not in by_name]
    for name, value in wanted.items():
        if name in by_name:
            edits.append((by_name[name].start(3) + 1, by_name[name].end(3) - 1, value))
    if added:
        last = found[-1] if found else None
        before = found[-2].end() if len(found) > 1 else len("<Armor")
        gap = tag[before:last.start()] if last else " "
        sep = gap if ("\n" in gap or "\r" in gap) else " "
        at = last.end() if last else len("<Armor")
        edits.append((at, at, "".join(f'{sep}{name}="{value}"' for name, value in added)))
    for start, end, text in sorted(edits, reverse=True):
        tag = tag[:start] + text + tag[end:]
    return tag


def plan_file(file: ArmoryFile, changes: list) -> str:
    """The new text of one file; raises ValueError unless it parses and holds every wanted value."""
    by_entry: dict = {}
    for change in changes:
        by_entry.setdefault(id(change.entry), (change.entry, {}))[1][change.attr] = change.want
    text = file.text
    for entry, wanted in sorted(by_entry.values(), key=lambda pair: pair[0].start, reverse=True):
        text = text[:entry.start] + edit_tag(text[entry.start:entry.end], wanted) + text[entry.end:]
    try:
        ET.fromstring(text.encode("utf-8")[len(BOM):] if text.startswith(BOM_CHAR) else text.encode("utf-8"))
    except ET.ParseError as exc:
        raise ValueError(f"{file.rel}: the edited file does not parse ({exc})")
    after = {e.item_id: e for e in scan_text(file.rel, text)}
    for change in changes:
        got = effective(change.attr, after[change.entry.item_id].attrs.get(change.attr))
        if got != change.want:
            raise ValueError(f"{file.rel}: {change.entry.item_id} {change.attr} reads {got} after the edit, want {change.want}")
    return text


def write_files(plans: list, tag: str) -> None:
    for file, text in plans:
        backup = file.path.with_name(file.path.name + ".bak-" + tag)
        if not backup.exists():
            backup.write_bytes(file.raw)
        file.path.write_bytes(text.encode("utf-8"))
        print(f"written  {file.rel} (backup {backup.name})")


# --- command line -----------------------------------------------------------------------------------------------------

def _show(value) -> str:
    return "absent" if value is None else value


def _invalid_lines(invalid: list) -> list:
    return [f'INVALID {rel} {item_id} {attr}="{raw}" (the engine reads: {", ".join(ATTRS[attr].values)})'
            for rel, item_id, attr, raw in invalid]


def _report(evaluation: Evaluation, label: str) -> None:
    for change in evaluation.changes:
        if label == "DRIFT":
            print(f"DRIFT {change.entry.rel} {change.entry.item_id} {change.attr} live={_show(change.old)} "
                  f"want={change.want} [{change.why}]")
        else:
            print(f"CHANGE {change.entry.rel} {change.entry.item_id} {change.attr} {_show(change.old)} -> "
                  f"{change.want} [{change.why}]")
    for line in _invalid_lines(evaluation.invalid):
        print(line)
    for item_id, where in sorted(evaluation.missing.items()):
        print(f"MISSING {item_id} (named by {'; '.join(where)})")
    if evaluation.new_ids:
        sample = ", ".join(evaluation.new_ids[:10]) + (" ..." if len(evaluation.new_ids) > 10 else "")
        print(f"WARNING {len(evaluation.new_ids)} armour item(s) are not in the snapshot (an art drop?); rules still "
              f"apply to them, run --snapshot to hold their values: {sample}")


def take_snapshot(files: list) -> tuple:
    """(snapshot files, errors): each present attribute as the engine reads it; an absent one is left out. The
    errors include every INVALID line, so no snapshot is taken while a value the engine rejects exists."""
    out, errors, seen = {}, [], {}
    for file in files:
        for entry in file.entries:
            if entry.item_id in seen:
                errors.append(f"ERROR {entry.item_id} is defined in {seen[entry.item_id]} and {file.rel}")
                continue
            seen[entry.item_id] = file.rel
            out.setdefault(file.rel, {})[entry.item_id] = {attr: effective(attr, raw) for attr, raw in entry.attrs.items()}
    return out, errors + _invalid_lines(invalid_values(files))


def snapshot_diff(old: dict, new: dict) -> list:
    """One line per difference between two snapshot file maps: a changed attribute, a moved, added or removed item."""
    def flat(files):
        return {item_id: (rel, values) for rel, items in files.items() for item_id, values in items.items()}
    before, after = flat(old), flat(new)
    lines = [f"SNAPSHOT-ADDED {after[i][0]} {i}" for i in sorted(after.keys() - before.keys())]
    lines += [f"SNAPSHOT-REMOVED {before[i][0]} {i}" for i in sorted(before.keys() - after.keys())]
    for item_id in sorted(before.keys() & after.keys()):
        (old_rel, old_values), (rel, values) = before[item_id], after[item_id]
        if old_rel != rel:
            lines.append(f"SNAPSHOT-CHANGE {rel} {item_id} file {old_rel} -> {rel}")
        lines += [f"SNAPSHOT-CHANGE {rel} {item_id} {attr} {_show(old_values.get(attr))} -> {_show(values.get(attr))}"
                  for attr in ATTRS if old_values.get(attr) != values.get(attr)]
    return lines


def main(argv=None) -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the changes (needs --backup-tag)")
    mode.add_argument("--check", action="store_true", help="exit 1 on drift or a missing id, 2 if it cannot run")
    mode.add_argument("--snapshot", action="store_true", help="rewrite the spec's snapshot from the live Armory")
    ap.add_argument("--expect", type=int, help="with --snapshot: the number of differences from the old snapshot you "
                                               "read and accept; the write is refused unless it matches")
    ap.add_argument("--backup-tag", help="suffix of the one-time backup of each changed file: <file>.xml.bak-<tag>")
    ap.add_argument("--armory-path", help="the LOTRLOME_Armory folder (default: the installed module)")
    ap.add_argument("--spec", default=str(DEFAULT_SPEC), help="the spec file (default: tools/armour_cover_flags.json)")
    args = ap.parse_args(argv)
    if args.expect is not None and not args.snapshot:
        ap.error("--expect belongs to --snapshot")
    if args.apply and not (args.backup_tag and TAG.fullmatch(args.backup_tag)):
        ap.error("--apply needs --backup-tag made of letters, digits, - and _ (the backup must not end in .xml)")

    armory = Path(args.armory_path) if args.armory_path else game_modules(DEFAULT_GAME_ROOT) / "LOTRLOME_Armory"
    module_data = armory / "ModuleData"
    if not module_data.is_dir():
        print(f"ERROR: SKIPPED, LOTRLOME_Armory is not installed at {armory}; nothing was checked")
        return 2
    try:
        spec = load_spec(args.spec)
    except SpecError as exc:
        print(f"ERROR: unusable spec: {exc}")
        return 1
    files, errors = scan_armory(module_data)
    for line in errors:
        print(line)
    if errors:
        return 1
    if not files:
        print(f"ERROR: SKIPPED, no armour item found under {module_data}; nothing was checked")
        return 2

    if args.snapshot:
        taken, problems = take_snapshot(files)
        for line in problems:
            print(line)
        if problems:
            return 1
        differences = snapshot_diff(spec.files, taken)
        for line in differences:
            print(line)
        print(f"snapshot: {len(differences)} difference(s) from the old snapshot")
        if len(differences) != (args.expect or 0):
            print(f"ERROR: refused, nothing written. Read every line above; run --check first and correct the Armory "
                  f"with --apply if a value is a reset, never snapshot a reset. If every line is meant, run again "
                  f"with --expect {len(differences)}.")
            return 1
        Path(args.spec).write_bytes(dump_spec(Spec(spec.comment, spec.rules, dt.date.today().isoformat(), taken)).encode("utf-8"))
        count = sum(len(items) for items in taken.values())
        held = {a: sum(1 for items in taken.values() for v in items.values() if a in v) for a in ATTRS}
        print(f"snapshot: {count} item(s) in {len(taken)} file(s) written to {args.spec}")
        print("  items carrying each attribute: " + ", ".join(f"{a} {n}" for a, n in held.items() if n))
        return 0

    try:
        evaluation = evaluate(spec, files)
    except Conflict as exc:
        print(f"ERROR: two rules disagree: {exc}")
        return 1
    if args.check:
        _report(evaluation, "DRIFT")
        bad = len(evaluation.changes) + len(evaluation.missing) + len(evaluation.invalid)
        print(f"check: {len(evaluation.changes)} attribute(s) differ from the spec, {len(evaluation.missing)} id(s) "
              f"missing from the Armory, {len(evaluation.invalid)} value(s) the engine rejects"
              + ("" if bad else "; all match"))
        return 1 if bad else 0

    _report(evaluation, "CHANGE")
    for rel, item_id, attr, value in evaluation.already:
        print(f"already set {rel} {item_id} {attr}={value}")
    for line in evaluation.rule_lines:
        print(line)
    by_file: dict = {}
    for change in evaluation.changes:
        by_file.setdefault(change.file.rel, (change.file, []))[1].append(change)
    print(f"{len(evaluation.changes)} attribute(s) to change in {len(by_file)} file(s)")
    if evaluation.invalid:
        print(f"ERROR: {len(evaluation.invalid)} value(s) the engine rejects (INVALID above)"
              + ("; refused, nothing written. Fix them in the Armory first." if args.apply
                 else "; --apply would refuse. Fix them in the Armory first."))
        return 1
    if not args.apply:
        print("DRY RUN: nothing written")
        return 0
    try:
        plans = [(file, plan_file(file, changes)) for file, changes in by_file.values()]
    except ValueError as exc:
        print(f"ERROR: {exc}")
        return 1
    write_files(plans, args.backup_tag)
    return 0


if __name__ == "__main__":
    sys.exit(main())
