#!/usr/bin/env python3
"""Tests for `tools/melee_ladder.py` (the spec) and `tools/fix_melee_ladder.py` (the roster pass).

Synthetic data throughout. The real pass needs the live Bannerlord install, which CI does not
have, so the decision logic is written as pure functions over plain data and tested that way.

The tests that matter most are the constraint ones. An earlier pass of the fixer ranked culture
as a tiebreak rather than a filter, and handed Gondor's Swan Knights Uruk-hai halberds; another
recorded only the first slot an item sat in, which would have half-applied a swap. Both are
pinned below.
"""

from __future__ import annotations

import contextlib
import io
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import fix_melee_ladder as fixer  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402
import melee_ladder as ladder  # noqa: E402


_absolute = abs


class approx:
    """The part of pytest.approx these tests use: CI runs unittest with no pytest (2026-11-14).

    Same tolerance rule as pytest: `abs` alone, `rel` alone, both (the larger wins) or neither
    (1e-6 relative, 1e-12 absolute).
    """

    def __init__(self, expected, rel=None, abs=None):
        if rel is None and abs is None:
            rel, abs = 1e-6, 1e-12
        self.expected = expected
        self.tolerance = max((rel or 0) * _absolute(expected), abs or 0)

    def __eq__(self, actual):
        return _absolute(actual - self.expected) <= self.tolerance

    def __repr__(self):
        return f"approx({self.expected} +- {self.tolerance})"


class TmpCase(unittest.TestCase):
    """A fresh temporary folder per test, as pytest's tmp_path gave."""

    def setUp(self):
        self.tmp_path = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.tmp_path, True)


# --- helpers ------------------------------------------------------------------------------


def spec(**over):
    base = {
        "anchor_tier": 5,
        "curve": {str(t): v for t, v in enumerate([25, 34, 43, 52, 60, 69, 78, 87, 96, 105, 114])},
        "band": 0.25,
        "kingdom_offset": {"rivendell": 0.10, "mordor": -0.02},
        "ceiling": 140,
        "hero_blades": ["hero_blade"],
        "exempt_troops": {},
    }
    base.update(over)
    return base


def weapon(item_id, template="OneHandedSword", dps=50.0, name=None, blade="blade"):
    """A `Priced` whose best_dps is exactly `dps`, via a swing-capable mode."""
    # best_dps = damage / (20.8/raw + 0.1). Pick raw=20.8 so the cycle is 1.1s, then solve.
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"swing"}),
        md.WeaponStats(swing_damage=dps * 1.1, raw_swing_speed=20.8),
    )
    return cata.Priced(
        item=cata.Item(item_id, name or item_id, "", template, ((blade, "Blade", 100),), "src"),
        usages=(usage,), blade_piece=blade, tier=3,
    )


def troop(tid, level, culture, items, slots=None, path="troops_x.xml"):
    slots = slots or {i: frozenset({"Item0"}) for i in items}
    return report.Troop(tid, tid, level, culture, "troops_x.xml", frozenset(items),
                        path=path, slots_of=slots)


# --- spec ---------------------------------------------------------------------------------


class SpecTests(TmpCase):
    def test_the_shipped_spec_is_valid(self):
        """The file that actually ships must load, or every gate reading it checks nothing."""
        assert ladder.validate_spec(ladder.load_spec()) == []

    def test_the_shipped_spec_has_no_stale_troop_exemptions(self):
        """Measured against the real rosters: an entry for a deleted troop skips nothing silently."""
        ids = {t.id for t in report.load_troops(report.TROOPS)}
        assert ladder.stale_exemptions(ids, ladder.load_spec()) == []

    def test_target_applies_the_kingdom_offset(self):
        s = spec()
        assert ladder.target(5, "rivendell", s) == approx(69 * 1.10)
        assert ladder.target(5, "mordor", s) == approx(69 * 0.98)
        # A culture with no entry takes the plain tier target.
        assert ladder.target(5, "nowhere", s) == approx(69)

    def test_target_clamps_a_tier_outside_the_curve(self):
        s = spec()
        assert ladder.target(99, "x", s) == ladder.target(10, "x", s)
        assert ladder.target(-3, "x", s) == ladder.target(0, "x", s)

    def test_band_brackets_the_target(self):
        b = ladder.band(5, "nowhere", spec())
        assert b.low == approx(69 * 0.75)
        assert b.high == approx(69 * 1.25)
        assert b.verdict(69) == "ok"
        assert b.verdict(10) == "under"
        assert b.verdict(200) == "over"

    def test_validate_spec_rejects_a_broken_spec(self):
        for broken, expect in [
            ({"curve": {}}, "no `curve`"),
            ({"band": 5}, "band"),
            ({"ceiling": -1}, "ceiling"),
            ({"anchor_tier": 99}, "anchor_tier"),
        ]:
            with self.subTest(expect=expect):
                problems = ladder.validate_spec(spec(**broken))
                assert problems
                assert any(expect in p for p in problems)

    def test_validate_spec_rejects_a_curve_that_does_not_climb(self):
        """The spec exists to make the ladder climb. One that does not is the bug it fixes."""
        flat = {str(t): 50 for t in range(11)}
        problems = ladder.validate_spec(spec(curve=flat))
        assert any("not strictly increasing" in p for p in problems)

    def test_validate_spec_rejects_a_ceiling_below_the_curve(self):
        problems = ladder.validate_spec(spec(ceiling=50))
        assert any("ceiling" in p and "below the top" in p for p in problems)

    def test_validate_spec_rejects_a_curve_with_a_missing_tier(self):
        curve = {str(t): 10 * (t + 1) for t in range(11)}
        del curve["7"]
        assert any("tier(s) 7" in p for p in ladder.validate_spec(spec(curve=curve)))

    def test_load_spec_refuses_a_missing_file(self):
        with self.assertRaisesRegex(ladder.LadderError, "missing"):
            ladder.load_spec(Path("no/such/spec.json"))

    def test_load_spec_refuses_a_self_contradicting_file(self):
        bad = self.tmp_path / "s.json"
        bad.write_text(json.dumps(spec(band=9)), encoding="utf-8")
        with self.assertRaisesRegex(ladder.LadderError, "self-contradicting"):
            ladder.load_spec(bad)


# --- findings -----------------------------------------------------------------------------


