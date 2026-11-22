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

    # #1434: console diagnostics in an unattended run, one command= line each, in order, for both kinds.
    def test_commands_write_one_line_each_in_order(self):
        for argv in ([], ["--kind", "story-mission", "--mission", "helms_deep_defend"]):
            known, _ = parse(argv + ["--command", "taom.audit_settlement_entrances", "--command", "taom.other 2 x"])
            text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
            lines = [line for line in text.splitlines() if line.startswith("command=")]
            self.assertEqual(lines, ["command=taom.audit_settlement_entrances", "command=taom.other 2 x"])

    def test_no_command_writes_no_command_line(self):
        known, _ = parse([])
        text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        self.assertNotIn("command=", text)

    def test_a_command_that_is_not_one_non_empty_line_is_refused(self):
        for bad in ("taom.a\nkind=story-mission", "taom.a\rkind=x", "   "):
            with self.subTest(bad=bad):
                self.assertIn("one non-empty line", parse_fails(["--command", bad]))

    def test_custom_values(self):
        known, _ = parse(["--days", "30", "--culture", "sturgia", "--speed", "7.5", "--scenario", "war-30", "--stall-seconds", "900"])
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        keys = arm_dict(text)
        self.assertEqual((keys["days"], keys["culture"], keys["speed"], keys["scenario"], keys["stall_seconds"]), ("30", "sturgia", "7.5", "war-30", "900"))
        self.assertEqual(wall, 1800)

    def test_by_date_arms_no_open_command_and_runs_fast(self):
        known, _ = parse(["--kind", "story-offer", "--event", "hornburg", "--choice", "defend", "--by-date"])
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        keys = arm_dict(text)
        self.assertEqual((keys["open_by_date"], keys["speed"], keys["after"]), ("1", "15", "taom.chronicle_status"))
        self.assertNotIn("chronicle_open", text)
        self.assertEqual(wall, rs.default_wall_limit(rs.BY_DATE_DAYS, rs.DEFAULT_MISSION_LIMIT_SECONDS))
        self.assertGreaterEqual(wall, 3600)

    def test_by_date_takes_a_speed_and_the_plain_offer_does_not(self):
        known, _ = parse(["--kind", "story-offer", "--event", "hornburg", "--choice", "defend", "--by-date", "--speed", "30"])
        text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        self.assertEqual(arm_dict(text)["speed"], "30")
        self.assertIn("needs --by-date", parse_fails(["--kind", "story-offer", "--event", "breaking", "--choice", "defend", "--speed", "30"]))

    def test_the_plain_offer_still_arms_the_open_command_first(self):
        known, _ = parse(["--kind", "story-offer", "--event", "breaking", "--choice", "defend"])
        text, _ = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=None))
        self.assertIn("command=taom.chronicle_open breaking confirm\n", text)
        self.assertNotIn("open_by_date", text)
        self.assertEqual(arm_dict(text)["speed"], "1")

    def test_by_date_is_only_for_the_offer_kind(self):
        self.assertIn("--by-date is only for --kind story-offer", parse_fails(["--by-date"]))
        self.assertIn("--by-date is only for --kind story-offer", parse_fails(["--kind", "story-mission", "--mission", "helms_deep_defend", "--by-date"]))

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


ATTACK_LAUNCH = STORY_LAUNCH.replace("helms_deep_defend", "helms_deep_attack")
ATTACK_PASS = STORY_PASS.replace("helms_deep_defend", "helms_deep_attack").replace("verdict=Won", "verdict=Lost")
ATTACK_ENDED = STORY_ENDED.replace("verdict=Won side=Defending reason=TargetReached", "verdict=Won side=Attacking reason=KeepLost")


def attack_log():
    return "\n".join([ATTACK_LAUNCH, STORY_BOOT, ATTACK_ENDED, ATTACK_PASS.replace("verdict=Lost", "verdict=Won"), UNLOAD_LINE,
                      STAMP_LINE.format(HEAD)])


FIELD_LAUNCH = STORY_LAUNCH.replace("helms_deep_defend", "parth_galen_save")
FIELD_BOOT = STORY_BOOT.replace("siege_ai=true main=true friendly=false", "siege_ai=false main=true friendly=false field=true")
FIELD_ENDED = STORY_ENDED.replace("verdict=Won side=Defending reason=TargetReached", "verdict=Lost side=Defending reason=HeroFell")
FIELD_PASS = STORY_PASS.replace("helms_deep_defend", "parth_galen_save").replace("verdict=Won", "verdict=Lost")


def field_log(boot=FIELD_BOOT, ended=FIELD_ENDED):
    return "\n".join([FIELD_LAUNCH, boot, ended, FIELD_PASS, UNLOAD_LINE, STAMP_LINE.format(HEAD)])


