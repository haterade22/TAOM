#!/usr/bin/env python3
"""Unit tests for the mesh-reference validator (tools/validate_mesh_refs.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_validate_mesh_refs.py

These tests are pure stdlib with synthetic fixtures and an injectable present-set
(no game install / no real .tpac needed): the engine functions extract_refs_from_text,
classify, scan_tpac_metameshes, body_present_in_tpacs and parse_rgl_text are all
called directly with hand-built inputs, mirroring test_validate_moduledata.py.

Each test maps to one part of the contract:
  - extract all 6 attrs with correct kind + line numbers
  - exclude the 3 non-mesh attrs + record prefab as PREFAB_REF
  - missing-visual -> MISSING_MESH (ERROR); present-visual -> clean
  - body byte-scan present vs absent
  - rgl get_object failed -> RUNTIME_MISSING_BODY; body NOT in log -> no finding
  - rgl duplicate line -> DUPLICATE_MESH_NAME
  - unparsed-tpac -> UNVERIFIED_MESH + UNPARSED_TPAC (no false ERROR)
  - exit codes + --code filter + --warnings-as-errors
  - malformed-XML tolerance
  - culture grouping
  - a minimal hand-built TOC byte buffer proving the Tier-B struct offsets
"""
import os
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import validate_mesh_refs as vm  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "validate_mesh_refs.py"


def _codes(issues):
    return [i.code for i in issues]


