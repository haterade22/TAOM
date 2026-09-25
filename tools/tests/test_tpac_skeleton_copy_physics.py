#!/usr/bin/env python3
"""Unit tests for tools/tpac_skeleton_copy_physics.py.

Run:  python -m unittest tools.tests.test_tpac_skeleton_copy_physics

Synthetic skeletons and packages only; no game install, Kit or Blender needed. Pins:
  - SkeletonUserData (bodies, d6, ik and hinge joints) parses and rebuilds byte for byte; an unknown joint type
    is refused
  - copied onto a skeleton whose bones run along +Y with a different roll per bone, twice the size, every capsule
    end lands at twice its world position and every joint frame keeps its world orientation (the mapping is
    exact when only frames and scale differ)
  - a donor bone the target lacks drops its body and joints; the target's header is kept, Usage changes
  - a fit writes its hit capsules and sizes the ragdoll radius by the donor's ragdoll-to-max-hit ratio
  - the CLI dry run writes nothing; --apply writes a backup and a package that reads back; it refuses while the
    game or the Kit runs and refuses a fit made for another skeleton
Skips where lz4, numpy or xxhash is missing, as the sibling capsule tests do.
"""
import io
import json
import math
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    import numpy as np
    import test_skeleton_hit_capsules as th  # the synthetic tpac builder
except ImportError as exc:
    raise unittest.SkipTest("tpac_skeleton_copy_physics needs lz4, numpy and xxhash (%s)" % exc)
import skeleton_hit_capsules as shc  # noqa: E402
import tpac_skeleton_copy_physics as cp  # noqa: E402

# a minimal humanoid: world origins, and each bone's axis child (None for a leaf)
ORIGINS = {"pelvis": (0, 0, 1.0), "spine": (0, 0.02, 1.3), "head": (0, 0.05, 1.6), "l_finger0": (0.3, 0, 1.3),
           "l_thigh": (0.1, 0, 0.95), "l_foot": (0.1, -0.02, 0.1), "l_toe0": (0.1, 0.15, 0.02),
           "r_thigh": (-0.1, 0, 0.95), "r_foot": (-0.1, -0.02, 0.1), "r_toe0": (-0.1, 0.15, 0.02)}
PARENTS = {"pelvis": None, "spine": "pelvis", "head": "spine", "l_finger0": "spine", "l_thigh": "pelvis",
           "l_foot": "l_thigh", "l_toe0": "l_foot", "r_thigh": "pelvis", "r_foot": "r_thigh", "r_toe0": "r_foot"}
AXIS_CHILD = {"pelvis": "spine", "spine": "head", "l_thigh": "l_foot", "l_foot": "l_toe0", "r_thigh": "r_foot",
              "r_foot": "r_toe0"}
ORDER = list(ORIGINS)


