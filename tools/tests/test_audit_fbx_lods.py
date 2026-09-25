#!/usr/bin/env python3
"""Unit tests for tools/audit_fbx_lods.py.

Run:  python -m unittest tools.tests.test_audit_fbx_lods

The reader is proven against a synthetic binary FBX built here byte by byte, so the test needs
neither Blender nor the game install. The naming cases are the shapes the 2026-09-25 census of
the live Armory actually found.
"""
import os
import struct
import sys
import tempfile
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import audit_fbx_lods as afl  # noqa: E402


# --------------------------------------------------------------------------- #
# a minimal FBX 7400 binary writer, enough for Objects + Connections
# --------------------------------------------------------------------------- #
def _prop(p):
    kind, value = p
    if kind == "L":
        return b"L" + struct.pack("<q", value)
    if kind == "S":
        return b"S" + struct.pack("<I", len(value)) + value
    if kind in ("i", "d"):
        fmt = "i" if kind == "i" else "d"
        raw = struct.pack("<%d%s" % (len(value), fmt), *value)
        return kind.encode() + struct.pack("<III", len(value), 0, len(raw)) + raw
    raise ValueError(kind)


def _node(name, props, children, offset):
    pbytes = b"".join(_prop(p) for p in props)
    head_len = 13 + len(name)
    body = b""
    pos = offset + head_len + len(pbytes)
    for child in children:
        chunk = _node(*child, pos)
        body += chunk
        pos += len(chunk)
    if children:
        body += b"\x00" * 13
    end = offset + head_len + len(pbytes) + len(body)
    return (struct.pack("<IIIB", end, len(props), len(pbytes), len(name)) + name.encode()
            + pbytes + body)


def build_fbx(top_nodes):
    data = b"Kaydara FBX Binary  \x00\x1a\x00" + struct.pack("<I", 7400)
    for n in top_nodes:
        data += _node(*n, len(data))
    return data + b"\x00" * 13 + b"\x00" * 160


def cls(name, klass):
    return ("S", name.encode() + b"\x00\x01" + klass.encode())


def sample_fbx():
    """One mesh `Hair_A_lod0`: a quad plus a triangle (3 tris, 5 vertices), one material and two
    blend-shape channels; and a LOD `Hair_A_lod1` with one triangle and no morphs."""
    objects = [
        ("Geometry", [("L", 10), cls("g0", "Geometry"), ("S", b"Mesh")], [
            ("Vertices", [("d", [0.0] * 15)], []),
            ("PolygonVertexIndex", [("i", [0, 1, 2, -4, 2, 3, -5])], []),
        ]),
        ("Model", [("L", 11), cls("Hair_A_lod0", "Model"), ("S", b"Mesh")], []),
        ("Material", [("L", 12), cls("m_hair", "Material"), ("S", b"")], []),
        ("Deformer", [("L", 13), cls("bs", "Deformer"), ("S", b"BlendShape")], []),
        ("Deformer", [("L", 14), cls("Basis_0", "SubDeformer"), ("S", b"BlendShapeChannel")], []),
        ("Deformer", [("L", 15), cls("FaceWidth_1", "SubDeformer"), ("S", b"BlendShapeChannel")], []),
        ("Geometry", [("L", 16), cls("shape_00", "Geometry"), ("S", b"Shape")], []),
        ("Geometry", [("L", 17), cls("shape_01", "Geometry"), ("S", b"Shape")], []),
        ("Geometry", [("L", 20), cls("g1", "Geometry"), ("S", b"Mesh")], [
            ("Vertices", [("d", [0.0] * 9)], []),
            ("PolygonVertexIndex", [("i", [0, 1, -3])], []),
        ]),
        ("Model", [("L", 21), cls("Hair_A_lod1", "Model"), ("S", b"Mesh")], []),
        ("Model", [("L", 30), cls("head", "Model"), ("S", b"LimbNode")], []),
    ]
    conns = [(10, 11), (12, 11), (13, 10), (14, 13), (15, 13), (16, 14), (17, 15), (20, 21),
             (12, 21)]
    connections = [("C", [("S", b"OO"), ("L", c), ("L", p)], []) for c, p in conns]
    return build_fbx([("Objects", [], objects), ("Connections", [], connections)])


