#!/usr/bin/env python3
"""tools/fords_round_robin.py: the schedule of every pair once, what is left to play, and the per-faction table.

Run:  python -m unittest tools.tests.test_fords_round_robin
"""
import unittest
from collections import Counter

from tools import fords_round_robin as rr

FACTIONS = [f"f{i:02d}" for i in range(13)]


def result(west, east, winner=None, mt=2400.0, keep_fell=None, killed=(100, 100), lost=(100, 100), towers=(0, 0), run="r"):
    return {"run": run, "west": west, "east": east, "winner": winner, "mt": mt, "keep_fell": keep_fell,
            "killed": {"West": killed[0], "East": killed[1]}, "lost": {"West": lost[0], "East": lost[1]},
            "towers_lost": {"West": towers[0], "East": towers[1]}}


class ScheduleTests(unittest.TestCase):
    def test_every_pair_plays_once(self):
        pairs = rr.schedule(FACTIONS)
        self.assertEqual(78, len(pairs))
        self.assertEqual(78, len({frozenset(p) for p in pairs}))

    def test_with_an_odd_count_every_faction_plays_west_exactly_half_its_matches(self):
        west = Counter(w for w, _ in rr.schedule(FACTIONS))
        self.assertEqual({6}, set(west[f] for f in FACTIONS))

    def test_todo_leaves_out_a_pair_played_on_either_side(self):
        pairs = rr.schedule(["a", "b", "c"])
        done = [result(pairs[0][1], pairs[0][0])]
        self.assertEqual(pairs[1:], rr.todo(pairs, done))


class TallyTests(unittest.TestCase):
    def test_a_keep_win_and_a_time_win_count_apart(self):
        rows = rr.tally([result("a", "b", winner="West", mt=1800.0, keep_fell=1800.0),
                         result("c", "a", winner="East", mt=2400.0)])
        a = rows["a"]
        self.assertEqual((2, 2, 0, 0), (a["played"], a["wins"], a["losses"], a["draws"]))
        self.assertEqual((1, 1), (a["keep_wins"], a["time_wins"]))
        self.assertEqual(1, rows["b"]["losses"])

    def test_no_winner_is_a_draw(self):
        rows = rr.tally([result("a", "b")])
        self.assertEqual(1, rows["a"]["draws"])
        self.assertEqual(1, rows["b"]["draws"])

    def test_trade_and_towers_are_read_from_the_factions_own_side(self):
        rows = rr.tally([result("a", "b", killed=(300, 50), lost=(100, 200), towers=(1, 4))])
        self.assertAlmostEqual(3.0, rows["a"]["trade"])
        self.assertAlmostEqual(0.25, rows["b"]["trade"])
        self.assertEqual((4, 1), (rows["a"]["towers_taken"], rows["a"]["towers_lost"]))

    def test_minutes_average_over_the_factions_matches(self):
        rows = rr.tally([result("a", "b", mt=1800.0), result("a", "c", mt=2400.0)])
        self.assertAlmostEqual(35.0, rows["a"]["minutes"])

    def test_the_table_sorts_by_win_share_and_names_every_column(self):
        text = rr.table(rr.tally([result("a", "b", winner="West", keep_fell=100.0)]))
        lines = text.splitlines()
        self.assertIn("| Faction | Played | Won | Lost | Draw | Won by keep | Won on time | Gold trade | Towers taken | Towers lost | Minutes |", lines[0])
        self.assertTrue(lines[2].startswith("| a |"))


if __name__ == "__main__":
    unittest.main()
