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

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import analyze_melee_ladder as report  # noqa: E402
import melee_catalogue as cata  # noqa: E402
import melee_damage as md  # noqa: E402
import restat_melee_blades as restat  # noqa: E402


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


def test_rewrite_changes_only_the_named_piece():
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


def test_rewrite_touches_nothing_else_about_the_piece():
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


def test_rewrite_is_a_no_op_for_an_unknown_piece():
    out, changed = restat.rewrite(PIECES_XML, {"no_such_blade": (9.0, 9.0)})
    assert changed == 0
    assert out == PIECES_XML


def test_rewrite_output_still_parses():
    out, _ = restat.rewrite(PIECES_XML, {"blade_one": (1.0, 0.5), "blade_two": (2.0, 1.0)})
    ET.fromstring(out)   # raises if not well-formed


# --- the write path -----------------------------------------------------------------------


def test_write_backs_up_before_writing_and_never_uses_a_xml_extension(tmp_path):
    """An `.xml` backup in a ModuleData folder is globbed by the engine and duplicates ids."""
    path = tmp_path / "pieces.xml"
    path.write_text(PIECES_XML, encoding="utf-8")
    restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="test")

    backups = [p for p in tmp_path.iterdir() if p != path]
    assert len(backups) == 1
    assert backups[0].name == "pieces.xml.bak-test"
    assert backups[0].suffix != ".xml"
    # The backup holds the ORIGINAL, so the edit is undoable.
    assert backups[0].read_text(encoding="utf-8") == PIECES_XML


def test_write_preserves_a_bom(tmp_path):
    path = tmp_path / "pieces.xml"
    path.write_bytes(b"\xef\xbb\xbf" + PIECES_XML.encode("utf-8"))
    restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="t")
    assert path.read_bytes().startswith(b"\xef\xbb\xbf")


def test_write_preserves_crlf_line_endings(tmp_path):
    path = tmp_path / "pieces.xml"
    path.write_bytes(PIECES_XML.replace("\n", "\r\n").encode("utf-8"))
    restat.write_pieces(path, {"blade_one": (6.0, 3.0)}, tag="t")
    raw = path.read_bytes()
    assert b"\r\n" in raw
    assert b"\n" not in raw.replace(b"\r\n", b"")


def test_write_is_a_no_op_when_nothing_matches(tmp_path):
    path = tmp_path / "pieces.xml"
    path.write_text(PIECES_XML, encoding="utf-8")
    assert restat.write_pieces(path, {"nope": (1.0, 1.0)}, tag="t") == 0
    # No backup is taken for a write that does not happen.
    assert [p for p in tmp_path.iterdir() if p != path] == []


# --- the guards ---------------------------------------------------------------------------


def test_a_ladder_exempt_troops_weapon_is_left_alone():
    """The cave troll: a scenery-scale club whose slowness is the point."""
    cat = catalogue_with_pieces()
    priced = {"club": weapon("club", dps=25.0, template="TwoHandedMace")}
    troops = [troop("cave_troll", 51, "mordor", ["club"])]
    s = spec(exempt_troops={"cave_troll": "a monster, not a soldier"})
    plans, notes = restat.build_plans(priced, cat, troops, s)
    assert plans == []
    assert any("ladder-exempt" in n for n in notes)


def test_a_couchable_weapon_is_left_alone():
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


def test_a_hero_blade_is_left_alone():
    cat = catalogue_with_pieces()
    priced = {"anduril": weapon("anduril", dps=200.0, blade="hero_blade")}
    cat.pieces["hero_blade"] = cat.pieces["blade_one"]
    troops = [troop("lord", 51, "gondor", ["anduril"])]
    plans, _notes = restat.build_plans(priced, cat, troops, spec(hero_blades=["hero_blade"]))
    assert plans == []


# --- the arithmetic -----------------------------------------------------------------------


def test_the_multiplier_is_the_ratio_of_target_to_current():
    """DPS is exactly linear in damage_factor, so no search is needed."""
    cat = catalogue_with_pieces()
    # A tier-5 troop with no kingdom offset targets 69; give it a 34.5 DPS weapon, so x2.
    priced = {"sword": weapon("sword", dps=34.5)}
    troops = [troop("t", 26, "nowhere", ["sword"])]
    plans, _ = restat.build_plans(priced, cat, troops, spec())
    assert len(plans) == 1
    p = plans[0]
    assert p.ratio == pytest.approx(2.0, rel=1e-3)
    assert p.new_swing == pytest.approx(8.0, abs=0.01)   # 4.00 x 2
    assert p.new_thrust == pytest.approx(4.0, abs=0.01)  # 2.00 x 2


def test_a_weapon_already_on_target_is_not_rewritten():
    """Keeps the diff to what must move, and makes a second run a no-op."""
    cat = catalogue_with_pieces()
    priced = {"sword": weapon("sword", dps=69.0)}
    troops = [troop("t", 26, "nowhere", ["sword"])]
    assert restat.build_plans(priced, cat, troops, spec())[0] == []


def test_a_shared_blade_is_priced_for_its_weakest_anchor():
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
    assert plans[0].target_dps == pytest.approx(34)
    assert sorted(plans[0].items) == ["high", "low"]


def test_factors_are_clamped_into_a_sane_range_and_the_clamp_is_reported():
    cat = catalogue_with_pieces()
    # Absurdly weak weapon on a tier-10 troop: the raw ratio would blow past the ceiling.
    priced = {"twig": weapon("twig", dps=1.0)}
    troops = [troop("elite", 51, "nowhere", ["twig"])]
    plans, _ = restat.build_plans(priced, cat, troops, spec())
    assert len(plans) == 1
    assert plans[0].new_swing <= restat.MAX_FACTOR
    assert plans[0].clamped


def test_a_zero_factor_stays_zero():
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
