#!/usr/bin/env python
"""tools/run_command_trial.py: the judge, the build check, the arguments and one whole run with a fake game.

Run:  python -m unittest tools.tests.test_run_command_trial
"""
import contextlib
import io
import os
import re
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import run_command_trial as trial  # noqa: E402
import run_fords_autorun as runner  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
TRIAL_SOURCE = REPO / "Client" / "Features" / "SeatOfSeeing" / "Trial"
DOMAIN_SOURCE = REPO / "Main" / "Features" / "SeatOfSeeing" / "Domain"
STEPS_SOURCE = DOMAIN_SOURCE / "SeatTrialRows.cs"

FINAL = {"reason": "final", "started": "2026-11-04T10:00:00"}
HEAD = "1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc"
BUILT = datetime(2026, 11, 4, 9, 11, 4)

# The stamp line of the first real run (E:/Temp/command-trial-runs/20261104-031119): UTC build time 09:11:04, the log's own clock 03:11 local.
STAMP_LINE = ("[2026-11-04 03:11:29] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261104-091104Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty "
              "TAOM.Dependencies=v0.1.0.0 build.20261104-091104Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty (pair OK)")
# The same build with a clean tree: a clean stamp is the default of full_log, because a dirty one needs a changed file to be this checkout.
CLEAN_STAMP_LINE = STAMP_LINE.replace(".dirty", "")
UNLOAD_LINE = "[2026-11-04 03:12:21] [INFO] [Shutdown] clean module unload"
OLDER_CHANGE = {"Main/a.cs": datetime(2026, 11, 4, 9, 0, 0)}

# The newest real run (E:/Temp/command-trial-runs/20261104-042903), copied from its log: the build stamp, the six step lines and the unload line.
REAL_RUN_LINES = (
    "[2026-11-04 04:29:13] [INFO] [BuildStamp] TAOM=v2.0.0.0 build.20261104-102854Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty "
    "TAOM.Dependencies=v0.1.0.0 build.20261104-102854Z+1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc.dirty (pair OK)",
    "[2026-11-04 04:29:47] [INFO] [SeatTrial] step=refuse_in_deployment result=PASS taken=1 mode=Deployment state=Off",
    "[2026-11-04 04:29:55] [INFO] [SeatTrial] step=enter result=PASS rising=1 seated=1 frames=244 controller=Player",
    "[2026-11-04 04:29:57] [INFO] [SeatTrial] step=fly result=PASS moved=60.0 expected=59.9 height=22.0 seconds=2.0",
    "[2026-11-04 04:29:58] [INFO] [SeatTrial] step=hero_stands result=PASS speed=0.0 seconds=1.5",
    "[2026-11-04 04:29:59] [INFO] [SeatTrial] step=leave_glide result=PASS glide=1 before=55.0 after=37.1 near=10.0 seconds=0.4",
    "[2026-11-04 04:30:00] [INFO] [SeatTrial] step=reenter_keeps_height result=PASS before=22.0 after=22.0",
    "[2026-11-04 04:30:01] [INFO] [Shutdown] clean module unload",
)


def pass_line(step, facts="x=1", level="INFO"):
    return f"[2026-11-04 10:01:00] [{level}] [SeatTrial] step={step} result=PASS {facts}"


def fail_line(step, facts="reason=timeout"):
    return f"[2026-11-04 10:01:00] [WARNING] [SeatTrial] step={step} result=FAIL {facts}"


def full_log(skip=(), extra=(), stamp=CLEAN_STAMP_LINE, unload=True):
    rows = [stamp] if stamp else []
    rows += [pass_line(step) for step in trial.STEPS if step not in skip]
    rows += list(extra)
    if unload:
        rows.append(UNLOAD_LINE)
    return "\n".join(rows)


def judge(done, report_json, log_text=None, **kwargs):
    """trial.judge with this checkout's HEAD and a clean tree (no changed file), unless a test says otherwise."""
    kwargs.setdefault("head", HEAD)
    kwargs.setdefault("changed", {})
    return trial.judge(done, report_json, log_text, **kwargs)


RUNNER_NAMES = ("ENV_VAR", "DONE_FILE", "STATUS_FILE", "RUNS_ROOT", "judge", "arm_text_for")


def restoring_runner(**patches):
    """Patches module globals of the Fords runner and puts every one back afterwards: main() sets six of them, and other tests in the same
    process read them."""
    keep = {name: getattr(runner, name) for name in RUNNER_NAMES}
    keep.update(patches)
    return mock.patch.multiple(runner, **keep)