def story_armed(**overrides):
    values = {"kind": "story-mission", "scenario": "story-mission", "culture": "gondor", "days": None, "mission": "helms_deep_defend",
              "side": "Defending"}
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

    def test_an_unknown_mission_is_refused_at_parse(self):
        message = parse_fails(["--kind", "story-mission", "--mission", "nowhere"])
        self.assertIn("nowhere", message)
        self.assertIn("helms_deep_attack", message)

    def test_an_attack_mission_parses(self):
        known, _ = parse(["--kind", "story-mission", "--mission", "Helms_Deep_Attack"])
        self.assertEqual(known.mission, "helms_deep_attack")

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

    def test_story_field_reads_the_checkout_row(self):
        self.assertTrue(rs.story_field("parth_galen_save"))
        self.assertTrue(rs.story_field("Parth_Galen_Kill"))
        self.assertFalse(rs.story_field("helms_deep_defend"))
        self.assertFalse(rs.story_field("nowhere"))

    def test_story_must_live_reads_the_checkout_row(self):
        self.assertTrue(rs.story_must_live("parth_galen_save"))
        self.assertTrue(rs.story_must_live("parth_galen_kill"))
        self.assertFalse(rs.story_must_live("helms_deep_defend"))
        self.assertFalse(rs.story_must_live("nowhere"))

    def test_a_hero_fall_end_is_allowed_by_must_live_not_by_the_scene(self):
        self.assertIn("HeroFell", rs.story_ended_pass("Defending", must_live=True))
        self.assertNotIn("HeroFell", rs.story_ended_pass("Defending"))

    def test_a_field_run_passes_with_the_field_boot_line_and_a_hero_fell_end(self):
        self.assertEqual(rs.story_log_problems(field_log(), "parth_galen_save", "Defending"), [])
        code, why = story_judge(good_story_done(mission="parth_galen_save", verdict="Lost"), field_log(),
                                armed=story_armed(mission="parth_galen_save"))
        self.assertEqual(code, 0, why)

    def test_a_field_run_under_the_siege_list_is_no_pass(self):
        problems = rs.story_log_problems(field_log(boot=STORY_BOOT), "parth_galen_save", "Defending")
        self.assertEqual(len(problems), 1)
        self.assertIn("hosted.boot", problems[0])

    def test_a_siege_run_cannot_end_by_a_heros_fall(self):
        log = STORY_LOG.replace("verdict=Won side=Defending reason=TargetReached", "verdict=Lost side=Defending reason=HeroFell")
        problems = rs.story_log_problems(log, "helms_deep_defend", "Defending")
        self.assertEqual(len(problems), 1)
        self.assertIn("story-missions.ended", problems[0])

    def test_a_siege_run_with_the_field_boot_line_is_no_pass(self):
        problems = rs.story_log_problems(STORY_LOG.replace(STORY_BOOT, FIELD_BOOT), "helms_deep_defend", "Defending")
        self.assertEqual(len(problems), 1)
        self.assertIn("hosted.boot", problems[0])

    def test_story_side_reads_the_checkout_row(self):
        self.assertEqual(rs.story_side("helms_deep_attack"), "Attacking")
        self.assertEqual(rs.story_side("helms_deep_defend"), "Defending")
        self.assertEqual(rs.story_side("Helms_Deep_Attack"), "Attacking")
        self.assertIsNone(rs.story_side("nowhere"))

    def test_an_attack_run_passes_with_side_attacking(self):
        log = attack_log()
        self.assertEqual(rs.story_log_problems(log, "helms_deep_attack", "Attacking"), [])
        code, why = story_judge(good_story_done(mission="helms_deep_attack"), log,
                                armed=story_armed(mission="helms_deep_attack", side="Attacking"))
        self.assertEqual(code, 0, why)

    def test_the_judge_uses_the_side_read_at_launch_not_the_file(self):
        known, _ = parse(["--kind", "story-mission", "--mission", "helms_deep_attack"])
        self.assertEqual(known.side, "Attacking")
        with tempfile.TemporaryDirectory() as folder, mock.patch.object(rs, "STORY_MISSIONS_FILE", Path(folder) / "missing.json"):
            code, why = story_judge(good_story_done(mission="helms_deep_attack"), attack_log(), armed=known)
        self.assertEqual(code, 0, why)

    def test_a_json_with_a_bom_is_read(self):
        with tempfile.TemporaryDirectory() as folder:
            bom = Path(folder) / "story_missions.json"
            bom.write_bytes(b"\xef\xbb\xbf" + rs.STORY_MISSIONS_FILE.read_bytes())
            with mock.patch.object(rs, "STORY_MISSIONS_FILE", bom):
                self.assertEqual(rs.story_side("helms_deep_defend"), "Defending")

    def test_an_unreadable_json_is_named_at_parse(self):
        with tempfile.TemporaryDirectory() as folder, mock.patch.object(rs, "STORY_MISSIONS_FILE", Path(folder) / "missing.json"):
            message = parse_fails(STORY_ARGS)
        self.assertIn("could not be read or holds no row with side Defend or Attack", message)

    def test_a_defence_that_logs_side_attacking_is_rejected(self):
        log = STORY_LOG.replace("side=Defending", "side=Attacking")
        problems = rs.story_log_problems(log, "helms_deep_defend", "Defending")
        self.assertEqual(len(problems), 1)
        self.assertIn("taom.story-missions.ended", problems[0])

    def test_an_attack_that_logs_side_defending_is_rejected(self):
        problems = rs.story_log_problems(attack_log().replace("side=Attacking", "side=Defending"), "helms_deep_attack", "Attacking")
        self.assertEqual(len(problems), 1)
        self.assertIn("taom.story-missions.ended", problems[0])

    def test_an_abandoned_attack_is_not_a_pass(self):
        log = attack_log().replace("verdict=Won side=Attacking reason=KeepLost", "verdict=Lost side=Attacking reason=Abandoned")
        self.assertEqual(len(rs.story_log_problems(log, "helms_deep_attack", "Attacking")), 1)

    def test_an_unknown_mission_fails_the_judge(self):
        code, why = story_judge(good_story_done(mission="nowhere"), STORY_LOG.replace("helms_deep_defend", "nowhere"),
                                armed=story_armed(mission="nowhere", side=None))
        self.assertEqual(code, 1)
        self.assertIn("nowhere", why)

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

    def test_both_printers_print_commands_txt_and_name_a_missing_one(self):
        for printer in (rs.show_story_results, rs.show_results):
            for written in (True, False):
                with self.subTest(printer=printer.__name__, written=written), tempfile.TemporaryDirectory() as folder:
                    log = Path(folder) / "taom_debug_x.log"
                    log.write_text(STORY_LOG, encoding="utf-8")
                    (Path(folder) / "arm.txt").write_text("kind=campaign-new\ncommand=taom.x\n", encoding="utf-8")
                    if written:
                        (Path(folder) / rs.COMMANDS_FILE).write_text("> taom.x\nTOKEN_1434\n", encoding="utf-8")
                    out, messages = io.StringIO(), []
                    with mock.patch.object(rs.subprocess, "run", return_value=SimpleNamespace(returncode=1, stdout="", stderr="")), \
                            contextlib.redirect_stdout(out):
                        printer("reason=ok\n", log, None, messages.append)
                    self.assertEqual(written, "TOKEN_1434" in out.getvalue())
                    self.assertEqual(not written, any("wrote no commands.txt" in m for m in messages))

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


def check_line(check_id, detail, result="pass"):
    return f"[2026-11-16 11:00:00] [INFO] [Check] id={check_id} result={result} {detail}"


def hits(patterns, line):
    return any(re.search(pattern, line) for pattern in patterns)


