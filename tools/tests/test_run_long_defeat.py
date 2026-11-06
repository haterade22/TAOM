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
# The two real-run fixtures predate the class check, so a test adds the line the game writes now (LongDefeatChecks.Class) to what it reads.
CLASS_LINE = ("[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.class result=pass class=archer mounted=false "
              "kit=Item0=ladder_rohan_bow_t3,Horse=,HorseHarness= has_mount=false wrong=none skipped=none")
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
# a power the game reads back owes an affix-effect line too. A run that cleared 3 waves was owed a pick, which the bot takes in the next intermission: it owes the
# boon check (ld.BOON_FROM_CLEARS). The reroll check is owed only when the report says a reroll was banked or spent. A run that cleared wave 4 met its first commander (ld.owes_commander).
BOONED = ld.REQUIRED_CHECKS + ("boon",)
STANDARD = BOONED + ("affix", "affix-effect", "commander")
FULL_LOG = log_of(STANDARD, 5)
LOSE_LOG = log_of(ld.LOSE_CHECKS, 0)
FINAL = {"reason": "final", "started": "2026-11-04T10:00:00"}
def wave_row(wave, seed=7, **more):
    """A wave row of a schema 2 report with the affixes the game would roll for it (the scheduled count of the shipped schedule, drawn from the seed) and the commander the
    roster configures for the wave, killed with no ability used (the summon counts are 0)."""
    spec = ld.shipped_commanders()
    expected = ld.commander_expected(spec, wave) if spec else None
    commander = None if expected is None else {"troop": expected[1], "rank": expected[0], "steps": expected[2], "outcome": "killed", "uses": {}, "usesAfterEnd": 0}
    return {"wave": wave, "affixes": ld.affix_roll(seed, wave, ld.affix_count(ld.shipped_affix_schedule(), wave)),
            "summoned": 0, "summonKills": 0, "summonFelled": 0, "commander": commander, **more}


# The class is the bot's draw for the report's seed, as the game takes it when the arm file names none (ld.bot_class, pinned to ClassPlanTests.cs).
REPORT = {"schema": 2, "seed": 7, "class": ld.bot_class(7), "startWave": 1, "poison": {"ownerMissing": 0}, "final": True, "wavesCleared": 5, "wavesFelled": 1}
BLOCK = "[2026-11-04 09:59:59] [ERROR] [CrashReport] swallowed during the mission load"