def _rot(axis, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    i, j = [(1, 2), (2, 0), (0, 1)][axis]
    m = np.eye(3)
    m[i, i], m[i, j], m[j, i], m[j, j] = c, -s, s, c
    return m


def _donor_bases():
    """World basis per bone (columns), +X along the bone, a different roll each."""
    out = {}
    for k, name in enumerate(ORDER):
        o = np.array(ORIGINS[name], float)
        tip = ORIGINS[AXIS_CHILD[name]] if name in AXIS_CHILD else None
        d = (np.array(tip) - o) if tip else (o - np.array(ORIGINS[PARENTS[name]]))
        d /= np.linalg.norm(d)
        ref = np.array([0, 0, 1.0]) if abs(d[2]) < 0.9 else np.array([0, 1.0, 0])
        y = np.cross(ref, d)
        y /= np.linalg.norm(y)
        base = np.column_stack([d, y, np.cross(d, y)])
        out[name] = base @ _rot(0, 23 * k)  # roll about the bone
    return out


def _skeleton(bases, scale):
    """Bones with rest matrices (row convention, as tpac stores them) for the given world bases and scale."""
    world = {}
    for name in ORDER:
        w = np.eye(4)
        w[:3, :3] = bases[name].T
        w[3, :3] = np.array(ORIGINS[name]) * scale
        world[name] = w
    bones = []
    for name in ORDER:
        p = PARENTS[name]
        rest = world[name] @ np.linalg.inv(world[p]) if p else world[name]
        bones.append({"name": name, "parent": ORDER.index(p) if p else -1, "rest": rest})
    return bones


def _target_bases(donor):
    """The same bones along +Y (X taken to Y), each with its own roll about Y."""
    return {n: b @ _rot(2, -90) @ _rot(1, 40 + 31 * k) for k, (n, b) in enumerate(donor.items())}


def _body(bone, rr=0.08, cr=0.1, cmax=0.15):
    return {"bone": bone, "blend": 1, "type": "biped_" + bone, "zone": "legs", "mass": 3.5,
            "rp1": (0.05, 0.02, -0.01, 1.0), "rp2": (0.3, -0.03, 0.02, 1.0), "rr": rr,
            "cp1": (0.04, 0.01, 0.0, 1.0), "cp2": (0.25, 0.0, 0.03, 1.0), "cr": cr, "cmax": cmax}


def _joint(kind, child, parent, rot):
    rot = tuple(float(x) for x in np.array(rot) / np.linalg.norm(rot))
    j = {"skipped": 0, "type": kind, "name": "%s_%s_%s" % (kind, parent, child), "bone1": child, "bone2": parent,
         "rot": rot, "pos": (0.0, 0.0, 0.0, 1.0)}
    if kind == "d6":
        j.update(locks=["locked"] * 3 + ["limited"] * 3, limits=(0.01, -0.5, 0.5, 0.7, 0.9))
    elif kind == "ik":
        j.update(ik_uint=0, limits=(0.3, 0.3, -0.2, 0.2))
    else:
        j.update(limits=(0.1, 0.2))
    return j


def _donor_userdata():
    s = math.sqrt(0.5)
    return {"bb_padding": 0.2, "bb_min": (0, 0, 0, 1.0), "bb_max": (0, 0, 0, 1.0), "usage": "human",
            "unknown_str": "", "guid": bytes(16), "bodies": [_body(n) for n in ORDER], "unknown_int": 0,
            "constraints": [_joint("d6", "l_foot", "l_thigh", (0.887, 0.0, 0.0, -0.462)),
                            _joint("ik", "spine", "pelvis", (0.9, 0.3, -0.2, 0.25)),
                            _joint("d6", "l_finger0", "spine", (s, 0.0, s, 0.0)),
                            _joint("hinge", "head", "spine", (1.0, 0.0, 0.0, 0.0))]}


def _empty_userdata(bones):
    empty = {"blend": 0, "type": "", "zone": "none", "mass": 0.0, "rp1": (0, 0, 0, 1.0), "rp2": (0, 0, 0, 1.0),
             "rr": -1.0, "cp1": (0, 0, 0, 1.0), "cp2": (0, 0, 0, 1.0), "cr": -1.0, "cmax": 0.0}
    return {"bb_padding": 0.0, "bb_min": (0, 0, 0, 1.0), "bb_max": (0, 0, 0, 1.0), "usage": "other",
            "unknown_str": "", "guid": bytes(16), "unknown_int": 0, "constraints": [],
            "bodies": [dict(empty, bone=b["name"]) for b in bones]}


def _sk(bones, userdata):
    world = shc.world_matrices(bones)
    return {"name": "s", "bones": bones, "world": world, "userdata": userdata,
            "index": {b["name"]: i for i, b in enumerate(bones)}, "axis": shc.bone_axis(bones, world)}


def _pair(scale=2.0):
    db = _donor_bases()
    donor_bones = _skeleton(db, 1.0)
    target_bones = [b for b in _skeleton(_target_bases(db), scale) if b["name"] != "l_finger0"]
    for b in target_bones:  # parent indices shift once l_finger0 is gone
        p = PARENTS[b["name"]]
        b["parent"] = [x["name"] for x in target_bones].index(p) if p else -1
    return _sk(donor_bones, _donor_userdata()), _sk(target_bones, _empty_userdata(target_bones))


def _wpoint(sk, bone, p):
    w = sk["world"][sk["index"][bone]]
    return w[:3, :3].T @ np.array(p[:3]) + w[3, :3]


def _wframe(sk, bone, q):
    return sk["world"][sk["index"][bone]][:3, :3].T @ cp.quat_matrix(np.array(q))


class FormatTests(unittest.TestCase):
    def test_userdata_round_trips_byte_for_byte(self):
        data = cp.build_userdata(_donor_userdata())
        self.assertEqual(cp.build_userdata(cp.parse_userdata(data)), data)
        self.assertEqual([c["type"] for c in cp.parse_userdata(data)["constraints"]], ["d6", "ik", "d6", "hinge"])

    def test_an_unknown_joint_type_is_refused(self):
        u = _donor_userdata()
        u["constraints"][3]["type"] = "spring"
        with self.assertRaises(cp.Refused):
            cp.parse_userdata(cp.build_userdata(u))


class TransferTests(unittest.TestCase):
    def test_axes_detected(self):
        donor, target = _pair()
        self.assertEqual(donor["axis"][:2], (0, 1.0))
        self.assertEqual(target["axis"][:2], (1, 1.0))

    def test_capsules_land_at_scaled_world_positions_and_joints_keep_their_world_frame(self):
        donor, target = _pair(scale=2.0)
        new, dropped, g, _ = cp.transfer(donor, target, "human")
        self.assertAlmostEqual(g, 2.0, places=6)
        target["userdata"] = new
        by = {b["bone"]: b for b in donor["userdata"]["bodies"]}
        for b in new["bodies"]:
            for key in ("rp1", "rp2", "cp1", "cp2"):
                want = 2.0 * _wpoint(donor, b["bone"], by[b["bone"]][key])
                np.testing.assert_allclose(_wpoint(target, b["bone"], b[key]), want, atol=1e-6,
                                           err_msg="%s %s" % (b["bone"], key))
            self.assertAlmostEqual(b["rr"], 2.0 * by[b["bone"]]["rr"], places=6)
            self.assertAlmostEqual(b["mass"], by[b["bone"]]["mass"], places=6)
        dj = {c["name"]: c for c in donor["userdata"]["constraints"]}
        self.assertEqual(len(new["constraints"]), 3)
        for c in new["constraints"]:
            np.testing.assert_allclose(_wframe(target, c["bone1"], c["rot"]),
                                       _wframe(donor, c["bone1"], dj[c["name"]]["rot"]), atol=1e-6, err_msg=c["name"])
            self.assertEqual(c["limits"], dj[c["name"]]["limits"])

    def test_a_missing_bone_drops_its_body_and_joints_and_the_header_is_kept(self):
        donor, target = _pair()
        new, dropped, _, _ = cp.transfer(donor, target, "human")
        self.assertEqual(dropped, ["l_finger0", "d6_spine_l_finger0"])
        self.assertEqual([b["bone"] for b in new["bodies"]], [b["name"] for b in target["bones"]])
        self.assertEqual(new["usage"], "human")
        self.assertEqual(new["bb_padding"], 0.0)

    def test_a_missing_bone_is_carried_through_its_parent(self):
        # the grip bones a rig authored without them needs: l_finger0 exists on the donor only
        donor, target = _pair(scale=2.0)
        carried = cp.carry_missing_bones(donor, target)
        self.assertEqual([(c["name"], c["parent"]) for c in carried], [("l_finger0", "spine")])
        world = np.array(carried[0]["world"]).reshape(4, 4)
        donor_world = donor["world"][donor["index"]["l_finger0"]]
        np.testing.assert_allclose(world[3, :3], 2.0 * donor_world[3, :3], atol=1e-6)
        np.testing.assert_allclose(world[:3, :3], donor_world[:3, :3], atol=1e-6)
        rest = np.array(carried[0]["rest"]).reshape(4, 4)
        np.testing.assert_allclose(rest @ target["world"][target["index"]["spine"]], world, atol=1e-6)

    def test_reframed_bones_keep_their_positions_and_map_by_identity(self):
        # human clips store joint rotations: they bend a rig right only when every bone's axes mean the human's
        donor, target = _pair(scale=2.0)
        frames = cp.reframe_bones(donor, target)
        names = [f["name"] for f in frames]
        self.assertEqual(sorted(names), sorted([b["name"] for b in target["bones"]] + ["l_finger0"]))
        for f in frames:
            if f["name"] in target["index"]:
                np.testing.assert_allclose(np.array(f["world"]).reshape(4, 4)[3, :3],
                                           target["world"][target["index"][f["name"]]][3, :3], atol=1e-9)
        order = [n for n in ORDER if n in names]
        bones = [{"name": n, "parent": order.index(PARENTS[n]) if PARENTS[n] else -1,
                  "rest": np.array(frames[names.index(n)]["rest"]).reshape(4, 4)} for n in order]
        reframed = _sk(bones, _empty_userdata(bones))
        np.testing.assert_allclose(
            [np.array(frames[names.index(n)]["world"]).reshape(4, 4) for n in order], reframed["world"], atol=1e-9)
        maps, _, _ = cp.bone_maps(donor, reframed)
        for name, (M, _) in maps.items():
            np.testing.assert_allclose(M, np.eye(3), atol=1e-9, err_msg=name)
        new, _, _, _ = cp.transfer(donor, reframed, "human")
        dj = {c["name"]: c for c in donor["userdata"]["constraints"]}
        for c in new["constraints"]:
            np.testing.assert_allclose(c["rot"], dj[c["name"]]["rot"], atol=1e-6, err_msg=c["name"])

    def test_a_reframed_rig_maps_by_identity_even_when_an_offset_moved_a_carried_child(self):
        # the troll's hands: no child when re-framed, then a grip child placed 0.22 m off by the artist, which the
        # axis-child rule would aim the hand at (23 to 30 deg off on the real rig)
        donor, full = _pair(scale=2.0)
        keep = [b for b in full["bones"] if b["name"] != "l_toe0"]
        names = [b["name"] for b in keep]
        bones = [dict(b, parent=names.index(full["bones"][b["parent"]]["name"]) if b["parent"] >= 0 else -1) for b in keep]
        target = _sk(bones, _empty_userdata(bones))
        frames = cp.reframe_bones(donor, target, {"l_toe0": (0.0, 0.3, -0.2)})
        order = [n for n in ORDER if n in [f["name"] for f in frames]]
        by = {f["name"]: f for f in frames}
        rebuilt = [{"name": n, "parent": order.index(PARENTS[n]) if PARENTS[n] else -1,
                    "rest": np.array(by[n]["rest"]).reshape(4, 4)} for n in order]
        reframed = _sk(rebuilt, _empty_userdata(rebuilt))
        maps, _, _ = cp.bone_maps(donor, reframed)
        self.assertGreater(np.abs(maps["l_foot"][0] - np.eye(3)).max(), 0.05, "the pitfall this mode exists for")
        maps, _, _ = cp.bone_maps(donor, reframed, identity=True)
        for name, (M, _) in maps.items():
            np.testing.assert_allclose(M, np.eye(3), atol=1e-12, err_msg=name)
        new, _, _, _ = cp.transfer(donor, reframed, "human", identity=True)
        dj = {c["name"]: c for c in donor["userdata"]["constraints"]}
        foot = [c for c in new["constraints"] if c["bone1"] == "l_foot"][0]
        np.testing.assert_allclose(foot["rot"], dj[foot["name"]]["rot"], atol=1e-6)

    def test_an_offset_moves_a_carried_bone_and_keeps_its_axes(self):
        # the artist's placement: the troll's fist hangs below the wrist, so its grips sit 0.22 m lower
        donor, target = _pair(scale=2.0)
        plain = cp.carry_missing_bones(donor, target)[0]
        moved = cp.carry_missing_bones(donor, target, {"l_finger0": (0.0, 0.0, -0.22)})[0]
        a, b = np.array(plain["world"]).reshape(4, 4), np.array(moved["world"]).reshape(4, 4)
        np.testing.assert_allclose(b[3, :3] - a[3, :3], [0.0, 0.0, -0.22], atol=1e-9)
        np.testing.assert_allclose(b[:3, :3], a[:3, :3], atol=1e-9)
        rest = np.array(moved["rest"]).reshape(4, 4)
        np.testing.assert_allclose(rest @ target["world"][target["index"]["spine"]], b, atol=1e-9)

    def test_a_fit_writes_hit_capsules_and_sizes_the_ragdoll_radius(self):
        donor, target = _pair()
        donor["userdata"]["bodies"][ORDER.index("l_toe0")].update(rr=-1.0)
        fit = {"skeleton": "s", "bodies": [
            {"bone": "l_thigh", "action": "refit", "new": {"cp1": [0, 0.1, 0], "cp2": [0, 0.9, 0], "cr": 0.6}},
            {"bone": "l_toe0", "action": "refit", "new": {"cp1": [0, 0, 0], "cp2": [0, 0.2, 0], "cr": 0.3}},
            {"bone": "spine", "action": "kept", "new": {"cp1": [0, 0.2, 0], "cp2": [0, 0.5, 0], "cr": 0.7}},
            {"bone": "head", "action": "kept", "new": {"cp1": [0, 0, 0], "cp2": [0, 0, 0], "cr": -1.0}}]}
        new, _, _, _ = cp.transfer(donor, target, "human", fit)
        by = {b["bone"]: b for b in new["bodies"]}
        self.assertEqual(by["l_thigh"]["cp2"], (0, 0.9, 0, 1.0))
        self.assertEqual((by["l_thigh"]["cr"], by["l_thigh"]["cmax"]), (0.6, 0.6))
        self.assertAlmostEqual(by["l_thigh"]["rr"], 0.08 / 0.15 * 0.6, places=9)
        self.assertEqual(by["l_toe0"]["rr"], -1.0, "no ragdoll capsule on the donor, none on the target")
        # kept: the package's capsule already covered more of its skin (a re-run over a fitted package)
        self.assertEqual((by["spine"]["cp2"], by["spine"]["cr"]), ((0, 0.5, 0, 1.0), 0.7))
        self.assertAlmostEqual(by["spine"]["rr"], 0.08 / 0.15 * 0.7, places=9)
        self.assertAlmostEqual(by["head"]["cr"], 2.0 * 0.1, places=6, msg="a kept body with no capsule takes the copy")


def _definition(bones):
    return th._definition("s", [(b["name"], b["parent"], list(b["rest"].reshape(-1))) for b in bones])


def _package(sk):
    return th._tpac([(shc.SKELETON_ITEM_TYPE, "s", [(shc.SKELETON_DEFINITION_TYPE, _definition(sk["bones"])),
                                                     (shc.SKELETON_USERDATA_TYPE, cp.build_userdata(sk["userdata"]))]),
                     (shc.METAMESH_ITEM_TYPE, "mesh", [(th.MESH_SEGMENT_TYPE, th.MESHDATA)])])


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        donor, target = _pair()
        self.donor = os.path.join(self.tmp.name, "donor.tpac")
        self.target = os.path.join(self.tmp.name, "target.tpac")
        for path, sk in ((self.donor, donor), (self.target, target)):
            with open(path, "wb") as fh:
                fh.write(_package(sk))
        self.before = th._read(self.target)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *extra):
        out = io.StringIO()
        with redirect_stdout(out):
            code = cp.main(["--tpac", self.target, "--skeleton", "s", "--donor", self.donor, "--donor-skeleton", "s",
                            *extra])
        return code, out.getvalue()

    def test_dry_run_writes_nothing(self):
        code, out = self._run()
        self.assertEqual(code, 0, out)
        self.assertIn("dry run", out)
        self.assertEqual(th._read(self.target), self.before)

    def test_apply_writes_a_backup_and_a_package_that_reads_back(self):
        with mock.patch.object(cp, "game_or_kit_running", return_value=False):
            code, out = self._run("--apply")
        self.assertEqual(code, 0, out)
        backups = [f for f in os.listdir(self.tmp.name) if ".bak-physics-" in f]
        self.assertEqual(len(backups), 1)
        self.assertEqual(th._read(os.path.join(self.tmp.name, backups[0])), self.before)
        raw = th._read(self.target)
        self.assertEqual(shc.stale_segment_hashes(raw), [])
        sk = cp.load_skeleton(raw, "s")
        self.assertEqual(sk["userdata"]["usage"], "human")
        self.assertEqual(len(sk["userdata"]["constraints"]), 3)

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        with mock.patch.object(cp, "game_or_kit_running", return_value=True):
            code, _ = self._run("--apply")
        self.assertEqual(code, 2)
        self.assertEqual(th._read(self.target), self.before)

    def test_missing_bones_writes_their_frames_and_leaves_the_package_alone(self):
        path = os.path.join(self.tmp.name, "grip.json")
        code, out = self._run("--missing-bones", path)
        self.assertEqual(code, 0, out)
        self.assertEqual(th._read(self.target), self.before)
        with open(path) as fh:
            data = json.load(fh)
        self.assertEqual(data["skeleton"], "s")
        self.assertEqual([b["name"] for b in data["bones"]], ["l_finger0"])
        self.assertEqual(len(data["bones"][0]["world"]), 16)
        self.assertIn("pelvis", data["target_world"])

    def _reframe_json(self, turn_deg=0.0):
        sk = cp.load_skeleton(th._read(self.target), "s")
        bones = []
        for b, w in zip(sk["bones"], sk["world"]):
            w = w.copy()
            if b["name"] == "spine" and turn_deg:
                c, s = math.cos(math.radians(turn_deg)), math.sin(math.radians(turn_deg))
                w[:3, :3] = np.array([[c, -s, 0], [s, c, 0], [0, 0, 1.0]]) @ w[:3, :3]
            bones.append({"name": b["name"], "world": [float(x) for x in w.reshape(-1)]})
        path = os.path.join(self.tmp.name, "reframe.json")
        with open(path, "w") as fh:
            json.dump({"skeleton": "s", "mode": "reframe", "bones": bones}, fh)
        return path

    def test_reframed_mode_accepts_a_matching_record(self):
        code, out = self._run("--reframed", self._reframe_json())
        self.assertEqual(code, 0, out)
        self.assertIn("every map is the identity", out)

    def test_reframed_mode_refuses_a_package_that_does_not_match_its_record(self):
        code, out = self._run("--reframed", self._reframe_json(turn_deg=5.0))
        self.assertEqual(code, 1, out)
        self.assertIn("spine", out)

    def test_a_fit_for_another_skeleton_is_refused(self):
        path = os.path.join(self.tmp.name, "fit.json")
        with open(path, "w") as fh:
            json.dump({"skeleton": "other_skeleton", "bodies": []}, fh)
        code, out = self._run("--fit", path)
        self.assertEqual(code, 1)
        self.assertIn("other_skeleton", out)


if __name__ == "__main__":
    unittest.main()
