#!/usr/bin/env python3
"""Validate every engine-loaded ModuleData XML file against the engine's own XSD.

`validate_moduledata.py` resolves cross-references; its own passes never check a file
against the schema the engine reads it with. So an element missing a required attribute,
or carrying one the engine does not know, passed every TAOM gate: `clan_umbar_3` shipped
with no `initial_home_settlement` that way (fixed 2026-06-22). The engine does validate on
a normal campaign load, but `MBObjectManager.LoadXmlWithValidation` only PRINTS a failure
to the log and loads the file anyway, so the mistake is silent in game.
`validate_moduledata.py` now runs this tool over the repo module as its SCHEMA_INVALID
pass, so the commit hook gates it too.

This tool mirrors the engine's file resolution and schema choice (v1.5.3 decompile,
`TaleWorlds.ObjectSystem.MBObjectManager.GetMergedXmlForManaged`,
`XmlResource.GetXmlListAndApply` and `TaleWorlds.ModuleManager.ModuleHelper.GetXmlPath` /
`GetXsdPath` / `GetXsdPathForModules`). The engine's `<IncludedGameTypes>` filter is not
applied, so it may validate a file a given game type never loads, never the reverse.

  * Registrations: each `<XmlNode><XmlName id="X" path="P"/>` in a module's SubModule.xml.
    An `<XmlNode>` with no `<XmlName>`, or an `<XmlName>` with no `id` or no `path`, makes
    the engine throw at startup (`GetXmlListAndApply`); each is reported, never skipped.
  * Files: a registration loads `ModuleData/P.xml` when that file exists, else every
    top-level file of the folder `ModuleData/P/` returned by .NET Framework's
    `GetFiles("*.xml")`, which matches long and 8.3 short names: an exact `.xml`
    everywhere, plus `foo.xmlbak`-style extensions (short name `FOO~1.XML`) on volumes that
    generate short names (Windows system volumes by default; not this machine's E:,
    measured 2026-09-18). A `foo.xml.bak` never loads. This tool matches the union, so a
    backup that would load on a player's volume is never missed. With neither file nor
    folder, the engine looks for a stylesheet `P.xsl` then `P.xslt` (`HandleXsltList`): an
    XSLT-only registration that transforms other modules' data.
  * A registration that resolves to none of those loads nothing (silently for every
    registration today; a dead one that is the FIRST for its id across the active modules
    makes the engine throw out of `LoadXML` instead, `CreateDocumentFromXmlFile("")`), and
    so does a folder holding no `*.xml`. This tool reports both.
  * Schema: `<Module>/ModuleData/XmlSchemas/X.xsd` when the module ships one, else
    `<game>/XmlSchemas/X.xsd`. With neither, the engine logs that the schema is missing
    and loads the file unvalidated: reported as NO SCHEMA, never as a failure.
  * The engine injects `_replaceWhileMerging` into every complex type as an optional
    xs:boolean before validating, so that attribute (and only that one, spelled exactly
    so, with an xs:boolean value) is accepted here too.
  * The engine reads each data file with a default `XmlReaderSettings`, which prohibits
    DTDs: a DOCTYPE throws inside a bare catch and the file loads nothing, so it fails here.
  * An XSD that includes, imports or redefines a schema by URL is refused, never fetched.

A file passed explicitly that no `<XmlName>` loads is classed by what else the engine does
with it, none of which an XSD checks:
  * LANGUAGE FILE: a `ModuleData/Languages/**/language_data.xml`, or a file one lists through
    `<LanguageFile xml_path>` or `<VoiceFile xml_path>` (relative to ModuleData/Languages, as
    `LanguageData.Deserialize` joins it). The engine reads it while its language is active: the
    <functions> always and the <strings> rows for every language but English, from the document's
    second node, so the XML declaration must come first. A manifest the engine cannot load (it
    does not parse, or its root is not a <LanguageData> with an id) is named as a real fault.
  * STYLESHEET: the `.xsl` (else `.xslt`) that `MBObjectManager.HandleXsltList` queues beside a
    loaded file or for an XSLT-only registration. `CreateMergedXmlFile` applies it to the data
    merged from earlier registrations of the same id, before its own file merges, and never for
    an id's first registration.
  * NOT REGISTERED: no `<XmlName>` or language manifest loads it and no `project.mbproj` row names it.
    If the engine reads it at all, it is only by fixed name (`global_strings.xml`, the scene lists,
    `ModuleData/project.mbproj` itself), which this tool does not check. Expected for TAOM's own data
    files that C# reads directly (Careers, AbilityTemplates, ...); a real fault for an .xml file under
    ModuleData/Languages that no manifest the engine loads lists.
  * NATIVE: a file named by a `<file id name>` row of the module's `ModuleData/project.mbproj` (root
    `<base>`), or the `.xsl`, else `.xslt`, beside it. The row alone does not make it load.
    `XmlResource.GetMbprojxmls` registers the rows, and the managed `GetMergedXmlForNative` reads the
    rows whose exact id a caller requests: it joins `<module folder>/<name>` as strings (relative to
    the module, not to ModuleData) and applies a stylesheet to the data merged from earlier rows of
    that id, never for the id's first row. Native code reads the other kinds by the row's `type` and
    applies no stylesheet. This tool hard-codes neither the engine's id list nor its type list, so it
    names the row's id and type and claims no load. An unloadable `project.mbproj` (it does not parse,
    its root is not `<base>`, a row has no id or name) makes the engine
    throw at startup, and it is read as the engine reads it (as a StreamReader reads it: UTF-8 unless a
    byte order mark says otherwise, the declared encoding ignored, a BOM-less UTF-16 file not well-formed;
    a character reference the engine accepts with CheckCharacters=false, such as `&#1;`, still
    fails here): passed by name, it is reported like a bad SubModule.xml. No XSD here checks
    any of it; the gates are listed by `python tools/data_gates.py --list`.
A file passed by name that is a real fault (a manifest the engine cannot load, an unlisted .xml under
ModuleData/Languages) makes the run exit 1 and the summary read FAIL; the summary counts them too. A
whole-module run classifies nothing by name, so its exit code is unchanged.

Usage:
    python tools/validate_xml_schemas.py                       # the repo module
    python tools/validate_xml_schemas.py --live                # + TAOM_Map, LOTRLOME_Armory
    python tools/validate_xml_schemas.py FILE [FILE ...]       # only these files
    python tools/validate_xml_schemas.py --module DIR [--schemas DIR]

A FILE is validated under its own module (the nearest parent folder holding SubModule.xml)
even when --module does not name it. --live reads the Modules folder from
$BANNERLORD_GAME_MODULES, else $BANNERLORD_GAME_DIR/Modules (tools/_gamedir.py).

Exit codes: 0 clean; 1 a file failed, a registration loads nothing, an unloadable project.mbproj
was passed by name, or a by-name run named a real fault; 2 bad input (lxml not installed, no schema
folder, no module or no Modules folder for --live, a FILE that is not a file, a malformed
SubModule.xml), so a partial run never reports clean.
"""
from __future__ import annotations

