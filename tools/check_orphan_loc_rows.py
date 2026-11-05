#!/usr/bin/env python
"""Gate for registered `taom_*` localization rows that nothing reads (#1018).

A row in TAOM's own English strings files costs a translation in 12 languages. When the code that
read it is removed (`taom_enlist_wait_text` sat unused from 3164fb291 until #1016), the row stays and
no check notices. This lists every `taom_*` id in the English strings files and fails when none of
these names it:

  * a C# string literal under Main/ or Client/ (`{=id}` or the bare id; comments do not count),
  * a data file anywhere under Main/_Module (ModuleData, GUI prefabs and so on) outside the strings
    files and the Languages/ copies: XML, JSON or XSLT, naming `{=id}` or the bare id (XML and XSLT
    comments do not count),
  * the engine itself: a row of taom_module_strings.xml whose text opens `{=KEY}` with an 8-character
    vanilla KEY is TAOM's English reword of a vanilla text, read under that key; its own id is
    arbitrary. (LocalizationOverrideLoader registers any key not starting `taom_` and not `!` or `*`;
    the 8-character shape is the project convention the localization tests pin, and this exemption
    matches that convention.) The loader keeps the LAST row per key, so an earlier row for the same
    key, whatever its id, is reported as SHADOWED,
  * a composed family declared in tools/loc_composed_families.json (name -> regex, reason, site),
    for keys built at run time such as `DutyText.KeyFor`. A regex group named `stem` must also
    appear as a token in the data, so the rows of a removed duty or option stop passing,
  * the reasoned allowlist tools/loc_orphan_allowlist.json (id -> reason), for rows kept on purpose.

The English files are those registered as GameText in Main/_Module/SubModule.xml plus the table in
tools/_loc_sources.py (a listed file that is missing is a finding). Out of scope: the Languages/<LANG>
copies, ids not starting `taom_` (vanilla keys, the dotted `str_key_name.*` keybind rows), and
anything outside this repo: the live TAOM_Map and LOTRLOME_Armory may read a row this gate calls dead.

Known limit: the C# scan ends a string at the next quote, so a literal nested inside an interpolation
hole (`$"a {Key("taom_x")} b"`) is not seen. That errs toward a false UNREAD, never a missed orphan.

Exit codes: 0 clean, 1 an unread row or a stale declaration, 2 an unreadable or malformed input.
Read-only; no traceback on bad input.

Usage:
    python tools/check_orphan_loc_rows.py
    python tools/check_orphan_loc_rows.py --repo-root <dir>
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _loc_sources import OVERRIDE_SOURCE, TAOM_SOURCES, VANILLA_ID_SHAPE  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
PREFIX = "taom_"
INLINE_KEY = re.compile(r"\{=([^}]*)\}")
TOKEN = re.compile(r"[A-Za-z0-9_]+")
# Comments first so a literal inside one is never read; char literals so '"' opens no string.
CSHARP_PIECE = re.compile(
    r"//[^\n]*|/\*.*?\*/|'(?:[^'\\\n]|\\.)+'|@\"(?:[^\"]|\"\")*\"|\"(?:[^\"\\\n]|\\.)*\"", re.S)
XML_COMMENT = re.compile(r"<!--.*?-->", re.S)
DATA_SUFFIXES = {".xml", ".json", ".xslt"}
SKIP_DIRS = {"bin", "obj", ".git"}


class GateError(Exception):
    """An input the gate cannot read (exit 2)."""


def _decode(path):
    try:
        return path.read_bytes().decode("utf-8-sig")
    except (OSError, UnicodeDecodeError) as exc:
        raise GateError("cannot read %s: %s" % (path, exc))


def _parse(path):
    try:
        return ET.fromstring(_decode(path).encode("utf-8"))
    except ET.ParseError as exc:
        raise GateError("malformed XML %s: %s" % (path, exc))


def english_files(root):
    """(existing English strings files, listed ones that are missing): GameText-registered in
    SubModule.xml plus the _loc_sources table."""
    moddata = root / "Main" / "_Module" / "ModuleData"
    listed = [src for src, _ in TAOM_SOURCES]
    rels = list(listed)
    sub = root / "Main" / "_Module" / "SubModule.xml"
    if sub.is_file():
        rels += [el.get("path", "") + ".xml" for el in _parse(sub).iter("XmlName")
                 if el.get("id") == "GameText"]
    found, seen = [], set()
    for rel in rels:
        path = moddata / rel
        if rel not in seen and path.is_file():
            seen.add(rel)
            found.append(path)
    return found, [rel for rel in listed if not (moddata / rel).is_file()]


def read_rows(files):
    """[(id, file, inline_key)] for every string row, taom_ or not, in file order. Shadowing is by
    position over all of them, because the loader's dictionary does not care about the id."""
    rows = []
    for path in files:
        for el in _parse(path).iter("string"):
            m = INLINE_KEY.match(el.get("text", "") or "")
            rows.append((el.get("id", ""), path, m.group(1) if m else ""))
    return rows


def _walk(base, suffixes):
    if not base.is_dir():
        return
    for path in sorted(base.rglob("*")):
        if path.suffix.lower() in suffixes and path.is_file() \
                and not SKIP_DIRS.intersection(path.relative_to(base).parts):
            yield path


