#!/usr/bin/env python3
"""Gate for the modding handbook: attribute tables vs the decompile, examples vs disk.

Every attribute or child-element table under docs/modding/ carries a marker naming
the engine deserializer it was sourced from:

    <!-- engine-table type="TaleWorlds.Core.Monster"
         file="Core/TaleWorlds.Core/TaleWorlds.Core/Monster.cs"
         method="Deserialize" inert="cloth_alternative_color1" -->

and every worked example carries one naming the shipped file and entry it was
copied from:

    <!-- example file="Main/_Module/ModuleData/taom_spcultures.xml" id="erebor" -->

This script opens the cited decompile file, finds the named method plus every
non-public helper it calls in the same class (transitively), and extracts the
attribute and element names those bodies read through the idioms ILSpy emits for
the v1.4.8 dump:

    Attributes["x"]  Attributes?["x"]  GetAttribute("x")
    ReadObjectReferenceFromXml("x", ...)  ReadObjectReferenceFromXml<T>("x", ...)
    XmlHelper.ReadBool|ReadInt|ReadFloat|ReadString|ReadHexCode(node, "x")
    ReadVec3(node, "x")  DeserializeBoneIndex(node, "x", ...)
    DeserializeBoneIndexArray(list, node, flag, "x_", ...)      (a PREFIX: x_0, x_1 ...)
    node.Name == "x"  Name == "x"  .Name.Equals("x")  switch (node.Name) { case "x": }
    SelectSingleNode("x")  SelectNodes("x")           (each path segment is an element)

The last two lines are idioms the brief did not list. ItemObject.Deserialize
dispatches its component elements through a switch (ItemObject.cs:577-598), and
ModuleInfo.LoadWithFullPath reads SubModule.xml by XPath (ModuleInfo.cs:80-149);
without them every documented `<Armor>` or `Name@value` row would read as invented.

Column one of the table that follows the marker is then diffed against those sets:

    FABRICATION   a documented name the method never reads         (exit 1)
    GAP           a read name the table omits and inert= does not list (exit 1)
    NO_TABLE, MISSING_SOURCE, MISSING_METHOD, MANIFEST_STALE      (exit 1)
    MISSING_EXAMPLE  an example id absent from its file            (exit 1)

Every backticked token in column one is a claim. `name` is an attribute; `<name>`
is a child element; `Name@value` is the element Name plus its attribute value
(the `<Name value="..."/>` shape of SubModule.xml); `A/B@Id` is elements A and B
plus attribute Id; `@Optional` is an attribute alone. Tables in one chapter that
cite the same type/file/method are one documentation set, so the attribute table
and the child-element table for a class do not flag each other's names as gaps.
An example id is satisfied by `id="x"` on any element or by SubModule.xml's
`<Id value="x"/>`.

Attributes read by a base class (`id` in MBObjectBase, `name` in
BasicCultureObject) are not charged to a derived class's marker: give the base
file its own marker. The FABRICATION message says so when the method calls
base.Deserialize.

`--update` writes tools/handbook_attribute_manifest.json, the read-name sets per
marker, so a machine without the dump checks tables against the committed
manifest instead. Without either the tool exits 2 and says which is missing
(`.claude/rules/environment-failures.md`). Read-only: it never touches a doc.

Usage:
    python tools/check_handbook_attributes.py [--dump-root PATH] [--json PATH] [--update]

Exit codes: 0 clean, 1 findings, 2 bad input (no dump and no manifest).
"""
from __future__ import annotations

