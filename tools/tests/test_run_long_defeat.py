#!/usr/bin/env python
"""tools/run_long_defeat.py: the judge, the arguments and the strings it shares with the C# side, with no game.

Run:  python -m unittest tools.tests.test_run_long_defeat
"""
import io
import json
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


# A run that clears 5 waves reached wave 5, which carries an affix, so it also owes the affix check (ld.AFFIX_FROM_CLEARS), and a report row with
# a power the game reads back owes an affix-effect line too.
STANDARD = ld.REQUIRED_CHECKS + ("affix", "affix-effect")
FULL_LOG = log_of(STANDARD, 5)
LOSE_LOG = log_of(ld.LOSE_CHECKS, 0)
FINAL = {"reason": "final", "started": "2026-11-04T10:00:00"}
def wave_row(wave, seed=7, **more):
    """A wave row of a schema 2 report with the affixes the game would roll for it: the scheduled count of the shipped schedule, drawn from the seed."""
    return {"wave": wave, "affixes": ld.affix_roll(seed, wave, ld.affix_count(ld.shipped_affix_schedule(), wave)), **more}


REPORT = {"schema": 2, "seed": 7, "startWave": 1, "poison": {"ownerMissing": 0}, "final": True, "wavesCleared": 5, "wavesFelled": 1}
BLOCK = "[2026-11-04 09:59:59] [ERROR] [CrashReport] swallowed during the mission load"


