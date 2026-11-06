#!/usr/bin/env python3
"""tools/run_fords_autorun.py: the pure helpers and the machine access around them, with the machine patched out.

Run:  python -m unittest tools.tests.test_run_fords_autorun

The launcher itself starts the real game and is not run here.
"""
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import time
import unittest
import zlib
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import run_fords_autorun as r  # noqa: E402


class ArmTextTests(unittest.TestCase):
    def test_only_out_gives_out_and_quit(self):
        self.assertEqual(r.arm_text(r"E:\x"), "out=E:\\x\nquit=1\n")

    def test_control_check_adds_its_key_only_when_set(self):
        self.assertIn("control_check=240\n", r.arm_text(r"E:\x", control_check=240))
        self.assertNotIn("control_check", r.arm_text(r"E:\x"))

    def test_speed_adds_its_key_only_when_set(self):
        self.assertIn("speed=10\n", r.arm_text(r"E:\x", speed=10))
        self.assertNotIn("speed", r.arm_text(r"E:\x"))

    def test_west_ai_adds_its_key_only_when_set(self):
        self.assertIn("west_ai=1\n", r.arm_text(r"E:\x", west_ai=True))
        self.assertNotIn("west_ai", r.arm_text(r"E:\x"))

    def test_every_choice_is_written_in_key_value_lines(self):
        text = r.arm_text(r"E:\x", west="gondor", east="mordor", ai="hard", scene="s", live_seconds=90,
                          live_wait_seconds=300, wall_limit_seconds=900)
        self.assertEqual(text.splitlines(), ["west=gondor", "east=mordor", "ai=hard", "scene=s", "live_seconds=90",
                                             "live_wait_seconds=300", "wall_limit_seconds=900", "out=E:\\x", "quit=1"])

    def test_lines_use_lf_only(self):
        self.assertNotIn("\r", r.arm_text(r"E:\x", west="gondor"))

    def test_quit_can_be_switched_off(self):
        self.assertIn("quit=0\n", r.arm_text(r"E:\x", quit_when_done=False))

    def test_blank_choice_is_left_out(self):
        self.assertNotIn("west=", r.arm_text(r"E:\x", west=""))


class SafelyExitedTests(unittest.TestCase):
    def test_zero_becomes_one_and_crlf_is_kept(self):
        data = b"a = 1\r\nsafely_exited = 0\r\nb = 2\r\n"
        new, changed = r.rewrite_safely_exited(data)
        self.assertTrue(changed)
        self.assertEqual(new, b"a = 1\r\nsafely_exited = 1\r\nb = 2\r\n")

    def test_already_one_is_unchanged(self):
        data = b"safely_exited = 1\r\n"
        new, changed = r.rewrite_safely_exited(data)
        self.assertFalse(changed)
        self.assertEqual(new, data)

    def test_absent_line_is_unchanged(self):
        data = b"other = 0\n"
        self.assertEqual(r.rewrite_safely_exited(data), (data, False))


class CrashScanTests(unittest.TestCase):
    def test_rgl_marker(self):
        self.assertTrue(r.rgl_crashed("x\nUnhandled Exception Code: 0xC0000005\ny"))
        self.assertFalse(r.rgl_crashed("all quiet"))

    def test_watchdog_marker(self):
        self.assertTrue(r.watchdog_crashed("[x] Crash occurred at 12:00"))
        self.assertFalse(r.watchdog_crashed("Watching process 100"))


class WatchdogTokenTests(unittest.TestCase):
    def test_matches_the_pid_token(self):
        self.assertTrue(r.watchdog_targets_pid(r"Watchdog.exe -p 1234 -x", 1234))

    def test_matches_at_the_end_and_when_quoted(self):
        self.assertTrue(r.watchdog_targets_pid(r"Watchdog.exe -p 1234", 1234))
        self.assertTrue(r.watchdog_targets_pid('Watchdog.exe -p "1234" -x', 1234))

    def test_a_longer_number_is_another_process(self):
        self.assertFalse(r.watchdog_targets_pid(r"Watchdog.exe -p 12345", 1234))
        self.assertFalse(r.watchdog_targets_pid(r"Watchdog.exe -p 51234", 1234))

    def test_another_flag_is_not_a_pid_token(self):
        self.assertFalse(r.watchdog_targets_pid(r"Watchdog.exe -pp 1234", 1234))

    def test_empty_command_line(self):
        self.assertFalse(r.watchdog_targets_pid(None, 1234))
        self.assertFalse(r.watchdog_targets_pid("", 1234))


class NewestLogTests(unittest.TestCase):
    def test_picks_the_newest_created_after_the_start(self):
        logs = [("a.log", 90.0), ("b.log", 120.0), ("c.log", 150.0)]
        self.assertEqual(r.pick_newest_log(logs, started_epoch=100.0), "c.log")

    def test_ignores_logs_older_than_the_start_minus_slack(self):
        logs = [("old.log", 90.0), ("edge.log", 98.5)]
        self.assertEqual(r.pick_newest_log(logs, started_epoch=100.0), "edge.log")
        self.assertIsNone(r.pick_newest_log([("old.log", 90.0)], started_epoch=100.0))

    def test_no_logs(self):
        self.assertIsNone(r.pick_newest_log([], started_epoch=100.0))


class DoneFileTests(unittest.TestCase):
    def test_parses_values_with_spaces_and_equals(self):
        text = "reason=final\r\nrun=1\r\nfinal_total=pass=3 fail=2 partial=6 notseen=5\r\nreport=E:\\a=b\\r.json\r\n"
        done = r.parse_done(text)
        self.assertEqual(done["reason"], "final")
        self.assertEqual(done["final_total"], "pass=3 fail=2 partial=6 notseen=5")
        self.assertEqual(done["report"], "E:\\a=b\\r.json")

    def test_skips_lines_without_a_key(self):
        self.assertEqual(r.parse_done("junk\n=x\nreason=final\n"), {"reason": "final"})


class VerdictLinesTests(unittest.TestCase):
    def test_only_the_final_pass_lines(self):
        log = "\n".join([
            "[FordsOfWar][Verdict] run=1 w=1 mt=1 final=0 check=01 result=pass",
            "[FordsOfWar][Verdict] run=1 w=2 mt=2 final=1 check=01 result=pass",
            "[FordsOfWar][Verdict] run=1 w=2 mt=2 final=1 total pass=3 fail=2 partial=6 notseen=5 report=\"x\"",
            "[FordsOfWar][Stage] run=1 final=1",
        ])
        lines = r.final_verdict_lines(log)
        self.assertEqual(len(lines), 2)
        self.assertTrue(all("final=1" in line for line in lines))