class JudgeTests(unittest.TestCase):

    def test_the_required_list_is_the_plans(self):
        # Update this tuple as each later slice grows the list: FULL_LOG and the loop below are built from REQUIRED_CHECKS itself.
        self.assertEqual(ld.REQUIRED_CHECKS, ("menu", "boot", "general", "muster", "keep", "class", "orders", "hud", "wave", "no-rout", "gold", "shop", "early-start", "end", "best-run"))

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
        self.assertEqual(ld.LOSE_CHECKS, ("menu", "boot", "general", "muster", "keep", "class", "orders", "hud", "end", "best-run"))

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
        rows = FIXTURE.read_text(encoding="utf-8").splitlines() + [CLASS_LINE] + list(extra)
        return [(None, number, row) for number, row in enumerate(rows, 1)]

    def test_the_real_run_passes_the_judge(self):
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8") + "\n" + CLASS_LINE
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
            if row.id in ("taom.long-defeat.wave", "taom.long-defeat.player-death", "taom.long-defeat.scale", "taom.long-defeat.affix", "taom.long-defeat.affix-effect",
                          "taom.long-defeat.boon", "taom.long-defeat.reroll", "taom.long-defeat.commander", "taom.long-defeat.summon"):
                self.assertEqual(result, iv.NOT_REACHED, row.id)
            else:
                self.assertEqual(result, iv.PASS, row.id)

    def test_an_injected_failure_flips_the_judge_and_its_row(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8") + "\n" + CLASS_LINE
        report = {**REPORT, "wavesCleared": 2}
        for row in manifest.checks:
            bad = f"[2026-11-04 04:00:00] [INFO] [Check] id={row.id} result=fail"
            self.assertEqual(ld.judge({"reason": "final"}, report, text + "\n" + bad)[0], 1, row.id)
            self.assertEqual(iv.judge_segment(row, self.fixture_lines([bad]), []).verdict, iv.FAIL, row.id)

    def test_the_lost_run_passes_the_lose_judge_and_its_rows(self):
        # Run 20261104-035343: three waves cleared, then the defenders died. The done file of a --lose run carries expect=lost.
        ld.tree_head = FIXTURE_HEAD
        self.addCleanup(setattr, ld, "tree_head", None)
        text = LOST_FIXTURE.read_text(encoding="utf-8") + "\n" + CLASS_LINE
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
    (1, 5, ["drums"]),
    (1, 10, ["discipline", "hammer"]),
    (1, 15, ["discipline", "eye", "lash"]),
    (12345, 40, ["hide", "hammer", "eye"]),
    (7, 5, ["hide"]),
    (0, 15, ["lash", "eye", "terror"]),
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
        self.assertEqual(["lash", "drums", "eye", "discipline", "hide", "hammer", "terror"], list(ld.AFFIX_POOL), "Slice 9 adds blades here")
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
                self.assertFalse({"blades"} & set(ld.affix_roll(seed, wave, 3)), (seed, wave))

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
        seed = more.get("seed", REPORT["seed"])
        return {**REPORT, "class": ld.bot_class(seed), "wavesCleared": cleared, "waves": [wave_row(w) for w in range(1, cleared + 1)] if rows is None else rows, **more}

    def test_a_run_that_cleared_five_waves_owes_the_affix_check(self):
        report = self.report(cleared=5)
        code, why = ld.judge(FINAL, report, log_of(BOONED, 5))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.affix", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0)

    def test_a_run_that_stopped_at_the_clear_of_wave_four_never_started_wave_five_and_owes_none(self):
        # The plan says "4 or more clears", which would fail a --waves 4 run: the check is written when a wave with affixes starts.
        ld.target_waves = 4
        self.assertEqual(ld.AFFIX_FROM_CLEARS, 5)
        self.assertEqual(ld.judge(FINAL, self.report(cleared=4), log_of(BOONED + ("commander",), 4))[0], 0, "wave 4 met its commander")

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
        self.assertEqual(["hide"], rows[4]["affixes"], "seed 7 rolls hide at wave 5")
        code, why = ld.judge(FINAL, self.report(rows=wrong), FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("wave 5", why)
        self.assertIn("lash", why)
        self.assertIn("hide", why)

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
        without = log_of(BOONED + ("affix",), 5)
        # The seeds that roll each power at wave 5 (the in-game proof's --seed N).
        seeds = {"hide": 0, "drums": 1, "discipline": 2, "eye": 4, "terror": 6, "hammer": 8, "lash": 27}
        for power, seed in seeds.items():
            report = self.report(seed=seed, rows=[wave_row(w, seed) for w in range(1, 6)])
            self.assertEqual([power], report["waves"][4]["affixes"])
            code, why = ld.judge(FINAL, report, without)
            self.assertEqual(code, 1, power)
            self.assertIn("taom.long-defeat.affix-effect", why)
            self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0, power)
        self.assertEqual(sorted(seeds), sorted(ld.AFFIX_EFFECT_IDS))

    def test_the_effect_line_may_be_a_pass_or_a_skip_but_a_fail_fails_the_run(self):
        base = log_of(BOONED + ("affix", "commander"), 5)
        line = "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result={} power=hide troop=x"
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("pass"))[0], 0)
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("skip"))[0], 0, "an unarmoured soldier cannot show hide: still read")
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("fail"))[0], 1)

    def test_a_row_with_no_effect_power_owes_no_effect_line(self):
        self.assertFalse(ld.reached_effect_affixes({"waves": [{"affixes": []}, "x", {"affixes": "hide"}]}))
        self.assertFalse(ld.reached_effect_affixes({"waves": [{"affixes": ["blades"]}]}), "blades has no effect line until Slice 9")
        self.assertTrue(ld.reached_effect_affixes({"waves": [{"affixes": ["blades", "eye"]}]}))
        self.assertTrue(ld.reached_effect_affixes({"waves": [{"affixes": ["hammer"]}]}), "the hammer proves its engine write under the same id")
        self.assertTrue(ld.reached_effect_affixes({"waves": [{"affixes": ["terror"]}]}), "terror proves its pulses under the same id")

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
        # One reroll banked and 5 waves cleared: the bot spent it at its first pick, so the reroll check is owed too.
        log = log_of(STANDARD + ("reroll",), 5)
        self.assertEqual(ld.judge(FINAL, good, log)[0], 0)
        for key in ("seed", "startWave", "startRerolls"):
            code, why = ld.judge(FINAL, {**good, key: 99}, log)
            self.assertEqual(code, 1, key)
            self.assertIn(key, why)
            self.assertEqual(ld.judge(FINAL, {k: v for k, v in good.items() if k != key}, log)[0], 1, key + " missing")

    def test_a_seed_of_zero_is_still_checked(self):
        ld.expected_report = {"seed": 0}
        self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": 5}, FULL_LOG)[0], 1)
        self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": 0, "class": ld.bot_class(0)}, FULL_LOG)[0], 0)

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
        # Cavalry is not in classes.json until Slices 10 and 11, so this plays the day it is: the class is on offer and armed.
        offer = mock.patch.object(ld, "class_offer", return_value=ld.CLASSES + ("cavalry",))
        offer.start()
        self.addCleanup(offer.stop)
        ld.expected_report = {"class": "cavalry"}
        report = {**REPORT, "class": "cavalry"}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)
        self.assertIn("taom.long-defeat.sally-return", why)
        full = log_of(STANDARD + ("sally", "sally-return"), 5)
        self.assertEqual(ld.judge(FINAL, report, full)[0], 0)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("sally",), 5))[0], 1)

    def test_another_class_asks_nothing_extra(self):
        for name in ld.CLASSES:
            ld.expected_report = {"class": name}
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
        self.assertEqual(ld.judge({**FINAL, "expect": "deep"}, report, log_of(ld.DEEP_CHECKS + ("wave", "affix-effect", "boon"), 5))[0], 0)
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
            self.assertEqual(ld.judge(FINAL, {**REPORT, "seed": good, "class": ld.bot_class(good)}, FULL_LOG)[0], 0, good)
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
        # Four clears owed a pick at the third, so the boon check is owed too (ld.BOON_FROM_CLEARS).
        log = log_of(ld.DEEP_CHECKS + ("wave", "boon"), 4)
        report = {**REPORT, "wavesCleared": 4}
        code, why = ld.judge(self.DEEP, report, log)
        self.assertEqual(code, 0, why)
        self.assertEqual(ld.judge(self.DEEP, report, log_of(ld.DEEP_CHECKS + ("wave", "boon"), 3))[0], 1, "four waves cleared, three wave lines")
        self.assertEqual(ld.judge(self.DEEP, report, log_of(ld.DEEP_CHECKS + ("wave",), 4))[0], 1, "a pick was owed and no boon check was written")

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
        # Remove each name here the day its slice lands: the second assert fails then (scale landed in Slice 2, affix in Slice 3, class in Slice 5, commander in Slice 7). Nothing is owed now.
        not_built_yet = set()
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

    def test_the_class_is_one_the_game_can_play_or_all(self):
        for name in ld.CLASSES + ("all",):
            self.assertEqual(self.parse("--class", name).klass, name)
        with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit):
            self.parse("--class", "wizard")

    def test_cavalry_is_refused_by_name_until_the_game_can_play_it(self):
        # Slices 10 and 11 add the class to classes.json; until then neither --class cavalry nor --class all may arm a class the game would silently replace.
        self.assertNotIn("cavalry", ld.CLASSES)
        stderr = io.StringIO()
        with mock.patch("sys.stderr", stderr), self.assertRaises(SystemExit):
            self.parse("--class", "cavalry")
        self.assertIn("cavalry", stderr.getvalue())
        self.assertIn("not playable yet", stderr.getvalue())

    def test_all_never_arms_cavalry(self):
        self.assertNotIn("cavalry", [ld.class_for_run("all", n) for n in range(12)])

    def test_deep_is_start_wave_twelve_and_four_waves_whatever_those_flags_said(self):
        known = ld.apply_deep(self.parse("--deep", "--waves", "9", "--start-wave", "3"))
        self.assertEqual((known.start_wave, known.waves), (12, 4))
        plain = ld.apply_deep(self.parse("--waves", "9"))
        self.assertEqual((plain.start_wave, plain.waves), (None, 9))

    def test_deep_and_lose_cannot_be_asked_together(self):
        with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit):
            self.parse("--deep", "--lose")

    def test_the_classes_are_read_from_the_shipped_classes_json(self):
        # Pinned to the file itself, read here without the wrapper's own reader: the foot classes of Slice 5, in the file's order.
        shipped = json.loads((REPO / "Main" / "_Module" / "ModuleData" / "long_defeat" / "classes.json").read_text(encoding="utf-8-sig"))
        self.assertEqual(ld.CLASSES, tuple(row["id"] for row in shipped["classes"]))
        self.assertEqual(ld.CLASSES, ("sword_and_board", "two_handed", "archer"))

    def test_every_shipped_class_has_a_kit_for_the_helms_defender_culture(self):
        shipped = json.loads((REPO / "Main" / "_Module" / "ModuleData" / "long_defeat" / "classes.json").read_text(encoding="utf-8-sig"))
        fortress = json.loads((REPO / "Main" / "_Module" / "ModuleData" / "long_defeat" / "fortresses.json").read_text(encoding="utf-8-sig"))
        culture = next(f["defenderCulture"] for f in fortress["fortresses"] if f["id"] == "helms")
        self.assertEqual(ld.class_offer("helms"), tuple(row["id"] for row in shipped["classes"] if culture in row["kits"]))
        self.assertEqual(ld.class_offer("helms"), ld.CLASSES)

    def test_the_offer_keeps_only_the_classes_with_a_kit_for_the_fortresses_defender_culture(self):
        with tempfile.TemporaryDirectory() as folder:
            classes, fortresses = Path(folder) / "classes.json", Path(folder) / "fortresses.json"
            classes.write_text(json.dumps({"classes": [{"id": "a", "kits": {"Vlandia": {}}}, {"id": "b", "kits": {"rohan": {}}}]}), encoding="utf-8")
            fortresses.write_text(json.dumps({"fortresses": [{"id": "helms", "defenderCulture": "vlandia"}, {"id": "elsewhere", "defenderCulture": "gondor"}]}), encoding="utf-8")
            with mock.patch.object(ld, "CLASS_FILE", classes), mock.patch.object(ld, "FORTRESS_FILE", fortresses):
                self.assertEqual(ld.class_offer("helms"), ("a",), "the culture matches without case, on both sides")
                self.assertEqual(ld.class_offer("elsewhere"), ())
                self.assertEqual(ld.class_offer("not_a_fortress"), ())
            fortresses.write_text(json.dumps({"fortresses": []}), encoding="utf-8")
            with mock.patch.object(ld, "CLASS_FILE", classes), mock.patch.object(ld, "FORTRESS_FILE", fortresses):
                self.assertEqual(ld.class_offer("helms"), ())

    def test_a_class_file_that_is_missing_or_unreadable_offers_nothing(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "classes.json"
            self.assertEqual(ld.shipped_classes(path), [])
            path.write_text("not json", encoding="utf-8")
            self.assertEqual(ld.shipped_classes(path), [])
            path.write_text('{"classes": [{"id": "a", "kits": {"vlandia": {}}}, {"id": 5, "kits": {}}, {"kits": {}}, "x", {"id": "b", "kits": []}]}', encoding="utf-8")
            self.assertEqual(ld.shipped_classes(path), [("a", {"vlandia"})])

    def test_all_rotates_through_the_classes_one_per_run(self):
        self.assertEqual([ld.class_for_run("all", n) for n in range(6)],
                         ["sword_and_board", "two_handed", "archer", "sword_and_board", "two_handed", "archer"])
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
        self.assertEqual(classes, ["sword_and_board", "two_handed", "archer", "sword_and_board", "two_handed"])

    def test_the_judge_expects_the_class_each_run_armed_run_by_run(self):
        arm = self.captured_arm("--class", "all", "--runs", "4")
        for expected in ("sword_and_board", "two_handed", "archer", "sword_and_board"):
            arm("E:/out", SimpleNamespace(wall_limit=None))
            self.assertEqual(ld.expected_report.get("class"), expected)
            self.assertEqual(ld.expected_report.get("startRerolls"), 1, "the other armed fields stay")

    def test_a_fixed_class_is_expected_back_and_none_leaves_the_bot_to_the_seed(self):
        self.captured_arm("--class", "archer")("E:/out", SimpleNamespace(wall_limit=None))
        self.assertEqual(ld.expected_report["class"], "archer")
        self.captured_arm()("E:/out", SimpleNamespace(wall_limit=None))
        self.assertNotIn("class", ld.expected_report)

    def test_a_second_main_call_does_not_carry_the_last_runs_class(self):
        self.captured_arm("--class", "archer")("E:/out", SimpleNamespace(wall_limit=None))
        self.captured_arm("--seed", "3")
        self.assertNotIn("class", ld.expected_report)

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


class ClassJudgeTests(unittest.TestCase):
    """The class of a run (phase2-plan.md Slice 5): the report names the class the arm file asked for, or the one the bot draws from the seed, and the class check passes."""

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def test_the_bots_class_is_pinned_to_the_games_vectors(self):
        # The same seeds and answers as ClassPlanTests.Choose_TheBotsPick_IsPinned: stream 3 at (0, 0), Below(3) over the classes in classes.json order.
        got = [ld.bot_class(seed) for seed in range(8)]
        self.assertEqual(got, ["two_handed", "sword_and_board", "archer", "archer", "archer", "sword_and_board", "two_handed", "two_handed"])
        self.assertEqual([ld.SplitMix64(seed, 3, 0, 0).below(3) for seed in range(8)], [1, 0, 2, 2, 2, 0, 1, 1])

    def test_the_bots_class_reads_the_offer_of_the_reports_fortress(self):
        with mock.patch.object(ld, "class_offer", return_value=("a", "b")) as offer:
            self.assertEqual(ld.bot_class(0, "elsewhere"), "a")
        offer.assert_called_once_with("elsewhere")

    def test_no_class_on_offer_gives_no_class(self):
        with mock.patch.object(ld, "class_offer", return_value=()):
            self.assertIsNone(ld.bot_class(7))
            self.assertIsNone(ld.class_problem({"seed": 7, "class": ""}))
            self.assertIn("offers none", ld.class_problem({"seed": 7, "class": "archer"}))

    def test_an_unarmed_run_must_report_the_class_the_seed_draws(self):
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)
        wrong = next(c for c in ld.CLASSES if c != REPORT["class"])
        code, why = ld.judge(FINAL, {**REPORT, "class": wrong}, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn(REPORT["class"], why)
        self.assertIn("seed 7", why)

    def test_an_armed_class_replaces_the_seeds_draw(self):
        other = next(c for c in ld.CLASSES if c != REPORT["class"])
        ld.expected_report = {"class": other}
        self.assertEqual(ld.judge(FINAL, {**REPORT, "class": other}, FULL_LOG)[0], 0)
        code, why = ld.judge(FINAL, REPORT, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("class", why)
        self.assertIn("never took effect", why)

    def test_a_class_the_game_does_not_offer_fails_even_when_it_was_armed(self):
        # --class cavalry before it ships: the game falls back, so the report names a shipped class and the armed one is not back.
        ld.expected_report = {"class": "cavalry"}
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 1)
        ld.expected_report = {"class": "cavalry"}
        code, why = ld.judge(FINAL, {**REPORT, "class": "cavalry"}, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("not one of the classes", why)

    def test_a_report_with_no_class_fails_when_classes_are_on_offer(self):
        for bad in (None, "", 5, True):
            code, why = ld.judge(FINAL, {**REPORT, "class": bad}, FULL_LOG)
            self.assertEqual(code, 1, repr(bad))
            self.assertIn("class", why)
        self.assertEqual(ld.judge(FINAL, {k: v for k, v in REPORT.items() if k != "class"}, FULL_LOG)[0], 1)

    def test_the_class_check_is_required_of_a_standard_a_lose_and_a_deep_run(self):
        self.assertIn("class", ld.REQUIRED_CHECKS)
        self.assertIn("class", ld.LOSE_CHECKS)
        self.assertIn("class", ld.DEEP_CHECKS)
        without = "\n".join(row for row in FULL_LOG.splitlines() if "long-defeat.class " not in row)
        code, why = ld.judge(FINAL, REPORT, without)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.class", why)

    def test_a_lose_run_also_owes_the_class_line_and_the_class_match(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**REPORT, "final": False, "wavesCleared": 0}
        self.assertEqual(ld.judge(lost, report, LOSE_LOG)[0], 0)
        without = "\n".join(row for row in LOSE_LOG.splitlines() if "long-defeat.class " not in row)
        self.assertEqual(ld.judge(lost, report, without)[0], 1)
        wrong = next(c for c in ld.CLASSES if c != REPORT["class"])
        self.assertEqual(ld.judge(lost, {**report, "class": wrong}, LOSE_LOG)[0], 1)

    def test_a_failed_class_line_fails_the_run(self):
        bad = FULL_LOG + "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.class result=fail class=archer wrong=Item0:bow>sword"
        self.assertEqual(ld.judge(FINAL, REPORT, bad)[0], 1)

    def test_every_class_check_text_the_judge_reads_is_text_the_game_writes(self):
        checks = source("Diagnostics", "LongDefeatChecks.cs")
        self.assertIn('"taom.long-defeat.class"', checks)
        self.assertIn("class=", checks)
        self.assertIn("wrong=", checks)

    def test_the_class_line_is_the_one_the_csharp_check_formats(self):
        checks = source("Diagnostics", "LongDefeatChecks.cs")
        start = checks.index("internal static CheckVerdict Class(")
        formatted = checks[start:checks.index("private static bool NearFactor", start)]
        # The manifest's pass pattern needs the class, then the mount flag, then a space; the game's head writes them in that order.
        self.assertIn('class={wanted.ClassId} mounted={Flag(wanted.Mounted)} kit=', formatted)
        for key in ("has_mount=", "wrong=", "skipped="):
            self.assertIn(key, formatted, key)


class ClassManifestTests(unittest.TestCase):

    def manifest(self):
        return tomllib.loads((REPO / "tools" / "ingame_checks" / "1079.toml").read_text(encoding="utf-8"))

    def test_the_class_row_is_here_with_its_pass_pattern_and_fails_when_absent_after_boot(self):
        row = next(r for r in self.manifest()["check"] if r["id"] == "taom.long-defeat.class")
        self.assertEqual(row["absence"], "fail")
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        check = next(c for c in manifest.checks if c.id == "taom.long-defeat.class")
        lines = [(None, 1, CLASS_LINE)]
        self.assertEqual(iv.judge_segment(check, lines, []).verdict, iv.PASS)
        failed = [(None, 1, CLASS_LINE.replace("result=pass", "result=fail"))]
        self.assertEqual(iv.judge_segment(check, failed, []).verdict, iv.FAIL)

    def test_a_booted_run_that_read_classes_json_but_has_no_class_line_reads_fail(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        check = next(c for c in manifest.checks if c.id == "taom.long-defeat.class")
        boot = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.boot result=pass deployment_finished=true siege_ai=true main=true friendly=false"
        read = "[2026-11-04 09:59:00] [INFO] LongDefeatConfigCatalog: classes.json: loaded, 3 class(es)"
        self.assertEqual(iv.judge_segment(check, [(None, 1, read), (None, 2, boot)], []).verdict, iv.FAIL)
        self.assertEqual(iv.judge_segment(check, [], []).verdict, iv.NOT_REACHED)

    def test_a_booted_run_from_a_build_that_never_read_classes_json_reads_not_reached(self):
        # Every build from before Slice 5 binds to this row through fixed_in and writes no class line; that is not a failure of the class check.
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        check = next(c for c in manifest.checks if c.id == "taom.long-defeat.class")
        boot = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.boot result=pass deployment_finished=true siege_ai=true main=true friendly=false"
        self.assertEqual(iv.judge_segment(check, [(None, 1, boot)], []).verdict, iv.NOT_REACHED)

    def test_the_catalog_line_the_row_waits_for_is_a_line_every_read_of_classes_json_writes(self):
        scope = source("Config", "LongDefeatConfigScope.cs")
        catalog = source("Config", "LongDefeatConfigCatalog.cs")
        self.assertIn('Tag = "LongDefeatConfigCatalog"', scope)
        self.assertIn('ClassesFile = "classes.json"', catalog)
        self.assertIn("$\"{Tag}: {FileName}: ", scope)
        row = next(r for r in self.manifest()["check"] if r["id"] == "taom.long-defeat.class")
        self.assertIn(r"LongDefeatConfigCatalog: classes\.json: ", row["reached"])

    def test_the_manifests_class_pattern_takes_any_class_id_the_file_may_hold(self):
        row = next(r for r in self.manifest()["check"] if r["id"] == "taom.long-defeat.class")
        pattern = re.compile(row["pass"][0])
        for cid in list(ld.CLASSES) + ["two-handed", "archer2", "Cavalry_1"]:
            self.assertTrue(pattern.search(CLASS_LINE.replace("class=archer", "class=" + cid)), cid)

    def test_the_human_rows_for_the_kit_and_the_second_question_are_here(self):
        claims = [h["claim"] for h in self.manifest()["human"]]
        self.assertTrue(any("class kit" in c for c in claims), claims)
        self.assertTrue(any("second question" in c for c in claims), claims)


# The pinned boon draws over the shipped boons.json (thirteen free rows since Slice 7 added the third Legend row): seed 1, pick 1 draws 0, 1 and 2 (each reroll leaves out the three just shown), and
# pick 2 after taking the first card of draw 1. The same rows are pinned in LongDefeatShippedConfigTests.cs, so the bot's replay cannot drift from
# the game. They move when a later slice adds rows to boons.json, and both suites move with them.
BOON_PINNED = (
    (1, 1, 0, ["haggler_of_edoras", "stout_hearts_at_the_gate", "the_long_watch"]),
    (1, 1, 1, ["hornblowers_bargain", "riddermark_levy", "wains_of_the_westfold"]),
    (1, 1, 2, ["the_long_watch", "healers_of_the_hold", "granary_stores"]),
)
BOON_PINNED_SECOND = (1, 2, 0, ["haggler_of_edoras", "granary_stores", "healers_of_the_hold"])


def boon_pool(owned=(), class_id="", groups=()):
    """The rows a draw may use: the free categories, the class rows of `class_id`, and the troop category rows of `groups`, less `owned` (written out here, apart from ld.boon_eligible)."""
    rows, weight = ld.shipped_boons()
    return [r for r in rows if r["id"] not in owned
            and (r["category"] in ld.BOON_FREE_CATEGORIES or (r["category"] == "class" and r["class"] == class_id) or r["category"] in groups)], weight


def boon_picks(seed=1, rerolls=1, take=None, class_id="", groups=()):
    """The boons[] a game would report for one pick drawn from this seed with `rerolls` rerolls, taking `take` (a card of the last draw) or the first."""
    pool, weight = boon_pool(class_id=class_id, groups=groups)
    draws, shown = [], []
    for draw in range(rerolls + 1):
        shown = ld.boon_cards(seed, 1, draw, pool, weight, shown)
        draws.append(shown)
    return [{"pick": 1, "wave": 3, "offered": draws, "taken": take or draws[-1][0], "takenBeforeWave": 4}]


class BoonReplayTests(unittest.TestCase):

    def test_the_python_draw_gives_the_pinned_cards(self):
        pool, weight = boon_pool()
        shown = []
        for seed, pick, draw, cards in BOON_PINNED:
            shown = ld.boon_cards(seed, pick, draw, pool, weight, shown)
            self.assertEqual(shown, cards, (seed, pick, draw))
        taken = BOON_PINNED[1][3][0]
        pool, weight = boon_pool(owned=(taken,))
        seed, pick, draw, cards = BOON_PINNED_SECOND
        self.assertEqual(ld.boon_cards(seed, pick, draw, pool, weight, []), cards)

    def test_every_pinned_draw_is_also_pinned_in_the_csharp_test(self):
        text = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "Config" / "LongDefeatShippedConfigTests.cs").read_text(encoding="utf-8")
        for _, _, _, cards in BOON_PINNED + (BOON_PINNED_SECOND,):
            self.assertIn("new[] { " + ", ".join(f'"{c}"' for c in cards) + " }", text, cards)

    def test_the_draw_rules_are_the_games(self):
        deck = source("Domain", "BoonDeck.cs")
        self.assertIn("OfferStream", deck)
        self.assertEqual(2, int(re.search(r"OfferStream = (\d+)", source("Domain", "RunRandom.cs")).group(1)))
        for name, value in (("PickEvery", ld.BOON_FROM_CLEARS), ("RerollEvery", ld.BOON_REROLL_EVERY), ("MaxBank", ld.BOON_MAX_BANK), ("CardCount", ld.BOON_CARDS)):
            self.assertEqual(value, int(re.search(rf"{name} = (\d+)", deck).group(1)), name)
        self.assertEqual(ld.BOON_ROW_WEIGHT, int(re.search(r"RowWeight = (\d+)", source("Domain", "BoonPlan.cs")).group(1)))

    def test_a_reroll_leaves_out_the_cards_just_shown_unless_fewer_than_three_others_remain(self):
        pool = [{"id": c, "category": "warchest", "class": "", "cut": 0} for c in "abcd"]
        first = ld.boon_cards(1, 1, 0, pool, 1, [])
        again = ld.boon_cards(1, 1, 1, pool, 1, first)
        self.assertEqual(first, ["a", "d", "c"])
        self.assertEqual(again, ["d", "c", "a"], "one other row is fewer than three, so nothing is left out")
        wide = [{"id": c, "category": "warchest", "class": "", "cut": 0} for c in "abcdefgh"]
        shown = ld.boon_cards(1, 1, 0, wide, 1, [])
        self.assertTrue(set(shown).isdisjoint(ld.boon_cards(1, 1, 1, wide, 1, shown)))

    def test_a_legend_row_weighs_its_legend_weight(self):
        pool = [{"id": "a", "category": "warchest", "class": "", "cut": 0}, {"id": "L", "category": "legend", "class": "", "cut": -1}]
        heavy = sum("L" in ld.boon_cards(seed, 1, 0, pool, 90, [])[:1] for seed in range(200))
        light = sum("L" in ld.boon_cards(seed, 1, 0, pool, 1, [])[:1] for seed in range(200))
        self.assertGreater(heavy, 120)
        self.assertLess(light, 60)

    def test_the_shipped_file_reads_row_for_row_with_its_cuts(self):
        rows, weight = ld.shipped_boons()
        self.assertEqual(weight, 1)
        self.assertEqual([r["id"] for r in rows], [b["id"] for b in json.loads(ld.BOON_FILE.read_text(encoding="utf-8"))["boons"]])
        self.assertEqual({r["id"]: r["cut"] for r in rows if r["cut"]}, {"light_of_the_phial": -1, "riders_at_dawn": -2})

    def test_a_missing_or_malformed_file_has_no_rows(self):
        for text in (None, "[]", "{not json"):
            with tempfile.TemporaryDirectory() as folder:
                path = Path(folder) / "boons.json"
                if text is not None:
                    path.write_text(text, encoding="utf-8")
                self.assertEqual(ld.shipped_boons(path), ([], 1))

    def test_the_shipped_file_holds_class_and_troop_rows_for_the_replay_to_draw(self):
        rows, _ = ld.shipped_boons()
        for category in ld.BOON_TROOP_CATEGORIES:
            self.assertGreaterEqual(sum(r["category"] == category for r in rows), 3, category)
        for cid in ld.CLASSES:
            self.assertGreaterEqual(sum(r["category"] == "class" and r["class"] == cid for r in rows), 3, cid)

    def test_a_replayed_pick_with_a_reroll_passes(self):
        report = {"seed": 1, "wavesCleared": 5, "startRerolls": 1, "boons": boon_picks(1, rerolls=1), "rerolls": {"earned": 2, "used": 1, "banked": 1}}
        self.assertIsNone(ld.boon_problem(report))

    def test_a_card_that_the_seed_does_not_draw_fails_naming_the_draw(self):
        picks = boon_picks(1, rerolls=1)
        picks[0]["offered"][1] = ["riddermark_levy", "the_long_watch", "haggler_of_edoras"]
        report = {"seed": 1, "wavesCleared": 5, "boons": picks, "rerolls": {"earned": 2, "used": 1, "banked": 1}}
        problem = ld.boon_problem(report)
        self.assertIn("pick 1 draw 1", problem)
        self.assertIn("hornblowers_bargain", problem)

    def test_a_taken_card_that_was_never_shown_fails(self):
        picks = boon_picks(1, rerolls=0)
        picks[0]["taken"] = "riders_at_dawn"
        problem = ld.boon_problem({"seed": 1, "wavesCleared": 3, "boons": picks, "rerolls": {"earned": 0, "used": 0, "banked": 0}})
        self.assertIn("did not show", problem)

    def test_a_pick_with_no_takenBeforeWave_or_out_of_order_fails(self):
        picks = boon_picks(1, rerolls=0)
        base = {"seed": 1, "wavesCleared": 3, "rerolls": {"earned": 0, "used": 0, "banked": 0}}
        self.assertIn("takenBeforeWave", ld.boon_problem({**base, "boons": [{**picks[0], "takenBeforeWave": None}]}))
        self.assertIn("pick 1", ld.boon_problem({**base, "boons": [{**picks[0], "pick": 2}]}))

    def test_a_second_pick_is_drawn_over_the_boons_taken_before_it(self):
        pool, weight = boon_pool()
        first = boon_picks(1, rerolls=0)
        taken = first[0]["taken"]
        pool2, _ = boon_pool(owned=(taken,))
        second = {"pick": 2, "wave": 6, "offered": [ld.boon_cards(1, 2, 0, pool2, weight, [])], "taken": None, "takenBeforeWave": 0}
        report = {"seed": 1, "wavesCleared": 6, "boons": first + [second], "rerolls": {"earned": 1, "used": 0, "banked": 1}}
        self.assertIsNone(ld.boon_problem(report))
        second["offered"] = [ld.boon_cards(1, 2, 0, pool, weight, [])]
        self.assertIn("pick 2 draw 0", ld.boon_problem(report) or "")

    def test_only_the_last_pick_may_be_left_open(self):
        first = {**boon_picks(1, rerolls=0)[0], "taken": None, "takenBeforeWave": 0}
        problem = ld.boon_problem({"seed": 1, "wavesCleared": 6, "boons": [first, {**first, "pick": 2}], "rerolls": {"earned": 0, "used": 0, "banked": 0}})
        self.assertIn("took nothing", problem)

    def test_a_lapsed_pick_is_only_honest_when_no_row_was_eligible(self):
        lapsed = {"pick": 1, "wave": 3, "offered": [], "taken": None, "takenBeforeWave": 0}
        problem = ld.boon_problem({"seed": 1, "wavesCleared": 3, "boons": [lapsed], "rerolls": {"earned": 0, "used": 0, "banked": 0}})
        self.assertIn("lapsed", problem)
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "boons.json"
            path.write_text('{"boons": []}', encoding="utf-8")
            self.assertIsNone(ld.boon_problem({"seed": 1, "wavesCleared": 3, "boons": [lapsed], "rerolls": {"earned": 0, "used": 0, "banked": 0}}, path))

    def test_two_lapsed_picks_in_a_row_replay_when_no_row_was_eligible(self):
        lapsed = {"pick": 1, "wave": 3, "offered": [], "taken": None, "takenBeforeWave": 0}
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "boons.json"
            path.write_text('{"boons": []}', encoding="utf-8")
            report = {"seed": 1, "wavesCleared": 6, "boons": [lapsed, {**lapsed, "pick": 2, "wave": 6}], "rerolls": {"earned": 1, "used": 0, "banked": 1}}
            self.assertIsNone(ld.boon_problem(report, path))

    def test_more_picks_than_the_clears_owe_fails(self):
        problem = ld.boon_problem({"seed": 1, "wavesCleared": 2, "boons": boon_picks(1, rerolls=0), "rerolls": {"earned": 0, "used": 0, "banked": 0}})
        self.assertIn("owe 0", problem)

    def test_a_report_that_lists_fewer_picks_than_its_clears_owe_fails(self):
        books = {"earned": 1, "used": 0, "banked": 1}
        self.assertIn("owe 1", ld.boon_problem({"seed": 1, "wavesCleared": 5, "boons": [], "rerolls": books}))
        first = boon_picks(1, rerolls=0)
        self.assertIn("owe 2", ld.boon_problem({"seed": 1, "wavesCleared": 6, "boons": first, "rerolls": {"earned": 1, "used": 0, "banked": 1}}))

    def test_fewer_picks_than_the_clears_owe_is_honest_while_the_last_pick_is_still_open(self):
        open_first = [{**boon_picks(1, rerolls=0)[0], "taken": None, "takenBeforeWave": 0}]
        books = {"earned": 1, "used": 0, "banked": 1}
        self.assertIsNone(ld.boon_problem({"seed": 1, "wavesCleared": 6, "boons": open_first, "rerolls": books}), "pick 2 waits behind the open pick 1")

    def test_earned_below_the_start_bank_fails(self):
        picks = boon_picks(1, rerolls=0)
        problem = ld.boon_problem({"seed": 1, "wavesCleared": 3, "startRerolls": 2, "boons": picks, "rerolls": {"earned": 1, "used": 0, "banked": 1}})
        self.assertIn("less than the start bank", problem)

    def test_the_reroll_books_must_add_up(self):
        picks = boon_picks(1, rerolls=1)
        base = {"seed": 1, "wavesCleared": 5, "startRerolls": 1, "boons": picks}
        self.assertIn("rerolls.used", ld.boon_problem({**base, "rerolls": {"earned": 2, "used": 0, "banked": 2}}))
        self.assertIn("not the bank", ld.boon_problem({**base, "rerolls": {"earned": 2, "used": 1, "banked": 0}}))
        self.assertIn("outside 0 to 3", ld.boon_problem({**base, "rerolls": {"earned": 5, "used": 1, "banked": 4}}))
        self.assertIn("more than a start bank", ld.boon_problem({**base, "startRerolls": 0, "rerolls": {"earned": 3, "used": 1, "banked": 2}}))
        self.assertIn("rerolls block", ld.boon_problem({**base, "rerolls": None}))

    def test_a_report_with_no_boons_list_or_seed_has_nothing_to_replay(self):
        self.assertIsNone(ld.boon_problem({"seed": 1}))
        self.assertIsNone(ld.boon_problem({"boons": []}))
        self.assertIsNone(ld.boon_problem({"seed": True, "boons": []}))

    def test_a_class_row_is_eligible_for_its_own_class_only(self):
        rows = [{"id": "w", "category": "warchest", "class": "", "cut": 0}, {"id": "a", "category": "class", "class": "archer", "cut": 0}]
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "boons.json"
            path.write_text(json.dumps({"boons": [{"id": r["id"], "category": r["category"], "class": r["class"]} for r in rows]}), encoding="utf-8")
            archer = {"seed": 1, "class": "archer", "wavesCleared": 3, "rerolls": {"earned": 0, "used": 0, "banked": 0}}
            both = [ld.boon_cards(1, 1, 0, rows, 1, [])]
            only = [ld.boon_cards(1, 1, 0, rows[:1], 1, [])]
            self.assertIsNone(ld.boon_problem({**archer, "boons": [{"pick": 1, "offered": both, "taken": both[0][0], "takenBeforeWave": 4}]}, path))
            self.assertIsNone(ld.boon_problem({**archer, "class": "", "boons": [{"pick": 1, "offered": only, "taken": "w", "takenBeforeWave": 4}]}, path))


class BoonJudgeTests(unittest.TestCase):

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, picks=None, cleared=5, **more):
        picks = boon_picks(1, rerolls=1, class_id=ld.bot_class(1)) if picks is None else picks
        used = sum(max(0, len(p["offered"]) - 1) for p in picks)
        return {**REPORT, "seed": 1, "class": ld.bot_class(1), "wavesCleared": cleared, "startRerolls": 1, "boons": picks, "rerolls": {"earned": 1 + cleared // 5, "used": used, "banked": 1 + cleared // 5 - used},
                "waves": [wave_row(w, 1) for w in range(1, cleared + 1)], **more}

    def test_a_run_that_cleared_four_waves_owes_the_boon_check(self):
        ld.target_waves = 4
        report = self.report(cleared=4)
        code, why = ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS + ("reroll",), 4))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.boon", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS + ("boon", "reroll", "commander"), 4))[0], 0)

    def test_a_run_that_stops_at_the_third_clear_never_sees_the_intermission_and_owes_neither(self):
        # --waves 3: the pick opens at the clear that ends the run, so the bot has no intermission to take it in.
        ld.target_waves = 3
        report = self.report(picks=[{"pick": 1, "wave": 3, "offered": [boon_picks(1, rerolls=0, class_id=ld.bot_class(1))[0]["offered"][0]], "taken": None, "takenBeforeWave": 0}], cleared=3,
                             rerolls={"earned": 1, "used": 0, "banked": 1})
        self.assertIsNone(ld.boon_problem(report))
        self.assertFalse(ld.owes_boon(report))
        self.assertFalse(ld.owes_reroll(report))
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 3))[0], 0)

    def test_a_run_that_cleared_two_owes_neither(self):
        ld.target_waves = 2
        report = {**REPORT, "wavesCleared": 2, "startRerolls": 1, "waves": [wave_row(1, 7), wave_row(2, 7)]}
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 2))[0], 0)

    def test_a_banked_reroll_at_the_first_pick_owes_the_reroll_check(self):
        report = self.report()
        code, why = ld.judge(FINAL, report, log_of(STANDARD, 5))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.reroll", why)
        self.assertNotIn("taom.long-defeat.boon,", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("reroll",), 5))[0], 0)

    def test_a_run_armed_with_no_reroll_that_spent_none_owes_no_reroll_check(self):
        picks = boon_picks(1, rerolls=0, class_id=ld.bot_class(1))
        report = self.report(picks=picks, startRerolls=0, rerolls={"earned": 1, "used": 0, "banked": 1})
        ld.expected_report = {"startRerolls": 0}
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0)
        self.assertFalse(ld.owes_reroll(report))

    def test_boons_that_do_not_replay_fail_the_run_naming_the_draw(self):
        picks = boon_picks(1, rerolls=1, class_id=ld.bot_class(1))
        picks[0]["offered"][0] = ["riders_at_dawn", "the_long_watch", "haggler_of_edoras"]
        code, why = ld.judge(FINAL, self.report(picks=picks), log_of(STANDARD + ("reroll",), 5))
        self.assertEqual(code, 1)
        self.assertIn("boons do not replay", why)
        self.assertIn("draw 0", why)

    def test_a_lose_run_is_not_asked_for_either(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(), "final": False}
        self.assertEqual(ld.judge(lost, report, LOSE_LOG)[0], 0)


