#!/usr/bin/env python3
"""tools/fords_match_review.py: reading one match's log, and the findings drawn from it.

Run:  python -m unittest tools.tests.test_fords_match_review
"""
import json
import tempfile
import unittest
from pathlib import Path

from tools import fords_match_review as r

LOG = """\
[2026-11-03 11:40:00] [INFO] [FordsOfWar][Summary] run=1 w=500.0 mt=490.0 phase=Live liveS=480.0 liveW=80 liveE=170 backlog=0 deathsW=107 deathsE=1256 paidW=1 towersW=2 towersE=0 blows=1
[2026-11-03 11:47:00] [INFO] [FordsOfWar][Summary] run=1 w=900.0 mt=900.0 phase=Live liveS=890.0 liveW=87 liveE=228 backlog=0 deathsW=154 deathsE=1485 paidW=1 towersW=2 towersE=0 blows=1
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] troop=fow_elf kills=725 deaths=47 valueKilled=5726 valueLost=2021
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] troop=fow_warg kills=11 deaths=55 valueKilled=169 valueLost=1485
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] poison side=West doses=40 damage=500
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] side=West slot=1 earned=7286 spent=7185 left=701
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Balance] side=East slot=2 earned=5380 spent=5925 left=55
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Upgrade] side=West track={=TAOM_FOW_Upgrade_MissileDamage}Missile damage price=255
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Upgrade] blows raised=1162 lowered=2345 (totals)
[2026-11-03 11:47:01] [INFO] [FordsOfWar][Housekeep] thrownTopUps=0 horsesRemoved=0 horsesSpared=0 wargsRemoved=42 wargsSpared=0 (totals) riderlessOthersPeak=spider:4
[2026-11-03 11:47:01] [INFO] [FordsOfWar][EscMenu] run=1 w=20.9 mt=9.4 paused=true
[2026-11-03 11:47:01] [ERROR] [FordsOfWar] OnAgentHit failed (NullReferenceException: x)
[2026-11-03 11:47:01] [ERROR] [TableauDiag] something the mode does not own
[2026-11-03 11:47:01] [WARNING] [FordsOfWar] a warning
"""


def facts(**changes):
    base = {
        "west": "rivendell", "east": "mordor", "reason": "final", "crashed": False, "mt": 900.0,
        "deaths": {"West": 100, "East": 100}, "towers_lost": {"West": 1, "East": 1}, "tower_damage": {"West": 10000, "East": 10000},
        "gold": {"West": {"earned": 5000, "spent": 5000, "left": 100}, "East": {"earned": 5000, "spent": 5000, "left": 100}},
        "troops": {}, "faction_of": {}, "upgrades": {"West": ["a"], "East": ["a"]}, "heroes": {"West": 1, "East": 1},
        "poison": {}, "poison_factions": [], "creatures": {}, "free_troops": set(), "spawned": {}, "esc_pauses": 0, "shader_compiles": 0,
        "perf": {"avg": 5.0, "p95": 8.0}, "errors": [], "warnings": 0, "wargs": 0, "riderless_others": "-", "speed": 1,
    }
    base.update(changes)
    return base


def text(found):
    return "\n".join(found)


class HeroLivesTests(unittest.TestCase):
    MARKS = [
        {"Kind": "fielded", "HeroId": "a", "Side": "West", "Mt": 100.0},
        {"Kind": "spawned", "HeroId": "a", "Side": "West", "Mt": 100.0},
        {"Kind": "died", "HeroId": "a", "Side": "West", "Mt": 160.0},
        {"Kind": "spawned", "HeroId": "a", "Side": "West", "Mt": 200.0},
        {"Kind": "respawned", "HeroId": "a", "Side": "West", "Mt": 200.0},
        {"Kind": "spawned", "HeroId": "b", "Side": "East", "Mt": 300.0},
    ]

    def test_a_life_runs_to_its_death_or_to_the_end_of_the_match(self):
        lives = r.hero_lives(self.MARKS, end=400.0)
        self.assertEqual({"side": "West", "lives": 2, "seconds": 260.0}, lives["a"])
        self.assertEqual({"side": "East", "lives": 1, "seconds": 100.0}, lives["b"])

    def test_a_hero_that_kills_little_for_its_time_on_the_field_is_flagged(self):
        found = text(r.findings(facts(hero_lives={"h": {"side": "West", "lives": 2, "seconds": 600.0}}, troops={"h": {"kills": 5, "deaths": 2, "valueKilled": 100, "valueLost": 2000}})))
        self.assertIn("HERO weak: h (rivendell) 5 kills in 600 s on the field, 0.5 per minute", found)

    def test_a_hero_that_kills_well_or_barely_fought_is_not_flagged(self):
        strong = facts(hero_lives={"h": {"side": "West", "lives": 1, "seconds": 60.0}}, troops={"h": {"kills": 5, "deaths": 1, "valueKilled": 100, "valueLost": 1000}})
        brief = facts(hero_lives={"h": {"side": "East", "lives": 1, "seconds": 30.0}})
        self.assertNotIn("HERO weak", text(r.findings(strong)))
        self.assertNotIn("HERO weak", text(r.findings(brief)))


