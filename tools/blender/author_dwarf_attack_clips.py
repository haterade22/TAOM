"""Hand-keyed dwarf attack clips (#1087): dwarf_head_butt and dwarf_drop_kick on dwarf_skeleton_a.

Run inside Blender (MCP or the Text editor), Blender 5.1+:
    ns = {"__name__": "taom_dwarf"}
    exec(open(r"E:/repos/TAOM/tools/blender/author_dwarf_attack_clips.py", encoding="utf-8").read(), ns)
    ns["main"](engine_json=r"E:/LOTRAOMAssets/dwarf_clips_to_import/_reference/dwarf_skeleton_a.json",
               out_dir=r"E:/LOTRAOMAssets/dwarf_clips_to_import")

The rig is build_engine_rig() from retarget_mannequin_to_human.py on the dwarf's own engine dump
(`tools/read_anim_keyframes_tpac.ps1` skeleton fields), so every bone local is engine-exact and the Kit stores it
verbatim. Export follows that script's proven "pose" root-yaw mode: frame 0 is the rest frame (the Kit zeroes the
root position track against it) and the root carries RotZ(180), which the Kit's own root yaw cancels.

A key pose is data. Axes are the character's: X right, Y forward, Z up (the engine dwarf faces +Y).
  aim:  bone -> direction its chain (bone head to CHAIN_CHILD head) points, after the parents moved.
  rot:  bone -> (rx, ry, rz) degrees, XYZ euler about the bone's head, applied after the parents moved.
        +rx leans an upward bone back and swings a downward bone forward; +ry leans right; +rz turns left.
  root: (dx, dy, dz) metres added to the pelvis, applied before `plant`.
  plant: feet that stand on the ground. A foot keeps the world spot of the key it lands on until a key stops
        planting it; a two-bone leg IK holds it there and the pelvis moves so the lowest planted ankle sits at its
        rest height. The leftover slide is reported, never hidden.
  travel (per clip): {"distance": m, "end_frame": f}, the forward move the Kit's displace_position flag and
        displacement usage give the agent. The clip stays in place; a planted foot slides back by the travel so it
        stands still in the world.
Bones a key does not name take the previous key's value, so a key lists only what changes; None releases one.
"""
import json
import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

RT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else
                       r"E:/repos/TAOM/tools/blender", "retarget_mannequin_to_human.py")

ARMATURE_NAME = "dwarf_skeleton_a_notused"
FPS = 30
CHAIN_CHILD = {
    "pelvis": "spine", "spine": "spine1", "spine1": "spine2", "spine2": "neck", "neck": "head",
    "l_clavicle": "l_upperarm_twist", "l_upperarm_twist": "l_foretwist", "l_foretwist": "l_hand",
    "r_clavicle": "r_upperarm_twist", "r_upperarm_twist": "r_foretwist", "r_foretwist": "r_hand",
    "l_thigh": "l_calf", "l_calf": "l_foot", "l_foot": "l_toe0",
    "r_thigh": "r_calf", "r_calf": "r_foot", "r_foot": "r_toe0",
}

# ------------------------------------------------------------------ poses
# Shared stance: left foot leads, knees soft, fists up in front of the chest. Clips open and close on it so
# the engine's blend in and out has a pose that reads as "ready".
READY = {
    "plant": ("l_foot", "r_foot"),
    "aim": {
        "l_thigh": (-0.08, 0.18, -1.0), "l_calf": (-0.04, -0.08, -1.0), "l_foot": (-0.05, 1.0, -0.45),
        "r_thigh": (0.1, -0.12, -1.0), "r_calf": (0.06, -0.22, -1.0), "r_foot": (0.1, 1.0, -0.45),
        "l_upperarm_twist": (-0.35, 0.25, -1.0), "l_foretwist": (0.15, 1.0, 0.55),
        "r_upperarm_twist": (0.35, 0.05, -1.0), "r_foretwist": (-0.1, 1.0, 0.6),
    },
    "rot": {"pelvis": (0, 0, 8), "spine": (-4, 0, -4), "spine1": (-3, 0, -3), "spine2": (0, 0, 0),
            "neck": (0, 0, 0), "head": (3, 0, 0)},
}

