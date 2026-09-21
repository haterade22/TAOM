#!/usr/bin/env python3
"""Unit tests for COLLISION_BODY_BORROWED (tools/validate_moduledata.py).

Run:  python -m unittest tools.tests.test_collision_body_borrowed
  or:  python tools/tests/test_collision_body_borrowed.py

The bug class (#633): a weapon mesh with no `bo_` twin of its own borrows another kit's body.
The borrowed name resolves, so every "does it resolve" gate reads clean, and the item now
depends on art that can be renamed or retired without it (#599). Three Rhun longbows shipped
that way on the elven bow's body.

The rule: a `body_name` an Armory tpac ships must be its own mesh's twin (`bo_<mesh>` or
`bo_cap_<mesh>`), or nobody's twin (a variant name), or a twin from the SAME kit (recolours and
variants share one geometry), or a cross-kit share listed in `_SHARED_BODY_BY_DESIGN`. Vanilla
bodies and shields are exempt; a body no pack ships belongs to MISSING_COLLISION_BODY.

Synthetic: no game install, no tpac files. The tpac TOC scan and the ref extraction are stubbed
so the ownership rule is tested on its own.
"""
import os
import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import validate_mesh_refs as vmr  # noqa: E402
import validate_moduledata as vm  # noqa: E402

GAME = Path("X:/Modules")
ARMORY_TPAC = str(GAME / "LOTRLOME_Armory" / "Assets" / "weapons_geo.tpac")
VANILLA_TPAC = str(GAME / "Native" / "AssetPackages" / "bodies_shared.tpac")


def ref(name, attr, kind, item_id, line=1, file="items.xml"):
    return vmr.MeshRef(name=name, attr=attr, kind=kind, file=file, line=line,
                       item_id=item_id, culture="rhun")


def item(item_id, mesh, body, file="items.xml"):
    return [ref(mesh, "mesh", "visual_mesh", item_id, file=file),
            ref(body, "body_name", "collision_body", item_id, file=file)]