class AttackManifestTests(unittest.TestCase):
    """tools/ingame_checks/1409.toml: every pass and fail pattern is tried against a line built from the C# that writes it."""

    SCOPE = r"\[StoryMissions\] launching \S+ for \x27helms_deep_attack\x27 "

    @classmethod
    def setUpClass(cls):
        import tomllib
        cls.manifest = tomllib.loads((ROOT / "tools/ingame_checks/1409.toml").read_text(encoding="utf-8"))

    def rows(self, check_id):
        return [c for c in self.manifest["check"] if c["id"] == check_id]

    def row(self, check_id):
        found = self.rows(check_id)
        self.assertEqual(len(found), 1, check_id)
        return found[0]

    def test_header(self):
        self.assertEqual((self.manifest["issue"], self.manifest["logs"]), (1409, ["taom"]))
        self.assertRegex(self.manifest["fixed_in"], r"^[0-9a-f]{7,40}$")

    def test_every_row_but_the_done_row_is_scoped_to_the_attack_launch(self):
        for check in self.manifest["check"]:
            if check["id"] == "taom.autorun.story-mission.done":
                self.assertNotIn("scope", check)
                self.assertNotIn("scope_end", check)
                continue
            self.assertEqual(check["scope"], self.SCOPE, check["id"])
            self.assertEqual(check["scope_end"], r"\[(LongDefeat|StoryMissions)\] launching ", check["id"])

    def test_the_ended_pass_is_the_attacking_pattern(self):
        ended = self.rows("taom.story-missions.ended")
        self.assertEqual([rs.story_ended_pass("Attacking")], ended[0]["pass"])
        self.assertEqual(ended[0].get("absence", "fail"), "not-reached")
        line = check_line("taom.story-missions.ended", "verdict=Won side=Attacking reason=KeepLost waves=1")
        self.assertTrue(hits(ended[0]["pass"], line))
        self.assertFalse(hits(ended[0]["fail"], line))
        self.assertFalse(hits(ended[0]["pass"], line.replace("side=Attacking", "side=Defending")))
        abandoned = line.replace("verdict=Won side=Attacking reason=KeepLost", "verdict=Lost side=Attacking reason=Abandoned")
        self.assertFalse(hits(ended[0]["pass"], abandoned))
        self.assertTrue(hits(ended[0]["fail"], abandoned))
        self.assertTrue(hits(ended[0]["fail"], line.replace("result=pass", "result=fail")))

    def test_the_balance_row_reads_a_won_attack_and_is_not_reached_otherwise(self):
        balance = self.row("taom.story-missions.attack-won")
        self.assertEqual(balance["absence"], "not-reached")
        won = check_line("taom.story-missions.ended", "verdict=Won side=Attacking reason=KeepLost waves=1")
        lost = check_line("taom.story-missions.ended", "verdict=Lost side=Attacking reason=Abandoned waves=0")
        self.assertTrue(hits(balance["pass"], won))
        self.assertFalse(hits(balance["pass"], lost))
        self.assertFalse(hits(balance["pass"], won.replace("verdict=Won", "verdict=Lost")))

    def test_the_done_row_names_the_attack_mission(self):
        done = self.row("taom.autorun.story-mission.done")
        line = check_line("taom.autorun.story-mission.done", "reason=ok mission=helms_deep_attack verdict=Lost mission_seconds=611.2 culture=gondor")
        self.assertTrue(hits(done["pass"], line))
        self.assertFalse(hits(done["pass"], line.replace("mission=helms_deep_attack", "mission=helms_deep_defend")))
        refused = line.replace("result=pass reason=ok", "result=fail reason=mission_limit")
        self.assertTrue(hits(done["fail"], refused))
        self.assertFalse(hits(done["fail"], refused.replace("helms_deep_attack", "helms_deep_defend")))

    def test_general_needs_the_main_on_the_attacker_team(self):
        general = self.row("taom.story-missions.hosted.general")
        line = check_line("taom.story-missions.hosted.general",
                          "general_is_main=true player_general=true order_owner_is_main=true main_on_team=true side=attacker")
        self.assertTrue(hits(general["pass"], line))
        for bad in (line.replace("side=attacker", "side=defender"), line.replace("main_on_team=true", "main_on_team=false")):
            self.assertFalse(hits(general["pass"], bad))
        self.assertTrue(hits(general["fail"], line.replace("result=pass", "result=fail").replace("main_on_team=true", "main_on_team=false")))

    def test_each_enemy_hero_stands_in_a_class_formation_under_ai(self):
        hero = self.row("taom.story-missions.hosted.hero")
        self.assertEqual(len(hero["pass"]), 3)
        tail = "is_hero=0 campaign=1 mounted=0 formation={f} held=0 strangers=57 order=Charge health_factor=3.00 wanted_factor=3.00 wielded={w} side=enemy"
        lines = {
            "aragorn": check_line("taom.story-missions.hosted.hero", "hero=aragorn troop=taom_hero_aragorn " + tail.format(f=0, w="strider_sword")),
            "legolas": check_line("taom.story-missions.hosted.hero", "hero=legolas troop=taom_hero_legolas " + tail.format(f=1, w="wm_mirkwood_bow_a01")),
            "gimli": check_line("taom.story-missions.hosted.hero", "hero=gimli troop=taom_hero_gimli " + tail.format(f=0, w="sm_dwarf_erebor_axe_2h_c")),
        }
        for name, line in lines.items():
            self.assertEqual(sum(1 for p in hero["pass"] if re.search(p, line)), 1, name)
            self.assertFalse(hits(hero["fail"], line))
            self.assertFalse(hits(hero["pass"], line.replace(" side=enemy", "")), name)
            self.assertFalse(hits(hero["pass"], line.replace("formation=0", "formation=7").replace("formation=1", "formation=7")), name)
            self.assertFalse(hits(hero["pass"], line.replace("held=0", "held=1")), name)
        self.assertTrue(hits(hero["fail"], lines["gimli"].replace("result=pass", "result=fail")))

    def test_the_fired_row_reads_owned_here(self):
        fired = self.row("taom.hero-abilities.fired")
        self.assertEqual(len(fired["pass"]), 3)
        gimli = check_line("taom.hero-abilities.fired", "hero=taom_hero_gimli fired=1 contact_s=27 allies=58 shocked=10 owned=1")
        self.assertTrue(hits(fired["pass"], gimli))
        self.assertFalse(hits(fired["pass"], gimli.replace("owned=1", "owned=0")))
        self.assertFalse(hits(fired["pass"], gimli.replace("shocked=10", "shocked=0")))
        self.assertTrue(hits(fired["fail"], gimli.replace("result=pass", "result=fail")))

    def test_the_instant_kill_pattern_is_the_1359_pattern(self):
        import tomllib
        old = tomllib.loads((ROOT / "tools/ingame_checks/1359.toml").read_text(encoding="utf-8"))
        old_row = next(c for c in old["check"] if c["id"] == "taom.signature-strikes.instant-kill")
        for key in ("reached", "pass", "fail"):
            self.assertEqual(self.row("taom.signature-strikes.instant-kill")[key], old_row[key])

    def test_the_lord_fell_row_reads_the_attacker_side(self):
        row = self.row("taom.story-missions.hosted.lord-fell")
        line = check_line("taom.story-missions.hosted.lord-fell", "deaths=2 side=attacker")
        self.assertTrue(hits(row["pass"], line))
        self.assertFalse(hits(row["pass"], line.replace("side=attacker", "side=defender")))
        self.assertEqual(row["absence"], "not-reached")

    def test_the_hero_fall_row_is_a_pass_line(self):
        row = self.row("taom.story-missions.hosted.hero-fall")
        self.assertTrue(hits(row["pass"], check_line("taom.story-missions.hosted.hero-fall", "hero=gimli cause=killed")))
        self.assertTrue(hits(row["pass"], check_line("taom.story-missions.hosted.hero-fall", "hero=legolas cause=fled")))

    def test_the_player_start_line_judges_the_place_by_inside(self):
        row = self.row("taom.story-missions.player-start")
        good = ("[2026-11-16 11:00:01] [INFO] [LongDefeat][Player] start main=Eren owner=Eren controller=AI z=9.9 at=(227.7, 146.2, 9.9) "
                "frame=(231.0, 150.0, 9.9) inside=true")
        self.assertTrue(hits(row["pass"], good))
        self.assertFalse(hits(row["fail"], good))
        bad = good.replace("inside=true", "inside=false")
        self.assertTrue(hits(row["fail"], bad))
        self.assertFalse(hits(row["pass"], bad))
        wave = good.replace("start", "wave 1")
        self.assertFalse(hits(row["pass"], wave))
        self.assertFalse(hits(row["fail"], wave.replace("inside=true", "inside=false")))

    def test_the_engines_row_reads_arrival_and_a_destroyed_gate(self):
        row = self.row("taom.story-missions.attack-engines")
        done = ("[2026-11-16 11:00:09] [INFO] [LongDefeat][Engines] ladders [] towers arrived/destroyed/forced [False/False/True,True/False/True] "
                "rams arrived/destroyed/done/forced [True/False/False/True] gates [0.0 Destroyed destroyed True disabled False] lanes [Left open False breach False]")
        self.assertTrue(hits(row["reached"], done))
        for pattern in row["pass"]:
            self.assertTrue(re.search(pattern, done), pattern)
        still = done.replace("[False/False/True,True/False/True]", "[False/False/True,False/False/True]")
        self.assertFalse(all(re.search(p, still) for p in row["pass"]))

    def test_the_manifest_lists_the_owed_human_rows(self):
        self.assertGreaterEqual(len(self.manifest.get("human", [])), 4)
        self.assertTrue(any("HeavyCavalry" in h["claim"] and "out of walkable area" in h["claim"] for h in self.manifest["human"]))

    def judged(self, check_id, *seen):
        """The verdict ingame_verdict gives a scoped session that holds the attack launch line and this one line."""
        import ingame_verdict as iv
        manifest = iv.load_manifest(ROOT / "tools/ingame_checks/1409.toml", 1409)
        check = next(c for c in manifest.checks if c.id == check_id)
        launch = ("[2026-11-16 11:00:00] [INFO] [StoryMissions] launching taom_rohan_castle_helms_deep_forceatmo for 'helms_deep_attack' "
                  "(story1, autoplay True)")

        class Log:
            kind = "taom"
            name = "taom_debug_test.log"
            lines = list(enumerate([launch, *seen], 1))

        return iv.judge_check(check, [Log()], []).verdict

    def test_a_wrong_side_or_a_missing_frame_is_a_fail_not_a_not_reached(self):
        import ingame_verdict as iv
        general = check_line("taom.story-missions.hosted.general",
                             "general_is_main=true player_general=true order_owner_is_main=true main_on_team=true side=attacker")
        self.assertEqual(self.judged("taom.story-missions.hosted.general", general), iv.PASS)
        self.assertEqual(self.judged("taom.story-missions.hosted.general", general.replace("side=attacker", "side=defender")), iv.FAIL)
        tail = "is_hero=0 campaign=1 mounted=0 formation={f} held=0 strangers=57 order=Charge health_factor=3.00 wanted_factor=3.00 wielded={w} side=enemy"
        heroes = [check_line("taom.story-missions.hosted.hero", f"hero={n} troop=taom_hero_{n} " + tail.format(f=f, w=w))
                  for n, f, w in (("aragorn", 0, "strider_sword"), ("legolas", 1, "wm_mirkwood_bow_a01"), ("gimli", 0, "sm_dwarf_erebor_axe_2h_c"))]
        self.assertEqual(self.judged("taom.story-missions.hosted.hero", *heroes), iv.PASS)
        defence_hero = heroes[0].replace("formation=0", "formation=7").replace("held=0", "held=1").replace(" side=enemy", "")
        self.assertEqual(self.judged("taom.story-missions.hosted.hero", defence_hero, *heroes[1:]), iv.FAIL)
        start = ("[2026-11-16 11:00:01] [INFO] [LongDefeat][Player] start main=Eren owner=Eren controller=AI z=9.9 at=(227.7, 146.2, 9.9) "
                 "frame=(231.0, 150.0, 9.9) inside=true")
        self.assertEqual(self.judged("taom.story-missions.player-start", start), iv.PASS)
        self.assertEqual(self.judged("taom.story-missions.player-start", start.replace("frame=(231.0, 150.0, 9.9) ", "")), iv.FAIL)

    def test_an_ended_line_of_the_wrong_side_or_reason_is_a_fail(self):
        import ingame_verdict as iv
        for detail, want in (("verdict=Won side=Attacking reason=KeepLost waves=1", iv.PASS),
                             ("verdict=Lost side=Attacking reason=TargetReached waves=1", iv.PASS),
                             ("verdict=Won side=Defending reason=TargetReached waves=1", iv.FAIL),
                             ("verdict=Lost side=Defending reason=KeepLost waves=1", iv.FAIL),
                             ("verdict=Lost side=Attacking reason=KeepLost waves=1", iv.FAIL),
                             ("verdict=Lost side=Attacking reason=DefendersDead waves=1", iv.FAIL),
                             ("verdict=Won side=Attacking reason=TargetReached waves=1", iv.FAIL),
                             ("verdict=Lost side=Attacking reason=Abandoned waves=0", iv.FAIL)):
            with self.subTest(detail=detail):
                self.assertEqual(self.judged("taom.story-missions.ended", check_line("taom.story-missions.ended", detail)), want)


