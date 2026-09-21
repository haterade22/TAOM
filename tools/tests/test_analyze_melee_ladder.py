#!/usr/bin/env python3
"""Tests for `tools/analyze_melee_ladder.py`, the read-only melee ladder report.

Everything here runs on synthetic data. The report itself needs the live Bannerlord install,
which CI does not have, so the analysis functions are written as pure functions over small
structures and tested that way. A test that needed the install would be skipped in CI, and a
skipped test proves nothing.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as ml  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402


# --- helpers ------------------------------------------------------------------------------


def priced(item_id, name, template="OneHandedSword", swing=0, thrust=0, blade="blade",
           culture="x", raw_swing=20.0, raw_thrust=8.0):
    """A `Priced` with the given headline numbers, via a single all-capable usage.

    The raw speeds default to mid-range real values rather than zero, because the ladder
    analysis ranks on sustained damage and a zero speed means an infinite cycle. Tests that
    care about speed pass their own.
    """
    strikes = set()
    if swing:
        strikes.add("swing")
    if thrust:
        strikes.add("thrust")
    usage = cata.Usage(
        description="D",
        is_primary=True,
        features=(),
        usage_set="set",
        strikes=frozenset(strikes),
        stats=md.WeaponStats(
            swing_damage=swing, thrust_damage=thrust,
            raw_swing_speed=raw_swing, raw_thrust_speed=raw_thrust,
        ),
    )
    return cata.Priced(
        item=cata.Item(item_id, name, culture, template, ((blade, "Blade", 100),), "src"),
        usages=(usage,),
        blade_piece=blade,
        tier=3,
    )


def troop(tid, level, culture, items):
    return ml.Troop(tid, tid, level, culture, "troops_x.xml", frozenset(items))


# --- numbered lines -------------------------------------------------------------------------


@pytest.mark.parametrize(
    "name,expected",
    [
        ("[Mordor] Uruk Sword III", ("[Mordor] Uruk Sword", "III")),
        ("[Dale] Dale Sword I", ("[Dale] Dale Sword", "I")),
        ("[Erebor] Two-Handed Axe VIII", ("[Erebor] Two-Handed Axe", "VIII")),
        ("Anduril", None),
        ("[Gondor] Sword of the Tower", None),
    ],
)
def test_line_key_splits_a_trailing_numeral(name, expected):
    assert ml.line_key(name) == expected


def test_a_numeral_only_counts_as_its_own_word():
    """`Sword II` is a line step. `Sword MII` is not, and neither is a bare `I` inside a word."""
    assert ml.line_key("Blade MII") is None
    assert ml.line_key("Blade II") == ("Blade", "II")


def test_build_lines_groups_and_sorts_by_numeral_not_by_id():
    items = {
        "s_c": priced("s_c", "[X] Sword III", swing=40),
        "s_a": priced("s_a", "[X] Sword I", swing=60),
        "s_b": priced("s_b", "[X] Sword II", swing=50),
        "lone": priced("lone", "Anduril", swing=99),
    }
    lines = ml.build_lines(items)
    assert set(lines) == {"[X] Sword"}
    assert [m.numeral for m in lines["[X] Sword"]] == ["I", "II", "III"]


def test_line_regressions_finds_a_line_that_gets_worse_as_it_climbs():
    """The Dale Sword shape: I=56, II=53, III=48."""
    items = {
        "a": priced("a", "[Dale] Dale Sword I", swing=56, thrust=44),
        "b": priced("b", "[Dale] Dale Sword II", swing=53, thrust=39),
        "c": priced("c", "[Dale] Dale Sword III", swing=48, thrust=35),
    }
    breaks = ml.line_regressions(ml.build_lines(items))
    assert {(b.stat, b.delta) for b in breaks} == {
        ("swing", -3), ("swing", -5), ("thrust", -5), ("thrust", -4),
    }
    # Worst first.
    assert breaks[0].delta == -5


def test_line_regressions_is_silent_on_a_line_that_climbs():
    items = {
        "a": priced("a", "[X] Axe I", swing=51),
        "b": priced("b", "[X] Axe II", swing=53),
        "c": priced("c", "[X] Axe III", swing=62),
    }
    assert ml.line_regressions(ml.build_lines(items)) == []


def test_a_missing_attack_is_not_treated_as_a_regression():
    """A spear has no swing at all. Zero must mean "cannot", not "does nothing"."""
    items = {
        "a": priced("a", "[X] Spear I", thrust=45),
        "b": priced("b", "[X] Spear II", thrust=49),
    }
    assert ml.line_regressions(ml.build_lines(items)) == []


def test_each_attack_is_judged_separately():
    """Trading swing for thrust up the line is a design choice, not a break, on the stat that rose."""
    items = {
        "a": priced("a", "[X] Bill I", swing=90, thrust=30),
        "b": priced("b", "[X] Bill II", swing=80, thrust=50),
    }
    breaks = ml.line_regressions(ml.build_lines(items))
    assert [(b.stat, b.delta) for b in breaks] == [("swing", -10)]


def test_id_order_conflicts_flags_a_line_whose_letters_disagree_with_its_numerals():
    items = {
        "x_b": priced("x_b", "[X] Sword I", swing=10),
        "x_a": priced("x_a", "[X] Sword II", swing=20),
    }
    conflicts = ml.id_order_conflicts(ml.build_lines(items))
    assert [c[0] for c in conflicts] == ["[X] Sword"]


def test_id_order_conflicts_is_quiet_when_letters_and_numerals_agree():
    items = {
        "x_a": priced("x_a", "[X] Sword I", swing=10),
        "x_b": priced("x_b", "[X] Sword II", swing=20),
    }
    assert ml.id_order_conflicts(ml.build_lines(items)) == []


# --- placement ------------------------------------------------------------------------------


def test_a_weapon_is_anchored_to_its_lowest_wearer():
    """derive_armor_tiers' rule: one militiaman carrying it makes it a low-tier weapon."""
    items = {"w": priced("w", "[X] Sword I", swing=60)}
    troops = [
        troop("militia", 6, "x", ["w"]),     # tier 1
        troop("elite", 51, "x", ["w"]),      # tier 10
    ]
    placements = ml.place(items, troops)
    assert placements["w"].anchor_tier == 1
    assert placements["w"].span == 9
    assert placements["w"].min_level == 6
    assert placements["w"].max_level == 51


