#!/usr/bin/env python3
"""Unit tests for tools/validate_xml_schemas.py.

The gap this tool closes: `validate_moduledata.py` resolves cross-references but never
checks a file against the engine's own XSD, so an element missing a required attribute
(`clan_umbar_3` shipped with no `initial_home_settlement`) passes every gate. The engine
does validate, but only prints the failure to its log and loads the file anyway.

Every test builds a SYNTHETIC module tree and schema folder in a tempdir, so the suite
never needs a game install. Each guard is tested in both directions, because a check that
has only ever been seen passing is not known to be able to fail.
"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import validate_xml_schemas as vx  # noqa: E402

# CI's tools-tests job installs nothing, so lxml is absent there. Registration and
# schema-choice logic is stdlib and always runs; only what compiles or applies an XSD
# needs lxml.
NEEDS_LXML = unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")

FACTIONS_XSD = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Factions">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Faction" minOccurs="0" maxOccurs="unbounded">
          <xs:complexType>
            <xs:attribute name="id" type="xs:string" use="required"/>
            <xs:attribute name="initial_home_settlement" type="xs:string" use="required"/>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>
"""

GOOD_CLANS = """<?xml version="1.0" encoding="utf-8"?>
<Factions>
  <Faction id="clan_a" initial_home_settlement="Settlement.town_A1"/>
</Factions>
"""

SUBMODULE = """<?xml version="1.0" encoding="utf-8"?>
<Module>
  <Xmls>
    <XmlNode><XmlName id="Factions" path="characters/clans"/></XmlNode>
    <XmlNode><XmlName id="Factions" path="more_clans"/></XmlNode>
  </Xmls>
</Module>
"""


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _link_dir(target: Path, link: Path) -> bool:
    """A directory link at `link`: a symlink, else (Windows without the symlink privilege)
    a junction, which needs no privilege. False when neither can be made."""
    try:
        os.symlink(target, link, target_is_directory=True)
        return True
    except (OSError, NotImplementedError):
        pass
    try:
        import _winapi
        _winapi.CreateJunction(str(target), str(link))
        return True
    except (ImportError, AttributeError, OSError):
        return False


SINGLE_REGISTRATION = """<?xml version="1.0" encoding="utf-8"?>
<Module><Xmls><XmlNode><XmlName id="Factions" path="characters/clans"/></XmlNode></Xmls></Module>
"""

BROKEN_CLANS = GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', "")


class _Tree(unittest.TestCase):
    """A module with one file registration and one folder registration."""

    def setUp(self):
        # The compiled-schema cache is keyed by path; a temp path can repeat across tests.
        vx._schema.cache_clear()
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.module = root / "Modules" / "TestMod"
        self.md = self.module / "ModuleData"
        self.schemas = root / "XmlSchemas"
        _write(self.schemas / "Factions.xsd", FACTIONS_XSD)
        _write(self.module / "SubModule.xml", SUBMODULE)
        self.clans = self.md / "characters" / "clans.xml"
        _write(self.clans, GOOD_CLANS)
        # A folder registration: the engine globs its top level only.
        _write(self.md / "more_clans" / "north.xml", GOOD_CLANS)
        _write(self.md / "more_clans" / "nested" / "ignored.xml", "<not even xml")

    def tearDown(self):
        self._tmp.cleanup()

    def _errors(self, text: str) -> list:
        _write(self.clans, text)
        return vx.validate(self.clans, self.schemas / "Factions.xsd")


