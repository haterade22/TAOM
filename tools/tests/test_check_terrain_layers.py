#!/usr/bin/env python3
"""tools/check_terrain_layers.py: no terrain node may use only flora layers (or no layer at all).

Such a node makes the engine compile a terrain shader with no layers (normalize(0), X4008) on every
load of the scene (Helm's Deep node 0,3, 2026-11-28). Every test builds its own scene file; one more
reads the live install and skips without it."""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_terrain_layers as ctl  # noqa: E402


def layer(name: str, flora=(), seasons=ctl.SEASONS) -> str:
    """A layer; `flora` lists the seasons whose entry is a flora layer."""
    body = "".join(f'<{s} is_enabled="true" name="{name}" is_flora_layer="{"true" if s in flora else "false"}"'
                   f' is_mesh_blend_layer="false"><textures/></{s}>' for s in seasons)
    return f'<layer name="{name}">{body}</layer>'


def node(idx: int, idy: int, summer: int, fall=None, winter=None, spring=None) -> str:
    masks = {"summer": summer, "fall": summer if fall is None else fall,
             "winter": summer if winter is None else winter, "spring": summer if spring is None else spring}
    attrs = " ".join(f'layer_is_used_mask_{s}="{m}"' for s, m in masks.items())
    return f'<node idx="{idx}" idy="{idy}" {attrs} layer_is_used_mask="true"><data/></node>'


def scene(layers: str, nodes: str, size="150.000") -> str:
    return (f'<scene><terrain enabled="true" node_size="{size}"><layers version="1">{layers}</layers>'
            f'<nodes>{nodes}</nodes></terrain></scene>')


class Base(unittest.TestCase):
    def run_tool(self, xml: str, folder: str = "some_scene", extra=()):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / folder / "scene.xscene"
            path.parent.mkdir()
            path.write_text(xml, encoding="utf-8")
            out, err = io.StringIO(), io.StringIO()
            with redirect_stdout(out), redirect_stderr(err):
                code = ctl.main([str(path), *extra])
            return code, out.getvalue(), err.getvalue()


class FloraOnlyNodes(Base):
    TWO = layer("default", flora=("summer",)) + layer("rock")

    def test_a_node_using_only_a_flora_layer_fails_with_its_square(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 1)))
        self.assertEqual(code, 1)
        self.assertIn("FAIL some_scene node (0,3) summer mask 1", out)
        self.assertIn("x 0-150 y 450-600", out)

    def test_a_node_that_also_uses_a_non_flora_layer_passes(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 3)))
        self.assertEqual(code, 0, out)

    def test_the_flag_is_judged_per_season(self):
        """Layer 0 is flora in summer only: summer fails, and fall, winter, spring do not."""
        code, out, _ = self.run_tool(scene(self.TWO, node(1, 2, 1)))
        self.assertEqual(code, 1)
        self.assertEqual(out.count("FAIL"), 1)
        self.assertIn("summer", out)
        for season in ("fall", "winter", "spring"):
            self.assertNotIn(f"{season} mask", out)

    def test_the_mask_is_judged_per_season(self):
        """Summer uses the non-flora layer, fall uses layer 0 alone."""
        flora_all = layer("default", flora=ctl.SEASONS) + layer("rock")
        code, out, _ = self.run_tool(scene(flora_all, node(0, 0, 3, fall=1)))
        self.assertEqual(code, 1)
        self.assertIn("fall mask 1", out)
        self.assertNotIn("summer mask", out)

    def test_a_mask_of_zero_fails(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(2, 1, 0)))
        self.assertEqual(code, 1)
        self.assertIn("no layer is used", out)

    def test_two_flora_layers_and_nothing_else_fail(self):
        layers = layer("a", flora=("summer",)) + layer("b", flora=("summer",)) + layer("c")
        code, out, _ = self.run_tool(scene(layers, node(0, 0, 3)))
        self.assertEqual(code, 1)
        self.assertIn("0, 1", out)

    def test_a_missing_node_size_still_fails_without_a_square(self):
        xml = scene(self.TWO, node(0, 3, 1)).replace(' node_size="150.000"', "")
        code, out, _ = self.run_tool(xml)
        self.assertEqual(code, 1)
        self.assertNotIn("world square", out)

    def test_a_layer_elsewhere_in_the_file_is_not_a_terrain_layer(self):
        xml = scene(self.TWO, node(0, 0, 3)).replace("<scene>", "<scene><layer name='x'/>")
        code, out, _ = self.run_tool(xml)
        self.assertEqual(code, 0, out)


