"""creature_fit_math: the geometry behind the creature animation fit (grip, off hand, clearance, IK).

Pure numpy, so every rule is proven here without Blender. The engine conventions the functions encode were
measured on 2026-09-30 (docs/features/troll-race.md "Fine tuning"): a rest frame is 16 floats whose rows are
the basis vectors and whose m12..m14 are the offset; a clip's local is T(offset) R(quat), quaternions w,x,y,z;
a held weapon's frame is its grip bone's frame, pieces along +Z, the striking face along -X.
"""
import math
import os
import sys
import unittest

try:
    import numpy as np
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("creature_fit_math needs numpy (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import creature_fit_math as m  # noqa: E402


def rot_z(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return np.array([[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]])


def rot_x(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return np.array([[1.0, 0.0, 0.0], [0.0, c, -s], [0.0, s, c]])


def rest16(R, p):
    """The engine's layout: rows are basis vectors (the columns of R), then the offset."""
    return [R[0, 0], R[1, 0], R[2, 0], 0.0, R[0, 1], R[1, 1], R[2, 1], 0.0,
            R[0, 2], R[1, 2], R[2, 2], 0.0, p[0], p[1], p[2], 1.0]


def chain_spec():
    """A three-bone chain along +X: root at the origin, 1 m to b1, 0.5 m to b2 (turned 90 deg about Z)."""
    return {"bones": [
        {"name": "root", "parent": None, "rest": rest16(np.eye(3), [0.0, 0.0, 0.0])},
        {"name": "b1", "parent": "root", "rest": rest16(np.eye(3), [1.0, 0.0, 0.0])},
        {"name": "b2", "parent": "b1", "rest": rest16(rot_z(90), [0.5, 0.0, 0.0])},
    ]}


class QuaternionTests(unittest.TestCase):
    def test_round_trip_through_a_matrix(self):
        R = rot_z(30) @ rot_x(-70)
        q = m.mat_to_quat(R)
        self.assertAlmostEqual(float(np.linalg.norm(q)), 1.0, places=9)
        np.testing.assert_allclose(m.quat_to_mat(q), R, atol=1e-9)

    def test_w_first_order_matches_the_engine_dump(self):
        # 90 deg about Z: w = cos 45, z = sin 45, stored w, x, y, z
        np.testing.assert_allclose(m.quat_to_mat([math.sqrt(0.5), 0.0, 0.0, math.sqrt(0.5)]), rot_z(90), atol=1e-9)

    def test_slerp_halfway_is_half_the_angle_and_takes_the_short_arc(self):
        q0 = m.mat_to_quat(np.eye(3))
        q1 = -m.mat_to_quat(rot_z(80))          # the same rotation, other hemisphere
        np.testing.assert_allclose(m.quat_to_mat(m.slerp(q0, q1, 0.5)), rot_z(40), atol=1e-9)


class SkeletonTests(unittest.TestCase):
    def test_rest_local_reads_rows_as_basis_vectors(self):
        R, p = m.rest_local(rest16(rot_z(90), [1.0, 2.0, 3.0]))
        np.testing.assert_allclose(R, rot_z(90), atol=1e-12)
        np.testing.assert_allclose(p, [1.0, 2.0, 3.0])

    def test_rest_world_accumulates_down_the_chain(self):
        sk = m.Skeleton.from_spec(chain_spec())
        Rw, pw = sk.rest_world()
        np.testing.assert_allclose(pw[sk.index("b2")], [1.5, 0.0, 0.0], atol=1e-12)
        np.testing.assert_allclose(Rw[sk.index("b2")], rot_z(90), atol=1e-12)

    def test_fk_of_a_rotated_parent_moves_the_child(self):
        sk = m.Skeleton.from_spec(chain_spec())
        R = np.repeat(sk.rest_R[None], 1, axis=0).copy()
        p = sk.rest_p[None].copy()
        R[0, sk.index("b1")] = rot_z(90)            # bend at b1: b2 now lies along +Y from b1
        Rw, pw = m.fk(sk, R, p)
        np.testing.assert_allclose(pw[0, sk.index("b2")], [1.0, 0.5, 0.0], atol=1e-12)

    def test_sample_clip_uses_rest_for_bones_without_tracks_and_adds_the_root_delta(self):
        spec = chain_spec()
        q = m.mat_to_quat(rot_z(90))
        spec["boneAnims"] = [
            {"bone": "root", "rot": [], "pos": []},
            {"bone": "b1", "rot": [{"t": 0, "w": 1.0, "x": 0.0, "y": 0.0, "z": 0.0},
                                   {"t": 2, "w": q[0], "x": q[1], "y": q[2], "z": q[3]}], "pos": []},
            {"bone": "b2", "rot": [], "pos": []},
        ]
        spec["root"] = {"pos": [{"t": 0, "x": 0.0, "y": 0.0, "z": 0.0}, {"t": 2, "x": 0.0, "y": 0.0, "z": 0.2}]}
        sk = m.Skeleton.from_spec(spec)
        R, p = m.sample_clip(sk, spec, [1])
        np.testing.assert_allclose(R[0, sk.index("b1")], rot_z(45), atol=1e-9)    # halfway, slerped
        np.testing.assert_allclose(R[0, sk.index("b2")], rot_z(90), atol=1e-12)   # no track: rest
        np.testing.assert_allclose(p[0, sk.index("root")], [0.0, 0.0, 0.1], atol=1e-12)

    def test_clip_length_is_the_last_key_plus_one(self):
        spec = chain_spec()
        spec["boneAnims"] = [{"bone": "b1", "rot": [{"t": 0, "w": 1, "x": 0, "y": 0, "z": 0},
                                                     {"t": 41, "w": 1, "x": 0, "y": 0, "z": 0}], "pos": []}]
        self.assertEqual(m.clip_length(spec), 42)


class SkinningTests(unittest.TestCase):
    def test_rest_pose_leaves_the_mesh_where_it_is(self):
        sk = m.Skeleton.from_spec(chain_spec())
        Rw, pw = sk.rest_world()
        v = np.array([[1.2, 0.1, 0.0], [0.3, -0.2, 0.1]])
        idx = np.array([[1, 0, 0, 0], [0, 1, 0, 0]])
        w = np.array([[1.0, 0, 0, 0], [0.5, 0.5, 0, 0]])
        np.testing.assert_allclose(m.skin(v, idx, w, Rw, pw, Rw, pw), v, atol=1e-12)

    def test_a_vertex_follows_its_bone_rigidly(self):
        sk = m.Skeleton.from_spec(chain_spec())
        Rr, pr = sk.rest_world()
        R = sk.rest_R[None].copy()
        R[0, sk.index("b1")] = rot_z(90)
        Rw, pw = m.fk(sk, R, sk.rest_p[None])
        v = np.array([[1.5, 0.0, 0.0]])                    # 0.5 m out along b1
        out = m.skin(v, np.array([[1, 0, 0, 0]]), np.array([[1.0, 0, 0, 0]]), Rr, pr, Rw[0], pw[0])
        np.testing.assert_allclose(out, [[1.0, 0.5, 0.0]], atol=1e-12)


class ShaftTests(unittest.TestCase):
    def test_distance_and_position_along_the_grip_z(self):
        perp, along = m.line_point(np.array([0.03, 0.04, -0.14]), np.zeros(3), np.array([0.0, 0.0, 1.0]))
        self.assertAlmostEqual(perp, 0.05)
        self.assertAlmostEqual(along, -0.14)

    def test_capsule_depth_is_positive_inside_and_negative_outside(self):
        d = m.capsule_depth(np.array([[0.0, 0.02, 0.5], [0.0, 0.2, 0.5], [0.0, 0.0, 1.05]]),
                            np.zeros(3), np.array([0.0, 0.0, 1.0]), 0.05)
        self.assertAlmostEqual(d[0], 0.03)
        self.assertAlmostEqual(d[1], -0.15)
        self.assertAlmostEqual(d[2], 0.0)                  # on the end cap's surface

    def test_box_depth_uses_the_nearest_face(self):
        axes = rot_z(90)                                    # the box's x runs along world y
        d = m.box_depth(np.array([[0.0, 0.5, 0.0], [0.0, 0.0, 0.3], [1.0, 0.0, 0.0]]),
                        np.zeros(3), axes, np.array([0.6, 0.1, 0.35]))
        self.assertAlmostEqual(d[0], 0.1)                   # 0.5 along the box x (half 0.6): 0.1 from that face
        self.assertAlmostEqual(d[1], 0.05)
        self.assertLess(d[2], 0.0)


class SimilarityFitTests(unittest.TestCase):
    def test_recovers_a_scaled_rotated_shifted_copy_exactly(self):
        rng = np.random.default_rng(7)
        src = rng.normal(size=(28, 3))
        R = rot_z(180) @ rot_x(90)
        dst = 0.01 * src @ R.T + np.array([0.3, -2.0, 1.1])
        fit = m.similarity_fit(src, dst)
        self.assertAlmostEqual(fit["scale"], 0.01, places=12)
        np.testing.assert_allclose(fit["R"], R, atol=1e-9)
        self.assertLess(fit["residual_max"], 1e-12)
        np.testing.assert_allclose(m.apply_similarity(fit, src), dst, atol=1e-12)

    def test_never_returns_a_mirror(self):
        src = np.array([[0.0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]])
        fit = m.similarity_fit(src, src * np.array([1.0, 1.0, -1.0]))
        self.assertGreater(np.linalg.det(fit["R"]), 0)
        self.assertGreater(fit["residual_max"], 0.1)          # a mirror cannot be fitted, and says so


class SplineTests(unittest.TestCase):
    def test_passes_through_every_key(self):
        keys = np.array([[0.0, 0.0], [1.0, 2.0], [3.0, 1.0], [4.0, 4.0]])
        times = [1, 10, 20, 30]
        out = m.catmull_rom(times, keys, [1, 10, 20, 30])
        np.testing.assert_allclose(out, keys, atol=1e-12)

    def test_is_smooth_between_keys(self):
        out = m.catmull_rom([0, 10, 20], np.array([[0.0], [1.0], [0.0]]), np.arange(0, 21))
        self.assertLess(float(np.max(np.abs(np.diff(out[:, 0], 2)))), 0.05)   # no kinks
        self.assertGreater(out[10, 0], out[9, 0])

    def test_holds_outside_the_keys(self):
        out = m.catmull_rom([5, 10], np.array([[1.0], [2.0]]), [0, 12])
        np.testing.assert_allclose(out[:, 0], [1.0, 2.0])


class BallProjectionTests(unittest.TestCase):
    def test_a_point_inside_every_ball_stays(self):
        x = m.project_into_balls(np.array([0.1, 0.0, 0.0]), [np.zeros(3), np.array([0.5, 0, 0])], [1.0, 1.0])
        np.testing.assert_allclose(x, [0.1, 0.0, 0.0])

    def test_a_point_outside_lands_in_the_intersection(self):
        c = [np.array([-0.5, 0.0, 0.0]), np.array([0.5, 0.0, 0.0])]
        x = m.project_into_balls(np.array([0.0, 3.0, 0.0]), c, [1.0, 1.0])
        for ci in c:
            self.assertLessEqual(np.linalg.norm(x - ci), 1.0 + 1e-6)
        self.assertAlmostEqual(x[1], math.sqrt(0.75), places=3)      # the closest point of the lens, straight up

    def test_disjoint_balls_meet_halfway_between_their_surfaces(self):
        x = m.project_into_balls(np.array([0.0, 2.0, 0.0]), [np.array([-2.0, 0, 0]), np.array([2.0, 0, 0])],
                                 [1.0, 1.0])
        np.testing.assert_allclose(x, [0.0, 0.0, 0.0], atol=1e-6)


class EaseTests(unittest.TestCase):
    def test_a_one_frame_need_spreads_before_and_after_it(self):
        x = np.zeros(21)
        x[10] = 1.0
        e = m.ease_series(x, lead=3, sigma=0)
        np.testing.assert_allclose(e[7:14], 1.0)                 # held 3 frames either side
        self.assertEqual(e[5], 0.0)

    def test_smoothing_keeps_the_series_continuous(self):
        x = np.zeros(41)
        x[20] = 1.0
        e = m.ease_series(x, lead=4, sigma=2.0)
        self.assertLess(float(np.max(np.abs(np.diff(e)))), 0.25)
        self.assertGreater(e[20], 0.9)

    def test_vectors_ease_by_their_magnitude(self):
        x = np.zeros((11, 3))
        x[5] = [0.0, 0.3, 0.4]
        e = m.ease_series(x, lead=2, sigma=0)
        np.testing.assert_allclose(e[3], [0.0, 0.3, 0.4])

    def test_axis_angle_turns_about_its_axis(self):
        np.testing.assert_allclose(m.axis_angle([0.0, 0.0, math.pi / 2]), rot_z(90), atol=1e-12)


class SurfaceDepthTests(unittest.TestCase):
    def sphere(self, n=400, r=1.0):
        # a Fibonacci sphere: points and their outward normals
        k = np.arange(n) + 0.5
        phi = np.arccos(1 - 2 * k / n)
        th = math.pi * (1 + 5 ** 0.5) * k
        pts = np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], axis=1)
        return pts * r, pts

    def test_points_inside_a_closed_surface_read_their_depth(self):
        v, n = self.sphere()
        d = m.surface_depth(np.array([[0.0, 0.0, 0.9], [0.0, 0.0, 1.2], [0.5, 0.0, 0.0]]), v, n, reach=0.6)
        self.assertAlmostEqual(d[0], 0.1, delta=0.03)           # 0.1 m under the surface
        self.assertLess(d[1], 0.0)                              # outside
        self.assertAlmostEqual(d[2], 0.5, delta=0.06)

    def test_a_point_beyond_reach_of_the_surface_is_not_inside(self):
        v, n = self.sphere(r=3.0)
        d = m.surface_depth(np.array([[0.0, 0.0, 0.0]]), v, n, reach=0.5)   # the centre, 3 m from any vertex
        self.assertLess(d[0], 0.0)


class CraftedLayoutTests(unittest.TestCase):
    def test_the_hill_troll_hammer_stacks_up_the_grip_z(self):
        lay = m.crafted_layout([{"mesh": "handle", "role": "handle", "length": 3.5096, "piece_offset": 0.95},
                                {"mesh": "head", "role": "head", "length": 0.6963}])
        handle, head = lay
        self.assertAlmostEqual(handle["z_centre"], 0.95)
        self.assertAlmostEqual(handle["z_min"], -0.8048)            # the butt, 80.5 cm below the fist
        self.assertAlmostEqual(handle["z_max"], 2.7048)
        self.assertAlmostEqual(head["z_min"], 2.7048)               # seated flush on the handle's top
        self.assertAlmostEqual(head["z_centre"], 3.05295)

    def test_a_layout_needs_its_handle_first(self):
        with self.assertRaises(ValueError):
            m.crafted_layout([{"mesh": "head", "role": "head", "length": 0.7}])


class FistCavityTests(unittest.TestCase):
    def ring(self, centre, radius, z=(-0.05, 0.0, 0.05), n=72):
        pts = []
        for zz in z:
            for k in range(n):
                a = 2 * math.pi * k / n
                pts.append([centre[0] + radius * math.cos(a), centre[1] + radius * math.sin(a), zz])
        return np.array(pts)

    def test_a_fist_wrapped_round_the_shaft_reads_centred_with_full_coverage(self):
        c = m.fist_cavity(self.ring((0.0, 0.0), 0.06), np.eye(3), np.zeros(3), zone_half=0.08, reach=0.2)
        self.assertAlmostEqual(c["radius"], 0.06, places=6)
        self.assertLess(np.linalg.norm(c["centre"]), 1e-6)
        self.assertGreater(c["coverage"], 0.99)

    def test_an_offset_fist_reports_where_its_cavity_centre_is(self):
        c = m.fist_cavity(self.ring((0.07, -0.02), 0.05), np.eye(3), np.zeros(3), zone_half=0.08, reach=0.3)
        np.testing.assert_allclose(c["centre"], [0.07, -0.02], atol=1e-6)
        self.assertAlmostEqual(c["radius"], 0.05, places=6)

    def test_points_outside_the_grip_zone_are_ignored(self):
        pts = np.vstack([self.ring((0.0, 0.0), 0.06), self.ring((0.3, 0.0), 0.01, z=(0.5,))])
        c = m.fist_cavity(pts, np.eye(3), np.zeros(3), zone_half=0.08, reach=0.2)
        self.assertAlmostEqual(c["radius"], 0.06, places=6)

    def test_the_cavity_is_read_in_the_grip_frame(self):
        R = rot_x(90)                                       # grip z along world -y
        pts = self.ring((0.0, 0.0), 0.06) @ R.T + np.array([1.0, 2.0, 3.0])
        c = m.fist_cavity(pts, R, np.array([1.0, 2.0, 3.0]), zone_half=0.08, reach=0.2)
        self.assertAlmostEqual(c["radius"], 0.06, places=6)
        self.assertLess(np.linalg.norm(c["centre"]), 1e-6)


class IkTests(unittest.TestCase):
    def test_rotation_between_turns_one_direction_onto_another(self):
        R = m.rotation_between(np.array([1.0, 0, 0]), np.array([0.0, 0, 1.0]))
        np.testing.assert_allclose(R @ [1.0, 0, 0], [0.0, 0, 1.0], atol=1e-12)
        np.testing.assert_allclose(R @ R.T, np.eye(3), atol=1e-12)

    def test_rotation_between_opposite_directions_is_a_half_turn(self):
        R = m.rotation_between(np.array([0.0, 0, 1.0]), np.array([0.0, 0, -1.0]))
        np.testing.assert_allclose(R @ [0.0, 0, 1.0], [0.0, 0, -1.0], atol=1e-12)

    def test_two_bone_ik_reaches_a_reachable_target_and_keeps_the_lengths(self):
        a, b, c = np.zeros(3), np.array([1.0, 0, 0]), np.array([1.0, 0.8, 0])
        t = np.array([0.6, 1.0, 0.4])
        b2, c2 = m.two_bone_ik(a, b, c, t, pole=np.array([1.0, 0, 0]))
        np.testing.assert_allclose(c2, t, atol=1e-9)
        self.assertAlmostEqual(np.linalg.norm(b2 - a), 1.0)
        self.assertAlmostEqual(np.linalg.norm(c2 - b2), 0.8)

    def test_two_bone_ik_bends_toward_the_pole(self):
        a, b, c = np.zeros(3), np.array([0.7, 0.7, 0]), np.array([1.2, 0.0, 0])
        b2, _ = m.two_bone_ik(a, b, c, np.array([1.2, 0.0, 0.0]), pole=np.array([0.0, 0.0, 1.0]))
        self.assertGreater(b2[2], 0.3)

    def test_two_bone_ik_straightens_toward_an_unreachable_target(self):
        a, b, c = np.zeros(3), np.array([1.0, 0, 0]), np.array([1.0, 1.0, 0])
        b2, c2 = m.two_bone_ik(a, b, c, np.array([0.0, 5.0, 0.0]), pole=np.array([1.0, 0, 0]))
        np.testing.assert_allclose(c2, [0.0, 2.0, 0.0], atol=1e-9)


def arm_spec():
    """A right grip on a stick, and a left arm whose grip starts off it. Bones: root; r_grip on the root; the left
    arm upper -> upper_tw -> fore -> fore_tw -> hand -> grip, each 0.5 m along X, the grip 0.2 m out."""
    b = [("root", None, np.eye(3), [0.0, 0.0, 0.0]),
         ("r_grip", "root", np.eye(3), [1.0, 0.0, 0.0]),
         ("l_upper", "root", np.eye(3), [0.0, 0.5, 0.0]),
         ("l_upper_tw", "l_upper", np.eye(3), [0.25, 0.0, 0.0]),
         ("l_fore", "l_upper_tw", rot_z(-60), [0.25, 0.0, 0.0]),
         ("l_fore_tw", "l_fore", np.eye(3), [0.25, 0.0, 0.0]),
         ("l_hand", "l_fore_tw", np.eye(3), [0.25, 0.0, 0.0]),
         ("l_grip", "l_hand", np.eye(3), [0.2, 0.0, 0.0])]
    return {"bones": [{"name": n, "parent": p, "rest": rest16(R, off)} for n, p, R, off in b]}


ARM = {"upper": "l_upper", "upper_twist": "l_upper_tw", "fore": "l_fore", "fore_twist": "l_fore_tw",
       "hand": "l_hand", "grip": "l_grip"}


class OffhandTests(unittest.TestCase):
    def solve(self, along, weight=1.0):
        sk = m.Skeleton.from_spec(arm_spec())
        R, p = sk.rest_R[None].copy(), sk.rest_p[None].copy()
        R2, rep = m.solve_offhand(sk, R, p, "r_grip", ARM, along=along, weight=np.array([weight]))
        Rw, pw = m.fk(sk, R2, p)
        return sk, Rw[0], pw[0], rep, R, R2

    def test_the_off_grip_lands_on_the_shaft_below_the_main_grip(self):
        sk, Rw, pw, rep, _, _ = self.solve(along=-0.2)       # the wrist target 0.978 m out; the arm reaches 1.0
        g, o = sk.index("r_grip"), sk.index("l_grip")
        perp, along = m.line_point(pw[o], pw[g], Rw[g][:, 2])
        self.assertLess(perp, 1e-6)
        self.assertAlmostEqual(along, -0.2, places=6)
        self.assertGreater(float(Rw[o][:, 2] @ Rw[g][:, 2]), 0.999999)   # the thumbs face the same way
        self.assertLess(rep["miss_m"][0], 1e-6)

    def test_the_arm_keeps_its_lengths(self):
        sk, Rw, pw, _, _, _ = self.solve(along=-0.3)
        up, fo, ha = sk.index("l_upper"), sk.index("l_fore"), sk.index("l_hand")
        self.assertAlmostEqual(float(np.linalg.norm(pw[fo] - pw[up])), 0.5, places=9)
        self.assertAlmostEqual(float(np.linalg.norm(pw[ha] - pw[fo])), 0.5, places=9)

    def test_only_the_arm_bones_change_and_twists_keep_their_locals(self):
        sk, _, _, _, R, R2 = self.solve(along=-0.3)
        for n in ("root", "r_grip", "l_upper_tw", "l_fore_tw", "l_grip"):
            np.testing.assert_allclose(R2[0, sk.index(n)], R[0, sk.index(n)], atol=1e-12)

    def test_zero_weight_leaves_the_frame_alone(self):
        sk, _, _, rep, R, R2 = self.solve(along=-0.3, weight=0.0)
        np.testing.assert_allclose(R2, R, atol=1e-12)

    def test_an_out_of_reach_target_reports_the_miss(self):
        _, _, _, rep, _, _ = self.solve(along=-3.0)
        self.assertGreater(rep["miss_m"][0], 0.5)

    def test_a_pole_direction_sets_which_way_the_elbow_points(self):
        sk = m.Skeleton.from_spec(arm_spec())
        R, p = sk.rest_R[None].copy(), sk.rest_p[None].copy()
        tp = np.array([[0.6, 0.5, 0.0]])
        tR = np.eye(3)[None]
        for pole, sign in (([0.0, 0.0, 1.0], 1), ([0.0, 0.0, -1.0], -1)):
            R2, _ = m.solve_arm(sk, R, p, ARM, tp, tR, np.array([1.0]), pole_dir=np.array([pole]))
            _, pw = m.fk(sk, R2, p)
            self.assertGreater(sign * pw[0, sk.index("l_fore")][2], 0.1)

    def test_solve_arm_puts_the_grip_at_a_frame(self):
        sk = m.Skeleton.from_spec(arm_spec())
        R, p = sk.rest_R[None].copy(), sk.rest_p[None].copy()
        tp = np.array([[0.7, 0.3, 0.2]])
        tR = (rot_x(30) @ rot_z(-40))[None]
        R2, rep = m.solve_arm(sk, R, p, ARM, tp, tR, np.array([1.0]))
        Rw, pw = m.fk(sk, R2, p)
        o = sk.index("l_grip")
        np.testing.assert_allclose(pw[0, o], tp[0], atol=1e-9)
        np.testing.assert_allclose(Rw[0, o], tR[0], atol=1e-9)
        self.assertLess(rep["miss_m"][0], 1e-9)


if __name__ == "__main__":
    unittest.main()