class ParseLogTests(unittest.TestCase):
    def setUp(self):
        self.parsed = r.parse_log(LOG)

    def test_the_last_summary_gives_the_deaths_and_towers(self):
        self.assertEqual(self.parsed["deaths"], {"West": 154, "East": 1485})
        self.assertEqual(self.parsed["towers_lost"], {"West": 2, "East": 0})
        self.assertEqual(self.parsed["mt"], 900.0)

    def test_troops_gold_and_poison_come_from_the_balance_lines(self):
        self.assertEqual(self.parsed["troops"]["fow_elf"], {"kills": 725, "deaths": 47, "valueKilled": 5726, "valueLost": 2021})
        self.assertEqual(self.parsed["gold"]["East"], {"earned": 5380, "spent": 5925, "left": 55})
        self.assertEqual(self.parsed["poison"]["West"], {"doses": 40, "damage": 500})

    def test_the_gold_words_of_a_newer_build_are_read(self):
        parsed = r.parse_log("[FordsOfWar][Balance] side=East slot=2 earned=50 spent=40 left=10 income=20 bounty=30 units=10 creatures=30")
        self.assertEqual(parsed["gold"]["East"], {"earned": 50, "spent": 40, "left": 10, "income": 20, "bounty": 30, "units": 10, "creatures": 30})

    def test_upgrades_keep_their_english_name_and_the_blow_totals_are_not_upgrades(self):
        self.assertEqual(self.parsed["upgrades"], {"West": ["Missile damage"], "East": []})

    def test_the_last_housekeep_line_gives_wargs_and_other_mounts(self):
        self.assertEqual(self.parsed["wargs"], 42)
        self.assertEqual(self.parsed["riderless_others"], "spider:4")

    def test_the_fastest_speed_of_the_match_is_read(self):
        self.assertEqual(self.parsed["speed"], 1)
        parsed = r.parse_log("[FordsOfWar][Speed] speed=4 mt=10.0\n[FordsOfWar][Speed] speed=10 mt=20.0\n[FordsOfWar][Speed] speed=1 mt=30.0\n")
        self.assertEqual(parsed["speed"], 10)

    def test_pauses_errors_and_warnings_are_counted(self):
        self.assertEqual(self.parsed["esc_pauses"], 1)
        self.assertEqual(len(self.parsed["errors"]), 1)
        self.assertIn("OnAgentHit", self.parsed["errors"][0])
        self.assertEqual(self.parsed["warnings"], 1)
        self.assertEqual(self.parsed["other_errors"], 1)


class RunnerOutcomeTests(unittest.TestCase):
    def test_a_pass_that_names_the_crash_report_is_no_crash(self):
        self.assertEqual(r.runner_outcome("11:47:50 PASS: reason=final, no CrashReport block"), ("reason=final, no CrashReport block", False))

    def test_a_game_that_exited_early_is_a_crash(self):
        reason, crashed = r.runner_outcome("11:02:57 FAIL: the game exited (code 3221225477) before the done file")
        self.assertTrue(crashed)
        self.assertIn("3221225477", reason)

    def test_a_failed_check_is_not_a_crash(self):
        self.assertFalse(r.runner_outcome("11:02:57 FAIL: check 3 failed")[1])

    def test_no_outcome_is_named(self):
        self.assertEqual(r.runner_outcome("nothing here"), ("no outcome line", False))


class FactionTableTests(unittest.TestCase):
    def test_a_troop_also_on_sale_is_not_counted_as_free(self):
        faction = {"units": [{"troop": "sold"}], "freeWaves": [{"units": [{"troop": "sold"}, {"troop": "only_free"}]}]}
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "factions.json"
            path.write_text(json.dumps({"factions": {"a": faction, "b": {}}}), encoding="utf-8")
            self.assertEqual(r._faction_tables(path, "a", "b")[2], {"only_free"})


