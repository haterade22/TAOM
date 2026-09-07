#!/usr/bin/env python3
"""Unit tests for rebalance_template_power.py.

The tool exists because a flat `max_value` cannot balance these templates. Raider cultures
differ by troop tier, so at a flat 20 per stack the eight raider templates span 64.4 to
112.4 power, a 1.75x spread. Balancing has to target a POWER budget, not a headcount.

Two failure modes are pinned here because both have shipped in this repo before:

  - Rounding a stack out of existence. A stack at min 0 / max 0 spawns nothing and cannot
    be restored by a later retarget, because every future scale multiplies a spread of
    zero. That is the 2026-09-04 Black Numenorean deletion, recorded in
    docs/reference/party-template-sizing.md.
  - Emitting min > max. The engine fills a stack to `min + (max - min) * r`, so a max
    below its min drives the stack below its own floor.
"""
import math
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import rebalance_template_power as rtp  # noqa: E402

BOM = "﻿"


class TierForLevel(unittest.TestCase):
    """Tier is clamp(ceil((level - 5) / 5), 0, 10) - a pure function of level."""

    def test_known_troop_levels(self):
        # Every one of these is a real TAOM troop level, checked against the shipped data.
        self.assertEqual(rtp.tier_for_level(1), 0)    # villager_erebor
        self.assertEqual(rtp.tier_for_level(6), 1)    # dunland_peasant
        self.assertEqual(rtp.tier_for_level(11), 2)   # dunland_raider
        self.assertEqual(rtp.tier_for_level(16), 3)   # armed_trader_*
        self.assertEqual(rtp.tier_for_level(21), 4)   # caravan_guard_*
        self.assertEqual(rtp.tier_for_level(26), 5)   # veteran_caravan_guard_*

    def test_level_zero_is_tier_zero_not_negative(self):
        # armed_trader_rohan and its three siblings ship with no level= attribute at all,
        # which the engine reads as 0. The clamp is what stops that going negative.
        self.assertEqual(rtp.tier_for_level(0), 0)

    def test_clamps_at_the_top(self):
        self.assertEqual(rtp.tier_for_level(500), 10)

    def test_matches_the_formula_across_the_range(self):
        for lvl in range(0, 60):
            expected = max(0, min(10, math.ceil((lvl - 5) / 5)))
            self.assertEqual(rtp.tier_for_level(lvl), expected, "level %d" % lvl)


class ScaleToPower(unittest.TestCase):
    """scale_to_power picks counts whose power-weighted sum lands on the budget."""

    # dunland_raiders: T1, T2, T2, T3
    DUNLAND_POWERS = [0.66, 0.96, 0.96, 1.30]
    DUNLAND_MINS = [8, 4, 3, 1]
    DUNLAND_MAXES = [50, 50, 50, 50]

    def test_hits_the_budget(self):
        new = rtp.scale_to_power(self.DUNLAND_MAXES, self.DUNLAND_POWERS,
                                 budget=78.0, floors=self.DUNLAND_MINS)
        got = rtp.power_of(new, self.DUNLAND_POWERS)
        self.assertAlmostEqual(got, 78.0, delta=1.5)

    def test_equal_shape_gives_equal_counts(self):
        # All four stacks share max_value=50, so the shape is flat and the solution is too.
        new = rtp.scale_to_power(self.DUNLAND_MAXES, self.DUNLAND_POWERS,
                                 budget=78.0, floors=self.DUNLAND_MINS)
        self.assertEqual(len(set(new)), 1,
                         "a flat input shape must give a flat output: %r" % new)
        self.assertEqual(new[0], 20)

    def test_a_stronger_roster_gets_fewer_bodies_for_the_same_power(self):
        # erebor_warriors is T2,T3,T4,T4 against dunland's T1,T2,T2,T3. Same budget,
        # fewer men. This is the whole reason the tool targets power instead of headcount.
        erebor = rtp.scale_to_power([50, 50, 50, 50], [0.96, 1.30, 1.68, 1.68],
                                    budget=78.0, floors=[8, 4, 3, 1])
        dunland = rtp.scale_to_power(self.DUNLAND_MAXES, self.DUNLAND_POWERS,
                                     budget=78.0, floors=self.DUNLAND_MINS)
        self.assertLess(sum(erebor), sum(dunland))
        self.assertAlmostEqual(rtp.power_of(erebor, [0.96, 1.30, 1.68, 1.68]),
                               rtp.power_of(dunland, self.DUNLAND_POWERS), delta=3.0)

    def test_never_returns_a_count_below_its_floor(self):
        # A brutal cut must still leave every stack spawnable.
        new = rtp.scale_to_power([3495, 5], [1.30, 2.10], budget=20.0, floors=[0, 0])
        self.assertTrue(all(c >= 1 for c in new),
                        "a stack that could spawn a troop must keep a nonzero max: %r" % new)

    def test_respects_an_explicit_floor(self):
        new = rtp.scale_to_power([50, 50], [1.30, 2.10], budget=10.0, floors=[8, 4])
        self.assertGreaterEqual(new[0], 8)
        self.assertGreaterEqual(new[1], 4)

    def test_is_idempotent(self):
        once = rtp.scale_to_power(self.DUNLAND_MAXES, self.DUNLAND_POWERS,
                                  budget=78.0, floors=self.DUNLAND_MINS)
        twice = rtp.scale_to_power(once, self.DUNLAND_POWERS,
                                   budget=78.0, floors=self.DUNLAND_MINS)
        self.assertEqual(once, twice)

    def test_zero_power_roster_is_refused_rather_than_dividing_by_zero(self):
        # Every Rohan caravan troop is level 0 today. Silently "solving" a zero-power
        # roster would emit an absurd count; the tool must say so instead.
        with self.assertRaises(ValueError):
            rtp.scale_to_power([10, 10], [0.0, 0.0], budget=78.0, floors=[1, 1])


