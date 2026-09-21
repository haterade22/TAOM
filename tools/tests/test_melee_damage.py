#!/usr/bin/env python3
"""Tests for `tools/melee_damage.py`, the ported crafted-weapon physics.

The specification here is the v1.5.3 decompile, not a previous run of this code. Every
expected value is either a constant read straight out of the engine source, a quantity
computed by hand from a formula in it, or a golden captured once and then reviewed.

Two of these tests exist to stop a future reader "fixing" the engine. They are named
`test_engine_quirk_*` and they pin behaviour that looks wrong and is not.
"""

from __future__ import annotations

import math
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import melee_damage as md  # noqa: E402


# --- helpers ------------------------------------------------------------------------------


def piece_from_xml(xml: str) -> md.Piece:
    piece = md.parse_piece(ET.fromstring(xml))
    assert piece is not None
    return piece


SWORD_BUILD = [("Handle", 0), ("Guard", 1), ("Blade", 2), ("Pommel", -1)]
AXE_BUILD = [("Handle", 0), ("Blade", 1), ("Pommel", -1)]


def simple_blade(length=80.0, weight=1.0, swing=3.0, thrust=3.0) -> md.Piece:
    return piece_from_xml(
        f"""<CraftingPiece id="b" piece_type="Blade" length="{length}" weight="{weight}">
              <BladeData physics_material="metal_weapon">
                <Thrust damage_type="Pierce" damage_factor="{thrust}" />
                <Swing damage_type="Cut" damage_factor="{swing}" />
              </BladeData>
            </CraftingPiece>"""
    )


def simple_handle(length=20.0, weight=0.4) -> md.Piece:
    return piece_from_xml(
        f'<CraftingPiece id="h" piece_type="Handle" length="{length}" weight="{weight}" />'
    )


MELEE = md.WeaponFlags(frozenset({"MeleeWeapon"}))
TWO_HANDED = md.WeaponFlags(frozenset({"MeleeWeapon", "NotUsableWithOneHand"}))
POLEARM = md.WeaponFlags(frozenset({"MeleeWeapon", "NotUsableWithOneHand", "WideGrip"}))


# --- engine constants ---------------------------------------------------------------------


def test_display_constants_match_the_engine():
    # CombatStatCalculator.cs:11-19
    assert md.SWING_SPEED_CONST == 4.5454545
    assert md.THRUST_SPEED_CONST == 11.764706
    assert md.ARM_LENGTH == 0.5
    assert md.ARM_WEIGHT == 2.5


def test_swing_and_thrust_timesteps_differ():
    """The engine really does use two different timesteps; collapsing them shifts speeds."""
    # Crafting.cs:531-540 (float32 0.01) vs :553-560 (double 0.01)
    assert md.SWING_DT == 0.009999999776482582
    assert md.THRUST_DT == 0.01
    assert md.SWING_DT != md.THRUST_DT


def test_piece_type_order_matches_the_enum():
    # CraftingPiece.cs:14-22. Crafting.cs indexes UsedPieces by this, [0]=Blade, [2]=Handle.
    assert md.PIECE_TYPES == ("Blade", "Guard", "Handle", "Pommel")


# --- parsing ------------------------------------------------------------------------------


def test_length_is_centimetres_and_splits_into_two_half_distances():
    # CraftingPiece.cs:167-171
    piece = piece_from_xml('<CraftingPiece id="p" piece_type="Handle" length="80" weight="1" />')
    assert piece.length == pytest.approx(0.8)
    assert piece.distance_to_next == pytest.approx(0.4)
    assert piece.distance_to_previous == pytest.approx(0.4)


def test_length_is_implied_when_only_the_two_distances_are_given():
    # CraftingPiece.cs:173-179
    piece = piece_from_xml(
        '<CraftingPiece id="p" piece_type="Guard" weight="1"'
        ' distance_to_next_piece="30" distance_to_previous_piece="10" />'
    )
    assert piece.distance_to_next == pytest.approx(0.3)
    assert piece.distance_to_previous == pytest.approx(0.1)
    assert piece.length == pytest.approx(0.4)


def test_inertia_is_the_uniform_rod_formula():
    # CraftingPiece.cs:180 -- 1/12 * m * L^2, in metres
    piece = piece_from_xml('<CraftingPiece id="p" piece_type="Blade" length="100" weight="2" />')
    assert piece.inertia == pytest.approx((1.0 / 12.0) * 2.0 * 1.0 * 1.0)


