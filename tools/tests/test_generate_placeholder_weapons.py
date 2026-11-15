#!/usr/bin/env python3
"""Tests for `tools/generate_placeholder_weapons.py`: how a placeholder's damage factor is chosen.

Synthetic armory in a temporary folder. The real generator reads the installed Armory, which CI
does not have; `build` is a pure function over a catalogue and two small XML files, so it is
tested that way. The point pinned here is the tier limit (2026-11-14, #1271): a placeholder made
for tier 3 may not carry the blow of a tier 10 weapon, whatever its DPS target would allow.
unittest, not pytest, because CI runs the tool tests with unittest (test_ci_runner_compat.py).
"""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import generate_placeholder_weapons as gen  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402
import melee_ladder as ladder  # noqa: E402

PIECES = """<?xml version="1.0" encoding="utf-8"?>
<CraftingPieces>
    <CraftingPiece id="blade_one" name="{=b1}Blade" piece_type="Blade" length="70" weight="1.0">
        <BladeData physics_material="metal_weapon">
            <Thrust damage_type="Pierce" damage_factor="2.00" />
            <Swing damage_type="Cut" damage_factor="4.00" />
        </BladeData>
    </CraftingPiece>
</CraftingPieces>
"""

ITEMS = """<?xml version="1.0" encoding="utf-8"?>
<Items>
    <CraftedItem id="donor" name="{=d}Donor" crafting_template="OneHandedSword">
        <Pieces>
            <Piece id="blade_one" Type="Blade" />
        </Pieces>
    </CraftedItem>
</Items>
"""


def armory(root: Path) -> Path:
    data = root / "ModuleData"
    (data / "LOTRLOME_items").mkdir(parents=True)
    (data / "LOTRLOME_crafting_pieces.xml").write_text(PIECES, encoding="utf-8")
    (data / "LOTRLOME_items" / "swords.xml").write_text(ITEMS, encoding="utf-8")
    return root


def donor_weapon(template: str, usage: cata.Usage) -> tuple[cata.Catalogue, dict]:
    cat = cata.Catalogue()
    item = cata.Item("donor", "Donor", "", template, (("blade_one", "Blade", 100),), "src")
    cat.items["donor"] = item
    cat.descriptions["Desc"] = cata.Description("Desc", "T", frozenset(), frozenset({"blade_one"}), ())
    priced = cata.Priced(item=item, usages=(usage,), blade_piece="blade_one", tier=3)
    return cat, {"donor": priced}


def slow_sword() -> cata.Usage:
    """55 swing at about 25 DPS: its blow is large against its DPS, so the blow limit binds."""
    return cata.Usage("D", True, (), "set", frozenset({"swing"}),
                      md.WeaponStats(swing_damage=55, raw_swing_speed=10.0))


def spec_for(tier: int, culture: str = "nowhere", **extra) -> dict:
    return {"tag": "taom_ph_", "weapons": [
        {"id": "taom_ph_x", "donor": "donor", "tier": tier, "tiers": [tier, tier],
         "cultures": [culture], "name": "[Test] Sword", **extra}]}


class PlaceholderTierLimitTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = armory(Path(self._tmp.name))

    def test_k_is_capped_by_the_tier_limit_not_the_class_limit(self):
        cat, priced = donor_weapon("OneHandedSword", slow_sword())
        _p, _i, plan, _s, _d, _t = gen.build(spec_for(3), cat, priced, [], self.root)
        _w, donor, band, k, _blade, _xml, _users, capped = plan[0]
        lspec = ladder.load_spec()
        tier_cap = ladder.max_factor(ladder.tier_limit("OneHandedSword", 3, "nowhere", lspec),
                                     donor.swing_damage, donor.thrust_damage, donor.swing_dps, donor.thrust_dps)
        class_cap = ladder.max_factor(ladder.class_limit("OneHandedSword", lspec),
                                      donor.swing_damage, donor.thrust_damage, donor.swing_dps, donor.thrust_dps)
        self.assertLess(tier_cap, class_cap)
        self.assertGreater(band.target / donor.best_dps, tier_cap)   # the DPS target alone would overshoot the blow
        self.assertTrue(capped)
        self.assertAlmostEqual(k, tier_cap)

    def test_a_tier_10_placeholder_of_the_best_craft_reaches_the_class_limit(self):
        """Only tier 10 of craft 1.0 is the class limit itself; one step off it and the tier limit is lower."""
        cat, priced = donor_weapon("OneHandedSword", slow_sword())
        lspec = ladder.load_spec()
        for tier, culture, reaches_class in ((10, "rivendell", True), (10, "nowhere", False),
                                             (9, "rivendell", False)):
            with self.subTest(tier=tier, culture=culture):
                _p, _i, plan, _s, _d, _t = gen.build(spec_for(tier, culture), cat, priced, [], self.root)
                donor, k = plan[0][1], plan[0][3]
                args = (donor.swing_damage, donor.thrust_damage, donor.swing_dps, donor.thrust_dps)
                class_cap = ladder.max_factor(ladder.class_limit("OneHandedSword", lspec), *args)
                tier_cap = ladder.max_factor(ladder.tier_limit("OneHandedSword", tier, culture, lspec), *args)
                self.assertAlmostEqual(k, tier_cap)
                if reaches_class:
                    self.assertAlmostEqual(k, class_cap)
                else:
                    self.assertLess(k, class_cap)

    def test_a_thrust_dominant_polearm_donor_is_banded_from_the_thrust_top(self):
        usage = cata.Usage("D", True, (), "set", frozenset({"swing", "thrust"}),
                           md.WeaponStats(swing_damage=20, raw_swing_speed=20.8,
                                          thrust_damage=30, raw_thrust_speed=12.0))
        cat, priced = donor_weapon("TwoHandedPolearm", usage)
        self.assertGreater(priced["donor"].thrust_dps, priced["donor"].swing_dps)
        _p, _i, plan, _s, _d, _t = gen.build(spec_for(8), cat, priced, [], self.root)
        band = plan[0][2]
        lspec = ladder.load_spec()
        self.assertAlmostEqual(band.target,
                               45 * lspec["class_tier_fraction"]["8"] * lspec["craft_default"]["polearm"])


