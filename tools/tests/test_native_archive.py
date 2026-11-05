#!/usr/bin/env python
"""Unit tests for tools/native_archive.py, which archives each installed native engine build
(#1004).

Run:  python -m unittest discover -s tools/tests -p "test_native_archive.py"

Hermetic: a fake install (Version.xml plus fake DLL bytes) and a fake archive live in a tempdir,
the engine-method map and the matcher index are mocked, and Popen is mocked, so no case reads
the real install, runs ilspycmd or capstone, or starts a process.
"""
import contextlib
import hashlib
import io
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_archive as na  # noqa: E402

CLIENT_BYTES = b"client-dll-bytes" * 64
EDITOR_BYTES = b"editor-dll-bytes" * 64


def sha16(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()[:16]


class ArchiveCase(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.base = Path(self._tmp.name)
        self.game = self.base / "game"
        self.archive = self.base / "archive"
        self.client = self._install("Win64_Shipping_Client", CLIENT_BYTES)
        self.editor = self._install("Win64_Shipping_wEditor", EDITOR_BYTES)
        maps = mock.patch("native_engine_methods.load_or_build",
                          return_value=mock.Mock(problems=[]))
        index = mock.patch("native_build_match.load_index", return_value=mock.Mock())
        self.load_or_build = maps.start()
        self.load_index = index.start()
        self.addCleanup(mock.patch.stopall)
        self.addCleanup(self._tmp.cleanup)

    def _install(self, folder: str, data: bytes, version: str = "v1.5.5") -> Path:
        d = self.game / "bin" / folder
        d.mkdir(parents=True, exist_ok=True)
        (d / "Version.xml").write_text(f'<Version>\n\t<Singleplayer Value="{version}"/>\n</Version>',
                                       encoding="utf-8")
        dll = d / "TaleWorlds.Native.dll"
        dll.write_bytes(data)
        return dll

    def run_main(self, *extra):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = na.main(["--game-dir", str(self.game), "--archive-dir", str(self.archive), *extra])
        return rc, buf.getvalue()

    def settle(self):
        """Run --detach once so the one-time oracle lines are reported, and drop them."""
        with mock.patch("subprocess.Popen"):
            self.run_main("--detach")

    def state(self) -> dict:
        return json.loads((self.archive / na.STATE_NAME).read_text(encoding="utf-8"))

    def names(self) -> list:
        return sorted(p.name for p in self.archive.glob("TaleWorlds.Native.*.dll"))


class ForegroundTests(ArchiveCase):
    def test_new_dlls_are_copied_once_and_the_second_run_prints_nothing(self):
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.5.client.dll",
                                        "TaleWorlds.Native.v1.5.5.wEditor.dll"])
        self.assertEqual((self.archive / "TaleWorlds.Native.v1.5.5.client.dll").read_bytes(),
                         CLIENT_BYTES)
        entry = self.state()[self.client.as_posix()]
        self.assertEqual(entry["sha16"], sha16(CLIENT_BYTES))
        self.assertEqual(entry["size"], len(CLIENT_BYTES))
        self.assertEqual(entry["archived_as"], "TaleWorlds.Native.v1.5.5.client.dll")
        self.assertEqual((entry["map"], entry["index"]), ("ok", "ok"))
        calls = (self.load_or_build.call_count, self.load_index.call_count)
        rc, out = self.run_main()
        self.assertEqual((rc, out), (0, ""))
        self.assertEqual((self.load_or_build.call_count, self.load_index.call_count), calls)

    def test_map_is_built_on_the_installed_dll_and_index_on_the_archive_copy(self):
        self.run_main()
        installed = {Path(c.args[0]) for c in self.load_or_build.call_args_list}
        self.assertEqual(installed, {self.client, self.editor})
        indexed = {Path(c.args[0]) for c in self.load_index.call_args_list}
        self.assertEqual(indexed, {self.archive / "TaleWorlds.Native.v1.5.5.client.dll",
                                   self.archive / "TaleWorlds.Native.v1.5.5.wEditor.dll"})

    def test_same_version_other_sha_gets_the_sha_suffix_and_the_old_file_is_untouched(self):
        self.archive.mkdir()
        old = self.archive / "TaleWorlds.Native.v1.5.5.client.dll"
        old.write_bytes(b"an older build")
        stamp = os.stat(old).st_mtime_ns
        self.run_main()
        self.assertEqual(old.read_bytes(), b"an older build")
        self.assertEqual(os.stat(old).st_mtime_ns, stamp)
        suffixed = self.archive / f"TaleWorlds.Native.v1.5.5.client.{sha16(CLIENT_BYTES)}.dll"
        self.assertEqual(suffixed.read_bytes(), CLIENT_BYTES)
        self.assertEqual(self.state()[self.client.as_posix()]["archived_as"], suffixed.name)
        self.assertEqual(list(self.archive.glob("*.tmp")), [])

    def test_an_sha_already_archived_under_another_name_is_not_copied_again(self):
        self.archive.mkdir()
        (self.archive / "TaleWorlds.Native.v1.5.4.client.dll").write_bytes(CLIENT_BYTES)
        self.run_main()
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.4.client.dll",
                                        "TaleWorlds.Native.v1.5.5.wEditor.dll"])
        self.assertEqual(self.state()[self.client.as_posix()]["archived_as"],
                         "TaleWorlds.Native.v1.5.4.client.dll")

    def test_a_changed_dll_is_archived_again_next_run(self):
        self.run_main()
        self._install("Win64_Shipping_Client", CLIENT_BYTES + b"patched", version="v1.5.6")
        self.run_main()
        self.assertIn("TaleWorlds.Native.v1.5.6.client.dll", self.names())

    def test_missing_game_dir_prints_nothing(self):
        self.game = self.base / "nowhere"
        self.assertEqual(self.run_main(), (0, ""))
        self.assertFalse(self.archive.exists())

    def test_missing_dll_is_skipped_silently_and_the_other_build_is_still_archived(self):
        self.editor.unlink()
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertNotIn("NOTE", out)
        self.assertNotIn("wEditor", out)
        self.assertEqual(self.names(), ["TaleWorlds.Native.v1.5.5.client.dll"])
        self.settle()
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_uncreatable_archive_dir_prints_the_note(self):
        blocker = self.base / "blocker"
        blocker.write_text("a file", encoding="utf-8")
        self.archive = blocker / "sub"
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertTrue(out.startswith("NOTE: native archive UNCHECKED: "), out)
        self.assertEqual(len(out.strip().splitlines()), 1)

    def test_a_corrupt_state_file_is_rebuilt_not_fatal(self):
        self.archive.mkdir()
        (self.archive / na.STATE_NAME).write_text("{not json", encoding="utf-8")
        rc, out = self.run_main()
        self.assertEqual(rc, 0)
        self.assertEqual(self.state()[self.client.as_posix()]["map"], "ok")


