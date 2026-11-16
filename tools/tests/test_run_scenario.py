#!/usr/bin/env python
"""tools/run_scenario.py: the arm text, the arguments, the judge and the wiring into the Fords runner, with no game.

Run:  python -m unittest tools.tests.test_run_scenario
"""
import contextlib
import io
import json
import os
import re
import sys
import tempfile
import subprocess
import unittest
from datetime import datetime
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import autorun_build  # noqa: E402
import run_fords_autorun as runner  # noqa: E402
import run_scenario as rs  # noqa: E402

PASS_LINE = "[2026-11-15 10:00:00] [INFO] [Check] id=taom.autorun.campaign-new.done result=pass days=3"
UNLOAD_LINE = "[Shutdown] clean module unload"
CRASH_LINE = "[2026-11-15 10:05:00] [ERROR] [CrashReport] boom"


def parse(argv):
    known, rest = rs.parse_own(argv)
    return known, rest


def parse_fails(argv):
    err = io.StringIO()
    with contextlib.redirect_stderr(err), self_assert_exit():
        rs.parse_own(argv)
    return err.getvalue()


@contextlib.contextmanager
def self_assert_exit():
    try:
        yield
    except SystemExit as exit_info:
        if exit_info.code == 0:
            raise AssertionError("expected a non-zero exit")
        return
    raise AssertionError("expected SystemExit")


def json_walk(node):
    """Every dict in a parsed JSON tree (cultures.json nests some cultures one level down)."""
    if isinstance(node, dict):
        yield node
        for value in node.values():
            yield from json_walk(value)
    elif isinstance(node, list):
        for value in node:
            yield from json_walk(value)


def arm_dict(text):
    return dict(line.split("=", 1) for line in text.splitlines())


def good_done(**overrides):
    done = {"reason": "ok", "kind": "campaign-new", "days_run": "3", "days_wanted": "3", "started": "2026-11-15T09:00:00-05:00"}
    done.update(overrides)
    return done


HEAD = "a" * 40
BASE_LOG = "\n".join([PASS_LINE, UNLOAD_LINE])
STAMP_LINE = ("[2026-11-15 09:00:00] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261115-090000Z+{} TAOM.Dependencies=v0.1.0.0"
              " (pair OK)")
GOOD_LOG = BASE_LOG + "\n" + STAMP_LINE.format(HEAD)


def stamped_log(sha):
    return BASE_LOG + "\n" + STAMP_LINE.format(sha)


def judge(done, report_json, log_text=None, armed=None):
    """rs.judge with the clean checkout of HEAD that main would read at launch, for the tests of the other conditions."""
    return rs.judge(done, report_json, log_text, armed=armed, checkout=(HEAD, {}))


class ArmTextTests(unittest.TestCase):
    def test_defaults_write_every_key(self):
        known, _ = parse([])
        text, wall = rs.arm_text_for(Path(r"E:\x\run"), known, SimpleNamespace(wall_limit=None))
        keys = arm_dict(text)
        self.assertEqual(list(keys), ["kind", "out", "scenario", "culture", "days", "speed", "stall_seconds", "wall_limit_seconds", "quit"])
        self.assertEqual(keys["kind"], "campaign-new")
        self.assertEqual(keys["out"], r"E:\x\run")
        self.assertEqual(keys["scenario"], "campaign-new")
        self.assertEqual(keys["culture"], "gondor")
        self.assertEqual(keys["days"], "3")
        self.assertEqual(keys["speed"], "15")
        self.assertEqual(keys["stall_seconds"], "600")
        self.assertEqual(keys["quit"], "1")
        self.assertEqual(wall, 1800)
        self.assertEqual(keys["wall_limit_seconds"], "1800")

    def test_default_culture_is_a_creation_culture_and_matches_the_game(self):
        root = Path(__file__).resolve().parents[2]
        cultures = json.loads((root / "Main/_Module/ModuleData/charactercreation/cultures.json").read_text(encoding="utf-8-sig"))
        ids = {entry["culture_id"] for entry in json_walk(cultures) if "culture_id" in entry}
        self.assertIn(rs.DEFAULT_CULTURE, ids)
        plan = (root / "Main/Features/Autorun/Scenario/ScenarioPlan.cs").read_text(encoding="utf-8")
        self.assertIn(f'DefaultCulture = "{rs.DEFAULT_CULTURE}"', plan)

    def test_custom_values(self):
        known, _ = parse(["--days", "30", "--culture", "sturgia", "--speed", "7.5", "--scenario", "war-30", "--stall-seconds", "900"])
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        keys = arm_dict(text)
        self.assertEqual((keys["days"], keys["culture"], keys["speed"], keys["scenario"], keys["stall_seconds"]), ("30", "sturgia", "7.5", "war-30", "900"))
        self.assertEqual(wall, 1800)

    def test_computed_wall_limit_grows_with_days(self):
        self.assertEqual(rs.default_wall_limit(3), 1800)
        self.assertEqual(rs.default_wall_limit(60), 1800)
        self.assertEqual(rs.default_wall_limit(1000), 20600)

    def test_runner_wall_limit_wins(self):
        known, _ = parse([])
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=5000.0))
        self.assertEqual(wall, 5000.0)
        self.assertEqual(arm_dict(text)["wall_limit_seconds"], "5000")

    def test_quit_is_always_one(self):
        known, _ = parse([])
        text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        self.assertTrue(text.endswith("quit=1\n"))
        self.assertNotIn("\r", text)

    def test_fractional_values_keep_every_digit(self):
        known, _ = parse(["--speed", "12.3456789"])
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=1234567.5))
        keys = arm_dict(text)
        self.assertEqual(float(keys["wall_limit_seconds"]), wall)
        self.assertEqual(float(keys["speed"]), 12.3456789)


