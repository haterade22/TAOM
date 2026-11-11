#!/usr/bin/env python3
"""tools/check_siege_wall_scripts.py: a siege wall's solid and broken child each carry a WallSegment script.

The base game's siege set-up calls GetFirstScriptOfType<WallSegment>() on both children with no null
check, so a breach mesh that an art pass replaced without the script throws at every siege start on
that scene (Helm's Deep, 2026-11-10). Every test builds its own scene files; one more reads the live
TAOM_Map and skips without the install."""
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_siege_wall_scripts as csw  # noqa: E402

WALL_SCRIPT = '<scripts><script name="WallSegment"><variables><variable name="SideTag" value="{side}"/></variables></script></scripts>'
CHILD_SCRIPT = '<scripts><script name="WallSegment"><variables><variable name="SideTag" value=""/></variables></script></scripts>'
OTHER_SCRIPT = '<scripts><script name="SynchedMissionObject"/></scripts>'


def child(name: str, tag: str, script: bool = True, inner: str = "", scripts: str = "") -> str:
    block = scripts or (CHILD_SCRIPT if script else "")
    return (f'<game_entity name="{name}"><tags><tag name="{tag}"/></tags>{block}'
            f"<children>{inner}</children></game_entity>")


def wall(name: str, side: str, children: str) -> str:
    return (f'<game_entity name="{name}">{WALL_SCRIPT.format(side=side)}'
            f"<children>{children}</children></game_entity>")


def scene(*walls: str) -> str:
    return f'<scene name="s"><entities>{"".join(walls)}</entities></scene>'


GOOD_LEFT = wall("wall_left", "left", child("solid_l", "solid_child") + child("broken_l", "broken_child"))
GOOD_RIGHT = wall("wall_right", "right", child("solid_r", "solid_child") + child("broken_r", "broken_child"))
GOOD_SCENE = scene(GOOD_LEFT, GOOD_RIGHT)


def right_wall(children: str, side: str = "right", name: str = "wall_right") -> str:
    """A scene with a good left wall and the right wall under test."""
    return scene(GOOD_LEFT, wall(name, side, children))


class SiegeWallScriptTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.scenes = Path(self._tmp.name) / "SceneObj"
        self.scenes.mkdir()

    def tearDown(self):
        self._tmp.cleanup()

    def write(self, scene_name: str, text: str) -> Path:
        folder = self.scenes / scene_name
        folder.mkdir()
        path = folder / "scene.xscene"
        path.write_text(text, encoding="utf-8")
        return path

    def run_tool(self, *args: str, folder: bool = True):
        out, err = io.StringIO(), io.StringIO()
        argv = (["--scenes", str(self.scenes)] if folder else []) + list(args)
        with redirect_stdout(out), redirect_stderr(err):
            code = csw.main(argv)
        return code, out.getvalue(), err.getvalue()

    def test_two_walls_with_both_child_scripts_pass(self):
        self.write("castle_a", GOOD_SCENE)
        code, out, _ = self.run_tool()
        self.assertEqual(0, code)
        self.assertIn("PASS: 1 scene(s) read, 2 sided wall(s) with a breach, 0 fault(s)", out)

    def test_a_broken_child_without_the_script_fails_and_is_named(self):
        self.write("castle_a", GOOD_SCENE)
        self.write("helms", right_wall(child("solid_r", "solid_child") + child("sm_breach_right", "broken_child", script=False)))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("helms: wall 'wall_right' (side right): broken_child 'sm_breach_right' has no WallSegment script", out)
        self.assertIn("FAIL: 2 scene(s) read, 4 sided wall(s) with a breach, 1 fault(s)", out)
        self.assertNotIn("castle_a:", out)

    def test_a_child_with_only_another_script_fails(self):
        # The engine asks for a WallSegment script by type; another script on the child does not answer it.
        self.write("castle_a", right_wall(child("solid_r", "solid_child")
                                          + child("broken_r", "broken_child", scripts=OTHER_SCRIPT)))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("broken_child 'broken_r' has no WallSegment script", out)

    def test_a_solid_child_without_the_script_fails(self):
        self.write("castle_a", right_wall(child("solid_r", "solid_child", script=False) + child("broken_r", "broken_child")))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("castle_a: wall 'wall_right' (side right): solid_child 'solid_r' has no WallSegment script", out)

    def test_a_wall_with_no_solid_child_fails(self):
        self.write("castle_a", right_wall(child("broken_r", "broken_child")))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("castle_a: wall 'wall_right' (side right): no child tagged solid_child", out)

    def test_middle_is_a_side(self):
        self.write("castle_a", right_wall(child("solid_m", "solid_child") + child("broken_m", "broken_child", script=False),
                                          side="middle", name="wall_mid"))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("wall 'wall_mid' (side middle)", out)

    def test_a_wall_without_a_side_is_not_judged(self):
        # ArrangeDestructedMeshes takes only walls whose DefenseSide is set, and OnInit sets it from exactly
        # "left", "middle" or "right": the switch is case-sensitive.
        for side in ("", "north", "Right"):
            with self.subTest(side=side):
                name = f"castle_{side or 'none'}"
                self.write(name, scene(GOOD_LEFT, GOOD_RIGHT,
                                       wall("piece", side, child("s", "solid_child", script=False)
                                            + child("b", "broken_child", script=False))))
        code, out, _ = self.run_tool()
        self.assertEqual(0, code)
        self.assertIn("PASS: 3 scene(s) read, 6 sided wall(s) with a breach, 0 fault(s)", out)

    def test_a_sided_wall_without_a_breach_child_is_not_judged(self):
        self.write("castle_a", scene(GOOD_LEFT, GOOD_RIGHT, wall("gatehouse", "middle", child("s", "solid_child", script=False))))
        code, out, _ = self.run_tool()
        self.assertEqual(0, code)
        self.assertIn("2 sided wall(s) with a breach", out)

    def test_only_a_direct_child_counts(self):
        # GetFirstChildEntityWithTag and the set-up's filter both read GetChildren(), which is one level deep.
        nested_breach = child("holder", "reference", script=False, inner=child("deep", "broken_child", script=False))
        self.write("castle_a", scene(GOOD_LEFT, GOOD_RIGHT, wall("extra", "middle", child("solid_x", "solid_child") + nested_breach)))
        script_below = child("broken_r", "broken_child", script=False, inner=child("deep", "reference"))
        self.write("castle_b", right_wall(child("solid_r", "solid_child") + script_below))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertNotIn("castle_a:", out)
        self.assertIn("castle_b: wall 'wall_right' (side right): broken_child 'broken_r' has no WallSegment script", out)

    def test_the_first_tagged_child_is_the_one_the_engine_reads(self):
        self.write("castle_a", right_wall(child("solid_r", "solid_child")
                                          + child("old_breach", "broken_child", script=False)
                                          + child("new_breach", "broken_child")))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("broken_child 'old_breach' has no WallSegment script", out)

    def test_a_nested_wall_is_judged_too(self):
        inner_wall = wall("inner", "right", child("solid_i", "solid_child") + child("broken_i", "broken_child", script=False))
        self.write("castle_a", scene(GOOD_LEFT, f'<game_entity name="group"><children>{inner_wall}</children></game_entity>'))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("wall 'inner'", out)

    def test_an_odd_number_of_walls_fails(self):
        # The set-up takes the walls two per level (one call per wall hit-point entry, and the campaign
        # and The Long Defeat pass two), so a wall with no pair throws in the same loop.
        self.write("one_wall", scene(GOOD_RIGHT))
        self.write("three_walls", scene(GOOD_LEFT, GOOD_RIGHT, GOOD_RIGHT.replace("wall_right", "wall_right_2")))
        self.write("six_walls", scene(*[GOOD_LEFT, GOOD_RIGHT] * 3))
        code, out, _ = self.run_tool()
        self.assertEqual(1, code)
        self.assertIn("one_wall: 1 sided wall(s) with a breach, an odd number", out)
        self.assertIn("three_walls: 3 sided wall(s) with a breach, an odd number", out)
        self.assertNotIn("six_walls:", out)
        self.assertIn("FAIL: 3 scene(s) read, 10 sided wall(s) with a breach, 2 fault(s)", out)

    def test_a_missing_folder_exits_2(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = csw.main(["--scenes", str(self.scenes / "nowhere")])
        self.assertEqual(2, code)
        self.assertIn("ERROR: scene folder not found", err.getvalue())
        self.assertNotIn("PASS", out.getvalue())

    def test_a_folder_with_no_scene_exits_2(self):
        code, out, err = self.run_tool()
        self.assertEqual(2, code)
        self.assertIn("ERROR: no scene.xscene under", err)
        self.assertNotIn("PASS", out)

    def test_an_unreadable_scene_exits_2_and_is_named(self):
        self.write("castle_a", GOOD_SCENE)
        self.write("broken_file", "<scene><entities>")
        code, out, err = self.run_tool()
        self.assertEqual(2, code)
        self.assertIn("ERROR: cannot read broken_file", err)
        self.assertNotIn("PASS", out)

    def test_a_fault_and_an_unreadable_scene_still_names_the_fault(self):
        self.write("helms", right_wall(child("solid_r", "solid_child") + child("b", "broken_child", script=False)))
        self.write("broken_file", "<scene>")
        code, out, _ = self.run_tool()
        self.assertEqual(2, code)
        self.assertIn("helms: wall 'wall_right'", out)

    def test_two_scene_folders_are_both_read(self):
        other = Path(self._tmp.name) / "Other" / "SceneObj"
        (other / "castle_b").mkdir(parents=True)
        (other / "castle_b" / "scene.xscene").write_text(GOOD_SCENE, encoding="utf-8")
        self.write("castle_a", GOOD_SCENE)
        code, out, _ = self.run_tool("--scenes", str(other))
        self.assertEqual(0, code)
        self.assertIn("PASS: 2 scene(s) read, 4 sided wall(s) with a breach, 0 fault(s)", out)

    def test_a_named_scene_file_is_the_one_judged(self):
        # data_gates.py hands the gate the scene files the author named: a staged drop or a channel copy
        # must be judged itself, never the live install in its place.
        faulty = self.write("helms", right_wall(child("solid_r", "solid_child") + child("b", "broken_child", script=False)))
        good = self.write("castle_a", GOOD_SCENE)
        code, out, _ = self.run_tool(str(faulty), folder=False)
        self.assertEqual(1, code)
        self.assertIn("helms: wall 'wall_right'", out)
        self.assertIn("FAIL: 1 scene(s) read", out)
        code, out, _ = self.run_tool(str(good), folder=False)
        self.assertEqual(0, code)
        self.assertIn("PASS: 1 scene(s) read, 2 sided wall(s) with a breach, 0 fault(s)", out)

    def test_a_named_file_that_is_missing_or_not_a_scene_exits_2(self):
        good = self.write("castle_a", GOOD_SCENE)
        other = good.with_name("terrain.bin")
        other.write_text("x", encoding="utf-8")
        for path, text in ((self.scenes / "gone" / "scene.xscene", "ERROR: scene file not found"),
                           (other, "ERROR: not a scene.xscene")):
            with self.subTest(path=path.name):
                code, out, err = self.run_tool(str(good), str(path), folder=False)
                self.assertEqual(2, code)
                self.assertIn(text, err)
                self.assertNotIn("PASS", out)


class LiveInstallTests(unittest.TestCase):
    """The live TAOM_Map is unversioned: this is the only run of the gate that an art drop cannot skip."""

    @unittest.skipUnless(csw.DEFAULT_SCENES.is_dir(), "no live TAOM_Map SceneObj folder on this machine")
    def test_every_live_taom_map_scene_passes(self):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = csw.main([])
        self.assertEqual(0, code, out.getvalue() + err.getvalue())


if __name__ == "__main__":
    unittest.main()
