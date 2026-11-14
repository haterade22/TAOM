#!/usr/bin/env python3
"""tools/check_release_pair.py: a release channel's TAOM and TAOM_Map must agree (rca-ered-luin-2026-10-27.md row 2).

Every test builds a synthetic channel and a synthetic install, because a channel under
E:/LOTRAOM_Releases is the maintainer's and holds no vanilla module."""
import io
import os
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_release_pair as crp  # noqa: E402

CULTURES = '<SPCultures>\n  <Culture id="realm_a" />\n</SPCultures>\n'
CLANS = ('<Factions>\n  <Faction id="clan_a_1" culture="Culture.realm_a" '
         'initial_home_settlement="Settlement.town_a1" />\n</Factions>\n')
SETTLEMENTS = ('<Settlements>\n  <Settlement id="town_a1" owner="Faction.clan_a_1" culture="Culture.realm_a">\n'
               '    <Locations complex_template="LocationComplexTemplate.town_complex">\n'
               '      <Location id="prison" scene_name="empire_dungeon_stealth" />\n'
               '    </Locations>\n  </Settlement>\n'
               '</Settlements>\n')
VANILLA_CLANS = '<Factions>\n  <Faction id="clan_vanilla_1" culture="Culture.empire" />\n</Factions>\n'
# A 1.5.x prison break spawns its guards on stealth_agent and its reinforcements on
# prison_break_reinforcement_point; the pre-stealth *_dungeon_a scenes carry neither.
STEALTH_SCENE = ('<scene><entity><tag name="stealth_agent"/></entity><entity>'
                 '<variable name="SpawnGroupTag" value="prison_break_reinforcement_point"/></entity></scene>\n')