class RegistrationTests(_Tree):
    def test_file_and_folder_registrations_resolve_like_the_engine(self):
        entries, missing = vx.registered_files(self.module)
        paths = sorted(p.name for p, _ in entries)
        self.assertEqual(paths, ["clans.xml", "north.xml"],
                         "a folder registration loads its top-level *.xml only")
        self.assertEqual({i for _, i in entries}, {"Factions"})
        self.assertEqual(missing, [])

    def test_xslt_only_registration_is_not_missing(self):
        """No .xml and no folder means the engine looks for `<path>.xsl`, then `.xslt`
        (MBObjectManager.HandleXsltList): the registration transforms other modules' data.
        spcultures, spclans and lords in the repo are all this shape."""
        for ext in (".xslt", ".xsl"):
            with self.subTest(ext=ext):
                _write(self.module / "SubModule.xml", SUBMODULE.replace("more_clans", "spclans"))
                sheet = self.md / f"spclans{ext}"
                _write(sheet, "<xsl:stylesheet/>")
                entries, missing = vx.registered_files(self.module)
                self.assertEqual(missing, [])
                self.assertEqual(sorted(p.name for p, _ in entries), ["clans.xml"],
                                 "a stylesheet is not validated against the data schema")
                sheet.unlink()

    def test_registration_that_resolves_to_nothing_is_reported(self):
        _write(self.module / "SubModule.xml", SUBMODULE.replace("more_clans", "typo_clans"))
        _, missing = vx.registered_files(self.module)
        self.assertEqual(len(missing), 1)
        self.assertIn("typo_clans", missing[0])

    def test_registration_shapes_the_engine_throws_on_are_reported(self):
        """XmlResource.GetXmlListAndApply throws at startup on an <XmlNode> with no
        <XmlName>, and on an <XmlName> with no `id` or no `path` attribute."""
        shapes = {
            "no XmlName": ('<XmlNode><IncludedGameTypes/></XmlNode>', "<XmlName>"),
            "no id": ('<XmlNode><XmlName path="characters/clans"/></XmlNode>', "'id'"),
            "no path": ('<XmlNode><XmlName id="Factions"/></XmlNode>', "'path'"),
        }
        for label, (node, names) in shapes.items():
            with self.subTest(label):
                _write(self.module / "SubModule.xml", f"<Module><Xmls>{node}</Xmls></Module>")
                entries, missing = vx.registered_files(self.module)
                self.assertEqual(entries, [], "a registration the engine throws on loads nothing")
                self.assertEqual(len(missing), 1, missing)
                self.assertIn(names, missing[0])
                self.assertIn("throws", missing[0])

    def test_folder_with_no_xml_loads_nothing_and_is_reported(self):
        """The engine takes the folder branch whenever the folder exists, so a folder with
        no *.xml loads nothing (and never falls back to a stylesheet)."""
        (self.md / "more_clans" / "north.xml").unlink()
        _write(self.md / "more_clans" / "readme.txt", "notes")
        entries, missing = vx.registered_files(self.module)
        self.assertEqual(sorted(p.name for p, _ in entries), ["clans.xml"])
        self.assertEqual(len(missing), 1, missing)
        self.assertIn("more_clans", missing[0])
        self.assertIn("no *.xml", missing[0])

    def test_folder_glob_is_the_superset_of_what_any_volume_loads(self):
        """.NET Framework's GetFiles("*.xml") also matches `foo.xmlbak` on a volume with 8.3
        short names (FOO~1.XML). The tool takes that superset so a backup that would load
        on a player's volume is never missed; `foo.xml.bak` loads nowhere."""
        _write(self.md / "more_clans" / "south.xmlbak", GOOD_CLANS)
        _write(self.md / "more_clans" / "west.xml.bak", GOOD_CLANS)
        entries, _ = vx.registered_files(self.module)
        self.assertEqual(sorted(p.name for p, _ in entries), ["clans.xml", "north.xml", "south.xmlbak"])


class SchemaChoiceTests(_Tree):
    def test_game_schema_is_used_when_the_module_ships_none(self):
        self.assertEqual(vx.schema_path(self.module, "Factions", self.schemas),
                         self.schemas / "Factions.xsd")

    def test_module_local_schema_wins_over_the_game_schema(self):
        local = self.md / "XmlSchemas" / "Factions.xsd"
        _write(local, FACTIONS_XSD)
        self.assertEqual(vx.schema_path(self.module, "Factions", self.schemas), local)

    def test_id_with_no_schema_anywhere_is_none(self):
        self.assertIsNone(vx.schema_path(self.module, "CareerChoices", self.schemas))