class KnownFaults(Base):
    TWO = layer("default", flora=("summer",)) + layer("rock")

    def test_the_helms_deep_node_is_tolerated_and_named(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 1)), folder="taom_rohan_castle_helms_deep_forceatmo")
        self.assertEqual(code, 0, out)
        self.assertIn("KNOWN taom_rohan_castle_helms_deep_forceatmo node (0,3)", out)

    def test_another_node_of_the_same_scene_still_fails(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 1) + node(0, 4, 1)),
                                     folder="taom_rohan_castle_helms_deep_forceatmo")
        self.assertEqual(code, 1)
        self.assertIn("FAIL taom_rohan_castle_helms_deep_forceatmo node (0,4)", out)

    def test_the_same_node_in_another_scene_still_fails(self):
        code, _, _ = self.run_tool(scene(self.TWO, node(0, 3, 1)), folder="other_scene")
        self.assertEqual(code, 1)

    def test_no_known_reports_the_tolerated_nodes_as_faults(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 1)), folder="taom_rohan_castle_helms_deep_forceatmo",
                                     extra=("--no-known",))
        self.assertEqual(code, 1)
        self.assertIn("FAIL", out)

    def test_every_entry_states_a_reason(self):
        for entry in ctl.KNOWN_FAULTS:
            self.assertEqual(len(entry), 6, entry)
            self.assertTrue(entry[5].strip(), entry)

    def test_the_known_entries_are_keyed_on_literal_nodes_with_no_wildcard(self):
        dwarves = sorted((e[1], e[2]) for e in ctl.KNOWN_FAULTS if e[0] == "taom_dwarves_battle_001_forceatmo")
        self.assertEqual(dwarves, sorted([(0, 3), (0, 9), (2, 0), (2, 13), (3, 13), (4, 12), (5, 0), (9, 0),
                                          (12, 11), (12, 12), (13, 2), (13, 5), (13, 8), (13, 11), (13, 12)]))
        for entry in ctl.KNOWN_FAULTS:
            self.assertNotIn(None, entry)
            self.assertEqual(entry[4], 1)

    DWARVES = "taom_dwarves_battle_001_forceatmo"
    FALL = layer("rock_cliff", flora=("fall",)) + layer("rock")

    def test_a_dwarves_node_outside_the_fifteen_fails(self):
        code, out, _ = self.run_tool(scene(self.FALL, node(7, 7, 1)), folder=self.DWARVES)
        self.assertEqual(code, 1)
        self.assertIn("FAIL taom_dwarves_battle_001_forceatmo node (7,7) fall mask 1", out)

    def test_a_listed_dwarves_node_is_known_in_fall_with_mask_1(self):
        code, out, _ = self.run_tool(scene(self.FALL, node(0, 3, 1)), folder=self.DWARVES)
        self.assertEqual(code, 0, out)
        self.assertIn("KNOWN taom_dwarves_battle_001_forceatmo node (0,3) fall mask 1", out)

    def test_helms_deep_flora_only_in_fall_is_a_new_fault(self):
        fall_flora = layer("default", flora=("fall",)) + layer("rock")
        code, out, _ = self.run_tool(scene(fall_flora, node(0, 3, 1)),
                                     folder="taom_rohan_castle_helms_deep_forceatmo")
        self.assertEqual(code, 1)
        self.assertIn("FAIL taom_rohan_castle_helms_deep_forceatmo node (0,3) fall mask 1", out)

    def test_a_known_node_with_another_mask_is_a_new_fault(self):
        two_flora = layer("default", flora=("summer",)) + layer("b", flora=("summer",)) + layer("rock")
        code, out, _ = self.run_tool(scene(two_flora, node(0, 3, 3)),
                                     folder="taom_rohan_castle_helms_deep_forceatmo")
        self.assertEqual(code, 1)
        self.assertIn("FAIL", out)

    def test_an_entry_that_matches_nothing_in_a_read_scene_is_stale(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 3)), folder="taom_rohan_castle_helms_deep_forceatmo")
        self.assertEqual(code, 0, out)
        self.assertIn("STALE known fault taom_rohan_castle_helms_deep_forceatmo node (0,3) summer mask 1", out)
        self.assertIn("remove it", out)

    def test_an_entry_for_a_scene_that_was_not_read_is_not_stale(self):
        code, out, _ = self.run_tool(scene(self.TWO, node(0, 3, 3)), folder="other_scene")
        self.assertEqual(code, 0, out)
        self.assertNotIn("STALE", out)