class ReaderTests(unittest.TestCase):
    def setUp(self):
        fd, self.path = tempfile.mkstemp(suffix=".fbx")
        with os.fdopen(fd, "wb") as f:
            f.write(sample_fbx())

    def tearDown(self):
        os.remove(self.path)

    def test_reads_mesh_models_only(self):
        meshes = afl.read_fbx_meshes(self.path)
        self.assertEqual([m.name for m in meshes], ["Hair_A_lod0", "Hair_A_lod1"])

    def test_triangle_and_vertex_counts(self):
        lod0, lod1 = afl.read_fbx_meshes(self.path)
        self.assertEqual((lod0.tris, lod0.vertices), (3, 5))
        self.assertEqual((lod1.tris, lod1.vertices), (1, 3))

    def test_morph_channels_in_order_and_materials(self):
        lod0, lod1 = afl.read_fbx_meshes(self.path)
        self.assertEqual(lod0.channels, ["Basis_0", "FaceWidth_1"])
        self.assertEqual(lod0.shapes, ["shape_00", "shape_01"])
        self.assertEqual(lod1.channels, [])
        self.assertEqual(lod0.materials, ["m_hair"])

    def test_ascii_fbx_is_refused_loudly(self):
        with open(self.path, "wb") as f:
            f.write(b"; FBX 7.4.0 project file\n")
        with self.assertRaises(ValueError):
            afl.read_fbx_meshes(self.path)


class SplitLodTests(unittest.TestCase):
    def check(self, name, chain, level, defect=None):
        self.assertEqual(afl.split_lod(name), (chain, level, defect), name)

    def test_bare_name_is_lod0(self):
        self.check("orc_a_head", "orc_a_head", 0)

    def test_dot_and_underscore_schemes(self):
        self.check("orc_a_head.lod3", "orc_a_head", 3)
        self.check("Dwarf_Hair_A_lod0", "Dwarf_Hair_A", 0)
        self.check("Dwarf_Hair_A_lod3", "Dwarf_Hair_A", 3)

    def test_part_suffix_survives(self):
        self.check("SK_Elf_Basemesh_A1_Head.eyes.lod2", "SK_Elf_Basemesh_A1_Head.eyes", 2)
        self.check("sk_dwarf_bm_f1_head.eye_lod1", "sk_dwarf_bm_f1_head.eye", 1)
        self.check("SK_Elephant_Armor_A.base", "SK_Elephant_Armor_A.base", 0)

    def test_defects(self):
        self.check("SK_Dwarf_Iron_Gloves_A.lod", "SK_Dwarf_Iron_Gloves_A.lod", None,
                   afl.DEFECT_NO_NUMBER)
        self.check("roh_nbl_glv.lod4.001", "roh_nbl_glv.lod4.001", None, afl.DEFECT_DUPLICATE)
        self.check("wm_rohan_shield_a01_ggg.od2", "wm_rohan_shield_a01_ggg.od2", None,
                   afl.DEFECT_TYPO)
        self.check("numenorean_sword_guard_b.001", "numenorean_sword_guard_b.001", None,
                   afl.DEFECT_DUPLICATE)


class NamingHelperTests(unittest.TestCase):
    def test_metamesh_is_lowercased_name_before_the_first_part(self):
        self.assertEqual(afl.metamesh_of("SK_Pale_Uruk_BM_A_Head.eyes"), "sk_pale_uruk_bm_a_head")
        self.assertEqual(afl.metamesh_of("Dwarf_Hair_A"), "dwarf_hair_a")

    def test_cloth_shapes(self):
        for name in ("clo_SK_GB_Uruk_Cape", "CLO_Uruk_Mordor_Helmet_C", "rivendell_body_clo",
                     "roh_nbl_hlm_clo"):
            self.assertTrue(afl.is_cloth(name), name)
        for name in ("SK_Cloak_A", "closed_helm", "sk_dale_boots_archer_a01"):
            self.assertFalse(afl.is_cloth(name), name)

    def test_tpac_for_fbx(self):
        self.assertEqual(afl.tpac_for("Race Test/Hair/Dwarf_Hairs.fbx"),
                         "Race Test/Hair/Dwarf_Hairs_geo.tpac")
        self.assertEqual(afl.tpac_for("Race Test\\Hair\\Dwarf_Hairs.FBX"),
                         "Race Test/Hair/Dwarf_Hairs_geo.tpac")


def mesh(name, tris=100, channels=(), materials=("m",)):
    return afl.FbxMesh(name, tris, tris, list(channels), list(materials))


