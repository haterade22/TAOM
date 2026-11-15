#!/usr/bin/env python3
"""Contract tests for tools/troop_purpose.py, the one rule that ties a troop's skills to its kit (#343).

Run:  python -m unittest tools.tests.test_troop_purpose

Synthetic data only. The rule: the Item0 class of the battle sets is the troop's main weapon, Item0
holds a real weapon, and the troop's own weapon skill values are reordered so the main class and
then the other carried classes hold the highest ones.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import troop_purpose as tp  # noqa: E402


def skills(oh=0, th=0, pol=0, bow=0, xbow=0, thr=0, ath=50, rid=20):
    return {"Athletics": ath, "Riding": rid, "OneHanded": oh, "TwoHanded": th, "Polearm": pol,
            "Bow": bow, "Crossbow": xbow, "Throwing": thr}


SWORD_SHIELD = {"Item0": "OneHanded", "Item1": "Shield"}
TWO_HANDER = {"Item0": "TwoHanded", "Item1": "OneHanded"}
ARCHER_2H = {"Item0": "Bow", "Item1": "Arrows", "Item2": "Arrows", "Item3": "TwoHanded"}
ARCHER_1H = {"Item0": "Bow", "Item1": "Arrows", "Item2": "Arrows", "Item3": "OneHanded"}


class MainClassTests(unittest.TestCase):
    def test_main_class_is_the_item0_class(self):
        kit = tp.kit_of([TWO_HANDER, TWO_HANDER])
        self.assertEqual(tp.main_class(kit), "TwoHanded")

    def test_a_shield_in_item0_is_not_a_main_class(self):
        kit = tp.kit_of([{"Item0": "Shield", "Item1": "OneHanded"}])
        self.assertIsNone(tp.main_class(kit))

    def test_most_sets_win_when_sets_disagree(self):
        kit = tp.kit_of([TWO_HANDER, TWO_HANDER, SWORD_SHIELD])
        self.assertEqual(tp.main_class(kit), "TwoHanded")
        self.assertTrue(tp.main_disputed(kit))

    def test_a_tie_between_sets_goes_to_the_higher_skill(self):
        kit = tp.kit_of([TWO_HANDER, {"Item0": "Polearm"}])
        self.assertEqual(tp.main_class(kit, skills(th=100, pol=120)), "Polearm")
        self.assertEqual(tp.main_class(kit, skills(th=130, pol=120)), "TwoHanded")

    def test_counts_are_battle_sets_carrying_the_class(self):
        kit = tp.kit_of([ARCHER_2H, ARCHER_1H])
        self.assertEqual(kit.counts, {"Bow": 2, "Arrows": 2, "TwoHanded": 1, "OneHanded": 1})
        self.assertTrue(kit.launcher_in_every_set)


class PurposeSkillsTests(unittest.TestCase):
    def test_the_player_report_lorien_archer_two_hander_follows_the_kit(self):
        # Bow and a two handed sword in every set: the troop fought with its weak TwoHanded.
        kit = tp.kit_of([ARCHER_2H, ARCHER_2H])
        out = tp.purpose_skills(skills(oh=260, th=235, pol=265, bow=320, xbow=85, thr=105), kit)
        self.assertEqual((out["Bow"], out["TwoHanded"]), (320, 265))
        self.assertEqual(sorted([out["OneHanded"], out["TwoHanded"], out["Polearm"]]), [235, 260, 265])

    def test_melee_values_stay_among_the_melee_skills(self):
        kit = tp.kit_of([SWORD_SHIELD])
        before = skills(oh=230, th=210, pol=235, bow=25, xbow=20, thr=80)
        out = tp.purpose_skills(before, kit)
        self.assertEqual(out["OneHanded"], 235)
        self.assertEqual(sorted(out[c] for c in tp.MELEE), sorted(before[c] for c in tp.MELEE))
        self.assertEqual(sum(out.values()), sum(before.values()))

    def test_athletics_riding_and_throwing_never_move(self):
        kit = tp.kit_of([{"Item0": "Polearm", "Item1": "Throwing"}])
        before = skills(oh=200, th=150, pol=100, thr=180, ath=99, rid=77)
        out = tp.purpose_skills(before, kit)
        self.assertEqual((out["Athletics"], out["Riding"], out["Throwing"]), (99, 77, 180))
        self.assertEqual(out["Polearm"], 200)

    def test_a_carried_secondary_comes_before_an_uncarried_class(self):
        kit = tp.kit_of([TWO_HANDER])
        out = tp.purpose_skills(skills(oh=100, th=120, pol=300), kit)
        self.assertEqual((out["TwoHanded"], out["OneHanded"], out["Polearm"]), (300, 120, 100))

    def test_carried_classes_order_by_the_sets_that_carry_them(self):
        a = {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}
        b = {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}
        c = {"Item0": "Bow", "Item1": "Arrows", "Item2": "Polearm"}
        out = tp.purpose_skills(skills(oh=90, th=50, pol=150, bow=200), tp.kit_of([a, b, c]))
        self.assertEqual((out["OneHanded"], out["Polearm"], out["TwoHanded"]), (150, 90, 50))

    def test_a_crossbowman_takes_the_higher_launcher_value(self):
        kit = tp.kit_of([{"Item0": "Crossbow", "Item1": "Bolts", "Item2": "OneHanded"}])
        out = tp.purpose_skills(skills(oh=165, bow=205, xbow=40), kit)
        self.assertEqual((out["Crossbow"], out["Bow"]), (205, 40))

    def test_a_pinned_value_is_left_where_it_is(self):
        # A ranged ladder cell owns its Bow value; the purpose rule must not move it off the cell.
        kit = tp.kit_of([ARCHER_1H])
        out = tp.purpose_skills(skills(oh=100, bow=120, xbow=160), kit, pinned={"Bow"})
        self.assertEqual((out["Bow"], out["Crossbow"]), (120, 160))

    def test_idempotent(self):
        kit = tp.kit_of([TWO_HANDER, {"Item0": "Polearm"}, ARCHER_1H])
        once = tp.purpose_skills(skills(oh=10, th=200, pol=200, bow=40, xbow=90, thr=5), kit)
        self.assertEqual(tp.purpose_skills(once, kit), once)

    def test_no_main_class_still_orders_carried_first(self):
        # calculate_skills callers that know only the carried set, not the slots.
        kit = tp.kit_from_classes({"TwoHanded"})
        out = tp.purpose_skills(skills(oh=310, th=160, pol=340), kit)
        self.assertEqual(out["TwoHanded"], 340)


class KitMismatchTests(unittest.TestCase):
    def test_purpose_skills_leave_no_same_kind_mismatch(self):
        kit = tp.kit_of([SWORD_SHIELD])
        before = skills(oh=230, th=210, pol=235)
        self.assertTrue(tp.kit_mismatches(before, kit))
        self.assertEqual(tp.kit_mismatches(tp.purpose_skills(before, kit), kit), [])

    def test_a_launcher_main_below_a_melee_skill_is_reported(self):
        kit = tp.kit_of([ARCHER_1H])
        found = tp.kit_mismatches(skills(oh=130, bow=105), kit)
        self.assertEqual([(f.cls, f.over) for f in found], [("Bow", "OneHanded")])
        self.assertTrue(found[0].cross_kind)

    def test_ties_are_not_a_mismatch(self):
        kit = tp.kit_of([SWORD_SHIELD])
        self.assertEqual(tp.kit_mismatches(skills(oh=240, pol=240), kit), [])

    def test_throwing_above_a_melee_main_is_allowed(self):
        # Vanilla skirmishers hold a sword at spawn and carry more Throwing than OneHanded
        # (battanian_mounted_skirmisher 150 over 130).
        kit = tp.kit_of([{"Item0": "OneHanded", "Item1": "Throwing", "Item2": "Shield"}])
        self.assertEqual(tp.kit_mismatches(skills(oh=130, thr=150), kit), [])

    def test_an_uncarried_class_above_a_carried_secondary_is_reported(self):
        kit = tp.kit_of([TWO_HANDER])
        found = tp.kit_mismatches(skills(oh=100, th=300, pol=200), kit)
        self.assertEqual([(f.cls, f.over) for f in found], [("OneHanded", "Polearm")])


class PurposeRaiseTests(unittest.TestCase):
    """After the upgrade clamp: raise, never lower, until the purpose order holds again."""

    def test_the_main_class_is_raised_to_a_clamped_secondary(self):
        kit = tp.kit_of([TWO_HANDER])
        before = skills(oh=150, th=220, pol=140)
        clamped = skills(oh=250, th=220, pol=150)
        out = tp.purpose_raise(clamped, before, kit)
        self.assertEqual((out["TwoHanded"], out["OneHanded"], out["Polearm"]), (250, 250, 150))

    def test_a_pinned_launcher_is_never_raised(self):
        kit = tp.kit_of([ARCHER_1H])
        out = tp.purpose_raise(skills(oh=90, bow=100, xbow=160), skills(oh=90, bow=100, xbow=40), kit,
                               pinned={"Bow"})
        self.assertEqual(out["Bow"], 100)

    def test_a_cross_kind_main_is_raised_only_when_the_clamp_caused_it(self):
        kit = tp.kit_of([SWORD_SHIELD])
        out = tp.purpose_raise(skills(oh=100, bow=200), skills(oh=100, bow=50), kit)
        self.assertEqual(out["OneHanded"], 200)
        self.assertEqual(tp.purpose_raise(skills(oh=100, bow=200), skills(oh=100, bow=200), kit)["OneHanded"], 100)

    def test_no_change_on_a_purpose_ordered_troop(self):
        kit = tp.kit_of([TWO_HANDER])
        s = skills(oh=200, th=220, pol=150)
        self.assertEqual(tp.purpose_raise(s, s, kit), s)


THROWER = {"Item0": "Throwing", "Item1": "Throwing", "Item2": "OneHanded", "Item3": "Shield"}


class ThrowerTests(unittest.TestCase):
    """A javelin in Item0 is a thrower's main weapon (vanilla's skirmisher factions spawn with it in
    hand, and the Marshal's Word thrower trial passed that way), so Throwing is a main class."""

    def test_a_thrown_weapon_in_item0_is_the_main_class(self):
        kit = tp.kit_of([THROWER, THROWER])
        self.assertEqual(tp.main_class(kit), "Throwing")
        self.assertIsNone(tp.item0_problem(THROWER))
        self.assertIsNone(tp.item0_swap_slot(THROWER, launcher_troop=False))
        self.assertEqual(tp.fix_item0(THROWER, False), (THROWER, None))

    def test_a_throwers_melee_is_ordered_carried_first_and_throwing_keeps_its_value(self):
        kit = tp.kit_of([THROWER])
        out = tp.purpose_skills(skills(oh=120, th=135, pol=130, thr=200), kit)
        self.assertEqual((out["OneHanded"], out["Throwing"]), (135, 200))
        self.assertEqual(sorted(out[c] for c in tp.MELEE), [120, 130, 135])

    def test_a_thrower_under_its_melee_skill_is_a_cross_kind_finding(self):
        kit = tp.kit_of([THROWER])
        found = tp.kit_mismatches(skills(oh=230, thr=55), kit)
        self.assertEqual([(f.kind, f.cls, f.over) for f in found], [("main", "Throwing", "OneHanded")])
        self.assertTrue(found[0].cross_kind)

    def test_the_raise_after_the_clamp_never_invents_a_throwing_value(self):
        kit = tp.kit_of([THROWER])
        out = tp.purpose_raise(skills(oh=250, thr=100), skills(oh=150, thr=100), kit)
        self.assertEqual(out["Throwing"], 100)