class JudgeTests(unittest.TestCase):

    def test_no_done_file_fails(self):
        self.assertEqual(judge(None, None, full_log())[0], 1)

    def test_a_full_log_with_reason_final_passes(self):
        code, why = judge(FINAL, None, full_log())
        self.assertEqual(code, 0, why)
        self.assertIn(str(len(trial.STEPS)), why)

    def test_the_pass_text_names_the_build_it_judged(self):
        code, why = judge(FINAL, None, full_log())
        self.assertEqual(code, 0, why)
        self.assertIn("1735f2add built", why)
        self.assertIn("20261104-091104", why)
        code, why = judge(FINAL, None, full_log(stamp=STAMP_LINE), changed=OLDER_CHANGE)
        self.assertEqual(code, 0, why)
        self.assertIn("1735f2add.dirty", why)

    def test_a_reason_other_than_final_fails_by_name(self):
        for reason in ("live_limit", "never_live", "wall_limit", "mission_closed", "start_refused", "start_failed", "script_failed", "exit_stalled", "final+exit_stalled"):
            self.assertEqual(judge({"reason": reason}, None, full_log()), (1, f"reason={reason}"), reason)

    def test_a_done_file_with_no_reason_fails(self):
        self.assertEqual(judge({}, None, full_log())[0], 1)

    def test_no_log_fails(self):
        self.assertEqual(judge(FINAL, None, None)[0], 1)
        self.assertEqual(judge(FINAL, None, "")[0], 1)

    def test_any_fail_line_fails_and_names_the_step(self):
        log = full_log(skip=("fly",), extra=(fail_line("fly"),))
        code, why = judge(FINAL, None, log)
        self.assertEqual(code, 1)
        self.assertIn("fly", why)
        self.assertIn("FAIL", why)

    def test_a_fail_line_beats_a_pass_line_of_the_same_step_in_either_order(self):
        passing_then_failing = full_log(extra=(fail_line("enter"),))
        failing_then_passing = "\n".join([CLEAN_STAMP_LINE, fail_line("enter"), full_log().replace(CLEAN_STAMP_LINE + "\n", "", 1)])
        for log in (passing_then_failing, failing_then_passing):
            code, why = judge(FINAL, None, log)
            self.assertEqual(code, 1)
            self.assertIn("enter", why)

    def test_a_result_fail_line_of_any_tag_fails(self):
        log = full_log(extra=("[2026-11-04 10:01:05] [WARNING] [OtherFeature] something result=FAIL else",))
        code, why = judge(FINAL, None, log)
        self.assertEqual(code, 1)
        self.assertIn("something result=FAIL", why)

    def test_each_missing_step_line_fails_by_name(self):
        for step in trial.STEPS:
            code, why = judge(FINAL, None, full_log(skip=(step,)))
            self.assertEqual(code, 1, step)
            self.assertIn(step, why)

    def test_a_step_named_only_in_the_armed_line_is_still_missing(self):
        log = full_log(skip=("fly",), extra=("[2026-11-04 10:00:31] [INFO] [SeatTrial] armed: steps=fly out=E:/x",))
        code, why = judge(FINAL, None, log)
        self.assertEqual(code, 1)
        self.assertIn("fly", why)

    def test_only_the_steps_that_were_armed_are_required(self):
        log = "\n".join([CLEAN_STAMP_LINE, pass_line("enter"), pass_line("fly"), UNLOAD_LINE])
        self.assertEqual(judge(FINAL, None, log, steps=("enter", "fly"))[0], 0)
        self.assertEqual(judge(FINAL, None, log, steps=("enter", "fly", "hero_stands"))[0], 1)

    def test_a_crash_report_block_fails_even_before_the_start(self):
        block = "[2026-11-04 09:59:59] [ERROR] [CrashReport] swallowed during the mission load"
        if runner.crash_report_line(block, None) is None:
            self.fail("the sample line does not match the runner's CrashReport pattern")
        code, why = judge(FINAL, None, full_log(extra=(block,)))
        self.assertEqual(code, 1)
        self.assertIn("CrashReport", why)

    def test_a_crash_report_info_line_is_only_a_startup_note(self):
        note = "[2026-11-04 10:00:00] [INFO] [CrashReport] handler installed"
        self.assertEqual(judge(FINAL, None, full_log(extra=(note,)))[0], 0)

    def test_a_seat_fault_check_line_fails_the_run(self):
        check = "[2026-11-04 10:01:04] [INFO] [Check] id=taom.seat-of-seeing.fault result=fail mission=1 error=NullReferenceException"
        code, why = judge(FINAL, None, full_log(extra=(check,)))
        self.assertEqual(code, 1)
        self.assertIn("taom.seat-of-seeing.fault", why)

    def test_another_features_failed_check_does_not_fail_the_run(self):
        check = "[2026-11-04 10:01:04] [WARNING] [Check] id=taom.other.thing result=fail mission=1"
        self.assertEqual(judge(FINAL, None, full_log(extra=(check,)))[0], 0)

    def test_a_passing_seat_check_line_is_not_a_failure(self):
        check = "[2026-11-04 10:01:04] [INFO] [Check] id=taom.seat-of-seeing.enter result=pass mission=1 blend=1"
        self.assertEqual(judge(FINAL, None, full_log(extra=(check,)))[0], 0)

    def test_the_fail_scan_cannot_pass_by_matching_nothing(self):
        """A sweep that returns zero proves nothing until it has seen a known instance."""
        self.assertEqual(judge(FINAL, None, full_log(extra=(fail_line("leave_glide"),)))[0], 1)
        self.assertEqual(judge(FINAL, None, full_log())[0], 0)

    def test_a_log_without_the_clean_unload_line_fails(self):
        code, why = judge(FINAL, None, full_log(unload=False))
        self.assertEqual(code, 1)
        self.assertIn("clean module unload", why)
        self.assertEqual(judge(FINAL, None, full_log(unload=True))[0], 0)

    def test_a_deploy_that_is_not_this_checkout_fails_the_run(self):
        for log, named in ((full_log(stamp=None), "BuildStamp"),
                           (full_log(stamp=STAMP_LINE.replace("1735f2add88c0aa640afe3a1cdbb2dfa88b0dddc", "0000000deadbeef")), "0000000de")):
            code, why = judge(FINAL, None, log)
            self.assertEqual(code, 1, named)
            self.assertIn(named, why)
        code, why = judge(FINAL, None, full_log(stamp=STAMP_LINE),
                          changed={"Client/Features/SeatOfSeeing/Trial/CommandTrialScript.cs": datetime(2026, 11, 4, 9, 30)})
        self.assertEqual(code, 1)
        self.assertIn("CommandTrialScript.cs", why)

    def test_the_newest_real_run_passes_and_one_step_turned_to_fail_does_not(self):
        """The judge on lines the game itself wrote, not lines this file made: a format drift fails here before a run does."""
        changed = {"Client/Features/SeatOfSeeing/Trial/CommandTrialScript.cs": datetime(2026, 11, 4, 10, 0, 0)}
        log = "\n".join(REAL_RUN_LINES)
        code, why = judge(FINAL, None, log, changed=changed)
        self.assertEqual(code, 0, why)
        self.assertIn(f"{len(trial.STEPS)} steps with a PASS line", why)

        broken = log.replace("step=fly result=PASS", "step=fly result=FAIL")
        self.assertNotEqual(broken, log)
        code, why = judge(FINAL, None, broken, changed=changed)
        self.assertEqual(code, 1)
        self.assertIn("fly", why)

    def test_the_real_run_lines_carry_the_tables_steps_in_order(self):
        names = [match.group(1) for match in map(trial.STEP_LINE.search, REAL_RUN_LINES) if match]
        self.assertEqual(tuple(names), trial.STEPS)