# --------------------------------------------------------------------------- #
# Reference extraction                                                         #
# --------------------------------------------------------------------------- #
class ExtractRefsTests(unittest.TestCase):
    def test_extracts_all_six_attrs_with_kind_and_line(self):
        xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'          # 1
            '<Items>\n'                                          # 2
            '  <Item id="helm_a" mesh="sk_gd_ano_helmet_a">\n'  # 3
            '    <Flags body_name="bo_helm_a" />\n'              # 4
            '  </Item>\n'                                        # 5
            '  <Item id="shield_a" shield_body_name="bo_shield_a">\n'  # 6
            '    <ItemComponent>\n'                              # 7
            '      <Armor holster_mesh="hm_a" holster_mesh_with_weapon="hmw_a" />\n'  # 8
            '    </ItemComponent>\n'                             # 9
            '  </Item>\n'                                        # 10
            '  <Item id="horse_a" flying_mesh="fm_a" />\n'       # 11
            '</Items>\n'                                         # 12
        )
        refs = vm.extract_refs_from_text(xml, "gondor/head_armors.xml")
        by_attr = {r.attr: r for r in refs}
        self.assertEqual(set(by_attr), {
            "mesh", "body_name", "shield_body_name",
            "holster_mesh", "holster_mesh_with_weapon", "flying_mesh",
        })
        self.assertEqual(by_attr["mesh"].kind, "visual_mesh")
        self.assertEqual(by_attr["body_name"].kind, "collision_body")
        self.assertEqual(by_attr["shield_body_name"].kind, "collision_body")
        self.assertEqual(by_attr["holster_mesh"].kind, "visual_mesh")
        self.assertEqual(by_attr["holster_mesh_with_weapon"].kind, "visual_mesh")
        self.assertEqual(by_attr["flying_mesh"].kind, "visual_mesh")
        # Line numbers point at the source line.
        self.assertEqual(by_attr["mesh"].line, 3)
        self.assertEqual(by_attr["body_name"].line, 4)
        self.assertEqual(by_attr["shield_body_name"].line, 6)
        self.assertEqual(by_attr["holster_mesh"].line, 8)
        self.assertEqual(by_attr["flying_mesh"].line, 11)
        # item_id attribution flows down from the owning <Item>.
        self.assertEqual(by_attr["mesh"].item_id, "helm_a")
        self.assertEqual(by_attr["body_name"].item_id, "helm_a")
        self.assertEqual(by_attr["flying_mesh"].item_id, "horse_a")
        # culture derived from folder.
        self.assertEqual(by_attr["mesh"].culture, "gondor")

    def test_excludes_three_false_positive_attrs(self):
        xml = (
            '<Item id="x" mesh="real_mesh" mesh_maturity_type="Teenager" '
            'holster_mesh_length="42.0" recalculate_body="true" covers_body="true" />\n'
        )
        refs = vm.extract_refs_from_text(xml, "gondor/body_armors.xml")
        attrs = {r.attr for r in refs}
        self.assertIn("mesh", attrs)
        self.assertNotIn("mesh_maturity_type", attrs)
        self.assertNotIn("holster_mesh_length", attrs)
        self.assertNotIn("recalculate_body", attrs)
        self.assertNotIn("covers_body", attrs)
        # exactly one ref (the real mesh)
        self.assertEqual(len(refs), 1)
        self.assertEqual(refs[0].name, "real_mesh")

    def test_extracts_skin_body_meshes_from_skins_xml(self):
        """skins.xml body meshes are in scope (#403, 2026-08-07).

        skins.xml has always sat inside the default scan root (ModuleData/), so this file
        was walked for a year and reported nothing purely because these eight attribute
        names were not registered. The gap cost two players a hard CTD: an unresolved skin
        mesh faults natively with a null geometry base, producing no managed exception and
        therefore no TAOM crash bundle to triage.
        """
        xml = (
            '<skin gender="1" name="woman" skeleton="dwarf_skeleton_a"\n'
            '      body_meta_mesh="sk_dwarf_bm_f1_body"\n'
            '      body_meta_mesh_shoulders="sk_dwarf_bm_f1_shoulder"\n'
            '      body_meta_mesh_upperbody="box_a"\n'
            '      face_meta_mesh="sk_dwarf_bm_f1_head"\n'
            '      hands_mesh="sk_dwarf_bm_f1_arms"\n'
            '      legs_mesh="sk_dwarf_bm_f1_legs"\n'
            '      underwear_bottom_mesh="sk_dwarf_underwear_female_a"\n'
            '      underwear_top_mesh="" />\n'
        )
        refs = vm.extract_refs_from_text(xml, "skins.xml")
        by_attr = {r.attr: r for r in refs}

        for attr in ("body_meta_mesh", "body_meta_mesh_shoulders", "body_meta_mesh_upperbody",
                     "face_meta_mesh", "hands_mesh", "legs_mesh", "underwear_bottom_mesh"):
            self.assertIn(attr, by_attr, f"{attr} must be extracted from skins.xml")
            self.assertEqual(by_attr[attr].kind, "visual_mesh")

        self.assertEqual(by_attr["underwear_bottom_mesh"].name, "sk_dwarf_underwear_female_a")
        # Empty values are skipped by the attr regex, so the empty top slot yields no ref.
        self.assertNotIn("underwear_top_mesh", by_attr)

    def test_excludes_body_mesh_suffix_which_is_not_a_mesh_name(self):
        """body_mesh_suffix holds "_fem", a suffix appended to ARMOUR mesh names at render
        time — not a mesh. Registering it would raise MISSING_MESH on every female skin."""
        xml = '<skin name="woman" body_mesh_suffix="_fem" body_meta_mesh="real_body" />\n'
        refs = vm.extract_refs_from_text(xml, "skins.xml")
        attrs = {r.attr for r in refs}
        self.assertIn("body_meta_mesh", attrs)
        self.assertNotIn("body_mesh_suffix", attrs)
        self.assertEqual(len(refs), 1)

    def test_skin_mesh_missing_from_present_set_is_an_error(self):
        """The #403 regression, end to end through classify().

        `sk_dwarf_underwear_female` is a strict PREFIX of the shipped
        `sk_dwarf_underwear_female_a`, so any substring check calls it present. Tier B
        compares against exact .tpac TOC names, which is the whole reason it catches this.
        """
        xml = ('<skin name="woman" '
               'underwear_bottom_mesh="sk_dwarf_underwear_female" />\n')
        refs = vm.extract_refs_from_text(xml, "skins.xml")
        present = vm.PresentSet(metameshes={"sk_dwarf_underwear_female_a"},
                                tpac_paths=["fake.tpac"])

        issues = vm.classify(refs, present, None, scan_bodies=False)
        missing = [i for i in issues if i.code == "MISSING_MESH"]

        self.assertEqual(len(missing), 1, "the prefix-only name must NOT count as present")
        self.assertIn("sk_dwarf_underwear_female", missing[0].message)

    def test_prefab_is_recorded_as_prefab_kind_not_mesh(self):
        xml = '<Item id="x" prefab="some_prefab" mesh="real_mesh" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/body_armors.xml")
        kinds = {r.attr: r.kind for r in refs}
        self.assertEqual(kinds["prefab"], "prefab")
        self.assertEqual(kinds["mesh"], "visual_mesh")
        # prefab surfaces as PREFAB_REF (INFO), never as a mesh-missing finding.
        issues = vm.classify(refs, present=None, rgl=None, scan_bodies=False)
        prefab_issues = [i for i in issues if i.code == "PREFAB_REF"]
        self.assertEqual(len(prefab_issues), 1)
        self.assertEqual(prefab_issues[0].severity, vm.Severity.INFO)

    def test_defensive_mesh_element_and_multi_mesh(self):
        xml = (
            '<Item id="x" multi_mesh="combo_mesh">\n'
            '  <meshes>\n'
            '    <Mesh name="part_a" />\n'
            '    <Mesh name="part_b" />\n'
            '  </meshes>\n'
            '</Item>\n'
        )
        refs = vm.extract_refs_from_text(xml, "gondor/body_armors.xml")
        names = {r.name for r in refs}
        self.assertEqual(names, {"combo_mesh", "part_a", "part_b"})
        self.assertTrue(all(r.kind == "visual_mesh" for r in refs))

    def test_comment_stripped_refs_not_extracted(self):
        xml = (
            '<Items>\n'
            '  <!-- <Item id="ghost" mesh="ghost_mesh" /> -->\n'
            '  <Item id="real" mesh="real_mesh" />\n'
            '</Items>\n'
        )
        refs = vm.extract_refs_from_text(xml, "gondor/head_armors.xml")
        names = {r.name for r in refs}
        self.assertIn("real_mesh", names)
        self.assertNotIn("ghost_mesh", names)

    def test_malformed_xml_does_not_crash(self):
        xml = '<Items>\n  <Item id="x" mesh="m1"\n'  # unterminated
        refs = vm.extract_refs_from_text(xml, "gondor/head_armors.xml")
        self.assertIsInstance(refs, list)
        self.assertTrue(any(r.name == "m1" for r in refs))