class FindingsTests(TmpCase):
    def test_findings_reports_both_directions_and_the_ceiling(self):
        s = spec()
        kits = [
            ("elite", "nowhere", 10, "toothpick", 20.0, "b"),   # way under
            ("militia", "nowhere", 1, "godsword", 200.0, "b"),  # way over, and past the ceiling
            ("fine", "nowhere", 5, "sword", 69.0, "b"),
        ]
        kinds = {(f.troop, f.kind) for f in ladder.findings(kits, s)}
        assert ("elite", "under") in kinds
        assert ("militia", "over") in kinds
        assert ("militia", "ceiling") in kinds
        assert not any(t == "fine" for t, _k in kinds)

    def test_a_hero_blade_is_exempt_from_the_ceiling_but_not_from_the_band(self):
        s = spec()
        kits = [("lord", "nowhere", 10, "anduril", 200.0, "hero_blade")]
        kinds = {f.kind for f in ladder.findings(kits, s)}
        assert "ceiling" not in kinds   # a hero weapon may out-sustain everything
        assert "over" in kinds          # but a line troop holding one is still a ladder problem

    def test_an_exempt_troop_is_skipped_entirely(self):
        s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        kits = [("cave_troll", "mordor", 10, "club", 5.0, "b")]
        assert ladder.findings(kits, s) == []

    def test_findings_are_worst_first_and_respect_min_gap(self):
        s = spec()
        kits = [
            ("a", "nowhere", 5, "w", 50.0, "b"),   # just under the 51.75 floor
            ("b", "nowhere", 5, "w", 5.0, "b"),    # far under
        ]
        found = ladder.findings(kits, s)
        assert [f.troop for f in found] == ["b", "a"]
        assert [f.troop for f in ladder.findings(kits, s, min_gap=10)] == ["b"]

    def test_stale_exemptions_names_troops_that_no_longer_exist(self):
        s = spec(exempt_troops={"gone": "why", "here": "why"})
        assert ladder.stale_exemptions({"here"}, s) == ["gone"]


# --- the roster fixer ---------------------------------------------------------------------


class RosterFixerTests(TmpCase):
    def test_family_reads_the_id_prefix_not_the_culture_attribute(self):
        assert fixer.family("wm_gondor_sword_a01") == "wm_gondor"
        assert fixer.family("sm_uruk_halberd_b") == "sm_uruk"

    def test_culture_is_a_hard_constraint_not_a_preference(self):
        """The defect this pins: Gondor knights were being handed Uruk-hai halberds.

        Gondor owns no in-band polearm here. The correct answer is to REFUSE, leaving the case for
        the restat pass, not to reach into Mordor's rack because the number fits.
        """
        priced = {
            "wm_gondor_spear_a": weapon("wm_gondor_spear_a", "TwoHandedPolearm", dps=20.0),
            "sm_uruk_halberd_b": weapon("sm_uruk_halberd_b", "TwoHandedPolearm", dps=100.0),
        }
        troops = [
            troop("gondor_knight", 46, "gondor", ["wm_gondor_spear_a"]),
            troop("uruk", 46, "mordor", ["sm_uruk_halberd_b"]),
        ]
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), troops, spec(), shields=set())
        assert [s.new for s in swaps if s.troop == "gondor_knight"] == []
        assert any(t.id == "gondor_knight" and "owns no" in why for t, _i, why in unresolved)

    def test_a_swap_stays_inside_the_troops_own_weapon_class(self):
        """Skills, animations and holsters follow the class; a sword-for-mace swap is a new troop."""
        priced = {
            "sword_weak": weapon("wm_x_sword_a", "OneHandedSword", dps=10.0),
            "mace_right": weapon("wm_x_mace_a", "Mace", dps=69.0),
        }
        troops = [troop("t", 26, "x", ["wm_x_sword_a", "wm_x_mace_a"])]
        # Rebuild the dict keyed by real item id.
        priced = {p.item.id: p for p in priced.values()}
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), troops, spec(), shields=set())
        assert all(s.old != "wm_x_sword_a" or s.new != "wm_x_mace_a" for s in swaps)

    def test_a_swap_is_emitted_for_every_slot_the_weapon_sits_in(self):
        """Six troops carry one melee weapon in two slots; a single swap would half-apply it."""
        priced = {
            "wm_x_axe_a": weapon("wm_x_axe_a", "OneHandedAxe", dps=200.0),
            "wm_x_axe_b": weapon("wm_x_axe_b", "OneHandedAxe", dps=34.0),
            "wm_x_axe_c": weapon("wm_x_axe_c", "OneHandedAxe", dps=36.0),
        }
        t = troop("t", 6, "x", ["wm_x_axe_a", "wm_x_axe_b"],
                  slots={"wm_x_axe_a": frozenset({"Item0", "Item2"}),
                         "wm_x_axe_b": frozenset({"Item1"})})
        pool_owner = troop("other", 6, "x", ["wm_x_axe_c"])
        swaps, _unres = fixer.plan(priced, cata.Catalogue(), [t, pool_owner], spec(), shields=set())
        moved = {s.slot for s in swaps if s.old == "wm_x_axe_a"}
        assert moved == {"Item0", "Item2"}

    def test_two_weapons_of_one_troop_never_collapse_onto_one_replacement(self):
        """The defect this pins: the Barad-Dur Guard's spear and spare halberd both became Halberd IX.

        Two different weapons in, two different weapons out; the second takes the next-ranked pick.
        """
        priced = {p.item.id: p for p in (
            weapon("sm_uruk_spear_a", "TwoHandedPolearm", dps=20.0),
            weapon("sm_uruk_halberd_b", "TwoHandedPolearm", dps=200.0),
            weapon("sm_uruk_halberd_j", "TwoHandedPolearm", dps=69.0),
            weapon("sm_uruk_halberd_c", "TwoHandedPolearm", dps=72.0),
        )}
        t = troop("guard", 26, "x", ["sm_uruk_spear_a", "sm_uruk_halberd_b"],
                  slots={"sm_uruk_spear_a": frozenset({"Item0"}),
                         "sm_uruk_halberd_b": frozenset({"Item2"})})
        pool_owner = troop("other", 26, "x", ["sm_uruk_halberd_j", "sm_uruk_halberd_c"])
        swaps, _u = fixer.plan(priced, cata.Catalogue(), [t, pool_owner], spec(), shields=set())
        new_ids = [s.new for s in swaps if s.troop == "guard"]
        assert len(new_ids) == 2
        assert len(set(new_ids)) == 2

    def test_a_swap_never_duplicates_a_weapon_the_troop_keeps(self):
        """An in-band weapon stays; an out-of-band one must not be swapped into a copy of it."""
        priced = {p.item.id: p for p in (
            weapon("sm_uruk_spear_a", "TwoHandedPolearm", dps=20.0),
            weapon("sm_uruk_halberd_j", "TwoHandedPolearm", dps=69.0),
        )}
        t = troop("guard", 26, "x", ["sm_uruk_spear_a", "sm_uruk_halberd_j"],
                  slots={"sm_uruk_spear_a": frozenset({"Item0"}),
                         "sm_uruk_halberd_j": frozenset({"Item2"})})
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [t], spec(), shields=set())
        assert [s.new for s in swaps] == []
        assert any(i == "sm_uruk_spear_a" and "already in this troop's kit" in why
                   for _t, i, why in unresolved)

    def test_a_kept_weapon_never_pushes_the_pick_onto_a_hero_blade(self):
        """The only non-hero in-band sword is one the troop keeps; the hero blade must not fill in."""
        priced = {p.item.id: p for p in (
            weapon("wm_x_sword_a", "OneHandedSword", dps=20.0),
            weapon("wm_x_sword_b", "OneHandedSword", dps=69.0),
            weapon("wm_x_sword_hero", "OneHandedSword", dps=70.0, blade="hero_blade"),
        )}
        t = troop("line", 26, "x", ["wm_x_sword_a", "wm_x_sword_b"],
                  slots={"wm_x_sword_a": frozenset({"Item0"}),
                         "wm_x_sword_b": frozenset({"Item1"})})
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [t], spec(), shields=set())
        assert [s.new for s in swaps] == []
        assert any(i == "wm_x_sword_a" and "already in this troop's kit" in why
                   for _t, i, why in unresolved)

    def test_a_troop_already_in_band_is_left_alone(self):
        priced = {"wm_x_sword_a": weapon("wm_x_sword_a", "OneHandedSword", dps=69.0)}
        troops = [troop("t", 26, "x", ["wm_x_sword_a"])]   # level 26 -> tier 5, target 69
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), troops, spec(), shields=set())
        assert swaps == []
        assert unresolved == []

    def test_an_exempt_troop_is_never_swapped(self):
        priced = {
            "wm_x_club_a": weapon("wm_x_club_a", "TwoHandedMace", dps=5.0),
            "wm_x_club_b": weapon("wm_x_club_b", "TwoHandedMace", dps=114.0),
        }
        t = troop("cave_troll", 51, "mordor", ["wm_x_club_a"])
        s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        swaps, _u = fixer.plan(priced, cata.Catalogue(), [t], s, shields=set())
        assert swaps == []

    def test_improvement_is_positive_for_a_swap_toward_target(self):
        sw = fixer.Swap("t", "f", "c", 5, "Item0", "old", "new",
                        old_dps=200.0, new_dps=70.0, target=69.0, reason="x")
        assert sw.improvement > 0

    def test_shield_forbidden_reads_the_resolved_usage_set_flags(self):
        """The orientation.md "Shield plus polearm" trap, shipped three times: a shield plus a no-shield polearm."""
        cat = cata.Catalogue(
            templates={"P": cata.Template("P", "Polearm", [("Handle", 0)], ["D"])},
            descriptions={"D": cata.Description("D", "P", frozenset({"MeleeWeapon"}),
                                                frozenset({"blade"}), ("polearm", "swing"))},
            usage_flags={"polearm_swing": frozenset({"requires_no_shield"})},
        )
        item = cata.Item("spear", "Spear", "", "P", (("blade", "Blade", 100),), "src")
        assert fixer.shield_forbidden(cat, item) is True

        cat.usage_flags = {"polearm_swing": frozenset()}
        assert fixer.shield_forbidden(cat, item) is False

    def test_troop_carries_shield_detects_any_shield_in_the_kit(self):
        t = troop("t", 10, "x", ["sword", "round_shield"])
        assert fixer.troop_carries_shield(t, {"round_shield"}) is True
        assert fixer.troop_carries_shield(t, {"other_shield"}) is False


