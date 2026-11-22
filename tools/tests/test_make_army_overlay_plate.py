"""The army panel's plate, cut from the stats plate art (#1532).

Run:  python -m unittest tools.tests.test_make_army_overlay_plate

The prefab draws cb_army_plate.png at 396 x 234 UI px, the stats plate's density (2.25 source px per UI px), and
ArmyOverlayPrefabTests reads its bottom border row. These tests pin the size, that the shipped file is the
generator's output, and that the joints leave the border and the stone rows whole.
"""
import sys
import unittest
from pathlib import Path

try:
    from PIL import Image, ImageChops
except ImportError:  # Pillow is a tool dependency, not a test-runner one
    raise unittest.SkipTest("Pillow is not installed")

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import make_army_overlay_plate as plate  # noqa: E402

SHIPPED = REPO / "Main/_Module/GUI/FactionUI/RuntimeSprites/cb_army_plate.png"


class ArmyPlateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.built = plate.build()
        cls.alpha = cls.built.split()[3]
        cls.src = Image.open(plate.SOURCE).convert("RGBA")

    def test_size_matches_the_prefab_at_the_stats_plates_density(self):
        self.assertEqual((891, 527), self.built.size)
        self.assertAlmostEqual(396 * 2.25, self.built.width, delta=1)
        self.assertAlmostEqual(234 * 2.25, self.built.height, delta=1)

    def test_shipped_file_is_the_generators_output(self):
        # Pixel bytes, not ImageChops.difference(...).getbbox(): on RGBA that looks at alpha only.
        shipped = Image.open(SHIPPED).convert("RGBA")
        self.assertEqual(self.built.size, shipped.size)
        self.assertTrue(shipped.tobytes() == self.built.tobytes(),
                        "cb_army_plate.png differs from make_army_overlay_plate.py: re-run it with --out")

    def test_top_border_is_one_straight_line_across_every_joint(self):
        # The first opaque row is the same from the left chamfer to the right one, so no step or gap shows.
        tops = set()
        for x in range(60, self.built.width - 60):
            column = [y for y in range(60) if self.alpha.getpixel((x, y)) > 128]
            self.assertTrue(column, f"no top border at x={x}")
            tops.add(column[0])
        self.assertEqual(1, len(tops), f"top border rows vary: {sorted(tops)}")

    def test_bottom_border_is_one_straight_line_across_every_joint(self):
        bottoms = set()
        for x in range(90, self.built.width - 90):
            column = [y for y in range(self.built.height - 80, self.built.height) if self.alpha.getpixel((x, y)) > 128]
            self.assertTrue(column, f"no bottom border at x={x}")
            bottoms.add(column[-1])
        self.assertEqual(1, len(bottoms), f"bottom border rows vary: {sorted(bottoms)}")

    def test_side_edge_is_one_straight_line_down_the_extra_rows(self):
        # The reflected stone rows of each end keep the straight edge: no bump at a joint.
        edges = set()
        for y in range(45, 426):
            row = [x for x in range(self.built.width - 60, self.built.width) if self.alpha.getpixel((x, y)) > 128]
            self.assertTrue(row, f"no right edge at y={y}")
            edges.add(row[-1])
        self.assertEqual(1, len(edges), f"right edge columns vary: {sorted(edges)}")

    def test_end_strip_runs_into_its_bottom_part_on_consecutive_source_rows(self):
        # The reflected rows just above the pasted bottom part and the part itself are consecutive source rows,
        # so the lower joint shows no jump.
        above = 6
        right_width = self.src.width - plate.RIGHT_FROM
        x0 = self.built.width - right_width + plate.SEAM
        bottom_from = min(plate.BOTTOM_FROM, plate.EDGE_BAND[1])
        rows = self.src.height - bottom_from + above
        built = self.built.crop((x0, self.built.height - rows, self.built.width, self.built.height))
        source = self.src.crop((plate.RIGHT_FROM + plate.SEAM, bottom_from - above, self.src.width, self.src.height))
        self.assertTrue(built.tobytes() == source.tobytes(), "the end strip's lower joint skips source rows")

    def test_outline_is_symmetric(self):
        mirrored = self.alpha.transpose(Image.FLIP_LEFT_RIGHT)
        diff = ImageChops.difference(self.alpha, mirrored).point(lambda v: 255 if v > 128 else 0)
        self.assertIsNone(diff.getbbox(), "left and right ends differ in shape")


if __name__ == "__main__":
    unittest.main()