import argparse
import functools
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# lxml is the only non-stdlib dependency and the tools tests (`/verify` Step 2b) do not
# require it, so it is optional at import time (check_external_xslt.py's convention). Without it nothing
# can be validated, and main() says so with exit 2 rather than a traceback.
try:
    from lxml import etree
    HAVE_LXML = True
except ImportError:
    etree = None
    HAVE_LXML = False

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import game_dir, game_modules  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
REPO_MODULE = REPO_ROOT / "Main" / "_Module"
_DEFAULT_INSTALL = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
GAME_DIR = Path(game_dir(_DEFAULT_INSTALL))
#: The Modules folder `--live` reads, resolved like every sibling tool's.
DEFAULT_GAME_MODULES = game_modules(_DEFAULT_INSTALL)

#: Live, unversioned modules that `--live` adds.
LIVE_MODULES = ("TAOM_Map", "LOTRLOME_Armory")

MERGE_DIRECTIVE = "_replaceWhileMerging"
XS_BOOLEAN = {"true", "false", "1", "0"}
#: xs:boolean's whitespace facet is "collapse", which strips these four and nothing else.
XS_WHITESPACE = " \t\r\n"
MAX_ERRORS_SHOWN = 20

#: TAOM attributes the engine's own XSD does not declare, keyed by (schema id, element name) ->
#: the attribute names allowed on that element. The engine tolerates an undeclared attribute
#: (MBObjectManager.ValidationEventHandler only Debug.Prints and loading continues), so this
#: suppresses exactly the "attribute is not declared" / "not allowed" error for these
#: element+attribute pairs, nothing else: a different undeclared attribute, or this one on a
#: different element, still fails. taom_body_length: Main/Features/MonsterSize (#646, Mike,
#: 2026-09-23, "the monster xml should control the size").
ALLOWED_EXTRA_ATTRIBUTES = {
    ("Monsters", "Monster"): {"taom_body_length"},
}

