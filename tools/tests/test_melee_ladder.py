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

import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import fix_melee_ladder as fixer  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402
import melee_ladder as ladder  # noqa: E402


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


def test_the_shipped_spec_is_valid():
    """The file that actually ships must load, or every gate reading it checks nothing."""
    assert ladder.validate_spec(ladder.load_spec()) == []


def test_the_shipped_spec_has_no_stale_troop_exemptions():
    """Measured against the real rosters: an entry for a deleted troop skips nothing silently."""
    ids = {t.id for t in report.load_troops(report.TROOPS)}
    assert ladder.stale_exemptions(ids, ladder.load_spec()) == []


def test_target_applies_the_kingdom_offset():
    s = spec()
    assert ladder.target(5, "rivendell", s) == pytest.approx(69 * 1.10)
    assert ladder.target(5, "mordor", s) == pytest.approx(69 * 0.98)
    # A culture with no entry takes the plain tier target.
    assert ladder.target(5, "nowhere", s) == pytest.approx(69)


def test_target_clamps_a_tier_outside_the_curve():
    s = spec()
    assert ladder.target(99, "x", s) == ladder.target(10, "x", s)
    assert ladder.target(-3, "x", s) == ladder.target(0, "x", s)


def test_band_brackets_the_target():
    b = ladder.band(5, "nowhere", spec())
    assert b.low == pytest.approx(69 * 0.75)
    assert b.high == pytest.approx(69 * 1.25)
    assert b.verdict(69) == "ok"
    assert b.verdict(10) == "under"
    assert b.verdict(200) == "over"


@pytest.mark.parametrize(
    "broken,expect",
    [
        ({"curve": {}}, "no `curve`"),
        ({"band": 5}, "band"),
        ({"ceiling": -1}, "ceiling"),
        ({"anchor_tier": 99}, "anchor_tier"),
    ],
)
def test_validate_spec_rejects_a_broken_spec(broken, expect):
    problems = ladder.validate_spec(spec(**broken))
    assert problems
    assert any(expect in p for p in problems)


def test_validate_spec_rejects_a_curve_that_does_not_climb():
    """The spec exists to make the ladder climb. One that does not is the bug it fixes."""
    flat = {str(t): 50 for t in range(11)}
    problems = ladder.validate_spec(spec(curve=flat))
    assert any("not strictly increasing" in p for p in problems)


def test_validate_spec_rejects_a_ceiling_below_the_curve():
    problems = ladder.validate_spec(spec(ceiling=50))
    assert any("ceiling" in p and "below the top" in p for p in problems)


def test_validate_spec_rejects_a_curve_with_a_missing_tier():
    curve = {str(t): 10 * (t + 1) for t in range(11)}
    del curve["7"]
    assert any("tier(s) 7" in p for p in ladder.validate_spec(spec(curve=curve)))


def test_load_spec_refuses_a_missing_file():
    with pytest.raises(ladder.LadderError, match="missing"):
        ladder.load_spec(Path("no/such/spec.json"))


def test_load_spec_refuses_a_self_contradicting_file(tmp_path):
    bad = tmp_path / "s.json"
    bad.write_text(json.dumps(spec(band=9)), encoding="utf-8")
    with pytest.raises(ladder.LadderError, match="self-contradicting"):
        ladder.load_spec(bad)


# --- findings -----------------------------------------------------------------------------


def test_findings_reports_both_directions_and_the_ceiling():
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


def test_a_hero_blade_is_exempt_from_the_ceiling_but_not_from_the_band():
    s = spec()
    kits = [("lord", "nowhere", 10, "anduril", 200.0, "hero_blade")]
    kinds = {f.kind for f in ladder.findings(kits, s)}
    assert "ceiling" not in kinds   # a hero weapon may out-sustain everything
    assert "over" in kinds          # but a line troop holding one is still a ladder problem


def test_an_exempt_troop_is_skipped_entirely():
    s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
    kits = [("cave_troll", "mordor", 10, "club", 5.0, "b")]
    assert ladder.findings(kits, s) == []


def test_findings_are_worst_first_and_respect_min_gap():
    s = spec()
    kits = [
        ("a", "nowhere", 5, "w", 50.0, "b"),   # just under the 51.75 floor
        ("b", "nowhere", 5, "w", 5.0, "b"),    # far under
    ]
    found = ladder.findings(kits, s)
    assert [f.troop for f in found] == ["b", "a"]
    assert [f.troop for f in ladder.findings(kits, s, min_gap=10)] == ["b"]


def test_stale_exemptions_names_troops_that_no_longer_exist():
    s = spec(exempt_troops={"gone": "why", "here": "why"})
    assert ladder.stale_exemptions({"here"}, s) == ["gone"]


# --- the roster fixer ---------------------------------------------------------------------


def test_family_reads_the_id_prefix_not_the_culture_attribute():
    assert fixer.family("wm_gondor_sword_a01") == "wm_gondor"
    assert fixer.family("sm_uruk_halberd_b") == "sm_uruk"


def test_culture_is_a_hard_constraint_not_a_preference():
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


def test_a_swap_stays_inside_the_troops_own_weapon_class():
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


def test_a_swap_is_emitted_for_every_slot_the_weapon_sits_in():
    """Six troops carry one melee weapon in two slots; a single swap would half-apply it."""
    priced = {
        "wm_x_axe_a": weapon("wm_x_axe_a", "OneHandedAxe", dps=200.0),
        "wm_x_axe_b": weapon("wm_x_axe_b", "OneHandedAxe", dps=34.0),
    }
    t = troop("t", 6, "x", ["wm_x_axe_a", "wm_x_axe_b"],
              slots={"wm_x_axe_a": frozenset({"Item0", "Item2"}),
                     "wm_x_axe_b": frozenset({"Item1"})})
    swaps, _unres = fixer.plan(priced, cata.Catalogue(), [t], spec(), shields=set())
    moved = {s.slot for s in swaps if s.old == "wm_x_axe_a"}
    assert moved == {"Item0", "Item2"}


def test_a_troop_already_in_band_is_left_alone():
    priced = {"wm_x_sword_a": weapon("wm_x_sword_a", "OneHandedSword", dps=69.0)}
    troops = [troop("t", 26, "x", ["wm_x_sword_a"])]   # level 26 -> tier 5, target 69
    swaps, unresolved = fixer.plan(priced, cata.Catalogue(), troops, spec(), shields=set())
    assert swaps == []
    assert unresolved == []


def test_an_exempt_troop_is_never_swapped():
    priced = {
        "wm_x_club_a": weapon("wm_x_club_a", "TwoHandedMace", dps=5.0),
        "wm_x_club_b": weapon("wm_x_club_b", "TwoHandedMace", dps=114.0),
    }
    t = troop("cave_troll", 51, "mordor", ["wm_x_club_a"])
    s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
    swaps, _u = fixer.plan(priced, cata.Catalogue(), [t], s, shields=set())
    assert swaps == []


def test_improvement_is_positive_for_a_swap_toward_target():
    sw = fixer.Swap("t", "f", "c", 5, "Item0", "old", "new",
                    old_dps=200.0, new_dps=70.0, target=69.0, reason="x")
    assert sw.improvement > 0


def test_shield_forbidden_reads_the_resolved_usage_set_flags():
    """The trap CLAUDE.md records as shipped three times: a shield plus a no-shield polearm."""
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


def test_troop_carries_shield_detects_any_shield_in_the_kit():
    t = troop("t", 10, "x", ["sword", "round_shield"])
    assert fixer.troop_carries_shield(t, {"round_shield"}) is True
    assert fixer.troop_carries_shield(t, {"other_shield"}) is False
