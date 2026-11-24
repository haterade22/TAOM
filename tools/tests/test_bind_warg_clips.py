"""bind_warg_clips.py: the warg's and the dire wolf's walk on their own in-place twins (#1565), in the Armory's
as_warg and as_dire_wolf and in the snapshot. Runs on small CRLF + BOM files shaped like the Armory's and on
packages the tests build, never on the install."""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
import uuid
from unittest import mock

try:
    import xxhash  # noqa: F401
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("the packages these tests build need xxhash (%s)" % exc)

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bind_warg_clips as b  # noqa: E402
import repoint_anim_clip as rp  # noqa: E402
import set_clip_balance_name as scb  # noqa: E402
import test_creature_fit as tcf  # noqa: E402
import test_repoint_anim_clip as trp  # noqa: E402
import test_set_clip_balance_name as tsb  # noqa: E402

BOM = b"\xef\xbb\xbf"
WARG_OLD = '\t\t<action type="act_warg_forward_walk_stand" animation="warg_walkfast" />\r\n'
WARG_NEW = '\t\t<action type="act_warg_forward_walk_stand" animation="warg_walk_stand" />\r\n'
WOLF_OLD = '\t\t<action type="act_warg_forward_walk_stand" animation="dire_wolf_walkfast" />\r\n'
WOLF_NEW = '\t\t<action type="act_warg_forward_walk_stand" animation="dire_wolf_walk_stand" />\r\n'
SETS = ('<?xml version="1.0" encoding="utf-8"?>\r\n<action_sets>\r\n'
        '\t<action_set id="as_human_warrior" skeleton="human_skeleton">\r\n'
        '\t\t<action type="act_warg_forward_walk_stand" animation="rider_warg_forward_walk" />\r\n'
        '\t</action_set>\r\n'
        '\t<action_set id="as_warg" skeleton="skeleton_warg" movement_system="quadrupedal">\r\n'
        '\t\t<action type="act_warg_forward_walk" animation="warg_walkfast" />\r\n'
        + WARG_OLD +
        '\t\t<action type="act_warg_forward_trot_stand" animation="warg_trot_stand" />\r\n'
        '\t</action_set>\r\n'
        '\t<action_set id="as_dire_wolf" skeleton="adod_wolf_armature_unused" movement_system="quadrupedal">\r\n'
        '\t\t<action type="act_warg_forward_walk" animation="dire_wolf_walkfast" />\r\n'
        + WOLF_OLD +
        '\t</action_set>\r\n</action_sets>\r\n')
DONE = SETS.replace(WARG_OLD, WARG_NEW).replace(WOLF_OLD, WOLF_NEW)
# creature: (clips folder under Assets, plan file, twin, template, master file stem)
CREATURES = {"warg": ("creature/warg/animations", "warg_gait_plan.json", "warg_walk_stand", "warg_trot_stand", "Warg_Walk"),
             "wolf": ("creature/direwolf/animations", "dire_wolf_gait_plan.json", "dire_wolf_walk_stand",
                      "dire_wolf_trot_stand", "adod_wolf_walk")}


def plan_of(creature, duration=2.06):
    _, _, twin, template, master = CREATURES[creature]
    return [{"clip": twin, "template": template, "master": master, "source1": 1, "source2": 29, "duration": duration}]