class ShaderListTests(unittest.TestCase):
    @staticmethod
    def blob(build, modules):
        b, m = build.encode("ascii"), modules.encode("ascii")
        return struct.pack("<II", 1, len(b)) + b + struct.pack("<I", len(m)) + m + b"records"

    def test_reads_build_and_module_list(self):
        self.assertEqual(r.read_shader_list(self.blob("125053", "Native;TAOM")), ("125053", "Native;TAOM"))

    def test_unparsable_data_is_none(self):
        self.assertIsNone(r.read_shader_list(b"\x01"))

    def test_expected_list_is_ordinally_sorted_and_matches_the_f5_list(self):
        self.assertEqual(r.expected_shader_list(r.launch_modules()),
                         "BirthAndDeath;CustomBattle;DualWield;DualWield_TAOM;LOTRLOME_Armory;Native;SandBoxCore;Sandbox;"
                         "StoryMode;TAOM;TAOM.Dependencies;TAOM_Map")


class LaunchArgumentTests(unittest.TestCase):
    F5_LIST = ["TAOM.Dependencies", "Native", "SandBoxCore", "CustomBattle", "Sandbox", "StoryMode", "BirthAndDeath",
               "LOTRLOME_Armory", "DualWield", "TAOM_Map", "TAOM", "DualWield_TAOM"]

    def test_module_list_is_read_from_the_launch_settings_and_equals_the_f5_list(self):
        self.assertEqual(r.launch_modules(), self.F5_LIST)

    def test_module_argument_is_the_f5_list(self):
        self.assertEqual(r.modules_arg(r.launch_modules()),
                         "_MODULES_*TAOM.Dependencies*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*BirthAndDeath*"
                         "LOTRLOME_Armory*DualWield*TAOM_Map*TAOM*DualWield_TAOM*_MODULES_")

    def test_the_module_id_placeholder_becomes_taom(self):
        settings = {"profiles": {"Bannerlord": {"commandLineArgs": "/singleplayer _MODULES_*Native*$(ModuleId)*X*_MODULES_"}}}
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "launchSettings.json"
            path.write_text(json.dumps(settings), encoding="utf-8")
            self.assertEqual(r.launch_modules(path), ["Native", "TAOM", "X"])

    def test_a_settings_file_without_the_profile_raises(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "launchSettings.json"
            path.write_text(json.dumps({"profiles": {}}), encoding="utf-8")
            with self.assertRaises(ValueError):
                r.launch_modules(path)


class JudgeTests(unittest.TestCase):
    DONE = {"reason": "final", "started": "2026-10-31T22:08:36.9869565-05:00", "ended": "2026-10-31T22:19:19.7622814-05:00"}

    def test_final_reason_with_a_final_report_passes(self):
        self.assertEqual(r.judge({"reason": "final"}, {"final": True})[0], 0)

    def test_other_reasons_fail(self):
        for reason in ("start_refused", "never_live", "wall_limit", "final_timeout", "start_failed"):
            self.assertEqual(r.judge({"reason": reason}, {"final": True})[0], 1, reason)

    def test_missing_done_or_report_fails(self):
        self.assertEqual(r.judge({}, None)[0], 1)
        self.assertEqual(r.judge({"reason": "final"}, None)[0], 1)
        self.assertEqual(r.judge({"reason": "final"}, {"final": False})[0], 1)

    def test_a_fail_row_in_the_report_fails_the_run(self):
        report = {"final": True, "verdicts": [{"Check": 3, "Result": "Pass"}, {"Check": 9, "Result": "Fail", "Title": "Melee"}]}
        code, why = r.judge({"reason": "final"}, report)
        self.assertEqual(code, 1)
        self.assertIn("check 9", why)

    def test_pass_partial_and_notseen_rows_do_not_fail_the_run(self):
        report = {"final": True, "verdicts": [{"Check": 1, "Result": "Partial"}, {"Check": 2, "Result": "NotSeen"},
                                              {"Check": 3, "Result": "Pass"}]}
        self.assertEqual(r.judge({"reason": "final"}, report)[0], 0)

    def test_a_crash_report_block_inside_the_run_fails_it(self):
        log = ("[2026-10-31 22:08:40] [INFO] [CrashReport] main thread id 3 recorded at Subscribe()\n"
               "[2026-10-31 22:17:23] [ERROR] [CrashReport] TAOM CrashReport captured 2026-11-01 03:17:23.756 UTC\n")
        code, why = r.judge(self.DONE, {"final": True}, log)
        self.assertEqual(code, 1)
        self.assertIn("22:17:23", why)

    def test_startup_crash_report_lines_below_error_level_do_not_fail_the_run(self):
        log = ("[2026-10-31 22:08:40] [INFO] [CrashReport] main thread id 3 recorded at Subscribe()\n"
               "[2026-10-31 22:08:40] [WARNING] [CrashReport] Native2Managed: target not found, skipped: X\n")
        self.assertEqual(r.judge(self.DONE, {"final": True}, log)[0], 0)

    def test_a_crash_report_block_after_the_done_time_still_fails_the_run(self):
        # The driver writes the done file before it quits, so a quit-time capture comes after `ended` (Codex review 2026-11-01, F1).
        log = "[2026-10-31 22:19:25] [ERROR] [CrashReport] TAOM CrashReport captured at shutdown\n"
        code, why = r.judge(self.DONE, {"final": True}, log)
        self.assertEqual(code, 1)
        self.assertIn("22:19:25", why)

    def test_a_crash_report_block_before_the_start_is_not_this_runs_failure(self):
        log = "[2026-10-31 22:00:01] [ERROR] [CrashReport] TAOM CrashReport captured at boot\n"
        self.assertEqual(r.judge(self.DONE, {"final": True}, log)[0], 0)

    def test_unparsable_times_count_every_crash_report_block(self):
        log = "[2026-10-31 22:17:23] [ERROR] [CrashReport] TAOM CrashReport captured\n"
        self.assertEqual(r.judge({"reason": "final"}, {"final": True}, log)[0], 1)


class FinalVerdictTests(unittest.TestCase):
    PASSED = (0, "reason=final, report final=true")

    def test_a_clean_exit_keeps_the_judged_result(self):
        self.assertEqual(r.final_verdict(self.PASSED, None, False), self.PASSED)
        self.assertEqual(r.final_verdict(self.PASSED, None, False, 0), self.PASSED)

    def test_the_normal_exit_code_after_quit_keeps_the_judged_result(self):
        # Bannerlord exits with -1 (4294967295 as a Windows exit code) after its own QuitGame, clean unload line present.
        self.assertEqual(r.final_verdict(self.PASSED, None, False, 4294967295), self.PASSED)
        self.assertEqual(r.final_verdict(self.PASSED, None, False, -1), self.PASSED)

    def test_an_abnormal_exit_code_fails_a_passing_run(self):
        code, why = r.final_verdict(self.PASSED, None, False, 3221225477)
        self.assertEqual(code, 1)
        self.assertIn("0xC0000005", why)

    def test_a_crash_after_the_done_file_fails_a_passing_run(self):
        code, why = r.final_verdict(self.PASSED, "rgl_log_1.txt reports a crash", False)
        self.assertEqual(code, 1)
        self.assertIn("rgl_log_1.txt", why)

    def test_a_kill_after_the_done_file_fails_a_passing_run(self):
        code, why = r.final_verdict(self.PASSED, None, True)
        self.assertEqual(code, 1)
        self.assertIn("killed", why)

    def test_an_already_failed_run_keeps_its_own_reason(self):
        failed = (1, "reason=never_live")
        self.assertEqual(r.final_verdict(failed, "crash", True, 3221225477), failed)


class DialogActionTests(unittest.TestCase):
    # The engine's notice as seen on 2026-10-31 (title, the static text, the button).
    NOTICE = ["OK", "", "A change in your active mods was detected. Deleting the runtime shader cache to avoid rendering issues."]

    def test_the_mod_change_notice_is_dismissed(self):
        self.assertEqual(r.dialog_action("Mod change detected", self.NOTICE), "dismiss")

    def test_the_title_alone_is_not_enough(self):
        self.assertEqual(r.dialog_action("Mod change detected", ["OK", "Something else"]), "leave")

    def test_a_crash_title_fails_the_run(self):
        self.assertEqual(r.dialog_action("Bannerlord Crash Report", ["Send", "Close"]), "fail")
        self.assertEqual(r.dialog_action("Fatal error", []), "fail")

    def test_any_other_dialog_is_left_alone(self):
        self.assertEqual(r.dialog_action("Safe Mode", ["Yes", "No"]), "leave")
        self.assertEqual(r.dialog_action("", []), "leave")


class PowershellTests(unittest.TestCase):
    def test_a_non_zero_exit_raises_instead_of_returning_empty_output(self):
        failed = SimpleNamespace(returncode=1, stdout="", stderr="boom")
        with mock.patch.object(r.subprocess, "run", return_value=failed):
            with self.assertRaises(OSError):
                r.powershell("Get-Date")

    def test_a_zero_exit_returns_stripped_output(self):
        done = SimpleNamespace(returncode=0, stdout="  hello \n", stderr="")
        with mock.patch.object(r.subprocess, "run", return_value=done):
            self.assertEqual(r.powershell("Get-Date"), "hello")


class PreflightTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.game = Path(self.tmp.name)
        exe_dir = self.game / "bin" / "Win64_Shipping_Client"
        exe_dir.mkdir(parents=True)
        (exe_dir / "Bannerlord.exe").write_bytes(b"")

    def test_nothing_running_and_the_exe_present_is_go(self):
        with mock.patch.object(r, "list_processes", return_value=[]):
            self.assertEqual(r.preflight(self.game), [])

    def test_a_running_bannerlord_refuses(self):
        running = [{"pid": 7, "name": "Bannerlord.exe", "path": "x", "cmd": "", "created": ""}]
        with mock.patch.object(r, "list_processes", return_value=running):
            problems = r.preflight(self.game)
        self.assertEqual(len(problems), 1)
        self.assertIn("pid 7", problems[0])

    def test_a_watchdog_from_the_game_folder_refuses(self):
        running = [{"pid": 8, "name": "Watchdog.exe", "path": str(self.game / "bin" / "Watchdog.exe"), "cmd": "", "created": ""}]
        with mock.patch.object(r, "list_processes", return_value=running):
            self.assertEqual(len(r.preflight(self.game)), 1)

    def _kit_and_dog(self, target=54052):
        kit = {"pid": 54052, "name": r.KIT_PROCESS, "path": str(self.game / "bin" / "Win64_Shipping_wEditor" / r.KIT_PROCESS), "cmd": "", "created": ""}
        dog = {"pid": 24000, "name": "Watchdog.exe", "path": str(self.game / "bin" / "Watchdog.exe"), "cmd": f"Watchdog.exe -p {target} -dd x", "created": ""}
        return [kit, dog]

    def test_an_open_kit_refuses_and_says_why(self):
        with mock.patch.object(r, "list_processes", return_value=self._kit_and_dog()):
            problems = r.preflight(self.game)
        self.assertEqual(len(problems), 1)
        self.assertIn("Modding Kit", problems[0])
        self.assertIn("shader cache", problems[0])

    def test_a_watchdog_of_anything_but_the_kit_refuses_without_naming_the_kit(self):
        with mock.patch.object(r, "list_processes", return_value=self._kit_and_dog(target=999)):
            problems = r.preflight(self.game)
        self.assertEqual(len(problems), 1)
        self.assertIn("pid 24000", problems[0])
        self.assertNotIn("Modding Kit", problems[0])

    def test_the_games_own_launcher_is_not_the_kit(self):
        launcher = {"pid": 54052, "name": r.KIT_PROCESS, "path": str(self.game / "bin" / "Win64_Shipping_Client" / r.KIT_PROCESS), "cmd": "", "created": ""}
        dog = {"pid": 24000, "name": "Watchdog.exe", "path": str(self.game / "bin" / "Watchdog.exe"), "cmd": "Watchdog.exe -p 54052", "created": ""}
        with mock.patch.object(r, "list_processes", return_value=[launcher, dog]):
            problems = r.preflight(self.game)
        self.assertEqual(len(problems), 1)
        self.assertNotIn("Modding Kit", problems[0])

    def test_a_missing_exe_refuses(self):
        (self.game / "bin" / "Win64_Shipping_Client" / "Bannerlord.exe").unlink()
        with mock.patch.object(r, "list_processes", return_value=[]):
            self.assertIn("missing", r.preflight(self.game)[0])

    def test_a_process_listing_that_fails_refuses_instead_of_passing(self):
        with mock.patch.object(r, "list_processes", side_effect=OSError("powershell exited 1")):
            problems = r.preflight(self.game)
        self.assertEqual(len(problems), 1)
        self.assertIn("could not list", problems[0])

    def test_a_process_listing_that_is_not_json_refuses(self):
        with mock.patch.object(r, "list_processes", side_effect=ValueError("bad json")):
            self.assertIn("could not list", r.preflight(self.game)[0])


class IdleGateTests(unittest.TestCase):
    def test_a_disabled_gate_passes_whatever_the_idle_time(self):
        self.assertTrue(r.idle_ok(0, 0))
        self.assertTrue(r.idle_ok(None, 0))

    def test_idle_at_or_past_the_required_minutes_passes(self):
        self.assertTrue(r.idle_ok(600, 10))
        self.assertTrue(r.idle_ok(601.5, 10))

    def test_idle_short_of_the_required_minutes_fails(self):
        self.assertFalse(r.idle_ok(599.9, 10))
        self.assertFalse(r.idle_ok(0, 10))

    def test_an_unknown_idle_time_fails_closed_when_the_gate_is_on(self):
        self.assertFalse(r.idle_ok(None, 10))

    def test_tick_delta_is_plain_subtraction_without_a_wrap(self):
        self.assertEqual(r.tick_delta_ms(5000, 2000), 3000)
        self.assertEqual(r.tick_delta_ms(2000, 2000), 0)

    def test_tick_delta_survives_the_32_bit_tick_count_wrap(self):
        self.assertEqual(r.tick_delta_ms(1000, 0xFFFFFC18), 1000 + 1000)
        self.assertEqual(r.tick_delta_ms(0, 0xFFFFFFFF), 1)

    def test_the_idle_text_names_the_state_and_the_requirement(self):
        self.assertEqual(r.idle_state_text(1234.4, 10), "input 1234 s ago; the gate needs 600 s: ok")
        self.assertEqual(r.idle_state_text(30, 10), "input 30 s ago; the gate needs 600 s: NOT idle")
        self.assertEqual(r.idle_state_text(30, 0), "input 30 s ago; the gate is off (--require-idle-minutes 0)")
        self.assertIn("unreadable", r.idle_state_text(None, 10))

    def _libs(self, last_input_tick, now_tick, accepted=True):
        user32 = mock.Mock()
        kernel32 = mock.Mock()
        seen = {}

        def get_last_input_info(pointer):
            info = pointer._obj
            seen["cb_size"] = info.cbSize
            info.dwTime = last_input_tick
            return 1 if accepted else 0

        user32.GetLastInputInfo.side_effect = get_last_input_info
        kernel32.GetTickCount.return_value = now_tick
        return user32, kernel32, seen

    def test_seconds_since_last_input_is_the_tick_difference_in_seconds(self):
        user32, kernel32, seen = self._libs(last_input_tick=10_000, now_tick=25_500)
        with mock.patch.object(r, "_input_libs", return_value=(user32, kernel32)):
            self.assertEqual(r.seconds_since_last_input(), 15.5)
        self.assertEqual(seen["cb_size"], 8)

    def test_seconds_since_last_input_handles_the_tick_wrap(self):
        user32, kernel32, _ = self._libs(last_input_tick=0xFFFFFC18, now_tick=1000)
        with mock.patch.object(r, "_input_libs", return_value=(user32, kernel32)):
            self.assertEqual(r.seconds_since_last_input(), 2.0)

    def test_a_refused_query_raises_instead_of_reading_as_idle(self):
        user32, kernel32, _ = self._libs(last_input_tick=0, now_tick=0, accepted=False)
        with mock.patch.object(r, "_input_libs", return_value=(user32, kernel32)):
            with self.assertRaises(OSError):
                r.seconds_since_last_input()

    def test_the_input_libs_declare_their_argument_and_return_types(self):
        if sys.platform != "win32":
            self.skipTest("user32 and kernel32 exist on Windows only")
        user32, kernel32 = r._input_libs()
        self.assertIsNotNone(user32.GetLastInputInfo.argtypes)
        self.assertIsNotNone(user32.GetLastInputInfo.restype)
        self.assertEqual(list(kernel32.GetTickCount.argtypes), [])
        self.assertIsNotNone(kernel32.GetTickCount.restype)

    def test_the_machine_reading_is_a_plausible_number_on_windows(self):
        if sys.platform != "win32":
            self.skipTest("user32 and kernel32 exist on Windows only")
        value = r.seconds_since_last_input()
        self.assertGreaterEqual(value, 0)
        self.assertLess(value, 60 * 60 * 24 * 60)


class PreflightIdleTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.game = Path(self.tmp.name)
        exe_dir = self.game / "bin" / "Win64_Shipping_Client"
        exe_dir.mkdir(parents=True)
        (exe_dir / "Bannerlord.exe").write_bytes(b"")

    def test_recent_input_refuses_with_the_reason_and_the_override(self):
        with mock.patch.object(r, "list_processes", return_value=[]), \
                mock.patch.object(r, "seconds_since_last_input", return_value=42.0):
            problems = r.preflight(self.game, 10)
        self.assertEqual(len(problems), 1)
        self.assertIn("the PC had input 42 s ago", problems[0])
        self.assertIn("the idle gate needs 600 s", problems[0])
        self.assertIn("--require-idle-minutes 0", problems[0])
        self.assertIn("maintainer has said yes", problems[0])

    def test_enough_idle_time_is_go(self):
        with mock.patch.object(r, "list_processes", return_value=[]), \
                mock.patch.object(r, "seconds_since_last_input", return_value=601.0):
            self.assertEqual(r.preflight(self.game, 10), [])

    def test_a_disabled_gate_never_reads_the_input_clock(self):
        with mock.patch.object(r, "list_processes", return_value=[]), \
                mock.patch.object(r, "seconds_since_last_input", side_effect=AssertionError("must not be read")):
            self.assertEqual(r.preflight(self.game, 0), [])

    def test_an_unreadable_input_clock_refuses_instead_of_passing(self):
        with mock.patch.object(r, "list_processes", return_value=[]), \
                mock.patch.object(r, "seconds_since_last_input", side_effect=OSError("no desktop")):
            problems = r.preflight(self.game, 10)
        self.assertEqual(len(problems), 1)
        self.assertIn("could not read the input idle time", problems[0])

    def test_a_running_game_and_recent_input_both_show(self):
        running = [{"pid": 7, "name": "Bannerlord.exe", "path": "x", "cmd": "", "created": ""}]
        with mock.patch.object(r, "list_processes", return_value=running), \
                mock.patch.object(r, "seconds_since_last_input", return_value=5.0):
            self.assertEqual(len(r.preflight(self.game, 10)), 2)

    def test_the_idle_refusal_survives_a_failed_process_listing(self):
        with mock.patch.object(r, "list_processes", side_effect=OSError("powershell exited 1")), \
                mock.patch.object(r, "seconds_since_last_input", return_value=5.0):
            self.assertEqual(len(r.preflight(self.game, 10)), 2)


class StopOwnTreeTests(unittest.TestCase):
    @staticmethod
    def proc(running=True, pid=100):
        proc = mock.Mock()
        proc.pid = pid
        proc.poll.return_value = None if running else 0
        return proc

    def test_a_running_game_is_killed_through_its_popen_handle(self):
        proc = self.proc()
        with mock.patch.object(r, "list_processes", return_value=[]), mock.patch.object(r, "kill_pid") as kill_pid:
            killed = r.stop_own_tree(proc, lambda _: None)
        self.assertTrue(killed)
        proc.kill.assert_called_once_with()
        proc.wait.assert_called_once_with(30)
        kill_pid.assert_not_called()

    def test_an_exited_game_is_not_killed(self):
        proc = self.proc(running=False)
        with mock.patch.object(r, "list_processes", return_value=[]):
            self.assertFalse(r.stop_own_tree(proc, lambda _: None))
        proc.kill.assert_not_called()

    def test_only_the_watchdog_that_watches_this_pid_is_killed(self):
        proc = self.proc(running=False, pid=100)
        dogs = [{"pid": 11, "name": "Watchdog.exe", "cmd": "Watchdog.exe -p 100 -x"},
                {"pid": 12, "name": "Watchdog.exe", "cmd": "Watchdog.exe -p 1000"}]
        with mock.patch.object(r, "list_processes", return_value=dogs), mock.patch.object(r, "kill_pid") as kill_pid:
            killed = r.stop_own_tree(proc, lambda _: None)
        self.assertTrue(killed)
        kill_pid.assert_called_once_with(11)

    def test_a_watchdog_listing_that_fails_does_not_hide_the_game_kill(self):
        proc = self.proc()
        said = []
        with mock.patch.object(r, "list_processes", side_effect=OSError("boom")):
            killed = r.stop_own_tree(proc, said.append)
        self.assertTrue(killed)
        proc.kill.assert_called_once_with()
        self.assertTrue(any("watchdog" in line.lower() for line in said))

    def test_a_game_that_ignores_the_kill_wait_is_reported_not_raised(self):
        proc = self.proc()
        proc.wait.side_effect = subprocess.TimeoutExpired("x", 30)
        with mock.patch.object(r, "list_processes", return_value=[]):
            self.assertTrue(r.stop_own_tree(proc, lambda _: None))


class ResetSafelyExitedTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "engine_config.txt"
        self.said = []

    def test_zero_is_rewritten_to_one(self):
        self.path.write_bytes(b"a = 1\r\nsafely_exited = 0\r\n")
        r.reset_safely_exited(self.path, self.said.append)
        self.assertEqual(self.path.read_bytes(), b"a = 1\r\nsafely_exited = 1\r\n")
        self.assertIn("was 0", self.said[0])

    def test_one_is_reported_as_already_one(self):
        self.path.write_bytes(b"safely_exited = 1\r\n")
        r.reset_safely_exited(self.path, self.said.append)
        self.assertIn("already 1", self.said[0])

    def test_an_absent_key_is_reported_not_called_already_one(self):
        self.path.write_bytes(b"other = 0\r\n")
        r.reset_safely_exited(self.path, self.said.append)
        self.assertIn("key not found", self.said[0])
        self.assertNotIn("already 1", self.said[0])

    def test_an_unreadable_file_is_reported(self):
        r.reset_safely_exited(self.path, self.said.append)
        self.assertIn("not readable", self.said[0])

    def test_a_failed_write_is_reported_not_raised(self):
        self.path.write_bytes(b"safely_exited = 0\r\n")
        with mock.patch.object(Path, "write_bytes", side_effect=OSError("locked")):
            r.reset_safely_exited(self.path, self.said.append)
        self.assertIn("not written", self.said[0])


class WatchTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.folder = Path(self.tmp.name)
        self.said = []
        patches = [mock.patch.object(r.time, "sleep"), mock.patch.object(r, "handle_dialogs", return_value=None),
                   mock.patch.object(r, "scan_crash", return_value=None)]
        for patcher in patches:
            patcher.start()
            self.addCleanup(patcher.stop)

    @staticmethod
    def proc(returncode=None):
        proc = mock.Mock()
        proc.pid = 100
        proc.poll.return_value = returncode
        proc.returncode = returncode
        return proc

    def test_the_done_file_ends_the_watch(self):
        (self.folder / r.DONE_FILE).write_text("reason=final\n", encoding="utf-8")
        self.assertEqual(r.watch(self.proc(), self.folder, 100, self.said.append), "done")

    def test_a_game_that_exits_without_a_done_file_is_a_failure_text(self):
        result = r.watch(self.proc(returncode=3), self.folder, 100, self.said.append)
        self.assertIn("exited (code 3)", result)

    def test_a_crash_dialog_ends_the_watch(self):
        with mock.patch.object(r, "handle_dialogs", return_value="crash dialog 'Problem'"):
            result = r.watch(self.proc(), self.folder, 100, self.said.append)
        self.assertEqual(result, "crash: crash dialog 'Problem'")

    def test_a_crash_marker_in_the_engine_logs_ends_the_watch(self):
        with mock.patch.object(r, "scan_crash", return_value="rgl_log_100.txt reports a crash"):
            result = r.watch(self.proc(), self.folder, 100, self.said.append)
        self.assertEqual(result, "crash: rgl_log_100.txt reports a crash")

    def test_a_deadline_in_the_past_times_out(self):
        result = r.watch(self.proc(), self.folder, -10 * 3600, self.said.append)
        self.assertTrue(result.startswith("timeout"))

    def test_a_status_file_that_never_changes_stalls(self):
        (self.folder / r.STATUS_FILE).write_text("phase=Live\n", encoding="utf-8")
        clock = iter(range(0, 10**6, 100))
        with mock.patch.object(r.time, "time", side_effect=lambda: float(next(clock))):
            result = r.watch(self.proc(), self.folder, 10**6, self.said.append)
        self.assertTrue(result.startswith("stall"))


class CollectTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.folder = root / "run"
        self.folder.mkdir()
        self.bin_dir = root / "bin"
        (self.bin_dir / "Logs").mkdir(parents=True)
        self.engine_logs = root / "engine"
        self.engine_logs.mkdir()
        patcher = mock.patch.object(r, "LOGS_DIR", str(self.engine_logs))
        patcher.start()
        self.addCleanup(patcher.stop)
        self.said = []

    def test_the_done_files_log_and_report_are_copied_and_the_report_is_parsed(self):
        log = self.bin_dir / "Logs" / "taom_debug_2026-10-31_22-08-35.log"
        report = self.bin_dir / "Logs" / "fords_report_2026-10-31_22-08-35_run1.json"
        log.write_text("line\n", encoding="utf-8")
        report.write_text(json.dumps({"final": True}), encoding="utf-8")
        (self.engine_logs / "rgl_log_100.txt").write_text("ok", encoding="utf-8")
        done = {"log": str(log), "report": str(report)}

        log_copy, report_json = r.collect(self.folder, self.bin_dir, done, 0.0, 100, self.said.append)

        self.assertEqual(report_json, {"final": True})
        self.assertEqual(log_copy, self.folder / log.name)
        self.assertTrue((self.folder / "rgl_log_100.txt").is_file())

    def test_without_a_done_log_the_newest_log_after_the_start_is_used(self):
        log = self.bin_dir / "Logs" / "taom_debug_2026-10-31_22-08-35.log"
        log.write_text("line\n", encoding="utf-8")

        log_copy, report_json = r.collect(self.folder, self.bin_dir, {}, 0.0, 100, self.said.append)

        self.assertEqual(log_copy, self.folder / log.name)
        self.assertIsNone(report_json)

    def test_nothing_to_copy_returns_none_and_says_so(self):
        log_copy, report_json = r.collect(self.folder, self.bin_dir, {}, 10**12, 100, self.said.append)

        self.assertIsNone(log_copy)
        self.assertIsNone(report_json)
        self.assertTrue(any(line.startswith("no log file") for line in self.said))

    def test_an_unreadable_report_json_is_reported(self):
        report = self.bin_dir / "Logs" / "fords_report_x_run1.json"
        report.write_text("{not json", encoding="utf-8")

        _, report_json = r.collect(self.folder, self.bin_dir, {"report": str(report)}, 0.0, 100, self.said.append)

        self.assertIsNone(report_json)
        self.assertTrue(any("unreadable" in line for line in self.said))


class HandleDialogsTests(unittest.TestCase):
    NOTICE = ["OK", "", "A change in your active mods was detected. Deleting the runtime shader cache."]

    @staticmethod
    def dialogs(*rows):
        return mock.patch.object(r, "game_dialogs", return_value=list(rows))

    def test_the_notice_is_dismissed_through_its_ok_button(self):
        said = []
        with self.dialogs((5, "Mod change detected", self.NOTICE, {"OK": 1})), \
                mock.patch.object(r, "click_button", return_value=True) as click:
            self.assertIsNone(r.handle_dialogs(100, set(), said.append))
        click.assert_called_once_with(5, 1)
        self.assertEqual(len(said), 1)

    def test_a_dismissed_window_is_logged_once_even_if_it_lingers(self):
        said, seen = [], set()
        with self.dialogs((5, "Mod change detected", self.NOTICE, {"OK": 1})), mock.patch.object(r, "click_button", return_value=True):
            r.handle_dialogs(100, seen, said.append)
            r.handle_dialogs(100, seen, said.append)
            r.handle_dialogs(100, seen, said.append)
        self.assertEqual(len(said), 1)

    def test_a_click_that_found_no_button_is_not_logged_as_dismissed(self):
        said = []
        with self.dialogs((5, "Mod change detected", self.NOTICE, {"OK": 1})), mock.patch.object(r, "click_button", return_value=False):
            r.handle_dialogs(100, set(), said.append)
        self.assertEqual(said, [])

    def test_a_crash_dialog_returns_its_text_and_is_not_clicked(self):
        with self.dialogs((6, "Bannerlord Crash Report", ["Send", "Close"], {"Close": 2})), \
                mock.patch.object(r, "click_button") as click:
            result = r.handle_dialogs(100, set(), lambda _: None)
        self.assertIn("Bannerlord Crash Report", result)
        click.assert_not_called()

    def test_any_other_dialog_is_logged_once_and_never_clicked(self):
        said, seen = [], set()
        with self.dialogs((7, "Safe Mode", ["Yes", "No"], {"Yes": 1})), mock.patch.object(r, "click_button") as click:
            r.handle_dialogs(100, seen, said.append)
            r.handle_dialogs(100, seen, said.append)
        self.assertEqual(len(said), 1)
        click.assert_not_called()

    def test_a_dialog_enumeration_failure_is_reported_not_raised(self):
        said = []
        with mock.patch.object(r, "game_dialogs", side_effect=OSError("no user32")):
            self.assertIsNone(r.handle_dialogs(100, set(), said.append))
        self.assertIn("dialog check failed", said[0])


class ClickButtonTests(unittest.TestCase):
    def test_the_click_is_a_posted_wm_command_with_the_control_id_and_button_handle(self):
        user32 = mock.Mock()
        user32.GetDlgItem.return_value = 77
        user32.PostMessageW.return_value = 1
        with mock.patch.object(r, "_user32", return_value=user32):
            self.assertTrue(r.click_button(5, 1))
        user32.PostMessageW.assert_called_once_with(5, 0x0111, 1, 77)

    def test_the_click_never_sends_a_blocking_message_or_activates_the_window(self):
        user32 = mock.Mock()
        user32.GetDlgItem.return_value = 77
        user32.PostMessageW.return_value = 1
        with mock.patch.object(r, "_user32", return_value=user32):
            r.click_button(5, 1)
        user32.SendMessageW.assert_not_called()
        user32.SetForegroundWindow.assert_not_called()

    def test_a_missing_button_posts_nothing(self):
        user32 = mock.Mock()
        user32.GetDlgItem.return_value = 0
        with mock.patch.object(r, "_user32", return_value=user32):
            self.assertFalse(r.click_button(5, 1))
        user32.PostMessageW.assert_not_called()


class LauncherInputTests(unittest.TestCase):
    def test_non_positive_wall_limit_and_live_seconds_are_rejected(self):
        parser = r.build_parser()
        for flag in ("--wall-limit", "--live-seconds"):
            for value in ("0", "-5", "nan"):
                with self.assertRaises(SystemExit, msg=f"{flag} {value}"), mock.patch("sys.stderr"):
                    parser.parse_args([flag, value])

    def test_a_non_positive_control_check_is_rejected_and_a_positive_one_reaches_the_arm_file(self):
        parser = r.build_parser()
        for value in ("0", "-5", "nan"):
            with self.assertRaises(SystemExit, msg=value), mock.patch("sys.stderr"):
                parser.parse_args(["--control-check", value])
        text, _ = r.arm_text_for(r"E:\x", parser.parse_args(["--control-check", "240"]))
        self.assertIn("control_check=240.0\n", text)

    def test_the_documented_command_line_still_parses(self):
        args = r.build_parser().parse_args(["--scene", "s", "--west", "w", "--east", "e", "--ai", "a", "--live-seconds", "90",
                                            "--wall-limit", "300", "--west-ai", "--runs", "2", "--runs-root", "x", "--dry-run"])
        self.assertEqual((args.live_seconds, args.wall_limit, args.runs, args.west_ai, args.dry_run), (90.0, 300.0, 2, True, True))

    def test_the_idle_gate_defaults_to_ten_minutes_and_can_be_switched_off(self):
        parser = r.build_parser()
        self.assertEqual(parser.parse_args([]).require_idle_minutes, 10)
        self.assertEqual(parser.parse_args(["--require-idle-minutes", "0"]).require_idle_minutes, 0)
        self.assertEqual(parser.parse_args(["--require-idle-minutes", "2.5"]).require_idle_minutes, 2.5)

    def test_a_negative_or_non_finite_idle_requirement_is_rejected(self):
        parser = r.build_parser()
        for value in ("-1", "nan", "inf"):
            with self.assertRaises(SystemExit, msg=value), mock.patch("sys.stderr"):
                parser.parse_args(["--require-idle-minutes", value])

    def test_the_runs_root_is_resolved_to_an_absolute_path(self):
        args = r.build_parser().parse_args(["--runs-root", "relative-runs"])
        self.assertTrue(Path(r.resolved_runs_root(args)).is_absolute())

    def test_the_arm_file_always_carries_the_runners_wall_limit(self):
        with tempfile.TemporaryDirectory() as tmp:
            args = r.build_parser().parse_args([])
            arm, wall_limit = r.write_arm_file(Path(tmp), args)
            text = arm.read_text(encoding="utf-8")
        self.assertEqual(wall_limit, r.DEFAULT_WALL_LIMIT_SECONDS)
        self.assertIn(f"wall_limit_seconds={r.DEFAULT_WALL_LIMIT_SECONDS}\n", text)

    def test_an_explicit_wall_limit_is_the_one_written_and_returned(self):
        with tempfile.TemporaryDirectory() as tmp:
            args = r.build_parser().parse_args(["--wall-limit", "900"])
            arm, wall_limit = r.write_arm_file(Path(tmp), args)
            text = arm.read_text(encoding="utf-8")
        self.assertEqual(wall_limit, 900.0)
        self.assertIn("wall_limit_seconds=900.0\n", text)

    def test_the_arm_file_is_written_with_lf_line_ends(self):
        with tempfile.TemporaryDirectory() as tmp:
            args = r.build_parser().parse_args(["--west", "gondor"])
            arm, _ = r.write_arm_file(Path(tmp), args)
            self.assertNotIn(b"\r", arm.read_bytes())


class SafeWatchTests(unittest.TestCase):
    def test_a_runner_error_in_watch_is_turned_into_a_failure_reason(self):
        with mock.patch.object(r, "watch", side_effect=RuntimeError("boom")):
            self.assertEqual(r.safe_watch(mock.Mock(), Path("."), 10, lambda _: None), "runner error: RuntimeError: boom")

    def test_a_normal_watch_result_passes_through(self):
        with mock.patch.object(r, "watch", return_value="done"):
            self.assertEqual(r.safe_watch(mock.Mock(), Path("."), 10, lambda _: None), "done")


class WaitAfterCrashTests(unittest.TestCase):
    class Proc:
        def __init__(self, polls):
            self.polls = polls

        def poll(self):
            return self.polls.pop(0) if self.polls else None

    def test_waits_until_the_game_ends(self):
        said = []
        self.assertTrue(r.wait_after_crash(self.Proc([None, None, 0]), 60, said.append, sleep=lambda _: None))
        self.assertIn("answer", said[0])

    def test_gives_up_after_the_limit(self):
        clock = iter(range(0, 1000, 10))
        self.assertFalse(r.wait_after_crash(self.Proc([]), 30, lambda _: None, sleep=lambda _: None, now=lambda: next(clock)))

    def test_zero_seconds_does_not_wait(self):
        said = []
        self.assertFalse(r.wait_after_crash(self.Proc([None]), 0, said.append))
        self.assertEqual([], said)

    def test_calls_on_poll_while_it_waits_and_once_after(self):
        polls = []
        r.wait_after_crash(self.Proc([None, None, 0]), 60, lambda _: None, sleep=lambda _: None, on_poll=lambda: polls.append(1))
        self.assertEqual(3, len(polls))


class SaveEngineDumpTests(unittest.TestCase):
    """2026-11-05: the crash uploader deleted crashes/<stamp>/dump.dmp when Mike sent the report, so the run kept no dump. The Watchdog
    writes the dump twice (a stack-only pass, then the full one) and lets the game die right after, so the signal is its own log."""

    def setUp(self):
        self.root = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, str(self.root), True)
        self.log = self.root / "watchdog_log_7.txt"
        self.target = self.root / "engine_dump.dmp"
        self.said = []

    def dump(self, stamp, data):
        path = self.root / "crashes" / stamp / "dump.dmp"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def write_log(self, *paths, uploader=True):
        lines = ["Writing stack only dump... This may take a while..."] + [f"Dump file created at {p}." for p in paths]
        self.log.write_text("\n".join(lines + (["Starting Crash Uploader"] if uploader else [])) + "\n", encoding="utf-8")

    def save(self, state=None):
        return r.save_engine_dump(self.log, self.target, state if state is not None else {}, self.said.append)

    def test_copies_the_dump_the_log_names_once_the_uploader_starts(self):
        path = self.dump("a", b"H" * 100)
        self.write_log(path, path)
        self.assertTrue(self.save())
        self.assertEqual(b"H" * 100, self.target.read_bytes())
        self.assertIn("engine_dump.dmp", self.said[-1])

    def test_waits_while_the_watchdog_is_still_writing(self):
        path = self.dump("a", b"S" * 10)
        self.write_log(path, uploader=False)
        self.assertFalse(self.save())
        self.assertFalse(self.target.exists())

    def test_takes_the_last_dump_the_log_names_not_a_newer_one_elsewhere(self):
        ours = self.dump("ours", b"O" * 10)
        self.dump("other", b"X" * 50)
        self.write_log(ours)
        self.assertTrue(self.save())
        self.assertEqual(b"O" * 10, self.target.read_bytes())

    def test_copies_once_and_says_it_once(self):
        path = self.dump("a", b"H" * 10)
        self.write_log(path)
        state = {}
        for _ in range(3):
            self.assertTrue(self.save(state))
        self.assertEqual(1, sum("saved" in line for line in self.said))

    def test_no_log_or_no_dump_line_saves_nothing_quietly(self):
        self.assertFalse(self.save())
        self.write_log()
        self.assertFalse(self.save())
        self.assertEqual([], self.said)

    def test_a_dump_the_uploader_already_deleted_is_said_once(self):
        self.write_log(self.root / "crashes" / "gone" / "dump.dmp")
        state = {}
        self.assertFalse(self.save(state))
        self.assertFalse(self.save(state))
        self.assertEqual(1, len(self.said))
        self.assertIn("by hand", self.said[0])

    def test_a_copy_error_is_said_and_not_raised(self):
        path = self.dump("a", b"H" * 10)
        self.write_log(path)
        with mock.patch.object(r.shutil, "copy2", side_effect=PermissionError(13, "denied")):
            self.assertFalse(self.save())
        self.assertIn(str(path), self.said[-1])