ARCHER_2H_SIDE = {"Item0": "Bow", "Item1": "Arrows", "Item2": "TwoHanded"}
ARCHER_1H_SIDE = {"Item0": "Bow", "Item1": "Arrows", "Item2": "OneHanded"}


class RoleTieTests(unittest.TestCase):
    """Classes of one kind that share a role (main or side) in an equal number of battle sets, and
    never sit in that role together, are alternatives: both take the higher of their values."""

    def test_the_marchwarden_side_tie_raises_the_lower_class(self):
        kit = tp.kit_of([ARCHER_2H_SIDE, ARCHER_1H_SIDE])
        self.assertEqual(kit.ties, (("OneHanded", "TwoHanded"),))
        out = tp.tie_raise(skills(oh=195, th=130, pol=125, bow=255), kit)
        self.assertEqual((out["OneHanded"], out["TwoHanded"], out["Polearm"], out["Bow"]),
                         (195, 195, 125, 255))

    def test_a_main_tie_raises_too(self):
        kit = tp.kit_of([{"Item0": "OneHanded", "Item1": "Shield"}, {"Item0": "Polearm", "Item1": "Shield"}])
        self.assertEqual(kit.ties, (("OneHanded", "Polearm"),))
        out = tp.tie_raise(skills(oh=33, pol=53, th=40), kit)
        self.assertEqual((out["OneHanded"], out["Polearm"], out["TwoHanded"]), (53, 53, 40))

    def test_a_main_tie_ranks_both_main_classes_above_the_side_weapon(self):
        # Dale's lake town line: a spear opens one set, a two-hander the other, a sword in both
        # side slots. Both openers are main; the sidearm stays below them and is not lifted.
        kit = tp.kit_of([{"Item0": "Polearm", "Item1": "OneHanded"}, {"Item0": "TwoHanded", "Item1": "OneHanded"}])
        out = tp.tie_raise(tp.purpose_skills(skills(oh=110, th=97, pol=120), kit), kit)
        self.assertEqual((out["Polearm"], out["TwoHanded"], out["OneHanded"]), (120, 120, 97))
        self.assertEqual(tp.purpose_raise(out, out, kit), out)

    def test_a_three_to_one_split_does_not_raise(self):
        kit = tp.kit_of([ARCHER_2H_SIDE] * 3 + [ARCHER_1H_SIDE])
        self.assertEqual(kit.ties, ())
        s = skills(oh=195, th=130, bow=255)
        self.assertEqual(tp.tie_raise(s, kit), s)

    def test_a_minority_tie_under_a_larger_class_does_not_raise(self):
        sets = [{"Item0": "Polearm"}] * 3 + [{"Item0": "OneHanded"}, {"Item0": "TwoHanded"}]
        self.assertEqual(tp.kit_of(sets).ties, ())

    def test_the_main_class_never_joins_a_side_tie(self):
        # A rotation: the sword opens two sets and is a side weapon in the third, where the
        # two-hander opens. The sidearms tie with each other, never up to the main class.
        sets = [{"Item0": "OneHanded", "Item1": "TwoHanded"}, {"Item0": "OneHanded", "Item1": "Polearm"},
                {"Item0": "TwoHanded", "Item1": "OneHanded"}]
        kit = tp.kit_of(sets)
        self.assertEqual(kit.ties, (("TwoHanded", "Polearm"),))
        out = tp.tie_raise(skills(oh=200, th=120, pol=100), kit)
        self.assertEqual((out["OneHanded"], out["TwoHanded"], out["Polearm"]), (200, 120, 120))

    def test_two_classes_carried_together_are_not_alternatives(self):
        both = {"Item0": "OneHanded", "Item1": "TwoHanded", "Item2": "Polearm"}
        self.assertEqual(tp.kit_of([both, both]).ties, ())

    def test_a_pinned_class_holds_its_group(self):
        kit = tp.kit_of([{"Item0": "Bow", "Item1": "Arrows"}, {"Item0": "Crossbow", "Item1": "Bolts"}])
        self.assertEqual(kit.ties, (("Bow", "Crossbow"),))
        s = skills(bow=200, xbow=40)
        self.assertEqual(tp.tie_raise(s, kit, pinned={"Bow"}), s)

    def test_idempotent_and_a_known_classes_kit_has_no_tie(self):
        kit = tp.kit_of([ARCHER_2H_SIDE, ARCHER_1H_SIDE])
        once = tp.tie_raise(skills(oh=195, th=130), kit)
        self.assertEqual(tp.tie_raise(once, kit), once)
        self.assertEqual(tp.kit_from_classes({"OneHanded", "TwoHanded"}).ties, ())


