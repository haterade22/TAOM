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


def skip_line(name, wave, reason):
    """The skip the game writes for a sally or a return it did not make (LongDefeatSallyChecks.Skipped): the wave and the reason."""
    return f"[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.{name} result=skip w={wave} reason={reason}"


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
            "summoned": 0, "summonKills": 0, "summonFelled": 0, "commander": commander, **NO_VOLLEYS, **more}


# A run that bought nothing of the fortifications: no purchase, no gold spent, no level, and a wave row whose volleys never fired.
NO_VOLLEYS = {"volley": {"uses": 0, "arrows": 0, "hits": 0, "kills": 0, "withheld": 0}, "towerVolley": {"uses": 0, "arrows": 0, "hits": 0, "kills": 0, "withheld": 0}}
NO_FORT = {"purchases": [], "gold": {"spent": 0, "spentOn": {"packs": 0, "gate": 0, "towers": 0, "fieldCare": 0, "volley": 0}},
           "fortifications": {"gateLevel": 0, "towerLevel": 0, "volleyLevel": 0}}


# The class is the bot's draw for the report's seed, as the game takes it when the arm file names none (ld.bot_class, pinned to ClassPlanTests.cs).
REPORT = {"schema": 2, "seed": 7, "class": ld.bot_class(7), "startWave": 1, "poison": {"ownerMissing": 0}, "final": True, "wavesCleared": 5, "wavesFelled": 1,
          "waves": [wave_row(n) for n in range(1, 6)], **NO_FORT}