class CaravanBand(unittest.TestCase):
    """Caravans are solved as a band: the MIN must already clear the raider budget."""

    # A 14-culture caravan: armed_trader T3, caravan_guard T4, veteran T5.
    POWERS = [1.30, 1.68, 2.10]
    ELITE_MAXES = [18, 14, 17]

    def test_min_clears_the_floor_budget_and_max_sits_above_it(self):
        mins, maxes = rtp.solve_band(self.ELITE_MAXES, self.POWERS,
                                     floor_power=96.0, spread=0.15)
        self.assertAlmostEqual(rtp.power_of(mins, self.POWERS), 96.0, delta=2.0)
        self.assertAlmostEqual(rtp.power_of(maxes, self.POWERS), 96.0 * 1.15, delta=2.0)

    def test_every_stack_has_min_not_greater_than_max(self):
        mins, maxes = rtp.solve_band(self.ELITE_MAXES, self.POWERS,
                                     floor_power=96.0, spread=0.15)
        for mn, mx in zip(mins, maxes):
            self.assertLessEqual(mn, mx, "min %d > max %d" % (mn, mx))

    def test_the_whole_band_beats_the_raider_budget(self):
        # This is the property the feature turns on: localAdvantage >= 1 at every draw,
        # because avoidScore is zero only when the caravan is at least as strong.
        raider_budget = 78.0
        mins, _ = rtp.solve_band(self.ELITE_MAXES, self.POWERS,
                                 floor_power=86.0, spread=0.15)
        self.assertGreater(rtp.power_of(mins, self.POWERS), raider_budget)

    def test_a_weaker_troop_mix_gets_more_bodies(self):
        # harad/isengard/rhun caravan guards are one tier down (T3,T3,T4).
        strong_mins, _ = rtp.solve_band(self.ELITE_MAXES, [1.30, 1.68, 2.10],
                                        floor_power=96.0, spread=0.15)
        weak_mins, _ = rtp.solve_band(self.ELITE_MAXES, [1.30, 1.30, 1.68],
                                      floor_power=96.0, spread=0.15)
        self.assertGreater(sum(weak_mins), sum(strong_mins))

    def test_is_stable_for_this_fixture(self):
        # NOT a general fixed-point proof: solve_band drifts by one on some inputs when fed
        # its own output. See CanonicalShapeIsWhatMakesCaravansIdempotent for the real
        # invariant, which is that rewrite_text never hands it a data-derived shape.
        mins1, maxes1 = rtp.solve_band(self.ELITE_MAXES, self.POWERS,
                                       floor_power=96.0, spread=0.15)
        mins2, maxes2 = rtp.solve_band(maxes1, self.POWERS,
                                       floor_power=96.0, spread=0.15)
        self.assertEqual(maxes1, maxes2)
        self.assertEqual(mins1, mins2)