#: lxml's XSD "attribute not declared" message, e.g. "Element 'Monster', attribute
#: 'taom_body_length': The attribute 'taom_body_length' is not allowed."
_UNDECLARED_ATTRIBUTE_RE = re.compile(r"Element '([^']+)', attribute '([^']+)':.*is not allowed")


def _is_allowed_extra_attribute(message: str, xml_id: str) -> bool:
    match = _UNDECLARED_ATTRIBUTE_RE.search(message)
    if not match:
        return False
    element, attribute = match.group(1), match.group(2)
    return attribute in ALLOWED_EXTRA_ATTRIBUTES.get((xml_id, element), ())

_XS = "{http://www.w3.org/2001/XMLSchema}"
_SCHEMA_REFS = (f"{_XS}include", f"{_XS}import", f"{_XS}redefine")
_THROWS = "the engine throws on it at startup (XmlResource.GetXmlListAndApply)"
_THROWS_MBPROJ = "the engine throws on it at startup (XmlResource.GetMbprojxmls)"


#: The byte order marks `new StreamReader(path)` detects (XmlResource.cs:92), longest first because the
#: UTF-32 little-endian mark begins with the UTF-16 one.
_BOMS = ((b"\xff\xfe\x00\x00", "utf-32-le"), (b"\x00\x00\xfe\xff", "utf-32-be"),
         (b"\xef\xbb\xbf", "utf-8"), (b"\xff\xfe", "utf-16-le"), (b"\xfe\xff", "utf-16-be"))


def _read_like_engine(path) -> str:
    """The text of `path` as `new StreamReader(path)` decodes it: a byte order mark picks its own
    encoding and is dropped, anything else is UTF-8 with an invalid byte read as U+FFFD. A UTF-16 file
    with no mark is therefore UTF-8 text full of NULs, which no XML parser accepts."""
    data = Path(path).read_bytes()
    for bom, codec in _BOMS:
        if data.startswith(bom):
            return data[len(bom):].decode(codec, errors="replace")
    return data.decode("utf-8", errors="replace")


def _parse_like_engine(path):
    """The root element of `path`, read as the engine reads project.mbproj (XmlResource) and
    language_data.xml (LocalizedTextManager): through a StreamReader (`_read_like_engine`), so the
    declared encoding is ignored, a byte order mark is honoured and a file declaring an encoding expat
    does not know still loads (expat ignores the declaration when it is given a str).
    Raises ParseError when the text is not well-formed, which includes any NUL: a BOM-less UTF-16
    file reads as UTF-8 text with a NUL after every character, and .NET rejects `<` followed by NUL
    (a name cannot start with it), where expat would sniff the UTF-16 and parse it. A raw NUL anywhere
    fails in the engine too, so rejecting it matches. Limit: a character reference the engine accepts
    with CheckCharacters=false (such as &#1;) still fails here."""
    text = _read_like_engine(path)
    if "\x00" in text:
        raise ET.ParseError("the text contains a NUL character (a BOM-less UTF-16 file reads as UTF-8 "
                            "with a NUL after every character)")
    return ET.fromstring(text)


def _stylesheet(xsl: Path):
    """The stylesheet MBObjectManager.HandleXsltList queues for `xsl`: that file when it exists,
    else `<xsl>t`, else None."""
    return next((p for p in (xsl, Path(f"{xsl}t")) if p.is_file()), None)


