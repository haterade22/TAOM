"""
Pure math for fit_hair_morphs.py: make a hair or beard mesh follow the head's face morph channels (2026-09-28,
Saruman).

The engine morphs a race head's hair and beard by the same face channels as the head. Saruman's hair and beard
carry 101 channels, but the artist authored motion on only a few (hair 51, beard 67, 68, 71, 95, 100); on channel
46 the scalp moves 42 mm and the jaw 23 mm while the hair and beard roots move nothing, so in game the hair
floats off a scalp that the face sliders have moved. The Kit applies no morph, so it looks right there.

Method: every vertex is tied to a ROOT, a vertex of its own connected island lying within `contact` of the head
(the nearest one by edge hops, so a strand follows its own root and a scalp cap follows the scalp under each
part); an island with no vertex that close uses its vertex nearest the head. Each root is anchored to its `k`
nearest head vertices by inverse distance. A channel's offset for a vertex is the anchor's weighted head offset,
a pure translation, so strands keep their shape. Kept free of bpy so it can be tested outside Blender.
"""
import math
from collections import deque

CONTACT = 0.008   # m: a vertex this close to a head vertex is a root
K = 4


def islands(n, edges):
    """Connected-component id per vertex (union-find over `edges`, pairs of vertex indices)."""
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for a, b in edges:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    return [find(i) for i in range(n)]


def roots(n, edges, near_dist, contact=CONTACT):
    """Root vertex per vertex. `near_dist[i]` is vertex i's distance to the nearest head vertex."""
    adj = [[] for _ in range(n)]
    for a, b in edges:
        adj[a].append(b)
        adj[b].append(a)
    comp = islands(n, edges)
    root = [-1] * n
    queue = deque()
    for i in range(n):
        if near_dist[i] <= contact:
            root[i] = i
            queue.append(i)
    best = {}
    for i in range(n):
        c = comp[i]
        if c not in best or near_dist[i] < near_dist[best[c]]:
            best[c] = i
    for c, i in best.items():          # islands with no contact vertex: seed from the nearest one
        if root[i] == -1 and near_dist[i] > contact:
            root[i] = i
            queue.append(i)
    while queue:                        # multi-source BFS: each vertex takes its nearest root by hops
        a = queue.popleft()
        for b in adj[a]:
            if root[b] == -1:
                root[b] = root[a]
                queue.append(b)
    return root


def weights(dists):
    """Inverse-distance weights, exact hit wins."""
    for j, d in enumerate(dists):
        if d < 1e-9:
            return [1.0 if i == j else 0.0 for i in range(len(dists))]
    inv = [1.0 / d for d in dists]
    s = sum(inv)
    return [w / s for w in inv]


def anchor_offset(anchor, head_base, head_frame):
    """Weighted head offset for one anchor [(head index, weight), ...]."""
    x = y = z = 0.0
    for j, w in anchor:
        b, f = head_base[j], head_frame[j]
        x += w * (f[0] - b[0])
        y += w * (f[1] - b[1])
        z += w * (f[2] - b[2])
    return (x, y, z)


def follow(base, root, anchors, head_base, head_frame):
    """The object's positions for one channel: each vertex moved by its root's anchor offset."""
    cache = {}
    out = []
    for i, p in enumerate(base):
        r = root[i]
        if r not in cache:
            cache[r] = anchor_offset(anchors[r], head_base, head_frame)
        d = cache[r]
        out.append((p[0] + d[0], p[1] + d[1], p[2] + d[2]))
    return out


def max_motion(base, frame):
    return max(math.dist(a, b) for a, b in zip(base, frame)) if base else 0.0
