"""build_battle_scenes: the generator for sp_battle_scenes.xml, the campaign field-battle scene list.

Uses a small synthetic vanilla scene list in a temp dir, so it runs in CI with no game install. Proves
the guards that keep the engine's loader from throwing at campaign start (GameSceneDataManager runs
int.Parse on every map_indices token, so an empty list kills every campaign load), the coverage
contract (every index 0-255 resolves to a scene), the byte shape (no BOM, CRLF, root is the first
element), the --check / --dry-run / --apply modes, and region-table regressions found in review.
"""
import io
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import build_battle_scenes as bbs  # noqa: E402

VANILLA = """<?xml version="1.0" encoding="utf-8"?>
<SPBattleScenes>
\t<Scene id="battle_terrain_a" terrain="Plain" forest_density="Low" map_indices="1" />
\t<Scene id="battle_terrain_b" terrain="Desert" map_indices="2">
\t\t<TerrainTypes>
\t\t\t<TerrainType name="Canyon" />
\t\t</TerrainTypes>
\t</Scene>
\t<!--<Scene id="battle_terrain_c" terrain="Steppe" />-->
</SPBattleScenes>
"""

T = "battle_terrain_"
CUSTOM = {"taom_test_battle_forceatmo": ("Swamp", "Low"), bbs.CATCH_ALL: ("Plain", None)}


def vanilla_scenes():
    return {s.get("id"): s for s in ET.fromstring(VANILLA).findall("Scene")}


def render(regions):
    return bbs.render(regions, vanilla_scenes(), CUSTOM)


def scenes_of(text):
    return {s.get("id"): s for s in ET.fromstring(text.encode("utf-8")).findall("Scene")}


def indices(scene):
    return [int(tok) for tok in scene.get("map_indices").replace(" ", "").split(",")]


class GuardTests(unittest.TestCase):
    def test_empty_cell_list_refused(self):
        with self.assertRaisesRegex(ValueError, "at least one cell"):
            render([("Stub", [], [T + "a"])])

    def test_cell_outside_0_255_refused(self):
        with self.assertRaisesRegex(ValueError, "0-255"):
            render([("Typo", [1100], [T + "a"])])

    def test_double_hyphen_in_region_name_refused(self):
        with self.assertRaisesRegex(ValueError, "--"):
            render([("Gondor -- Pelennor", [5], [T + "a"])])

    def test_non_integer_cell_refused(self):
        for cell in ("110", True):  # int.Parse("True") would throw at campaign start
            with self.subTest(cell=cell), self.assertRaisesRegex(ValueError, "0-255"):
                render([("Typo", [cell], [T + "a"])])

    def test_output_that_does_not_parse_is_refused(self):
        custom = {**CUSTOM, "a&b": ("Plain", None)}  # passes the unknown-id check, breaks the XML
        with self.assertRaisesRegex(ValueError, "does not parse"):
            bbs.render([("Region", [5], ["a&b"])], vanilla_scenes(), custom)

    def test_unknown_or_commented_out_vanilla_id_refused(self):
        for sid in (T + "zz", T + "c"):
            with self.subTest(sid=sid), self.assertRaisesRegex(ValueError, sid):
                render([("Region", [5], [sid])])

    def test_no_scene_is_ever_written_with_empty_map_indices(self):
        text = render([("Everything", list(range(256)), [T + "a"])])
        self.assertNotIn('map_indices=""', text)
        self.assertNotIn(bbs.CATCH_ALL, scenes_of(text))


