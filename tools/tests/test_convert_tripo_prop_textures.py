#!/usr/bin/env python3
"""Unit tests for tools/oneoff/convert_tripo_prop_textures.py.

Run:  python -m unittest tools.tests.test_convert_tripo_prop_textures

Synthetic PNG sets in a temporary folder: which file takes which role (a Painter glTF export packs
AO, roughness and metallic into one `occlusionRoughnessMetallic` map), the `_d`/`_s` packing, and
the guards that keep a run from converting the wrong set or overwriting what is already there.
"""
import contextlib
import importlib.util
import io
import os
import tempfile
import unittest
from pathlib import Path

try:
    import numpy as np
    from PIL import Image
except ImportError:  # the converter needs both; skip, as the sibling image tests do
    raise unittest.SkipTest("numpy or Pillow is not installed")

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "oneoff", "convert_tripo_prop_textures.py")
_spec = importlib.util.spec_from_file_location("convert_tripo_prop_textures", SCRIPT)
conv = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(conv)


def png(path, rgb, size=8):
    Image.fromarray(np.full((size, size, 3), rgb, dtype=np.uint8)).save(path)


def gltf_set(folder, name, base=(200, 100, 50), orm=(51, 153, 255)):
    """One Painter glTF set: baseColor, normal, occlusionRoughnessMetallic (R AO, G rough, B metal)."""
    png(folder / f"{name}_baseColor.png", base)
    png(folder / f"{name}_normal.png", (128, 128, 255))
    png(folder / f"{name}_occlusionRoughnessMetallic.png", orm)


def run(*argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = conv.main([str(a) for a in argv])
    return code, out.getvalue()


class FindMapsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.src = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_orm_map_never_claims_a_single_role(self):
        gltf_set(self.src, "set")
        maps = conv.find_maps(self.src)
        self.assertEqual(sorted(maps), ["base", "normal", "orm"])
        self.assertEqual(maps["orm"].name, "set_occlusionRoughnessMetallic.png")

    def test_normal_map_is_not_read_as_orm(self):
        png(self.src / "x_normal.png", (128, 128, 255))
        self.assertNotIn("orm", conv.find_maps(self.src))

    def test_match_with_trailing_underscore_separates_prefix_sets(self):
        gltf_set(self.src, "carn_dum_town")
        gltf_set(self.src, "carn_dum_towers")
        maps = conv.find_maps(self.src, "carn_dum_town_")
        self.assertEqual(maps["base"].name, "carn_dum_town_baseColor.png")


class ConvertTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.src = Path(self.tmp.name) / "src"
        self.dst = Path(self.tmp.name) / "dst"
        self.src.mkdir()

    def tearDown(self):
        self.tmp.cleanup()

    def read(self, name):
        return np.asarray(Image.open(self.dst / name).convert("RGB")).astype(int)

    def test_s_packs_metal_gloss_ao_from_the_orm_map(self):
        gltf_set(self.src, "set", base=(200, 100, 50), orm=(51, 153, 0))
        code, _ = run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(code, 0)
        s = self.read("t_x_s.png")[0, 0]
        self.assertEqual(list(s), [0, 255 - 153, 51])  # R metal, G 1 - rough, B AO
        self.assertEqual(list(self.read("t_x_d.png")[0, 0]), [200, 100, 50])  # metal 0 keeps albedo

    def test_full_metal_zeroes_the_diffuse(self):
        gltf_set(self.src, "set", base=(200, 100, 50), orm=(255, 128, 255))
        run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(list(self.read("t_x_d.png")[0, 0]), [0, 0, 0])

    def test_separate_maps_win_over_the_orm_map(self):
        gltf_set(self.src, "set", orm=(51, 153, 0))
        png(self.src / "set_roughness.png", (10, 10, 10))
        run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(int(self.read("t_x_s.png")[0, 0, 1]), 255 - 10)

    def test_several_sets_without_match_refuse(self):
        gltf_set(self.src, "a")
        gltf_set(self.src, "b")
        code, out = run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(code, 1)
        self.assertIn("--match", out)
        self.assertFalse(self.dst.exists() and any(self.dst.iterdir()))

    def test_src_and_dst_are_required(self):
        for argv in ((), ("--src", self.src), ("--dst", self.dst)):
            with self.assertRaises(SystemExit) as cm, contextlib.redirect_stderr(io.StringIO()):
                run(*argv)
            self.assertEqual(cm.exception.code, 2)

    def test_each_set_converts_without_stem(self):
        gltf_set(self.src, "a")
        gltf_set(self.src, "b")
        code, _ = run("--src", self.src, "--dst", self.dst)
        self.assertEqual(code, 0)
        self.assertEqual(sorted(p.name for p in self.dst.iterdir()),
                         [f"t_{s}_{k}.png" for s in "ab" for k in "dns"])

    def test_prefix_sets_do_not_leak(self):
        gltf_set(self.src, "carn_dum_town", base=(200, 0, 0), orm=(255, 128, 0))
        gltf_set(self.src, "carn_dum_towers", base=(0, 0, 200), orm=(255, 128, 0))
        code, _ = run("--src", self.src, "--dst", self.dst)
        self.assertEqual(code, 0)
        self.assertEqual(list(self.read("t_carn_dum_town_d.png")[0, 0]), [200, 0, 0])
        self.assertEqual(list(self.read("t_carn_dum_towers_d.png")[0, 0]), [0, 0, 200])

    def test_bare_role_names_need_stem(self):
        png(self.src / "basecolor.png", (200, 100, 50))
        code, out = run("--src", self.src, "--dst", self.dst)
        self.assertEqual(code, 1)
        self.assertIn("--stem", out)
        self.assertFalse(self.dst.exists())
        code, _ = run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(code, 0)
        self.assertTrue((self.dst / "t_x_d.png").exists())

    def test_flat_normal_is_reported(self):
        gltf_set(self.src, "set")
        code, out = run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(code, 0)
        self.assertIn("[convert] WARNING: set normal map is flat", out)
        self.assertTrue((self.dst / "t_x_n.png").exists())

    def test_multi_set_run_writes_all_or_nothing(self):
        gltf_set(self.src, "a")
        gltf_set(self.src, "b")
        self.dst.mkdir()
        png(self.dst / "t_b_s.png", (1, 2, 3))
        code, out = run("--src", self.src, "--dst", self.dst)
        self.assertEqual(code, 1)
        self.assertIn("t_b_s.png", out)
        self.assertEqual([p.name for p in self.dst.iterdir()], ["t_b_s.png"])

    def test_existing_output_is_not_overwritten_without_force(self):
        gltf_set(self.src, "set")
        self.dst.mkdir()
        png(self.dst / "t_x_d.png", (1, 2, 3))
        before = (self.dst / "t_x_d.png").read_bytes()
        code, out = run("--src", self.src, "--dst", self.dst, "--stem", "t_x")
        self.assertEqual(code, 1)
        self.assertIn("--force", out)
        self.assertEqual((self.dst / "t_x_d.png").read_bytes(), before)
        code, _ = run("--src", self.src, "--dst", self.dst, "--stem", "t_x", "--force")
        self.assertEqual(code, 0)
        self.assertNotEqual((self.dst / "t_x_d.png").read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