def test_place_ignores_items_that_are_not_priced_melee():
    items = {"w": priced("w", "[X] Sword I", swing=60)}
    placements = ml.place(items, [troop("t", 10, "x", ["w", "some_bow", "some_armour"])])
    assert set(placements) == {"w"}


# --- inversions -----------------------------------------------------------------------------


def test_tier_inversion_is_found_when_a_higher_troop_carries_a_weaker_weapon():
    items = {
        "good": priced("good", "[X] Spear I", template="Polearm", thrust=210),
        "bad": priced("bad", "[X] Spear II", template="Polearm", thrust=54),
    }
    troops = [
        troop("militia", 11, "mordor", ["good"]),   # tier 2
        troop("knight", 41, "mordor", ["bad"]),     # tier 8
    ]
    found = ml.tier_inversions(items, troops)
    assert len(found) == 1
    assert found[0].high.id == "knight"
    assert found[0].low.id == "militia"
    assert found[0].damage_deficit == 156        # one blow
    assert found[0].deficit > 100                # and sustained, which is what ranks it


def test_inversions_do_not_cross_cultures_or_weapon_classes():
    items = {
        "good": priced("good", "[X] Spear I", template="Polearm", thrust=200),
        "bad": priced("bad", "[Y] Mace I", template="Mace", swing=50),
    }
    # Different culture AND different class: neither is a comparison worth making.
    troops = [
        troop("militia", 11, "mordor", ["good"]),
        troop("knight", 41, "gondor", ["bad"]),
    ]
    assert ml.tier_inversions(items, troops) == []


