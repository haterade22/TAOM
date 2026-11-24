#!/usr/bin/env python3
"""Tests for `tools/restat_melee_blades.py`, pass 2 of the melee ladder fix (#631).

This tool writes the UNVERSIONED Armory, where there is no git safety net, so the tests lean
hard on the write path: only `damage_factor` may move, the document must still parse, a backup
must exist, and the backup must never take a `.xml` extension (the engine globs those folders
and an `.xml` backup injects duplicate crafting piece ids).

Two guards are pinned by name because both would be invisible defects. Restatting a couchable
lance multiplies a hit the DPS model cannot see, and restatting a ladder-exempt troop's weapon
quadruples a monster's club to fix a number nobody reads.
"""

from __future__ import annotations

import contextlib
import dataclasses
import io
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402
import melee_ladder as ladder  # noqa: E402
import restat_melee_blades as restat  # noqa: E402
import taom_schema as ts  # noqa: E402


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


# Local rather than imported from the sibling test module: pytest does not put the tests
# directory on sys.path, and a cross-test import couples two files for two small helpers.
def spec(**over):
    base = {
        "anchor_tier": 5,
        "curve": {str(t): v for t, v in enumerate([25, 34, 43, 52, 60, 69, 78, 87, 96, 105, 114])},
        "band": 0.25,
        "kingdom_offset": {},
        "ceiling": 140,
        "hero_blades": [],
        "exempt_troops": {},
    }
    base.update(over)
    return base


def troop(tid, level, culture, items):
    return report.Troop(tid, tid, level, culture, "troops_x.xml", frozenset(items),
                        path="troops_x.xml",
                        slots_of={i: frozenset({"Item0"}) for i in items})


PIECES_XML = """<?xml version="1.0" encoding="utf-8"?>
<CraftingPieces>
    <CraftingPiece
        id="blade_one"
        name="One"
        piece_type="Blade"
        length="70"
        weight="1.0">
        <BladeData
            physics_material="metal_weapon">
            <Thrust
                damage_type="Pierce"
                damage_factor="2.00" />
            <Swing
                damage_type="Cut"
                damage_factor="4.00" />
        </BladeData>
    </CraftingPiece>
    <CraftingPiece
        id="blade_two"
        name="Two"
        piece_type="Blade"
        length="70"
        weight="1.0">
        <BladeData
            physics_material="metal_weapon">
            <Thrust
                damage_type="Pierce"
                damage_factor="1.50" />
            <Swing
                damage_type="Cut"
                damage_factor="3.00" />
        </BladeData>
    </CraftingPiece>
</CraftingPieces>
"""


def weapon(item_id, dps, blade="blade_one", template="OneHandedSword"):
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"swing"}),
        md.WeaponStats(swing_damage=dps * 1.1, raw_swing_speed=20.8),
    )
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", template, ((blade, "Blade", 100),), "src"),
        usages=(usage,), blade_piece=blade, tier=3,
    )


def catalogue_with_pieces():
    cat = cata.Catalogue()
    for node in ET.fromstring(PIECES_XML).iter("CraftingPiece"):
        piece = md.parse_piece(node)
        cat.pieces[piece.id] = piece
    return cat


# --- the rewrite --------------------------------------------------------------------------


class RewriteTests(TmpCase):
    def test_rewrite_changes_only_the_named_piece(self):
        out, changed = restat.rewrite(PIECES_XML, {"blade_one": (6.0, 3.0)})
        assert changed == 1
        root = ET.fromstring(out)
        by_id = {p.get("id"): p for p in root.iter("CraftingPiece")}
        one = by_id["blade_one"].find("BladeData")
        two = by_id["blade_two"].find("BladeData")
        assert one.find("Swing").get("damage_factor") == "6.00"
        assert one.find("Thrust").get("damage_factor") == "3.00"
        # The untouched piece keeps both of its factors exactly.
        assert two.find("Swing").get("damage_factor") == "3.00"
        assert two.find("Thrust").get("damage_factor") == "1.50"

    def test_rewrite_touches_nothing_else_about_the_piece(self):
        out, _ = restat.rewrite(PIECES_XML, {"blade_one": (6.0, 3.0)})
        before = {p.get("id"): dict(p.attrib) for p in ET.fromstring(PIECES_XML).iter("CraftingPiece")}
        after = {p.get("id"): dict(p.attrib) for p in ET.fromstring(out).iter("CraftingPiece")}
        assert before == after
        # Damage TYPES must survive: a restat changes how hard, never how it hurts.
        for doc in (PIECES_XML, out):
            for p in ET.fromstring(doc).iter("CraftingPiece"):
                bd = p.find("BladeData")
                assert bd.find("Swing").get("damage_type") == "Cut"
                assert bd.find("Thrust").get("damage_type") == "Pierce"

    def test_rewrite_is_a_no_op_for_an_unknown_piece(self):
        out, changed = restat.rewrite(PIECES_XML, {"no_such_blade": (9.0, 9.0)})
        assert changed == 0
        assert out == PIECES_XML

    def test_rewrite_output_still_parses(self):
        out, _ = restat.rewrite(PIECES_XML, {"blade_one": (1.0, 0.5), "blade_two": (2.0, 1.0)})
        ET.fromstring(out)   # raises if not well-formed


# --- the write path -----------------------------------------------------------------------


class WritePathTests(TmpCase):
    def test_write_backs_up_before_writing_and_never_uses_a_xml_extension(self):
        """An `.xml` backup in a ModuleData folder is globbed by the engine and duplicates ids."""
        path = self.tmp_path / "pieces.xml"
        path.write_text(PIECES_XML, encoding="utf-8")
        restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="test")

        backups = [p for p in self.tmp_path.iterdir() if p != path]
        assert len(backups) == 1
        assert backups[0].name == "pieces.xml.bak-test"
        assert backups[0].suffix != ".xml"
        # The backup holds the ORIGINAL, so the edit is undoable.
        assert backups[0].read_text(encoding="utf-8") == PIECES_XML

    def test_write_preserves_a_bom(self):
        path = self.tmp_path / "pieces.xml"
        path.write_bytes(b"\xef\xbb\xbf" + PIECES_XML.encode("utf-8"))
        restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="t")
        assert path.read_bytes().startswith(b"\xef\xbb\xbf")

    def test_write_preserves_crlf_line_endings(self):
        path = self.tmp_path / "pieces.xml"
        path.write_bytes(PIECES_XML.replace("\n", "\r\n").encode("utf-8"))
        restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="t")
        raw = path.read_bytes()
        assert b"\r\n" in raw
        assert b"\n" not in raw.replace(b"\r\n", b"")

    def test_write_is_a_no_op_when_nothing_matches(self):
        path = self.tmp_path / "pieces.xml"
        path.write_text(PIECES_XML, encoding="utf-8")
        assert restat.write_pieces(path, {"nope": (1.0, 1.0)}, tag="t") == 0
        # No backup is taken for a write that does not happen.
        assert [p for p in self.tmp_path.iterdir() if p != path] == []


