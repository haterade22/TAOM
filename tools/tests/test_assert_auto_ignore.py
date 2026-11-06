#!/usr/bin/env python3
"""tools/assert_auto_ignore.py: the button choice and one poll pass, with the machine patched out.

Run:  python -m unittest tools.tests.test_assert_auto_ignore

The real click needs a live engine dialog and is not run here.
"""
import os
import sys
import unittest
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import assert_auto_ignore as a  # noqa: E402

ASSERT_BUTTONS = {"&Abort": 3, "&Retry": 4, "&Ignore": 5}
ASSERT_TEXT = ["&Abort", "&Retry", "&Ignore", "Assertion Failed!"]


class ChooseButtonTests(unittest.TestCase):
    def test_each_title_chooses_its_position(self):
        buttons = {"First": 11, "Second": 12, "Third": 13}
        expected = {
            "Always Ignore?": 11, "RGL CONTENT WARNING": 11, "RGL CONTENT ERROR": 11, "RGL WARNING": 11,
            "Safe Mode": 12, "*_*": 12,
            "ASSERT": 13, "SAFE ASSERT": 13,
        }
        self.assertEqual(set(expected), set(a.BUTTON_POSITION))
        for title, control in expected.items():
            self.assertEqual(a.choose_button(title, buttons), control, title)

    def test_assert_chooses_ignore_with_and_without_the_mnemonic(self):
        self.assertEqual(a.choose_button("ASSERT", ASSERT_BUTTONS), 5)
        self.assertEqual(a.choose_button("ASSERT", {"Abort": 3, "Retry": 4, "Ignore": 5}), 5)

    def test_an_unknown_title_chooses_nothing(self):
        self.assertIsNone(a.choose_button("Mod change detected", {"OK": 1}))

    def test_too_few_buttons_chooses_nothing(self):
        self.assertIsNone(a.choose_button("ASSERT", {"OK": 1}))

    def test_a_position_on_abort_chooses_nothing(self):
        self.assertIsNone(a.choose_button("RGL WARNING", {"&Abort": 3, "&Retry": 4, "&Ignore": 5}))
        self.assertIsNone(a.choose_button("ASSERT", {"&Ignore": 5, "&Retry": 4, " abort ": 3}))


class AnswerDialogsTests(unittest.TestCase):
    def pass_with(self, dialogs, seen, clicked=0, accepted=True):
        lines = []
        with mock.patch.object(a, "game_dialogs", side_effect=lambda pid: dialogs.get(pid, [])), \
                mock.patch.object(a, "click_button", return_value=accepted) as click:
            total = a.answer_dialogs(sorted(dialogs) or [1], seen, clicked, lines.append)
        return total, lines, click

    def test_posts_on_every_poll_and_logs_a_window_once(self):
        dialogs = {7: [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]}
        seen = set()
        total, lines, click = self.pass_with(dialogs, seen)
        self.assertEqual(total, 1)
        self.assertEqual(len(lines), 1)
        self.assertIn("Assertion Failed!", lines[0])
        click.assert_called_once_with(100, 5)
        total, lines, click = self.pass_with(dialogs, seen, total)
        self.assertEqual(total, 1)
        self.assertEqual(lines, [])
        click.assert_called_once_with(100, 5)

    def test_a_closed_window_leaves_seen_so_a_reused_handle_counts_again(self):
        seen = set()
        total, _, _ = self.pass_with({7: [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]}, seen)
        total, _, _ = self.pass_with({7: []}, seen, total)
        self.assertEqual(seen, set())
        total, lines, _ = self.pass_with({7: [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]}, seen, total)
        self.assertEqual(total, 2)
        self.assertEqual(len(lines), 1)

    def test_an_unknown_dialog_is_logged_once_and_never_clicked(self):
        dialogs = {7: [(100, "Mod change detected", ["OK", "A change"], {"OK": 1})]}
        seen = set()
        total, lines, click = self.pass_with(dialogs, seen)
        self.assertEqual(total, 0)
        self.assertIn("left open", lines[0])
        self.assertIn("A change", lines[0])
        click.assert_not_called()
        _, lines, _ = self.pass_with(dialogs, seen)
        self.assertEqual(lines, [])

    def test_a_refused_click_is_not_counted_and_is_reported(self):
        total, lines, _ = self.pass_with({7: [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]}, set(), accepted=False)
        self.assertEqual(total, 0)
        self.assertIn("refused", lines[0])

    def test_a_refused_click_counts_once_when_a_later_post_is_accepted(self):
        dialogs = {7: [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]}
        seen = set()
        total, _, _ = self.pass_with(dialogs, seen, accepted=False)
        total, lines, _ = self.pass_with(dialogs, seen, total)
        self.assertEqual(total, 1)
        self.assertEqual(len(lines), 1)
        total, lines, _ = self.pass_with(dialogs, seen, total)
        self.assertEqual(total, 1)
        self.assertEqual(lines, [])

    def test_a_failed_dialog_read_is_printed_and_the_next_process_still_runs(self):
        lines = []

        def dialogs_of(pid):
            if pid == 1:
                raise OSError("denied")
            return [(100, "ASSERT", ASSERT_TEXT, ASSERT_BUTTONS)]

        with mock.patch.object(a, "game_dialogs", side_effect=dialogs_of), \
                mock.patch.object(a, "click_button", return_value=True) as click:
            total = a.answer_dialogs([1, 2], set(), 0, lines.append)
        self.assertEqual(total, 1)
        self.assertTrue(any("denied" in line for line in lines))
        click.assert_called_once_with(100, 5)


