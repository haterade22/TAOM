#!/usr/bin/env python3
"""tools/memory_audit.py: index budget, broken links, orphans and prune candidates.

Run:  python -m unittest tools.tests.test_memory_audit

git and gh are replaced with fakes; nothing here reads the real memory folder.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import memory_audit as m  # noqa: E402

INDEX = """# TAOM Project Memory

## In-Flight (owed checks)
- [done-card](features/done-card.md) - all pushed at abc1234; #1200 closed
- [open-card](features/open-card.md) - #1300 still open
- [local-card](features/local-card.md) - committed d0d95bf27, not pushed
- [missing](features/missing.md) - gone

## Machine-local
- [pref](feedback_pref.md) - a lasting preference
"""


def write(root, rel, text):
    p = Path(root, rel)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")


class Fixture(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)
        write(self.dir, "MEMORY.md", INDEX)
        write(self.dir, "features/done-card.md", "---\nname: done-card\n---\nMerged abc1234 to trunk.\n")
        write(self.dir, "features/open-card.md", "---\nname: open-card\n---\nPushed 1a2b3c4d. #1300 open.\n")
        write(self.dir, "features/local-card.md", "---\nname: local-card\n---\nd0d95bf27 waits.\n")
        write(self.dir, "feedback_pref.md", "---\nname: pref\n---\nSee [[linked-only]].\n")
        write(self.dir, "topics/linked-only.md", "---\nname: linked-only\n---\nReached by a wiki link.\n")
        write(self.dir, "features/stray.md", "---\nname: stray\n---\nNothing links here.\n")

    def tearDown(self):
        self.tmp.cleanup()


class ParseIndexTests(unittest.TestCase):
    def test_entries_carry_their_section(self):
        entries = m.parse_index(INDEX)
        self.assertEqual([e.name for e in entries], ["done-card", "open-card", "local-card", "missing", "pref"])
        self.assertTrue(entries[0].section.startswith("In-Flight"))
        self.assertEqual(entries[4].section, "Machine-local")

    def test_budget_counts_lines_and_bytes(self):
        lines, size, over = m.index_budget("a\n" * 41)
        self.assertEqual((lines, size, over), (41, 82, True))
        self.assertFalse(m.index_budget("a\n" * 40)[2])
        self.assertTrue(m.index_budget("x" * 4097)[2])


class RefTests(unittest.TestCase):
    def test_commits_need_a_digit_and_a_hex_letter(self):
        commits, _ = m.extract_refs("pushed abc1234 and d0d95bf27; not deadbeef, not 1234567, not abc12")
        self.assertEqual(commits, {"abc1234", "d0d95bf27"})

    def test_session_ids_are_not_commits(self):
        card = ("---\nname: x\nmetadata:\n  originSessionId: aa54ce84-9d20-4e3b-8f1a-a2c71df032d8\n---\n"
                "Pushed abc1234. Session 92c6f55e-0a3f-41f4-b7e0-b6daa24359d7 wrote it.\n")
        commits, _ = m.extract_refs(m.card_body(card))
        self.assertEqual(commits, {"abc1234"})

    def test_issue_numbers_skip_paths_and_anchors(self):
        _, issues = m.extract_refs("#1200 closed, PR #1341, see doc.md#1999 and #12")
        self.assertEqual(issues, {1200, 1341})


class LinkTests(Fixture):
    def test_broken_links_name_the_missing_file(self):
        entries = m.parse_index(INDEX)
        self.assertEqual(m.broken_links(self.dir, entries), ["features/missing.md"])

    def test_orphans_ignore_index_and_wiki_links(self):
        self.assertEqual(m.orphans(self.dir), ["features/stray.md"])


class VerdictTests(Fixture):
    def setUp(self):
        super().setUp()
        self.commit_state = {"abc1234": "trunk", "1a2b3c4d": "trunk", "d0d95bf27": "local"}.get
        self.issue_state = {1200: "closed", 1300: "open"}.get

    def verdicts(self):
        entries = [e for e in m.parse_index(INDEX) if e.name != "missing"]
        return {v.name: v for v in m.card_verdicts(self.dir, entries, self.commit_state, self.issue_state)}

    def test_pushed_and_closed_card_is_a_prune_candidate(self):
        self.assertEqual(self.verdicts()["done-card"].verdict, "prune")

    def test_open_issue_keeps_the_card(self):
        v = self.verdicts()["open-card"]
        self.assertEqual(v.verdict, "keep")
        self.assertIn("#1300 open", v.reasons)

    def test_local_commit_and_unpushed_words_keep_the_card(self):
        v = self.verdicts()["local-card"]
        self.assertEqual(v.verdict, "keep")
        self.assertIn("d0d95bf27 local", v.reasons)
        self.assertIn("says not pushed", v.reasons)

    def test_machine_local_cards_are_not_judged(self):
        self.assertNotIn("pref", self.verdicts())

    def test_unknown_issue_state_keeps_the_card(self):
        self.issue_state = {}.get
        v = self.verdicts()["done-card"]
        self.assertEqual(v.verdict, "keep")
        self.assertIn("#1200 unknown", v.reasons)


class FindDirTests(unittest.TestCase):
    def test_finds_the_folder_named_after_the_main_checkout(self):
        with tempfile.TemporaryDirectory() as home:
            write(home, ".claude/projects/e--repos-TAOM/memory/MEMORY.md", "x")
            write(home, ".claude/projects/e--repos-Other/memory/MEMORY.md", "x")
            found = m.find_memory_dir(Path(home), "TAOM")
            self.assertEqual(found, Path(home, ".claude/projects/e--repos-TAOM/memory"))
            self.assertIsNone(m.find_memory_dir(Path(home), "Nope"))


if __name__ == "__main__":
    unittest.main()