class PowerTable(unittest.TestCase):
    """The power table is read from the shipped config, never hardcoded."""

    def test_reads_the_live_battle_balance_config(self):
        table = rtp.load_power_table()
        # Values as shipped in Main/_Module/ModuleData/configs/battle_balance_config.json.
        self.assertAlmostEqual(table[0], 0.40)
        self.assertAlmostEqual(table[1], 0.66)
        self.assertAlmostEqual(table[3], 1.30)
        self.assertAlmostEqual(table[5], 2.10)

    def test_covers_every_tier_the_tier_function_can_return(self):
        table = rtp.load_power_table()
        for t in range(0, 11):
            self.assertIn(t, table, "no power for tier %d" % t)


class TroopLevelIndex(unittest.TestCase):
    """Levels come out of the shipped troop XML, so the tool cannot drift from the data."""

    def test_finds_known_troops(self):
        levels = rtp.load_troop_levels()
        self.assertEqual(levels["dunland_peasant"], 6)
        self.assertEqual(levels["caravan_guard_erebor"], 21)
        self.assertEqual(levels["veteran_caravan_guard_erebor"], 26)

    def test_index_is_not_empty(self):
        # A renamed folder would empty this silently and every downstream check would
        # then pass against nothing. Same class of bug as UPGRADE_INDEX_EMPTY.
        self.assertGreater(len(rtp.load_troop_levels()), 500)


