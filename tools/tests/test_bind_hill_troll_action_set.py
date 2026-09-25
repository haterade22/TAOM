"""bind_hill_troll_action_set.py: the standalone as_hill_troll_warrior body from Native's as_human_warrior, every
code kept, Fab clips first where the cave troll rules bind one, the retargeted human clip where the index has it,
the troll's own idles reused for the inventory, conversation and cheer codes, the human clip inherited otherwise;
byte-faithful replacement of that one set's body and of the pose set's bound clips."""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import _gamedir  # noqa: E402
import bind_hill_troll_action_set as bh  # noqa: E402

NATIVE = """<?xml version="1.0" encoding="utf-8"?>
<action_sets>
\t<action_set
\t\tid="as_human_warrior"
\t\tskeleton="human_skeleton"
\t\tmovement_system="bipedal">
\t\t<action type="act_walk_forward_unarmed" animation="walk_forward_unarmed" />
\t\t<action type="act_idle_2h_1" animation="troop_stand_2h_1" alternative_group="idle_2h" />
\t\t<action type="act_idle_2h_1" animation="troop_stand_2h_1_b" alternative_group="idle_2h_b" />
\t\t<action type="act_ready_slashright_2h" animation="ready_slashright_2h" />
\t\t<!-- <action type="act_disabled_thing" animation="disabled_thing" /> -->
\t\t<action type="act_blocked_slashright_2h" animation="blocked_slashright_2h" />
\t\t<action type="act_swim_idle" animation="swim_idle" />
\t\t<action type="act_strike_chest_front" animation="strike_chest_front" />
\t\t<action type="act_inventory_idle" animation="inventory_idle" blend_in_period="0.3" />
\t\t<action type="act_cheer_2" animation="cheer_2" />
\t</action_set>
</action_sets>
"""

LIVE = ("﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<action_sets>\r\n"
        "\t<action_set id=\"as_cave_troll_warrior\" skeleton=\"human_skeleton\" movement_system=\"bipedal\">\r\n"
        "\t\t<action type=\"act_walk_forward_unarmed\" animation=\"anim_troll_walk1\" />\r\n"
        "\t</action_set>\r\n"
        "\t<action_set id=\"as_hill_troll_warrior\" skeleton=\"troll_skeleton_a\" movement_system=\"bipedal\">\r\n"
        "\t\t<action\r\n\t\t\ttype=\"act_walk_forward_unarmed\"\r\n\t\t\tanimation=\"walk_forward_unarmed\" />\r\n"
        "\t</action_set>\r\n"
        "\t<action_set id=\"as_hill_troll_poses\" base_set=\"as_hill_troll_warrior\">\r\n"
        "\t\t<action type=\"act_stand_1\" animation=\"stand_1\" />\r\n"
        "\t\t<action type=\"act_stand_2\" animation=\"stand_2\" />\r\n"
        "\t</action_set>\r\n"
        "</action_sets>\r\n")

HUMAN = {"anim_hill_troll_ready_slashright_2h", "anim_hill_troll_blocked_slashright_2h",
         "anim_hill_troll_strike_chest_front", "anim_hill_troll_troop_stand_2h_1"}
FAB = {"anim_hill_troll_walk1", "anim_hill_troll_combat_idle1", "anim_hill_troll_combat_idle2",
       "anim_hill_troll_combat_hit_front1", "anim_hill_troll_idle1"}


def actions():
    return bh.human_actions(NATIVE)


def code_of(line):
    return line.split('type="')[1].split('"')[0]


def write(path, text, encoding="utf-8"):
    with open(path, "w", encoding=encoding, newline="") as fh:
        fh.write(text)


class BindHillTests(unittest.TestCase):
    def test_fab_clip_wins_where_the_cave_troll_rules_bind_one(self):
        a = {"type": "act_walk_forward_unarmed", "animation": "walk_forward_unarmed"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB), ("anim_hill_troll_walk1", "fab"))
        a = {"type": "act_strike_chest_front", "animation": "strike_chest_front"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB), ("anim_hill_troll_combat_hit_front1", "fab"))

    def test_retargeted_human_clip_when_the_index_has_it(self):
        a = {"type": "act_ready_slashright_2h", "animation": "ready_slashright_2h"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB), ("anim_hill_troll_ready_slashright_2h", "human"))

    def test_fab_armed_idle_beats_the_retargeted_human_idle(self):
        a = {"type": "act_idle_2h_1", "animation": "troop_stand_2h_1", "alternative_group": "idle_2h"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB), ("anim_hill_troll_combat_idle1", "fab"))

    def test_inherits_the_human_clip_when_nothing_else_exists(self):
        a = {"type": "act_swim_idle", "animation": "swim_idle"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB), ("swim_idle", "inherited"))

    def test_fab_rule_without_the_fab_clip_falls_through(self):
        a = {"type": "act_walk_forward_unarmed", "animation": "walk_forward_unarmed"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, set()), ("walk_forward_unarmed", "inherited"))


