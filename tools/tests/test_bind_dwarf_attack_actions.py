"""bind_dwarf_attack_actions.py: the two dwarf attack actions in the Armory's action_types.xml and as_dwarf_warrior
(#1087, 2026-11-06). Runs on small CRLF + BOM files shaped like the Armory's, never on the install."""
import os
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import bind_dwarf_attack_actions as b  # noqa: E402

BOM = b"\xef\xbb\xbf"
TYPES = ('<?xml version="1.0" encoding="utf-8"?>\r\n<action_types>\r\n'
         '\t<action name="act_war_ram_butt" type="actt_kick" />\r\n'
         '\t<action name="act_troll_brute_force" />\r\n'
         '\t<action name="act_warg_stand_1"/>\r\n</action_types>\r\n')
SETS = ('<?xml version="1.0" encoding="utf-8"?>\r\n<action_sets>\r\n'
        '\t<action_set\r\n\t\tid="as_dwarf_warrior"\r\n\t\tskeleton="dwarf_skeleton_a"\r\n\t\tmovement_system="bipedal">\r\n'
        '\t\t<action\r\n\t\t\ttype="act_kick_right_leg"\r\n\t\t\tanimation="kick_right_leg" />\r\n'
        '\t</action_set>\r\n'
        '\t<action_set\r\n\t\tid="as_dwarf_warrior_in_tavern"\r\n\t\tskeleton="dwarf_skeleton_a">\r\n'
        '\t\t<action type="act_sit_1" animation="sit_1" />\r\n\t</action_set>\r\n</action_sets>\r\n')


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.armory = os.path.join(self.tmp.name, "armory")
        self.snap = os.path.join(self.tmp.name, "snap")
        for d in (self.armory, self.snap):
            os.makedirs(d)
            self.put(d, "action_types.xml", TYPES)
            self.put(d, "action_sets.xml", SETS)
        patcher = mock.patch.object(b, "game_or_kit_running", return_value=False)   # never ask the real machine
        self.guard = patcher.start()
        self.addCleanup(patcher.stop)

    def tearDown(self):
        self.tmp.cleanup()

    def put(self, d, name, text):
        with open(os.path.join(d, name), "wb") as f:
            f.write(BOM + text.encode("utf-8"))

    def raw(self, d, name):
        with open(os.path.join(d, name), "rb") as f:
            return f.read()

    def everything(self):
        out = {}
        for d in (self.armory, self.snap):
            for n in sorted(os.listdir(d)):
                out[(os.path.basename(d), n)] = self.raw(d, n)
        return out

    def baks(self):
        return [f for d in (self.armory, self.snap) for f in os.listdir(d) if ".bak-" in f]

    def run_main(self, *extra):
        return b.main(["--armory", self.armory, "--snapshot", self.snap] + list(extra))