# --- the guards ---------------------------------------------------------------------------


class GuardsTests(TmpCase):
    def test_a_ladder_exempt_troops_weapon_is_left_alone(self):
        """The cave troll: a scenery-scale club whose slowness is the point."""
        cat = catalogue_with_pieces()
        priced = {"club": weapon("club", dps=25.0, template="TwoHandedMace")}
        troops = [troop("cave_troll", 51, "mordor", ["club"])]
        s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        plans, notes = restat.build_plans(priced, cat, troops, s)
        assert plans == []
        assert any("ladder-exempt" in n for n in notes)

    def test_an_unworn_sibling_never_moves_a_blade_that_only_exempt_troops_carry(self):
        cat = catalogue_with_pieces()
        priced = {"club": weapon("club", dps=100.0, template="TwoHandedMace"),
                  "spare": weapon("spare", dps=250.0, template="TwoHandedMace")}
        troops = [troop("cave_troll", 51, "mordor", ["club"])]
        s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        plans, notes = restat.build_plans(priced, cat, troops, s)
        assert plans == []
        assert any("blade_one" in n and "ladder-exempt" in n for n in notes), notes

    def test_an_unworn_plan_lists_every_weapon_on_the_blade_and_all_its_wearers(self):
        cat = catalogue_with_pieces()
        priced = {"worn": weapon("worn", dps=69.0), "spare": weapon("spare", dps=150.0)}
        troops = [troop("t", 26, "gondor", ["worn"])]
        # The culture filter keeps the tier pass off the blade, so the unworn pass lowers it.
        plans, _ = restat.build_plans(priced, cat, troops, spec(), {"elsewhere"})
        assert [(p.blade, p.items, p.wearers) for p in plans] == [("blade_one", ["spare", "worn"], 1)]

    def test_a_couchable_weapon_is_left_alone(self):
        """A couched charge adds closing speed inside a squared term the DPS model cannot see."""
        cat = catalogue_with_pieces()
        cat.templates["P"] = cata.Template("P", "Polearm", [("Handle", 0)], ["Couch"])
        cat.descriptions["Couch"] = cata.Description(
            "Couch", "P", frozenset({"MeleeWeapon"}), frozenset({"blade_one"}), ("polearm", "couch")
        )
        priced = {"lance": weapon("lance", dps=19.0, template="P")}
        troops = [troop("knight", 36, "gondor", ["lance"])]
        plans, notes = restat.build_plans(priced, cat, troops, spec())
        assert plans == []
        assert any("couched" in n for n in notes)

    def test_a_hero_blade_is_left_alone(self):
        cat = catalogue_with_pieces()
        priced = {"anduril": weapon("anduril", dps=200.0, blade="hero_blade")}
        cat.pieces["hero_blade"] = cat.pieces["blade_one"]
        troops = [troop("lord", 51, "gondor", ["anduril"])]
        plans, _notes = restat.build_plans(priced, cat, troops, spec(hero_blades=["hero_blade"]))
        assert plans == []


# --- the arithmetic -----------------------------------------------------------------------