@NEEDS_LXML
class ValidationTests(_Tree):
    def test_valid_file_passes(self):
        self.assertEqual(self._errors(GOOD_CLANS), [])

    def test_missing_required_attribute_fails_with_line(self):
        errors = self._errors(GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        self.assertEqual(len(errors), 1)
        self.assertIn("initial_home_settlement", errors[0])
        self.assertTrue(errors[0].startswith("L3:"), errors[0])

    def test_unknown_attribute_fails(self):
        errors = self._errors(GOOD_CLANS.replace("/>", ' max_prosperity="5"/>'))
        self.assertEqual(len(errors), 1)
        self.assertIn("max_prosperity", errors[0])

    def test_replace_while_merging_is_allowed_like_the_engine(self):
        """The engine injects `_replaceWhileMerging` as an optional boolean into every
        complex type before validating (MBObjectManager.LoadXmlWithValidation)."""
        self.assertEqual(self._errors(GOOD_CLANS.replace("/>", ' _replaceWhileMerging="true"/>')), [])

    def test_replace_while_merging_must_still_be_boolean(self):
        errors = self._errors(GOOD_CLANS.replace("/>", ' _replaceWhileMerging="maybe"/>'))
        self.assertEqual(len(errors), 1)
        self.assertIn("_replaceWhileMerging", errors[0])

    def test_other_underscore_attributes_are_not_exempt(self):
        errors = self._errors(GOOD_CLANS.replace("/>", ' _skipMerge="true"/>'))
        self.assertEqual(len(errors), 1)

    def test_malformed_xml_fails(self):
        errors = self._errors("<Factions><Faction id='x'></Factions>")
        self.assertEqual(len(errors), 1)
        self.assertIn("not well-formed", errors[0])

    def test_replace_while_merging_lexical_forms_follow_xs_boolean(self):
        """xs:boolean's whitespace collapse strips only space, tab, CR and LF, and its
        lexical space is exactly true/false/1/0. A NBSP or an upper-case TRUE is not a
        boolean, so the engine reports it and the exemption must not hide it."""
        cases = {" true ": True, "1": True, "TRUE": False, "\xa0true": False}
        for value, exempt in cases.items():
            with self.subTest(value=repr(value)):
                errors = self._errors(GOOD_CLANS.replace("/>", f' _replaceWhileMerging="{value}"/>'))
                self.assertEqual(errors == [], exempt, errors)

    def test_replace_while_merging_directive_is_case_sensitive(self):
        errors = self._errors(GOOD_CLANS.replace("/>", ' _ReplaceWhileMerging="true"/>'))
        self.assertEqual(len(errors), 1)
        self.assertIn("_ReplaceWhileMerging", errors[0])

    def test_doctype_fails_because_the_engine_prohibits_dtds(self):
        """LoadXmlWithValidation reads with a default XmlReaderSettings (DtdProcessing
        Prohibit), so a DOCTYPE throws inside a bare catch and the file loads nothing."""
        errors = self._errors(GOOD_CLANS.replace("<Factions>", "<!DOCTYPE Factions>\n<Factions>", 1))
        self.assertEqual(len(errors), 1, errors)
        self.assertTrue(errors[0].startswith("L1:"), errors[0])
        self.assertIn("DOCTYPE", errors[0])
        self.assertIn("loads nothing", errors[0])

    def test_unreadable_file_is_an_error_not_a_traceback(self):
        errors = vx.validate(self.md / "vanished.xml", self.schemas / "Factions.xsd")
        self.assertEqual(len(errors), 1, errors)
        self.assertTrue(errors[0].startswith("L0: unreadable:"), errors[0])

    def test_remote_schema_include_is_refused_without_fetching(self):
        """The schema compiler follows include/import/redefine schemaLocation whatever the
        parser flags say, over the network included. Refuse before compiling."""
        refs = {
            "include": '<xs:include schemaLocation="http://127.0.0.1:9/evil.xsd"/>',
            "import": '<xs:import namespace="urn:x" schemaLocation="https://127.0.0.1:9/x.xsd"/>',
            "redefine": '<xs:redefine schemaLocation="ftp://127.0.0.1:9/r.xsd"/>',
        }
        for kind, ref in refs.items():
            with self.subTest(kind):
                vx._schema.cache_clear()
                xsd = self.schemas / f"Remote_{kind}.xsd"
                _write(xsd, FACTIONS_XSD.replace('<xs:element name="Factions">',
                                                 f'{ref}\n  <xs:element name="Factions">', 1))
                with mock.patch.object(vx.etree, "XMLSchema",
                                       side_effect=AssertionError("compiled")) as compile_:
                    errors = vx.validate(self.clans, xsd)
                compile_.assert_not_called()
                self.assertEqual(len(errors), 1, errors)
                self.assertIn("includes a remote schema", errors[0])
                self.assertIn("127.0.0.1:9", errors[0])

    def test_local_schema_include_still_compiles(self):
        """The other direction: a relative include is ordinary XSD and is followed."""
        _write(self.schemas / "part.xsd", """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:complexType name="FactionType">
    <xs:attribute name="id" type="xs:string" use="required"/>
    <xs:attribute name="initial_home_settlement" type="xs:string" use="required"/>
  </xs:complexType>
</xs:schema>
""")
        split = self.schemas / "Split.xsd"
        _write(split, """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:include schemaLocation="part.xsd"/>
  <xs:element name="Factions">
    <xs:complexType><xs:sequence>
      <xs:element name="Faction" type="FactionType" minOccurs="0" maxOccurs="unbounded"/>
    </xs:sequence></xs:complexType>
  </xs:element>
</xs:schema>
""")
        self.assertEqual(vx.validate(self.clans, split), [])
        _write(self.clans, BROKEN_CLANS)
        self.assertEqual(len(vx.validate(self.clans, split)), 1)


@NEEDS_LXML
class RunTests(_Tree):
    def test_clean_tree_reports_every_registered_file(self):
        report = vx.run([self.module], self.schemas)
        self.assertEqual(sorted(Path(r["file"]).name for r in report["checked"]),
                         ["clans.xml", "north.xml"])
        self.assertEqual(report["failed"], [])

    def test_broken_file_fails_the_run(self):
        _write(self.clans, GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        report = vx.run([self.module], self.schemas)
        self.assertEqual([Path(r["file"]).name for r in report["failed"]], ["clans.xml"])

    def test_id_without_schema_is_reported_not_failed(self):
        _write(self.module / "SubModule.xml", SUBMODULE.replace('id="Factions" path="more_clans"',
                                                                 'id="CareerChoices" path="more_clans"'))
        report = vx.run([self.module], self.schemas)
        self.assertEqual([Path(r["file"]).name for r in report["no_schema"]], ["north.xml"])
        self.assertEqual(report["failed"], [])

    def test_only_filter_limits_the_report(self):
        report = vx.run([self.module], self.schemas, only=[self.clans])
        self.assertEqual([Path(r["file"]).name for r in report["checked"]], ["clans.xml"])

    def test_dead_registration_counts_only_when_submodule_is_under_review(self):
        """A registration that loads nothing is a SubModule.xml defect. A review of one data
        file must not fail on the live TAOM_Map's pre-existing dead registrations; a review
        that names SubModule.xml must see them."""
        _write(self.module / "SubModule.xml", SUBMODULE.replace("more_clans", "typo_clans"))
        report = vx.run([self.module], self.schemas, only=[self.clans])
        self.assertEqual(report["missing"], [])
        report = vx.run([self.module], self.schemas, only=[self.clans, self.module / "SubModule.xml"])
        self.assertEqual(len(report["missing"]), 1)
        self.assertEqual(report["not_registered"], [], "SubModule.xml itself is not a data file")

    def test_unregistered_file_is_named_not_silently_dropped(self):
        stray = self.md / "custom_settlements.xml"
        _write(stray, GOOD_CLANS)
        report = vx.run([self.module], self.schemas, only=[stray])
        self.assertEqual(report["checked"], [])
        self.assertEqual([Path(p).name for p in report["not_registered"]], ["custom_settlements.xml"])

    def test_path_through_a_link_matches_its_registration(self):
        """A path spelled through a symlink or junction is the same file as its
        registration; comparing unresolved paths would call it NOT REGISTERED."""
        link = self.module.parent / "LinkedMod"
        if not _link_dir(self.module, link):
            self.skipTest("neither a symlink nor a junction can be created here")
        report = vx.run([self.module], self.schemas,
                        only=[link / "ModuleData" / "characters" / "clans.xml"])
        self.assertEqual(report["not_registered"], [])
        self.assertEqual([Path(r["file"]).name for r in report["checked"]], ["clans.xml"])

    def test_one_file_under_two_ids_is_validated_under_both(self):
        _write(self.schemas / "Clans2.xsd", FACTIONS_XSD)
        _write(self.module / "SubModule.xml", SUBMODULE.replace(
            'id="Factions" path="more_clans"', 'id="Clans2" path="characters/clans"'))
        report = vx.run([self.module], self.schemas)
        self.assertEqual(sorted(r["id"] for r in report["checked"]), ["Clans2", "Factions"])
        self.assertEqual({Path(r["file"]).name for r in report["checked"]}, {"clans.xml"})

    def test_uncompilable_schema_fails_every_file_and_compiles_once(self):
        """lru_cache does not cache an exception, so a failure must be cached as a value
        or the broken XSD is recompiled once per file."""
        _write(self.schemas / "Factions.xsd", FACTIONS_XSD.replace('type="xs:string"', 'type="NoSuchType"', 1))
        with mock.patch.object(vx.etree, "XMLSchema", wraps=vx.etree.XMLSchema) as compile_:
            report = vx.run([self.module], self.schemas)
        self.assertEqual(sorted(Path(r["file"]).name for r in report["failed"]), ["clans.xml", "north.xml"])
        for rec in report["failed"]:
            self.assertEqual(len(rec["errors"]), 1)
            self.assertIn("does not compile", rec["errors"][0])
        self.assertEqual(compile_.call_count, 1)


def _run_main(*argv):
    out, err = io.StringIO(), io.StringIO()
    with redirect_stdout(out), redirect_stderr(err):
        code = vx.main(list(argv))
    return code, out.getvalue() + err.getvalue()


class NoLxmlTests(_Tree):
    """Runs everywhere, CI included: without lxml nothing can be validated, so the CLI
    must say so and exit 2 (bad input), never 1 (a file failed) or 0 (clean)."""

    def test_cli_exits_two_and_says_nothing_was_validated(self):
        with mock.patch.object(vx, "HAVE_LXML", False):
            code, text = _run_main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 2)
        self.assertIn("lxml not installed; nothing was validated", text)


@NEEDS_LXML
class CliTests(_Tree):
    def _main(self, *argv):
        return _run_main(*argv)

    def test_clean_run_exits_zero(self):
        code, _ = self._main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 0)

    def test_failure_exits_one_and_names_the_file(self):
        _write(self.clans, GOOD_CLANS.replace(' initial_home_settlement="Settlement.town_A1"', ""))
        code, text = self._main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 1)
        self.assertIn("clans.xml", text)

    def test_missing_registration_target_exits_one(self):
        _write(self.module / "SubModule.xml", SUBMODULE.replace("more_clans", "typo_clans"))
        code, text = self._main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 1)
        self.assertIn("typo_clans", text)

    def test_absent_schema_folder_exits_two_never_clean(self):
        code, _ = self._main("--module", str(self.module),
                             "--schemas", str(self.schemas.parent / "nowhere"))
        self.assertEqual(code, 2)

    def _cli(self, *extra):
        return self._main("--module", str(self.module), "--schemas", str(self.schemas), *extra)

    def test_a_file_argument_that_is_not_a_file_exits_two(self):
        """A typo'd path or a folder must not read as "PASS: 0 file(s) validated"."""
        for label, arg in (("nonexistent", self.md / "typo.xml"), ("directory", self.md)):
            with self.subTest(label):
                code, text = self._cli(str(arg))
                self.assertEqual(code, 2, text)
                self.assertIn(f"not a file: {arg}", text)
                self.assertIn("Nothing was validated", text)
                self.assertNotIn("PASS", text)

    def test_summary_counts_files_not_registered(self):
        stray = self.md / "custom_settlements.xml"
        _write(stray, GOOD_CLANS)
        code, text = self._cli(str(stray))
        self.assertEqual(code, 0, "an unregistered file is named, not failed")
        self.assertIn("NOT REGISTERED", text)
        self.assertIn("1 not registered", text.splitlines()[-1])

    def test_malformed_submodule_exits_two_not_a_traceback(self):
        _write(self.module / "SubModule.xml", "<Module><Xmls>")
        code, text = self._cli()
        self.assertEqual(code, 2)
        self.assertIn("is not well-formed", text)
        self.assertIn("Nothing was validated", text)

    def test_a_file_from_an_unscanned_module_is_validated_under_its_own_module(self):
        """A file passed from a module --module does not name must not read as PASS. The
        tool finds its module (the nearest parent holding SubModule.xml) and scans it."""
        other = self.module.parent / "OtherMod"
        _write(other / "SubModule.xml", SINGLE_REGISTRATION)
        bad = other / "ModuleData" / "characters" / "clans.xml"
        _write(bad, BROKEN_CLANS)
        code, text = self._cli(str(bad))
        self.assertEqual(code, 1, text)
        self.assertIn("FAIL", text)
        self.assertIn("OtherMod", text)
        self.assertNotIn("NOT REGISTERED", text)

    def test_a_file_its_own_module_does_not_register_stays_not_registered(self):
        """The other direction of module inference: finding the module is not the same
        as registering the file."""
        stray = self.md / "custom_settlements.xml"
        _write(stray, BROKEN_CLANS)
        code, text = self._cli(str(stray))
        self.assertEqual(code, 0, text)
        self.assertIn("NOT REGISTERED", text)

    def test_live_reads_the_modules_folder_from_gamedir(self):
        """--live resolves the Modules folder through _gamedir.game_modules, so
        BANNERLORD_GAME_MODULES is honoured like every sibling tool."""
        game_modules = Path(self._tmp.name) / "OtherInstall" / "Modules"
        for name in vx.LIVE_MODULES:
            _write(game_modules / name / "SubModule.xml", SINGLE_REGISTRATION)
            _write(game_modules / name / "ModuleData" / "characters" / "clans.xml", GOOD_CLANS)
        _write(game_modules / "TAOM_Map" / "ModuleData" / "characters" / "clans.xml", BROKEN_CLANS)
        with mock.patch.object(vx, "DEFAULT_GAME_MODULES", game_modules):
            code, text = self._cli("--live")
        self.assertEqual(code, 1, text)
        self.assertIn(str(game_modules / "TAOM_Map"), text)
        self.assertIn("4 file(s) validated", text)

    def test_live_without_an_install_exits_two_naming_the_variables(self):
        with mock.patch.object(vx, "DEFAULT_GAME_MODULES", Path(self._tmp.name) / "nowhere" / "Modules"):
            code, text = self._cli("--live")
        self.assertEqual(code, 2)
        self.assertIn("BANNERLORD_GAME_DIR", text)
        self.assertIn("BANNERLORD_GAME_MODULES", text)
        self.assertIn("Nothing was validated", text)

    def test_module_folder_without_submodule_exits_two(self):
        empty = self.module.parent / "NotAModule"
        empty.mkdir()
        code, text = self._main("--module", str(empty), "--schemas", str(self.schemas))
        self.assertEqual(code, 2)
        self.assertIn("no SubModule.xml", text)

    def test_no_schema_line_names_the_ids(self):
        _write(self.module / "SubModule.xml", SUBMODULE.replace('id="Factions" path="more_clans"',
                                                                 'id="CareerChoices" path="more_clans"'))
        code, text = self._cli()
        self.assertEqual(code, 0)
        self.assertIn("NO SCHEMA  1 file(s) under ids the engine ships no XSD for: CareerChoices", text)

    def test_errors_past_the_display_cap_are_counted(self):
        extra = 3
        factions = "\n".join(f'  <Faction id="clan_{n}"/>' for n in range(vx.MAX_ERRORS_SHOWN + extra))
        _write(self.clans, f"<Factions>\n{factions}\n</Factions>\n")
        code, text = self._cli()
        self.assertEqual(code, 1)
        self.assertIn(f"... {extra} more", text)
        # Factions start on line 2, so the cap-th error shown is on line cap + 1.
        self.assertIn(f"L{vx.MAX_ERRORS_SHOWN + 1}:", text)
        self.assertNotIn(f"L{vx.MAX_ERRORS_SHOWN + 2}:", text)


@NEEDS_LXML
class RepoBaselineTests(unittest.TestCase):
    """The shipped repo module against the installed engine's own XSDs: every registered
    file validates and every registration loads something. Skipped, never faked, without
    the engine's XmlSchemas folder."""

    def test_repo_module_is_schema_clean(self):
        schemas = Path(vx.GAME_DIR) / "XmlSchemas"
        if not schemas.is_dir():
            self.skipTest(f"no engine XmlSchemas folder at {schemas}")
        vx._schema.cache_clear()
        report = vx.run([vx.REPO_MODULE], schemas)
        self.assertGreater(len(report["checked"]), 0, "a run that checked nothing is not clean")
        self.assertEqual(report["missing"], [])
        failures = "\n".join(f"{r['file']}: {r['errors'][:3]}" for r in report["failed"])
        self.assertEqual(report["failed"], [], "\n" + failures)


if __name__ == "__main__":
    unittest.main()
