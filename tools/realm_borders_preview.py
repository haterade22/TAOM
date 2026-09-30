#!/usr/bin/env python3
"""Offline preview of TAOM's realm borders on the real campaign map (read-only).

Renders what the approved realm-borders design (docs/reviews/adopt-kingdom-borders-2026-09-30.md) would
draw, so the territory model and the look can be tuned without the game:

  compare OUT.png                  Kingdom Borders' model (unweighted nearest settlement on a 150 grid)
                                   next to TAOM's terrain-aware provinces, whole map
  capture OUT.png --capture F=K    the same province map before and after fiefs change hands
  looks OUT.png [--crop ...]       the six candidate looks, textured with the ImagineArt tiles
  tiles                            rebuild the seamless tiles in tools/realm_border_art/ from the
                                   ImagineArt originals there

The territory model: one province per fief (a town or castle plus its villages), grown by a
multi-source cost-distance flood over TAOM_Map's heightmap. Sea and lakes are never claimed, coherent
steep ranges are walls, slopes cost more, towns start ahead of castles and castles ahead of villages,
and land beyond the maximum claim cost stays wild. Borders are province edges whose owners differ,
simplified (Ramer-Douglas-Peucker) then smoothed (Chaikin). In game the cost grid comes from navmesh
terrain types instead, which also makes rivers into borders; the heightmap is this tool's stand-in.

Inputs, all read-only: TAOM_Map's settlements.xml and AssetSources/Support/terrain_heightmap.png,
vanilla SandBox spclans.xml transformed by the repo's spclans.xslt, and TAOM's characters/clans.xml.
Needs numpy, scipy, Pillow and lxml; exits 2 without them or without the install. Writes only the
requested image (and, for `tiles`, the tile PNGs beside their originals).
"""
import argparse
import os
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
ART = HERE / "realm_border_art"

WORLD = 1600.0            # TAOM_Map terrain: node_dimension 16 x node_size 100
HMIN, HMAX = 41.160, 99.926
HEAD_START = {"town": 30.0, "castle": 18.0, "village": 6.0}

# Draft, lore-leaning colours for previews only. The shipped palette is a data file (proposal 2).
PREVIEW_COLOURS = {
    "empire_w": ("Gondor", (206, 222, 238)), "empire_s": ("Mordor", (200, 36, 28)),
    "vlandia": ("Rohan", (232, 184, 46)), "empire": ("Dunland", (156, 98, 178)),
    "isengard": ("Isengard", (250, 250, 246)), "aserai": ("Harad", (222, 124, 42)),
    "battania": ("Khand", (128, 180, 64)), "khuzait": ("Rhun", (64, 164, 164)),
    "dolguldur": ("Dol Guldur", (120, 96, 156)), "lothlorien": ("Lothlorien", (236, 226, 120)),
    "mirkwood": ("Mirkwood", (96, 176, 96)), "erebor": ("Erebor", (60, 120, 200)),
    "sturgia": ("Dale", (72, 150, 230)), "rivendell": ("Rivendell", (180, 200, 255)),
    "gundabad": ("Gundabad", (170, 60, 50)), "mistymountainorcs": ("Misty Mountains", (150, 110, 70)),
    "goblin": ("Goblin Town", (120, 140, 60)), "bluecraig": ("Blue Craig", (90, 110, 170)),
    "umbar": ("Umbar", (170, 40, 90)), "shaghana": ("Shaghana", (230, 150, 90)),
    "abanissa": ("Abanissa", (40, 90, 170)), "lindon": ("Lindon", (150, 230, 210)),
}
# Wars for the war-front look only; the game reads real war state (proposal 4).
PREVIEW_WARS = {frozenset(p) for p in (
    ("empire_w", "empire_s"), ("vlandia", "empire_s"), ("vlandia", "isengard"), ("vlandia", "empire"),
    ("empire_w", "umbar"), ("empire_w", "aserai"), ("lothlorien", "dolguldur"), ("mirkwood", "dolguldur"))}

try:
    import numpy as np
    from scipy import ndimage
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import dijkstra
except ImportError as exc:  # the pure helpers below need numpy and scipy
    np = None
    _IMPORT_ERROR = exc
else:
    _IMPORT_ERROR = None


# ---- pure geometry and territory model (unit-tested) --------------------------------------------

def rdp(points, eps):
    """Ramer-Douglas-Peucker: keep the endpoints, drop points within eps of the chord."""
    p = np.asarray(points, dtype=np.float64)
    if len(p) < 3:
        return p
    a, b = p[0], p[-1]
    ab = b - a
    length = float(np.hypot(*ab))
    if length < 1e-12:
        d = np.hypot(*(p - a).T)
    else:
        d = np.abs(ab[0] * (p[:, 1] - a[1]) - ab[1] * (p[:, 0] - a[0])) / length
    i = int(np.argmax(d))
    if d[i] <= eps:
        return np.vstack([a, b])
    return np.vstack([rdp(p[:i + 1], eps)[:-1], rdp(p[i:], eps)])


def chaikin(points, iterations, closed=False):
    """Chaikin corner cutting. An open curve keeps its endpoints, so chains still meet at junctions."""
    p = np.asarray(points, dtype=np.float64)
    for _ in range(iterations):
        if len(p) < 3 and not closed:
            return p
        if closed:
            q = np.roll(p, -1, axis=0)
            p = np.stack([0.75 * p + 0.25 * q, 0.25 * p + 0.75 * q], axis=1).reshape(-1, 2)
        else:
            q = p[1:]
            mid = np.stack([0.75 * p[:-1] + 0.25 * q, 0.25 * p[:-1] + 0.75 * q], axis=1).reshape(-1, 2)
            p = np.vstack([p[:1], mid, p[-1:]])
    return p