class JudgeTests(unittest.TestCase):

    def test_the_required_list_is_the_plans(self):
        # Update this tuple as each later slice grows the list: FULL_LOG and the loop below are built from REQUIRED_CHECKS itself.
        self.assertEqual(ld.REQUIRED_CHECKS, ("menu", "boot", "general", "muster", "keep", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run"))

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
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

        self.assertEqual(ld.judge(lost, {**REPORT, "maxSpeed": 4}, LOSE_LOG)[0], 0)
        self.assertEqual(ld.judge(lost, {**REPORT, "maxSpeed": 1}, LOSE_LOG)[0], 1)

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
            self.assertEqual(ld.judge({"reason": "lost_keep", "expect": "lost"}, {**REPORT, "final": False}, kept)[0], 1, line)

    def test_a_stamp_that_names_this_trees_head_passes_and_names_the_build(self):
        code, why = ld.judge(FINAL, REPORT, FULL_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {HEAD[:9]},", why)

    def test_a_dirty_stamp_on_this_trees_head_passes_and_says_dirty(self):
        code, why = ld.judge(FINAL, REPORT, log_of(STANDARD, 5, stamp_line(dirty=True)))
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {HEAD[:9]}.dirty", why)

    def test_a_stale_deploy_fails_naming_both_builds(self):
        old = "f" * 40
        code, why = ld.judge(FINAL, REPORT, log_of(STANDARD, 5, stamp_line(old)))
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
        code, why = ld.judge(FINAL, REPORT, log_of(STANDARD, 4))
        self.assertEqual(code, 1)
        self.assertIn("4 wave pass lines", why)

    def test_the_wave_lines_are_counted_against_what_the_report_cleared(self):
        ld.target_waves = 2
        self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=2), log_of(STANDARD, 2))[0], 0)
        self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=3), log_of(STANDARD, 2))[0], 1)

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
            self.assertEqual(ld.judge(FINAL, dict(REPORT, wavesCleared=bad), log_of(STANDARD, 1))[0], 1, repr(bad))

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
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "tree_head", None)

    def test_a_lost_run_passes_on_the_shorter_list_with_no_wave_cleared(self):
        for reason in ld.LOSE_REASONS:
            code, why = ld.judge({"reason": reason, "expect": "lost"}, {**REPORT, "final": False, "wavesCleared": 0}, LOSE_LOG)
            self.assertEqual(code, 0, why)

    def test_a_lost_run_with_no_wave_cleared_passes_on_the_lines_the_director_writes(self):
        # LongDefeatChecks.BestRunSaved for a 0-wave run saved into a fresh folder and read back.
        best = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.best-run result=pass waves=0 kills=14 read_back=same"
        log = "\n".join([stamp_line()] + [pass_line(n) for n in ld.LOSE_CHECKS if n != "best-run"] + [best] + READ_BACK_LINES)
        self.assertEqual(ld.judge(self.LOST, {**REPORT, "final": False, "wavesCleared": 0}, log)[0], 0)
        skipped = best.replace("result=pass", "result=skip")
        self.assertEqual(ld.judge(self.LOST, {**REPORT, "final": False, "wavesCleared": 0}, log.replace(best, skipped))[0], 1)

    def test_a_lost_run_is_not_asked_for_the_wave_shop_or_horn_checks(self):
        for name in ("wave", "no-rout", "gold", "shop", "early-start"):
            self.assertNotIn(name, ld.LOSE_CHECKS)

    def test_the_shorter_list_is_the_plans(self):
        self.assertEqual(ld.LOSE_CHECKS, ("menu", "boot", "general", "muster", "keep", "orders", "hud", "end", "best-run"))

    def test_each_check_of_the_shorter_list_missing_fails(self):
        for name in ld.LOSE_CHECKS:
            kept = "\n".join(row for row in LOSE_LOG.splitlines() if f"long-defeat.{name} " not in row)
            self.assertEqual(ld.judge(self.LOST, {**REPORT, "final": False}, kept)[0], 1, name)

    def test_a_lose_run_that_won_fails(self):
        code, why = ld.judge({"reason": "final", "expect": "lost"}, REPORT, LOSE_LOG)
        self.assertEqual(code, 1)
        self.assertIn("lost_defenders or lost_keep", why)

    def test_a_lose_run_that_stalled_or_faulted_fails(self):
        for reason in ("stalemate", "fault", "abandoned", "live_limit"):
            self.assertEqual(ld.judge({"reason": reason, "expect": "lost"}, {**REPORT, "final": False}, LOSE_LOG)[0], 1, reason)

    def test_a_lost_run_with_a_failed_check_fails(self):
        log = LOSE_LOG + "\n[Check] id=taom.long-defeat.end result=fail reason=fault"
        self.assertEqual(ld.judge(self.LOST, {**REPORT, "final": False}, log)[0], 1)


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
        known = ld.build_parser().parse_known_args(["--lose", "--screenshots", "5", "--speed", "10", "--seed", "1", "--start-wave", "3"])[0]
        for key, _ in ld.arm_pairs(known, "out", 3900, "archer"):
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
        code, why = ld.judge({"reason": "final"}, {**REPORT, "wavesCleared": 2, "wavesFelled": 0}, text)
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
            if row.id in ("taom.long-defeat.wave", "taom.long-defeat.player-death", "taom.long-defeat.scale", "taom.long-defeat.affix", "taom.long-defeat.affix-effect"):
                self.assertEqual(result, iv.NOT_REACHED, row.id)
            else:
                self.assertEqual(result, iv.PASS, row.id)

    def test_an_injected_failure_flips_the_judge_and_its_row(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8")
        report = {**REPORT, "wavesCleared": 2}
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
        code, why = ld.judge(done, {**REPORT, "final": False, "wavesCleared": 3}, text)
        self.assertEqual(code, 0, why)
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        lines = [(None, number, row) for number, row in enumerate(text.splitlines(), 1)]
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.player-death")
        self.assertEqual(iv.judge_segment(row, lines, []).verdict, iv.PASS)
        bad = "[2026-11-04 04:30:00] [INFO] [Check] id=taom.long-defeat.end result=fail reason=fault"
        self.assertEqual(ld.judge(done, {**REPORT, "final": False, "wavesCleared": 3}, text + "\n" + bad)[0], 1)

    SCALE_LINE = ("[2026-11-05 10:00:00] [INFO] [Check] id=taom.long-defeat.scale result=pass w=11 health=1.050 spawn=1.050 late=1.049 "
                  "troop=fow_urukhai_spearman in_mix=true damage=1.03 amplified=12 steps=0 over_room=0")

    def test_the_scale_row_reads_the_line_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.scale")
        self.assertEqual(row.absence, "not-reached")
        lines = [(None, 1, self.SCALE_LINE)]
        self.assertEqual(iv.judge_segment(row, lines, []).verdict, iv.PASS)
        failed = self.SCALE_LINE.replace("result=pass", "result=fail")
        self.assertEqual(iv.judge_segment(row, [(None, 1, failed)], []).verdict, iv.FAIL)
        self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED)

    def test_the_scale_line_is_the_one_the_csharp_check_formats(self):
        checks = source("Diagnostics", "LongDefeatChecks.cs")
        self.assertIn('ScaleId = "taom.long-defeat.scale"', checks)
        start = checks.index("internal static CheckVerdict Scale(")
        formatted = checks[start:checks.index("private static bool NearFactor")]
        for key in ("w=", "health=", "spawn=", "late=", "troop=", "in_mix=", "damage=", "amplified=", "steps=", "over_room="):
            self.assertIn(key + "{", formatted, key)
        self.assertRegex(self.SCALE_LINE, r"result=pass w=[0-9]+ health=[0-9.]+ spawn=[0-9.]+ late=[0-9.]+ troop=\S+ in_mix=(true|false) damage=[0-9.]+ amplified=[0-9]+ steps=[0-9]+ over_room=[0-9]+")

    def test_the_manifest_names_the_issue_and_the_taom_log(self):
        data = self.manifest()
        self.assertEqual(data["issue"], 1079)
        self.assertEqual(data["logs"], ["taom"])


class PatchTests(unittest.TestCase):

    def setUp(self):
        # ld.main arms the report fields it will hold the judge to; a later judge test must not inherit them.
        self.addCleanup(setattr, ld, "expected_report", {})

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
                         collect=lambda folder, bin_dir, done, started, pid, say: (None, {**REPORT, "startRerolls": 1}))
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


PINNED = [
    # (seed, stream, a, b, first three outputs): the same rows as RunRandomTests.cs, taken from an independent SplitMix64.
    (0, 0, 0, 0, [16294208416658607535, 7960286522194355700, 487617019471545679]),
    (1, 1, 5, 0, [2680490473348048908, 12813384503896764240, 3276828284648647372]),
    (1, 1, 10, 0, [7412692618947198593, 16684773309981356620, 15431304901105078789]),
    (1, 1, 15, 0, [16936612977461133540, 8028821510459182801, 4634075737854962681]),
    (1, 2, 0, 0, [17911839290282890590, 8196980753821780235, 8195237237126968761]),
    (1, 3, 0, 0, [7862637804313477842, 13015481187462834606, 15212506146343009075]),
    (9223372036854775807, 2, 7, 3, [6093248950766842696, 15789776932012360393, 7519895420366855887]),
    (12345, 1, 40, 0, [10993538888499654729, 6960065003137395634, 11458948633771559195]),
]


