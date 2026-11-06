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

# `/verify` Step 2b runs these tests and does not require lxml. Registration and
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


MONSTERS_XSD = """<?xml version="1.0" encoding="utf-8"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Monsters">
    <xs:complexType>
      <xs:sequence>
        <xs:element name="Monster" minOccurs="0" maxOccurs="unbounded">
          <xs:complexType>
            <xs:attribute name="id" type="xs:string" use="required"/>
            <xs:attribute name="base_monster" type="xs:string" use="required"/>
          </xs:complexType>
        </xs:element>
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>
"""

GOOD_MONSTER = """<?xml version="1.0" encoding="utf-8"?>
<Monsters>
  <Monster id="taom_animalia_elk" base_monster="horse" taom_body_length="100" />
</Monsters>
"""


@NEEDS_LXML
class UndeclaredAttributeIsReportedTests(unittest.TestCase):
    """D15: the public Armory's Monster files carry taom_body_length, which v1.4.8 Monsters.xsd
    does not declare. It stays a reported failure (WP115's pairing check reads it as a signal)."""

    def test_taom_body_length_on_monster_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            xsd = root / "Monsters.xsd"
            monster = root / "m.xml"
            _write(xsd, MONSTERS_XSD)
            _write(monster, GOOD_MONSTER)
            vx._schema.cache_clear()
            errors = vx.validate(monster, xsd)
        self.assertEqual(len(errors), 1, errors)
        self.assertIn("taom_body_length", errors[0])


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

    def test_a_submodule_with_an_unknown_encoding_exits_two_not_a_traceback(self):
        _write(self.module / "SubModule.xml", '<?xml version="1.0" encoding="bogus"?><Module/>')
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


class LiveTaomMapRegistrationTests(unittest.TestCase):
    """The live TAOM_Map is unversioned; a resync that adds a registration must show up here. The
    1.4.x package registers these eight; the public package is UNVERIFIED. Stdlib only, so it runs
    without lxml; skipped, never faked, without the install."""

    ALLOWED_IDS = {"Factions", "Items", "Kingdoms", "NPCCharacters", "SPCultures", "Settlements",
                   "WorkshopTypes", "partyTemplates"}

    def test_taom_map_registers_exactly_the_expected_ids(self):
        module = Path(vx.DEFAULT_GAME_MODULES) / "TAOM_Map"
        if not (module / "SubModule.xml").is_file():
            self.skipTest(f"no live TAOM_Map at {module}")
        entries, missing = vx.registered_files(module)
        self.assertEqual(missing, [], "\n" + "\n".join(missing))
        ids = {xml_id for _, xml_id in entries}
        self.assertIn("Settlements", ids)
        self.assertLessEqual(ids, self.ALLOWED_IDS, sorted(ids - self.ALLOWED_IDS))


LANGUAGE_DATA = """<?xml version="1.0" encoding="utf-8"?>
<LanguageData id="Test">
  <!-- <LanguageFile xml_path="XX/commented.xml" /> -->
  <LanguageFile xml_path="XX/listed.xml" />
  <LanguageFile xml_path="" />
</LanguageData>
"""


