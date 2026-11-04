#!/usr/bin/env python
"""tools/run_long_defeat.py: the judge, the arguments and the strings it shares with the C# side, with no game.

Run:  python -m unittest tools.tests.test_run_long_defeat
"""
import io
import os
import re
import sys
import tempfile
import tomllib
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import run_fords_autorun as runner  # noqa: E402
import run_long_defeat as ld  # noqa: E402
import ingame_verdict as iv  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
LD_SOURCE = REPO / "Main" / "Features" / "LongDefeat"


def source(*parts):
    return (LD_SOURCE.joinpath(*parts)).read_text(encoding="utf-8")


def pass_line(name):
    return f"[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.{name} result=pass detail=1"


HEAD = "0123456789abcdef0123456789abcdef01234567"
FIXTURE_HEAD = "985b0d54e1672ede8d9dc08ed986d0f3870aa5d3"
FIXTURE = REPO / "tools" / "tests" / "fixtures" / "long_defeat_checks.txt"
LOST_FIXTURE = REPO / "tools" / "tests" / "fixtures" / "long_defeat_lost_checks.txt"
READ_BACK_LINES = ["[2026-11-04 10:00:00] [INFO] [LongDefeat] spawn level ok: defenders Infantry 20 of 20, far 0, off the navmesh 0, on the frame point 2",
                   "[2026-11-04 10:00:00] [INFO] [LongDefeat] spawn level ok: attackers Infantry 83 of 83, far 0, off the navmesh 0, on the frame point 2"]


def stamp_line(sha=HEAD, dirty=False):
    return (f"[2026-11-04 10:00:00] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261104-100000Z+{sha}{'.dirty' if dirty else ''} "
            "TAOM.Dependencies=v0.1.0.0 (pair OK)")


def log_of(names, waves, stamp=None):
    """A log the way a run writes it: the stamp first, one pass line per id (one per cleared wave for wave), the two read-backs."""
    rows = [stamp_line() if stamp is None else stamp]
    for name in names:
        rows.extend([pass_line(name)] * (waves if name == "wave" else 1))
    return "\n".join(rows + READ_BACK_LINES)


FULL_LOG = log_of(ld.REQUIRED_CHECKS, 5)
LOSE_LOG = log_of(ld.LOSE_CHECKS, 0)
FINAL = {"reason": "final", "started": "2026-11-04T10:00:00"}
REPORT = {"final": True, "wavesCleared": 5, "wavesFelled": 1}
BLOCK = "[2026-11-04 09:59:59] [ERROR] [CrashReport] swallowed during the mission load"


