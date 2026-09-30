#!/usr/bin/env python3
"""
The geometry behind fitting a creature's clips to its body and its weapon (2026-09-30, hill troll first).

Pure numpy (scipy for the fist's cavity), no Blender, so every rule is unit-tested
(tools/tests/test_creature_fit_math.py). tools/creature_fit.py drives it; the Blender side only exports the mesh
and writes the corrected clips.

Engine conventions, each measured rather than assumed (docs/features/troll-race.md "Fine tuning"):
- A skeleton's rest frame is 16 floats whose ROWS are the bone's basis vectors and whose m12..m14 are its offset
  from the parent (tools/read_anim_keyframes_tpac.ps1 dumps them; retarget_mannequin_to_human._engine_rest_world
  reads them the same way). The fourth column is not trusted: TaleWorlds' own packages store 0 under it.
- A clip's bone local is T(offset) R(quat), quaternions stored w, x, y, z. A bone with no rotation track keeps
  its rest rotation; with no position track, its rest offset (the troll's masters carry no position track on any
  bone, so every bone length comes from the rig). The root's motion is a delta from its rest offset.
- A held weapon's frame IS its grip bone's frame (r_finger0 for the main hand): the grip at the bone's origin,
  the crafting pieces stacked along +Z, the striking face along -X. Measured on vanilla two-handed clips: the
  left grip stays within 0.7 to 3 cm of the right grip's Z line, and a swing's fastest frames move along -X.
"""
import math

import numpy as np


# ------------------------------------------------------------------------------------------------ rotations
def quat_to_mat(q):
    """w, x, y, z (normalised here) -> 3x3. Works on (..., 4)."""
    q = np.asarray(q, dtype=float)
    q = q / np.linalg.norm(q, axis=-1, keepdims=True)
    w, x, y, z = q[..., 0], q[..., 1], q[..., 2], q[..., 3]
    R = np.empty(q.shape[:-1] + (3, 3))
    R[..., 0, 0] = 1 - 2 * (y * y + z * z)
    R[..., 0, 1] = 2 * (x * y - z * w)
    R[..., 0, 2] = 2 * (x * z + y * w)
    R[..., 1, 0] = 2 * (x * y + z * w)
    R[..., 1, 1] = 1 - 2 * (x * x + z * z)
    R[..., 1, 2] = 2 * (y * z - x * w)
    R[..., 2, 0] = 2 * (x * z - y * w)
    R[..., 2, 1] = 2 * (y * z + x * w)
    R[..., 2, 2] = 1 - 2 * (x * x + y * y)
    return R