class LoaderBucketTests(_Tree):
    """Files the engine loads WITHOUT an <XmlName>: language files through language_data.xml
    (LocalizedTextManager.LoadLocalizationXmls, LanguageData.Deserialize) and stylesheets
    through MBObjectManager.HandleXsltList. Each is named for what it is, not NOT REGISTERED."""

    def _languages(self, manifest=LANGUAGE_DATA):
        langs = self.md / "Languages"
        _write(langs / "XX" / "language_data.xml", manifest)
        listed = langs / "XX" / "listed.xml"
        unlisted = langs / "XX" / "unlisted.xml"
        commented = langs / "XX" / "commented.xml"
        for p in (listed, unlisted, commented):
            _write(p, "<base/>")
        return listed, unlisted, commented, langs / "XX" / "language_data.xml"

    def test_a_listed_language_file_is_a_language_file(self):
        listed, _, _, _ = self._languages()
        report = vx.run([self.module], self.schemas, only=[listed])
        self.assertEqual(report["not_registered"], [])
        self.assertEqual([Path(r["file"]).name for r in report["language_file"]], ["listed.xml"])
        self.assertEqual(report["checked"], [])

    def test_an_unlisted_or_commented_language_file_stays_not_registered(self):
        _, unlisted, commented, _ = self._languages()
        report = vx.run([self.module], self.schemas, only=[unlisted, commented])
        self.assertEqual(report["language_file"], [])
        self.assertEqual(sorted(Path(p).name for p in report["not_registered"]),
                         ["commented.xml", "unlisted.xml"])

    def test_the_language_manifest_itself_is_a_language_file(self):
        _, _, _, manifest = self._languages()
        report = vx.run([self.module], self.schemas, only=[manifest])
        self.assertEqual(report["not_registered"], [])
        self.assertEqual(len(report["language_file"]), 1)

    def test_xml_path_is_relative_to_the_languages_folder(self):
        """LanguageData.Deserialize prefixes modulePath (ModuleData/Languages), not the
        folder holding language_data.xml, so a sibling spelled bare is not the listed file."""
        listed, _, _, _ = self._languages()
        bare = listed.parent / "XX" / "listed.xml"
        _write(bare, "<base/>")
        report = vx.run([self.module], self.schemas, only=[bare])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_a_voice_file_a_manifest_lists_is_a_language_file(self):
        """LanguageData.Deserialize also records <VoiceFile xml_path>, and LocalizedVoiceManager
        loads it, so it is not the 'real fault' an unlisted Languages/ file is."""
        listed, _, _, _ = self._languages(
            LANGUAGE_DATA.replace('<LanguageFile xml_path="XX/listed.xml"',
                                  '<VoiceFile xml_path="XX/listed.xml"'))
        report = vx.run([self.module], self.schemas, only=[listed])
        self.assertEqual(report["not_registered"], [])
        self.assertEqual([Path(r["file"]).name for r in report["language_file"]], ["listed.xml"])

    def test_an_xml_path_with_a_leading_slash_is_still_the_listed_file(self):
        """LanguageData.Deserialize joins modulePath + "/" + xml_path as strings, so a leading
        slash still opens a file under ModuleData/Languages; pathlib would root it at the drive."""
        listed, _, _, _ = self._languages(
            LANGUAGE_DATA.replace('xml_path="XX/listed.xml"', 'xml_path="/XX/listed.xml"'))
        report = vx.run([self.module], self.schemas, only=[listed])
        self.assertEqual([Path(r["file"]).name for r in report["language_file"]], ["listed.xml"])

    def test_a_manifest_without_an_id_lists_nothing(self):
        """LanguageData.LoadFromXml skips a language_data.xml with no id, so the files it
        names are never loaded and stay NOT REGISTERED."""
        listed, _, _, _ = self._languages(LANGUAGE_DATA.replace(' id="Test"', ""))
        report = vx.run([self.module], self.schemas, only=[listed])
        self.assertEqual(report["language_file"], [])
        self.assertEqual([Path(p).name for p in report["not_registered"]], ["listed.xml"])

    @NEEDS_LXML
    def test_a_mixed_call_still_classes_the_language_file(self):
        """A registered file in the call must not switch the language scan off for the rest."""
        listed, _, _, _ = self._languages()
        report = vx.run([self.module], self.schemas, only=[self.clans, listed])
        self.assertEqual([Path(r["file"]).name for r in report["language_file"]], ["listed.xml"])
        self.assertEqual([Path(r["file"]).name for r in report["checked"]], ["clans.xml"])

    @NEEDS_LXML
    def test_only_registered_files_skip_the_language_and_stylesheet_scans(self):
        self._languages()
        with mock.patch.object(vx, "listed_language_files", side_effect=AssertionError("scanned")), \
                mock.patch.object(vx, "applied_stylesheets", side_effect=AssertionError("scanned")):
            report = vx.run([self.module], self.schemas, only=[self.clans])
        self.assertEqual([Path(r["file"]).name for r in report["checked"]], ["clans.xml"])

    def test_a_manifest_with_an_unknown_encoding_loads_like_the_engine_reads_it(self):
        """LocalizedTextManager reads the text with StreamReader.ReadToEnd and LoadXml, so it ignores the
        declared encoding; expat raises LookupError for one it does not know."""
        _, _, _, manifest = self._languages(LANGUAGE_DATA.replace(
            '<?xml version="1.0" encoding="utf-8"?>', '<?xml version="1.0" encoding="bogus"?>'))
        listed = manifest.parent / "listed.xml"
        report = vx.run([self.module], self.schemas, only=[manifest, listed])
        self.assertEqual(sorted(Path(r["file"]).name for r in report["language_file"]),
                         ["language_data.xml", "listed.xml"])
        self.assertEqual([r["broken"] for r in report["language_file"]], [None, None])

    def test_a_manifest_with_an_unclosed_tag_is_still_broken(self):
        _, _, _, manifest = self._languages('<?xml version="1.0" encoding="bogus"?>\n<LanguageData id="Test">')
        report = vx.run([self.module], self.schemas, only=[manifest])
        self.assertIn("it does not parse", report["language_file"][0]["broken"])

    def test_a_manifest_whose_root_is_not_language_data_is_broken(self):
        _, _, _, manifest = self._languages(LANGUAGE_DATA.replace("LanguageData", "Other"))
        report = vx.run([self.module], self.schemas, only=[manifest])
        self.assertEqual(len(report["language_file"]), 1)
        self.assertIn("its root is not a <LanguageData>", report["language_file"][0]["broken"])

    def test_stylesheet_beside_a_registered_file_is_a_stylesheet(self):
        for ext in (".xsl", ".xslt"):
            with self.subTest(ext=ext):
                sheet = self.clans.with_suffix(ext)
                _write(sheet, "<xsl:stylesheet/>")
                report = vx.run([self.module], self.schemas, only=[sheet])
                self.assertEqual(report["not_registered"], [])
                self.assertEqual([Path(r["file"]).name for r in report["stylesheet"]],
                                 [sheet.name])
                self.assertEqual(report["stylesheet"][0]["id"], "Factions")
                sheet.unlink()

    def test_stylesheet_beside_a_folder_member_is_a_stylesheet(self):
        sheet = self.md / "more_clans" / "north.xsl"
        _write(sheet, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[sheet])
        self.assertEqual(len(report["stylesheet"]), 1)

    def test_xslt_only_registration_stylesheet_is_a_stylesheet(self):
        _write(self.module / "SubModule.xml", SUBMODULE.replace("more_clans", "spclans"))
        sheet = self.md / "spclans.xslt"
        _write(sheet, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[sheet])
        self.assertEqual(len(report["stylesheet"]), 1)

    def test_xslt_is_ignored_when_an_xsl_also_exists(self):
        """HandleXsltList returns at the first existing of `.xsl`, `.xslt`."""
        _write(self.clans.with_suffix(".xsl"), "<xsl:stylesheet/>")
        shadowed = self.clans.with_suffix(".xslt")
        _write(shadowed, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[shadowed])
        self.assertEqual(report["stylesheet"], [])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_a_stylesheet_nothing_registers_stays_not_registered(self):
        stray = self.md / "orphan.xsl"
        _write(stray, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[stray])
        self.assertEqual(report["stylesheet"], [])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_a_stylesheet_beside_an_unregistered_xml_stays_not_registered(self):
        stray = self.md / "custom.xsl"
        _write(self.md / "custom.xml", GOOD_CLANS)
        _write(stray, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[stray])
        self.assertEqual(len(report["not_registered"]), 1)