def test_center_of_mass_defaults_to_the_midpoint():
    # CraftingPiece.cs:181-183
    default = piece_from_xml('<CraftingPiece id="p" piece_type="Blade" length="100" weight="1" />')
    assert default.center_of_mass == pytest.approx(0.5)
    tip_heavy = piece_from_xml(
        '<CraftingPiece id="p" piece_type="Blade" length="100" weight="1" center_of_mass="0.75" />'
    )
    assert tip_heavy.center_of_mass == pytest.approx(0.75)


def test_full_scale_defaults_by_piece_type_not_to_false():
    # CraftingPiece.cs:202-203 -- Guard and Pommel default true, the others false.
    def made(ptype, attr=""):
        return piece_from_xml(
            f'<CraftingPiece id="p" piece_type="{ptype}" length="10" weight="1" {attr} />'
        )

    assert made("Guard").full_scale is True
    assert made("Pommel").full_scale is True
    assert made("Blade").full_scale is False
    assert made("Handle").full_scale is False
    # An explicit attribute wins over the type default, in both directions.
    assert made("Guard", 'full_scale="false"').full_scale is False
    assert made("Blade", 'full_scale="true"').full_scale is True


def test_build_data_offsets_are_centimetres():
    piece = piece_from_xml(
        '<CraftingPiece id="p" piece_type="Handle" length="20" weight="1">'
        '<BuildData piece_offset="5" previous_piece_offset="3" next_piece_offset="-2" />'
        "</CraftingPiece>"
    )
    assert piece.piece_offset == pytest.approx(0.05)
    assert piece.previous_piece_offset == pytest.approx(0.03)
    assert piece.next_piece_offset == pytest.approx(-0.02)


def test_blade_data_and_flags_are_read():
    piece = simple_blade(swing=3.5, thrust=2.25)
    assert piece.blade is not None
    assert piece.blade.swing_factor == pytest.approx(3.5)
    assert piece.blade.swing_type == "Cut"
    assert piece.blade.thrust_factor == pytest.approx(2.25)
    assert piece.blade.thrust_type == "Pierce"

    flagged = piece_from_xml(
        '<CraftingPiece id="p" piece_type="Blade" length="10" weight="1">'
        '<Flags><Flag name="CanKnockDown" /><Flag name="Civilian" /></Flags>'
        "</CraftingPiece>"
    )
    assert flagged.flags == frozenset({"CanKnockDown", "Civilian"})


def test_a_non_piece_node_is_skipped_rather_than_guessed():
    assert md.parse_piece(ET.fromstring('<CraftingPiece piece_type="Blade" />')) is None
    assert md.parse_piece(ET.fromstring('<CraftingPiece id="x" piece_type="Nonsense" />')) is None


def test_missing_crafting_pieces_file_is_empty_not_an_error():
    assert md.parse_crafting_pieces(Path("no/such/file.xml")) == {}


# --- scaling ------------------------------------------------------------------------------


def test_an_unscaled_element_returns_raw_values():
    el = md.Element(simple_blade(), 100)
    assert el.is_scaled is False
    assert el.scaled_weight == el.piece.weight
    assert el.scaled_length == el.piece.length


def test_full_scale_pieces_scale_by_volume_others_linearly():
    # WeaponDesignElement.cs:44 -- the cube is the whole point of the flag.
    guard = piece_from_xml(
        '<CraftingPiece id="g" piece_type="Guard" length="10" weight="1.0" />'
    )
    blade = simple_blade(weight=1.0)
    assert md.Element(guard, 50).scaled_weight == pytest.approx(1.0 * 0.5**3)
    assert md.Element(blade, 50).scaled_weight == pytest.approx(1.0 * 0.5)
    # Length is linear for both.
    assert md.Element(guard, 50).scaled_length == pytest.approx(guard.length * 0.5)


# --- physics ------------------------------------------------------------------------------


def test_parallel_axis():
    assert md.parallel_axis(2.0, 3.0, 4.0) == pytest.approx(2.0 + 3.0 * 16.0)
    assert md.parallel_axis(2.0, 3.0, 0.0) == pytest.approx(2.0)


def test_thrust_magnitude_adds_the_arm_weight_unless_thrown():
    # CombatStatCalculator.cs:43-48
    held = md.strike_magnitude_thrust(10.0, 1.5, is_thrown=False)
    thrown = md.strike_magnitude_thrust(10.0, 1.5, is_thrown=True)
    assert held == pytest.approx(0.125 * 0.5 * (1.5 + 2.5) * 100.0)
    assert thrown == pytest.approx(0.125 * 0.5 * 1.5 * 100.0)
    assert held > thrown