def mat_to_quat(R):
    """3x3 -> w, x, y, z with w >= 0 (Shepperd's method)."""
    R = np.asarray(R, dtype=float)
    t = R[0, 0] + R[1, 1] + R[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        q = [0.25 * s, (R[2, 1] - R[1, 2]) / s, (R[0, 2] - R[2, 0]) / s, (R[1, 0] - R[0, 1]) / s]
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = math.sqrt(1.0 + R[0, 0] - R[1, 1] - R[2, 2]) * 2
        q = [(R[2, 1] - R[1, 2]) / s, 0.25 * s, (R[0, 1] + R[1, 0]) / s, (R[0, 2] + R[2, 0]) / s]
    elif R[1, 1] > R[2, 2]:
        s = math.sqrt(1.0 + R[1, 1] - R[0, 0] - R[2, 2]) * 2
        q = [(R[0, 2] - R[2, 0]) / s, (R[0, 1] + R[1, 0]) / s, 0.25 * s, (R[1, 2] + R[2, 1]) / s]
    else:
        s = math.sqrt(1.0 + R[2, 2] - R[0, 0] - R[1, 1]) * 2
        q = [(R[1, 0] - R[0, 1]) / s, (R[0, 2] + R[2, 0]) / s, (R[1, 2] + R[2, 1]) / s, 0.25 * s]
    q = np.array(q)
    q /= np.linalg.norm(q)
    return -q if q[0] < 0 else q


def slerp(q0, q1, u):
    """Spherical interpolation on the short arc."""
    q0 = np.asarray(q0, dtype=float)
    q1 = np.asarray(q1, dtype=float)
    d = float(np.dot(q0, q1))
    if d < 0:
        q1, d = -q1, -d
    if d > 0.9995:
        q = q0 + u * (q1 - q0)
        return q / np.linalg.norm(q)
    th = math.acos(d)
    return (math.sin((1 - u) * th) * q0 + math.sin(u * th) * q1) / math.sin(th)


def rotation_between(u, v):
    """The smallest rotation taking direction u onto direction v."""
    u = np.asarray(u, dtype=float) / np.linalg.norm(u)
    v = np.asarray(v, dtype=float) / np.linalg.norm(v)
    axis = np.cross(u, v)
    s = np.linalg.norm(axis)
    c = float(np.dot(u, v))
    if s < 1e-12:
        if c > 0:
            return np.eye(3)
        p = np.cross(u, [1.0, 0.0, 0.0])
        if np.linalg.norm(p) < 1e-6:
            p = np.cross(u, [0.0, 1.0, 0.0])
        p /= np.linalg.norm(p)
        return 2 * np.outer(p, p) - np.eye(3)
    k = axis / s
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + s * K + (1 - c) * (K @ K)


def similarity_fit(src, dst):
    """The rotation, uniform scale and shift taking points `src` onto `dst` (Umeyama, never a mirror), with the
    largest residual: how an FBX's Blender frame maps onto the engine's, read off the bone positions both carry."""
    src = np.asarray(src, dtype=float)
    dst = np.asarray(dst, dtype=float)
    ms, md = src.mean(axis=0), dst.mean(axis=0)
    xs, xd = src - ms, dst - md
    U, S, Vt = np.linalg.svd(xd.T @ xs / len(src))
    D = np.diag([1.0, 1.0, np.sign(np.linalg.det(U @ Vt)) or 1.0])
    R = U @ D @ Vt
    scale = float(np.trace(np.diag(S) @ D) / (np.sum(xs * xs) / len(src)))
    t = md - scale * R @ ms
    fit = {"R": R, "scale": scale, "t": t}
    fit["residual_max"] = float(np.max(np.linalg.norm(apply_similarity(fit, src) - dst, axis=1)))
    return fit


def apply_similarity(fit, pts):
    return fit["scale"] * np.asarray(pts, dtype=float) @ fit["R"].T + fit["t"]


def axis_angle(v):
    """The rotation matrix of a rotation vector (axis times angle in radians)."""
    v = np.asarray(v, dtype=float)
    th = float(np.linalg.norm(v))
    if th < 1e-12:
        return np.eye(3)
    k = v / th
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(th) * K + (1 - math.cos(th)) * (K @ K)


def catmull_rom(times, values, at):
    """A Catmull-Rom spline through keyed values (K, D) at increasing `times`, sampled `at`: it passes through
    every key with continuous velocity; the ends hold their key outside the keyed span."""
    times = np.asarray(times, dtype=float)
    V = np.asarray(values, dtype=float)
    K = len(times)
    out = []
    for t in np.atleast_1d(np.asarray(at, dtype=float)):
        if t <= times[0]:
            out.append(V[0])
            continue
        if t >= times[-1]:
            out.append(V[-1])
            continue
        i = int(np.searchsorted(times, t, side="right")) - 1
        t0, t1 = times[i], times[i + 1]
        h = t1 - t0
        u = (t - t0) / h

        def tangent(j):
            a, b = max(j - 1, 0), min(j + 1, K - 1)
            return (V[b] - V[a]) / (times[b] - times[a]) * h
        m0, m1 = tangent(i), tangent(i + 1)
        u2, u3 = u * u, u * u * u
        out.append((2 * u3 - 3 * u2 + 1) * V[i] + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * V[i + 1]
                   + (u3 - u2) * m1)
    return np.array(out)


def ease_series(values, lead, sigma):
    """A per-frame correction eased in and out: each frame takes the largest need within `lead` frames either side
    (so a turn starts before the contact it avoids), then a Gaussian of `sigma` frames smooths it.
    Works on (F,) or (F, k) arrays; never below the raw need where the raw need was largest."""
    x = np.asarray(values, dtype=float)
    flat = x.reshape(len(x), -1)
    mag = np.linalg.norm(flat, axis=1)
    F = len(x)
    held = np.empty_like(flat)
    for f in range(F):
        lo, hi = max(0, f - lead), min(F, f + lead + 1)
        j = lo + int(np.argmax(mag[lo:hi]))
        held[f] = flat[j]
    if sigma > 0:
        r = int(math.ceil(3 * sigma))
        k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
        k /= k.sum()
        pad = np.pad(held, ((r, r), (0, 0)), mode="edge")
        held = np.stack([np.convolve(pad[:, c], k, mode="valid") for c in range(held.shape[1])], axis=1)
    return held.reshape(x.shape)


def angle_deg(R):
    """The rotation angle of a 3x3 rotation, in degrees."""
    return math.degrees(math.acos(max(-1.0, min(1.0, (float(np.trace(R)) - 1) / 2))))


# ------------------------------------------------------------------------------------------------ skeleton
def rest_local(m16):
    """An engine rest frame (16 floats, rows = basis vectors, m12..m14 = offset) -> (R, p), column vectors."""
    m = m16
    R = np.array([[m[0], m[4], m[8]], [m[1], m[5], m[9]], [m[2], m[6], m[10]]], dtype=float)
    return R, np.array([m[12], m[13], m[14]], dtype=float)


class Skeleton:
    """An engine skeleton in list order (parents before children), from a keyframe JSON or an engine dump."""

    def __init__(self, names, parents, rest_R, rest_p):
        self.names = list(names)
        self.parents = list(parents)
        self.rest_R = np.asarray(rest_R, dtype=float)
        self.rest_p = np.asarray(rest_p, dtype=float)
        self._index = {n: i for i, n in enumerate(self.names)}
        for i, pi in enumerate(self.parents):
            if pi >= i:
                raise ValueError("bone %s lists its parent after itself" % self.names[i])

    @classmethod
    def from_spec(cls, spec):
        bones = spec["bones"]
        names = [b["name"] for b in bones]
        index = {n: i for i, n in enumerate(names)}
        parents = [index[b["parent"]] if b.get("parent") else -1 for b in bones]
        Rs, ps = zip(*(rest_local(b["rest"]) for b in bones))
        return cls(names, parents, np.array(Rs), np.array(ps))

    def index(self, name):
        return self._index[name]

    def rest_world(self):
        Rw, pw = fk(self, self.rest_R[None], self.rest_p[None])
        return Rw[0], pw[0]


def fk(sk, R_local, p_local):
    """Local frames (F, B, 3, 3) and offsets (F, B, 3) -> world frames and positions, down the list order."""
    R_local = np.asarray(R_local, dtype=float)
    p_local = np.asarray(p_local, dtype=float)
    Rw = np.empty_like(R_local)
    pw = np.empty_like(p_local)
    for i, pi in enumerate(sk.parents):
        if pi < 0:
            Rw[:, i] = R_local[:, i]
            pw[:, i] = p_local[:, i]
        else:
            Rw[:, i] = Rw[:, pi] @ R_local[:, i]
            pw[:, i] = pw[:, pi] + np.einsum("fij,fj->fi", Rw[:, pi], p_local[:, i])
    return Rw, pw


def _track(frames, keys):
    t = np.array([f["t"] for f in frames], dtype=float)
    v = np.array([[f[k] for k in keys] for f in frames], dtype=float)
    return t, v


def _sample(t, v, at, is_quat):
    if at <= t[0]:
        return v[0]
    if at >= t[-1]:
        return v[-1]
    hi = int(np.searchsorted(t, at, side="right"))
    lo = hi - 1
    span = t[hi] - t[lo]
    u = (at - t[lo]) / span if span > 1e-9 else 0.0
    if is_quat:
        return slerp(v[lo], v[hi], u)
    return v[lo] + u * (v[hi] - v[lo])


def clip_length(spec):
    """A master's length: its last key plus one (vanilla masters key sparsely; the Duration field is the root
    track's key count, not the length)."""
    last = 0.0
    for a in spec.get("boneAnims", []):
        for tr in (a.get("rot") or [], a.get("pos") or []):
            if tr:
                last = max(last, tr[-1]["t"])
    root = (spec.get("root") or {}).get("pos") or []
    if root:
        last = max(last, root[-1]["t"])
    return int(round(last)) + 1


def sample_clip(sk, spec, frames):
    """Clip locals at the given frame times -> (R_local (F, B, 3, 3), p_local (F, B, 3)). Interpolated the way the
    engine plays a sparse master: slerp between rotation keys, lerp between position keys."""
    frames = list(frames)
    F, B = len(frames), len(sk.names)
    R = np.repeat(sk.rest_R[None], F, axis=0).copy()
    p = np.repeat(sk.rest_p[None], F, axis=0).copy()
    anims = {a["bone"]: a for a in spec.get("boneAnims", [])}
    for bi, name in enumerate(sk.names):
        a = anims.get(name)
        if not a:
            continue
        if a.get("rot"):
            t, v = _track(a["rot"], ("w", "x", "y", "z"))
            R[:, bi] = quat_to_mat(np.array([_sample(t, v, f, True) for f in frames]))
        if a.get("pos"):
            t, v = _track(a["pos"], ("x", "y", "z"))
            p[:, bi] = np.array([_sample(t, v, f, False) for f in frames])
    root = (spec.get("root") or {}).get("pos") or []
    if root:
        t, v = _track(root, ("x", "y", "z"))
        delta = np.array([_sample(t, v, f, False) for f in frames])
        for bi, pi in enumerate(sk.parents):
            if pi < 0:
                p[:, bi] = p[:, bi] + delta
    return R, p


# ------------------------------------------------------------------------------------------------ skin
def skin(v_rest, idx, w, R_rest_w, p_rest_w, R_w, p_w):
    """Linear blend skinning: each influence moves the vertex with its bone from the rest world frame to the posed
    one. v_rest (N, 3), idx (N, K) bone indices, w (N, K) weights summing to 1, frames (B, 3, 3) and (B, 3)."""
    A = np.einsum("bij,bkj->bik", R_w, R_rest_w)                  # R_w @ R_rest^T per bone
    t = p_w - np.einsum("bij,bj->bi", A, p_rest_w)
    moved = np.einsum("nkij,nj->nki", A[idx], v_rest) + t[idx]    # (N, K, 3)
    return np.einsum("nk,nki->ni", w, moved)


# ------------------------------------------------------------------------------------------------ weapon
def crafted_layout(pieces):
    """Where each crafted piece sits along the grip's Z. The handle comes first: its centre is `piece_offset` up
    from the grip (the hill troll's 0.95 m on a 3.51 m handle puts the grip 80.5 cm above the butt); each later
    piece is seated flush on the one before (a piece's distance to its neighbour is half its length,
    TaleWorlds.Core.CraftingPiece). Lengths in metres."""
    if not pieces or pieces[0].get("role") != "handle":
        raise ValueError("the layout starts at the handle")
    out = []
    top = None
    for pc in pieces:
        L = float(pc["length"])
        centre = float(pc.get("piece_offset", 0.0)) if top is None else top + L / 2
        out.append({"mesh": pc["mesh"], "role": pc["role"], "length": L, "z_centre": centre,
                    "z_min": centre - L / 2, "z_max": centre + L / 2})
        top = centre + L / 2
    return out


# ------------------------------------------------------------------------------------------------ shapes
def line_point(p, a, d):
    """(distance of p from the line through a along d, p's position along d from a)."""
    d = np.asarray(d, dtype=float) / np.linalg.norm(d)
    r = np.asarray(p, dtype=float) - np.asarray(a, dtype=float)
    along = float(r @ d)
    return float(np.linalg.norm(r - along * d)), along


def capsule_depth(points, a, b, r):
    """Per point: radius minus the distance to segment ab (positive inside the capsule)."""
    points = np.asarray(points, dtype=float)
    ab = np.asarray(b, dtype=float) - np.asarray(a, dtype=float)
    u = np.clip(((points - a) @ ab) / float(ab @ ab), 0.0, 1.0)
    near = np.asarray(a, dtype=float) + u[:, None] * ab
    return r - np.linalg.norm(points - near, axis=1)


def box_depth(points, centre, axes, half):
    """Per point: the depth inside an oriented box (its axes are the columns of `axes`), by the nearest face.
    Negative outside (not a true distance there, only the sign is meant)."""
    local = (np.asarray(points, dtype=float) - np.asarray(centre, dtype=float)) @ np.asarray(axes, dtype=float)
    return np.min(np.asarray(half, dtype=float) - np.abs(local), axis=1)


def surface_depth(points, verts, normals, reach):
    """How far each point is under a closed skin (positive inside, negative outside), from the nearest skin vertex
    and its outward normal. A point with no skin vertex within `reach` counts as outside: the test only judges
    points near the surface, which is where one body part enters another."""
    from scipy.spatial import cKDTree
    points = np.asarray(points, dtype=float)
    dist, idx = cKDTree(np.asarray(verts, dtype=float)).query(points)
    depth = -np.einsum("ij,ij->i", points - verts[idx], normals[idx])
    depth[dist > reach] = -1.0
    return depth


def skin_normals(n_rest, idx, w, R_rest_w, R_w):
    """Rest normals turned by each vertex's blended bone rotation (enough to tell inside from outside)."""
    A = np.einsum("bij,bkj->bik", R_w, R_rest_w)
    n = np.einsum("nk,nkij,nj->ni", w, A[idx], n_rest)
    return n / np.linalg.norm(n, axis=1, keepdims=True)


def fist_cavity(points, grip_R, grip_p, zone_half, reach, nbins=36, band=0.03):
    """The hole a closed hand leaves for a shaft, read in the grip bone's frame (the shaft runs along its Z).

    Hand vertices within `zone_half` of the grip along Z and within `reach` of the Z axis are projected on the
    grip's XY plane; the cavity is the largest empty circle centred inside their hull (a Voronoi vertex).
    Returns {"centre": its XY offset from the grip axis (m), "radius" (m), "coverage": the share of `nbins` angle
    sectors round the centre holding a vertex within radius + band (a wrapped fist reads near 1), "n": points}."""
    from scipy.spatial import Delaunay, Voronoi

    local = (np.asarray(points, dtype=float) - np.asarray(grip_p, dtype=float)) @ np.asarray(grip_R, dtype=float)
    keep = (np.abs(local[:, 2]) <= zone_half) & (np.hypot(local[:, 0], local[:, 1]) <= reach)
    xy = np.unique(np.round(local[keep, :2], 9), axis=0)
    if len(xy) < 4:
        return {"centre": None, "radius": None, "coverage": 0.0, "n": int(len(xy))}
    hull = Delaunay(xy)
    vor = Voronoi(xy)
    best, best_r = None, -1.0
    for v in vor.vertices:
        if hull.find_simplex(v) < 0:
            continue
        r = float(np.min(np.linalg.norm(xy - v, axis=1)))
        if r > best_r:
            best, best_r = v, r
    if best is None:
        return {"centre": None, "radius": None, "coverage": 0.0, "n": int(len(xy))}
    rel = xy - best
    dist = np.linalg.norm(rel, axis=1)
    near = rel[dist <= best_r + band]
    sectors = np.floor((np.arctan2(near[:, 1], near[:, 0]) + math.pi) / (2 * math.pi) * nbins).astype(int) % nbins
    return {"centre": np.asarray(best, dtype=float), "radius": best_r,
            "coverage": len(set(sectors.tolist())) / float(nbins), "n": int(len(xy))}


# ------------------------------------------------------------------------------------------------ IK
def _slerp_mat(A, B, u):
    if u >= 1.0:
        return B
    if u <= 0.0:
        return A
    return quat_to_mat(slerp(mat_to_quat(A), mat_to_quat(B), u))


def solve_offhand(sk, R_local, p_local, main_grip, arm, along, weight, pole_dir=None):
    """Put the off hand's grip on the main grip's shaft: `along` metres up its Z (negative: toward the butt), its
    own Z along the shaft's (the thumbs face the head, as every vanilla two-handed clip holds it), its roll the
    clip's (the smallest turn that aligns the Z axes). Per frame `weight` in [0, 1] blends the corrected locals
    into the clip's (0 where the source hand lets go of the weapon). See solve_arm for the chain and the report."""
    Rw, pw = fk(sk, R_local, p_local)
    g, gr = sk.index(main_grip), sk.index(arm["grip"])
    Z = Rw[:, g, :, 2]
    along = np.broadcast_to(np.asarray(along, dtype=float), (len(R_local),))   # one value, or one per frame
    target_p = pw[:, g] + along[:, None] * Z
    target_R = np.array([rotation_between(Rw[f, gr][:, 2], Z[f]) @ Rw[f, gr] for f in range(len(R_local))])
    return solve_arm(sk, R_local, p_local, arm, target_p, target_R, weight, pole_dir)


def solve_arm(sk, R_local, p_local, arm, target_p, target_R, weight, pole_dir=None):
    """Move an arm so its grip bone sits at target_p (F, 3) with world rotation target_R (F, 3, 3). A two-bone IK
    from the shoulder moves the upper arm and the forearm, then the hand turns; the twist bones keep their
    locals, so the roll stays spread as the clip spread it. The elbow bends toward `pole_dir` (F, 3, world) when
    given, else keeps the clip's bend plane (which means nothing for an arm the clip left hanging). `arm` names
    the chain: upper, upper_twist, fore, fore_twist, hand, grip. Per frame `weight` in [0, 1] blends the corrected
    locals into the clip's. Returns (new R_local, {"miss_m": per frame, the grip's distance from its target})."""
    R_out = np.array(R_local, dtype=float, copy=True)
    Rw, pw = fk(sk, R_local, p_local)
    ua, ut, fa, ft, ha, gr = (sk.index(arm[k]) for k in ("upper", "upper_twist", "fore", "fore_twist", "hand",
                                                          "grip"))
    ua_parent = sk.parents[ua]
    miss = np.zeros(len(R_local))
    for f in range(len(R_local)):
        w = float(weight[f])
        if w <= 0.0:
            continue
        target = np.asarray(target_p[f], dtype=float)
        Rh_t = np.asarray(target_R[f], dtype=float) @ R_local[f, gr].T
        wrist_t = target - Rh_t @ p_local[f, gr]
        S, E, W = pw[f, ua], pw[f, fa], pw[f, ha]
        pole = np.asarray(pole_dir[f], dtype=float) if pole_dir is not None else E - (S + W) / 2
        E2, W2 = two_bone_ik(S, E, W, wrist_t, pole=pole)
        Rua = rotation_between(E - S, E2 - S) @ Rw[f, ua]
        Rut = Rua @ R_local[f, ut]
        Rfa0 = Rut @ R_local[f, fa]
        W_mid = E2 + Rfa0 @ (p_local[f, ft] + R_local[f, ft] @ p_local[f, ha])
        Rfa = rotation_between(W_mid - E2, W2 - E2) @ Rfa0
        Rft = Rfa @ R_local[f, ft]
        parent_R = Rw[f, ua_parent] if ua_parent >= 0 else np.eye(3)
        new = {ua: parent_R.T @ Rua, fa: Rut.T @ Rfa, ha: Rft.T @ Rh_t}
        for bi, Rn in new.items():
            R_out[f, bi] = _slerp_mat(R_local[f, bi], Rn, w)
        miss[f] = float(np.linalg.norm((W2 + Rh_t @ p_local[f, gr]) - target))
    return R_out, {"miss_m": miss}


def project_into_balls(x0, centres, radii, iterations=500, tol=1e-10):
    """The point nearest x0 that lies inside every ball (Dykstra's projection; plain alternating projections only
    find SOME point of the intersection, which moves a swing further than it has to). Balls that do not meet give
    the middle of the gap between the two nearest surfaces: the best both arms can do."""
    x = np.asarray(x0, dtype=float).copy()
    centres = [np.asarray(c, dtype=float) for c in centres]
    inc = [np.zeros(3) for _ in centres]

    def proj(y, c, r):
        d = y - c
        n = float(np.linalg.norm(d))
        return y if n <= r else c + d * (r / n)
    for _ in range(iterations):
        prev = x.copy()
        for i, (c, r) in enumerate(zip(centres, radii)):
            y = proj(x + inc[i], c, r)
            inc[i] = x + inc[i] - y
            x = y
        if float(np.linalg.norm(x - prev)) < tol:
            break
    if all(np.linalg.norm(x - c) <= r + 1e-6 for c, r in zip(centres, radii)):
        return x
    if len(centres) == 2:
        a, b = centres
        u = (b - a) / np.linalg.norm(b - a)
        gap = float(np.linalg.norm(b - a)) - radii[0] - radii[1]
        if gap > 0:
            return a + u * (radii[0] + gap / 2)
    return x


def two_bone_ik(a, b, c, target, pole):
    """New (middle, end) positions for the chain a-b-c reaching `target`, bone lengths kept, bending toward the
    `pole` direction (a world vector; the chain's current bend is the fallback). An unreachable target straightens
    the chain toward it."""
    a, b, c, target = (np.asarray(x, dtype=float) for x in (a, b, c, target))
    l1, l2 = np.linalg.norm(b - a), np.linalg.norm(c - b)
    to = target - a
    d = np.linalg.norm(to)
    u = to / d if d > 1e-12 else (c - a) / np.linalg.norm(c - a)
    if d >= l1 + l2:
        return a + u * l1, a + u * (l1 + l2)
    d = max(d, abs(l1 - l2) + 1e-9)
    cos_a = (l1 * l1 + d * d - l2 * l2) / (2 * l1 * d)
    sin_a = math.sqrt(max(0.0, 1 - cos_a * cos_a))
    v = None
    for hint in (np.asarray(pole, dtype=float), b - a):
        perp = hint - (hint @ u) * u
        if np.linalg.norm(perp) > 1e-9:
            v = perp / np.linalg.norm(perp)
            break
    if v is None:
        v = np.cross(u, [0.0, 0.0, 1.0])
        if np.linalg.norm(v) < 1e-9:
            v = np.cross(u, [1.0, 0.0, 0.0])
        v /= np.linalg.norm(v)
    mid = a + l1 * (cos_a * u + sin_a * v)
    return mid, a + u * d
