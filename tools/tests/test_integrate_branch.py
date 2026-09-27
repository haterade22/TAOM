#!/usr/bin/env python
"""Unit tests for the integration merge tool (tools/integrate_branch.py).

Run:  python tools/tests/test_integrate_branch.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. Each test builds a throwaway repository in a temp directory: a main checkout, a branch
`feat` ("theirs") and a linked integration worktree on `integrate` ("ours"), with the machine's git
config isolated (GIT_CONFIG_GLOBAL is an empty file, GIT_CONFIG_NOSYSTEM=1). Conflict markers are
built from repeated characters so this file never holds a marker line itself.
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import integrate_branch as ib  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "integrate_branch.py"
START, BASE, MID, END = "<" * 7, "|" * 7, "=" * 7, ">" * 7
LESSONS = "docs/reviews/lessons/misc.md"
LOG = "docs/reviews/REVIEW-LOG.md"
REDIRECTING_GIT_VARS = ("GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_COMMON_DIR",
                        "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES")


class UnionTests(unittest.TestCase):
    def test_ours_comes_before_theirs_and_markers_go(self):
        data = f"a\n{START} HEAD\nours\n{MID}\ntheirs\n{END} feat\nz\n".encode("utf-8")
        self.assertEqual(ib.union(data), (b"a\nours\ntheirs\nz\n", 1))

    def test_a_diff3_block_gives_ours_then_what_theirs_adds_to_the_base(self):
        data = (f"{START} HEAD\nold\nours\n{BASE} base\nold\n{MID}\nold\ntheirs\n{END} feat\n"
                .encode("utf-8"))
        self.assertEqual(ib.union(data), (b"old\nours\ntheirs\n", 1))

    def test_a_side_that_edits_a_base_line_is_not_an_append(self):
        # Keeping both sides would duplicate the shared line, once edited and once not.
        for ours, theirs in (("ours", "old\ntheirs"), ("old\nours", "theirs"), ("ours", "theirs")):
            text = f"{START} HEAD\n{ours}\n{BASE} base\nold\n{MID}\n{theirs}\n{END} feat\n"
            with self.assertRaises(ValueError, msg=text):
                ib.union(text.encode("utf-8"))

    def test_the_base_comparison_ignores_line_endings(self):
        data = (f"{START} HEAD\r\nold\r\nours\r\n{BASE} base\r\nold\n{MID}\r\nold\r\ntheirs\r\n"
                f"{END} feat\r\n").encode("utf-8")
        self.assertEqual(ib.union(data), (b"old\r\nours\r\ntheirs\r\n", 1))

    def test_line_endings_and_bom_are_kept(self):
        data = (f"\ufefftop\r\n{START} HEAD\r\nours\r\n{MID}\r\ntheirs\r\n{END} feat\r\n"
                .encode("utf-8"))
        self.assertEqual(ib.union(data), ("\ufefftop\r\nours\r\ntheirs\r\n".encode("utf-8"), 1))

    def test_malformed_conflicts_are_refused(self):
        for text in (f"{START} a\n{START} b\n",                  # nested, unterminated
                     f"{START} a\nx\n{START} b\ny\n{MID}\nz\n{END} b\n",  # nested, well closed
                     f"{START} a\nours\n{END} feat\n",            # end inside ours
                     f"{BASE} base\nold\n",                        # base outside a block
                     f"{START} a\nours\n{MID}\n{BASE} b\n{END} feat\n",  # base inside theirs
                     f"x\n{END} feat\n",                          # end without start
                     f"{START} HEAD\nours\n{MID}\ntheirs\n",      # unterminated
                     "no markers at all\n"):
            with self.assertRaises(ValueError, msg=text):
                ib.union(text.encode("utf-8"))


class AppendOnlyGlobTests(unittest.TestCase):
    def test_a_star_stays_inside_one_folder(self):
        match = ib.append_only_matcher(ib.DEFAULT_APPEND_ONLY)
        self.assertTrue(match("docs/reviews/lessons/misc.md"))
        self.assertTrue(match("docs/reviews/REVIEW-LOG.md"))
        self.assertFalse(match("docs/reviews/lessons/archive/old.md"))
        self.assertFalse(match("docs/reviews/lessons/misc.txt"))


class MergeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.tmp = Path(self.temp.name)
        empty = self.tmp / "gitconfig-empty"
        empty.write_text("", encoding="utf-8")
        self.env = {k: v for k, v in os.environ.items() if k not in REDIRECTING_GIT_VARS}
        self.env.update(GIT_CONFIG_GLOBAL=str(empty), GIT_CONFIG_NOSYSTEM="1",
                        PYTHONIOENCODING="utf-8")
        self.main = self.tmp / "main"
        self.main.mkdir()
        self.git(self.main, "init", "-q", "-b", "main")
        self.git(self.main, "config", "user.email", "test@example.invalid")
        self.git(self.main, "config", "user.name", "integrate_branch test")
        self.git(self.main, "config", "core.autocrlf", "false")
        self.message = self.tmp / "merge-message.txt"
        self.message.write_text("merge(improve): v9.9.9 - feat into integration\n\nWhy.\n",
                                encoding="utf-8")

    def git(self, cwd, *args, check=True):
        proc = subprocess.run(["git", *args], cwd=cwd, env=self.env, capture_output=True)
        if check and proc.returncode:
            raise AssertionError(proc.stderr.decode("utf-8", "replace"))
        return proc.stdout.decode("utf-8").strip()

    def write(self, root, rel, data):
        path = Path(root) / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data if isinstance(data, bytes) else data.encode("utf-8"))

    def commit(self, cwd, files, message="change"):
        for rel, data in files.items():
            self.write(cwd, rel, data)
        self.git(cwd, "add", "--", *files)
        self.git(cwd, "commit", "-q", "-m", message)

    def build(self, base, ours, theirs):
        """base on main; theirs on feat; ours on integrate in the linked worktree self.wt."""
        self.commit(self.main, base, "base")
        self.git(self.main, "switch", "-q", "-c", "feat")
        if theirs:
            self.commit(self.main, theirs, "theirs")
        self.git(self.main, "switch", "-q", "main")
        self.wt = self.tmp / "wt-integrate"
        self.git(self.main, "worktree", "add", "-q", "-b", "integrate", str(self.wt), "main")
        if ours:
            self.commit(self.wt, ours, "ours")
        self.before = self.git(self.wt, "rev-parse", "HEAD")

    def run_tool(self, *extra, worktree=None, branch="feat"):
        return subprocess.run([sys.executable, "-B", str(TOOL), "--worktree", str(worktree or self.wt),
                               "--message-file", str(self.message), *extra, branch],
                              env=self.env, capture_output=True)

    def read(self, rel, root=None):
        return (Path(root or self.wt) / rel).read_bytes()

    def merging(self, root=None):
        return self.git(root or self.wt, "rev-parse", "-q", "--verify", "MERGE_HEAD", check=False) != ""

    def assert_ready(self, proc, branch="feat"):
        """Exit 0 with the merge staged, not committed, and the commit command printed for the
        orchestrator to run through Bash; then run it as the orchestrator would."""
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)
        self.assertEqual(self.git(self.wt, "rev-parse", "HEAD"), self.before)
        self.assertEqual(self.git(self.wt, "rev-parse", "MERGE_HEAD"), self.git(self.main, "rev-parse", branch))
        self.assertEqual(self.git(self.wt, "diff", "--name-only"), "")  # nothing left unstaged
        wanted = str(self.message).replace("\\", "/")
        self.assertIn(f'ready to commit: git commit -F "{wanted}"'.encode("utf-8"), proc.stdout)
        self.git(self.wt, "commit", "-q", "-F", wanted)

    def build_appends(self):
        self.build(
            {LESSONS: "# Lessons\n\n### one\ntext one\n", LOG: "# Log\n\n## Review 1\nbody\n",
             "src.txt": "x\n"},
            {LESSONS: "# Lessons\n\n### one\ntext one\n\n### two\ntext two\n",
             LOG: "# Log\n\n## Review 1\nbody\n\n## Review 2 ours\nbody\n"},
            {LESSONS: "# Lessons\n\n### one\ntext one\n\n### three\ntext three\n",
             LOG: "# Log\n\n## Review 1\nbody\n\n## Review 2 theirs\nbody\n"})

    def test_append_only_conflicts_merge_by_union_ours_first_each_entry_whole(self):
        # Both REVIEW-LOG entries end with the same line. A default-style merge trims that common
        # line out of the conflict block, and a union would then leave the first entry without it.
        self.build_appends()
        proc = self.run_tool()
        self.assert_ready(proc)
        self.assertEqual(self.read(LESSONS),
                         b"# Lessons\n\n### one\ntext one\n\n### two\ntext two\n\n### three\ntext three\n")
        self.assertEqual(self.read(LOG),
                         b"# Log\n\n## Review 1\nbody\n\n## Review 2 ours\nbody\n\n## Review 2 theirs\nbody\n")
        parents = self.git(self.wt, "rev-list", "--parents", "-1", "HEAD").split()
        self.assertEqual(parents[1:], [self.before, self.git(self.main, "rev-parse", "feat")])
        self.assertEqual(self.git(self.wt, "log", "-1", "--format=%s"),
                         "merge(improve): v9.9.9 - feat into integration")
        self.assertEqual(self.git(self.wt, "status", "--porcelain"), "")
        self.assertIn(b"resolved by union: " + LESSONS.encode(), proc.stdout)

    def assert_hand(self, proc, *paths):
        self.assertEqual(proc.returncode, 2, proc.stdout + proc.stderr)
        for path in paths:
            self.assertIn(path.encode("utf-8"), proc.stdout)
        self.assertTrue(self.merging())
        self.assertEqual(self.git(self.wt, "rev-parse", "HEAD"), self.before)

    def test_an_edit_to_a_shared_line_goes_to_hand_resolution(self):
        self.build({LESSONS: "# Lessons\n\n### one\ntext one\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext one, ours\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext one, theirs\n"})
        self.assert_hand(self.run_tool(), f"{LESSONS}: not resolved by union")
        self.assertEqual(self.git(self.wt, "diff", "--name-only", "--diff-filter=U"), LESSONS)

    def test_ours_fixing_the_last_entry_while_both_append_goes_to_hand_resolution(self):
        # A union would keep the old line and its fix; git's diff3 block holds the fix in ours.
        self.build({LESSONS: "# Lessons\n\n### one\ntext one\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext one (fixed)\n\n### two\nt2\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext one\n\n### three\nt3\n"})
        self.assert_hand(self.run_tool(), f"{LESSONS}: not resolved by union")
        self.assertIn(START.encode(), self.read(LESSONS))

    def test_a_base_without_a_final_newline_still_unions_two_appends(self):
        self.build({LESSONS: "# Lessons\n\n### one\ntext one"},
                   {LESSONS: "# Lessons\n\n### one\ntext one\n\n### two\nt2\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext one\n\n### three\nt3\n"})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LESSONS),
                         b"# Lessons\n\n### one\ntext one\n\n### two\nt2\n\n### three\nt3\n")

    def test_crlf_and_bom_are_kept_byte_for_byte(self):
        bom = b"\xef\xbb\xbf"
        self.build({LESSONS: bom + b"# Lessons\r\n\r\n### one\r\ntext one\r\n"},
                   {LESSONS: bom + b"# Lessons\r\n\r\n### one\r\ntext one\r\n\r\n### two\r\ntext two\r\n"},
                   {LESSONS: bom + b"# Lessons\r\n\r\n### one\r\ntext one\r\n\r\n### three\r\ntext three\r\n"})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LESSONS), bom + b"# Lessons\r\n\r\n### one\r\ntext one\r\n\r\n"
                         b"### two\r\ntext two\r\n\r\n### three\r\ntext three\r\n")

    def test_a_conflict_outside_the_append_only_set_stops_with_the_merge_in_progress(self):
        self.build({LESSONS: "# Lessons\n\n### one\ntext\n", "src.txt": "x\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext\n\n### two\nt\n", "src.txt": "ours\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext\n\n### three\nt\n", "src.txt": "theirs\n"})
        proc = self.run_tool()
        self.assert_hand(proc, "src.txt")
        self.assertEqual(self.git(self.wt, "diff", "--name-only", "--diff-filter=U"), "src.txt")
        self.assertNotIn(START.encode(), self.read(LESSONS))
        wanted = str(self.message).replace("\\", "/")
        self.assertIn(f'git commit -F "{wanted}"'.encode("utf-8"), proc.stdout)

    def test_a_folder_below_the_append_only_glob_is_resolved_by_hand(self):
        nested = "docs/reviews/lessons/archive/old.md"
        self.build({nested: "# A\n"}, {nested: "# A\nours\n"}, {nested: "# A\ntheirs\n"})
        self.assert_hand(self.run_tool(), f"{nested}: conflict outside the append-only set")

    def test_a_duplicated_heading_stops_with_the_merge_in_progress(self):
        self.build({LESSONS: "# Lessons\n\n### one\ntext\n"},
                   {LESSONS: "# Lessons\n\n### same\nx\n\n### one\ntext\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext\n\n### same\ny\n"})
        self.assert_hand(self.run_tool(), "### same")

    def test_a_duplicate_the_base_already_had_is_tolerated(self):
        old = "# Lessons\n\n### same\nx\n\n### same\ny\n"
        self.build({LESSONS: old}, {"other.txt": "ours\n"}, {LESSONS: old + "\n### new\nz\n"})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LESSONS), (old + "\n### new\nz\n").encode("utf-8"))

    def test_a_seam_in_a_cleanly_merged_file_gets_a_blank_line_and_old_seams_stay(self):
        self.build({LESSONS: "# Lessons\nintro\n### zero\nz\n\n### one\ntext one\n"},
                   {"other.txt": "ours\n"},
                   {LESSONS: "# Lessons\nintro\n### zero\nz\n\n### one\ntext one\n### three\nt\n"})
        proc = self.run_tool()
        self.assert_ready(proc)
        self.assertEqual(self.read(LESSONS),
                         b"# Lessons\nintro\n### zero\nz\n\n### one\ntext one\n\n### three\nt\n")

    def test_a_review_log_seam_is_a_level_two_heading(self):
        self.build({LOG: "# Log\n\n## Review 1\nbody\n"}, {"other.txt": "ours\n"},
                   {LOG: "# Log\n\n## Review 1\nbody\n## Review 2\nbody\n### Detail\nx\n"})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LOG), b"# Log\n\n## Review 1\nbody\n\n## Review 2\nbody\n### Detail\nx\n")

    def test_a_heading_shaped_line_inside_a_code_fence_is_left_alone(self):
        entry = "\n### two\nA quoted template:\n```markdown\n### Step\n```\n"
        self.build({LESSONS: "# Lessons\n\n### one\ntext\n"}, {"other.txt": "o\n"},
                   {LESSONS: "# Lessons\n\n### one\ntext\n" + entry})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LESSONS), ("# Lessons\n\n### one\ntext\n" + entry).encode("utf-8"))

    def test_an_inserted_seam_line_takes_the_file_line_ending(self):
        self.build({LESSONS: b"# Lessons\r\n\r\n### one\r\ntext one\r\n"},
                   {"other.txt": "ours\n"},
                   {LESSONS: b"# Lessons\r\n\r\n### one\r\ntext one\r\n### three\r\nt\r\n"})
        self.assert_ready(self.run_tool())
        self.assertEqual(self.read(LESSONS),
                         b"# Lessons\r\n\r\n### one\r\ntext one\r\n\r\n### three\r\nt\r\n")

    def test_a_leftover_marker_carried_by_the_branch_stops_the_merge(self):
        self.build({"a.txt": "a\n"}, {"b.txt": "b\n"}, {"notes.md": f"x\n{START} HEAD\ny\n"})
        self.assert_hand(self.run_tool(), "notes.md: leftover conflict marker")

    def test_a_leftover_end_marker_stops_the_merge_too(self):
        self.build({"a.txt": "a\n"}, {"b.txt": "b\n"}, {"notes.md": f"x\n{END} feat\ny\n"})
        self.assert_hand(self.run_tool(), "notes.md: leftover conflict marker")

    def test_a_leftover_marker_is_reported_beside_a_path_that_needs_a_hand(self):
        self.build({"src.txt": "x\n", "notes.md": "a\n"},
                   {"src.txt": "ours\n"},
                   {"src.txt": "theirs\n", "notes.md": f"a\n{START} HEAD\nb\n"})
        self.assert_hand(self.run_tool(), "src.txt: conflict outside the append-only set",
                         "notes.md: leftover conflict marker")

    def test_a_custom_append_only_glob_is_unioned(self):
        self.build({"notes/log.md": "# Log\n"},
                   {"notes/log.md": "# Log\nours\n"},
                   {"notes/log.md": "# Log\ntheirs\n"})
        self.assert_ready(self.run_tool("--append-only", "notes/*.md"))
        self.assertEqual(self.read("notes/log.md"), b"# Log\nours\ntheirs\n")

    def test_the_main_checkout_is_refused(self):
        self.build_appends()
        head = self.git(self.main, "rev-parse", "HEAD")
        proc = self.run_tool(worktree=self.main)
        self.assertEqual(proc.returncode, 1)
        self.assertIn(b"main checkout", proc.stderr)
        self.assertFalse(self.merging(self.main))
        self.assertEqual(self.git(self.main, "rev-parse", "HEAD"), head)

    def test_a_dirty_tree_is_refused(self):
        self.build_appends()
        self.write(self.wt, "src.txt", "edited\n")
        proc = self.run_tool()
        self.assertEqual(proc.returncode, 1)
        self.assertIn(b"not clean", proc.stderr)
        self.assertIn(b"src.txt", proc.stderr)
        self.assertFalse(self.merging())
        self.assertEqual(self.read("src.txt"), b"edited\n")

    def test_a_merge_already_in_progress_is_refused(self):
        # Both sides add the same file, so the stale merge leaves a clean tree and a MERGE_HEAD.
        self.build({"a.txt": "a\n"}, {"f.txt": "same\n"}, {"f.txt": "same\n"})
        stale = self.git(self.main, "rev-parse", "feat")
        self.git(self.wt, "merge", "--no-ff", "--no-commit", "feat")
        self.assertEqual(self.git(self.wt, "status", "--porcelain"), "")
        self.git(self.main, "switch", "-q", "-c", "other")
        self.commit(self.main, {"o.txt": "other\n"}, "other work")
        self.git(self.main, "switch", "-q", "main")
        proc = self.run_tool(branch="other")
        self.assertEqual(proc.returncode, 1, proc.stdout + proc.stderr)
        self.assertIn(b"already in progress", proc.stderr)
        self.assertEqual(self.git(self.wt, "rev-parse", "MERGE_HEAD"), stale)
        self.assertEqual(self.git(self.wt, "rev-parse", "HEAD"), self.before)

    def test_a_branch_already_merged_starts_no_merge_and_is_refused(self):
        self.build({"a.txt": "a\n"}, {"b.txt": "b\n"}, None)
        proc = self.run_tool()
        self.assertEqual(proc.returncode, 1, proc.stdout + proc.stderr)
        self.assertIn(b"no merge", proc.stderr)
        self.assertNotIn(b"ready to commit", proc.stdout)

    def test_a_revision_that_looks_like_an_option_is_refused(self):
        self.build_appends()
        for extra in ((), ("--dry-run",)):
            proc = self.run_tool(*extra, "--", branch="--no-ff")
            self.assertEqual(proc.returncode, 1, proc.stdout + proc.stderr)
            self.assertIn(b"--no-ff is not a commit", proc.stderr)
            self.assertFalse(self.merging())

    def test_a_missing_or_empty_message_file_is_refused_before_any_merge(self):
        self.build_appends()
        for content in (None, "  \n"):
            if content is None:
                self.message.unlink()
            else:
                self.message.write_text(content, encoding="utf-8")
            proc = self.run_tool()
            self.assertEqual(proc.returncode, 1, proc.stdout + proc.stderr)
            self.assertIn(b"message file", proc.stderr)
            self.assertFalse(self.merging())

    def test_a_message_failing_the_public_text_check_is_refused_before_any_merge(self):
        self.build_appends()
        self.message.write_text("merge(improve): v9.9.9 - feat \u2014 into integration\n\n"
                                "Built in E:\\repos\\x.\n", encoding="utf-8")
        proc = self.run_tool()
        self.assertEqual(proc.returncode, 1, proc.stdout + proc.stderr)
        self.assertIn(b":1: em-dash", proc.stderr)
        self.assertIn(b":3: local-path", proc.stderr)
        self.assertFalse(self.merging())

    def test_an_unexpected_error_exits_three_without_a_traceback(self):
        # An append-only file that is not UTF-8 cannot be read for its seams.
        self.build({LESSONS: b"# L\n\xff\n"}, {"other.txt": "o\n"}, {LESSONS: b"# L\n\xff\n\n### two\nt\n"})
        proc = self.run_tool()
        self.assertEqual(proc.returncode, 3, proc.stdout + proc.stderr)
        self.assertIn(b"unexpected error", proc.stderr)
        self.assertNotIn(b"Traceback", proc.stderr)

    def test_a_dry_run_reports_the_conflict_set_and_touches_nothing(self):
        self.build({LESSONS: "# L\n\n### one\nt\n", "src.txt": "x\n"},
                   {LESSONS: "# L\n\n### one\nt\n\n### two\nt\n", "src.txt": "ours\n"},
                   {LESSONS: "# L\n\n### one\nt\n\n### three\nt\n", "src.txt": "theirs\n"})
        proc = self.run_tool("--dry-run")
        self.assertEqual(proc.returncode, 2, proc.stdout + proc.stderr)
        out = proc.stdout.decode("utf-8")
        self.assertRegex(out, r"union.*" + LESSONS)
        self.assertRegex(out, r"hand.*src\.txt")
        self.assertFalse(self.merging())
        self.assertEqual(self.git(self.wt, "status", "--porcelain"), "")
        self.assertEqual(self.git(self.wt, "rev-parse", "HEAD"), self.before)

    def test_a_dry_run_with_only_append_only_conflicts_exits_zero(self):
        self.build_appends()
        proc = self.run_tool("--dry-run")
        self.assertEqual(proc.returncode, 0, proc.stdout + proc.stderr)
        self.assertIn(LESSONS.encode(), proc.stdout)
        self.assertFalse(self.merging())
        self.assertEqual(self.git(self.wt, "rev-parse", "HEAD"), self.before)


class SafetyTests(unittest.TestCase):
    def test_the_tool_names_no_destructive_git_verb_and_never_commits(self):
        source = TOOL.read_text(encoding="utf-8")
        for verb in ('"reset"', '"clean"', '"stash"', '"--abort"', '"checkout"', '"restore"',
                     '"rm"', '"--force"', '"-f"', '"commit"'):
            self.assertNotIn(verb, source)


if __name__ == "__main__":
    unittest.main()
