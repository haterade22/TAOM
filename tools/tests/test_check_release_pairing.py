#!/usr/bin/env python3
"""Unit tests for the release pairing check (tools/check_release_pairing.py).

Run:  python -m unittest tools.tests.test_check_release_pairing

Everything runs against temp-dir module trees; the only repo read is characters/clans.xml, which is
the bandit list the tool itself reads.
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import add_map_villages as amv  # noqa: E402
import check_release_pairing as crp  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "check_release_pairing.py"

XSD = """<?xml version="1.0"?>
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
  <xs:element name="Monsters"><xs:complexType><xs:sequence>
    <xs:element name="Monster"><xs:complexType>
      <xs:attribute name="id" type="xs:string" />
    </xs:complexType></xs:element>
  </xs:sequence></xs:complexType></xs:element>
</xs:schema>
"""


def _write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def _submodule(version: str) -> str:
    return f'<Module><Id value="X" /><Version value="{version}" /></Module>\n'


def _bandit_cultures() -> list:
    root = ET.parse(crp.REPO_CLANS).getroot()
    return sorted({f.get("culture") for f in root.iter("Faction") if f.get("is_bandit") == "true"})


def _settlements(villages=amv.VILLAGES, cultures=()) -> str:
    rows = []
    for v in villages:
        rows.append(f'<Settlement id="{v.id}" posX="1.000" posY="2.000" culture="Culture.{v.culture}">'
                    f'<Components><Village id="{amv.comp_id(v.id)}" bound="Settlement.{v.bound}" /></Components>'
                    '</Settlement>')
    for i, culture in enumerate(cultures):
        rows.append(f'<Settlement id="hideout_{i}" type="Hideout" culture="{culture}">'
                    f'<Components><Hideout id="hc_{i}" /></Components></Settlement>')
    return "<Settlements>\n" + "\n".join(rows) + "\n</Settlements>\n"


def _scene(villages=amv.VILLAGES) -> str:
    rows = [f'<game_entity name="{v.id}"><transform position="1.000, 2.000, 0.000" /></game_entity>'
            for v in villages]
    return "<scene>\n" + "\n".join(rows) + "\n</scene>\n"


def _complete(td, armory_version="v2.0.26", map_version="v2.0.27") -> Path:
    """A game folder whose TAOM_Map and Armory pass every check. Returns its Modules root."""
    game = Path(td) / "game"
    modules = game / "Modules"
    _write(game / "XmlSchemas" / "Monsters.xsd", XSD)
    tmap = modules / "TAOM_Map"
    _write(tmap / "SubModule.xml", _submodule(map_version))
    _write(tmap / "ModuleData" / "settlements.xml", _settlements(cultures=_bandit_cultures()))
    _write(tmap / "SceneObj" / "Main_map" / "scene.xscene", _scene())
    for lang in amv.LANGS:
        loc = "\n".join(f'<string id="Settlements.Settlement.name.{v.id}" text="x" />' for v in amv.VILLAGES)
        _write(tmap / "ModuleData" / "Languages" / lang / "loc_settlements.xml", f"<strings>\n{loc}\n</strings>\n")
    armory = modules / "LOTRLOME_Armory"
    _write(armory / "SubModule.xml", _submodule(armory_version))
    _write(armory / "ModuleData" / "monsters.xml", '<Monsters><Monster id="human" /></Monsters>\n')
    return modules


def _run(*args):
    return subprocess.run([sys.executable, str(TOOL), *args], capture_output=True, text=True,
                          encoding="utf-8", errors="replace")


class TestIdentity(unittest.TestCase):
    def _module(self, td):
        root = Path(td) / "M"
        _write(root / "SubModule.xml", _submodule("v2.0.27"))
        _write(root / "ModuleData" / "a.xml", "<a />")
        _write(root / "ModuleData" / "sub" / "b.xml", "<b />")
        return root

    def test_stable_and_counts_files(self):
        with tempfile.TemporaryDirectory() as td:
            root = self._module(td)
            first = crp.module_identity(root)
            self.assertEqual(first, crp.module_identity(root))
            self.assertEqual(first["version"], "v2.0.27")
            self.assertEqual(first["files"], 2)
            self.assertTrue(first["fingerprint"].startswith("sha256:"))

    def test_changes_with_a_moduledata_byte(self):
        with tempfile.TemporaryDirectory() as td:
            root = self._module(td)
            before = crp.module_identity(root)["fingerprint"]
            _write(root / "ModuleData" / "a.xml", "<a/>")
            self.assertNotEqual(before, crp.module_identity(root)["fingerprint"])

    def test_ignores_a_version_edit_and_a_backup(self):
        with tempfile.TemporaryDirectory() as td:
            root = self._module(td)
            before = crp.module_identity(root)["fingerprint"]
            _write(root / "SubModule.xml", _submodule("v2.4.0"))
            _write(root / "ModuleData" / "a.xml.bak", "old")
            self.assertEqual(before, crp.module_identity(root)["fingerprint"])


class TestVersion(unittest.TestCase):
    def test_hand_typed_and_1_5_x_numbers_are_findings(self):
        for version in ("v2.0.29.5", "v2.0.31"):
            self.assertEqual(len(crp.version_findings({"TAOM_Map": {"version": version}})), 1, version)

    def test_a_1_4_x_number_is_not(self):
        self.assertEqual(crp.version_findings({"TAOM_Map": {"version": "v2.0.27"}}), [])


class TestPairingSignals(unittest.TestCase):
    def test_an_undeclared_monster_attribute_is_a_signal_naming_the_file(self):
        with tempfile.TemporaryDirectory() as td:
            armory = Path(td) / "LOTRLOME_Armory"
            _write(armory / "ModuleData" / "monsters.xml",
                   '<Monsters><Monster id="human" taom_body_length="1.0" /></Monsters>')
            _write(armory / "ModuleData" / "items.xml", '<Items><Monster taom_other="1" /></Items>')
            xsd = _write(Path(td) / "Monsters.xsd", XSD)
            signals = crp.undeclared_monster_attributes(armory, xsd)
            self.assertEqual(list(signals), ["taom_body_length"])
            self.assertEqual(signals["taom_body_length"], ["ModuleData/monsters.xml"])

    def test_a_signal_never_changes_the_exit_code(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            _write(modules / "LOTRLOME_Armory" / "ModuleData" / "monsters.xml",
                   '<Monsters><Monster id="human" taom_body_length="1.0" /></Monsters>')
            pin = Path(td) / "pin.json"
            pin.write_text(json.dumps({"modules": {
                n: crp.module_identity(modules / n) for n in crp.PINNED_MODULES}}), encoding="utf-8")
            r = _run("--modules", str(modules), "--pin", str(pin))
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("PAIRING SIGNAL (decision D15, never an error)", r.stdout)
            self.assertIn("taom_body_length", r.stdout)


class TestHideouts(unittest.TestCase):
    CLANS = ('<Factions><Faction id="a" culture="Culture.one" is_bandit="true" />'
             '<Faction id="b" culture="Culture.two" is_bandit="true" />'
             '<Faction id="c" culture="Culture.three" /></Factions>')

    def test_a_bandit_culture_without_a_den_is_one_finding_naming_it(self):
        with tempfile.TemporaryDirectory() as td:
            clans = _write(Path(td) / "clans.xml", self.CLANS)
            settlements = _write(Path(td) / "settlements.xml", _settlements(villages=(), cultures=["Culture.one"]))
            findings = crp.hideout_findings(clans, settlements)
            self.assertEqual(len(findings), 1, findings)
            self.assertIn("Culture.two", findings[0])

    def test_the_repo_defines_a_bandit_clan(self):
        self.assertGreaterEqual(len(_bandit_cultures()), 1)


class TestVillages(unittest.TestCase):
    def test_an_empty_map_is_a_finding_naming_the_first_table_id(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            tmap = modules / "TAOM_Map"
            _write(tmap / "ModuleData" / "settlements.xml", "<Settlements />")
            findings, _note = crp.village_findings(tmap)
            self.assertTrue(any(amv.VILLAGES[0].id in f for f in findings), findings[:3])

    def test_a_complete_map_has_none(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            self.assertEqual(crp.village_findings(modules / "TAOM_Map"), ([], None))

    def test_an_absent_scene_gives_the_note_and_no_scene_findings(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            (modules / "TAOM_Map" / "SceneObj" / "Main_map" / "scene.xscene").unlink()
            findings, note = crp.village_findings(modules / "TAOM_Map")
            self.assertEqual(findings, [])
            self.assertIn("scene.xscene", note)

    def test_a_missing_language_file_is_a_finding(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            (modules / "TAOM_Map" / "ModuleData" / "Languages" / "FR" / "loc_settlements.xml").unlink()
            findings, _note = crp.village_findings(modules / "TAOM_Map")
            self.assertEqual(len(findings), 1, findings)
            self.assertIn("FR", findings[0])


class TestPin(unittest.TestCase):
    IDENTITY = {"TAOM_Map": {"version": "v1", "files": 1, "fingerprint": "sha256:aa"}}

    def test_a_missing_pin_is_a_finding(self):
        with tempfile.TemporaryDirectory() as td:
            findings = crp.pin_findings(self.IDENTITY, Path(td) / "none.json")
            self.assertEqual(len(findings), 1)
            self.assertTrue(findings[0].startswith("PIN_MISSING"), findings)

    def test_a_matching_pin_is_clean(self):
        with tempfile.TemporaryDirectory() as td:
            pin = _write(Path(td) / "pin.json", json.dumps({"modules": self.IDENTITY}))
            self.assertEqual(crp.pin_findings(self.IDENTITY, pin), [])

    def test_a_different_fingerprint_is_a_mismatch(self):
        with tempfile.TemporaryDirectory() as td:
            other = {"TAOM_Map": {"version": "v1", "files": 1, "fingerprint": "sha256:bb"}}
            pin = _write(Path(td) / "pin.json", json.dumps({"modules": other}))
            findings = crp.pin_findings(self.IDENTITY, pin)
            self.assertEqual(len(findings), 1)
            self.assertTrue(findings[0].startswith("PIN_MISMATCH"), findings)


class TestCli(unittest.TestCase):
    def test_exit_0_on_a_complete_fixture_with_a_matching_pin(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            pin = Path(td) / "pin.json"
            pin.write_text(json.dumps({"modules": {
                n: crp.module_identity(modules / n) for n in crp.PINNED_MODULES}}), encoding="utf-8")
            r = _run("--modules", str(modules), "--pin", str(pin))
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            first = r.stdout.splitlines()[0]
            self.assertTrue(first.startswith("modules: TAOM_Map v2.0.27 sha256:"), first)
            self.assertIn(", LOTRLOME_Armory v2.0.26 sha256:", first)

    def test_exit_1_on_findings(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td, map_version="v2.0.29.5")
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"))
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            self.assertIn("MODULE_VERSION", r.stdout)
            self.assertIn("PIN_MISSING", r.stdout)

    def test_exit_2_on_a_missing_taom_map(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            (modules / "TAOM_Map" / "ModuleData" / "settlements.xml").unlink()
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"))
            self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_exit_2_on_a_missing_schema(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            r = _run("--modules", str(modules), "--game-root", str(Path(td) / "nowhere"),
                     "--pin", str(Path(td) / "none.json"))
            self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
            self.assertIn("Monsters.xsd", r.stderr)

    def test_exit_2_on_settlements_that_do_not_parse(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            _write(modules / "TAOM_Map" / "ModuleData" / "settlements.xml", "<broken")
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"))
            self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
            self.assertIn("ERROR:", r.stderr)
            self.assertIn("settlements.xml", r.stderr)
            self.assertNotIn("Traceback", r.stderr)

    def test_exit_2_on_a_schema_that_does_not_parse(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            _write(modules.parent / "XmlSchemas" / "Monsters.xsd", "<broken")
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"))
            self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
            self.assertIn("ERROR:", r.stderr)
            self.assertIn("Monsters.xsd", r.stderr)
            self.assertNotIn("Traceback", r.stderr)

    def test_an_armory_xml_that_does_not_parse_is_a_note_naming_it(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            _write(modules / "LOTRLOME_Armory" / "ModuleData" / "bad_monsters.xml", "<broken")
            pin = Path(td) / "pin.json"
            pin.write_text(json.dumps({"modules": {
                n: crp.module_identity(modules / n) for n in crp.PINNED_MODULES}}), encoding="utf-8")
            r = _run("--modules", str(modules), "--pin", str(pin))
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("note: ModuleData/bad_monsters.xml does not parse", r.stdout)

    def test_record_writes_the_json_ignoring_the_missing_pin(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            _write(modules / "LOTRLOME_Armory" / "ModuleData" / "monsters.xml",
                   '<Monsters><Monster id="human" taom_body_length="1.0" /></Monsters>')
            out = Path(td) / "new_pin.json"
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"), "--record", str(out))
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            pin = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(sorted(pin), ["modules", "pairing_signals", "recorded_utc"])
            self.assertEqual(sorted(pin["modules"]), sorted(crp.PINNED_MODULES))
            self.assertIn("taom_body_length", pin["pairing_signals"])

    def test_record_refuses_when_a_finding_exists(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td, armory_version="v2.0.31")
            out = Path(td) / "new_pin.json"
            r = _run("--modules", str(modules), "--pin", str(Path(td) / "none.json"), "--record", str(out))
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            self.assertFalse(out.exists())

    def test_nothing_is_written_without_record(self):
        with tempfile.TemporaryDirectory() as td:
            modules = _complete(td)
            pin = Path(td) / "pin.json"
            _run("--modules", str(modules), "--pin", str(pin))
            self.assertFalse(pin.exists())


if __name__ == "__main__":
    unittest.main()
