#!/usr/bin/env python3
"""tools/fords_round_robin.py: the schedule of every pair once, what is left to play, and the per-faction table.

Run:  python -m unittest tools.tests.test_fords_round_robin
"""
import contextlib
import io
import json
import shutil
import subprocess
import tempfile
import unittest
from collections import Counter
from pathlib import Path

from tools import fords_round_robin as rr

FACTIONS = [f"f{i:02d}" for i in range(13)]
SHA = "92973f27a1b2c3d4e5f60718293a4b5c6d7e8f90"
LOG = ("[2026-11-03 11:47:00] [INFO] [FordsOfWar][Summary] run=1 w=2431.6 mt=2431.6 phase=Live liveS=1 liveW=80 liveE=170 backlog=0 deathsW=1 deathsE=2 paidW=1 towersW=2 towersE=0 blows=1\n"
       "[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] troop=t1 kills=1 deaths=1 valueKilled=500 valueLost=100\n")


def result(west, east, winner="West", mt=2400.0, time_win=True, killed=(100, 100), lost=(100, 100), towers=(0, 0), run="r"):
    return {"run": run, "west": west, "east": east, "winner": winner, "mt": mt, "time_win": time_win,
            "values": {"West": {"killed": killed[0], "lost": lost[0]}, "East": {"killed": killed[1], "lost": lost[1]}},
            "towers_lost": {"West": towers[0], "East": towers[1]}}


def write_run(root, name, west="a", east="b", taom=f"build.20261105T010203Z+{SHA}", winner="West", final=True, winner_mt=None, report_text=None, on_time=None,
              west_ai=True, speed=4, control_check=None):
    folder = Path(root) / name
    folder.mkdir()
    (folder / "taom_debug_1.log").write_text(LOG, encoding="utf-8")
    names = f"west={west}\neast={east}\n" if west is not None else ""
    arm = names + ("west_ai=1\n" if west_ai else "") + (f"speed={speed}\n" if speed else "") + (f"control_check={control_check}\n" if control_check else "")
    (folder / "arm.txt").write_text(arm, encoding="utf-8")
    end = {"Winner": winner, "KeepFellMt": None, "WinnerMt": winner_mt}
    if on_time is not None:
        end["EndedOnTime"] = on_time
    report = {"taom": taom, "final": final, "evidence": {"End": end, "Spawn": {}}}
    (folder / "fords_report_1.json").write_text(report_text if report_text is not None else json.dumps(report), encoding="utf-8")
    return folder


def run_results(root, build="92973f27a", pairs=(("a", "b"),)):
    err = io.StringIO()
    with contextlib.redirect_stderr(err):
        return rr.results_for(build, pairs, root), err.getvalue()


class ScheduleTests(unittest.TestCase):
    def test_every_pair_plays_once(self):
        pairs = rr.schedule(FACTIONS)
        self.assertEqual(78, len(pairs))
        self.assertEqual(78, len({frozenset(p) for p in pairs}))

    def test_with_an_odd_count_every_faction_plays_west_exactly_half_its_matches(self):
        west = Counter(w for w, _ in rr.schedule(FACTIONS))
        self.assertEqual({6}, set(west[f] for f in FACTIONS))

    def test_todo_leaves_out_a_pair_played_in_its_scheduled_order_only(self):
        pairs = rr.schedule(["a", "b", "c"])
        self.assertEqual(pairs[1:], rr.todo(pairs, [result(*pairs[0])]))
        self.assertEqual(pairs, rr.todo(pairs, [result(pairs[0][1], pairs[0][0])]))