def test_adjacent_tiers_are_not_an_inversion():
    """One tier apart is usually a deliberate sidegrade, so the default gap is two."""
    items = {
        "good": priced("good", "a", template="Mace", swing=90),
        "bad": priced("bad", "b", template="Mace", swing=40),
    }
    troops = [troop("low", 11, "x", ["good"]), troop("high", 16, "x", ["bad"])]  # tiers 2 and 3
    assert ml.tier_inversions(items, troops) == []
    assert len(ml.tier_inversions(items, troops, min_gap=1)) == 1


def test_min_deficit_filters_rounding_noise():
    """One damage apart at the same speed is under a DPS point, so a 5-DPS floor drops it."""
    items = {
        "good": priced("good", "a", template="Mace", swing=55),
        "bad": priced("bad", "b", template="Mace", swing=54),
    }
    troops = [troop("low", 11, "x", ["good"]), troop("high", 41, "x", ["bad"])]
    found = ml.tier_inversions(items, troops, min_deficit=0.5)
    assert len(found) == 1
    assert found[0].deficit < 1.0
    assert ml.tier_inversions(items, troops, min_deficit=5) == []


def test_one_row_per_troop_and_class_showing_its_worst_case():
    """A badly-armed capstone loses to every tier below it; that is one problem, not five."""
    # A clean ladder below (100 -> 150 -> 180) with one broken capstone on top, so the
    # capstone is the only inverted troop and it loses to all three.
    items = {
        "w100": priced("w100", "a", template="Mace", swing=100),
        "w150": priced("w150", "b", template="Mace", swing=150),
        "w180": priced("w180", "c", template="Mace", swing=180),
        "bad": priced("bad", "d", template="Mace", swing=40),
    }
    troops = [
        troop("t2", 11, "x", ["w100"]),
        troop("t3", 16, "x", ["w150"]),
        troop("t4", 21, "x", ["w180"]),
        troop("capstone", 51, "x", ["bad"]),
    ]
    found = ml.tier_inversions(items, troops)
    assert len(found) == 1
    assert found[0].high.id == "capstone"
    # Measured against the WORST case (the 180 weapon), not the nearest.
    assert found[0].damage_deficit == 140
    assert found[0].low_item == "w180"


def test_a_troop_is_judged_on_its_best_weapon_of_that_class():
    items = {
        "weak": priced("weak", "a", template="Mace", swing=30),
        "strong": priced("strong", "b", template="Mace", swing=120),
        "rival": priced("rival", "c", template="Mace", swing=100),
    }
    troops = [
        troop("low", 11, "x", ["rival"]),
        troop("high", 41, "x", ["weak", "strong"]),  # carries both; the 120 is what counts
    ]
    assert ml.tier_inversions(items, troops) == []


# --- envelope and fan-out ---------------------------------------------------------------------


def test_envelope_summarises_per_class():
    items = {
        "a": priced("a", "a", template="Mace", swing=10),
        "b": priced("b", "b", template="Mace", swing=50),
        "c": priced("c", "c", template="Mace", swing=90),
        "d": priced("d", "d", template="OneHandedSword", swing=70),
    }
    env = ml.envelope(items)
    assert env["Mace"].low == 10
    assert env["Mace"].median == 50
    assert env["Mace"].high == 90
    assert env["Mace"].count == 3
    assert env["OneHandedSword"].count == 1


def test_envelope_skips_weapons_with_no_usable_attack():
    items = {"a": priced("a", "a", template="Mace", swing=0, thrust=0)}
    assert ml.envelope(items) == {}