class ClassifyTests(unittest.TestCase):
    CATALOGUE = {
        "sk_boots_a": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "Y"},
        "sk_boots_b": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "N"},
        "clo_cape_a": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "Y"},
        "dwarf_hair_a": {"tpacs": {"Race Test/Hair/Dwarf_Hairs_geo.tpac"}, "referenced": "N"},
        "sk_head": {"tpacs": {"Race Test/uruk_geo.tpac"}, "referenced": "N"},
        "sk_saddle": {"tpacs": {"creature/saddle_geo.tpac"}, "referenced": "Y"},
    }

    def run_classify(self, files, skins=()):
        chains = []
        for rel, meshes in files:
            chains.extend(afl.build_chains(rel, meshes))
        return afl.classify(chains, self.CATALOGUE, set(skins))

    def names(self, section):
        return sorted(c.chain for c in section)

    def test_sections(self):
        s = self.run_classify([
            ("dale/boots.fbx", [mesh("sk_boots_a"), mesh("sk_boots_b"), mesh("clo_cape_a"),
                                mesh("sk_boots_c"), mesh("sk_boots_d"), mesh("sk_boots_d.lod1")]),
            ("Race Test/uruk.fbx", [mesh("SK_Head"), mesh("SK_Head.eyes")]),
        ], skins={"sk_head"})
        self.assertEqual(self.names(s["fix"]), ["SK_Head", "SK_Head.eyes", "sk_boots_a"])
        self.assertEqual(self.names(s["unreferenced"]), ["sk_boots_b"])
        # in no tpac at all, so not shipped from this FBX: excluded however many LODs it has
        self.assertEqual(self.names(s["excluded"]), ["sk_boots_c", "sk_boots_d"])
        # cloth needs no LODs (Mike, 2026-09-25): it is in no section at all
        for rows in s.values():
            self.assertNotIn("clo_cape_a", self.names(rows))

    def test_every_mesh_needs_lod0_through_lod5(self):
        """The rule (Mike, 2026-09-25): LOD0 to LOD5, no gaps. A chain that stops early or skips a
        level is incomplete; one with all six (or more) is not listed."""
        s = self.run_classify([("dale/boots.fbx", [
            mesh("sk_boots_a"), mesh("sk_boots_a.lod1"), mesh("sk_boots_a.lod2"),
            mesh("sk_boots_b"), mesh("sk_boots_b.lod2"), mesh("sk_boots_b.lod4"), mesh("sk_boots_b.lod5"),
            mesh("sk_saddle")] + [mesh("sk_saddle.lod%d" % n) for n in range(1, 7)])],
            skins={"sk_boots_b"})
        self.assertEqual(self.names(s["incomplete"]), ["sk_boots_a", "sk_boots_b"])
        self.assertEqual([sorted(c.missing) for c in s["incomplete"]], [[3, 4, 5], [1, 3]])

    def test_an_unused_incomplete_chain_is_informational(self):
        s = self.run_classify([("Race Test/Hair/Dwarf_Hairs.fbx", [
            mesh("Dwarf_Hair_A_lod0", 18278), mesh("Dwarf_Hair_A_lod1"), mesh("Dwarf_Hair_A_lod2")])])
        self.assertEqual(s["incomplete"], [])
        self.assertEqual(self.names(s["unreferenced"]), ["Dwarf_Hair_A"])

    def test_lod_number_past_nine_is_a_defect(self):
        """`SK_RH_Loke_*.lod44`, `SM_Dwarf_Erebor_*.lod52`: typos, not LOD44."""
        self.assertEqual(afl.split_lod("wm_axe_blade.lod52"), ("wm_axe_blade.lod52", None,
                                                              afl.DEFECT_RANGE))

    def test_duplicate_fbx_is_excluded_not_counted_twice(self):
        """elephants/ and creature/elephant/ hold the same meshes; only the FBX whose tpac ships
        a mesh reports it."""
        s = self.run_classify([
            ("creature/saddle.fbx", [mesh("sk_saddle")]),
            ("old/saddle.fbx", [mesh("sk_saddle")]),
        ])
        self.assertEqual([c.fbx for c in s["fix"]], ["creature/saddle.fbx"])
        self.assertEqual([c.fbx for c in s["excluded"]], ["old/saddle.fbx"])

    def test_defects_are_their_own_section(self):
        s = self.run_classify([("dale/boots.fbx", [
            mesh("sk_boots_a"), mesh("sk_boots_a.lod1.001"), mesh("sk_boots_b.od2")])])
        self.assertEqual(self.names(s["defects"]), ["sk_boots_a.lod1.001", "sk_boots_b.od2"])

    def test_defect_in_a_copy_that_ships_nothing_is_excluded(self):
        """The ten elephant FBX copies all carry `.pillow.001`; only the one whose tpac ships
        the mesh should report it."""
        s = self.run_classify([
            ("creature/saddle.fbx", [mesh("sk_saddle"), mesh("sk_saddle.pillow.001")]),
            ("old/saddle.fbx", [mesh("sk_saddle.pillow.001")]),
        ])
        self.assertEqual([c.fbx for c in s["defects"]], ["creature/saddle.fbx"])
        self.assertIn("old/saddle.fbx", [c.fbx for c in s["excluded"]])

    def test_numbered_parts_with_falling_tris_read_as_a_lod_chain(self):
        """SK_GB_Uruk_Cape_Pauldron_Elite_A shipped `.base` 412 tris, `.base1` 257 ... `.base5`
        14: six sub-meshes the Kit draws at once, not one mesh with LODs."""
        s = self.run_classify([("dale/boots.fbx", [
            mesh("sk_boots_a.base", 412), mesh("sk_boots_a.base1", 257),
            mesh("sk_boots_a.base2", 106)])])
        self.assertEqual(self.names(s["defects"]), ["sk_boots_a.base1", "sk_boots_a.base2"])
        self.assertEqual(s["defects"][0].defect, afl.DEFECT_NUMBERED)
        self.assertEqual(self.names(s["fix"]), ["sk_boots_a.base"])

    def test_numbered_parts_that_do_not_shrink_are_left_alone(self):
        s = self.run_classify([("dale/boots.fbx", [
            mesh("sk_boots_a.gem", 10), mesh("sk_boots_a.gem1", 50)])])
        self.assertEqual(s["defects"], [])
        self.assertEqual(self.names(s["fix"]), ["sk_boots_a.gem", "sk_boots_a.gem1"])

    def test_case_differences_share_one_chain(self):
        chains = afl.build_chains("x.fbx", [mesh("SK_Head.Eyes"), mesh("SK_Head.eyes.lod1")])
        self.assertEqual(len(chains), 1)
        self.assertEqual(chains[0].levels, {0, 1})