def read_tokens(root, strings_files):
    """(code tokens, data tokens): the identifier-shaped tokens a reader could name, from C# string
    literals and from data files. A family's `stem` must be a DATA token."""
    code, data = set(), set()
    for sub in ("Main", "Client"):
        for path in _walk(root / sub, {".cs"}):
            for piece in CSHARP_PIECE.findall(_decode(path)):
                if piece[0] in "\"@":
                    code.update(TOKEN.findall(piece))
    skip = {p.resolve() for p in strings_files}
    for path in _walk(root / "Main" / "_Module", DATA_SUFFIXES):
        if "Languages" in path.relative_to(root).parts or path.resolve() in skip:
            continue
        text = _decode(path)
        if path.suffix.lower() in (".xml", ".xslt"):
            text = XML_COMMENT.sub(" ", text)   # a commented-out element reads nothing
        data.update(TOKEN.findall(text))
    return code, data


def _load_map(path, what, keys):
    """name -> entry from a JSON file (entries are reason strings when keys is None)."""
    if not path.is_file():
        return {}
    try:
        data = json.loads(_decode(path))
    except ValueError as exc:
        raise GateError("malformed JSON %s: %s" % (path, exc))
    if not isinstance(data, dict):
        raise GateError("%s must be a JSON object: %s" % (what, path))
    for name, entry in data.items():
        if keys is None:
            ok = isinstance(entry, str) and entry.strip()
        else:
            ok = isinstance(entry, dict) and all(
                isinstance(entry.get(k), str) and entry[k].strip() for k in keys)
        if not ok:
            need = "a reason" if keys is None else ", ".join(keys)
            raise GateError("%s entry %r needs %s: %s" % (what, name, need, path))
    return data


def _is_reword(path, key):
    return path.name == OVERRIDE_SOURCE and bool(VANILLA_ID_SHAPE.fullmatch(key))


def _composed(rid, patterns, data_tokens):
    """True when a declared family matches the id and its `stem` group (if any) is named in the data."""
    for pat in patterns.values():
        m = pat.fullmatch(rid)
        if m and ("stem" not in pat.groupindex or m.group("stem") in data_tokens):
            return True
    return False


def check(root, families_path, allow_path):
    """(findings, row_count, file_count)."""
    files, missing = english_files(root)
    if not files:
        raise GateError("no English strings files under %s" % (root / "Main" / "_Module" / "ModuleData"))
    all_rows = read_rows(files)
    ids = {rid for rid, _, _ in all_rows if rid.startswith(PREFIX)}
    n_rows = sum(1 for rid, _, _ in all_rows if rid.startswith(PREFIX))
    if not n_rows:
        raise GateError("no taom_ rows in %d English strings file(s)" % len(files))
    code_tokens, data_tokens = read_tokens(root, files)
    tokens = code_tokens | data_tokens
    families = _load_map(families_path, "family", ("regex", "reason", "site"))
    allow = _load_map(allow_path, "allowlist", None)
    patterns = {}
    for name, entry in families.items():
        try:
            patterns[name] = re.compile(entry["regex"])
        except re.error as exc:
            raise GateError("family %r has a bad regex: %s" % (name, exc))

    findings = ["MISSING SOURCE %s: listed in tools/_loc_sources.py" % rel for rel in missing]
    last = {}   # vanilla key -> position of the row the loader keeps (the last one)
    for i, (rid, path, key) in enumerate(all_rows):
        if _is_reword(path, key):
            last[key] = i
    used = set()   # allowlist entries that suppressed a finding
    for i, (rid, path, key) in enumerate(all_rows):
        if _is_reword(path, key):
            if last[key] != i:
                if rid in allow:
                    used.add(rid)
                else:
                    findings.append("SHADOWED %s: %s registers {=%s} later and wins"
                                    % (rid, all_rows[last[key]][0], key))
            continue
        if not rid.startswith(PREFIX) or rid in tokens or _composed(rid, patterns, data_tokens):
            continue
        if rid in allow:
            used.add(rid)
        else:
            findings.append("UNREAD %s (%s)" % (rid, path.name))
    for name, pat in patterns.items():
        if not any(pat.fullmatch(i) for i in ids):
            findings.append("STALE FAMILY %s: matches no row" % name)
        if not (root / families[name]["site"]).is_file():
            findings.append("STALE FAMILY %s: site %s is not a file" % (name, families[name]["site"]))
    for rid in allow:
        if rid not in ids:
            findings.append("STALE ALLOWLIST %s: not a row" % rid)
        elif rid not in used:
            findings.append("STALE ALLOWLIST %s: read another way, drop the entry" % rid)
    return findings, n_rows, len(files)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--repo-root", type=Path, default=REPO_ROOT)
    ap.add_argument("--families", type=Path, help="default tools/loc_composed_families.json")
    ap.add_argument("--allowlist", type=Path, help="default tools/loc_orphan_allowlist.json")
    args = ap.parse_args(argv)
    root = args.repo_root
    try:
        findings, n_rows, n_files = check(
            root, args.families or root / "tools" / "loc_composed_families.json",
            args.allowlist or root / "tools" / "loc_orphan_allowlist.json")
    except GateError as exc:
        print("error: %s" % exc, file=sys.stderr)
        return 2
    for line in findings:
        print(line)
    print("checked %d rows in %d files" % (n_rows, n_files))
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