class FailureTests(ArchiveCase):
    def test_a_map_failure_is_recorded_reported_next_run_retried_and_keeps_the_copy(self):
        self.load_or_build.side_effect = RuntimeError("ilspycmd not found")
        self.run_main()
        copy = self.archive / "TaleWorlds.Native.v1.5.5.client.dll"
        self.assertEqual(copy.read_bytes(), CLIENT_BYTES)
        entry = self.state()[self.client.as_posix()]
        self.assertEqual(entry["map"], "failed: ilspycmd not found")
        self.assertEqual(entry["index"], "ok")

        with mock.patch("subprocess.Popen") as popen:
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        self.assertIn("NOTE: native archive: v1.5.5 client map failed: ilspycmd not found "
                      "(see the log)", out.splitlines())
        self.assertNotIn("archiving", out)
        self.assertEqual(popen.call_count, 1)

        self.load_or_build.side_effect = None
        self.run_main()
        self.assertEqual(self.state()[self.client.as_posix()]["map"], "ok")
        self.assertTrue(copy.is_file())
        self.settle()
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_an_index_failure_is_reported_as_an_index_failure(self):
        self.load_index.side_effect = RuntimeError("capstone missing")
        self.run_main()
        with mock.patch("subprocess.Popen"):
            rc, out = self.run_main("--detach")
        self.assertIn("NOTE: native archive: v1.5.5 client index failed: capstone missing "
                      "(see the log)", out.splitlines())

    def test_a_map_with_problems_counts_as_a_failure(self):
        self.load_or_build.return_value = mock.Mock(problems=["gap at 0x10"])
        self.run_main()
        self.assertTrue(self.state()[self.client.as_posix()]["map"].startswith("failed: "))