class BoonAffixCutTests(unittest.TestCase):
    """A Legend boon that cuts a roll is allowed on the wave it was taken before, and after it, and nowhere else (the Slice 3 note on the Slice 4 row)."""

    SEED = 3

    def report(self, rows):
        picks = [{"pick": 1, "wave": 8, "offered": [["light_of_the_phial"]], "taken": "light_of_the_phial", "takenBeforeWave": 11}]
        return {"seed": self.SEED, "waves": rows, "boons": picks}

    def row(self, wave, cut=0):
        scheduled = ld.affix_count(ld.shipped_affix_schedule(), wave)
        return {"wave": wave, "affixes": ld.affix_roll(self.SEED, wave, scheduled)[:max(0, scheduled + cut)]}

    def test_the_cut_applies_from_the_wave_the_boon_was_taken_before(self):
        report = self.report([self.row(10), self.row(11, -1), self.row(12, -1)])
        self.assertIsNone(ld.affix_problem(report))
        self.assertEqual(ld.affix_cut(report, 10), 0)
        self.assertEqual(ld.affix_cut(report, 11), -1)
        self.assertEqual(ld.affix_cut(report, 30), -1)

    def test_a_row_before_the_boon_that_is_cut_fails(self):
        report = self.report([self.row(10, -1)])
        self.assertIn("wave 10", ld.affix_problem(report))

    def test_a_row_after_the_boon_that_is_not_cut_fails(self):
        report = self.report([self.row(11)])
        self.assertIn("wave 11", ld.affix_problem(report))
        self.assertIn("cut of -1", ld.affix_problem(report))

    def test_the_cut_shortens_the_roll_the_game_took_not_the_schedule_count(self):
        # A schedule row can ask for 8 while the pool holds fewer: the game rolls the whole pool and the cut leaves one less.
        report = self.report([{"wave": 11, "affixes": ld.affix_roll(self.SEED, 11, 8)[:len(ld.AFFIX_POOL) - 1]}])
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "affixes.json"
            path.write_text('{"schedule": [{"fromWave": 1, "count": 8}]}', encoding="utf-8")
            with mock.patch.object(ld, "AFFIX_FILE", path):
                self.assertIsNone(ld.affix_problem(report))

    def test_a_cut_is_a_prefix_of_the_roll_and_never_below_none(self):
        full = ld.affix_roll(self.SEED, 11, 2)
        self.assertEqual(self.row(11, -1)["affixes"], full[:1])
        self.assertEqual(self.row(11, -5)["affixes"], [])

    def test_a_pick_that_took_nothing_or_names_an_unknown_boon_cuts_nothing(self):
        for taken in (None, "not_a_boon"):
            report = {"seed": self.SEED, "waves": [self.row(11)], "boons": [{"pick": 1, "taken": taken, "takenBeforeWave": 4}]}
            self.assertEqual(ld.affix_cut(report, 11), 0)
            self.assertIsNone(ld.affix_problem(report))

    def test_the_two_legend_cuts_stack(self):
        picks = [{"pick": 1, "taken": "light_of_the_phial", "takenBeforeWave": 11}, {"pick": 2, "taken": "riders_at_dawn", "takenBeforeWave": 14}]
        report = {"seed": self.SEED, "boons": picks}
        self.assertEqual((ld.affix_cut(report, 13), ld.affix_cut(report, 14)), (-1, -3))


