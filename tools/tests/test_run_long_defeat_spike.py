#!/usr/bin/env python3
"""tools/run_long_defeat_spike.py: the judge and the arguments, with no game.

Run:  python -m unittest tools.tests.test_run_long_defeat_spike
"""
import os
import re
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import run_fords_autorun as runner  # noqa: E402
import run_long_defeat_spike as spike  # noqa: E402

SPIKE_SOURCE = Path(__file__).resolve().parents[2] / "Main" / "Features" / "LongDefeat" / "Spike"


def spike_code():
    """Every C# file of the spike, without comments, so that a comment naming a call neither satisfies nor trips a pin."""
    text = "\n".join(path.read_text(encoding="utf-8") for path in sorted(SPIKE_SOURCE.glob("*.cs")))
    return re.sub(r"//.*", "", text)


FULL_LOG = "\n".join([
    "[LongDefeat][S1] spawn level ok: defenders 101; Infantry 68 at frame (1.0, 2.0, 3.0): far 0, off the navmesh 0, on the frame point 1, z 2.5/3.0/4.0",
    "[LongDefeat][S1] after initial spawn: deployment finished True",
    "[LongDefeat][S2] wave 1 CLEARED at 120.0 s",
    "[LongDefeat][S5] deployed Mangonel at (1.0, 2.0, 3.0)",
    "[LongDefeat][S7] after the armour write: head 10.0",
    "[LongDefeat][S4] repaired: hit points 100.0/100.0",
    "[LongDefeat][S2] wave 3 CLEARED at 400.0 s",
])
FINAL = {"reason": "final", "started": "2026-11-03T10:00:00"}
BLOCK_BEFORE_START = "[2026-11-03 09:59:59] [ERROR] [CrashReport] swallowed during the mission load"