def partition(cost, blocked, seeds, cell, max_claim, pocket_cells=0, water=None):
    """Multi-source cost-distance flood: the label of every cell, -1 where unclaimed.

    cost      float grid, cost per world unit of walking a cell
    blocked   bool grid, never entered by the flood (sea, lakes, crest walls)
    seeds     (label, row, col, head_start); several seeds may share a label
    max_claim cells whose best cost exceeds this stay unclaimed
    pocket_cells  an unclaimed patch of land smaller than this (walls included, water never) joins
              the realm around it, so a small ridge or a valley ringed by crests is not a hole
    water     bool grid of cells that never join a pocket; None means no water
    """
    n_rows, n_cols = cost.shape
    n = n_rows * n_cols
    idx = np.arange(n).reshape(n_rows, n_cols)
    rows, cols, vals = [], [], []
    for dy, dx in ((0, 1), (1, 0), (1, 1), (1, -1)):
        c0, c1 = max(0, -dx), n_cols - max(0, dx)
        a = idx[0:n_rows - dy, c0:c1].ravel()
        b = idx[dy:n_rows, c0 + dx:c1 + dx].ravel()
        ok = ~(blocked.ravel()[a] | blocked.ravel()[b])
        w = cell * (2 ** 0.5 if dx and dy else 1.0) * 0.5 * (cost.ravel()[a] + cost.ravel()[b])
        rows.append(a[ok]); cols.append(b[ok]); vals.append(w[ok])
    labels = sorted({s[0] for s in seeds})
    lab_pos = {lab: i for i, lab in enumerate(labels)}
    kmax = max((s[3] for s in seeds), default=0.0)
    sr, sc, sv = [], [], []
    for lab, r, c, head in seeds:
        sr.append(n + lab_pos[lab]); sc.append(idx[r, c]); sv.append(kmax - head + 1e-3)
    rows.append(np.array(sr, dtype=np.int64)); cols.append(np.array(sc, dtype=np.int64))
    vals.append(np.array(sv, dtype=np.float64))
    total = n + len(labels)
    graph = coo_matrix((np.concatenate(vals), (np.concatenate(rows), np.concatenate(cols))),
                       shape=(total, total)).tocsr()
    dist, _, src = dijkstra(graph, directed=False, indices=np.arange(n, total), min_only=True,
                            return_predecessors=True)
    dist = dist[:n].reshape(n_rows, n_cols) - kmax
    lab_arr = np.array(labels)
    raw = src[:n].reshape(n_rows, n_cols) - n
    out = np.where((raw >= 0) & np.isfinite(dist) & (dist <= max_claim) & ~blocked,
                   lab_arr[np.clip(raw, 0, len(labels) - 1)] if labels else -1, -1)
    if pocket_cells:
        pocket = (out < 0) if water is None else (out < 0) & ~water
        plab, pn = ndimage.label(pocket)
        sizes = ndimage.sum(pocket, plab, index=np.arange(1, pn + 1))
        for i in np.nonzero(sizes < pocket_cells)[0] + 1:
            m = plab == i
            ring = ndimage.binary_dilation(m) & ~m & (out >= 0)
            if ring.any():
                v, cnt = np.unique(out[ring], return_counts=True)
                out[m] = v[np.argmax(cnt)]
    return out


def border_chains(kmap):
    """Chains of cell-corner lattice points between two different claimed labels.

    Returns [(points (k, 2) as (row, col) lattice coords, (low label, high label))]. A chain follows
    one label pair and stops where the pair changes, so three-realm corners end chains.
    """
    kmap = np.asarray(kmap)
    edges = []
    hz = (kmap[:, :-1] != kmap[:, 1:]) & (kmap[:, :-1] >= 0) & (kmap[:, 1:] >= 0)
    for r, c in zip(*np.nonzero(hz)):
        a, b = int(kmap[r, c]), int(kmap[r, c + 1])
        edges.append(((r, c + 1), (r + 1, c + 1), (min(a, b), max(a, b))))
    vt = (kmap[:-1, :] != kmap[1:, :]) & (kmap[:-1, :] >= 0) & (kmap[1:, :] >= 0)
    for r, c in zip(*np.nonzero(vt)):
        a, b = int(kmap[r, c]), int(kmap[r + 1, c])
        edges.append(((r + 1, c), (r + 1, c + 1), (min(a, b), max(a, b))))
    at = {}
    for k, e in enumerate(edges):
        at.setdefault(e[0], []).append(k)
        at.setdefault(e[1], []).append(k)
    used = [False] * len(edges)
    chains = []
    for k, e in enumerate(edges):
        if used[k]:
            continue
        used[k] = True
        pts = [e[0], e[1]]
        for head in (False, True):
            while True:
                v = pts[0] if head else pts[-1]
                nxt = next((q for q in at[v] if not used[q] and edges[q][2] == e[2]), None)
                if nxt is None:
                    break
                used[nxt] = True
                o = edges[nxt][1] if edges[nxt][0] == v else edges[nxt][0]
                if head:
                    pts.insert(0, o)
                else:
                    pts.append(o)
        chains.append((np.array(pts, dtype=np.float64), e[2]))
    return chains