class StoryManifestTests(unittest.TestCase):
    def manifest(self, issue):
        import tomllib
        return tomllib.loads((ROOT / "tools/ingame_checks" / f"{issue}.toml").read_text(encoding="utf-8"))

    def check(self, manifest, check_id):
        return next(entry for entry in manifest["check"] if entry["id"] == check_id)

    def test_the_parth_galen_rows_match_the_driver_and_carry_their_own_scope(self):
        m = self.manifest(1105)
        for label, mission, side in (("parth-galen-save", "parth_galen_save", "Defending"), ("parth-galen-kill", "parth_galen_kill", "Attacking")):
            boot = self.check(m, f"taom.story-missions.hosted.boot.{label}")
            ended = self.check(m, f"taom.story-missions.ended.{label}")
            self.assertEqual([rs.STORY_FIELD_BOOT_PASS], boot["pass"])
            self.assertEqual([rs.story_ended_pass(side, must_live=True)], ended["pass"])
            for suffix in ("hosted.boot", "hosted.hero", "field-spawn", "ended"):
                row = self.check(m, f"taom.story-missions.{suffix}.{label}")
                self.assertEqual(row["scope"], r"\[StoryMissions\] launching \S+ for \x27" + mission + r"\x27 ")
                self.assertEqual(row["scope_end"], r"\[(LongDefeat|StoryMissions)\] launching ")

    def test_a_boromir_who_is_placed_wrongly_fails_his_row_and_a_right_one_does_not(self):
        import re
        m = self.manifest(1105)
        head = "[Check] id=taom.story-missions.hosted.hero result=pass hero=boromir troop=taom_hero_boromir is_hero=0 campaign=1 mounted=0 "
        tail = "strangers=0 order=Move health_factor=8.00 wanted_factor=8.00 wielded=wm_gondor_boromir_sword"
        held = head + "formation=7 held=1 " + tail
        cases = (("parth-galen-save", head + "formation=0 held=0 " + tail + " group=class"), ("parth-galen-kill", head + "formation=0 held=0 " + tail + " side=enemy"))
        for label, good in cases:
            row = self.check(m, f"taom.story-missions.hosted.hero.{label}")
            self.assertTrue(any(re.search(p, held) for p in row["fail"]), label + ": a hero held in the heroes' own group is a failure, not a row nobody reached")
            self.assertFalse(any(re.search(p, good) for p in row["fail"]), label)
            self.assertTrue(any(re.search(p, good) for p in row["pass"]), label)

    def test_the_helms_deep_rows_read_only_their_own_mission(self):
        m = self.manifest(1105)
        for check_id in ("taom.story-missions.hosted.boot", "taom.story-missions.ended"):
            self.assertEqual(self.check(m, check_id)["scope"], r"\[StoryMissions\] launching \S+ for \x27helms_deep_defend\x27 ")

    def test_story_patterns_match_the_1105_manifest(self):
        old = self.manifest(1105)
        self.assertEqual([rs.STORY_BOOT_PASS], self.check(old, "taom.story-missions.hosted.boot")["pass"])
        self.assertEqual([rs.story_ended_pass("Defending")], self.check(old, "taom.story-missions.ended")["pass"])

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
        self.assertIn(f'CommandsFile = "{rs.COMMANDS_FILE}"', driver)
        self.assertIn('case "command":', (scenario / "ScenarioArmFile.cs").read_text(encoding="utf-8"))