class DumpCrashTests(unittest.TestCase):
    """procdump v12.01 returns 0xFFFFFFFE for a dump it wrote and for a pid it never found, so the file decides."""

    PROCDUMP_EXIT = 4294967294

    def setUp(self):
        self.folder = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, str(self.folder), True)

    def fake_run(self, code, writes=None, calls=None):
        def run(command, **kwargs):
            if calls is not None:
                calls.append((command, kwargs))
            if writes is not None:
                Path(command[-1]).write_bytes(writes)
            return SimpleNamespace(returncode=code, stdout=None, stderr=None)
        return run

    def test_runs_procdump_with_the_pid_and_the_run_folder(self):
        said, calls = [], []
        r.dump_crash(4242, self.folder, said.append, run=self.fake_run(0, calls=calls), which=lambda name: "C:/tools/procdump.exe")
        command, kwargs = calls[0]
        self.assertEqual(["C:/tools/procdump.exe", "-accepteula", "-ma", "4242", str(self.folder / "crash.dmp")], command)
        self.assertIn("timeout", kwargs)

    def test_procdump_gets_no_pipe_and_writes_its_output_to_procdump_log(self):
        # A pipe is inherited by procdump64.exe, and its UTF-16 text lands as NUL bytes in runner_log.txt.
        calls = []
        r.dump_crash(1, self.folder, lambda _: None, run=self.fake_run(0, calls=calls), which=lambda name: "procdump")
        kwargs = calls[0][1]
        self.assertNotIn("capture_output", kwargs)
        self.assertNotEqual(subprocess.PIPE, kwargs.get("stdout"))
        self.assertNotEqual(subprocess.PIPE, kwargs.get("stderr"))
        self.assertTrue((self.folder / "procdump.log").is_file())

    def test_a_missing_tool_is_reported_and_does_not_raise(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=mock.Mock(side_effect=AssertionError), which=lambda name: None)
        self.assertIn("procdump", said[0])

    def test_exit_0xFFFFFFFE_with_a_dump_file_is_written(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=self.fake_run(self.PROCDUMP_EXIT, writes=b"MDMP"), which=lambda name: "procdump")
        self.assertEqual("crash dump written: " + str(self.folder / "crash.dmp"), said[-1])

    def test_exit_0xFFFFFFFE_with_no_dump_file_is_failed(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=self.fake_run(self.PROCDUMP_EXIT), which=lambda name: "procdump")
        self.assertIn("crash dump failed", said[-1])
        self.assertNotIn("written", said[-1])

    def test_exit_0_with_no_dump_file_is_failed(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=self.fake_run(0), which=lambda name: "procdump")
        self.assertIn("crash dump failed", said[-1])

    def test_an_empty_dump_file_is_failed(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=self.fake_run(self.PROCDUMP_EXIT, writes=b""), which=lambda name: "procdump")
        self.assertIn("crash dump failed", said[-1])

    def test_a_dump_file_with_a_nonzero_exit_is_still_written(self):
        said = []
        r.dump_crash(1, self.folder, said.append, run=self.fake_run(1, writes=b"MDMP"), which=lambda name: "procdump")
        self.assertIn("crash dump written", said[-1])

    def test_a_timeout_is_reported_and_does_not_raise(self):
        said = []

        def run(command, **kwargs):
            raise subprocess.TimeoutExpired(command, 1)

        r.dump_crash(1, self.folder, said.append, run=run, which=lambda name: "procdump")
        self.assertIn("timed out", said[-1])

    def test_an_unwritable_folder_is_reported_and_does_not_raise(self):
        said = []
        r.dump_crash(1, self.folder / "missing", said.append, run=mock.Mock(side_effect=AssertionError), which=lambda name: "procdump")
        self.assertIn("crash dump failed", said[-1])

    def test_a_child_that_holds_the_output_does_not_hold_up_the_timeout(self):
        # procdump.exe spawns procdump64.exe, which inherits stdout. With a pipe, subprocess.run killed the parent and then waited on the child.
        pid_file = self.folder / "child.pid"
        parent = self.folder / "parent.py"
        parent.write_text(
            "import subprocess, sys, time\n"
            "child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'])\n"
            f"open({str(pid_file)!r}, 'w').write(str(child.pid))\n"
            "time.sleep(30)\n", encoding="utf-8")

        def run(command, **kwargs):
            return subprocess.run([sys.executable, str(parent)], **kwargs)

        started = time.monotonic()
        r.dump_crash(1, self.folder, lambda _: None, run=run, which=lambda name: "procdump", timeout=1)
        elapsed = time.monotonic() - started
        for _ in range(50):
            if pid_file.is_file() and pid_file.read_text():
                break
            time.sleep(0.1)
        child_pid = int(pid_file.read_text())

        def kill_child():
            try:
                os.kill(child_pid, 9)
            except OSError:
                pass

        self.addCleanup(kill_child)
        self.assertLess(elapsed, 5)