def smooth_chain(points, eps=0.9, iterations=4):
    """Stair-steps to curves: simplify first, then smooth. Closed loops stay closed."""
    pts = np.asarray(points, dtype=np.float64)
    closed = len(pts) > 3 and np.allclose(pts[0], pts[-1])
    if not closed:
        return chaikin(rdp(pts, eps), iterations)
    far = int(np.argmax(np.hypot(*(pts - pts[0]).T)))
    loop = np.vstack([rdp(pts[:far + 1], eps)[:-1], rdp(pts[far:], eps)])[:-1]
    sm = chaikin(loop, iterations, closed=True)
    return np.vstack([sm, sm[:1]])


def nearest_seed_grid(xs, ys, seed_labels, x0, y0, x1, y1, res):
    """Kingdom Borders' territory model: the label of the nearest seed on a res x res grid.

    Grid index [i, j] is x = x0 + i/(res-1)*(x1-x0), y = y0 + j/(res-1)*(y1-y0), as in the mod.
    """
    gx = x0 + np.arange(res) / (res - 1) * (x1 - x0)
    gy = y0 + np.arange(res) / (res - 1) * (y1 - y0)
    out = np.empty((res, res), dtype=np.int32)
    for i in range(res):
        d = (gx[i] - xs)[None, :] ** 2 + (gy[:, None] - ys[None, :]) ** 2
        out[i, :] = seed_labels[np.argmin(d, axis=1)]
    return out


# ---- inputs ----------------------------------------------------------------------------------------

def modules_dir():
    from _gamedir import game_modules
    return Path(game_modules(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))


def load_ownership(mods, overrides=None):
    """Fiefs (id -> x, y, kind, kingdom) and villages, at starting ownership plus overrides."""
    from lxml import etree
    vanilla = etree.parse(str(mods / "SandBox" / "ModuleData" / "spclans.xml"))
    merged = etree.XSLT(etree.parse(str(REPO / "Main" / "_Module" / "ModuleData" / "spclans.xslt")))(vanilla)
    clan_kingdom = {}
    for doc in (merged, etree.parse(str(mods / "TAOM" / "ModuleData" / "characters" / "clans.xml"))):
        for f in doc.iter("Faction"):
            sf = f.get("super_faction")
            if f.get("id"):
                clan_kingdom[f.get("id")] = sf.split(".", 1)[1] if sf else None
    root = etree.parse(str(mods / "TAOM_Map" / "ModuleData" / "settlements.xml")).getroot()
    fiefs, villages, names = {}, [], {}
    for s in root.iter("Settlement"):
        comp = s.find("Components")
        if comp is None or len(comp) == 0:
            continue
        c = comp[0]
        sid = s.get("id")
        names[sid] = re.sub(r"\{=[^}]*\}", "", s.get("name", "")).strip()
        x, y = float(s.get("posX")), float(s.get("posY"))
        if c.tag == "Town":
            kind = "castle" if c.get("is_castle", "false").lower() == "true" else "town"
            owner = clan_kingdom.get((s.get("owner") or "").split(".", 1)[-1])
            fiefs[sid] = dict(x=x, y=y, kind=kind, k=(overrides or {}).get(sid, owner))
        elif c.tag == "Village":
            villages.append(dict(x=x, y=y, bound=(c.get("bound") or "").split(".", 1)[-1]))
    return fiefs, villages, names


def load_heightmap(mods):
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    path = mods / "TAOM_Map" / "AssetSources" / "Support" / "terrain_heightmap.png"
    return np.asarray(Image.open(path), dtype=np.float32)[:8192, :8192]