class JudgeTests(unittest.TestCase):

    def test_the_required_list_is_the_plans(self):
        # Update this tuple as each later slice grows the list: FULL_LOG and the loop below are built from REQUIRED_CHECKS itself.
        self.assertEqual(ld.REQUIRED_CHECKS, ("menu", "boot", "general", "muster", "keep", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run"))

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)

    def test_a_given_speed_must_be_the_reports_max_speed(self):
        ld.expected_speed = 10
        self.addCleanup(setattr, ld, "expected_speed", None)

        code, why = ld.judge(FINAL, {**REPORT, "maxSpeed": 10}, FULL_LOG)

        self.assertEqual(code, 0, why)
        self.assertIn("speed 10x", why)

    def test_a_report_with_another_max_speed_fails_naming_both(self):
        ld.expected_speed = 10
        self.addCleanup(setattr, ld, "expected_speed", None)

        for report in ({**REPORT, "maxSpeed": 4}, {**REPORT, "maxSpeed": 1}, REPORT, {**REPORT, "maxSpeed": "10"}):
            code, why = ld.judge(FINAL, report, FULL_LOG)
            self.assertEqual(code, 1, report)
            self.assertIn("maxSpeed", why)
            self.assertIn("--speed 10", why)

    def test_the_speed_check_holds_a_lose_run_too(self):
        ld.expected_speed = 4
        self.addCleanup(setattr, ld, "expected_speed", None)
        lost = {"reason": "lost_defenders", "expect": "lost"}

        self.assertEqual(ld.judge(lost, {"maxSpeed": 4, "final": True}, LOSE_LOG)[0], 0)
        self.assertEqual(ld.judge(lost, {"maxSpeed": 1, "final": True}, LOSE_LOG)[0], 1)

    def test_no_speed_given_asks_nothing_of_the_report(self):
        self.assertIsNone(ld.expected_speed)
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)
        self.assertNotIn("speed", ld.judge(FINAL, REPORT, FULL_LOG)[1])

    def test_a_full_run_passes_and_counts_the_felled_waves(self):
        code, why = ld.judge(FINAL, REPORT, FULL_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn("5 waves cleared", why)
        self.assertIn("1 waves ended by felling", why)

    def test_each_side_must_be_read_back(self):
        # Attackers have no other floor check, so a log with only the defenders' read-back must not pass.
        for line in ld.READ_BACK:
            kept = "\n".join(row for row in FULL_LOG.splitlines() if line not in row)
            code, why = ld.judge(FINAL, REPORT, kept)
            self.assertEqual(code, 1, line)
            self.assertIn(line, why)

    def test_a_lose_run_is_read_back_too(self):
        for line in ld.READ_BACK:
            kept = "\n".join(row for row in LOSE_LOG.splitlines() if line not in row)
            self.assertEqual(ld.judge({"reason": "lost_keep", "expect": "lost"}, {"final": False}, kept)[0], 1, line)

    def test_a_stamp_that_names_this_trees_head_passes_and_names_the_build(self):
        code, why = ld.judge(FINAL, REPORT, FULL_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {HEAD[:9]},", why)

    def test_a_dirty_stamp_on_this_trees_head_passes_and_says_dirty(self):
        code, why = ld.judge(FINAL, REPORT, log_of(ld.REQUIRED_CHECKS, 5, stamp_line(dirty=True)))
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {HEAD[:9]}.dirty", why)

    def test_a_stale_deploy_fails_naming_both_builds(self):
        old = "f" * 40
        code, why = ld.judge(FINAL, REPORT, log_of(ld.REQUIRED_CHECKS, 5, stamp_line(old)))
        self.assertEqual(code, 1)
        self.assertIn(old[:9], why)
        self.assertIn(HEAD[:9], why)

    def test_a_log_with_no_stamp_fails(self):
        code, why = ld.judge(FINAL, REPORT, "\n".join(row for row in FULL_LOG.splitlines() if "[BuildStamp]" not in row))
        self.assertEqual(code, 1)
        self.assertIn("BuildStamp", why)

    def test_an_unknown_tree_head_fails_rather_than_skips_the_match(self):
        ld.tree_head = None
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 1)

    def test_fewer_wave_pass_lines_than_waves_cleared_fails(self):
        # The manifest asks for 5 wave lines; one line beside wavesCleared=5 passed the judge.
        code, why = ld.judge(FINAL, REPORT, log_of(ld.REQUIRED_CHECKS, 4))
        self.assertEqual(code, 1)
        self.assertIn("4 wave pass lines", why)

    def test_the_wave_lines_are_counted_against_what_the_report_cleared(self):
        ld.target_waves = 2
        self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=2), log_of(ld.REQUIRED_CHECKS, 2))[0], 0)
        self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=3), log_of(ld.REQUIRED_CHECKS, 2))[0], 1)

    def test_no_done_file_fails(self):
        self.assertEqual(ld.judge(None, REPORT, FULL_LOG), (1, "no done file"))

    def test_every_reason_but_final_fails_a_normal_run(self):
        for reason in ("lost_defenders", "lost_keep", "stalemate", "abandoned", "fault", "live_limit", "wall_limit", "start_refused",
                       "never_started", "final+exit_stalled", ""):
            code, why = ld.judge({"reason": reason}, REPORT, FULL_LOG)
            self.assertEqual(code, 1, reason)
            self.assertIn("wanted final", why)

    def test_a_missing_report_fails(self):
        self.assertEqual(ld.judge(FINAL, None, FULL_LOG)[0], 1)

    def test_a_report_that_is_not_final_fails(self):
        self.assertEqual(ld.judge(FINAL, dict(REPORT, final=False), FULL_LOG), (1, "the JSON report is not final"))

    def test_too_few_waves_cleared_fails(self):
        code, why = ld.judge(FINAL, dict(REPORT, wavesCleared=4), FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("wavesCleared=4", why)

    def test_a_report_with_no_wave_count_fails(self):
        # At a target of 1, True would pass a bare comparison (True == 1), so only the type test can fail it.
        ld.target_waves = 1
        for bad in (None, "5", True):
            self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=bad), log_of(ld.REQUIRED_CHECKS, 1))[0], 1, repr(bad))

    def test_no_log_or_an_empty_log_fails(self):
        self.assertEqual(ld.judge(FINAL, REPORT, None)[0], 1)
        self.assertEqual(ld.judge(FINAL, REPORT, "")[0], 1)

    def test_each_required_check_missing_fails_by_name(self):
        for name in ld.REQUIRED_CHECKS:
            kept = "\n".join(row for row in FULL_LOG.splitlines() if f"long-defeat.{name} " not in row)
            code, why = ld.judge(FINAL, REPORT, kept)
            self.assertEqual(code, 1, name)
            self.assertIn(f"taom.long-defeat.{name}", why)

    def test_a_check_that_only_skipped_does_not_count_as_a_pass(self):
        skipped = FULL_LOG.replace("long-defeat.wave result=pass", "long-defeat.wave result=skip")
        code, why = ld.judge(FINAL, REPORT, skipped)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.wave", why)

    def test_a_check_id_that_only_starts_like_a_required_one_does_not_count(self):
        # taom.long-defeat.gold-extra must not satisfy taom.long-defeat.gold
        log = FULL_LOG.replace("long-defeat.gold result=pass", "long-defeat.gold-extra result=pass")
        self.assertEqual(ld.judge(FINAL, REPORT, log)[0], 1)

    def test_a_failed_check_fails_even_when_the_same_id_passed_elsewhere(self):
        log = FULL_LOG + "\n[2026-11-04 10:01:00] [INFO] [Check] id=taom.long-defeat.shop result=fail spawned=3"
        code, why = ld.judge(FINAL, REPORT, log)
        self.assertEqual(code, 1)
        self.assertIn("long-defeat.shop result=fail", why)

    def test_another_features_failed_check_is_not_this_runs_business(self):
        log = FULL_LOG + "\n[Check] id=taom.fords.something result=fail"
        self.assertEqual(ld.judge(FINAL, REPORT, log)[0], 0)

    def test_soldiers_on_the_wrong_floor_fail_whatever_else_passed(self):
        log = FULL_LOG + "\n[LongDefeat] spawn level WRONG: attackers Infantry 90 of 90, far 64, off the navmesh 8, on the frame point 0"
        code, why = ld.judge(FINAL, REPORT, log)
        self.assertEqual(code, 1)
        self.assertIn("far 64", why)

    def test_a_run_that_also_ended_early_still_names_the_wrong_floor(self):
        log = FULL_LOG + "\n[LongDefeat] spawn level WRONG: defenders Infantry 68 of 68, far 64, off the navmesh 8, on the frame point 0"
        code, why = ld.judge({"reason": "live_limit"}, REPORT, log)
        self.assertEqual(code, 1)
        self.assertIn("reason=live_limit", why)
        self.assertIn("far 64", why)

    def test_a_crash_report_block_before_the_start_still_fails(self):
        self.assertIsNotNone(runner.crash_report_line(BLOCK, None), "the sample line does not match the runner's CrashReport pattern")
        code, why = ld.judge(FINAL, REPORT, FULL_LOG + "\n" + BLOCK)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)