class OutputTests(unittest.TestCase):
    REGIONS = [
        ("North", [1, 2, 3], [T + "a", "taom_test_battle_forceatmo"]),
        ("South", [3, 4], [T + "b"]),
    ]

    def test_union_and_catch_all_cover_exactly_0_255(self):
        scenes = scenes_of(render(self.REGIONS))
        covered = [i for s in scenes.values() for i in indices(s)]
        self.assertEqual(set(covered), set(range(256)))
        self.assertEqual(indices(scenes[T + "b"]), [3, 4])
        self.assertEqual(set(indices(scenes[bbs.CATCH_ALL])), set(range(256)) - {1, 2, 3, 4})

    def test_catch_all_listed_in_a_region_is_emitted_once(self):
        text = render([("Rocks", [7], [bbs.CATCH_ALL])])
        self.assertEqual(text.count(f'id="{bbs.CATCH_ALL}"'), 1)
        self.assertIn(7, indices(scenes_of(text)[bbs.CATCH_ALL]))

    def test_vanilla_attributes_and_terrain_types_are_copied(self):
        scenes = scenes_of(render(self.REGIONS))
        self.assertEqual(scenes[T + "b"].get("terrain"), "Desert")
        self.assertEqual([t.get("name") for t in scenes[T + "b"].iter("TerrainType")], ["Canyon"])
        self.assertEqual(scenes["taom_test_battle_forceatmo"].get("terrain"), "Swamp")
        self.assertIsNone(scenes[bbs.CATCH_ALL].get("forest_density"))

    def test_bytes_no_bom_crlf_root_first(self):
        data = render(self.REGIONS).encode("utf-8")
        self.assertFalse(data.startswith(b"\xef\xbb\xbf"))
        self.assertEqual(data.count(b"\n"), data.count(b"\r\n"))
        lines = data.decode("utf-8").split("\r\n")
        self.assertTrue(lines[0].startswith("<?xml"))
        self.assertEqual(lines[1], "<SPBattleScenes>")  # the engine takes ChildNodes[1] as the root

    def test_two_renders_are_byte_identical(self):
        self.assertEqual(render(self.REGIONS), render(self.REGIONS))

    def test_overlaps_lists_cells_claimed_by_two_regions(self):
        self.assertEqual(bbs.overlaps(self.REGIONS), {3: ["North", "South"]})


class SceneFolderTests(unittest.TestCase):
    def test_missing_scene_folder_reported_case_insensitively(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "SandBoxCore" / "SceneObj" / "Battle_Terrain_A").mkdir(parents=True)
            missing = bbs.missing_scene_folders([T + "a", T + "b"], Path(tmp))
        self.assertEqual(missing, [T + "b"])


class ModeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        root = Path(self.tmp.name)
        self.vanilla_path = root / "sp_battle_scenes_vanilla.xml"
        self.vanilla_path.write_text(VANILLA, encoding="utf-8")
        self.out = root / "sp_battle_scenes.xml"
        self.scene_dirs = [root / "SandBoxCore" / "SceneObj" / sid for sid in (T + "a", T + "b", bbs.CATCH_ALL)]
        for d in self.scene_dirs:
            d.mkdir(parents=True)
        self.saved = (bbs.VANILLA_XML, bbs.OUT, bbs.REGIONS, bbs.CUSTOM, bbs.MODULES)
        bbs.VANILLA_XML, bbs.OUT, bbs.MODULES = self.vanilla_path, self.out, root
        bbs.REGIONS, bbs.CUSTOM = [("North", [1, 2], [T + "a"])], CUSTOM

    def tearDown(self):
        bbs.VANILLA_XML, bbs.OUT, bbs.REGIONS, bbs.CUSTOM, bbs.MODULES = self.saved
        self.tmp.cleanup()

    def run_main(self, *argv):
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            return bbs.main(list(argv))

    def stdout_of(self, *argv):
        out = io.StringIO()
        with redirect_stdout(out), redirect_stderr(io.StringIO()):
            bbs.main(list(argv))
        return out.getvalue()

    def test_dry_run_writes_nothing(self):
        self.assertEqual(self.run_main(), 0)
        self.assertEqual(self.run_main("--dry-run"), 0)
        self.assertFalse(self.out.exists())

    def test_apply_then_check_passes_and_a_hand_edit_fails_it(self):
        self.assertEqual(self.run_main("--check"), 1)  # no file yet
        self.assertEqual(self.run_main("--apply"), 0)
        self.assertEqual(self.run_main("--check"), 0)
        self.out.write_bytes(self.out.read_bytes().replace(b'map_indices="1, 2"', b'map_indices="1"'))
        self.assertEqual(self.run_main("--check"), 1)

    def test_check_ignores_line_ending_conversion(self):
        self.run_main("--apply")
        self.out.write_bytes(self.out.read_bytes().replace(b"\r\n", b"\n"))
        self.assertEqual(self.run_main("--check"), 0)

    def test_modes_are_mutually_exclusive(self):
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit) as cm:
                bbs.main(["--dry-run", "--apply"])
        self.assertEqual(cm.exception.code, 2)
        self.assertFalse(self.out.exists())

    def test_bad_table_exits_1_and_writes_nothing(self):
        bbs.REGIONS = [("Stub", [], [T + "a"])]
        self.assertEqual(self.run_main("--apply"), 1)
        self.assertFalse(self.out.exists())

    def test_check_fails_when_a_scene_folder_is_missing(self):
        self.run_main("--apply")
        self.assertEqual(self.run_main("--check"), 0)
        self.scene_dirs[0].rmdir()  # battle_terrain_a, the region's only scene
        self.assertEqual(self.run_main("--check"), 1)

    def test_shared_cell_line_shows_each_regions_scene_count(self):
        bbs.REGIONS = OutputTests.REGIONS
        self.assertIn("shared cell 3: North (2) + South (1)", self.stdout_of())