class ReuseTests(unittest.TestCase):
    def test_inventory_and_conversation_codes_reuse_the_relaxed_idle(self):
        for code in ("act_inventory_idle", "act_inventory_idle_start", "act_conversation_normal_1",
                     "act_conversation_hip_2_left_stance"):
            a = {"type": code, "animation": "whatever"}
            self.assertEqual(bh.bind_hill(code, a, HUMAN, FAB), ("anim_hill_troll_idle1", "reuse"), code)

    def test_cheers_alternate_the_two_combat_idles_by_number(self):
        expected = {"act_cheer_1": "anim_hill_troll_combat_idle1", "act_cheer_2": "anim_hill_troll_combat_idle2",
                    "act_cheering_high_03": "anim_hill_troll_combat_idle1",
                    "act_cheering_low_10": "anim_hill_troll_combat_idle2", "act_cheer": "anim_hill_troll_combat_idle1"}
        for code, clip in expected.items():
            self.assertEqual(bh.bind_hill(code, {"type": code, "animation": "c"}, HUMAN, FAB), (clip, "reuse"), code)

    def test_a_missing_reuse_clip_inherits_the_human_clip(self):
        a = {"type": "act_cheer_2", "animation": "cheer_2"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN, FAB - {"anim_hill_troll_combat_idle2"}),
                         ("cheer_2", "inherited"))

    def test_a_retargeted_human_clip_beats_the_reuse_rule(self):
        a = {"type": "act_cheer_2", "animation": "cheer_2"}
        self.assertEqual(bh.bind_hill(a["type"], a, HUMAN | {"anim_hill_troll_cheer_2"}, FAB),
                         ("anim_hill_troll_cheer_2", "human"))

    def test_other_idle_codes_are_not_reused(self):
        for code in ("act_stand_1", "act_swim_idle", "act_sit_idle"):
            self.assertIsNone(bh.reuse_clip(code, FAB), code)


class BodyTests(unittest.TestCase):
    def test_body_keeps_every_active_node_and_its_other_attributes(self):
        lines, counts = bh.build_body(actions(), HUMAN, FAB)
        self.assertEqual([code_of(ln) for ln in lines],
                         ["act_walk_forward_unarmed", "act_idle_2h_1", "act_idle_2h_1", "act_ready_slashright_2h",
                          "act_blocked_slashright_2h", "act_swim_idle", "act_strike_chest_front",
                          "act_inventory_idle", "act_cheer_2"])
        idles = [ln for ln in lines if "act_idle_2h_1" in ln]
        self.assertIn('alternative_group="idle_2h"', idles[0])
        self.assertIn('alternative_group="idle_2h_b"', idles[1])
        self.assertTrue(all('animation="anim_hill_troll_combat_idle1"' in ln for ln in idles))
        inventory = [ln for ln in lines if "act_inventory_idle" in ln][0]
        self.assertIn('animation="anim_hill_troll_idle1"', inventory)
        self.assertIn('blend_in_period="0.3"', inventory)
        self.assertNotIn("act_disabled_thing", " ".join(lines))   # commented out in Native: not an active code
        # no brute-force clip in FAB: the extra is left out
        self.assertEqual(counts, {"fab": 4, "human": 2, "reuse": 2, "inherited": 1})

    def test_the_brute_force_action_is_appended_when_its_clip_exists(self):
        lines, counts = bh.build_body(actions(), HUMAN, FAB | {"anim_hill_troll_attack1"})
        self.assertEqual(lines[-1].strip(), '<action type="act_troll_brute_force" animation="anim_hill_troll_attack1" />')
        self.assertEqual(counts["extra"], 1)
        self.assertEqual([ln for ln in lines if "act_troll_brute_force" in ln], [lines[-1]])

    def test_replace_touches_only_the_hill_troll_body_and_keeps_bom_and_crlf(self):
        lines, _ = bh.build_body(actions(), HUMAN, FAB)
        new_text, old_count = bh.replace_body(LIVE, lines, "\r\n")
        self.assertEqual(old_count, 1)
        self.assertTrue(new_text.startswith("﻿"))
        self.assertNotIn("\n", new_text.replace("\r\n", ""))
        start = LIVE.index('id="as_cave_troll_warrior"')
        self.assertEqual(new_text[start:new_text.index('id="as_hill_troll_warrior"')],
                         LIVE[start:LIVE.index('id="as_hill_troll_warrior"')])
        self.assertEqual(new_text[new_text.index('id="as_hill_troll_poses"'):],
                         LIVE[LIVE.index('id="as_hill_troll_poses"'):])
        root = ET.fromstring(new_text.lstrip("﻿").encode("utf-8"))
        hill = [s for s in root if s.get("id") == "as_hill_troll_warrior"][0]
        self.assertEqual([a.get("type") for a in hill], [code_of(ln) for ln in lines])
        self.assertEqual(new_text.count("GENERATED by"), 1)

    def test_replace_is_idempotent(self):
        lines, _ = bh.build_body(actions(), HUMAN, FAB)
        once, _ = bh.replace_body(LIVE, lines, "\r\n")
        twice, _ = bh.replace_body(once, lines, "\r\n")
        self.assertEqual(once, twice)