class Item0Tests(unittest.TestCase):

    def test_a_set_of_thrown_weapons_only_is_left_alone(self):
        slots = {"Item0": "Throwing", "Item1": "Throwing", "Item2": "Shield"}
        self.assertIsNone(tp.item0_problem(slots))
        self.assertIsNone(tp.item0_swap_slot(slots, launcher_troop=False))

    def test_a_melee_item0_is_the_designers_choice(self):
        slots = {"Item0": "OneHanded", "Item1": "Polearm", "Item2": "Shield"}
        self.assertIsNone(tp.item0_problem(slots))
        self.assertIsNone(tp.item0_swap_slot(slots, launcher_troop=False))

    def test_shield_and_ammunition_never_open_the_kit(self):
        self.assertIn("Shield", tp.item0_problem({"Item0": "Shield", "Item1": "Polearm"}))
        self.assertIn("Arrows", tp.item0_problem({"Item0": "Arrows", "Item1": "Bow"}))
        self.assertIsNone(tp.launcher_problem({"Item0": "Arrows", "Item1": "Bow"}, False))

    def test_an_archer_moves_the_launcher_into_item0(self):
        slots = {"Item0": "Arrows", "Item1": "OneHanded", "Item2": "Bow"}
        self.assertEqual(tp.item0_swap_slot(slots, launcher_troop=True), "Item2")
        self.assertEqual(tp.item0_swap_slot(slots, launcher_troop=False), "Item1")

    def test_an_archer_with_a_sword_in_item0_is_reported_not_reordered(self):
        slots = {"Item0": "OneHanded", "Item1": "Bow", "Item2": "Arrows"}
        # Vanilla ships this layout, and which weapon opens the kit is a designer's call: it is
        # the launcher reason of the warning, never the shield and ammunition error.
        self.assertIsNone(tp.item0_problem(slots))
        self.assertIn("launcher", tp.launcher_problem(slots, launcher_troop=True))
        self.assertIsNone(tp.launcher_problem(slots, launcher_troop=False))
        self.assertIsNone(tp.item0_swap_slot(slots, launcher_troop=True))

    def test_a_launcher_troop_needs_the_group_and_a_launcher_in_every_set(self):
        both = tp.kit_of([ARCHER_1H, ARCHER_2H])
        self.assertTrue(tp.is_launcher_troop(both, "Ranged"))
        self.assertTrue(tp.is_launcher_troop(both, "HorseArcher"))
        self.assertFalse(tp.is_launcher_troop(both, "Cavalry"))
        self.assertFalse(tp.is_launcher_troop(tp.kit_of([ARCHER_1H, SWORD_SHIELD]), "Ranged"))