def test_blade_fan_out_reports_what_one_edit_moves():
    """Damage lives on the blade, so a shared blade is a shared edit."""
    items = {
        "w1": priced("w1", "a", swing=50, blade="shared"),
        "w2": priced("w2", "b", swing=60, blade="shared"),
        "w3": priced("w3", "c", swing=70, blade="own"),
    }
    troops = [troop("t1", 6, "x", ["w1"]), troop("t2", 41, "x", ["w2"])]
    fan = ml.blade_fan_out(items, ml.place(items, troops))
    assert fan["shared"].fan == 2
    assert fan["shared"].items == ["w1", "w2"]
    assert fan["shared"].troops == {"t1", "t2"}
    assert fan["shared"].tiers == {1, 8}
    assert fan["own"].fan == 1
    assert fan["own"].troops == set()


# --- troop loading ----------------------------------------------------------------------------


def test_load_troops_reads_both_equipment_casings_and_skips_civilian(tmp_path):
    """Inline rosters spell it lowercase, standalone files uppercase. Missing one hides a category."""
    d = tmp_path / "troops"
    d.mkdir()
    (d / "troops_x.xml").write_text(
        """<NPCCharacters>
             <NPCCharacter id="t1" level="21" name="{=k}Tee One" culture="Culture.gondor">
               <Equipments>
                 <EquipmentRoster>
                   <equipment slot="Item0" id="Item.lower_case_sword" />
                 </EquipmentRoster>
                 <EquipmentRoster>
                   <Equipment slot="Item0" id="Item.upper_case_axe" />
                 </EquipmentRoster>
                 <EquipmentRoster equipmentType="Civilian">
                   <equipment slot="Item0" id="Item.civilian_dagger" />
                 </EquipmentRoster>
               </Equipments>
             </NPCCharacter>
           </NPCCharacters>""",
        encoding="utf-8",
    )
    troops = ml.load_troops(d)
    assert len(troops) == 1
    got = troops[0]
    assert got.items == {"lower_case_sword", "upper_case_axe"}
    assert "civilian_dagger" not in got.items
    assert got.name == "Tee One"
    assert got.culture == "gondor"
    assert got.level == 21
    assert got.tier == 4  # clamp(ceil((21-5)/5), 0, 10)


def test_load_troops_reports_a_missing_directory_rather_than_returning_nothing():
    with pytest.raises(ml.AnalysisError):
        ml.load_troops(Path("no/such/troops"))


def test_a_troop_with_no_level_does_not_crash_the_tier_maths(tmp_path):
    d = tmp_path / "troops"
    d.mkdir()
    (d / "troops_x.xml").write_text(
        '<NPCCharacters><NPCCharacter id="t" name="n"><Equipments><EquipmentRoster>'
        '<equipment slot="Item0" id="Item.w" /></EquipmentRoster></Equipments>'
        "</NPCCharacter></NPCCharacters>",
        encoding="utf-8",
    )
    got = ml.load_troops(d)[0]
    assert got.level == 0
    assert got.tier == 0


# --- DPS-aware ladder analysis ----------------------------------------------------------------


def dps_priced(item_id, name, template="OneHandedSword", swing=0, thrust=0,
               raw_swing=20.0, raw_thrust=8.0, blade="blade"):
    """A `Priced` with explicit raw speeds, so the cycle time is controllable."""
    strikes = set()
    if swing:
        strikes.add("swing")
    if thrust:
        strikes.add("thrust")
    usage = cata.Usage(
        "D", True, (), "set", frozenset(strikes),
        md.WeaponStats(
            swing_damage=swing, thrust_damage=thrust,
            raw_swing_speed=raw_swing, raw_thrust_speed=raw_thrust,
        ),
    )
    return cata.Priced(
        item=cata.Item(item_id, name, "x", template, ((blade, "Blade", 100),), "src"),
        usages=(usage,), blade_piece=blade, tier=3,
    )


def test_a_step_that_loses_damage_but_holds_dps_is_not_a_regression():
    """The Dale Sword shape. Ranking on one blow alone would call this a defect."""
    items = {
        "a": dps_priced("a", "[X] Sword I", swing=56, raw_swing=18.9),
        "b": dps_priced("b", "[X] Sword II", swing=48, raw_swing=22.9),
    }
    breaks = ml.line_regressions(ml.build_lines(items))
    assert len(breaks) == 1          # damage did fall, so it is still reported
    assert breaks[0].real is False   # but output did not, so it is a trade, not a fault
    assert breaks[0].dps_delta > 0


