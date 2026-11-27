#!/usr/bin/env python3
"""Unit tests for the pure parts of the painter_lab recipes, without Painter.

Run:  python -m unittest tools.tests.test_painter_lab_recipes

The recipes live outside the repo (E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/painter_lab/
recipes). Painter's modules are replaced by mocks, then the recipe file is executed the way
painter_run.py executes it (a fresh globals dict with ARGS), so the config builders are proven
without the bridge. Painter-side calls are NOT covered: those need a run in Painter.
The tests skip when the recipe folder is not on this machine.
"""
import json
import os
import sys
import unittest
from unittest import mock

RECIPES = os.environ.get(
    "PAINTER_LAB_RECIPES",
    r"E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell\painter_lab\recipes")
MODULES = ["substance_painter", "substance_painter.application", "substance_painter.baking",
           "substance_painter.colormanagement", "substance_painter.event", "substance_painter.export",
           "substance_painter.layerstack", "substance_painter.project", "substance_painter.resource",
           "substance_painter.textureset", "substance_painter.ui", "substance_painter.levels",
           "PySide6", "PySide6.QtCore"]


def run_recipe(name, args):
    """Execute a recipe file with mocked Painter modules; return its globals."""
    stubs = {m: mock.MagicMock(name=m) for m in MODULES}
    with mock.patch.dict(sys.modules, stubs):
        with open(os.path.join(RECIPES, name), encoding="utf-8") as handle:
            code = compile(handle.read(), name, "exec")
        scope = {"ARGS": dict(args, lib=os.path.join(RECIPES, "r00_lib.py"))}
        exec(code, scope)
    return scope


@unittest.skipUnless(os.path.isdir(RECIPES), "recipe folder not on this machine")
class ExportConfigTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.r = run_recipe("r05_export_packed.py", {"mode": "config", "out": "E:/x", "size": 1024})

    def config(self, **kw):
        defaults = dict(out_dir="E:/x", set_names=["t"], size=1024)
        defaults.update(kw)
        return self.r["build_export_config"](**defaults)

    def maps(self, **kw):
        return {m["fileName"]: m for m in self.config(**kw)["exportPresets"][0]["maps"]}

    def test_mode_config_returns_json_without_painter(self):
        result = self.r["RESULT"]["config"]
        json.dumps(result)
        self.assertEqual(result["exportPath"], "E:/x")

    def test_packed_s_default_is_metal_gloss_ao(self):
        s = self.maps()["$textureSet_s"]["channels"]
        got = [(c["destChannel"], c["srcMapType"], c["srcMapName"], c["srcChannel"]) for c in s]
        self.assertEqual(got, [("R", "documentMap", "metallic", "L"),
                               ("G", "virtualMap", "Glossiness", "L"),
                               ("B", "virtualMap", "AO_Mixed", "L")])

    def test_packed_s_gltf_order(self):
        s = self.maps(s_order="ao_rough_metal")["$textureSet_s"]["channels"]
        self.assertEqual([c["srcMapName"] for c in s], ["AO_Mixed", "roughness", "metallic"])

    def test_unknown_pack_order_is_refused(self):
        with self.assertRaises(ValueError):
            self.config(s_order="rgb")

    def test_normal_name_follows_the_choice(self):
        for kind in ("OpenGL", "DirectX"):
            n = self.maps(normal=kind)["$textureSet_normal"]["channels"]
            self.assertEqual({c["srcMapName"] for c in n}, {"Normal_" + kind})
            self.assertEqual([c["destChannel"] for c in n], ["R", "G", "B"])

    def test_only_base_colour_is_dithered(self):
        maps = self.maps()
        flags = {k: v["parameters"]["dithering"] for k, v in maps.items()}
        self.assertEqual(flags, {"$textureSet_baseColor": True, "$textureSet_normal": False,
                                 "$textureSet_s": False, "$textureSet_height": False})

    def test_height_is_16_bit_png_and_optional(self):
        self.assertEqual(self.maps()["$textureSet_height"]["parameters"]["bitDepth"], "16")
        self.assertNotIn("$textureSet_height", self.maps(height=False))

    def test_global_rule_carries_size_and_padding_only(self):
        params = self.config()["exportParameters"][0]["parameters"]
        self.assertEqual(params, {"sizeLog2": 10, "paddingAlgorithm": "infinite"})

    def test_dilation_only_for_algorithms_that_read_it(self):
        self.assertEqual(self.config(padding="diffusion", dilation=8)["exportParameters"][0]["parameters"]
                         ["dilationDistance"], 8)
        self.assertNotIn("dilationDistance", self.config()["exportParameters"][0]["parameters"])

    def test_size_must_be_a_power_of_two(self):
        for bad in (1000, 64, 0):
            with self.assertRaises(ValueError):
                self.config(size=bad)

    def test_non_square_size_is_a_log2_pair(self):
        self.assertEqual(self.config(size=(2048, 1024))["exportParameters"][0]["parameters"]["sizeLog2"], [11, 10])

    def test_export_list_names_every_set(self):
        self.assertEqual(self.config(set_names=["a", "b"])["exportList"], [{"rootPath": "a"}, {"rootPath": "b"}])

    def test_normal_must_be_known(self):
        with self.assertRaises(ValueError):
            self.config(normal="Mikktspace")


@unittest.skipUnless(os.path.isdir(RECIPES), "recipe folder not on this machine")
class BakeHelperTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # the recipe runs its bake at import: give it no files, so it stops at the first check
        with mock.patch.dict(sys.modules, {m: mock.MagicMock(name=m) for m in MODULES}):
            with open(os.path.join(RECIPES, "r03_bake_trim_sheet.py"), encoding="utf-8") as handle:
                text = handle.read().replace("RESULT = bake_trim_sheet(ARGS)  # noqa: F821", "")
            cls.scope = {"ARGS": {"lib": os.path.join(RECIPES, "r00_lib.py")}}
            exec(compile(text, "r03", "exec"), cls.scope)

    def test_find_prop_matches_key_or_label(self):
        prop = mock.Mock()
        prop.label.return_value = "Max Frontal Distance"
        found = self.scope["find_prop"]({"MaxHeight": prop}, "frontal")
        self.assertIs(found, prop)
        self.assertIsNone(self.scope["find_prop"]({"MaxHeight": prop}, "cage"))

    def test_missing_input_file_is_named(self):
        with self.assertRaises(FileNotFoundError):
            self.scope["bake_trim_sheet"]({"low": "E:/does/not/exist_low.fbx", "high": "E:/x_high.fbx"})

    def test_export_name_table_covers_the_seven_default_maps(self):
        table = self.scope["MAP_USAGE"]
        for name in self.scope["DEFAULT_MAPS"]:
            self.assertIn(name, table)


if __name__ == "__main__":
    unittest.main()