class ArithmeticTests(TmpCase):
    def test_the_multiplier_is_the_ratio_of_target_to_current(self):
        """DPS is exactly linear in damage_factor, so no search is needed."""
        cat = catalogue_with_pieces()
        # A tier-5 troop with no kingdom offset targets 69; give it a 34.5 DPS weapon, so x2.
        priced = {"sword": weapon("sword", dps=34.5)}
        troops = [troop("t", 26, "nowhere", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, spec())
        assert len(plans) == 1
        p = plans[0]
        assert p.ratio == approx(2.0, rel=1e-3)
        assert p.new_swing == approx(8.0, abs=0.01)   # 4.00 x 2
        assert p.new_thrust == approx(4.0, abs=0.01)  # 2.00 x 2

    def test_a_weapon_already_on_target_is_not_rewritten(self):
        """Keeps the diff to what must move, and makes a second run a no-op."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=69.0)}
        troops = [troop("t", 26, "nowhere", ["sword"])]
        assert restat.build_plans(priced, cat, troops, spec())[0] == []

    def test_a_shared_blade_is_priced_for_its_weakest_anchor(self):
        """One edit moves every weapon on the blade, so it is priced for the lowest wearer."""
        cat = catalogue_with_pieces()
        priced = {
            "low": weapon("low", dps=100.0, blade="blade_one"),
            "high": weapon("high", dps=100.0, blade="blade_one"),
        }
        troops = [
            troop("militia", 6, "nowhere", ["low"]),    # tier 1, target 34
            troop("elite", 51, "nowhere", ["high"]),    # tier 10, target 114
        ]
        plans, _ = restat.build_plans(priced, cat, troops, spec())
        assert len(plans) == 1
        assert plans[0].anchor_tier == 1
        assert plans[0].target_dps == approx(34)
        assert sorted(plans[0].items) == ["high", "low"]

    def test_factors_are_clamped_into_a_sane_range_and_the_clamp_is_reported(self):
        cat = catalogue_with_pieces()
        # Absurdly weak weapon on a tier-10 troop: the raw ratio would blow past the ceiling.
        priced = {"twig": weapon("twig", dps=1.0)}
        troops = [troop("elite", 51, "nowhere", ["twig"])]
        plans, _ = restat.build_plans(priced, cat, troops, spec())
        assert len(plans) == 1
        assert plans[0].new_swing <= restat.MAX_FACTOR
        assert plans[0].clamped

    def test_a_zero_factor_stays_zero(self):
        """A mace has no thrust. Scaling must not invent one."""
        cat = catalogue_with_pieces()
        cat.pieces["blade_one"] = md.parse_piece(ET.fromstring(
            '<CraftingPiece id="blade_one" piece_type="Blade" length="70" weight="1.0">'
            '<BladeData><Thrust damage_type="Blunt" damage_factor="0" />'
            '<Swing damage_type="Blunt" damage_factor="3.0" /></BladeData></CraftingPiece>'
        ))
        priced = {"mace": weapon("mace", dps=34.5, template="Mace")}
        troops = [troop("t", 26, "nowhere", ["mace"])]
        plans, _ = restat.build_plans(priced, cat, troops, spec())
        assert plans[0].old_thrust == 0.0
        assert plans[0].new_thrust == 0.0


# --- class limits (melee_ladders.json `class_limits`) -------------------------------------

LIMITS = {"OneHandedSword": {"swing": 60, "thrust": 55, "swing_dps": 83, "thrust_dps": 47}}


class ClassLimitsTests(TmpCase):
    def test_a_raise_stops_at_the_class_blow_limit(self):
        """2026-11-14: a blade raised to its tier's DPS target swung for 140 in one hand."""
        cat = catalogue_with_pieces()
        # Tier 5 targets 69 DPS, so x2 from 34.5 (a 37.95 swing); the 60 swing limit allows less.
        priced = {"sword": weapon("sword", dps=34.5)}
        troops = [troop("t", 26, "nowhere", ["sword"])]
        plans, notes = restat.build_plans(priced, cat, troops, spec(class_limits=LIMITS))
        assert len(plans) == 1
        assert plans[0].ratio == approx(59.5 / (37.95 + 0.5), rel=1e-3)
        assert any("class limit" in n for n in notes)

    def test_an_unworn_weapon_over_its_class_limit_is_lowered(self):
        """No troop anchors it, but it reaches the player through shops and loot."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=100.0)}  # 110 swing, under the 140 DPS ceiling
        plans, _ = restat.build_plans(priced, cat, [], spec(class_limits=LIMITS))
        assert len(plans) == 1
        assert plans[0].ratio == approx(59.5 / 110.5, rel=1e-3)

    def test_a_thrust_at_its_limit_does_not_take_swing_damage_off_the_blade(self):
        """Each attack has its own damage_factor, so each is held by its own limits."""
        cat = catalogue_with_pieces()
        usage = cata.Usage(
            "D", True, (), "set", frozenset({"swing", "thrust"}),
            md.WeaponStats(swing_damage=40, raw_swing_speed=20.8, thrust_damage=100, raw_thrust_speed=3.85),
        )
        sword = cata.Priced(
            item=cata.Item("sword", "sword", "", "OneHandedSword", (("blade_one", "Blade", 100),), "src"),
            usages=(usage,), blade_piece="blade_one", tier=3,
        )
        plans, _ = restat.build_plans({"sword": sword}, cat, [], spec(class_limits=LIMITS))
        assert len(plans) == 1
        assert plans[0].new_swing == plans[0].old_swing
        assert plans[0].new_thrust < plans[0].old_thrust


# --- per-class targets and hero limits (2026-11-14) ---------------------------------------

SWORD = {"swing": 95, "thrust": 55, "swing_dps": 83, "thrust_dps": 47}


def class_spec(**over):
    base = dict(
        class_limits={"OneHandedSword": SWORD},
        craft_column={"OneHandedSword": "sword"},
        craft={"rivendell": {"sword": 1.0, "axe": 1.0, "polearm": 1.0}},
        craft_default={"sword": 0.85, "axe": 0.85, "polearm": 0.85},
        class_band=0.15,
        hero_blades=["hero_blade"],
        hero_makers={},
        hero_bonus={"other": 5},
    )
    base.update(over)
    return spec(**base)


def hero_catalogue():
    cat = catalogue_with_pieces()
    cat.pieces["hero_blade"] = cat.pieces["blade_one"]
    return cat


def lands_on(dps, limit, p, attack="swing"):
    """True when `dps` is the limit to within what the DPS cap may give up.

    `ladder.max_factor` keeps the largest whole damage inside a DPS limit and allows for a raw
    damage half a point above the shown one, scaled by the factor. A planned DPS therefore sits
    under the limit by less than half a point plus that scaled half point, or over it by less
    than the half point that rounding cannot show.
    """
    per_point = getattr(p, f"{attack}_dps") / getattr(p, f"{attack}_damage")
    ratio = dps / getattr(p, f"{attack}_dps")
    return limit - per_point * (1 + ratio) / 2 < dps < limit + per_point / 2


class ClassTargetsHeroLimitsTests(TmpCase):
    def test_the_tier_target_is_the_per_class_one_when_the_template_is_known(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=34.5)}
        troops = [troop("t", 26, "rivendell", ["sword"])]   # tier 5
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert len(plans) == 1
        assert lands_on(plans[0].target_dps, 83 * 69 / 114, priced["sword"])

    def test_a_hero_blade_above_its_hero_limit_is_lowered_to_it(self):
        """2026-11-14: the plain class limit is 83 DPS; the `other` bonus of 5 lifts a hero blade to 88."""
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        troops = [troop("lord", 51, "rivendell", ["anduril"])]
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert [p.blade for p in plans] == ["hero_blade"]
        assert lands_on(plans[0].target_dps, 88, priced["anduril"])

    def test_a_hero_blade_over_its_limit_that_only_exempt_troops_carry_is_left_alone(self):
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        troops = [troop("cave_troll", 51, "rivendell", ["anduril"])]
        s = class_spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        for build in (restat.build_plans, restat.build_limit_plans):
            with self.subTest(build=build.__name__):
                plans, notes = build(priced, cat, troops, s)
                assert plans == []
                assert any("hero_blade" in n and "ladder-exempt" in n for n in notes), notes

    def test_a_hero_blade_below_its_hero_limit_is_left_alone_not_raised(self):
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=60.0, blade="hero_blade")}
        troops = [troop("lord", 51, "rivendell", ["anduril"])]
        assert restat.build_plans(priced, cat, troops, class_spec())[0] == []

    def test_an_unworn_hero_blade_over_its_hero_limit_is_lowered_too(self):
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        plans, _ = restat.build_plans(priced, cat, [], class_spec())
        assert [p.blade for p in plans] == ["hero_blade"]

    def test_a_hero_blade_gets_no_tier_target(self):
        """A hero blade over its limit comes down to the limit, never to the tier target below it."""
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        troops = [troop("lord", 6, "rivendell", ["anduril"])]   # tier 1 target would be 18
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert lands_on(plans[0].target_dps, 88, priced["anduril"])


# --- --limits-only -------------------------------------------------------------------------


class LimitsOnlyTests(TmpCase):
    def test_limits_only_lowers_a_worn_blade_above_its_class_limit(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=100.0)}   # 110 swing, limits 95 and 83
        troops = [troop("t", 26, "rivendell", ["sword"])]
        plans, _ = restat.build_limit_plans(priced, cat, troops, class_spec())
        assert [p.blade for p in plans] == ["blade_one"]
        assert plans[0].new_swing == approx(4.0 * 0.83, abs=0.01)
        assert plans[0].new_thrust == plans[0].old_thrust

    def test_a_factor_cut_to_a_limit_is_rounded_down_so_the_weapon_ends_inside_it(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=96.0)}   # 4.00 x 91.5/106.1 = 3.4495, which rounds up to 3.45
        plans, _ = restat.build_limit_plans(priced, cat, [], class_spec())
        assert plans[0].new_swing == 3.44
        _cat, after = applied(cat, priced, plans)
        assert after["sword"].swing_dps <= SWORD["swing_dps"]

    def test_limits_only_raises_nothing_and_ignores_the_tier_target(self):
        """The full restat would raise this tier 10 sword to 83 DPS; --limits-only leaves it."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=30.0)}
        troops = [troop("lord", 51, "rivendell", ["sword"])]
        assert restat.build_plans(priced, cat, troops, class_spec())[0] != []
        assert restat.build_limit_plans(priced, cat, troops, class_spec())[0] == []

    def test_limits_only_lowers_an_unworn_blade_over_its_limit(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=100.0)}
        plans, _ = restat.build_limit_plans(priced, cat, [], class_spec())
        assert [p.blade for p in plans] == ["blade_one"]

    def test_limits_only_judges_a_hero_blade_by_its_hero_limit(self):
        cat = hero_catalogue()
        inside = {"anduril": weapon("anduril", dps=86.0, blade="hero_blade")}   # over 83, under 88
        over = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        assert restat.build_limit_plans(inside, cat, [], class_spec())[0] == []
        plans, _ = restat.build_limit_plans(over, cat, [], class_spec())
        assert [p.blade for p in plans] == ["hero_blade"]

    def test_limits_only_leaves_a_ladder_exempt_troops_weapon_alone(self):
        cat = catalogue_with_pieces()
        priced = {"club": weapon("club", dps=100.0)}
        troops = [troop("cave_troll", 51, "mordor", ["club"])]
        s = class_spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
        plans, notes = restat.build_limit_plans(priced, cat, troops, s)
        assert plans == []
        assert any("ladder-exempt" in n for n in notes)

    def test_limits_only_lowers_a_couchable_weapon_because_lowering_is_safe(self):
        cat = catalogue_with_pieces()
        cat.templates["P"] = cata.Template("P", "Polearm", [("Handle", 0)], ["Couch"])
        cat.descriptions["Couch"] = cata.Description(
            "Couch", "P", frozenset({"MeleeWeapon"}), frozenset({"blade_one"}), ("polearm", "couch")
        )
        priced = {"lance": weapon("lance", dps=100.0, template="P")}
        s = class_spec(class_limits={"P": SWORD}, craft_column={"P": "polearm"})
        plans, _ = restat.build_limit_plans(priced, cat, [troop("k", 36, "gondor", ["lance"])], s)
        assert [p.blade for p in plans] == ["blade_one"]