class StaleBuildTests(unittest.TestCase):
    """stale_build, each clause in both directions, with the stamp line of the first real run as the passing sample."""

    def test_a_log_with_no_stamp_line_is_stale(self):
        self.assertIn("BuildStamp", trial.stale_build("[2026-11-04 10:00:00] [INFO] nothing here", HEAD, {}))
        self.assertIn("BuildStamp", trial.stale_build("[2026-11-04 10:00:00] [INFO] [BuildStamp] sample", HEAD, {}), "a stamp with no git sha")

    def test_the_real_stamp_line_is_the_one_the_pattern_reads(self):
        match = trial.BUILD_STAMP.search(STAMP_LINE)
        self.assertIsNotNone(match)
        self.assertEqual(match.group(1), "20261104-091104")
        self.assertEqual(match.group(2), HEAD)
        self.assertEqual(match.group(3), ".dirty")

    def test_another_sha_is_stale_and_names_both(self):
        stamp = STAMP_LINE.replace(HEAD, "0000000deadbeef")
        reason = trial.stale_build(stamp, HEAD, {})
        self.assertIn("0000000de", reason)
        self.assertIn("1735f2add", reason)

    def test_the_same_sha_is_not_stale_and_a_short_sha_matches_by_prefix(self):
        self.assertIsNone(trial.stale_build(CLEAN_STAMP_LINE, HEAD, {}))
        short = CLEAN_STAMP_LINE.replace(HEAD, "1735f2a")
        self.assertIsNone(trial.stale_build(short, HEAD, {}))

    def test_a_changed_file_newer_than_the_build_is_stale_and_named(self):
        changed = {"Main/a.cs": datetime(2026, 11, 4, 9, 11, 5), "Client/b.cs": datetime(2026, 11, 4, 9, 0, 0)}
        reason = trial.stale_build(STAMP_LINE, HEAD, changed)
        self.assertIn("Main/a.cs", reason)
        self.assertNotIn("Client/b.cs", reason)

    def test_a_changed_file_older_than_the_build_is_not_stale(self):
        self.assertIsNone(trial.stale_build(STAMP_LINE, HEAD, {"Main/a.cs": datetime(2026, 11, 4, 9, 11, 3)}))

    def test_a_file_at_exactly_the_stamps_time_is_not_stale(self):
        self.assertIsNone(trial.stale_build(STAMP_LINE, HEAD, {"Main/a.cs": BUILT}))

    def test_a_clean_match_with_no_changed_file_is_not_stale(self):
        self.assertIsNone(trial.stale_build(CLEAN_STAMP_LINE, HEAD, {}))

    def test_a_dirty_build_of_a_tree_that_has_no_change_is_stale(self):
        """A dirty build, then a restore; or a sibling worktree at the same HEAD with edits of its own."""
        reason = trial.stale_build(STAMP_LINE, HEAD, {})
        self.assertIn("uncommitted edits", reason)
        self.assertIn("this tree has none", reason)

    def test_a_clean_build_of_a_tree_with_a_change_older_than_the_build_is_stale(self):
        """A file restored with its old time still differs from HEAD, and a clean build cannot have compiled it."""
        reason = trial.stale_build(CLEAN_STAMP_LINE, HEAD, OLDER_CHANGE)
        self.assertIn("clean build", reason)
        self.assertIn("Main/a.cs", reason)

    def test_a_clean_build_of_a_tree_whose_only_change_is_a_deletion_is_stale(self):
        self.assertIn("clean build", trial.stale_build(CLEAN_STAMP_LINE, HEAD, {"Main/gone.cs": datetime.min}))

    def test_a_dirty_build_with_an_older_change_is_the_real_run_and_is_not_stale(self):
        self.assertIsNone(trial.stale_build(STAMP_LINE, HEAD, OLDER_CHANGE))