@NEEDS_LXML
class LoaderBucketCliTests(_Tree):
    _languages = LoaderBucketTests._languages

    def _cli(self, *files):
        return _run_main("--module", str(self.module), "--schemas", str(self.schemas),
                         *map(str, files))

    def test_a_manifest_that_does_not_parse_is_named_a_fault_not_loaded(self):
        """LocalizedTextManager.LoadXmlFile asserts and loads nothing from a manifest it cannot
        parse, so the tool must not call that manifest loaded."""
        _, _, _, manifest = self._languages(LANGUAGE_DATA.replace("</LanguageData>", ""))
        code, text = self._cli(manifest)
        line = next(l for l in text.splitlines() if "language_data.xml" in l and l.startswith("LANGUAGE FILE"))
        self.assertIn("the engine loads nothing from it", line)
        self.assertIn("a real fault", line)
        self.assertNotIn("loaded through", line)
        self.assertEqual(code, 1, "a real fault in a by-name run fails the run")
        self.assertTrue(text.splitlines()[-1].startswith("FAIL"), text)
        self.assertIn("1 real fault(s) named above", text.splitlines()[-1])

    def test_a_file_an_id_less_manifest_lists_stays_not_registered_with_the_fault(self):
        listed, _, _, _ = self._languages(LANGUAGE_DATA.replace(' id="Test"', ""))
        _, text = self._cli(listed)
        line = next(l for l in text.splitlines() if l.startswith("NOT REGISTERED"))
        self.assertIn("no language_data.xml the engine loads lists it", line)

    def test_an_id_less_manifest_passed_by_name_prints_its_root_reason_as_a_fault(self):
        _, _, _, manifest = self._languages(LANGUAGE_DATA.replace(' id="Test"', ""))
        code, text = self._cli(manifest)
        line = next(l for l in text.splitlines()
                    if l.startswith("LANGUAGE FILE") and "language_data.xml" in l)
        self.assertIn("its root is not a <LanguageData> with a non-empty id", line)
        self.assertIn("(a real fault)", line)
        self.assertEqual(code, 1)
        self.assertIn("1 real fault(s) named above", text.splitlines()[-1])

    def test_an_unlisted_languages_xml_exits_one(self):
        _, unlisted, _, _ = self._languages()
        code, text = self._cli(unlisted)
        self.assertEqual(code, 1, text)
        self.assertTrue(text.splitlines()[-1].startswith("FAIL"), text)

    def test_a_clean_by_name_run_exits_zero(self):
        listed, _, _, manifest = self._languages()
        csharp = self.md / "careers.xml"
        _write(csharp, "<Careers/>")
        code, text = self._cli(listed, manifest, csharp)
        self.assertEqual(code, 0, text)
        self.assertTrue(text.splitlines()[-1].startswith("PASS"), text)

    def test_a_whole_module_run_with_a_stray_languages_xml_still_exits_zero(self):
        self._languages()
        code, text = _run_main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 0, text)

    def test_a_whole_module_run_with_a_broken_manifest_still_exits_zero(self):
        self._languages(LANGUAGE_DATA.replace("</LanguageData>", ""))
        code, text = _run_main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 0, text)

    def test_each_not_registered_path_is_classified_once(self):
        _, unlisted, _, _ = self._languages()
        csharp = self.md / "careers.xml"
        _write(csharp, "<Careers/>")
        with mock.patch.object(vx, "_xml_in_own_languages_folder",
                               wraps=vx._xml_in_own_languages_folder) as spy:
            self._cli(unlisted, csharp)
        self.assertEqual(spy.call_count, 2)

    def test_a_non_xml_file_under_languages_is_not_a_fault(self):
        notes = self.md / "Languages" / "ids_plain.txt"
        _write(notes, "ids")
        _, text = self._cli(notes)
        line = next(l for l in text.splitlines() if l.startswith("NOT REGISTERED"))
        self.assertNotIn("a real fault", line)

    def test_a_whole_module_run_prints_no_bucket_counts(self):
        code, text = _run_main("--module", str(self.module), "--schemas", str(self.schemas))
        self.assertEqual(code, 0, text)
        self.assertNotIn("language file(s)", text.splitlines()[-1])

    def test_a_real_fault_line_gives_one_reason_and_a_data_line_none_of_the_fault(self):
        _, unlisted, _, _ = self._languages()
        csharp = self.md / "careers.xml"
        _write(csharp, "<Careers/>")
        _, text = self._cli(unlisted, csharp)
        lines = text.splitlines()
        unlisted_line = next(l for l in lines if l.startswith("NOT REGISTERED") and "unlisted.xml" in l)
        csharp_line = next(l for l in lines if l.startswith("NOT REGISTERED") and "careers.xml" in l)
        self.assertNotIn("expected for", unlisted_line)
        self.assertNotIn("a real fault", csharp_line)

    def test_a_relative_path_inside_languages_still_gets_the_fault(self):
        _, unlisted, _, _ = self._languages()
        cwd = os.getcwd()
        os.chdir(self.md)
        try:  # restore before tearDown deletes the tree: Windows cannot delete the current folder
            _, text = self._cli(Path("Languages") / "XX" / "unlisted.xml")
        finally:
            os.chdir(cwd)
        line = next(l for l in text.splitlines() if l.startswith("NOT REGISTERED"))
        self.assertIn("a real fault", line)

    def test_a_languages_folder_elsewhere_in_moduledata_is_not_the_fault(self):
        """The engine scans only <module>/ModuleData/Languages (LocalizedTextManager:91)."""
        other = self.md / "career_system" / "Languages" / "foo.xml"
        _write(other, "<base/>")
        _, text = self._cli(other)
        line = next(l for l in text.splitlines() if l.startswith("NOT REGISTERED"))
        self.assertNotIn("a real fault", line)

    def test_a_path_the_console_cannot_encode_does_not_crash_the_run(self):
        moved = Path(self._tmp.name) / "Мод" / "TestMod"
        os.makedirs(moved.parent, exist_ok=True)
        os.replace(self.module, moved)
        self.module, self.md = moved, moved / "ModuleData"
        listed, _, _, _ = self._languages()
        raw = io.BytesIO()
        out = io.TextIOWrapper(raw, encoding="cp1252")
        with mock.patch.object(sys, "stdout", out):
            code = vx.main(["--module", str(self.module), "--schemas", str(self.schemas), str(listed)])
            out.flush()
        self.assertEqual(code, 0, raw.getvalue())

    def test_lines_and_summary_count_each_bucket(self):
        listed, unlisted, _, _ = self._languages()
        sheet = self.clans.with_suffix(".xsl")
        _write(sheet, "<xsl:stylesheet/>")
        csharp = self.md / "careers.xml"
        _write(csharp, "<Careers/>")
        code, text = self._cli(listed, unlisted, sheet, csharp)
        self.assertEqual(code, 1, text)
        lines = text.splitlines()
        self.assertTrue(any(l.startswith("LANGUAGE FILE") and "listed.xml" in l
                            and "language_data.xml" in l for l in lines), text)
        self.assertTrue(any(l.startswith("STYLESHEET") and "clans.xsl" in l
                            and "Factions" in l for l in lines), text)
        unlisted_line = next(l for l in lines if l.startswith("NOT REGISTERED") and "unlisted.xml" in l)
        self.assertIn("no language_data.xml the engine loads lists it", unlisted_line)
        self.assertIn("1 real fault(s) named above", lines[-1])
        csharp_line = next(l for l in lines if l.startswith("NOT REGISTERED") and "careers.xml" in l)
        self.assertIn("no project.mbproj row names it", csharp_line)
        self.assertIn("only by fixed name (global_strings.xml, the scene lists, "
                      "ModuleData/project.mbproj itself)", csharp_line)
        self.assertNotIn("never reads or validates", text)
        self.assertIn("1 language file(s)", lines[-1])
        self.assertIn("1 stylesheet(s)", lines[-1])
        self.assertIn("2 not registered", lines[-1])