class CatalogueTests(unittest.TestCase):
    def test_rows_fold_to_metameshes_and_keep_the_strongest_reference(self):
        rows = [
            {"mesh": "SK_Head", "kind": "metamesh", "tpac": "a_geo.tpac", "referenced": "N"},
            {"mesh": "sk_head", "kind": "metamesh", "tpac": "b_geo.tpac", "referenced": "Y"},
            {"mesh": "bo_sk_head", "kind": "physicsshape", "tpac": "a_geo.tpac", "referenced": "Y"},
        ]
        cat = afl.catalogue_from_rows(rows)
        self.assertEqual(set(cat), {"sk_head"})
        self.assertEqual(cat["sk_head"]["tpacs"], {"a_geo.tpac", "b_geo.tpac"})
        self.assertEqual(cat["sk_head"]["referenced"], "Y")


class SkinsRefsTests(unittest.TestCase):
    def test_collects_skin_attributes_and_hair_names(self):
        xml = b"""<?xml version="1.0" encoding="utf-8"?>
<skins><race id="goblin"><skin name="man" skeleton="human_skeleton"
  body_meta_mesh="SK_Goblin_Body" face_meta_mesh="sk_goblin_head" hands_mesh="sk_goblin_hands"
  body_meta_mesh_shoulders="sk_goblin_shoulder" underwear_bottom_mesh="">
  <hair_meshes group_id="7"><hair_mesh /><hair_mesh name="dwarf_hair_a" cover_type1="dwarf_hair_a_c" />
  </hair_meshes><beard_meshes><beard_mesh name="sk_dwarf_beard_a_01" /></beard_meshes>
</skin></race></skins>"""
        fd, path = tempfile.mkstemp(suffix=".xml")
        with os.fdopen(fd, "wb") as f:
            f.write(xml)
        try:
            refs = afl.skins_mesh_refs(path)
        finally:
            os.remove(path)
        self.assertEqual(refs, {"sk_goblin_body", "sk_goblin_head", "sk_goblin_hands",
                                "sk_goblin_shoulder", "dwarf_hair_a", "dwarf_hair_a_c",
                                "sk_dwarf_beard_a_01"})


