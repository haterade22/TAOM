#!/usr/bin/env python3
"""Unit tests for tools/loop_progress.py (the no-progress breaker for work-driving loops).

Run:  python -B -m unittest tools.tests.test_loop_progress -v

Each test builds a throwaway git repo in a tempdir and calls main() the way a loop would.
"""
import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import loop_progress as lp  # noqa: E402


def git(repo, *args):
    subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True)


class LoopProgressTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        root = Path(self._tmp.name)
        self.repo = root / "repo"
        self.repo.mkdir()
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.name", "t")
        git(self.repo, "config", "user.email", "t@example.com")
        git(self.repo, "config", "commit.gpgsign", "false")
        (self.repo / "a.txt").write_text("one\n")
        git(self.repo, "add", "a.txt")
        git(self.repo, "commit", "-q", "-m", "init")
        self.state = root / "state" / "loop.json"
        self.state.parent.mkdir()
        self.watch_root = root / "watch"
        self.watch_root.mkdir()

    def tick(self, *extra, limit=3):
        argv = ["--state", str(self.state), "--repo", str(self.repo), "--limit", str(limit), *extra]
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = lp.main(argv)
        self.last_out, self.last_err = out.getvalue(), err.getvalue()
        return code

    def load(self):
        return json.loads(self.state.read_text())

    def test_first_tick_writes_state(self):
        self.assertEqual(self.tick(), 0)
        data = self.load()
        self.assertEqual(data["no_progress"], 0)
        self.assertEqual(data["ticks"], 1)
        self.assertEqual(set(data["parts"]), {"head", "status", "diff", "untracked", "watch"})
        self.assertIn("first tick", self.last_out)

    def test_unchanged_ticks_trip_breaker(self):
        codes = [self.tick() for _ in range(5)]
        self.assertEqual(codes, [0, 0, 0, 3, 3])
        self.assertEqual(self.load()["no_progress"], 4)

    def test_output_format(self):
        self.tick()
        self.tick()
        self.assertEqual(
            self.last_out.strip(),
            "loop_progress: tick 2, no progress 1 of 3 (changed: none)",
        )

    def test_new_commit_resets(self):
        self.tick()
        self.tick()
        (self.repo / "b.txt").write_text("b\n")
        git(self.repo, "add", "b.txt")
        git(self.repo, "commit", "-q", "-m", "second")
        self.assertEqual(self.tick(), 0)
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("head", self.last_out)

    def test_tracked_edit_resets(self):
        self.tick()
        self.tick()
        (self.repo / "a.txt").write_text("one\ntwo\n")
        self.tick()
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("diff", self.last_out)

    def test_second_edit_to_modified_file_resets(self):
        (self.repo / "a.txt").write_text("one\ntwo\n")
        self.tick()
        self.tick()
        self.assertEqual(self.load()["no_progress"], 1)
        (self.repo / "a.txt").write_text("one\nthree!\n")
        self.tick()
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("diff", self.last_out)

    def test_untracked_content_change_resets(self):
        u = self.repo / "u.txt"
        u.write_text("x")
        self.tick()
        self.tick()
        self.assertEqual(self.load()["no_progress"], 1)
        u.write_text("xyz")
        self.tick()
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("untracked", self.last_out)

    def test_untracked_mtime_change_resets(self):
        u = self.repo / "u.txt"
        u.write_text("x")
        os.utime(u, ns=(1_000_000_000_000_000_000, 1_000_000_000_000_000_000))
        self.tick()
        os.utime(u, ns=(1_100_000_000_000_000_000, 1_100_000_000_000_000_000))
        self.tick()
        self.assertEqual(self.load()["no_progress"], 0)

    def test_watch_file_created_and_touched_reset(self):
        w = self.watch_root / "w.xml"
        self.tick("--watch-path", str(w))
        self.tick("--watch-path", str(w))
        self.assertEqual(self.load()["no_progress"], 1)
        w.write_text("hi")
        os.utime(w, ns=(1_000_000_000_000_000_000, 1_000_000_000_000_000_000))
        self.tick("--watch-path", str(w))
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("watch", self.last_out)
        os.utime(w, ns=(1_200_000_000_000_000_000, 1_200_000_000_000_000_000))
        self.tick("--watch-path", str(w))
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("watch", self.last_out)
        self.tick("--watch-path", str(w))
        self.assertEqual(self.load()["no_progress"], 1)

    def test_watch_directory_change_resets(self):
        d = self.watch_root / "dir"
        (d / "sub").mkdir(parents=True)
        (d / "sub" / "f.txt").write_text("1")
        self.tick("--watch-path", str(d))
        (d / "sub" / "g.txt").write_text("2")
        self.tick("--watch-path", str(d))
        self.assertEqual(self.load()["no_progress"], 0)
        self.assertIn("watch", self.last_out)

    def test_corrupt_state_starts_over(self):
        self.state.write_text("{not json")
        self.assertEqual(self.tick(), 0)
        self.assertIn("first tick", self.last_out)
        self.assertTrue(self.last_err.strip())
        self.assertEqual(self.load()["ticks"], 1)

    def test_not_a_repo_returns_1_without_state(self):
        plain = Path(self._tmp.name) / "plain"
        plain.mkdir()
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = lp.main(["--state", str(self.state), "--repo", str(plain)])
        self.assertEqual(code, 1)
        self.assertFalse(self.state.exists())

    def test_empty_repo_without_commit_is_a_value(self):
        empty = Path(self._tmp.name) / "empty"
        empty.mkdir()
        git(empty, "init", "-q")
        argv = ["--state", str(self.state), "--repo", str(empty)]
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(lp.main(argv), 0)
            self.assertEqual(lp.main(argv), 0)
        self.assertEqual(self.load()["no_progress"], 1)

    def test_state_is_required(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as cm:
            lp.main([])
        self.assertEqual(cm.exception.code, 2)


if __name__ == "__main__":
    unittest.main()
