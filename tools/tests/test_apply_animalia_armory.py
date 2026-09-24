#!/usr/bin/env python3
"""Unit tests for the Animalia elk and moose armory writer (tools/apply_animalia_armory.py).

Pure stdlib, synthetic game dir, no real install needed (--game-dir points the whole script at a
temp tree). Siblings: test_apply_rohan_spear_reforge.py, test_apply_dead_mesh_item_swaps.py.

Every case builds its own pristine tree under a TemporaryDirectory, because the script mutates
files in place and a shared fixture would let one test's writes leak into the next.
"""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_animalia_armory as aa  # noqa: E402

BOM = b"\xef\xbb\xbf"

# Every act_horse_* action either OVERRIDES table binds, for the synthetic Native as_horse set.
ALL_HORSE_ACTIONS = sorted({action for overrides in aa.OVERRIDES.values() for action in overrides})


def _native_action_sets_xml():
    rows = "".join('\t\t<action type="%s" animation="horse_template_%d" />\n' % (a, i)
                   for i, a in enumerate(ALL_HORSE_ACTIONS))
    return ("<action_sets>\n"
            "\t<action_set id=\"as_horse\" skeleton=\"horse_skeleton\">\n"
            + rows +
            "\t</action_set>\n"
            "</action_sets>\n")


ARMORY_ACTION_SETS = (
    "<action_sets>\r\n"
    "\t<!-- ============================== WARG ============================== -->\r\n"
    "\t<action_set id=\"as_warg\" skeleton=\"warg_skeleton\" base_set=\"as_horse\" />\r\n"
    "\t<action_set id=\"as_war_ram\" skeleton=\"horse_skeleton\" base_set=\"as_horse\">\r\n"
    "\t\t<action type=\"act_war_ram_butt\" animation=\"war_ram_butt\" />\r\n"
    "\t</action_set>\r\n"
    "\t<!-- ============================== /WARG ============================== -->\r\n"
    "</action_sets>\r\n"
)

ARMORY_ACTION_TYPES = (
    "<action_types>\r\n"
    "\t<action name=\"act_war_ram_butt\" type=\"actt_kick\" />\r\n"
    "</action_types>\r\n"
)

ARMORY_ACTION_TYPES_NO_RAM = (
    "<action_types>\r\n"
    "</action_types>\r\n"
)

ARMORY_HORSES = (
    "<Items>\r\n"
    "  <Item\r\n"
    "      id=\"taom_elk_saddle_a\"\r\n"
    "      name=\"{=taom_elk_saddle_a}[Mirkwood] Elk Saddle\"\r\n"
    "      mesh=\"elk_saddle_001\"\r\n"
    "      Type=\"HorseHarness\">\r\n"
    "      <ItemComponent><HorseHarness body_armor=\"45\" material_type=\"Leather\" family_type=\"1\" /></ItemComponent>\r\n"
    "  </Item>\r\n"
    "</Items>\r\n"
)

ARMORY_SUBMODULE = (
    "<Module>\r\n"
    "\t<Xmls>\r\n"
    "\t\t<XmlNode>\r\n"
    "\t\t\t<XmlName id=\"Monsters\" path=\"Monsters/LOTR/lotr_monster_elk\"/>\r\n"
    "\t\t\t<IncludedGameTypes>\r\n"
    "\t\t\t\t<GameType value = \"Campaign\"/>\r\n"
    "\t\t\t</IncludedGameTypes>\r\n"
    "\t\t</XmlNode>\r\n"
    "\t</Xmls>\r\n"
    "</Module>\r\n"
)


def _all_wanted_clip_stems():
    stems = {"anim_animalia_%s_%s" % (animal, stem)
             for animal, overrides in aa.OVERRIDES.items() for stem in overrides.values()}
    stems |= {clip for _, clip in aa.ANTLER.values()}
    return stems