class DocumentSafety(unittest.TestCase):
    """Byte-faithful IO, and never write XML that stopped parsing."""

    SAMPLE = (
        BOM + '<?xml version="1.0" encoding="utf-8"?>\r\n'
        '<MBPartyTemplates>\r\n'
        '\t<MBPartyTemplate id="dunland_raiders_raider_party_template">\r\n'
        '\t\t<stacks>\r\n'
        '\t\t\t<PartyTemplateStack min_value="8" max_value="50" troop="NPCCharacter.dunland_peasant" />\r\n'
        '\t\t\t<PartyTemplateStack min_value="4" max_value="50" troop="NPCCharacter.dunland_raider" />\r\n'
        '\t\t</stacks>\r\n'
        '\t</MBPartyTemplate>\r\n'
        '</MBPartyTemplates>\r\n'
    )
    LEVELS = {"dunland_peasant": 6, "dunland_raider": 11}

    def test_rewrite_preserves_bom_and_crlf(self):
        out = rtp.rewrite_text(self.SAMPLE, self.LEVELS,
                               rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        self.assertTrue(out.startswith(BOM), "the UTF-8 BOM must survive")
        self.assertNotIn("\n\n", out)
        self.assertEqual(out.count("\r\n"), self.SAMPLE.count("\r\n"),
                         "line endings must survive unchanged")

    def test_rewrite_still_parses(self):
        import xml.etree.ElementTree as ET
        out = rtp.rewrite_text(self.SAMPLE, self.LEVELS,
                               rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        ET.fromstring(out.lstrip(BOM))  # raises if the transform broke the document

    def test_refuses_to_return_unparsable_xml(self):
        broken = self.SAMPLE.replace("</MBPartyTemplates>", "")
        with self.assertRaises(ValueError):
            rtp.rewrite_text(broken, self.LEVELS,
                             rtp.load_power_table(), rtp.DEFAULT_BUDGETS)

    def test_rewrite_is_idempotent(self):
        once = rtp.rewrite_text(self.SAMPLE, self.LEVELS, rtp.load_power_table(),
                                rtp.DEFAULT_BUDGETS)[0]
        twice = rtp.rewrite_text(once, self.LEVELS, rtp.load_power_table(),
                                 rtp.DEFAULT_BUDGETS)[0]
        self.assertEqual(once, twice)

    def test_an_unknown_troop_is_reported_not_silently_skipped(self):
        # A stack naming a troop the index cannot resolve would otherwise be counted at
        # power 0 and drag the whole template's budget upward.
        _, _, unknown = rtp.rewrite_text(self.SAMPLE, {"dunland_peasant": 6},
                                         rtp.load_power_table(), rtp.DEFAULT_BUDGETS)
        self.assertIn("dunland_raider", unknown)




class CanonicalCaravanShape(unittest.TestCase):
    """All 17 caravan pairs must resolve to one composition, whatever they shipped with.

    Rohan, Dale and Dunland carry a `1/1` armed_trader stack where the other 14 carry `12/15`,
    so their caravans are roughly half the bodies for no stated reason. The tool normalises the
    shape rather than scaling each template's own, so the asymmetry cannot survive a run and
    cannot creep back in through a later hand-edit.
    """

    ODD_ROHAN = (
        BOM + '<?xml version="1.0" encoding="utf-8"?>\r\n'
        '<MBPartyTemplates>\r\n'
        '\t<MBPartyTemplate id="caravan_template_rohan">\r\n'
        '\t\t<stacks>\r\n'
        '\t\t\t<PartyTemplateStack min_value="1" max_value="1" troop="NPCCharacter.armed_trader_rohan" />\r\n'
        '\t\t\t<PartyTemplateStack min_value="5" max_value="10" troop="NPCCharacter.caravan_guard_rohan" />\r\n'
        '\t\t\t<PartyTemplateStack min_value="1" max_value="5" troop="NPCCharacter.veteran_caravan_guard_rohan" />\r\n'
        '\t\t</stacks>\r\n'
        '\t</MBPartyTemplate>\r\n'
        '</MBPartyTemplates>\r\n'
    )
    ODD_LEVELS = {"armed_trader_rohan": 16, "caravan_guard_rohan": 21,
                  "veteran_caravan_guard_rohan": 26}

    def test_role_is_resolved_by_troop_id_not_by_position(self):
        self.assertEqual(rtp.caravan_role("veteran_caravan_guard_erebor"), "veteran_caravan_guard")
        self.assertEqual(rtp.caravan_role("caravan_guard_erebor"), "caravan_guard")
        self.assertEqual(rtp.caravan_role("armed_trader_erebor"), "armed_trader")
        self.assertIsNone(rtp.caravan_role("dunland_peasant"))

    def test_veteran_is_matched_before_the_plain_guard(self):
        # "veteran_caravan_guard_x" ends with "caravan_guard_x", so a naive suffix or substring
        # test silently classifies every veteran stack as a plain guard and halves its weight.
        self.assertNotEqual(rtp.caravan_role("veteran_caravan_guard_rohan"), "caravan_guard")

    def test_an_odd_shaped_template_is_normalised(self):
        out, rows, unknown = rtp.rewrite_text(self.ODD_ROHAN, self.ODD_LEVELS,
                                              rtp.load_power_table(), rtp.DEFAULT_BUDGETS)
        self.assertEqual(unknown, [])
        self.assertEqual(len(rows), 1)
        self.assertNotIn('min_value="1" max_value="1"', out,
                         "the 1/1 armed_trader stack must not survive a run")
        self.assertGreaterEqual(rows[0]["new_min_power"],
                                rtp.DEFAULT_BUDGETS["caravan"]["floor_power"] - 2.0)

    def test_an_odd_shaped_template_lands_where_a_normal_one_does(self):
        # The asymmetry fix, stated as an equality: Rohan and Erebor must come out identical,
        # because their caravan troops are the same three tiers.
        normal = self.ODD_ROHAN.replace('min_value="1" max_value="1"',
                                        'min_value="12" max_value="15"')
        a = rtp.rewrite_text(self.ODD_ROHAN, self.ODD_LEVELS,
                             rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        b = rtp.rewrite_text(normal, self.ODD_LEVELS,
                             rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        self.assertEqual(a, b)

    def test_canonical_shape_is_independent_of_culture(self):
        a = rtp.canonical_shape_for(
            "caravan", ["armed_trader_x", "caravan_guard_x", "veteran_caravan_guard_x"])
        b = rtp.canonical_shape_for(
            "caravan", ["armed_trader_y", "caravan_guard_y", "veteran_caravan_guard_y"])
        self.assertEqual(a, b)
        self.assertEqual(len(a), 3)

    def test_elite_shape_differs_from_the_regular_one(self):
        roles = ["armed_trader_x", "caravan_guard_x", "veteran_caravan_guard_x"]
        self.assertNotEqual(rtp.canonical_shape_for("caravan", roles),
                            rtp.canonical_shape_for("elite_caravan", roles))

    def test_an_unrecognised_caravan_stack_is_refused(self):
        # Counting an unknown stack at zero would quietly inflate the rest of the template.
        with self.assertRaises(ValueError):
            rtp.canonical_shape_for("caravan", ["armed_trader_x", "dunland_peasant"])

    def test_rewrite_is_idempotent_on_an_odd_template(self):
        once = rtp.rewrite_text(self.ODD_ROHAN, self.ODD_LEVELS,
                                rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        twice = rtp.rewrite_text(once, self.ODD_LEVELS,
                                 rtp.load_power_table(), rtp.DEFAULT_BUDGETS)[0]
        self.assertEqual(once, twice)


class FlatBanditSolve(unittest.TestCase):
    """Bandit templates are flat by construction, and solving them that way is what makes the
    tool a fixed point.

    Every raider template is N stacks all sharing one `max_value`; every boss template is that
    plus a pinned `1/1` hero stack. Scaling each stack from the template's own current value
    makes the result depend on what the last run wrote, and when the power budget falls between
    two reachable values the tool oscillates: `gundabad_raiders_boss_party_template` flipped
    between 18 and 19 per stack, 103 and 108 power, on alternate runs. Solving for the single
    shared count instead depends only on the budget and the troop tiers, so it converges in one
    pass and stays there.
    """

    # gundabad_raiders_boss: pinned boss + four equal stacks.
    PINNED_MINS = [1, 5, 6, 4, 3]
    PINNED_MAXES = [1, 50, 50, 50, 50]
    PINNED_POWERS = [2.10, 1.68, 1.30, 1.30, 0.96]

    def test_a_pinned_stack_is_never_moved(self):
        out = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES, self.PINNED_POWERS, 105.0)
        self.assertEqual(out[0], 1, "the 1/1 boss hero stack must stay exactly one")

    def test_unpinned_stacks_stay_equal(self):
        out = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES, self.PINNED_POWERS, 105.0)
        self.assertEqual(len(set(out[1:])), 1, "the flat stacks must stay flat: %r" % out)

    def test_is_a_fixed_point(self):
        once = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES, self.PINNED_POWERS, 105.0)
        twice = rtp.solve_flat(self.PINNED_MINS, once, self.PINNED_POWERS, 105.0)
        thrice = rtp.solve_flat(self.PINNED_MINS, twice, self.PINNED_POWERS, 105.0)
        self.assertEqual(once, twice)
        self.assertEqual(twice, thrice)

    def test_converges_regardless_of_the_starting_values(self):
        # The whole point: the answer must not depend on what the previous run wrote.
        from_fifty = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES,
                                    self.PINNED_POWERS, 105.0)
        from_nineteen = rtp.solve_flat(self.PINNED_MINS, [1, 19, 19, 19, 19],
                                       self.PINNED_POWERS, 105.0)
        from_two = rtp.solve_flat(self.PINNED_MINS, [1, 2, 2, 2, 2],
                                  self.PINNED_POWERS, 105.0)
        self.assertEqual(from_fifty, from_nineteen)
        self.assertEqual(from_nineteen, from_two)

    def test_lands_near_the_budget(self):
        out = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES, self.PINNED_POWERS, 105.0)
        got = rtp.power_of(out, self.PINNED_POWERS)
        # Granularity is one shared step, so exact landing is not always possible.
        step = sum(self.PINNED_POWERS[1:])
        self.assertLessEqual(abs(got - 105.0), step / 2.0 + 0.01)

    def test_never_emits_a_max_below_its_own_min(self):
        # A tiny budget must still respect every stack's min_value.
        out = rtp.solve_flat(self.PINNED_MINS, self.PINNED_MAXES, self.PINNED_POWERS, 1.0)
        for mn, mx in zip(self.PINNED_MINS, out):
            self.assertLessEqual(mn, mx, "min %d > max %d" % (mn, mx))

    def test_a_non_flat_template_is_refused_rather_than_flattened(self):
        # Flattening a deliberately uneven template would silently rewrite its composition.
        with self.assertRaises(ValueError):
            rtp.solve_flat([1, 5, 6], [1, 50, 30], [2.10, 1.68, 1.30], 105.0)

    def test_a_raider_template_with_no_pinned_stack(self):
        mins = [8, 4, 3, 1]
        maxes = [50, 50, 50, 50]
        powers = [0.66, 0.96, 0.96, 1.30]
        out = rtp.solve_flat(mins, maxes, powers, 78.0)
        self.assertEqual(out, [20, 20, 20, 20])
        self.assertAlmostEqual(rtp.power_of(out, powers), 77.6, places=2)