def cleared(n, seed=7, **more):
    """REPORT with n cleared waves and the one row the game writes for each (RunStats.CloseWave); a wave a run ends in has no row."""
    return {**REPORT, "seed": seed, "class": ld.bot_class(seed), "wavesCleared": n, "waves": [wave_row(w, seed) for w in range(1, n + 1)], **more}


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
        self.assertEqual(ld.judge(FINAL, cleared(2), log_of(STANDARD, 2))[0], 0)
        self.assertEqual(ld.judge(FINAL, cleared(3), log_of(STANDARD, 2))[0], 1)

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
        code, why = ld.judge(FINAL, cleared(4), FULL_LOG)
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
            code, why = ld.judge({"reason": reason, "expect": "lost"}, cleared(0, final=False), LOSE_LOG)
            self.assertEqual(code, 0, why)

    def test_a_lost_run_with_no_wave_cleared_passes_on_the_lines_the_director_writes(self):
        # LongDefeatChecks.BestRunSaved for a 0-wave run saved into a fresh folder and read back.
        best = "[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.best-run result=pass waves=0 kills=14 read_back=same"
        log = "\n".join([stamp_line()] + [pass_line(n) for n in ld.LOSE_CHECKS if n != "best-run"] + [best] + READ_BACK_LINES)
        self.assertEqual(ld.judge(self.LOST, cleared(0, final=False), log)[0], 0)
        skipped = best.replace("result=pass", "result=skip")
        self.assertEqual(ld.judge(self.LOST, cleared(0, final=False), log.replace(best, skipped))[0], 1)

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
        code, why = ld.judge({"reason": "final"}, cleared(2, wavesFelled=0), text)
        self.assertEqual(code, 0, why)
        self.assertIn(f"build {FIXTURE_HEAD[:9]}.dirty", why)

    def test_the_affix_effect_row_judges_the_blades_line_the_game_writes(self):
        # The line is LongDefeatAffixChecks.Blades' detail under the affix-effect id, keyed by power (see LongDefeatBladesChecksTests for the exact text).
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.affix-effect")
        head = "[2026-11-05 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result={} "
        detail = "power=blades w=5 doses=12 hits=40 damage=96 owner_missing={} withheld=1"
        passing = [(None, 1, head.format("pass") + detail.format(0))]
        failing = [(None, 1, head.format("fail") + detail.format(1))]
        skipping = [(None, 1, head.format("skip") + "power=blades w=5 doses=0 hits=0 damage=0 owner_missing=0 withheld=0 reason=no-hit")]

        self.assertEqual(iv.judge_segment(row, passing, []).verdict, iv.PASS)
        self.assertEqual(iv.judge_segment(row, failing, []).verdict, iv.FAIL)
        self.assertEqual(iv.judge_segment(row, skipping, []).verdict, iv.NOT_REACHED, "a wave in which nothing could show the poison proves nothing")

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
                          "taom.long-defeat.boon", "taom.long-defeat.reroll", "taom.long-defeat.commander", "taom.long-defeat.summon",
                          "taom.long-defeat.sally", "taom.long-defeat.sally-return", "taom.long-defeat.sally-contact", "taom.long-defeat.field-care",
                          "taom.long-defeat.fort-points", "taom.long-defeat.gate-level", "taom.long-defeat.gate-repair", "taom.long-defeat.gate-attacked", "taom.long-defeat.volley", "taom.long-defeat.posts"):
                self.assertEqual(result, iv.NOT_REACHED, row.id)
            else:
                self.assertEqual(result, iv.PASS, row.id)

    def test_an_injected_failure_flips_the_judge_and_its_row(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        ld.target_waves, ld.tree_head = 2, FIXTURE_HEAD
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        text = FIXTURE.read_text(encoding="utf-8") + "\n" + CLASS_LINE
        report = cleared(2)
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
        code, why = ld.judge(done, cleared(3, final=False), text)
        self.assertEqual(code, 0, why)
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        lines = [(None, number, row) for number, row in enumerate(text.splitlines(), 1)]
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.player-death")
        self.assertEqual(iv.judge_segment(row, lines, []).verdict, iv.PASS)
        bad = "[2026-11-04 04:30:00] [INFO] [Check] id=taom.long-defeat.end result=fail reason=fault"
        self.assertEqual(ld.judge(done, cleared(3, final=False), text + "\n" + bad)[0], 1)

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

    SALLY_LINES = {
        "sally": "w=1 how=remount mount=yes dist=0.4 deaths=0->0 old_state=Routed old_lost=no",
        "sally-return": "w=1 dist=1.2 keep_radius=14.0 deaths=0->0",
        "sally-contact": "w=1 t=12.5 hits_given=1 hits_taken=0 mount_hits=0 kills=0",
    }

    def test_the_sally_rows_read_the_lines_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        for name, detail in self.SALLY_LINES.items():
            row = next(c for c in manifest.checks if c.id == f"taom.long-defeat.{name}")
            head = f"[2026-11-05 10:00:00] [INFO] [Check] id=taom.long-defeat.{name} result="
            self.assertEqual(row.absence, "not-reached", name)
            self.assertEqual(iv.judge_segment(row, [(None, 1, head + "pass " + detail)], []).verdict, iv.PASS, name)
            self.assertEqual(iv.judge_segment(row, [(None, 1, head + "fail " + detail)], []).verdict, iv.FAIL, name)
            self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED, name)

    def test_a_skipped_sally_line_proves_nothing_and_a_late_fail_beside_a_pass_is_a_fail(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.sally")
        head = "[2026-11-05 10:00:00] [INFO] [Check] id=taom.long-defeat.sally result="
        skipped = [(None, 1, head + "skip w=1 reason=no-sally-point")]
        both = [(None, 1, head + "pass " + self.SALLY_LINES["sally"]), (None, 2, head + "fail w=1 reason=old-body-reported-lost callback=removed old_state=Killed")]
        self.assertEqual(iv.judge_segment(row, skipped, []).verdict, iv.NOT_REACHED)
        self.assertEqual(iv.judge_segment(row, both, []).verdict, iv.FAIL)

    def test_the_sally_lines_are_the_ones_the_csharp_checks_format(self):
        checks = source("Diagnostics", "LongDefeatSallyChecks.cs")
        for key in ("w=", "how=", "mount=", "dist=", "deaths=", "old_state=", "old_lost=", "keep_radius=", "t=", "reason=", "callback="):
            self.assertIn(key, checks, key)
        for name in ("sally", "sally-return", "sally-contact"):
            self.assertIn(f'"taom.long-defeat.{name}"', source("Diagnostics", "LongDefeatChecks.cs"))

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
    (1, 5, ["hide"]),
    (1, 10, ["drums", "terror"]),
    (1, 15, ["hide", "hammer", "blades"]),
    (12345, 40, ["drums", "discipline", "blades"]),
    (7, 5, ["terror"]),
    (0, 15, ["lash", "drums", "discipline"]),
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
        self.assertEqual(["lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades"], list(ld.AFFIX_POOL), "all eight roll since Slice 9")
        self.assertEqual([i for i in ld.AFFIX_IDS if i in ld.AFFIX_POOL], list(ld.AFFIX_POOL), "the pool keeps the order of the eight")
        self.assertTrue(set(ld.AFFIX_EFFECT_IDS) <= set(ld.AFFIX_POOL), "an effect line is only owed for a power that can roll")

    def test_a_shorter_roll_is_a_prefix_of_a_longer_one(self):
        for seed in range(20):
            full = ld.affix_roll(seed, 20, 8)
            self.assertEqual(sorted(full), sorted(ld.AFFIX_POOL), "a roll of more than the pool holds is the whole pool")
            for count in range(len(ld.AFFIX_POOL) + 1):
                self.assertEqual(full[:count], ld.affix_roll(seed, 20, count), (seed, count))

    def test_every_power_of_the_eight_can_roll_now_that_each_acts(self):
        seen = {power for seed in range(200) for wave in (5, 10, 15, 40) for power in ld.affix_roll(seed, wave, 3)}
        self.assertEqual(set(ld.AFFIX_IDS), seen)

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
        self.assertEqual(["terror"], rows[4]["affixes"], "seed 7 rolls terror at wave 5")
        code, why = ld.judge(FINAL, self.report(rows=wrong), FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("wave 5", why)
        self.assertIn("lash", why)
        self.assertIn("terror", why)

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
        seeds = {"eye": 0, "hide": 1, "lash": 2, "blades": 3, "discipline": 5, "terror": 7, "hammer": 9, "drums": 15}
        for power, seed in seeds.items():
            report = self.report(seed=seed, rows=[wave_row(w, seed) for w in range(1, 6)])
            self.assertEqual([power], report["waves"][4]["affixes"])
            code, why = ld.judge(FINAL, report, without)
            self.assertEqual(code, 1, power)
            self.assertIn("taom.long-defeat.affix-effect", why)
            # Seeds 2 and 5 draw the Cavalry class since Slice 11, which owes its sally checks as well.
            owed = tuple(ld.CLASS_CHECKS.get(report["class"], ()))
            self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + owed, 5))[0], 0, power)
        self.assertEqual(sorted(seeds), sorted(ld.AFFIX_EFFECT_IDS))

    def test_the_effect_line_may_be_a_pass_or_a_skip_but_a_fail_fails_the_run(self):
        base = log_of(BOONED + ("affix", "commander"), 5)
        line = "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result={} power=hide troop=x"
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("pass"))[0], 0)
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("skip"))[0], 0, "an unarmoured soldier cannot show hide: still read")
        self.assertEqual(ld.judge(FINAL, self.report(), base + line.format("fail"))[0], 1)

    def test_a_row_with_no_effect_power_owes_no_effect_line(self):
        self.assertFalse(ld.reached_effect_affixes({"waves": [{"affixes": []}, "x", {"affixes": "hide"}]}))
        self.assertTrue(ld.reached_effect_affixes({"waves": [{"affixes": ["blades"]}]}), "blades proves its poison under the same id")
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
        self.assertEqual(ld.judge(FINAL, {**cleared(5), "seed": 5}, FULL_LOG)[0], 1)
        self.assertEqual(ld.judge(FINAL, cleared(5, seed=0), FULL_LOG)[0], 0)

    def test_an_owner_missing_above_zero_fails_and_zero_passes(self):
        zero = FULL_LOG + "\n[2026-11-04 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result=pass power=hide owner_missing=0 withheld=2"
        self.assertEqual(ld.judge(FINAL, REPORT, zero)[0], 0)
        for count in ("1", "3", "12"):
            code, why = ld.judge(FINAL, REPORT, zero.replace("owner_missing=0", "owner_missing=" + count))
            self.assertEqual(code, 1, count)
            self.assertIn("owner_missing=" + count, why)

    def test_a_blades_line_with_owner_missing_fails_the_run_even_as_a_pass(self):
        line = "\n[2026-11-05 10:00:00] [INFO] [Check] id=taom.long-defeat.affix-effect result=pass power=blades w=5 doses=3 hits=4 damage=9 owner_missing={} withheld=0"
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG + line.format(0))[0], 0)
        code, why = ld.judge(FINAL, REPORT, FULL_LOG + line.format(2))
        self.assertEqual(code, 1)
        self.assertIn("owner_missing=2", why)

    def test_the_largest_owner_missing_in_the_log_is_the_one_reported(self):
        log = FULL_LOG + "\nx owner_missing=0\ny owner_missing=4\nz owner_missing=2"
        self.assertIn("owner_missing=4", ld.judge(FINAL, REPORT, log)[1])

    def test_the_cavalry_class_also_requires_sally_and_sally_return(self):
        # Once a sally point is recorded (a pass line for the sally), the cavalry class owes both checks again.
        ld.expected_report = {"class": "cavalry"}
        report = {**REPORT, "class": "cavalry"}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)
        self.assertIn("taom.long-defeat.sally-return", why)
        full = log_of(STANDARD + ("sally", "sally-return"), 5)
        self.assertEqual(ld.judge(FINAL, report, full)[0], 0)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("sally",), 5))[0], 1)

    def cavalry_run(self):
        ld.expected_report = {"class": "cavalry"}
        return {**REPORT, "class": "cavalry"}, log_of(STANDARD + ("sally", "sally-return"), 5)

    # ---- no sally point recorded (Slice 11): Cavalry rides from Begin and the game writes a skip for the sally and for the return at every wave ----

    def no_point_log(self, waves=5, reason="no-sally-point", checks=STANDARD):
        """The log of a Cavalry run with no sally point: the standard passes, then a sally skip at each wave's start and a sally-return skip at each clear."""
        rows = [log_of(checks, waves)]
        for wave in range(1, waves + 1):
            rows.append(skip_line("sally", wave, reason))
            rows.append(skip_line("sally-return", wave, reason))
        return "\n".join(rows)

    def test_a_cavalry_run_with_no_sally_point_recorded_passes_on_skips_that_say_so(self):
        report, _ = self.cavalry_run()
        code, why = ld.judge(FINAL, report, self.no_point_log())
        self.assertEqual(code, 0, why)
        self.assertIn("no sally point recorded", why)
        self.assertIn("sally and sally-return skipped", why)

    def test_the_no_sally_point_skip_is_accepted_for_that_reason_alone(self):
        report, _ = self.cavalry_run()
        for reason in ("point-unusable", "taken-over", "no-lord"):
            code, why = ld.judge(FINAL, report, self.no_point_log(reason=reason))
            self.assertEqual(code, 1, reason)
            self.assertIn("taom.long-defeat.sally", why, reason)

    def test_one_other_reason_among_the_no_sally_point_skips_spoils_the_waiver(self):
        report, _ = self.cavalry_run()
        log = self.no_point_log() + "\n" + skip_line("sally", 6, "taken-over")
        code, why = ld.judge(FINAL, report, log)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)

    def test_the_waiver_needs_the_skip_lines_and_not_just_a_missing_pass(self):
        report, _ = self.cavalry_run()
        # No pass and no skip at all for either check: nothing says why, so both are still missing.
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)
        # The sally skipped for no point but a cleared wave wrote no return line of any kind.
        only_out = log_of(STANDARD, 5) + "\n" + skip_line("sally", 1, "no-sally-point")
        code, why = ld.judge(FINAL, report, only_out)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally-return", why)

    def test_once_a_point_exists_both_checks_are_required_again(self):
        report, _ = self.cavalry_run()
        # A sally that went out passes, so the point is recorded: a return skip that claims no point does not stand in for the return.
        log = log_of(STANDARD + ("sally",), 5) + "\n" + skip_line("sally-return", 1, "no-sally-point")
        code, why = ld.judge(FINAL, report, log)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally-return", why)
        # A pass on both is what counts, whatever skips sit beside it.
        both = log_of(STANDARD + ("sally", "sally-return"), 5) + "\n" + skip_line("sally", 2, "no-sally-point")
        self.assertEqual(ld.judge(FINAL, report, both)[0], 0)

    # Slice 11 review (data flow): the game writes reason=no-sally-point for a malformed `sally` block too (FortressesReader drops it with a warning and the plan has no sally), and the judge
    # does not fail on config warnings. The waiver therefore also needs this tree's fortresses.json to hold no `sally` key for the fortress.
    def fortresses_with(self, folder, sally):
        row = {"id": "helms", "defenderCulture": "vlandia"}
        if sally is not None:
            row["sally"] = sally
        path = Path(folder) / "fortresses.json"
        path.write_text(json.dumps({"fortresses": [row]}), encoding="utf-8")
        return path

    def test_the_waiver_holds_only_while_the_fortress_row_records_no_sally_block(self):
        report, _ = self.cavalry_run()
        log = self.no_point_log()
        with tempfile.TemporaryDirectory() as folder:
            with mock.patch.object(ld, "FORTRESS_FILE", self.fortresses_with(folder, None)):
                self.assertEqual(ld.judge(FINAL, report, log)[0], 0)
            for block in ({"x": 1, "y": 2, "z": 3, "fx": 0, "fy": 1}, {"x": 1}, "garbage"):
                with mock.patch.object(ld, "FORTRESS_FILE", self.fortresses_with(folder, block)):
                    code, why = ld.judge(FINAL, report, log)
                    self.assertEqual(code, 1, str(block))
                    self.assertIn("taom.long-defeat.sally", why, str(block))

    def test_the_shipped_fortresses_record_no_sally_block_so_the_waiver_stands_today(self):
        self.assertFalse(ld.sally_recorded("helms"))
        self.assertFalse(ld.sally_recorded(None))

    def test_a_lose_run_owes_the_sally_but_never_the_return(self):
        # A run built to lose clears no wave on purpose (LOSE_CHECKS), so a sally-return pass is not owed; the sally is written at wave 1's start whatever follows.
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**cleared(0), "class": "cavalry", "final": False}
        ld.expected_report = {"class": "cavalry"}
        code, why = ld.judge(lost, report, log_of(ld.LOSE_CHECKS + ("sally",), 0))
        self.assertEqual(code, 0, why)
        self.assertEqual(ld.judge(lost, report, log_of(ld.LOSE_CHECKS, 0))[0], 1)
        no_point = log_of(ld.LOSE_CHECKS, 0) + "\n" + skip_line("sally", 1, "no-sally-point")
        self.assertEqual(ld.judge(lost, report, no_point)[0], 0)

    def test_a_deep_run_lost_before_its_first_clear_owes_no_return(self):
        lost = {"reason": "lost_defenders", "expect": "deep"}
        report = {**REPORT, "class": "cavalry", "startWave": 12, "wavesCleared": 0, "final": False, "waves": []}
        ld.expected_report = {"class": "cavalry", "startWave": 12}
        log = log_of(ld.DEEP_CHECKS + ("sally",), 0)
        code, why = ld.judge(lost, report, log)
        self.assertNotIn("sally-return", why)
        cleared = {**report, "wavesCleared": 1, "waves": [wave_row(12)]}
        code, why = ld.judge(lost, cleared, log)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally-return", why)

    def test_sally_contact_is_reported_for_a_cavalry_run_and_never_required(self):
        report, base = self.cavalry_run()
        none = ld.judge(FINAL, report, base)
        self.assertEqual(none[0], 0, none[1])
        self.assertIn("sally-contact: not reached", none[1])
        contact = pass_line("sally-contact") + "\n" + pass_line("sally-contact").replace("detail=1", "detail=2")
        got = ld.judge(FINAL, report, base + "\n" + contact)
        self.assertEqual(got[0], 0, got[1])
        self.assertIn("sally-contact: 2 pass, 0 skip", got[1])
        quiet = base + "\n" + pass_line("sally-contact").replace("result=pass", "result=skip")
        self.assertIn("sally-contact: 0 pass, 1 skip", ld.judge(FINAL, report, quiet)[1])

    def test_a_failed_sally_contact_line_still_fails_the_run_like_any_failed_check(self):
        report, base = self.cavalry_run()
        code, why = ld.judge(FINAL, report, base + "\n" + pass_line("sally-contact").replace("result=pass", "result=fail"))
        self.assertEqual(code, 1)
        self.assertIn("failed check", why)

    def test_a_late_loss_report_on_the_old_body_fails_the_run_through_the_sally_check(self):
        # The game writes a second line for the wave, result=fail, when native reports the faded body killed after the step passed: any failed check line fails the run.
        report, base = self.cavalry_run()
        code, why = ld.judge(FINAL, report, base + "\n" + pass_line("sally").replace("result=pass", "result=fail"))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.sally", why)

    def test_a_foot_class_report_adds_no_contact_note(self):
        for name in (c for c in ld.CLASSES if c not in ld.CLASS_CHECKS):
            ld.expected_report = {"class": name}
            self.assertNotIn("sally-contact", ld.judge(FINAL, {**REPORT, "class": name}, FULL_LOG)[1], repr(name))

    def test_another_class_asks_nothing_extra(self):
        feet = [c for c in ld.CLASSES if c not in ld.CLASS_CHECKS]
        self.assertEqual(feet, ["sword_and_board", "two_handed", "archer"], "only Cavalry adds checks")
        for name in feet:
            ld.expected_report = {"class": name}
            self.assertEqual(ld.judge(FINAL, {**REPORT, "class": name}, FULL_LOG)[0], 0, repr(name))

    def test_a_run_that_played_past_the_authored_list_owes_the_scale_check(self):
        rows = [wave_row(n) for n in range(7, 10)] + [wave_row(10, healthFactor=1.0), wave_row(11, healthFactor=1.05)]
        report = {**REPORT, "wavesCleared": len(rows), "waves": rows}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.scale", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("scale",), 5))[0], 0)

    def test_a_damage_factor_alone_also_owes_the_scale_check(self):
        # The game's own rule (a wave is scaled when health, damage or the tier steps moved) also holds for a roster whose health line is flat.
        rows = [wave_row(n) for n in range(7, 11)] + [wave_row(11, healthFactor=1.0, damageFactor=1.03)]
        report = {**REPORT, "wavesCleared": len(rows), "waves": rows}
        code, why = ld.judge(FINAL, report, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.scale", why)

    def test_a_run_inside_the_authored_list_asks_no_scale_check(self):
        for waves in ([], [wave_row(1, healthFactor=1.0)], [wave_row(1)], [wave_row(1, healthFactor=True)],
                      [wave_row(1, healthFactor="2")], ["x"]):
            self.assertEqual(ld.judge(FINAL, cleared(5, waves=waves + [wave_row(n) for n in range(len(waves) + 1, 6)]), FULL_LOG)[0], 0, repr(waves))

    def test_a_won_run_needs_one_row_for_each_cleared_wave(self):
        # RCA phase2 row 120: wavesCleared=5 with waves=[] gave the affix, boon and commander replays nothing to replay, and the judge passed it.
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)
        for rows in ([], REPORT["waves"][:4], REPORT["waves"] + [wave_row(6)]):
            code, why = ld.judge(FINAL, {**REPORT, "waves": rows}, FULL_LOG)
            self.assertEqual(code, 1, len(rows))
            self.assertIn("wavesCleared=5", why)
            self.assertIn(f"waves lists {len(rows)} row(s)", why)

    def test_a_lost_run_carries_rows_for_the_waves_it_cleared_and_none_for_the_one_it_lost(self):
        # SurvivalRun.ClearWave is the only writer of a row (RunStats.CloseWave), so a run lost in wave 3 after two clears lists two rows, and one lost in wave 1 lists none.
        lost = {"reason": "lost_defenders", "expect": "lost"}
        for n in (0, 2):
            self.assertEqual(ld.judge(lost, cleared(n, final=False), LOSE_LOG)[0], 0, n)
        for rows in ([wave_row(1), wave_row(2), wave_row(3)], [wave_row(1)]):
            code, why = ld.judge(lost, cleared(2, final=False, waves=rows), LOSE_LOG)
            self.assertEqual(code, 1, len(rows))
            self.assertIn("waves lists", why)

    def test_the_wave_row_rule_leaves_a_report_with_the_wrong_type_to_the_checks_that_name_it(self):
        self.assertEqual(ld.waves_count_problem({"wavesCleared": 5, "waves": "rows"}), "")
        self.assertEqual(ld.waves_count_problem({"wavesCleared": "5", "waves": []}), "")
        self.assertEqual(ld.waves_count_problem({"wavesCleared": True, "waves": [{}]}), "")

    def test_a_schema_2_report_without_a_waves_list_fails_the_judge(self):
        for waves in (None, "waves", {}, 5):
            code, why = ld.judge(FINAL, {**REPORT, "waves": waves}, FULL_LOG)
            self.assertEqual(code, 1, repr(waves))
            self.assertIn("must carry a waves list", why)
        missing = {key: value for key, value in REPORT.items() if key != "waves"}
        code, why = ld.judge(FINAL, missing, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("must carry a waves list", why)

    def test_the_deep_run_lists_scale_once(self):
        rows = [wave_row(12, healthFactor=1.1)] + [wave_row(n) for n in range(13, 17)]
        report = {**REPORT, "wavesCleared": len(rows), "waves": rows}
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
            self.assertEqual(ld.judge(FINAL, cleared(5, seed=good), FULL_LOG)[0], 0, good)
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
        report = cleared(4)
        code, why = ld.judge(self.DEEP, report, log)
        self.assertEqual(code, 0, why)
        self.assertEqual(ld.judge(self.DEEP, report, log_of(ld.DEEP_CHECKS + ("wave", "boon"), 3))[0], 1, "four waves cleared, three wave lines")
        self.assertEqual(ld.judge(self.DEEP, report, log_of(ld.DEEP_CHECKS + ("wave",), 4))[0], 1, "a pick was owed and no boon check was written")

    def test_a_lost_deep_run_passes_without_a_cleared_wave(self):
        log = log_of(ld.DEEP_CHECKS, 0)
        for reason in ld.LOSE_REASONS:
            code, why = ld.judge({"reason": reason, "expect": "deep"}, cleared(1, final=False), log)
            self.assertEqual(code, 0, why)

    def test_a_deep_run_that_stalled_or_faulted_fails(self):
        for reason in ("stalemate", "fault", "abandoned", "live_limit"):
            self.assertEqual(ld.judge({"reason": reason, "expect": "deep"}, {**REPORT, "final": False}, log_of(ld.DEEP_CHECKS, 0))[0], 1, reason)

    def test_each_deep_check_missing_fails_by_name(self):
        log = log_of(ld.DEEP_CHECKS + ("wave",), 4)
        for name in ld.DEEP_CHECKS:
            kept = "\n".join(row for row in log.splitlines() if f"long-defeat.{name} " not in row)
            code, why = ld.judge(self.DEEP, cleared(4), kept)
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

    def test_cavalry_is_accepted_now_that_classes_json_ships_it(self):
        self.assertIn("cavalry", ld.CLASSES)
        self.assertEqual(self.parse("--class", "cavalry").klass, "cavalry")
        self.assertFalse(hasattr(ld, "PLANNED_CLASSES"), "no class is planned but unplayable any more")

    def test_a_class_the_game_does_not_know_is_still_refused_by_name(self):
        stderr = io.StringIO()
        with mock.patch("sys.stderr", stderr), self.assertRaises(SystemExit):
            self.parse("--class", "wizard")
        self.assertIn("wizard", stderr.getvalue())
        self.assertIn("cavalry", stderr.getvalue(), "the message lists the classes on offer, Cavalry among them")

    def test_all_gives_each_of_four_runs_the_next_class_and_cavalry_is_the_fourth(self):
        self.assertEqual([ld.class_for_run("all", n) for n in range(4)], ["sword_and_board", "two_handed", "archer", "cavalry"])
        self.assertEqual(ld.class_for_run("all", 4), "sword_and_board", "the rotation wraps")

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
        self.assertEqual(ld.CLASSES, ("sword_and_board", "two_handed", "archer", "cavalry"))

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

    def test_the_judge_expects_the_class_each_run_armed_run_by_run(self):
        arm = self.captured_arm("--class", "all", "--runs", "4")
        for expected in ("sword_and_board", "two_handed", "archer", "cavalry"):
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
        # The same seeds and answers as ClassPlanTests.Choose_TheBotsPick_OverTheShippedFour_IncludesCavalry_AndIsPinnedToThePythonReplay: stream 3 at (0, 0), Below(4) over the
        # classes in classes.json order (Slice 11 added Cavalry, which moved every seed's class from the Below(3) draw of Slice 5).
        got = [ld.bot_class(seed) for seed in range(8)]
        self.assertEqual(got, ["sword_and_board", "archer", "cavalry", "archer", "two_handed", "cavalry", "archer", "archer"])
        self.assertEqual([ld.SplitMix64(seed, 3, 0, 0).below(4) for seed in range(8)], [0, 2, 3, 2, 1, 3, 2, 2])

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
        # A class classes.json does not ship: the game falls back, so the report names a shipped class and the armed one is not back.
        ld.expected_report = {"class": "wizard"}
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 1)
        ld.expected_report = {"class": "wizard"}
        code, why = ld.judge(FINAL, {**REPORT, "class": "wizard"}, FULL_LOG)
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
        report = cleared(0, final=False)
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

    # Slice 11: a fourth class moved every seed's unarmed class draw (Below(3) to Below(4)), and the draws here are pinned to the class this seed drew before, so the class is armed.
    CLASS = "sword_and_board"

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {"class": self.CLASS}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, picks=None, cleared=5, **more):
        picks = boon_picks(1, rerolls=1, class_id=self.CLASS) if picks is None else picks
        used = sum(max(0, len(p["offered"]) - 1) for p in picks)
        return {**REPORT, "seed": 1, "class": self.CLASS, "wavesCleared": cleared, "startRerolls": 1, "boons": picks, "rerolls": {"earned": 1 + cleared // 5, "used": used, "banked": 1 + cleared // 5 - used},
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
        report = self.report(picks=[{"pick": 1, "wave": 3, "offered": [boon_picks(1, rerolls=0, class_id=self.CLASS)[0]["offered"][0]], "taken": None, "takenBeforeWave": 0}], cleared=3,
                             rerolls={"earned": 1, "used": 0, "banked": 1})
        self.assertIsNone(ld.boon_problem(report))
        self.assertFalse(ld.owes_boon(report))
        self.assertFalse(ld.owes_reroll(report))
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 3))[0], 0)

    def test_a_run_that_cleared_two_owes_neither(self):
        ld.target_waves = 2
        report = {**REPORT, "class": self.CLASS, "wavesCleared": 2, "startRerolls": 1, "waves": [wave_row(1, 7), wave_row(2, 7)]}
        self.assertEqual(ld.judge(FINAL, report, log_of(ld.REQUIRED_CHECKS, 2))[0], 0)

    def test_a_banked_reroll_at_the_first_pick_owes_the_reroll_check(self):
        report = self.report()
        code, why = ld.judge(FINAL, report, log_of(STANDARD, 5))
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.reroll", why)
        self.assertNotIn("taom.long-defeat.boon,", why)
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD + ("reroll",), 5))[0], 0)

    def test_a_run_armed_with_no_reroll_that_spent_none_owes_no_reroll_check(self):
        picks = boon_picks(1, rerolls=0, class_id=self.CLASS)
        report = self.report(picks=picks, startRerolls=0, rerolls={"earned": 1, "used": 0, "banked": 1})
        ld.expected_report = {"startRerolls": 0, "class": self.CLASS}
        self.assertEqual(ld.judge(FINAL, report, log_of(STANDARD, 5))[0], 0)
        self.assertFalse(ld.owes_reroll(report))

    def test_boons_that_do_not_replay_fail_the_run_naming_the_draw(self):
        picks = boon_picks(1, rerolls=1, class_id=self.CLASS)
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
    SEEDS = ((33, None, "stout_yeomanry", "infantry"), (22, "archer", "marksmans_call", "archers"), (4, "archer", "steady_hand", "player"), (29, None, "gallop_of_the_eorlingas", "player"))
    FIELDED = {"infantry", "archers", "player"}
    # Slice 11: a Cavalry run's bot draws a cavalry TROOP boon here, and no soldier of that group ever stands (the lord is the player record), so it reads skip with reason=no-soldier.
    CAVALRY_TROOP_SEED = (19, None, "fleet_horses_of_the_mark", "cavalry")

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

    def test_the_manifest_names_exactly_these_seeds(self):
        line = self.seed_line()
        for seed, armed, _, _ in self.SEEDS:
            self.assertIn((f"--class {armed} " if armed else "") + f"--seed {seed}", line)
        self.assertNotIn("--seed 19", line, "the cavalry troop seed is named in the next comment line, as a skip")

    def test_a_cavalry_seed_plays_cavalry_and_takes_a_boon_that_reaches_the_lords_horse(self):
        seed, armed, boon, group = next(s for s in self.SEEDS if s[0] == 29)
        self.assertEqual(ld.bot_class(seed), "cavalry")
        taken, raw = self.first_pick(seed, "cavalry")
        self.assertEqual(taken, boon)
        row = next(b for b in raw["boons"] if b["id"] == boon)
        self.assertEqual({(e["key"], e["target"]) for e in row["effects"]}, {("moveSpeedPercent", "player")}, "a move speed boon of the player record reaches the horse through MountSpeedPercent")

    def test_the_cavalry_troop_seed_draws_cavalry_and_reads_skip_because_no_rider_stands(self):
        seed, armed, boon, group = self.CAVALRY_TROOP_SEED
        self.assertEqual(ld.bot_class(seed), "cavalry")
        taken, _ = self.first_pick(seed, "cavalry")
        self.assertEqual(taken, boon)
        self.assertNotIn(group, self.FIELDED, "no cavalry soldier is fielded, so the read for this boon is read=skip reason=no-soldier")
        text = (REPO / "tools" / "ingame_checks" / "1079.toml").read_text(encoding="utf-8")
        self.assertIn(f"seed {seed} draws cavalry and takes Fleet Horses of the Mark", text)