class ScreenshotConversionTests(unittest.TestCase):
    @staticmethod
    def bmp(width, height, pixels):
        """A bottom-up 24-bit BMP. pixels are (b, g, r) tuples, bottom row first."""
        stride = ((width * 24 + 31) // 32) * 4
        body = bytearray()
        for y in range(height):
            row = bytearray()
            for x in range(width):
                row += bytes(pixels[y * width + x])
            row += b"\0" * (stride - len(row))
            body += row
        header = b"BM" + struct.pack("<IHHI", 54 + len(body), 0, 0, 54) + struct.pack("<IiiHHIIiiII", 40, width, height, 1, 24, 0, len(body), 0, 0, 0, 0)
        return bytes(header) + bytes(body)

    def test_a_small_picture_becomes_a_png_with_red_and_blue_swapped_and_the_top_row_first(self):
        raw = self.bmp(2, 2, [(1, 2, 3), (4, 5, 6), (7, 8, 9), (10, 11, 12)])

        png = r.bmp_to_png(raw)

        self.assertEqual(b"\x89PNG\r\n\x1a\n", png[:8])
        data_start = png.index(b"IDAT") + 4
        size = struct.unpack(">I", png[data_start - 8:data_start - 4])[0]
        rows = zlib.decompress(png[data_start:data_start + size])
        self.assertEqual(bytes([0, 9, 8, 7, 12, 11, 10, 0, 3, 2, 1, 6, 5, 4]), rows)

    def test_anything_but_a_24_bit_bmp_is_refused(self):
        self.assertIsNone(r.bmp_to_png(b"not a picture" + b"\0" * 60))

    def test_the_run_folder_gets_pngs_and_loses_the_bmps(self):
        with tempfile.TemporaryDirectory() as folder:
            shot = Path(folder) / "hud_5_0.bmp"
            shot.write_bytes(self.bmp(1, 1, [(1, 2, 3)]))
            (Path(folder) / "hud_bad.bmp").write_bytes(b"nope" + b"\0" * 60)
            said = []

            self.assertEqual(1, r.convert_screenshots(folder, said.append))

            self.assertTrue((Path(folder) / "hud_5_0.png").is_file())
            self.assertFalse(shot.exists())
            self.assertTrue((Path(folder) / "hud_bad.bmp").exists(), "a file it cannot read is left for a person")
            self.assertTrue(any("hud_bad" in line for line in said))

    def test_screenshots_reach_the_arm_file(self):
        self.assertIn("screenshots=8,45", r.arm_text("E:/x", screenshots="8,45"))


if __name__ == "__main__":
    unittest.main()