class TallyTests(unittest.TestCase):
    def test_a_keep_win_and_a_time_win_count_apart(self):
        rows = rr.tally([result("a", "b", winner="West", mt=1800.0, time_win=False),
                         result("c", "a", winner="East", mt=2400.0)])
        a = rows["a"]
        self.assertEqual((2, 2, 0), (a["played"], a["wins"], a["losses"]))
        self.assertEqual((1, 1), (a["keep_wins"], a["time_wins"]))
        self.assertEqual(1, rows["b"]["losses"])

    def test_the_table_has_no_draw_column(self):
        self.assertNotIn("Draw", rr.COLUMNS)

    def test_trade_and_towers_are_read_from_the_factions_own_side(self):
        rows = rr.tally([result("a", "b", killed=(300, 50), lost=(100, 200), towers=(1, 4))])
        self.assertAlmostEqual(3.0, rows["a"]["trade"])
        self.assertAlmostEqual(0.25, rows["b"]["trade"])
        self.assertEqual((4, 1), (rows["a"]["towers_taken"], rows["a"]["towers_lost"]))

    def test_a_trade_of_nothing_for_nothing_prints_a_dash(self):
        text = rr.table(rr.tally([result("a", "b", killed=(0, 0), lost=(0, 0))]))
        self.assertIn("| a | 1 | 1 | 0 | 0 | 1 | - |", text)

    def test_minutes_average_over_the_factions_matches(self):
        rows = rr.tally([result("a", "b", mt=1800.0), result("a", "c", mt=2400.0)])
        self.assertAlmostEqual(35.0, rows["a"]["minutes"])

    def test_the_table_sorts_by_win_share_and_names_every_column(self):
        text = rr.table(rr.tally([result("a", "b", winner="West", time_win=False)]))
        lines = text.splitlines()
        self.assertIn("| Faction | Played | Won | Lost | Won by keep | Won on time | Gold trade | Towers taken | Towers lost | Minutes |", lines[0])
        self.assertTrue(lines[2].startswith("| a |"))


class ResultsForTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = self.tmp.name
        self.addCleanup(self.tmp.cleanup)

    def test_a_finished_match_with_a_winner_counts(self):
        write_run(self.root, "r1")
        results, _ = run_results(self.root)
        self.assertEqual(["r1"], [r["run"] for r in results])

    def test_a_final_report_with_no_winner_did_not_finish(self):
        write_run(self.root, "r1", winner=None)
        self.assertEqual([], run_results(self.root)[0])

    def test_a_winner_in_a_report_that_is_not_final_does_not_count(self):
        write_run(self.root, "r1", final=False)
        self.assertEqual([], run_results(self.root)[0])

    def test_minutes_use_the_winners_own_time_when_the_report_has_one(self):
        write_run(self.root, "r1", winner_mt=2410.0)
        self.assertEqual(2410.0, run_results(self.root)[0][0]["mt"])
        write_run(self.root, "r2", winner_mt=None)
        self.assertEqual(2431.6, run_results(self.root)[0][0]["mt"])

    def test_a_clean_stamp_counts_and_a_dirty_or_unversioned_one_does_not(self):
        write_run(self.root, "r1", taom=f"build.20261105T010203Z+{SHA}.dirty")
        write_run(self.root, "r2", taom=f"build.20261105T010203Z+{SHA}.nogit")
        self.assertEqual([], run_results(self.root)[0])
        write_run(self.root, "r3", taom=f"build.20261105T010203Z+{SHA}")
        self.assertEqual(["r3"], [r["run"] for r in run_results(self.root)[0]])

    def test_a_prefix_found_only_in_the_timestamp_does_not_count(self):
        write_run(self.root, "r1", taom="build.20261105T92973f27aZ+0123456789abcdef")
        self.assertEqual([], run_results(self.root)[0])

    def test_only_the_scheduled_orientation_counts_and_each_left_out_folder_is_named(self):
        write_run(self.root, "swapped", west="b", east="a")
        write_run(self.root, "nameless", west=None)
        write_run(self.root, "mirror", west="a", east="a")
        results, err = run_results(self.root)
        self.assertEqual([], results)
        for name in ("swapped", "nameless", "mirror"):
            self.assertIn(name, err)
        write_run(self.root, "scheduled")
        self.assertEqual(["scheduled"], [r["run"] for r in run_results(self.root)[0]])

    def test_a_recorded_time_end_decides_the_time_win_in_the_record(self):
        write_run(self.root, "r1", on_time=True)
        self.assertTrue(run_results(self.root)[0][0]["time_win"])
        write_run(self.root, "r2", on_time=False)
        self.assertFalse(run_results(self.root)[0][0]["time_win"])

    def test_a_match_that_is_not_4x_with_both_sides_ai_and_no_control_check_is_named_and_left_out(self):
        pairs = [("a", "b")]
        for name, change, why in (("fast", {"speed": 10}, "speed was 10"), ("eastonly", {"west_ai": False}, "west_ai"),
                                  ("checked", {"control_check": 240}, "control check"), ("nospeed", {"speed": None}, "speed was unset")):
            with self.subTest(name):
                write_run(self.root, name, **change)
                results, err = run_results(self.root)
                self.assertEqual([], results)
                self.assertIn(f"left out {name}: ", err)
                self.assertIn(why, err)
                self.assertEqual(pairs, rr.todo(pairs, results))
                shutil.rmtree(Path(self.root) / name)

    def test_a_final_report_with_a_winner_counts_even_when_the_runner_log_says_the_game_exited(self):
        folder = write_run(self.root, "r1")
        (folder / "runner_log.txt").write_text("11:47:50 FAIL: the game exited with code 0xC0000005 after the done file\n", encoding="utf-8")
        self.assertEqual(["r1"], [r["run"] for r in run_results(self.root)[0]])

    def test_a_malformed_report_in_another_build_does_not_stop_the_queue(self):
        write_run(self.root, "r0", report_text="{not json")
        write_run(self.root, "r1")
        results, err = run_results(self.root)
        self.assertEqual(["r1"], [r["run"] for r in results])
        self.assertIn("r0", err)

    def test_a_report_missing_its_evidence_on_this_build_is_named_and_skipped(self):
        write_run(self.root, "r0", report_text=json.dumps({"taom": f"build.1+{SHA}", "final": True, "evidence": {"End": {"Winner": "West"}}}))
        write_run(self.root, "r1")
        results, err = run_results(self.root)
        self.assertEqual(["r1"], [r["run"] for r in results])
        self.assertIn("r0", err)

    def test_another_builds_run_is_skipped_without_being_read_as_a_run(self):
        write_run(self.root, "r1", taom="build.1+0123456789abcdef")
        results, err = run_results(self.root)
        self.assertEqual(([], ""), (results, err))


