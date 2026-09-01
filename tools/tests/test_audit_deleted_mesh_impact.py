#!/usr/bin/env python3
"""Unit tests for the deleted-mesh impact audit (tools/audit_deleted_mesh_impact.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_audit_deleted_mesh_impact.py

Pure stdlib, synthetic fixtures, no game install and no real .tpac: every engine
function takes its inputs directly, mirroring test_validate_mesh_refs.py.

Each test maps to one part of the contract:
  - mesh set diff: gone vs newly-added, both directions reported separately
  - team-colour recoverability is an EXACT suffix rule, never a fuzzy match
  - prefixed item refs: attribute-agnostic, multi-line opens, comments excluded
  - bare item refs: the four C#-parsed config shapes an Item.-anchored sweep misses
  - the roster hop: item -> EquipmentRoster -> NPCCharacter
  - blast radius: ORPHAN vs EQUIPPED
  - the report rows join all of the above and stay deterministic
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_deleted_mesh_impact as am  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "audit_deleted_mesh_impact.py"


# --------------------------------------------------------------------------- #
# Mesh set diff                                                                #
# --------------------------------------------------------------------------- #
class TestMeshDiff(unittest.TestCase):
    def test_gone_and_added_are_reported_separately(self):
        delta = am.diff_mesh_sets({"a", "b", "c"}, {"b", "c", "d"})
        self.assertEqual(delta.gone, {"a"})
        self.assertEqual(delta.added, {"d"})

    def test_identical_sets_yield_no_delta(self):
        delta = am.diff_mesh_sets({"a", "b"}, {"a", "b"})
        self.assertEqual(delta.gone, set())
        self.assertEqual(delta.added, set())

    def test_lod_suffixes_are_normalised_before_diffing(self):
        # The engine appends .lodN; a ref names the base. A pack exposing only
        # "x.lod2" must not read as "x is gone".
        delta = am.diff_mesh_sets({"x.lod2", "y"}, {"x", "y"})
        self.assertEqual(delta.gone, set())


# --------------------------------------------------------------------------- #
# Recoverability - the exact team-colour rule                                  #
# --------------------------------------------------------------------------- #
class TestRecoverability(unittest.TestCase):
    def test_team_colour_variant_recovers_to_surviving_base(self):
        surviving = {"sk_dwarf_erebor_chest_plate_elite_a2"}
        for colour in ("blue", "green", "red"):
            name = "sk_dwarf_erebor_chest_plate_elite_a2_" + colour
            kind, repl = am.classify_recoverability(name, surviving)
            self.assertEqual(kind, am.TEAM_COLOUR)
            self.assertEqual(repl, "sk_dwarf_erebor_chest_plate_elite_a2")

    def test_team_colour_suffix_without_surviving_base_is_lost(self):
        kind, repl = am.classify_recoverability("gone_thing_red", set())
        self.assertEqual(kind, am.LOST)
        self.assertEqual(repl, "")

    def test_plain_deleted_mesh_is_lost(self):
        kind, repl = am.classify_recoverability("angbor_body", {"forlong_body"})
        self.assertEqual(kind, am.LOST)
        self.assertEqual(repl, "")

    def test_no_fuzzy_match_is_ever_promoted_to_a_remedy(self):
        # difflib pairs lotr_troll_helmet with lotr_troll_feet at cutoff 0.86.
        # That is advisory noise and must never classify as recoverable.
        kind, _ = am.classify_recoverability("lotr_troll_helmet", {"lotr_troll_feet"})
        self.assertEqual(kind, am.LOST)


# --------------------------------------------------------------------------- #
# Prefixed item references (Item.x)                                            #
# --------------------------------------------------------------------------- #
class TestPrefixedRefs(unittest.TestCase):
    def test_single_line_equipment_tag(self):
        xml = '<equipment slot="Body" id="Item.angbor_body" />'
        refs = am.extract_item_refs_from_text(xml, "troops/t.xml")
        self.assertEqual([r.item_id for r in refs], ["angbor_body"])
        self.assertEqual(refs[0].line, 1)

    def test_multiline_equipment_tag_is_not_missed(self):
        # 12 troop files split the tag across three lines. A per-line grep
        # anchored on the element name undercounts these by about half.
        xml = '<equipment\n    slot="Item0"\n    id="Item.wm_rivendell_sword_a01" />'
        refs = am.extract_item_refs_from_text(xml, "troops/t.xml")
        self.assertEqual([r.item_id for r in refs], ["wm_rivendell_sword_a01"])
        self.assertEqual(refs[0].line, 3)

    def test_matcher_is_attribute_agnostic(self):
        xml = '<item id="Item.a" /><Equipment slot="Body" id="Item.b" />'
        refs = am.extract_item_refs_from_text(xml, "c.xml")
        self.assertEqual(sorted(r.item_id for r in refs), ["a", "b"])

    def test_commented_refs_are_excluded(self):
        xml = '<!-- <equipment id="Item.ghost" /> -->\n<equipment id="Item.real" />'
        refs = am.extract_item_refs_from_text(xml, "c.xml")
        self.assertEqual([r.item_id for r in refs], ["real"])

    def test_owning_character_is_attached_to_each_ref(self):
        xml = (
            '<NPCCharacter id="lord_a">\n'
            '  <equipment id="Item.sword" />\n'
            '</NPCCharacter>\n'
            '<NPCCharacter id="lord_b">\n'
            '  <equipment id="Item.shield" />\n'
            '</NPCCharacter>\n'
        )
        refs = am.extract_item_refs_from_text(xml, "characters/lords.xml")
        self.assertEqual({r.item_id: r.owner for r in refs},
                         {"sword": "lord_a", "shield": "lord_b"})

    def test_anonymous_roster_does_not_steal_the_owner(self):
        # Shape A wraps equipment in an ANONYMOUS <EquipmentRoster>. Scanning
        # for "the next id= after an owner tag" latches onto the first
        # id="Item.x" instead, so every character comes out named after a sword.
        xml = (
            '<NPCCharacter id="npc_rhun_a">\n'
            '  <Equipments>\n'
            '    <EquipmentRoster civilian="true">\n'
            '      <equipment slot="Body" id="Item.easterling_boots" />\n'
            '    </EquipmentRoster>\n'
            '  </Equipments>\n'
            '</NPCCharacter>\n'
        )
        refs = am.extract_item_refs_from_text(xml, "characters/npcs_rhun.xml")
        self.assertEqual([r.owner for r in refs], ["npc_rhun_a"])

    def test_an_owner_is_never_an_item_reference(self):
        xml = (
            '<NPCCharacter id="npc_a">\n'
            '  <EquipmentRoster>\n'
            '    <equipment id="Item.sword" />\n'
            '    <equipment id="Item.shield" />\n'
            '  </EquipmentRoster>\n'
            '</NPCCharacter>\n'
        )
        refs = am.extract_item_refs_from_text(xml, "characters/npcs_a.xml")
        self.assertTrue(refs)
        self.assertFalse([r for r in refs if r.owner.startswith("Item.")])

    def test_owner_survives_a_multiline_element_open(self):
        xml = (
            '<EquipmentRoster\n'
            '    id="gondor_bat_template_medium_a"\n'
            '    culture="Culture.gondor">\n'
            '  <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />\n'
            '</EquipmentRoster>\n'
        )
        refs = am.extract_item_refs_from_text(xml, "equipmentsets/g.xml")
        self.assertEqual(refs[0].owner, "gondor_bat_template_medium_a")


# --------------------------------------------------------------------------- #
# Bare item references - the C#-parsed configs                                 #
# --------------------------------------------------------------------------- #
class TestCraftingPieceRefs(unittest.TestCase):
    """A crafting piece lives in a different id namespace from `Item.`, so
    neither the prefixed-ref nor the roster shape can see it. Before these two
    shapes existed the audit reported every crafting piece as ORPHAN.

    That verdict covered all six `easterling_*` weapon pieces on 2026-09-01.
    They build `easterling_sword` and `easterling_spear`, and `easterling_spear`
    is player CAREER STARTING equipment, so acting on the ORPHAN verdict would
    have deleted a Rhun start's weapon. It was caught by hand, which is not a
    control. These tests are the control.
    """

    def test_usable_piece_in_a_crafting_template_is_a_reference(self):
        xml = ('<CraftingTemplate id="TwoHandedPolearm">\n'
               '  <UsablePiece piece_id="easterling_spear_blade" />\n'
               '  <UsablePiece piece_id="easterling_spear_handle" />\n'
               '</CraftingTemplate>\n')
        refs = am.extract_piece_refs_from_text(xml, "crafting_templates.xslt")
        self.assertEqual([r.item_id for r in refs],
                         ["easterling_spear_blade", "easterling_spear_handle"])
        self.assertTrue(all(r.shape == "crafting_piece" for r in refs))

    def test_piece_inside_a_crafted_item_is_a_reference(self):
        xml = ('<CraftedItem id="easterling_spear" crafting_template="TwoHandedPolearm">\n'
               '  <Pieces>\n'
               '    <Piece id="easterling_spear_blade" Type="Blade" />\n'
               '    <Piece id="easterling_spear_handle" Type="Handle" />\n'
               '  </Pieces>\n'
               '</CraftedItem>\n')
        refs = am.extract_piece_refs_from_text(xml, "LOTRAOM_weapons.xml")
        self.assertEqual({r.item_id for r in refs},
                         {"easterling_spear_blade", "easterling_spear_handle"})

    def test_a_crafting_piece_definition_is_not_a_reference(self):
        """`<CraftingPiece id="x">` DEFINES x. Only <Piece>/<UsablePiece> refer."""
        xml = '<CraftingPiece id="easterling_spear_blade" mesh="m" />\n'
        self.assertEqual(am.extract_piece_refs_from_text(xml, "x.xml"), [])

    def test_comments_are_not_counted(self):
        xml = '<!-- <Piece id="easterling_spear_blade" Type="Blade" /> -->\n'
        self.assertEqual(am.extract_piece_refs_from_text(xml, "x.xml"), [])

    def test_line_numbers_are_reported(self):
        xml = ('<Pieces>\n'
               '  <Piece id="a" />\n'
               '  <Piece id="b" />\n'
               '</Pieces>\n')
        refs = am.extract_piece_refs_from_text(xml, "x.xml")
        self.assertEqual([(r.item_id, r.line) for r in refs], [("a", 2), ("b", 3)])


class TestBareRefs(unittest.TestCase):
    def test_settlement_guard_spear(self):
        xml = '<Spear culture="gondor" item="western_spear_3_t3" />'
        refs = am.extract_bare_refs(xml, "settlement_guards/settlement_guards_config.xml")
        self.assertEqual([r.item_id for r in refs], ["western_spear_3_t3"])

    def test_culture_marketplace_item(self):
        xml = '<Item id="warg_brown" cultures="isengard,mordor" min_stock="1" />'
        refs = am.extract_bare_refs(xml, "culture_marketplace/culture_marketplace_config.xml")
        self.assertEqual([r.item_id for r in refs], ["warg_brown"])

    def test_lotr_issue_colon_namespaced_source(self):
        xml = 'item_source="item:grain"\nitem_source="item:iron"'
        refs = am.extract_bare_refs(xml, "lotr_issues/taom_lotr_issues.xml")
        self.assertEqual([r.item_id for r in refs], ["grain", "iron"])

    def test_banner_bearer_json_values(self):
        blob = json.dumps({
            "CultureBanners": {"gondor": "standard_of_duty_t1", "vlandia": "banner_t1"},
            "DefaultBannerItemId": "",
        })
        refs = am.extract_bare_refs(blob, "banner_bearers/banner_bearers_config.json")
        self.assertEqual(sorted(r.item_id for r in refs),
                         ["banner_t1", "standard_of_duty_t1"])

    def test_unknown_file_yields_nothing(self):
        # The bare shapes are file-scoped on purpose: an <Item id="x"> in an
        # Armory item file is a DEFINITION, not a reference.
        xml = '<Item id="sk_gd_helmet_a" mesh="sk_gd_helmet_a" />'
        self.assertEqual(
            am.extract_bare_refs(xml, "LOTRLOME_items/gondor/head_armors.xml"), [])


# --------------------------------------------------------------------------- #
# Roster hop: item -> EquipmentRoster -> NPCCharacter                          #
# --------------------------------------------------------------------------- #
class TestRosterHop(unittest.TestCase):
    def test_equipment_set_refs_are_extracted_with_their_owner(self):
        xml = (
            '<NPCCharacter id="lord_1_1">\n'
            '  <Equipments>\n'
            '    <EquipmentSet id="dunland_bat_template_medium_a" />\n'
            '  </Equipments>\n'
            '</NPCCharacter>\n'
        )
        refs = am.extract_roster_refs_from_text(xml, "characters/lords.xml")
        self.assertEqual(len(refs), 1)
        self.assertEqual(refs[0].roster_id, "dunland_bat_template_medium_a")
        self.assertEqual(refs[0].owner, "lord_1_1")

    def test_a_roster_definition_is_not_a_roster_reference(self):
        xml = ('<EquipmentRoster id="r1"><EquipmentSet>'
               '<Equipment id="Item.x" /></EquipmentSet></EquipmentRoster>')
        self.assertEqual(am.extract_roster_refs_from_text(xml, "e.xml"), [])

    def test_item_reaches_characters_through_the_roster(self):
        item_refs = [am.ItemRef("axe", "equipmentsets/e.xml", 3, "prefixed", "roster_a")]
        roster_refs = [
            am.RosterRef("roster_a", "characters/lords.xml", 9, "lord_1"),
            am.RosterRef("roster_a", "taom_wanderers.xml", 4, "wanderer_2"),
            am.RosterRef("roster_b", "characters/lords.xml", 20, "lord_9"),
        ]
        reached = am.resolve_roster_hop({"axe"}, item_refs, roster_refs)
        self.assertEqual(reached["axe"], {"lord_1", "wanderer_2"})

    def test_xslt_template_match_identifies_the_owning_lord(self):
        # lords.xslt authors ~389 lords' <Equipments> wholesale. The owner is
        # the template's match predicate, not an <NPCCharacter> tag, so an
        # element-only owner scan attributes all 778 roster refs to nobody and
        # silently reports every XSLT-authored lord as unaffected.
        xslt = (
            "<xsl:template match=\"NPCCharacter[@id='lord_1_1']\">\n"
            "  <Equipments>\n"
            "    <EquipmentSet id=\"angbor_bat_equipment\" />\n"
            "  </Equipments>\n"
            "</xsl:template>\n"
        )
        refs = am.extract_roster_refs_from_text(xslt, "lords.xslt")
        self.assertEqual([r.owner for r in refs], ["lord_1_1"])

    def test_xslt_culture_template_owns_its_item_refs(self):
        xslt = ("<xsl:template match=\"Culture[@id='empire']\">\n"
                "  <item id=\"Item.dunland_caerdh_axe_2h_a\" />\n"
                "</xsl:template>\n")
        refs = am.extract_item_refs_from_text(xslt, "spcultures.xslt")
        self.assertEqual([r.owner for r in refs], ["empire"])
        self.assertEqual([r.shape for r in refs], ["xslt"])

    def test_item_outside_any_roster_reaches_nobody_indirectly(self):
        item_refs = [am.ItemRef("axe", "troops/t.xml", 3, "prefixed", "troop_a")]
        reached = am.resolve_roster_hop({"axe"}, item_refs, [])
        self.assertEqual(reached["axe"], set())


# --------------------------------------------------------------------------- #
# Crafting-piece owner backfill                                                #
# --------------------------------------------------------------------------- #
class TestEntryIdBackfill(unittest.TestCase):
    """validate_mesh_refs attributes a ref to <Item>/<CraftedItem> only, so a
    mesh named by a <CraftingPiece> arrives ownerless. Six deleted easterling
    weapon meshes are exactly that, and an ownerless row cannot be acted on."""

    def _ref(self, name, rel, line, item_id=""):
        import validate_mesh_refs as vm
        return vm.MeshRef(name, "mesh", "visual_mesh", rel, line, item_id, "")

    def test_crafting_piece_id_is_backfilled(self):
        xml = (
            '<CraftingPieces>\n'
            '  <CraftingPiece id="easterling_sword_blade" piece_type="Blade"\n'
            '      mesh="easterling_sword_blade">\n'
            '  </CraftingPiece>\n'
            '</CraftingPieces>\n'
        )
        refs = [self._ref("easterling_sword_blade", "LOTRLOME_crafting_pieces.xml", 3)]
        am.backfill_entry_ids(refs, {"LOTRLOME_crafting_pieces.xml": xml})
        self.assertEqual(refs[0].item_id, "easterling_sword_blade")

    def test_an_existing_item_id_is_never_overwritten(self):
        xml = '<Item id="real_id" mesh="m" />'
        refs = [self._ref("m", "i.xml", 1, item_id="real_id")]
        am.backfill_entry_ids(refs, {"i.xml": xml})
        self.assertEqual(refs[0].item_id, "real_id")

    def test_missing_text_leaves_the_ref_untouched(self):
        refs = [self._ref("m", "absent.xml", 1)]
        am.backfill_entry_ids(refs, {})
        self.assertEqual(refs[0].item_id, "")


# --------------------------------------------------------------------------- #
# Blast radius + the joined report rows                                        #
# --------------------------------------------------------------------------- #
def _mesh_refs():
    import validate_mesh_refs as vm
    return [
        vm.MeshRef("angbor_body", "mesh", "visual_mesh",
                   "LOTRLOME_items/gondor/body_armors.xml", 12, "angbor_body", "gondor"),
        vm.MeshRef("sk_x_red", "mesh", "visual_mesh",
                   "LOTRLOME_items/erebor/body_armors.xml", 30, "sk_x_red", "erebor"),
    ]


class TestImpactRows(unittest.TestCase):
    def test_orphan_and_equipped_are_distinguished(self):
        rows = am.build_impact_rows(
            gone={"angbor_body", "sk_x_red"},
            surviving={"sk_x"},
            mesh_refs=_mesh_refs(),
            item_refs=[am.ItemRef("angbor_body", "characters/npcs_gondor.xml",
                                  5, "prefixed", "npc_a")],
            roster_refs=[],
        )
        by_item = {r.item_id: r for r in rows}
        self.assertEqual(by_item["angbor_body"].blast, am.EQUIPPED)
        self.assertEqual(by_item["sk_x_red"].blast, am.ORPHAN)

    def test_recoverability_is_carried_onto_the_row(self):
        rows = am.build_impact_rows(
            gone={"angbor_body", "sk_x_red"}, surviving={"sk_x"},
            mesh_refs=_mesh_refs(), item_refs=[], roster_refs=[])
        by_item = {r.item_id: r for r in rows}
        self.assertEqual(by_item["sk_x_red"].recoverability, am.TEAM_COLOUR)
        self.assertEqual(by_item["sk_x_red"].replacement, "sk_x")
        self.assertEqual(by_item["angbor_body"].recoverability, am.LOST)

    def test_surviving_meshes_produce_no_rows(self):
        rows = am.build_impact_rows(
            gone=set(), surviving={"angbor_body"},
            mesh_refs=_mesh_refs(), item_refs=[], roster_refs=[])
        self.assertEqual(rows, [])

    def test_rows_are_deterministically_ordered(self):
        rows = am.build_impact_rows(
            gone={"angbor_body", "sk_x_red"}, surviving={"sk_x"},
            mesh_refs=_mesh_refs(), item_refs=[], roster_refs=[])
        self.assertEqual([r.mesh for r in rows], sorted(r.mesh for r in rows))

    def test_characters_include_both_direct_and_roster_reached(self):
        rows = am.build_impact_rows(
            gone={"angbor_body"}, surviving=set(),
            mesh_refs=_mesh_refs()[:1],
            item_refs=[
                am.ItemRef("angbor_body", "characters/npcs_gondor.xml",
                           5, "prefixed", "npc_direct"),
                am.ItemRef("angbor_body", "equipmentsets/e.xml",
                           9, "prefixed", "roster_a"),
            ],
            roster_refs=[am.RosterRef("roster_a", "characters/lords.xml",
                                      2, "lord_via_roster")],
        )
        self.assertEqual(rows[0].characters, ["lord_via_roster", "npc_direct"])


# --------------------------------------------------------------------------- #
# Git corroboration: LFS pointers                                              #
# --------------------------------------------------------------------------- #
class TestLfsPointer(unittest.TestCase):
    """The asset repo tracks *.tpac through LFS, so `git show` hands back a
    128-byte pointer rather than the pack. Scanning that finds no meshes, and
    counting it as "nothing to see" reports a silent zero that reads exactly
    like the deleted set being confirmed empty."""

    def test_pointer_blob_is_detected(self):
        blob = (b"version https://git-lfs.github.com/spec/v1\n"
                b"oid sha256:4f23e8e6cb637ed253704f505f\nsize 9827545\n")
        self.assertTrue(am.is_lfs_pointer(blob))

    def test_real_tpac_bytes_are_not_a_pointer(self):
        self.assertFalse(am.is_lfs_pointer(b"TPAC\x02\x00\x00\x00rest"))

    def test_empty_blob_is_not_a_pointer(self):
        self.assertFalse(am.is_lfs_pointer(b""))


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
class TestCli(unittest.TestCase):
    def test_help_exits_zero(self):
        r = subprocess.run([sys.executable, str(TOOL), "--help"],
                           capture_output=True, text=True)
        self.assertEqual(r.returncode, 0)

    def test_bad_root_exits_two_not_clean(self):
        with tempfile.TemporaryDirectory() as td:
            missing = str(Path(td) / "nope")
            r = subprocess.run([sys.executable, str(TOOL), "--armory", missing],
                               capture_output=True, text=True)
            self.assertEqual(r.returncode, 2)

    def test_tool_declares_no_apply_flag(self):
        # This pass is report-only by contract. A writer must not appear here
        # without the guardrail being revisited.
        self.assertNotIn("--apply", TOOL.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