class GitStateTests(unittest.TestCase):

    def test_the_state_of_this_checkout_is_a_head_and_a_dict(self):
        head, changed = trial.git_state()
        self.assertRegex(head, r"^[0-9a-f]{40}$")
        self.assertIsInstance(changed, dict)

    def test_the_command_asks_for_no_renames_and_takes_no_optional_lock_over_the_builds_own_paths(self):
        calls = []

        def fake_run(command, **kwargs):
            calls.append(command)
            return SimpleNamespace(returncode=0, stdout=HEAD + "\n" if "rev-parse" in command else "", stderr="")

        with mock.patch.object(trial.subprocess, "run", side_effect=fake_run):
            trial.git_state(REPO)

        status = next(command for command in calls if "status" in command)
        self.assertIn("--no-optional-locks", status)
        self.assertIn("--no-renames", status)
        self.assertEqual(tuple(status[status.index("--") + 1:]), trial.WATCHED_PATHS)

    def test_a_real_repository_reports_an_edit_a_staged_rename_an_untracked_file_and_a_deletion(self):
        """Real git in a temp repo. The hooks path, the identity and the config files are given or cleared on the command line, so
        nothing of the machine reaches the repo. A deleted path, and the old path of a rename, are changes with no time."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Path(tmp)
            options = ["-c", "core.hooksPath=", "-c", "user.name=Trial", "-c", "user.email=trial@example.invalid", "-c", "commit.gpgsign=false",
                       "-c", "core.autocrlf=false"]
            environment = {**os.environ, "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1"}

            def git(*args):
                result = subprocess.run(["git", "-C", tmp, *options, *args], capture_output=True, text=True, encoding="utf-8", timeout=60, env=environment)
                self.assertEqual(result.returncode, 0, result.stderr)
                return result.stdout

            for name in ("Main/edit.cs", "Main/old.cs", "Client/gone.cs", "Dependencies/quiet.cs"):
                (repo / name).parent.mkdir(parents=True, exist_ok=True)
                (repo / name).write_text(f"// {name}, long enough for git to see a rename\n" * 12, encoding="utf-8")
            git("init", "-q")
            git("add", "-A")
            git("commit", "-q", "-m", "base")

            (repo / "Main/edit.cs").write_text("// edited\n", encoding="utf-8")
            git("mv", "Main/old.cs", "Main/new.cs")
            (repo / "Client/fresh.cs").write_text("// untracked\n", encoding="utf-8")
            (repo / "Client/gone.cs").unlink()
            for name in ("Main/edit.cs", "Main/new.cs", "Client/fresh.cs"):
                os.utime(repo / name, (1762247465, 1762247465))
            self.assertIn("R  Main/old.cs -> Main/new.cs", git("status", "--porcelain", "-uall"), "the fixture must hold a staged rename")

            head, changed = trial.git_state(repo)

        self.assertRegex(head, r"^[0-9a-f]{40}$")
        self.assertEqual(sorted(changed), ["Client/fresh.cs", "Client/gone.cs", "Main/edit.cs", "Main/new.cs", "Main/old.cs"])
        for name in ("Main/edit.cs", "Main/new.cs", "Client/fresh.cs"):
            self.assertEqual(changed[name], datetime(2025, 11, 4, 9, 11, 5), name)
        for name in ("Client/gone.cs", "Main/old.cs"):
            self.assertEqual(changed[name], datetime.min, name)

    def test_the_watched_paths_are_the_ones_the_build_stamps_its_dirty_mark_from(self):
        props = (REPO / "Directory.Build.props").read_text(encoding="utf-8")
        match = re.search(r"git --no-optional-locks status [^\"]*? -- ([^\"]*?) 2&gt;nul", props)
        self.assertIsNotNone(match, "the stamp target's git status command was not found in Directory.Build.props")
        self.assertEqual(tuple(match.group(1).split()), trial.WATCHED_PATHS)

    def test_a_git_that_fails_raises_instead_of_answering_empty(self):
        with mock.patch.object(trial.subprocess, "run", return_value=SimpleNamespace(returncode=128, stdout="", stderr="fatal: not a git repository")):
            with self.assertRaises(OSError):
                trial.git_state(REPO)

    def test_main_refuses_when_git_fails_and_never_reaches_the_runner(self):
        reached = []
        with restoring_runner(main=lambda rest: reached.append(rest) or 0), \
                mock.patch.object(trial, "git_state", side_effect=OSError("git exited 128")), \
                contextlib.redirect_stdout(io.StringIO()) as out:
            self.assertEqual(trial.main(["--dry-run"]), 1)
        self.assertEqual(reached, [])
        self.assertIn("git", out.getvalue())


class SourceTieTests(unittest.TestCase):
    """The judge and the arm text name strings the C# side owns. A rewording there would otherwise pass every test here."""

    def test_the_seats_steps_are_the_seats_row_lists_names_in_order(self):
        source = STEPS_SOURCE.read_text(encoding="utf-8")
        self.assertEqual(tuple(re.findall(r'new SeatTrialStep\(\s*"(\w+)"', source)), trial.SEAT_STEPS)

    def test_every_step_is_a_features_steps_so_far_the_seats(self):
        self.assertEqual(trial.STEPS, trial.SEAT_STEPS)

    def test_the_line_shape_is_the_one_the_game_writes(self):
        source = (DOMAIN_SOURCE / "SeatTrialRules.cs").read_text(encoding="utf-8")
        self.assertIn('"[SeatTrial] step="', source)
        self.assertIn('" result="', source)
        self.assertIn('"PASS"', source)
        self.assertIn('"FAIL"', source)

    def test_the_fault_scan_matches_every_check_id_the_seat_writes_and_the_line_the_log_writes(self):
        ids = re.findall(r'"(taom\.[a-z0-9.-]+)"', (DOMAIN_SOURCE / "SeatChecks.cs").read_text(encoding="utf-8"))
        self.assertIn("taom.seat-of-seeing.fault", ids)
        for check in ids:
            self.assertTrue(trial.SEAT_CHECK_FAIL.search(f"[Check] id={check} result=fail mission=1"), check)
        outcome_log = (REPO / "Main" / "Core" / "Logging" / "OutcomeLog.cs").read_text(encoding="utf-8")
        self.assertIn('"[Check] id=" + id + " result=" + result', outcome_log)

    def test_each_arm_key_is_one_the_options_read(self):
        source = (TRIAL_SOURCE / "CommandTrialOptions.cs").read_text(encoding="utf-8")
        known = SimpleNamespace(scene="battle_terrain_a", live_limit=420, steps="enter")
        keys = [key for key, _ in trial.arm_pairs(known, "E:/out")]
        self.assertGreaterEqual(len(keys), 5)
        for key in keys:
            self.assertIn(f'case "{key}":', source, key)

    def test_the_file_names_and_the_variable_match_the_game(self):
        autostart = (TRIAL_SOURCE / "CommandTrialAutostart.cs").read_text(encoding="utf-8")
        options = (TRIAL_SOURCE / "CommandTrialOptions.cs").read_text(encoding="utf-8")
        self.assertIn(f'DoneFile = "{trial.DONE_FILE}"', autostart)
        self.assertIn(f'StatusFile = "{trial.STATUS_FILE}"', autostart)
        self.assertIn(f'EnvVar = "{trial.ENV_VAR}"', options)

    def test_the_default_scene_and_limit_are_the_games_defaults(self):
        options = (TRIAL_SOURCE / "CommandTrialOptions.cs").read_text(encoding="utf-8")
        self.assertIn(f'DefaultScene = "{trial.DEFAULT_SCENE}"', options)
        self.assertIn(f"DefaultLiveLimitSeconds = {trial.DEFAULT_LIVE_LIMIT}f", options)

    def test_the_done_reasons_the_wrapper_cites_are_ones_the_game_writes(self):
        source = "".join(path.read_text(encoding="utf-8") for path in TRIAL_SOURCE.glob("*.cs"))
        for reason in ("final", "live_limit", "never_live", "wall_limit", "mission_closed", "start_refused", "start_failed", "script_failed"):
            self.assertIn(f'"{reason}"', source, reason)

    def test_the_done_file_the_game_writes_is_read_by_the_runners_own_parser(self):
        """The same literal as CommandTrialAutostartTests.DoneText_HasTheContractsKeys_InOrder_OnePerLine_WithLfOnly."""
        text = ("reason=final\nrun=1\nreport=none\nlog=E:\\Games\\Logs\\taom_debug_1.log\nlive_seconds=31.4\nwall_seconds=187.3\n"
                "started=2026-11-04T10:00:00.0000000+01:00\nended=2026-11-04T10:03:07.0000000+01:00\n")
        done = runner.parse_done(text)
        self.assertEqual(done["reason"], "final")
        self.assertEqual(done["log"], "E:\\Games\\Logs\\taom_debug_1.log")
        self.assertEqual(done["report"], "none")
        self.assertEqual(done["started"], "2026-11-04T10:00:00.0000000+01:00")
        self.assertEqual(judge(done, None, full_log())[0], 0)

    def test_the_runs_root_is_the_contracts(self):
        self.assertEqual(trial.RUNS_ROOT, r"E:\Temp\command-trial-runs")


