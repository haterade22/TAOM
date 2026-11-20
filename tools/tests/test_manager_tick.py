#!/usr/bin/env python3
"""tools/manager_tick.py: the gate that decides whether an idle manager tick may start, with the machine patched out.

Run:  python -m unittest tools.tests.test_manager_tick

The launcher itself starts a real headless session and is not run here.
"""
import os
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import manager_tick as mt  # noqa: E402

NOW = 1_800_000_000


def gate(**over):
    """A gate input where every check passes, with the named fields replaced."""
    base = dict(stop=False, pause_until=0, now=NOW, idle_seconds=3600.0, idle_minutes=10.0,
                game_running=False, tick_held=False, worktree_ok=True, worktree_clean=True)
    base.update(over)
    return mt.decide(**base)


class DecideTests(unittest.TestCase):
    def test_every_check_passing_starts_the_tick(self):
        start, reason = gate()
        self.assertTrue(start, reason)

    def test_a_stop_file_refuses(self):
        start, reason = gate(stop=True)
        self.assertFalse(start)
        self.assertIn("STOP", reason)

    def test_a_pause_in_the_future_refuses(self):
        start, reason = gate(pause_until=NOW + 60)
        self.assertFalse(start)
        self.assertIn("usage", reason)

    def test_a_pause_in_the_past_does_not_refuse(self):
        start, _ = gate(pause_until=NOW - 1)
        self.assertTrue(start)

    def test_recent_input_refuses(self):
        start, reason = gate(idle_seconds=599.0)
        self.assertFalse(start)
        self.assertIn("idle", reason)

    def test_exactly_the_idle_time_starts(self):
        start, _ = gate(idle_seconds=600.0)
        self.assertTrue(start)

    def test_an_unreadable_input_age_refuses(self):
        start, reason = gate(idle_seconds=None)
        self.assertFalse(start)
        self.assertIn("idle", reason)

    def test_a_running_game_refuses(self):
        start, reason = gate(game_running=True)
        self.assertFalse(start)
        self.assertIn("Bannerlord", reason)

    def test_an_unknown_game_state_refuses(self):
        start, reason = gate(game_running=None)
        self.assertFalse(start)
        self.assertIn("Bannerlord", reason)

    def test_a_tick_another_session_holds_refuses(self):
        start, reason = gate(tick_held=True)
        self.assertFalse(start)
        self.assertIn("manager-tick", reason)

    def test_an_unknown_claim_state_refuses(self):
        start, reason = gate(tick_held=None)
        self.assertFalse(start)
        self.assertIn("manager-tick", reason)

    def test_a_missing_worktree_refuses(self):
        start, reason = gate(worktree_ok=False)
        self.assertFalse(start)
        self.assertIn("worktree", reason)

    def test_a_dirty_worktree_refuses(self):
        start, reason = gate(worktree_clean=False)
        self.assertFalse(start)
        self.assertIn("uncommitted", reason)

    def test_an_unknown_worktree_state_refuses(self):
        start, reason = gate(worktree_clean=None)
        self.assertFalse(start)
        self.assertIn("uncommitted", reason)


class RefreshTests(unittest.TestCase):
    def test_refresh_fetches_the_trunk_then_detaches_at_it(self):
        calls = []

        def fake(args, **_):
            calls.append(args)
            return mock.Mock(returncode=0, stdout="", stderr="")
        with mock.patch.object(mt.subprocess, "run", side_effect=fake):
            ok, _ = mt.refresh("E:/x")
        self.assertTrue(ok)
        self.assertEqual(calls[0][-3:], ["fetch", "origin", "bannerlord-1.5.x"])
        self.assertEqual(calls[1][-3:], ["checkout", "--detach", "FETCH_HEAD"])

    def test_a_failed_fetch_never_checks_out(self):
        calls = []

        def fake(args, **_):
            calls.append(args)
            return mock.Mock(returncode=1, stdout="", stderr="no network")
        with mock.patch.object(mt.subprocess, "run", side_effect=fake):
            ok, why = mt.refresh("E:/x")
        self.assertFalse(ok)
        self.assertEqual(len(calls), 1)
        self.assertIn("no network", why)


