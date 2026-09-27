"""
Make a head's eyeballs follow their sockets through the face morph channels (2026-09-26, the female dwarf).

Pure math, stdlib only, so it is tested without Blender (tools/tests/test_eye_follow.py); fit_eye_morphs.py
applies it to an FBX.

WHY
The engine's static face morph moves the head's LOD0 sub-meshes channel by channel. On a working head the
`.eye` channels carry the eyeball along with its socket: the male dwarf's socket ring moves 6.2 mm on channel
14 and his eye 8.0 mm. The female dwarf's `.eye` had no channels at all (#385, a crash), and
`add_face_morph_channels.py` gave it 101 zero-offset ones. That stopped the crash, but her sockets still move
up to 8.5 mm while her eyeballs stay put, so in game the socket's skin and a dark gap show where the eye
should be. The Kit shows her with no morph applied, so she looks right there.

WHAT IT COMPUTES
For each eye (the eyeball vertices split into two clusters along the axis that separates them), the socket
ring is every head vertex within half an eyeball's width of that eyeball. For each channel, the ring's motion
is fitted as a translation plus a uniform scale about the eyeball's centre (least squares), and the eyeball is
moved by that same fit. A ring that does not move gives an eye that does not move.
"""
import math

SCALE_LIMITS = (0.5, 2.0)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def _mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _mean(points):
    n = len(points)
    if not n:
        raise ValueError("no points")
    return (sum(p[0] for p in points) / n, sum(p[1] for p in points) / n, sum(p[2] for p in points) / n)


def split_eyes(eye):
    """Split the eyeball vertices into the two eyes: along the axis where the vertex spread is widest (the
    two eyes sit apart on it), at the midpoint of that spread. Returns two index lists, lower side first."""
    if len(eye) < 2:
        raise ValueError("an eye mesh needs at least two vertices")
    spans = [max(p[k] for p in eye) - min(p[k] for p in eye) for k in range(3)]
    axis = spans.index(max(spans))
    mid = (max(p[axis] for p in eye) + min(p[axis] for p in eye)) / 2.0
    low = [i for i, p in enumerate(eye) if p[axis] < mid]
    high = [i for i, p in enumerate(eye) if p[axis] >= mid]
    if not low or not high:
        raise ValueError("the eye vertices do not form two eyes")
    return low, high


def width(points):
    """The widest axis-aligned extent of a set of points."""
    return max(max(p[k] for p in points) - min(p[k] for p in points) for k in range(3))


def socket_ring(head, eyeball, radius=None):
    """Head vertex indices within `radius` of any eyeball vertex (default half the eyeball's width)."""
    r = width(eyeball) / 2.0 if radius is None else radius
    return [i for i, p in enumerate(head) if any(math.dist(p, q) < r for q in eyeball)]


def fit_similarity(points, moved, centre):
    """Least-squares translation t and uniform scale a about `centre` taking `points` to `moved`:
    moved_i ~ centre + t + a * (points_i - centre). Returns (t, a), a clamped to SCALE_LIMITS."""
    q = [_sub(p, centre) for p in points]
    y = [_sub(m, centre) for m in moved]
    mq, my = _mean(q), _mean(y)
    num = sum(_dot(_sub(qi, mq), _sub(yi, my)) for qi, yi in zip(q, y))
    den = sum(_dot(_sub(qi, mq), _sub(qi, mq)) for qi in q)
    a = num / den if den > 0 else 1.0
    a = min(max(a, SCALE_LIMITS[0]), SCALE_LIMITS[1])
    t = _sub(my, _mul(mq, a))
    return t, a


def follow(head_basis, head_key, eye_basis, rings=None):
    """The eyeball's positions for one channel: each eye moved by the fit of its own socket ring from
    `head_basis` to `head_key`. `rings` (from socket_ring per eye) can be passed in to avoid recomputing."""
    out = list(eye_basis)
    for n, side in enumerate(split_eyes(eye_basis)):
        ball = [eye_basis[i] for i in side]
        ring = rings[n] if rings is not None else socket_ring(head_basis, ball)
        if not ring:
            raise ValueError("no head vertex near eye %d" % n)
        centre = _mean(ball)
        t, a = fit_similarity([head_basis[i] for i in ring], [head_key[i] for i in ring], centre)
        for i in side:
            out[i] = _add(_add(centre, t), _mul(_sub(eye_basis[i], centre), a))
    return out


def rings_for(head_basis, eye_basis):
    """The socket ring of each eye, in split_eyes order."""
    return [socket_ring(head_basis, [eye_basis[i] for i in side]) for side in split_eyes(eye_basis)]
