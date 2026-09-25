#!/usr/bin/env python
"""Unit tests for the release CHANGELOG generator (tools/changelog_from_commits.py).

Run:  python tools/tests/test_changelog_from_commits.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. The parse, render and insert functions are tested on fixture `git log` text;
the EndToEndTests build a throwaway git repository in a temp directory and run the script.
"""
import contextlib
import io
import os
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import changelog_from_commits as cfc  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "changelog_from_commits.py"


def record(sha, subject, body=""):
    """One commit as `git log --format=%H%x1f%s%x1f%b%x1e` prints it (git ends a body with a
    newline and separates records with one)."""
    return f"{sha}\x1f{subject}\x1f{body}\n\x1e\n"


FEAT = record("a" * 40, "feat(nazgul): v2.0.30 - the Nine scream (#645)",
              "The SCREAM is a second signature.\n\nNot-tested: in game.\n")
FIX = record("b" * 40, "fix(mission): v2.0.30 - park off-thread callback writes (#634)",
             "One paragraph.\n")
DOCS = record("c" * 40, "docs: v2.0.30 - a doc with no scope")
DIAG = record("d" * 40, "diag(boot): v2.0.30 - an uncommon type")
PLAIN = record("e" * 40, "Add unit tests for Animalia armory writer functionality",
               "Body of an IDE commit.\n")
NO_VERSION = record("f" * 40, "feat: Implement career kit generation from troop data (#629)")
HEADING_BODY = record("7" * 40, "docs(release): v2.0.30 - show the next heading",
                      "Example:\n\n```markdown\n## v2.0.32 (2026-11-01)\n  ### Fixes\n```\n")


class ParseLogTests(unittest.TestCase):
    def test_parse_log_reads_sha_subject_and_type(self):
        (c,) = cfc.parse_log(FEAT)
        self.assertEqual(c.sha, "a" * 40)
        self.assertEqual(c.subject, "feat(nazgul): v2.0.30 - the Nine scream (#645)")
        self.assertEqual(c.type, "feat")

    def test_parse_log_reads_a_subject_with_no_scope(self):
        (c,) = cfc.parse_log(DOCS)
        self.assertEqual(c.type, "docs")

    def test_parse_log_marks_subjects_without_the_label_as_unlabelled(self):
        commits = cfc.parse_log(PLAIN + NO_VERSION)
        self.assertEqual([c.type for c in commits], [None, None])
        self.assertEqual(commits[1].subject,
                         "feat: Implement career kit generation from troop data (#629)")

    def test_parse_log_keeps_a_multi_paragraph_body_verbatim(self):
        (c,) = cfc.parse_log(FEAT)
        self.assertEqual(c.body, "The SCREAM is a second signature.\n\nNot-tested: in game.")

    def test_parse_log_keeps_log_order_and_skips_empty_records(self):
        commits = cfc.parse_log(FIX + "\n" + FEAT + "\n")
        self.assertEqual([c.sha[0] for c in commits], ["b", "a"])


class RenderSectionTests(unittest.TestCase):
    def render(self, raw):
        return cfc.render_section("v2.0.31", "2026-10-01", "v2.0.30", cfc.parse_log(raw))

    def test_render_heading_carries_version_date_and_counts(self):
        text = self.render(FEAT + PLAIN)
        self.assertTrue(text.startswith(
            "## v2.0.31 (2026-10-01)\n\n"
            "Commits since v2.0.30: 2 (1 with the version label, 1 without).\n"))

    def test_render_groups_known_types_in_fixed_order_then_other_types(self):
        text = self.render(DIAG + DOCS + FIX + FEAT)
        order = [text.index(h) for h in
                 ("### Features", "### Fixes", "### Documentation", "### diag")]
        self.assertEqual(order, sorted(order))

    def test_render_lists_unlabelled_commits_in_their_own_last_group(self):
        text = self.render(PLAIN + FEAT + NO_VERSION)
        tail = text[text.index("### Commits without the version label"):]
        self.assertIn("#### Add unit tests for Animalia armory writer functionality", tail)
        self.assertIn("#### feat: Implement career kit generation from troop data (#629)", tail)
        self.assertNotIn("### Features", tail)

    def test_render_entry_is_subject_short_sha_then_body(self):
        text = self.render(FEAT)
        self.assertIn(
            "#### feat(nazgul): v2.0.30 - the Nine scream (#645)\n\n"
            "`aaaaaaaa`\n\n"
            "The SCREAM is a second signature.\n\nNot-tested: in game.\n", text)

    def test_render_keeps_log_order_inside_a_group(self):
        second_fix = record("9" * 40, "fix(ui): v2.0.30 - a later fix")
        text = self.render(second_fix + FIX)
        self.assertLess(text.index("a later fix"), text.index("park off-thread"))

    def test_render_omits_empty_groups_and_ends_with_one_newline(self):
        text = self.render(FIX)
        self.assertNotIn("### Features", text)
        self.assertNotIn("### Commits without the version label", text)
        self.assertTrue(text.endswith("One paragraph.\n"))

    def test_render_orders_other_types_alphabetically(self):
        text = self.render(DIAG + record("1" * 40, "ci: v2.0.30 - x"))
        self.assertLess(text.index("### ci"), text.index("### diag"))

    def test_render_escapes_a_body_line_that_would_be_a_heading(self):
        text = self.render(HEADING_BODY)
        body_lines = text[text.index("`77777777`"):].splitlines()[1:]
        self.assertFalse([line for line in body_lines if line.lstrip(" ").startswith("#")],
                         text)
        self.assertIn("\\## v2.0.32 (2026-11-01)", text)
        self.assertIn("  \\### Fixes", text)

    def test_render_leaves_an_issue_reference_at_a_line_start_alone(self):
        text = self.render(record("8" * 40, "fix: v2.0.30 - x", "#622: the gate was dead.\n"))
        self.assertIn("\n#622: the gate was dead.\n", text)


