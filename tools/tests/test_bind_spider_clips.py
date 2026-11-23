#!/usr/bin/env python3
"""Tests for tools/bind_spider_clips.py (#1541).

Run:  python -m unittest tools.tests.test_bind_spider_clips

The planners are pure text functions, so they run on small files built here in the live files' shape: CRLF, tabs,
a rider set before the spider's own set, and the June rows. `main` runs on a temporary install with cooked clips.
Some expectations are written out here as literals, not read from the tool's tables: a test that compares the
output with the table that made it cannot see a wrong table.
"""
import io
import os
import re
import sys
import tempfile
import unittest
import uuid
import xml.etree.ElementTree as ET
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bind_spider_clips as b  # noqa: E402

JUNE = {**{a: "an_spi_walk_2" for a in b.REBIND}, "act_spider_run_forward": "an_spi_run",
        "act_spider_idle": "an_spi_idle"}


def rider_set(set_id, extra=""):
    return ('\t<action_set id="%s" skeleton="human_skeleton" movement_system="bipedal">\r\n'
            '\t\t<action type="act_spider_walk_forward" animation="rider_warg_forward_walk" />\r\n'
            '\t\t<action type="act_spider_run_forward" animation="rider_warg_canter" />\r\n'
            '%s\t\t<action type="act_warg_rear" animation="rider_warg_taunt" />\r\n'
            '\t</action_set>\r\n' % (set_id, extra))


def action_sets(riders=("as_human_warrior", "as_dwarf_warrior"), spider_extra="", rider_extra=""):
    rows = "".join('\t\t<action type="%s" animation="%s" />\r\n' % kv for kv in sorted(JUNE.items()))
    return ('<action_sets>\r\n' + "".join(rider_set(r, rider_extra) for r in riders)
            + '\t<action_set id="as_spider"\r\n\t            skeleton="spider_skeleton"\r\n'
            '\t            movement_system="quadrupedal">\r\n' + rows + spider_extra
            + '\t\t<action type="act_spider_attack_front" animation="an_spi_attack_front" />\r\n\t</action_set>\r\n'
            '\t<action_set id="as_spider_town_and_village" skeleton="spider_skeleton" base_set="as_spider">\r\n'
            '\t\t<action type="act_spider_idle" animation="an_spi_idle" />\r\n\t</action_set>\r\n'
            '\t<action_set id="as_elephant" skeleton="elephant_skeleton">\r\n'
            '\t\t<action type="act_elephant_forward_walk" animation="war_elephant_walk" />\r\n\t</action_set>\r\n'
            '</action_sets>\r\n')


def rows_of(text, set_id):
    """The set's rows as the engine reads them: parsed, so a commented-out row is not one."""
    found = [s for s in ET.fromstring(text.encode("utf-8")).iter("action_set") if s.get("id") == set_id]
    return {a.get("type"): a.get("animation") for a in found[0].findall("action")}


def roots(types, sets, usage):
    return {"action_types.xml": ET.fromstring(types.encode("utf-8")), "action_sets.xml": ET.fromstring(sets.encode("utf-8")),
            "monster_usage_sets.xml": ET.fromstring(usage.encode("utf-8"))}


USAGE = ('<monster_usage_sets>\r\n\t<monster_usage_set\r\n\t\tid="spider"\r\n\t\trear_action="act_spider_rear">\r\n'
         '\t\t<monster_usage_upper_body_movements>\r\n'
         '\t\t\t<monster_usage_upper_body_movement pace="1" direction="none" action="act_spider_idle" />\r\n'
         '\t\t</monster_usage_upper_body_movements>\r\n\t\t<monster_usage_movements>\r\n'
         '\t\t\t<!-- Pace 0 -->\r\n'
         '\t\t\t<monster_usage_movement is_left_foot="False" pace="0" direction="front" turn_direction="none" action="act_spider_walk_forward" />\r\n'
         '\t\t</monster_usage_movements>\r\n\t</monster_usage_set>\r\n'
         '\t<monster_usage_set id="elephant">\r\n\t\t<monster_usage_movements>\r\n'
         '\t\t\t<monster_usage_movement is_left_foot="False" pace="0" direction="front" turn_direction="none" action="act_elephant_backward_walk" />\r\n'
         '\t\t</monster_usage_movements>\r\n\t</monster_usage_set>\r\n</monster_usage_sets>\r\n')
