import os
from pathlib import Path
import subprocess
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import hair_tool_pipeline as htp


class ProfileTests(unittest.TestCase):
    def test_dwarf_hair_uses_verified_material_and_sibling_donors(self):
        options = htp.profile_options("dwarf_hair")
        self.assertEqual(options["material"], "m_dwarf_beards_b")
        self.assertIn("Dwarf_Hair_A_lod0", options["references"])
        self.assertEqual(options["target"], "Dwarf_Hair_New_lod0")

    def test_dwarf_beard_uses_verified_material_and_sibling_donors(self):
        options = htp.profile_options("dwarf_beard")
        self.assertEqual(options["material"], "m_dwarf_beards_b")
        self.assertIn("SK_Dwarf_Beard_A_01", options["references"])
        self.assertEqual(options["target"], "SK_Dwarf_Beard_New")

    def test_elf_requires_explicit_material(self):
        with self.assertRaisesRegex(ValueError, "material"):
            htp.profile_options("elf_hair", references=["Finished_Elf_Hair"])

    def test_elf_requires_explicit_weight_donor(self):
        with self.assertRaisesRegex(ValueError, "reference"):
            htp.profile_options("elf_hair", material="m_elf_hair")

    def test_elf_accepts_explicit_material_and_donor(self):
        options = htp.profile_options(
            "elf_hair", material="m_elf_hair", references=["Finished_Elf_Hair"]
        )
        self.assertEqual(options["material"], "m_elf_hair")
        self.assertEqual(options["references"], ["Finished_Elf_Hair"])

    def test_explicit_values_override_dwarf_defaults(self):
        options = htp.profile_options(
            "dwarf_hair", material="custom", references=["Custom_Donor"]
        )
        self.assertEqual(options["material"], "custom")
        self.assertEqual(options["references"], ["Custom_Donor"])

    def test_starter_profiles_are_deterministic_and_positive(self):
        for name in htp.PROFILES:
            with self.subTest(name=name):
                settings = htp.starter_settings(name)
                self.assertGreater(settings["length"], 0)
                self.assertGreater(settings["density"], 0)
                self.assertGreater(settings["width"], 0)
                self.assertGreaterEqual(settings["resolution"], 2)
                self.assertGreaterEqual(settings["seed"], 0)


class GuideTests(unittest.TestCase):
    def test_front_root_drapes_down(self):
        self.assertEqual(htp.downward_direction((0, 1, 0)), (0, 0, -1))

    def test_top_root_has_a_deterministic_downhill_direction(self):
        first = htp.downward_direction((0, 0, 1))
        second = htp.downward_direction((0, 0, 1))
        self.assertEqual(first, second)
        self.assertLess(first[2], 0)
        self.assertAlmostEqual(sum(x * x for x in first), 1.0)

    def test_diagonal_root_direction_is_unit_and_downhill(self):
        direction = htp.downward_direction((0.5, 0.5, 0.5))
        self.assertLess(direction[2], 0)
        self.assertAlmostEqual(sum(x * x for x in direction), 1.0)

    def test_profiles_have_finite_guide_counts(self):
        for name in htp.PROFILES:
            with self.subTest(name=name):
                self.assertGreaterEqual(htp.starter_settings(name)["guides"], 8)

    def test_projection_gate_rejects_frame_errors(self):
        error = htp.projection_error({"max_mm": 1854.5})
        self.assertIn("1854.5", error)

    def test_projection_gate_accepts_reasonable_donor_distances(self):
        self.assertIsNone(htp.projection_error({"max_mm": 125.0}))


class FinalMeshGateTests(unittest.TestCase):
    def valid(self):
        return {
            "vertices": 100,
            "polygons": 50,
            "uv_layers": ["UVMap", "HairTool_UV"],
            "weighted_vertices": 100,
            "shape_keys": [],
            "materials": ["m_dwarf_beards_b"],
        }

    def test_valid_mesh_passes(self):
        self.assertEqual(htp.final_mesh_errors(self.valid(), "m_dwarf_beards_b"), [])

    def test_requires_both_uv_layers(self):
        mesh = self.valid()
        mesh["uv_layers"] = ["UVMap"]
        self.assertIn("HairTool_UV", htp.final_mesh_errors(mesh, "m_dwarf_beards_b")[0])

    def test_rejects_unweighted_vertices(self):
        mesh = self.valid()
        mesh["weighted_vertices"] = 99
        self.assertIn("99/100", htp.final_mesh_errors(mesh, "m_dwarf_beards_b")[0])

    def test_rejects_residual_shape_keys(self):
        mesh = self.valid()
        mesh["shape_keys"] = ["Basis", "shape_01"]
        self.assertIn("shape_01", htp.final_mesh_errors(mesh, "m_dwarf_beards_b")[0])

    def test_rejects_wrong_material(self):
        mesh = self.valid()
        mesh["materials"] = ["HT_Default_Material"]
        self.assertIn("HT_Default_Material", htp.final_mesh_errors(mesh, "m_dwarf_beards_b")[0])

    def test_rejects_empty_mesh(self):
        mesh = self.valid()
        mesh["vertices"] = 0
        mesh["polygons"] = 0
        mesh["weighted_vertices"] = 0
        errors = htp.final_mesh_errors(mesh, "m_dwarf_beards_b")
        self.assertTrue(any("empty" in error for error in errors))


class BlenderLaunchTests(unittest.TestCase):
    def test_script_bootstrap_loads_shared_blender_modules(self):
        script = Path(htp.__file__).resolve()
        code = (
            "import importlib.util; "
            f"spec=importlib.util.spec_from_file_location('hair_tool_pipeline', r'{script}'); "
            "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module); "
            "import sys,types; sys.modules['bpy']=types.ModuleType('bpy'); "
            "import add_mesh_lods; import transfer_upper_mesh_weights"
        )
        result = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)


if __name__ == "__main__":
    unittest.main()
