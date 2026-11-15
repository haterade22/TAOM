#!/usr/bin/env python
"""tools/run_scenario.py: the arm text, the arguments, the judge and the wiring into the Fords runner, with no game.

Run:  python -m unittest tools.tests.test_run_scenario
"""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

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


GOOD_LOG = "\n".join([PASS_LINE, UNLOAD_LINE])


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
        self.assertEqual(rs.judge(good_done(), None, GOOD_LOG)[0], 0)

    def test_more_days_than_wanted_passes(self):
        self.assertEqual(rs.judge(good_done(days_run="4"), None, GOOD_LOG)[0], 0)

    def test_no_done_file(self):
        code, why = rs.judge({}, None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("no done file", why)

    def test_short_days(self):
        code, why = rs.judge(good_done(days_run="2"), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("days_run=2", why)

    def test_unreadable_days(self):
        code, why = rs.judge(good_done(days_run="many"), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("days_run", why)

    def test_non_ok_reason(self):
        for reason in ("stalled", "wall_limit", "map_timeout", "creation_failed", "start_refused", "creation_timeout"):
            with self.subTest(reason=reason):
                code, why = rs.judge(good_done(reason=reason), None, GOOD_LOG)
                self.assertEqual(code, 1)
                self.assertIn("reason=" + reason, why)

    def test_missing_pass_line(self):
        code, why = rs.judge(good_done(), None, UNLOAD_LINE)
        self.assertEqual(code, 1)
        self.assertIn("result=pass", why)

    def test_fail_line_is_not_a_pass(self):
        log = "[Check] id=taom.autorun.campaign-new.done result=fail\n" + UNLOAD_LINE
        self.assertEqual(rs.judge(good_done(), None, log)[0], 1)

    def test_no_log(self):
        code, why = rs.judge(good_done(), None, None)
        self.assertEqual(code, 1)
        self.assertIn("no TAOM log copy", why)

    def test_crash_line_before_the_driver_started_fails(self):
        log = "[2026-11-15 08:59:59] [ERROR] [CrashReport] init failed: X\n" + GOOD_LOG
        code, why = rs.judge(good_done(started="2026-11-15T09:00:00"), None, log)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_done_file_must_echo_the_armed_plan(self):
        armed = SimpleNamespace(kind="campaign-new", culture="gondor", days=150, scenario="campaign-new")
        code, why = rs.judge(good_done(culture="gondor", scenario="campaign-new"), None, GOOD_LOG, armed=armed)
        self.assertEqual(code, 1)
        self.assertIn("days_wanted=3", why)
        done = good_done(culture="gondor", scenario="campaign-new", days_run="150", days_wanted="150")
        self.assertEqual(rs.judge(done, None, GOOD_LOG, armed=armed)[0], 0)

    def test_arm_file_warning_in_the_log_fails(self):
        log = GOOD_LOG + "\n[2026-11-15 09:00:01] [WARNING] [Autorun][Scenario] arm file: speed '1e-50' is not a number above 0"
        code, why = rs.judge(good_done(), None, log)
        self.assertEqual(code, 1)
        self.assertIn("arm file", why)

    def test_build_must_be_this_tree(self):
        head = "a" * 40
        stamped = GOOD_LOG + "\n[2026-11-15 09:00:00] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261115-090000Z+{} TAOM.Dependencies=v0.1.0.0 (pair OK)"
        cases = (("b" * 40, 1), (head, 0), (head + ".dirty", 0))
        for sha, want in cases:
            with self.subTest(sha=sha):
                self.assertEqual(rs.judge(good_done(), None, stamped.format(sha), head=head)[0], want)
        code, why = rs.judge(good_done(), None, GOOD_LOG, head=head)
        self.assertEqual(code, 1)
        self.assertIn("BuildStamp", why)

    def test_driver_source_keeps_the_arm_warning_prefix(self):
        source = (Path(__file__).resolve().parents[2] / "Main/Features/Autorun/Scenario/ScenarioAutorunDriver.cs").read_text(encoding="utf-8")
        self.assertIn('"[Autorun][Scenario] "', source)
        self.assertIn('"arm file: "', source)

    def test_crash_marker(self):
        code, why = rs.judge(good_done(), None, GOOD_LOG + "\n" + CRASH_LINE)
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_missing_unload_line(self):
        code, why = rs.judge(good_done(), None, PASS_LINE)
        self.assertEqual(code, 1)
        self.assertIn("unload", why)

    def test_every_failure_gets_its_own_reason(self):
        code, why = rs.judge(good_done(reason="stalled", days_run="1"), None, "")
        self.assertEqual(code, 1)
        self.assertIn("reason=stalled", why)
        self.assertIn("days_run=1", why)
        self.assertIn("result=pass", why)


NAMES = ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "arm_text_for", "judge", "show_results", "handle_dialogs", "STALL_SECONDS", "main")


class MainWiringTests(unittest.TestCase):
    def setUp(self):
        saved = {name: getattr(runner, name) for name in NAMES}
        self.addCleanup(lambda: [setattr(runner, name, value) for name, value in saved.items()])

    def run_main(self, argv):
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
            code = rs.main(argv)
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
        with mock.patch.object(rs, "tree_head_sha", return_value=None):
            code, why = got["judge"](good_done(culture="gondor", scenario="campaign-new"), None, GOOD_LOG)
        self.assertEqual(code, 1)
        self.assertIn("days_wanted=3", why)
        self.assertIn("BuildStamp", why)

    def test_runner_judge_compares_with_the_head_read_at_launch(self):
        # A commit made while the game runs must not fail the run as a stale deploy.
        with mock.patch.object(rs, "tree_head_sha", return_value="a" * 40):
            _, got = self.run_main(["--days", "3"])
        seen = {}
        with mock.patch.object(rs, "tree_head_sha", return_value="b" * 40), \
                mock.patch.object(rs, "judge", side_effect=lambda *a: seen.setdefault("head", a[4]) and (0, "")):
            got["judge"](good_done(), None, GOOD_LOG)
        self.assertEqual(seen["head"], "a" * 40)

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


if __name__ == "__main__":
    unittest.main()