# --- class limits: one blow and DPS, per crafting template --------------------------------

SWORD_LIMIT = {"swing": 95, "thrust": 55, "swing_dps": 83, "thrust_dps": 47}


class ClassLimitsOneBlowTests(TmpCase):
    def test_the_shipped_spec_limits_every_melee_template(self):
        """A template with no limit is a class the blow gate cannot see."""
        limits = ladder.load_spec()["class_limits"]
        for template in ("Dagger", "Mace", "OneHandedAxe", "OneHandedSword", "Pike", "TwoHandedAxe",
                         "TwoHandedMace", "TwoHandedPolearm", "TwoHandedSword"):
            assert template in limits, template

    def test_a_one_handed_sword_cannot_kill_a_100_health_troop_in_one_blow(self):
        """Mike, 2026-11-14: most troops have 100 health, so a one handed blow stays under it."""
        limits = ladder.load_spec()["class_limits"]
        for template in ("Mace", "OneHandedAxe", "OneHandedSword"):
            assert limits[template]["swing"] < 100, template

    def test_max_factor_is_bound_by_the_blow_before_the_dps(self):
        """The 2026-11-14 defect: a slow sword met its DPS target with a 140 blow."""
        # 53 swing at 45 DPS: the DPS limit alone allows x1.84 (98 swing); the blow limit is lower.
        k = ladder.max_factor(SWORD_LIMIT, swing=53, thrust=0, swing_dps=45.0, thrust_dps=0.0)
        assert k < 83 / 45
        assert 53.5 * k <= 94.5  # the unrounded blow cannot display above 95

    def test_max_factor_is_bound_by_the_dps_for_a_fast_weapon(self):
        k = ladder.max_factor(SWORD_LIMIT, swing=40, thrust=0, swing_dps=60.0, thrust_dps=0.0)
        assert k < 83 / 60
        assert k == approx(55.5 / 40.5)  # 55 damage at 1.5 DPS a point is the most inside 83

    def test_a_weapon_at_the_dps_cap_cannot_round_its_damage_up_past_the_dps_limit(self):
        """DPS moves in whole damage points, so the cap keeps the same half point the blow cap keeps."""
        per_point = 60.0 / 40
        k = ladder.max_factor(SWORD_LIMIT, swing=40, thrust=0, swing_dps=60.0, thrust_dps=0.0)
        for raw in (39.5, 40.0, 40.25, 40.49):
            shown = int(raw * k + 0.5)
            assert shown * per_point <= SWORD_LIMIT["swing_dps"], raw

    def test_floor_factor_rounds_down_to_two_decimals_and_keeps_an_exact_value(self):
        assert ladder.floor_factor(3.4583) == 3.45
        assert ladder.floor_factor(4.5699) == 4.56
        assert ladder.floor_factor(2.3) == 2.3
        assert ladder.floor_factor(4.0 * 0.88) == 3.52   # 3.5200000000000005 and 3.52 are one value
        assert ladder.floor_factor(3.52 - 1e-7) == 3.51

    def test_max_factor_ignores_an_attack_the_weapon_does_not_have(self):
        k = ladder.max_factor(SWORD_LIMIT, swing=0, thrust=30, swing_dps=0.0, thrust_dps=20.0)
        assert k == approx(min(54.5 / 30.5, 47 / 20))

    def test_max_factor_is_unbounded_without_a_limit(self):
        assert ladder.max_factor(None, 50, 0, 50.0, 0.0) == float("inf")

    def test_limit_breaches_names_each_stat_over_its_limit(self):
        over = ladder.limit_breaches(SWORD_LIMIT, swing=140, thrust=77, swing_dps=119.0, thrust_dps=40.0)
        assert [b[0] for b in over] == ["swing", "thrust", "swing_dps"]
        assert ladder.limit_breaches(SWORD_LIMIT, 95, 55, 83.0, 47.0) == []
        assert ladder.limit_breaches(None, 500, 500, 500.0, 500.0) == []

    def test_validate_spec_rejects_a_broken_class_limit(self):
        bad = spec(class_limits={"OneHandedSword": {"swing": -5, "thrust": 55, "swing_dps": 83}})
        problems = ladder.validate_spec(bad)
        assert any("class_limits" in p and "swing" in p for p in problems)
        assert any("thrust_dps" in p for p in problems)

    def test_blow_findings_reports_a_carried_weapon_over_its_class_limit_once(self):
        s = spec(class_limits={"OneHandedSword": SWORD_LIMIT})
        carried = [
            ("t9", "sword_140", "OneHandedSword", "blade", 140, 77, 119.0, 40.0),
            ("t10", "sword_140", "OneHandedSword", "blade", 140, 77, 119.0, 40.0),
            ("t1", "sword_ok", "OneHandedSword", "blade", 60, 30, 50.0, 20.0),
        ]
        found = ladder.blow_findings(carried, s)
        assert [(f.item, f.troops) for f in found] == [("sword_140", ("t10", "t9"))]
        assert "swing 140 above 95" in found[0].describe()

    def test_blow_findings_skips_exempt_troops(self):
        s = spec(class_limits={"OneHandedSword": SWORD_LIMIT}, exempt_troops={"troll": "monster"})
        carried = [("troll", "club", "OneHandedSword", "blade", 140, 0, 119.0, 0.0)]
        assert ladder.blow_findings(carried, s) == []