def _registrations(module_dir: Path):
    """(rows, missing) from one walk of a module's SubModule.xml, the loop of
    MBObjectManager.GetMergedXmlForManaged. rows are (xml_id, rel, files, xsls) per <XmlName> the
    engine accepts: the files it loads (ModuleData/<rel>.xml, else the top-level *.xml of the folder
    ModuleData/<rel>, possibly none) and the `.xsl` path HandleXsltList tries for each (the one
    ModuleData/<rel>.xsl without a folder). missing are human-readable registrations that load
    nothing or that the engine throws on, in document order."""
    module_dir = Path(module_dir)
    moduledata = module_dir / "ModuleData"
    submodule = module_dir / "SubModule.xml"
    rows, missing = [], []
    for node in ET.parse(submodule).getroot().iterfind("./Xmls/XmlNode"):
        name = node.find("XmlName")
        if name is None:
            missing.append(f"an <XmlNode> in {submodule} has no <XmlName> child: {_THROWS}")
            continue
        absent = [attr for attr in ("id", "path") if attr not in name.attrib]
        if absent:
            shown = "".join(f' {k}="{v}"' for k, v in name.attrib.items())
            missing.append(f"<XmlName{shown}> in {submodule} has no "
                           f"{' or '.join(repr(a) for a in absent)} attribute: {_THROWS}")
            continue
        xml_id, rel = name.get("id"), name.get("path")
        as_file, as_folder = moduledata / f"{rel}.xml", moduledata / rel
        if as_file.is_file():
            files, xsls = [as_file], [moduledata / f"{rel}.xsl"]
        elif rel and as_folder.is_dir():
            # Deliberately the superset of GetFiles("*.xml"): an extension STARTING with
            # "xml" also matches through an 8.3 short name on volumes that have them.
            files = [p for p in sorted(as_folder.iterdir())
                     if p.is_file() and p.suffix.lower().startswith(".xml")]
            xsls = [Path(str(p).replace(".xml", ".xsl")) for p in files]
            if not files:
                missing.append(f'<XmlName id="{xml_id}" path="{rel}"> in {submodule}: '
                               f"the folder holds no *.xml, so it loads nothing")
        else:
            files, xsls = [], [moduledata / f"{rel}.xsl"]
            if _stylesheet(xsls[0]) is None:  # else XSLT-only: transforms other modules' data
                missing.append(f'<XmlName id="{xml_id}" path="{rel}"> in {submodule} '
                               f"resolves to no file or folder, so it loads nothing")
        rows.append((xml_id, rel, files, xsls))
    return rows, missing


def registered_files(module_dir: Path):
    """(entries, missing): entries are (xml_path, xml_id) the engine loads for this module;
    missing are human-readable registrations that load nothing or that the engine throws on."""
    rows, missing = _registrations(module_dir)
    return [(p, xml_id) for xml_id, _, files, _ in rows for p in files], missing


def applied_stylesheets(module_dir: Path) -> dict:
    """{_key(sheet): (sheet path, xml_id, registered path)} for every stylesheet the engine applies
    for this module's registrations (MBObjectManager.GetMergedXmlForManaged + HandleXsltList).
    The sheet beside a loaded file is that file's path with `.xml` replaced by `.xsl`; a
    registration that resolves to no file or folder looks for `<path>.xsl`. Each lookup takes
    `.xsl` when it exists, else `.xslt`, never both."""
    sheets = {}
    for xml_id, rel, _, xsls in _registrations(module_dir)[0]:
        for xsl in xsls:
            sheet = _stylesheet(xsl)
            if sheet:
                sheets[_key(sheet)] = (sheet, xml_id, rel)
    return sheets