MBPROJ = """<?xml version="1.0" encoding="utf-8"?>
<base>
  <file id="soln_voice_definitions" name="ModuleData/voice_definitions.xml"/>
  <file id="soln_action_sets" name="ModuleData/action_sets.xml"/>
</base>
"""


BROKEN_MBPROJ = {
    "does not parse": "<base><file id=",
    "is empty": "",
    "root is not base": "<other><file id='a' name='ModuleData/voice_definitions.xml'/></other>",
    "row without an id": '<base><file name="ModuleData/voice_definitions.xml"/></base>',
    "row without a name": '<base><file id="x"/></base>',
    "unclosed tag after an unknown encoding": '<?xml version="1.0" encoding="bogus"?><base><file id="x"',
}


class NativeBucketTests(_Tree):
    """Files a project.mbproj row loads: `Module` requests soln_* ids, `XmlResource.GetMbprojxmls`
    registers each <file id name> row of <module>/ModuleData/project.mbproj, and
    `MBObjectManager.GetMergedXmlForNative` reads `<module folder>/<name>` (not relative to
    ModuleData) and applies `<name minus its last 4 characters>.xsl`, else `.xslt`."""

    def _mbproj(self, text=MBPROJ):
        _write(self.md / "project.mbproj", text)
        data = self.md / "voice_definitions.xml"
        _write(data, "<voice_definitions/>")
        return data

    def test_a_file_a_row_names_is_native(self):
        data = self._mbproj()
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual(report["not_registered"], [])
        self.assertEqual([(Path(r["file"]).name, r["id"], r["kind"]) for r in report["native"]],
                         [("voice_definitions.xml", "soln_voice_definitions", "data")])

    def test_the_row_name_is_relative_to_the_module_folder_not_moduledata(self):
        _write(self.md / "project.mbproj",
               MBPROJ.replace('name="ModuleData/voice_definitions.xml"', 'name="voice_definitions.xml"'))
        under_moduledata = self.md / "voice_definitions.xml"
        at_module_root = self.module / "voice_definitions.xml"
        _write(under_moduledata, "<voice_definitions/>")
        _write(at_module_root, "<voice_definitions/>")
        report = vx.run([self.module], self.schemas, only=[under_moduledata, at_module_root])
        self.assertEqual([Path(r["file"]) for r in report["native"]], [at_module_root])
        self.assertEqual([Path(p) for p in report["not_registered"]], [under_moduledata])

    def test_the_stylesheet_beside_a_row_file_is_native(self):
        self._mbproj()
        for ext in (".xsl", ".xslt"):
            with self.subTest(ext=ext):
                sheet = self.md / f"voice_definitions{ext}"
                _write(sheet, "<xsl:stylesheet/>")
                report = vx.run([self.module], self.schemas, only=[sheet])
                self.assertEqual([(Path(r["file"]).name, r["kind"]) for r in report["native"]],
                                 [(sheet.name, "stylesheet")])
                self.assertEqual(report["not_registered"], [])
                sheet.unlink()

    def test_an_xslt_is_ignored_when_an_xsl_also_exists(self):
        self._mbproj()
        _write(self.md / "voice_definitions.xsl", "<xsl:stylesheet/>")
        shadowed = self.md / "voice_definitions.xslt"
        _write(shadowed, "<xsl:stylesheet/>")
        report = vx.run([self.module], self.schemas, only=[shadowed])
        self.assertEqual(report["native"], [])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_an_xml_no_row_names_stays_not_registered(self):
        self._mbproj()
        stray = self.md / "careers.xml"
        _write(stray, "<Careers/>")
        report = vx.run([self.module], self.schemas, only=[stray])
        self.assertEqual(report["native"], [])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_a_malformed_mbproj_is_handled_without_a_crash(self):
        for text in ("<base><file id=", "<other><file id='a' name='b.xml'/></other>", ""):
            with self.subTest(text=text):
                data = self._mbproj(text)
                report = vx.run([self.module], self.schemas, only=[data])
                self.assertEqual(report["native"], [])
                self.assertEqual(len(report["not_registered"]), 1)

    def test_a_row_missing_its_id_or_name_is_skipped(self):
        data = self._mbproj('<base><file name="ModuleData/voice_definitions.xml"/><file id="x"/></base>')
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual(report["native"], [])

    def test_a_row_name_joins_as_a_string_like_the_engine(self):
        """A leading slash still opens a file under the module; an absolute name does not escape it
        (pathlib would root the join at the drive)."""
        data = self.md / "voice_definitions.xml"
        _write(data, "<voice_definitions/>")
        _write(self.md / "project.mbproj",
               MBPROJ.replace('name="ModuleData/voice_definitions.xml"',
                              'name="/ModuleData/voice_definitions.xml"'))
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual([Path(r["file"]).name for r in report["native"]], ["voice_definitions.xml"])
        _write(self.md / "project.mbproj",
               MBPROJ.replace('name="ModuleData/voice_definitions.xml"', f'name="{data}"'))
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual(report["native"], [])
        self.assertEqual(len(report["not_registered"]), 1)

    def test_the_record_carries_the_row_type(self):
        data = self._mbproj(MBPROJ.replace('<file id="soln_voice_definitions"',
                                           '<file id="soln_voice_definitions" type="voice"'))
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual([r["type"] for r in report["native"]], ["voice"])

    def test_an_unknown_encoding_in_the_mbproj_loads_like_the_engine_reads_it(self):
        """XmlResource reads project.mbproj through a StreamReader, so the declared encoding is ignored."""
        data = self._mbproj(MBPROJ.replace('encoding="utf-8"', 'encoding="bogus"'))
        found, problems = vx.native_files(self.module)
        self.assertEqual(problems, [])
        self.assertEqual([v[1] for v in found.values()], ["soln_voice_definitions"])
        report = vx.run([self.module], self.schemas, only=[data, self.md / "project.mbproj"])
        self.assertEqual(report["missing"], [])
        self.assertEqual(len(report["native"]), 1)

    def test_a_short_row_name_is_not_a_problem(self):
        """GetMbprojxmls never checks the length; only a requested id reaches the Remove(Length - 4)."""
        self._mbproj('<base><file id="y" name="ab"/></base>')
        found, problems = vx.native_files(self.module)
        self.assertEqual((found, problems), ({}, []))

    def test_mbproj_rows_returns_id_name_type_and_one_problem_per_bad_row(self):
        self._mbproj('<base><file name="a.xml" id="x" type="t"/><file id="y" name="b.xml"/>'
                            '<file id="z"/><!-- <file id="c" name="c.xml"/> --></base>')
        rows, problems = vx.mbproj_rows(self.md / "project.mbproj")
        self.assertEqual(rows, [("x", "a.xml", "t"), ("y", "b.xml", None)])
        self.assertEqual(len(problems), 1, problems)
        self.assertIn('<file id="z">', problems[0])
        self.assertEqual(vx.mbproj_rows(self.md / "absent.mbproj"), ([], []))

    def test_native_files_returns_the_files_and_the_problems(self):
        self._mbproj()
        found, problems = vx.native_files(self.module)
        self.assertEqual(len(found), 1)
        self.assertEqual(problems, [])

    def test_each_broken_mbproj_passed_by_name_is_a_missing_registration(self):
        for label, text in BROKEN_MBPROJ.items():
            with self.subTest(label):
                self._mbproj(text)
                report = vx.run([self.module], self.schemas, only=[self.md / "project.mbproj"])
                self.assertEqual(len(report["missing"]), 1, report["missing"])
                self.assertIn("project.mbproj", report["missing"][0])
                self.assertIn("throws", report["missing"][0])
                self.assertEqual(report["not_registered"], [])

    def test_a_clean_mbproj_passed_by_name_is_neither_missing_nor_not_registered(self):
        self._mbproj()
        report = vx.run([self.module], self.schemas, only=[self.md / "project.mbproj"])
        self.assertEqual(report["missing"], [])
        self.assertEqual(report["not_registered"], [])

    def test_a_broken_mbproj_is_not_reported_for_another_file(self):
        data = self._mbproj("<base><file id=")
        report = vx.run([self.module], self.schemas, only=[data])
        self.assertEqual(report["missing"], [])

    @NEEDS_LXML
    def test_a_registered_file_skips_the_mbproj_scan(self):
        self._mbproj()
        with mock.patch.object(vx, "native_files", side_effect=AssertionError("scanned")):
            report = vx.run([self.module], self.schemas, only=[self.clans])
        self.assertEqual(len(report["checked"]) + len(report["no_schema"]), 1)

    @NEEDS_LXML
    def test_the_cli_prints_the_native_line_and_counts_it(self):
        data = self._mbproj()
        sheet = self.md / "voice_definitions.xsl"
        _write(sheet, "<xsl:stylesheet/>")
        code, text = _run_main("--module", str(self.module), "--schemas", str(self.schemas),
                               str(data), str(sheet))
        self.assertEqual(code, 0, text)
        lines = text.splitlines()
        data_line = next(l for l in lines if l.startswith("NATIVE") and "voice_definitions.xml" in l)
        self.assertIn('named by project.mbproj row id="soln_voice_definitions" type="-" (data); '
                      'it loads only if the engine requests that id or reads that type; '
                      'no XSD here checks it; gates: python tools/data_gates.py', data_line)
        sheet_line = next(l for l in lines if l.startswith("NATIVE") and "voice_definitions.xsl" in l)
        self.assertIn('row id="soln_voice_definitions" type="-" (stylesheet)', sheet_line)
        self.assertIn("2 native", lines[-1])
        self.assertIn("0 not registered", lines[-1])