# --- tier limits, the dominant polearm attack and the craft prefix (2026-11-14, #1271) -----

TIER_FRACTION = {str(t): v for t, v in enumerate(
    [0.58, 0.62, 0.66, 0.72, 0.77, 0.81, 0.88, 0.91, 0.94, 0.97, 1.0])}
POLEARM = {"swing": 185, "thrust": 60, "swing_dps": 138, "thrust_dps": 45}


def tier_spec(**over):
    base = dict(
        class_tier_fraction=TIER_FRACTION,
        class_limits={"OneHandedSword": SWORD, "TwoHandedPolearm": POLEARM},
        craft_column={"OneHandedSword": "sword", "TwoHandedPolearm": "polearm"},
        craft={"rivendell": {"sword": 1.0, "axe": 1.0, "polearm": 1.0},
               "mordor": {"sword": 0.82, "axe": 0.85, "polearm": 0.82},
               "mordor_numenorean": {"sword": 0.90, "axe": 0.88, "polearm": 0.90}},
        craft_troop_prefix={"mordor_num_": "mordor_numenorean"},
    )
    base.update(over)
    return class_spec(**base)


def heavy_hitter(item_id, swing_damage=55, raw_swing_speed=10.0):
    """A slow sword: its one blow is large against its DPS, so the blow limit binds first."""
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"swing"}),
        md.WeaponStats(swing_damage=swing_damage, raw_swing_speed=raw_swing_speed),
    )
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", "OneHandedSword", (("blade_one", "Blade", 100),), "src"),
        usages=(usage,), blade_piece="blade_one", tier=3,
    )


def spear(item_id):
    """A polearm that swings for 18 DPS and thrusts for 30: a spear in the hand."""
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"swing", "thrust"}),
        md.WeaponStats(swing_damage=20, raw_swing_speed=20.8, thrust_damage=30, raw_thrust_speed=12.0),
    )
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", "TwoHandedPolearm", (("blade_one", "Blade", 100),), "src"),
        usages=(usage,), blade_piece="blade_one", tier=3,
    )