def rig_fbx(child_offset, frame):
    """Two bones with clusters. `frame` rotates the whole file (a global axis change, as Blender's
    export makes); `child_offset` moves the child bone relative to the root."""
    import math
    def mat(t, rot_deg):
        c, s = math.cos(math.radians(rot_deg)), math.sin(math.radians(rot_deg))
        # column-major 4x4: rotation about Z, then translation
        return [c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, t[0], t[1], t[2], 1]
    def rotate(p):
        c, s = math.cos(math.radians(frame)), math.sin(math.radians(frame))
        return (c * p[0] - s * p[1], s * p[0] + c * p[1], p[2])
    objects = [
        ("Model", [("L", 1), cls("root", "Model"), ("S", b"LimbNode")], []),
        ("Model", [("L", 2), cls("child", "Model"), ("S", b"LimbNode")], []),
        ("Deformer", [("L", 3), cls("", "SubDeformer"), ("S", b"Cluster")],
         [("TransformLink", [("d", mat(rotate((0, 0, 0)), frame))], [])]),
        ("Deformer", [("L", 4), cls("", "SubDeformer"), ("S", b"Cluster")],
         [("TransformLink", [("d", mat(rotate(child_offset), frame))], [])]),
    ]
    connections = [("C", [("S", b"OO"), ("L", c), ("L", p)], []) for c, p in ((1, 3), (2, 4))]
    return build_fbx([("Objects", [], objects), ("Connections", [], connections)])


class BindPoseTests(unittest.TestCase):
    def drift(self, a_bytes, b_bytes):
        paths = []
        for data in (a_bytes, b_bytes):
            fd, p = tempfile.mkstemp(suffix=".fbx")
            with os.fdopen(fd, "wb") as f:
                f.write(data)
            paths.append(p)
        try:
            return afl.bind_pose_drift(*paths)
        finally:
            for p in paths:
                os.remove(p)

    def test_a_global_frame_change_is_not_drift(self):
        bones, axis, offset, extent = self.drift(rig_fbx((0, 1, 0), 0), rig_fbx((0, 1, 0), 90))
        self.assertEqual(bones, 2)
        self.assertLess(axis, 1e-9)
        self.assertLess(offset, 1e-9)
        self.assertAlmostEqual(extent, 1.0, places=6)   # the child sits 1 unit from the root

    def test_a_moved_bone_is_drift(self):
        bones, axis, offset, extent = self.drift(rig_fbx((0, 1, 0), 0), rig_fbx((0, 1.5, 0), 90))
        self.assertAlmostEqual(offset, 0.5, places=6)


class DriftTests(unittest.TestCase):
    def test_same_text_is_no_drift_whatever_the_line_endings(self):
        self.assertFalse(afl.drifted("a\nb\n", "a\r\nb\r\n"))

    def test_changed_text_is_drift(self):
        self.assertTrue(afl.drifted("a\nb\n", "a\nc\n"))

    def test_missing_report_is_drift(self):
        self.assertTrue(afl.drifted(None, "a\n"))


class DiffTests(unittest.TestCase):
    def test_reports_added_removed_and_changed(self):
        a = [mesh("head", 100, ["FaceWidth_1"]), mesh("gone")]
        b = [mesh("head", 100, ["Basis_0", "FaceWidth_1"]), mesh("head.lod1", 70)]
        lines = afl.diff_meshes(a, b)
        text = "\n".join(lines)
        self.assertIn("+ head.lod1", text)
        self.assertIn("- gone", text)
        self.assertIn("~ head: channels 1 -> 2", text)

    def test_relabelled_channels_over_the_same_shapes_say_so(self):
        """The hair FBX names every channel `Mesh`; Blender's export names each after its shape.
        The shapes, which carry the order, are unchanged."""
        a = [afl.FbxMesh("hair", 10, 10, ["Mesh", "Mesh"], ["m"], ["shape_01", "shape_02"])]
        b = [afl.FbxMesh("hair", 10, 10, ["shape_01", "shape_02"], ["m"], ["shape_01", "shape_02"])]
        self.assertEqual(afl.diff_meshes(a, b),
                         ["~ hair: channel labels renamed, shape geometry and order unchanged"])

    def test_identical_is_empty(self):
        self.assertEqual(afl.diff_meshes([mesh("a")], [mesh("a")]), [])


if __name__ == "__main__":
    unittest.main()