# Seed 1, pick 1, an archer at Helm's Deep (the garrison and shop hold infantry, archers and a Cavalry troop on the shop list): 25 rows are eligible. Pinned in
# LongDefeatShippedConfigTests.cs too, over the same boons.json, so the replay of class and troop rows cannot drift from the game.
BOON_PINNED_ARCHER = (
    (1, 1, 0, ["marksmans_call", "hornblowers_bargain", "spears_of_the_westfold"]),
    (1, 1, 1, ["shields_locked", "the_long_watch", "hearts_of_the_eored"]),
    (1, 1, 2, ["spears_of_the_westfold", "glittering_caves_refuge", "marksmans_call"]),
)
ALL_GROUPS = ("infantry", "archers", "cavalry")


class TroopGroupTests(unittest.TestCase):
    """A defender's group is its character's default_group through the engine's flags (the game's BoonTargets.OfCharacter): the replay needs it to know which troop rows were eligible."""

    def test_each_default_group_lands_in_one_group_mounted_first(self):
        expected = {"Infantry": "infantry", "HeavyInfantry": "infantry", "Ranged": "archers", "Skirmisher": "archers", "LightCavalry": "archers", "Cavalry": "cavalry",
                    "HeavyCavalry": "cavalry", "HorseArcher": "cavalry", None: "infantry", "NoSuchGroup": "infantry", "": "infantry"}
        for default_group, group in expected.items():
            self.assertEqual(ld.troop_group(default_group), group, default_group)

    def test_the_two_flag_tables_are_the_ones_the_binding_test_pins_to_the_engine(self):
        text = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "LongDefeatBindingTests.cs").read_text(encoding="utf-8")
        table = dict(re.findall(r"(\w+):(M?R?)[,\"]", re.search(r'Assert\.AreEqual\("(Infantry:[^"]+)"', text).group(1)))
        self.assertEqual({k.lower() for k, v in table.items() if "M" in v}, set(ld.MOUNTED_GROUPS))
        self.assertEqual({k.lower() for k, v in table.items() if "R" in v}, set(ld.RANGED_GROUPS))

    def test_helms_holds_a_troop_of_each_group_because_the_kings_knights_are_a_cavalry_troop(self):
        self.assertEqual(ld.troop_groups("helms"), set(ALL_GROUPS))
        self.assertEqual(ld.troop_groups(), set(ALL_GROUPS), "the default fortress is helms")
        self.assertEqual(ld.troop_groups("no_such_fortress"), set())

    def write_world(self, folder, garrison, offers, troops):
        folder = Path(folder)
        (folder / "fortresses.json").write_text(json.dumps({"fortresses": [{"id": "f", "shop": "s", "garrison": [{"troop": t, "class": c, "count": 1} for t, c in garrison]}]}), encoding="utf-8")
        (folder / "shop.json").write_text(json.dumps({"shops": {"s": {"offers": [{"id": t, "troop": t, "class": c} for t, c in offers]}}}), encoding="utf-8")
        body = "".join(f'<NPCCharacter id="{t}"' + (f' default_group="{g}"' if g else "") + "/>" for t, g in troops)
        (folder / "troops.xml").write_text(f"<NPCCharacters>{body}</NPCCharacters>", encoding="utf-8")
        return mock.patch.multiple(ld, FORTRESS_FILE=folder / "fortresses.json", SHOP_FILE=folder / "shop.json", TROOP_FILE=folder / "troops.xml")

    def test_the_groups_come_from_the_garrison_and_the_shop_of_the_named_fortress(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.write_world(folder, [("a", "Infantry")], [("b", "Ranged")], [("a", "Infantry"), ("b", "Ranged")]):
                self.assertEqual(ld.troop_groups("f"), {"infantry", "archers"})
            with self.write_world(folder, [("a", "Infantry")], [("k", "Infantry")], [("a", "Infantry"), ("k", "Cavalry")]):
                self.assertEqual(ld.troop_groups("f"), {"infantry", "cavalry"}, "a rider on the shop list is cavalry whatever slot it fills")

    def test_a_troop_the_file_does_not_hold_falls_back_on_its_formation_slot_as_the_game_does(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.write_world(folder, [("x", "Ranged"), ("y", "Infantry")], [], []):
                self.assertEqual(ld.troop_groups("f"), {"archers", "infantry"})

    def test_a_missing_troop_file_still_gives_the_slot_groups(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.write_world(folder, [("x", "Ranged")], [], []), mock.patch.object(ld, "TROOP_FILE", Path(folder) / "none.xml"):
                self.assertEqual(ld.troop_groups("f"), {"archers"})


class TargetBoonReplayTests(unittest.TestCase):
    """The class and troop rows in the replay (phase2-plan.md Slice 6): the deck is built with the run's class and the groups its garrison and shop hold."""

    def report(self, picks, **more):
        used = sum(max(0, len(p["offered"]) - 1) for p in picks)
        return {"seed": 1, "class": "archer", "fortress": "helms", "wavesCleared": 5, "startRerolls": 1, "boons": picks,
                "rerolls": {"earned": 2, "used": used, "banked": 2 - used}, **more}

    def pick(self, class_id="archer", groups=ALL_GROUPS, rerolls=1, take=None):
        return boon_picks(1, rerolls=rerolls, take=take, class_id=class_id, groups=groups)

    def test_the_python_draw_over_an_archers_pool_gives_the_pinned_cards(self):
        pool, weight = boon_pool(class_id="archer", groups=ALL_GROUPS)
        self.assertEqual(len(pool), 25)
        shown = []
        for seed, pick, draw, cards in BOON_PINNED_ARCHER:
            shown = ld.boon_cards(seed, pick, draw, pool, weight, shown)
            self.assertEqual(shown, cards, (seed, pick, draw))

    def test_every_archer_vector_is_also_pinned_in_the_csharp_test(self):
        text = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "Config" / "LongDefeatShippedConfigTests.cs").read_text(encoding="utf-8")
        for _, _, _, cards in BOON_PINNED_ARCHER:
            self.assertIn("new[] { " + ", ".join(f'"{c}"' for c in cards) + " }", text, cards)

    def test_a_pick_drawn_over_the_class_and_troop_rows_replays(self):
        report = self.report(self.pick())
        self.assertIsNone(ld.boon_problem(report))
        self.assertTrue(any(r["category"] in ld.BOON_TROOP_CATEGORIES + ("class",) for r in ld.shipped_boons()[0] if r["id"] in report["boons"][0]["offered"][0]),
                        "the pinned first draw must hold a troop or class row, or this test proves nothing")

    def test_a_class_row_of_another_class_is_never_drawn_for_this_class(self):
        report = self.report(self.pick(class_id="two_handed"), **{"class": "archer"})
        self.assertIn("pick 1 draw", ld.boon_problem(report) or "")

    def test_a_run_of_another_class_draws_its_own_class_rows(self):
        report = self.report(self.pick(class_id="sword_and_board"), **{"class": "sword_and_board"})
        self.assertIsNone(ld.boon_problem(report))
        self.assertIn("shieldwall_captain", [r["id"] for r in boon_pool(class_id="sword_and_board", groups=ALL_GROUPS)[0]])
        self.assertNotIn("shieldwall_captain", [r["id"] for r in boon_pool(class_id="archer", groups=ALL_GROUPS)[0]])

    def test_a_troop_row_is_only_drawn_when_the_fortress_holds_that_group(self):
        no_cavalry = self.report(self.pick(groups=("infantry", "archers")))
        self.assertIn("pick 1 draw", ld.boon_problem(no_cavalry) or "", "helms holds a cavalry troop, so a pool without cavalry rows is not the game's")
        with tempfile.TemporaryDirectory() as folder:
            troops = Path(folder) / "troops.xml"
            troops.write_text('<NPCCharacters><NPCCharacter id="fow_rohan_edoras_golden_hall_eorlingas_rider" default_group="Infantry"/></NPCCharacters>', encoding="utf-8")
            with mock.patch.object(ld, "TROOP_FILE", troops):
                self.assertEqual(ld.troop_groups("helms"), set(ALL_GROUPS) - {"cavalry"}, "with the rider made infantry the shop holds no cavalry troop")
                self.assertIsNone(ld.boon_problem(self.report(self.pick(groups=("infantry", "archers")))))

    def test_a_report_that_names_no_fortress_replays_over_the_free_and_class_rows_only(self):
        report = self.report(self.pick(groups=()))
        del report["fortress"]
        self.assertIsNone(ld.boon_problem(report))

    def test_a_troop_row_of_a_missing_group_is_not_eligible(self):
        rows = {"id": "c", "category": "cavalry", "class": "", "cut": 0, "reads": False}
        self.assertFalse(ld.boon_eligible(rows, "", {"infantry"}))
        self.assertTrue(ld.boon_eligible(rows, "", {"cavalry"}))
        self.assertTrue(ld.boon_eligible({**rows, "category": "warchest"}, "", set()))
        self.assertFalse(ld.boon_eligible({**rows, "category": "class", "class": "archer"}, "", set()))
        self.assertTrue(ld.boon_eligible({**rows, "category": "class", "class": "archer"}, "archer", set()))


class NamedBoonSeedTests(unittest.TestCase):
    """The seeds 1079.toml names for the boon check take a boon the unattended bot can read back. The bot buys the first offer of the slot it wants, so no cavalry ever
    stands in its run and a cavalry boon could only write read=skip (reason=no-soldier): a named seed must take a boon of a group that is fielded."""

    # (seed, class armed with --class or None for the bot's own draw, the boon its first pick takes, the target group it changes)
    SEEDS = ((33, None, "stout_yeomanry", "infantry"), (22, "archer", "marksmans_call", "archers"), (4, None, "steady_hand", "player"))
    FIELDED = {"infantry", "archers", "player"}

    def first_pick(self, seed, class_id):
        """The game's bot at the first pick (ShopBot.Decide): one reroll from the start bank, more only while every card has a drawback, then the first card without one."""
        raw = json.loads(ld.BOON_FILE.read_text(encoding="utf-8-sig"))
        drawback = {b["id"]: bool(b.get("drawback")) for b in raw["boons"]}
        rows, weight = ld.shipped_boons()
        groups = ld.troop_groups()
        pool = [r for r in rows if ld.boon_eligible(r, class_id, groups)]
        cards = ld.boon_cards(seed, 1, 0, pool, weight, [])
        bank, draw = ld.DEFAULT_START_REROLLS, 0
        while bank > 0 and (draw == 0 or all(drawback[c] for c in cards)):
            draw += 1
            bank -= 1
            cards = ld.boon_cards(seed, 1, draw, pool, weight, cards)
        return next((c for c in cards if not drawback[c]), cards[0]), raw

    def seed_line(self):
        text = (REPO / "tools" / "ingame_checks" / "1079.toml").read_text(encoding="utf-8")
        return next(line for line in text.splitlines() if line.startswith("# Seeds whose first offer"))

    def test_each_named_seed_takes_the_boon_it_is_named_for_and_that_boon_changes_a_group_that_stands(self):
        for seed, armed, boon, group in self.SEEDS:
            class_id = armed or ld.bot_class(seed)
            taken, raw = self.first_pick(seed, class_id)
            self.assertEqual(taken, boon, (seed, armed))
            row = next(b for b in raw["boons"] if b["id"] == boon)
            targets = {e["target"] for e in row["effects"] + row.get("drawback", []) if e.get("key") in ld.BOON_READ_KEYS}
            self.assertEqual(targets, {group}, (seed, boon))
            self.assertLessEqual(targets, self.FIELDED, (seed, boon))

    def test_the_bot_never_buys_a_rider_because_the_first_offer_of_each_slot_is_not_cavalry(self):
        known = {c.get("id"): c.get("default_group") for c in ld.ET.parse(ld.TROOP_FILE).getroot().iter("NPCCharacter")}
        shop = json.loads(ld.SHOP_FILE.read_text(encoding="utf-8-sig"))["shops"]["rohan"]["offers"]
        for slot in ("Infantry", "Ranged"):
            first = next(o for o in shop if o["class"] == slot)
            self.assertNotEqual(ld.troop_group(known[first["troop"]]), "cavalry", first["id"])
        fortress = next(f for f in json.loads(ld.FORTRESS_FILE.read_text(encoding="utf-8-sig"))["fortresses"] if f["id"] == "helms")
        for row in fortress["garrison"]:
            self.assertNotEqual(ld.troop_group(known[row["troop"]]), "cavalry", row["troop"])

    def test_the_manifest_names_exactly_these_seeds_and_no_cavalry_one(self):
        line = self.seed_line()
        for seed, armed, _, _ in self.SEEDS:
            self.assertIn((f"--class {armed} " if armed else "") + f"--seed {seed}", line)
        self.assertNotIn("cavalry", line.lower())


class BoonReadBackJudgeTests(unittest.TestCase):
    """A taken boon with a stat or health key was read back from a soldier of its target: the boon line says read=pass or read=skip, or the run never proved the boon reached anyone."""

    # Seed 5 draws wains_of_the_westfold, swift_nock and haggler_of_edoras first for its class (sword_and_board), one of which owes a read-back; the draw moves when a slice adds boon rows.
    SEED = 5

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def card(self, reads):
        """A card of the first draw for the bot's class at Helm's Deep that does (reads) or does not owe a read-back; the draw moves when a slice adds boon rows."""
        draw = boon_picks(self.SEED, rerolls=0, class_id=ld.bot_class(self.SEED), groups=ALL_GROUPS)[0]["offered"][0]
        owed = {r["id"]: r["reads"] for r in ld.shipped_boons()[0]}
        return next(c for c in draw if owed[c] == reads)

    def report(self, taken, cleared=5):
        picks = boon_picks(self.SEED, rerolls=0, take=taken, class_id=ld.bot_class(self.SEED), groups=ALL_GROUPS)
        return {**REPORT, "seed": self.SEED, "fortress": "helms", "class": ld.bot_class(self.SEED), "wavesCleared": cleared, "startRerolls": 0, "boons": picks,
                "rerolls": {"earned": 1, "used": 0, "banked": 1}, "waves": [wave_row(w, self.SEED) for w in range(1, cleared + 1)]}

    def test_the_read_owed_rows_are_exactly_those_with_a_stat_or_health_key(self):
        owed = {r["id"] for r in ld.shipped_boons()[0] if r["reads"]}
        self.assertIn("stout_yeomanry", owed)
        self.assertIn("steady_aim", owed)
        self.assertNotIn("spears_of_the_westfold", owed, "a damage-only boon is read at the blow, not from a driven property")
        self.assertNotIn("granary_stores", owed)
        self.assertNotIn("light_of_the_phial", owed)

    def test_a_pick_of_a_read_owed_boon_owes_its_read_token_once_a_later_wave_cleared(self):
        self.assertEqual(ld.read_owed_picks(self.report("stout_yeomanry")), [1])
        self.assertEqual(ld.read_owed_picks(self.report("granary_stores")), [])
        self.assertEqual(ld.read_owed_picks(self.report("stout_yeomanry", cleared=3)), [], "the run ended at the clear that opened the pick")
        self.assertEqual(ld.read_owed_picks({"boons": [{"pick": 1, "taken": None, "wave": 3}], "wavesCleared": 5}), [])
        self.assertEqual(ld.read_owed_picks({"wavesCleared": 5}), [])

    def log(self, read):
        tail = f" read={read}" if read else ""
        boon = ("[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.boon result=pass p=1 cards=3 eligible=24 offered=a,b,c taken=stout_yeomanry "
                "draws=1 distinct=true applied=true gold=0" + tail)
        return log_of(STANDARD, 5).replace(pass_line("boon"), boon)

    def judged(self, read, taken=None):
        return ld.judge(FINAL, self.report(taken or self.card(True)), self.log(read))

    def test_a_run_whose_boon_line_lacks_the_read_token_fails_naming_the_pick(self):
        code, why = self.judged(None)
        self.assertEqual(code, 1)
        self.assertIn("pick(s) 1", why)
        self.assertIn("read=", why)

    def test_a_read_that_failed_never_satisfies_the_judge(self):
        self.assertEqual(self.judged("fail")[0], 1)

    def test_a_pass_or_a_skip_read_satisfies_the_judge(self):
        for read in ("pass", "skip"):
            code, why = self.judged(read)
            self.assertEqual(code, 0, f"{read}: {why}")

    def test_a_boon_with_nothing_to_read_owes_no_token(self):
        # A card with no stat or health key (a war chest or damage-only boon) is read at the blow or in the purse, not from a driven property.
        code, why = self.judged(None, taken=self.card(False))
        self.assertEqual(code, 0, why)

    def test_the_csharp_line_carries_the_token_the_wrapper_asks_for(self):
        source_text = source("Diagnostics", "LongDefeatBoonChecks.cs")
        self.assertIn('" read=" + read', source_text)
        self.assertIn("p={Num(record.Pick)} cards={Num(last.Ids.Count)}", source_text)

    def test_the_read_keys_are_the_games_stat_keys_and_health(self):
        plan = source("Domain", "BoonPlan.cs")
        stat_block = re.search(r"StatKeys \{ get; \} = new\[\]\s*\{(.*?)\};", plan, re.S).group(1)
        names = re.findall(r"(\w+)", stat_block)
        values = dict(re.findall(r'public const string (\w+) = "(\w+)";', plan))
        self.assertEqual(sorted([values[n] for n in names] + ["healthPercent"]), sorted(ld.BOON_READ_KEYS))


class CommanderJudgeTests(unittest.TestCase):
    """The commanders in the report (phase2-plan.md section 7, rules 16 and 17): one at each multiple of the roster's `every` with the configured troop, rank and steps, every summon
    accounted for, and the `commander` and `summon` checks owed by a run that reached them."""

    SEED = 5

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def commander(self, troop, rank, steps=0, outcome="killed", uses=None, after_end=0):
        return {"troop": troop, "rank": rank, "steps": steps, "outcome": outcome, "uses": uses if uses is not None else {"war_cry": 1}, "usesAfterEnd": after_end}

    def rows(self, through=5, **overrides):
        """Wave rows 1..through with the configured commander at the multiples of four; `overrides` replaces fields of a row by wave number (w4=...)."""
        shipped = {4: self.commander("lord_I3_1", "captain"), 8: self.commander("lord_I1_1", "lieutenant"), 12: self.commander("lord_I4_1", "general"),
                   16: self.commander("lord_I4_1", "general", steps=1)}
        rows = []
        for w in range(1, through + 1):
            row = wave_row(w, self.SEED, summoned=0, summonKills=0, summonFelled=0, commander=shipped.get(w))
            rows.append({**row, **overrides.get(f"w{w}", {})})
        return rows

    def report(self, rows, **more):
        return {**REPORT, "seed": self.SEED, "fortress": "helms", "class": ld.bot_class(self.SEED), "wavesCleared": len(rows), "startRerolls": 0, "waves": rows,
                "boons": [], "rerolls": {"earned": 0, "used": 0, "banked": 0}, **more}

    def test_the_shipped_commanders_are_read_from_waves_json(self):
        spec = ld.shipped_commanders()
        self.assertEqual(spec["every"], 4)
        self.assertEqual(spec["ranks"], [("captain", "lord_I3_1"), ("lieutenant", "lord_I1_1"), ("general", "lord_I4_1")])
        self.assertEqual(spec["summon"], {"summon_uruks"})
        self.assertEqual(spec["max_steps"], 10)
        self.assertIsNone(ld.shipped_commanders("no_such_fortress"))

    def test_the_rank_and_steps_follow_the_ranks_then_the_steps(self):
        spec = ld.shipped_commanders()
        got = {w: ld.commander_expected(spec, w) for w in (3, 4, 8, 12, 16, 20, 200)}
        self.assertIsNone(got[3])
        self.assertEqual(got[4], ("captain", "lord_I3_1", 0))
        self.assertEqual(got[8], ("lieutenant", "lord_I1_1", 0))
        self.assertEqual(got[12], ("general", "lord_I4_1", 0))
        self.assertEqual(got[16], ("general", "lord_I4_1", 1))
        self.assertEqual(got[20], ("general", "lord_I4_1", 2))
        self.assertEqual(got[200], ("general", "lord_I4_1", 10), "the steps stop at maxSteps")

    def test_a_clean_run_of_waves_with_commanders_replays(self):
        self.assertIsNone(ld.commander_problem(self.report(self.rows(16))))

    def test_a_report_with_no_waves_or_an_unknown_fortress_has_nothing_to_replay(self):
        self.assertIsNone(ld.commander_problem({"fortress": "helms"}))
        self.assertIsNone(ld.commander_problem(self.report(self.rows(5), fortress="no_such_fortress")))

    def test_a_missing_or_extra_commander_fails_naming_the_wave(self):
        self.assertIn("wave 4", ld.commander_problem(self.report(self.rows(5, w4={"commander": None}))))
        extra = ld.commander_problem(self.report(self.rows(5, w5={"commander": self.commander("lord_I3_1", "captain")})))
        self.assertIn("wave 5", extra)

    def test_a_wrong_troop_rank_or_steps_fails(self):
        for field, wrong in (("troop", "lord_I1_1"), ("rank", "general"), ("steps", 1)):
            commander = {**self.commander("lord_I3_1", "captain"), field: wrong}
            problem = ld.commander_problem(self.report(self.rows(5, w4={"commander": commander})))
            self.assertIn(field, problem)

    def test_a_commander_still_standing_at_a_clear_or_using_an_ability_after_his_end_fails(self):
        standing = self.commander("lord_I3_1", "captain", outcome="standing")
        self.assertIn("outcome", ld.commander_problem(self.report(self.rows(5, w4={"commander": standing}))))
        late = self.commander("lord_I3_1", "captain", after_end=1)
        self.assertIn("usesAfterEnd", ld.commander_problem(self.report(self.rows(5, w4={"commander": late}))))

    def test_every_summon_of_a_cleared_wave_was_killed_or_felled(self):
        ok = {"summoned": 20, "summonKills": 17, "summonFelled": 3}
        self.assertIsNone(ld.commander_problem(self.report(self.rows(5, w4=ok))))
        short = {"summoned": 20, "summonKills": 17, "summonFelled": 2}
        self.assertIn("summon", ld.commander_problem(self.report(self.rows(5, w4=short))))

    def test_a_run_that_cleared_a_commander_wave_owes_the_commander_check_and_a_summon_owes_its_own(self):
        self.assertTrue(ld.owes_commander(self.report(self.rows(4))))
        self.assertFalse(ld.owes_commander(self.report(self.rows(3))))
        self.assertTrue(ld.owes_commander({"startWave": 1, "wavesCleared": 5}), "no wave rows: the cleared count decides")
        self.assertFalse(ld.owes_commander({"startWave": 1, "wavesCleared": 3}))
        lieutenant = self.commander("lord_I1_1", "lieutenant", uses={"war_cry": 2, "summon_uruks": 1})
        self.assertTrue(ld.owes_summon(self.report(self.rows(8, w8={"commander": lieutenant}))))
        self.assertFalse(ld.owes_summon(self.report(self.rows(8))), "he never summoned")

    def five_clears(self, rows):
        """A report of five clears, which owe one boon pick (at the third clear), taken as the first card of the seed's draw."""
        picks = boon_picks(self.SEED, rerolls=0, class_id=ld.bot_class(self.SEED), groups=ALL_GROUPS)
        return self.report(rows, boons=picks, rerolls={"earned": 1, "used": 0, "banked": 1})

    def judged(self, rows, checks):
        return ld.judge(FINAL, self.five_clears(rows), log_of(checks, len(rows)))

    def test_the_judge_fails_a_run_with_a_commander_wave_and_no_commander_line(self):
        owed = tuple(c for c in STANDARD if c != "commander")
        code, why = self.judged(self.rows(5), owed)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.commander", why)

    def test_the_judge_passes_it_with_the_line(self):
        code, why = self.judged(self.rows(5), STANDARD)
        self.assertEqual(code, 0, why)

    def test_the_judge_asks_for_the_summon_check_of_a_run_whose_commander_summoned(self):
        # --start-wave 8 --waves 1: one clear, so no pick is owed and the lieutenant's wave is the only row.
        ld.target_waves = 1
        ld.expected_report = {"startWave": 8}
        lieutenant = self.commander("lord_I1_1", "lieutenant", uses={"war_cry": 2, "summon_uruks": 1})
        rows = [wave_row(8, self.SEED, commander=lieutenant, summoned=20, summonKills=20, summonFelled=0)]
        report = self.report(rows, startWave=8, wavesCleared=1)
        code, why = ld.judge(FINAL, report, log_of(STANDARD, 1))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.summon", why)
        code, why = ld.judge(FINAL, report, log_of(STANDARD + ("summon",), 1))
        self.assertEqual(code, 0, why)

    def test_the_judge_fails_a_report_whose_commanders_do_not_replay(self):
        code, why = self.judged(self.rows(5, w4={"commander": None}), STANDARD)
        self.assertEqual(code, 1)
        self.assertIn("commanders", why)


if __name__ == "__main__":
    unittest.main()