def test_thrust_magnitude_is_zero_at_or_below_no_speed():
    assert md.strike_magnitude_thrust(0.0, 2.0) == 0.0
    assert md.strike_magnitude_thrust(-5.0, 2.0) == 0.0


def test_swing_magnitude_grows_with_speed():
    a = md.strike_magnitude_swing(5.0, 0.9, 1.5, 1.0, 0.2, 0.4)
    b = md.strike_magnitude_swing(10.0, 0.9, 1.5, 1.0, 0.2, 0.4)
    assert b > a > 0.0


def test_base_blow_clamps_the_impact_point_to_the_sweet_spot():
    # CombatStatCalculator.cs:66 -- anything past 0.93 is treated as 0.93.
    args = (8.0, 1.0, 1.5, 0.2, 0.4)
    assert md.base_blow_magnitude_swing(*args, 0.99) == pytest.approx(
        md.base_blow_magnitude_swing(*args, 0.93)
    )


def test_base_blow_is_the_best_of_the_sampled_points():
    args = (8.0, 1.0, 1.5, 0.2, 0.4)
    best = md.base_blow_magnitude_swing(*args, 0.7)
    window = min(0.4 / 1.0, 1.0)
    samples = [
        md.strike_magnitude_swing(8.0, 0.7 + (i / 4.0) * window, 1.5, 1.0, 0.2, 0.4)
        for i in range(5)
        if 0.7 + (i / 4.0) * window < 1.0
    ]
    assert best == pytest.approx(max(samples))


def test_a_wide_grip_pays_full_swing_drag():
    # Crafting.cs:526 -- 1.0 for a melee wide grip, 0.3 otherwise. More drag, more time.
    _, narrow = md.simulate_swing_layer(1.5, 170.0, 12.0, 4.0, 2.0, melee_wide_grip=False)
    _, wide = md.simulate_swing_layer(1.5, 170.0, 12.0, 4.0, 2.0, melee_wide_grip=True)
    assert wide > narrow


def test_a_heavier_weapon_swings_slower():
    light = md.swing_speed(1.0, 0.9, MELEE)
    heavy = md.swing_speed(4.0, 0.9, MELEE)
    assert light > heavy > 0.0


def test_two_hands_beat_one_on_the_same_weapon():
    """The second hand adds torque, which is why the same blade differs by usage."""
    assert md.swing_speed(3.0, 1.2, TWO_HANDED) > md.swing_speed(3.0, 1.2, MELEE)


def test_a_stalled_swing_reports_rather_than_hanging():
    # The engine's loop has no exit. Ours must not spin forever on malformed data.
    with pytest.raises(md.MeleeDamageError):
        md.simulate_swing_layer(1.5, 1.0, 1.0, 1.0, reach=1e6, melee_wide_grip=True)


def test_layer_simulations_reject_non_positive_inertia_and_mass():
    with pytest.raises(md.MeleeDamageError):
        md.simulate_swing_layer(1.5, 200.0, 20.0, 0.0, 1.0, False)
    with pytest.raises(md.MeleeDamageError):
        md.simulate_thrust_layer(0.6, 250.0, 48.0, 0.0)


# --- engine quirks that must not be "fixed" ------------------------------------------------


def test_engine_quirk_parallel_axis_uses_unscaled_piece_inertia():
    """`Crafting.cs:508-513` reads CraftingPiece.Inertia/CenterOfMass, not the Scaled ones.

    Only the MASS is scaled. So halving a piece's scale does not quarter its contribution to
    rotational inertia the way real physics would. This is engine behaviour and the shipped
    stats depend on it.
    """
    blade = simple_blade(length=80.0, weight=1.0)
    elements = {"Blade": md.Element(blade, 50), "Handle": md.Element(simple_handle(), 100)}
    build = [("Handle", 0), ("Blade", 1)]
    got = md.weapon_inertia(elements, build, com=0.1)

    expected = 0.0
    offset = -0.1
    for ptype, _ in build:
        el = elements[ptype]
        # unscaled inertia and unscaled centre of mass, scaled mass
        expected += md.parallel_axis(
            el.piece.inertia, el.scaled_weight, offset + el.piece.center_of_mass
        )
        offset += el.scaled_length
    assert got == pytest.approx(expected)

    # And it differs from the "correct" physics, which is how we know the quirk is live.
    physical = 0.0
    offset = -0.1
    for ptype, _ in build:
        el = elements[ptype]
        scaled_inertia = el.piece.inertia * (el.scale_factor**2)
        physical += md.parallel_axis(
            scaled_inertia, el.scaled_weight, offset + el.scaled_center_of_mass
        )
        offset += el.scaled_length
    assert got != pytest.approx(physical)