class PlanTests(Fixture):
    def test_plan_types_fresh_file_adds_both_after_brute_force(self):
        new = b.plan_types(TYPES)
        lines = new.split("\r\n")
        i = next(n for n, ln in enumerate(lines) if "act_troll_brute_force" in ln)
        rows = [ln.strip() for ln in lines[i + 1:] if ln.strip().startswith('<action name="act_dwarf_')]
        self.assertEqual(rows, ['<action name="act_dwarf_head_butt" type="actt_kick" />',
                                '<action name="act_dwarf_drop_kick" type="actt_kick" />'])
        self.assertNotIn("\n", new.replace("\r\n", ""), "CRLF kept on every inserted line")

    def test_plan_types_already_bound_returns_same_text(self):
        once = b.plan_types(TYPES)
        self.assertEqual(b.plan_types(once), once)

    def test_plan_types_wrong_type_refuses(self):
        bad = b.plan_types(TYPES).replace('act_dwarf_drop_kick" type="actt_kick"', 'act_dwarf_drop_kick" type="actt_fall"')
        with self.assertRaises(b.Refuse):
            b.plan_types(bad)

    def test_plan_types_one_of_two_present_refuses(self):
        half = TYPES.replace('\t<action name="act_troll_brute_force" />',
                             '\t<action name="act_troll_brute_force" />\r\n\t<action name="act_dwarf_head_butt" type="actt_kick" />')
        with self.assertRaises(b.Refuse):
            b.plan_types(half)

    def test_plan_types_no_anchor_refuses(self):
        with self.assertRaises(b.Refuse):
            b.plan_types(TYPES.replace("act_troll_brute_force", "act_other"))

    def test_plan_sets_fresh_file_binds_only_in_the_warrior_set(self):
        new = b.plan_sets(SETS)
        warrior = new[:new.index("as_dwarf_warrior_in_tavern")]
        tavern = new[new.index("as_dwarf_warrior_in_tavern"):]
        self.assertIn('<action type="act_dwarf_head_butt" animation="dwarf_head_butt" />', warrior)
        self.assertIn('<action type="act_dwarf_drop_kick" animation="dwarf_drop_kick" />', warrior)
        self.assertNotIn("act_dwarf_", tavern)
        self.assertLess(warrior.index("act_kick_right_leg"), warrior.index("act_dwarf_head_butt"), "appended at the end")

    def test_plan_sets_already_bound_returns_same_text(self):
        once = b.plan_sets(SETS)
        self.assertEqual(b.plan_sets(once), once)

    def test_plan_sets_wrong_clip_refuses(self):
        bad = b.plan_sets(SETS).replace('animation="dwarf_drop_kick"', 'animation="kick_right_leg"')
        with self.assertRaises(b.Refuse):
            b.plan_sets(bad)

    def test_plan_sets_row_in_another_set_refuses(self):
        stray = SETS.replace('<action type="act_sit_1" animation="sit_1" />',
                             '<action type="act_dwarf_head_butt" animation="dwarf_head_butt" />')
        with self.assertRaises(b.Refuse):
            b.plan_sets(stray)

    def test_plan_sets_missing_set_refuses(self):
        with self.assertRaises(b.Refuse):
            b.plan_sets(SETS.replace('id="as_dwarf_warrior"', 'id="as_other"'))

    def test_plan_sets_only_one_of_two_rows_bound_refuses(self):
        once = b.plan_sets(SETS)
        half = once.replace('\t\t<action type="act_dwarf_drop_kick" animation="dwarf_drop_kick" />\r\n', "")
        self.assertNotEqual(half, once)
        with self.assertRaises(b.Refuse):
            b.plan_sets(half)

    def test_plan_sets_a_row_bound_twice_refuses(self):
        once = b.plan_sets(SETS)
        row = '\t\t<action type="act_dwarf_head_butt" animation="dwarf_head_butt" />\r\n'
        with self.assertRaises(b.Refuse):
            b.plan_sets(once.replace(row, row + row))

    def test_plan_types_a_type_declared_twice_refuses(self):
        once = b.plan_types(TYPES)
        row = '\t<action name="act_dwarf_head_butt" type="actt_kick" />\r\n'
        with self.assertRaises(b.Refuse):
            b.plan_types(once.replace(row, row + row))

    def test_nl_picks_the_majority_ending(self):
        self.assertEqual(b._nl("a\r\nb\r\nc\n"), "\r\n")
        self.assertEqual(b._nl("a\nb\nc\r\n"), "\n")
        self.assertEqual(b._nl("a\nb\n"), "\n")

    def test_lf_only_files_round_trip_without_a_carriage_return(self):
        for n, t in (("action_types.xml", TYPES), ("action_sets.xml", SETS)):
            lf = t.replace("\r\n", "\n")
            new = b.plan_checked(n, lf)
            self.assertNotEqual(new, lf)
            self.assertNotIn("\r", new)
            self.assertEqual(b.plan_checked(n, new), new)

    def test_one_stray_crlf_in_an_lf_file_does_not_switch_the_inserted_lines(self):
        lf = SETS.replace("\r\n", "\n").replace("<action_sets>\n", "<action_sets>\r\n", 1)
        self.assertNotIn('\r\n\t\t<action type="act_dwarf', b.plan_sets(lf))


class ParsedValidationTests(Fixture):
    """The planners splice text; every planned output is also parsed and counted (Codex F4)."""

    def applied(self):
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.run_main("--check"), 0)

    def corrupt_snapshot(self, name, fn):
        path = os.path.join(self.snap, name)
        text = self.raw(self.snap, name).decode("utf-8")
        new = fn(text)
        self.assertNotEqual(new, text)
        with open(path, "wb") as f:
            f.write(new.encode("utf-8"))

    def test_check_fails_when_both_type_rows_are_commented_out(self):
        self.applied()

        def comment(t):
            for a, _ in b.ACTIONS:
                t = t.replace('<action name="%s" type="actt_kick" />' % a, '<!-- <action name="%s" type="actt_kick" /> -->' % a)
            return t
        self.corrupt_snapshot("action_types.xml", comment)
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_fails_when_both_set_rows_are_commented_out(self):
        self.applied()

        def comment(t):
            for a, clip in b.ACTIONS:
                t = t.replace('<action type="%s" animation="%s" />' % (a, clip), '<!-- <action type="%s" animation="%s" /> -->' % (a, clip))
            return t
        self.corrupt_snapshot("action_sets.xml", comment)
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_fails_on_a_duplicate_declaration_with_reordered_attributes(self):
        self.applied()
        self.corrupt_snapshot("action_types.xml", lambda t: t.replace(
            "</action_types>", '\t<action type="actt_fall" name="act_dwarf_drop_kick" />\r\n</action_types>'))
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_fails_when_the_root_close_tag_is_missing(self):
        self.applied()
        self.corrupt_snapshot("action_types.xml", lambda t: t.replace("</action_types>", ""))
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_fails_on_a_root_level_action_element(self):
        self.applied()
        self.corrupt_snapshot("action_sets.xml", lambda t: t.replace(
            "</action_sets>", '\t<action type="act_other" animation="x" />\r\n</action_sets>'))
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_fails_when_a_row_has_reordered_attributes_and_the_wrong_clip(self):
        # the planner's pattern misses this row; the parsed check sees it
        self.applied()
        self.corrupt_snapshot("action_sets.xml", lambda t: t.replace(
            '<action type="act_dwarf_drop_kick" animation="dwarf_drop_kick" />',
            '<action animation="dwarf_head_butt" type="act_dwarf_drop_kick" />'))
        self.assertEqual(self.run_main("--check"), 1)

    def test_unparsable_snapshot_blocks_dry_run_and_apply_without_a_write(self):
        conflict = SETS.replace("</action_sets>", "<<<<<<< HEAD\r\n=======\r\n>>>>>>> other\r\n</action_sets>")
        self.put(self.snap, "action_sets.xml", conflict)
        before = self.everything()
        self.assertEqual(self.run_main(), 1)
        self.assertEqual(self.run_main("--apply"), 1)
        self.assertEqual(self.everything(), before)
        self.assertEqual(self.baks(), [])

    def test_one_refused_file_makes_apply_write_nothing(self):
        self.put(self.snap, "action_sets.xml", SETS.replace('id="as_dwarf_warrior"', 'id="as_other"'))
        before = self.everything()
        self.assertEqual(self.run_main("--apply"), 1)
        self.assertEqual(self.everything(), before)
        self.assertEqual(self.baks(), [])