class HeroTroopHandling(unittest.TestCase):
    """A hero-flagged troop is refused, not costed on the ordinary curve.

    `TaomMilitaryPowerModel` costs a hero completely differently: the tier is `Level / 4 + 1`
    rather than `ceil((Level - 5) / 5)`, and the multiplier is `HeroMultiplier` (1.5), never the
    mounted one. Feeding a hero through the troop formula would silently mis-budget its whole
    template. None of the 50 templates carries one today, so this is a guard against a future
    edit rather than a live fix, which is exactly why it needs a test: nothing else would notice.
    """

    def test_a_hero_flagged_troop_is_refused(self):
        table = rtp.load_power_table()
        levels = {"some_boss": 26, "some_grunt": 11}

        self.assertIsNone(
            rtp.troop_power("some_boss", levels, table, None, {"some_boss"}),
            "a hero must be refused, not costed on the troop curve")
        self.assertIsNotNone(
            rtp.troop_power("some_grunt", levels, table, None, {"some_boss"}),
            "a non-hero in the same roster must still cost normally")

    def test_a_template_naming_a_hero_is_skipped_and_reported(self):
        # Same treatment as an unresolved id: skip the whole template and name it, rather than
        # counting the hero at zero power and inflating every other stack's share of the budget.
        levels = {"dunland_peasant": 6, "dunland_raider": 11}
        _, rows, unknown = rtp.rewrite_text(
            DocumentSafety.SAMPLE, levels, rtp.load_power_table(), rtp.DEFAULT_BUDGETS,
            heroes={"dunland_raider"})

        self.assertIn("dunland_raider", unknown)
        self.assertEqual(rows, [], "the whole template must be skipped, not partially retuned")

    def test_no_troop_in_the_shipped_templates_is_currently_a_hero(self):
        # Pins the claim the tool's docstring makes. If this ever fails, the tool started
        # skipping a real template and the retune silently stopped covering it.
        heroes = rtp.load_hero_troops()
        self.assertIsInstance(heroes, set)
        _, rows, unknown = rtp.rewrite_text(
            rtp.TARGET_FILE.read_bytes().decode("utf-8"),
            rtp.load_troop_levels(), rtp.load_power_table(), rtp.DEFAULT_BUDGETS,
            rtp.load_mounted_troops(), "all", heroes)
        self.assertEqual(unknown, [], "every stack in the 50 templates must still be costable")
        self.assertEqual(len(rows), 50, "all 50 templates must still be retuned, got %d" % len(rows))