def test_engine_quirk_inertia_offset_advances_forward_for_every_piece():
    """`Crafting.cs:290` does `num += ScaledLength` with no regard to build-order sign.

    A pommel at order -1 sits behind the grip in the centre-of-mass pass, but the inertia
    pass walks it forward past the blade anyway.
    """
    blade = simple_blade(length=80.0, weight=1.2)
    handle = simple_handle(length=20.0, weight=0.4)
    pommel = piece_from_xml(
        '<CraftingPiece id="p" piece_type="Pommel" length="6" weight="0.3" />'
    )
    elements = {
        "Blade": md.Element(blade),
        "Handle": md.Element(handle),
        "Pommel": md.Element(pommel),
    }
    got = md.weapon_inertia(elements, AXE_BUILD, com=0.15)

    expected = 0.0
    offset = -0.15
    for ptype, _order in AXE_BUILD:  # order is deliberately ignored, exactly as the engine does
        el = elements[ptype]
        expected += md.parallel_axis(
            el.piece.inertia, el.scaled_weight, offset + el.piece.center_of_mass
        )
        offset += el.scaled_length
    assert got == pytest.approx(expected)


# --- assembly -----------------------------------------------------------------------------


def test_reach_is_not_the_sum_of_piece_lengths():
    """The gap that motivated this port.

    `CraftedWeaponLength` comes off the pivot walk (`WeaponDesign.cs:277+`), not from adding
    the forward pieces up. Reach feeds swing drag, the impact window and the lever arm, so
    approximating it with a sum moves damage.
    """
    elements = {
        "Handle": md.Element(simple_handle(length=20.0)),
        "Blade": md.Element(simple_blade(length=80.0)),
    }
    _pivots, length, _bottom = md.pivot_distances(elements, [("Handle", 0), ("Blade", 1)])
    naive_sum = sum(el.scaled_length for el in elements.values())
    assert not math.isnan(length)
    assert length != pytest.approx(naive_sum)


def test_pivot_walk_places_the_handle_at_the_grip():
    handle = simple_handle(length=20.0)
    elements = {"Handle": md.Element(handle), "Blade": md.Element(simple_blade(length=80.0))}
    pivots, _length, hand_to_bottom = md.pivot_distances(elements, [("Handle", 0), ("Blade", 1)])
    # Order 0 with no offsets pivots at exactly zero.
    assert pivots["Handle"] == pytest.approx(0.0)
    # The butt sits a handle half-length below the grip.
    assert hand_to_bottom == pytest.approx(handle.distance_to_previous)


def test_a_piece_absent_from_the_build_gets_a_nan_pivot():
    elements = {"Handle": md.Element(simple_handle()), "Blade": md.Element(simple_blade())}
    pivots, _l, _b = md.pivot_distances(elements, SWORD_BUILD)
    assert math.isnan(pivots["Guard"])
    assert math.isnan(pivots["Pommel"])


def test_center_of_mass_moves_toward_a_heavier_blade():
    handle = simple_handle()
    light = {"Handle": md.Element(handle), "Blade": md.Element(simple_blade(weight=0.5))}
    heavy = {"Handle": md.Element(handle), "Blade": md.Element(simple_blade(weight=3.0))}
    build = [("Handle", 0), ("Blade", 1)]
    light_com = md.center_of_mass(light, build, sum(e.scaled_weight for e in light.values()))
    heavy_com = md.center_of_mass(heavy, build, sum(e.scaled_weight for e in heavy.values()))
    assert heavy_com > light_com


def test_center_of_mass_refuses_a_weightless_weapon():
    with pytest.raises(md.MeleeDamageError):
        md.center_of_mass({}, [], 0.0)


# --- compute ------------------------------------------------------------------------------


def test_compute_needs_a_blade_carrying_blade_data():
    elements = {"Handle": md.Element(simple_handle())}
    with pytest.raises(md.MeleeDamageError, match="Blade"):
        md.compute(elements, [("Handle", 0)], MELEE)