import argparse
import bisect
import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import game_modules as resolve_game_modules  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DOCS_DIR = REPO_ROOT / "docs" / "modding"
MANIFEST_PATH = REPO_ROOT / "tools" / "handbook_attribute_manifest.json"
DEFAULT_DUMP_ROOT = r"E:\Decompiled_Bannerlord\_categories_v1.4.8"
DUMP_ENV_VAR = "TAOM_DECOMPILE_ROOT"
DEFAULT_GAME_MODULES = resolve_game_modules(
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")

MARKER_KINDS = ("engine-table", "example")


class DumpRootMissing(Exception):
    """Raised by run() when tables need the dump and neither it nor a manifest exists."""

    def __init__(self, dump_root, manifest_path):
        self.dump_root = dump_root
        self.manifest_path = manifest_path
        super().__init__(f"decompile dump root not found: {dump_root}")


# ---------------------------------------------------------------------------
# Markdown side: markers and tables
# ---------------------------------------------------------------------------

# `.*?` rather than `[^>]*?`: an inert list may hold `<Module>`, and a marker that
# fails to parse makes its table vanish from the check without a word.
_MARKER_RE = re.compile(r"<!--\s*(engine-table|example)\b(.*?)-->", re.DOTALL)
_KV_RE = re.compile(r'([A-Za-z_][\w-]*)\s*=\s*"([^"]*)"')
_BACKTICK_RE = re.compile(r"`([^`]+)`")
_ELEMENT_NAME_RE = re.compile(r"^<([^<>\s]+)>$")
_SEPARATOR_RE = re.compile(r"^\|?\s*:?-{1,}")
# One segment of an XPath, in a table row or in a SelectSingleNode / SelectNodes
# literal: `*`, `.` and `..` are wildcards, not elements the engine names.
_XPATH_SEGMENT_RE = re.compile(r"^[A-Za-z_][\w.\-]*$")


def _claims_for(token: str, line_no: int) -> list:
    """The (name, is_element) claims one backticked column-one token makes.

    `name`            an attribute
    `<Name>`          a child element
    `Name@value`      the element Name and its attribute value (SubModule.xml style)
    `A/B@Id`          elements A and B, attribute Id
    `@Optional`       an attribute on the element the previous row named
    `Subs/Sub`        elements Subs and Sub
    """
    token = token.strip()
    em = _ELEMENT_NAME_RE.match(token)
    if em:
        return [{"name": em.group(1), "is_element": True, "line": line_no, "raw": token}]
    if "@" in token or "/" in token:
        path, _, attr = token.rpartition("@") if "@" in token else (token, "", "")
        # `*`, `.` and `..` are XPath wildcards, not elements the engine names.
        claims = [{"name": seg, "is_element": True, "line": line_no, "raw": token}
                  for seg in path.split("/") if _XPATH_SEGMENT_RE.match(seg)]
        if attr:
            claims.append({"name": attr, "is_element": False, "line": line_no, "raw": token})
        return claims
    return [{"name": token, "is_element": False, "line": line_no, "raw": token}]


def _split_inert(value: str) -> list:
    return [part.strip() for part in value.split(",") if part.strip()]


def _masked_for_markers(text: str) -> str:
    """Blank out fenced blocks and inline code spans, preserving offsets.

    A chapter documents its own marker syntax, so a literal marker inside backticks is an
    illustration and not a claim. Scanning raw text made the handbook's own "how to read a
    chapter" section fail the gate, and escaping the delimiter to get past it rendered the
    entity literally to the reader (a code span does not decode entities). Masking here is
    the fix that keeps both the gate and the prose correct.
    """
    out = list(text)
    i = 0
    n = len(text)
    in_fence = False
    while i < n:
        if text.startswith("```", i) and (i == 0 or text[i - 1] == "\n"):
            in_fence = not in_fence
            for j in range(i, min(i + 3, n)):
                out[j] = " "
            i += 3
            continue
        if in_fence:
            if text[i] != "\n":
                out[i] = " "
            i += 1
            continue
        if text[i] == "`":
            j = i + 1
            while j < n and text[j] != "`" and text[j] != "\n":
                j += 1
            if j < n and text[j] == "`":
                for k in range(i, j + 1):
                    out[k] = " "
                i = j + 1
                continue
        i += 1
    return "".join(out)


def parse_markers(text: str) -> list:
    """Every engine-table / example marker in a document, with its 1-based line.

    Markers inside a fenced block or an inline code span are illustrations, not claims.
    """
    markers = []
    for m in _MARKER_RE.finditer(_masked_for_markers(text)):
        kind, body = m.group(1), m.group(2)
        attrs = {k: v for k, v in _KV_RE.findall(body)}
        marker = {"kind": kind, "line": text.count("\n", 0, m.start()) + 1}
        if kind == "engine-table":
            marker["type"] = attrs.get("type", "")
            marker["file"] = attrs.get("file", "").replace("\\", "/")
            marker["method"] = attrs.get("method") or "Deserialize"
            marker["inert"] = _split_inert(attrs.get("inert", ""))
        else:
            marker["file"] = attrs.get("file", "").replace("\\", "/")
            marker["id"] = attrs.get("id", "")
        markers.append(marker)
    return markers


def parse_table_after(lines: list, marker_index: int):
    """Rows of the markdown table that follows a marker line (0-based index).

    Only blank lines may separate the marker from its table. Returns None when
    no table follows. Each row is {"name", "is_element", "line"} where line is the
    1-based file line; rows whose first cell holds no backticked name are skipped.
    """
    i = marker_index + 1
    while i < len(lines) and not lines[i].strip():
        i += 1
    if i >= len(lines) or not lines[i].lstrip().startswith("|"):
        return None
    table_lines = []
    while i < len(lines) and lines[i].lstrip().startswith("|"):
        table_lines.append((i + 1, lines[i]))
        i += 1
    rows = []
    for line_no, raw in table_lines[1:]:
        stripped = raw.strip()
        if _SEPARATOR_RE.match(stripped) and set(stripped) <= set("|:- "):
            continue
        cells = stripped.strip("|").split("|")
        for token in _BACKTICK_RE.findall(cells[0]) if cells else []:
            rows.extend(_claims_for(token, line_no))
    return rows


# ---------------------------------------------------------------------------
# C# side: a small scanner, method bodies, read idioms
# ---------------------------------------------------------------------------

def _skip_string(src: str, i: int) -> int:
    """Index just past the string literal whose opening quote is at src[i]."""
    n = len(src)
    prefix = src[max(0, i - 2):i]
    verbatim = "@" in prefix
    interpolated = "$" in prefix
    j = i + 1
    while j < n:
        c = src[j]
        if verbatim:
            if c == '"':
                if j + 1 < n and src[j + 1] == '"':
                    j += 2
                    continue
                return j + 1
            j += 1
            continue
        if c == "\\":
            j += 2
            continue
        if c == '"':
            return j + 1
        if interpolated and c == "{":
            if j + 1 < n and src[j + 1] == "{":
                j += 2
                continue
            j = _match(src, j, "{", "}") + 1
            continue
        j += 1
    return n


def _skip_char(src: str, i: int) -> int:
    j = i + 1
    if j < len(src) and src[j] == "\\":
        j += 1
    k = src.find("'", j + 1)
    return len(src) if k < 0 else k + 1


def _match(src: str, open_idx: int, open_ch: str, close_ch: str) -> int:
    """Index of the bracket closing the one at open_idx, skipping strings and comments."""
    depth = 0
    i = open_idx
    n = len(src)
    while i < n:
        c = src[i]
        if c == "/" and src.startswith("//", i):
            nl = src.find("\n", i)
            i = n if nl < 0 else nl
            continue
        if c == "/" and src.startswith("/*", i):
            end = src.find("*/", i + 2)
            i = n if end < 0 else end + 2
            continue
        if c == '"':
            i = _skip_string(src, i)
            continue
        if c == "'":
            i = _skip_char(src, i)
            continue
        if c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return n - 1


_MODIFIERS = ("public", "private", "protected", "internal", "static", "override",
              "virtual", "new", "sealed", "abstract", "unsafe", "extern", "async",
              "partial")
_DECL_RE = re.compile(
    r"(?m)^[ \t]*"
    r"(?P<mods>(?:(?:" + "|".join(_MODIFIERS) + r")\s+)*)"
    r"(?P<type>[A-Za-z_][\w.]*(?:\s*<[^;{}()]*?>)?(?:\s*\[\s*(?:,\s*)*\])*\s*\??)"
    r"\s+(?P<name>[A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(")
_NOT_A_TYPE = {"return", "new", "else", "throw", "case", "await", "yield", "using",
               "goto", "typeof", "default", "is", "as", "ref", "out", "in", "var",
               "delegate", "event", "operator"}
_NOT_A_NAME = {"if", "while", "for", "foreach", "switch", "catch", "using", "lock",
               "return", "new", "typeof", "sizeof", "nameof", "default", "when", "is",
               "fixed", "do", "else"}


def find_method_bodies(src: str) -> dict:
    """name -> [{"start", "end", "public"}] for every method declared in the file.

    Spans cover the body between (and excluding) the braces. Expression-bodied
    methods span up to their terminating semicolon; bodiless declarations are
    skipped.
    """
    decls = {}
    for m in _DECL_RE.finditer(src):
        type_token = m.group("type").split("<")[0].strip().rstrip("?")
        name = m.group("name")
        if type_token in _NOT_A_TYPE or name in _NOT_A_NAME:
            continue
        open_paren = m.end() - 1
        close_paren = _match(src, open_paren, "(", ")")
        i = close_paren + 1
        while i < len(src) and src[i].isspace():
            i += 1
        if src.startswith("where", i):
            brace = src.find("{", i)
            semi = src.find(";", i)
            if brace < 0 or (0 <= semi < brace):
                continue
            i = brace
        if i >= len(src):
            continue
        if src[i] == "{":
            end = _match(src, i, "{", "}")
            span = {"start": i + 1, "end": end}
        elif src.startswith("=>", i):
            end = src.find(";", i)
            span = {"start": i + 2, "end": len(src) if end < 0 else end}
        else:
            continue
        span["public"] = "public" in m.group("mods").split()
        decls.setdefault(name, []).append(span)
    return decls


_ATTR_PATTERNS = (
    re.compile(r'Attributes\??\[\s*"([^"]+)"\s*\]'),
    re.compile(r'\bGetAttribute\(\s*"([^"]+)"'),
    re.compile(r'\bReadObjectReferenceFromXml(?:<[^<>()]+>)?\(\s*"([^"]+)"'),
    re.compile(r'\bXmlHelper\.Read\w+\([^;"()]*?"([^"]+)"'),
    re.compile(r'\bReadVec3\([^;"()]*?"([^"]+)"'),
    re.compile(r'\bDeserializeBoneIndex\([^;"()]*?"([^"]+)"'),
)
_PREFIX_PATTERNS = (
    re.compile(r'\bDeserializeBoneIndexArray\([^;"()]*?"([^"]+)"'),
)
_ELEMENT_PATTERNS = (
    re.compile(r'\bName\s*[!=]=\s*"([^"]+)"'),
    re.compile(r'\bName\.Equals\(\s*"([^"]+)"'),
)
# SelectSingleNode("A/B") / SelectNodes("X"): every path segment is an element the
# method reaches for (ModuleInfo.LoadWithFullPath reads SubModule.xml this way).
_XPATH_PATTERN = re.compile(r'\bSelect(?:SingleNode|Nodes)\(\s*"([^"]+)"')
_SWITCH_ON_NAME_RE = re.compile(r"\bswitch\s*\(\s*[\w.]*\bName\s*\)")
_CASE_RE = re.compile(r'\bcase\s+"([^"]+)"\s*:')
_CALL_RE = re.compile(r"(?<![\w.])(?:this\.)?([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(")


def _line_of(line_starts: list, offset: int) -> int:
    return bisect.bisect_right(line_starts, offset)


def _record(target: dict, name: str, line: int) -> None:
    if name not in target or (line and line < target[name]):
        target[name] = line


def extract_reads(src: str, method_name: str) -> dict:
    """Attribute / element / prefix names the method (plus its helpers) reads.

    Returns {"found", "attributes": {name: line}, "elements": {name: line},
    "prefixes": {name: line}, "helpers": [names], "calls_base"}. Lines are 1-based
    and the first read wins.
    """
    decls = find_method_bodies(src)
    result = {"found": method_name in decls, "attributes": {}, "elements": {},
              "prefixes": {}, "helpers": [], "calls_base": False}
    if not result["found"]:
        return result
    line_starts = [0] + [m.end() for m in re.finditer(r"\n", src)]
    base_re = re.compile(r"\bbase\." + re.escape(method_name) + r"\s*\(")

    visited = set()
    queue = [method_name]
    while queue:
        name = queue.pop(0)
        if name in visited:
            continue
        visited.add(name)
        for span in decls[name]:
            body = src[span["start"]:span["end"]]
            base = span["start"]
            if name == method_name and base_re.search(body):
                result["calls_base"] = True
            for pattern in _ATTR_PATTERNS:
                for m in pattern.finditer(body):
                    _record(result["attributes"], m.group(1), _line_of(line_starts, base + m.start(1)))
            for pattern in _PREFIX_PATTERNS:
                for m in pattern.finditer(body):
                    _record(result["prefixes"], m.group(1), _line_of(line_starts, base + m.start(1)))
            for pattern in _ELEMENT_PATTERNS:
                for m in pattern.finditer(body):
                    _record(result["elements"], m.group(1), _line_of(line_starts, base + m.start(1)))
            for m in _XPATH_PATTERN.finditer(body):
                line = _line_of(line_starts, base + m.start(1))
                for seg in m.group(1).split("/"):
                    if _XPATH_SEGMENT_RE.match(seg):
                        _record(result["elements"], seg, line)
            for sw in _SWITCH_ON_NAME_RE.finditer(body):
                brace = body.find("{", sw.end())
                if brace < 0:
                    continue
                block_end = _match(body, brace, "{", "}")
                for cm in _CASE_RE.finditer(body, brace, block_end):
                    _record(result["elements"], cm.group(1), _line_of(line_starts, base + cm.start(1)))
            for cm in _CALL_RE.finditer(body):
                callee = cm.group(1)
                if callee == name or callee not in decls or callee in visited:
                    continue
                if all(d["public"] for d in decls[callee]):
                    continue
                queue.append(callee)
    result["helpers"] = sorted(visited - {method_name})
    return result


# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------

def marker_key(marker: dict) -> str:
    return f"{marker['type']}|{marker['file']}|{marker['method']}"


def _finding(label, doc, line, name, message, type_name="", file=""):
    return {"label": label, "doc": doc, "line": line, "name": name, "message": message,
            "type": type_name, "file": file}


def _matches_prefix(name: str, prefixes) -> str | None:
    for prefix in prefixes:
        if name.startswith(prefix) and len(name) > len(prefix):
            rest = name[len(prefix):]
            if rest.isdigit() or rest in ("<n>", "N", "n", "*"):
                return prefix
    return None


def check_tables(doc: str, group: list, reads: dict) -> list:
    """FABRICATION / GAP findings for every table in one (doc, marker key) group.

    `group` is a list of (marker, rows). Documented names and inert lists are
    unioned across the group before gaps are computed.
    """
    attr_reads = set(reads["attributes"])
    elem_reads = set(reads["elements"])
    prefixes = set(reads["prefixes"])
    inert_attrs, inert_elems = set(), set()
    for marker, _rows in group:
        for entry in marker["inert"]:
            em = _ELEMENT_NAME_RE.match(entry)
            (inert_elems if em else inert_attrs).add(em.group(1) if em else entry)

    findings = []
    documented_attrs, documented_elems, covered_prefixes = set(), set(), set()
    first = group[0][0]
    method = first["method"]
    hint = (" (the method calls base.Deserialize: a base-class read needs its own "
            "engine-table marker citing the base file)" if reads.get("calls_base") else "")
    for marker, rows in group:
        for row in rows:
            name = row["name"]
            via = f" (row `{row['raw']}`)" if row.get("raw", name) != name else ""
            if row["is_element"]:
                documented_elems.add(name)
                if name not in elem_reads:
                    findings.append(_finding(
                        "FABRICATION", doc, row["line"], f"<{name}>",
                        f"`<{name}>` is documented as an element{via} but {method} never "
                        f"reaches for it{hint}", marker["type"], marker["file"]))
                continue
            documented_attrs.add(name)
            if name in attr_reads:
                continue
            prefix = _matches_prefix(name, prefixes)
            if prefix:
                covered_prefixes.add(prefix)
                continue
            findings.append(_finding(
                "FABRICATION", doc, row["line"], name,
                f"`{name}` is documented{via} but {method} never reads it{hint}",
                marker["type"], marker["file"]))

    for name in sorted(attr_reads - documented_attrs - inert_attrs):
        findings.append(_finding(
            "GAP", doc, first["line"], name,
            f"`{name}` is read at {Path(first['file']).name}:{reads['attributes'][name] or '?'} "
            f"but no table documents it and inert= does not list it",
            first["type"], first["file"]))
    for prefix in sorted(prefixes - covered_prefixes - inert_attrs):
        findings.append(_finding(
            "GAP", doc, first["line"], prefix,
            f"`{prefix}<n>` (an indexed attribute family) is read at "
            f"{Path(first['file']).name}:{reads['prefixes'][prefix] or '?'} but no table "
            f"documents any member of it and inert= does not list the prefix",
            first["type"], first["file"]))
    for name in sorted(elem_reads - documented_elems - inert_elems):
        findings.append(_finding(
            "GAP", doc, first["line"], f"<{name}>",
            f"`<{name}>` is tested for at {Path(first['file']).name}:"
            f"{reads['elements'][name] or '?'} but no table documents it and inert= does "
            f"not list it",
            first["type"], first["file"]))
    return findings


# `id="x"` on any element, or SubModule.xml's identity element `<Id value="x"/>`.
_ID_RE_TEMPLATE = r"""(?<![\w\-.:])id\s*=\s*(["']){0}\1|<Id\s+value\s*=\s*(["']){0}\2"""


def id_exists_in_file(path: Path, ident: str) -> bool:
    """True when `id="<ident>"` or `<Id value="<ident>"` occurs as a whole value."""
    text = Path(path).read_text(encoding="utf-8-sig", errors="replace")
    return re.search(_ID_RE_TEMPLATE.format(re.escape(ident)), text) is not None


def resolve_example_file(rel: str, repo_root: Path, game_modules: Path):
    """(path or None, [candidates tried]) for a repo-relative or module-relative path."""
    candidates = [Path(repo_root) / rel, Path(game_modules) / rel]
    for candidate in candidates:
        if candidate.is_file():
            return candidate, candidates
    return None, candidates


def check_example(doc: str, marker: dict, repo_root: Path, game_modules: Path):
    path, tried = resolve_example_file(marker["file"], repo_root, game_modules)
    if path is None:
        where = " or ".join(str(c.parent) for c in tried)
        note = ""
        if not Path(game_modules).is_dir():
            note = f"; the Modules folder {game_modules} does not exist (set $BANNERLORD_GAME_DIR)"
        return _finding("MISSING_EXAMPLE", doc, marker["line"], marker["id"],
                        f"example file `{marker['file']}` not found under {repo_root} "
                        f"or {game_modules} (looked in {where}){note}", file=marker["file"])
    if not id_exists_in_file(path, marker["id"]):
        return _finding("MISSING_EXAMPLE", doc, marker["line"], marker["id"],
                        f"no entry with id=\"{marker['id']}\" in {path}", file=marker["file"])
    return None


# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------

def resolve_dump_root(cli_value, env=None) -> Path:
    """--dump-root, else $TAOM_DECOMPILE_ROOT (blank counts as unset), else the default."""
    if cli_value:
        return Path(cli_value)
    env = os.environ if env is None else env
    value = env.get(DUMP_ENV_VAR)
    if value and value.strip():
        return Path(value.strip())
    return Path(DEFAULT_DUMP_ROOT)


def load_manifest(path) -> dict | None:
    if path is None or not Path(path).is_file():
        return None
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_manifest(manifest: dict, path: Path) -> None:
    text = json.dumps(manifest, indent=2, sort_keys=True) + "\n"
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def _reads_from_manifest(entry: dict) -> dict:
    return {"found": True,
            "attributes": {n: None for n in entry.get("attributes", [])},
            "elements": {n: None for n in entry.get("elements", [])},
            "prefixes": {n: None for n in entry.get("prefixes", [])},
            "helpers": list(entry.get("helpers", [])),
            "calls_base": bool(entry.get("calls_base", False))}


def _manifest_entry(marker: dict, reads: dict) -> dict:
    return {"type": marker["type"], "file": marker["file"], "method": marker["method"],
            "attributes": sorted(reads["attributes"]),
            "elements": sorted(reads["elements"]),
            "prefixes": sorted(reads["prefixes"]),
            "helpers": sorted(reads["helpers"]),
            "calls_base": bool(reads["calls_base"])}


def run(docs_dir, dump_root, repo_root, game_modules, manifest_path) -> dict:
    """Check every marker under docs_dir. Returns the report dict (so --json is free).

    Raises DumpRootMissing when engine-table markers exist but neither the dump
    root nor a manifest does. Example markers never need the dump.
    """
    docs_dir = Path(docs_dir)
    dump_root = Path(dump_root) if dump_root is not None else None
    dump_available = dump_root is not None and dump_root.is_dir()
    manifest_data = load_manifest(manifest_path)

    findings = []
    markers_total = 0
    docs_seen = 0
    groups = {}      # (doc, key) -> [(marker, rows)]
    order = []       # insertion order of group keys
    for doc_path in sorted(docs_dir.rglob("*.md")) if docs_dir.is_dir() else []:
        docs_seen += 1
        text = doc_path.read_text(encoding="utf-8-sig", errors="replace")
        lines = text.splitlines()
        doc = doc_path.relative_to(docs_dir).as_posix()
        for marker in parse_markers(text):
            markers_total += 1
            if marker["kind"] == "example":
                finding = check_example(doc, marker, Path(repo_root), Path(game_modules))
                if finding:
                    findings.append(finding)
                continue
            rows = parse_table_after(lines, marker["line"] - 1)
            if rows is None:
                findings.append(_finding(
                    "NO_TABLE", doc, marker["line"], marker["type"],
                    "no markdown table follows the engine-table marker (only blank lines "
                    "may separate them)", marker["type"], marker["file"]))
                continue
            gk = (doc, marker_key(marker))
            if gk not in groups:
                groups[gk] = []
                order.append(gk)
            groups[gk].append((marker, rows))

    source = None
    if groups:
        if dump_available:
            source = "dump"
        elif manifest_data is not None:
            source = "manifest"
        else:
            raise DumpRootMissing(dump_root, manifest_path)

    manifest_out = {"dump_root_name": dump_root.name if dump_available else
                    (manifest_data or {}).get("dump_root_name", ""), "markers": {}}
    reads_cache = {}
    for gk in order:
        doc, key = gk
        group = groups[gk]
        first = group[0][0]
        if key not in reads_cache:
            reads_cache[key] = _load_reads(first, source, dump_root, manifest_data)
        reads, problem = reads_cache[key]
        if problem:
            label, message = problem
            findings.append(_finding(label, doc, first["line"], first["type"], message,
                                     first["type"], first["file"]))
            continue
        manifest_out["markers"][key] = _manifest_entry(first, reads)
        findings.extend(check_tables(doc, group, reads))

    return {"findings": findings, "markers": markers_total, "docs": docs_seen,
            "source": source, "manifest": manifest_out,
            "dump_root": str(dump_root) if dump_root else None,
            "dump_available": dump_available, "docs_dir": str(docs_dir)}


def _load_reads(marker: dict, source: str, dump_root, manifest_data):
    """(reads, None) or (None, (label, message)) for one marker."""
    if source == "dump":
        cs_path = Path(dump_root) / marker["file"]
        if not cs_path.is_file():
            return None, ("MISSING_SOURCE",
                          f"cited file `{marker['file']}` does not exist under {dump_root}")
        src = cs_path.read_text(encoding="utf-8-sig", errors="replace")
        reads = extract_reads(src, marker["method"])
        if not reads["found"]:
            return None, ("MISSING_METHOD",
                          f"no method named `{marker['method']}` with a body in "
                          f"`{marker['file']}`")
        return reads, None
    entry = (manifest_data or {}).get("markers", {}).get(marker_key(marker))
    if entry is None:
        return None, ("MANIFEST_STALE",
                      f"marker `{marker_key(marker)}` is not in the committed manifest; "
                      f"re-run with --update on a machine that has the dump")
    return _reads_from_manifest(entry), None


def exit_code_for(report: dict) -> int:
    return 1 if report["findings"] else 0


def _print_report(report: dict, manifest_path) -> None:
    for f in report["findings"]:
        where = f["type"] or f["file"]
        print(f"{f['label']:<16}{f['doc']}:{f['line']} {where}: {f['message']}")
    if report["source"] == "manifest":
        against = (f"the committed manifest {manifest_path} (dump root "
                   f"{report['dump_root']} not found)")
    elif report["source"] == "dump":
        against = f"the dump at {report['dump_root']}"
    else:
        against = "nothing (no engine-table markers)"
    print(f"{report['markers']} marker(s) in {report['docs']} doc(s) checked against "
          f"{against}: {len(report['findings'])} finding(s)")


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--docs", default=str(DOCS_DIR), help="handbook folder to scan")
    ap.add_argument("--dump-root", default=None,
                    help=f"decompile root (default ${DUMP_ENV_VAR} or {DEFAULT_DUMP_ROOT})")
    ap.add_argument("--repo-root", default=str(REPO_ROOT))
    ap.add_argument("--game-modules", default=str(DEFAULT_GAME_MODULES),
                    help="Bannerlord Modules folder for module-relative example files")
    ap.add_argument("--manifest", default=str(MANIFEST_PATH),
                    help="committed read-name manifest (fallback when the dump is absent)")
    ap.add_argument("--json", dest="json_out", default=None, help="write the report here")
    ap.add_argument("--update", action="store_true",
                    help="rewrite the manifest from the dump (needs the dump)")
    args = ap.parse_args(argv)

    dump_root = resolve_dump_root(args.dump_root)
    if args.update and not dump_root.is_dir():
        print(f"ERROR: --update needs the decompile dump, and none was found at {dump_root}\n"
              f"       Set ${DUMP_ENV_VAR} or pass --dump-root to the v1.4.8 decompile "
              f"(default {DEFAULT_DUMP_ROOT}).", file=sys.stderr)
        return 2

    try:
        report = run(args.docs, dump_root, args.repo_root, args.game_modules, args.manifest)
    except DumpRootMissing as exc:
        print(f"ERROR: decompile dump root not found: {exc.dump_root}\n"
              f"       Set ${DUMP_ENV_VAR} (or pass --dump-root) to the v1.4.8 decompile, "
              f"default {DEFAULT_DUMP_ROOT}.\n"
              f"       No committed manifest at {exc.manifest_path} to fall back on either; "
              f"a machine with the dump writes one with --update.", file=sys.stderr)
        return 2

    code = exit_code_for(report)
    report["exit_code"] = code
    if args.update:
        write_manifest(report["manifest"], Path(args.manifest))
        print(f"manifest written: {args.manifest} ({len(report['manifest']['markers'])} marker(s))")
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(report, fh, indent=2)
            fh.write("\n")
    _print_report(report, args.manifest)
    return code


if __name__ == "__main__":
    sys.exit(main())