class RegionTableTests(unittest.TestCase):
    """The shipped REGIONS table, checked without a game install."""

    def test_shipped_table_passes_the_guards_and_covers_every_painted_cell(self):
        cells_of, _ = bbs.assign(bbs.REGIONS)
        claimed = set().union(*(c for sid, c in cells_of.items() if sid != bbs.CATCH_ALL))
        self.assertTrue(set(range(1, 181)) | {255} <= claimed)

    def test_mordor_scenes_stay_off_harad_and_khand_cells(self):
        # Review 2026-10-03: rng(161, 179) put Mordor scenes on 176-178, which hold Harad and Khand
        # castles and villages and no Mordor settlement.
        cells_of, _ = bbs.assign(bbs.REGIONS)
        for sid in bbs.MORDOR:
            self.assertFalse(cells_of[sid] & {176, 177, 178}, sid)

    def test_earlier_lists_mistakes_stay_fixed(self):
        # Before 2026-10-03 the Rohan scene sat on Minas Tirith and Anorien, and the Mordor scenes on
        # Gondor, Ithilien, Harad and Khand cells (worldmap-battle-scene-grid.md, "Pitfalls").
        cells_of, _ = bbs.assign(bbs.REGIONS)
        self.assertFalse(cells_of["taom_rohan_battle_001_forceatmo"] & {85, 86, 90, 91, 92})
        for sid in bbs.MORDOR:
            self.assertFalse(cells_of[sid] & {85, 94, 109, 127, 128, 131, 152, 153}, sid)

    def test_committed_xml_matches_the_table_and_parses_like_the_engine(self):
        # --check needs the install for vanilla's attributes; the cell map does not, so CI holds "edit
        # REGIONS, never the XML". The engine int.Parses every token after Replace(" ", "").Split(','),
        # and counts <Scene> elements, so a duplicate id would double that scene's odds.
        data = bbs.OUT.read_bytes()
        self.assertEqual(data.decode("utf-8").splitlines()[1], "<SPBattleScenes>")
        committed = {}
        for scene in ET.fromstring(data).findall("Scene"):
            sid = scene.get("id")
            tokens = scene.get("map_indices", "").replace(" ", "").split(",")
            self.assertTrue(all(t.isdigit() and int(t) <= 255 for t in tokens), sid)
            self.assertNotIn(sid, committed, "duplicate Scene id")
            committed[sid] = {int(t) for t in tokens}
        expected, _ = bbs.assign(bbs.REGIONS)
        self.assertEqual(committed, dict(expected))


if __name__ == "__main__":
    unittest.main()