class TierLimitsDominantPolearmTests(TmpCase):
    def test_a_blade_worn_at_tier_3_is_held_to_the_tier_limit_not_the_class_limit(self):
        """95 x 0.72 x craft: a tier 3 troop may not swing what only a tier 10 troop may."""
        cat = catalogue_with_pieces()
        priced = {"sword": heavy_hitter("sword")}
        troops = [troop("t", 16, "nowhere", ["sword"])]   # tier 3, craft_default 0.85
        plans, notes = restat.build_plans(priced, cat, troops, tier_spec())
        assert len(plans) == 1
        limit = 95 * 0.72 * 0.85
        assert plans[0].new_swing / plans[0].old_swing == approx((limit - 0.5) / 55.5, abs=0.002)
        # The unrounded blow cannot display above the tier limit.
        assert 55.5 * plans[0].new_swing / plans[0].old_swing - 0.5 <= limit + 1e-9
        assert any("class limit" in n for n in notes)

    def test_the_same_blade_worn_at_tier_10_by_the_best_craft_reaches_the_class_limit(self):
        cat = catalogue_with_pieces()
        priced = {"sword": heavy_hitter("sword")}
        troops = [troop("t", 51, "rivendell", ["sword"])]   # tier 10, craft 1.0
        plans, _ = restat.build_plans(priced, cat, troops, tier_spec())
        assert plans[0].new_swing / plans[0].old_swing == approx(94.5 / 55.5, abs=0.002)

    def test_a_sibling_is_held_to_the_tier_limit_of_the_blades_anchor(self):
        """A shared blade is priced for its weakest wearer, and every weapon on it is held there."""
        cat = catalogue_with_pieces()
        priced = {"low": heavy_hitter("low"), "high": heavy_hitter("high")}
        troops = [troop("recruit", 16, "nowhere", ["low"]), troop("elite", 51, "rivendell", ["high"])]
        plans, _ = restat.build_plans(priced, cat, troops, tier_spec())
        assert len(plans) == 1 and plans[0].anchor_tier == 3
        assert plans[0].new_swing / plans[0].old_swing == approx((95 * 0.72 * 0.85 - 0.5) / 55.5, abs=0.002)

    def test_a_hero_blade_is_still_held_to_its_hero_limit_whatever_the_tier(self):
        cat = hero_catalogue()
        priced = {"anduril": weapon("anduril", dps=100.0, blade="hero_blade")}
        troops = [troop("lord", 16, "rivendell", ["anduril"])]   # tier 3
        plans, _ = restat.build_plans(priced, cat, troops, tier_spec())
        assert [p.blade for p in plans] == ["hero_blade"]
        assert lands_on(plans[0].target_dps, 88, priced["anduril"])

    def test_class_ratios_use_the_tier_limit_only_when_a_tier_is_given(self):
        s = tier_spec()
        p = heavy_hitter("sword")
        plain = restat.class_ratios(p, s)
        tiered = restat.class_ratios(p, s, 3, "nowhere")
        assert plain[0] == approx(94.5 / 55.5)
        assert tiered[0] == approx((95 * 0.72 * 0.85 - 0.5) / 55.5)
        assert restat.class_ratios(p, s, 10, "rivendell")[0] == approx(plain[0])

    def test_class_ratios_hold_a_hero_blade_to_its_hero_limit_even_with_a_tier(self):
        s = tier_spec()
        p = weapon("anduril", dps=100.0, blade="hero_blade")
        assert restat.class_ratios(p, s, 3, "rivendell") == restat.class_ratios(p, s)

    def test_a_thrust_dominant_spear_targets_the_thrust_top_not_the_halberd_swing(self):
        """45 x share x craft, not 138 x share x craft."""
        cat = catalogue_with_pieces()
        priced = {"spear": spear("spear")}
        assert priced["spear"].thrust_dps > priced["spear"].swing_dps > 0
        troops = [troop("t", 26, "rivendell", ["spear"])]   # tier 5
        plans, _ = restat.build_plans(priced, cat, troops, tier_spec())
        assert len(plans) == 1
        assert lands_on(plans[0].target_dps, 45 * 0.81, priced["spear"], "thrust")

    def test_a_swing_dominant_halberd_still_targets_the_swing_top(self):
        cat = catalogue_with_pieces()
        usage = cata.Usage(
            "D", True, (), "set", frozenset({"swing", "thrust"}),
            md.WeaponStats(swing_damage=40, raw_swing_speed=20.8, thrust_damage=15, raw_thrust_speed=12.0),
        )
        halberd = cata.Priced(
            item=cata.Item("halberd", "halberd", "", "TwoHandedPolearm", (("blade_one", "Blade", 100),), "src"),
            usages=(usage,), blade_piece="blade_one", tier=3,
        )
        assert halberd.swing_dps > halberd.thrust_dps
        troops = [troop("t", 26, "rivendell", ["halberd"])]
        plans, _ = restat.build_plans({"halberd": halberd}, cat, troops, tier_spec())
        assert lands_on(plans[0].target_dps, 138 * 0.81, halberd)

    def test_a_black_numenorean_is_priced_by_its_own_craft_row(self):
        """mordor_num_ troops are filed under the mordor culture; the prefix table rates them apart."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=20.0)}
        num = [troop("mordor_num_swordsman", 26, "mordor", ["sword"])]
        orc = [troop("mordor_orc_swordsman", 26, "mordor", ["sword"])]
        plan_num = restat.build_plans(priced, cat, num, tier_spec())[0][0]
        plan_orc = restat.build_plans(priced, cat, orc, tier_spec())[0][0]
        assert lands_on(plan_num.target_dps, 83 * 0.81 * 0.90, priced["sword"])
        assert lands_on(plan_orc.target_dps, 83 * 0.81 * 0.82, priced["sword"])

    def test_limits_only_still_uses_the_plain_class_limit(self):
        """--limits-only lowers what breaks a limit and nothing else: tier limits are not part of it."""
        cat = catalogue_with_pieces()
        priced = {"sword": heavy_hitter("sword", swing_damage=80)}   # 80 swing: inside 95, over tier 3's 58
        troops = [troop("t", 16, "nowhere", ["sword"])]
        assert restat.build_limit_plans(priced, cat, troops, tier_spec())[0] == []

    def test_a_couchable_blade_that_needs_a_raise_and_a_cut_is_still_cut(self):
        """he_spear: the swing wants a raise (refused), the thrust sits above its limit (allowed)."""
        cat = catalogue_with_pieces()
        cat.templates["P"] = cata.Template("P", "Polearm", [("Handle", 0)], ["Couch"])
        cat.descriptions["Couch"] = cata.Description(
            "Couch", "P", frozenset({"MeleeWeapon"}), frozenset({"blade_one"}), ("polearm", "couch")
        )
        usage = cata.Usage(
            "D", True, (), "set", frozenset({"swing", "thrust"}),
            md.WeaponStats(swing_damage=100, raw_swing_speed=20.8, thrust_damage=70, raw_thrust_speed=12.0),
        )
        lance = cata.Priced(
            item=cata.Item("lance", "lance", "", "P", (("blade_one", "Blade", 100),), "src"),
            usages=(usage,), blade_piece="blade_one", tier=3,
        )
        assert lance.swing_dps > lance.thrust_dps
        s = tier_spec(class_limits={"P": POLEARM}, craft_column={"P": "polearm"})
        plans, notes = restat.build_plans({"lance": lance}, cat, [troop("k", 26, "rivendell", ["lance"])], s)
        assert len(plans) == 1
        assert plans[0].new_swing == plans[0].old_swing          # the raise is refused
        assert plans[0].new_thrust < plans[0].old_thrust          # the cut to the tier limit is not
        assert any("couched" in n for n in notes)


# --- shared blades, carriers and idempotence (2026-11-14, #1271 deep review) ----------------

TWO_HANDED_SWORD = {"swing": 150, "thrust": 60, "swing_dps": 110, "thrust_dps": 50}


def mixed_spec(**over):
    base = dict(
        class_limits={"OneHandedSword": SWORD, "TwoHandedSword": TWO_HANDED_SWORD},
        craft_column={"OneHandedSword": "sword", "TwoHandedSword": "sword"},
    )
    base.update(over)
    return class_spec(**base)


def two_attack(item_id, swing_damage, raw_swing_speed, thrust_damage, raw_thrust_speed,
               blade="blade_one", template="OneHandedSword"):
    usage = cata.Usage(
        "D", True, (), "set", frozenset({"swing", "thrust"}),
        md.WeaponStats(swing_damage=swing_damage, raw_swing_speed=raw_swing_speed,
                       thrust_damage=thrust_damage, raw_thrust_speed=raw_thrust_speed),
    )
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", template, ((blade, "Blade", 100),), "src"),
        usages=(usage,), blade_piece=blade, tier=3,
    )


def applied(cat, priced, plans):
    """What --apply followed by a fresh load does, in memory: the factors move, the weapons re-price."""
    new_cat = cata.Catalogue()
    new_cat.pieces = dict(cat.pieces)
    new_cat.templates, new_cat.descriptions = cat.templates, cat.descriptions
    by_blade = {p.blade: p for p in plans}
    for p in plans:
        piece = new_cat.pieces[p.blade]
        new_cat.pieces[p.blade] = dataclasses.replace(
            piece, blade=dataclasses.replace(piece.blade, swing_factor=p.new_swing,
                                             thrust_factor=p.new_thrust))
    out = {}
    for iid, pr in priced.items():
        plan = by_blade.get(pr.blade_piece)
        if plan is None:
            out[iid] = pr
            continue
        swing = plan.new_swing / plan.old_swing if plan.old_swing else 1.0
        thrust = plan.new_thrust / plan.old_thrust if plan.old_thrust else 1.0
        usages = []
        for u in pr.usages:
            stats = dataclasses.replace(u.stats, swing_damage=u.stats.swing_damage * swing,
                                        thrust_damage=u.stats.thrust_damage * thrust)
            usages.append(dataclasses.replace(u, stats=stats))
        out[iid] = dataclasses.replace(pr, usages=tuple(usages))
    return new_cat, out


class SharedBladeTests(TmpCase):
    def test_a_raise_to_the_tier_target_is_rounded_down_so_the_weapon_stays_inside_the_tier_limit(self):
        """The tier limit's DPS is the tier target, so a factor rounded up lands above the limit."""
        cat = catalogue_with_pieces()
        s = class_spec()
        priced = {"sword": weapon("sword", dps=44.0)}   # 4.00 x 55.5/48.9 = 4.5399, which rounds up to 4.54
        troops = [troop("t", 26, "rivendell", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, s)
        assert plans[0].new_swing == 4.53
        _cat, after = applied(cat, priced, plans)
        limit = ladder.tier_limit("OneHandedSword", 5, "rivendell", s)
        assert after["sword"].swing_dps <= limit["swing_dps"]

    def test_a_raise_that_rounds_down_to_inside_the_tolerance_is_not_planned(self):
        """The ideal move is 2.05 percent, over the 2 percent tolerance; the factor written is 3.39, a
        1.8 percent move, so a run on that output must find nothing to do."""
        cat = catalogue_with_pieces()
        cat.pieces["blade_one"] = md.parse_piece(ET.fromstring(
            '<CraftingPiece id="blade_one" piece_type="Blade" length="70" weight="1.0">'
            '<BladeData><Thrust damage_type="Pierce" damage_factor="0" />'
            '<Swing damage_type="Cut" damage_factor="3.33" /></BladeData></CraftingPiece>'))
        s = class_spec()
        priced = {"sword": weapon("sword", dps=49.2278)}
        ideal = ladder.target(5, "rivendell", s, "OneHandedSword", True, False) / priced["sword"].swing_dps
        assert 1.0201 < ideal < 1.0215, ideal
        troops = [troop("t", 26, "rivendell", ["sword"])]
        assert restat.build_plans(priced, cat, troops, s)[0] == []

    def _two_cultures_on_one_blade(self):
        cat = catalogue_with_pieces()
        priced = {"a_sword": weapon("a_sword", dps=100.0, blade="blade_one"),
                  "b_axe": weapon("b_axe", dps=100.0, blade="blade_one"),
                  "a_own": weapon("a_own", dps=100.0, blade="blade_two")}
        return cat, priced

    def test_a_culture_run_leaves_a_blade_another_culture_wears(self):
        """#1241: the file that loads first used to label a shared blade, so --culture restatted
        the blade of a culture that was not in the run. Either file order gives the same answer."""
        cat, priced = self._two_cultures_on_one_blade()
        a = troop("a_t", 6, "gondor", ["a_sword", "a_own"])
        b = troop("b_t", 6, "rohan", ["b_axe"])
        for order in ([a, b], [b, a]):
            plans, notes = restat.build_plans(priced, cat, order, spec(), {"gondor"})
            assert [p.blade for p in plans] == ["blade_two"], plans
            assert any(n.startswith(restat.SHARED_NOTE) and "blade_one" in n and "rohan" in n
                       for n in notes), notes

    def test_a_culture_run_that_names_every_wearer_restats_the_shared_blade(self):
        cat, priced = self._two_cultures_on_one_blade()
        troops = [troop("a_t", 6, "gondor", ["a_sword"]), troop("b_t", 6, "rohan", ["b_axe"])]
        plans, notes = restat.build_plans(priced, cat, troops, spec(), {"gondor", "rohan"})
        assert [p.blade for p in plans] == ["blade_one"]
        assert not any(n.startswith(restat.SHARED_NOTE) for n in notes)

    def test_culture_help_says_it_names_a_craft_row_not_an_engine_culture(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit):
            restat.main(["--help"])
        assert "craft row" in " ".join(out.getvalue().split())

    def test_a_second_restat_of_its_own_output_plans_nothing(self):
        """M3: a swing held at its blow limit never reaches the DPS target, and the free thrust
        used to be raised again on every run."""
        cat = catalogue_with_pieces()
        priced = {"sword": two_attack("sword", 52, 14.0, 20, 3.85)}
        troops = [troop("t", 26, "rivendell", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert len(plans) == 1
        cat2, priced2 = applied(cat, priced, plans)
        assert restat.build_plans(priced2, cat2, troops, class_spec())[0] == []

    def test_the_free_thrust_follows_the_held_swing_instead_of_the_unreachable_target(self):
        cat = catalogue_with_pieces()
        priced = {"sword": two_attack("sword", 52, 14.0, 20, 3.85)}
        troops = [troop("t", 26, "rivendell", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert plans[0].new_thrust / plans[0].old_thrust <= plans[0].new_swing / plans[0].old_swing + 1e-6

    def test_an_attack_the_blade_does_not_have_never_forces_a_plan(self):
        """A mace blade with a zero thrust factor: its absent thrust once carried the full ratio
        and kept a plan alive whose swing was already at the limit."""
        cat = catalogue_with_pieces()
        cat.pieces["blade_one"] = md.parse_piece(ET.fromstring(
            '<CraftingPiece id="blade_one" piece_type="Blade" length="70" weight="1.0">'
            '<BladeData><Thrust damage_type="Blunt" damage_factor="0" />'
            '<Swing damage_type="Blunt" damage_factor="4.0" /></BladeData></CraftingPiece>'))
        limits = {"OneHandedSword": {"swing": 63.7, "thrust": 55, "swing_dps": 83, "thrust_dps": 47}}
        priced = {"mace": weapon("mace", dps=34.5)}
        troops = [troop("t", 26, "rivendell", ["mace"])]
        assert restat.build_plans(priced, cat, troops, class_spec(class_limits=limits))[0] == []

    def test_the_min_factor_clamp_never_raises_a_factor(self):
        """M7: a piece already under MIN_FACTOR that must come down stays where it is."""
        cat = catalogue_with_pieces()
        cat.pieces["blade_one"] = md.parse_piece(ET.fromstring(
            '<CraftingPiece id="blade_one" piece_type="Blade" length="70" weight="1.0">'
            '<BladeData><Thrust damage_type="Pierce" damage_factor="0.2" />'
            '<Swing damage_type="Cut" damage_factor="0.3" /></BladeData></CraftingPiece>'))
        priced = {"sword": weapon("sword", dps=130.0)}
        troops = [troop("t", 6, "rivendell", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert len(plans) == 1
        assert plans[0].new_swing <= plans[0].old_swing
        assert plans[0].new_thrust <= plans[0].old_thrust

    def test_a_move_under_one_displayed_damage_point_is_not_planned(self):
        """Damage shows as a whole number, so a 3 percent raise of a 26 point blow is under one
        point. Quantization of small blows kept a second run busy with 2.01 percent moves."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=24.0)}   # swing 26.4, tier 1 target 24.75 DPS
        troops = [troop("t", 6, "rivendell", ["sword"])]
        assert restat.build_plans(priced, cat, troops, class_spec())[0] == []

    def test_a_blade_shared_by_one_and_two_handed_weapons_is_not_moved(self):
        """M1: priced for the one handed wearer, a two handed axe lost a third of its swing."""
        cat = catalogue_with_pieces()
        priced = {"one": weapon("one", dps=20.0, template="OneHandedSword"),
                  "two": weapon("two", dps=60.0, template="TwoHandedSword")}
        troops = [troop("recruit", 6, "rivendell", ["one"]), troop("elite", 51, "rivendell", ["two"])]
        plans, _ = restat.build_plans(priced, cat, troops, mixed_spec())
        assert plans == []
        assert restat.build_limit_plans(priced, cat, troops, mixed_spec())[0] == []

    def test_a_mixed_blade_is_reported_with_every_weapon_its_class_and_its_display(self):
        priced = {"one": weapon("one", dps=20.0, template="OneHandedSword"),
                  "two": weapon("two", dps=60.0, template="TwoHandedSword"),
                  "solo": weapon("solo", dps=20.0, blade="blade_two")}
        mixed = restat.mixed_blades(priced)
        assert [m.blade for m in mixed] == ["blade_one"]
        text = mixed[0].describe()
        for needle in ("blade_one", "one", "OneHandedSword", "two", "TwoHandedSword", "22", "66"):
            assert needle in text, (needle, text)

    def test_a_mixed_blade_over_a_class_limit_comes_down_only_as_far_as_that_limit(self):
        """The exception to M1: the two handed weapon breaks its CLASS limit (swing dps 110)."""
        cat = catalogue_with_pieces()
        priced = {"one": weapon("one", dps=20.0, template="OneHandedSword"),
                  "two": weapon("two", dps=130.0, template="TwoHandedSword")}
        troops = [troop("recruit", 6, "rivendell", ["one"]), troop("elite", 51, "rivendell", ["two"])]
        for build in (restat.build_plans, restat.build_limit_plans):
            with self.subTest(build=build.__name__):
                plans, _notes = build(priced, cat, troops, mixed_spec())
                assert [p.blade for p in plans] == ["blade_one"]
                assert plans[0].new_swing / plans[0].old_swing == approx(110 / 130, abs=0.01)
        notes = restat.build_plans(priced, cat, troops, mixed_spec())[1]
        assert any("class limit" in n and "one handed" in n for n in notes), notes

    def test_the_target_comes_from_the_weakest_carrier_not_the_first_one_loaded(self):
        """M2: the craft row and tier came from whichever troop loaded first."""
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=60.0)}
        troops = [troop("elite", 51, "rivendell", ["sword"]), troop("recruit", 6, "nowhere", ["sword"])]
        plans, _ = restat.build_plans(priced, cat, troops, class_spec())
        assert plans[0].anchor_tier == 1
        assert plans[0].culture == "nowhere"
        assert plans[0].target_dps == approx(83 * 34 / 114 * 0.85)

    def test_no_carrier_ends_above_its_own_tier_limit(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=20.0)}
        troops = [troop("elite", 51, "rivendell", ["sword"]), troop("recruit", 6, "nowhere", ["sword"])]
        s = class_spec()
        plans, _ = restat.build_plans(priced, cat, troops, s)
        _cat, after = applied(cat, priced, plans)
        _kits, carried = ts.melee_pass_rows(troops, after, s)
        assert ladder.blow_findings(carried, s) == []

    def test_a_ladder_exempt_carrier_sets_neither_the_target_nor_the_limit(self):
        cat = catalogue_with_pieces()
        priced = {"sword": weapon("sword", dps=20.0)}
        troops = [troop("cave_troll", 6, "nowhere", ["sword"]), troop("elite", 51, "rivendell", ["sword"])]
        s = class_spec(exempt_troops={"cave_troll": "a monster"})
        plans, _ = restat.build_plans(priced, cat, troops, s)
        assert plans[0].anchor_tier == 10
        assert plans[0].culture == "rivendell"

    def test_a_note_names_the_weapon_and_class_that_bound_the_factor(self):
        """M7: the note named the first sibling's template, not the weapon that held the blade."""
        cat = catalogue_with_pieces()
        priced = {"aaa_light": weapon("aaa_light", dps=34.5),
                  "zzz_heavy": heavy_hitter("zzz_heavy")}
        troops = [troop("t", 26, "rivendell", ["aaa_light", "zzz_heavy"])]
        _plans, notes = restat.build_plans(priced, cat, troops, class_spec())
        held = [n for n in notes if "held at" in n]
        assert held and "zzz_heavy" in held[0] and "OneHandedSword" in held[0], notes


SHIELD_POLEARM = {"swing": None, "thrust": 45, "swing_dps": None, "thrust_dps": 37}


def shield_spear(item_id, thrust_damage=20):
    """A spear as the Armory builds one: a one handed thrust mode a shield allows (primary) and a
    two handed swing and thrust mode that refuses a shield."""
    one = cata.Usage("OneHandedPolearm", True, (), "onehanded_polearm_block_long_rshield_thrust",
                     frozenset({"thrust"}),
                     md.WeaponStats(thrust_damage=thrust_damage, raw_thrust_speed=12.0),
                     class_template="OneHandedPolearm")
    two = cata.Usage("TwoHandedPolearm", False, (), "polearm_block_long_shield_swing_thrust",
                     frozenset({"swing", "thrust"}),
                     md.WeaponStats(swing_damage=60, raw_swing_speed=20.8,
                                    thrust_damage=thrust_damage + 6, raw_thrust_speed=12.0),
                     shield_ok=False, class_template="TwoHandedPolearm")
    return cata.Priced(
        item=cata.Item(item_id, item_id, "", "TwoHandedPolearm", (("blade_one", "Blade", 100),), "src"),
        usages=(one, two), blade_piece="blade_one", tier=3,
    )


def shield_spec():
    return tier_spec(
        class_limits={"OneHandedSword": SWORD, "TwoHandedPolearm": POLEARM,
                      "OneHandedPolearm": SHIELD_POLEARM},
        craft_column={"OneHandedSword": "sword", "TwoHandedPolearm": "polearm",
                      "OneHandedPolearm": "polearm"})


class ShieldCarrierTests(TmpCase):
    """A troop with a shield wields a spear in its one handed thrust mode (2026-11-16), so the restat
    prices the blade on that mode for it, as the gate and the fixer judge it."""

    def test_a_shield_carrier_prices_the_blade_on_the_thrust_it_holds(self):
        cat = catalogue_with_pieces()
        p = shield_spear("sp")
        troops = [troop("t", 26, "rivendell", ["sp", "sh"])]   # tier 5, craft 1.0
        plans, _ = restat.build_plans({"sp": p}, cat, troops, shield_spec(), shields={"sh"})
        assert len(plans) == 1
        ratio_t = plans[0].new_thrust / plans[0].old_thrust
        target = ladder.target(5, "rivendell", shield_spec(), "OneHandedPolearm", False, True)
        # The target is the tier limit: the restat lands just under it (half point margin, factor rounded down).
        assert target - 1.5 <= p.with_shield().thrust_dps * ratio_t <= target

    def test_without_a_shield_the_same_troop_prices_the_two_handed_swing(self):
        cat = catalogue_with_pieces()
        p = shield_spear("sp")
        troops = [troop("t", 26, "rivendell", ["sp"])]
        plans, _ = restat.build_plans({"sp": p}, cat, troops, shield_spec(), shields={"sh"})
        ratio_s = plans[0].new_swing / plans[0].old_swing
        target = ladder.target(5, "rivendell", shield_spec(), "TwoHandedPolearm", True, True)
        assert target - 1.5 <= p.swing_dps * ratio_s <= target

    def test_the_two_handed_mode_stays_inside_its_class_limit(self):
        """A player without a shield can still swing it: the mode nobody in a troop uses keeps a limit."""
        cat = catalogue_with_pieces()
        p = shield_spear("sp", thrust_damage=5)
        troops = [troop("t", 51, "rivendell", ["sp", "sh"])]   # tier 10: a large thrust raise
        plans, _ = restat.build_plans({"sp": p}, cat, troops, shield_spec(), shields={"sh"})
        ratio_s = plans[0].new_swing / plans[0].old_swing
        assert p.swing_damage * ratio_s <= POLEARM["swing"] + 1e-9
        assert p.swing_dps * ratio_s <= POLEARM["swing_dps"] + 1e-9


class CheckFlagTests(TmpCase):
    """--check (#673): the dry-run pass, exit 1 when any blade would move, 0 when none."""

    def _run(self, plans, argv):
        from unittest import mock
        pieces = self.tmp_path / "pieces.xml"
        pieces.write_text("<CraftingPieces/>", encoding="utf-8")
        with mock.patch.object(restat, "MODULES", self.tmp_path),                 mock.patch.object(restat.ladder, "load_spec", return_value={}),                 mock.patch.object(restat.mc, "load", return_value=None),                 mock.patch.object(restat.mc, "price_all", return_value=({}, [])),                 mock.patch.object(restat.mc, "load_shield_ids", return_value=[]),                 mock.patch.object(restat.report, "load_troops", return_value=[]),                 mock.patch.object(restat, "build_plans", return_value=(plans, [])),                 mock.patch.object(restat, "write_pieces") as write,                 contextlib.redirect_stdout(io.StringIO()) as out:
            code = restat.main(["--pieces", str(pieces)] + argv)
        return code, out.getvalue(), write, pieces.read_text(encoding="utf-8")

    def _plan(self):
        return restat.BladePlan("blade_one", ["sword"], 6, "gondor", 100.0, 90.0,
                                1.0, 1.1, 1.0, 1.0, 1)

    def test_check_exits_1_when_a_blade_would_move_and_writes_nothing(self):
        code, out, write, text = self._run([self._plan()], ["--check"])
        assert code == 1, out
        write.assert_not_called()
        assert text == "<CraftingPieces/>"

    def test_check_exits_0_when_nothing_would_move(self):
        code, out, write, _ = self._run([], ["--check"])
        assert code == 0, out
        write.assert_not_called()

    def test_plain_run_still_exits_0_with_a_pending_change(self):
        code, _, write, _ = self._run([self._plan()], [])
        assert code == 0
        write.assert_not_called()

    def test_check_cannot_be_combined_with_apply(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            restat.main(["--check", "--apply"])


if __name__ == "__main__":
    unittest.main()