def mbproj_rows(mbproj: Path):
    """(rows, problems) of one project.mbproj, parsed as the engine reads it (XmlResource.GetMbprojxmls).
    rows are `(id, name, type or None)` for each `<file>` under the root `<base>` that has an id and a
    name; problems are human-readable reasons the engine throws at startup, in document order: the file
    does not parse, its root is not <base>, a row has no id or no name (that row is not in rows). A
    path that is not a file has neither."""
    mbproj = Path(mbproj)
    rows: list[tuple[str, str, str | None]] = []
    problems: list[str] = []
    if not mbproj.is_file():
        return rows, problems
    try:
        root = _parse_like_engine(mbproj)
    except (ET.ParseError, OSError, ValueError, LookupError) as exc:
        return rows, [f"{mbproj} cannot be loaded ({exc}): {_THROWS_MBPROJ}"]
    if root.tag != "base":
        return rows, [f"the root of {mbproj} is <{root.tag}>, not <base>: {_THROWS_MBPROJ}"]
    for row in root.iterfind("file"):
        row_id, name = row.get("id"), row.get("name")
        if row_id is None or name is None:
            shown = "".join(f' {k}="{v}"' for k, v in row.attrib.items())
            problems.append(f"<file{shown}> in {mbproj} has no 'id' or no 'name' attribute: "
                            f"{_THROWS_MBPROJ}")
            continue
        rows.append((row_id, name, row.get("type")))
    return rows, problems


def native_files(module_dir: Path):
    """(found, problems). found is {_key(file): (file, row id, "data" or "stylesheet", row type or
    None)} for the files named by this module's `ModuleData/project.mbproj`. The engine reads each
    `<file id name>` row under the root `<base>` (XmlResource.GetMbprojxmls), joins `<module
    folder>/<name>` as strings (ModuleHelper.GetXmlPathForNative: relative to the module, not to
    ModuleData; a leading slash is stripped here as the language code's join does, so it cannot
    escape the module) and looks for `<name minus its last 4 characters>.xsl`, else `.xslt`
    (GetXsltPathForNative, HandleXsltList). problems are human-readable reasons the file cannot be
    loaded, each one a startup throw: it does not parse, its root is not <base>, a row has no id or
    name. A project.mbproj that does not exist has neither files nor problems. A name shorter than the
    4 characters GetXsltPathForNative removes is no problem here: only a requested id reaches that call."""
    module_dir = Path(module_dir)
    mbproj = module_dir / "ModuleData" / "project.mbproj"
    found = {}
    rows, problems = mbproj_rows(mbproj)
    for row_id, name, row_type in rows:
        relative = name.lstrip("/\\")
        data = Path(f"{module_dir}/{relative}")
        if data.is_file():
            found[_key(data)] = (data, row_id, "data", row_type)
        sheet = _stylesheet(Path(f"{module_dir}/{relative[:-4]}.xsl"))
        if sheet:
            found.setdefault(_key(sheet), (sheet, row_id, "stylesheet", row_type))
    return found, problems


def listed_language_files(module_dir: Path):
    """{_key(file): (manifest path, why that manifest loads nothing or None)} for this module.
    The engine finds each `language_data.xml` anywhere under ModuleData/Languages
    (LocalizedTextManager.LoadLocalizationXmls) and joins a `<LanguageFile>` or `<VoiceFile>`
    `xml_path` to ModuleData/Languages itself as a string (`modulePath + "/" + xml_path`), not to
    the manifest's own folder (LanguageData.Deserialize). A manifest is itself scanned, so it is
    listed as its own entry; one it cannot parse, or whose root is not a <LanguageData> with an
    id, loads nothing and carries the reason as the second item."""
    languages = Path(module_dir) / "ModuleData" / "Languages"
    listed = {}
    if not languages.is_dir():
        return listed
    for manifest in sorted(p for p in languages.rglob("*")
                           if p.name.lower() == "language_data.xml" and p.is_file()):
        key = _key(manifest)
        listed[key] = (manifest, None)
        try:
            root = _parse_like_engine(manifest)
        except (ET.ParseError, OSError, ValueError, LookupError) as exc:
            listed[key] = (manifest, f"it does not parse ({exc})")
            continue
        if root.tag != "LanguageData" or not root.get("id"):
            listed[key] = (manifest, "its root is not a <LanguageData> with a non-empty id")
            continue
        for entry in root:
            rel = entry.get("xml_path")
            if entry.tag in ("LanguageFile", "VoiceFile") and rel:
                file_key = _key(languages / rel.lstrip("/\\"))
                listed[file_key] = (manifest, listed[file_key][1] if file_key in listed else None)
    return listed


def schema_path(module_dir: Path, xml_id: str, game_schemas: Path):
    """The XSD the engine validates `xml_id` with, or None when there is none."""
    local = Path(module_dir) / "ModuleData" / "XmlSchemas" / f"{xml_id}.xsd"
    if local.is_file():
        return local
    shared = Path(game_schemas) / f"{xml_id}.xsd"
    return shared if shared.is_file() else None