class LoseJudgeTests(unittest.TestCase):
    """A --lose run arms expect=lost, and the done file echoes it."""

    LOST = {"reason": "lost_defenders", "expect": "lost"}

    def setUp(self):
        ld.tree_head = HEAD
        self.addCleanup(setattr, ld, "tree_head", None)

    def test_a_lost_run_passes_on_the_shorter_list_with_no_wave_cleared(self):
        for reason in ld.LOSE_REASONS:
            code, why = ld.judge({"reason": reason, "expect": "lost"}, {"final": False, "wavesCleared": 0}, LOSE_LOG)
            self.assertEqual(code, 0, why)

    def test_a_lost_run_with_no_wave_cleared_passes_on_the_lines_the_director_writes(self):
        # LongDefeatChecks.BestRunSaved for a 0-wave run saved into a fresh folder and read back.
        best = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.best-run result=pass waves=0 kills=14 read_back=same"
        log = "\n".join([stamp_line()] + [pass_line(n) for n in ld.LOSE_CHECKS if n != "best-run"] + [best] + READ_BACK_LINES)
        self.assertEqual(ld.judge(self.LOST, {"final": False, "wavesCleared": 0}, log)[0], 0)
        skipped = best.replace("result=pass", "result=skip")
        self.assertEqual(ld.judge(self.LOST, {"final": False, "wavesCleared": 0}, log.replace(best, skipped))[0], 1)

    def test_a_lost_run_is_not_asked_for_the_wave_shop_or_horn_checks(self):
        for name in ("wave", "no-rout", "gold", "shop", "early-start"):
            self.assertNotIn(name, ld.LOSE_CHECKS)

    def test_the_shorter_list_is_the_plans(self):
        self.assertEqual(ld.LOSE_CHECKS, ("menu", "boot", "general", "muster", "keep", "orders", "hud", "end", "best-run"))

    def test_each_check_of_the_shorter_list_missing_fails(self):
        for name in ld.LOSE_CHECKS:
            kept = "\n".join(row for row in LOSE_LOG.splitlines() if f"long-defeat.{name} " not in row)
            self.assertEqual(ld.judge(self.LOST, {"final": False}, kept)[0], 1, name)

    def test_a_lose_run_that_won_fails(self):
        code, why = ld.judge({"reason": "final", "expect": "lost"}, REPORT, LOSE_LOG)
        self.assertEqual(code, 1)
        self.assertIn("lost_defenders or lost_keep", why)

    def test_a_lose_run_that_stalled_or_faulted_fails(self):
        for reason in ("stalemate", "fault", "abandoned", "live_limit"):
            self.assertEqual(ld.judge({"reason": reason, "expect": "lost"}, {"final": False}, LOSE_LOG)[0], 1, reason)

    def test_a_lost_run_with_a_failed_check_fails(self):
        log = LOSE_LOG + "\n[Check] id=taom.long-defeat.end result=fail reason=fault"
        self.assertEqual(ld.judge(self.LOST, {"final": False}, log)[0], 1)