def factions_text(owner):
    """A factions.json in which troop t1 belongs to the faction named owner."""
    return json.dumps({"factions": {name: ({"units": [{"troop": "t1"}]} if name == owner else {}) for name in ("a", "b")}})


class MainTests(unittest.TestCase):
    def run_main(self, command, root, show):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = rr.main([command, "--build", "92973f27a", "--runs-root", str(root)], show=show)
        return code, out.getvalue()

    def test_troop_credit_follows_the_factions_file_the_build_committed(self):
        with tempfile.TemporaryDirectory() as root:
            write_run(root, "r1")
            _, west_owns = self.run_main("summary", root, lambda build: factions_text("a"))
            _, east_owns = self.run_main("summary", root, lambda build: factions_text("b"))
        self.assertTrue(next(line for line in west_owns.splitlines() if line.startswith("| a |")).count("5.00"))
        self.assertTrue(next(line for line in east_owns.splitlines() if line.startswith("| b |")).count("5.00"))

    def test_the_schedule_of_todo_comes_from_the_builds_file_too(self):
        with tempfile.TemporaryDirectory() as root:
            self.assertEqual((0, "a b\n"), self.run_main("todo", root, lambda build: factions_text("a")))

    def test_an_unknown_build_is_a_usage_error(self):
        def unknown(build):
            raise subprocess.CalledProcessError(128, "git show")
        err = io.StringIO()
        with tempfile.TemporaryDirectory() as root, self.assertRaises(SystemExit) as stop, contextlib.redirect_stderr(err):
            rr.main(["todo", "--build", "92973f27a", "--runs-root", root], show=unknown)
        self.assertEqual(2, stop.exception.code)
        self.assertIn("unknown build 92973f27a", err.getvalue())


if __name__ == "__main__":
    unittest.main()