LANCE_SET = {"Item0": "OneHanded", "Item1": "Shield", "Item2": "Polearm"}
COUCH_AT_2 = frozenset({"Item2"})


def plan(sets, couchable=None, group="Cavalry", mounted=True, named=False):
    return tp.main_weapon_slots(sets, couchable if couchable is not None else [COUCH_AT_2] * len(sets),
                                group, mounted, named)


class NamesAPolearmTests(unittest.TestCase):
    def test_a_word_that_starts_with_the_stem_matches(self):
        for text in ("Linhir Spearman", "Serelond Pikeman", "Falathrim Lancer", "Easterling Halberdier",
                     "Goblin Pike-Reaver", "Pinnath Gelin Spearwarden", "gondor_ca_pikewarden"):
            self.assertTrue(tp.names_a_polearm(text), text)

    def test_a_veteran_prefix_in_an_id_matches(self):
        self.assertTrue(tp.names_a_polearm("urukhai_veteranpikeman"))
        self.assertTrue(tp.names_a_polearm("urukhai_veteranspearman"))

    def test_a_stem_inside_another_word_does_not_match(self):
        for text in ("Spiked Club Brute", "Balance Warden", "Vigilance Guard", "Ambulance"):
            self.assertFalse(tp.names_a_polearm(text), text)

    def test_any_of_the_texts_may_carry_the_word(self):
        self.assertTrue(tp.names_a_polearm("Dalian Guardsman", "dale_spearman"))
        self.assertFalse(tp.names_a_polearm("Dalian Guardsman", "dale_guardsman"))


