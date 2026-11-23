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
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import generate_lothlorien_troops as lorien  # noqa: E402
import generate_placeholder_weapons as gen  # noqa: E402
import generate_snow_orc_troops as snow  # noqa: E402
import realm_troops as rt  # noqa: E402
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
        # check_spec refuses two craft rows in one entry; carriers of one culture can still differ
        # through `craft_troop_prefix`, so the cap over carrier rows is tested on its own here.
        with mock.patch.object(gen, "check_spec", lambda spec, lspec=None: spec):
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


class PlaceholderCarrierLoadingTests(unittest.TestCase):
    """#1295: a donor given by a troop-level <equipment> directly under <Equipments> is in the troop's
    kit (the engine lays it over every set), so the troop is a carrier."""

    def carriers(self, body):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "troops_x.xml").write_text(
                '<NPCCharacters><NPCCharacter id="t" level="21" culture="Culture.nowhere">'
                f"<Equipments>{body}</Equipments></NPCCharacter></NPCCharacters>", encoding="utf-8")
            troops = report.load_troops(Path(tmp))
        w = {"id": "placeholder", "donor": "donor", "cultures": ["nowhere"], "tiers": [4, 8]}
        return [t.id for t in gen.carriers_of(w, troops)]

    def test_a_donor_in_a_roster_child_is_a_carrier(self):
        self.assertEqual(["t"], self.carriers(
            '<EquipmentRoster><equipment slot="Item0" id="Item.donor" /></EquipmentRoster>'))

    def test_a_donor_in_a_troop_level_equipment_is_a_carrier(self):
        self.assertEqual(["t"], self.carriers(
            '<EquipmentRoster><equipment slot="Item1" id="Item.shield" /></EquipmentRoster>'
            '<equipment slot="Item0" id="Item.donor" />'))

    def test_an_uppercase_troop_level_equipment_is_not_read(self):
        # The engine reads only a lowercase troop-level override, so an uppercase one gives nothing.
        self.assertEqual([], self.carriers(
            '<EquipmentRoster><equipment slot="Item1" id="Item.shield" /></EquipmentRoster>'
            '<Equipment slot="Item0" id="Item.donor" />'))


def shield_spear_donor() -> tuple[cata.Catalogue, dict]:
    """A spear as the Armory builds one: a one handed thrust mode a shield allows (primary) and a two
    handed swing and thrust mode that refuses a shield."""
    one = cata.Usage("OneHandedPolearm", True, (), "onehanded_polearm_block_long_rshield_thrust",
                     frozenset({"thrust"}), md.WeaponStats(thrust_damage=20, raw_thrust_speed=12.0),
                     class_template="OneHandedPolearm")
    two = cata.Usage("TwoHandedPolearm", False, (), "polearm_block_long_shield_swing_thrust",
                     frozenset({"swing", "thrust"}),
                     md.WeaponStats(swing_damage=60, raw_swing_speed=20.8, thrust_damage=26, raw_thrust_speed=12.0),
                     shield_ok=False, class_template="TwoHandedPolearm")
    cat, _ = donor_weapon("TwoHandedPolearm", one)
    item = cat.items["donor"]
    return cat, {"donor": cata.Priced(item=item, usages=(one, two), blade_piece="blade_one", tier=3)}