class BoonReadBackJudgeTests(unittest.TestCase):
    """A taken boon with a stat or health key was read back from a soldier of its target: the boon line says read=pass or read=skip, or the run never proved the boon reached anyone."""

    # Seed 5 draws wains_of_the_westfold, swift_nock and haggler_of_edoras first for its class (sword_and_board), one of which owes a read-back; the draw moves when a slice adds boon rows.
    SEED = 5

    # Slice 11: a fourth class moved every seed's unarmed class draw (Below(3) to Below(4)), and the draws here are pinned to the class this seed drew before, so the class is armed.
    CLASS = "sword_and_board"

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {"class": self.CLASS}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def card(self, reads):
        """A card of the first draw for the bot's class at Helm's Deep that does (reads) or does not owe a read-back; the draw moves when a slice adds boon rows."""
        draw = boon_picks(self.SEED, rerolls=0, class_id=self.CLASS, groups=ALL_GROUPS)[0]["offered"][0]
        owed = {r["id"]: r["reads"] for r in ld.shipped_boons()[0]}
        return next(c for c in draw if owed[c] == reads)

    def report(self, taken, cleared=5):
        picks = boon_picks(self.SEED, rerolls=0, take=taken, class_id=self.CLASS, groups=ALL_GROUPS)
        return {**REPORT, "seed": self.SEED, "fortress": "helms", "class": self.CLASS, "wavesCleared": cleared, "startRerolls": 0, "boons": picks,
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

    # Slice 11: a fourth class moved every seed's unarmed class draw (Below(3) to Below(4)), and the draws here are pinned to the class this seed drew before, so the class is armed.
    CLASS = "sword_and_board"

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {"class": self.CLASS}
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
        return {**REPORT, "seed": self.SEED, "fortress": "helms", "class": self.CLASS, "wavesCleared": len(rows), "startRerolls": 0, "waves": rows,
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
        picks = boon_picks(self.SEED, rerolls=0, class_id=self.CLASS, groups=ALL_GROUPS)
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
        ld.expected_report = {"startWave": 8, "class": self.CLASS}
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


class FortificationsLoaderTests(unittest.TestCase):
    """The Python loader against FortificationsReader (phase3-plan.md section 5): both read tools/tests/long_defeat_fort_cases.json, whose expectations were written by hand from the plan's rules table."""

    CASES = json.loads((REPO / "tools" / "tests" / "long_defeat_fort_cases.json").read_text(encoding="utf-8"))["cases"]

    def same(self, expected, got, where):
        if isinstance(expected, dict):
            self.assertIsInstance(got, dict, where)
            self.assertEqual(sorted(expected), sorted(got), where)
            for key in expected:
                self.same(expected[key], got[key], f"{where}.{key}")
        elif isinstance(expected, list):
            self.assertIsInstance(got, list, where)
            self.assertEqual(len(expected), len(got), where)
            for i, (e, g) in enumerate(zip(expected, got)):
                self.same(e, g, f"{where}[{i}]")
        elif isinstance(expected, bool) or isinstance(expected, str) or expected is None:
            self.assertEqual(expected, got, where)
        else:
            self.assertAlmostEqual(expected, got, places=6, msg=where)

    def test_every_shared_case_reads_as_the_case_file_says(self):
        self.assertGreaterEqual(len(self.CASES), 40)
        for case in self.CASES:
            shop = {} if case["absent"] else {"fortifications": case["block"]}
            plan, warnings = ld.read_fortifications(shop)
            if case["expected"] is None:
                self.assertEqual((plan, warnings), (None, 0), case["name"])
                continue
            expected = dict(case["expected"])
            self.assertEqual(warnings, expected.pop("warnings"), case["name"])
            self.same(expected, plan, case["name"])

    def test_the_shipped_shop_loads_whole_with_no_warning(self):
        shops = json.loads(ld.SHOP_FILE.read_text(encoding="utf-8"))["shops"]
        plan, warnings = ld.read_fortifications(shops["rohan"])

        self.assertEqual(warnings, 0)
        self.assertEqual([len(plan["gate"]["levels"]), len(plan["towers"]["levels"]), len(plan["volley"]["levels"])], [3, 3, 2])
        self.assertEqual(plan["towers"]["volley"]["item"], "fow_tower_arrow")
        self.assertEqual(ld.shipped_fortifications(), plan)

    def test_a_fortress_the_file_does_not_know_has_no_plan(self):
        self.assertIsNone(ld.shipped_fortifications("nowhere"))

    def test_the_offered_list_is_the_games_and_offers_the_gate_and_field_care(self):
        domain = source("Domain", "FortPlan.cs")
        self.assertIn("Offered { get; } = new(3, 2, true, 2);", domain)
        self.assertIn("None { get; } = new(0, 0, false, 0);", domain)
        self.assertEqual(ld.FORT_OFFERED, {"gate": 3, "towers": 2, "fieldCare": True, "volley": 2})

    def test_the_price_range_is_the_shops(self):
        self.assertIn("MaxPrice = 1_000_000;", source("Config", "FortificationsReader.cs"))
        self.assertEqual(ld.MAX_PRICE, 1_000_000)


class FortPriceTests(unittest.TestCase):
    """The price factor mirror: 32-bit float arithmetic as GoldFactors.Apply and RunModifiers.Factor do it (pinned in SurvivalRunFortTests.cs too)."""

    def test_a_factor_is_one_plus_the_percent_over_a_hundred_in_single_precision(self):
        self.assertEqual(ld.price_factor(0), 1.0)
        self.assertEqual(ld.price_factor(-20), ld._f32(0.8))
        self.assertEqual(ld.price_factor(-100), 0.0)
        self.assertEqual(ld.price_factor(-250), 0.0, "never below 0")

    def test_a_price_is_rounded_half_away_from_zero_and_never_below_one(self):
        self.assertEqual(ld.apply_price(180, ld.price_factor(-20)), 144)
        self.assertEqual(ld.apply_price(200, ld.price_factor(10)), 220)
        self.assertEqual(ld.apply_price(200, ld.price_factor(-100)), 1)
        self.assertEqual(ld.apply_price(150, ld.price_factor(0)), 150)

    def test_the_half_case_follows_the_games_float_not_a_double(self):
        # 0.45 as a 32-bit float is 0.449999988, so 150 * 0.45 is 67.4999982 and rounds to 67; the double 0.45 gives exactly 67.5 and would round to 68.
        # SurvivalRunFortTests.cs pins the same pair on the C# side (a -55 percent boon on the field care price of 150).
        self.assertEqual(ld.apply_price(150, ld.price_factor(-55)), 67)
        self.assertEqual(150 * 0.45, 67.5)

    def test_a_repair_price_is_the_ceil_of_the_price_times_the_share_at_least_one(self):
        self.assertEqual(list(ld.repair_prices(200, 1.0))[-1], 200)
        self.assertIn(74, ld.repair_prices(200, 0.37))
        self.assertIn(1, ld.repair_prices(200, 0.0))
        self.assertNotIn(80, ld.repair_prices(200, 0.37))

    def test_the_shipped_price_boons_are_the_five_the_plan_names(self):
        self.assertEqual(ld.shipped_price_percents(), {"haggler_of_edoras": -20, "hornblowers_bargain": 10, "wains_of_the_westfold": 10, "light_of_the_phial": 25, "the_long_silence": 15})


class PurchaseJudgeTests(unittest.TestCase):
    """Rules 8, 19 and 20 of leaderboard-plan.md, replayed over a report (phase3-plan.md Slice 2): the books, the prices through the boons' price factor, the levels in order, the volley counts."""

    PLAN = {
        "gate": {"repairPrice": 200, "levels": [[50, 200], [100, 300], [150, 450]]},
        "towers": {"troop": "t", "levels": [[4, False, 250], [8, False, 300], [8, True, 400]], "volley": {"item": "fow_tower_arrow", "arrows": 6}},
        "care": {"share": 0.75, "price": 150, "perIntermission": 1},
        "volley": {"item": "ld_elven_arrow", "originHeight": 3.0, "levels": [[350, 12, 45, 8, 90, "auto"], [300, 16, 40, 8, 90, "crosshair"]]},
    }
    OFFERS = {"militia": 180}
    PERCENTS = {"haggler": -20, "dear": 10}

    def setUp(self):
        patcher = mock.patch.dict(ld.FORT_OFFERED, {"gate": 3, "towers": 3, "fieldCare": True, "volley": 2})
        patcher.start()
        self.addCleanup(patcher.stop)

    def buy(self, wave, kind, base, price, level=0, share=0.0, id="", shares=None):
        row = {"beforeWave": wave, "kind": kind, "id": id, "level": level, "basePrice": base, "price": price, "share": share}
        if kind == "gateRepair":
            row["shares"] = [share] if shares is None else shares
        return row

    def report(self, purchases, boons=(), waves=(), **more):
        spent_on = dict.fromkeys(ld.SPENT_ON, 0)
        for p in purchases:
            if isinstance(p, dict) and p.get("kind") in ld.KIND_SINK and isinstance(p.get("price"), int):
                spent_on[ld.KIND_SINK[p["kind"]]] += p["price"]
        levels = {"gateLevel": 0, "towerLevel": 0, "volleyLevel": 0}
        for p in purchases:
            if isinstance(p, dict) and p.get("kind") in levels:
                levels[p["kind"]] = max(levels[p["kind"]], p["level"])
        return {"fortress": "helms", "purchases": list(purchases), "boons": list(boons), "waves": list(waves), "fortifications": levels,
                "gold": {"spent": sum(spent_on.values()), "spentOn": spent_on}, **more}

    def problem(self, report):
        return ld.purchase_problem(report, "helms", fort=self.PLAN, offers=self.OFFERS, percents=self.PERCENTS)

    def row(self, wave, volley=None, tower=None):
        return {"wave": wave, "volley": volley or dict(NO_VOLLEYS["volley"]), "towerVolley": tower or dict(NO_VOLLEYS["towerVolley"])}

    def test_a_run_that_bought_nothing_passes(self):
        self.assertIsNone(self.problem(self.report([])))

    def test_a_full_schedule_with_a_price_boon_replays(self):
        haggler = {"pick": 1, "taken": "haggler", "takenBeforeWave": 4}
        report = self.report([
            self.buy(1, "pack", 180, 180, id="militia"),
            self.buy(1, "volleyLevel", 350, 350, level=1),
            self.buy(1, "towerLevel", 250, 250, level=1),
            self.buy(2, "gateRepair", 200, 74, share=0.37),
            self.buy(2, "gateLevel", 200, 200, level=1),
            self.buy(2, "fieldCare", 150, 150, share=0.75),
            self.buy(4, "pack", 180, 144, id="militia"),
            self.buy(4, "gateLevel", 300, 240, level=2),
            self.buy(4, "fieldCare", 150, 120, share=0.75),
        ], boons=[haggler])

        self.assertIsNone(self.problem(report))

    def test_a_repair_charges_the_factored_price_times_the_missing_share(self):
        haggler = {"pick": 1, "taken": "haggler", "takenBeforeWave": 4}

        self.assertIsNone(self.problem(self.report([self.buy(5, "gateRepair", 200, 60, share=0.37)], boons=[haggler])))
        self.assertIn("paid 74", self.problem(self.report([self.buy(5, "gateRepair", 200, 74, share=0.37)], boons=[haggler])))
        self.assertIsNone(self.problem(self.report([self.buy(2, "gateRepair", 200, 200, share=1.0)])), "a broken gate pays all of it")

    def test_a_repair_share_is_read_to_the_three_decimals_the_report_writes(self):
        # 200 * 0.3704 is 74.08 and the game charges 75; the report writes 0.37, whose own ceil is 74. The half-thousandth either side is allowed.
        self.assertIsNone(self.problem(self.report([self.buy(2, "gateRepair", 200, 75, share=0.37)])))

    def test_a_repair_of_two_gates_is_priced_per_gate(self):
        # Slice 5 (RCA row 8): two gates of 0.371 at a repair price of 100 cost ceil(37.1) twice, 76; one gate of 0.742 would cost 75. The share is the total, the shares are the gates'.
        plan = {**self.PLAN, "gate": {"repairPrice": 100, "levels": [[50, 200]]}}
        two = self.report([self.buy(2, "gateRepair", 100, 76, share=0.742, shares=[0.371, 0.371])])
        one = self.report([self.buy(2, "gateRepair", 100, 75, share=0.742, shares=[0.371, 0.371])])

        self.assertIsNone(ld.purchase_problem(two, "helms", fort=plan, offers=self.OFFERS, percents=self.PERCENTS))
        self.assertIn("paid 75", ld.purchase_problem(one, "helms", fort=plan, offers=self.OFFERS, percents=self.PERCENTS))

    def test_a_repair_of_a_broken_and_a_damaged_gate_may_total_over_one(self):
        # A share is a gate's; three gates of 0.4 total 1.2 and are accepted, where the single-share rule refused any share above 1.
        report = self.report([self.buy(2, "gateRepair", 200, 240, share=1.2, shares=[0.4, 0.4, 0.4])])

        self.assertIsNone(self.problem(report))

    def test_a_repair_needs_a_share_for_each_gate_that_adds_up_to_its_share(self):
        self.assertIn("shares", self.problem(self.report([{**self.buy(2, "gateRepair", 200, 74, share=0.37), "shares": None}])))
        self.assertIn("shares", self.problem(self.report([self.buy(2, "gateRepair", 200, 74, share=0.37, shares=[])])))
        self.assertIn("shares", self.problem(self.report([self.buy(2, "gateRepair", 200, 74, share=0.37, shares=[0.37, 1.5])])))
        self.assertIn("shares", self.problem(self.report([self.buy(2, "gateRepair", 200, 74, share=0.37, shares=[0.0])])), "a gate that was not repaired has no share")
        self.assertIn("sum", self.problem(self.report([self.buy(2, "gateRepair", 200, 74, share=0.37, shares=[0.2, 0.2])])))

    def test_a_repair_of_the_smallest_share_the_game_sells_replays(self):
        # The game refuses a repair under 0.0005 (GateMath.MinMissingShare) and writes a share half away from zero, so the smallest written share is 0.001 at a price of 1.
        self.assertIsNone(self.problem(self.report([self.buy(2, "gateRepair", 200, 1, share=0.001)])))

    def test_a_purchase_in_the_intermission_of_a_pick_may_come_before_or_after_it(self):
        pick = {"pick": 1, "taken": "haggler", "takenBeforeWave": 4}

        self.assertIsNone(self.problem(self.report([self.buy(4, "fieldCare", 150, 150, share=0.75)], boons=[pick])), "bought before the pick")
        self.assertIsNone(self.problem(self.report([self.buy(4, "fieldCare", 150, 120, share=0.75)], boons=[pick])), "bought after it")
        self.assertIn("paid 130", self.problem(self.report([self.buy(4, "fieldCare", 150, 130, share=0.75)], boons=[pick])))
        self.assertIn("paid 120", self.problem(self.report([self.buy(3, "fieldCare", 150, 120, share=0.75)], boons=[pick])), "a pick for wave 4 cannot price wave 3")

    def test_a_drawback_boon_raises_the_prices(self):
        pick = {"pick": 1, "taken": "dear", "takenBeforeWave": 2}

        self.assertIsNone(self.problem(self.report([self.buy(2, "pack", 180, 198, id="militia")], boons=[pick])))

    def test_the_half_price_follows_the_games_single_precision_factor(self):
        pick = [{"pick": 1, "taken": "tithe", "takenBeforeWave": 2}]
        report = self.report([self.buy(3, "fieldCare", 150, 67, share=0.75)], boons=pick)

        self.assertIsNone(ld.purchase_problem(report, "helms", fort=self.PLAN, offers=self.OFFERS, percents={"tithe": -55}))
        self.assertIn("paid 68", ld.purchase_problem(self.report([self.buy(3, "fieldCare", 150, 68, share=0.75)], boons=pick), "helms", fort=self.PLAN, offers=self.OFFERS, percents={"tithe": -55}))

    def test_a_base_price_other_than_shop_jsons_is_refused(self):
        for purchase in (self.buy(1, "pack", 170, 170, id="militia"), self.buy(1, "gateLevel", 150, 150, level=1), self.buy(1, "gateRepair", 150, 150, share=1.0),
                         self.buy(1, "towerLevel", 200, 200, level=1), self.buy(1, "fieldCare", 100, 100, share=0.75), self.buy(1, "volleyLevel", 300, 300, level=1)):
            self.assertIn("basePrice", self.problem(self.report([purchase])), purchase["kind"])

    def test_a_pack_the_shop_does_not_sell_is_refused(self):
        self.assertIn("basePrice", self.problem(self.report([self.buy(1, "pack", 180, 180, id="ghost")])))

    def test_a_price_the_factor_does_not_give_is_refused(self):
        self.assertIn("paid 190", self.problem(self.report([self.buy(1, "pack", 180, 190, id="militia")])))
        self.assertIn("paid 1", self.problem(self.report([self.buy(1, "fieldCare", 150, 1, share=0.75)])))

    def test_levels_are_bought_in_order_with_no_gap(self):
        self.assertIn("in order", self.problem(self.report([self.buy(1, "gateLevel", 300, 300, level=2)])))
        self.assertIn("in order", self.problem(self.report([self.buy(1, "gateLevel", 200, 200, level=1), self.buy(1, "gateLevel", 200, 200, level=1)])))
        self.assertIsNone(self.problem(self.report([self.buy(1, "gateLevel", 200, 200, level=1), self.buy(2, "gateLevel", 300, 300, level=2), self.buy(3, "gateLevel", 450, 450, level=3)])))

    def test_a_level_beyond_the_shops_rows_is_refused(self):
        rows = [self.buy(1, "volleyLevel", 350, 350, level=1), self.buy(1, "volleyLevel", 300, 300, level=2), self.buy(1, "volleyLevel", 300, 300, level=3)]

        self.assertIn("not offered", self.problem(self.report(rows)))

    def test_field_care_is_limited_to_per_intermission_a_wave(self):
        two = [self.buy(2, "fieldCare", 150, 150, share=0.75), self.buy(2, "fieldCare", 150, 150, share=0.75)]

        self.assertIn("more than 1", self.problem(self.report(two)))
        self.assertIsNone(self.problem(self.report([two[0], self.buy(3, "fieldCare", 150, 150, share=0.75)])))

    def test_a_repair_share_must_be_above_zero_and_at_most_one(self):
        self.assertIn("share", self.problem(self.report([self.buy(1, "gateRepair", 200, 1, share=0.0)])))
        self.assertIn("share", self.problem(self.report([self.buy(1, "gateRepair", 200, 200, share=1.5)])))

    def test_the_final_levels_are_the_highest_bought(self):
        report = self.report([self.buy(1, "gateLevel", 200, 200, level=1)])
        report["fortifications"]["gateLevel"] = 2

        self.assertIn("fortifications.gateLevel", self.problem(report))

    def test_the_books_must_balance(self):
        report = self.report([self.buy(1, "pack", 180, 180, id="militia")])
        report["gold"]["spent"] = 181
        self.assertIn("gold.spent", self.problem(report))

        report = self.report([self.buy(1, "pack", 180, 180, id="militia")])
        report["gold"]["spentOn"]["gate"] = 10
        report["gold"]["spentOn"]["packs"] = 170
        report["gold"]["spent"] = 180
        self.assertIn("spentOn", self.problem(report))

    def test_a_spent_on_that_is_not_the_five_sinks_is_refused(self):
        report = self.report([])
        del report["gold"]["spentOn"]["volley"]
        self.assertIn("spentOn", self.problem(report))
        report = self.report([])
        report["gold"]["spentOn"]["packs"] = -1
        self.assertIn("spentOn", self.problem(report))

    def test_purchases_and_levels_are_required(self):
        report = self.report([])
        del report["purchases"]
        self.assertIn("purchases", self.problem(report))
        report = self.report([])
        del report["fortifications"]
        self.assertIn("fortifications", self.problem(report))

    def test_a_purchase_that_is_not_a_purchase_is_refused(self):
        self.assertIn("purchase 0", self.problem(self.report([{"kind": "gift"}])))
        self.assertIn("purchase 0", self.problem(self.report([5])))
        self.assertIn("not a number", self.problem(self.report([{**self.buy(1, "pack", 180, 180, id="militia"), "price": "180"}])))

    # ---- D31: this build offers nothing yet ----

    def test_a_kind_this_build_does_not_offer_is_refused(self):
        with mock.patch.dict(ld.FORT_OFFERED, {"gate": 1, "towers": 0, "fieldCare": False, "volley": 0}):
            self.assertIsNone(self.problem(self.report([self.buy(1, "gateLevel", 200, 200, level=1)])))
            self.assertIn("not offered", self.problem(self.report([self.buy(1, "gateLevel", 200, 200, level=1), self.buy(1, "gateLevel", 300, 300, level=2)])))
            self.assertIn("not offered", self.problem(self.report([self.buy(1, "towerLevel", 250, 250, level=1)])))
            self.assertIn("not offered", self.problem(self.report([self.buy(1, "fieldCare", 150, 150, share=0.75)])))
            self.assertIn("not offered", self.problem(self.report([self.buy(1, "volleyLevel", 350, 350, level=1)])))
            self.assertIsNone(self.problem(self.report([self.buy(1, "gateRepair", 200, 200, share=1.0)])), "the gate offered offers the repair")

    def test_the_shipped_empty_offered_refuses_every_fortification_and_passes_a_pack_only_report(self):
        with mock.patch.dict(ld.FORT_OFFERED, {"gate": 0, "towers": 0, "fieldCare": False, "volley": 0}):
            self.assertIsNone(self.problem(self.report([self.buy(1, "pack", 180, 180, id="militia")])))
            for purchase in (self.buy(1, "gateLevel", 200, 200, level=1), self.buy(1, "gateRepair", 200, 200, share=1.0), self.buy(1, "towerLevel", 250, 250, level=1),
                             self.buy(1, "fieldCare", 150, 150, share=0.75), self.buy(1, "volleyLevel", 350, 350, level=1)):
                self.assertIn("not offered", self.problem(self.report([purchase])), purchase["kind"])

    # ---- rule 20 ----

    def test_a_volley_count_before_the_first_level_is_refused(self):
        report = self.report([self.buy(3, "volleyLevel", 350, 350, level=1)], waves=[self.row(2, volley={"uses": 1, "arrows": 12, "hits": 5, "kills": 1, "withheld": 0})])

        self.assertIn("no level that fires it", self.problem(report))

    def test_volley_arrows_are_at_most_uses_times_the_level_in_force(self):
        bought = [self.buy(1, "volleyLevel", 350, 350, level=1), self.buy(2, "volleyLevel", 300, 300, level=2)]
        ok = self.report(bought, waves=[self.row(1, volley={"uses": 2, "arrows": 24, "hits": 12, "kills": 4, "withheld": 0}),
                                        self.row(2, volley={"uses": 2, "arrows": 32, "hits": 12, "kills": 4, "withheld": 1})])
        self.assertIsNone(self.problem(ok))

        too_many = self.report(bought, waves=[self.row(1, volley={"uses": 2, "arrows": 25, "hits": 12, "kills": 4, "withheld": 0})])
        self.assertIn("above uses 2 times the 12 arrows", self.problem(too_many))
        old_level = self.report(bought, waves=[self.row(1, volley={"uses": 2, "arrows": 32, "hits": 12, "kills": 4, "withheld": 0})])
        self.assertIn("above uses 2 times the 12 arrows", self.problem(old_level), "level 2 was bought for wave 2, not wave 1")

    def test_volley_kills_are_at_most_its_arrows(self):
        report = self.report([self.buy(1, "volleyLevel", 350, 350, level=1)], waves=[self.row(1, volley={"uses": 1, "arrows": 12, "hits": 12, "kills": 13, "withheld": 0})])

        self.assertIn("kills", self.problem(report))

    def test_tower_volley_counts_need_the_tower_level_that_adds_it(self):
        fired = {"uses": 2, "arrows": 12, "hits": 6, "kills": 2, "withheld": 0}
        early = self.report([self.buy(1, "towerLevel", 250, 250, level=1), self.buy(1, "towerLevel", 300, 300, level=2)], waves=[self.row(2, tower=fired)])
        self.assertIn("no level that fires it", self.problem(early))

        late = self.report([self.buy(1, "towerLevel", 250, 250, level=1), self.buy(1, "towerLevel", 300, 300, level=2), self.buy(2, "towerLevel", 400, 400, level=3)],
                           waves=[self.row(2, tower=fired), self.row(3, tower={**fired, "arrows": 13})])
        self.assertIn("wave 3", self.problem(late))
        self.assertIsNone(self.problem(self.report([self.buy(1, "towerLevel", 250, 250, level=1), self.buy(1, "towerLevel", 300, 300, level=2), self.buy(2, "towerLevel", 400, 400, level=3)],
                                                   waves=[self.row(2, tower=fired)])))

    def test_a_wave_row_without_its_volley_counts_is_refused(self):
        self.assertIn("volley", self.problem(self.report([], waves=[{"wave": 1}])))
        self.assertIn("volley", self.problem(self.report([], waves=[self.row(1, volley={"uses": 0})])))

    def test_a_wave_row_without_a_wave_number_is_refused_not_a_traceback(self):
        fired = {"uses": 1, "arrows": 12, "hits": 5, "kills": 1, "withheld": 0}
        row = {"volley": fired, "towerVolley": dict(NO_VOLLEYS["towerVolley"])}

        self.assertIn("no wave number", self.problem(self.report([self.buy(1, "volleyLevel", 350, 350, level=1)], waves=[row])))
        self.assertIn("no wave number", self.problem(self.report([], waves=[{**row, "wave": "2"}])))

    # ---- the judge ----

    def test_the_judge_fails_a_report_whose_books_do_not_replay(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        report = {**REPORT, "gold": {"spent": 5, "spentOn": {"packs": 0, "gate": 0, "towers": 0, "fieldCare": 0, "volley": 0}}}

        code, why = ld.judge(FINAL, report, FULL_LOG)

        self.assertEqual(code, 1)
        self.assertIn("gold.spent", why)
        self.assertEqual(ld.judge(FINAL, REPORT, FULL_LOG)[0], 0)


class StartGoldArgumentTests(unittest.TestCase):
    """--start-gold: the test option of phase3-plan.md D27, armed as start_gold and echoed back in the report's startGold."""

    def setUp(self):
        ld.target_waves = 5
        ld.tree_head = HEAD
        ld.expected_report = {}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def parse(self, *argv):
        return ld.build_parser().parse_known_args(list(argv))[0]

    def captured_arm(self, *argv):
        return PhaseTwoArmTests.captured_arm(self, *argv)

    def test_start_gold_defaults_to_none_and_takes_zero_to_a_hundred_thousand(self):
        self.assertIsNone(self.parse().start_gold)
        self.assertEqual(self.parse("--start-gold", "0").start_gold, 0)
        self.assertEqual(self.parse("--start-gold", "100000").start_gold, 100000)
        for bad in ("-1", "100001", "x", "1.5"):
            with mock.patch("sys.stderr", new_callable=io.StringIO), self.assertRaises(SystemExit, msg=bad):
                self.parse("--start-gold", bad)

    def test_the_flag_reaches_the_arm_text_and_a_zero_is_armed(self):
        text, _ = self.captured_arm("--start-gold", "5000")("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("start_gold=5000\n", text)
        text, _ = self.captured_arm("--start-gold", "0")("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("start_gold=0\n", text)
        text, _ = self.captured_arm()("E:/out", SimpleNamespace(wall_limit=None))
        self.assertNotIn("start_gold", text)

    def test_the_arm_key_and_its_range_are_the_games(self):
        plan = source("Autorun", "LongDefeatAutorunPlan.cs")
        self.assertIn('case "start_gold":', plan)
        self.assertIn(("start_gold", 5000), ld.arm_pairs(self.parse("--start-gold", "5000"), "out", 3900))
        self.assertIn("MaxStartGold = 100_000;", source("Domain", "RunOptions.cs"))

    def test_the_report_must_echo_the_armed_start_gold(self):
        self.captured_arm("--start-gold", "5000", "--start-rerolls", "0")
        ld.tree_head = HEAD
        self.assertEqual(ld.expected_report["startGold"], 5000)

        armed = {**REPORT, "startRerolls": 0}
        self.assertEqual(ld.judge(FINAL, {**armed, "startGold": 5000}, FULL_LOG)[0], 0)
        for report in (armed, {**armed, "startGold": 600}, {**armed, "startGold": "5000"}, {**armed, "startGold": True}):
            code, why = ld.judge(FINAL, report, FULL_LOG)
            self.assertEqual(code, 1, report.get("startGold"))
            self.assertIn("startGold", why)

    def test_a_run_armed_with_no_start_gold_must_report_none(self):
        self.captured_arm("--start-rerolls", "0")
        ld.tree_head = HEAD
        self.assertNotIn("startGold", ld.expected_report)

        armed = {**REPORT, "startRerolls": 0}
        self.assertEqual(ld.judge(FINAL, {**armed, "startGold": None}, FULL_LOG)[0], 0)
        self.assertEqual(ld.judge(FINAL, armed, FULL_LOG)[0], 0, "a report with no key at all is a null one")
        code, why = ld.judge(FINAL, {**armed, "startGold": 5000}, FULL_LOG)
        self.assertEqual(code, 1)
        self.assertIn("startGold", why)

    def test_the_report_writes_start_gold(self):
        self.assertIn('["startGold"]', source("Diagnostics", "LongDefeatRunReport.cs"))


class FieldCareJudgeTests(unittest.TestCase):
    """Phase 3 Slice 4: field care is offered, so the report may list it and the judge owes its check line (phase3-plan.md section 6, D31)."""

    # LongDefeatFortChecks.FieldCare's detail, as LongDefeatDirectorCareTests pins it.
    DETAIL = "c=1 share=0.75 count=150 before=0.600 after=0.900 expected=0.900 reread=0.900 paid=150 purse=150"
    HEAD_LINE = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.field-care result={} "

    def setUp(self):
        ld.target_waves = 2
        ld.tree_head = HEAD
        ld.expected_report = {"startGold": 5000}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, care=True, **more):
        bought = [{"beforeWave": 2, "kind": "fieldCare", "id": "", "level": 0, "basePrice": 150, "price": 150, "share": 0.75}] if care else []
        spent = 150 if care else 0
        return {**REPORT, "wavesCleared": 2, "wavesFelled": 0, "startGold": 5000, "waves": [wave_row(1), wave_row(2)], "purchases": bought,
                "gold": {"spent": spent, "spentOn": {"packs": 0, "gate": 0, "towers": 0, "fieldCare": spent, "volley": 0}}, **more}

    def log(self, *extra):
        return log_of(ld.REQUIRED_CHECKS, 2) + "\n" + "\n".join(extra)

    def test_the_build_offers_the_gate_and_field_care(self):
        self.assertEqual(ld.FORT_OFFERED, {"gate": 3, "towers": 2, "fieldCare": True, "volley": 2}, "tower levels 1 and 2 joined in Slice 7; level 3, the tower volley, waits for Slice 8")

    def test_a_report_that_bought_care_replays_from_the_shop(self):
        self.assertIsNone(ld.purchase_problem(self.report()))

    def test_every_other_kind_is_still_refused(self):
        # Tower level 3 is the tower volley (Slice 8): levels 1 and 2 are for sale, the third is not.
        bought = [{"beforeWave": n, "kind": "towerLevel", "id": "", "level": n, "basePrice": price, "price": price, "share": 0.0} for n, price in ((1, 250), (2, 300), (3, 400))]
        report = self.report(care=False, purchases=bought, gold={"spent": 950, "spentOn": {"packs": 0, "gate": 0, "towers": 950, "fieldCare": 0, "volley": 0}},
                             fortifications={"gateLevel": 0, "towerLevel": 3, "volleyLevel": 0})
        self.assertIn("not offered", ld.purchase_problem(report) or "")

    def test_a_run_that_bought_care_owes_the_field_care_check(self):
        self.assertTrue(ld.owes_field_care(self.report()))
        code, why = ld.judge(FINAL, self.report(), self.log())
        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.field-care", why)

        code, why = ld.judge(FINAL, self.report(), self.log(self.HEAD_LINE.format("pass") + self.DETAIL))
        self.assertEqual(code, 0, why)
        self.assertIn("field-care", why)

    def test_a_skip_does_not_pay_the_debt(self):
        skipped = self.HEAD_LINE.format("skip") + self.DETAIL + " reason=too-little-missing"

        code, why = ld.judge(FINAL, self.report(), self.log(skipped))

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.field-care", why)

    def test_a_failed_line_fails_the_run(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.HEAD_LINE.format("pass") + self.DETAIL, self.HEAD_LINE.format("fail") + self.DETAIL))

        self.assertEqual(code, 1)
        self.assertIn("failed check", why)

    def test_a_run_that_bought_none_owes_none(self):
        self.assertFalse(ld.owes_field_care(self.report(care=False)))
        self.assertEqual(ld.judge(FINAL, self.report(care=False), self.log())[0], 0)

    def test_the_debt_reads_the_purchases_and_nothing_else(self):
        self.assertFalse(ld.owes_field_care({}))
        self.assertFalse(ld.owes_field_care({"purchases": "x"}))
        self.assertFalse(ld.owes_field_care({"purchases": [5, {"kind": "pack"}]}))
        self.assertTrue(ld.owes_field_care({"purchases": [{"kind": "pack"}, {"kind": "fieldCare"}]}))

    def test_a_lose_run_is_not_asked_for_it(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(), "final": False, "wavesCleared": 1, "waves": [wave_row(1)]}

        code, why = ld.judge(lost, report, LOSE_LOG)

        self.assertEqual(code, 0, why)

    def test_the_manifest_row_reads_the_line_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.field-care")
        self.assertEqual(row.absence, "not-reached")
        head = self.HEAD_LINE
        self.assertEqual(iv.judge_segment(row, [(None, 1, head.format("pass") + self.DETAIL)], []).verdict, iv.PASS)
        self.assertEqual(iv.judge_segment(row, [(None, 1, head.format("fail") + self.DETAIL)], []).verdict, iv.FAIL)
        self.assertEqual(iv.judge_segment(row, [(None, 1, head.format("skip") + self.DETAIL + " reason=too-little-missing")], []).verdict, iv.NOT_REACHED, "a heal of nothing proves nothing")
        self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED)

    def test_the_manifest_names_the_proof_run(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.field-care")
        self.assertIn("--waves 2 --garrison-scale 1.3 --start-gold 5000", row.needs)

    def test_the_line_is_the_one_the_csharp_check_formats(self):
        checks = source("Diagnostics", "LongDefeatFortChecks.cs")
        self.assertIn('FieldCareId = "taom.long-defeat.field-care"', source("Diagnostics", "LongDefeatChecks.cs"))
        for key in ("c=", "share=", "count=", "before=", "after=", "expected=", "reread=", "paid=", "purse=", "reason=too-little-missing"):
            self.assertIn(key, checks, key)
        self.assertRegex(self.DETAIL, r"^c=[0-9]+ share=[0-9.]+ count=[0-9]+ before=[0-9.]+ after=[0-9.]+ expected=[0-9.]+ reread=([0-9.]+|none) paid=[0-9]+ purse=[0-9]+$")
        self.assertIn('"c" + heal.N', source("LongDefeatDirector.Fort.cs"))

    def test_the_wrapper_names_the_proof_run(self):
        self.assertIn("--waves 2 --garrison-scale 1.3 --start-gold 5000", (REPO / "tools" / "run_long_defeat.py").read_text(encoding="utf-8"))


class GateJudgeTests(unittest.TestCase):
    """Phase 3 Slice 5: the gate is offered, so the report may list a level and a repair and the judge owes their check lines (phase3-plan.md section 6, D31)."""

    # The details LongDefeatGateChecks formats, as LongDefeatGateChecksTests pins them.
    LEVEL = "l=1 factor=1.50 gates=1 max=22500.0/22500.0 hp=11000.0->16499.0 share=0.733->0.733 index=1->1"
    REPAIR = "w=2 path=broken gates=1 hp=15000.0/15000.0 index=0 disabled=false closed=true open=false near=0 hidden_down=true reset=found"
    ATTACKED = "w=2 path=broken start=15000.0 now=12000.0 lost=3000.0 fell=false through=0 samples=12"
    HEAD_LINE = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.{} result={} "

    def setUp(self):
        ld.target_waves = 3
        ld.tree_head = HEAD
        ld.expected_report = {"startGold": 5000}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, level=True, repair=True, waves=3, **more):
        bought = []
        if repair:
            bought.append({"beforeWave": 2, "kind": "gateRepair", "id": "", "level": 0, "basePrice": 200, "price": 200, "share": 1.0, "shares": [1.0]})
        if level:
            bought.append({"beforeWave": 2, "kind": "gateLevel", "id": "", "level": 1, "basePrice": 200, "price": 200, "share": 0.0})
        spent = sum(p["price"] for p in bought)
        return {**REPORT, "wavesCleared": waves, "wave": waves, "wavesFelled": 0, "startGold": 5000, "waves": [wave_row(n) for n in range(1, waves + 1)], "purchases": bought,
                "gold": {"spent": spent, "spentOn": {"packs": 0, "gate": spent, "towers": 0, "fieldCare": 0, "volley": 0}},
                "fortifications": {"gateLevel": 1 if level else 0, "towerLevel": 0, "volleyLevel": 0}, **more}

    def log(self, *extra):
        return log_of(ld.REQUIRED_CHECKS, 3) + "\n" + "\n".join(extra)

    def line(self, name, result, detail):
        return self.HEAD_LINE.format(name, result) + detail

    def passes(self):
        return [self.line("gate-level", "pass", self.LEVEL), self.line("gate-repair", "pass", self.REPAIR), self.line("gate-attacked", "pass", self.ATTACKED)]

    def test_the_debts_follow_the_purchases(self):
        both = self.report()
        self.assertTrue(ld.owes_gate_level(both))
        self.assertTrue(ld.owes_gate_repair(both))
        self.assertTrue(ld.owes_gate_attacked(both))
        self.assertFalse(ld.owes_gate_repair(self.report(repair=False)))
        self.assertFalse(ld.owes_gate_attacked(self.report(repair=False)))
        self.assertFalse(ld.owes_gate_level(self.report(level=False)))

    def test_the_attack_is_owed_only_when_a_wave_after_the_repair_was_played(self):
        # The repair is before wave 2; a run that ended with wave 1 cleared never met the repaired gate.
        self.assertFalse(ld.owes_gate_attacked(self.report(waves=1)))
        self.assertTrue(ld.owes_gate_attacked(self.report(waves=1, wave=2)), "ended in wave 2: the run ends in the wave the gate was attacked in")
        self.assertTrue(ld.owes_gate_attacked(self.report(waves=2)))

    def test_the_debts_read_the_purchases_and_nothing_else(self):
        for bad in ({}, {"purchases": "x"}, {"purchases": [5, {"kind": "pack"}]}):
            self.assertFalse(ld.owes_gate_level(bad))
            self.assertFalse(ld.owes_gate_repair(bad))
            self.assertFalse(ld.owes_gate_attacked(bad))
        self.assertFalse(ld.owes_gate_attacked({"purchases": [{"kind": "gateRepair", "beforeWave": "2"}], "wave": 3}))

    def test_a_report_that_bought_the_gate_replays_from_the_shop(self):
        self.assertIsNone(ld.purchase_problem(self.report()))

    def test_a_run_with_all_three_lines_passes(self):
        code, why = ld.judge(FINAL, self.report(), self.log(*self.passes()))

        self.assertEqual(code, 0, why)
        for name in ("gate-level", "gate-repair", "gate-attacked"):
            self.assertIn(name, why)

    def test_each_missing_line_fails_the_run_by_its_name(self):
        for missing, lines in (("gate-level", [1, 2]), ("gate-repair", [0, 2]), ("gate-attacked", [0, 1])):
            code, why = ld.judge(FINAL, self.report(), self.log(*[self.passes()[i] for i in lines]))
            self.assertEqual(code, 1, missing)
            self.assertIn("taom.long-defeat." + missing, why)

    def test_a_skip_does_not_pay_the_debt_and_a_no_blow_skip_says_why(self):
        skipped = self.line("gate-attacked", "skip", self.ATTACKED + " reason=no-blow")

        code, why = ld.judge(FINAL, self.report(), self.log(self.passes()[0], self.passes()[1], skipped))

        self.assertEqual(code, 1)
        self.assertIn("gate-attacked", why)
        self.assertIn("no-blow", why)

    def test_a_failed_line_fails_the_run(self):
        code, why = ld.judge(FINAL, self.report(), self.log(*self.passes(), self.line("gate-attacked", "fail", self.ATTACKED.replace("through=0", "through=2"))))

        self.assertEqual(code, 1)
        self.assertIn("failed check", why)

    def test_a_run_that_bought_no_gate_owes_none(self):
        code, why = ld.judge(FINAL, self.report(level=False, repair=False), self.log())

        self.assertEqual(code, 0, why)

    def test_a_lose_run_is_not_asked_for_them(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(), "final": False, "wavesCleared": 1, "waves": [wave_row(1)]}

        code, why = ld.judge(lost, report, LOSE_LOG)

        self.assertEqual(code, 0, why)

    def test_the_manifest_rows_read_the_lines_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        lines = {"gate-level": self.LEVEL, "gate-repair": self.REPAIR, "gate-attacked": self.ATTACKED}
        for name, detail in lines.items():
            row = next(c for c in manifest.checks if c.id == "taom.long-defeat." + name)
            self.assertEqual(row.absence, "not-reached", name)
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line(name, "pass", detail))], []).verdict, iv.PASS, name)
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line(name, "fail", detail))], []).verdict, iv.FAIL, name)
            self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED, name)
        attacked = next(c for c in manifest.checks if c.id == "taom.long-defeat.gate-attacked")
        self.assertEqual(iv.judge_segment(attacked, [(None, 1, self.line("gate-attacked", "skip", self.ATTACKED + " reason=no-blow"))], []).verdict, iv.NOT_REACHED)
        self.assertNotEqual(iv.judge_segment(attacked, [(None, 1, self.line("gate-attacked", "pass", self.ATTACKED.replace("through=0", "through=2")))], []).verdict, iv.PASS,
                            "a repaired gate with attackers behind it is not a pass")

    def test_the_manifest_rows_read_a_fortress_with_two_gates_too(self):
        # With two or more visible gates the C# checks key every number g0., g1. and so on (LongDefeatGateChecks.Prefix); the pass patterns must still match them.
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        level = ("l=1 factor=1.50 gates=2 g0.max=22500.0/22500.0 g0.hp=11000.0->16499.0 g0.share=0.733->0.733 g0.index=1->1 "
                 "g1.max=22500.0/22500.0 g1.hp=15000.0->22499.0 g1.share=1.000->1.000 g1.index=0->0")
        repair = ("w=2 path=broken gates=2 g0.hp=15000.0/15000.0 g0.index=0 g0.disabled=false g0.closed=true g0.open=false g0.near=0 "
                  "g1.hp=15000.0/15000.0 g1.index=0 g1.disabled=false g1.closed=true g1.open=false g1.near=1 hidden_down=true reset=found")
        for name, detail in (("gate-level", level), ("gate-repair", repair)):
            row = next(c for c in manifest.checks if c.id == "taom.long-defeat." + name)
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line(name, "pass", detail))], []).verdict, iv.PASS, name)

    def test_a_gate_attacked_line_that_measured_nothing_is_not_a_pass(self):
        # samples= counts only the samples that measured a plane; through=0 over none proves nothing.
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        attacked = next(c for c in manifest.checks if c.id == "taom.long-defeat.gate-attacked")
        unmeasured = self.ATTACKED.replace("samples=12", "samples=0")
        self.assertNotEqual(iv.judge_segment(attacked, [(None, 1, self.line("gate-attacked", "pass", unmeasured))], []).verdict, iv.PASS)
        self.assertEqual(iv.judge_segment(attacked, [(None, 1, self.line("gate-attacked", "pass", self.ATTACKED))], []).verdict, iv.PASS)

    def test_the_manifest_names_the_proof_run(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        for name in ("gate-level", "gate-repair", "gate-attacked"):
            row = next(c for c in manifest.checks if c.id == "taom.long-defeat." + name)
            self.assertIn("--waves 3 --garrison-scale 1.3 --start-gold 5000", row.needs, name)

    def test_the_lines_are_the_ones_the_csharp_checks_format(self):
        checks = source("Diagnostics", "LongDefeatGateChecks.cs")
        ids = source("Diagnostics", "LongDefeatChecks.cs")
        for const, name in (("GateLevelId", "gate-level"), ("GateRepairId", "gate-repair"), ("GateAttackedId", "gate-attacked"), ("FortPointsId", "fort-points")):
            self.assertIn(f'{const} = "taom.long-defeat.{name}"', ids, name)
        for key in ("l=", "factor=", "gates=", "max=", "hp=", "share=", "index=", "w=", "path=", "disabled=", "closed=", "open=", "near=", "hidden_down=", "reset=",
                    "start=", "now=", "lost=", "fell=", "through=", "samples=", "ended=run", "reason=no-blow", "points=", "failed=", "reason=none-recorded"):
            self.assertIn(key, checks, key)
        self.assertRegex(self.LEVEL, r"^l=[0-9]+ factor=[0-9.]+ gates=[0-9]+ max=[0-9.]+/[0-9.]+ hp=[0-9.]+->[0-9.]+ share=[0-9.]+->[0-9.]+ index=[0-9]+->[0-9]+$")
        self.assertRegex(self.REPAIR, r"^w=[0-9]+ path=(broken|damaged) gates=[0-9]+ hp=[0-9.]+/[0-9.]+ index=[0-9]+ disabled=(true|false) closed=(true|false) open=(true|false) near=[0-9]+ hidden_down=(true|false) reset=(found|missing)$")
        self.assertRegex(self.ATTACKED, r"^w=[0-9]+ path=(broken|damaged) start=[0-9.]+ now=[0-9.]+ lost=-?[0-9.]+ fell=(true|false) through=[0-9]+ samples=[0-9]+$")
        director = source("LongDefeatDirector.Gates.cs")
        for key in ('"l" + level', '"w" + wave', '"w" + watch.Wave'):
            self.assertIn(key, director, key)

    def test_the_wrapper_names_the_proof_run(self):
        self.assertIn("--waves 3 --garrison-scale 1.3 --start-gold 5000", (REPO / "tools" / "run_long_defeat.py").read_text(encoding="utf-8"))


class FortPointsJudgeTests(unittest.TestCase):
    """The fort-points check (Slice 5): a skip while the fortress records no point, a pass line owed once it does."""

    def fortresses(self, helms):
        return {"fortresses": [{"id": "helms", **helms}, {"id": "other", "towers": [{"id": "t", "posts": [{}]}]}]}

    def test_a_fortress_with_no_recorded_point_owes_nothing(self):
        for helms in ({}, {"towers": []}, {"volley": {"origins": []}}, {"towers": "x"}, {"volley": 3}):
            self.assertFalse(ld.recorded_fort_points("helms", self.fortresses(helms)), helms)

    def test_a_tower_or_a_volley_origin_is_a_recorded_point(self):
        self.assertTrue(ld.recorded_fort_points("helms", self.fortresses({"towers": [{"id": "hornburg", "posts": [{"x": 1}], "volley": []}]})))
        self.assertTrue(ld.recorded_fort_points("helms", self.fortresses({"towers": [{"id": "hornburg", "volley": [{"x": 1}]}]})))
        self.assertTrue(ld.recorded_fort_points("helms", self.fortresses({"volley": {"origins": [{"x": 1, "y": 2, "z": 3}]}})))

    def test_the_shipped_helms_records_none_yet(self):
        self.assertFalse(ld.recorded_fort_points("helms"), "Mike records the points on 2026-11-06")

    def test_the_debt_is_a_pass_line_and_a_skip_does_not_pay_it(self):
        report = {"fortress": "helms"}
        self.assertFalse(ld.owes_fort_points(report, {"fortresses": [{"id": "helms"}]}))
        self.assertTrue(ld.owes_fort_points(report, {"fortresses": [{"id": "helms", "volley": {"origins": [{"x": 1}]}}]}))


class VolleyJudgeTests(unittest.TestCase):
    """Phase 3 Slice 6: the Elven Volley is offered, so the report may list its levels and a use, and the judge owes the volley check lines (phase3-plan.md section 6, D31)."""

    # The details LongDefeatVolleyChecks formats, as LongDefeatVolleyChecksTests pins them.
    AUTO = ("w=1 use=1 source=elven mode=auto arrows=12 shots=12 candidates=27 distinct=12 spawned=0 hits=8 blocked=1 friendly=0 cancelled=0 kills=3 "
            "affector_null=0 affector_side=0 owner=fow_rohan_eastfold_bowman mult=1.00 base_speed=45.0 impact_speed=30.0 window=6.0")
    CROSSHAIR = ("w=2 use=1 source=elven mode=crosshair aim=bot ray_gap=1.2 arrows=16 shots=16 candidates=40 distinct=16 spawned=0 hits=9 blocked=0 friendly=0 cancelled=1 kills=4 "
                 "affector_null=0 affector_side=0 owner=fow_rohan_eastfold_bowman mult=1.00 base_speed=45.0 impact_speed=30.0 window=6.0")
    HEAD_LINE = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.{} result={} "

    def setUp(self):
        ld.target_waves = 3
        ld.tree_head = HEAD
        ld.expected_report = {"startGold": 5000}
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})

    def report(self, levels=2, waves=3, fire=True, **more):
        bought = [{"beforeWave": 1, "kind": "volleyLevel", "id": "", "level": 1, "basePrice": 350, "price": 350, "share": 0.0}]
        if levels >= 2:
            bought.append({"beforeWave": 2, "kind": "volleyLevel", "id": "", "level": 2, "basePrice": 300, "price": 300, "share": 0.0})
        spent = sum(p["price"] for p in bought)
        rows = []
        for n in range(1, waves + 1):
            arrows = 16 if n >= 2 and levels >= 2 else 12
            volley = {"uses": 1, "arrows": arrows, "hits": 8, "kills": 3, "withheld": 0} if fire else NO_VOLLEYS["volley"]
            rows.append(wave_row(n, volley=volley))
        return {**REPORT, "wavesCleared": waves, "wave": waves, "wavesFelled": 0, "startGold": 5000, "waves": rows, "purchases": bought,
                "gold": {"spent": spent, "spentOn": {"packs": 0, "gate": 0, "towers": 0, "fieldCare": 0, "volley": spent}},
                "fortifications": {"gateLevel": 0, "towerLevel": 0, "volleyLevel": levels}, **more}

    def line(self, result, detail, name="volley"):
        return self.HEAD_LINE.format(name, result) + detail

    def log(self, *extra):
        return log_of(ld.REQUIRED_CHECKS, 3) + "\n" + "\n".join(extra)

    def test_the_shipped_build_offers_both_levels_of_the_volley(self):
        self.assertEqual(ld.FORT_OFFERED, {"gate": 3, "towers": 2, "fieldCare": True, "volley": 2})

    def test_a_report_that_bought_the_volley_replays_from_the_shop(self):
        self.assertIsNone(ld.purchase_problem(self.report()))
        self.assertIsNone(ld.purchase_problem(self.report(levels=1)))

    def test_a_level_beyond_the_build_is_still_refused(self):
        report = self.report()
        report["purchases"].append({"beforeWave": 3, "kind": "volleyLevel", "id": "", "level": 3, "basePrice": 300, "price": 300, "share": 0.0})
        report["gold"]["spent"] = report["gold"]["spentOn"]["volley"] = 950
        report["fortifications"]["volleyLevel"] = 3

        self.assertIn("not offered", ld.purchase_problem(report))

    def test_the_debts_follow_the_purchases_and_the_waves_played(self):
        self.assertTrue(ld.owes_volley(self.report()))
        self.assertTrue(ld.owes_volley_crosshair(self.report()))
        self.assertFalse(ld.owes_volley_crosshair(self.report(levels=1)), "level one aims itself")
        self.assertFalse(ld.owes_volley({**self.report(), "purchases": []}))
        # Level 2 is bought before wave 2: a run that stopped after wave 1 never met it.
        self.assertFalse(ld.owes_volley_crosshair(self.report(waves=1)))
        self.assertTrue(ld.owes_volley(self.report(waves=1)))
        # Bought but the run ended before the wave it precedes: nothing was owed.
        late = self.report(waves=1)
        late["purchases"][0]["beforeWave"] = 4
        self.assertFalse(ld.owes_volley(late))

    def test_the_debts_read_the_purchases_and_nothing_else(self):
        for bad in ({}, {"purchases": "x"}, {"purchases": [5, {"kind": "pack"}]}):
            self.assertFalse(ld.owes_volley(bad))
            self.assertFalse(ld.owes_volley_crosshair(bad))
        self.assertFalse(ld.owes_volley({"purchases": [{"kind": "volleyLevel", "beforeWave": "1"}], "wave": 3}))

    def test_a_run_with_the_auto_and_the_crosshair_line_passes(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.line("pass", self.AUTO), self.line("pass", self.CROSSHAIR)))

        self.assertEqual(code, 0, why)
        self.assertIn("volley", why)

    def test_a_missing_line_fails_the_run_by_its_name(self):
        code, why = ld.judge(FINAL, self.report(), self.log())

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.volley", why)

    def test_a_volley_that_never_fired_is_caught_though_the_report_replays(self):
        # The bot arming nothing (BotActionKind.Volley with no case) leaves uses at 0, which rule 20 accepts; only the check line is owed and absent.
        report = self.report(fire=False)
        self.assertIsNone(ld.purchase_problem(report))

        code, why = ld.judge(FINAL, report, self.log())

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.volley", why)

    def test_level_two_owes_a_crosshair_line_and_an_auto_line_does_not_pay_it(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.line("pass", self.AUTO)))

        self.assertEqual(code, 1)
        self.assertIn("mode=crosshair", why)

    def test_a_level_one_run_needs_only_the_auto_line(self):
        code, why = ld.judge(FINAL, self.report(levels=1), self.log(self.line("pass", self.AUTO)))

        self.assertEqual(code, 0, why)

    def test_a_skip_does_not_pay_the_debt(self):
        cut = self.line("skip", self.AUTO + " ended=run reason=window-cut")

        code, why = ld.judge(FINAL, self.report(levels=1), self.log(cut))

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.volley", why)

    def test_a_failed_line_fails_the_run(self):
        code, why = ld.judge(FINAL, self.report(levels=1), self.log(self.line("pass", self.AUTO), self.line("fail", self.AUTO.replace("friendly=0", "friendly=1"))))

        self.assertEqual(code, 1)
        self.assertIn("failed check", why)

    def test_a_lose_run_is_not_asked_for_it(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(), "final": False, "wavesCleared": 1, "waves": [wave_row(1)]}

        code, why = ld.judge(lost, report, LOSE_LOG)

        self.assertEqual(code, 0, why)

    def test_the_manifest_row_reads_the_lines_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.volley")
        self.assertEqual(row.absence, "not-reached")
        for detail in (self.AUTO, self.CROSSHAIR, self.AUTO.replace("source=elven", "source=tower-hornburg"), self.CROSSHAIR.replace("ray_gap=1.2", "ray_gap=NaN").replace("aim=bot", "aim=person")):
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line("pass", detail))], []).verdict, iv.PASS, detail)
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line("fail", detail))], []).verdict, iv.FAIL, detail)
        self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED)
        cut = self.line("skip", self.AUTO + " ended=run reason=window-cut")
        self.assertEqual(iv.judge_segment(row, [(None, 1, cut)], []).verdict, iv.NOT_REACHED, "a window cut short proves nothing")
        uncancelled = self.AUTO.replace("friendly=0", "friendly=2")
        self.assertNotEqual(iv.judge_segment(row, [(None, 1, self.line("pass", uncancelled))], []).verdict, iv.PASS, "an uncancelled friendly hit is not a pass")
        spawned = self.AUTO.replace("spawned=0", "spawned=1")
        self.assertNotEqual(iv.judge_segment(row, [(None, 1, self.line("pass", spawned))], []).verdict, iv.PASS, "a fire that spawned an agent is not a pass")

    def test_the_manifest_names_the_proof_run_and_says_it_needs_recorded_origins(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.volley")
        self.assertIn("--waves 3 --garrison-scale 1.3 --start-gold 5000", row.needs)
        self.assertIn("volley.origins", row.needs)
        text = (REPO / "tools" / "ingame_checks" / "1079.toml").read_text(encoding="utf-8")
        self.assertIn("pressing N during a wave looses it", text)

    def test_the_line_is_the_one_the_csharp_check_formats(self):
        checks = source("Diagnostics", "LongDefeatVolleyChecks.cs")
        ids = source("Diagnostics", "LongDefeatChecks.cs")
        self.assertIn('VolleyId = "taom.long-defeat.volley"', ids)
        for key in ("w=", "use=", "source=", "mode=crosshair", "mode=auto", "aim=", "ray_gap=", "arrows=", "shots=", "candidates=", "distinct=", "spawned=", "hits=", "blocked=", "friendly=",
                    "cancelled=", "kills=", "affector_null=", "affector_side=", "owner=", "mult=", "base_speed=", "impact_speed=", "window=", "ended=", "reason=window-cut"):
            self.assertIn(key, checks, key)
        self.assertRegex(self.AUTO, r"^w=[0-9]+ use=[0-9]+ source=(elven|tower-[a-z_]+) mode=auto arrows=[0-9]+ shots=[0-9]+ candidates=[0-9]+ distinct=[0-9]+ spawned=[0-9]+ hits=[0-9]+ blocked=[0-9]+ "
                                  r"friendly=[0-9]+ cancelled=[0-9]+ kills=[0-9]+ affector_null=[0-9]+ affector_side=[0-9]+ owner=\S+ mult=[0-9.]+ base_speed=[0-9.]+ impact_speed=[0-9.]+ window=[0-9.]+$")
        director = source("LongDefeatDirector.Volley.cs")
        self.assertIn('$"{use.Source.Id}-w{use.Wave', director)

    def test_the_wrapper_names_the_proof_run(self):
        self.assertIn("--waves 3 --garrison-scale 1.3 --start-gold 5000", (REPO / "tools" / "run_long_defeat.py").read_text(encoding="utf-8"))

    def test_the_offered_mirror_is_the_one_the_game_ships(self):
        text = source("Domain", "FortPlan.cs")
        self.assertIn("new(3, 2, true, 2)", text, "FortOffer.Offered, which FORT_OFFERED mirrors")


class FortPointsHintTests(unittest.TestCase):
    """The fort-points check counts the points the game's reader accepted, so the judge names a reader that dropped what the file records."""

    def fortresses(self):
        return {"fortresses": [{"id": "helms", "towers": [{"id": "hornburg", "posts": [{"x": 1}, {"x": 2}], "volley": [{"x": 3}]}], "volley": {"origins": [{"x": 4}, {"x": 5}]}}]}

    def test_the_recorded_count_is_every_post_spot_and_origin(self):
        self.assertEqual(ld.recorded_fort_point_count("helms", self.fortresses()), 5)
        self.assertEqual(ld.recorded_fort_point_count("helms", {"fortresses": [{"id": "helms"}]}), 0)
        self.assertEqual(ld.recorded_fort_point_count("none", self.fortresses()), 0)

    def test_a_reader_that_dropped_points_is_named_and_one_that_kept_all_is_not(self):
        line = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.fort-points result=pass points={} failed=0"

        note = ld.fort_points_note({"fortress": "helms"}, line.format(3), self.fortresses())
        self.assertIn("3 of 5", note)
        self.assertEqual(ld.fort_points_note({"fortress": "helms"}, line.format(5), self.fortresses()), "")
        self.assertEqual(ld.fort_points_note({"fortress": "helms"}, "no line", self.fortresses()), "")
        self.assertEqual(ld.fort_points_note({"fortress": "helms"}, line.format(0), {"fortresses": [{"id": "helms"}]}), "", "nothing recorded, nothing dropped")

    def test_a_skip_for_none_recorded_while_the_file_records_points_names_the_reader(self):
        report = {**REPORT, "fortress": "helms"}
        skip = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.fort-points result=skip points=0 reason=none-recorded"
        with mock.patch.object(ld, "recorded_fort_points", return_value=True):
            ld.target_waves = 5
            ld.tree_head = HEAD
            ld.expected_report = {}
            self.addCleanup(setattr, ld, "target_waves", None)
            self.addCleanup(setattr, ld, "tree_head", None)
            code, why = ld.judge(FINAL, {**report, "wavesCleared": 5, "waves": [wave_row(n) for n in range(1, 6)]}, FULL_LOG + "\n" + skip)

        self.assertEqual(code, 1)
        self.assertIn("fort-points", why)
        self.assertIn("reader accepted none", why)


class PostsJudgeTests(unittest.TestCase):
    """Phase 3 Slice 7: tower levels 1 and 2 are offered, so the report may list them and the judge owes the posts check lines (phase3-plan.md section 6, D31, spike S6)."""

    # The detail LongDefeatPostsChecks formats, as LongDefeatPostsChecksTests pins it (tower=hornburg w=2 ... short=0 failed=0). Every word is name=value: the game's OutcomeLog.SanitiseDetail glues a word with no
    # '=' to the value before it ("held=4 of 4" was logged as "held=4_of_4"), so a pattern written for the raw detail could never match a real log.
    PASS = ("tower=hornburg w=1 level=1 variant=A formation=4 posts=8 expected=4 manned=4 held=4 sampled=4 max_drift=0.4 max_dz=0.2 shots=15 split=0 out=0 short=0 failed=0")
    PASS2 = PASS.replace("w=1 level=1", "w=2 level=2")
    HEAD_LINE = "[2026-11-06 10:00:00] [INFO] [Check] id=taom.long-defeat.{} result={} "

    def setUp(self):
        ld.target_waves = 2
        ld.tree_head = HEAD
        ld.expected_report = {"startGold": 5000}
        ld.posts_variant = None
        self.addCleanup(setattr, ld, "target_waves", None)
        self.addCleanup(setattr, ld, "tree_head", None)
        self.addCleanup(setattr, ld, "expected_report", {})
        self.addCleanup(setattr, ld, "posts_variant", None)

    def report(self, levels=2, waves=2, **more):
        bought = [{"beforeWave": 1, "kind": "towerLevel", "id": "", "level": 1, "basePrice": 250, "price": 250, "share": 0.0}]
        if levels >= 2:
            bought.append({"beforeWave": 2, "kind": "towerLevel", "id": "", "level": 2, "basePrice": 300, "price": 300, "share": 0.0})
        spent = sum(p["price"] for p in bought)
        return {**cleared(waves), "wave": waves, "wavesFelled": 0, "startGold": 5000, "purchases": bought,
                "gold": {"spent": spent, "spentOn": {"packs": 0, "gate": 0, "towers": spent, "fieldCare": 0, "volley": 0}},
                "fortifications": {"gateLevel": 0, "towerLevel": levels, "volleyLevel": 0}, **more}

    @staticmethod
    def sanitise(detail):
        """OutcomeLog.SanitiseDetail: whitespace-separated words, a word with no '=' joined to the text before it with '_'."""
        out = ""
        for word in detail.split():
            if out:
                out += " " if "=" in word[1:] else "_"
            out += word
        return out

    def line(self, result, detail, name="posts"):
        return self.HEAD_LINE.format(name, result) + self.sanitise(detail)

    def passes(self, variant="A"):
        """The two pass lines a bot run with both tower levels owes: level 1 in wave 1 and level 2 in wave 2."""
        return [self.line("pass", d.replace("variant=A", f"variant={variant}")) for d in (self.PASS, self.PASS2)]

    def log(self, *extra):
        return log_of(ld.REQUIRED_CHECKS, 2) + "\n" + "\n".join(extra)

    def test_the_shipped_build_offers_two_tower_levels(self):
        self.assertEqual(ld.FORT_OFFERED["towers"], 2)

    def test_a_report_that_bought_both_tower_levels_replays_from_the_shop(self):
        self.assertIsNone(ld.purchase_problem(self.report()))
        self.assertIsNone(ld.purchase_problem(self.report(levels=1)))

    def test_the_towers_volley_level_is_still_refused(self):
        report = self.report()
        report["purchases"].append({"beforeWave": 3, "kind": "towerLevel", "id": "", "level": 3, "basePrice": 400, "price": 400, "share": 0.0})
        report["gold"]["spent"] = report["gold"]["spentOn"]["towers"] = 950
        report["fortifications"]["towerLevel"] = 3

        self.assertIn("not offered", ld.purchase_problem(report))

    def test_the_debt_follows_the_purchases_and_the_waves_played(self):
        self.assertTrue(ld.owes_posts(self.report()))
        self.assertFalse(ld.owes_posts({**self.report(), "purchases": []}))
        late = self.report(waves=1)
        late["purchases"][0]["beforeWave"] = 4
        self.assertFalse(ld.owes_posts(late), "bought for a wave the run never reached")
        for bad in ({}, {"purchases": "x"}, {"purchases": [5, {"kind": "pack"}]}, {"purchases": [{"kind": "towerLevel", "beforeWave": "1"}], "wave": 3}):
            self.assertFalse(ld.owes_posts(bad))

    def test_a_run_with_the_posts_pass_passes(self):
        code, why = ld.judge(FINAL, self.report(), self.log(*self.passes()))

        self.assertEqual(code, 0, why)
        self.assertIn("posts", why)

    def test_the_second_level_owes_a_level_two_pass(self):
        self.assertTrue(ld.owes_posts_level2(self.report()))
        self.assertFalse(ld.owes_posts_level2(self.report(levels=1)))
        late = self.report(waves=1)
        late["purchases"][1]["beforeWave"] = 4
        self.assertFalse(ld.owes_posts_level2(late), "bought for a wave the run never reached")
        for bad in ({}, {"purchases": "x"}, {"purchases": [{"kind": "towerLevel", "beforeWave": 1, "level": "2"}], "wave": 3}):
            self.assertFalse(ld.owes_posts_level2(bad))

    def test_a_level_one_pass_alone_does_not_pay_a_level_two_buy(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.line("pass", self.PASS)))

        self.assertEqual(code, 1)
        self.assertIn("level=2", why)

    def test_a_level_two_skip_does_not_pay_it_either(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.line("pass", self.PASS), self.line("skip", self.PASS2 + " reason=short-wave")))

        self.assertEqual(code, 1)
        self.assertIn("level=2", why)

    def test_a_run_that_bought_only_level_one_owes_only_level_one(self):
        code, why = ld.judge(FINAL, self.report(levels=1), self.log(self.line("pass", self.PASS)))

        self.assertEqual(code, 0, why)

    def test_a_run_that_bought_towers_and_has_no_pass_fails_by_name(self):
        code, why = ld.judge(FINAL, self.report(), self.log())

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.posts", why)

    def test_a_skip_does_not_pay_the_debt(self):
        code, why = ld.judge(FINAL, self.report(), self.log(self.line("skip", self.PASS + " reason=short-wave")))

        self.assertEqual(code, 1)
        self.assertIn("taom.long-defeat.posts", why)

    def test_a_failed_posts_line_fails_the_run(self):
        code, why = ld.judge(FINAL, self.report(), self.log(*self.passes(), self.line("fail", self.PASS.replace("shots=15", "shots=0"))))

        self.assertEqual(code, 1)
        self.assertIn("failed check", why)

    def towerless(self):
        return {**self.report(), "purchases": [], "gold": {"spent": 0, "spentOn": {"packs": 0, "gate": 0, "towers": 0, "fieldCare": 0, "volley": 0}},
                "fortifications": {"gateLevel": 0, "towerLevel": 0, "volleyLevel": 0}}

    def test_a_run_that_bought_no_tower_owes_no_posts_line(self):
        self.assertEqual(ld.judge(FINAL, self.towerless(), self.log())[0], 0)

    def test_an_armed_variant_with_no_tower_bought_fails_by_name(self):
        ld.posts_variant = "B"

        code, why = ld.judge(FINAL, self.towerless(), self.log())

        self.assertEqual(code, 1)
        self.assertIn("never tried", why)
        self.assertIn("--posts-variant B", why)
        lost = {"reason": "lost_defenders", "expect": "lost"}
        lose_report = {**self.report(), "final": False, "wavesCleared": 1, "waves": [wave_row(1)]}
        self.assertEqual(ld.judge(lost, lose_report, LOSE_LOG)[0], 0, "a run built to lose buys no tower and is not asked for the variant")

    def test_main_arms_the_variant_it_was_given_and_clears_one_left_in_the_shell(self):
        keep = {name: getattr(runner, name) for name in ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for", "handle_dialogs", "show_results")}
        with mock.patch.dict(os.environ, {ld.POSTS_VARIANT_ENV: "C"}), mock.patch.multiple(runner, main=lambda rest: 0, **keep):
            ld.main(["--waves", "2"])
            self.assertNotIn(ld.POSTS_VARIANT_ENV, os.environ, "a variable left in the shell would arm a variant the judge never checks")
            ld.main(["--waves", "2", "--posts-variant", "b"])
            self.assertEqual(os.environ[ld.POSTS_VARIANT_ENV], "B")

    def test_the_pass_fixture_is_the_detail_the_game_tests_pin(self):
        pinned = (REPO / "TAOM.Tests" / "Features" / "LongDefeat" / "Diagnostics" / "LongDefeatPostsChecksTests.cs").read_text(encoding="utf-8")
        self.assertIn(self.PASS.replace("w=1", "w=2"), pinned, "the Python fixture drifted from the detail LongDefeatPostsChecks writes")

    def test_a_lose_run_is_not_asked_for_it(self):
        lost = {"reason": "lost_defenders", "expect": "lost"}
        report = {**self.report(), "final": False, "wavesCleared": 1, "waves": [wave_row(1)]}

        code, why = ld.judge(lost, report, LOSE_LOG)

        self.assertEqual(code, 0, why)

    def test_the_armed_variant_must_be_the_one_the_pass_names(self):
        ld.posts_variant = "B"
        self.assertEqual(ld.judge(FINAL, self.report(), self.log(*self.passes()))[0], 1, "a variant A line when B was armed: the env var never took")
        code, why = ld.judge(FINAL, self.report(), self.log(*self.passes("B")))
        self.assertEqual(code, 0, why)

    def test_the_variant_pattern_does_not_match_a_longer_token_or_a_skip(self):
        pattern = ld.posts_variant_pattern("A")
        self.assertIsNone(pattern.search(self.line("skip", self.PASS)))
        self.assertIsNone(pattern.search(self.line("pass", self.PASS.replace("variant=A", "variant=AB"))))
        self.assertIsNotNone(pattern.search(self.line("pass", self.PASS)))

    def test_the_flag_takes_a_variant_in_either_case_and_refuses_another(self):
        parser = ld.build_parser()
        self.assertEqual(parser.parse_args(["--posts-variant", "c"]).posts_variant, "C")
        self.assertIsNone(parser.parse_args([]).posts_variant)
        with self.assertRaises(SystemExit):
            parser.parse_args(["--posts-variant", "D"])

    def test_the_environment_name_is_the_games(self):
        text = source("Domain", "LdPosts.cs")
        self.assertIn(f'EnvironmentName = "{ld.POSTS_VARIANT_ENV}"', text)

    def test_the_manifest_row_reads_the_line_the_game_writes(self):
        manifest = iv.load_manifest(REPO / "tools" / "ingame_checks" / "1079.toml", 1079)
        row = next(c for c in manifest.checks if c.id == "taom.long-defeat.posts")
        self.assertEqual(row.absence, "not-reached")
        for detail in (self.PASS, self.PASS2, self.PASS.replace("variant=A", "variant=C").replace("short=0", "short=2"), self.PASS + " ended=run"):
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line("pass", detail))], []).verdict, iv.PASS, detail)
            self.assertEqual(iv.judge_segment(row, [(None, 1, self.line("fail", detail))], []).verdict, iv.FAIL, detail)
        self.assertNotEqual(iv.judge_segment(row, [(None, 1, self.line("pass", self.PASS.replace("failed=0", "failed=1")))], []).verdict, iv.PASS, "a spawn that failed is not a pass")
        self.assertEqual(iv.judge_segment(row, [], []).verdict, iv.NOT_REACHED)
        cut = self.line("skip", self.PASS + " ended=run reason=window-cut")
        self.assertEqual(iv.judge_segment(row, [(None, 1, cut)], []).verdict, iv.NOT_REACHED, "a run cut short proves nothing")
        self.assertNotEqual(iv.judge_segment(row, [(None, 1, self.line("pass", self.PASS.replace("held=4 sampled=4", "held=3 sampled=4")))], []).verdict, iv.PASS, "an archer off his post is not a pass")


if __name__ == "__main__":
    unittest.main()