def test_compute_produces_a_plausible_one_handed_sword():
    elements = {
        "Handle": md.Element(simple_handle(length=18.0, weight=0.35)),
        "Blade": md.Element(simple_blade(length=85.0, weight=1.1, swing=3.0, thrust=3.0)),
    }
    stats = md.compute(elements, [("Handle", 0), ("Blade", 1)], MELEE)
    # Sanity bands, not golden values: a one-handed sword is roughly this shape in vanilla.
    assert 0.5 <= stats.weight <= 3.0
    assert 0.5 <= stats.reach <= 1.5
    assert 50 <= stats.swing_speed <= 130
    assert 50 <= stats.thrust_speed <= 130
    assert 0 < stats.swing_damage < 200
    assert 0 < stats.thrust_damage < 200
    assert stats.swing_type == "Cut"
    assert stats.thrust_type == "Pierce"


def test_a_bigger_damage_factor_scales_damage_proportionally():
    """Step 5 is a pure multiply, so doubling the factor doubles the displayed damage."""
    def stats_for(factor):
        elements = {
            "Handle": md.Element(simple_handle()),
            "Blade": md.Element(simple_blade(swing=factor, thrust=factor)),
        }
        return md.compute(elements, [("Handle", 0), ("Blade", 1)], MELEE)

    one = stats_for(2.0)
    two = stats_for(4.0)
    assert two.swing_damage == pytest.approx(one.swing_damage * 2, abs=1)
    assert two.thrust_damage == pytest.approx(one.thrust_damage * 2, abs=1)
    # Speed is physics, not the factor, so it must not move at all.
    assert one.swing_speed == two.swing_speed


def test_the_same_blade_differs_between_one_and_two_handed_usage():
    elements = {
        "Handle": md.Element(simple_handle(length=30.0, weight=0.6)),
        "Blade": md.Element(simple_blade(length=100.0, weight=1.8)),
    }
    build = [("Handle", 0), ("Blade", 1)]
    one = md.compute(elements, build, MELEE)
    two = md.compute(elements, build, TWO_HANDED)
    assert two.swing_speed != one.swing_speed
    assert two.swing_damage != one.swing_damage


# --- armour -------------------------------------------------------------------------------


def test_armour_reduction_is_a_no_op_at_zero_armour():
    assert md.armour_reduction("Cut", 100.0, 0.0) == pytest.approx(100.0)
    assert md.armour_reduction("Blunt", 100.0, 0.0) == pytest.approx(100.0)
    assert md.armour_reduction("Pierce", 100.0, 0.0) == pytest.approx(100.0)


@pytest.mark.parametrize(
    "damage_type,expected",
    [
        # Hand-computed from scaled = 100 * 50/100 = 50 at armour 50:
        #   Cut    0.1*50 + 0.9*max(0, 50 - 50*0.50) = 5     + 22.5   = 27.5
        #   Pierce 0.25*50 + 0.75*max(0, 50 - 50*0.33) = 12.5 + 25.125 = 37.625
        #   Blunt  0.6*50 + 0.4*max(0, 50 - 50*0.20) = 30    + 16     = 46.0
        ("Cut", 27.5),
        ("Pierce", 37.625),
        ("Blunt", 46.0),
    ],
)
def test_armour_reduction_known_values(damage_type, expected):
    assert md.armour_reduction(damage_type, 100.0, 50.0) == pytest.approx(expected)


def test_blunt_beats_pierce_beats_cut_against_armour():
    cut = md.armour_reduction("Cut", 100.0, 40.0)
    pierce = md.armour_reduction("Pierce", 100.0, 40.0)
    blunt = md.armour_reduction("Blunt", 100.0, 40.0)
    assert blunt > pierce > cut


def test_armour_reduction_handles_degenerate_input():
    assert md.armour_reduction("Cut", 0.0, 30.0) == 0.0
    assert md.armour_reduction("Cut", -10.0, 30.0) == 0.0
    # An unknown damage type falls back to Cut rather than throwing mid-report.
    assert md.armour_reduction("Nonsense", 100.0, 50.0) == pytest.approx(
        md.armour_reduction("Cut", 100.0, 50.0)
    )


def test_armour_never_amplifies_damage():
    for armour in (0, 10, 25, 50, 100, 300):
        for dtype in ("Cut", "Pierce", "Blunt"):
            assert md.armour_reduction(dtype, 100.0, armour) <= 100.0 + 1e-9


