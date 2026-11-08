"""Tests for tools/fords_round_data.py: one round robin match as a per-troop record."""
import unittest

from tools import fords_round_data as data


def _facts():
    return {
        "west": "gondor", "east": "mordor", "winner": "West", "mt": 1800.0,
        "towers_lost": {"West": 1, "East": 4}, "deaths": {"West": 900, "East": 1200},
        "faction_of": {"fow_g_bow": "West", "fow_m_orc": "East", "fow_shared": None},
        "spawned": {"fow_g_bow": 100, "fow_m_orc": 300, "fow_shared": 40},
        "troops": {
            "fow_g_bow": {"kills": 150, "deaths": 80, "valueKilled": 1500, "valueLost": 900},
            "fow_m_orc": {"kills": 70, "deaths": 290, "valueKilled": 800, "valueLost": 2000},
            "fow_shared": {"kills": 5, "deaths": 5, "valueKilled": 50, "valueLost": 50},
            "(unknown)": {"kills": 60, "deaths": 0, "valueKilled": 600, "valueLost": 0},
        },
        "on_time": False, "keep_fell": 1790.0,
    }


class MatchRecordTests(unittest.TestCase):
    def test_troops_are_kept_per_side_with_their_spawn_count(self):
        record = data.match_record(_facts(), run="20261107-0800")

        self.assertEqual(record["troops"]["fow_g_bow"], {"side": "West", "spawned": 100, "kills": 150, "deaths": 80})
        self.assertEqual(record["troops"]["fow_m_orc"]["side"], "East")

    def test_a_troop_both_sides_field_is_left_out(self):
        record = data.match_record(_facts(), run="r")

        self.assertNotIn("fow_shared", record["troops"])

    def test_kills_with_no_known_killer_are_kept_apart(self):
        record = data.match_record(_facts(), run="r")

        self.assertNotIn("(unknown)", record["troops"])
        self.assertEqual(record["unknown_kills"], 60)

    def test_the_outcome_fields_come_from_the_facts(self):
        record = data.match_record(_facts(), run="r")

        self.assertEqual((record["west"], record["east"], record["winner"]), ("gondor", "mordor", "West"))
        self.assertAlmostEqual(record["minutes"], 30.0)
        self.assertEqual(record["towers_lost"], {"West": 1, "East": 4})
        self.assertFalse(record["on_time"])

    def test_tower_damage_by_class_heroes_and_falls_are_kept(self):
        facts = _facts()
        facts["tower_classes"] = {"West": {"melee": 100, "ranged": 20, "hero": 50, "siege": 0, "other": 1},
                                  "East": {"melee": 10, "ranged": 0, "hero": 0, "siege": 0, "other": 0}}
        facts["heroes"] = {"West": 2, "East": 0}
        facts["tower_falls"] = {"tower_east_0_0": 1200.0, "keep_east": None}

        record = data.match_record(facts, run="r")

        self.assertEqual(record["tower_damage"]["West"], {"melee": 100, "ranged": 20, "hero": 50, "other": 1})
        self.assertEqual(record["heroes"], {"West": 2, "East": 0})
        self.assertEqual(record["tower_falls"], {"tower_east_0_0": 20.0, "keep_east": None})

    def test_a_spawned_troop_with_no_tally_row_counts_as_no_kills_and_no_deaths(self):
        facts = _facts()
        facts["spawned"]["fow_g_new"] = 12
        facts["faction_of"]["fow_g_new"] = "West"

        record = data.match_record(facts, run="r")

        self.assertEqual(record["troops"]["fow_g_new"], {"side": "West", "spawned": 12, "kills": 0, "deaths": 0})


if __name__ == "__main__":
    unittest.main()