OFFER_ARGS = ["--kind", "story-offer", "--event", "breaking", "--choice", "defend"]
OFFER_DONE = ("[2026-11-21 10:00:00] [INFO] [Check] id=taom.autorun.story-offer.done result=pass reason=ok event=breaking choice=defend "
              "verdict=Lost mission_seconds=230.4 culture=gondor")
OFFER_SHOWN = ("[2026-11-21 09:50:00] [INFO] [Check] id=taom.story-missions.offer.shown result=pass mission=parth_galen_save target= "
               "choices=Defend side=Free")
OFFER_RESULT = ("[2026-11-21 09:59:00] [INFO] [Check] id=taom.story-missions.offer.result result=pass verdict=Lost result=Occurred "
                "hero=lord_1_75")
OFFER_LOG = "\n".join([OFFER_SHOWN, FIELD_LAUNCH, FIELD_BOOT, FIELD_ENDED, OFFER_RESULT, OFFER_DONE, UNLOAD_LINE, STAMP_LINE.format(HEAD)])
FATE_DONE = OFFER_DONE.replace("choice=defend verdict=Lost mission_seconds=230.4", "choice=fate verdict=- mission_seconds=0.0")
FATE_RESOLVED = ("[2026-11-21 09:55:00] [INFO] [WarChronicle] 'breaking' Occurred on day 12 (the player let fate decide; lord_1_75: Killed).")
FATE_LOG = "\n".join([OFFER_SHOWN, FATE_RESOLVED, FATE_DONE, UNLOAD_LINE, STAMP_LINE.format(HEAD)])
HORNBURG_SHOWN = ("[2026-11-21 09:50:00] [INFO] [Check] id=taom.story-missions.offer.shown result=pass mission=helms_deep_defend "
                  "target=town_V2 choices=Defend side=Free")
HORNBURG_DONE = OFFER_DONE.replace("event=breaking", "event=hornburg").replace("verdict=Lost", "verdict=Won")
HORNBURG_RESULT = ("[2026-11-21 09:59:00] [INFO] [Check] id=taom.story-missions.offer.result result=pass verdict=Won result=Held")
HORNBURG_LOG = "\n".join([HORNBURG_SHOWN, STORY_LAUNCH, STORY_BOOT, STORY_ENDED, HORNBURG_RESULT, HORNBURG_DONE, UNLOAD_LINE,
                          STAMP_LINE.format(HEAD)])


def offer_armed(**overrides):
    values = {"kind": "story-offer", "scenario": "story-offer", "culture": "gondor", "days": None, "event": "breaking", "choice": "defend",
              "hero": True, "mission": "parth_galen_save", "shown_mission": "parth_galen_save", "side": "Defending"}
    values.update(overrides)
    return SimpleNamespace(**values)


def fate_armed(**overrides):
    return offer_armed(choice="fate", mission=None, side=None, **overrides)


def hornburg_armed(**overrides):
    return offer_armed(event="hornburg", hero=False, mission="helms_deep_defend", shown_mission="helms_deep_defend", **overrides)


def good_offer_done(**overrides):
    done = {"reason": "ok", "kind": "story-offer", "scenario": "story-offer", "culture": "gondor", "event": "breaking", "choice": "defend",
            "verdict": "Lost", "mission_seconds": "230.4"}
    done.update(overrides)
    return done


def offer_judge(done, log_text, armed=None):
    return rs.judge(done, None, log_text, armed=armed or offer_armed(), checkout=(HEAD, {}))


