import json
from pathlib import Path
import tempfile
import unittest

import numpy as np

from tools.hair_engine import library, presets, rigging
from tools.hair_engine.model import Hair, Strand


def line(count=5):
    return Strand(np.column_stack((np.zeros(count), np.zeros(count), -np.linspace(0., 1., count))))


class RiggingTests(unittest.TestCase):
    def test_ribbon_bones_form_parented_chain(self):
        bones = rigging.ribbon_bones(line(), 4)
        self.assertEqual(len(bones), 4)
        self.assertEqual(bones[0]["parent"], None)
        self.assertEqual(bones[1]["parent"], "hair_bone_000")
        np.testing.assert_allclose(bones[0]["head"], [0., 0., 0.])
        np.testing.assert_allclose(bones[-1]["tail"], [0., 0., -1.])

    def test_ribbon_weights_are_normalized_and_at_most_two_bones(self):
        weights = rigging.ribbon_weights(line(), 4)
        self.assertEqual(len(weights), 5)
        for row in weights:
            self.assertLessEqual(len(row), 2)
            self.assertAlmostEqual(sum(row.values()), 1.)
        self.assertEqual(weights[0], {"hair_bone_000": 1.0})
        self.assertEqual(weights[-1], {"hair_bone_003": 1.0})

    def test_spring_pins_root_and_preserves_approximate_rest_lengths(self):
        simulation = rigging.SpringSimulation([line()], gravity=np.zeros(3), damping=0.9)
        simulation.step(1 / 60, iterations=8)
        positions = simulation.strands[0].positions
        np.testing.assert_allclose(positions[0], [0., 0., 0.])
        lengths = np.linalg.norm(np.diff(positions, axis=0), axis=1)
        np.testing.assert_allclose(lengths, 0.25, atol=1e-6)

    def test_spring_collision_pushes_points_out(self):
        simulation = rigging.SpringSimulation([line()], gravity=np.array([0., 0., -1.]),
                                              collision_spheres=[(np.array([0., 0., -0.5]), 0.1)])
        simulation.step(0.1, iterations=8)
        distances = np.linalg.norm(simulation.strands[0].positions - np.array([0., 0., -0.5]), axis=1)
        self.assertTrue(np.all(distances >= 0.099999))


class PresetTests(unittest.TestCase):
    def valid(self):
        return {"schema_version": [1, 0, 0], "kind": "deformer", "name": "curl",
                "settings": {"turns": 3}}

    def test_version_requires_integer_tuple(self):
        self.assertEqual(presets.version_tuple([1, 2, 3]), (1, 2, 3))
        with self.assertRaisesRegex(ValueError, "version"):
            presets.version_tuple("1.10.0")
        with self.assertRaisesRegex(ValueError, "version"):
            presets.version_tuple([1.5, 0, 0])

    def test_json_round_trip_is_deterministic(self):
        first = presets.dumps(self.valid())
        second = presets.dumps(self.valid())
        self.assertEqual(first, second)
        self.assertEqual(presets.loads(first), self.valid())

    def test_migration_uses_explicit_versions_and_preserves_backup(self):
        old = {"schema_version": [0, 9, 0], "kind": "deformer", "name": "old",
               "deformer": {"turns": 2}}
        migrated, backup = presets.migrate(old)
        self.assertEqual(migrated["settings"], {"turns": 2})
        self.assertEqual(migrated["schema_version"], [1, 0, 0])
        self.assertIn("deformer", backup)

    def test_write_validates_before_touching_existing_file(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "preset.json"
            path.write_text("keep", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "schema_version"):
                presets.write(path, {"kind": "deformer"}, overwrite=True)
            self.assertEqual(path.read_text(encoding="utf-8"), "keep")

    def test_hair_round_trip_preserves_strand_metadata(self):
        hair = Hair([line()])
        hair.strands[0].tags = frozenset({"dwarf", "beard"})
        hair.strands[0].point_attributes = {"mask": np.linspace(0., 1., 5)}
        data = presets.hair_to_dict(hair)
        restored = presets.hair_from_dict(data)
        self.assertEqual(restored.strands[0].tags, hair.strands[0].tags)
        np.testing.assert_allclose(restored.strands[0].point_attributes["mask"],
                                   hair.strands[0].point_attributes["mask"])


class LibraryTests(unittest.TestCase):
    def test_library_round_trip_and_duplicate_names(self):
        with tempfile.TemporaryDirectory() as folder:
            store = library.PresetLibrary(folder)
            first = store.save("beard", self.hair(), overwrite=False)
            second = store.save("beard", self.hair(), overwrite=False)
            self.assertNotEqual(first, second)
            self.assertEqual(len(store.list()), 2)
            restored = store.load(first)
            np.testing.assert_allclose(restored.strands[0].positions, self.hair().strands[0].positions)

    def test_library_rejects_unsafe_names(self):
        with tempfile.TemporaryDirectory() as folder:
            store = library.PresetLibrary(folder)
            with self.assertRaisesRegex(ValueError, "name"):
                store.save("../escape", self.hair())

    def test_duplicate_remap_is_stable(self):
        mapping = library.duplicate_remap(["hair", "hair", "beard", "hair"])
        self.assertEqual(mapping, ["hair", "hair.001", "beard", "hair.002"])

    @staticmethod
    def hair():
        return Hair([line()])


if __name__ == "__main__":
    unittest.main()