class ShaderDialogTests(unittest.TestCase):
    """Helm's Deep can stop its load at the terrain shader's compile dialog (X4008), and nobody is at an unattended game to answer it."""

    TITLE = "$BASE/Shaders/Sources/pbr_terrain.rs"
    TEXTS = ["&Retry", "Cancel", 'Error compiling shader file: "$BASE/Shaders/Sources/pbr_terrain.rs" error X4008']
    BUTTONS = {"&Retry": 4, "Cancel": 2}

    def answer(self, dialogs, click=True, seen=None):
        said, clicks = [], []
        seen = set() if seen is None else seen
        with mock.patch.object(runner, "game_dialogs", lambda pid: dialogs), \
                mock.patch.object(runner, "click_button", lambda hwnd, control: clicks.append((hwnd, control)) or click):
            self.returned = ld.handle_dialogs(1, seen, said.append)
        return said, clicks, seen

    def test_the_terrain_shader_dialog_gets_cancel(self):
        said, clicks, seen = self.answer([(7, self.TITLE, self.TEXTS, self.BUTTONS)])
        self.assertEqual(clicks, [(7, 2)])
        self.assertIn(7, seen)
        self.assertIn("Cancel", said[0])

    def test_the_gbuffer_variant_gets_cancel_too(self):
        _, clicks, _ = self.answer([(8, "$BASE/Shaders/Sources/pbr_terrain_gbuffer.rs", self.TEXTS, self.BUTTONS)])
        self.assertEqual(clicks, [(8, 2)])

    def test_another_shader_dialog_is_left_open(self):
        for shader in ("taom_deformer_bow", "pbr_terrain_shadowmap"):
            said, clicks, _ = self.answer([(9, f"$BASE/Shaders/Sources/{shader}.rs", ["&Retry", "Cancel", "Error"], self.BUTTONS)])
            self.assertEqual(clicks, [], shader)
            self.assertIn("left open", said[0], shader)

    def test_another_error_in_the_same_shader_is_left_open(self):
        # Only X4008 is pressed: a new error in the terrain shader is a new fact, and the runner logs it.
        texts = ["&Retry", "Cancel", 'Error compiling shader file: "$BASE/Shaders/Sources/pbr_terrain.rs" error X3004: undeclared identifier']
        said, clicks, _ = self.answer([(7, self.TITLE, texts, self.BUTTONS)])
        self.assertEqual(clicks, [])
        self.assertIn("left open", said[0])

    def test_a_dialog_that_lingers_is_pressed_again_and_logged_once(self):
        said, clicks, _ = self.answer([(7, self.TITLE, self.TEXTS, self.BUTTONS)], seen={7})
        self.assertEqual(clicks, [(7, 2)])
        self.assertEqual(said, [])

    def test_a_click_that_fails_does_not_claim_a_press(self):
        said, _, seen = self.answer([(7, self.TITLE, self.TEXTS, self.BUTTONS)], click=False)
        self.assertFalse(any("pressed" in line for line in said), said)
        self.assertIn("left open", said[0])

    def test_a_matching_title_with_no_cancel_button_is_left_open(self):
        said, clicks, _ = self.answer([(7, self.TITLE, ["&Retry", self.TEXTS[2]], {"&Retry": 4})])
        self.assertEqual(clicks, [])
        self.assertIn("left open", said[0])

    def test_the_fords_mod_change_answer_still_runs(self):
        notice = (5, runner.MOD_CHANGE_TITLE, [runner.MOD_CHANGE_TEXT + " x", "OK"], {"OK": 1})
        said, clicks, _ = self.answer([notice])
        self.assertEqual(clicks, [(5, 1)])

    def test_a_crash_dialog_still_ends_the_watch(self):
        self.answer([(5, "Fatal problem", ["boom"], {"OK": 1})])
        self.assertIn("boom", self.returned)

    def test_a_failed_listing_does_not_stop_the_watch(self):
        said = []

        def broken(pid):
            raise OSError("no windows")

        with mock.patch.object(runner, "game_dialogs", broken):
            self.assertIsNone(ld.handle_dialogs(1, set(), said.append))
        self.assertIn("dialog check failed", said[0])