class OfferArmTextTests(unittest.TestCase):
    def test_the_offer_kind_writes_its_keys_in_order_with_the_open_command_first_and_the_status_after(self):
        known, _ = parse(OFFER_ARGS + ["--command", "taom.other 1"])
        text, wall = rs.arm_text_for(Path(r"E:\x\run"), known, SimpleNamespace(wall_limit=None))
        self.assertEqual([line.split("=", 1)[0] for line in text.splitlines()],
                         ["kind", "out", "scenario", "event", "choice", "mission_limit_seconds", "culture", "speed", "wall_limit_seconds", "quit",
                          "command", "command", "after"])
        lines = text.splitlines()
        self.assertEqual(lines[:2], ["kind=story-offer", r"out=E:\x\run"])
        self.assertEqual(lines[-3:], ["command=taom.chronicle_open breaking confirm", "command=taom.other 1", "after=taom.chronicle_status"])
        self.assertIn("event=breaking\nchoice=defend\n", text)
        self.assertIn("speed=1\n", text)
        self.assertEqual((wall, arm_dict_first(text)["wall_limit_seconds"]), (3000, "3000"))

    def test_the_event_and_the_choice_are_stripped_lowercased_and_read_once(self):
        known, _ = parse(["--kind", "story-offer", "--event", " Hornburg ", "--choice", "defend"])
        self.assertEqual((known.event, known.mission, known.shown_mission, known.side, known.hero), ("hornburg", "helms_deep_defend", "helms_deep_defend", "Defending", False))
        known, _ = parse(OFFER_ARGS)
        self.assertEqual((known.mission, known.hero, known.side), ("parth_galen_save", True, "Defending"))

    def test_attack_launches_the_attack_mission_and_fate_launches_none(self):
        known, _ = parse(["--kind", "story-offer", "--event", "breaking", "--choice", "attack", "--culture", "isengard"])
        self.assertEqual((known.mission, known.shown_mission, known.side), ("parth_galen_kill", "parth_galen_save", "Attacking"))
        known, _ = parse(["--kind", "story-offer", "--event", "breaking", "--choice", "fate"])
        self.assertEqual((known.mission, known.side, known.shown_mission), (None, None, "parth_galen_save"))

    def test_the_runner_wall_limit_wins_for_the_offer_kind(self):
        known, _ = parse(OFFER_ARGS)
        text, wall = rs.arm_text_for(Path(r"E:\x"), known, SimpleNamespace(wall_limit=4000.0))
        self.assertEqual((wall, arm_dict_first(text)["wall_limit_seconds"]), (4000.0, "4000"))

    def test_the_offer_flags_are_required_and_checked_against_the_chronicle(self):
        self.assertIn("--event", parse_fails(["--kind", "story-offer", "--choice", "defend"]))
        self.assertIn("--choice", parse_fails(["--kind", "story-offer", "--event", "breaking"]))
        message = parse_fails(["--kind", "story-offer", "--event", "nowhere", "--choice", "defend"])
        self.assertIn("nowhere", message)
        self.assertIn("hornburg", message)
        self.assertIn("breaking", message)
        self.assertIn("invalid choice", parse_fails(["--kind", "story-offer", "--event", "breaking", "--choice", "run"]))

    def test_hornburg_ships_no_attack_so_attack_is_refused_for_it(self):
        message = parse_fails(["--kind", "story-offer", "--event", "hornburg", "--choice", "attack", "--culture", "isengard"])
        self.assertIn("attackMission", message)

    def test_the_culture_must_be_able_to_see_the_chosen_side(self):
        self.assertIn("isengard", parse_fails(["--kind", "story-offer", "--event", "breaking", "--choice", "attack"]))
        self.assertIn("evil", parse_fails(["--kind", "story-offer", "--event", "breaking", "--choice", "defend", "--culture", "isengard"]))
        known, _ = parse(["--kind", "story-offer", "--event", "breaking", "--choice", "fate", "--culture", "isengard"])
        self.assertEqual(known.culture, "isengard")

    def test_fate_needs_a_culture_that_sees_a_side_of_the_event(self):
        # StorySideGate.Choose: the player's side must not be opposed to the defender (defend) or the first attacker (attack, if one ships).
        message = parse_fails(["--kind", "story-offer", "--event", "hornburg", "--choice", "fate", "--culture", "isengard"])
        self.assertIn("no side", message)
        self.assertIn("hornburg", message)
        known, _ = parse(["--kind", "story-offer", "--event", "breaking", "--choice", "fate", "--culture", "isengard"])
        self.assertEqual(known.culture, "isengard")
        known, _ = parse(["--kind", "story-offer", "--event", "hornburg", "--choice", "fate"])
        self.assertEqual(known.mission, None)

    def test_the_sides_follow_the_events_own_defender_and_first_attacker(self):
        row = {"defender": "vlandia", "attackers": ["isengard", "empire"], "story": {"mission": "m"}}
        self.assertEqual(rs.offer_sides("free", row), (True, False))
        self.assertEqual(rs.offer_sides("evil", row), (False, False))
        self.assertEqual(rs.offer_sides("neutral", row), (True, False))
        row["story"]["attackMission"] = "a"
        self.assertEqual(rs.offer_sides("free", row), (True, False))
        self.assertEqual(rs.offer_sides("evil", row), (False, True))
        self.assertEqual(rs.offer_sides("neutral", row), (True, True))
        evil_defender = {"defender": "mordor", "attackers": ["gondor"], "story": {"mission": "m", "attackMission": "a"}}
        self.assertEqual(rs.offer_sides("evil", evil_defender), (True, False))

    def test_a_culture_the_alignment_table_does_not_list_is_refused_for_the_offer_kind(self):
        message = parse_fails(["--kind", "story-offer", "--event", "breaking", "--choice", "fate", "--culture", "nowhere"])
        self.assertIn("alignment.json", message)
        self.assertIn("nowhere", message)
        with mock.patch.object(rs, "ALIGNMENT_FILE", Path(r"E:\nowhere\alignment.json")):
            self.assertIn("alignment.json", parse_fails(OFFER_ARGS))

    def test_the_named_evil_culture_is_a_creation_culture_and_evil_in_the_alignment_table(self):
        cultures = json.loads((ROOT / "Main/_Module/ModuleData/charactercreation/cultures.json").read_text(encoding="utf-8-sig"))
        self.assertIn(rs.OFFER_EVIL_CULTURE, {entry["culture_id"] for entry in json_walk(cultures) if "culture_id" in entry})
        self.assertEqual(rs.culture_alignment(rs.OFFER_EVIL_CULTURE), "evil")
        self.assertEqual(rs.culture_alignment(rs.DEFAULT_CULTURE), "free")
        self.assertIsNone(rs.culture_alignment("nowhere"))

    def test_the_foreign_flags_are_refused_with_the_offer_kind(self):
        for flag, value in (("--days", "3"), ("--speed", "5"), ("--stall-seconds", "60"), ("--mission", "helms_deep_defend")):
            with self.subTest(flag=flag):
                self.assertIn(flag, parse_fails(OFFER_ARGS + [flag, value]))

    def test_the_offer_flags_are_refused_with_the_other_kinds(self):
        for argv in (["--event", "breaking"], ["--choice", "defend"], STORY_ARGS + ["--event", "breaking"]):
            with self.subTest(argv=argv):
                self.assertIn("only for --kind story-offer", parse_fails(argv))

    def test_the_story_events_are_read_from_the_chronicle_json(self):
        stories = rs.chronicle_stories()
        self.assertEqual(sorted(stories), ["breaking", "hornburg"])
        self.assertTrue(stories["breaking"]["hero"])
        self.assertFalse(stories["hornburg"]["hero"])
        self.assertEqual(stories["breaking"]["story"]["attackMission"], "parth_galen_kill")

    def test_an_unreadable_chronicle_is_an_empty_table_and_is_named_at_parse(self):
        with mock.patch.object(rs, "CHRONICLE_FILE", Path(r"E:\nowhere\war_chronicle.json")):
            self.assertEqual(rs.chronicle_stories(), {})
            self.assertIn("could not be read", parse_fails(OFFER_ARGS))

    def test_the_offer_speed_is_a_speed_the_arm_file_takes(self):
        self.assertTrue(0 < rs.OFFER_SPEED <= rs.MAX_SPEED)


def arm_dict_first(text):
    """arm_dict for a text with repeated keys: the first value of each."""
    result = {}
    for line in text.splitlines():
        key, value = line.split("=", 1)
        result.setdefault(key, value)
    return result