class PosesTests(unittest.TestCase):
    def test_rebinds_only_the_pose_override_to_the_troll_idle(self):
        new_text, rebound = bh.rebind_poses(LIVE, FAB)
        self.assertEqual(rebound, 1)
        self.assertEqual(new_text, LIVE.replace('type="act_stand_1" animation="stand_1"',
                                                'type="act_stand_1" animation="anim_hill_troll_idle1"'))

    def test_rebinding_twice_changes_nothing_more(self):
        once, _ = bh.rebind_poses(LIVE, FAB)
        twice, rebound = bh.rebind_poses(once, FAB)
        self.assertEqual((twice, rebound), (once, 0))

    def test_leaves_the_pose_alone_when_the_idle_clip_is_missing(self):
        self.assertEqual(bh.rebind_poses(LIVE, FAB - {"anim_hill_troll_idle1"}), (LIVE, 0))

    def test_a_missing_pose_set_is_refused(self):
        without = LIVE.replace('id="as_hill_troll_poses"', 'id="as_other_poses"')
        with self.assertRaises(SystemExit):
            bh.rebind_poses(without, FAB)


class ClipDiscoveryTests(unittest.TestCase):
    def test_available_clips_read_the_index_keys_and_the_fab_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            idx = os.path.join(tmp, "clips_index.json")
            write(idx, json.dumps({"ready_slashright_2h": {"master": "x"}, "stand_2h": {"master": "y"}}), "utf-8-sig")
            names = os.path.join(tmp, "names.json")
            write(names, json.dumps({"_comment": "x", "cave_troll_free_walk_0": "anim_hill_troll_walk1"}))
            human, fab = bh.available_clips([idx], names)
        self.assertEqual(human, {"anim_hill_troll_ready_slashright_2h", "anim_hill_troll_stand_2h"})
        self.assertEqual(fab, {"anim_hill_troll_walk1"})

    def test_clips_dir_keeps_only_clips_that_exist_on_disk(self):
        with tempfile.TemporaryDirectory() as tmp:
            idx = os.path.join(tmp, "clips_index.json")
            write(idx, json.dumps({"ready_slashright_2h": {}, "aserai_mp_guard_idle_2hperk": {}}))
            names = os.path.join(tmp, "names.json")
            write(names, json.dumps({"a": "anim_hill_troll_walk1", "b": "anim_hill_troll_idle1"}))
            clips = os.path.join(tmp, "clips")
            os.mkdir(clips)
            for n in ("anim_hill_troll_ready_slashright_2h", "anim_hill_troll_walk1", "anim_hill_troll_unrelated"):
                write(os.path.join(clips, n + "_anm.tpac"), "x")
            write(os.path.join(clips, "anim_hill_troll_idle1_geo.tpac"), "x")     # a master, not a clip
            human, fab = bh.available_clips([idx], names, clips)
        self.assertEqual(human, {"anim_hill_troll_ready_slashright_2h"})   # the refused clip is not on disk
        self.assertEqual(fab, {"anim_hill_troll_walk1"})                    # idle1 has a master but no clip yet

    def test_renames_shorten_a_clip_name_past_the_engine_limit(self):
        long = "ready_from_right_slashright_2h_unbalanced_left_stance"
        renames = {long: "anim_hill_troll_ready_from_right_slashright_2h_unbalanced_ls"}
        self.assertTrue(len("anim_hill_troll_" + long) > 63 and len(renames[long]) <= 63)
        a = {"type": "act_ready_from_right_slashright_2h_unbalanced_left_stance", "animation": long}
        human = {renames[long]}
        self.assertEqual(bh.bind_hill(a["type"], a, human, set(), renames), (renames[long], "human"))
        # without the map the long name is unknown
        self.assertEqual(bh.bind_hill(a["type"], a, human, set()), (long, "inherited"))
        with tempfile.TemporaryDirectory() as tmp:
            idx = os.path.join(tmp, "clips_index.json")
            write(idx, json.dumps({long: {}, "stand_2h": {}}))
            names = os.path.join(tmp, "names.json")
            write(names, json.dumps({"a": "anim_hill_troll_walk1"}))
            h, _ = bh.available_clips([idx], names, None, renames)
            self.assertEqual(h, {renames[long], "anim_hill_troll_stand_2h"})
            rn = os.path.join(tmp, "renames.json")
            write(rn, json.dumps({"_comment": "x", **renames}))
            self.assertEqual(bh.load_renames(rn), renames)
        self.assertEqual(bh.load_renames(""), {})


class MainTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        tmp = self._tmp.name
        self.live = os.path.join(tmp, "action_sets.xml")
        with open(self.live, "wb") as fh:
            fh.write(LIVE.encode("utf-8"))
        self.native = os.path.join(tmp, "native.xml")
        write(self.native, NATIVE)
        self.idx = os.path.join(tmp, "clips_index.json")
        write(self.idx, json.dumps({k[len("anim_hill_troll_"):]: {} for k in HUMAN}))
        self.names = os.path.join(tmp, "names.json")
        write(self.names, json.dumps({str(i): n for i, n in enumerate(sorted(FAB))}))
        self.clips = os.path.join(tmp, "clips")
        os.mkdir(self.clips)
        for n in HUMAN | FAB:
            write(os.path.join(self.clips, n + "_anm.tpac"), "x")

    def tearDown(self):
        self._tmp.cleanup()

    def run_main(self, *extra):
        argv = ["--live", self.live, "--native", self.native, "--clips-index", self.idx,
                "--fab-names", self.names, "--clips-dir", self.clips, "--renames", ""] + list(extra)
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            rc = bh.main(argv)
        return rc, out.getvalue(), err.getvalue()

    def read_live(self):
        with open(self.live, "rb") as fh:
            return fh.read()

    def test_dry_run_writes_nothing(self):
        before = self.read_live()
        rc, out, _ = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.read_live(), before)
        self.assertIn("DRY RUN", out)
        self.assertIn("reuse 2", out)
        self.assertIn("overrides rebound: 1", out)

    def test_a_missing_clips_folder_is_refused(self):
        before = self.read_live()
        rc, _, err = self.run_main("--clips-dir", os.path.join(self.clips, "nope"), "--apply")
        self.assertEqual(rc, 2)
        self.assertIn("clips folder not found", err)
        self.assertEqual(self.read_live(), before)

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        before = self.read_live()
        with mock.patch.object(bh, "game_or_kit_running", return_value=True):
            rc, _, err = self.run_main("--apply")
        self.assertEqual(rc, 2)
        self.assertIn("REFUSED", err)
        self.assertEqual(self.read_live(), before)

    def test_apply_backs_up_writes_both_sets_and_is_idempotent(self):
        before = self.read_live()
        with mock.patch.object(bh, "game_or_kit_running", return_value=False):
            rc, _, _ = self.run_main("--apply")
            self.assertEqual(rc, 0)
            after = self.read_live()
            self.assertTrue(after.startswith(b"\xef\xbb\xbf"))
            root = ET.fromstring(after.decode("utf-8-sig").encode("utf-8"))
            sets = {s.get("id"): s for s in root}
            self.assertEqual(sets["as_hill_troll_poses"][0].get("animation"), "anim_hill_troll_idle1")
            self.assertEqual(len(list(sets["as_hill_troll_warrior"])), 9)
            backups = [f for f in os.listdir(self._tmp.name) if f.startswith("action_sets.xml.bak-hilltroll-bind-")]
            self.assertEqual(len(backups), 1)
            with open(os.path.join(self._tmp.name, backups[0]), "rb") as fh:
                self.assertEqual(fh.read(), before)
            rc, out, _ = self.run_main("--apply")
        self.assertEqual(rc, 0)
        self.assertIn("no change", out)
        self.assertEqual(self.read_live(), after)

    def test_uses_the_shared_fail_closed_process_guard(self):
        self.assertIs(bh.game_or_kit_running, _gamedir.game_or_kit_running)


if __name__ == "__main__":
    unittest.main()
