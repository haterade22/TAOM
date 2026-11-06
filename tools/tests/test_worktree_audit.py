#!/usr/bin/env python3
"""Unit tests for the read-only worktree audit (tools/worktree_audit.py).

Run:  python -m unittest discover -s tools/tests -p "test_worktree_audit.py"

Each test builds a throwaway git repo in a temp dir (local user config, base branch
bannerlord-1.5.x) and adds worktrees inside it. No network, no TAOM repo access.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import worktree_audit as wa  # noqa: E402


def git(cwd, *args):
    subprocess.run(["git", "-C", str(cwd), *args], check=True, capture_output=True, text=True)


class WorktreeAuditTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = Path(self.tmp.name)
        self.repo = root / "main"
        self.repo.mkdir()
        git(self.repo, "init", "-b", "bannerlord-1.5.x")
        git(self.repo, "config", "user.name", "Test")
        git(self.repo, "config", "user.email", "t@example.com")
        git(self.repo, "config", "commit.gpgsign", "false")
        (self.repo / "a.txt").write_text("one\n")
        git(self.repo, "add", "a.txt")
        git(self.repo, "commit", "-m", "base")
        self.root = root

    def tearDown(self):
        # git marks worktree files read-only on some platforms; ignore cleanup errors
        try:
            self.tmp.cleanup()
        except OSError:
            pass

    def add_wt(self, name):
        path = self.root / name
        git(self.repo, "worktree", "add", "-b", name, str(path))
        return path

    def buckets(self):
        rows = wa.audit(str(self.repo), "bannerlord-1.5.x")
        return {Path(r["path"]).name: r["bucket"] for r in rows}

    def test_buckets(self):
        self.add_wt("merged-clean")
        ahead = self.add_wt("ahead")
        (ahead / "b.txt").write_text("b\n")
        git(ahead, "add", "b.txt")
        git(ahead, "commit", "-m", "extra")
        wip = self.add_wt("wip")
        (wip / "a.txt").write_text("changed\n")
        untracked = self.add_wt("untracked")
        (untracked / "new.txt").write_text("x\n")
        b = self.buckets()
        self.assertEqual(b["main"], "main")
        self.assertEqual(b["merged-clean"], "safe")
        self.assertEqual(b["ahead"], "hold-unmerged")
        self.assertEqual(b["wip"], "hold-wip")
        self.assertEqual(b["untracked"], "review")

    def rows(self):
        return {Path(r["path"]).name: r for r in wa.audit(str(self.repo), "bannerlord-1.5.x")}

    def test_a_worktree_nested_in_another_makes_the_parent_review(self):
        parent = self.add_wt("parent")
        child = parent / ".claude" / "worktrees" / "child"
        git(self.repo, "worktree", "add", "-b", "child", str(child))
        rows = self.rows()
        self.assertEqual(rows["parent"]["bucket"], "review")
        self.assertIn("child", rows["parent"]["note"])
        self.assertEqual(rows["child"]["bucket"], "safe")
        self.assertEqual(rows["child"]["note"], "")
        self.assertEqual(rows["main"]["bucket"], "main")

    def test_main_is_never_demoted_by_a_nested_worktree(self):
        nested = self.repo / ".claude" / "worktrees" / "inner"
        git(self.repo, "worktree", "add", "-b", "inner", str(nested))
        self.assertEqual(self.rows()["main"]["bucket"], "main")

    def test_a_sibling_with_a_common_name_prefix_is_not_nested(self):
        self.add_wt("wt")
        self.add_wt("wt-2")
        b = self.buckets()
        self.assertEqual((b["wt"], b["wt-2"]), ("safe", "safe"))

    def test_status_does_not_refresh_the_worktree_index(self):
        wt = self.add_wt("stat")
        idx = self.repo / ".git" / "worktrees" / "stat" / "index"
        before = idx.read_bytes()
        os.utime(wt / "a.txt", (1000000000, 1000000000))  # stat change, same content
        self.buckets()
        self.assertEqual(idx.read_bytes(), before)

    def test_untracked_files_show_even_when_the_config_hides_them(self):
        wt = self.add_wt("hidden")
        git(self.repo, "config", "status.showUntrackedFiles", "no")
        (wt / "notes.txt").write_text("work\n")
        self.assertEqual(self.buckets()["hidden"], "review")

    def test_a_failed_status_is_review_not_clean(self):
        wt = self.add_wt("broken")
        (wt / "a.txt").write_text("CHANGED\n")
        (self.repo / ".git" / "worktrees" / "broken" / "index").write_text("garbage")
        row = self.rows()["broken"]
        self.assertEqual(row["bucket"], "review")
        self.assertIn("status failed", row["note"])

    def test_a_missing_worktree_path_is_review(self):
        wt = self.add_wt("gone")
        shutil.rmtree(wt, ignore_errors=True)
        row = self.rows()["gone"]
        self.assertFalse(row["exists"])
        self.assertEqual(row["bucket"], "review")

    def test_a_locked_worktree_is_review(self):
        self.add_wt("held")
        git(self.repo, "worktree", "lock", str(self.root / "held"))
        self.assertEqual(self.buckets()["held"], "review")

    def test_text_output_shows_the_note(self):
        parent = self.add_wt("parent")
        git(self.repo, "worktree", "add", "-b", "child", str(parent / "sub" / "child"))
        self.assertIn("contains worktree", wa.format_table(wa.audit(str(self.repo), "bannerlord-1.5.x")))

    def test_parse_detached_and_prunable(self):
        text = (
            "worktree /r/main\nHEAD aaaaaaaaaaaaaaaa\nbranch refs/heads/bannerlord-1.5.x\n\n"
            "worktree /r/det\nHEAD bbbbbbbbbbbbbbbb\ndetached\n\n"
            "worktree /r/gone\nHEAD cccccccccccccccc\nbranch refs/heads/gone\n"
            "locked why\nprunable gitdir file points to non-existent location\n"
        )
        blocks = wa.parse_porcelain(text)
        self.assertEqual(len(blocks), 3)
        self.assertEqual(blocks[0]["branch"], "bannerlord-1.5.x")
        self.assertEqual(blocks[1]["branch"], "(detached)")
        self.assertTrue(blocks[2]["prunable"])
        self.assertTrue(blocks[2]["locked"])

    def test_missing_base_exits_2(self):
        r = subprocess.run(
            [sys.executable, wa.__file__, "--repo", str(self.repo), "--base", "no-such-branch"],
            capture_output=True, text=True)
        self.assertEqual(r.returncode, 2)
        self.assertTrue(r.stderr.strip())

    def test_not_a_repo_exits_2(self):
        r = subprocess.run([sys.executable, wa.__file__, "--repo", str(self.root)],
                           capture_output=True, text=True)
        self.assertEqual(r.returncode, 2)


if __name__ == "__main__":
    unittest.main()