def _write_bom_crlf(path: Path, text: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(BOM + text.encode("utf-8"))


def _build_pristine_tree(root: Path, action_types=ARMORY_ACTION_TYPES,
                          clip_stems=None):
    """A synthetic $BANNERLORD_GAME_DIR with everything steps 1-5 need already present, per the
    live shape: a Native as_horse set covering every action the script overrides, an Armory
    action_sets.xml with the /WARG marker and an as_war_ram set binding act_war_ram_butt,
    action_types.xml declaring it, LOTRAOM_horses.xml with taom_elk_saddle_a, SubModule.xml with
    the great elk's Monsters registration, and one placeholder _anm.tpac per clip the script binds."""
    native_sets = root / "Modules" / "Native" / "ModuleData" / "action_sets.xml"
    native_sets.parent.mkdir(parents=True, exist_ok=True)
    native_sets.write_text(_native_action_sets_xml(), encoding="utf-8")

    armory = root / "Modules" / "LOTRLOME_Armory"
    # The live Armory already holds the great elk's Monster file in this folder; the script
    # relies on Monsters/LOTR/ existing (it creates no directories of its own).
    _write_bom_crlf(armory / "ModuleData" / "Monsters" / "LOTR" / "lotr_monster_elk.xml",
                    "<Monsters>\r\n\t<Monster id=\"taom_elk\" base_monster=\"horse\" action_set=\"as_war_ram\" "
                    "weight=\"500\" hit_points=\"250\" />\r\n</Monsters>\r\n")
    _write_bom_crlf(armory / "SubModule.xml", ARMORY_SUBMODULE)
    _write_bom_crlf(armory / "ModuleData" / "action_sets.xml", ARMORY_ACTION_SETS)
    _write_bom_crlf(armory / "ModuleData" / "action_types.xml", action_types)
    _write_bom_crlf(armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_horses.xml", ARMORY_HORSES)

    clips_dir = armory / "Assets" / "creature" / "elk" / "animations"
    clips_dir.mkdir(parents=True, exist_ok=True)
    for stem in (clip_stems if clip_stems is not None else _all_wanted_clip_stems()):
        (clips_dir / (stem + "_anm.tpac")).write_bytes(b"placeholder")
    return root


# The files the script may touch; used to snapshot / diff a tree across a run.
_TRACKED_RELATIVE = [
    "Modules/LOTRLOME_Armory/SubModule.xml",
    "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml",
    "Modules/LOTRLOME_Armory/ModuleData/action_types.xml",
    "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml",
    "Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_animalia.xml",
]


def _snapshot(root: Path):
    out = {}
    for rel in _TRACKED_RELATIVE:
        p = root / rel
        out[rel] = p.read_bytes() if p.is_file() else None
    return out


def _backup_files(root: Path):
    armory = root / "Modules" / "LOTRLOME_Armory"
    return sorted(str(p.relative_to(root)) for p in armory.rglob("*") if ".bak" in p.name)


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *argv, running=False):
        buf = io.StringIO()
        args = list(argv) + ["--game-dir", str(self.root)]
        with redirect_stdout(buf), mock.patch.object(aa, "game_or_kit_running", return_value=running):
            code = aa.main(args)
        return code, buf.getvalue()

    # (a) pristine dry run: exit 0, all five steps printed, every file byte-identical after.
    def test_pristine_dry_run_prints_all_five_steps_and_writes_nothing(self):
        _build_pristine_tree(self.root)
        before = _snapshot(self.root)
        code, out = self._run()
        self.assertEqual(code, 0, out)
        for marker in ("Monsters: new lotr_monster_animalia.xml",
                       "SubModule.xml: Monsters block inserted",
                       "action_sets.xml: as_animalia_elk (",
                       "LOTRAOM_horses.xml: taom_animalia_elk_a",
                       "action_types.xml: declares",
                       "as_animalia_elk binds act_animalia_elk_antler",
                       "as_animalia_moose binds act_animalia_moose_antler"):
            self.assertIn(marker, out, out)
        self.assertIn("dry run", out)
        after = _snapshot(self.root)
        self.assertEqual(before, after)
        self.assertEqual(_backup_files(self.root), [])

    # (b) one antler clip missing, --apply: exit 1, every file byte-identical.
    def test_apply_with_one_antler_clip_missing_refuses_and_writes_nothing(self):
        stems = _all_wanted_clip_stems() - {aa.ANTLER["elk"][1]}
        _build_pristine_tree(self.root, clip_stems=stems)
        before = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("CLIP MISSING: %s" % aa.ANTLER["elk"][1], out)
        self.assertNotIn("already declared", out, "a refused run must not claim the antler actions exist")
        self.assertNotIn("already binds", out)
        after = _snapshot(self.root)
        self.assertEqual(before, after)
        self.assertEqual(_backup_files(self.root), [])

    # (c) act_war_ram_butt missing, --apply: exit 1, nothing written, names the war ram ledger.
    def test_apply_with_no_war_ram_anchor_refuses_and_names_the_ledger(self):
        _build_pristine_tree(self.root, action_types=ARMORY_ACTION_TYPES_NO_RAM)
        before = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("docs/reference/lotrlome-war-ram-changes.md", out)
        self.assertNotIn("already declared", out, "a refused run must not claim the antler actions exist")
        self.assertNotIn("already binds", out)
        after = _snapshot(self.root)
        self.assertEqual(before, after)
        self.assertEqual(_backup_files(self.root), [])

    # (d) --apply then --apply again: second run changes no byte, BOM/CRLF preserved, the
    # Monsters carry taom_body_length 100/150, the two items keep Items.xsd's required body_length at the
    # placeholder 100 (TAOM overrides it from the Monster at game init).
    def test_apply_then_apply_again_is_byte_stable_and_writes_the_new_size_recipe(self):
        _build_pristine_tree(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)

        mon_path = self.root / "Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_animalia.xml"
        mon_text = mon_path.read_text(encoding="utf-8")
        self.assertIn('taom_body_length="100"', mon_text)
        self.assertIn('taom_body_length="150"', mon_text)

        horses_path = self.root / "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml"
        horses_bytes = horses_path.read_bytes()
        self.assertTrue(horses_bytes.startswith(BOM), "BOM must survive the write")
        horses_text = horses_bytes.decode("utf-8")
        self.assertIn("\r\n", horses_text, "CRLF must survive the write")
        for item_id in ("taom_animalia_elk_a", "taom_animalia_moose_a"):
            import re
            m = re.search(r'<Item\s+id="%s".*?</Item>' % item_id, horses_text, re.S)
            self.assertIsNotNone(m, item_id)
            self.assertIn('body_length="100"', m.group(0), "%s must keep the placeholder body_length" % item_id)
            self.assertNotIn('body_length="150"', m.group(0), "%s must not carry the Monster's size" % item_id)

        submodule_path = self.root / "Modules/LOTRLOME_Armory/SubModule.xml"
        self.assertTrue(submodule_path.read_bytes().startswith(BOM))
        self.assertIn(b"\r\n", submodule_path.read_bytes())

        after_first = _snapshot(self.root)
        code2, out2 = self._run("--apply")
        self.assertEqual(code2, 0, out2)
        after_second = _snapshot(self.root)
        self.assertEqual(after_first, after_second, "a second --apply must change no byte")

    def _break(self, rel, old, new):
        p = self.root / rel
        data = p.read_bytes()
        self.assertEqual(data.count(old.encode("utf-8")), 1, "the fixture no longer holds %r once" % old)
        p.write_bytes(data.replace(old.encode("utf-8"), new.encode("utf-8")))

    def _assert_refused_unchanged(self, before, expect):
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn(expect, out)
        self.assertEqual(before, _snapshot(self.root))
        self.assertEqual(_backup_files(self.root), [])

    def test_apply_refuses_without_the_great_elks_submodule_anchor(self):
        _build_pristine_tree(self.root)
        self._break("Modules/LOTRLOME_Armory/SubModule.xml", "Monsters/LOTR/lotr_monster_elk", "Monsters/LOTR/lotr_monster_gone")
        self._assert_refused_unchanged(_snapshot(self.root), "lotrlome-elk-changes.md")

    def test_apply_refuses_without_the_warg_marker(self):
        _build_pristine_tree(self.root)
        self._break("Modules/LOTRLOME_Armory/ModuleData/action_sets.xml", " /WARG ", " /GONE ")
        self._assert_refused_unchanged(_snapshot(self.root), "/WARG marker not found")

    def test_apply_refuses_without_the_elk_saddle_anchor(self):
        _build_pristine_tree(self.root)
        self._break("Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml",
                    'id="taom_elk_saddle_a"', 'id="taom_gone_saddle"')
        self._assert_refused_unchanged(_snapshot(self.root), '"taom_elk_saddle_a" not found')

    def test_apply_refuses_when_an_override_is_not_an_as_horse_action(self):
        _build_pristine_tree(self.root)
        action = next(iter(aa.OVERRIDES["elk"]))
        native = self.root / "Modules" / "Native" / "ModuleData" / "action_sets.xml"
        text = native.read_text(encoding="utf-8")
        self.assertIn('type="%s"' % action, text)
        native.write_text(text.replace('type="%s"' % action, 'type="act_removed_for_the_test"'), encoding="utf-8")
        self._assert_refused_unchanged(_snapshot(self.root), "NOT AN ACTION: ")

    def test_apply_refuses_when_a_gait_clip_is_missing(self):
        stem = "anim_animalia_elk_" + next(iter(aa.OVERRIDES["elk"].values()))
        _build_pristine_tree(self.root, clip_stems=_all_wanted_clip_stems() - {stem})
        self._assert_refused_unchanged(_snapshot(self.root), "CLIP MISSING: %s" % stem)

    def test_dry_run_never_asks_whether_the_game_runs(self):
        _build_pristine_tree(self.root)
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(aa, "game_or_kit_running",
                                                     side_effect=AssertionError("guard called on a read-only path")):
            code = aa.main(["--game-dir", str(self.root)])
        self.assertEqual(code, 0, buf.getvalue())

    def test_apply_backs_up_each_changed_file_once_with_its_pristine_bytes(self):
        _build_pristine_tree(self.root)
        pristine = _snapshot(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        expected = {
            "Modules/LOTRLOME_Armory/SubModule.xml": aa.BACKUP_SUFFIX,
            "Modules/LOTRLOME_Armory/ModuleData/action_sets.xml": aa.BACKUP_SUFFIX,
            "Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml": aa.BACKUP_SUFFIX,
            "Modules/LOTRLOME_Armory/ModuleData/action_types.xml": aa.ANTLER_BACKUP,
        }
        for rel, suffix in expected.items():
            backup = self.root / (rel + suffix)
            self.assertTrue(backup.is_file(), "no %s backup for %s" % (suffix, rel))
            self.assertEqual(backup.read_bytes(), pristine[rel], "%s's backup must hold its pristine bytes" % rel)
        self.assertEqual(len(_backup_files(self.root)), len(expected), "the new Monsters file needs no backup")
        for rel in expected:
            text = (self.root / rel).read_bytes().decode("utf-8")
            self.assertNotRegex(text, r"(?<!\r)\n", "%s gained a bare LF" % rel)
        backups_first = {p: (self.root / p).read_bytes() for p in _backup_files(self.root)}
        code2, _ = self._run("--apply")
        self.assertEqual(code2, 0)
        self.assertEqual(backups_first, {p: (self.root / p).read_bytes() for p in _backup_files(self.root)},
                         "a second --apply must not touch the backups")

    def test_a_game_dir_without_the_armory_exits_2(self):
        from contextlib import redirect_stderr
        with self.assertRaises(SystemExit) as cm, redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            aa.main(["--game-dir", str(self.root / "nowhere")])
        self.assertEqual(cm.exception.code, 2)

    def test_an_empty_game_dir_exits_2_rather_than_falling_back_to_the_live_install(self):
        # An unset variable passed as --game-dir "" must not silently target the live Armory.
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(aa, "game_dir", side_effect=AssertionError("fell back to the install")):
            code = aa.main(["--game-dir", ""])
        self.assertEqual(code, 2, buf.getvalue())
        self.assertIn("--game-dir", buf.getvalue())

    def _applied_tree(self):
        _build_pristine_tree(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)

    def test_a_half_present_item_pair_is_refused_naming_the_missing_item(self):
        # The presence check used to key on the elk's id alone: with the elk there and the moose gone, step 4
        # said "already present" and the moose never came back.
        self._applied_tree()
        self._break("Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml",
                    'id="taom_animalia_moose_a"', 'id="taom_gone_moose_a"')
        before = _snapshot(self.root)
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("taom_animalia_moose_a", out)
        self.assertEqual(before, _snapshot(self.root))

    def test_a_missing_map_twin_is_refused_naming_the_set(self):
        self._applied_tree()
        self._break("Modules/LOTRLOME_Armory/ModuleData/action_sets.xml",
                    'id="as_animalia_moose_map"', 'id="as_gone_moose_map"')
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("as_animalia_moose_map", out)

    def test_a_monsters_file_missing_one_monster_is_refused(self):
        self._applied_tree()
        self._break("Modules/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_animalia.xml",
                    'id="taom_animalia_moose"', 'id="taom_gone_moose"')
        code, out = self._run()
        self.assertEqual(code, 1, out)
        self.assertIn("taom_animalia_moose", out)

    def test_an_apply_with_nothing_to_write_says_so_and_names_no_files(self):
        self._applied_tree()
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("nothing to write", out)
        self.assertNotIn("APPLIED", out)

    def test_an_apply_names_every_file_it_wrote(self):
        _build_pristine_tree(self.root)
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("APPLIED 5 file(s)", out)
        for name in ("lotr_monster_animalia.xml", "action_sets.xml", "action_types.xml", "LOTRAOM_horses.xml", "SubModule.xml"):
            self.assertIn(name, out)

    # (e) game_or_kit_running mocked True: --apply returns 2 and nothing changes.
    def test_apply_refuses_while_game_or_kit_runs(self):
        _build_pristine_tree(self.root)
        before = _snapshot(self.root)
        code, out = self._run("--apply", running=True)
        self.assertEqual(code, 2, out)
        self.assertIn("REFUSED", out)
        after = _snapshot(self.root)
        self.assertEqual(before, after)
        self.assertEqual(_backup_files(self.root), [])


class LiveRecipeParityTests(unittest.TestCase):
    """The ledger's reinstall path replays this script, so its recipe must match what the live Armory holds: a
    live hand edit (a resize, a stat change) that skips the recipe would be silently reverted by the next replay
    (RCA rca-animalia-2026-09-23.md row 1, the moose written back at 100). Compares attributes, not comments.
    Skipped where the install is absent (CI, the laptop)."""

    def setUp(self):
        import xml.etree.ElementTree as ET
        self.ET = ET
        root = os.environ.get("BANNERLORD_GAME_DIR") or aa._DEFAULT_GAME_DIR
        self.md = Path(root) / "Modules" / "LOTRLOME_Armory" / "ModuleData"
        if not (self.md / "Monsters" / "LOTR" / "lotr_monster_animalia.xml").is_file():
            self.skipTest("LOTRLOME_Armory with the Animalia Monsters is not installed here")

    def _by_id(self, elements):
        return {e.get("id"): e for e in elements}

    def test_every_recipe_monster_matches_the_live_monster_attribute_for_attribute(self):
        recipe = self._by_id(self.ET.fromstring(aa.MONSTER_FILE.split("?>", 1)[1]).iter("Monster"))
        live = self._by_id(self.ET.parse(self.md / "Monsters" / "LOTR" / "lotr_monster_animalia.xml").getroot().iter("Monster"))
        self.assertEqual(sorted(recipe), sorted(live))
        for mid, m in recipe.items():
            self.assertEqual(dict(m.attrib), dict(live[mid].attrib), "Monster %s differs from the recipe" % mid)

    def test_every_recipe_item_matches_the_live_item_and_its_horse(self):
        recipe = self._by_id(self.ET.fromstring("<Items>" + aa.ITEMS + "</Items>").iter("Item"))
        live_all = self._by_id(self.ET.parse(self.md / "LOTRLOME_items" / "LOTRAOM_horses.xml").getroot().iter("Item"))
        for iid, item in recipe.items():
            self.assertIn(iid, live_all, "%s is missing from the live Armory" % iid)
            live = live_all[iid]
            self.assertEqual(dict(item.attrib), dict(live.attrib), "Item %s differs from the recipe" % iid)
            self.assertEqual(dict(item.find(".//Horse").attrib), dict(live.find(".//Horse").attrib),
                             "%s's <Horse> differs from the recipe" % iid)


class NlOfTests(unittest.TestCase):
    """tools/README.md "XML I/O convention": pick the newline by majority, not presence."""

    def test_majority_wins_over_one_stray_line(self):
        mostly_lf = "a\n" * 100 + "b\r\n"
        mostly_crlf = "a\r\n" * 100 + "b\n"
        self.assertEqual(aa.nl_of(mostly_lf), "\n")
        self.assertEqual(aa.nl_of(mostly_crlf), "\r\n")


class AntlerEditsTests(unittest.TestCase):
    """antler_edits is a pure function: no file I/O, so these run with no game dir at all."""

    def _sets_text(self):
        return (
            "<action_sets>\n"
            "\t<action_set id=\"as_animalia_elk\" skeleton=\"horse_skeleton\" base_set=\"as_horse\">\n"
            "\t\t<action type=\"act_horse_forward_walk\" animation=\"anim_animalia_elk_loco_walk\" />\n"
            "\t</action_set>\n"
            "\t<action_set id=\"as_animalia_moose\" skeleton=\"horse_skeleton\" base_set=\"as_horse\">\n"
            "\t\t<action type=\"act_horse_forward_walk\" animation=\"anim_animalia_moose_loco_walk\" />\n"
            "\t</action_set>\n"
            "</action_sets>\n"
        )

    def _types_text(self):
        return "<action_types>\n\t<action name=\"act_war_ram_butt\" type=\"actt_kick\" />\n</action_types>\n"

    def test_binds_both_animals_and_declares_both_actions(self):
        clips = {clip for _, clip in aa.ANTLER.values()}
        types_after, sets_after, problems = aa.antler_edits(self._types_text(), self._sets_text(), clips)
        self.assertEqual(problems, [])
        for action, clip in aa.ANTLER.values():
            self.assertIn('name="%s"' % action, types_after)
            self.assertIn('type="%s" animation="%s"' % (action, clip), sets_after)

    def test_is_idempotent(self):
        clips = {clip for _, clip in aa.ANTLER.values()}
        once = aa.antler_edits(self._types_text(), self._sets_text(), clips)
        twice = aa.antler_edits(once[0], once[1], clips)
        self.assertEqual(once[:2], twice[:2])
        self.assertEqual(twice[2], [])

    def test_missing_clip_is_a_problem_and_nothing_changes(self):
        types_after, sets_after, problems = aa.antler_edits(self._types_text(), self._sets_text(), set())
        self.assertEqual(types_after, self._types_text())
        self.assertEqual(sets_after, self._sets_text())
        self.assertTrue(any("CLIP MISSING" in p for p in problems))

    def test_missing_war_ram_anchor_names_the_ledger(self):
        clips = {clip for _, clip in aa.ANTLER.values()}
        no_ram = "<action_types>\n</action_types>\n"
        _, _, problems = aa.antler_edits(no_ram, self._sets_text(), clips)
        self.assertTrue(any("docs/reference/lotrlome-war-ram-changes.md" in p for p in problems))

    def test_missing_action_set_is_a_problem_naming_the_animal(self):
        clips = {clip for _, clip in aa.ANTLER.values()}
        sets_without_moose = self._sets_text().split('\t<action_set id="as_animalia_moose"')[0] + "</action_sets>\n"
        _, _, problems = aa.antler_edits(self._types_text(), sets_without_moose, clips)
        self.assertTrue(any("as_animalia_moose" in p for p in problems))


if __name__ == "__main__":
    unittest.main(verbosity=2)