@NEEDS_LXML
class NativeBucketCliTests(_Tree):
    _mbproj = NativeBucketTests._mbproj

    def _cli(self, *files):
        return _run_main("--module", str(self.module), "--schemas", str(self.schemas),
                         *map(str, files))

    def test_each_broken_mbproj_passed_by_name_exits_one(self):
        for label, text in BROKEN_MBPROJ.items():
            with self.subTest(label):
                self._mbproj(text)
                code, out = self._cli(self.md / "project.mbproj")
                self.assertEqual(code, 1, out)
                self.assertTrue(any(l.startswith("MISSING") and "project.mbproj" in l
                                    for l in out.splitlines()), out)
                self.assertTrue(out.splitlines()[-1].startswith("FAIL"), out)

    def test_a_clean_mbproj_passed_by_name_exits_zero_with_no_not_registered_line(self):
        self._mbproj()
        code, out = self._cli(self.md / "project.mbproj")
        self.assertEqual(code, 0, out)
        self.assertNotIn("NOT REGISTERED", out)
        self.assertIn("0 not registered", out.splitlines()[-1])

    def test_a_broken_mbproj_with_only_a_data_file_named_exits_zero(self):
        data = self._mbproj("<base><file id=")
        code, out = self._cli(data)
        self.assertEqual(code, 0, out)

    def test_a_dead_row_id_is_not_called_loaded(self):
        data = self._mbproj('<base><file id="taom_invented" name="ModuleData/voice_definitions.xml"/></base>')
        code, out = self._cli(data)
        line = next(l for l in out.splitlines() if l.startswith("NATIVE"))
        self.assertIn('id="taom_invented"', line)
        self.assertNotIn("loaded", line)
        self.assertEqual(code, 0, out)