class SharedVectorTests(unittest.TestCase):
    """The bot's replay and the game's RunRandom must derive the same numbers: both suites pin the same rows."""

    def test_the_python_generator_gives_the_pinned_outputs(self):
        for seed, stream, a, b, expected in PINNED:
            gen = ld.SplitMix64(seed, stream, a, b)
            self.assertEqual(expected, [gen.next() for _ in expected], (seed, stream, a, b))

    def test_below_is_the_draw_modulo_n_and_zero_for_no_range(self):
        self.assertEqual(2680490473348048908 % 8, ld.SplitMix64(1, 1, 5, 0).below(8))
        gen = ld.SplitMix64(1, 1, 5, 0)
        self.assertEqual(0, gen.below(0))
        self.assertEqual(0, gen.below(-3))
        ref = ld.SplitMix64(1, 1, 5, 0)
        ref.next()
        ref.next()
        self.assertEqual(ref.next(), gen.next(), "a draw is consumed even when n gives 0")

    def test_every_pinned_row_is_also_pinned_in_the_csharp_test(self):
        csharp = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "Domain" / "RunRandomTests.cs").read_text(encoding="utf-8")
        for seed, stream, a, b, expected in PINNED[1:]:
            self.assertIn(f"[DataRow({seed}UL, {stream}, {a}, {b}, " + ", ".join(f"{n}UL" for n in expected) + ")]", csharp, (seed, stream, a, b))
        self.assertIn("16294208416658607535UL, 7960286522194355700UL, 487617019471545679UL", csharp)

    def test_the_constants_are_the_ones_the_game_uses(self):
        game = source("Domain", "RunRandom.cs")
        for name, value in (("Gamma", ld.SplitMix64.GAMMA), ("MixA", ld.SplitMix64.MIX_A), ("MixB", ld.SplitMix64.MIX_B)):
            self.assertIn(f"{name} = 0x{value:X}UL;", game, name)

    def test_the_seed_range_is_the_games(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        self.assertIn("seed <= RunOptions.MaxSeed", plan)
        self.assertIn("MaxSeed = (1UL << 53) - 1;", source("Domain", "RunOptions.cs"))
        self.assertEqual(2 ** 53 - 1, ld.MAX_SEED)


PINNED_ROLLS = [
    # (seed, wave, the roll at the shipped schedule): the same rows as AffixPlanTests.cs, taken from an independent implementation.
    (1, 5, ["discipline"]),
    (1, 10, ["discipline", "drums"]),
    (1, 15, ["lash", "eye", "hide"]),
    (12345, 40, ["hide", "discipline", "lash"]),
    (7, 5, ["discipline"]),
    (0, 15, ["drums", "hide", "discipline"]),
]


class AffixReplayTests(unittest.TestCase):
    """The bot replays the game's affix roll from the report's seed: both suites pin the same rows, so neither side can drift alone."""

    def test_the_python_roll_gives_the_pinned_powers(self):
        schedule = ld.shipped_affix_schedule()
        for seed, wave, expected in PINNED_ROLLS:
            self.assertEqual(expected, ld.affix_roll(seed, wave, ld.affix_count(schedule, wave)), (seed, wave))

    def test_every_pinned_roll_is_also_pinned_in_the_csharp_test(self):
        csharp = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "Domain" / "AffixPlanTests.cs").read_text(encoding="utf-8")
        for seed, wave, expected in PINNED_ROLLS:
            self.assertIn(f'[DataRow({seed}UL, {wave}, "{",".join(expected)}")]', csharp, (seed, wave))

    def test_the_power_ids_are_the_games_in_the_games_order(self):
        game = source("Domain", "AffixPlan.cs")
        found = re.search(r"Ids \{ get; \} = new\[\] \{ ([^}]*) \}", game)
        self.assertIsNotNone(found)
        names = {m.group(1): m.group(2) for m in re.finditer(r'public const string (\w+) = "(\w+)";', game)}
        self.assertEqual(list(ld.AFFIX_IDS), [names[part.strip()] for part in found.group(1).split(",")])

    def test_the_rolled_pool_is_the_games_and_holds_only_powers_with_an_effect(self):
        game = source("Domain", "AffixPlan.cs")
        found = re.search(r"Pool \{ get; \} = new\[\] \{ ([^}]*) \}", game)
        self.assertIsNotNone(found)
        names = {m.group(1): m.group(2) for m in re.finditer(r'public const string (\w+) = "(\w+)";', game)}
        self.assertEqual(list(ld.AFFIX_POOL), [names[part.strip()] for part in found.group(1).split(",")])
        self.assertEqual(["lash", "drums", "eye", "discipline", "hide"], list(ld.AFFIX_POOL), "Slices 8 and 9 add hammer, terror and blades here")
        self.assertEqual([i for i in ld.AFFIX_IDS if i in ld.AFFIX_POOL], list(ld.AFFIX_POOL), "the pool keeps the order of the eight")
        self.assertTrue(set(ld.AFFIX_EFFECT_IDS) <= set(ld.AFFIX_POOL), "an effect line is only owed for a power that can roll")

    def test_a_shorter_roll_is_a_prefix_of_a_longer_one(self):
        for seed in range(20):
            full = ld.affix_roll(seed, 20, 8)
            self.assertEqual(sorted(full), sorted(ld.AFFIX_POOL), "a roll of more than the pool holds is the whole pool")
            for count in range(len(ld.AFFIX_POOL) + 1):
                self.assertEqual(full[:count], ld.affix_roll(seed, 20, count), (seed, count))

    def test_no_seed_rolls_a_power_that_does_nothing_yet(self):
        for seed in range(200):
            for wave in (5, 10, 15, 40):
                self.assertFalse({"hammer", "terror", "blades"} & set(ld.affix_roll(seed, wave, 3)), (seed, wave))

    def test_the_schedule_is_the_shipped_files(self):
        self.assertEqual(((5, 1), (10, 2), (15, 3)), ld.shipped_affix_schedule())
        self.assertEqual(ld.AFFIX_DEFAULT_SCHEDULE, ld.shipped_affix_schedule())
        self.assertEqual([0, 0, 1, 1, 2, 2, 3, 3], [ld.affix_count(ld.shipped_affix_schedule(), w) for w in (1, 4, 5, 9, 10, 14, 15, 40)])

    def schedule_of(self, text, raw=None):
        """What shipped_affix_schedule reads from a scratch affixes.json holding this text (or these raw bytes)."""
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "affixes.json"
            path.write_bytes(raw if raw is not None else text.encode("utf-8"))
            with mock.patch.object(ld, "AFFIX_FILE", path):
                return ld.shipped_affix_schedule()

    def test_a_missing_file_or_one_that_is_not_a_json_object_rolls_nothing_as_the_game_reads_it(self):
        with mock.patch.object(ld, "AFFIX_FILE", Path(tempfile.gettempdir()) / "no-such-affixes.json"):
            self.assertEqual((), ld.shipped_affix_schedule())
        for text in ("", "not json", "[1, 2]", "5"):
            self.assertEqual((), self.schedule_of(text), text)

    def test_an_absent_or_malformed_schedule_is_the_shipped_default_as_the_game_reads_it(self):
        # Each row mirrors a branch of AffixesReader.ReadSchedule (and LongDefeatConfigScope.TryInt: a string, a float or a boolean is not a whole number).
        for text in ('{}', '{"schedule": null}', '{"schedule": "5"}', '{"schedule": {"fromWave": 5, "count": 1}}',
                     '{"schedule": [{"fromWave": 10, "count": 2}, {"fromWave": 5, "count": 1}]}',
                     '{"schedule": [{"fromWave": 5, "count": 1}, {"fromWave": 5, "count": 2}]}',
                     '{"schedule": [{"fromWave": "5", "count": 1}]}', '{"schedule": [{"fromWave": 5.7, "count": 1}]}',
                     '{"schedule": [{"fromWave": 5.0, "count": 1}]}', '{"schedule": [{"fromWave": true, "count": 1}]}',
                     '{"schedule": [{"fromWave": 5, "count": 9}]}', '{"schedule": [{"fromWave": 0, "count": 1}]}',
                     '{"schedule": [{"fromWave": 1001, "count": 1}]}', '{"schedule": [{"fromWave": 5}]}', '{"schedule": [5]}',
                     '{"schedule": [{"fromWave": Infinity, "count": 1}]}'):
            self.assertEqual(ld.AFFIX_DEFAULT_SCHEDULE, self.schedule_of(text), text)

    def test_an_empty_schedule_stays_empty_as_the_game_reads_it(self):
        self.assertEqual((), self.schedule_of('{"schedule": []}'))

    def test_a_good_schedule_is_read_row_for_row_and_a_bom_is_skipped(self):
        text = '{"schedule": [{"fromWave": 3, "count": 0}, {"fromWave": 7, "count": 2}]}'
        self.assertEqual(((3, 0), (7, 2)), self.schedule_of(text))
        self.assertEqual(((3, 0), (7, 2)), self.schedule_of(None, raw=b"\xef\xbb\xbf" + text.encode("utf-8")))

    def test_the_shipped_json_holds_the_eight_ids(self):
        shipped = json.loads(ld.AFFIX_FILE.read_text(encoding="utf-8"))
        self.assertEqual(sorted(ld.AFFIX_IDS), sorted(shipped["powers"]))