# --- per-class tier targets, craft columns and hero limits (2026-11-14) -------------------

CLASS_LIMITS = {
    "OneHandedSword": SWORD_LIMIT,
    "OneHandedAxe": {"swing": 85, "thrust": None, "swing_dps": 70, "thrust_dps": None},
    "TwoHandedPolearm": {"swing": 185, "thrust": 60, "swing_dps": 138, "thrust_dps": 45},
}
CRAFT_COLUMN = {"OneHandedSword": "sword", "OneHandedAxe": "axe", "TwoHandedPolearm": "polearm"}
# The shipped weapon_tier_fraction (2026-11-14): the share of a class top that tier t may reach.
TIER_FRACTION = {str(t): v for t, v in enumerate(
    [0.58, 0.62, 0.66, 0.72, 0.77, 0.81, 0.88, 0.91, 0.94, 0.97, 1.0])}


def class_spec(**over):
    base = dict(
        class_limits=CLASS_LIMITS,
        craft_column=CRAFT_COLUMN,
        craft={"rivendell": {"sword": 1.0, "axe": 0.9, "polearm": 0.97},
               "mordor": {"sword": 0.82, "axe": 0.85, "polearm": 0.82}},
        craft_default={"sword": 0.85, "axe": 0.85, "polearm": 0.85},
        class_band=0.15,
        class_tier_fraction=dict(TIER_FRACTION),
        craft_troop_prefix={"mordor_num_": "mordor"},
        hero_makers={"hero_blade": "high_elven_dwarven"},
        hero_bonus={"high_elven_dwarven": 10, "other": 5},
    )
    base.update(over)
    return spec(**base)