class UsageLimitTests(unittest.TestCase):
    def test_a_usage_limit_result_is_seen(self):
        self.assertTrue(mt.usage_limited('{"type":"result","is_error":true,"result":"Claude AI usage limit reached|1800003600"}'))

    def test_a_normal_result_is_not_a_usage_limit(self):
        self.assertFalse(mt.usage_limited('{"type":"result","is_error":false,"result":"tick done"}'))

    def test_an_error_about_something_else_is_not_a_usage_limit(self):
        self.assertFalse(mt.usage_limited('{"type":"result","is_error":true,"result":"hook denied the push"}'))


class CommandTests(unittest.TestCase):
    def test_the_command_runs_the_tick_headless_and_never_in_plan_mode(self):
        cmd = mt.tick_command("claude", "tick")
        self.assertEqual(cmd[:2], ["claude", "-p"])
        self.assertIn("/manage tick", cmd)
        mode = cmd[cmd.index("--permission-mode") + 1]
        self.assertEqual(mode, "default")

    def test_a_dry_tick_passes_its_flag(self):
        self.assertIn("/manage tick --dry", mt.tick_command("claude", "tick --dry"))


class StateTests(unittest.TestCase):
    def test_pause_until_reads_a_missing_file_as_no_pause(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(mt.read_pause(Path(d)), 0)

    def test_pause_until_reads_a_written_pause(self):
        with tempfile.TemporaryDirectory() as d:
            mt.write_pause(Path(d), NOW + 3600)
            self.assertEqual(mt.read_pause(Path(d)), NOW + 3600)

    def test_a_corrupt_pause_file_reads_as_a_pause_of_one_hour(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "PAUSE").write_text("not a number", encoding="utf-8")
            self.assertEqual(mt.read_pause(Path(d), now=NOW), NOW + 3600)


class MainTests(unittest.TestCase):
    def _run(self, argv, start, run_result=None):
        with tempfile.TemporaryDirectory() as d, \
                mock.patch.object(mt, "state_dir", return_value=Path(d)), \
                mock.patch.object(mt, "gather", return_value=dict(stop=False, pause_until=0, now=NOW, idle_seconds=3600.0,
                                                                  idle_minutes=10.0, game_running=False,
                                                                  tick_held=not start, worktree_ok=True, worktree_clean=True)), \
                mock.patch.object(mt, "refresh", return_value=(True, "")), \
                mock.patch.object(mt, "run_tick", return_value=run_result or (0, '{"type":"result","is_error":false}')) as run:
            code = mt.main(argv)
            log = (Path(d) / "launcher.log").read_text(encoding="utf-8") if (Path(d) / "launcher.log").exists() else ""
            pause = mt.read_pause(Path(d))
        return code, run, log, pause

    def test_without_run_it_only_reports(self):
        code, run, log, _ = self._run(["--worktree", "E:/x"], start=True)
        self.assertEqual(code, 0)
        run.assert_not_called()
        self.assertEqual(log, "")

    def test_with_run_and_a_passing_gate_it_starts_one_tick_and_logs(self):
        code, run, log, _ = self._run(["--worktree", "E:/x", "--run"], start=True)
        self.assertEqual(code, 0)
        run.assert_called_once()
        self.assertIn("started", log)

    def test_with_run_and_a_refusing_gate_it_starts_nothing(self):
        code, run, log, _ = self._run(["--worktree", "E:/x", "--run"], start=False)
        self.assertEqual(code, mt.REFUSED)
        run.assert_not_called()
        self.assertIn("refused", log)

    def test_a_usage_limit_writes_a_pause(self):
        code, _, log, pause = self._run(["--worktree", "E:/x", "--run"], start=True,
                                        run_result=(1, '{"type":"result","is_error":true,"result":"usage limit reached"}'))
        self.assertGreater(pause, int(time.time()) + mt.PAUSE_SECONDS - 60)
        self.assertIn("usage limit", log)


if __name__ == "__main__":
    unittest.main()