@functools.lru_cache(maxsize=None)
def _schema(xsd_path: str):
    """(compiled schema, None), or (None, why it cannot be used). A failure is returned as
    a value because lru_cache does not cache an exception: an uncompilable XSD would
    otherwise be recompiled once per file under its id."""
    try:
        tree = etree.parse(xsd_path)
    except (etree.XMLSyntaxError, OSError) as exc:
        return None, f"schema {xsd_path} does not compile: {exc}"
    # The schema compiler follows include / import / redefine schemaLocation on its own,
    # over the network included, whatever the parser flags say. Refuse a URL before it
    # compiles. Only this XSD's own references are read: none of the engine's 51 XSDs
    # references another at all (measured 2026-09-18).
    for ref in tree.iter(*_SCHEMA_REFS):
        location = ref.get("schemaLocation") or ""
        if "://" in location:
            return None, f"schema {xsd_path} includes a remote schema ({location}); refusing to fetch it"
    try:
        return etree.XMLSchema(tree), None
    except (etree.XMLSchemaParseError, etree.XMLSyntaxError) as exc:
        return None, f"schema {xsd_path} does not compile: {exc}"


def validate(xml_path: Path, xsd_path: Path, xml_id: str = None) -> list:
    """Problems with one file, each "L<line>: <message>". Empty list means clean. `xml_id` gates
    ALLOWED_EXTRA_ATTRIBUTES: without it (the default) nothing is suppressed."""
    parser = etree.XMLParser(resolve_entities=False, no_network=True, huge_tree=True)
    try:
        doc = etree.parse(str(xml_path), parser)
    except etree.XMLSyntaxError as exc:
        return [f"L{exc.lineno}: not well-formed: {exc.msg}"]
    except OSError as exc:
        return [f"L0: unreadable: {exc}"]
    if doc.docinfo.doctype:
        return ["L1: has a DOCTYPE: the engine prohibits DTDs (XmlReaderSettings.DtdProcessing), "
                "so this file loads nothing"]
    schema, problem = _schema(str(xsd_path))
    if problem:
        return [f"L0: {problem}"]

    for el in doc.iter(tag=etree.Element):
        value = el.attrib.get(MERGE_DIRECTIVE)
        if value is not None and value.strip(XS_WHITESPACE) in XS_BOOLEAN:
            del el.attrib[MERGE_DIRECTIVE]

    if schema.validate(doc):
        return []
    errors = [f"L{e.line}: {e.message}" for e in schema.error_log]
    if xml_id is not None:
        errors = [e for e in errors if not _is_allowed_extra_attribute(e, xml_id)]
    return errors


def _key(path) -> str:
    # realpath, not abspath: a path through a symlink or junction is the same file.
    return os.path.normcase(os.path.realpath(str(path)))


def module_of(path):
    """The module a file belongs to: the nearest parent folder holding SubModule.xml."""
    for parent in Path(path).resolve().parents:
        if (parent / "SubModule.xml").is_file():
            return parent
    return None