HEAD_BUTT = {
    "name": "dwarf_head_butt",
    "impact_frame": 13,
    "keys": [
        dict(frame=1, **READY),
        # wind-up: hands grab forward, chest and head pull back, weight onto the rear foot
        dict(frame=8, plant=("l_foot", "r_foot"), root=(0, -0.04, 0),
             aim={"l_upperarm_twist": (-0.25, 0.85, -0.45), "l_foretwist": (0.25, 1.0, 0.15),
                  "r_upperarm_twist": (0.25, 0.85, -0.45), "r_foretwist": (-0.25, 1.0, 0.15),
                  "l_thigh": (-0.08, 0.1, -1.0), "l_calf": (-0.04, -0.05, -1.0),
                  "r_thigh": (0.1, -0.22, -1.0), "r_calf": (0.06, -0.4, -1.0)},
             rot={"spine": (13, 0, 0), "spine1": (11, 0, 0), "spine2": (8, 0, 0), "neck": (14, 0, 0), "head": (8, 0, 0)}),
        # impact: hands yank back to the chest, trunk snaps forward over the lead knee, chin tucked so the brow lands
        dict(frame=13, plant=("l_foot", "r_foot"), root=(0, 0.1, 0),
             aim={"l_upperarm_twist": (-0.3, -0.2, -1.0), "l_foretwist": (0.45, 1.0, 0.35),
                  "r_upperarm_twist": (0.3, -0.2, -1.0), "r_foretwist": (-0.45, 1.0, 0.35),
                  "l_thigh": (-0.08, 0.45, -1.0), "l_calf": (-0.04, -0.2, -1.0),
                  "r_thigh": (0.1, -0.35, -1.0), "r_calf": (0.06, -0.1, -1.0)},
             rot={"pelvis": (0, 0, 2), "spine": (-14, 0, -1), "spine1": (-14, 0, -1), "spine2": (-10, 0, 0),
                  "neck": (-12, 0, 0), "head": (-14, 0, 0)}),
        dict(frame=16, plant=("l_foot", "r_foot"), root=(0, 0.09, 0),
             rot={"spine": (-12, 0, -1), "spine1": (-12, 0, -1), "spine2": (-8, 0, 0), "neck": (-8, 0, 0), "head": (-10, 0, 0)}),
        # recover: straighten, shake it off, back to ready
        dict(frame=26, plant=("l_foot", "r_foot"), root=(0, 0.02, 0),
             aim={"l_upperarm_twist": (-0.35, 0.15, -1.0), "l_foretwist": (0.15, 1.0, 0.45),
                  "r_upperarm_twist": (0.35, 0.0, -1.0), "r_foretwist": (-0.1, 1.0, 0.5)},
             rot={"spine": (-2, 0, -3), "spine1": (-2, 0, -2), "spine2": (2, 0, 0), "neck": (4, 0, 0), "head": (2, 3, 0)}),
        dict(frame=36, **READY),
    ],
}

SPINE_RELEASE = {"spine": None, "spine1": None, "spine2": None, "neck": None}