class BorrowedBodyTests(unittest.TestCase):
    def run_check(self, meshes, bodies, refs, vanilla_bodies=()):
        armory = vmr.TpacScanResult(path=ARMORY_TPAC, metamesh_names=set(meshes),
                                    physicsshape_names=set(bodies), parsed_ok=True)
        vanilla = vmr.TpacScanResult(path=VANILLA_TPAC, physicsshape_names=set(vanilla_bodies),
                                     parsed_ok=True)
        by_path = {ARMORY_TPAC: armory, VANILLA_TPAC: vanilla}
        with mock.patch.object(vm, "_loaded_tpacs", return_value=[Path(ARMORY_TPAC), Path(VANILLA_TPAC)]), \
             mock.patch.object(vmr, "scan_tpac_metameshes", side_effect=lambda p: by_path[str(p)]), \
             mock.patch.object(vmr, "extract_refs", return_value=list(refs)):
            return vm.borrowed_body_issues(GAME, Path("does/not/exist"))

    def test_another_kits_twin_is_an_error(self):
        """The #633 shape: a Rhun bow on the elven bow's body."""
        issues = self.run_check(
            meshes=["sm_rh_drag_longbow_a", "wm_elven_bow_a03"],
            bodies=["bo_wm_elven_bow_a03"],
            refs=item("bow", "sm_rh_drag_longbow_a", "bo_wm_elven_bow_a03"))
        self.assertEqual([i.code for i in issues], [vm.BORROWED_BODY_CODE])
        self.assertEqual(issues[0].entry_id, "bow")
        self.assertIn("bo_sm_rh_drag_longbow_a", issues[0].message,
                      "the message must name the twin to author")

    def test_own_twin_is_clean(self):
        """The fix: the twin authored into the mesh's own FBX."""
        issues = self.run_check(
            meshes=["sm_rh_drag_longbow_a"], bodies=["bo_sm_rh_drag_longbow_a"],
            refs=item("bow", "sm_rh_drag_longbow_a", "bo_sm_rh_drag_longbow_a"))
        self.assertEqual(issues, [])

    def test_own_capsule_is_clean(self):
        """bo_cap_<mesh> is the mesh's own capsule (the Ithilien and Harad bows)."""
        issues = self.run_check(
            meshes=["wm_ithilien_bow"], bodies=["bo_cap_wm_ithilien_bow"],
            refs=item("bow", "wm_ithilien_bow", "bo_cap_wm_ithilien_bow"))
        self.assertEqual(issues, [])

    def test_a_variant_name_is_nobodys_twin(self):
        """bo_uruk_halberd_blade_a1 for sm_uruk_halberd_blade_a1: the artist's own naming, no
        shipped mesh is called uruk_halberd_blade_a1, so it is not a borrow."""
        issues = self.run_check(
            meshes=["sm_uruk_halberd_blade_a1"], bodies=["bo_uruk_halberd_blade_a1"],
            refs=item("blade", "sm_uruk_halberd_blade_a1", "bo_uruk_halberd_blade_a1"))
        self.assertEqual(issues, [])

    def test_sharing_within_a_kit_is_design(self):
        """The ruby Aranruth blade on the base blade's body: one geometry, two textures."""
        issues = self.run_check(
            meshes=["wm_aranruth_sword_blade", "wm_aranruth_sword_ruby_blade"],
            bodies=["bo_wm_aranruth_sword_blade"],
            refs=item("ruby", "wm_aranruth_sword_ruby_blade", "bo_wm_aranruth_sword_blade"))
        self.assertEqual(issues, [])

    def test_the_rhun_family_share_is_authorised(self):
        """Dragon and Khamul are re-textured Loke geometry (Mike, 2026-09-21)."""
        issues = self.run_check(
            meshes=["sm_rh_drag_sword_blade_a", "sm_rh_loke_sword_blade_a"],
            bodies=["bo_sm_rh_loke_sword_blade_a"],
            refs=item("blade", "sm_rh_drag_sword_blade_a", "bo_sm_rh_loke_sword_blade_a"))
        self.assertEqual(issues, [])

    def test_a_vanilla_body_is_never_a_borrow(self):
        """bo_shortbow_d and friends ship in Native, resident whatever TAOM does, and vanilla
        art is never a placeholder for ours."""
        issues = self.run_check(
            meshes=["sm_uruk_bow_a", "shortbow_d"], bodies=[],
            refs=item("bow", "sm_uruk_bow_a", "bo_shortbow_d"),
            vanilla_bodies=["bo_shortbow_d"])
        self.assertEqual(issues, [])

    def test_shields_share_capsules_by_convention(self):
        """A shield carries bo_cap_* in body_name and the full body in shield_body_name, and 60
        shipped rows borrow a sibling culture's."""
        issues = self.run_check(
            meshes=["sm_dg_khml_shield_heavy_a", "sm_rh_loke_shield_a"],
            bodies=["bo_cap_sm_rh_loke_shield_a", "bo_sm_rh_loke_shield_a"],
            refs=item("shield", "sm_dg_khml_shield_heavy_a", "bo_cap_sm_rh_loke_shield_a")
                 + [ref("bo_sm_rh_loke_shield_a", "shield_body_name", "collision_body", "shield")])
        self.assertEqual(issues, [])

    def test_a_body_no_pack_ships_is_left_to_missing_collision_body(self):
        """Two codes for one ref would double-report."""
        issues = self.run_check(
            meshes=["sm_rh_drag_longbow_a"], bodies=[],
            refs=item("bow", "sm_rh_drag_longbow_a", "bo_nonexistent"))
        self.assertEqual(issues, [])

    def test_crafting_pieces_are_paired_per_piece(self):
        """Before #633 every piece ref carried item_id "" and all bodies compared against one
        mesh. Two pieces in one file: the first borrows across kits, the second is clean."""
        issues = self.run_check(
            meshes=["sm_rh_drag_axe_blade_1h_a", "wm_gondor_axe_blade", "sm_kit_blade_b"],
            bodies=["bo_wm_gondor_axe_blade", "bo_sm_kit_blade_b"],
            refs=item("piece_a", "sm_rh_drag_axe_blade_1h_a", "bo_wm_gondor_axe_blade",
                      file="LOTRLOME_crafting_pieces.xml")
                 + item("piece_b", "sm_kit_blade_b", "bo_sm_kit_blade_b",
                        file="LOTRLOME_crafting_pieces.xml"))
        self.assertEqual([(i.code, i.entry_id) for i in issues],
                         [(vm.BORROWED_BODY_CODE, "piece_a")])

    def test_no_armory_packs_is_reported_not_silently_clean(self):
        """"every body missing" filtered to nothing reads exactly like a clean run."""
        with mock.patch.object(vm, "_loaded_tpacs", return_value=[Path(VANILLA_TPAC)]):
            issues = vm.borrowed_body_issues(GAME, Path("does/not/exist"))
        self.assertEqual([i.code for i in issues], [vm.BORROWED_BODY_CODE])
        self.assertIn("NOT verified", issues[0].message)

    def test_a_scan_that_raises_is_reported(self):
        with mock.patch.object(vm, "_loaded_tpacs", return_value=[Path(ARMORY_TPAC)]), \
             mock.patch.object(vmr, "build_present_set", side_effect=OSError("disk gone")):
            issues = vm.borrowed_body_issues(GAME, Path("does/not/exist"))
        self.assertEqual([i.code for i in issues], [vm.BORROWED_BODY_CODE])
        self.assertIn("NOT verified", issues[0].message)
        self.assertIn("disk gone", issues[0].message)


class KitTests(unittest.TestCase):
    def test_kit_strips_art_prefixes_and_keeps_two_tokens(self):
        self.assertEqual(vm._kit("sm_rh_drag_sword_blade_a"), "rh_drag")
        self.assertEqual(vm._kit("bo_wm_elven_bow_a03"), "elven_bow")
        self.assertEqual(vm._kit("bo_cap_wm_harad_bow_a01"), "harad_bow")
        self.assertEqual(vm._kit("wm_rivendell_sword_a01_silver_blade"), "rivendell_sword")

    def test_twin_owner_prefers_the_capsule_prefix(self):
        meshes = {"wm_harad_bow_a01", "cap_wm_harad_bow_a01"}
        self.assertEqual(vm._twin_owner("bo_cap_wm_harad_bow_a01", meshes), "wm_harad_bow_a01")
        self.assertIsNone(vm._twin_owner("bo_uruk_halberd_blade_a1", {"sm_uruk_halberd_blade_a1"}))


if __name__ == "__main__":
    unittest.main()
