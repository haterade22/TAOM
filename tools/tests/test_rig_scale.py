#!/usr/bin/env python3
"""Unit tests for tools/blender/rig_scale.py (no Blender needed: bpy is imported lazily).

Run:  python -m unittest tools.tests.test_rig_scale
"""
import importlib.util
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location("rig_scale", os.path.join(TOOLS, "blender", "rig_scale.py"))
rs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(rs)


class LengthChannelTests(unittest.TestCase):
    def test_a_pose_bone_location_is_a_length(self):
        self.assertTrue(rs.is_length_channel('pose.bones["L Thigh"].location'))

    def test_the_object_location_is_a_length(self):
        self.assertTrue(rs.is_length_channel("location"))

    def test_rotations_and_scales_are_not_lengths(self):
        for path in ('pose.bones["Pelvis"].rotation_quaternion', 'pose.bones["Pelvis"].rotation_euler',
                     'pose.bones["Pelvis"].scale', "rotation_euler", "rotation_quaternion", "scale"):
            self.assertFalse(rs.is_length_channel(path), path)

    def test_a_custom_property_named_location_is_not_a_length(self):
        self.assertFalse(rs.is_length_channel('pose.bones["Pelvis"]["location"]'))
        self.assertFalse(rs.is_length_channel("delta_location"))


class TailOffsetTests(unittest.TestCase):
    """The rig moves toward its tail so the wolf's seat lands where the warg's rider clip puts the rider."""

    def test_a_rig_facing_minus_y_moves_toward_plus_y(self):
        got = rs.tail_offset((0.0, 0.0, 0.65), (0.0, -0.8, 0.9), 0.52)
        self.assertEqual([round(v, 6) for v in got], [0.0, 0.52, 0.0])

    def test_a_rig_facing_plus_x_moves_toward_minus_x(self):
        got = rs.tail_offset((1.0, 2.0, 0.0), (3.0, 2.0, 5.0), 0.5)
        self.assertEqual([round(v, 6) for v in got], [-0.5, 0.0, 0.0])

    def test_the_height_never_changes(self):
        got = rs.tail_offset((0.0, 0.0, 0.0), (0.0, -1.0, 1.0), 1.0)
        self.assertEqual([round(v, 6) for v in got], [0.0, 1.0, 0.0])

    def test_zero_distance_is_no_move(self):
        self.assertEqual(list(rs.tail_offset((0.0, 0.0, 0.0), (0.0, -1.0, 0.0), 0.0)), [0.0, 0.0, 0.0])

    def test_a_vertical_spine_is_refused(self):
        with self.assertRaises(ValueError):
            rs.tail_offset((0.0, 0.0, 0.0), (0.0, 0.0, 1.0), 0.5)


class _Key:
    def __init__(self, x, y):
        self.co, self.handle_left, self.handle_right = [x, y], [x - 1, y], [x + 1, y]


class _Curve:
    def __init__(self, path, index, values):
        self.data_path, self.array_index = path, index
        self.keyframe_points = [_Key(i, v) for i, v in enumerate(values)]
        self.updated = False

    def update(self):
        self.updated = True


class _Action:
    def __init__(self, curves):
        self.fcurves = curves


class _Object:
    def __init__(self):
        self.location = [1.0, 2.0, 0.65]


class MoveObjectTests(unittest.TestCase):
    """An animation file moves its armature object, so the move stays constant in model space while the object turns."""

    def test_the_location_and_its_keys_move_and_nothing_else(self):
        obj = _Object()
        loc_y = _Curve("location", 1, [0.0, 0.5])
        rot = _Curve("rotation_euler", 1, [0.1, 0.2])
        bone = _Curve('pose.bones["Pelvis"].location', 1, [0.3, 0.4])
        moved = rs.move_object(obj, (0.0, 0.52, 0.0), [_Action([loc_y, rot, bone])])
        self.assertEqual(obj.location, [1.0, 2.52, 0.65])
        self.assertEqual([round(k.co[1], 6) for k in loc_y.keyframe_points], [0.52, 1.02])
        self.assertEqual([round(k.handle_left[1], 6) for k in loc_y.keyframe_points], [0.52, 1.02])
        self.assertEqual([round(k.handle_right[1], 6) for k in loc_y.keyframe_points], [0.52, 1.02])
        self.assertEqual([k.co[1] for k in rot.keyframe_points], [0.1, 0.2])
        self.assertEqual([k.co[1] for k in bone.keyframe_points], [0.3, 0.4])
        self.assertEqual(moved, 2)
        self.assertTrue(loc_y.updated)

    def test_each_axis_takes_its_own_part(self):
        obj = _Object()
        x, z = _Curve("location", 0, [1.0]), _Curve("location", 2, [1.0])
        rs.move_object(obj, (0.25, 0.0, -0.5), [_Action([x, z])])
        self.assertEqual((x.keyframe_points[0].co[1], z.keyframe_points[0].co[1]), (1.25, 0.5))


class SeatPointTests(unittest.TestCase):
    """The seat is the low rear end of the saddle's top surface, behind the pommel, where the rider's pelvis rests."""

    # A midline profile like the warg's orc_rider_saddle (model space, +y forward): the seat rises to the pommel, the
    # straps hang far below, and another part stands apart further forward.
    PROFILE = [(-0.12, 1.51), (-0.07, 1.519), (-0.02, 1.532), (0.03, 1.581), (0.08, 1.612), (0.13, 1.67),
               (0.18, 1.709), (0.23, 1.712), (0.42, 0.633), (0.87, 1.544), (1.27, 1.411)]

    def _points(self, flip=1.0, x=0.0):
        return [(x, flip * y, z) for y, z in self.PROFILE]

    def test_the_seat_is_the_rear_end_of_the_rise_to_the_pommel(self):
        y, z = rs.seat_point(self._points(), tail_y=-1.0)
        self.assertAlmostEqual(y, -0.12, places=6)
        self.assertAlmostEqual(z, 1.51, places=6)

    def test_a_rig_facing_minus_y_mirrors_the_answer(self):
        y, z = rs.seat_point(self._points(flip=-1.0), tail_y=1.0)
        self.assertAlmostEqual(y, 0.12, places=6)
        self.assertAlmostEqual(z, 1.51, places=6)

    def test_a_small_shift_moves_the_seat_by_the_same_amount(self):
        # The y is the vertex's own, not a bin centre, so a move under one bin still shows (the #1081 tooling lens).
        shifted = [(x, y + 0.015, z) for x, y, z in self._points()]  # 0.02 would put -0.12 on a bin edge
        self.assertAlmostEqual(rs.seat_point(shifted, tail_y=-1.0)[0], -0.105, places=6)

    def test_points_off_the_midline_are_ignored(self):
        # Unfiltered, this point opens the next bin toward the tail, and the walk would end there at (-0.17, 1.4).
        points = self._points() + [(0.5, -0.17, 1.4)]
        self.assertEqual(rs.seat_point(points, tail_y=-1.0), rs.seat_point(self._points(), tail_y=-1.0))

    def test_no_midline_points_is_refused(self):
        with self.assertRaises(ValueError):
            rs.seat_point(self._points(x=0.5), tail_y=-1.0)


if __name__ == "__main__":
    unittest.main()
