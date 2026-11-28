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
            self.assertEqual(len(entry), 5, entry)
            self.assertTrue(entry[4].strip(), entry)


class NotRun(Base):
    def test_a_file_that_does_not_parse_is_not_run(self):
        code, _, err = self.run_tool("<scene><terrain")
        self.assertEqual(code, 2)
        self.assertIn("NOT RUN", err)

    def test_a_missing_file_is_not_run(self):
        err = io.StringIO()
        with redirect_stderr(err):
            self.assertEqual(ctl.main(["no/such/scene.xscene"]), 2)

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