class LockTests(ArchiveCase):
    def _lock(self, age_seconds: float) -> Path:
        self.archive.mkdir(parents=True, exist_ok=True)
        lock = self.archive / na.LOCK_NAME
        lock.write_text("1 1\n", encoding="utf-8")
        t = time.time() - age_seconds
        os.utime(lock, (t, t))
        return lock

    def test_a_held_fresh_lock_prints_nothing_and_does_nothing(self):
        self._lock(10)
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)
        self.assertEqual(self.run_main(), (0, ""))
        self.assertEqual(self.names(), [])

    def test_a_stale_lock_is_taken_and_released(self):
        lock = self._lock(na.LOCK_STALE_SECONDS + 60)
        self.run_main()
        self.assertEqual(len(self.names()), 2)
        self.assertFalse(lock.exists())

    def test_the_lock_is_released_after_a_normal_run(self):
        self.run_main()
        self.assertFalse((self.archive / na.LOCK_NAME).exists())


class DetachTests(ArchiveCase):
    def test_detach_returns_without_waiting_and_prints_one_line(self):
        with mock.patch("subprocess.Popen") as popen:
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        log = self.archive / "native-archive.log"
        self.assertEqual(out, f"native archive: archiving v1.5.5 client, v1.5.5 wEditor in the "
                              f"background (log: {log.as_posix()})\n")
        self.assertEqual(popen.call_count, 1)
        cmd = popen.call_args.args[0]
        kwargs = popen.call_args.kwargs
        self.assertNotIn("--detach", cmd)
        self.assertEqual(cmd[1], str(Path(na.__file__).resolve()))
        self.assertIn(str(self.game), cmd)
        self.assertIn(str(self.archive), cmd)
        self.assertEqual(kwargs["stdin"], subprocess.DEVNULL)
        self.assertEqual(kwargs["stderr"], subprocess.STDOUT)
        if os.name == "nt":
            self.assertTrue(kwargs["creationflags"] & 0x8)    # DETACHED_PROCESS
            self.assertTrue(kwargs["creationflags"] & 0x200)  # CREATE_NEW_PROCESS_GROUP
        else:
            self.assertTrue(kwargs["start_new_session"])
        self.assertEqual(self.names(), [])  # the parent copied nothing

    def test_detach_with_nothing_to_do_starts_nothing(self):
        self.run_main()
        self.settle()
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 0)

    def test_a_failed_spawn_says_unchecked(self):
        with mock.patch("subprocess.Popen", side_effect=OSError("no process for you")):
            rc, out = self.run_main("--detach")
        self.assertEqual(rc, 0)
        self.assertIn("NOTE: native archive UNCHECKED: ", out)

    def test_a_log_past_one_megabyte_is_truncated(self):
        self.archive.mkdir()
        log = self.archive / "native-archive.log"
        log.write_bytes(b"x" * (na.LOG_CAP + 1))
        with mock.patch("subprocess.Popen"):
            self.run_main("--detach")
        self.assertEqual(log.stat().st_size, 0)

    def test_a_small_log_is_kept(self):
        self.archive.mkdir()
        log = self.archive / "native-archive.log"
        log.write_bytes(b"earlier run\n")
        with mock.patch("subprocess.Popen"):
            self.run_main("--detach")
        self.assertEqual(log.read_bytes(), b"earlier run\n")


class StatusTests(ArchiveCase):
    def test_status_prints_one_line_per_archived_build(self):
        self.run_main()
        rc, out = self.run_main("--status")
        self.assertEqual(rc, 0)
        lines = out.strip().splitlines()
        self.assertEqual(len(lines), 2)
        self.assertTrue(any("v1.5.5 client" in l and "TaleWorlds.Native.v1.5.5.client.dll" in l
                            for l in lines), out)

    def test_status_with_no_state_says_so(self):
        rc, out = self.run_main("--status")
        self.assertEqual(rc, 0)
        self.assertIn("nothing archived", out)