class ClassTierTargetsCraftTests(TmpCase):
    def test_the_shipped_spec_has_a_craft_column_for_every_limited_template(self):
        s = ladder.load_spec()
        assert set(s["craft_column"]) >= set(s["class_limits"])
        assert set(s["craft_column"].values()) == {"sword", "axe", "polearm"}

    def test_class_target_puts_the_best_culture_at_the_class_top_on_tier_10(self):
        s = class_spec()
        assert ladder.target(10, "rivendell", s, template="OneHandedSword") == approx(83)
        # Tier 5 is the class_tier_fraction share of the class top (2026-11-14), not the curve's.
        assert ladder.target(5, "rivendell", s, template="OneHandedSword") == approx(83 * 0.81)

    def test_class_target_uses_the_craft_column_of_the_template(self):
        s = class_spec()
        assert ladder.target(10, "rivendell", s, template="OneHandedAxe") == approx(70 * 0.9)
        assert ladder.target(10, "mordor", s, template="OneHandedSword") == approx(83 * 0.82)

    def test_class_target_takes_the_default_craft_for_an_unlisted_culture(self):
        s = class_spec()
        assert ladder.target(10, "nowhere", s, template="OneHandedSword") == approx(83 * 0.85)

    def test_class_target_ignores_the_kingdom_offset(self):
        """`craft` replaces `kingdom_offset` wherever a template is known; applying both would count twice."""
        s = class_spec(kingdom_offset={"rivendell": 0.5})
        assert ladder.target(10, "rivendell", s, template="OneHandedSword") == approx(83)

    def test_a_thrust_only_polearm_tops_out_at_the_thrust_limit(self):
        s = class_spec()
        spear = ladder.target(10, "rivendell", s, template="TwoHandedPolearm", swings=False, thrusts=True)
        halberd = ladder.target(10, "rivendell", s, template="TwoHandedPolearm", swings=True, thrusts=True)
        assert spear == approx(45 * 0.97)
        assert halberd == approx(138 * 0.97)

    def test_an_axe_that_cannot_thrust_ignores_the_null_thrust_limit(self):
        s = class_spec()
        assert ladder.target(10, "rivendell", s, template="OneHandedAxe", swings=True, thrusts=True) \
            == approx(70 * 0.9)

    def test_each_class_spreads_over_ten_tiers(self):
        """The reason for the per-class curve: the shared one passed the sword limit near tier 6."""
        s = class_spec()
        values = [ladder.target(t, "rivendell", s, template="OneHandedSword") for t in range(11)]
        assert all(b > a for a, b in zip(values, values[1:]))
        assert values[10] == approx(83)

    def test_a_spec_without_class_tier_fraction_takes_the_curve_share(self):
        """The legacy shape: curve[tier] / curve[10] until a spec carries class_tier_fraction."""
        s = class_spec()
        del s["class_tier_fraction"]
        assert ladder.target(5, "rivendell", s, template="OneHandedSword") == approx(83 * 69 / 114)
        assert ladder.tier_share(5, s) == approx(69 / 114)

    def test_tier_share_reads_class_tier_fraction_and_clamps_the_tier(self):
        s = class_spec()
        assert ladder.tier_share(0, s) == 0.58
        assert ladder.tier_share(10, s) == 1.0
        assert ladder.tier_share(14, s) == 1.0
        assert ladder.tier_share(-3, s) == 0.58

    def test_tier_limit_scales_all_four_fields_by_the_tier_share_and_craft(self):
        s = class_spec()
        limit = ladder.tier_limit("OneHandedSword", 3, "mordor", s)
        f = 0.72 * 0.82
        assert limit["swing"] == approx(95 * f)
        assert limit["thrust"] == approx(55 * f)
        assert limit["swing_dps"] == approx(83 * f)
        assert limit["thrust_dps"] == approx(47 * f)

    def test_tier_limit_on_the_top_tier_of_the_best_craft_is_the_class_limit(self):
        s = class_spec()
        assert ladder.tier_limit("OneHandedSword", 10, "rivendell", s) == SWORD_LIMIT

    def test_tier_limit_keeps_a_null_limit_null(self):
        limit = ladder.tier_limit("OneHandedAxe", 5, "rivendell", class_spec())
        assert limit["thrust"] is None and limit["thrust_dps"] is None
        assert limit["swing_dps"] == approx(70 * 0.81 * 0.9)

    def test_tier_limit_takes_the_default_craft_for_an_unlisted_culture(self):
        limit = ladder.tier_limit("OneHandedSword", 10, "nowhere", class_spec())
        assert limit["swing_dps"] == approx(83 * 0.85)

    def test_tier_limit_is_the_class_limit_where_no_per_class_target_exists(self):
        """No template column, no craft keys, or no limit at all: nothing to scale by."""
        assert ladder.tier_limit("Pike", 3, "rivendell", class_spec()) is None
        legacy = spec(class_limits=CLASS_LIMITS)
        assert ladder.tier_limit("OneHandedSword", 3, "rivendell", legacy) == SWORD_LIMIT

    def test_tier_limit_equals_the_dps_target_for_a_swinging_sword(self):
        """The DPS target and the DPS limit are one number, so a restat lands exactly on the limit."""
        s = class_spec()
        for tier in range(11):
            assert ladder.tier_limit("OneHandedSword", tier, "mordor", s)["swing_dps"] == approx(
                ladder.target(tier, "mordor", s, template="OneHandedSword"))

    def test_every_shipped_dps_limit_is_exactly_its_dps_target(self):
        """Both read one tier factor, so equality holds by construction and not to a rounding error."""
        s = ladder.load_spec()
        for template in s["class_limits"]:
            for craft in ("rivendell", "mordor", "no_such_culture"):
                for tier in range(11):
                    limit = ladder.tier_limit(template, tier, craft, s)
                    for stat, swings, thrusts in (("swing_dps", True, False), ("thrust_dps", False, True)):
                        if limit[stat]:
                            assert limit[stat] == ladder.target(tier, craft, s, template, swings, thrusts), (
                                template, craft, tier, stat)

    def test_dominant_flags_drop_the_swing_of_a_thrust_dominant_polearm(self):
        s = class_spec()
        assert ladder.dominant_flags("TwoHandedPolearm", 40, 60, 20.0, 31.0, s) == (False, True)

    def test_dominant_flags_keep_the_swing_of_a_swing_dominant_halberd(self):
        s = class_spec()
        assert ladder.dominant_flags("TwoHandedPolearm", 80, 40, 60.0, 31.0, s) == (True, True)
        # A tie is swing-dominant: the class top moves to the thrust only when thrust DPS is higher.
        assert ladder.dominant_flags("TwoHandedPolearm", 80, 40, 31.0, 31.0, s) == (True, True)

    def test_dominant_flags_leave_a_sword_untouched_even_when_its_thrust_is_higher(self):
        s = class_spec()
        assert ladder.dominant_flags("OneHandedSword", 30, 50, 20.0, 40.0, s) == (True, True)

    def test_dominant_flags_pass_a_single_attack_through(self):
        s = class_spec()
        assert ladder.dominant_flags("TwoHandedPolearm", 0, 60, 0.0, 31.0, s) == (False, True)
        assert ladder.dominant_flags("OneHandedAxe", 70, 0, 50.0, 0.0, s) == (True, False)

    def test_dominant_flags_without_a_craft_column_change_nothing(self):
        legacy = spec(class_limits=CLASS_LIMITS)
        assert ladder.dominant_flags("TwoHandedPolearm", 40, 60, 20.0, 31.0, legacy) == (True, True)

    def test_a_thrust_dominant_polearm_is_targeted_from_the_thrust_top(self):
        s = class_spec()
        swings, thrusts = ladder.dominant_flags("TwoHandedPolearm", 40, 60, 20.0, 31.0, s)
        assert ladder.target(10, "rivendell", s, "TwoHandedPolearm", swings, thrusts) \
            == approx(45 * 0.97)

    def test_craft_key_maps_a_troop_id_prefix_to_a_craft_row(self):
        s = class_spec()
        assert ladder.craft_key("mordor_num_swordsman", "empire", s) == "mordor"
        assert ladder.craft_key("gondor_levy", "gondor", s) == "gondor"

    def test_craft_key_without_a_prefix_table_is_the_culture(self):
        s = class_spec()
        del s["craft_troop_prefix"]
        assert ladder.craft_key("mordor_num_swordsman", "empire", s) == "empire"

    def test_the_shipped_spec_rates_the_black_numenoreans_by_their_own_craft_row(self):
        s = ladder.load_spec()
        assert ladder.craft_key("mordor_num_captain", "mordor", s) == "mordor_numenorean"
        assert ladder.craft_key("mordor_orc_archer", "mordor", s) == "mordor"
        assert s["craft"]["mordor_numenorean"] == {"sword": 0.90, "axe": 0.88, "polearm": 0.90}

    def test_the_shipped_tier_fraction_matches_the_decided_values(self):
        s = ladder.load_spec()
        assert [s["class_tier_fraction"][str(t)] for t in range(11)] == [
            0.58, 0.62, 0.66, 0.72, 0.77, 0.81, 0.88, 0.91, 0.94, 0.97, 1.0]

    def test_the_shipped_craft_rows_are_the_decided_values(self):
        craft = ladder.load_spec()["craft"]
        assert craft["vlandia"] == {"sword": 0.88, "axe": 0.85, "polearm": 1.0}
        assert craft["umbar"] == {"sword": 0.88, "axe": 0.85, "polearm": 0.90}
        assert craft["shaghana"] == craft["umbar"]
        assert craft["feral_orcs"] == {"sword": 0.82, "axe": 0.85, "polearm": 0.82}

    def test_the_shipped_spec_keeps_every_one_handed_blow_under_a_hundred_at_every_tier(self):
        """R1a: no ordinary one handed weapon displays 100 or more; the class limits hold the top at 95."""
        s = ladder.load_spec()
        for template in ("Dagger", "Mace", "OneHandedAxe", "OneHandedSword"):
            for tier in range(11):
                for culture in list(s["craft"]) + ["nowhere"]:
                    limit = ladder.tier_limit(template, tier, culture, s)
                    assert max(v for k, v in limit.items() if k in ("swing", "thrust") and v) < 100

    def test_without_a_template_the_target_is_the_legacy_one(self):
        s = class_spec()
        assert ladder.target(5, "rivendell", s) == approx(69 * 1.10)
        assert ladder.band(5, "rivendell", s).low == approx(69 * 1.10 * 0.75)

    def test_a_template_without_a_class_limit_takes_the_legacy_target(self):
        s = class_spec()
        assert ladder.target(5, "rivendell", s, template="Pike") == approx(69 * 1.10)

    def test_a_spec_without_craft_keys_takes_the_legacy_target(self):
        """A spec written before the craft keys must still mean what it meant."""
        s = spec(class_limits=CLASS_LIMITS)
        assert ladder.target(5, "nowhere", s, template="OneHandedSword") == approx(69)

    def test_class_band_is_narrower_than_the_legacy_band(self):
        s = class_spec()
        b = ladder.band(10, "rivendell", s, template="OneHandedSword")
        assert b.low == approx(83 * 0.85)
        assert b.high == approx(83 * 1.15)

    def test_findings_judges_an_extended_row_by_its_class_band(self):
        s = class_spec()
        # A tier 10 Rivendell swordsman with a 100 DPS sword: inside the legacy band (114 +/- 25%),
        # above the one handed sword band (83 +/- 15%).
        long_row = [("lord", "rivendell", 10, "sword", 100.0, "b", "OneHandedSword", True, False)]
        short_row = [("lord", "rivendell", 10, "sword", 100.0, "b")]
        assert [f.kind for f in ladder.findings(long_row, s)] == ["over"]
        assert ladder.findings(short_row, s) == []

    def test_findings_passes_the_attack_flags_to_the_target(self):
        s = class_spec()
        thrust_only = [("g", "rivendell", 10, "spear", 45.0, "b", "TwoHandedPolearm", False, True)]
        both = [("g", "rivendell", 10, "spear", 45.0, "b", "TwoHandedPolearm", True, True)]
        assert ladder.findings(thrust_only, s) == []
        assert [f.kind for f in ladder.findings(both, s)] == ["under"]

    def test_validate_spec_rejects_a_broken_craft_or_hero_table(self):
        for broken, expect in [
            ({"craft": {"x": {"sword": 1.2, "axe": 1.0, "polearm": 1.0}}}, "craft"),
            ({"craft": {"x": {"sword": 1.0, "axe": 1.0}}}, "polearm"),
            ({"craft_default": {"sword": 0.9, "axe": 0.9}}, "polearm"),
            ({"craft_column": {"OneHandedSword": "sword"}}, "OneHandedAxe"),
            ({"craft_column": {**CRAFT_COLUMN, "OneHandedSword": "knife"}}, "knife"),
            ({"class_band": 1.5}, "class_band"),
            ({"hero_bonus": {"other": 5}}, "high_elven_dwarven"),
            ({"hero_bonus": {"high_elven_dwarven": 10}}, "other"),
            ({"hero_makers": {"gone_blade": "other"}}, "gone_blade"),
            ({"class_tier_fraction": {k: v for k, v in TIER_FRACTION.items() if k != "4"}}, "tier"),
            ({"class_tier_fraction": {**TIER_FRACTION, "4": 0.66}}, "increasing"),
            ({"class_tier_fraction": {**TIER_FRACTION, "3": 0.60, "2": 0.66}}, "increasing"),
            ({"class_tier_fraction": {**TIER_FRACTION, "0": 0.0}}, "class_tier_fraction"),
            ({"class_tier_fraction": {**TIER_FRACTION, "10": 1.2}}, "class_tier_fraction"),
            ({"class_tier_fraction": {**TIER_FRACTION, "10": 0.99}}, "1.0"),
            ({"class_tier_fraction": {**TIER_FRACTION, "11": 1.0}}, "tier"),
            ({"class_tier_fraction": [0.5, 1.0]}, "class_tier_fraction"),
            ({"craft_troop_prefix": {"x_": "no_such_row"}}, "no_such_row"),
            ({"craft_troop_prefix": ["x_"]}, "craft_troop_prefix"),
        ]:
            with self.subTest(expect=expect):
                problems = ladder.validate_spec(class_spec(**broken))
                assert any(expect in p for p in problems), problems

    def test_validate_spec_accepts_the_class_spec(self):
        assert ladder.validate_spec(class_spec()) == []

    def test_hero_limit_raises_each_dps_by_the_bonus_and_each_blow_in_proportion(self):
        s = class_spec()
        limit = ladder.hero_limit("hero_blade", "OneHandedSword", s)
        assert limit["swing_dps"] == 93
        assert limit["thrust_dps"] == 57
        assert limit["swing"] == round(95 * 93 / 83)
        assert limit["thrust"] == round(55 * 57 / 47)

    def test_hero_limit_leaves_a_null_limit_null(self):
        limit = ladder.hero_limit("hero_blade", "OneHandedAxe", class_spec())
        assert limit["thrust"] is None and limit["thrust_dps"] is None
        assert limit["swing_dps"] == 80

    def test_a_hero_blade_with_no_maker_takes_the_other_bonus(self):
        s = class_spec(hero_makers={})
        assert ladder.hero_limit("hero_blade", "OneHandedSword", s)["swing_dps"] == 88

    def test_hero_limit_is_none_for_a_template_without_a_class_limit(self):
        assert ladder.hero_limit("hero_blade", "Pike", class_spec()) is None

    def test_limit_for_picks_the_hero_limit_only_for_a_hero_blade(self):
        s = class_spec()
        assert ladder.limit_for("hero_blade", "OneHandedSword", s)["swing_dps"] == 93
        assert ladder.limit_for("plain", "OneHandedSword", s)["swing_dps"] == 83

    def test_limit_for_takes_the_tier_limit_for_an_ordinary_blade_when_given_a_tier(self):
        s = class_spec()
        assert ladder.limit_for("plain", "OneHandedSword", s, 3, "mordor") \
            == ladder.tier_limit("OneHandedSword", 3, "mordor", s)
        assert ladder.limit_for("plain", "OneHandedSword", s, 0, "mordor") \
            == ladder.tier_limit("OneHandedSword", 0, "mordor", s)
        assert ladder.limit_for("plain", "OneHandedSword", s) == ladder.class_limit("OneHandedSword", s)

    def test_limit_for_keeps_a_hero_blade_on_its_hero_limit_at_every_tier(self):
        s = class_spec()
        assert ladder.limit_for("hero_blade", "OneHandedSword", s, 3, "mordor") \
            == ladder.hero_limit("hero_blade", "OneHandedSword", s)

    def test_a_hero_blade_over_its_hero_limit_is_a_blow_finding(self):
        s = class_spec(exempt_troops={})
        over = [("lord", "anduril", "OneHandedSword", "hero_blade", 120, 0, 100.0, 0.0)]
        inside = [("lord", "anduril", "OneHandedSword", "hero_blade", 100, 0, 90.0, 0.0)]
        found = ladder.blow_findings(over, s)
        assert [f.item for f in found] == ["anduril"]
        assert "hero" in found[0].describe()
        assert ladder.blow_findings(inside, s) == []

    def test_a_carried_weapon_is_judged_by_its_carriers_tier_limit(self):
        """2026-11-14: a tier 3 troop may not carry a blade that only a tier 10 troop may swing."""
        s = class_spec()
        # 80 swing is inside the class limit of 95 but above the tier 3 limit of 95 x 0.72 x 0.85.
        sword = ("t3", "blade_80", "OneHandedSword", "plain", 80, 0, 60.0, 0.0)
        assert ladder.blow_findings([sword], s) == []
        found = ladder.blow_findings([sword + (3, "nowhere")], s)
        assert [f.item for f in found] == ["blade_80"]
        assert "tier limit" in found[0].describe()
        top = ("t10", "blade_80", "OneHandedSword", "plain", 80, 0, 60.0, 0.0)
        assert ladder.blow_findings([top + (10, "rivendell")], s) == []

    def test_a_carried_hero_blade_keeps_its_hero_limit_whatever_the_tier(self):
        s = class_spec()
        inside = [("lord", "anduril", "OneHandedSword", "hero_blade", 100, 0, 90.0, 0.0, 3, "rivendell")]
        assert ladder.blow_findings(inside, s) == []

    def test_a_weapon_carried_at_two_tiers_is_reported_once_with_its_worst_breach(self):
        s = class_spec()
        rows = [
            ("t5", "blade_80", "OneHandedSword", "plain", 80, 0, 60.0, 0.0, 5, "rivendell"),
            ("t2", "blade_80", "OneHandedSword", "plain", 80, 0, 60.0, 0.0, 2, "rivendell"),
            ("t10", "blade_80", "OneHandedSword", "plain", 80, 0, 60.0, 0.0, 10, "rivendell"),
        ]
        found = ladder.blow_findings(rows, s)
        assert [f.item for f in found] == ["blade_80"]
        assert found[0].troops == ("t2", "t5")
        assert ladder.tier_limit("OneHandedSword", 2, "rivendell", s)["swing"] \
            == approx(found[0].breaches[0][2])

    def test_every_shipped_hero_blade_has_a_maker(self):
        """A blade with no maker silently takes `other`; the table should say so on purpose."""
        s = ladder.load_spec()
        assert set(s["hero_blades"]) == set(s["hero_makers"])

    def test_the_report_marks_exactly_the_spec_hero_blades(self):
        """analyze_melee_ladder kept its own copy of 18 blades while the spec listed 26."""
        assert set(report.HERO_BLADE_IDS) == set(ladder.load_spec()["hero_blades"])