# --- catalogue: usage resolution ----------------------------------------------------------
#
# These cover `tools/melee_catalogue.py`, the wiring around the physics. The bugs they pin
# are the ones that actually bit during the port: a hardcoded build order, a primary-usage
# guess, and reading attack capability off the wrong field.

import melee_catalogue as cata  # noqa: E402


def _cat(**kw) -> cata.Catalogue:
    c = cata.Catalogue()
    for key, value in kw.items():
        setattr(c, key, value)
    return c


def test_strike_types_union_the_base_set_chain():
    """`onehanded_shield_axe` declares no usages and inherits swing from its base.

    Taking the first chain entry that declares anything would say an axe cannot swing.
    """
    cat = _cat(
        usage_sets={
            "onehanded_shield_axe": ("onehanded_block_shield_swing", frozenset()),
            "onehanded_block_shield_swing": (None, frozenset({"swing"})),
        }
    )
    assert cat.strike_types("onehanded_shield_axe") == frozenset({"swing"})


def test_strike_types_survive_a_cyclic_base_set():
    cat = _cat(usage_sets={"a": ("b", frozenset({"swing"})), "b": ("a", frozenset({"thrust"}))})
    assert cat.strike_types("a") == frozenset({"swing", "thrust"})


def test_strike_types_of_an_unknown_set_is_empty_not_an_error():
    assert _cat().strike_types("no_such_set") == frozenset()


def test_usage_set_id_drops_tokens_the_pieces_exclude():
    item = cata.Item("i", "n", "c", "T", (("blade", "Blade", 100),), "src")
    desc = cata.Description("D", "OneHandedSword", frozenset(), frozenset(), ("onehanded", "block", "swing"))
    cat = _cat(excluded_features={"blade": frozenset({"block"})})
    assert cata.usage_set_id(cat, item, desc) == "onehanded_swing"


def test_matching_descriptions_keeps_template_order_and_needs_every_piece():
    """First match wins, and a description missing ONE piece does not match at all."""
    item = cata.Item(
        "i", "n", "c", "Polearm", (("a", "Blade", 100), ("b", "Handle", 100)), "src"
    )
    cat = _cat(
        templates={"Polearm": cata.Template("Polearm", "Polearm", [("Handle", 0)], ["One", "Two"])},
        descriptions={
            "One": cata.Description("One", "P", frozenset(), frozenset({"a"}), ()),
            "Two": cata.Description("Two", "P", frozenset(), frozenset({"a", "b"}), ()),
        },
    )
    matches = cata.matching_descriptions(cat, item)
    # "One" lacks piece b, so it is not a mode at all.
    assert [d.id for d in matches] == ["Two"]
    assert cata.resolve_description(cat, item).id == "Two"


def test_weapon_flags_or_in_the_pieces_own_flags():
    """Crafting.cs:601 ORs the design's accumulated piece flags into the description's."""
    blade = piece_from_xml(
        '<CraftingPiece id="b" piece_type="Blade" length="50" weight="1">'
        '<Flags><Flag name="CanKnockDown" /></Flags></CraftingPiece>'
    )
    item = cata.Item("i", "n", "c", "T", (("b", "Blade", 100),), "src")
    desc = cata.Description("D", "c", frozenset({"MeleeWeapon"}), frozenset({"b"}), ())
    flags = cata.weapon_flags(_cat(pieces={"b": blade}), item, desc)
    assert flags.values == frozenset({"MeleeWeapon", "CanKnockDown"})


def test_a_mode_that_cannot_swing_is_excluded_from_the_headline_swing():
    """The OneHandedPolearm case: its physics computes a swing the game can never use."""
    weak = cata.Usage("OneHandedPolearm", True, (), "set_a", frozenset({"thrust"}),
                      md.WeaponStats(swing_damage=12, thrust_damage=30))
    strong = cata.Usage("TwoHandedPolearm", False, (), "set_b", frozenset({"swing", "thrust"}),
                        md.WeaponStats(swing_damage=161, thrust_damage=35))
    priced = cata.Priced(
        item=cata.Item("i", "n", "c", "TwoHandedPolearm", (), "src"),
        usages=(weak, strong),
        blade_piece="b",
        tier=3,
    )
    assert priced.primary.description == "OneHandedPolearm"   # primary is still the primary
    assert priced.swing_damage == 161                          # but the swing comes from the mode that has one
    assert priced.thrust_damage == 35
    assert priced.best_damage == 161