class EngineReaderTests(unittest.TestCase):
    """_parse_like_engine reads bytes the way `new StreamReader(path)` does (XmlResource.cs:121): UTF-8 by
    default, a byte order mark picks its own encoding, the declared encoding is ignored (#987 item 3)."""

    DOC = '<base><file id="x" name="a.xml"/></base>'

    def _parse(self, data: bytes):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "project.mbproj"
            path.write_bytes(data)
            return vx._parse_like_engine(path)

    def test_a_bom_less_utf16_file_is_not_well_formed_like_the_engine_reads_it(self):
        for codec in ("utf-16-le", "utf-16-be"):
            with self.subTest(codec):
                with self.assertRaises((vx.ET.ParseError, ValueError)):
                    self._parse(self.DOC.encode(codec))

    def test_a_bom_less_utf16_file_declaring_utf16_is_still_utf8_to_the_engine(self):
        doc = '<?xml version="1.0" encoding="utf-16"?>' + self.DOC
        with self.assertRaises((vx.ET.ParseError, ValueError)):
            self._parse(doc.encode("utf-16-le"))

    def test_a_utf16_file_with_a_bom_loads(self):
        for codec in ("utf-16", "utf-16-be"):
            with self.subTest(codec):
                data = self.DOC.encode(codec)
                if codec == "utf-16-be":
                    data = b"\xfe\xff" + data
                self.assertEqual("base", self._parse(data).tag)

    def test_a_utf32le_bom_file_loads(self):
        # The UTF-32 little-endian mark begins with the UTF-16 one (FF FE): it must not read as UTF-16.
        self.assertEqual("base", self._parse(b"\xff\xfe\x00\x00" + self.DOC.encode("utf-32-le")).tag)
        self.assertEqual("base", self._parse(b"\x00\x00\xfe\xff" + self.DOC.encode("utf-32-be")).tag)

    def test_a_raw_nul_in_valid_utf8_is_named_a_nul_not_bad_utf8(self):
        with self.assertRaises(vx.ET.ParseError) as caught:
            self._parse(b'<base><file id="x" name="a\x00.xml"/></base>')
        self.assertIn("contains a NUL character", str(caught.exception))
        self.assertNotIn("not UTF-8", str(caught.exception))

    def test_a_utf8_bom_loads(self):
        self.assertEqual("base", self._parse(b"\xef\xbb\xbf" + self.DOC.encode("utf-8")).tag)

    def test_a_latin1_byte_reads_as_a_replacement_character_not_as_latin1(self):
        doc = '<?xml version="1.0" encoding="iso-8859-1"?><base><file id="a\xe9" name="n.xml"/></base>'
        root = self._parse(doc.encode("latin-1"))
        self.assertEqual("a\ufffd", root.find("file").get("id"))

    def test_mbproj_rows_reports_a_bom_less_utf16_file_as_a_startup_throw(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "project.mbproj"
            path.write_bytes(self.DOC.encode("utf-16-le"))
            rows, problems = vx.mbproj_rows(path)
        self.assertEqual([], rows)
        self.assertEqual(1, len(problems), problems)


class EngineIdUniquenessTests(unittest.TestCase):
    """#974: a repeated clan, hero or kingdom id needs no TAOM schema. The engine's own XSDs declare the row id
    unique (`xs:unique` on `@id`), and `SCHEMA_INVALID` validates the registered files against them in the commit
    hook. This pins both halves, so a lost registration or a changed engine XSD does not silently drop the check."""

    REPO = Path(__file__).resolve().parents[2]
    CASES = (("Factions", "characters/clans.xml", "Faction"),
             ("Heroes", "characters/heroes.xml", "Hero"),
             ("Kingdoms", "taom_spkingdoms.xml", "Kingdom"))

    def test_the_three_files_are_registered_under_their_engine_ids(self):
        # What SCHEMA_INVALID really validates, not text: a commented-out registration loads nothing.
        entries, _ = vx.registered_files(self.REPO / "Main" / "_Module")
        moduledata = self.REPO / "Main" / "_Module" / "ModuleData"
        resolved = {(Path(path).resolve(), xml_id) for path, xml_id in entries}
        for xml_id, rel, _ in self.CASES:
            with self.subTest(xml_id):
                self.assertIn(((moduledata / rel).resolve(), xml_id), resolved)

    @NEEDS_LXML
    def test_a_repeated_id_breaks_the_engine_schema(self):
        import copy
        from lxml import etree
        from _gamedir import game_dir
        schemas = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")) / "XmlSchemas"
        if not schemas.is_dir():
            self.skipTest(f"no engine XmlSchemas at {schemas}")
        for xml_id, rel, element in self.CASES:
            with self.subTest(xml_id):
                schema = etree.XMLSchema(etree.parse(str(schemas / f"{xml_id}.xsd")))
                doc = etree.parse(str(self.REPO / "Main" / "_Module" / "ModuleData" / rel))
                self.assertTrue(schema.validate(doc), f"{rel} must start valid")
                root = doc.getroot()
                root.append(copy.deepcopy(next(c for c in root if c.tag == element)))
                self.assertFalse(schema.validate(doc))
                self.assertIn("Duplicate key-sequence", str(schema.error_log))


if __name__ == "__main__":
    unittest.main()