# --- the roster fixer under per-class bands -------------------------------------------------


def thrust_weapon(item_id, template, dps):
    """A thrust-only `Priced` whose best_dps is `dps`."""
    # A fast thrust (raw 12.0): at the slow 3.85 a 40 DPS spear showed a 67 thrust, above the tier
    # limit of 58, and the fixer now refuses a candidate over its carrier's tier limit (2026-11-14).
    cycle = md.thrust_cycle_time(12.0, md.STUN_PERIOD_THRUST)
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"thrust"}),
        md.WeaponStats(thrust_damage=dps * cycle, raw_thrust_speed=12.0),
    )
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", template, (("blade", "Blade", 100),), "src"),
        usages=(usage,), blade_piece="blade", tier=3,
    )


class RosterFixerUnderClassTests(TmpCase):
    def test_a_swap_candidate_is_judged_against_its_own_class_band(self):
        """A halberd and a spear share the template TwoHandedPolearm but not the DPS curve.

        The spear tops at 45 DPS and the halberd at 138. A halberd sitting exactly on the spear's
        target is far under its own band, so it must not replace a spear.
        """
        s = class_spec()
        priced = {p.item.id: p for p in (
            thrust_weapon("wm_x_spear_weak", "TwoHandedPolearm", 20.0),
            thrust_weapon("wm_x_spear_good", "TwoHandedPolearm", 40.0),
            weapon("wm_x_halberd", "TwoHandedPolearm", dps=43.65),
        )}
        t = troop("lord", 51, "rivendell", ["wm_x_spear_weak"])
        owner = troop("owner", 51, "rivendell", ["wm_x_spear_good", "wm_x_halberd"])
        swaps, _unresolved = fixer.plan(priced, cata.Catalogue(), [t, owner], s, shields=set())
        assert [(sw.old, sw.new) for sw in swaps if sw.troop == "lord"] == [
            ("wm_x_spear_weak", "wm_x_spear_good")]

    def test_a_troop_is_judged_by_the_class_band_of_each_weapon_it_carries(self):
        s = class_spec()
        priced = {"wm_x_sword": weapon("wm_x_sword", "OneHandedSword", dps=83.0)}
        t = troop("lord", 51, "rivendell", ["wm_x_sword"])   # tier 10 target 83
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [t], s, shields=set())
        assert swaps == [] and unresolved == []

    def test_the_fixer_rates_a_black_numenorean_by_the_numenorean_craft_row(self):
        """mordor_num_ troops are filed under mordor but are not judged as orcs (craft_key)."""
        s = class_spec(
            craft={"mordor": {"sword": 0.82, "axe": 0.85, "polearm": 0.82},
                   "mordor_numenorean": {"sword": 0.90, "axe": 0.88, "polearm": 0.90}},
            craft_troop_prefix={"mordor_num_": "mordor_numenorean"},
        )
        priced = {p.item.id: p for p in (
            weapon("wm_x_sword_weak", dps=20.0),
            # Tier 5: inside the orc band (0.82 row, 46.9 to 63.4), under the Numenorean one (0.90
            # row, 51.4 to 69.6). The fixer also holds a pick to the carrier's tier limit, whose DPS
            # is the target, so no weapon can sit above the orc band top and inside the Numenorean
            # limit: the row is told apart from below.
            weapon("wm_x_sword_strong", dps=49.0),
        )}
        num = troop("mordor_num_swordsman", 26, "mordor", ["wm_x_sword_weak"])
        orc = troop("mordor_orc_swordsman", 26, "mordor", ["wm_x_sword_weak"])
        owner = troop("mordor_owner", 21, "mordor", ["wm_x_sword_strong"])   # tier 4, 0.82: 49 in band
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [num, orc, owner], s, shields=set())
        assert [(sw.troop, sw.new) for sw in swaps] == [("mordor_orc_swordsman", "wm_x_sword_strong")]
        assert [t.id for t, _i, _why in unresolved] == ["mordor_num_swordsman"]

    def test_the_fixer_judges_a_thrust_dominant_polearm_by_the_thrust_top(self):
        """A spear at 36 DPS is on a tier 5 target of 45 x 0.81 x 0.97, not 108 DPS under a halberd's."""
        s = class_spec()
        usage = cata.Usage(
            "D", True, (), "set", frozenset({"swing", "thrust"}),
            md.WeaponStats(swing_damage=20, raw_swing_speed=20.8, thrust_damage=35.5, raw_thrust_speed=12.0),
        )
        spear = cata.Priced(
            item=cata.Item("wm_x_spear", "wm_x_spear", "", "TwoHandedPolearm", (("blade", "Blade", 100),), "src"),
            usages=(usage,), blade_piece="blade", tier=3,
        )
        assert spear.thrust_dps > spear.swing_dps
        t = troop("guard", 26, "rivendell", ["wm_x_spear"])
        swaps, unresolved = fixer.plan({"wm_x_spear": spear}, cata.Catalogue(), [t], s, shields=set())
        assert swaps == [] and unresolved == []