def test_a_weapon_with_no_swing_capable_mode_reports_zero_not_its_unusable_number():
    only = cata.Usage("Pike", True, (), "polearm_pike", frozenset({"thrust"}),
                      md.WeaponStats(swing_damage=99, thrust_damage=40))
    priced = cata.Priced(cata.Item("i", "n", "c", "Pike", (), "src"), (only,), "b", 3)
    assert priced.swing_usage is None
    assert priced.swing_damage == 0
    assert priced.swing_speed == 0
    assert priced.thrust_damage == 40
    assert priced.best_damage == 40


def test_strip_loc_removes_the_localisation_key():
    assert cata.strip_loc("{=aom_x}[Mordor] Uruk Sword I") == "[Mordor] Uruk Sword I"
    assert cata.strip_loc("plain name") == "plain name"
    assert cata.strip_loc("") == ""


def test_thrown_templates_are_refused_rather_than_mispriced():
    cat = _cat()
    item = cata.Item("j", "n", "c", "Javelin", (), "src")
    with pytest.raises(cata.CatalogueError, match="thrown"):
        cata.price(cat, item)


# --- sustained damage (DPS) -----------------------------------------------------------------


def test_stun_periods_match_the_shipped_combat_parameters():
    """managed_core_parameters.xml, v1.5.3. The asymmetry is the point: thrust pays 6.7x."""
    assert md.STUN_PERIOD_SWING == 0.1
    assert md.STUN_PERIOD_THRUST == 0.67


def test_time_constants_are_the_speed_simulation_numerators():
    # Crafting.cs:330 and :360. These invert the speed stat back into an attack time.
    assert md.SWING_TIME_CONST == 20.8
    assert md.THRUST_TIME_CONST == 3.8500000000000005


def test_cycle_time_inverts_the_speed_constant_and_adds_the_stun():
    assert md.swing_cycle_time(20.8, stun=0.0) == pytest.approx(1.0)
    assert md.swing_cycle_time(20.8, stun=0.1) == pytest.approx(1.1)
    assert md.thrust_cycle_time(3.85, stun=0.0) == pytest.approx(1.0)


def test_doubling_the_speed_halves_the_attack_itself():
    """Exact by construction, which is what makes the DPS ordering trustworthy."""
    fast = md.swing_cycle_time(40.0, stun=0.0)
    slow = md.swing_cycle_time(20.0, stun=0.0)
    assert slow == pytest.approx(fast * 2)


def test_cycle_time_rejects_a_non_positive_speed():
    with pytest.raises(md.MeleeDamageError):
        md.swing_cycle_time(0.0)
    with pytest.raises(md.MeleeDamageError):
        md.thrust_cycle_time(-1.0)


def test_sustained_damage_is_damage_over_the_cycle():
    assert md.sustained_damage(100.0, 2.0) == pytest.approx(50.0)
    assert md.sustained_damage(0.0, 2.0) == 0.0
    assert md.sustained_damage(-5.0, 2.0) == 0.0
    with pytest.raises(md.MeleeDamageError):
        md.sustained_damage(100.0, 0.0)


def test_a_faster_weapon_can_beat_a_harder_hitting_one():
    """The case this whole model exists for.

    Numbers taken from two real Dale swords: `dale_sword_a` hits for 56 at speed 86,
    `dale_sword_c` for 48 at speed 104. On one blow `a` wins by 8. Over time `c` is ahead.
    """
    raw_a = 86 / md.SWING_SPEED_CONST
    raw_c = 104 / md.SWING_SPEED_CONST
    dps_a = md.sustained_damage(56, md.swing_cycle_time(raw_a))
    dps_c = md.sustained_damage(48, md.swing_cycle_time(raw_c))
    assert 56 > 48            # bigger blow
    assert dps_c > dps_a      # smaller total output, all the same


def test_thrust_pays_a_large_recovery_penalty_a_swing_does_not():
    """Same damage and same raw speed: the swing wins purely on the stun asymmetry."""
    swing = md.sustained_damage(50, md.swing_cycle_time(20.8))    # 1.0s + 0.10s
    thrust = md.sustained_damage(50, md.thrust_cycle_time(3.85))  # 1.0s + 0.67s
    assert swing > thrust
    assert swing / thrust == pytest.approx(1.67 / 1.10, rel=1e-6)