class GuardTests(Fixture):
    def test_apply_refuses_while_the_game_or_kit_runs_and_writes_nothing(self):
        self.guard.return_value = True
        before = self.everything()
        self.assertEqual(self.run_main("--apply"), 2)
        self.assertEqual(self.everything(), before)
        self.assertEqual(self.baks(), [])

    def test_dry_run_and_check_do_not_ask_the_guard(self):
        self.guard.return_value = True
        self.assertEqual(self.run_main(), 0)
        self.assertEqual(self.run_main("--check"), 1)
        self.guard.assert_not_called()


class SharedGuardTests(unittest.TestCase):
    def test_uses_the_shared_fail_closed_process_guard(self):
        import _gamedir
        self.assertIs(b.game_or_kit_running, _gamedir.game_or_kit_running)


class MainTests(Fixture):
    def test_main_dry_run_writes_nothing(self):
        before = [self.raw(d, n) for d in (self.armory, self.snap) for n in b.FILES]
        self.assertEqual(self.run_main(), 0)
        self.assertEqual([self.raw(d, n) for d in (self.armory, self.snap) for n in b.FILES], before)

    def test_main_check_on_unbound_files_fails(self):
        self.assertEqual(self.run_main("--check"), 1)

    def test_main_apply_then_check_passes_and_keeps_bom(self):
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.run_main("--check"), 0)
        for d in (self.armory, self.snap):
            for n in b.FILES:
                self.assertTrue(self.raw(d, n).startswith(BOM))
                self.assertEqual(self.raw(d, n).count(b"\n"), self.raw(d, n).count(b"\r\n"))

    def test_main_apply_backs_up_without_an_xml_extension(self):
        self.run_main("--apply")
        baks = [f for f in os.listdir(self.armory) if ".bak-dwarf-attacks-" in f]
        self.assertEqual(len(baks), 2)
        self.assertFalse(any(f.endswith(".xml") for f in baks))

    def test_main_apply_twice_changes_nothing_the_second_time(self):
        self.run_main("--apply")
        after = [self.raw(d, n) for d in (self.armory, self.snap) for n in b.FILES]
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual([self.raw(d, n) for d in (self.armory, self.snap) for n in b.FILES], after)

    def test_main_apply_fails_when_the_rows_are_not_there_after_the_write(self):
        real = b.plan_checked

        def lying(name, text):
            new = real(name, text)
            return new + " " if name == "action_types.xml" and "act_dwarf_head_butt" in text else new
        with mock.patch.object(b, "plan_checked", side_effect=lying):
            self.assertEqual(self.run_main("--apply"), 1)

    def test_main_check_snapshot_drift_fails(self):
        self.run_main("--apply")
        self.put(self.snap, "action_sets.xml", SETS)
        self.assertEqual(self.run_main("--check"), 1)

    def test_main_missing_file_fails(self):
        os.remove(os.path.join(self.snap, "action_types.xml"))
        self.assertEqual(self.run_main("--check"), 1)


@unittest.skipUnless(os.path.isdir(b.ARMORY), "the Armory is not installed here")
class LiveInstallTests(unittest.TestCase):
    def test_live_armory_and_snapshot_carry_both_rows(self):
        self.assertEqual(b.main(["--check"]), 0)


if __name__ == "__main__":
    unittest.main()