class AffixJudgeTests(unittest.TestCase):

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, cleared=5, rows=None, **more):
        return {**REPORT, "wavesCleared": cleared, "waves": [wave_row(w) for w in range(1, cleared + 1)] if rows is None else rows, **more}

    def test_a_run_that_cleared_five_waves_owes_the_affix_check(self):
        report = self.report(cleared=5)
        code, why = ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 5))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.affix", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0)

    def test_a_run_that_stopped_at_the_clear_of_wave_four_never_started_wave_five_and_owes_none(self):
        # The plan says "4 or more clears", which would fail a --waves 4 run: the check is written when a wave with affixes starts.
        ld.target_waves = 4
        self.assertEqual(ld.AFFIX_FROM_CLEARS, 5)
        self.assertEqual(ld.judge(FINAL, self.report(cleared=4), log_of(ld.REQUIRED_CHECKS, 4))[0], 0)

    def test_a_report_showing_a_wave_with_affixes_owes_the_check_whatever_it_cleared(self):
        # --start-wave 5 --waves 1: one clear, but the wave carried an affix.
        ld.target_waves = 1
        report = self.report(cleared=1, rows=[wave_row(5)], startWave=5)
        ld.expected_report = {"startWave": 5}
        code, why = ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 1))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.affix", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 1))[0], 0)

    def test_a_run_before_wave_five_owes_none(self):
        ld.target_waves = 1
        report = self.report(cleared=1, rows=[wave_row(2)])
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 1))[0], 0)

    def test_a_lose_run_is_not_asked_for_it(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(cleared=4), "final": False}
        self.assertEqual(ld.judge(lost, report, LOSE_LOG)[0], 0)

    def test_the_deep_run_lists_it_already(self):
        self.assertIn("affix", ld.DEEP_CHECKS)

    def test_affixes_that_replay_from_the_seed_pass(self):
        for seed in (0, 1, 7, 2 ** 53 - 1):
            ld.expected_report = {"seed": seed}
            report = self.report(cleared=15, seed=seed, rows=[wave_row(w, seed) for w in range(1, 16)])
            ld.target_waves = 15
            self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 15))[0], 0, seed)

    def test_affixes_that_do_not_replay_fail_naming_the_wave_and_the_roll(self):
        ld.target_waves = 5
        rows = [wave_row(w) for w in range(1, 6)]
        wrong = [{**row, "affixes": ["lash"]} if row["wave"] == 5 else row for row in rows]
        self.assertEqual(["discipline"], rows[4]["affixes"], "seed 7 rolls discipline at wave 5")
        code, why = ld.judge(FINAL, self.report(rows=wrong), FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("wave 5", why)
        self.assertIn("lash", why)
        self.assertIn("discipline", why)

    def test_a_wave_that_lost_its_affixes_or_gained_one_fails(self):
        rows = [wave_row(w) for w in range(1, 6)]
        for bad in ([], ["discipline", "lash"]):
            changed = rows[:4] + [{**rows[4], "affixes": bad}]
            self.assertEqual(ld.judge(FINAL, self.report(rows=changed), FULL_LOG)[0], 1, bad)
        early = rows[:1] + [{**rows[1], "affixes": ["discipline"]}] + rows[2:]
        self.assertEqual(ld.judge(FINAL, self.report(rows=early), FULL_LOG)[0], 1, "wave 2 carries none")

    def test_a_row_with_no_affixes_list_fails(self):
        rows = [wave_row(w) for w in range(1, 6)]
        rows[2] = {"wave": 3}
        code, why = ld.judge(FINAL, self.report(rows=rows), FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("no affixes list", why)

    def test_a_non_empty_boons_list_does_not_excuse_a_short_row(self):
        rows = [wave_row(w) for w in range(1, 6)]
        rows[4] = {**rows[4], "affixes": []}
        self.assertEqual(ld.judge(FINAL, self.report(rows=rows), FULL_LOG)[0], 1)
        self.assertEqual(ld.judge(FINAL, self.report(rows=rows, boons=[{"pick": 1}]), FULL_LOG)[0], 1)

    def test_a_cleared_wave_with_a_stat_power_or_hide_owes_an_affix_effect_line(self):
        without = log_of(ld.REQUIRED_CHECKS + ("affix",), 5)
        # The seeds that roll each power at wave 5 (the in-game proof's --seed N).
        seeds = {"discipline": 0, "hide": 2, "lash": 3, "eye": 8, "drums": 9}
        for power, seed in seeds.items():
            report = self.report(seed=seed, rows=[wave_row(w, seed) for w in range(1, 6)])
            self.assertEqual([power], report["waves"][4]["affixes"])
            code, why = ld.judge(FINAL, report, without)
            self.assertEqual(code, 1, power)
            self.assertIn("taom.long-defeat.affix-effect", why)
            self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0, power)
        self.assertEqual(sorted(seeds), sorted(ld.AFFIX_EFFECT_IDS))

    def test_the_effect_line_may_be_a_pass_or_a_skip_but_a_fail_fails_the_run(self):
        base = log_of(ld.REQUIRED_CHECKS + ("affix",), 5)
        line = "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result={} power=hide troop=x"
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("pass"))[0], 0)
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("skip"))[0], 0, "an unarmoured soldier cannot show hide: still read")
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("fail"))[0], 1)

    def test_a_row_with_no_effect_power_owes_no_effect_line(self):
        self.assertFalse(ld.reached_effect_affixes({"waves": [{"affixes": []}, "x", {"affixes": "hide"}]}))
        self.assertTrue(ld.reached_effect_affixes({"waves": [{"affixes": ["hammer", "eye"]}]}))

    def test_a_report_with_no_wave_rows_has_nothing_to_replay(self):
        self.assertIsNone(ld.affix_problem({"seed": 7}))
        self.assertIsNone(ld.affix_problem({"seed": 7, "waves": "x"}))
        self.assertIsNone(ld.affix_problem({"seed": True, "waves": []}))
        self.assertFalse(ld.reached_affixes({"waves": "x"}))
        self.assertFalse(ld.reached_affixes({"waves": [{"affixes": []}, "x", {"affixes": "hide"}]}))
        self.assertTrue(ld.reached_affixes({"waves": [{"affixes": ["hide"]}]}))