TYPES = ('<action_types>\r\n\t<action name="act_spider_idle_1" type="actt_idle" />\r\n'
         '\t<action name="act_spider_taunt" />\r\n\t<action name="act_elephant_forward_trot" />\r\n</action_types>\r\n')


class ActionSetTests(unittest.TestCase):
    def test_the_spider_set_takes_the_new_clips_and_the_new_actions(self):
        out = b.plan_action_sets(action_sets())
        rows = rows_of(out, "as_spider")
        for action, clip in b.REBIND.items():
            self.assertEqual(rows[action], clip, action)
        for action, clip, _ in b.NEW:
            self.assertEqual(rows[action], clip, action)
        self.assertEqual(rows["act_spider_attack_front"], "an_spi_attack_front")     # phase 2: untouched
        self.assertEqual(rows_of(out, "as_spider_town_and_village"), {"act_spider_idle": "spider_idle"})
        self.assertEqual(rows_of(out, "as_elephant"), {"act_elephant_forward_walk": "war_elephant_walk"})

    def test_the_rows_a_reader_would_look_for_are_the_ones_written(self):
        rows = rows_of(b.plan_action_sets(action_sets()), "as_spider")
        for action, clip in (("act_spider_walk_forward", "spider_walk"), ("act_spider_trot_forward", "spider_trot"),
                             ("act_spider_canter_forward", "spider_canter"), ("act_spider_run_forward", "spider_gallop"),
                             ("act_spider_walk_backward", "spider_walk_backwards"), ("act_spider_stand", "spider_stand"),
                             ("act_spider_walk_stand", "spider_walk_inplace"), ("act_spider_run_stand", "spider_gallop_inplace"),
                             ("act_spider_walk_left", "spider_strafe_left"), ("act_spider_turn_right", "spider_turn_right"),
                             ("act_spider_idle", "spider_idle"), ("act_spider_quick_stop", "spider_stand"),
                             ("act_horse_jump_forward", "spider_walk")):
            self.assertEqual(rows[action], clip, action)

    def test_every_rider_set_gets_a_rider_row_for_each_new_action(self):
        out = b.plan_action_sets(action_sets())
        for set_id in ("as_human_warrior", "as_dwarf_warrior"):
            rows = rows_of(out, set_id)
            for action, _, rider in b.NEW:
                self.assertEqual(rows[action], rider, "%s %s" % (set_id, action))
            self.assertEqual(rows["act_spider_stand"], "rider_warg_stand_for_movement")
            self.assertEqual(rows["act_spider_walk_backward"], "rider_warg_walkbackfast")
            self.assertEqual(rows["act_spider_trot_forward"], "rider_warg_trot")
            self.assertEqual(rows["act_spider_run_forward"], "rider_warg_canter")
            self.assertEqual(rows["act_warg_rear"], "rider_warg_taunt")

    def test_a_new_row_that_is_there_on_the_wrong_clip_is_put_right(self):
        # the first version asked only "is there a row", so --check passed a trot action on the walk clip
        wrong = action_sets(spider_extra='\t\t<action type="act_spider_trot_forward" animation="spider_walk" />\r\n',
                            rider_extra='\t\t<action type="act_spider_stand" animation="rider_warg_trot" />\r\n')
        out = b.plan_action_sets(wrong)
        self.assertNotEqual(out, wrong)
        self.assertEqual(rows_of(out, "as_spider")["act_spider_trot_forward"], "spider_trot")
        for set_id in ("as_human_warrior", "as_dwarf_warrior"):
            self.assertEqual(rows_of(out, set_id)["act_spider_stand"], "rider_warg_stand_for_movement")
        self.assertEqual(out.count('type="act_spider_trot_forward"'), 1 + 2)       # one in as_spider, one per rider set

    def test_a_row_inside_a_comment_is_not_a_row(self):
        # the live file holds hundreds of commented-out rows: one must neither satisfy the plan nor be edited
        comment = '\t\t<!-- <action type="act_spider_stand" animation="an_spi_idle" /> -->\r\n'
        out = b.plan_action_sets(action_sets(spider_extra=comment))
        self.assertIn(comment, out)
        self.assertEqual(rows_of(out, "as_spider")["act_spider_stand"], "spider_stand")
        gone = action_sets().replace('\t\t<action type="act_spider_turn_left" animation="an_spi_walk_2" />\r\n',
                                     '\t\t<!-- <action type="act_spider_turn_left" animation="an_spi_walk_2" /> -->\r\n')
        with self.assertRaises(b.BindError) as err:
            b.plan_action_sets(gone)
        self.assertIn("act_spider_turn_left", str(err.exception))

    def test_a_set_inside_a_comment_is_not_the_spider_set(self):
        text = action_sets().replace('<action_sets>\r\n', '<action_sets>\r\n\t<!-- <action_set id="as_spider" skeleton="old">\r\n'
                                     '\t\t<action type="act_spider_idle" animation="gone" />\r\n\t</action_set> -->\r\n')
        out = b.plan_action_sets(text)
        self.assertIn('animation="gone"', out)
        self.assertEqual(rows_of(out, "as_spider")["act_spider_idle"], "spider_idle")

    def test_it_is_idempotent_keeps_crlf_and_still_parses(self):
        once = b.plan_action_sets(action_sets())
        self.assertEqual(b.plan_action_sets(once), once)
        self.assertNotIn("\n", once.replace("\r\n", ""))
        ET.fromstring(once.encode("utf-8"))

    def test_it_refuses_a_file_with_one_rider_set(self):
        with self.assertRaises(b.BindError):
            b.plan_action_sets(action_sets(riders=("as_human_warrior",)))

    def test_it_refuses_a_spider_set_that_lost_a_row(self):
        text = action_sets().replace('\t\t<action type="act_spider_turn_left" animation="an_spi_walk_2" />\r\n', "")
        with self.assertRaises(b.BindError) as err:
            b.plan_action_sets(text)
        self.assertIn("act_spider_turn_left", str(err.exception))