OLD_SCENE = '<scene><entity name="chair"><tag name="npc_common"/></entity></scene>\n'
STRIP = ('<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
         '  <xsl:template match="Settlement"/>\n</xsl:stylesheet>\n')


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class ReleasePairTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "channel" / "Modules"
        self.install = root / "install" / "Modules"
        self.taom = self.modules / "TAOM" / "ModuleData"
        self.map = self.modules / "TAOM_Map" / "ModuleData"
        _write(self.taom / "taom_spcultures.xml", CULTURES)
        _write(self.taom / "characters" / "clans.xml", CLANS)
        _write(self.map / "settlements.xml", SETTLEMENTS)
        _write(self.map / "settlements.xslt", STRIP)
        _write(self.install / "SandBox" / "ModuleData" / "spclans.xml", VANILLA_CLANS)
        # The channel holds no SandBox, so the vanilla dungeon scenes come from the install.
        _write(self.install / "SandBox" / "SceneObj" / "empire_dungeon_stealth" / "scene.xscene", STEALTH_SCENE)
        _write(self.install / "SandBox" / "SceneObj" / "empire_dungeon_a" / "scene.xscene", OLD_SCENE)

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = crp.main([str(self.modules), "--vanilla-modules", str(self.install)])
        return code, out.getvalue(), err.getvalue()

    def _assert_finds(self, code_name):
        code, out, _ = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn(code_name, out)
        self.assertNotIn("PASS:", out)

    def test_a_pair_that_agrees_passes(self):
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS:", out)

    def test_the_map_names_a_culture_the_taom_lacks(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace(
            "</Settlements>",
            '  <Settlement id="town_b1" owner="Faction.clan_a_1" culture="Culture.realm_b" />\n</Settlements>'))
        self._assert_finds("UNKNOWN_CULTURE")

    def test_the_map_seats_a_settlement_on_a_clan_the_taom_lacks(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace("Faction.clan_a_1", "Faction.clan_b_1"))
        self._assert_finds("BROKEN_FACTION_REF")

    def test_the_taom_names_a_settlement_the_map_lacks(self):
        _write(self.taom / "characters" / "clans.xml", CLANS.replace("Settlement.town_a1", "Settlement.town_b1"))
        self._assert_finds("BROKEN_SETTLEMENT_REF")

    def test_a_taom_culture_with_no_settlement_on_the_map(self):
        _write(self.taom / "taom_spcultures.xml", CULTURES.replace(
            "</SPCultures>", '  <Culture id="realm_b" />\n</SPCultures>'))
        _write(self.taom / "characters" / "clans.xml", CLANS.replace(
            "</Factions>",
            '  <Faction id="clan_b_1" culture="Culture.realm_b" />\n</Factions>'))
        self._assert_finds("LANDLESS_CULTURE")

    def test_a_vanilla_clan_resolves_through_the_install(self):
        # The channel holds no SandBox, so without the second root this owner would read as broken.
        _write(self.map / "settlements.xml", SETTLEMENTS.replace(
            "</Settlements>",
            '  <Settlement id="town_a2" owner="Faction.clan_vanilla_1" culture="Culture.realm_a" />\n</Settlements>'))
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)

    def test_a_clan_the_taom_strips_does_not_resolve(self):
        _write(self.taom / "spclans.xslt",
               '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
               '  <xsl:template match="Faction[@id=\'clan_vanilla_1\']"/>\n</xsl:stylesheet>\n')
        _write(self.map / "settlements.xml", SETTLEMENTS.replace(
            "</Settlements>",
            '  <Settlement id="town_a2" owner="Faction.clan_vanilla_1" culture="Culture.realm_a" />\n</Settlements>'))
        self._assert_finds("BROKEN_FACTION_REF")

    def test_a_file_the_game_never_loads_is_not_read(self):
        # custom_settlements.xml and TAOM's own settlements.xml are in no SubModule.xml row.
        for name in ("custom_settlements.xml", "settlements.xml"):
            _write(self.taom / name, '<Settlements><Village bound="Settlement.town_none" /></Settlements>\n')
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)

    def test_an_item_ref_outside_the_pair_is_not_reported(self):
        # The channel has no vanilla items, so every other code would read as a flood of false findings.
        _write(self.taom / "characters" / "troops.xml", '<NPCCharacters><x item="Item.none_such" /></NPCCharacters>\n')
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)

    def test_a_channel_with_no_map_is_refused_not_passed(self):
        (self.map / "settlements.xml").unlink()
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("PASS:", out)
        self.assertIn("settlements.xml", err)

    def test_a_channel_with_no_taom_clans_is_refused_not_passed(self):
        (self.taom / "characters" / "clans.xml").unlink()
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("PASS:", out)
        self.assertIn("clans.xml", err)

    def test_an_install_with_no_vanilla_clans_is_refused_not_passed(self):
        (self.install / "SandBox" / "ModuleData" / "spclans.xml").unlink()
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("PASS:", out)
        self.assertIn("spclans.xml", err)

    def test_a_map_with_no_settlement_rows_is_refused_not_passed(self):
        # An empty world switches the settlement and landless passes off, which reads as a clean run.
        _write(self.map / "settlements.xml", "<Settlements>\n</Settlements>\n")
        code, out, err = self._run()
        self.assertEqual(code, 2)
        self.assertNotIn("PASS:", out)
        self.assertIn("no settlement", err)


    def test_a_home_written_as_xslt_text_that_the_map_lacks_is_found(self):
        # spclans.xslt writes its homes as <xsl:attribute> element text, which no XML attribute read sees.
        _write(self.taom / "spclans.xslt",
               '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
               '  <xsl:template match="Faction">\n'
               '    <xsl:attribute name="initial_home_settlement">Settlement.town_b1</xsl:attribute>\n'
               '  </xsl:template>\n</xsl:stylesheet>\n')
        self._assert_finds("BROKEN_SETTLEMENT_REF")

    def test_a_home_written_as_xslt_text_that_the_map_holds_passes(self):
        _write(self.taom / "spclans.xslt",
               '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">\n'
               '  <xsl:attribute name="initial_home_settlement">Settlement.town_a1</xsl:attribute>\n'
               '</xsl:stylesheet>\n')
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)

    def test_a_prison_on_a_pre_stealth_scene_is_found(self):
        # Orthanc and Orthanc Gate shipped empire_dungeon_a (2026-11-13); every package must carry the repair.
        _write(self.map / "settlements.xml", SETTLEMENTS.replace("empire_dungeon_stealth", "empire_dungeon_a"))
        code, out, _ = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("PRISON_SCENE_NOT_STEALTH", out)
        self.assertIn("town_a1", out)
        self.assertIn("empire_dungeon_a", out)
        self.assertNotIn("PASS:", out)

    def test_a_pre_stealth_prison_in_a_higher_level_slot_is_found(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace(
            'scene_name="empire_dungeon_stealth"',
            'scene_name="empire_dungeon_stealth" scene_name_2="empire_dungeon_a"'))
        self._assert_finds("PRISON_SCENE_NOT_STEALTH")

    def test_a_prison_scene_the_channel_ships_itself_is_read_from_the_channel(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace("empire_dungeon_stealth", "taom_orthanc_dungeon"))
        _write(self.modules / "TAOM_Map" / "SceneObj" / "taom_orthanc_dungeon" / "scene.xscene", STEALTH_SCENE)
        code, out, _ = self._run()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS:", out)

    def test_a_prison_scene_no_module_ships_is_not_passed(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace("empire_dungeon_stealth", "none_such_dungeon"))
        self._assert_finds("PRISON_SCENE_NOT_STEALTH")

    def test_a_map_with_no_prison_rows_is_refused_not_passed(self):
        # The prison check would have judged nothing, which must not read as a pass.
        _write(self.map / "settlements.xml", SETTLEMENTS.replace('id="prison"', 'id="tavern"'))
        self._assert_refused("prison")

    def _assert_refused(self, needle):
        code, out, err = self._run()
        self.assertEqual(code, 2, out + err)
        self.assertNotIn("PASS:", out)
        self.assertIn(needle, err)

    def test_a_map_file_that_does_not_parse_is_refused_not_passed(self):
        _write(self.map / "settlements.xml", SETTLEMENTS.replace("</Settlements>", "  <Settlement id=\"town_a2\"\n"))
        self._assert_refused("settlements.xml")

    def test_a_taom_clans_file_that_does_not_parse_is_refused_not_passed(self):
        _write(self.taom / "characters" / "clans.xml", "<Factions><Faction id=")
        self._assert_refused("clans.xml")

    def test_a_vanilla_clans_file_that_does_not_parse_is_refused_not_passed(self):
        _write(self.install / "SandBox" / "ModuleData" / "spclans.xml", "<Factions>")
        self._assert_refused("spclans.xml")

    def test_a_map_with_no_settlements_xslt_is_refused_not_passed(self):
        # Without the strip, vanilla's settlement rows would return in game.
        (self.map / "settlements.xslt").unlink()
        self._assert_refused("settlements.xslt")


