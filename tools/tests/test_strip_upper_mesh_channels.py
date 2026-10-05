"""Tests for tools/blender/strip_upper_mesh_channels.py: which objects lose their channels, what is refused, and
main() on a stub bpy (the stub test_add_mesh_lods.py installs, given just enough scene for one run)."""
import importlib.util
import json
import os
import shutil
import sys
import tempfile
import types
import unittest
from unittest import mock

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import strip_upper_mesh_channels as sm  # noqa: E402

ADD_MESH_LODS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender", "add_mesh_lods.py")
NAMES = ["SK_saruman_hair", "SK_saruman_hair.lod1", "SK_saruman_hair.lod5", "SK_saruman_beard",
         "SK_saruman_beard.lod2", "sk_saruman_head.base", "SK_saruman_head.eye", "sk_saruman_head.mouth",
         "SK_saruman_hairpin", "SK_hands_male_a", "SK_full_body_saruman", "sk_dwarf_bm_f1_arms",
         "sk_dwarf_bm_f1_eyebrow_01"]


class TargetTests(unittest.TestCase):
    def test_a_mesh_takes_its_lods_and_nothing_else(self):
        self.assertEqual(sm.targets(NAMES, ["SK_saruman_hair"]),
                         ["SK_saruman_hair", "SK_saruman_hair.lod1", "SK_saruman_hair.lod5"])

    def test_several_meshes_combine(self):
        self.assertEqual(len(sm.targets(NAMES, ["SK_saruman_hair", "SK_saruman_beard"])), 5)

    def test_a_face_part_is_refused(self):
        """Saruman's own head, eye and mouth. Every real eye and mouth name also carries `head`, so the lone tokens
        prove `eye` and `mouth` are checked themselves; the message proves the face-part guard refused each one."""
        for name in ("sk_saruman_head.base", "SK_saruman_head.eye", "sk_saruman_head.mouth", "x.eye", "x.mouth"):
            with self.subTest(name=name), self.assertRaisesRegex(SystemExit, "face part"):
                sm.targets(NAMES + [name], [name])

    def test_hands_arms_and_bodies_are_refused(self):
        """They carry the hand-pose channels (docs/reference/race-face-and-hand-morphs.md); the first two sit in
        Saruman's own FBX, and check_race_morph_channels.py requires the dwarf's arms to keep 26."""
        for name in ("SK_hands_male_a", "SK_full_body_saruman", "sk_dwarf_bm_f1_arms"):
            with self.subTest(name=name), self.assertRaisesRegex(SystemExit, "not a hair"):
                sm.targets(NAMES, [name])

    def test_an_eyebrow_is_accepted(self):
        self.assertEqual(sm.targets(NAMES, ["sk_dwarf_bm_f1_eyebrow_01"]), ["sk_dwarf_bm_f1_eyebrow_01"])

    def test_an_unknown_mesh_is_refused(self):
        with self.assertRaisesRegex(SystemExit, "no mesh"):
            sm.targets(NAMES, ["SK_saruman_moustache"])

    def test_arguments_need_an_fbx_and_a_mesh(self):
        for argv in (["--fbx", "a.fbx"], ["--mesh", "m"]):
            with self.subTest(argv=argv), self.assertRaisesRegex(SystemExit, "required"):
                sm.parse_args(["blender", "--"] + argv)
        args = sm.parse_args(["blender", "--", "--fbx", "a.fbx", "--mesh", "m", "--apply"])
        self.assertEqual((args["fbx"], args["mesh"], args["apply"]), ("a.fbx", ["m"], True))


class _Mesh:
    """Just enough of a Blender mesh object for main() and add_mesh_lods.fingerprint()."""
    type = "MESH"
    dimensions = location = (1.0, 1.0, 1.0)
    material_slots = ()
    parent = None

    def __init__(self, name, channels=0):
        self.name = name
        keys = [types.SimpleNamespace(name=k) for k in ["Basis"] + ["shape_%03d" % i for i in range(channels)]]
        self.data = types.SimpleNamespace(
            vertices=[0, 1, 2], polygons=[types.SimpleNamespace(vertices=(0, 1, 2))], uv_layers=[0],
            shape_keys=types.SimpleNamespace(key_blocks=keys) if channels else None)

    def shape_key_clear(self):
        self.data.shape_keys = None