SUMMARY = ("oracle: 120 single-owner methods: correct 100 (hash 90, strings 10), wrong {w}, "
           "ambiguous 15, unknown 5")


def oracle_proc(rc=0, wrong=0, stderr=""):
    out = f"old: x\n  units: a\nnew: y\n  units: b\n{SUMMARY.format(w=wrong)}\n"
    return subprocess.CompletedProcess([], rc, stdout=out, stderr=stderr)


class PreviousBuildTests(ArchiveCase):
    def _files(self, *names):
        self.archive.mkdir(exist_ok=True)
        for n in names:
            (self.archive / n).write_bytes(n.encode())

    def test_picks_the_highest_lower_version_of_the_same_tag(self):
        self._files("TaleWorlds.Native.v1.5.2.client.dll", "TaleWorlds.Native.v1.5.10.client.dll",
                    "TaleWorlds.Native.v1.5.4.client.dll", "TaleWorlds.Native.v1.5.9.client.dll",
                    "TaleWorlds.Native.v1.5.6.wEditor.dll", "TaleWorlds.Native.v1.5.11.client.dll")
        got = na.previous_build(self.archive, "client", "v1.5.10", "TaleWorlds.Native.v1.5.10.client.dll")
        self.assertEqual(got, ("v1.5.9", "TaleWorlds.Native.v1.5.9.client.dll"))

    def test_ignores_the_other_tag(self):
        self._files("TaleWorlds.Native.v1.5.4.wEditor.dll")
        self.assertIsNone(na.previous_build(self.archive, "client", "v1.5.5", "x.dll"))

    def test_ignores_a_suffixed_copy_of_the_same_build(self):
        self._files("TaleWorlds.Native.v1.5.5.client.dll",
                    "TaleWorlds.Native.v1.5.5.client.0123456789abcdef.dll",
                    "TaleWorlds.Native.v1.5.4.client.dll")
        got = na.previous_build(self.archive, "client", "v1.5.5",
                                "TaleWorlds.Native.v1.5.5.client.0123456789abcdef.dll")
        self.assertEqual(got, ("v1.5.4", "TaleWorlds.Native.v1.5.4.client.dll"))

    def test_prefers_the_plain_name_over_a_suffixed_one_of_the_previous_version(self):
        self._files("TaleWorlds.Native.v1.5.4.client.0123456789abcdef.dll",
                    "TaleWorlds.Native.v1.5.4.client.dll")
        got = na.previous_build(self.archive, "client", "v1.5.5", "x.dll")
        self.assertEqual(got[1], "TaleWorlds.Native.v1.5.4.client.dll")

    def test_none_when_nothing_is_lower(self):
        self._files("TaleWorlds.Native.v1.5.5.client.dll", "TaleWorlds.Native.v1.5.6.client.dll")
        self.assertIsNone(na.previous_build(self.archive, "client", "v1.5.5", "x.dll"))

    def test_never_returns_the_build_itself_under_an_older_name(self):
        self._files("TaleWorlds.Native.v1.5.4.client.dll")
        self.assertIsNone(na.previous_build(self.archive, "client", "v1.5.5",
                                            "TaleWorlds.Native.v1.5.4.client.dll"))


class ParseOracleTests(unittest.TestCase):
    def test_the_summary_line_is_parsed(self):
        got = na.parse_oracle("noise\n" + SUMMARY.format(w=3) + "\nWRONG x\n")
        self.assertEqual(got, {"correct": 100, "wrong": 3, "ambiguous": 15, "unknown": 5})

    def test_no_summary_is_none(self):
        self.assertIsNone(na.parse_oracle("old: x\nnothing here\n"))


class OracleCase(ArchiveCase):
    def setUp(self):
        super().setUp()
        self.archive.mkdir()
        self.prev_client = self.archive / "TaleWorlds.Native.v1.5.4.client.dll"
        self.prev_client.write_bytes(b"older client")
        (self.archive / "TaleWorlds.Native.v1.5.4.wEditor.dll").write_bytes(b"older editor")
        self.this_client = self.archive / "TaleWorlds.Native.v1.5.5.client.dll"

    def oracle(self) -> dict:
        return self.state()[self.client.as_posix()]["oracle"]

    def run_with(self, proc, *extra):
        if isinstance(proc, Exception):
            patch = mock.patch("subprocess.run", side_effect=proc)
        else:
            patch = mock.patch("subprocess.run", return_value=proc)
        with patch as run:
            result = self.run_main(*extra)
        return run, result

    def detach_out(self):
        with mock.patch("subprocess.Popen"):
            return self.run_main("--detach")[1].splitlines()