def run(modules, game_schemas: Path, only=None) -> dict:
    """Validate every registered file of `modules` (or only those in `only`)."""
    report = {"checked": [], "failed": [], "no_schema": [], "missing": [], "not_registered": [],
              "language_file": [], "stylesheet": [], "native": []}
    wanted = {_key(p): p for p in only} if only is not None else None
    registrations, submodules, mbprojs = [], set(), set()
    for module_dir in modules:
        entries, missing = registered_files(module_dir)
        submodule = _key(Path(module_dir) / "SubModule.xml")
        submodules.add(submodule)
        # A dead registration is a SubModule.xml defect: it counts when SubModule.xml is
        # under review, never against a review of some other file in the module.
        if wanted is None or submodule in wanted:
            report["missing"].extend(missing)
        registrations.extend((path, xml_id, Path(module_dir)) for path, xml_id in entries)
        mbproj = _key(Path(module_dir) / "ModuleData" / "project.mbproj")
        if wanted is not None and mbproj in wanted:
            # project.mbproj is the file under review: what makes it unloadable is a fault of
            # that file, outside the `unknown` scan below so passing it alone still reports it
            mbprojs.add(mbproj)
            report["missing"].extend(native_files(module_dir)[1])

    if wanted is not None:
        known = {_key(path) for path, _, _ in registrations} | submodules | mbprojs
        unknown = {k: p for k, p in wanted.items() if k not in known}
        sheets, languages, natives = {}, {}, {}
        if unknown:  # a file a registration loads needs no scan
            for module_dir in modules:
                sheets.update(applied_stylesheets(module_dir))
                languages.update(listed_language_files(module_dir))
                natives.update(native_files(module_dir)[0])
        for key, path in unknown.items():
            if key in languages:
                manifest, broken = languages[key]
                report["language_file"].append({"file": str(path), "manifest": str(manifest),
                                                "broken": broken})
            elif key in sheets:
                _, xml_id, rel = sheets[key]
                report["stylesheet"].append({"file": str(path), "id": xml_id, "path": rel})
            elif key in natives:
                _, row_id, kind, row_type = natives[key]
                report["native"].append({"file": str(path), "id": row_id, "kind": kind,
                                         "type": row_type})
            else:
                report["not_registered"].append(str(path))
        registrations = [r for r in registrations if _key(r[0]) in wanted]

    for path, xml_id, module_dir in registrations:
        xsd = schema_path(module_dir, xml_id, game_schemas)
        if xsd is None:
            report["no_schema"].append({"file": str(path), "id": xml_id})
            continue
        errors = validate(path, xsd, xml_id)
        record = {"file": str(path), "id": xml_id, "schema": str(xsd), "errors": errors}
        report["checked"].append(record)
        if errors:
            report["failed"].append(record)
    return report


def _xml_in_own_languages_folder(path) -> bool:
    """True for an .xml file inside its module's own ModuleData/Languages, the only folder the engine
    scans for manifests (LocalizedTextManager.LoadLocalizationXmls), however the path is spelled."""
    resolved = Path(path).resolve()
    module = module_of(resolved)
    return (resolved.suffix.lower() == ".xml" and module is not None
            and (module / "ModuleData" / "Languages").resolve() in resolved.parents)


def _print(report: dict, by_name: bool) -> bool:
    """Print the report; True when the run is bad: a file failed, a registration loads nothing, or a
    by-name run printed "(a real fault)"."""
    faults = 0
    for rec in report["failed"]:
        print(f"FAIL  {rec['file']}  ({Path(rec['schema']).name})")
        for line in rec["errors"][:MAX_ERRORS_SHOWN]:
            print(f"      {line}")
        extra = len(rec["errors"]) - MAX_ERRORS_SHOWN
        if extra > 0:
            print(f"      ... {extra} more")
    for line in report["missing"]:
        print(f"MISSING  {line}")
    for rec in report["language_file"]:
        if rec.get("broken"):
            print(f"LANGUAGE FILE  {rec['file']}: the engine loads nothing from it: {rec['broken']} "
                  f"(a real fault)")
            faults += 1
            continue
        print(f"LANGUAGE FILE  {rec['file']}: loaded through {Path(rec['manifest']).name} "
              f"({rec['manifest']}); the engine checks it against no schema")
    for rec in report["stylesheet"]:
        print(f"STYLESHEET  {rec['file']}: queued for the <XmlName id=\"{rec['id']}\" "
              f"path=\"{rec['path']}\"> registration (MBObjectManager.HandleXsltList) and applied to "
              f"the data merged before it; the engine checks it against no schema")
    for rec in report["native"]:
        print(f"NATIVE  {rec['file']}: named by project.mbproj row id=\"{rec['id']}\" "
              f"type=\"{rec['type'] or '-'}\" ({rec['kind']}); it loads only if the engine requests that "
              f"id or reads that type; no XSD here checks it; gates: python tools/data_gates.py")
    for path in report["not_registered"]:
        if _xml_in_own_languages_folder(path):
            faults += 1
            reason = ("it sits under ModuleData/Languages but no language_data.xml the engine loads "
                      "lists it, so the engine never loads it (a real fault)")
        else:
            reason = ("if the engine reads it at all, it is only by fixed name (global_strings.xml, "
                      "the scene lists, ModuleData/project.mbproj itself), which this tool does not "
                      "check (expected for TAOM's own C#-read data files)")
        print(f"NOT REGISTERED  {path}: no scanned SubModule.xml <XmlName> or language manifest "
              f"loads it and no project.mbproj row names it; {reason}")
    if report["no_schema"]:
        ids = sorted({r["id"] for r in report["no_schema"]})
        print(f"NO SCHEMA  {len(report['no_schema'])} file(s) under ids the engine ships no XSD for: "
              f"{', '.join(ids)}")
    bad = bool(report["failed"] or report["missing"] or faults)
    # The buckets fill only from files passed by name; a whole-module run classifies none.
    buckets = (f", {len(report['language_file'])} language file(s), {len(report['stylesheet'])} "
               f"stylesheet(s), {len(report['native'])} native, {len(report['not_registered'])} not "
               f"registered, {faults} real fault(s) named above") if by_name else ""
    print(f"\n{'FAIL' if bad else 'PASS'}: {len(report['checked'])} file(s) validated, "
          f"{len(report['failed'])} failed, {len(report['missing'])} registration(s) resolve to nothing"
          f"{buckets}")
    return bad