class UsageAndTypeTests(unittest.TestCase):
    def _moves(self, text):
        spider = ET.fromstring(text.encode("utf-8")).findall("monster_usage_set")[0]
        return [(r.get("is_left_foot"), int(r.get("pace")), r.get("direction"), r.get("turn_direction"), r.get("action"))
                for r in spider.find("monster_usage_movements")]

    def test_the_spider_gets_the_whole_pace_table_and_no_other_set_changes(self):
        out = b.plan_usage(USAGE)
        root = ET.fromstring(out.encode("utf-8"))
        spider, elephant = root.findall("monster_usage_set")
        self.assertEqual(self._moves(out), list(b.MOVEMENTS))
        self.assertEqual(len(spider.find("monster_usage_upper_body_movements")), 1)
        self.assertEqual([r.get("action") for r in elephant.find("monster_usage_movements")], ["act_elephant_backward_walk"])
        self.assertEqual(b.plan_usage(out), out)
        self.assertNotIn("\n", out.replace("\r\n", ""))

    def test_the_table_has_the_keys_of_the_wargs_and_one_literal_row_for_each_pace(self):
        moves = self._moves(b.plan_usage(USAGE))
        keys = [m[:4] for m in moves]
        self.assertEqual(len(keys), 29)                     # the warg, the elephant and the vanilla horse have 29
        self.assertEqual(len(set(keys)), 29)
        for row in (("False", 0, "front", "none", "act_spider_walk_backward"), ("False", 0, "none", "none", "act_spider_walk_stand"),
                    ("False", 1, "none", "none", "act_spider_stand"), ("False", 1, "none", "left", "act_spider_turn_left"),
                    ("False", 2, "front", "none", "act_spider_walk_forward"), ("False", 2, "none", "none", "act_spider_walk_stand"),
                    ("False", 3, "front", "none", "act_spider_trot_forward"), ("False", 3, "none", "none", "act_spider_trot_stand"),
                    ("False", 4, "front", "none", "act_spider_canter_forward"), ("False", 4, "none", "none", "act_spider_canter_stand"),
                    ("False", 5, "front", "none", "act_spider_run_forward"), ("True", 5, "front", "none", "act_spider_run_forward"),
                    ("True", 5, "none", "none", "act_spider_run_stand"), ("False", 3, "left", "none", "act_spider_walk_left")):
            self.assertIn(row, moves)
        for pace in range(6):
            self.assertTrue([m for m in moves if m[1] == pace and m[2] == "none" and m[3] == "none"], pace)

    def test_the_new_actions_are_declared_once(self):
        out = b.plan_action_types(TYPES)
        names = re.findall(r'<action\s+name="([^"]+)"', out)
        for action in ("act_spider_stand", "act_spider_walk_backward", "act_spider_walk_stand", "act_spider_trot_forward",
                       "act_spider_trot_stand", "act_spider_canter_forward", "act_spider_canter_stand", "act_spider_run_stand"):
            self.assertEqual(names.count(action), 1, action)
        self.assertEqual(len(b.NEW), 8)
        self.assertLess(names.index("act_spider_taunt"), names.index("act_spider_stand"))
        self.assertLess(names.index("act_spider_run_stand"), names.index("act_elephant_forward_trot"))
        self.assertEqual(b.plan_action_types(out), out)

    def test_a_declaration_inside_a_comment_is_not_one(self):
        text = TYPES.replace('\t<action name="act_spider_taunt" />\r\n',
                             '\t<action name="act_spider_taunt" />\r\n\t<!-- <action name="act_spider_stand" /> -->\r\n')
        out = b.plan_action_types(text)
        self.assertIn("act_spider_stand", {a.get("name") for a in ET.fromstring(out.encode("utf-8")).iter("action")})

    def test_every_bound_clip_is_one_the_generator_makes(self):
        import spider_gait
        self.assertLessEqual(set(b.bound_clips()), set(spider_gait.CLIPS))