class OracleStepTests(OracleCase):
    def test_exit_0_is_ok_and_runs_the_matcher_with_the_pair(self):
        run, _ = self.run_with(oracle_proc(0))
        o = self.oracle()
        self.assertEqual((o["status"], o["against"], o["correct"], o["wrong"], o["ambiguous"],
                          o["unknown"], o["reported"]), ("ok", "v1.5.4", 100, 0, 15, 5, False))
        cmds = [c for c in run.call_args_list if "--oracle" in c.args[0]]
        self.assertEqual(len(cmds), 2)  # client and wEditor
        first = next(c for c in cmds if str(self.this_client) in c.args[0])
        tool = str(Path(na.__file__).resolve().parent / "native_build_match.py")
        self.assertEqual(first.args[0], [sys.executable, tool, "--old", str(self.prev_client),
                                         "--new", str(self.this_client), "--oracle"])
        self.assertEqual(first.kwargs["timeout"], 600)
        self.assertEqual(first.kwargs["cwd"], self.archive)

    def test_exit_1_is_wrong(self):
        self.run_with(oracle_proc(1, wrong=3))
        o = self.oracle()
        self.assertEqual((o["status"], o["wrong"]), ("wrong", 3))

    def test_exit_2_is_unscored_with_the_error_line(self):
        self.run_with(oracle_proc(2, stderr="ERROR: no engine-method map for old.dll\n"))
        o = self.oracle()
        self.assertEqual((o["status"], o["reason"]), ("unscored", "no engine-method map for old.dll"))

    def test_a_timeout_is_failed(self):
        self.run_with(subprocess.TimeoutExpired(["x"], 600))
        o = self.oracle()
        self.assertEqual(o["status"], "failed")
        self.assertIn("600", o["reason"])

    def test_another_exit_code_is_failed(self):
        self.run_with(oracle_proc(3, stderr="Traceback boom"))
        o = self.oracle()
        self.assertEqual(o["status"], "failed")
        self.assertIn("boom", o["reason"])

    def test_an_unparseable_summary_is_failed(self):
        self.run_with(subprocess.CompletedProcess([], 0, stdout="no summary", stderr=""))
        self.assertEqual(self.oracle()["status"], "failed")

    def test_no_earlier_build_is_unscored_without_running_anything(self):
        self.prev_client.unlink()
        run, _ = self.run_with(oracle_proc(0))
        o = self.oracle()
        self.assertEqual((o["status"], o["reason"]), ("unscored", "no earlier archived build"))
        self.assertEqual([c for c in run.call_args_list if str(self.this_client) in c.args[0]], [])

    def test_an_unknown_version_is_skipped_with_a_reason(self):
        (self.client.parent / "Version.xml").write_text("<Version/>", encoding="utf-8")
        self.run_with(oracle_proc(0))
        o = self.oracle()
        self.assertEqual(o["status"], "unscored")
        self.assertIn("unknown", o["reason"])

    def test_no_oracle_while_the_map_failed(self):
        self.load_or_build.side_effect = RuntimeError("no ilspycmd")
        run, _ = self.run_with(oracle_proc(0))
        self.assertNotIn("oracle", self.state()[self.client.as_posix()])
        self.assertEqual(run.call_count, 0)