class LabelParityTests(unittest.TestCase):
    """LABEL_RE must accept exactly what the commit-subject gate accepts."""

    SUBJECTS = [
        ("feat(nazgul): v2.0.30 - the Nine scream", True),
        ("fix!: v2.0.30 - breaking", True),
        ("docs: v2.0.30.1 - a four-part version", True),
        ("chore(release): v2.0.31 - TAOM v2.0.31", True),
        ("Feat: v2.0.30 - capital type", False),
        ("feat: 2.0.30 - no v", False),
        ("feat: v2.0.30 -  leading space in the description", False),
        ("feat: v2.0 - two-part version", False),
        ("feat(): v2.0.30 - empty scope", False),
        ("Add a test from an IDE", False),
    ]

    def hook_pattern(self):
        hook = (Path(__file__).resolve().parents[2] / ".claude" / "hooks"
                / "check-commit-subject-version.sh").read_text(encoding="utf-8")
        m = re.search(r'^pat = re\.compile\(r"(.+)"\)$', hook, re.M)
        self.assertIsNotNone(m, "the hook's subject pattern moved; update this test")
        return re.compile(m.group(1))

    def test_label_re_matches_the_subject_gate_in_both_directions(self):
        gate = self.hook_pattern()
        for subject, expected in self.SUBJECTS:
            with self.subTest(subject=subject):
                self.assertEqual(bool(gate.match(subject)), expected)
                self.assertEqual(bool(cfc.LABEL_RE.match(subject)), expected)


class InsertSectionTests(unittest.TestCase):
    HEADER = "# CHANGELOG\n\n> Generated.\n"
    SECTION = "## v2.0.31 (2026-10-01)\n\nnew\n"

    def test_insert_places_the_section_above_the_newest_release(self):
        old = self.HEADER + "\n## v2.0.30 (2026-09-18)\n\nold\n"
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertEqual(out, self.HEADER + "\n## v2.0.31 (2026-10-01)\n\nnew\n\n"
                                            "## v2.0.30 (2026-09-18)\n\nold\n")

    def test_insert_into_a_header_only_file_appends_after_the_header(self):
        out = cfc.insert_section(self.HEADER, self.SECTION, "v2.0.31")
        self.assertEqual(out, self.HEADER + "\n" + self.SECTION)

    def test_insert_refuses_a_version_already_present(self):
        old = self.HEADER + "\n## v2.0.31 (2026-10-01)\n\nnew\n"
        with self.assertRaises(ValueError):
            cfc.insert_section(old, self.SECTION, "v2.0.31")

    def test_insert_refuses_a_hand_written_section_above_the_releases(self):
        old = self.HEADER + "\n## 2026-10-02\n\n### fix: by hand\n\n## v2.0.30 (2026-09-18)\n"
        with self.assertRaises(ValueError) as ctx:
            cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertIn("## 2026-10-02", str(ctx.exception))

    def test_insert_refuses_a_hand_written_subheading_above_the_releases(self):
        for old in (self.HEADER + "\n### fix: by hand\n",
                    self.HEADER + "\n#### fix: by hand\n\n## v2.0.30 (2026-09-18)\n"):
            with self.subTest(old=old), self.assertRaises(ValueError):
                cfc.insert_section(old, self.SECTION, "v2.0.31")

    def test_insert_after_a_release_whose_body_showed_the_next_heading(self):
        commits = cfc.parse_log(HEADING_BODY)
        first = cfc.insert_section(self.HEADER, cfc.render_section(
            "v2.0.31", "2026-10-01", "v2.0.30", commits), "v2.0.31")
        second = cfc.insert_section(first, cfc.render_section(
            "v2.0.32", "2026-11-01", "v2.0.31", commits), "v2.0.32")
        self.assertLess(second.index("## v2.0.32 (2026-11-01)\n\nCommits since"),
                        second.index("## v2.0.31 (2026-10-01)"))
        with self.assertRaises(ValueError):
            cfc.insert_section(second, self.SECTION, "v2.0.31")

    def test_insert_does_not_confuse_a_longer_version(self):
        old = self.HEADER + "\n## v2.0.310 (2027-01-01)\n\nx\n"
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertIn("## v2.0.31 (2026-10-01)", out)

    def test_insert_keeps_crlf_line_endings(self):
        old = self.HEADER.replace("\n", "\r\n")
        out = cfc.insert_section(old, self.SECTION, "v2.0.31")
        self.assertNotIn("\n", out.replace("\r\n", ""))


class EndToEndTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        self.git("init", "-q")
        self.git("config", "user.email", "test@example.invalid")
        self.git("config", "user.name", "Changelog generator test")
        self.git("config", "core.autocrlf", "false")
        self.commit("chore(release): v2.0.30 - TAOM v2.0.30")
        self.git("tag", "v2.0.30")
        self.commit("fix(combat): v2.0.30 - archers hold the wall", "They hold it now.")
        self.commit("Add a test from an IDE")
        (self.repo / "CHANGELOG.md").write_text("# CHANGELOG\n\n> Generated.\n",
                                                encoding="utf-8")

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.repo), *args], capture_output=True,
                              check=True).stdout.decode("utf-8")

    def commit(self, subject, body=""):
        message = subject + ("\n\n" + body if body else "")
        self.git("commit", "-q", "--allow-empty", "-m", message)

    def run_tool(self, *args):
        return subprocess.run([sys.executable, str(TOOL), "--repo", str(self.repo), *args],
                              capture_output=True)

    def test_main_writes_the_section_from_the_previous_tag(self):
        proc = self.run_tool("--version", "v2.0.31", "--date", "2026-10-01", "--write")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        text = (self.repo / "CHANGELOG.md").read_text(encoding="utf-8")
        self.assertIn("## v2.0.31 (2026-10-01)", text)
        self.assertIn("Commits since v2.0.30: 2 (1 with the version label, 1 without).", text)
        self.assertIn("#### fix(combat): v2.0.30 - archers hold the wall", text)
        self.assertIn("They hold it now.", text)
        self.assertIn("#### Add a test from an IDE", text)
        self.assertNotIn("TAOM v2.0.30", text)
        self.assertIn(b"2 commits, 1 with the version label, 1 without", proc.stderr)

    def test_main_refuses_a_second_write_of_the_same_version(self):
        self.assertEqual(self.run_tool("--version", "v2.0.31", "--write").returncode, 0)
        before = (self.repo / "CHANGELOG.md").read_bytes()
        proc = self.run_tool("--version", "v2.0.31", "--write")
        self.assertEqual(proc.returncode, 2)
        self.assertEqual((self.repo / "CHANGELOG.md").read_bytes(), before)

    def test_main_prints_the_section_without_write(self):
        proc = self.run_tool("--version", "v2.0.31", "--date", "2026-10-01")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertTrue(proc.stdout.decode("utf-8").startswith("## v2.0.31 (2026-10-01)\n"))
        self.assertEqual((self.repo / "CHANGELOG.md").read_text(encoding="utf-8"),
                         "# CHANGELOG\n\n> Generated.\n")

    def test_main_rejects_a_malformed_version(self):
        self.assertEqual(self.run_tool("--version", "2.0.31").returncode, 2)

    def test_main_refuses_an_empty_range(self):
        self.git("tag", "v2.0.31")
        self.assertEqual(self.run_tool("--version", "v2.0.32").returncode, 2)

    def test_main_rejects_a_version_with_a_trailing_newline(self):
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            rc = cfc.main(["--version", "v2.0.31\n", "--repo", str(self.repo), "--write"])
        self.assertEqual(rc, 2)
        self.assertIn("--version must look like", err.getvalue())
        self.assertEqual((self.repo / "CHANGELOG.md").read_text(encoding="utf-8"),
                         "# CHANGELOG\n\n> Generated.\n")

    def test_main_names_the_commit_the_range_ends_at(self):
        head = self.git("rev-parse", "HEAD").strip()
        proc = self.run_tool("--version", "v2.0.31", "--write")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn(("ending at " + head).encode("ascii"), proc.stderr)

    def test_main_refuses_a_repo_with_no_release_tag(self):
        self.git("tag", "-d", "v2.0.30")
        proc = self.run_tool("--version", "v2.0.31")
        self.assertEqual(proc.returncode, 2)
        self.assertIn(b"describe", proc.stderr)

    def test_main_reads_a_bom_and_writes_without_one_keeping_crlf(self):
        (self.repo / "CHANGELOG.md").write_bytes(b"\xef\xbb\xbf# CHANGELOG\r\n\r\n> Generated.\r\n")
        proc = self.run_tool("--version", "v2.0.31", "--date", "2026-10-01", "--write")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        data = (self.repo / "CHANGELOG.md").read_bytes()
        self.assertTrue(data.startswith(b"# CHANGELOG\r\n"), data[:20])
        self.assertNotIn(b"\n", data.replace(b"\r\n", b""))

    def test_main_prints_non_ascii_bodies_as_utf8(self):
        self.commit("fix(rhun): v2.0.30 - Rhûn notables", "Ségwën keeps the ’quote’.")
        proc = self.run_tool("--version", "v2.0.31")
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("Ségwën keeps the ’quote’.".encode("utf-8"), proc.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