# The photo reference (Mike, 2026-11-06): a two-footed dropkick, body turned onto its side in the air, feet together
# at chest height, the low arm reaching for the ground. Two running steps first; the Kit's displace_position moves the
# agent 2 m forward by the landing (travel), and the clip itself stays in place.
DROP_KICK = {
    "name": "dwarf_drop_kick",
    "impact_frame": 21,
    "travel": {"distance": 2.0, "end_frame": 32},
    "keys": [
        dict(frame=1, **READY),
        # step 1: push off the lead (left) foot, right leg swings through, left arm forward
        dict(frame=4, plant=("l_foot",), root=(0, 0, 0.03),
             aim={"r_thigh": (0.1, 0.5, -1.0), "r_calf": (0.06, -0.7, -1.0), "r_foot": (0.1, 1.0, -0.6),
                  "l_thigh": (-0.08, -0.15, -1.0), "l_calf": (-0.04, -0.2, -1.0),
                  "l_upperarm_twist": (-0.25, 0.5, -1.0), "l_foretwist": (-0.05, 1.0, 0.4),
                  "r_upperarm_twist": (0.25, -0.5, -1.0), "r_foretwist": (0.1, 0.2, -1.0)},
             rot={"pelvis": (0, 0, 0), "spine": (-10, 0, 0), "spine1": (-6, 0, 0), "spine2": (0, 0, 0),
                  "neck": (6, 0, 0), "head": (4, 0, 0)}),
        # step 2: right foot lands ahead, left toes off behind
        dict(frame=8, plant=("r_foot",), root=(0, 0, -0.03),
             aim={"r_thigh": (0.1, 0.45, -1.0), "r_calf": (0.06, 0.05, -1.0), "r_foot": (0.1, 1.0, -0.4),
                  "l_thigh": (-0.08, -0.35, -1.0), "l_calf": (-0.04, -1.0, -0.6), "l_foot": (-0.05, 0.3, -1.0),
                  "l_upperarm_twist": (-0.25, -0.2, -1.0), "l_foretwist": (-0.05, 0.6, -0.4),
                  "r_upperarm_twist": (0.25, 0.3, -1.0), "r_foretwist": (0.05, 1.0, 0.2)}),
        # jump step: drive the left knee up off the right foot, both arms swing up
        dict(frame=11, plant=("r_foot",),
             aim={"l_thigh": (-0.08, 1.0, -0.2), "l_calf": (-0.04, -0.2, -1.0), "l_foot": (-0.05, 1.0, -0.3),
                  "r_thigh": (0.08, -0.25, -1.0), "r_calf": (0.05, -0.45, -1.0), "r_foot": (0.05, 0.5, -1.0),
                  "l_upperarm_twist": (-0.3, 0.7, 0.3), "l_foretwist": (-0.2, 0.4, 1.0),
                  "r_upperarm_twist": (0.3, 0.7, 0.3), "r_foretwist": (0.2, 0.4, 1.0)},
             rot={"spine": (2, 0, 0), "spine1": (0, 0, 0), "neck": (0, 0, 0), "head": (0, 0, 0)}),
        # airborne: legs gather, the turn onto the right side starts
        dict(frame=14, root=(0, 0, 0.22),
             aim={"l_thigh": (-0.06, 1.0, 0.1), "l_calf": (-0.04, -0.3, -1.0), "l_foot": (-0.05, 1.0, -0.3),
                  "r_thigh": (0.06, 1.0, 0.0), "r_calf": (0.04, -0.4, -1.0), "r_foot": (0.05, 1.0, -0.3)},
             rot={"pelvis": (20, 15, 0), "spine": (0, 0, 0)}),
        # tuck, half turned
        dict(frame=17, root=(0, 0, 0.35),
             aim={"l_thigh": (-0.05, 1.0, 0.4), "l_calf": (-0.02, 0.1, -1.0), "l_foot": (0.0, 1.0, 0.0),
                  "r_thigh": (0.05, 1.0, 0.4), "r_calf": (0.02, 0.1, -1.0), "r_foot": (0.0, 1.0, 0.0),
                  "r_upperarm_twist": (0.4, 0.2, -0.6), "r_foretwist": (0.2, 0.6, -0.6),
                  "l_upperarm_twist": (-0.4, -0.2, 0.6), "l_foretwist": (-0.2, 0.2, 1.0)},
             rot={"pelvis": (45, 45, 0)}),
        # impact: body level on its right side at chest height, both soles drive into the target, chin tucked
        dict(frame=21, root=(0, 0, 0.25),
             aim={"l_thigh": (0.0, 1.0, 0.2), "l_calf": (0.0, 1.0, 0.25), "l_foot": (0.0, 0.1, 1.0),
                  "r_thigh": (0.0, 1.0, 0.2), "r_calf": (0.0, 1.0, 0.25), "r_foot": (0.0, 0.1, 1.0),
                  "spine": (0.1, -1.0, 0.1), "spine1": (0.15, -1.0, 0.2), "spine2": (0.2, -1.0, 0.3),
                  "neck": (0.5, -0.8, 0.3),
                  "r_upperarm_twist": (0.3, 0.1, -1.0), "r_foretwist": (0.2, 0.3, -1.0),
                  "l_upperarm_twist": (-0.2, -0.6, 0.8), "l_foretwist": (-0.1, -0.4, 1.0)},
             rot={"pelvis": (80, 80, 0)}),
        dict(frame=24, root=(0, 0, 0.08),
             aim={"l_calf": (0.0, 1.0, 0.1), "r_calf": (0.0, 1.0, 0.05)},
             rot={"pelvis": (82, 85, 0)}),
        # fall on the side, knees fold, the low arm reaches for the ground
        dict(frame=28, root=(0, 0, -0.12),
             aim={"l_thigh": (0.0, 1.0, 0.3), "l_calf": (0.0, 0.4, -1.0), "l_foot": (0.0, 1.0, 0.0),
                  "r_thigh": (0.0, 1.0, 0.25), "r_calf": (0.0, 0.4, -1.0), "r_foot": (0.0, 1.0, 0.0),
                  "r_upperarm_twist": (0.2, 0.2, -1.0), "r_foretwist": (0.1, 0.3, -1.0)},
             rot={"pelvis": (85, 88, 0)}),
        # landed on the right side: forearm flat, left hand pushing in front of the chest, head held up
        dict(frame=32, root=(0, 0, -0.34),
             aim={"l_thigh": (0.05, 1.0, -0.15), "l_calf": (0.05, -0.6, -0.8), "l_foot": (0.0, 1.0, 0.0),
                  "r_thigh": (0.05, 1.0, -0.15), "r_calf": (0.05, -0.6, -0.8), "r_foot": (0.0, 1.0, 0.0),
                  "r_upperarm_twist": (0.2, 0.3, -1.0), "r_foretwist": (0.0, 1.0, -0.2),
                  "l_upperarm_twist": (0.6, 0.4, -0.7), "l_foretwist": (0.3, 0.2, -1.0),
                  "neck": (0.4, -0.6, 0.7)},
             rot={"pelvis": (85, 85, 0)}),
        # push up to a seat, feet drawn in under the knees
        dict(frame=37, root=(0, -0.08, -0.6),
             aim=dict(SPINE_RELEASE, **{
                  "l_thigh": (-0.15, 1.0, 0.45), "l_calf": (-0.05, -0.1, -1.0), "l_foot": (-0.05, 1.0, -0.15),
                  "r_thigh": (0.15, 1.0, 0.45), "r_calf": (0.05, -0.1, -1.0), "r_foot": (0.05, 1.0, -0.15),
                  "r_upperarm_twist": (0.4, -0.2, -1.0), "r_foretwist": (0.1, 0.0, -1.0),
                  "l_upperarm_twist": (-0.35, 0.3, -1.0), "l_foretwist": (-0.1, 0.5, -1.0)}),
             rot={"pelvis": (10, 20, 0), "spine": (4, 0, 0), "spine1": (0, 0, 0), "spine2": (0, 0, 0),
                  "neck": (0, 0, 0), "head": (0, 0, 0)}),
        # rock forward onto the feet, hands push off
        dict(frame=42, plant=("l_foot", "r_foot"),
             aim={"l_thigh": (-0.12, 1.0, -0.15), "l_calf": (-0.04, -0.9, -1.0), "l_foot": (-0.05, 1.0, -0.3),
                  "r_thigh": (0.12, 1.0, -0.25), "r_calf": (0.04, -0.9, -1.0), "r_foot": (0.05, 1.0, -0.3),
                  "l_upperarm_twist": (-0.35, 0.5, -1.0), "l_foretwist": (-0.1, 0.8, -0.3),
                  "r_upperarm_twist": (0.35, 0.5, -1.0), "r_foretwist": (0.1, 0.8, -0.3)},
             rot={"pelvis": (0, 0, 0), "spine": (-26, 0, 0), "spine1": (-12, 0, 0), "spine2": (-4, 0, 0),
                  "neck": (12, 0, 0), "head": (8, 0, 0)}),
        dict(frame=48, plant=("l_foot", "r_foot"),
             aim={"l_thigh": (-0.08, 0.45, -1.0), "l_calf": (-0.04, -0.3, -1.0), "l_foot": (-0.05, 1.0, -0.45),
                  "r_thigh": (0.1, 0.2, -1.0), "r_calf": (0.06, -0.35, -1.0), "r_foot": (0.1, 1.0, -0.45)},
             rot={"spine": (-10, 0, -2), "spine1": (-6, 0, -2), "spine2": (0, 0, 0), "neck": (4, 0, 0), "head": (3, 0, 0)}),
        dict(frame=54, **READY),
    ],
}