class FindingTests(unittest.TestCase):
    def test_an_even_match_has_no_balance_flag(self):
        self.assertNotIn("BALANCE", text(r.findings(facts())))

    def test_a_crash_or_a_short_match_is_named_first(self):
        found = r.findings(facts(crashed=True, reason="crash", mt=500.0))
        self.assertIn("CRASH", found[0])

    def test_a_lopsided_gold_trade_is_flagged_with_the_winner(self):
        troops = {"fow_elf": {"kills": 700, "deaths": 40, "valueKilled": 6000, "valueLost": 1000}, "fow_orc": {"kills": 40, "deaths": 700, "valueKilled": 1000, "valueLost": 6000}}
        found = text(r.findings(facts(troops=troops, faction_of={"fow_elf": "West", "fow_orc": "East"})))
        self.assertIn("BALANCE", found)
        self.assertIn("rivendell", found)
        self.assertIn("6.0", found)

    def test_a_match_at_more_than_normal_speed_is_named_first_so_nobody_reads_it_as_a_normal_one(self):
        self.assertIn("SPEED", r.findings(facts(speed=10))[0])
        self.assertIn("10x", r.findings(facts(speed=10))[0])
        self.assertNotIn("SPEED", text(r.findings(facts())))

    def test_a_tower_swing_is_flagged(self):
        self.assertIn("towers", text(r.findings(facts(towers_lost={"West": 0, "East": 4}))))

    def test_the_best_and_worst_troops_are_named_when_they_fought_enough(self):
        troops = {
            "fow_good": {"kills": 300, "deaths": 20, "valueKilled": 3000, "valueLost": 500},
            "fow_bad": {"kills": 5, "deaths": 60, "valueKilled": 100, "valueLost": 1500},
            "fow_few": {"kills": 1, "deaths": 1, "valueKilled": 500, "valueLost": 5},
        }
        found = text(r.findings(facts(troops=troops, faction_of={k: "West" for k in troops})))
        self.assertIn("fow_good", found)
        self.assertIn("fow_bad", found)
        self.assertNotIn("fow_few", found)

    def test_creature_calls_that_do_not_repay_their_gold_are_flagged(self):
        troops = {"fow_warg": {"kills": 11, "deaths": 55, "valueKilled": 169, "valueLost": 1485}}
        found = text(r.findings(facts(troops=troops, faction_of={"fow_warg": "East"}, creatures={"East": {"fow_warg"}})))
        self.assertIn("creature calls", found)
        self.assertIn("mordor", found)

    def test_a_side_that_mostly_fights_with_free_units_is_flagged(self):
        spawned = {"fow_free": 240, "fow_bought": 60}
        found = text(r.findings(facts(spawned=spawned, free_troops={"fow_free"}, faction_of={"fow_free": "West", "fow_bought": "West"})))
        self.assertIn("free", found)
        self.assertIn("80%", found)

    def test_gold_left_unspent_is_flagged(self):
        gold = {"West": {"earned": 5000, "spent": 2000, "left": 3000}, "East": {"earned": 5000, "spent": 5000, "left": 100}}
        self.assertIn("unspent", text(r.findings(facts(gold=gold))))

    def test_gold_poured_into_one_special_is_flagged_but_units_are_not(self):
        spent = {"earned": 5000, "spent": 4000, "left": 100, "units": 1000, "creatures": 3000}
        plain = {"earned": 5000, "spent": 4000, "left": 100, "units": 3900, "creatures": 100}
        self.assertIn("75% of its gold on creatures", text(r.findings(facts(gold={"West": spent, "East": plain}))))
        self.assertNotIn("of its gold on units", text(r.findings(facts(gold={"West": plain, "East": plain}))))

    def test_no_upgrades_or_heroes_on_a_side_is_flagged(self):
        found = text(r.findings(facts(upgrades={"West": [], "East": ["a"]}, heroes={"West": 0, "East": 1})))
        self.assertIn("no upgrade", found)
        self.assertIn("no hero", found)

    def test_a_poison_faction_with_no_doses_is_flagged_as_a_broken_feature(self):
        found = text(r.findings(facts(poison_factions=["West"], poison={})))
        self.assertIn("FEATURE", found)
        self.assertIn("poison", found)

    def test_errors_pauses_frames_and_shader_compiles_are_flagged(self):
        found = text(r.findings(facts(errors=["boom"], esc_pauses=2, perf={"avg": 9.0, "p95": 20.0}, shader_compiles=1500)))
        self.assertIn("ERROR", found)
        self.assertIn("Escape", found)
        self.assertIn("frame", found)
        self.assertIn("shader", found)


if __name__ == "__main__":
    unittest.main()
