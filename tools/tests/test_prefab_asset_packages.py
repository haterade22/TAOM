#!/usr/bin/env python3
"""Gate on the hand-written prefab packages in Main/_Module/AssetPackages/.

Run:  python -m unittest tools.tests.test_prefab_asset_packages

The four camp and refuge props there were built outside the Modding Kit with yotthani's
MithrilForge (docs/reference/tpac-static-prop-authoring.md). CampLayoutBuilder.PlaceCenteredPrefab
asks for each mesh by name through MetaMesh.GetCopy(..., mayReturnNull: true) and, on a miss, the
caller draws the vanilla siege-camp layout instead; the only trace is the entity count in the
`[FieldCamp] placed N` / `[Refuge] placed N` debug line. So a constant that no longer names a
packaged mesh item, or a package that stops parsing, would degrade the camps without an error.
This test makes each of those a failure:

  * the mesh constants the code passes to PlaceCenteredPrefab (found by scanning Main/**/*.cs for
    its call sites) are exactly the table below, and each is declared once, outside a comment;
  * each of those meshes is a Metamesh item in a package here, and no item name or GUID repeats;
  * every package parses, its header's TOC-size field places the data where its first segment
    starts, and its segments lie inside the file without overlapping.

What it cannot see: whether the game LOADS these packages. On 2026-09-29 it did not. The dev install
reads the module's loose Assets/ tree instead of AssetPackages/, and the editor-built release ships
only pack0.tpac, so every camp shows the vanilla fallback (docs/reference/tpac-static-prop-authoring.md
"What TAOM ships built this way"). This gate keeps the source copies and the code in step for when
that delivery is fixed.

Why not a byte round trip: MithrilForge's writer pads the TOC-size field and aligns each data segment
to 8 bytes, while tpac_clone_metamesh.serialize (like the Kit and every vanilla package) writes them
packed. The parser reads both; only the writer differs. Measured 2026-09-29 on all four packages.
"""
import os
import re
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import tpac_clone_metamesh as tcm  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
ASSET_PACKAGES = REPO / "Main" / "_Module" / "AssetPackages"

# (source file, constant name) for every prefab mesh the code places from these packages. A new
# prop is added here and to a package in the same change; the call-site test fails until it is.
MESH_CONSTANTS = [
    ("Main/Features/FieldCamp/Visuals/CampLayoutBuilder.cs", "FieldCampPrefabMesh"),
    ("Main/Features/FieldCamp/Visuals/CampLayoutBuilder.cs", "PalisadeRingMesh"),
    ("Main/Features/Refuge/Visuals/RefugeVisualService.cs", "RefugeCampMesh"),
    ("Main/Features/Refuge/Visuals/RefugeVisualService.cs", "RefugeRingMesh"),
]

# PlaceCenteredPrefab(scene, in center, <mesh>, ...): the third argument. A string literal there is
# captured with its quotes, so it can never equal a table entry and fails the call-site test.
CALL_SITE = re.compile(r'PlaceCenteredPrefab\(\s*\w+\s*,\s*in\s+\w+\s*,\s*(\w+|"[^"]*")\s*,')


def constant_value(source: str, name: str) -> str:
    """The value of `const string <name> = "...";`, declared exactly once at the start of a line,
    so a commented-out or doc-comment copy of the declaration is never read."""
    text = (REPO / source).read_text(encoding="utf-8-sig")
    hits = re.findall(r'^[ \t]*(?:(?:public|internal|private|protected)[ \t]+)*const[ \t]+string[ \t]+'
                      + re.escape(name) + r'[ \t]*=[ \t]*"([^"]+)"[ \t]*;', text, re.M)
    if len(hits) != 1:
        raise AssertionError(f"{source}: expected one `const string {name} = \"...\";`, found {len(hits)}")
    return hits[0]


def placed_prefab_arguments() -> set[str]:
    placed = set()
    for cs in (REPO / "Main").rglob("*.cs"):
        placed.update(CALL_SITE.findall(cs.read_text(encoding="utf-8-sig")))
    return placed


def prefab_packages() -> list[Path]:
    return sorted(ASSET_PACKAGES.glob("*.tpac"))


class PrefabAssetPackagesTests(unittest.TestCase):
    def test_packages_exist(self):
        # An empty glob would let every other test here pass over nothing.
        self.assertTrue(prefab_packages(), f"no .tpac under {ASSET_PACKAGES}")

    def test_the_table_is_what_the_code_places(self):
        self.assertEqual(placed_prefab_arguments(), {name for _, name in MESH_CONSTANTS},
                         "the PlaceCenteredPrefab call sites and MESH_CONSTANTS disagree; "
                         "a mesh the code places would go unchecked")

    def test_every_mesh_the_code_places_is_packaged(self):
        metameshes = {}
        for path in prefab_packages():
            for item in tcm.parse(path.read_bytes()).items:
                if item.is_metamesh:
                    metameshes[item.name] = path.name
        for source, name in MESH_CONSTANTS:
            with self.subTest(constant=name):
                mesh = constant_value(source, name)
                self.assertIn(mesh, metameshes,
                              f"{source} {name} = {mesh!r} is in no package under {ASSET_PACKAGES}; "
                              "the camp would fall back to the vanilla layout without an error")

    def test_no_two_items_share_a_name_or_guid(self):
        # A second package holding the same item shadows the first, and two loaded items with one
        # GUID crashed the engine once (tpac_clone_metamesh.py docstring).
        names, guids = [], []
        for path in prefab_packages():
            for item in tcm.parse(path.read_bytes()).items:
                names.append(item.name)
                guids.append(item.item_guid)
        self.assertEqual(len(names), len(set(names)), "an item name appears twice")
        self.assertEqual(len(guids), len(set(guids)), "an item GUID appears twice")

    def test_every_package_is_structurally_sound(self):
        for path in prefab_packages():
            with self.subTest(package=path.name):
                data = path.read_bytes()
                pkg = tcm.parse(data)
                self.assertTrue(pkg.items, "package has no items")
                toc_length = sum(len(item.toc) for item in pkg.items)
                toc_field = struct.unpack_from("<Q", data, 28)[0]
                data_start = tcm.HEADER_SIZE + toc_field
                self.assertGreaterEqual(toc_field, toc_length,
                                        "TOC-size field is shorter than the TOC itself")
                segments = sorted((seg.offset, seg.storage, item.name)
                                  for item in pkg.items for seg in item.segments)
                self.assertTrue(segments, "package has no data segments")
                self.assertEqual(segments[0][0], data_start,
                                 "the engine reads data from 36 + TOC-size; the first segment is elsewhere")
                previous_end = data_start
                for offset, storage, owner in segments:
                    self.assertGreaterEqual(offset, previous_end, f"{owner}: segment overlaps the previous one")
                    self.assertLessEqual(offset + storage, len(data), f"{owner}: segment runs past the end of the file")
                    previous_end = offset + storage


if __name__ == "__main__":
    unittest.main()