CLIPS = (HEAD_BUTT, DROP_KICK)


# ------------------------------------------------------------------ pose solver
class Rig:
    def __init__(self, arm):
        self.arm = arm
        self.rest = {b.name: b.matrix_local.copy() for b in arm.data.bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
        self.order = []

        def walk(b):
            self.order.append(b.name)
            for c in b.children:
                walk(c)
        for b in arm.data.bones:
            if b.parent is None:
                walk(b)
        self.root = self.order[0]
        for pb in arm.pose.bones:
            pb.rotation_mode = "QUATERNION"

    def fk(self, aim, rot, root_off):
        """Armature-space matrices of every bone for one key pose."""
        M = {}
        for bn in self.order:
            par = self.parent[bn]
            if par is None:
                inherited = Matrix.Translation(Vector(root_off)) @ self.rest[bn]
            else:
                inherited = M[par] @ self.rest[par].inverted() @ self.rest[bn]
            head = inherited.translation.copy()
            R = Matrix.Identity(3)
            if bn in rot:
                rx, ry, rz = rot[bn]
                R = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ").to_matrix()
            if bn in aim and bn in CHAIN_CHILD:
                c = CHAIN_CHILD[bn]
                off = (self.rest[bn].inverted() @ self.rest[c]).translation
                cur = (inherited.to_3x3() @ off).normalized()
                R = cur.rotation_difference(Vector(aim[bn]).normalized()).to_matrix() @ R
            about = Matrix.Translation(head) @ R.to_4x4() @ Matrix.Translation(-head)
            M[bn] = about @ inherited
        return M

    def plant(self, M, feet, refs):
        """Move the whole pose so the lowest planted ankle sits at the rest ankle height and, when some planted feet
        have an anchor (refs), so their midpoint keeps the anchors' X and Y. A foot with no anchor yet (it lands on
        this key) is only grounded. Returns the moved pose, the pelvis shift and each anchored ankle's slide."""
        cur = {f: M[f].translation for f in feet}
        low = min(c.z for c in cur.values())
        rest_z = min(self.rest[f].translation.z for f in feet)
        shift = Vector((0.0, 0.0, rest_z - low))
        held = [f for f in feet if f in refs]
        if held:
            ref_mid = sum((refs[f] for f in held), Vector()) / len(held)
            mid = sum((cur[f] for f in held), Vector()) / len(held)
            shift.x, shift.y = ref_mid.x - mid.x, ref_mid.y - mid.y
        T = Matrix.Translation(shift)
        M2 = {k: T @ v for k, v in M.items()}
        slide = {f: round(((M2[f].translation - refs[f]).to_2d()).length, 3) for f in held}
        return M2, shift, slide

    LEGS = (("l_thigh", "l_calf", "l_foot"), ("r_thigh", "r_calf", "r_foot"))

    def solve(self, aim, rot, root_off, feet, refs):
        """One key pose. With planted feet: plant, then aim each anchored leg by a two-bone IK so its ankle lands on
        the anchor (the knee bends in the plane the authored aim gave it), then plant again for the leftover."""
        M = self.fk(aim, rot, root_off)
        if not feet:
            return M, Vector(), {}
        M, shift, slide = self.plant(M, feet, refs)
        if not any(f in refs for f in feet):
            return M, shift, slide
        aim = dict(aim)
        for th, ca, fo in self.LEGS:
            if fo not in feet or fo not in refs:
                continue
            hip, knee0 = M[th].translation, M[ca].translation
            goal = Vector((refs[fo].x, refs[fo].y, self.rest[fo].translation.z))
            a = (self.rest[ca].translation - self.rest[th].translation).length
            b = (self.rest[fo].translation - self.rest[ca].translation).length
            d = goal - hip
            L = max(abs(a - b) + 1e-4, min(d.length, (a + b) * 0.999))
            n = d.normalized()
            pole = (knee0 - hip) - n * (knee0 - hip).dot(n)
            pole = pole.normalized() if pole.length > 1e-6 else Vector((0.0, 1.0, 0.0))
            x = (a * a - b * b + L * L) / (2.0 * L)
            knee = hip + n * x + pole * math.sqrt(max(a * a - x * x, 0.0))
            aim[th] = tuple(knee - hip)
            aim[ca] = tuple(hip + n * L - knee)
        root_total = Vector(root_off) + shift
        M = self.fk(aim, rot, root_total)
        M, shift2, slide = self.plant(M, feet, refs)
        return M, shift + shift2, slide

    def basis(self, M, root_yaw):
        """matrix_basis per bone. Only the root takes root_yaw: a child's basis is relative to its parent, so the
        turn rides down the hierarchy without changing any child's local."""
        out = {}
        for bn in self.order:
            par = self.parent[bn]
            if par is None:
                out[bn] = self.rest[bn].inverted() @ root_yaw @ M[bn]
            else:
                out[bn] = (self.rest[par].inverted() @ self.rest[bn]).inverted() @ M[par].inverted() @ M[bn]
        return out


def resolve_keys(clip):
    """Carry each bone's aim and rot forward from the previous key. A value of None releases that bone."""
    aim, rot, out = {}, {}, []
    for k in clip["keys"]:
        for src, dst in ((k.get("aim", {}), aim), (k.get("rot", {}), rot)):
            for bn, v in src.items():
                if v is None:
                    dst.pop(bn, None)
                else:
                    dst[bn] = v
        out.append({"frame": k["frame"], "aim": dict(aim), "rot": dict(rot),
                    "root": k.get("root", (0.0, 0.0, 0.0)), "plant": k.get("plant")})
    return out


def _lerp_dict(a, b, u, unit):
    out = {}
    for bn in set(a) | set(b):
        va, vb = Vector(a.get(bn, b.get(bn))), Vector(b.get(bn, a.get(bn)))
        v = va.normalized().lerp(vb.normalized(), u) if unit else va.lerp(vb, u)
        out[bn] = tuple(v)
    return out


def expand_planted_spans(keys):
    """Between two keys that plant the same foot, add a key on every frame, eased like Blender's Bezier
    (smoothstep), planting that foot. Bezier easing between the two keys alone ignores the linear travel, and the
    planted foot slid up to 4.8 cm a frame on the first run-up pass; solved per frame, it stays put."""
    out = []
    for k0, k1 in zip(keys, keys[1:]):
        out.append(k0)
        both = tuple(f for f in (k0["plant"] or ()) if f in (k1["plant"] or ()))
        if not both:
            continue
        for f in range(k0["frame"] + 1, k1["frame"]):
            u = (f - k0["frame"]) / float(k1["frame"] - k0["frame"])
            u = u * u * (3.0 - 2.0 * u)
            out.append({"frame": f, "aim": _lerp_dict(k0["aim"], k1["aim"], u, True),
                        "rot": _lerp_dict(k0["rot"], k1["rot"], u, False),
                        "root": tuple(Vector(k0["root"]).lerp(Vector(k1["root"]), u)),
                        "plant": both, "inbetween": True})
    out.append(keys[-1])
    return out


def travel_at(clip, frame):
    """Forward metres the engine has moved the agent by `frame`: displace_position moves it linearly from progress 0
    to the displacement usage's end progress (bannerlord-animation-system-map.md, displace_position). The Kit clip
    runs Source1 = 1, so progress 0 is frame 1."""
    t = clip.get("travel")
    if not t:
        return 0.0
    u = (frame - 1) / float(t["end_frame"] - 1)
    return t["distance"] * min(max(u, 0.0), 1.0)


def key_clip(rig, clip, action_name, root_yaw):
    """Key frame 0 as rest and every pose key; Blender's Bezier eases between keys and the FBX export bakes
    every frame. Returns the per-key report."""
    arm = rig.arm
    act = bpy.data.actions.get(action_name)
    if act is not None:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(action_name)
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    ad.action_slot = act.slots.new(id_type="OBJECT", name=arm.name)
    report = []
    prev_q = {}
    rest_pose = {bn: m.copy() for bn, m in rig.rest.items()}
    poses = [(0, rest_pose, None)]
    # A planted foot keeps its WORLD spot from the key it lands on to the last key that still plants it. The engine
    # moves the agent by the clip's displacement (travel), so in the clip's own space that spot slides back.
    world = {}
    for k in expand_planted_spans(resolve_keys(clip)):
        entry = {"frame": k["frame"]}
        back = Vector((0.0, travel_at(clip, k["frame"]), 0.0))
        feet = k["plant"] or ()
        world = {f: p for f, p in world.items() if f in feet}
        refs = {f: p - back for f, p in world.items()}
        M, shift, slide = rig.solve(k["aim"], k["rot"], k["root"], feet, refs)
        for f in feet:
            if f not in world:
                world[f] = M[f].translation.copy() + back
        if feet:
            entry["plant"] = list(feet)
            entry["plant_shift"] = [round(v, 3) for v in shift]
            entry["slide_m"] = slide
        entry["lowest_z"] = round(min(m.translation.z for m in M.values()), 3)
        poses.append((k["frame"], M, entry))
        if not k.get("inbetween"):
            report.append(entry)
    for frame, M, _ in poses:
        B = rig.basis(M, root_yaw)
        for bn in rig.order:
            pb = arm.pose.bones[bn]
            q = B[bn].to_quaternion()
            if bn in prev_q and prev_q[bn].dot(q) < 0.0:
                q.negate()
            prev_q[bn] = q
            pb.rotation_quaternion = q
            pb.location = B[bn].translation
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("location", frame=frame)
    report.append({"ground_clamp": ground_clamp(rig, act, range(1, poses[-1][0] + 1))})
    return act, report


def ground_clamp(rig, act, frames):
    """Bake the pelvis location on every frame and lift any frame whose lowest joint is below the ground. Easing
    between two grounded keys can pass through a pose with a toe under the floor (6.6 cm on the first drop kick
    pass), and a body lying on its side can put a shoulder there. A world Z lift is a pelvis location change of rest_rot^-1 @ (0, 0, dz); the root yaw is
    about Z, so it leaves Z alone. Returns the frames lifted and the largest lift."""
    arm = rig.arm
    pb = arm.pose.bones[rig.root]
    up_local = rig.rest[rig.root].to_3x3().inverted() @ Vector((0.0, 0.0, 1.0))
    scn = bpy.context.scene
    locs, lifted = {}, {}
    for f in frames:
        scn.frame_set(f)
        bpy.context.view_layer.update()
        low = min(p.head.z for p in arm.pose.bones)
        loc = pb.location.copy()
        if low < 0.0:
            loc = loc + up_local * (-low)
            lifted[f] = round(-low, 3)
        locs[f] = loc
    for f, loc in locs.items():
        pb.location = loc
        pb.keyframe_insert("location", frame=f)
    return {"frames": len(lifted), "max_lift_m": max(lifted.values()) if lifted else 0.0}


def contact_scan(rig, action, frames):
    """Per frame: lowest bone head height and each foot's ankle and toe height, from the evaluated pose."""
    arm = rig.arm
    arm.animation_data.action = action
    arm.animation_data.action_slot = action.slots[0]
    rows = []
    for f in frames:
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        pb = arm.pose.bones
        rows.append({"frame": f,
                     "min_z": round(min(p.head.z for p in pb), 3),
                     "ankles": [round(pb["l_foot"].head.z, 3), round(pb["r_foot"].head.z, 3)],
                     "toes": [round(pb["l_toe0"].head.z, 3), round(pb["r_toe0"].head.z, 3)],
                     "pelvis": [round(v, 3) for v in pb["pelvis"].head]})
    return rows


def main(engine_json, out_dir, export=True, only=None):
    rt = {"__name__": "taom_rt"}
    exec(open(RT_PATH, encoding="utf-8").read(), rt)
    scn = bpy.context.scene
    scn.render.fps = FPS
    arm = bpy.data.objects.get(ARMATURE_NAME)
    if arm is None:
        arm = rt["build_engine_rig"](engine_json, name=ARMATURE_NAME)
    rig = Rig(arm)
    no_yaw = Matrix.Identity(4)
    results = {}
    for clip in CLIPS:
        if only and clip["name"] not in only:
            continue
        n_last = clip["keys"][-1]["frame"]
        res = {"frames": n_last, "impact_frame": clip["impact_frame"],
               # the Kit clip runs Source1 = 1 to Source2 = last key; progress at the impact frame
               "impact_fraction": round((clip["impact_frame"] - 1) / float(n_last - 1), 3)}
        prev, rep = key_clip(rig, clip, clip["name"] + "_preview", no_yaw)
        res["keys"] = rep
        res["contact"] = contact_scan(rig, prev, range(1, n_last + 1, 1))
        res["min_z_all"] = min(r["min_z"] for r in res["contact"])
        if export:
            act, _ = key_clip(rig, clip, clip["name"], rt["KIT_ROOT_YAW"])

            class _T:   # export_clip takes the retarget script's target wrapper; it reads only .arm
                pass
            t = _T()
            t.arm = arm
            path = os.path.join(out_dir, clip["name"] + ".fbx")
            res["export"] = rt["export_clip"](t, act, path)
            if res["export"]["ok"]:
                res["engine_check"] = rt["check_export_against_engine"](path, engine_json)
            arm.animation_data.action = prev
            arm.animation_data.action_slot = prev.slots[0]
        results[clip["name"]] = res
    if export:
        with open(os.path.join(out_dir, "author_report.json"), "w", encoding="utf-8") as fh:
            json.dump(results, fh, indent=1)
    return results