class PlannedProblemTests(unittest.TestCase):
    """The plan is read back as the engine reads it; each thing this tool owns is broken alone."""

    def setUp(self):
        self.types, self.sets, self.usage = b.plan_action_types(TYPES), b.plan_action_sets(action_sets()), b.plan_usage(USAGE)

    def test_the_three_planned_files_read_back_clean(self):
        self.assertEqual(b.planned_problems(roots(self.types, self.sets, self.usage)), [])

    def test_an_undeclared_action_is_named(self):
        got = b.planned_problems(roots(TYPES, self.sets, self.usage))
        self.assertTrue([p for p in got if "act_spider_stand is not declared" in p], got)

    def test_a_spider_row_on_the_wrong_clip_is_named(self):
        sets = self.sets.replace('type="act_spider_trot_forward" animation="spider_trot"', 'type="act_spider_trot_forward" animation="spider_walk"')
        got = b.planned_problems(roots(self.types, sets, self.usage))
        self.assertEqual(got, ["as_spider plays act_spider_trot_forward on spider_walk, not spider_trot"])

    def test_a_rider_row_on_the_wrong_clip_or_missing_is_named(self):
        sets = self.sets.replace('type="act_spider_stand" animation="rider_warg_stand_for_movement"',
                                 'type="act_spider_stand" animation="rider_warg_trot"', 1)
        got = b.planned_problems(roots(self.types, sets, self.usage))
        self.assertEqual(len(got), 1, got)
        self.assertIn("as_human_warrior", got[0])
        self.assertIn("rider_warg_trot, not rider_warg_stand_for_movement", got[0])
        sets = self.sets.replace('\t\t<action type="act_spider_run_stand" animation="rider_warg_canter" />\r\n', "", 1)
        got = b.planned_problems(roots(self.types, sets, self.usage))
        self.assertTrue([p for p in got if "has no act_spider_run_stand row" in p], got)

    def test_a_row_that_is_only_in_a_comment_does_not_read_back(self):
        sets = self.sets.replace('\t\t<action type="act_spider_stand" animation="spider_stand" />\r\n',
                                 '\t\t<!-- <action type="act_spider_stand" animation="spider_stand" /> -->\r\n')
        got = b.planned_problems(roots(self.types, sets, self.usage))
        self.assertTrue([p for p in got if "as_spider has no act_spider_stand row" in p], got)

    def test_another_block_of_a_rider_set_that_overrides_an_owned_row_is_named(self):
        # the engine merges every block of one id: a later partial block wins, and it has no run row to be found by
        sets = self.sets.replace('</action_sets>', '\t<action_set id="as_human_warrior">\r\n'
                                 '\t\t<action type="act_spider_stand" animation="rider_warg_trot" />\r\n\t</action_set>\r\n</action_sets>')
        self.assertEqual(b.plan_action_sets(sets), sets, "the plan does not reach that block")
        got = b.planned_problems(roots(self.types, sets, self.usage))
        self.assertEqual(len(got), 1, got)
        self.assertIn("as_human_warrior", got[0])
        self.assertIn("plays act_spider_stand on rider_warg_trot, not rider_warg_stand_for_movement", got[0])

    def test_a_child_set_that_overrides_an_owned_row_is_named(self):
        goblin = ('\t<action_set id="as_goblin_warrior" skeleton="human_skeleton" base_set="as_human_warrior">\r\n'
                  '\t\t<action type="act_spider_trot_forward" animation="rider_warg_idle" />\r\n\t</action_set>\r\n')
        cave = ('\t<action_set id="as_spider_cave" skeleton="spider_skeleton" base_set="as_spider">\r\n'
                '\t\t<action type="act_spider_walk_forward" animation="an_spi_walk_2" />\r\n\t</action_set>\r\n')
        got = b.planned_problems(roots(self.types, self.sets.replace('</action_sets>', goblin + cave + '</action_sets>'), self.usage))
        self.assertEqual(len(got), 2, got)
        self.assertIn("as_goblin_warrior", got[0])
        self.assertIn("as_spider_cave (a descendant of as_spider) plays act_spider_walk_forward on an_spi_walk_2, not spider_walk", got[1])

    def test_a_set_two_steps_below_the_spiders_is_held_to_the_spiders_clips_not_the_riders(self):
        def deep(rows):
            return ('\t<action_set id="as_spider_cave" skeleton="spider_skeleton" base_set="as_spider_town_and_village">\r\n'
                    + rows + '\t</action_set>\r\n</action_sets>')

        own = self.sets.replace('</action_sets>', deep('\t\t<action type="act_spider_stand" animation="spider_stand" />\r\n'))
        self.assertEqual(b.planned_problems(roots(self.types, own, self.usage)), [])
        for row, said in (('<action type="act_spider_stand" animation="rider_warg_stand_for_movement" />', "not spider_stand"),
                          ('<action type="act_spider_walk_forward" animation="an_spi_walk_2" />', "not spider_walk")):
            got = b.planned_problems(roots(self.types, self.sets.replace('</action_sets>', deep('\t\t' + row + '\r\n')), self.usage))
            self.assertEqual(len(got), 1, got)
            self.assertIn("as_spider_cave (a descendant of as_spider)", got[0])
            self.assertIn(said, got[0])

    def test_a_row_of_another_creature_in_another_set_is_nobodys_business(self):
        other = ('\t<action_set id="as_goblin_warrior" skeleton="human_skeleton" base_set="as_human_warrior">\r\n'
                 '\t\t<action type="act_warg_rear" animation="rider_warg_idle" />\r\n'
                 '\t\t<action type="act_spider_idle" animation="rider_warg_idle_2" />\r\n\t</action_set>\r\n')
        self.assertEqual(b.planned_problems(roots(self.types, self.sets.replace('</action_sets>', other + '</action_sets>'), self.usage)), [])

    def test_a_movement_table_that_is_not_the_tools_is_named(self):
        usage = self.usage.replace('pace="3" direction="front" turn_direction="none" action="act_spider_trot_forward"',
                                   'pace="3" direction="front" turn_direction="none" action="act_spider_run_forward"')
        got = b.planned_problems(roots(self.types, self.sets, usage))
        self.assertTrue([p for p in got if "movement rows are not the table" in p], got)