class CanonicalShapeIsWhatMakesCaravansIdempotent(unittest.TestCase):
    """The caravan path is a fixed point because of `canonical_shape_for`, NOT `solve_band`.

    `solve_flat` was deliberately rewritten to converge from any starting value. `solve_band` was
    not, and feeding its own output back in as `shape` drifts the mins by one on some inputs. That
    is safe today only because `rewrite_text` always hands it a constant table keyed on troop role.
    These two tests pin the real invariant and the real caveat, so nobody "tidies away" the comment
    warning about it.
    """

    POWERS = [1.30, 1.68, 2.10]
    ROLES = ["armed_trader_x", "caravan_guard_x", "veteran_caravan_guard_x"]

    def test_canonical_shape_never_depends_on_stored_counts(self):
        # The actual guarantee: the shape is a function of role alone, so what is on disk cannot
        # influence the next run's answer.
        shape = rtp.canonical_shape_for("caravan", self.ROLES)
        self.assertEqual(shape, rtp.canonical_shape_for("caravan", self.ROLES))
        self.assertEqual(shape, list(rtp.CANONICAL_CARAVAN_SHAPE["caravan"][r] for r in
                                     ("armed_trader", "caravan_guard", "veteran_caravan_guard")))

    def test_solve_band_from_the_canonical_shape_is_stable(self):
        # What the pipeline actually does, twice: same shape in, same numbers out.
        shape = rtp.canonical_shape_for("elite_caravan", self.ROLES)
        first = rtp.solve_band(shape, self.POWERS, floor_power=110.0, spread=0.15)
        second = rtp.solve_band(shape, self.POWERS, floor_power=110.0, spread=0.15)
        self.assertEqual(first, second)

    def test_solve_band_is_documented_as_unsafe_when_fed_its_own_output(self):
        # Pins the caveat rather than the bug: the docstring must keep warning about this, because
        # the failure it prevents (the gundabad_raiders_boss oscillation) already shipped once.
        self.assertIn("Never call this with a shape derived from the file",
                      rtp.solve_band.__doc__)