class EntryPointTests(unittest.TestCase):
    """A crash must exit 3. Python's own code for an uncaught exception is 1, the code the tool documents
    as a disagreement, so a folder named folder.xml read as a verdict on the pair."""

    def test_unexpected_exception_exits_3_with_the_traceback(self):
        err = io.StringIO()
        with mock.patch.object(crp, "main", side_effect=RuntimeError("boom")), redirect_stderr(err):
            code = crp.entry()
        self.assertEqual(code, 3)
        self.assertIn("Traceback", err.getvalue())

    def test_a_normal_exit_code_passes_through(self):
        for rc in (0, 1, 2):
            with self.subTest(rc=rc), mock.patch.object(crp, "main", return_value=rc):
                self.assertEqual(crp.entry(), rc)

    def test_a_folder_named_like_an_xml_file_exits_3_from_the_command_line(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            modules, install = root / "channel" / "Modules", root / "install" / "Modules"
            taom, map_ = modules / "TAOM" / "ModuleData", modules / "TAOM_Map" / "ModuleData"
            _write(taom / "taom_spcultures.xml", CULTURES)
            _write(taom / "characters" / "clans.xml", CLANS)
            _write(map_ / "settlements.xml", SETTLEMENTS)
            _write(map_ / "settlements.xslt", STRIP)
            _write(install / "SandBox" / "ModuleData" / "spclans.xml", VANILLA_CLANS)
            (taom / "folder.xml").mkdir()
            run = subprocess.run([sys.executable, "-B", str(Path(crp.__file__).resolve()), str(modules),
                                  "--vanilla-modules", str(install)], capture_output=True, text=True, timeout=120)
        self.assertEqual(run.returncode, 3, run.stdout + run.stderr[-2000:])
        self.assertIn("Traceback", run.stderr)


if __name__ == "__main__":
    unittest.main()