class PatchTests(unittest.TestCase):

    def test_importing_the_wrapper_leaves_the_fords_runner_alone(self):
        self.assertEqual(runner.ENV_VAR, "TAOM_FORDS_AUTORUN")
        self.assertEqual(runner.DONE_FILE, "fords_autorun_done.txt")
        self.assertEqual(runner.RUNS_ROOT, r"E:\Temp\fords-runs")

    def test_main_points_the_runner_at_the_trial(self):
        seen = {}

        def fake_main(rest):
            seen.update(env=runner.ENV_VAR, done=runner.DONE_FILE, status=runner.STATUS_FILE, root=runner.RUNS_ROOT,
                        judge=runner.judge, arm=runner.arm_text_for, rest=rest)
            return 7

        with restoring_runner(main=fake_main), mock.patch.object(trial, "git_state", return_value=(HEAD, {})):
            self.assertEqual(trial.main(["--dry-run"]), 7)
        self.assertEqual(seen["env"], trial.ENV_VAR)
        self.assertEqual(seen["done"], trial.DONE_FILE)
        self.assertEqual(seen["status"], trial.STATUS_FILE)
        self.assertEqual(seen["root"], trial.RUNS_ROOT)
        self.assertEqual(seen["rest"], ["--dry-run"])
        text, limit = seen["arm"]("E:/out", SimpleNamespace(wall_limit=None))
        self.assertIn("out=E:/out\n", text)
        self.assertIn("quit=1\n", text)
        self.assertIn("scene=battle_terrain_a\n", text)
        self.assertNotIn("steps=", text, "no steps key means every step")
        self.assertEqual(limit, runner.DEFAULT_WALL_LIMIT_SECONDS)

    def test_the_judge_the_runner_gets_knows_the_armed_steps_and_the_checkout(self):
        seen = {}

        def fake_main(rest):
            seen["judge"] = runner.judge
            return 0

        with restoring_runner(main=fake_main), mock.patch.object(trial, "git_state", return_value=(HEAD, {"Main/a.cs": datetime(2026, 11, 4, 9, 30)})):
            trial.main(["--steps", "enter,fly"])
        rows = [STAMP_LINE, pass_line("enter"), pass_line("fly"), UNLOAD_LINE]
        self.assertEqual(seen["judge"](FINAL, None, "\n".join(rows))[0], 1, "Main/a.cs is newer than the build")
        clean_rows = [CLEAN_STAMP_LINE, pass_line("enter"), pass_line("fly"), UNLOAD_LINE]
        with restoring_runner(main=fake_main), mock.patch.object(trial, "git_state", return_value=(HEAD, {})):
            trial.main(["--steps", "enter,fly"])
        self.assertEqual(seen["judge"](FINAL, None, "\n".join(clean_rows))[0], 0)
        self.assertEqual(seen["judge"](FINAL, None, "\n".join([CLEAN_STAMP_LINE, pass_line("enter"), UNLOAD_LINE]))[0], 1)

    def test_a_wall_limit_flag_reaches_the_arm_text_result(self):
        seen = {}

        def fake_main(rest):
            seen["arm"] = runner.arm_text_for
            return 0

        with restoring_runner(main=fake_main), mock.patch.object(trial, "git_state", return_value=(HEAD, {})):
            trial.main(["--live-limit", "90", "--steps", "enter,fly", "--scene", "battle_terrain_b"])
        text, limit = seen["arm"]("E:/out", SimpleNamespace(wall_limit=1234.0))
        self.assertIn("live_limit_seconds=90\n", text)
        self.assertIn("steps=enter,fly\n", text)
        self.assertIn("scene=battle_terrain_b\n", text)
        self.assertEqual(limit, 1234.0)