class OracleBackfillTests(OracleCase):
    def test_an_entry_without_an_oracle_is_backfilled_once(self):
        self.run_with(oracle_proc(0))
        state = self.state()
        del state[self.client.as_posix()]["oracle"]
        (self.archive / na.STATE_NAME).write_text(json.dumps(state), encoding="utf-8")
        calls = (self.load_or_build.call_count, self.load_index.call_count)
        run, _ = self.run_with(oracle_proc(0))
        self.assertEqual(len([c for c in run.call_args_list if "--oracle" in c.args[0]]), 1)
        self.assertEqual(self.oracle()["status"], "ok")
        self.assertEqual((self.load_or_build.call_count, self.load_index.call_count), calls)
        run, _ = self.run_with(oracle_proc(0))
        self.assertEqual(run.call_count, 0)

    def test_the_detach_parent_spawns_for_a_backfill_and_prints_nothing(self):
        self.run_with(oracle_proc(0))
        state = self.state()
        for e in state.values():
            e["oracle"]["reported"] = True
        state[self.client.as_posix()].pop("oracle")
        (self.archive / na.STATE_NAME).write_text(json.dumps(state), encoding="utf-8")
        with mock.patch("subprocess.Popen") as popen:
            self.assertEqual(self.run_main("--detach"), (0, ""))
        self.assertEqual(popen.call_count, 1)

    def test_ok_wrong_and_unscored_results_are_not_rerun(self):
        for proc in (oracle_proc(0), oracle_proc(1, wrong=2),
                     oracle_proc(2, stderr="ERROR: no map\n")):
            state_file = self.archive / na.STATE_NAME
            if state_file.exists():
                state_file.unlink()
            self.run_with(proc)
            run, _ = self.run_with(proc)
            self.assertEqual(run.call_count, 0)

    def test_a_failed_oracle_is_retried(self):
        self.run_with(subprocess.TimeoutExpired(["x"], 600))
        self.assertEqual(self.oracle()["status"], "failed")
        run, _ = self.run_with(oracle_proc(0))
        self.assertEqual(self.oracle()["status"], "ok")
        self.assertGreaterEqual(run.call_count, 1)


class OracleReportTests(OracleCase):
    def setUp(self):
        super().setUp()
        self.editor.unlink()  # one build keeps the lines easy to read

    def test_ok_is_printed_once_then_silent(self):
        self.run_with(oracle_proc(0))
        out = self.detach_out()
        self.assertEqual(out, ["native archive: matcher oracle v1.5.4 -> v1.5.5 client: "
                               "100 correct, 0 wrong, 15 ambiguous, 5 unknown"])
        self.assertTrue(self.oracle()["reported"])
        self.assertEqual(self.detach_out(), [])

    def test_unscored_is_printed_once(self):
        self.run_with(oracle_proc(2, stderr="ERROR: no engine-method map for old\n"))
        out = self.detach_out()
        self.assertEqual(out, ["native archive: matcher oracle v1.5.4 -> v1.5.5 client unscored: "
                               "no engine-method map for old"])
        self.assertEqual(self.detach_out(), [])

    def test_wrong_is_printed_every_session(self):
        self.run_with(oracle_proc(1, wrong=3))
        want = ("NOTE: native archive: matcher oracle v1.5.4 -> v1.5.5 client: 3 WRONG matches; "
                "do not carry addresses on this pair (python tools/native_build_match.py --old "
                f"{self.prev_client.as_posix()} --new {self.this_client.as_posix()} --oracle)")
        for _ in range(3):
            self.assertEqual(self.detach_out(), [want])

    def test_failed_follows_the_failed_step_note_and_retries(self):
        self.run_with(subprocess.TimeoutExpired(["x"], 600))
        with mock.patch("subprocess.Popen") as popen:
            out = self.run_main("--detach")[1].splitlines()
        self.assertEqual(popen.call_count, 1)
        self.assertEqual(len(out), 1)
        self.assertTrue(out[0].startswith("NOTE: native archive: v1.5.5 client oracle failed: "), out)
        self.assertTrue(out[0].endswith("(see the log)"))

    def test_a_held_lock_prints_nothing_about_the_oracle_and_marks_nothing(self):
        self.run_with(oracle_proc(0))
        (self.archive / na.LOCK_NAME).write_text("1 1\n", encoding="utf-8")
        self.assertEqual(self.detach_out(), [])
        self.assertFalse(self.oracle()["reported"])
        (self.archive / na.LOCK_NAME).unlink()
        self.assertEqual(len(self.detach_out()), 1)

    def test_status_shows_the_oracle(self):
        self.run_with(oracle_proc(0))
        line = self.run_main("--status")[1].strip()
        self.assertIn("oracle=ok", line)
        self.assertIn("v1.5.4", line)


if __name__ == "__main__":
    unittest.main()