class RaiderMinWidening(unittest.TestCase):
    """Bandit mins are lowered so the early-game draw is not pinned near the floor.

    Vanilla gives a land bandit party the ratio `(0.4 + 0.8 * PlayerProgress) * U(0.2, 0.8)`, which
    at PlayerProgress 0 spans only 0.08 to 0.32. The spawn is `min + (max - min) * r`, so the early
    spread is `(max - min) * 0.24` and the floor is close to `min`. Cutting the endgame ceiling from
    200 to ~78 power therefore squeezed the early game twice: a lower floor AND a quarter of the
    spread. Lowering `min` cannot restore the old spread (that needs the old `max`), but it does drop
    the floor back to a vanilla-like number and recovers part of the range.
    """

    def test_target_is_a_fraction_of_the_max_sum(self):
        mins = [8, 4, 3, 1]
        maxes = [20, 20, 20, 20]

        out = rtp.scale_mins(mins, maxes, min_frac=0.125)

        self.assertAlmostEqual(sum(out) / sum(maxes), 0.125, delta=0.04)

    def test_keeps_relative_proportions(self):
        # The biggest stack must stay the biggest; the shape carries information.
        out = rtp.scale_mins([8, 4, 3, 1], [20, 20, 20, 20], min_frac=0.125)
        self.assertEqual(out, sorted(out, reverse=True))

    def test_never_drops_a_stack_to_zero(self):
        # A zero min is survivable (unlike a zero MAX, which deletes the troop), but a stack that
        # could always field at least one body should keep doing so.
        out = rtp.scale_mins([8, 4, 3, 1], [20, 20, 20, 20], min_frac=0.01)
        self.assertTrue(all(v >= 1 for v in out), out)

    def test_never_exceeds_its_own_max(self):
        # min > max drives the stack below its floor, because the engine fills to
        # `min + (max - min) * r` and a negative spread runs backwards.
        out = rtp.scale_mins([8, 4, 3, 1], [2, 2, 2, 2], min_frac=0.9)
        for mn, mx in zip(out, [2, 2, 2, 2]):
            self.assertLessEqual(mn, mx)

    def test_a_pinned_stack_is_left_alone(self):
        # The 1/1 boss hero stack must stay exactly one on both ends.
        out = rtp.scale_mins([1, 6, 5, 4], [1, 20, 20, 20], min_frac=0.125)
        self.assertEqual(out[0], 1)

    def test_is_idempotent(self):
        maxes = [20, 20, 20, 20]
        once = rtp.scale_mins([8, 4, 3, 1], maxes, min_frac=0.125)
        twice = rtp.scale_mins(once, maxes, min_frac=0.125)
        self.assertEqual(once, twice)

    def test_widening_lowers_the_early_floor_and_widens_the_range(self):
        # The property the change exists for, stated in the engine's own terms.
        maxes = [20, 20, 20, 20]
        before = [8, 4, 3, 1]
        after = rtp.scale_mins(before, maxes, min_frac=0.125)

        def early(mins):
            lo = sum(mins) + (sum(maxes) - sum(mins)) * 0.08
            hi = sum(mins) + (sum(maxes) - sum(mins)) * 0.32
            return lo, hi

        lo_b, hi_b = early(before)
        lo_a, hi_a = early(after)
        self.assertLess(lo_a, lo_b, "the early floor must come down")
        self.assertGreater(hi_a - lo_a, hi_b - lo_b, "the early range must widen")


if __name__ == "__main__":
    unittest.main()