class ArgumentTests(unittest.TestCase):
    def test_unknown_args_pass_through(self):
        known, rest = parse(["--days", "5", "--dry-run", "--wall-limit", "4000"])
        self.assertEqual(known.days, 5)
        self.assertEqual(rest, ["--dry-run", "--wall-limit", "4000"])

    def test_bad_days(self):
        for bad in ("0", "1001", "-1", "abc"):
            with self.subTest(bad=bad):
                self.assertIn("days", parse_fails(["--days", bad]))

    def test_bad_speed(self):
        for bad in ("0", "-2", "30.5", "nan", "inf", "x"):
            with self.subTest(bad=bad):
                self.assertIn("speed", parse_fails(["--speed", bad]))

    def test_speed_limit_is_allowed(self):
        known, _ = parse(["--speed", "30"])
        self.assertEqual(known.speed, 30.0)

    def test_unknown_kind(self):
        self.assertIn("kind", parse_fails(["--kind", "siege"]))

    def test_bad_stall_seconds(self):
        self.assertIn("stall", parse_fails(["--stall-seconds", "0"]))


class JudgeTests(unittest.TestCase):
    def test_ok(self):
        self.assertEqual(judge(good_done(), None, GOOD_LOG)[0], 0)

    def test_more_days_than_wanted_passes(self):
        self.assertEqual(judge(good_done(days_run="4"), None, GOOD_LOG)[0], 0)

    def test_no_done_file(self):
        code, why = judge({}, None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("no done file", why)

    def test_short_days(self):
        code, why = judge(good_done(days_run="2"), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("days_run=2", why)

    def test_unreadable_days(self):
        code, why = judge(good_done(days_run="many"), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("days_run", why)

    def test_non_ok_reason(self):
        for reason in ("stalled", "wall_limit", "map_timeout", "creation_failed", "start_refused", "creation_timeout"):
            with self.subTest(reason=reason):
                code, why = judge(good_done(reason=reason), None, GOOD_LOG)
                self.assertEqual(code, 1)
                self.assertIn("reason=" + reason, why)

    def test_missing_pass_line(self):
        code, why = judge(good_done(), None, UNLOAD_LINE)
        self.assertEqual(code, 1)
        self.assertIn("result=pass", why)

    def test_fail_line_is_not_a_pass(self):
        log = "[Check] id=taom.autorun.campaign-new.done result=fail\n" + UNLOAD_LINE
        self.assertEqual(judge(good_done(), None, log)[0], 1)

    def test_no_log(self):
        code, why = judge(good_done(), None, None)
        self.assertEqual(code, 1)
        self.assertIn("no TAOM log copy", why)

    def test_crash_line_before_the_driver_started_fails(self):
        log = "[2026-11-15 08:59:59] [ERROR] [CrashReport] init failed: X\n" + GOOD_LOG
        code, why = judge(good_done(started="2026-11-15T09:00:00"), None, log)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_done_file_must_echo_the_armed_plan(self):
        armed = SimpleNamespace(kind="campaign-new", culture="gondor", days=150, scenario="campaign-new")
        code, why = judge(good_done(culture="gondor", scenario="campaign-new"), None, GOOD_LOG, armed=armed)
        self.assertEqual(code, 1)
        self.assertIn("days_wanted=3", why)
        done = good_done(culture="gondor", scenario="campaign-new", days_run="150", days_wanted="150")
        self.assertEqual(judge(done, None, GOOD_LOG, armed=armed)[0], 0)

    def test_arm_file_warning_in_the_log_fails(self):
        log = GOOD_LOG + "\n[2026-11-15 09:00:01] [WARNING] [Autorun][Scenario] arm file: speed '1e-50' is not a number above 0"
        code, why = judge(good_done(), None, log)
        self.assertEqual(code, 1)
        self.assertIn("arm file", why)

    def test_build_must_be_this_tree(self):
        head = "a" * 40
        stamped = BASE_LOG + "\n[2026-11-15 09:00:00] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261115-090000Z+{} TAOM.Dependencies=v0.1.0.0 (pair OK)"
        edited = {"Main/a.cs": datetime(2026, 11, 15, 8, 0, 0)}
        cases = (("b" * 40, {}, 1), (head, {}, 0), (head + ".dirty", edited, 0))
        for sha, changed, want in cases:
            with self.subTest(sha=sha):
                self.assertEqual(rs.judge(good_done(), None, stamped.format(sha), checkout=(head, changed))[0], want)
        code, why = rs.judge(good_done(), None, BASE_LOG, checkout=(head, {}))
        self.assertEqual(code, 1)
        self.assertIn("BuildStamp", why)

    def test_a_dirty_stamp_with_a_clean_tree_fails(self):
        head = "a" * 40
        code, why = rs.judge(good_done(), None, stamped_log(head + ".dirty"), checkout=(head, {}))
        self.assertEqual(code, 1)
        self.assertIn("this tree has none", why)

    def test_a_clean_stamp_with_an_uncommitted_watched_file_fails(self):
        head = "a" * 40
        code, why = rs.judge(good_done(), None, stamped_log(head), checkout=(head, {"Main/a.cs": datetime(2026, 11, 15, 8, 0, 0)}))
        self.assertEqual(code, 1)
        self.assertIn("Main/a.cs", why)

    def test_a_watched_file_edited_after_the_build_fails(self):
        head = "a" * 40
        code, why = rs.judge(good_done(), None, stamped_log(head + ".dirty"), checkout=(head, {"Main/a.cs": datetime(2026, 11, 15, 9, 30, 0)}))
        self.assertEqual(code, 1)
        self.assertIn("changed after the build", why)

    def test_a_mismatch_stamp_fails(self):
        head = "a" * 40
        log = BASE_LOG + "\n" + STAMP_LINE.format(head).replace("(pair OK)", "MISMATCH built 2d apart")
        code, why = rs.judge(good_done(), None, log, checkout=(head, {}))
        self.assertEqual(code, 1)
        self.assertIn("MISMATCH", why)

    def test_a_pass_names_the_build_it_proves(self):
        head = "a" * 40
        edited = {"Main/a.cs": datetime(2026, 11, 15, 8, 0, 0)}
        for sha, changed, label in ((head, {}, "aaaaaaaaa built 20261115-090000 UTC"), (head + ".dirty", edited, "aaaaaaaaa.dirty built 20261115-090000 UTC")):
            with self.subTest(sha=sha):
                code, why = rs.judge(good_done(), None, stamped_log(sha), checkout=(head, changed))
                self.assertEqual(code, 0)
                self.assertTrue(why.startswith(f"build {label}, reason=ok, "), why)

    def test_driver_source_keeps_the_arm_warning_prefix(self):
        source = (Path(__file__).resolve().parents[2] / "Main/Features/Autorun/Scenario/ScenarioAutorunDriver.cs").read_text(encoding="utf-8")
        self.assertIn('"[Autorun][Scenario] "', source)
        self.assertIn('"arm file: "', source)

    def test_a_judge_with_no_checkout_fails(self):
        code, why = rs.judge(good_done(), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("not read at launch", why)

    def test_crash_marker(self):
        code, why = judge(good_done(), None, GOOD_LOG + "\n" + CRASH_LINE)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_missing_unload_line(self):
        code, why = judge(good_done(), None, PASS_LINE)
        self.assertEqual(code, 1)
        self.assertIn("unload", why)

    def test_every_failure_gets_its_own_reason(self):
        code, why = judge(good_done(reason="stalled", days_run="1"), None, "")
        self.assertEqual(code, 1)
        self.assertIn("reason=stalled", why)
        self.assertIn("days_run=1", why)
        self.assertIn("result=pass", why)


NAMES = ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "arm_text_for", "judge", "show_results", "handle_dialogs", "STALL_SECONDS", "main")


class MainWiringTests(unittest.TestCase):
    def setUp(self):
        saved = {name: getattr(runner, name) for name in NAMES}
        self.addCleanup(lambda: [setattr(runner, name, value) for name, value in saved.items()])

    def run_main(self, argv, state=("a" * 40, {})):
        """Runs main with autorun_build.checkout_state answering from state (an exception or a list of answers is used as side_effect)."""
        captured = {}

        def fake_main(rest):
            captured["rest"] = rest
            captured["env_var"] = runner.ENV_VAR
            captured["done"] = runner.DONE_FILE
            captured["status"] = runner.STATUS_FILE
            captured["root"] = runner.RUNS_ROOT
            captured["arm_text_for"] = runner.arm_text_for
            captured["judge"] = runner.judge
            captured["show_results"] = runner.show_results
            captured["handle_dialogs"] = runner.handle_dialogs
            captured["stall"] = runner.STALL_SECONDS
            return 0

        names = ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "arm_text_for", "judge", "show_results", "handle_dialogs", "STALL_SECONDS", "main")
        saved = {name: getattr(runner, name) for name in names}
        try:
            runner.main = fake_main
            kind = {"side_effect": state} if isinstance(state, (Exception, list)) else {"return_value": state}
            with mock.patch.object(autorun_build, "checkout_state", **kind) as git, contextlib.redirect_stdout(io.StringIO()) as out:
                code = rs.main(argv)
            captured["git"] = git
            captured["out"] = out.getvalue()
        finally:
            for name, value in saved.items():
                setattr(runner, name, value)
        return code, captured

    def test_sets_names_and_forwards_the_rest(self):
        code, got = self.run_main(["--days", "5", "--dry-run", "--runs", "2"])
        self.assertEqual(code, 0)
        self.assertEqual(got["rest"], ["--dry-run", "--runs", "2"])
        self.assertEqual(got["env_var"], "TAOM_AUTORUN")
        self.assertEqual(got["done"], "autorun_done.txt")
        self.assertEqual(got["status"], "autorun_status.txt")
        self.assertEqual(got["root"], r"E:\Temp\scenario-runs")
        self.assertIs(got["show_results"], rs.show_results)
        self.assertIs(got["handle_dialogs"], runner.handle_dialogs)

    def test_runner_judge_holds_the_run_to_the_armed_plan_and_this_tree(self):
        _, got = self.run_main(["--days", "150"])
        code, why = got["judge"](good_done(culture="gondor", scenario="campaign-new"), None, stamped_log("b" * 40))
        self.assertEqual(code, 1)
        self.assertIn("days_wanted=3", why)
        self.assertIn("this checkout is aaaaaaaaa", why)

    def test_a_git_that_fails_at_launch_returns_1_and_starts_nothing(self):
        for error in (OSError("git exited 128"), subprocess.TimeoutExpired("git", 60)):
            with self.subTest(error=type(error).__name__):
                code, got = self.run_main(["--days", "3"], state=error)
                self.assertEqual(code, 1)
                self.assertNotIn("rest", got)
                self.assertIn("cannot read the checkout from git", got["out"])

    def test_runner_judge_compares_with_the_checkout_read_at_launch(self):
        # A commit or an edit made while the game runs must not fail the run as a stale deploy.
        moved = ("b" * 40, {"Main/a.cs": datetime(2026, 11, 15, 9, 30, 0)})
        _, got = self.run_main(["--days", "3"], state=[("a" * 40, {}), moved])
        code, why = got["judge"](good_done(culture="gondor", scenario="campaign-new"), None, stamped_log("a" * 40))
        self.assertEqual(code, 0, why)
        self.assertEqual(got["git"].call_count, 1)

    def test_runner_judge_rejects_a_dirty_stamp_when_the_tree_was_clean_at_launch(self):
        _, got = self.run_main(["--days", "3"])
        code, why = got["judge"](good_done(), None, stamped_log("a" * 40 + ".dirty"))
        self.assertEqual(code, 1)
        self.assertIn("this tree has none", why)

    def test_runner_judge_rejects_a_clean_stamp_when_a_watched_file_was_edited_at_launch(self):
        _, got = self.run_main(["--days", "3"], state=("a" * 40, {"Main/a.cs": datetime(2026, 11, 15, 8, 0, 0)}))
        code, why = got["judge"](good_done(), None, stamped_log("a" * 40))
        self.assertEqual(code, 1)
        self.assertIn("Main/a.cs", why)

    def test_help_names_this_tools_arguments(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit) as exit_info:
            rs.parse_own(["--help"])
        self.assertEqual(exit_info.exception.code, 0)
        self.assertIn("--stall-seconds", out.getvalue())
        self.assertIn("--wall-limit", out.getvalue())
        self.assertNotIn("Fords of War match", out.getvalue())

    def test_armed_text_uses_the_parsed_arguments(self):
        seen = {}
        original = runner.main

        def fake_main(rest):
            seen["result"] = runner.arm_text_for(Path(r"E:\o"), SimpleNamespace(wall_limit=None))
            return 0

        try:
            runner.main = fake_main
            with mock.patch.object(autorun_build, "checkout_state", return_value=(HEAD, {})):
                rs.main(["--days", "10", "--culture", "vlandia"])
        finally:
            runner.main = original
        text, wall = seen["result"]
        self.assertIn("days=10\n", text)
        self.assertIn("culture=vlandia\n", text)
        self.assertEqual(wall, 1800)

    def test_runner_heartbeat_stall_is_not_changed(self):
        _, got = self.run_main(["--stall-seconds", "1200"])
        self.assertEqual(got["stall"], runner.STALL_SECONDS)

    def test_bad_argument_does_not_reach_the_runner(self):
        called = []
        original = runner.main
        runner.main = lambda rest: called.append(rest) or 0
        try:
            with contextlib.redirect_stderr(io.StringIO()), self_assert_exit():
                rs.main(["--days", "0"])
        finally:
            runner.main = original
        self.assertEqual(called, [])


class ShowResultsTests(unittest.TestCase):
    def test_ledger_is_saved_and_failure_is_not_fatal(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / "taom_debug_x.log"
            log.write_text(GOOD_LOG + "\n[BuildStamp] abc\n", encoding="utf-8")
            messages = []
            done_text = "reason=ok\ndays_run=3\n"
            ok = SimpleNamespace(returncode=0, stdout="LEDGER OUT\n", stderr="")
            out = io.StringIO()
            with mock.patch.object(rs.subprocess, "run", return_value=ok) as run, contextlib.redirect_stdout(out):
                rs.show_results(done_text, log, None, messages.append)
            self.assertEqual((Path(folder) / "ledger.txt").read_text(encoding="utf-8"), "LEDGER OUT\n")
            self.assertIn("LEDGER OUT", out.getvalue())
            self.assertIn("reason=ok", out.getvalue())
            self.assertIn("analyze_war_ledger.py", " ".join(run.call_args[0][0]))
            self.assertIn(str(log), run.call_args[0][0])
            self.assertEqual(run.call_args[0][0][1:3], ["-X", "utf8"])

            (Path(folder) / "ledger.txt").unlink()
            failed = SimpleNamespace(returncode=1, stdout="No [WarLedger] lines found\n", stderr="")
            with mock.patch.object(rs.subprocess, "run", return_value=failed), contextlib.redirect_stdout(io.StringIO()):
                rs.show_results(done_text, log, None, messages.append)
            self.assertFalse((Path(folder) / "ledger.txt").exists())
            self.assertTrue(any("exited 1" in m for m in messages))

            with mock.patch.object(rs.subprocess, "run", side_effect=OSError("nope")), contextlib.redirect_stdout(io.StringIO()):
                rs.show_results(done_text, log, None, messages.append)
            self.assertTrue(any("ledger" in m.lower() for m in messages))

    def test_no_log_copy_skips_the_ledger(self):
        messages = []
        with mock.patch.object(rs.subprocess, "run") as run, contextlib.redirect_stdout(io.StringIO()):
            rs.show_results("reason=stalled\n", None, None, messages.append)
        run.assert_not_called()


STORY_ARGS = ["--kind", "story-mission", "--mission", "helms_deep_defend"]
STORY_PASS = ("[2026-11-16 10:00:00] [INFO] [Check] id=taom.autorun.story-mission.done result=pass reason=ok mission=helms_deep_defend "
              "verdict=Won mission_seconds=230.4 culture=gondor")
STORY_LAUNCH = ("[2026-11-16 09:50:00] [INFO] [StoryMissions] launching taom_rohan_castle_helms_deep_forceatmo for 'helms_deep_defend' "
                "(story1, autoplay True)")
STORY_BOOT = ("[2026-11-16 09:50:05] [INFO] [Check] id=taom.story-missions.hosted.boot result=pass deployment_finished=true siege_ai=true "
              "main=true friendly=false")
STORY_ENDED = ("[2026-11-16 09:55:00] [INFO] [Check] id=taom.story-missions.ended result=pass verdict=Won side=Defending "
               "reason=TargetReached waves=1")
STORY_LOG = "\n".join([STORY_LAUNCH, STORY_BOOT, STORY_ENDED, STORY_PASS, UNLOAD_LINE, STAMP_LINE.format(HEAD)])
ROOT = Path(__file__).resolve().parents[2]
NOT_ARMED_LOG = ("[2026-11-16 09:00:00] [WARNING] [Autorun][Scenario] not armed: the arm file x needs kind=campaign-new (got 'story-mission')\n"
                 + UNLOAD_LINE)


def story_armed(**overrides):
    values = {"kind": "story-mission", "scenario": "story-mission", "culture": "gondor", "days": None, "mission": "helms_deep_defend"}
    values.update(overrides)
    return SimpleNamespace(**values)


def good_story_done(**overrides):
    done = {"reason": "ok", "kind": "story-mission", "scenario": "story-mission", "culture": "gondor", "mission": "helms_deep_defend",
            "verdict": "Won", "mission_seconds": "230.4"}
    done.update(overrides)
    return done


def story_judge(done, log_text, armed=None):
    return rs.judge(done, None, log_text, armed=armed or story_armed(), checkout=(HEAD, {}))


def without(log, needle):
    return "\n".join(line for line in log.splitlines() if needle not in line)


class StoryArmTextTests(unittest.TestCase):
    def test_story_kind_writes_mission_and_mission_limit_in_order(self):
        known, _ = parse(STORY_ARGS)
        text, wall = rs.arm_text_for(Path(r"E:\x\run"), known, SimpleNamespace(wall_limit=None))
        keys = arm_dict(text)
        self.assertEqual(list(keys), ["kind", "out", "scenario", "mission", "mission_limit_seconds", "culture", "wall_limit_seconds", "quit"])
        self.assertEqual((keys["kind"], keys["scenario"], keys["mission"], keys["mission_limit_seconds"], keys["culture"], keys["quit"]),
                         ("story-mission", "story-mission", "helms_deep_defend", "1200", "gondor", "1"))
        self.assertEqual(wall, 3000)
        self.assertEqual(keys["wall_limit_seconds"], "3000")

    def test_story_kind_needs_a_mission(self):
        self.assertIn("--mission", parse_fails(["--kind", "story-mission"]))

    def test_mission_with_campaign_new_is_refused(self):
        self.assertIn("--mission", parse_fails(["--mission", "helms_deep_defend"]))

    def test_days_with_the_story_kind_is_refused(self):
        self.assertIn("--days", parse_fails(STORY_ARGS + ["--days", "3"]))

    def test_campaign_new_still_defaults_to_three_days(self):
        known, _ = parse([])
        self.assertEqual((known.days, known.mission, known.scenario), (3, None, "campaign-new"))

    def test_an_empty_mission_is_refused(self):
        self.assertIn("--mission", parse_fails(["--kind", "story-mission", "--mission", " "]))

    def test_the_mission_is_stripped_and_lowercased_at_parse(self):
        # The game accepts any case and logs the catalog id, which is lowercase; the judge and the 1368 manifest compare with case.
        known, _ = parse(["--kind", "story-mission", "--mission", "  Helms_Deep_Defend "])
        self.assertEqual(known.mission, "helms_deep_defend")
        text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        self.assertEqual(arm_dict(text)["mission"], "helms_deep_defend")

    def test_speed_and_stall_seconds_with_the_story_kind_are_refused(self):
        self.assertIn("--speed", parse_fails(STORY_ARGS + ["--speed", "5"]))
        self.assertIn("--stall-seconds", parse_fails(STORY_ARGS + ["--stall-seconds", "60"]))
        self.assertIn("only for --kind campaign-new", parse_fails(STORY_ARGS + ["--speed", "5"]))
        self.assertIn("only for --kind campaign-new", parse_fails(STORY_ARGS + ["--stall-seconds", "60"]))

    def test_the_story_kind_without_speed_or_stall_seconds_parses(self):
        known, _ = parse(STORY_ARGS)
        self.assertEqual((known.speed, known.stall_seconds), (None, None))

    def test_campaign_new_fills_the_speed_and_stall_defaults(self):
        known, _ = parse([])
        self.assertEqual((known.speed, known.stall_seconds), (rs.DEFAULT_SPEED, rs.DEFAULT_STALL_SECONDS))

    def test_story_default_wall_limit_adds_the_mission_limit(self):
        self.assertEqual(rs.default_wall_limit(3), 1800)
        self.assertEqual(rs.default_wall_limit(3, rs.DEFAULT_MISSION_LIMIT_SECONDS), 3000)

    def test_runner_wall_limit_wins_for_the_story_kind(self):
        known, _ = parse(STORY_ARGS)
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=4000.0))
        self.assertEqual((wall, arm_dict(text)["wall_limit_seconds"]), (4000.0, "4000"))

    def test_there_is_no_mission_limit_flag(self):
        _, rest = parse(STORY_ARGS + ["--mission-limit", "60"])
        self.assertEqual(rest, ["--mission-limit", "60"])


class StoryJudgeTests(unittest.TestCase):
    def test_story_ok(self):
        code, why = story_judge(good_story_done(), STORY_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn("reason=ok, mission helms_deep_defend verdict Won, back on the map", why)
        self.assertIn("pass line present, no CrashReport block, clean unload", why)

    def test_a_lost_verdict_passes_too(self):
        log = STORY_LOG.replace("verdict=Won mission_seconds", "verdict=Lost mission_seconds").replace("verdict=Won side", "verdict=Lost side")
        self.assertEqual(story_judge(good_story_done(verdict="Lost"), log)[0], 0)

    def test_story_needs_its_own_pass_line(self):
        for log in (without(STORY_LOG, "story-mission.done"), STORY_LOG.replace("story-mission.done", "campaign-new.done"),
                    STORY_LOG.replace("result=pass reason=ok", "result=fail reason=ok")):
            code, why = story_judge(good_story_done(), log)
            self.assertEqual(code, 1)
            self.assertIn("story-mission.done result=pass", why)

    def test_the_campaign_pass_line_does_not_satisfy_the_story_kind(self):
        code, _ = story_judge(good_story_done(), without(STORY_LOG, "story-mission.done") + "\n" + PASS_LINE)
        self.assertEqual(code, 1)

    def test_the_pass_line_must_name_the_armed_mission_and_a_decided_verdict(self):
        for bad in (STORY_LOG.replace("mission=helms_deep_defend", "mission=other"), STORY_LOG.replace("verdict=Won mission", "verdict=Undecided mission")):
            self.assertEqual(story_judge(good_story_done(), bad)[0], 1)

    def test_story_needs_boot_and_ended_pass_lines(self):
        for needle in ("hosted.boot", "story-missions.ended"):
            code, why = story_judge(good_story_done(), without(STORY_LOG, needle))
            self.assertEqual(code, 1)
            self.assertIn(needle, why)

    def test_an_abandoned_end_is_not_a_pass(self):
        log = STORY_LOG.replace("verdict=Won side=Defending reason=TargetReached", "verdict=Lost side=Defending reason=Abandoned")
        code, why = story_judge(good_story_done(), log)
        self.assertEqual(code, 1)
        self.assertIn("story-missions.ended", why)

    def test_story_needs_exactly_one_launch_line_with_autoplay_true(self):
        for log in (without(STORY_LOG, "launching"), STORY_LOG.replace("autoplay True", "autoplay False")):
            code, why = story_judge(good_story_done(), log)
            self.assertEqual(code, 1)
            self.assertIn("launching", why)

    def test_a_second_mission_launch_fails(self):
        for second in (STORY_LAUNCH, STORY_LAUNCH.replace("helms_deep_defend", "dale_defend"),
                       "[2026-11-16 09:58:00] [INFO] [LongDefeat] launching scene for fortress x"):
            code, why = story_judge(good_story_done(), STORY_LOG + "\n" + second)
            self.assertEqual(code, 1)
            self.assertIn("launching", why)

    def test_story_verdict_must_be_won_or_lost(self):
        for verdict in ("Undecided", "-", "none", ""):
            code, why = story_judge(good_story_done(verdict=verdict), STORY_LOG)
            self.assertEqual(code, 1)
            self.assertIn("verdict=", why)

    def test_story_done_must_echo_the_mission(self):
        code, why = story_judge(good_story_done(mission="other"), STORY_LOG)
        self.assertEqual(code, 1)
        self.assertIn("mission=other", why)

    def test_story_done_must_echo_kind_scenario_and_culture(self):
        for key, value in (("kind", "campaign-new"), ("scenario", "x"), ("culture", "rohan")):
            code, why = story_judge(good_story_done(**{key: value}), STORY_LOG)
            self.assertEqual(code, 1)
            self.assertIn(f"{key}={value}", why)

    def test_a_story_done_file_needs_no_days(self):
        self.assertNotIn("days_run", good_story_done())
        self.assertEqual(story_judge(good_story_done(), STORY_LOG)[0], 0)

    def test_non_ok_story_reasons_fail_and_are_named(self):
        for reason in ("mission_refused", "mission_not_opened", "mission_limit", "mission_undecided", "mission_no_verdict", "return_failed"):
            with self.subTest(reason=reason):
                code, why = story_judge(good_story_done(reason=reason), STORY_LOG)
                self.assertEqual(code, 1)
                self.assertIn("reason=" + reason, why)

    def test_a_crash_block_and_a_missing_unload_still_fail(self):
        self.assertEqual(story_judge(good_story_done(), STORY_LOG + "\n" + CRASH_LINE)[0], 1)
        self.assertEqual(story_judge(good_story_done(), without(STORY_LOG, "Shutdown"))[0], 1)

    def test_a_stale_build_still_fails(self):
        code, why = rs.judge(good_story_done(), None, STORY_LOG.replace(HEAD, "b" * 40), armed=story_armed(), checkout=(HEAD, {}))
        self.assertEqual(code, 1)
        self.assertIn("not this checkout", why)

    def test_no_done_file_says_only_that_even_with_a_not_armed_line(self):
        # An unarmed build writes no done file, so the runner never calls the judge for it; the printers name the cause (StoryWiringTests).
        self.assertEqual(story_judge({}, STORY_LOG), (1, "no done file"))
        self.assertEqual(story_judge({}, NOT_ARMED_LOG), (1, "no done file"))


class StoryWiringTests(unittest.TestCase):
    def run_main(self, argv):
        seen = {}
        names = ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "arm_text_for", "judge", "show_results", "handle_dialogs", "STALL_SECONDS", "main")
        saved = {name: getattr(runner, name) for name in names}

        def fake_main(rest):
            seen["rest"] = rest
            seen["handle_dialogs"] = runner.handle_dialogs
            seen["show_results"] = runner.show_results
            seen["judge"] = runner.judge
            seen["arm"] = runner.arm_text_for(Path(r"E:\o"), SimpleNamespace(wall_limit=None))
            return 0

        try:
            runner.main = fake_main
            with mock.patch.object(autorun_build, "checkout_state", return_value=(HEAD, {})), contextlib.redirect_stdout(io.StringIO()):
                code = rs.main(argv)
        finally:
            for name, value in saved.items():
                setattr(runner, name, value)
        return code, seen

    def test_story_kind_presses_the_terrain_shader_dialog(self):
        # Helm's Deep stops its load at that dialog: the story kind uses the Long Defeat runner's handler, which presses Cancel on it.
        _, seen = self.run_main(STORY_ARGS)
        self.assertIs(seen["handle_dialogs"], rs.long_defeat.handle_dialogs)
        self.assertIsNot(seen["handle_dialogs"], rs.long_defeat.FORDS_HANDLE_DIALOGS)

    def test_campaign_new_keeps_the_fords_dialog_handling(self):
        _, seen = self.run_main([])
        self.assertIs(seen["handle_dialogs"], rs.long_defeat.FORDS_HANDLE_DIALOGS)
        self.assertIs(seen["show_results"], rs.show_results)

    def test_story_kind_uses_its_own_printer_and_judge(self):
        _, seen = self.run_main(STORY_ARGS)
        self.assertIs(seen["show_results"], rs.show_story_results)
        code, _ = seen["judge"](good_story_done(), None, STORY_LOG)
        self.assertEqual(code, 0)
        self.assertEqual(seen["rest"], [])
        self.assertEqual(arm_dict(seen["arm"][0])["mission"], "helms_deep_defend")

    def test_story_printer_prints_the_story_check_lines_and_the_launch_line_and_no_ledger(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / "taom_debug_x.log"
            log.write_text(STORY_LOG + "\n[2026-11-16 10:00:01] [INFO] [Check] id=taom.hero-abilities.fired result=pass\n", encoding="utf-8")
            messages = []
            out = io.StringIO()
            with mock.patch.object(rs.subprocess, "run") as run, contextlib.redirect_stdout(out):
                rs.show_story_results("reason=ok\nmission=helms_deep_defend\n", log, None, messages.append)
            run.assert_not_called()
            for want in ("reason=ok", "story-mission.done result=pass", "story-missions.hosted.boot", "story-missions.ended", "launching taom_rohan"):
                self.assertIn(want, out.getvalue())
            self.assertNotIn("hero-abilities", out.getvalue())
            self.assertTrue(any("clean module unload line: present" in m for m in messages))
            self.assertFalse((Path(folder) / "ledger.txt").exists())

    def test_both_printers_name_an_unarmed_build_from_its_log_line(self):
        # The runner prints for every stop reason with the log it collected, and an unarmed build ends as a stall with no done file.
        want = "this build did not arm the run: [Autorun][Scenario] not armed: the arm file x needs kind=campaign-new"
        for printer in (rs.show_story_results, rs.show_results):
            with self.subTest(printer=printer.__name__), tempfile.TemporaryDirectory() as folder:
                log = Path(folder) / "taom_debug_x.log"
                log.write_text(NOT_ARMED_LOG + "\n[Autorun][Scenario] not armed: a second line\n", encoding="utf-8")
                messages = []
                with mock.patch.object(rs.subprocess, "run", return_value=SimpleNamespace(returncode=1, stdout="", stderr="")), \
                        contextlib.redirect_stdout(io.StringIO()):
                    printer("", log, None, messages.append)
                self.assertEqual([m for m in messages if "did not arm" in m], [want + " (got 'story-mission')"])

    def test_the_printers_say_nothing_about_arming_for_an_armed_build(self):
        for printer in (rs.show_story_results, rs.show_results):
            with self.subTest(printer=printer.__name__), tempfile.TemporaryDirectory() as folder:
                log = Path(folder) / "taom_debug_x.log"
                log.write_text(STORY_LOG, encoding="utf-8")
                messages = []
                with mock.patch.object(rs.subprocess, "run", return_value=SimpleNamespace(returncode=1, stdout="", stderr="")), \
                        contextlib.redirect_stdout(io.StringIO()):
                    printer("reason=ok\n", log, None, messages.append)
                self.assertFalse(any("did not arm" in m for m in messages))

    def test_story_printer_with_no_log_copy_says_so(self):
        messages = []
        with contextlib.redirect_stdout(io.StringIO()):
            rs.show_story_results("reason=mission_limit\n", None, None, messages.append)
        self.assertTrue(any("no TAOM log copy" in m for m in messages))

    def test_help_names_the_mission_argument(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit):
            rs.parse_own(["--help"])
        self.assertIn("--mission", out.getvalue())
        self.assertIn("story-mission", out.getvalue())
        self.assertNotIn("--mission-limit", out.getvalue())


class StoryManifestTests(unittest.TestCase):
    def manifest(self, issue):
        import tomllib
        return tomllib.loads((ROOT / "tools/ingame_checks" / f"{issue}.toml").read_text(encoding="utf-8"))

    def check(self, manifest, check_id):
        return next(entry for entry in manifest["check"] if entry["id"] == check_id)

    def test_story_patterns_match_the_1105_manifest(self):
        old = self.manifest(1105)
        self.assertEqual([rs.STORY_BOOT_PASS], self.check(old, "taom.story-missions.hosted.boot")["pass"])
        self.assertEqual([rs.STORY_ENDED_PASS], self.check(old, "taom.story-missions.ended")["pass"])

    def test_the_1368_manifest_holds_the_three_rows_scoped_to_the_armed_mission(self):
        new = self.manifest(1368)
        self.assertEqual((new["issue"], new["fixed_in"], new["logs"]), (1368, "4baa2f53a", ["taom"]))
        self.assertEqual([c["id"] for c in new["check"]],
                         ["taom.autorun.story-mission.done", "taom.story-missions.hosted.boot", "taom.story-missions.ended"])
        old = self.manifest(1105)
        for check in new["check"][1:]:
            self.assertEqual(check["pass"], self.check(old, check["id"])["pass"])
        for check in new["check"][1:]:
            self.assertEqual(check["scope"], r"\[StoryMissions\] launching \S+ for \x27helms_deep_defend\x27 ")
            self.assertEqual(check["scope_end"], r"\[(LongDefeat|StoryMissions)\] launching ")
        done = new["check"][0]
        # One done line per process, and a failure before the launch writes no launching line: the done row has no scope and names the mission.
        self.assertNotIn("scope", done)
        self.assertNotIn("scope_end", done)
        self.assertEqual(done["fail"], [r"\[Check\] id=taom\.autorun\.story-mission\.done result=fail reason=\S+ mission=helms_deep_defend "])
        refused = STORY_PASS.replace("result=pass reason=ok", "result=fail reason=mission_refused")
        self.assertTrue(re.search(done["fail"][0], refused))
        self.assertFalse(re.search(done["fail"][0], refused.replace("mission=helms_deep_defend", "mission=dale_defend")))
        ended = self.check(new, "taom.story-missions.ended")
        self.assertTrue(any(re.search(p, STORY_ENDED.replace("verdict=Won side=Defending reason=TargetReached",
                                                             "verdict=Lost side=Defending reason=Abandoned")) for p in ended["fail"]))
        self.assertFalse(any(re.search(p, STORY_ENDED) for p in ended["fail"]))
        self.assertTrue(re.search(done["pass"][0], STORY_PASS))
        self.assertFalse(re.search(done["pass"][0], STORY_PASS.replace("verdict=Won", "verdict=Undecided")))
        self.assertFalse(re.search(done["pass"][0], STORY_PASS.replace("reason=ok", "reason=mission_limit")))

    def test_the_two_older_manifests_name_the_unattended_command(self):
        for issue in (1105, 1366):
            text = (ROOT / f"tools/ingame_checks/{issue}.toml").read_text(encoding="utf-8")
            self.assertIn("python tools/run_scenario.py --kind story-mission --mission helms_deep_defend", text)

    def test_driver_source_keeps_the_story_names(self):
        scenario = ROOT / "Main/Features/Autorun/Scenario"
        self.assertIn(f'StoryDoneId = "{rs.STORY_CHECK_ID}"', (scenario / "ScenarioOutcomes.cs").read_text(encoding="utf-8"))
        plan = (scenario / "ScenarioPlan.cs").read_text(encoding="utf-8")
        self.assertIn('KindStoryMission = "story-mission"', plan)
        self.assertIn(f"DefaultMissionLimitSeconds = {rs.DEFAULT_MISSION_LIMIT_SECONDS}.0", plan)
        launcher = (ROOT / "Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs").read_text(encoding="utf-8")
        self.assertIn("[StoryMissions] launching {definition.Scene} for '{definition.Id}' ({key}, autoplay {autoplay})", launcher)
        start = (scenario / "ScenarioAutorunStart.cs").read_text(encoding="utf-8")
        self.assertIn('"[Autorun][Scenario] not armed: "', start)
        driver = (scenario / "ScenarioAutorunDriver.cs").read_text(encoding="utf-8")
        for reason in ("mission_refused", "mission_not_opened", "mission_limit", "return_failed"):
            self.assertIn(reason, driver)

if __name__ == "__main__":
    unittest.main()