class MainWeaponSlotTests(unittest.TestCase):
    def test_a_mounted_lancer_with_a_sword_in_item0_trades_the_sword_for_the_lance(self):
        got = plan([LANCE_SET, LANCE_SET])
        self.assertEqual((got.rule, got.swaps, got.left), ("A", ((0, "Item2"), (1, "Item2")), ""))
        after = tp.apply_slot_swaps([LANCE_SET, LANCE_SET], got.swaps)
        self.assertEqual(after[0], {"Item0": "Polearm", "Item1": "Shield", "Item2": "OneHanded"})
        self.assertEqual(tp.main_class(tp.kit_of(after)), "Polearm")

    def test_only_the_two_items_trade_slots(self):
        row = {"Item0": "TwoHanded", "Item1": "Shield", "Item2": "Throwing", "Item3": "Polearm"}
        after = tp.apply_slot_swaps([row], plan([row], [frozenset({"Item3"})]).swaps)[0]
        self.assertEqual(after, {"Item0": "Polearm", "Item1": "Shield", "Item2": "Throwing", "Item3": "TwoHanded"})

    def test_the_lance_that_can_couch_is_the_one_exchanged(self):
        row = {"Item0": "OneHanded", "Item1": "Polearm", "Item2": "Polearm"}
        self.assertEqual(plan([row], [frozenset({"Item2"})]).swaps, ((0, "Item2"),))

    def test_a_rider_whose_spear_cannot_be_couched_is_left_and_reported(self):
        got = plan([LANCE_SET], [frozenset()])
        self.assertEqual((got.rule, got.swaps), ("", ()))
        self.assertIn("cannot be couched", got.left)

    def test_a_rider_with_a_lance_in_some_sets_only_is_left_and_reported(self):
        got = plan([LANCE_SET, {"Item0": "OneHanded", "Item1": "Shield"}], [COUCH_AT_2, frozenset()])
        self.assertEqual((got.rule, got.swaps), ("", ()))
        self.assertIn("not in every battle set", got.left)

    def test_a_horse_archer_is_left_and_reported(self):
        got = plan([LANCE_SET], group="HorseArcher")
        self.assertEqual((got.rule, got.swaps), ("", ()))
        self.assertIn("HorseArcher", got.left)

    def test_a_cavalry_troop_with_no_horse_in_a_set_is_not_a_lancer(self):
        got = plan([LANCE_SET], mounted=False)
        self.assertEqual((got.rule, got.swaps, got.left), ("", (), ""))

    def test_a_set_that_already_opens_with_the_lance_is_not_swapped(self):
        lance_first = {"Item0": "Polearm", "Item1": "Shield", "Item2": "OneHanded"}
        got = plan([lance_first, LANCE_SET], [frozenset({"Item0"}), COUCH_AT_2])
        self.assertEqual((got.rule, got.swaps), ("A", ((1, "Item2"),)))

    def test_a_troop_named_spearman_with_a_sword_in_item0_trades_for_its_spear(self):
        row = {"Item0": "OneHanded", "Item1": "Polearm", "Item2": "Shield"}
        got = plan([row], [frozenset()], group="Infantry", mounted=False, named=True)
        self.assertEqual((got.rule, got.swaps), ("B", ((0, "Item1"),)))

    def test_a_named_troop_with_the_polearm_in_some_sets_only_is_left_and_reported(self):
        got = plan([{"Item0": "OneHanded", "Item1": "Polearm"}, {"Item0": "OneHanded", "Item1": "Shield"}],
                   group="Infantry", mounted=False, named=True)
        self.assertEqual((got.rule, got.swaps), ("", ()))
        self.assertIn("not in every battle set", got.left)

    def test_a_swordsman_with_a_spare_spear_is_no_case(self):
        row = {"Item0": "OneHanded", "Item1": "Shield", "Item2": "Polearm"}
        self.assertEqual(plan([row], group="Infantry", mounted=False), tp.SlotPlan())

    def test_a_launcher_or_thrown_weapon_in_item0_leaves_the_troop_and_reports_it(self):
        for first in ("Bow", "Throwing"):
            row = {"Item0": first, "Item1": "Polearm"}
            got = plan([row, {"Item0": "OneHanded", "Item1": "Polearm"}], [frozenset({"Item1"})] * 2)
            self.assertEqual((got.rule, got.swaps), ("", ()))
            self.assertIn(f"Item0 holds {first}", got.left)

    def test_a_second_pass_plans_nothing(self):
        once = tp.apply_slot_swaps([LANCE_SET], plan([LANCE_SET]).swaps)
        again = tp.main_weapon_slots(once, [frozenset({"Item0"})], "Cavalry", True, False)
        self.assertEqual(again, tp.SlotPlan())

    def test_the_exchange_leaves_the_purpose_order_stable(self):
        after = tp.apply_slot_swaps([LANCE_SET], plan([LANCE_SET]).swaps)
        kit = tp.kit_of(after)
        ordered = tp.purpose_skills(skills(oh=200, pol=150, th=100), kit)
        self.assertEqual((ordered["Polearm"], ordered["OneHanded"]), (200, 150))
        self.assertEqual(tp.purpose_skills(ordered, kit), ordered)
        self.assertEqual(tp.kit_mismatches(ordered, kit), [])


