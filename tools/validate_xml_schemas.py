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

Out of scope: files loaded through `project.mbproj` (native data such as action sets; see
`audit_mbproj_registration.py` and `audit_action_set_parity.py`) and TAOM's own data files
that C# reads directly (Careers, AbilityTemplates, ...). Pass one of those explicitly and
it is listed as NOT REGISTERED, so it is named rather than silently skipped.

Usage:
    python tools/validate_xml_schemas.py                       # the repo module
    python tools/validate_xml_schemas.py --live                # + TAOM_Map, LOTRLOME_Armory
    python tools/validate_xml_schemas.py FILE [FILE ...]       # only these files
    python tools/validate_xml_schemas.py --module DIR [--schemas DIR]

A FILE is validated under its own module (the nearest parent folder holding SubModule.xml)
even when --module does not name it. --live reads the Modules folder from
$BANNERLORD_GAME_MODULES, else $BANNERLORD_GAME_DIR/Modules (tools/_gamedir.py).

Exit codes: 0 clean, 1 a file failed or a registration loads nothing, 2 bad input (lxml
not installed, no schema folder, no module or no Modules folder for --live, a FILE that is
not a file, a malformed SubModule.xml), so a partial run never reports clean.
"""
from __future__ import annotations

import argparse
import functools
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# lxml is the only non-stdlib dependency and CI's tools-tests job installs nothing, so it
# is optional at import time (check_external_xslt.py's convention). Without it nothing
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


def registered_files(module_dir: Path):
    """(entries, missing): entries are (xml_path, xml_id) the engine loads for this module;
    missing are human-readable registrations that load nothing or that the engine throws on."""
    module_dir = Path(module_dir)
    moduledata = module_dir / "ModuleData"
    submodule = module_dir / "SubModule.xml"
    entries, missing = [], []
    root = ET.parse(submodule).getroot()
    for node in root.iterfind("./Xmls/XmlNode"):
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
        as_file = moduledata / f"{rel}.xml"
        as_folder = moduledata / rel
        if as_file.is_file():
            entries.append((as_file, xml_id))
        elif rel and as_folder.is_dir():
            # Deliberately the superset of GetFiles("*.xml"): an extension STARTING with
            # "xml" also matches through an 8.3 short name on volumes that have them.
            found = [p for p in sorted(as_folder.iterdir())
                     if p.is_file() and p.suffix.lower().startswith(".xml")]
            if not found:
                missing.append(f'<XmlName id="{xml_id}" path="{rel}"> in {submodule}: '
                               f"the folder holds no *.xml, so it loads nothing")
            entries.extend((p, xml_id) for p in found)
        elif any((moduledata / f"{rel}{ext}").is_file() for ext in (".xsl", ".xslt")):
            continue  # XSLT-only: transforms other modules' data, has no XML of its own
        else:
            missing.append(f'<XmlName id="{xml_id}" path="{rel}"> in {submodule} '
                           f"resolves to no file or folder, so it loads nothing")
    return entries, missing


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
    report = {"checked": [], "failed": [], "no_schema": [], "missing": [], "not_registered": []}
    wanted = {_key(p): p for p in only} if only is not None else None
    registrations, submodules = [], set()
    for module_dir in modules:
        entries, missing = registered_files(module_dir)
        submodule = _key(Path(module_dir) / "SubModule.xml")
        submodules.add(submodule)
        # A dead registration is a SubModule.xml defect: it counts when SubModule.xml is
        # under review, never against a review of some other file in the module.
        if wanted is None or submodule in wanted:
            report["missing"].extend(missing)
        registrations.extend((path, xml_id, Path(module_dir)) for path, xml_id in entries)

    if wanted is not None:
        known = {_key(path) for path, _, _ in registrations} | submodules
        report["not_registered"] = [str(p) for k, p in wanted.items() if k not in known]
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


def _print(report: dict) -> None:
    for rec in report["failed"]:
        print(f"FAIL  {rec['file']}  ({Path(rec['schema']).name})")
        for line in rec["errors"][:MAX_ERRORS_SHOWN]:
            print(f"      {line}")
        extra = len(rec["errors"]) - MAX_ERRORS_SHOWN
        if extra > 0:
            print(f"      ... {extra} more")
    for line in report["missing"]:
        print(f"MISSING  {line}")
    for path in report["not_registered"]:
        print(f"NOT REGISTERED  {path}: no scanned SubModule.xml <XmlName> loads it, so the engine "
              f"never reads or validates it (expected for TAOM's own C#-read data files)")
    if report["no_schema"]:
        ids = sorted({r["id"] for r in report["no_schema"]})
        print(f"NO SCHEMA  {len(report['no_schema'])} file(s) under ids the engine ships no XSD for: "
              f"{', '.join(ids)}")
    bad = bool(report["failed"] or report["missing"])
    print(f"\n{'FAIL' if bad else 'PASS'}: {len(report['checked'])} file(s) validated, "
          f"{len(report['failed'])} failed, {len(report['missing'])} registration(s) resolve to nothing, "
          f"{len(report['not_registered'])} not registered")


def main(argv=None) -> int:
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
        except ET.ParseError as exc:
            print(f"ERROR: {submodule} is not well-formed ({exc}). Nothing was validated.",
                  file=sys.stderr)
            return 2
        except OSError as exc:
            print(f"ERROR: {submodule} could not be read ({exc}). Nothing was validated.",
                  file=sys.stderr)
            return 2

    report = run(unique, schemas, only)
    _print(report)
    return 1 if report["failed"] or report["missing"] else 0


if __name__ == "__main__":
    sys.exit(main())