class SystemHostTests(unittest.TestCase):
    QUESTION = (200, "*_*", ["&Yes", "&No", "The application faced a problem."], {"&Yes": 6, "&No": 7})

    def run_pass(self, dialogs, hosts):
        lines = []
        with mock.patch.object(a, "game_dialogs", side_effect=lambda pid: dialogs.get(pid, [])),                 mock.patch.object(a, "click_button", return_value=True) as click:
            total = a.answer_dialogs([7], set(), 0, lines.append, hosts)
        return total, lines, click

    def test_the_crash_question_shown_by_csrss_gets_no(self):
        total, lines, click = self.run_pass({7: [], 50: [self.QUESTION]}, [50])
        self.assertEqual(total, 1)
        click.assert_called_once_with(200, 7)
        self.assertIn("faced a problem", lines[0])

    def test_other_system_boxes_are_neither_clicked_nor_logged(self):
        other = (201, "ASSERT", ["&Abort", "&Retry", "&Ignore", "x"], {"&Abort": 3, "&Retry": 4, "&Ignore": 5})
        total, lines, click = self.run_pass({7: [], 50: [other]}, [50])
        self.assertEqual(total, 0)
        self.assertEqual(lines, [])
        click.assert_not_called()

    def test_host_pids_lists_csrss_and_an_error_gives_none(self):
        with mock.patch.object(a, "list_processes", return_value=[{"pid": 50, "name": "csrss.exe"}]) as listing:
            self.assertEqual(a.host_pids(lambda _: None), {50})
        listing.assert_called_once_with(["csrss.exe"])
        lines = []
        with mock.patch.object(a, "list_processes", side_effect=OSError("denied")):
            self.assertEqual(a.host_pids(lines.append), set())
        self.assertIn("denied", lines[0])


class GamePidsTests(unittest.TestCase):
    def pids_from(self, previous, listing):
        lines = []
        with mock.patch.object(a, "list_processes", **listing):
            return a.game_pids(previous, lines.append), lines

    def test_names_only_the_game_processes(self):
        with mock.patch.object(a, "list_processes", return_value=[]) as listing:
            a.game_pids(set(), lambda _: None)
        listing.assert_called_once_with(["TaleWorlds.MountAndBlade.Launcher.exe", "Bannerlord.exe", "Watchdog.exe"])

    def test_watches_the_game_watchdog_that_shows_the_crash_question(self):
        path = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_wEditor\Watchdog\Watchdog.exe"
        pids, _ = self.pids_from(set(), {"return_value": [{"pid": 9, "name": "Watchdog.exe", "path": path}]})
        self.assertEqual(pids, {9})

    def test_skips_a_watchdog_of_another_program(self):
        for path in (r"C:\Program Files\Other\Watchdog.exe", None):
            pids, lines = self.pids_from(set(), {"return_value": [{"pid": 9, "name": "Watchdog.exe", "path": path}]})
            self.assertEqual(pids, set(), path)
            self.assertEqual(lines, [])

    def test_crash_question_chooses_no(self):
        self.assertEqual(a.choose_button("*_*", {"&Yes": 6, "&No": 7}), 7)

    def test_reports_a_found_and_a_lost_process_once(self):
        pids, lines = self.pids_from(set(), {"return_value": [{"pid": 7, "name": "Bannerlord.exe"}]})
        self.assertEqual(pids, {7})
        self.assertEqual(len(lines), 1)
        pids, lines = self.pids_from({7}, {"return_value": [{"pid": 7, "name": "Bannerlord.exe"}]})
        self.assertEqual(lines, [])
        pids, lines = self.pids_from({7}, {"return_value": []})
        self.assertEqual(pids, set())
        self.assertEqual(len(lines), 1)

    def test_a_failed_listing_keeps_the_known_pids_and_prints_the_error(self):
        pids, lines = self.pids_from({7}, {"side_effect": OSError("no powershell")})
        self.assertEqual(pids, {7})
        self.assertIn("no powershell", lines[0])


if __name__ == "__main__":
    unittest.main()