class PhaseTwoJudgeTests(unittest.TestCase):

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def test_a_schema_one_report_is_refused_and_says_so(self):
        for schema in (1, None, 3, "2", True):
            report = {**REPORT, "schema": schema}
            code, why = ld.judge(FINAL, report, FULL_LOG)
            self.assertEqual(code, 1, repr(schema))
            self.assertIn("schema", why)

    def test_a_schema_two_report_passes(self):
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)

    def test_an_armed_option_must_come_back_in_the_report(self):
        ld.expected_report = {"seed": 7, "startWave": 3, "startRerolls": 1}
        good = {**REPORT, "seed": 7, "startWave": 3, "startRerolls": 1}
        self.assertEqual(ld.judge(FINAL, good, FULL_LOG)[0], 0)
        for key in ("seed", "startWave", "startRerolls"):
            code, why = ld.judge(FINAL, {**good, key: 99}, FULL_LOG)
            self.assertEqual(code, 1, key)
            self.assertIn(key, why)
            self.assertEqual(ld.judge(FINAL, {k: v for k, v in good.items() if k != key}, FULL_LOG)[0], 1, key + " missing")

    def test_a_seed_of_zero_is_still_checked(self):
        ld.expected_report = {"seed": 0}
        self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": 5}, FULL_LOG)[0], 1)
        self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": 0}, FULL_LOG)[0], 0)

    def test_an_owner_missing_above_zero_fails_and_zero_passes(self):
        zero = FULL_LOG + "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result=pass power=hide owner_missing=0 withheld=2"
        self.assertEqual(ld.judge(FINAL, REPORT, zero)[0], 0)
        for count in ("1", "3", "12"):
            code, why = ld.judge(FINAL, REPORT, zero.replace("owner_missing=0", "owner_missing=" + count))
            self.assertEqual(code, 1, count)
            self.assertIn("owner_missing=" + count, why)

    def test_the_largest_owner_missing_in_the_log_is_the_one_reported(self):
        log = FULL_LOG + "\nx owner_missing=0\ny owner_missing=4\nz owner_missing=2"
        self.assertIn("owner_missing=4", ld.judge(FINAL, REPORT, log)[1])

    def test_the_cavalry_class_also_requires_sally_and_sally_return(self):
        report = {**REPORT, "class": "cavalry"}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)
        self.assertIn("taom.long-defeat.sally-return", why)
        full = log_of(STANDARD + ("sally", "sally-return"), 5)
        self.assertEqual(ld.judge(FINAL, report, full)[0], 0)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("sally",), 5))[0], 1)

    def test_another_class_asks_nothing_extra(self):
        for name in ("", "archer", "sword_and_board", None):
            self.assertEqual(ld.judge(FINAL, {**REPORT, "class": name}, FULL_LOG)[0], 0, repr(name))

    def test_a_run_that_played_past_the_authored_list_owes_the_scale_check(self):
        report = {**REPORT, "waves": [wave_row(10, healthFactor=1.0), wave_row(11, healthFactor=1.05)]}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.scale", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("scale",), 5))[0], 0)

    def test_a_damage_factor_alone_also_owes_the_scale_check(self):
        # The game's own rule (a wave is scaled when health, damage or the tier steps moved) also holds for a roster whose health line is flat.
        report = {**REPORT, "waves": [wave_row(11, healthFactor=1.0, damageFactor=1.03)]}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.scale", why)

    def test_a_run_inside_the_authored_list_asks_no_scale_check(self):
        for waves in (None, [], [wave_row(1, healthFactor=1.0)], [wave_row(1)], [wave_row(1, healthFactor=True)],
                      [wave_row(1, healthFactor="2")], ["x"], "waves"):
            self.assertEqual(ld.judge(FINAL, {**REPORT, "waves": waves}, FULL_LOG)[0], 0, repr(waves))

    def test_the_deep_run_lists_scale_once(self):
        report = {**REPORT, "waves": [wave_row(12, healthFactor=1.1)]}
        self.assertEqual(ld.judge({**FINAL, "expect": "deep"}, report, log_of(ld.DEEP_CHECKS + ("wave", "affix-effect"), 5))[0], 0)
        self.assertEqual(ld.DEEP_CHECKS.count("scale"), 1)

    def test_every_run_must_report_start_wave_one_unless_it_armed_another(self):
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)
        for bad in (2, 12, 0, None, "1", True):
            code, why = ld.judge(FINAL, {**REPORT, "startWave": bad}, FULL_LOG)
            self.assertEqual(code, 1, repr(bad))
            self.assertIn("startWave", why)
        self.assertEqual(ld.judge(FINAL, {k: v for k, v in REPORT.items() if k != "startWave"}, FULL_LOG)[0], 1)

    def test_an_armed_start_wave_replaces_the_default(self):
        ld.expected_report = {"startWave": 3}
        self.assertEqual(ld.judge(FINAL, {**REPORT, "startWave": 3}, FULL_LOG)[0], 0)
        self.assertEqual(ld.judge(FINAL, {**REPORT, "startWave": 1}, FULL_LOG)[0], 1)

    def test_the_seed_must_be_a_whole_number_within_the_cap(self):
        for good in (0, 7, 2 ** 53 - 1):
            self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": good}, FULL_LOG)[0], 0, good)
        for bad in (None, True, False, "7", 7.0, -1, 2 ** 53, 2 ** 63 - 1):
            code, why = ld.judge(FINAL, {**REPORT, "seed": bad}, FULL_LOG)
            self.assertEqual(code, 1, repr(bad))
            self.assertIn("seed", why)
        self.assertEqual(ld.judge(FINAL, {k: v for k, v in REPORT.items() if k != "seed"}, FULL_LOG)[0], 1)

    def test_the_cap_is_two_to_the_fifty_third_minus_one(self):
        self.assertEqual(9007199254740991, ld.MAX_SEED)

    def test_the_reports_poison_owner_missing_above_zero_fails_with_a_clean_log(self):
        for count in (1, 5):
            code, why = ld.judge(FINAL, {**REPORT, "poison": {"ownerMissing": count}}, FULL_LOG)
            self.assertEqual(code, 1, count)
            self.assertIn(f"owner_missing={count}", why)

    def test_a_report_with_no_poison_block_or_a_bad_owner_missing_fails(self):
        for poison in (None, {}, [], {"ownerMissing": None}, {"ownerMissing": True}, {"ownerMissing": "0"}, {"ownerMissing": 0.0}):
            report = {**REPORT, "poison": poison}
            self.assertEqual(ld.judge(FINAL, report, FULL_LOG)[0], 1, repr(poison))
        self.assertEqual(ld.judge(FINAL, {k: v for k, v in REPORT.items() if k != "poison"}, FULL_LOG)[0], 1)
        self.assertIn("poison.ownerMissing", ld.judge(FINAL, {**REPORT, "poison": {}}, FULL_LOG)[1])

    def test_zero_in_the_report_and_a_clean_log_passes(self):
        self.assertEqual(ld.judge(FINAL, {**REPORT, "poison": {"ownerMissing": 0}}, FULL_LOG)[0], 0)

    def test_the_worst_of_the_report_and_the_log_is_the_one_reported(self):
        log = FULL_LOG + "\nx owner_missing=3"
        code, why = ld.judge(FINAL, {**REPORT, "poison": {"ownerMissing": 6}}, log)
        self.assertEqual(code, 1)
        self.assertIn("owner_missing=6", why)
        self.assertIn("owner_missing=3", ld.judge(FINAL, REPORT, log)[1])