class TroopMainWeaponTests(unittest.TestCase):
    """troop_main_weapon: the glue the writer and the validator share, from item ids to a SlotPlan."""
    CLASSES = {"sword": "OneHanded", "lance": "Polearm", "shield": "Shield", "bow": "Bow", "arrows": "Arrows"}
    RIDER = {"Item0": "sword", "Item1": "shield", "Item2": "lance", "Horse": "steed"}

    def run_plan(self, rows, couch=("lance",), group="Cavalry", names=("Rider", "rider_x")):
        return tp.troop_main_weapon(rows, self.CLASSES, set(couch), group, names)

    def test_a_mounted_lance_rider_gets_the_exchange_and_its_rows_after_it(self):
        plan, p2, rows = self.run_plan([self.RIDER, self.RIDER])
        self.assertEqual((plan.rule, plan.swaps, p2), ("A", ((0, "Item2"), (1, "Item2")), [None, None]))
        self.assertEqual(rows[0], dict(self.RIDER))     # the rows are the ones after the Item0 fix only

    def test_couchability_is_the_callers_set_and_not_the_class(self):
        plan, _, _ = self.run_plan([self.RIDER], couch=())
        self.assertEqual((plan.rule, plan.swaps), ("", ()))
        self.assertIn("cannot be couched", plan.left)

    def test_without_a_couch_set_nothing_is_planned(self):
        plan, _, _ = tp.troop_main_weapon([self.RIDER], self.CLASSES, None, "Cavalry", ("Rider",))
        self.assertEqual(plan, tp.SlotPlan())

    def test_a_missing_horse_in_one_set_is_not_mounted(self):
        plan, _, _ = self.run_plan([self.RIDER, {"Item0": "sword", "Item2": "lance"}])
        self.assertEqual(plan, tp.SlotPlan())

    def test_the_shield_fix_comes_first_and_the_lance_is_found_after_it(self):
        row = {"Item0": "shield", "Item1": "lance", "Item2": "sword", "Horse": "steed"}
        plan, p2, rows = self.run_plan([row])
        self.assertEqual(p2, ["Item1"])
        self.assertEqual(rows[0]["Item0"], "lance")
        self.assertEqual(plan, tp.SlotPlan())            # Item0 now holds the lance already

    def test_the_name_or_the_id_may_name_the_polearm(self):
        row = {"Item0": "sword", "Item1": "lance", "Item2": "shield"}
        plan, _, _ = self.run_plan([row], couch=(), group="Infantry", names=("Guard", "dale_spearman"))
        self.assertEqual((plan.rule, plan.swaps), ("B", ((0, "Item1"),)))


if __name__ == "__main__":
    unittest.main()