class _Objects(list):
    """bpy.data.objects: iterates the objects and indexes them by name."""

    def __getitem__(self, key):
        return next(o for o in self if o.name == key) if isinstance(key, str) else list.__getitem__(self, key)


class MainTests(unittest.TestCase):
    """main() end to end with --apply. Loading and re-importing leave the scene as it is (a lossless round trip);
    the export writes a stand-in file, so a replaced FBX shows in its bytes."""

    def run_main(self, objects, meshes):
        tmp = tempfile.mkdtemp()
        self.addCleanup(shutil.rmtree, tmp)
        fbx = os.path.join(tmp, "saruman.fbx")
        with open(fbx, "wb") as fh:
            fh.write(b"original")
        exports = []

        def export_fbx(filepath, **_):
            exports.append(filepath)
            with open(filepath, "wb") as fh:
                fh.write(b"re-exported")

        def noop(**_):
            return None

        bpy = types.ModuleType("bpy")
        bpy.data = types.SimpleNamespace(objects=_Objects(objects))
        bpy.ops = types.SimpleNamespace(
            wm=types.SimpleNamespace(read_factory_settings=noop), import_scene=types.SimpleNamespace(fbx=noop),
            object=types.SimpleNamespace(select_all=noop), export_scene=types.SimpleNamespace(fbx=export_fbx))
        argv = ["blender", "--", "--fbx", fbx, "--apply"] + [a for m in meshes for a in ("--mesh", m)]
        with mock.patch.dict(sys.modules, {"bpy": bpy}), mock.patch.object(sys, "argv", argv), \
                mock.patch.object(sys, "path", list(sys.path)):
            spec = importlib.util.spec_from_file_location("add_mesh_lods", ADD_MESH_LODS)
            aml = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(aml)          # binds the stub as its bpy
            sys.modules["add_mesh_lods"] = aml    # the module main() imports
            sm.main()
        with open(fbx + ".stripupper-report.json", encoding="utf-8") as fh:
            report = json.load(fh)
        with open(fbx, "rb") as fh:
            data = fh.read()
        return report, data, fbx, exports

    def test_a_run_with_nothing_to_strip_is_refused_before_any_export(self):
        """A re-run on a stripped file, or only a LOD named (Saruman's LODs 1 to 5 carry no channels)."""
        cases = ((["SK_saruman_hair"], [_Mesh("SK_saruman_hair"), _Mesh("SK_saruman_hair.lod1"),
                                        _Mesh("sk_saruman_head.base", 101)]),
                 (["SK_saruman_hair.lod1"], [_Mesh("SK_saruman_hair", 101), _Mesh("SK_saruman_hair.lod1")]))
        for meshes, objects in cases:
            with self.subTest(meshes=meshes):
                report, data, fbx, exports = self.run_main(objects, meshes)
                self.assertIn("nothing to strip", report.get("error", ""), report.get("traceback"))
                self.assertFalse(report["applied"])
                self.assertEqual(report["stage"], "done")
                self.assertEqual(exports, [])
                self.assertEqual(data, b"original")
                self.assertFalse(os.path.exists(fbx + ".bak-stripupper"))

    def test_channels_on_lod0_alone_are_stripped(self):
        """Saruman's shape: LOD0 carries the channels and its LODs none, which is work to do, not a refusal."""
        head = _Mesh("sk_saruman_head.base", 101)
        report, data, fbx, exports = self.run_main(
            [_Mesh("SK_saruman_hair", 101), _Mesh("SK_saruman_hair.lod1"), head], ["SK_saruman_hair"])
        self.assertNotIn("error", report, report.get("traceback"))
        self.assertEqual(report["removed_channels"], {"SK_saruman_hair": 101, "SK_saruman_hair.lod1": 0})
        self.assertTrue(report["applied"])
        self.assertEqual(len(exports), 1)
        self.assertEqual(data, b"re-exported")
        with open(fbx + ".bak-stripupper", "rb") as fh:
            self.assertEqual(fh.read(), b"original")
        self.assertEqual(len(head.data.shape_keys.key_blocks), 102)


if __name__ == "__main__":
    unittest.main()