class DeepJudgeTests(unittest.TestCase):
    DEEP = {"reason": "final", "expect": "deep"}

    def setUp(self):
        ld.target_waves = 4
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)

    def test_the_deep_list_is_the_plans(self):
        self.assertEqual(ld.DEEP_CHECKS, ("boot", "general", "class", "commander", "affix", "scale", "end"))

    def test_a_final_deep_run_needs_the_deep_list_and_the_wave_lines_but_not_the_standard_list(self):
        log = log_of(ld.DEEP_CHECKS + ("wave",), 4)
        report = {**REPORT, "wavesCleared": 4}
        code, why = ld.judge(self.DEEP, report, log)
        self.assertEqual(code, 0, why)
        self.assertEqual(ld.judge(self.DEEP, report, log_of(ld.DEEP_CHECKS + ("wave",), 3))[0], 1, "four waves cleared, three wave lines")

    def test_a_lost_deep_run_passes_without_a_cleared_wave(self):
        log = log_of(ld.DEEP_CHECKS, 0)
        for reason in ld.LOSE_REASONS:
            code, why = ld.judge({"reason": reason, "expect": "deep"}, {**REPORT, "final": False, "wavesCleared": 1}, log)
            self.assertEqual(code, 0, why)

    def test_a_deep_run_that_stalled_or_faulted_fails(self):
        for reason in ("stalemate", "fault", "abandoned", "live_limit"):
            self.assertEqual(ld.judge({"reason": reason, "expect": "deep"}, {**REPORT, "final": False}, log_of(ld.DEEP_CHECKS, 0))[0], 1, reason)

    def test_each_deep_check_missing_fails_by_name(self):
        log = log_of(ld.DEEP_CHECKS + ("wave",), 4)
        for name in ld.DEEP_CHECKS:
            kept = "\n".join(row for row in log.splitlines() if f"long-defeat.{name} " not in row)
            code, why = ld.judge(self.DEEP, {**REPORT, "wavesCleared": 4}, kept)
            self.assertEqual(code, 1, name)
            self.assertIn(f"taom.long-defeat.{name}", why)

    def test_a_standard_run_still_rejects_a_lost_end(self):
        code, why = ld.judge({"reason": "lost_defenders"}, REPORT, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("final", why)

    def test_the_deep_ids_are_owned_by_a_checks_class_or_named_as_not_built_yet(self):
        # Slices 5 and 7 add these ids to LongDefeatChecks. Remove each name here the day its slice lands: the second assert fails then (scale landed in Slice 2, affix in Slice 3).
        not_built_yet = {"class", "commander"}
        checks = source("Diagnostics", "LongDefeatChecks.cs") + (REPO / "Client" / "Features" / "LongDefeat" / "LongDefeatClientChecks.cs").read_text(encoding="utf-8")
        for name in ld.DEEP_CHECKS:
            owned = f'"taom.long-defeat.{name}"' in checks
            self.assertEqual(name not in not_built_yet, owned, name)


class PhaseTwoArgumentTests(unittest.TestCase):

    def parse(self, *argv):
        return ld.build_parser().parse_known_args(list(argv))[0]

    def test_the_new_flags_default_to_a_board_shaped_run(self):
        known = self.parse()
        self.assertIsNone(known.klass)
        self.assertIsNone(known.seed)
        self.assertIsNone(known.start_wave)
        self.assertEqual(known.start_rerolls, 1, "the wrapper arms one reroll so a 5-wave run proves it at clear 3")
        self.assertFalse(known.deep)

    def test_the_seed_takes_zero_to_two_to_the_fifty_third_minus_one(self):
        self.assertEqual(self.parse("--seed", "0").seed, 0)
        self.assertEqual(self.parse("--seed", "9007199254740991").seed, 9007199254740991)
        for bad in ("-1", "9007199254740992", "9223372036854775807", "x", "1.5"):
            with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit, msg=bad):
                self.parse("--seed", bad)

    def test_start_wave_and_rerolls_are_range_checked(self):
        self.assertEqual(self.parse("--start-wave", "100").start_wave, 100)
        self.assertEqual(self.parse("--start-rerolls", "0").start_rerolls, 0)
        self.assertEqual(self.parse("--start-rerolls", "3").start_rerolls, 3)
        for flag, bad in (("--start-wave", "0"), ("--start-wave", "101"), ("--start-rerolls", "4"), ("--start-rerolls", "-1")):
            with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit, msg=flag + bad):
                self.parse(flag, bad)

    def test_the_class_is_one_of_the_four_or_all(self):
        for name in ld.CLASSES + ("all",):
            self.assertEqual(self.parse("--class", name).klass, name)
        with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit):
            self.parse("--class", "wizard")

    def test_deep_is_start_wave_twelve_and_four_waves_whatever_those_flags_said(self):
        known = ld.apply_deep(self.parse("--deep", "--waves", "9", "--start-wave", "3"))
        self.assertEqual((known.start_wave, known.waves), (12, 4))
        plain = ld.apply_deep(self.parse("--waves", "9"))
        self.assertEqual((plain.start_wave, plain.waves), (None, 9))

    def test_deep_and_lose_cannot_be_asked_together(self):
        with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit):
            self.parse("--deep", "--lose")

    def test_the_classes_are_the_plans_four(self):
        self.assertEqual(ld.CLASSES, ("sword_and_board", "two_handed", "archer", "cavalry"))

    def test_all_rotates_through_the_classes_one_per_run(self):
        self.assertEqual([ld.class_for_run("all", n) for n in range(6)],
                         ["sword_and_board", "two_handed", "archer", "cavalry", "sword_and_board", "two_handed"])
        self.assertEqual(ld.class_for_run("archer", 3), "archer")
        self.assertIsNone(ld.class_for_run(None, 0))