class RunOnceTests(unittest.TestCase):
    """The whole wrapper through the real runner: only the machine (processes, the registry-backed folders, the game) is faked."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / "bin" / "Logs").mkdir(parents=True)
        (self.root / "logs").mkdir()

    def fake_game(self, log_text, writes_done=True, exit_code=0):
        root = self.root
        started = []

        class FakeGame:
            def __init__(self, command, cwd=None, env=None, **kwargs):
                self.pid = 4242
                self.returncode = exit_code
                self.env = env
                started.append(self)
                arm = Path(env[trial.ENV_VAR])
                out = Path(dict(line.split("=", 1) for line in arm.read_text(encoding="utf-8").splitlines())["out"])
                log = root / "bin" / "Logs" / "taom_debug_2026-11-04_10-00-00.log"
                log.write_text(log_text, encoding="utf-8")
                if writes_done:
                    (out / trial.DONE_FILE).write_text(
                        f"reason=final\nrun=1\nreport=none\nlog={log}\nlive_seconds=14.8\nwall_seconds=50.1\n"
                        "started=2026-11-04T10:00:00.0000000+01:00\nended=2026-11-04T10:00:50.0000000+01:00\n", encoding="utf-8")

            def poll(self):
                return self.returncode

            def wait(self, timeout=None):
                return self.returncode

            def kill(self):
                pass

        return FakeGame, started

    def run_wrapper(self, game, argv=()):
        patches = dict(
            preflight=lambda game_folder, idle=0: [],
            my_documents=lambda: self.root / "docs",
            inspect_environment=lambda docs, game_folder, modules, say: (self.root / "engine_config.txt", self.root / "bin"),
            list_processes=lambda names: [],
            handle_dialogs=lambda pid, seen, say: None,
            game_dir=lambda root: self.root / "game",
            launch_modules=lambda *args: ["Native", "TAOM"],
            LOGS_DIR=str(self.root / "logs"),
        )
        runs = self.root / "runs"
        with restoring_runner(**patches), mock.patch.object(runner.subprocess, "Popen", game), mock.patch.object(runner.time, "sleep"), \
                mock.patch.object(trial, "git_state", return_value=(HEAD, {})), contextlib.redirect_stdout(io.StringIO()):
            code = trial.main(["--require-idle-minutes", "0", "--runs-root", str(runs), *argv])
        folders = sorted(runs.iterdir())
        self.assertEqual(len(folders), 1)
        return code, folders[0]

    def test_a_good_run_exits_0_arms_the_game_and_names_the_build(self):
        game, started = self.fake_game(full_log())

        code, folder = self.run_wrapper(game)

        status = (folder / "runner_status.txt").read_text(encoding="utf-8")
        self.assertEqual(code, 0, status)
        self.assertIn("exit_code=0", status)
        self.assertIn("1735f2add built", status)
        arm = (folder / "arm.txt").read_text(encoding="utf-8")
        self.assertIn("scene=battle_terrain_a\n", arm)
        self.assertEqual(len(started), 1)
        self.assertEqual(Path(started[0].env[trial.ENV_VAR]), folder / "arm.txt")
        self.assertTrue((folder / trial.DONE_FILE).is_file())

    def test_a_run_with_no_done_file_exits_1(self):
        game, started = self.fake_game(full_log(), writes_done=False, exit_code=1)

        code, folder = self.run_wrapper(game)

        self.assertEqual(code, 1)
        self.assertEqual(len(started), 1)
        self.assertIn("exit_code=1", (folder / "runner_status.txt").read_text(encoding="utf-8"))

    def test_a_run_whose_log_comes_from_another_build_exits_1_and_says_so(self):
        game, started = self.fake_game(full_log(stamp=STAMP_LINE.replace(HEAD, "0000000deadbeef")))

        code, folder = self.run_wrapper(game)

        self.assertEqual(code, 1)
        self.assertIn("not this checkout", (folder / "runner_status.txt").read_text(encoding="utf-8"))


class ArgumentTests(unittest.TestCase):

    def parse(self, *argv):
        return trial.build_parser().parse_known_args(list(argv))[0]

    def test_the_defaults_parse(self):
        known = self.parse()
        self.assertEqual(known.scene, "battle_terrain_a")
        self.assertEqual(known.live_limit, 420)
        self.assertIsNone(known.steps)

    def test_an_unknown_step_is_refused(self):
        with self.assertRaises(SystemExit):
            trial.main(["--steps", "enter,flye", "--dry-run"])

    def test_a_blank_step_list_is_refused(self):
        with self.assertRaises(SystemExit):
            trial.main(["--steps", " , ", "--dry-run"])

    def test_a_negative_time_is_refused(self):
        with self.assertRaises(SystemExit):
            self.parse("--live-limit", "-1")

    def test_the_fords_runners_flags_pass_through(self):
        known, rest = trial.build_parser().parse_known_args(["--require-idle-minutes", "0", "--dry-run", "--keep-on-crash", "30"])
        self.assertEqual(rest, ["--require-idle-minutes", "0", "--dry-run", "--keep-on-crash", "30"])

    def test_the_help_says_which_flags_apply_and_which_are_ignored(self):
        epilog = trial.build_parser().epilog
        for flag in ("--require-idle-minutes", "--runs", "--keep-on-crash", "--runs-root", "--dry-run"):
            self.assertIn(flag, epilog)
        self.assertIn("--wall-limit moves only the runner's own deadline", epilog)
        self.assertIn("--west", epilog)
        self.assertIn("100000", epilog)

    def test_the_steps_are_normalised_to_the_tables_order_and_names(self):
        self.assertEqual(trial.normalised_steps(" Fly , enter ,fly"), ("enter", "fly"))
        self.assertEqual(trial.normalised_steps(None), trial.STEPS)


if __name__ == "__main__":
    unittest.main()