class PlaceholderShieldCarrierTests(unittest.TestCase):
    """A troop with a shield wields a spear in its one handed thrust mode (2026-11-16): the generator
    bands and caps a placeholder for such carriers in that mode, as the validator judges them."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = armory(Path(self._tmp.name))
        self.lspec = ladder.load_spec()

    def test_shield_carriers_band_the_placeholder_on_the_one_handed_thrust(self):
        cat, priced = shield_spear_donor()
        troops = [carrier("spearman", 26, "nowhere", ("donor", "sh"))]   # tier 5
        _p, _i, plan, _s, _d, _t = gen.build(spec_for(5), cat, priced, troops, self.root, shields={"sh"})
        band = plan[0][2]
        self.assertAlmostEqual(band.target, ladder.target(5, "nowhere", self.lspec, "OneHandedPolearm", False, True))

    def test_the_placeholder_is_not_over_the_limit_of_the_mode_its_carriers_hold(self):
        cat, priced = shield_spear_donor()
        donor = priced["donor"]
        troops = [carrier("spearman", 51, "nowhere", ("donor", "sh"))]   # tier 10: a large raise
        spec = spec_for(10)
        _p, _i, plan, _s, _d, _t = gen.build(spec, cat, priced, troops, self.root, shields={"sh"})
        k = plan[0][3]
        held = donor.with_shield()
        limit = ladder.tier_limit("OneHandedPolearm", 10, "nowhere", self.lspec)
        self.assertLessEqual(k, ladder.max_factor(limit, 0, held.thrust_damage, 0.0, held.thrust_dps) + 1e-9)
        rows = gen.carrier_rows(spec["weapons"][0], troops, self.lspec)
        self.assertEqual(gen.row_breaches(donor, rows, self.lspec, shielded={"spearman"}, default_held=True), [])
        # The two handed mode still reaches a player without a shield: it stays inside its class limit.
        full = ladder.class_limit("TwoHandedPolearm", self.lspec)
        self.assertLessEqual(k, ladder.max_factor(full, donor.swing_damage, donor.thrust_damage,
                                                  donor.swing_dps, donor.thrust_dps) + 1e-9)

    def test_without_a_shield_the_same_carrier_is_judged_on_the_whole_weapon(self):
        cat, priced = shield_spear_donor()
        troops = [carrier("halberdier", 26, "nowhere")]
        _p, _i, plan, _s, _d, _t = gen.build(spec_for(5), cat, priced, troops, self.root, shields={"sh"})
        band = plan[0][2]
        self.assertAlmostEqual(band.target, ladder.target(5, "nowhere", self.lspec, "TwoHandedPolearm", True, True))


class LineNumeralTests(unittest.TestCase):
    """A tier clone continues its donor's numbered line (owner, 2026-11-16). A melee line reads I to
    XXXIX; a numeral outside that range raises instead of dropping the weapon out of its line."""

    def test_a_line_runs_past_ten(self):
        self.assertEqual(report.line_key("[X] Spear XIV"), ("[X] Spear", "XIV"))
        self.assertEqual(report.NUMERAL_RANK["XIV"], 14)
        self.assertEqual([gen.roman(n) for n in (1, 4, 9, 14, 19, 24, 39)], ["I", "IV", "IX", "XIV", "XIX", "XXIV", "XXXIX"])

    def test_a_numeral_outside_the_line_range_raises(self):
        for n in (0, 40):
            with self.assertRaises(ValueError):
                gen.roman(n)


class CheckSpecTests(unittest.TestCase):
    """The spec rules that need no install run when the spec loads (`check_spec`), so the troop
    generators that call `placeholder_for` read a checked spec."""

    LSPEC = {"craft": {"a": [1.0, 1.0, 1.0], "b": [0.9, 1.0, 1.0]}, "craft_default": [0.85, 0.85, 0.85]}

    def spec(self, *rows):
        return {"tag": "taom_ph_", "weapons": [dict(r) for r in rows]}

    def row(self, wid, tiers, cultures=("a",), donor="d", tier=None):
        return {"id": wid, "donor": donor, "tier": tiers[0] if tier is None else tier, "tiers": list(tiers),
                "cultures": list(cultures), "name": wid}

    def test_overlapping_entries_of_one_donor_and_culture_raise(self):
        with self.assertRaisesRegex(gen.PlaceholderError, "same donor, culture and tier"):
            gen.check_spec(self.spec(self.row("taom_ph_x", (4, 6)), self.row("taom_ph_y", (6, 8))), self.LSPEC)

    def test_touching_spans_pass(self):
        gen.check_spec(self.spec(self.row("taom_ph_x", (4, 6)), self.row("taom_ph_y", (7, 8))), self.LSPEC)

    def test_cultures_with_different_craft_rows_raise(self):
        with self.assertRaisesRegex(gen.PlaceholderError, "different craft rows"):
            gen.check_spec(self.spec(self.row("taom_ph_x", (4, 6), ("a", "b"))), self.LSPEC)

    def test_cultures_on_the_default_row_share_it(self):
        gen.check_spec(self.spec(self.row("taom_ph_x", (4, 6), ("p", "q"))), self.LSPEC)

    def test_a_tier_outside_its_span_raises(self):
        with self.assertRaisesRegex(gen.PlaceholderError, "outside its tiers"):
            gen.check_spec(self.spec(self.row("taom_ph_x", (4, 6), tier=7)), self.LSPEC)

    def test_a_duplicate_id_and_a_missing_tag_raise(self):
        with self.assertRaisesRegex(gen.PlaceholderError, "duplicate id"):
            gen.check_spec(self.spec(self.row("taom_ph_x", (4, 4)), self.row("taom_ph_x", (6, 6), donor="e")), self.LSPEC)
        with self.assertRaisesRegex(gen.PlaceholderError, "tag"):
            gen.check_spec(self.spec(self.row("other_x", (4, 4))), self.LSPEC)

    def test_the_committed_spec_passes_without_an_install(self):
        gen.load_spec.cache_clear()
        self.assertTrue(gen.load_spec()["weapons"])

class NumberedLineFaultTests(unittest.TestCase):
    """Every numbered line keeps unique numerals, and no step a placeholder takes part in falls in DPS. A fall
    between two Armory weapons is the Armory's (25 lines on 16 Nov 2026) and is not this tool's to judge."""

    def test_a_duplicate_numeral_is_a_fault(self):
        faults = gen.line_faults({"[X] Axe": [(2, 40.0, "a", False), (2, 45.0, "taom_ph_b", True)]})
        self.assertEqual(len(faults), 1)
        self.assertIn("II", faults[0])

    def test_a_placeholder_below_the_step_before_it_is_a_fault(self):
        faults = gen.line_faults({"[X] Axe": [(1, 40.0, "a", False), (2, 50.0, "b", False),
                                              (3, 45.0, "taom_ph_c", True)]})
        self.assertEqual(len(faults), 1)
        self.assertIn("taom_ph_c", faults[0])

    def test_an_armory_fall_is_not_judged_and_a_rising_placeholder_passes(self):
        self.assertEqual(gen.line_faults({"[X] Axe": [(1, 50.0, "a", False), (2, 40.0, "b", False),
                                                      (3, 40.2, "taom_ph_c", True), (4, 60.0, "taom_ph_d", True)]}), [])

    def test_an_unpriced_member_is_skipped(self):
        self.assertEqual(gen.line_faults({"[X] Axe": [(1, None, "a", False), (2, 40.0, "taom_ph_b", True)]}), [])

    def test_numbered_lines_group_by_line_name(self):
        lines = gen.numbered_lines({"a": ("[X] Axe II", 40.0), "b": ("[X] Axe", 30.0), "taom_ph_c": ("[X] Axe III", 50.0)},
                                   {"taom_ph_c"})
        self.assertEqual(lines, {"[X] Axe": [(2, 40.0, "a", False), (3, 50.0, "taom_ph_c", True)]})


SPEC_ROWS = ({"id": "taom_ph_t", "donor": "donor_x", "tiers": [4, 6], "cultures": ["gondor"]},
             {"id": "taom_ph_u", "donor": "donor_x", "tiers": [7, 8], "cultures": ["gondor"]})


class SharedSwapRuleTests(unittest.TestCase):
    """One rule, read from the spec, swaps a donor for its placeholder: the tool's own step 4 and every troop
    generator that writes a whole file call it, so a regeneration never undoes a swap."""

    def test_a_donor_is_swapped_by_culture_and_tier(self):
        self.assertEqual(gen.placeholder_for("donor_x", "gondor", 5, SPEC_ROWS), "taom_ph_t")
        self.assertEqual(gen.placeholder_for("donor_x", "gondor", 7, SPEC_ROWS), "taom_ph_u")

    def test_anything_else_is_left_alone(self):
        self.assertEqual(gen.placeholder_for("donor_x", "gondor", 3, SPEC_ROWS), "donor_x")
        self.assertEqual(gen.placeholder_for("donor_x", "mordor", 5, SPEC_ROWS), "donor_x")
        self.assertEqual(gen.placeholder_for("other", "gondor", 5, SPEC_ROWS), "other")

    def test_the_troop_form_reads_the_melee_ladder_tier(self):
        level = 26   # engine tier 5
        tier = report.Troop("t", "t", level, "gondor", "x.xml", frozenset()).tier
        self.assertEqual(report.melee_tier("t", level), tier)
        self.assertEqual(gen.placeholder_for_troop("donor_x", "t", "gondor", level, SPEC_ROWS),
                         gen.placeholder_for("donor_x", "gondor", tier, SPEC_ROWS))

    def test_a_troop_the_melee_ladder_exempts_keeps_its_weapon(self):
        exempt = next(iter(ladder.load_spec()["exempt_troops"]))
        self.assertEqual(gen.placeholder_for_troop("donor_x", exempt, "gondor", 26, SPEC_ROWS), "donor_x")
        w = dict(SPEC_ROWS[0], cultures=["gondor"])
        troops = [carrier(exempt, 26, "gondor", ("donor_x",)), carrier("plain", 26, "gondor", ("donor_x",))]
        self.assertEqual([t.id for t in gen.carriers_of(w, troops)], ["plain"])

    def test_the_default_rows_are_the_spec_file(self):
        self.assertEqual([w["id"] for w in gen.spec_weapons()], [w["id"] for w in gen.load_spec()["weapons"]])

    def test_slots_in_a_generated_troop_file_are_left_to_its_generator(self):
        swaps = [{"file": "/x/troops_lothlorien.xml", "troop": "a", "slot": "Item0", "old": "d", "new": "p"},
                 {"file": "/x/troops_arthedain.xml", "troop": "b", "slot": "Item0", "old": "d", "new": "p"},
                 {"file": "/x/troops_gondor.xml", "troop": "c", "slot": "Item0", "old": "d", "new": "p"}]
        import fix_melee_ladder as fml
        self.assertNotIn("split_generated", vars(gen))   # one copy, in fix_melee_ladder
        writable, owed = fml.split_generated(swaps)
        self.assertEqual([s["troop"] for s in writable], ["c"])
        self.assertEqual([s["troop"] for s in owed], ["a", "b"])


class GeneratorAppliesTheSwapTests(unittest.TestCase):
    """A troop in a generated file gets its clone through its generator, under the same rule."""

    def rows(self, donor, culture, troop_id, level):
        tier = report.melee_tier(troop_id, level)
        return ({"id": "taom_ph_clone", "donor": donor, "tiers": [tier, tier], "cultures": [culture]},)

    def test_the_lothlorien_generator_renders_the_clone(self):
        troop = next(t for t in lorien.TROOPS if t["id"] == "lorien_militia_spearman")
        donor = troop["weapons"][0]["Item0"]
        rows = self.rows(donor, "lothlorien", troop["id"], lorien.TIER_LEVEL[troop["tier"]])
        with mock.patch.object(gen, "spec_weapons", return_value=rows):
            xml = lorien.render(lorien.TROOPS, {})   # the whole tree: the tavern copy names its source
        block = next(b for b in xml.split("<NPCCharacter") if f'id="{troop["id"]}"' in b)
        self.assertIn('id="Item.taom_ph_clone"', block)
        self.assertNotIn(f'id="Item.{donor}"', block)

    def test_the_snow_orc_generator_renders_the_clone(self):
        troop = snow.TROOPS[0]
        donor = next(v for k, v in troop["weapons"][0].items() if k.startswith("Item"))
        rows = self.rows(donor, "gundabad", troop["id"], snow.TIER_LEVEL[troop["tier"]])
        with mock.patch.object(gen, "spec_weapons", return_value=rows):
            xml = snow.render([troop], {})
        self.assertIn('id="Item.taom_ph_clone"', xml)
        self.assertNotIn(f'id="Item.{donor}"', xml)

    def test_a_realm_generator_renders_the_clone(self):
        realm = rt.Realm(culture="arthedain", label="[Arthedain]", script="x.py", pt_marker="X", militia={},
                         merc_source="", merc_name="")
        troop = rt.Troop("arthedain_x", "X", 26, "Infantry", "spear", rt.same("donor_y", "shield_z"),
                         ({"Body": "b"}, {"Body": "b"}), realm=realm)
        troop.sk = dict.fromkeys(rt.SKILLS, 0)
        rows = self.rows("donor_y", "arthedain", troop.id, troop.level)
        with mock.patch.object(gen, "spec_weapons", return_value=rows):
            xml = troop.xml("\n")
        self.assertEqual(xml.count('slot="Item0" id="Item.taom_ph_clone"'), 2)
        self.assertIn('slot="Item1" id="Item.shield_z"', xml)


class PlaceholderBladePieceTests(unittest.TestCase):
    """A cloned blade is a troop weapon's part, so the weapon designer never lists it
    (the engine reads is_hidden as CraftingPiece.IsHiddenOnDesigner), and its name is clean."""

    def build_piece(self, pieces_xml=PIECES):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        root = armory(Path(tmp.name))
        (root / "ModuleData" / "LOTRLOME_crafting_pieces.xml").write_text(pieces_xml, encoding="utf-8")
        cat, priced = donor_weapon("OneHandedSword", slow_sword())
        pieces, _i, _plan, _s, _d, _t = gen.build(spec_for(3), cat, priced, [], root)
        return pieces[0]

    def test_the_cloned_blade_is_hidden_on_the_designer(self):
        piece = self.build_piece()
        self.assertEqual(piece.count('is_hidden="true"'), 1, piece)
        self.assertIn('(placeholder)" is_hidden="true"', piece)

    def test_a_donor_name_with_a_double_space_is_written_with_one(self):
        # The Armory names the Iron Hills blades "Iron Hills  Sword Blade A", with two spaces.
        piece = self.build_piece(PIECES.replace('{=b1}Blade', '{=b1}Iron Hills  Sword  Blade '))
        self.assertIn('}Iron Hills Sword Blade (placeholder)"', piece)

    def test_a_donor_blade_already_hidden_keeps_one_attribute(self):
        piece = self.build_piece(PIECES.replace('piece_type="Blade"', 'is_hidden="false" piece_type="Blade"'))
        self.assertEqual(piece.count('is_hidden='), 1, piece)
        self.assertIn('is_hidden="true"', piece)


class LiveNumberedLineTests(unittest.TestCase):
    """LiveInstall: every numbered line of the installed Armory plus the placeholders, priced by the engine
    port, keeps unique numerals and does not fall at any step a placeholder takes part in."""

    def test_every_numbered_line_with_a_placeholder_holds(self):
        if not (report.MODULES / gen.ARMORY).is_dir():
            self.skipTest(f"no Armory at {report.MODULES / gen.ARMORY}")
        faults = gen.live_line_faults()
        self.assertEqual(faults, [], "\n".join(faults))


if __name__ == "__main__":
    unittest.main()