def terrain_grid(raw, n):
    """Heightmap stand-in for the navmesh terrain types: (cost, blocked, water)."""
    b = raw.reshape(n, raw.shape[0] // n, n, raw.shape[1] // n)
    hmean, hmin = b.mean(axis=(1, 3)), b.min(axis=(1, 3))
    water = ndimage.binary_opening(hmin <= 0.0, iterations=1)
    h = ndimage.gaussian_filter(HMIN + hmean / 65535.0 * (HMAX - HMIN), sigma=2.0)
    gy, gx = np.gradient(h, WORLD / n)
    slope = np.hypot(gx, gy)
    cost = np.ones((n, n))
    cost[slope > 0.12] = 2.0
    cost[slope > 0.20] = 6.0
    crest = slope > 0.30
    lab, nlab = ndimage.label(crest)
    sizes = ndimage.sum(crest, lab, index=np.arange(1, nlab + 1))
    blocked = water | np.isin(lab, np.nonzero(sizes >= 60)[0] + 1)
    return cost, blocked, water


def province_kingdom_map(fiefs, villages, cost, blocked, water, n, max_claim):
    """(province map, kingdom map, kingdom ids) with provinces labelled by fief index."""
    fief_ids = list(fiefs)
    index = {fid: i for i, fid in enumerate(fief_ids)}
    cell = WORLD / n

    def open_cell(x, y):
        r = min(n - 1, max(0, int((WORLD - y) / cell)))
        c = min(n - 1, max(0, int(x / cell)))
        if blocked[r, c]:
            for rad in range(1, 6):
                for rr in range(r - rad, r + rad + 1):
                    for cc in range(c - rad, c + rad + 1):
                        if 0 <= rr < n and 0 <= cc < n and not blocked[rr, cc]:
                            return rr, cc
        return r, c

    seeds = [(index[fid], *open_cell(f["x"], f["y"]), HEAD_START[f["kind"]]) for fid, f in fiefs.items()]
    seeds += [(index[v["bound"]], *open_cell(v["x"], v["y"]), HEAD_START["village"])
              for v in villages if v["bound"] in index]
    prov = partition(cost, blocked, seeds, cell, max_claim, pocket_cells=150, water=water)
    kingdoms = sorted({f["k"] for f in fiefs.values() if f["k"]})
    k_of = np.array([kingdoms.index(fiefs[fid]["k"]) if fiefs[fid]["k"] in kingdoms else -1 for fid in fief_ids])
    kmap = np.where(prov >= 0, k_of[np.clip(prov, 0, None)], -1)
    return prov, kmap, kingdoms


# ---- rendering ------------------------------------------------------------------------------------

def hillshade(raw, x0, y0, x1, y1, width, height, style="campaign"):
    """Backdrop pixels for a world crop: parchment relief or campaign-map colours."""
    from PIL import Image
    pxu = raw.shape[0] / WORLD
    sub = raw[int((WORLD - y1) * pxu):int((WORLD - y0) * pxu), int(x0 * pxu):int(x1 * pxu)]
    h = HMIN + np.asarray(Image.fromarray(sub).resize((width, height), Image.BILINEAR), np.float32) / 65535.0 * (HMAX - HMIN)
    water = np.asarray(Image.fromarray(((sub <= 0) * 255).astype(np.uint8)).resize((width, height), Image.BILINEAR)) > 110
    ppu = width / (x1 - x0)
    gy, gx = np.gradient(ndimage.gaussian_filter(h, 1.0), 1.0 / ppu)
    slope = np.hypot(gx, gy)
    if style == "parchment":
        shade = np.clip(0.72 + 1.6 * (-gx * 0.7 + gy * 0.7), 0.25, 1.15)
        col = np.stack([0.80 * shade, 0.76 * shade, 0.66 * shade], axis=-1) * 255
        col[water] = (140, 158, 173)
        return np.clip(col, 0, 255).astype(np.float32), water
    light = np.clip(0.92 + 0.85 * (-gx * 0.55 + gy * 0.75), 0.55, 1.18)
    blot = ndimage.gaussian_filter(np.random.default_rng(11).normal(0, 1, (height, width)), 18)
    blot = (blot - blot.min()) / max(1e-9, blot.max() - blot.min())
    grass = np.array([96, 128, 58.0]) * (0.9 + 0.2 * blot[..., None])
    th = np.clip((h - 50.0) / 18.0, 0, 1)[..., None]
    col = grass * (1 - th) + np.array([130, 122, 84.0]) * th
    col = col * (1 - 0.35 * blot[..., None]) + np.array([136, 150, 84.0]) * 0.35 * blot[..., None]
    tr = np.clip((slope - 0.16) / 0.30, 0, 1)[..., None]
    col = col * (1 - tr) + np.array([128, 120, 110.0]) * tr
    ts = (np.clip((h - 80.0) / 8.0, 0, 1) * np.clip((slope - 0.08) / 0.25, 0, 1))[..., None]
    col = col * (1 - ts) + np.array([238, 240, 242.0]) * ts
    coast = ndimage.binary_dilation(water, iterations=3) & ~water
    col[coast] = col[coast] * 0.4 + np.array([196, 180, 134.0]) * 0.6
    col = col * light[..., None]
    depth = np.clip(ndimage.distance_transform_edt(water) / 40.0, 0, 1)[..., None]
    sea = np.array([70, 118, 138.0]) * (1 - depth) + np.array([36, 70, 96.0]) * depth
    return np.clip(np.where(water[..., None], sea, col), 0, 255).astype(np.float32), water


def colour(k, kingdoms):
    return PREVIEW_COLOURS.get(kingdoms[k], (kingdoms[k], (170, 170, 170)))[1] if k >= 0 else (0, 0, 0)


def font(size, bold=False):
    from PIL import ImageFont
    for name in (("GARABD.TTF" if bold else "GARA.TTF"), "arial.ttf"):
        try:
            return ImageFont.truetype(name if name == "arial.ttf" else f"C:/Windows/Fonts/{name}", size)
        except OSError:
            continue
    return ImageFont.load_default()


def tint_and_edges(base, terr, water, kingdoms, alpha=0.38, frontier=True):
    img = base.copy()
    has = terr >= 0
    tint = np.zeros_like(img)
    for i in np.unique(terr[has]):
        tint[terr == i] = colour(int(i), kingdoms)
    img[has] = img[has] * (1 - alpha) + tint[has] * alpha
    realm = np.zeros(terr.shape, bool)
    wild = np.zeros(terr.shape, bool)
    for sa, sb in (((slice(None), slice(1, None)), (slice(None), slice(None, -1))),
                   ((slice(1, None), slice(None)), (slice(None, -1), slice(None)))):
        ta, tb = terr[sa], terr[sb]
        d = (ta != tb) & (ta >= 0) & (tb >= 0)
        realm[sa] |= d
        realm[sb] |= d
        wild[sa] |= (ta != tb) & ((ta < 0) ^ (tb < 0))
    realm = ndimage.binary_dilation(realm) & ~water
    if frontier:
        w = wild & ~water
        img[w] = img[w] * 0.55
    img[realm] = (30, 22, 18)
    return img


def cmd_compare(args):
    from PIL import Image, ImageDraw
    mods = modules_dir()
    fiefs, villages, _ = load_ownership(mods)
    raw = load_heightmap(mods)
    cost, blocked, water = terrain_grid(raw, args.grid)
    _, kmap, kingdoms = province_kingdom_map(fiefs, villages, cost, blocked, water, args.grid, args.max_claim)
    pts = [(f["x"], f["y"], f["k"]) for f in fiefs.values()]
    pts += [(v["x"], v["y"], fiefs[v["bound"]]["k"]) for v in villages if v["bound"] in fiefs]
    xs = np.array([p[0] for p in pts]); ys = np.array([p[1] for p in pts])
    lab = np.array([kingdoms.index(p[2]) if p[2] in kingdoms else -1 for p in pts])
    x0, x1, y0, y1 = xs.min() - 30, xs.max() + 30, ys.min() - 30, ys.max() + 30
    mod = nearest_seed_grid(xs, ys, lab, x0, y0, x1, y1, 150)
    r = args.size
    base, wr = hillshade(raw, 0, 0, WORLD, WORLD, r, r, style="parchment")
    px = (np.arange(r) + 0.5) * WORLD / r
    wx, wy = np.meshgrid(px, WORLD - px)
    ci = np.clip(np.rint((wx - x0) / (x1 - x0) * 149).astype(int), 0, 149)
    cj = np.clip(np.rint((wy - y0) / (y1 - y0) * 149).astype(int), 0, 149)
    inside = (wx >= x0) & (wx <= x1) & (wy >= y0) & (wy <= y1)
    terr_mod = np.where(inside, mod[ci, cj], -1)
    rr = (np.arange(r) * args.grid // r)
    terr_new = kmap[rr[:, None], rr[None, :]]
    panels = []
    for terr, title, frontier in ((terr_mod, "Kingdom Borders model: nearest settlement, 150 grid", False),
                                  (terr_new, "TAOM terrain-aware provinces", True)):
        im = Image.fromarray(tint_and_edges(base, terr, wr, kingdoms, frontier=frontier).astype(np.uint8))
        dr = ImageDraw.Draw(im)
        dr.rectangle((0, 0, r, 44), fill=(20, 20, 20))
        dr.text((12, 7), title, fill=(255, 255, 255), font=font(26))
        panels.append(im)
    out = Image.new("RGB", (2 * r + 16, r), (20, 20, 20))
    out.paste(panels[0], (0, 0)); out.paste(panels[1], (r + 16, 0))
    out.save(args.out)
    print(f"wrote {args.out}: {len(fiefs)} fiefs, {len(villages)} villages, {len(kingdoms)} kingdoms")
    return 0


def cmd_capture(args):
    from PIL import Image, ImageDraw
    mods = modules_dir()
    overrides = dict(c.split("=", 1) for c in args.capture)
    fiefs, villages, names = load_ownership(mods)
    raw = load_heightmap(mods)
    cost, blocked, water = terrain_grid(raw, args.grid)
    prov, kmap, kingdoms = province_kingdom_map(fiefs, villages, cost, blocked, water, args.grid, args.max_claim)
    fief_ids = list(fiefs)
    for k in overrides.values():
        if k not in kingdoms:
            kingdoms.append(k)
    after_k = np.array([kingdoms.index(overrides.get(fid, fiefs[fid]["k"]))
                        if overrides.get(fid, fiefs[fid]["k"]) in kingdoms else -1 for fid in fief_ids])
    k_after = np.where(prov >= 0, after_k[np.clip(prov, 0, None)], -1)
    x0, y0, x1, y1 = args.crop
    w, h = int((x1 - x0) * args.ppu), int((y1 - y0) * args.ppu)
    base, wr = hillshade(raw, x0, y0, x1, y1, w, h, style="parchment")
    cell = WORLD / args.grid
    yy, xx = np.mgrid[0:h, 0:w]
    gr = np.clip(((WORLD - (y1 - yy / args.ppu)) / cell).astype(int), 0, args.grid - 1)
    gc = np.clip(((x0 + xx / args.ppu) / cell).astype(int), 0, args.grid - 1)
    panels = []
    for km, title in ((kmap, "Start of campaign"), (k_after, f"After {len(overrides)} fief(s) change hands")):
        im = Image.fromarray(tint_and_edges(base, km[gr, gc], wr, kingdoms, frontier=False).astype(np.uint8))
        dr = ImageDraw.Draw(im)
        for fid, f in fiefs.items():
            cx, cy = (f["x"] - x0) * args.ppu, (y1 - f["y"]) * args.ppu
            if 0 <= cx < w and 0 <= cy < h:
                rad = 5 if f["kind"] == "town" else 3
                dr.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=(250, 250, 240), outline=(0, 0, 0))
                if km is k_after and fid in overrides:
                    dr.ellipse((cx - 14, cy - 14, cx + 14, cy + 14), outline=(210, 20, 20), width=4)
        dr.rectangle((0, 0, w, 44), fill=(20, 20, 20))
        dr.text((12, 7), title, fill=(255, 255, 255), font=font(26))
        panels.append(im)
    out = Image.new("RGB", (2 * w + 12, h), (20, 20, 20))
    out.paste(panels[0], (0, 0)); out.paste(panels[1], (w + 12, 0))
    out.save(args.out)
    moved = int((kmap != k_after).sum())
    print(f"wrote {args.out}: {moved} cells ({moved * cell * cell:.0f} sq units) changed realm")
    return 0


def _strip(name, mode):
    from PIL import Image
    p = ART / name
    return np.asarray(Image.open(p).convert(mode), np.float32) / 255.0 if p.exists() else None


def cmd_looks(args):
    from PIL import Image, ImageDraw
    from scipy.spatial import cKDTree
    mods = modules_dir()
    fiefs, villages, _ = load_ownership(mods)
    raw = load_heightmap(mods)
    cost, blocked, water = terrain_grid(raw, args.grid)
    _, kmap, kingdoms = province_kingdom_map(fiefs, villages, cost, blocked, water, args.grid, args.max_claim)
    cell = WORLD / args.grid
    x0, y0, x1, y1 = args.crop
    ppu = args.ppu
    pw, ph = int((x1 - x0) * ppu), int((y1 - y0) * ppu)
    # smoothed chains, resampled every 0.1 world units, with tangents and arc length
    samples, pairs = [], []
    for pts, pair in border_chains(kmap):
        sm = smooth_chain(pts)
        wx, wy = sm[:, 1] * cell, WORLD - sm[:, 0] * cell
        s = np.concatenate([[0.0], np.cumsum(np.hypot(np.diff(wx), np.diff(wy)))])
        if s[-1] < 0.5:
            continue
        t = np.arange(0.0, s[-1], 0.1)
        rx, ry = np.interp(t, s, wx), np.interp(t, s, wy)
        tx, ty = np.gradient(rx), np.gradient(ry)
        ln = np.hypot(tx, ty) + 1e-9
        samples.append(np.stack([rx, ry, t, tx / ln, ty / ln, np.full_like(t, len(pairs))], 1))
        pairs.append(pair)
    S = np.vstack(samples)
    pairs = np.array(pairs)
    pad = 30.0
    S = S[(S[:, 0] > x0 - pad) & (S[:, 0] < x1 + pad) & (S[:, 1] > y0 - pad) & (S[:, 1] < y1 + pad)]
    spx, spy = (S[:, 0] - x0) * ppu, (y1 - S[:, 1]) * ppu
    tpx, tpy = S[:, 3], -S[:, 4]
    yy, xx = np.mgrid[0:ph, 0:pw]
    P = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], 1)
    dd, ii = cKDTree(np.stack([spx, spy], 1)).query(P, k=1, distance_upper_bound=64.0)
    near = np.isfinite(dd)
    ii = np.where(near, ii, 0)
    dx_, dy_ = P[:, 0] - spx[ii], P[:, 1] - spy[ii]
    nl = dx_ * (-tpy[ii]) + dy_ * tpx[ii]
    U = np.where(near, np.abs(nl), 1e9).reshape(ph, pw)
    V = (S[ii, 2] * ppu + dx_ * tpx[ii] + dy_ * tpy[ii]).reshape(ph, pw)
    SIDE = np.sign(nl).reshape(ph, pw)
    chain = S[ii, 5].astype(int).reshape(ph, pw)
    pa, pb = pairs[chain, 0], pairs[chain, 1]
    gr = np.clip(((WORLD - (y1 - (yy + 0.5) / ppu)) / cell).astype(int), 0, args.grid - 1)
    gc = np.clip(((x0 + (xx + 0.5) / ppu) / cell).astype(int), 0, args.grid - 1)
    kpix = kmap[gr, gc]
    present = [int(k) for k in np.unique(kpix) if k >= 0]
    own = np.full((ph, pw), -1)
    if present:
        votes = np.stack([ndimage.gaussian_filter((kpix == k).astype(np.float32), sigma=cell * ppu * 0.8) for k in present])
        own = np.where(votes.max(axis=0) > 0.4, np.array(present)[np.argmax(votes, axis=0)], -1)
    across = np.where(own == pa, pb, np.where(own == pb, pa, -1))
    border = near.reshape(ph, pw) & (own >= 0) & (across >= 0)
    war = np.zeros((ph, pw), bool)
    for i, k in enumerate(kingdoms):
        for j, k2 in enumerate(kingdoms):
            if frozenset((k, k2)) in PREVIEW_WARS:
                war |= (own == i) & (across == j)
    base, _ = hillshade(raw, x0, y0, x1, y1, pw, ph)
    OWN = np.zeros((ph, pw, 3), np.float32)
    for k in present:
        OWN[own == k] = colour(k, kingdoms)
    PIG = np.clip(OWN * 0.86 - 0.3 * np.maximum(OWN.mean(-1, keepdims=True) - 160, 0), 0, 255)
    ink_t, gold_t, wash_t = _strip("tile_ink.png", "L"), _strip("tile_gold.png", "RGBA"), _strip("tile_wash.png", "L")
    dark = np.array([34, 26, 20], np.float32)

    def blend(img, c, a):
        a = np.clip(a, 0, 1)[..., None]
        return img * (1 - a) + np.asarray(c, np.float32) * a

    def centred(tex, half):
        th, tw = tex.shape[:2]
        u = np.clip(0.5 + SIDE * U / (2 * half), 0, 1)
        period = tw * 2 * half / th
        return tex[np.clip((u * (th - 1)).astype(int), 0, th - 1), ((np.mod(V, period) / period) * (tw - 1)).astype(int)]

    def heraldic(img):
        gap, band = 1.4, 6.5
        inb = border & (U >= gap) & (U <= gap + band)
        shade = 0.78 + 0.22 * np.clip(1 - (U - gap) / band, 0, 1)
        img = blend(img, OWN * shade[..., None], np.where(inb, 0.9, 0))
        return blend(img, dark, np.where(border & ((np.abs(U - gap) < 0.6) | (np.abs(U - gap - band) < 0.6)), 0.55, 0))

    def watercolour(img, width=24.0, strength=1.15, hairline=True):
        if wash_t is not None:
            th, tw = wash_t.shape
            pig = 1 - wash_t[np.clip((np.clip(U / width, 0, 1) * (th - 1)).astype(int), 0, th - 1),
                             ((np.mod(V, 520.0) / 520.0) * (tw - 1)).astype(int)]
        else:
            pig = np.clip(1 - U / width, 0, 1) ** 1.5
        a = np.where(border & (U < width), np.clip(pig * 1.1 * strength, 0, 0.95), 0)
        img = blend(img, 0.7 * PIG + 0.3 * (img * PIG / 255.0), a)
        return blend(img, dark, np.where(border & (U < 0.7), 0.7, 0)) if hairline else img

    def ink(img, tint=True):
        if tint:
            img = blend(img, OWN, np.where(border & (U < 10), 0.35 * np.clip(1 - U / 10, 0, 1), 0))
        if ink_t is None:
            return img
        return blend(img, (46, 28, 16), np.where(border & (U < 5.0), np.clip((1 - centred(ink_t, 5.0)) * 1.5, 0, 1), 0))

    def gilded(img):
        gap, band = 3.6, 6.0
        inb = border & (U >= gap) & (U <= gap + band)
        img = blend(img, OWN * (0.8 + 0.2 * np.clip(1 - (U - gap) / band, 0, 1))[..., None], np.where(inb, 0.82, 0))
        img = blend(img, dark, np.where(border & (np.abs(U - gap - band) < 0.6), 0.5, 0))
        if gold_t is None:
            return img
        g = centred(gold_t, 3.8)
        return blend(img, g[..., :3] * 255.0, np.where(border & (U < 3.8), g[..., 3], 0))

    def war_front(img):
        img = img * 0.8 + np.array([255, 110, 30.0]) * np.where(border & war, np.exp(-U / 6.0), 0)[..., None] * 0.85
        img = blend(img, (255, 236, 190), np.where(border & war & (U < 1.3), 0.95, 0))
        return blend(img, (226, 230, 236), np.where(border & ~war & (U < 0.9), 0.8, 0))

    looks = [("1  Heraldic bands", heraldic(base.copy()), False),
             ("2  Outline colouring (watercolour)", watercolour(base.copy()), False),
             ("3  Tolkien ink (dash-dot)", ink(base.copy()), False),
             ("4  Gilded frontier (gold cord)", gilded(base.copy()), False),
             ("5  War front", war_front(base.copy()), False),
             ("6  Atlas: watercolour + ink + names", ink(watercolour(base.copy(), 24.0, 1.05, False), tint=False), True)]
    title_font, letter_font = font(30, True), font(30, True)
    panels = []
    for title, arr, names in looks:
        im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
        dr = ImageDraw.Draw(im)
        for f in fiefs.values():
            cx, cy = (f["x"] - x0) * ppu, (y1 - f["y"]) * ppu
            if 0 <= cx < pw and 0 <= cy < ph:
                rad = 4 if f["kind"] == "town" else 3
                dr.ellipse((cx - rad, cy - rad, cx + rad, cy + rad), fill=(248, 244, 232), outline=(24, 18, 12))
        if names:
            for k in present:
                m = own == k
                if m.sum() < 9000:
                    continue
                dt = ndimage.distance_transform_edt(np.pad(m, 1))[1:-1, 1:-1]
                cy, cx = np.unravel_index(np.argmax(dt), dt.shape)
                label = " ".join("  ".join(PREVIEW_COLOURS.get(kingdoms[k], (kingdoms[k],))[0].upper().split(" ")))
                tw_ = dr.textlength(label, font=letter_font)
                dr.text((min(max(cx - tw_ / 2, 8), pw - tw_ - 8), cy - 16), label, fill=(52, 34, 20),
                        font=letter_font, stroke_width=3, stroke_fill=(242, 232, 206))
        dr.rectangle((0, 0, pw, 44), fill=(18, 16, 14))
        dr.text((12, 5), title, fill=(242, 234, 216), font=title_font)
        panels.append(im)
    gap = 10
    board = Image.new("RGB", (2 * pw + 3 * gap, 3 * ph + 4 * gap), (12, 11, 10))
    for i, im in enumerate(panels):
        board.paste(im, (gap + (i % 2) * (pw + gap), gap + (i // 2) * (ph + gap)))
    board.save(args.out)
    missing = [n for n, t in (("ink", ink_t), ("gold", gold_t), ("wash", wash_t)) if t is None]
    print(f"wrote {args.out}: {len(present)} realms in view" + (f"; missing tiles (run `tiles`): {missing}" if missing else ""))
    return 0


def cmd_tiles(_args):
    """ImagineArt originals in tools/realm_border_art/ -> seamless tiles beside them."""
    from PIL import Image

    def lum(path):
        a = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0
        return a, 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]

    # ink: a local paper estimate removes stains; cut between two gaps so the rhythm repeats
    _, L = lum(ART / "imagineart_ink.png")
    bg = ndimage.gaussian_filter(ndimage.maximum_filter(ndimage.gaussian_filter(L, 3), size=41), 15)
    ink = np.clip((bg - L) / np.maximum(bg, 1e-3) * 2.2, 0, 1)
    ink[ink < 0.12] = 0
    row = int(np.argmax(ink.sum(axis=1)))
    band = ink[row - 14:row + 14]
    width = band.shape[1]
    clean = band.max(axis=0) < 0.05
    start = next(x for x in range(90, 400) if clean[x:x + 4].all())
    end = next(x for x in range(width - 90, width - 500, -1) if clean[x - 4:x].all())
    Image.fromarray((255 * (1 - band[:, start:end])).astype(np.uint8)).save(ART / "tile_ink.png")

    # gold: key alpha off the black ground; pick the tile length whose seam matches the twist
    rgb, L = lum(ART / "imagineart_gold.png")
    alpha = np.clip((L - 0.07) / 0.22, 0, 1)
    rows = np.nonzero(alpha.mean(axis=1) > 0.25)[0]
    r0, r1 = rows.min() - 3, rows.max() + 4
    x0 = 100
    first = L[r0:r1, x0:x0 + 40]
    length = min(range(1100, 1440), key=lambda n_: float(np.mean((L[r0:r1, x0 + n_:x0 + n_ + 40] - first) ** 2)))
    g_rgb = rgb[r0:r1, x0:x0 + length + 24].copy()
    g_a = alpha[r0:r1, x0:x0 + length + 24].copy()
    ramp = np.linspace(0, 1, 24)[None, :]
    g_rgb[:, :24] = g_rgb[:, :24] * ramp[..., None] + g_rgb[:, -24:] * (1 - ramp[..., None])
    g_a[:, :24] = g_a[:, :24] * ramp + g_a[:, -24:] * (1 - ramp)
    g_rgb, g_a = g_rgb[:, :-24], g_a[:, :-24]
    g_col = np.clip(g_rgb / np.maximum(g_a[..., None], 0.05), 0, 1)
    Image.fromarray((255 * np.dstack([g_col, g_a])).astype(np.uint8), "RGBA").save(ART / "tile_gold.png")

    # wash: pigment against the paper, from the pooled edge down, ends cross-faded
    _, L = lum(ART / "imagineart_wash.png")
    paper = np.percentile(L[:, 300:1300], 97)
    pig = np.clip((paper - L) / paper * 1.35, 0, 1)
    mid = pig[:, 400:1200].mean(axis=1)
    top = max(0, int(np.argmax(mid > 0.35 * mid.max())) - 6)
    wash = pig[top:top + 300, 120:1440].copy()
    ramp = np.linspace(0, 1, 160)[None, :]
    wash[:, :160] = wash[:, :160] * ramp + wash[:, -160:] * (1 - ramp)
    wash = wash[:, :-160]
    Image.fromarray((255 * (1 - wash)).astype(np.uint8)).save(ART / "tile_wash.png")
    print(f"wrote tiles: ink {end - start}x{band.shape[0]}, gold {length}x{r1 - r0}, wash {wash.shape[1]}x{wash.shape[0]}")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--grid", type=int, default=512, help="province grid cells per side (default 512)")
    common.add_argument("--max-claim", type=float, default=190.0, help="cost beyond which land stays wild")
    p = sub.add_parser("compare", parents=[common], help="mod model vs terrain-aware provinces, whole map")
    p.add_argument("out")
    p.add_argument("--size", type=int, default=1024)
    p = sub.add_parser("capture", parents=[common], help="before and after fiefs change hands")
    p.add_argument("out")
    p.add_argument("--capture", nargs="+", required=True, metavar="FIEF=KINGDOM")
    p.add_argument("--crop", nargs=4, type=float, default=(790.0, 600.0, 1010.0, 790.0), metavar=("X0", "Y0", "X1", "Y1"))
    p.add_argument("--ppu", type=float, default=5.12, help="output pixels per world unit")
    p = sub.add_parser("looks", parents=[common], help="the six candidate looks on a crop")
    p.add_argument("out")
    p.add_argument("--crop", nargs=4, type=float, default=(560.0, 700.0, 1000.0, 1000.0), metavar=("X0", "Y0", "X1", "Y1"))
    p.add_argument("--ppu", type=float, default=2.5)
    sub.add_parser("tiles", help="rebuild the seamless tiles from the ImagineArt originals")
    args = ap.parse_args(argv)
    if _IMPORT_ERROR is not None:
        print(f"realm_borders_preview needs numpy and scipy: {_IMPORT_ERROR}", file=sys.stderr)
        return 2
    try:
        import lxml  # noqa: F401
        import PIL  # noqa: F401
    except ImportError as exc:
        print(f"realm_borders_preview needs lxml and Pillow: {exc}", file=sys.stderr)
        return 2
    if args.cmd != "tiles":
        mods = modules_dir()
        needed = [mods / "TAOM_Map" / "ModuleData" / "settlements.xml",
                  mods / "TAOM_Map" / "AssetSources" / "Support" / "terrain_heightmap.png",
                  mods / "SandBox" / "ModuleData" / "spclans.xml",
                  mods / "TAOM" / "ModuleData" / "characters" / "clans.xml"]
        gone = [str(p) for p in needed if not p.exists()]
        if gone:
            print("missing inputs (set BANNERLORD_GAME_DIR if the install is elsewhere):\n  " + "\n  ".join(gone), file=sys.stderr)
            return 2
    return {"compare": cmd_compare, "capture": cmd_capture, "looks": cmd_looks, "tiles": cmd_tiles}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