class SourceTieTests(unittest.TestCase):
    """The judge and the arm text name strings the C# side owns. A rewording there would otherwise pass every test here."""

    def test_every_required_check_is_an_id_a_checks_class_owns(self):
        # The client writes orders and hud (LongDefeatClientChecks); the director writes the rest (LongDefeatChecks).
        checks = source("Diagnostics", "LongDefeatChecks.cs") + (REPO / "Client" / "Features" / "LongDefeat" / "LongDefeatClientChecks.cs").read_text(encoding="utf-8")
        for name in ld.REQUIRED_CHECKS:
            self.assertIn(f'"taom.long-defeat.{name}"', checks, name)

    def test_the_spawn_level_label_is_text_the_director_writes(self):
        # The director's own test asserts the joined literal from the log it writes, so a rewording there fails that test and this one.
        director_test = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "LongDefeatDirectorTests.cs").read_text(encoding="utf-8")
        self.assertIn(f'Contains("{ld.SPAWN_LEVEL_WRONG}")', director_test)

    def test_the_read_back_lines_are_text_the_director_writes(self):
        director_test = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "LongDefeatDirectorTests.cs").read_text(encoding="utf-8")
        for line in ld.READ_BACK:
            self.assertTrue(line.startswith("spawn level ok: "), line)
            self.assertIn(f'"{line}', director_test, line)

    def test_each_arm_key_is_one_the_plan_reads(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        known = SimpleNamespace(fortress="helms", waves=5, lose=True, garrison_scale=None, live_limit=900, screenshots="5", speed=10)
        for key, _ in ld.arm_pairs(known, "out", 3900):
            self.assertIn(f'case "{key}":', plan, key)

    def test_speed_is_armed_when_given_and_left_out_when_not(self):
        parser = ld.build_parser()

        known, _ = parser.parse_known_args(["--speed", "10"])
        self.assertEqual(10, known.speed)
        self.assertEqual(10, dict(ld.arm_pairs(known, "out", 3900))["speed"])

        known, _ = parser.parse_known_args([])
        self.assertIsNone(known.speed)
        self.assertIsNone(dict(ld.arm_pairs(known, "out", 3900))["speed"], "an unset speed is dropped from the arm text, so the game plays at 1x")

    def test_a_speed_that_is_no_level_is_refused_before_the_game_starts(self):
        parser = ld.build_parser()
        for bad in ("3", "0", "20", "x"):
            with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit, msg=bad):
                parser.parse_known_args(["--speed", bad])

    def test_the_speed_levels_are_the_ones_the_game_accepts(self):
        game = (REPO / "Main" / "Features" / "FordsOfWar" / "Domain" / "FordsGameSpeed.cs").read_text(encoding="utf-8")
        self.assertIn("{ 1, 2, 4, FastForwardLevel }", game)
        self.assertEqual([1, 2, 4, 10], list(ld.SPEED_LEVELS))

    def test_the_file_names_and_the_variable_match_the_game(self):
        done = source("Autorun", "LongDefeatAutorunDone.cs")
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        self.assertIn(f'DoneFile = "{ld.DONE_FILE}"', done)
        self.assertIn(f'StatusFile = "{ld.STATUS_FILE}"', done)
        self.assertIn(f'EnvVar = "{ld.ENV_VAR}"', plan)

    def test_the_default_limits_are_the_plans(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        self.assertIn(f"DefaultLiveLimitSeconds = {ld.DEFAULT_LIVE_LIMIT}.0", plan)
        self.assertIn(f"DefaultWallLimitSeconds = {ld.DEFAULT_WALL_LIMIT}.0", plan)

    def test_the_done_reasons_the_judge_reads_are_words_the_game_writes(self):
        done = source("Autorun", "LongDefeatAutorunDone.cs")
        driver = source("Autorun", "LongDefeatAutorunDriver.cs")
        for reason in ("final", "lost_defenders", "lost_keep", "stalemate", "abandoned"):
            self.assertIn(f'"{reason}"', done, reason)
        for reason in ("fault", "live_limit", "wall_limit", "never_started"):
            self.assertIn(f'"{reason}"', driver, reason)

    def test_the_report_keys_the_judge_reads_are_keys_the_report_writes(self):
        report = source("Diagnostics", "LongDefeatRunReport.cs")
        for key in ("final", "wavesCleared", "wavesFelled"):
            self.assertIn(f'["{key}"]', report, key)

    def test_the_done_file_carries_the_keys_the_runner_reads(self):
        done = source("Autorun", "LongDefeatAutorunDone.cs")
        for key in ("reason", "report", "log", "started", "expect"):
            self.assertIn(f'"{key}="', done, key)

    def test_the_lose_scale_is_one_the_game_accepts(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        low = float(re.search(r"IsFiniteInRange\(scale, ([0-9.]+)f, MaxGarrisonScale\)", plan).group(1))
        high = float(re.search(r"MaxGarrisonScale = ([0-9.]+)f;", plan).group(1))
        self.assertGreaterEqual(ld.LOSE_GARRISON_SCALE, low)
        self.assertLessEqual(ld.LOSE_GARRISON_SCALE, high)
        parser = ld.build_parser()
        self.assertEqual(parser.parse_args(["--garrison-scale", str(low)]).garrison_scale, low)
        self.assertEqual(parser.parse_args(["--garrison-scale", str(high)]).garrison_scale, high)


class ManifestTests(unittest.TestCase):
    """Every id the judge requires has a row in the manifest, and a row lands with its emitter."""

    def manifest(self):
        return tomllib.loads((REPO / "tools" / "ingame_checks" / "1079.toml").read_text(encoding="utf-8"))

    def test_every_required_check_has_a_manifest_row(self):
        ids = {row["id"] for row in self.manifest()["check"]}
        for name in ld.REQUIRED_CHECKS:
            self.assertIn(f"taom.long-defeat.{name}", ids, name)

    def test_the_menu_row_is_here(self):
        # The menu check is a required check: LongDefeatMenuEntry writes it when the main menu first draws, before the autorun starts a run.
        ids = {row["id"] for row in self.manifest()["check"]}
        self.assertIn("taom.long-defeat.menu", ids)

    def test_a_boot_only_log_fails_the_mission_start_rows(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        boot = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.boot result=pass deployment_finished=true siege_ai=true main=true friendly=false"
        lines = [(None, 1, boot)]
        rows = {c.id: c for c in manifest.checks}
        self.assertEqual(iv.judge_segment(rows["taom.long-defeat.boot"], lines, []).verdict, iv.PASS)
        for name in ("general", "muster", "keep", "orders", "hud"):
            self.assertEqual(iv.judge_segment(rows[f"taom.long-defeat.{name}"], lines, []).verdict, iv.FAIL, name)
        self.assertEqual(iv.judge_segment(rows["taom.long-defeat.general"], [], []).verdict, iv.NOT_REACHED)

    def fixture_lines(self, extra=()):
        rows = FIXTURE.read_text(encoding="utf-8").splitlines() + list(extra)
        return [(None, number, row) for number, row in enumerate(rows, 1)]

    def test_the_real_run_passes_the_judge(self):
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8")
        code, why = ld.judge({"reason": "final"}, {"final": True, "wavesCleared": 2, "wavesFelled": 0}, text)
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {FIXTURE_HEAD[:9]}.dirty", why)

    def test_the_wave_row_needs_its_minimum_of_five_lines(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.wave")
        self.assertEqual(row.min, 5)
        wave = next(r for r in FIXTURE.read_text(encoding="utf-8").splitlines() if "long-defeat.wave result=pass" in r)
        self.assertEqual(iv.judge_segment(row, self.fixture_lines([wave] * 3), []).verdict, iv.PASS, "2 + 3 lines")
        self.assertEqual(iv.judge_segment(row, self.fixture_lines([wave] * 2), []).verdict, iv.NOT_REACHED, "2 + 2 lines")

    def test_each_manifest_row_reads_pass_on_the_real_lines(self):
        # The fixture holds the lines of run 20261104-033424 (2 waves, no player death), so wave (min 5) and player-death are not met.
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        lines = self.fixture_lines()
        for row in manifest.checks:
            result = iv.judge_segment(row, lines, []).verdict
            if row.id in ("taom.long-defeat.wave", "taom.long-defeat.player-death"):
                self.assertEqual(result, iv.NOT_REACHED, row.id)
            else:
                self.assertEqual(result, iv.PASS, row.id)

    def test_an_injected_failure_flips_the_judge_and_its_row(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8")
        report = {"final": True, "wavesCleared": 2}
        for row in manifest.checks:
            bad = f"[2026-11-04 04:00:00] [INFO] [Check] id={row.id} result=fail"
            self.assertEqual(ld.judge({"reason": "final"}, report, text + "\n" + bad)[0], 1, row.id)
            self.assertEqual(iv.judge_segment(row, self.fixture_lines([bad]), []).verdict, iv.FAIL, row.id)

    def test_the_lost_run_passes_the_lose_judge_and_its_rows(self):
        # Run 20261104-035343: three waves cleared, then the defenders died. The done file of a --lose run carries expect=lost.
        ld.tree_head = FIXTURE_HEAD
        self.addCleanup(setattr, ld, "tree_head", None)
        text = LOST_FIXTURE.read_text(encoding="utf-8")
        done = {"reason": "lost_defenders", "expect": "lost"}
        code, why = ld.judge(done, {"final": False, "wavesCleared": 3}, text)
        self.assertEqual(code, 0, why)
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        lines = [(None, number, row) for number, row in enumerate(text.splitlines(), 1)]
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.player-death")
        self.assertEqual(iv.judge_segment(row, lines, []).verdict, iv.PASS)
        bad = "[2026-11-04 04:30:00] [INFO] [Check] id=taom.long-defeat.end result=fail reason=fault"
        self.assertEqual(ld.judge(done, {"final": False, "wavesCleared": 3}, text + "\n" + bad)[0], 1)

    def test_the_manifest_names_the_issue_and_the_taom_log(self):
        data = self.manifest()
        self.assertEqual(data["issue"], 1079)
        self.assertEqual(data["logs"], ["taom"])


class PatchTests(unittest.TestCase):

    def test_importing_the_wrapper_leaves_the_fords_runner_alone(self):
        self.assertEqual(runner.ENV_VAR, "TAOM_FORDS_AUTORUN")
        self.assertEqual(runner.DONE_FILE, "fords_autorun_done.txt")

    def test_main_points_the_runner_at_the_long_defeat(self):
        seen = {}

        def fake_main(rest):
            seen.update(env=runner.ENV_VAR, done=runner.DONE_FILE, status=runner.STATUS_FILE, root=runner.RUNS_ROOT,
                        judge=runner.judge, arm=runner.arm_text_for, dialogs=runner.handle_dialogs, show=runner.show_results, rest=rest)
            return 7

        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        with mock.patch.multiple(runner, main=fake_main, **keep):
            self.assertEqual(ld.main(["--waves", "2", "--dry-run", "--runs", "3"]), 7)
        self.assertEqual(seen["env"], ld.ENV_VAR)
        self.assertEqual(seen["done"], ld.DONE_FILE)
        self.assertEqual(seen["status"], ld.STATUS_FILE)
        self.assertEqual(seen["root"], ld.RUNS_ROOT)
        self.assertIs(seen["judge"], ld.judge)
        self.assertIs(seen["dialogs"], ld.handle_dialogs)
        self.assertIs(seen["show"], ld.show_results)
        # While main has the runner's name patched, the wrapper must call the Fords handler and not itself.
        with mock.patch.object(runner, "handle_dialogs", ld.handle_dialogs), mock.patch.object(runner, "game_dialogs", lambda pid: []):
            self.assertIsNone(ld.handle_dialogs(1, set(), lambda line: None))
        self.assertEqual(seen["rest"], ["--dry-run", "--runs", "3"], "the wrapper's own flags must not reach the Fords parser")
        self.assertEqual(ld.target_waves, 2)
        ld.target_waves = None

    def test_the_results_print_long_defeat_evidence_and_no_fords_labels(self):
        said = []
        with tempfile.TemporaryDirectory() as root:
            log = Path(root) / "taom_debug.log"
            log.write_text("\n".join([stamp_line(), pass_line("wave"), "[2026-11-04 10:00:00] [INFO] [Shutdown] clean module unload"]), encoding="utf-8")
            with mock.patch("sys.stdout", new_callable=io.StringIO) as out:
                ld.show_results("reason=final\n", log, REPORT, said.append)
        text = out.getvalue()
        self.assertIn("[Check] id=taom.long-defeat.wave", text)
        self.assertIn("[BuildStamp]", text)
        self.assertIn("wavesCleared", text)
        self.assertNotIn("report verdicts", text)
        self.assertNotIn("final=1 lines", text)
        self.assertIn("present", said[0])

    def test_a_run_is_judged_by_the_long_defeat_judge_not_the_fords_one(self):
        # runner.run_once calls the module global judge, so a binding taken before main swapped it would be the Fords judge, which passes
        # this done file and report with no log at all. The attribute test above cannot see that.
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}

        class Proc:
            pid, returncode = 4242, 0

            def poll(self):
                return 0

            def wait(self, timeout=None):
                return 0

        def watch(proc, folder, wall_limit, say):
            (Path(folder) / ld.DONE_FILE).write_text("reason=final\n", encoding="utf-8")
            return "done"

        with tempfile.TemporaryDirectory() as root:
            fakes = dict(launch_modules=lambda: [], game_dir=lambda root_: Path(root), my_documents=lambda: Path(root),
                         inspect_environment=lambda docs, game, modules, say: (Path(root) / "engine_config.txt", Path(root)),
                         preflight=lambda game, minutes: [], safe_watch=watch, scan_crash=lambda pid, say: None,
                         collect=lambda folder, bin_dir, done, started, pid, say: (None, {"final": True, "wavesCleared": 5}))
            with mock.patch.multiple(runner, **keep), mock.patch.multiple(runner, **fakes), \
                    mock.patch.object(runner.subprocess, "Popen", lambda *a, **k: Proc()), \
                    mock.patch.object(ld, "tree_head_sha", lambda: HEAD):
                code = ld.main(["--waves", "5", "--runs-root", root, "--require-idle-minutes", "0"])
            status = next(Path(root).glob("*/runner_status.txt")).read_text(encoding="utf-8")
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.assertEqual(code, 1)
        self.assertIn("no TAOM log", status)

    def test_the_arm_text_is_the_plans_and_the_wall_limit_is_the_long_one(self):
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        captured = {}
        with mock.patch.multiple(runner, main=lambda rest: captured.setdefault("arm", runner.arm_text_for), **keep):
            ld.main(["--waves", "3", "--screenshots", "5,60"])
        text, limit = captured["arm"]("E:/out", SimpleNamespace(wall_limit=None))
        self.assertEqual(limit, 3900)
        self.assertNotEqual(limit, runner.DEFAULT_WALL_LIMIT_SECONDS)
        for line in ("fortress=helms\n", "waves=3\n", "live_limit_seconds=3600\n", "wall_limit_seconds=3900\n", "screenshots=5,60\n",
                     "out=E:/out\n", "quit=1\n"):
            self.assertIn(line, text)
        self.assertNotIn("garrison_scale", text)
        self.assertNotIn("expect", text)
        ld.target_waves = None

    def test_the_wall_limit_flag_wins(self):
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        captured = {}
        with mock.patch.multiple(runner, main=lambda rest: captured.setdefault("arm", runner.arm_text_for), **keep):
            ld.main([])
        text, limit = captured["arm"]("E:/out", SimpleNamespace(wall_limit=500))
        self.assertEqual(limit, 500)
        self.assertIn("wall_limit_seconds=500\n", text)
        ld.target_waves = None

    def test_lose_arms_the_small_garrison_and_the_expectation(self):
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        captured = {}
        with mock.patch.multiple(runner, main=lambda rest: captured.setdefault("arm", runner.arm_text_for), **keep):
            ld.main(["--lose"])
        text, _ = captured["arm"]("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("garrison_scale=0.2\n", text)
        self.assertIn("expect=lost\n", text)
        ld.target_waves = None

    def test_an_explicit_scale_beats_the_lose_default(self):
        known = ld.build_parser().parse_known_args(["--lose", "--garrison-scale", "0.5"])[0]
        self.assertIn(("garrison_scale", 0.5), ld.arm_pairs(known, "out", 3900))


class ArgumentTests(unittest.TestCase):

    def parse(self, *argv):
        return ld.build_parser().parse_known_args(list(argv))[0]

    def test_the_defaults_parse(self):
        known = self.parse()
        self.assertEqual(known.fortress, "helms")
        self.assertEqual(known.waves, 5)
        self.assertFalse(known.lose)
        self.assertIsNone(known.garrison_scale)
        self.assertEqual(known.live_limit, 3600)
        # The spike's three waves took 1,380 s, so five need more than the spike's 1,800 s live default.
        self.assertGreater(known.live_limit, 1800)

    def test_a_count_of_zero_or_less_is_refused(self):
        for value in ("0", "-1"):
            with self.assertRaises(SystemExit):
                self.parse("--waves", value)

    def test_a_scale_outside_the_games_range_is_refused(self):
        for value in ("0", "-1", "11", "nan", "inf"):
            with self.assertRaises(SystemExit):
                self.parse("--garrison-scale", value)

    def test_a_negative_live_limit_is_refused(self):
        with self.assertRaises(SystemExit):
            self.parse("--live-limit", "-1")


if __name__ == "__main__":
    unittest.main()