def test_a_step_that_loses_both_is_a_real_regression():
    items = {
        "a": dps_priced("a", "[X] Sword I", swing=100, raw_swing=20.0),
        "b": dps_priced("b", "[X] Sword II", swing=60, raw_swing=18.0),
    }
    breaks = ml.line_regressions(ml.build_lines(items))
    assert len(breaks) == 1
    assert breaks[0].real is True
    assert breaks[0].dps_delta < 0


def test_a_dps_difference_inside_the_tolerance_is_not_called_a_fault():
    """Damage drops by one and the speed almost exactly makes it back."""
    items = {
        "a": dps_priced("a", "[X] Sword I", swing=50, raw_swing=20.0),
        "b": dps_priced("b", "[X] Sword II", swing=49, raw_swing=20.42),
    }
    breaks = ml.line_regressions(ml.build_lines(items))
    assert breaks[0].delta == -1                              # one blow did drop
    assert abs(breaks[0].dps_delta) < ml.DPS_TOLERANCE        # output barely moved
    assert breaks[0].real is False                            # so it is not a fault


def test_inversions_rank_on_sustained_damage_not_one_blow():
    """A slow heavy weapon can look fine per blow and still be the weaker kit."""
    items = {
        "heavy_slow": dps_priced("heavy_slow", "a", template="Mace", swing=86, raw_swing=5.0),
        "light_fast": dps_priced("light_fast", "b", template="Mace", swing=81, raw_swing=25.0),
    }
    troops = [
        troop("low", 11, "x", ["light_fast"]),    # tier 2, smaller blow
        troop("high", 41, "x", ["heavy_slow"]),   # tier 8, bigger blow
    ]
    found = ml.tier_inversions(items, troops)
    assert len(found) == 1
    # The higher troop's weapon hits HARDER per blow ...
    assert found[0].high_damage > found[0].low_damage
    # ... and is still the inversion, because it sustains far less.
    assert found[0].deficit > 0
    assert found[0].high_dps < found[0].low_dps


def test_envelope_can_summarise_either_metric():
    items = {
        "a": dps_priced("a", "a", template="Mace", swing=100, raw_swing=10.0),
        "b": dps_priced("b", "b", template="Mace", swing=50, raw_swing=40.0),
    }
    by_damage = ml.envelope(items, "best_damage")
    by_dps = ml.envelope(items, "best_dps")
    assert by_damage["Mace"].high == 100
    # The 50-damage weapon sustains more, so the DPS ceiling comes from the other one.
    assert by_dps["Mace"].high == int(round(items["b"].best_dps))


def test_mode_choice_agrees_between_damage_and_dps():
    """Pinned because `Priced.swing_dps` reads the mode `swing_damage` chose.

    Measured across all 364 Armory weapons on 2026-09-20: 0 disagreements. If a future weapon
    breaks this, reporting damage from one mode and DPS from another would describe a weapon
    nobody can actually wield, and this test is the warning.
    """
    slow_hard = cata.Usage("Hard", True, (), "s1", frozenset({"swing"}),
                           md.WeaponStats(swing_damage=100, raw_swing_speed=10.0))
    fast_soft = cata.Usage("Soft", False, (), "s2", frozenset({"swing"}),
                           md.WeaponStats(swing_damage=60, raw_swing_speed=40.0))
    p = cata.Priced(cata.Item("i", "n", "c", "T", (), "src"), (slow_hard, fast_soft), "b", 3)
    # swing_usage picks on damage, so the reported DPS is that mode's, not the best available.
    assert p.swing_usage.description == "Hard"
    assert p.swing_dps == pytest.approx(slow_hard.swing_dps())
    assert fast_soft.swing_dps() > p.swing_dps  # the disagreement this test exists to detect