def main(argv=None) -> int:
    for stream in (sys.stdout, sys.stderr):  # a path the console cannot encode must not crash the run
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="backslashreplace")
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*",
                    help="only report these files (each is validated under its own module)")
    ap.add_argument("--module", action="append", default=None,
                    help="module folder holding SubModule.xml (repeatable; default: the repo module)")
    ap.add_argument("--live", action="store_true",
                    help=f"also scan the live {' and '.join(LIVE_MODULES)} installs")
    ap.add_argument("--schemas", default=str(GAME_DIR / "XmlSchemas"),
                    help="the engine's XmlSchemas folder")
    args = ap.parse_args(argv)

    if not HAVE_LXML:
        print("ERROR: lxml not installed; nothing was validated. Install it with: "
              "python -m pip install lxml", file=sys.stderr)
        return 2

    schemas = Path(args.schemas)
    if not schemas.is_dir():
        print(f"ERROR: engine schema folder not found: {schemas}\n"
              f"       Set BANNERLORD_GAME_DIR or pass --schemas. Nothing was validated.",
              file=sys.stderr)
        return 2

    only = [Path(f) for f in args.files] if args.files else None
    for path in only or []:
        if not path.is_file():
            print(f"ERROR: not a file: {path}. Nothing was validated.", file=sys.stderr)
            return 2

    modules = [Path(m) for m in (args.module or [str(REPO_MODULE)])]
    if args.live:
        if not DEFAULT_GAME_MODULES.is_dir():
            print(f"ERROR: Bannerlord Modules folder not found: {DEFAULT_GAME_MODULES}\n"
                  f"       Set BANNERLORD_GAME_DIR (or BANNERLORD_GAME_MODULES). "
                  f"Nothing was validated.", file=sys.stderr)
            return 2
        modules += [DEFAULT_GAME_MODULES / name for name in LIVE_MODULES]
    # A file from a module nobody named would otherwise read as NOT REGISTERED and PASS.
    modules += [m for m in (module_of(p) for p in only or []) if m is not None]
    seen, unique = set(), []
    for module_dir in modules:
        if _key(module_dir) not in seen:
            seen.add(_key(module_dir))
            unique.append(module_dir)

    for module_dir in unique:
        submodule = module_dir / "SubModule.xml"
        if not submodule.is_file():
            print(f"ERROR: no SubModule.xml in {module_dir}. Nothing was validated.", file=sys.stderr)
            return 2
        try:
            ET.parse(submodule)
        except (ET.ParseError, ValueError, LookupError) as exc:
            print(f"ERROR: {submodule} is not well-formed ({exc}). Nothing was validated.",
                  file=sys.stderr)
            return 2
        except OSError as exc:
            print(f"ERROR: {submodule} could not be read ({exc}). Nothing was validated.",
                  file=sys.stderr)
            return 2

    report = run(unique, schemas, only)
    return 1 if _print(report, by_name=only is not None) else 0


if __name__ == "__main__":
    sys.exit(main())