class Fixture(unittest.TestCase):
    """A module with both sets, each walk's master and a template record; `made` writes a twin, `cooked` its entry."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.module = os.path.join(self.tmp.name, "LOTRLOME_Armory")
        self.snap = os.path.join(self.tmp.name, "snap")
        self.plans = os.path.join(self.tmp.name, "plans")
        for d in (self.snap, self.plans, os.path.join(self.module, "ModuleData"), os.path.join(self.module, "RuntimeDataCache")):
            os.makedirs(d)
        self.live = os.path.join(self.module, "ModuleData", "action_sets.xml")
        self.snapshot = os.path.join(self.snap, "action_sets.xml")
        for path in (self.live, self.snapshot):
            self.put(path, SETS)
        for creature, (under, plan, _, template, master) in CREATURES.items():
            clips = self.clips(creature)
            os.makedirs(clips)
            with open(os.path.join(clips, template + "_anm.tpac"), "wb") as fh:
                fh.write(trp.gait_clip(name=template, usage=None, duration=0.73))
            with open(os.path.join(clips, master + "_geo.tpac"), "wb") as fh:
                fh.write(tcf.master_package(master.lower(), uuid.uuid4()))
            self.write_plan(creature)
        patcher = mock.patch.object(b, "game_or_kit_running", return_value=False)   # never ask the real machine
        self.guard = patcher.start()
        self.addCleanup(patcher.stop)

    def clips(self, creature):
        return os.path.join(self.module, "Assets", *CREATURES[creature][0].split("/"))

    def write_plan(self, creature, duration=2.06):
        with open(os.path.join(self.plans, CREATURES[creature][1]), "w", encoding="utf-8") as fh:
            json.dump(plan_of(creature, duration), fh)

    def put(self, path, text):
        with open(path, "wb") as fh:
            fh.write(BOM + text.encode("utf-8"))

    def raw(self, path):
        with open(path, "rb") as fh:
            return fh.read()

    def made(self, *creatures):
        for creature in creatures or CREATURES:
            with contextlib.redirect_stdout(io.StringIO()):
                rp.apply(rp.plan(plan_of(creature), self.clips(creature)), stamp="t")

    def cooked(self, *creatures):
        for creature in creatures or CREATURES:
            buf = self.raw(os.path.join(self.clips(creature), CREATURES[creature][2] + "_anm.tpac"))
            c = scb.parse(buf)
            with open(scb.rdc_path(self.module, c.package_guid), "wb") as fh:
                fh.write(tsb.rdc(tsb.stored_checksum(buf), item=c.item_guid))

    def run_main(self, *extra):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = b.main(["--armory", self.module, "--snapshot", self.snap, "--plans", self.plans] + list(extra))
        return code, out.getvalue() + err.getvalue()

    def baks(self):
        return [f for d in (os.path.dirname(self.live), self.snap) for f in os.listdir(d) if ".bak-" in f]

    def both(self):
        return self.raw(self.live), self.raw(self.snapshot)


class RebindTests(unittest.TestCase):
    def test_only_the_row_of_each_creatures_own_set_changes(self):
        new = b.rebind(SETS)
        self.assertEqual(new, DONE)
        self.assertIn('animation="rider_warg_forward_walk"', new)
        self.assertIn('<action type="act_warg_forward_walk" animation="warg_walkfast" />', new, "the front row stays")
        self.assertIn('<action type="act_warg_forward_walk" animation="dire_wolf_walkfast" />', new)

    def test_a_text_that_carries_the_bindings_is_returned_as_it_is(self):
        self.assertIs(b.rebind(DONE), DONE)

    def test_a_text_that_carries_one_binding_gets_the_other(self):
        self.assertEqual(b.rebind(SETS.replace(WARG_OLD, WARG_NEW)), DONE)
        self.assertEqual([x.twin for x in b.unbound(SETS.replace(WARG_OLD, WARG_NEW))], ["dire_wolf_walk_stand"])

    def test_a_row_on_another_clip_is_refused_never_repaired(self):
        with self.assertRaisesRegex(b.Refuse, "warg_idle"):
            b.rebind(SETS.replace(WARG_OLD, WARG_OLD.replace("warg_walkfast", "warg_idle")))
        with self.assertRaisesRegex(b.Refuse, "as_dire_wolf"):
            b.rebind(SETS.replace(WOLF_OLD, WOLF_OLD.replace("dire_wolf_walkfast", "warg_walk_stand")))

    def test_a_missing_or_doubled_row_is_refused(self):
        for text in (SETS.replace(WARG_OLD, ""), SETS.replace(WARG_OLD, WARG_OLD + WARG_OLD), SETS.replace(WOLF_OLD, "")):
            with self.assertRaises(b.Refuse):
                b.rebind(text)

    def test_a_file_without_one_of_the_sets_is_refused(self):
        with self.assertRaisesRegex(b.Refuse, "as_warg"):
            b.rebind(SETS.replace('id="as_warg"', 'id="as_wolf"'))
        with self.assertRaisesRegex(b.Refuse, "as_dire_wolf"):
            b.rebind(SETS.replace('id="as_dire_wolf"', 'id="as_wolf"'))

    def test_a_row_written_over_several_lines_is_read(self):
        spread = SETS.replace(WARG_OLD, '\t\t<action\r\n\t\t\ttype="act_warg_forward_walk_stand"\r\n'
                                        '\t\t\tanimation="warg_walkfast" />\r\n')
        self.assertIn('\t\t\tanimation="warg_walk_stand" />\r\n', b.rebind(spread))


class MainTests(Fixture):
    def test_nothing_is_bound_while_a_twin_is_not_there(self):
        self.made("warg")
        self.cooked("warg")
        before = self.both()
        code, text = self.run_main("--apply")
        self.assertEqual(code, 1)
        self.assertIn("dire_wolf_walk_stand is not there", text)
        self.assertNotIn("warg_walk_stand is not there", text.replace("dire_wolf_walk_stand", ""))
        self.assertEqual(self.both(), before)
        self.assertEqual(self.baks(), [])

    def test_nothing_is_bound_while_a_twin_is_not_cooked(self):
        """A clip bound with no cache entry crashed Agent.SetActionSet in another modder's test."""
        self.made()
        self.cooked("wolf")
        before = self.both()
        code, text = self.run_main("--apply")
        self.assertEqual(code, 1)
        self.assertRegex(text, "warg_walk_stand: it has no cache entry")
        self.assertEqual(self.both(), before)

    def test_nothing_is_bound_while_a_twin_does_not_hold_its_plans_values(self):
        """A cooked twin that the plan has since changed would be bound with the cadence of an older plan."""
        self.made()
        self.cooked()
        self.write_plan("wolf", duration=1.5)
        before = self.both()
        code, text = self.run_main("--apply")
        self.assertEqual(code, 1)
        self.assertIn("dire_wolf_walk_stand does not hold the plan", text)
        self.assertEqual(self.both(), before)

    def test_cooked_twins_are_bound_in_both_files_byte_faithfully(self):
        self.made()
        self.cooked()
        before = self.raw(self.live)
        code, text = self.run_main("--apply")
        self.assertEqual(code, 0, text)
        want = BOM + DONE.encode("utf-8")
        self.assertEqual(self.both(), (want, want))
        baks = self.baks()
        self.assertEqual(len(baks), 2)
        self.assertTrue(all(not f.endswith(".xml") for f in baks), "a backup must not be globbed as XML")
        live_bak = [f for f in os.listdir(os.path.dirname(self.live)) if ".bak-" in f][0]
        self.assertEqual(self.raw(os.path.join(os.path.dirname(self.live), live_bak)), before)

    def test_a_twin_that_is_bound_already_need_not_be_cooked_again_to_bind_the_other(self):
        """After a first run bound the warg, only the wolf's twin is asked for."""
        self.put(self.live, SETS.replace(WARG_OLD, WARG_NEW))
        self.put(self.snapshot, SETS.replace(WARG_OLD, WARG_NEW))
        self.made("wolf")
        self.cooked("wolf")
        code, text = self.run_main("--apply")
        self.assertEqual(code, 0, text)
        self.assertEqual(self.raw(self.live), BOM + DONE.encode("utf-8"))

    def test_a_second_apply_changes_nothing(self):
        self.made()
        self.cooked()
        self.run_main("--apply")
        first, baks = self.raw(self.live), self.baks()
        code, _ = self.run_main("--apply")
        self.assertEqual(code, 0)
        self.assertEqual(self.raw(self.live), first)
        self.assertEqual(self.baks(), baks)

    def test_the_dry_run_writes_nothing(self):
        self.made()
        self.cooked()
        before = self.both()
        code, text = self.run_main()
        self.assertEqual(code, 0, text)
        self.assertEqual(self.both(), before)

    def test_check_fails_until_both_files_carry_both_bindings(self):
        self.made()
        self.cooked()
        self.assertEqual(self.run_main("--check")[0], 1)
        self.run_main("--apply")
        self.assertEqual(self.run_main("--check")[0], 0)
        self.put(self.snapshot, SETS.replace(WARG_OLD, WARG_NEW))       # the repo's copy lacks the wolf's row
        code, text = self.run_main("--check")
        self.assertEqual(code, 1)
        self.assertIn(self.snapshot, text)
        self.assertIn("dire_wolf_walk_stand", text)

    def test_check_does_not_need_a_twin_cooked_and_writes_nothing(self):
        """The cook is the gait gates' finding (repoint_anim_clip.py --check). This check reads two XML files."""
        self.put(self.live, DONE)
        self.put(self.snapshot, DONE)
        before = self.both()
        code, text = self.run_main("--check")
        self.assertEqual(code, 0, text)
        self.assertEqual(self.both(), before)

    def test_a_machine_without_the_armory_is_not_run_when_the_snapshot_is_right(self):
        self.put(self.snapshot, DONE)
        os.remove(self.live)
        code, text = self.run_main("--check")
        self.assertEqual(code, 2)
        self.assertIn("NOT RUN", text)

    def test_a_missing_snapshot_fails_every_mode(self):
        """The snapshot is a tracked file of the repo. A deleted one, or a mistyped --snapshot, is a fault and must
        not read as a pass of the one file that was found."""
        self.put(self.live, DONE)
        os.remove(self.snapshot)
        for mode in (["--check"], [], ["--apply"]):
            code, text = self.run_main(*mode)
            self.assertEqual(code, 1, mode)
            self.assertIn(self.snapshot, text)

    def test_a_file_another_session_changed_after_the_read_is_not_overwritten(self):
        """Other sessions edit the live action_sets.xml. The write is built from the bytes read at the start, so a
        file that changed since then is refused: its new content would be lost, from the backup too."""
        self.made()
        self.cooked()
        theirs = BOM + SETS.replace("</action_sets>", "<!-- another session's edit -->\r\n</action_sets>").encode("utf-8")

        def other_session_writes():
            with open(self.live, "wb") as fh:
                fh.write(theirs)
            return False
        self.guard.side_effect = other_session_writes
        code, text = self.run_main("--apply")
        self.assertEqual(code, 1)
        self.assertIn("changed since", text)
        self.assertEqual(self.raw(self.live), theirs)
        self.assertEqual(self.raw(self.snapshot), BOM + SETS.encode("utf-8"), "nothing at all is written")
        self.assertEqual(self.baks(), [])

    def test_a_file_that_is_not_utf8_or_has_no_closing_tag_is_refused_with_no_traceback(self):
        for broken in (b"\xff\xfe\x00bad", BOM + SETS.replace("</action_set>", "").encode("utf-8")):
            with open(self.snapshot, "wb") as fh:
                fh.write(broken)
            code, text = self.run_main("--check")
            self.assertEqual(code, 1)
            self.assertIn("REFUSED", text)

    def test_a_wrong_snapshot_fails_even_without_the_armory(self):
        os.remove(self.live)
        self.assertEqual(self.run_main("--check")[0], 1)

    def test_apply_refuses_while_the_game_or_the_kit_runs(self):
        self.made()
        self.cooked()
        self.guard.return_value = True
        before = self.raw(self.live)
        code, text = self.run_main("--apply")
        self.assertEqual(code, 2)
        self.assertEqual(self.raw(self.live), before)

    def test_a_refused_file_stops_the_write_of_the_other(self):
        self.made()
        self.cooked()
        self.put(self.snapshot, SETS.replace(WARG_OLD, WARG_OLD.replace("warg_walkfast", "warg_idle")))
        before = self.raw(self.live)
        code, text = self.run_main("--apply")
        self.assertEqual(code, 1)
        self.assertIn("REFUSED", text)
        self.assertEqual(self.raw(self.live), before)

    def test_each_real_plan_names_the_twin_this_tool_binds(self):
        for binding in b.BINDINGS:
            with open(os.path.join(b.PLANS, binding.plan), encoding="utf-8-sig") as fh:
                names = [r["clip"] for r in json.load(fh)]
            self.assertIn(binding.twin, names)
            self.assertIn(binding.front, names)


if __name__ == "__main__":
    unittest.main()