class NotRun(Base):
    def test_a_file_that_does_not_parse_is_not_run(self):
        code, _, err = self.run_tool("<scene><terrain")
        self.assertEqual(code, 2)
        self.assertIn("ERROR:", err)

    def test_a_missing_file_is_not_run(self):
        err = io.StringIO()
        with redirect_stderr(err):
            self.assertEqual(ctl.main(["no/such/scene.xscene"]), 2)
        self.assertIn("ERROR:", err.getvalue())

    def test_a_file_not_named_scene_xscene_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "tools" / "check_terrain_layers.py"
            path.parent.mkdir()
            path.write_text("print('x')", encoding="utf-8")
            out, err = io.StringIO(), io.StringIO()
            with redirect_stdout(out), redirect_stderr(err):
                code = ctl.main([str(path)])
        self.assertEqual(code, 2)
        self.assertIn("ERROR:", err.getvalue())
        self.assertIn("scene.xscene", err.getvalue())

    def test_one_broken_file_does_not_hide_a_fault_in_another(self):
        with tempfile.TemporaryDirectory() as tmp:
            good = Path(tmp) / "a_scene" / "scene.xscene"
            bad = Path(tmp) / "b_scene" / "scene.xscene"
            for p, xml in ((good, scene(FloraOnlyNodes.TWO, node(0, 3, 1))), (bad, "<scene><terrain")):
                p.parent.mkdir()
                p.write_text(xml, encoding="utf-8")
            out, err = io.StringIO(), io.StringIO()
            with redirect_stdout(out), redirect_stderr(err):
                code = ctl.main([str(bad), str(good)])
        self.assertEqual(code, 1)
        self.assertIn("FAIL a_scene node (0,3)", out.getvalue())
        self.assertIn(f"ERROR: {bad}", err.getvalue())

    def test_a_terrain_with_nodes_but_no_mask_is_not_run(self):
        xml = ('<scene><terrain node_size="150"><layers version="1"></layers>'
               '<nodes><node idx="0" idy="0"><data/></node></nodes></terrain></scene>')
        code, _, err = self.run_tool(xml)
        self.assertEqual(code, 2)
        self.assertIn("ERROR:", err)
        self.assertIn("no layer_is_used_mask", err)

    def test_the_summary_counts_nodes_and_masks(self):
        code, out, _ = self.run_tool(scene(FloraOnlyNodes.TWO, node(0, 0, 3) + node(1, 0, 3)))
        self.assertEqual(code, 0, out)
        self.assertIn("1 scene(s) read, 2 node(s), 8 mask(s) judged", out)

    def test_a_flora_only_node_after_many_entities_is_still_found(self):
        """The stream frees elements as it goes; the layers and nodes must survive that."""
        entities = "<entities>" + "".join(f'<entity name="e{i}"><x/></entity>' for i in range(200)) + "</entities>"
        xml = scene(FloraOnlyNodes.TWO, node(0, 3, 1)).replace("<terrain", entities + "<terrain", 1)
        code, out, _ = self.run_tool(xml)
        self.assertEqual(code, 1)
        self.assertIn("node (0,3) summer mask 1", out)

    def test_a_missing_install_is_not_run(self):
        with tempfile.TemporaryDirectory() as tmp:
            err = io.StringIO()
            with redirect_stderr(err):
                self.assertEqual(ctl.main(["--game-dir", str(Path(tmp) / "none")]), 2)
            self.assertIn("NOT RUN", err.getvalue())

    def test_an_install_with_no_scene_is_not_run(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "Modules" / "TAOM_Map" / "SceneObj").mkdir(parents=True)
            err = io.StringIO()
            with redirect_stderr(err):
                self.assertEqual(ctl.main(["--game-dir", tmp]), 2)


class Live(unittest.TestCase):
    def test_the_live_scenes_hold_no_new_fault(self):
        from _gamedir import DEFAULT_GAME_ROOT, game_dir
        game = Path(game_dir(DEFAULT_GAME_ROOT))
        if not (game / "Modules").is_dir() or not ctl.scene_files(game, ["TAOM_Map"]):
            self.skipTest("no live TAOM_Map scenes")
        out = io.StringIO()
        with redirect_stdout(out):
            code = ctl.main([])
        self.assertEqual(code, 0, out.getvalue())


if __name__ == "__main__":
    unittest.main()