# --------------------------------------------------------------------------- #
# Tier B: visual meshes vs present-set                                         #
# --------------------------------------------------------------------------- #
class TierBTests(unittest.TestCase):
    def _refs(self, mesh_name):
        xml = f'<Item id="it" mesh="{mesh_name}" />\n'
        return vm.extract_refs_from_text(xml, "gondor/head_armors.xml")

    def test_present_visual_mesh_is_clean(self):
        refs = self._refs("sk_present")
        present = vm.PresentSet(metameshes={"sk_present"}, tpac_paths=["fake.tpac"])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        self.assertNotIn("MISSING_MESH", _codes(issues))

    def test_missing_visual_mesh_is_error(self):
        refs = self._refs("sk_absent")
        present = vm.PresentSet(metameshes={"sk_present"}, tpac_paths=["fake.tpac"])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        self.assertIn("MISSING_MESH", _codes(issues))
        miss = [i for i in issues if i.code == "MISSING_MESH"][0]
        self.assertEqual(miss.severity, vm.Severity.ERROR)
        self.assertIn("sk_absent", miss.message)

    def test_lod_suffix_matches_base_name(self):
        # engine derives .lod1..N; a ref to the base resolves against the TOC.
        refs = self._refs("sk_present.lod2")
        present = vm.PresentSet(metameshes={"sk_present"}, tpac_paths=["fake.tpac"])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        self.assertNotIn("MISSING_MESH", _codes(issues))

    def test_unparsed_tpac_degrades_to_unverified_no_false_error(self):
        refs = self._refs("sk_absent")
        present = vm.PresentSet(
            metameshes={"sk_present"}, tpac_paths=["a.tpac", "b.tpac"],
            unparsed=[("b.tpac", "suspicious udep_count=99999 at item 5")])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        codes = _codes(issues)
        self.assertIn("UNVERIFIED_MESH", codes)
        self.assertIn("UNPARSED_TPAC", codes)
        self.assertNotIn("MISSING_MESH", codes, "unparsed pack must not yield false MISSING")
        unver = [i for i in issues if i.code == "UNVERIFIED_MESH"][0]
        self.assertEqual(unver.severity, vm.Severity.WARNING)


# --------------------------------------------------------------------------- #
# Known-dead allowlist + the Assets/ fallback (2026-09-01)                     #
# --------------------------------------------------------------------------- #
class KnownDeadMeshTests(unittest.TestCase):
    """The allowlist exists so an accepted decision stops failing the gate. It
    must never turn into a blanket mute: exact names only, and it has to speak
    up when its own reason expires."""

    def _refs(self, mesh_name, item_id="it"):
        xml = f'<Item id="{item_id}" mesh="{mesh_name}" />\n'
        return vm.extract_refs_from_text(xml, "troll/body_armors.xml")

    def _present(self, *names):
        return vm.PresentSet(metameshes=set(names), tpac_paths=["fake.tpac"])

    def test_allowlisted_dead_mesh_warns_instead_of_erroring(self):
        refs = self._refs("lotr_troll_armor")
        issues = vm.classify(refs, self._present("other"), rgl=None, scan_bodies=False)
        codes = _codes(issues)
        self.assertIn("KNOWN_DEAD_MESH", codes)
        self.assertNotIn("MISSING_MESH", codes)
        known = [i for i in issues if i.code == "KNOWN_DEAD_MESH"][0]
        self.assertEqual(known.severity, vm.Severity.WARNING)
        self.assertIn("cave_troll", known.message, "the reason must reach the report")

    def test_every_allowlist_entry_states_a_reason_and_a_date(self):
        self.assertTrue(vm.KNOWN_DEAD_MESHES, "allowlist should not be empty here")
        for name, reason in vm.KNOWN_DEAD_MESHES.items():
            self.assertRegex(reason, r"\d{4}-\d{2}-\d{2}",
                             f"{name} needs a decision date")
            self.assertGreater(len(reason), 40, f"{name} needs a real reason")

    def test_allowlist_is_exact_names_not_prefixes(self):
        # A NEW dead mesh in an allowlisted family must still be an error.
        refs = self._refs("lotr_troll_greaves")
        issues = vm.classify(refs, self._present("other"), rgl=None, scan_bodies=False)
        codes = _codes(issues)
        self.assertIn("MISSING_MESH", codes)
        self.assertNotIn("KNOWN_DEAD_MESH", codes)

    def test_allowlist_entry_flagged_stale_when_art_returns(self):
        refs = self._refs("lotr_troll_armor")
        present = self._present(*vm.KNOWN_DEAD_MESHES)   # all art back
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False,
                             check_allowlist=True)
        stale = [i for i in issues if i.code == "STALE_DEAD_MESH_ALLOWLIST"]
        self.assertTrue(stale, "art returning must retire the entry")
        self.assertIn("lotr_troll_armor", {i.entry_id for i in stale})

    def test_allowlist_entry_flagged_stale_when_nothing_references_it(self):
        refs = self._refs("something_else")
        issues = vm.classify(refs, self._present("something_else"),
                             rgl=None, scan_bodies=False, check_allowlist=True)
        stale = {i.entry_id for i in issues if i.code == "STALE_DEAD_MESH_ALLOWLIST"}
        self.assertEqual(stale, set(vm.KNOWN_DEAD_MESHES))

    def test_unparsed_pack_still_wins_over_the_allowlist(self):
        # Degraded evidence must not be dressed up as an accepted decision.
        refs = self._refs("lotr_troll_armor")
        present = vm.PresentSet(metameshes={"other"}, tpac_paths=["a.tpac"],
                                unparsed=[("a.tpac", "bad seg_count")])
        codes = _codes(vm.classify(refs, present, rgl=None, scan_bodies=False))
        self.assertIn("UNVERIFIED_MESH", codes)
        self.assertNotIn("KNOWN_DEAD_MESH", codes)