def heavy_blow(item_id, swing_damage, dps, template="OneHandedSword"):
    """A swing-only `Priced` with a large blow and a chosen DPS: a slow, heavy weapon."""
    raw = 20.8 / (swing_damage / dps - 0.1)
    usage = cata.Usage("D", True, (), "set", frozenset({"swing"}),
                       md.WeaponStats(swing_damage=swing_damage, raw_swing_speed=raw))
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", template, (("blade", "Blade", 100),), "src"),
        usages=(usage,), blade_piece="blade", tier=3,
    )


class RosterFixerHelpTests(TmpCase):
    def test_culture_help_says_it_names_an_engine_culture(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit):
            fixer.main(["--help"])
        assert "engine culture" in " ".join(out.getvalue().split())


class RosterFixerTierLimitTests(TmpCase):
    """M5 (2026-11-14, #1271 deep review): a swap target is picked by band, and a band is a DPS
    window. A heavy slow weapon sits in the window and still shows a blow no tier 5 troop may carry."""

    def setUp(self):
        super().setUp()
        self.s = class_spec()
        # Tier 5 rivendell sword: target 83 x 0.81 = 67 DPS; the tier limit allows a 77 blow.
        self.good = heavy_blow("wm_x_sword_good", 70, 62.0)
        self.heavy = heavy_blow("wm_x_sword_heavy", 90, 67.0)
        self.weak = weapon("wm_x_sword_weak", "OneHandedSword", dps=10.0)

    def test_the_candidates_are_both_in_band_and_only_one_fits_the_tier_limit(self):
        limit = ladder.tier_limit("OneHandedSword", 5, "rivendell", self.s)
        for p in (self.good, self.heavy):
            assert ladder.band(5, "rivendell", self.s, "OneHandedSword").verdict(p.best_dps) == "ok"
        assert not ladder.limit_breaches(limit, self.good.swing_damage, 0, self.good.swing_dps, 0.0)
        assert ladder.limit_breaches(limit, self.heavy.swing_damage, 0, self.heavy.swing_dps, 0.0)

    def test_a_swap_never_lands_a_weapon_above_the_carriers_tier_limit(self):
        priced = {p.item.id: p for p in (self.good, self.heavy, self.weak)}
        t = troop("lord", 26, "rivendell", ["wm_x_sword_weak"])    # tier 5
        owner = troop("owner", 26, "rivendell", ["wm_x_sword_good", "wm_x_sword_heavy"])
        swaps, _u = fixer.plan(priced, cata.Catalogue(), [t, owner], self.s, shields=set())
        assert [(sw.old, sw.new) for sw in swaps if sw.troop == "lord"] == [
            ("wm_x_sword_weak", "wm_x_sword_good")]

    def test_a_slot_with_only_over_limit_candidates_is_unresolved_for_that_reason(self):
        priced = {p.item.id: p for p in (self.heavy, self.weak)}
        t = troop("lord", 26, "rivendell", ["wm_x_sword_weak"])
        owner = troop("owner", 26, "rivendell", ["wm_x_sword_heavy"])
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [t, owner], self.s, shields=set())
        assert [sw for sw in swaps if sw.troop == "lord"] == []
        assert any(tr.id == "lord" and "tier limit" in why for tr, _i, why in unresolved), unresolved

    def test_the_carriers_craft_row_sets_the_limit(self):
        """A mordor troop (sword craft 0.82) has a lower limit than a rivendell one (1.0)."""
        mid = heavy_blow("wm_x_sword_mid", 76, 55.0)
        limit_riv = ladder.tier_limit("OneHandedSword", 5, "rivendell", self.s)
        limit_mor = ladder.tier_limit("OneHandedSword", 5, "mordor", self.s)
        assert not ladder.limit_breaches(limit_riv, 76, 0, 55.0, 0.0)
        assert ladder.limit_breaches(limit_mor, 76, 0, 55.0, 0.0)
        priced = {p.item.id: p for p in (mid, self.weak)}
        t = troop("orc", 26, "mordor", ["wm_x_sword_weak"])
        owner = troop("owner", 26, "mordor", ["wm_x_sword_mid"])
        swaps, _u = fixer.plan(priced, cata.Catalogue(), [t, owner], self.s, shields=set())
        assert [sw for sw in swaps if sw.troop == "orc"] == []


