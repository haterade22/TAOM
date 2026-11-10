import json
import math
from pathlib import Path
import unittest

from tools.retopo_engine import differential, engine


ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / "tools/tests/fixtures/retopo_reference.json"


def canonical_vertices(vertices):
    return sorted(tuple(round(value, 5) for value in vertex) for vertex in vertices)


def canonical_faces(vertices, faces, oriented=True):
    canonical = []
    for face in faces:
        coordinates = [tuple(round(value, 5) for value in vertices[index]) for index in face]
        rotations = [tuple(coordinates[index:] + coordinates[:index]) for index in range(len(coordinates))]
        if not oriented:
            reversed_coordinates = list(reversed(coordinates))
            rotations.extend(tuple(reversed_coordinates[index:] + reversed_coordinates[:index]) for index in range(len(coordinates)))
        canonical.append(min(rotations))
    return sorted(canonical)


def canonical_uv_faces(vertices, faces, uv, oriented=True):
    if not uv:
        return []
    canonical = []
    cursor = 0
    for face in faces:
        if cursor + len(face) > len(uv):
            break
        items = []
        for index in face:
            items.append((
                tuple(round(value, 5) for value in vertices[index]),
                tuple(round(value, 5) for value in uv[cursor]),
            ))
            cursor += 1
        rotations = [tuple(items[index:] + items[:index]) for index in range(len(items))]
        if not oriented:
            reversed_items = list(reversed(items))
            rotations.extend(tuple(reversed_items[index:] + reversed_items[:index]) for index in range(len(items)))
        canonical.append(min(rotations))
    return sorted(canonical)


def signed_volume(vertices, faces):
    volume = 0.0
    for face in faces:
        first = vertices[face[0]]
        for index in range(1, len(face) - 1):
            second, third = vertices[face[index]], vertices[face[index + 1]]
            cross = (
                second[1] * third[2] - second[2] * third[1],
                second[2] * third[0] - second[0] * third[2],
                second[0] * third[1] - second[1] * third[0],
            )
            volume += sum(a * b for a, b in zip(first, cross)) / 6
    return volume


class RetopoDifferentialTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.reference = json.loads(REFERENCE.read_text(encoding="utf-8"))

    def assert_bounds_close(self, expected, actual, label, delta=0.0001):
        self.assertEqual(len(expected), len(actual), label)
        for expected_row, actual_row in zip(expected, actual):
            for expected_value, actual_value in zip(expected_row, actual_row):
                self.assertAlmostEqual(expected_value, actual_value, delta=delta, msg=label)

    def assert_mesh_contract(self, expected, actual, oriented=True, compare_faces=True):
        for key in ("vertices", "edges", "faces", "face_sizes", "components", "uv_name"):
            self.assertEqual(expected[key], actual[key], key)
        self.assert_bounds_close(expected["bounds"], actual["bounds"], "bounds")
        self.assertEqual(canonical_vertices(expected["geometry_vertices"]), canonical_vertices(actual["geometry_vertices"]))
        if compare_faces:
            self.assertEqual(canonical_faces(expected["geometry_vertices"], expected["geometry_faces"], oriented), canonical_faces(actual["geometry_vertices"], actual["geometry_faces"], oriented))
        self.assertIn(len(expected["geometry_uv"]), {0, sum(len(face) for face in expected["geometry_faces"])})
        if expected["geometry_uv"]:
            self.assert_bounds_close(expected["uv_bounds"], actual["uv_bounds"], "uv_bounds")
            self.assertEqual(canonical_uv_faces(expected["geometry_vertices"], expected["geometry_faces"], expected["geometry_uv"], oriented), canonical_uv_faces(actual["geometry_vertices"], actual["geometry_faces"], actual["geometry_uv"], oriented))

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
                    if case["name"] == "density_sharp_high":
                        self.assertIn(actual["faces"], {60, 62})
                    else:
                        self.assertEqual(expected["faces"], actual["faces"])
                    self.assertEqual(expected["components"], actual["components"])
                    self.assert_bounds_close(expected["bounds"], actual["bounds"], "bounds", delta=0.01)
                    self.assert_bounds_close(expected["uv_bounds"], actual["uv_bounds"], "uv_bounds", delta=0.01)
                    self.assertEqual(len(actual["geometry_uv"]), sum(len(face) for face in actual["geometry_faces"]))
                    self.assertTrue(all(math.isfinite(value) for coordinate in actual["geometry_uv"] for value in coordinate))
                    self.assertEqual(expected["uv_name"], actual["uv_name"])
                    self.assertTrue({"pattern_id", "part_id", "retopo_plane_id"} <= set(actual["attributes"]))
                    surface = engine.SourceSurface(case["source"])
                    cursor = 0
                    for face in actual["geometry_faces"]:
                        for vertex in face:
                            position, _, _ = surface.project(actual["geometry_uv"][cursor])
                            for axis in range(3):
                                self.assertAlmostEqual(position[axis], actual["geometry_vertices"][vertex][axis], delta=0.00001)
                            cursor += 1
                else:
                    validity_exceptions = {
                        "initialize", "thickness_negative", "mirror_same_y_separate",
                        "mirror_same_y_shared", "mirror_y_single",
                    }
                    self.assert_mesh_contract(expected, actual, oriented=case["name"] not in validity_exceptions, compare_faces=case["name"] != "initialize")
                    if case["name"] == "initialize":
                        self.assertEqual(4, actual["edges"])
                        self.assertTrue(all(abs(engine.signed_area(face, actual["geometry_vertices"])) > 0.99 for face in actual["geometry_faces"]))
                    elif case["name"].startswith("mirror_") and "y" in case["name"]:
                        self.assertTrue(all(engine.signed_area(face, actual["geometry_vertices"]) > 0 for face in actual["geometry_faces"]))
                    elif case["name"] == "thickness_negative":
                        self.assertGreater(signed_volume(actual["geometry_vertices"], actual["geometry_faces"]), 0)

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