class TpacModuleFallbackTests(unittest.TestCase):
    """A module with no cooked packs must fall back to Assets/, not vanish from
    the present-set. LOTRLOME_Armory has been in that state since v2.0.23, and
    without the fallback every mesh it owns read as MISSING."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.game = Path(self.tmp.name)
        self.addCleanup(self.tmp.cleanup)

    def _module(self, name):
        m = self.game / "Modules" / name
        m.mkdir(parents=True, exist_ok=True)
        return m

    def test_cooked_packs_are_preferred_when_present(self):
        m = self._module("Cooked")
        (m / "AssetPackages").mkdir()
        (m / "AssetPackages" / "pack0.tpac").write_bytes(b"x")
        (m / "Assets" / "sub").mkdir(parents=True)
        (m / "Assets" / "sub" / "loose.tpac").write_bytes(b"x")
        got = [p.name for p in vm.tpac_paths_for_modules(self.game, ["Cooked"])]
        self.assertEqual(got, ["pack0.tpac"])

    def test_falls_back_to_loose_assets_when_no_cooked_tree(self):
        m = self._module("Loose")
        (m / "Assets" / "sub").mkdir(parents=True)
        (m / "Assets" / "sub" / "loose.tpac").write_bytes(b"x")
        got = [p.name for p in vm.tpac_paths_for_modules(self.game, ["Loose"])]
        self.assertEqual(got, ["loose.tpac"])

    def test_empty_cooked_dir_still_falls_back(self):
        m = self._module("EmptyCooked")
        (m / "AssetPackages").mkdir()
        (m / "Assets").mkdir()
        (m / "Assets" / "loose.tpac").write_bytes(b"x")
        got = [p.name for p in vm.tpac_paths_for_modules(self.game, ["EmptyCooked"])]
        self.assertEqual(got, ["loose.tpac"])

    def test_module_with_no_tpac_at_all_contributes_nothing(self):
        self._module("Bare")
        self.assertEqual(vm.tpac_paths_for_modules(self.game, ["Bare"]), [])

    def test_missing_module_is_not_an_error(self):
        self.assertEqual(vm.tpac_paths_for_modules(self.game, ["Absent"]), [])


# --------------------------------------------------------------------------- #
# Tier C: collision-body raw-byte scan                                         #
# --------------------------------------------------------------------------- #
class TierCBodyScanTests(unittest.TestCase):
    def _tpac_with_token(self, token: bytes) -> Path:
        # Frame the token as the engine does: <int32 len><utf8 bytes>, then a NUL.
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(b"\x00\x00\x00\x00")
        f.write(struct.pack("<i", len(token)))
        f.write(token)
        f.write(b"\x00\x00\x00\x00")
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        return Path(f.name)

    def test_body_present_in_raw_bytes(self):
        p = self._tpac_with_token(b"bo_helm_a")
        self.assertTrue(vm.body_present_in_tpacs("bo_helm_a", [p]))

    def test_body_absent_in_raw_bytes(self):
        p = self._tpac_with_token(b"bo_helm_a")
        self.assertFalse(vm.body_present_in_tpacs("bo_missing", [p]))

    def test_body_prefix_does_not_false_match(self):
        # "bo_helm" must NOT match the longer "bo_helm_a" token (framing boundary).
        p = self._tpac_with_token(b"bo_helm_a")
        self.assertFalse(vm.body_present_in_tpacs("bo_helm", [p]))

    def test_toc_present_body_resolves_without_byte_scan(self):
        # Tier C is exact when a PhysicsShape TOC is available: no .tpac paths are
        # passed at all, so a pass here can only come from the TOC set.
        xml = '<Item id="sw" body_name="bo_dunland_caerdh_sword_blade_2h_a" />\n'
        refs = vm.extract_refs_from_text(xml, "LOTRLOME_crafting_pieces.xml")
        present = vm.PresentSet(
            physicsshapes={"bo_dunland_caerdh_sword_blade_2h_a"}, tpac_paths=[])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=True)
        self.assertEqual([i for i in issues if i.code == "MISSING_BODY"], [])

    def test_suffix_typo_body_is_flagged_even_though_real_body_ships(self):
        # The exact #352 shape: the shipped body is `..._2h_a`; the XML says
        # `..._2h`. A substring-ish check would wrongly pass this.
        xml = '<Item id="sw" body_name="bo_dunland_caerdh_sword_blade_2h" />\n'
        refs = vm.extract_refs_from_text(xml, "LOTRLOME_crafting_pieces.xml")
        present = vm.PresentSet(
            physicsshapes={"bo_dunland_caerdh_sword_blade_2h_a"}, tpac_paths=[])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=True)
        codes = [i.code for i in issues]
        self.assertIn("MISSING_BODY", codes)

    def test_missing_body_classifies_as_error(self):
        xml = '<Item id="sh" shield_body_name="bo_missing" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/shoulder_armors.xml")
        p = self._tpac_with_token(b"bo_other")
        issues = vm.classify(refs, present=None, rgl=None, scan_bodies=True,
                             body_tpac_paths=[p])
        self.assertIn("MISSING_BODY", _codes(issues))
        miss = [i for i in issues if i.code == "MISSING_BODY"][0]
        self.assertEqual(miss.severity, vm.Severity.ERROR)
        self.assertIn("bo_missing", miss.message)

    def test_present_body_no_finding(self):
        xml = '<Item id="sh" shield_body_name="bo_present" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/shoulder_armors.xml")
        p = self._tpac_with_token(b"bo_present")
        issues = vm.classify(refs, present=None, rgl=None, scan_bodies=True,
                             body_tpac_paths=[p])
        self.assertNotIn("MISSING_BODY", _codes(issues))


# --------------------------------------------------------------------------- #
# Tier A: rgl_log parsing + cross-reference                                    #
# --------------------------------------------------------------------------- #
class TierARglTests(unittest.TestCase):
    def test_get_object_failed_referenced_by_item_is_runtime_missing_error(self):
        log = "CONTENT WARNING: get_object failed for body: bo_referenced\n"
        rgl = vm.parse_rgl_text(log)
        self.assertIn("bo_referenced", rgl.missing_bodies)
        xml = '<Item id="sh" shield_body_name="bo_referenced" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/shoulder_armors.xml")
        issues = vm.classify(refs, present=None, rgl=rgl, scan_bodies=False)
        rt = [i for i in issues if i.code == "RUNTIME_MISSING_BODY"]
        self.assertEqual(len(rt), 1)
        self.assertEqual(rt[0].severity, vm.Severity.ERROR)
        self.assertEqual(rt[0].entry_id, "sh")

    def test_get_object_failed_not_referenced_is_info_only(self):
        # The real-world bo_gondor_brick_rubble_c case: a SCENE body, no item refs.
        log = "CONTENT WARNING: get_object failed for body: bo_gondor_brick_rubble_c\n"
        rgl = vm.parse_rgl_text(log)
        # An item that references a DIFFERENT body, so the logged body is unreferenced.
        xml = '<Item id="sh" shield_body_name="bo_something_else" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/shoulder_armors.xml")
        issues = vm.classify(refs, present=None, rgl=rgl, scan_bodies=False)
        rt = [i for i in issues if i.code == "RUNTIME_MISSING_BODY"]
        self.assertEqual(len(rt), 1)
        self.assertEqual(rt[0].severity, vm.Severity.INFO,
                         "scene body not referenced by any item must be INFO, not ERROR")
        self.assertIn("not referenced", rt[0].message)
        # And it must NOT produce an ERROR anywhere.
        self.assertFalse(any(i.severity is vm.Severity.ERROR for i in issues))

    def test_missing_material_is_warning(self):
        log = "CONTENT WARNING: Unable to find material for mesh sk_referenced\n"
        rgl = vm.parse_rgl_text(log)
        self.assertIn("sk_referenced", rgl.missing_materials)
        xml = '<Item id="it" mesh="sk_referenced" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/head_armors.xml")
        issues = vm.classify(refs, present=None, rgl=rgl, scan_bodies=False)
        mat = [i for i in issues if i.code == "RUNTIME_MISSING_MATERIAL"]
        self.assertEqual(len(mat), 1)
        self.assertEqual(mat[0].severity, vm.Severity.WARNING)

    def test_duplicate_registration_line_is_duplicate_mesh_name(self):
        log = ("Package item with same name and type already exist in "
               "$BASE/Modules/TAOM_Map/Assets/Scenes/Rohan/t_brickslarge_d_tex.tpac(t_brickslarge_d)\n")
        rgl = vm.parse_rgl_text(log)
        self.assertIn("t_brickslarge_d", rgl.duplicate_names)
        issues = vm.classify([], present=None, rgl=rgl, scan_bodies=False)
        dup = [i for i in issues if i.code == "DUPLICATE_MESH_NAME"]
        self.assertEqual(len(dup), 1)
        self.assertEqual(dup[0].severity, vm.Severity.WARNING)
        self.assertEqual(dup[0].entry_id, "t_brickslarge_d")


# --------------------------------------------------------------------------- #
# Tier B struct-offset proof: a minimal hand-built TOC byte buffer             #
# --------------------------------------------------------------------------- #
class TpacBinaryParseTests(unittest.TestCase):
    @staticmethod
    def _build_one_metamesh_tpac(name: str) -> bytes:
        """Hand-build a container-version-2 .tpac header with exactly one
        Metamesh item, proving scan_tpac_metameshes reads the documented offsets.
        Layout: magic(4) ver(4) pkg_guid(16) num_items(4) pad(4) pad(4)
        then one item: type_guid(16) item_guid(16) item_ver(4) name(sized)
        meta_size(8)=0 checksum(8) seg_count(4)=0 udep_count(4)=0."""
        buf = bytearray()
        buf += struct.pack("<I", vm.TPAC_MAGIC)
        buf += struct.pack("<I", 2)                 # container version 2
        buf += b"\x11" * 16                         # package_guid
        buf += struct.pack("<I", 1)                 # num_items
        buf += struct.pack("<I", 0)                 # padding
        buf += struct.pack("<I", 0)                 # padding
        # --- item ---
        buf += vm.METAMESH_TYPE_GUID                # 16-byte LE Metamesh type guid
        buf += b"\x22" * 16                         # item_guid
        buf += struct.pack("<I", 0)                 # item_ver (ver>1)
        nb = name.encode("utf-8")
        buf += struct.pack("<i", len(nb)) + nb      # sized string
        buf += struct.pack("<q", 0)                 # meta_size = 0
        buf += struct.pack("<q", 0)                 # checksum
        buf += struct.pack("<i", 0)                 # seg_count = 0
        buf += struct.pack("<i", 0)                 # udep_count = 0
        return bytes(buf)

    @staticmethod
    def _build_one_item_tpac(name: str, type_guid: bytes) -> bytes:
        """Same layout as _build_one_metamesh_tpac, with the item type parameterised
        so PhysicsShape entries can be built too."""
        buf = bytearray()
        buf += struct.pack("<I", vm.TPAC_MAGIC)
        buf += struct.pack("<I", 2)                 # container version 2
        buf += b"\x11" * 16                         # package_guid
        buf += struct.pack("<I", 1)                 # num_items
        buf += struct.pack("<I", 0)                 # padding
        buf += struct.pack("<I", 0)                 # padding
        # --- item ---
        buf += type_guid                            # 16-byte LE type guid
        buf += b"\x22" * 16                         # item_guid
        buf += struct.pack("<I", 0)                 # item_ver (ver>1)
        nb = name.encode("utf-8")
        buf += struct.pack("<i", len(nb)) + nb      # sized string
        buf += struct.pack("<q", 0)                 # meta_size = 0
        buf += struct.pack("<q", 0)                 # checksum
        buf += struct.pack("<i", 0)                 # seg_count = 0
        buf += struct.pack("<i", 0)                 # udep_count = 0
        return bytes(buf)

    def test_minimal_metamesh_toc_parses(self):
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(self._build_one_metamesh_tpac("sk_test_mesh"))
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        res = vm.scan_tpac_metameshes(f.name)
        self.assertTrue(res.parsed_ok, f"parse failed: {res.error}")
        self.assertIn("sk_test_mesh", res.metamesh_names)

    def test_physicsshape_toc_item_is_collected_as_a_body(self):
        # #352: collision bodies ARE first-class TOC items, not raw-byte-only.
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(self._build_one_item_tpac("bo_test_body", vm.PHYSICSSHAPE_TYPE_GUID))
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        res = vm.scan_tpac_metameshes(f.name)
        self.assertTrue(res.parsed_ok, f"parse failed: {res.error}")
        self.assertIn("bo_test_body", res.physicsshape_names)
        # A body must not be mistaken for a visual mesh.
        self.assertNotIn("bo_test_body", res.metamesh_names)

    def test_non_metamesh_item_not_collected(self):
        # Same layout but a different type_guid -> should not be collected.
        buf = bytearray(self._build_one_metamesh_tpac("sk_test_mesh"))
        # Overwrite the type_guid (first 16 bytes after the 32-byte header).
        type_off = 4 + 4 + 16 + 4 + 4 + 4
        buf[type_off:type_off + 16] = b"\x99" * 16
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(bytes(buf))
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        res = vm.scan_tpac_metameshes(f.name)
        self.assertTrue(res.parsed_ok)
        self.assertNotIn("sk_test_mesh", res.metamesh_names)

    def test_drifted_udep_count_soft_fails(self):
        buf = bytearray(self._build_one_metamesh_tpac("sk_test_mesh"))
        # Corrupt the trailing udep_count to a wild value -> soft-fail.
        buf[-4:] = struct.pack("<i", 999999)
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(bytes(buf))
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        res = vm.scan_tpac_metameshes(f.name)
        self.assertFalse(res.parsed_ok)
        self.assertIn("udep_count", res.error)

    def test_non_tpac_magic_soft_fails(self):
        f = tempfile.NamedTemporaryFile(suffix=".tpac", delete=False)
        f.write(b"NOTAPACK" + b"\x00" * 40)
        f.close()
        self.addCleanup(lambda: os.unlink(f.name))
        res = vm.scan_tpac_metameshes(f.name)
        self.assertFalse(res.parsed_ok)
        self.assertIn("not a tpac", res.error)


# --------------------------------------------------------------------------- #
# Reporting / grouping                                                         #
# --------------------------------------------------------------------------- #
class ReportTests(unittest.TestCase):
    def test_culture_grouping_in_report(self):
        xml_g = '<Item id="g" mesh="sk_absent_g" />\n'
        xml_r = '<Item id="r" mesh="sk_absent_r" />\n'
        refs = (vm.extract_refs_from_text(xml_g, "gondor/head_armors.xml")
                + vm.extract_refs_from_text(xml_r, "rohan/head_armors.xml"))
        present = vm.PresentSet(metameshes=set(), tpac_paths=["fake.tpac"])
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        report = vm.format_report(issues, refs, present, rgl=None, scan_bodies=False)
        self.assertIn("by culture/folder", report)
        self.assertIn("gondor", report)
        self.assertIn("rohan", report)

    def test_clean_report_says_pass_and_warns_about_scope(self):
        xml = '<Item id="g" mesh="sk_present" shield_body_name="bo_present" />\n'
        refs = vm.extract_refs_from_text(xml, "gondor/head_armors.xml")
        present = vm.PresentSet(metameshes={"sk_present"}, tpac_paths=["fake.tpac"])
        # scan_bodies False so the present body isn't checked; no missing of any kind.
        issues = vm.classify(refs, present, rgl=None, scan_bodies=False)
        report = vm.format_report(issues, refs, present, rgl=None, scan_bodies=False)
        self.assertIn("PASS", report)
        # A clean run must NOT read as "the hang can't be a missing body" — #352
        # proved a clean result only ever meant "clean WITHIN --items scope".
        self.assertIn("SCANNED SCOPE", report)
        self.assertNotIn("WEAKENED", report)


# --------------------------------------------------------------------------- #
# Reverse audit: packaged meshes with no item (--unreferenced / --prefix)       #
# --------------------------------------------------------------------------- #
class ReverseAuditTests(unittest.TestCase):
    def _refs(self, *mesh_names):
        xml = "".join(f'<Item id="it{i}" mesh="{m}" />\n'
                      for i, m in enumerate(mesh_names))
        return vm.extract_refs_from_text(xml, "gondor/head_armors.xml")

    def test_packaged_mesh_with_no_item_is_reported(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(metameshes={"sk_gd_used", "sk_gd_orphan"},
                                tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs)
        self.assertEqual(audit.orphans, ["sk_gd_orphan"])
        self.assertEqual(audit.referenced, 1)

    def test_referenced_mesh_is_not_reported(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(metameshes={"sk_gd_used"}, tpac_paths=["fake.tpac"])
        self.assertEqual(vm.reverse_audit(present, refs).orphans, [])

    def test_slim_variant_of_referenced_mesh_is_suppressed(self):
        # The engine resolves the female body variant by appending `_slim`, so it
        # is never named in item XML. Reporting it would be a false positive —
        # 100 of them for the Gondor set alone.
        refs = self._refs("sk_gd_chest")
        present = vm.PresentSet(metameshes={"sk_gd_chest", "sk_gd_chest_slim"},
                                tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs)
        self.assertEqual(audit.orphans, [])
        self.assertEqual(audit.slim_suppressed, 1)

    def test_slim_variant_with_no_referenced_base_is_still_an_orphan(self):
        # Suppression is conditional on the base being referenced — a `_slim`
        # whose base nothing uses is genuinely unreferenced art.
        refs = self._refs("sk_gd_other")
        present = vm.PresentSet(metameshes={"sk_gd_lonely_slim", "sk_gd_other"},
                                tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs)
        self.assertEqual(audit.orphans, ["sk_gd_lonely_slim"])
        self.assertEqual(audit.slim_suppressed, 0)

    def test_prefix_filters_candidates_only_never_the_referenced_set(self):
        # sk_md_orphan is out of prefix scope; sk_gd_used is referenced by an item
        # in a DIFFERENT prefix family and must still count as referenced.
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(
            metameshes={"sk_gd_used", "sk_gd_orphan", "sk_md_orphan"},
            tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs, prefix="sk_gd_")
        self.assertEqual(audit.orphans, ["sk_gd_orphan"])
        self.assertEqual(audit.considered, 2)
        self.assertEqual(audit.prefix, "sk_gd_")

    def test_lod_entries_collapse_to_the_base_name(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(
            metameshes={"sk_gd_used", "sk_gd_used.lod1", "sk_gd_used.lod5"},
            tpac_paths=["fake.tpac"])
        self.assertEqual(vm.reverse_audit(present, refs).orphans, [])

    def test_case_mismatch_counts_as_referenced_not_as_a_false_orphan(self):
        # Conservative direction: TOC casing drift must never manufacture an orphan.
        refs = self._refs("SK_GD_Used")
        present = vm.PresentSet(metameshes={"sk_gd_used"}, tpac_paths=["fake.tpac"])
        self.assertEqual(vm.reverse_audit(present, refs).orphans, [])

    def test_issues_are_info_and_carry_the_mesh_name(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(metameshes={"sk_gd_used", "sk_gd_orphan"},
                                tpac_paths=["fake.tpac"])
        issues = vm.reverse_audit_issues(vm.reverse_audit(present, refs))
        self.assertEqual(_codes(issues), ["UNREFERENCED_MESH"])
        self.assertEqual(issues[0].severity, vm.Severity.INFO)
        self.assertEqual(issues[0].entry_id, "sk_gd_orphan")

    def test_report_block_renders_counts(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(
            metameshes={"sk_gd_used", "sk_gd_orphan", "sk_gd_used_slim"},
            tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs)
        report = vm.format_report(vm.reverse_audit_issues(audit), refs, present,
                                  rgl=None, scan_bodies=False, reverse=audit)
        self.assertIn("Reverse audit (--unreferenced)", report)
        self.assertIn("_slim variants suppressed : 1", report)
        self.assertIn("UNREFERENCED (no item)    : 1", report)

    def test_clean_reverse_report_points_at_reachability_not_data(self):
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(metameshes={"sk_gd_used"}, tpac_paths=["fake.tpac"])
        audit = vm.reverse_audit(present, refs)
        report = vm.format_report([], refs, present, rgl=None, scan_bodies=False,
                                  reverse=audit)
        self.assertIn("NOT a data gap", report)

    def test_omitting_reverse_leaves_the_report_unchanged(self):
        # The mode is additive: without it, no reverse text may appear.
        refs = self._refs("sk_gd_used")
        present = vm.PresentSet(metameshes={"sk_gd_used"}, tpac_paths=["fake.tpac"])
        report = vm.format_report([], refs, present, rgl=None, scan_bodies=False)
        self.assertNotIn("Reverse audit", report)


# --------------------------------------------------------------------------- #
# CLI: exit codes, --code filter, --warnings-as-errors                         #
# --------------------------------------------------------------------------- #
class CliTests(unittest.TestCase):
    """End-to-end CLI runs against a synthetic item tree + offline-only tiers
    (no game install): --no-tier-b --no-rgl-log isolates ref extraction so exit
    codes are deterministic."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.items = Path(self._tmp.name) / "LOTRLOME_items"
        (self.items / "gondor").mkdir(parents=True)

    def tearDown(self):
        self._tmp.cleanup()

    def _write(self, rel, text):
        p = self.items / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")

    def _run(self, *extra):
        cmd = [sys.executable, str(TOOL), "--items", str(self.items),
               "--no-tier-b", "--no-rgl-log", *extra]
        return subprocess.run(cmd, capture_output=True, text=True)

    def test_clean_tree_exit_0(self):
        # No present-set + no rgl => only prefab/info possible; no ERROR.
        self._write("gondor/head_armors.xml", '<Item id="x" mesh="m" />\n')
        r = self._run()
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)

    def test_bad_items_path_exit_2(self):
        cmd = [sys.executable, str(TOOL), "--items",
               str(Path(self._tmp.name) / "does_not_exist"),
               "--no-tier-b", "--no-rgl-log"]
        r = subprocess.run(cmd, capture_output=True, text=True)
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)

    def test_missing_mesh_via_explicit_rgl_log_exit_1(self):
        # Supply an rgl_log naming an item-referenced missing body -> ERROR -> exit 1.
        self._write("gondor/shoulder_armors.xml",
                    '<Item id="sh" shield_body_name="bo_dead" />\n')
        log = Path(self._tmp.name) / "rgl_log_errors_test.txt"
        log.write_text("CONTENT WARNING: get_object failed for body: bo_dead\n",
                       encoding="utf-8")
        cmd = [sys.executable, str(TOOL), "--items", str(self.items),
               "--no-tier-b", "--rgl-log", str(log)]
        r = subprocess.run(cmd, capture_output=True, text=True)
        self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
        self.assertIn("RUNTIME_MISSING_BODY", r.stdout)

    def test_code_filter_narrows_output(self):
        self._write("gondor/head_armors.xml",
                    '<Item id="x" mesh="m" prefab="p" />\n')
        r = self._run("--code", "PREFAB_REF")
        self.assertEqual(r.returncode, 0)
        self.assertIn("PREFAB_REF", r.stdout)
        # Only PREFAB_REF should appear in the issue listing summary.
        self.assertNotIn("MISSING_MESH", r.stdout)

    def test_prefix_without_unreferenced_warns(self):
        self._write("gondor/head_armors.xml", '<Item id="x" mesh="m" />\n')
        r = self._run("--prefix", "sk_gd_")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("no effect without --unreferenced", r.stderr)

    def test_unreferenced_without_tier_b_warns_and_still_exits_0(self):
        # --no-tier-b means no present-set, so the reverse audit cannot run; it
        # must degrade with a warning rather than crash.
        self._write("gondor/head_armors.xml", '<Item id="x" mesh="m" />\n')
        r = self._run("--unreferenced")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("--unreferenced needs the Tier B present-set", r.stderr)
        self.assertNotIn("Reverse audit (--unreferenced)", r.stdout)

    def test_warnings_as_errors_flips_exit(self):
        # An rgl_log with only a missing-material (WARNING) -> exit 0 normally,
        # exit 1 with --warnings-as-errors.
        self._write("gondor/head_armors.xml", '<Item id="x" mesh="sk_x" />\n')
        log = Path(self._tmp.name) / "rgl_log_errors_w.txt"
        log.write_text("CONTENT WARNING: Unable to find material for mesh sk_x\n",
                       encoding="utf-8")
        base = [sys.executable, str(TOOL), "--items", str(self.items),
                "--no-tier-b", "--rgl-log", str(log)]
        r0 = subprocess.run(base, capture_output=True, text=True)
        self.assertEqual(r0.returncode, 0, r0.stdout + r0.stderr)
        r1 = subprocess.run(base + ["--warnings-as-errors"],
                            capture_output=True, text=True)
        self.assertEqual(r1.returncode, 1, r1.stdout + r1.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