class PhaseTwoArmTests(unittest.TestCase):

    def captured_arm(self, *argv):
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        captured = {}
        with mock.patch.multiple(runner, main=lambda rest: captured.setdefault("arm", runner.arm_text_for), **keep):
            ld.main(list(argv))
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "expected_report", {})
        return captured["arm"]

    def test_a_default_run_arms_one_reroll_and_no_other_phase_two_key(self):
        text, _ = self.captured_arm()("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("start_rerolls=1\n", text)
        for key in ("seed=", "start_wave=", "class=", "expect="):
            self.assertNotIn(key, text)

    def test_the_flags_reach_the_arm_text(self):
        text, _ = self.captured_arm("--seed", "7", "--start-wave", "3", "--start-rerolls", "2", "--class", "archer", "--waves", "1")("E:/out", SimpleNamespace(wall_limit=None))
        for line in ("seed=7\n", "start_wave=3\n", "start_rerolls=2\n", "class=archer\n", "waves=1\n"):
            self.assertIn(line, text)

    def test_a_seed_of_zero_is_armed(self):
        text, _ = self.captured_arm("--seed", "0")("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("seed=0\n", text)

    def test_deep_arms_wave_twelve_four_waves_and_expect_deep(self):
        text, _ = self.captured_arm("--deep")("E:/out", SimpleNamespace(wall_limit=None))
        for line in ("start_wave=12\n", "waves=4\n", "expect=deep\n"):
            self.assertIn(line, text)
        self.assertEqual(ld.target_waves, 4)

    def test_class_all_gives_each_run_the_next_class(self):
        arm = self.captured_arm("--class", "all", "--runs", "4")
        classes = [re.search(r"^class=(\S+)$", arm("E:/out", SimpleNamespace(wall_limit=None))[0], re.M).group(1) for _ in range(5)]
        self.assertEqual(classes, ["sword_and_board", "two_handed", "archer", "cavalry", "sword_and_board"])

    def test_a_second_main_call_starts_the_rotation_again(self):
        first = self.captured_arm("--class", "all")("E:/out", SimpleNamespace(wall_limit=None))[0]
        second = self.captured_arm("--class", "all")("E:/out", SimpleNamespace(wall_limit=None))[0]
        self.assertIn("class=sword_and_board\n", first)
        self.assertIn("class=sword_and_board\n", second)

    def test_the_judge_is_held_to_the_armed_report_fields(self):
        self.captured_arm("--seed", "7", "--start-wave", "3")
        self.assertEqual(ld.expected_report, {"startRerolls": 1, "seed": 7, "startWave": 3})
        self.captured_arm()
        self.assertEqual(ld.expected_report, {"startRerolls": 1})

    def test_every_phase_two_arm_key_is_one_the_plan_reads(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        known = ld.build_parser().parse_known_args(["--seed", "1", "--start-wave", "3", "--deep"])[0]
        for key, _ in ld.arm_pairs(ld.apply_deep(known), "out", 3900, "archer"):
            self.assertIn(f'case "{key}":', plan, key)

    def test_the_report_keys_the_judge_reads_are_keys_the_report_writes(self):
        report = source("Diagnostics", "LongDefeatRunReport.cs")
        for key in ("schema", "seed", "startWave", "startRerolls", "class"):
            self.assertIn(f'["{key}"]', report, key)
        self.assertIn(f"Schema = {ld.SCHEMA};", report)

    def test_the_start_wave_and_reroll_ranges_are_the_games(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        self.assertIn("MaxStartWave = 100;", plan)
        self.assertIn("MaxStartRerolls = 3;", plan)


if __name__ == "__main__":
    unittest.main()
