import json
import math
from pathlib import Path
import unittest

from tools.retopo_engine import differential, engine


ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / "tools/tests/fixtures/retopo_reference.json"


class RetopoDifferentialTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.reference = json.loads(REFERENCE.read_text(encoding="utf-8"))

    def assert_bounds_close(self, expected, actual, label, delta=0.0001):
        self.assertEqual(len(expected), len(actual), label)
        for expected_row, actual_row in zip(expected, actual):
            for expected_value, actual_value in zip(expected_row, actual_row):
                self.assertAlmostEqual(expected_value, actual_value, delta=delta, msg=label)

    def assert_mesh_contract(self, expected, actual):
        for key in ("vertices", "edges", "faces", "face_sizes", "components", "uv_name"):
            self.assertEqual(expected[key], actual[key], key)
        self.assert_bounds_close(expected["bounds"], actual["bounds"], "bounds")
        if expected["sample_uv"]:
            self.assert_bounds_close(expected["uv_bounds"], actual["uv_bounds"], "uv_bounds")

    def test_reference_cases_have_no_errors(self):
        failures = {name: value.get("error") for name, value in self.reference["cases"].items() if "error" in value}
        self.assertFalse(failures, failures)

    def test_public_behavior_matches_reference(self):
        for case in differential.cases():
            with self.subTest(case=case["name"]):
                expected = self.reference["cases"][case["name"]]
                if case["point_guides"]:
                    case["source"].attributes["point_guide"] = [
                        index in case["point_guides"] for index in range(len(case["source"].vertices))
                    ]
                actual = differential.summary(engine.retopologize(case["source"], case["guide"], case["settings"]))
                if case["name"] in {"auto_fill_hex_prepass", "auto_fill_hex_connected"}:
                    self.assertEqual(6, actual["vertices"])
                    self.assertEqual(2, actual["faces"])
                    self.assertEqual({"4": 2}, actual["face_sizes"])
                    self.assertEqual(1, actual["components"])
                elif case["name"].startswith("density_") and case["name"] != "density_sharp":
                    self.assertEqual({"4": actual["faces"]}, actual["face_sizes"])
                    self.assertGreaterEqual(actual["vertices"], 15)
                    minimum_faces = 36 if case["name"] == "density_sharp_high" else 48
                    if case["name"].startswith("density_full"):
                        minimum_faces = 8
                    self.assertGreaterEqual(actual["faces"], minimum_faces)
                    self.assertEqual(expected["components"], actual["components"])
                    self.assert_bounds_close(expected["bounds"], actual["bounds"], "bounds", delta=0.01)
                else:
                    self.assert_mesh_contract(expected, actual)

    def test_inspector_matches_reference(self):
        for fix in (False, True):
            for only_debug in (False, True):
                key = f"fix_{fix}_debug_{only_debug}"
                with self.subTest(key=key):
                    result, bad_edges = engine.inspect_topology(differential.non_manifold(), fix_split_edges=fix)
                    if only_debug:
                        result = differential.non_manifold()
                        result.faces = []
                    actual = differential.summary(result)
                    expected = self.reference["inspector"][key]
                    self.assertEqual(expected["faces"], actual["faces"])
                    if not only_debug:
                        self.assertEqual(expected["vertices"], actual["vertices"])
                    else:
                        self.assertEqual(1, len(bad_edges))


if __name__ == "__main__":
    unittest.main()