class MainTests(unittest.TestCase):
    """`main` on a temporary install: three live files, their snapshot copies, and a cooked pair for every clip."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.module = root / "game" / "Modules" / "LOTRLOME_Armory"
        self.live, self.snap, self.backup = self.module / "ModuleData", root / "snapshot", root / "backup"
        clips = self.module / b.CLIPS
        for folder in (self.live, self.snap, clips, self.module / "RuntimeDataCache"):
            folder.mkdir(parents=True)
        self.before = {"action_types.xml": TYPES, "action_sets.xml": action_sets(), "monster_usage_sets.xml": USAGE}
        for folder in (self.live, self.snap):
            for name, text in self.before.items():
                (folder / name).write_bytes(b"\xef\xbb\xbf" + text.encode("utf-8"))
        self.entries = {}
        for clip in b.bound_clips():
            guid = uuid.uuid4()
            for kind in ("anm", "geo"):
                path = clips / ("%s_%s.tpac" % (clip, kind))
                path.write_bytes(b"TPAC" + b"\0" * 4 + guid.bytes_le + b"\0" * 8)
                os.utime(path, (1_000_000, 1_000_000))
            self.entries[clip] = self.module / "RuntimeDataCache" / (str(guid) + ".rdc")
            self.entries[clip].write_bytes(b"RDC0")
            os.utime(self.entries[clip], (2_000_000, 2_000_000))
        for patch in (mock.patch.object(b, "SNAPSHOT", self.snap), mock.patch.object(b, "game_or_kit_running", return_value=False)):
            patch.start()
            self.addCleanup(patch.stop)

    def _run(self, *args):
        out = io.StringIO()
        with redirect_stdout(out):
            code = b.main(["--game", str(self.module.parent.parent), "--backup-dir", str(self.backup), *args])
        return code, out.getvalue()

    def _text(self, folder, name):
        return (folder / name).read_bytes().decode("utf-8-sig")

    def test_check_fails_until_the_binding_is_applied_and_apply_writes_it_once(self):
        code, out = self._run("--check")
        self.assertEqual(code, 1, out)
        self.assertIn("NOT bound (6 file(s) differ", out)
        self.assertIn('+ <action name="act_spider_stand" />', out)        # it names what differs
        code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        for folder in (self.live, self.snap):
            self.assertEqual(self._text(folder, "action_sets.xml"), b.plan_action_sets(action_sets()))
            self.assertTrue((folder / "action_types.xml").read_bytes().startswith(b"\xef\xbb\xbf"), "the BOM is kept")
            self.assertEqual([p.name for p in folder.iterdir() if p.name.endswith(b.TMP)], [])
        self.assertEqual(sorted(p.name for p in self.backup.iterdir()), sorted(n + ".before-spider-bind" for n in b.FILES))
        self.assertEqual((self.backup / "action_sets.xml.before-spider-bind").read_bytes().decode("utf-8-sig"), action_sets())
        self.assertEqual(self._run("--check")[0], 0)
        code, out = self._run("--apply")
        self.assertEqual((code, "already applied" in out), (0, True), out)

    def test_nothing_is_written_while_a_clip_has_no_current_cache_entry(self):
        os.remove(self.entries["spider_trot"])
        os.utime(self.entries["spider_walk"], (500_000, 500_000))
        code, out = self._run("--apply")
        self.assertEqual(code, 1, out)
        self.assertIn("NOT READY spider_trot: it has no cache entry", out)
        self.assertIn("NOT READY spider_walk: its cache entry is older", out)
        self.assertEqual(self._text(self.live, "action_sets.xml"), action_sets())
        self.assertEqual(self._run("--check")[0], 1)

    def test_nothing_is_written_while_the_game_or_the_kit_runs(self):
        with mock.patch.object(b, "game_or_kit_running", return_value=True):
            code, out = self._run("--apply")
        self.assertEqual(code, 2, out)
        self.assertEqual(self._text(self.live, "action_sets.xml"), action_sets())
        self.assertFalse(self.backup.exists())

    def test_the_files_go_in_declarations_rows_usage_live_before_the_snapshot(self):
        moved = []
        real = os.replace

        def spy(src, dst):
            moved.append(Path(dst))
            real(src, dst)

        with mock.patch.object(b.os, "replace", side_effect=spy):
            self.assertEqual(self._run("--apply")[0], 0)
        self.assertEqual(moved, [folder / name for folder in (self.live, self.snap) for name in b.FILES])

    def test_a_run_that_stops_between_two_files_leaves_whole_files_and_a_second_run_finishes(self):
        real, calls = os.replace, []

        def dies_on_the_third(src, dst):
            calls.append(dst)
            if len(calls) == 3:
                raise OSError("the disk went away")
            real(src, dst)

        with mock.patch.object(b.os, "replace", side_effect=dies_on_the_third):
            code, out = self._run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("run --apply again", out)
        self.assertEqual(self._text(self.live, "action_types.xml"), b.plan_action_types(TYPES))
        self.assertEqual(self._text(self.live, "action_sets.xml"), b.plan_action_sets(action_sets()))
        self.assertEqual(self._text(self.live, "monster_usage_sets.xml"), USAGE)       # whole, and still the old one
        for name in b.FILES:      # the state a game start would meet parses, and the old usage rows name bound actions
            ET.fromstring((self.live / name).read_bytes())
        self.assertEqual(self._run("--check")[0], 1)
        self.assertEqual(self._run("--apply")[0], 0)
        self.assertEqual(self._run("--check")[0], 0)

    def test_a_backup_that_fails_part_way_leaves_no_file_behind_in_the_module(self):
        real, calls = b.shutil.copy2, []

        def full_on_the_second(src, dst):
            calls.append(src)
            if len(calls) == 2:
                raise OSError("the backup disk is full")
            return real(src, dst)

        with mock.patch.object(b.shutil, "copy2", side_effect=full_on_the_second):
            code, out = self._run("--apply")
        self.assertEqual(code, 2, out)
        for folder in (self.live, self.snap):
            self.assertEqual(sorted(p.name for p in folder.iterdir()), sorted(b.FILES), folder)
            for name, text in self.before.items():
                self.assertEqual(self._text(folder, name), text)

    def test_a_backup_folder_that_cannot_be_made_stops_the_run_before_any_write(self):
        (self.backup.parent / "backup").write_text("a file where the folder should go")
        code, out = self._run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("no file of the module or the snapshot was changed", out)
        self.assertEqual(self._text(self.live, "action_types.xml"), TYPES)

    def test_a_machine_without_the_armory_cannot_run(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = b.main(["--game", str(Path(self.tmp.name) / "nowhere"), "--check"])
        self.assertEqual(code, 2)
        self.assertIn("environment gap", out.getvalue())


if __name__ == "__main__":
    unittest.main()