class PlaceholderPerAttackTests(unittest.TestCase):
    """Each attack has its own damage_factor and its own limits (#1296), as restat_melee_blades.class_ratios
    holds them: a thrust at its limit must not stop the swing."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = armory(Path(self._tmp.name))

    def test_each_attack_gets_its_own_factor_on_multiline_and_single_attack_pieces(self):
        xml = '<Swing damage_type="Cut"\r\n    damage_factor="4.00" />\r\n<Thrust\r\n    damage_factor="2.00" />'
        out = gen.scale_factors(xml, 1.5, 1.0)
        self.assertIn('damage_factor="6.00"', out)
        self.assertIn('damage_factor="2.00"', out)
        self.assertIn('damage_factor="3.00"', gen.scale_factors('<Thrust damage_factor="2.00" />', 9.0, 1.5))

    def test_a_thrust_at_its_limit_leaves_the_swing_free_to_rise(self):
        usage = cata.Usage("D", True, (), "set", frozenset({"swing", "thrust"}),
                           md.WeaponStats(swing_damage=45, raw_swing_speed=22.0,
                                          thrust_damage=30, raw_thrust_speed=12.0))
        cat, priced = donor_weapon("TwoHandedPolearm", usage)
        donor = priced["donor"]
        lspec = ladder.load_spec()
        limit = ladder.tier_limit("TwoHandedPolearm", 5, "nowhere", lspec)
        swing_cap = ladder.max_factor(limit, donor.swing_damage, 0, donor.swing_dps, 0.0)
        thrust_cap = ladder.max_factor(limit, 0, donor.thrust_damage, 0.0, donor.thrust_dps)
        _p, _i, plan, _s, _d, _t = gen.build(spec_for(5), cat, priced, [], self.root)
        k_target = plan[0][2].target / donor.best_dps
        self.assertLess(thrust_cap, 1.05)                  # the thrust has no room
        self.assertGreater(min(k_target, swing_cap), 2.0)  # the swing has plenty
        xml = plan[0][5]
        self.assertIn(f'damage_factor="{ladder.floor_factor(4.00 * min(k_target, swing_cap)):.2f}"', xml)
        self.assertIn(f'damage_factor="{ladder.floor_factor(2.00 * min(k_target, thrust_cap)):.2f}"', xml)
        self.assertAlmostEqual(plan[0][3], min(k_target, swing_cap))   # the DPS ratio follows the swing


class PlaceholderFactorRoundingTests(unittest.TestCase):
    def test_a_scaled_factor_is_rounded_down_never_up(self):
        xml = '<Swing damage_factor="4.00" /><Thrust damage_factor="2.00" />'
        out = gen.scale_factors(xml, 1.1245)   # 4.498 and 2.249 round to nearest as 4.50 and 2.25
        self.assertIn('damage_factor="4.49"', out)
        self.assertIn('damage_factor="2.24"', out)


def carrier(tid, level, culture, items=("donor",)):
    return report.Troop(tid, tid, level, culture, "troops_x.xml", frozenset(items),
                        path="troops_x.xml", slots_of={i: frozenset({"Item0"}) for i in items})


class PlaceholderCarrierLimitTests(unittest.TestCase):
    """M4 (2026-11-14, #1271 deep review): the weapon goes to every troop in `tiers` of every listed
    culture, so the cap is the tightest tier limit of those carriers, not of the spec's `tier`."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = armory(Path(self._tmp.name))
        self.lspec = ladder.load_spec()

    def cap(self, donor, tier, craft):
        return ladder.max_factor(ladder.tier_limit("OneHandedSword", tier, craft, self.lspec),
                                 donor.swing_damage, donor.thrust_damage, donor.swing_dps, donor.thrust_dps)

    def test_k_is_capped_at_the_lowest_tier_that_carries_the_weapon(self):
        cat, priced = donor_weapon("OneHandedSword", slow_sword())
        spec = spec_for(8)
        spec["weapons"][0]["tiers"] = [4, 8]
        troops = [carrier("recruit", 21, "nowhere"), carrier("guard", 41, "nowhere")]   # tiers 4 and 8
        _p, _i, plan, _s, _d, _t = gen.build(spec, cat, priced, troops, self.root)
        donor, k, capped = plan[0][1], plan[0][3], plan[0][7]
        self.assertTrue(capped)
        self.assertAlmostEqual(k, self.cap(donor, 4, "nowhere"))
        self.assertLess(k, self.cap(donor, 8, "nowhere"))

    def test_k_is_capped_at_the_lowest_craft_among_the_carriers(self):
        cat, priced = donor_weapon("OneHandedSword", slow_sword())
        spec = spec_for(8, "rivendell")
        spec["weapons"][0]["cultures"] = ["rivendell", "nowhere"]
        troops = [carrier("elf", 41, "rivendell"), carrier("man", 41, "nowhere")]
        _p, _i, plan, _s, _d, _t = gen.build(spec, cat, priced, troops, self.root)
        donor, k = plan[0][1], plan[0][3]
        self.assertAlmostEqual(k, self.cap(donor, 8, "nowhere"))
        self.assertLess(k, self.cap(donor, 8, "rivendell"))

    def test_carrier_rows_group_the_carriers_by_tier_and_craft(self):
        w = spec_for(8, "rivendell")["weapons"][0]
        w["tiers"] = [4, 8]
        w["cultures"] = ["rivendell", "nowhere"]
        troops = [carrier("elf", 41, "rivendell"), carrier("man_b", 41, "nowhere"),
                  carrier("man_a", 41, "nowhere"), carrier("recruit", 21, "nowhere")]
        self.assertEqual(gen.carrier_rows(w, troops, self.lspec),
                         {(8, "rivendell"): ["elf"], (8, "nowhere"): ["man_a", "man_b"],
                          (4, "nowhere"): ["recruit"]})

    def test_the_spec_tier_and_culture_are_a_row_even_when_no_troop_carries_it(self):
        w = spec_for(8, "rivendell")["weapons"][0]
        rows = gen.carrier_rows(w, [carrier("man", 41, "nowhere")], self.lspec)
        self.assertEqual(rows, {(8, "nowhere"): ["man"], (8, "rivendell"): []})

    def test_the_spec_row_is_rated_through_craft_key(self):
        w = spec_for(8, "mordor")["weapons"][0]
        lspec = dict(self.lspec, craft_troop_prefix={"taom_ph_": "mordor_numenorean"})
        self.assertEqual(gen.spec_craft(w, lspec), "mordor_numenorean")
        self.assertEqual(gen.spec_craft(w, self.lspec), "mordor")
        self.assertIn((8, "mordor_numenorean"), gen.carrier_rows(w, [], lspec))

    def test_the_check_names_every_carrier_of_a_row_the_weapon_is_over(self):
        heavy = cata.Usage("D", True, (), "set", frozenset({"swing"}),
                           md.WeaponStats(swing_damage=70, raw_swing_speed=10.0))
        cat, priced = donor_weapon("OneHandedSword", heavy)
        donor = priced["donor"]
        w = spec_for(8)["weapons"][0]
        w["tiers"] = [4, 8]
        troops = [carrier("recruit_b", 21, "nowhere"), carrier("recruit_a", 21, "nowhere"),
                  carrier("guard", 41, "nowhere")]
        # The weapon swings 70: above the tier 4 limit (62), inside the tier 8 limit (76).
        over = gen.row_breaches(donor, gen.carrier_rows(w, troops, self.lspec), self.lspec)
        self.assertEqual([(row, ids) for row, ids, _b in over],
                         [((4, "nowhere"), ["recruit_a", "recruit_b"])])
        self.assertEqual(gen.row_breaches(donor, gen.carrier_rows(w, troops[2:], self.lspec), self.lspec), [])


if __name__ == "__main__":
    unittest.main()
