#!/usr/bin/env python
"""Unit tests for the release CHANGELOG generator (tools/changelog_from_commits.py).

Run:  python tools/tests/test_changelog_from_commits.py
  or:  python -m unittest discover -s tools/tests -t .

Pure stdlib. The parse, render and insert functions are tested on fixture `git log` text;
the EndToEndTests build a throwaway git repository in a temp directory and run the script.
"""
import os
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


class ParseLogTests(unittest.TestCase):
    def test_parse_log_reads_type_scope_version_and_description(self):
        (c,) = cfc.parse_log(FEAT)
        self.assertEqual(c.sha, "a" * 40)
        self.assertEqual(c.type, "feat")
        self.assertEqual(c.scope, "nazgul")
        self.assertEqual(c.version, "v2.0.30")
        self.assertEqual(c.description, "the Nine scream (#645)")

    def test_parse_log_reads_a_subject_with_no_scope(self):
        (c,) = cfc.parse_log(DOCS)
        self.assertEqual((c.type, c.scope, c.version), ("docs", None, "v2.0.30"))

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


if __name__ == "__main__":
    unittest.main(verbosity=2)