class OfferJudgeTests(unittest.TestCase):
    def test_a_defence_through_the_offer_passes(self):
        code, why = offer_judge(good_offer_done(), OFFER_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn("event breaking choice defend verdict Lost", why)

    def test_a_hornburg_siege_defence_passes_with_the_siege_boot_line(self):
        done = good_offer_done(event="hornburg", verdict="Won")
        code, why = offer_judge(done, HORNBURG_LOG, hornburg_armed())
        self.assertEqual(code, 0, why)
        code, why = offer_judge(done, HORNBURG_LOG.replace("siege_ai=true", "siege_ai=false"), hornburg_armed())
        self.assertEqual(code, 1)
        self.assertIn("hosted.boot", why)

    def test_an_attack_passes_with_the_attack_mission_launch_and_side_attacking(self):
        launch = FIELD_LAUNCH.replace("parth_galen_save", "parth_galen_kill")
        ended = STORY_ENDED.replace("verdict=Won side=Defending reason=TargetReached", "verdict=Won side=Attacking reason=KeepLost")
        done = OFFER_DONE.replace("choice=defend", "choice=attack").replace("verdict=Lost", "verdict=Won")
        log = "\n".join([OFFER_SHOWN, launch, FIELD_BOOT, ended, OFFER_RESULT, done, UNLOAD_LINE, STAMP_LINE.format(HEAD)])
        armed = offer_armed(choice="attack", mission="parth_galen_kill", side="Attacking")
        code, why = offer_judge(good_offer_done(choice="attack", verdict="Won"), log, armed)
        self.assertEqual(code, 0, why)
        code, why = offer_judge(good_offer_done(choice="attack", verdict="Won"), log.replace("side=Attacking", "side=Defending"), armed)
        self.assertEqual(code, 1)
        self.assertIn("side=Attacking", why)

    def test_a_fate_run_on_a_hero_story_passes_on_the_chronicles_resolve_line(self):
        code, why = offer_judge(good_offer_done(choice="fate", verdict="-"), FATE_LOG, fate_armed())
        self.assertEqual(code, 0, why)
        code, why = offer_judge(good_offer_done(choice="fate", verdict="-"), without(FATE_LOG, "[WarChronicle]"), fate_armed())
        self.assertEqual(code, 1)
        self.assertIn("[WarChronicle] line", why)

    def test_a_fate_run_on_a_fortress_reads_the_decline_line_and_may_open_no_mission(self):
        declined = "[2026-11-21 09:55:00] [INFO] [WarChronicle] 'hornburg': the player let fate decide the story offer."
        done = HORNBURG_DONE.replace("choice=defend verdict=Won mission_seconds=230.4", "choice=fate verdict=- mission_seconds=0.0")
        log = "\n".join([HORNBURG_SHOWN, declined, done, UNLOAD_LINE, STAMP_LINE.format(HEAD)])
        armed = fate_armed(event="hornburg", hero=False, shown_mission="helms_deep_defend")
        code, why = offer_judge(good_offer_done(event="hornburg", choice="fate", verdict="-"), log, armed)
        self.assertEqual(code, 0, why)
        code, why = offer_judge(good_offer_done(event="hornburg", choice="fate", verdict="-"), log + "\n" + STORY_LAUNCH, armed)
        self.assertEqual(code, 1)
        self.assertIn("opens no mission", why)

    def test_every_missing_line_fails_with_its_own_reason(self):
        for needle, want in (("offer.done", "story-offer.done result=pass line"), ("offer.shown", "offer.shown pass line"),
                             ("hosted.boot", "hosted.boot pass line"), ("story-missions.ended", "ended pass line"),
                             ("offer.result", "offer.result pass line"), ("launching", "launching lines")):
            with self.subTest(needle=needle):
                needle = "story-offer.done" if needle == "offer.done" else needle
                code, why = offer_judge(good_offer_done(), without(OFFER_LOG, needle))
                self.assertEqual(code, 1)
                self.assertIn(want, why)

    def test_the_shown_line_must_name_the_events_story_mission(self):
        code, why = offer_judge(good_offer_done(), OFFER_LOG.replace("mission=parth_galen_save target=", "mission=helms_deep_defend target="))
        self.assertEqual(code, 1)
        self.assertIn("offer.shown pass line for mission parth_galen_save", why)

    def test_a_failed_result_line_is_no_pass(self):
        code, why = offer_judge(good_offer_done(), OFFER_LOG.replace("offer.result result=pass", "offer.result result=fail"))
        self.assertEqual(code, 1)
        self.assertIn("offer.result pass line", why)

    def test_a_second_launch_fails(self):
        code, why = offer_judge(good_offer_done(), OFFER_LOG + "\n" + FIELD_LAUNCH)
        self.assertEqual(code, 1)
        self.assertIn("launching lines", why)

    def test_the_done_pass_line_must_name_the_event_the_choice_and_a_decided_verdict(self):
        for bad in (OFFER_DONE.replace("event=breaking", "event=hornburg"), OFFER_DONE.replace("choice=defend", "choice=attack"),
                    OFFER_DONE.replace("verdict=Lost", "verdict=Undecided"), OFFER_DONE.replace("verdict=Lost", "verdict=-"),
                    OFFER_DONE.replace("result=pass", "result=fail")):
            with self.subTest(line=bad):
                code, why = offer_judge(good_offer_done(), OFFER_LOG.replace(OFFER_DONE, bad))
                self.assertEqual(code, 1)
                self.assertIn("story-offer.done result=pass line", why)

    def test_a_hero_fate_run_with_a_launching_line_fails(self):
        code, why = offer_judge(good_offer_done(choice="fate", verdict="-"), FATE_LOG + "\n" + FIELD_LAUNCH, fate_armed())
        self.assertEqual(code, 1)
        self.assertIn("opens no mission", why)

    def test_a_hero_fate_run_that_holds_only_the_fortress_decline_text_fails(self):
        fortress = "[2026-11-21 09:55:00] [INFO] [WarChronicle] 'breaking': the player let fate decide the story offer."
        log = FATE_LOG.replace(FATE_RESOLVED, fortress)
        code, why = offer_judge(good_offer_done(choice="fate", verdict="-"), log, fate_armed())
        self.assertEqual(code, 1)
        self.assertIn("[WarChronicle] line", why)

    def test_a_fate_done_line_needs_the_dash_verdict(self):
        code, why = offer_judge(good_offer_done(choice="fate", verdict="Won"), FATE_LOG, fate_armed())
        self.assertEqual(code, 1)
        self.assertIn("wanted -", why)

    def test_the_done_file_must_echo_the_armed_event_and_choice(self):
        code, why = offer_judge(good_offer_done(event="hornburg", choice="attack"), OFFER_LOG)
        self.assertEqual(code, 1)
        self.assertIn("event=hornburg in the done file, but the arm file asked for breaking", why)
        self.assertIn("choice=attack in the done file, but the arm file asked for defend", why)

    def test_the_verdict_and_the_reason_still_gate(self):
        code, why = offer_judge(good_offer_done(verdict="Undecided"), OFFER_LOG)
        self.assertEqual(code, 1)
        self.assertIn("wanted Won or Lost", why)
        for reason in ("offer_not_shown", "offer_refused", "mission_not_opened", "return_failed"):
            with self.subTest(reason=reason):
                code, why = offer_judge(good_offer_done(reason=reason), OFFER_LOG)
                self.assertEqual(code, 1)
                self.assertIn(f"reason={reason} (wanted ok)", why)

    def test_a_crash_block_a_missing_unload_and_a_stale_build_still_fail(self):
        self.assertIn("CrashReport", offer_judge(good_offer_done(), OFFER_LOG + "\n" + CRASH_LINE)[1])
        self.assertIn("clean module unload", offer_judge(good_offer_done(), without(OFFER_LOG, "clean module unload"))[1])
        self.assertIn("not this checkout", offer_judge(good_offer_done(), OFFER_LOG.replace(HEAD, "b" * 40))[1])

    def test_an_offer_run_is_judged_against_its_armed_plan_only(self):
        code, why = rs.judge(good_offer_done(), None, OFFER_LOG, checkout=(HEAD, {}))
        self.assertEqual(code, 1)
        self.assertIn("armed plan", why)


class OfferWiringTests(unittest.TestCase):
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

    def test_the_offer_kind_uses_its_own_printer_judge_and_the_long_defeat_dialog_handler(self):
        _, seen = self.run_main(OFFER_ARGS + ["--dry-run"])
        self.assertIs(seen["show_results"], rs.show_offer_results)
        self.assertIs(seen["handle_dialogs"], rs.long_defeat.handle_dialogs)
        self.assertEqual(seen["rest"], ["--dry-run"])
        code, why = seen["judge"](good_offer_done(), None, OFFER_LOG)
        self.assertEqual(code, 0, why)
        self.assertIn("event=breaking", seen["arm"][0])

    def test_the_offer_printer_prints_the_check_lines_the_event_lines_and_the_status_entry_and_no_ledger(self):
        status = ("[2026-11-21 09:59:30] [INFO] [Autorun][Scenario] command taom.chronicle_status\n[WarChronicle] Chronicle on campaign day 12\n"
                  "  breaking (Narrative) Occurred\n  hornburg (Siege) Pending\n[2026-11-21 09:59:31] [INFO] [Autorun][Scenario] phase x")
        other = "[2026-11-21 09:55:01] [INFO] [WarChronicle] 'hornburg' Active on day 3 (opened)."
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / "taom_debug_x.log"
            log.write_text(OFFER_LOG + "\n" + FATE_RESOLVED + "\n" + other + "\n" + status + "\n", encoding="utf-8")
            (Path(folder) / rs.COMMANDS_FILE).write_text("> taom.chronicle_status\nTOKEN_OFFER\n", encoding="utf-8")
            messages, out = [], io.StringIO()
            with mock.patch.object(rs.subprocess, "run") as run, contextlib.redirect_stdout(out):
                rs.show_offer_results("reason=ok\nevent=breaking\nchoice=defend\n", log, None, messages.append)
            run.assert_not_called()
            text = out.getvalue()
            for want in ("reason=ok", "story-offer.done result=pass", "offer.shown", "offer.result", "story-missions.ended", "launching taom_rohan",
                         "[WarChronicle] 'breaking' Occurred", "---- taom.chronicle_status ----", "  breaking (Narrative) Occurred", "TOKEN_OFFER"):
                self.assertIn(want, text)
            self.assertNotIn("'hornburg' Active", text)
            self.assertNotIn("phase x", text)
            self.assertTrue(any("clean module unload line: present" in m for m in messages))
            self.assertFalse((Path(folder) / "ledger.txt").exists())

    def test_the_offer_printer_prints_the_drivers_refusal_warning_with_its_pending_text(self):
        refusal = ("[2026-11-21 09:52:00] [WARNING] [Autorun][Scenario] offer_not_shown: the offer popup never showed in 120 s pending=none "
                   "command output: [WarChronicle] Opened: 'breaking' is Active until day 9. open_menu=none")
        other = "[2026-11-21 09:52:01] [WARNING] [Other] offer_not_shown: not the driver"
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / "taom_debug_x.log"
            log.write_text(refusal + "\n" + other + "\n" + UNLOAD_LINE, encoding="utf-8")
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                rs.show_offer_results("reason=offer_not_shown\nevent=breaking\n", log, None, lambda m: None)
            self.assertIn("offer_not_shown: the offer popup never showed in 120 s pending=none", out.getvalue())
            self.assertNotIn("not the driver", out.getvalue())

    def test_the_offer_printer_names_a_missing_status_entry_and_a_missing_log(self):
        with tempfile.TemporaryDirectory() as folder:
            log = Path(folder) / "taom_debug_x.log"
            log.write_text(NOT_ARMED_LOG, encoding="utf-8")
            messages, out = [], io.StringIO()
            with contextlib.redirect_stdout(out):
                rs.show_offer_results("", log, None, messages.append)
            self.assertIn("no taom.chronicle_status entry", out.getvalue())
            self.assertTrue(any("did not arm" in m for m in messages))
        messages = []
        with contextlib.redirect_stdout(io.StringIO()):
            rs.show_offer_results("reason=offer_not_shown\n", None, None, messages.append)
        self.assertTrue(any("no TAOM log copy" in m for m in messages))

    def test_help_names_the_offer_arguments(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit):
            rs.parse_own(["--help"])
        for want in ("story-offer", "--event", "--choice", "isengard"):
            self.assertIn(want, out.getvalue())

    def test_driver_source_keeps_the_offer_names_the_judge_and_the_printer_read(self):
        scenario = ROOT / "Main/Features/Autorun/Scenario"
        self.assertIn(f'StoryOfferDoneId = "{rs.OFFER_CHECK_ID}"', (scenario / "ScenarioOutcomes.cs").read_text(encoding="utf-8"))
        self.assertIn('KindStoryOffer = "story-offer"', (scenario / "ScenarioPlan.cs").read_text(encoding="utf-8"))
        arm = (scenario / "ScenarioArmFile.cs").read_text(encoding="utf-8")
        for key in ('case "event":', 'case "choice":', 'case "after":'):
            self.assertIn(key, arm)
        offer = (scenario / "ScenarioAutorunDriver.Offer.cs").read_text(encoding="utf-8")
        for reason in ("offer_refused", "offer_not_shown", "return_failed"):
            self.assertIn(reason, offer)
        driver = (scenario / "ScenarioAutorunDriver.cs").read_text(encoding="utf-8")
        self.assertIn('Prefix + "command " + command', driver)
        self.assertIn("RunCommands(_plan.AfterCommands)", offer)
        service = (ROOT / "Main/Features/WarChronicle/Events/ChronicleService.cs").read_text(encoding="utf-8")
        self.assertIn("[WarChronicle] '{definition.Id}' {outcome} on day {Day(now)} ({why}).", service)
        self.assertIn("[WarChronicle] '{eventId}': the player let fate decide the story offer.", service)
        self.assertIn('"the player let fate decide"', service)
        shown = (ROOT / "Main/Features/StoryMissions/StoryOfferService.cs").read_text(encoding="utf-8")
        self.assertIn('ShownId = "taom.story-missions.offer.shown"', shown)
        self.assertIn('ResultId = "taom.story-missions.offer.result"', shown)
        self.assertIn("$\"mission={offer.Window.MissionId} target=", shown)

    def test_the_chronicle_open_command_prints_the_opened_line_the_driver_looks_for(self):
        report = (ROOT / "Main/Features/WarChronicle/Cheats/ChronicleCheatReport.cs").read_text(encoding="utf-8")
        self.assertIn('"[WarChronicle] Opened: "', report)
        rules = (ROOT / "Main/Features/Autorun/Scenario/ScenarioRules.cs").read_text(encoding="utf-8")
        self.assertIn('"[WarChronicle] Opened: \'" + eventId + "\' is Active until day"', rules)
        service = (ROOT / "Main/Features/WarChronicle/Events/ChronicleService.cs").read_text(encoding="utf-8")
        self.assertIn("$\"'{definition.Id}' is Active until day {Day(record.CloseDay)}.\"", service)


if __name__ == "__main__":
    unittest.main()