if __name__ == "__main__":
    unittest.main()


def couch_spear(item_id, dps):
    """A thrust-only polearm with a couch usage, as `couchable_ids` reads one."""
    plain = thrust_weapon(item_id, "TwoHandedPolearm", dps)
    u = plain.usages[0]
    usage = cata.Usage(u.description, u.is_primary, ("couch",), u.usage_set, u.strikes, u.stats)
    return cata.Priced(item=plain.item, usages=(usage,), blade_piece=plain.blade_piece, tier=plain.tier)


class RosterFixerWeaponCategoryTests(TmpCase):
    """The maintainer's rule (2026-11-14): a troop keeps its weapon category.

    The defect these pin: the 2026-11-14 plan replaced poleaxes with short spears
    (`numenorean_poleaxe_heavy` 92 DPS to `dunland_caerdh_short_spear_e` 33), because both are
    TwoHandedPolearm and each sat in its own class band.
    """

    def test_an_over_armed_poleaxe_is_never_swapped_for_a_spear(self):
        s = class_spec()
        priced = {p.item.id: p for p in (
            weapon("wm_x_poleaxe", "TwoHandedPolearm", dps=200.0),
            thrust_weapon("wm_x_spear", "TwoHandedPolearm", 40.0),
        )}
        t = troop("lord", 51, "rivendell", ["wm_x_poleaxe"])
        owner = troop("owner", 51, "rivendell", ["wm_x_spear"])
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [t, owner], s, shields=set())
        assert [sw for sw in swaps if sw.troop == "lord"] == []
        assert [(tr.id, i) for tr, i, _why in unresolved] == [("lord", "wm_x_poleaxe")]

    def test_an_under_armed_poleaxe_is_never_given_a_weaker_spear(self):
        """A spear inside the spear band is still a weaker weapon than the poleaxe it would replace."""
        s = class_spec()
        priced = {p.item.id: p for p in (
            weapon("wm_x_poleaxe", "TwoHandedPolearm", dps=60.0),
            thrust_weapon("wm_x_spear", "TwoHandedPolearm", 40.0),
        )}
        t = troop("lord", 51, "rivendell", ["wm_x_poleaxe"])
        owner = troop("owner", 51, "rivendell", ["wm_x_spear"])
        swaps, _unresolved = fixer.plan(priced, cata.Catalogue(), [t, owner], s, shields=set())
        assert [sw for sw in swaps if sw.troop == "lord"] == []

    def test_a_lance_is_only_swapped_for_another_lance(self):
        s = class_spec()
        priced = {p.item.id: p for p in (
            couch_spear("wm_x_lance_weak", 20.0),
            thrust_weapon("wm_x_spear", "TwoHandedPolearm", 40.0),
            couch_spear("wm_x_lance_good", 39.0),
        )}
        rider = troop("rider", 51, "rivendell", ["wm_x_lance_weak"])
        owner = troop("owner", 51, "rivendell", ["wm_x_spear", "wm_x_lance_good"])
        swaps, _unresolved = fixer.plan(priced, cata.Catalogue(), [rider, owner], s, shields=set())
        assert [(sw.old, sw.new) for sw in swaps if sw.troop == "rider"] == [
            ("wm_x_lance_weak", "wm_x_lance_good")]

    def test_a_plain_spear_is_never_swapped_for_a_lance(self):
        s = class_spec()
        priced = {p.item.id: p for p in (
            thrust_weapon("wm_x_spear_weak", "TwoHandedPolearm", 20.0),
            couch_spear("wm_x_lance_good", 39.0),
        )}
        foot = troop("foot", 51, "rivendell", ["wm_x_spear_weak"])
        owner = troop("owner", 51, "rivendell", ["wm_x_lance_good"])
        swaps, unresolved = fixer.plan(priced, cata.Catalogue(), [foot, owner], s, shields=set())
        assert [sw for sw in swaps if sw.troop == "foot"] == []
        assert [(tr.id, i) for tr, i, _why in unresolved] == [("foot", "wm_x_spear_weak")]