class JudgeTests(unittest.TestCase):

    def test_no_done_file_fails(self):
        self.assertEqual(spike.judge(None, None, FULL_LOG)[0], 1)

    def test_a_full_log_passes_and_counts_the_waves(self):
        code, why = spike.judge(FINAL, None, FULL_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn("2 waves cleared", why)

    def test_a_run_that_hit_the_time_limit_fails(self):
        self.assertEqual(spike.judge({"reason": "live_limit"}, None, FULL_LOG), (1, "reason=live_limit"))

    def test_a_run_that_lost_every_defender_fails(self):
        self.assertEqual(spike.judge({"reason": "defenders_dead"}, None, FULL_LOG), (1, "reason=defenders_dead"))

    def test_no_log_fails(self):
        self.assertEqual(spike.judge(FINAL, None, None)[0], 1)

    def test_an_empty_log_fails(self):
        self.assertEqual(spike.judge(FINAL, None, "")[0], 1)

    def test_each_missing_spike_line_fails_by_name(self):
        for line in spike.EVIDENCE:
            kept = "\n".join(row for row in FULL_LOG.splitlines() if line not in row)
            code, why = spike.judge(FINAL, None, kept)
            self.assertEqual(code, 1, line)
            self.assertIn(line, why)

    def test_a_crash_report_block_before_the_start_still_fails(self):
        if runner.crash_report_line(BLOCK_BEFORE_START, None) is None:
            self.fail("the sample line does not match the runner's CrashReport pattern")
        code, why = spike.judge(FINAL, None, FULL_LOG + "\n" + BLOCK_BEFORE_START)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_a_repair_or_a_deploy_that_failed_does_not_count(self):
        for good, bad in (("[S4] repaired", "[S4] NOT repaired"), ("[S5] deployed", "[S5] NOT deployed")):
            code, why = spike.judge(FINAL, None, FULL_LOG.replace(good, bad))
            self.assertEqual(code, 1, bad)
            self.assertIn(good, why)

    def test_soldiers_on_the_wrong_floor_fail_even_when_another_batch_was_right(self):
        # The first in-game run put most of the garrison in the caves under Helm's Deep, and S1's line was still there.
        log = FULL_LOG + "\n[LongDefeat][S1] spawn level WRONG: attackers 120; Infantry 90 at frame (1.0, 2.0, 3.0): far 64, off the navmesh 8, on the frame point 0, z 0.0/0.0/4.0"
        code, why = spike.judge(FINAL, None, log)
        self.assertEqual(code, 1)
        self.assertIn("far 64", why)

    def test_a_log_from_a_build_without_the_spawn_check_fails(self):
        old = "\n".join(row for row in FULL_LOG.splitlines() if "spawn level" not in row)
        code, why = spike.judge(FINAL, None, old)
        self.assertEqual(code, 1)
        self.assertIn("spawn level ok", why)

    def test_a_run_that_also_ended_early_still_names_the_wrong_floor(self):
        # The by-hand caves run ended at the time limit, and that reason alone would hide the cause.
        log = FULL_LOG + "\n[LongDefeat][S1] spawn level WRONG: defenders 101; Infantry 68 at frame (1.0, 2.0, 3.0): far 64, off the navmesh 8, on the frame point 0, z 0.0/0.0/4.0"
        code, why = spike.judge({"reason": "live_limit"}, None, log)
        self.assertEqual(code, 1)
        self.assertIn("reason=live_limit", why)
        self.assertIn("far 64", why)


class SourceTieTests(unittest.TestCase):
    """The judge and the arm text name strings the C# side owns. A rewording there would otherwise pass every test here."""

    def test_each_evidence_line_is_text_the_runner_writes(self):
        source = (SPIKE_SOURCE / "LongDefeatSpikeRunner.cs").read_text(encoding="utf-8")
        for line in spike.EVIDENCE:
            text = line.replace("[LongDefeat]", "").replace("wave 1", "wave {_wave}")
            self.assertIn(text, source, line)

    def test_the_failure_label_is_text_the_runner_writes(self):
        source = (SPIKE_SOURCE / "LongDefeatSpikeRunner.cs").read_text(encoding="utf-8")
        self.assertIn(spike.SPAWN_LEVEL_WRONG.replace("[LongDefeat]", ""), source)

    def test_the_engine_places_each_soldier_not_a_grid_of_the_runner(self):
        # The engine computes a soldier's place only when the build data holds no position (Mission.cs:4227, :4262). A place from the runner skips the layout's navmesh filter.
        code = spike_code()
        self.assertRegex(code, r"GetAgentBuildDataToSpawnTroop\([^;]*,\s*null,\s*null\b")
        self.assertNotIn(".InitialPosition(", code)
        self.assertIn(".SetFormationPositioningFromDeploymentPlan(", code)
        self.assertIn(".MakeDefaultDeploymentPlans(", code)

    def test_every_batch_is_read_back(self):
        for who in ("defenders", "reinforcements", "attackers"):
            self.assertRegex(spike_code(), r'LogSpawnLevel\([^;]*"%s"\)' % who, who)

    def test_the_judged_lines_carry_the_tag(self):
        code = spike_code()
        self.assertIn('Tag = "[LongDefeat]"', code)
        self.assertRegex(code, r"\bInfo\([^;]*\[S1\] spawn level WRONG")

    def test_each_arm_key_is_one_the_options_read(self):
        source = (SPIKE_SOURCE / "LongDefeatSpikeOptions.cs").read_text(encoding="utf-8")
        known = SimpleNamespace(fortress="helms", waves=3, wave_size=120, garrison=100, intermission=15, live_limit=900, no_defenders_ai=False)
        for key, _ in spike.arm_pairs(known, "out"):
            self.assertIn(f'case "{key}":', source, key)

    def test_the_file_names_match_the_runner(self):
        source = (SPIKE_SOURCE / "LongDefeatSpikeRunner.cs").read_text(encoding="utf-8")
        options = (SPIKE_SOURCE / "LongDefeatSpikeOptions.cs").read_text(encoding="utf-8")
        self.assertIn(f'DoneFile = "{spike.DONE_FILE}"', source)
        self.assertIn(f'StatusFile = "{spike.STATUS_FILE}"', source)
        self.assertIn(f'EnvVar = "{spike.ENV_VAR}"', options)


class PatchTests(unittest.TestCase):

    def test_importing_the_wrapper_leaves_the_fords_runner_alone(self):
        self.assertEqual(runner.ENV_VAR, "TAOM_FORDS_AUTORUN")
        self.assertEqual(runner.DONE_FILE, "fords_autorun_done.txt")

    def test_main_points_the_runner_at_the_spike(self):
        seen = {}

        def fake_main(rest):
            seen.update(env=runner.ENV_VAR, done=runner.DONE_FILE, status=runner.STATUS_FILE, root=runner.RUNS_ROOT,
                        judge=runner.judge, arm=runner.arm_text_for, rest=rest)
            return 7

        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for")}
        with mock.patch.multiple(runner, main=fake_main, **keep):
            self.assertEqual(spike.main(["--dry-run"]), 7)
        self.assertEqual(seen["env"], spike.ENV_VAR)
        self.assertEqual(seen["done"], spike.DONE_FILE)
        self.assertEqual(seen["status"], spike.STATUS_FILE)
        self.assertEqual(seen["root"], spike.RUNS_ROOT)
        self.assertIs(seen["judge"], spike.judge)
        self.assertEqual(seen["rest"], ["--dry-run"])
        args = SimpleNamespace(wall_limit=None)
        text, limit = seen["arm"]("E:/out", args)
        self.assertIn("wave_size=120\n", text)
        self.assertIn("out=E:/out\n", text)
        self.assertEqual(limit, runner.DEFAULT_WALL_LIMIT_SECONDS)


class ArgumentTests(unittest.TestCase):

    def parse(self, *argv):
        return spike.build_parser().parse_known_args(list(argv))[0]

    def test_the_defaults_parse(self):
        known = self.parse()
        self.assertEqual(known.fortress, "helms")
        self.assertGreater(known.wave_size, spike.SHOCK_ASSAULT_UNITS)

    def test_an_unknown_fortress_is_refused(self):
        with self.assertRaises(SystemExit):
            self.parse("--fortress", "rivendell")

    def test_a_count_of_zero_is_refused(self):
        for flag in ("--waves", "--wave-size", "--garrison"):
            with self.assertRaises(SystemExit):
                self.parse(flag, "0")

    def test_a_negative_time_is_refused(self):
        for flag in ("--intermission", "--live-limit"):
            with self.assertRaises(SystemExit):
                self.parse(flag, "-1")

    def test_one_wave_is_refused_because_the_tests_run_between_waves(self):
        with self.assertRaises(SystemExit):
            spike.main(["--waves", "1", "--dry-run"])


if __name__ == "__main__":
    unittest.main()
