"""
Classify the loose hair cards of the dwarf grooms, and validate and resolve kitbash recipes (2026-10-08).

Pure math, stdlib only (no bpy, no numpy), so it is tested without Blender (tools/tests/test_groom_kitbash.py)
and imported inside Blender by kitbash_grooms.py.

WHY
The dwarf beards (SK_Dwarf_Beard_A_01..12) and hairs (Dwarf_Hair_A..J) are not single meshes: each is hundreds of
loose "hair cards" (16,545 parts over the 22 grooms), some of them rings that a braid of small cards is threaded
through. New grooms are made by KITBASHING: picking parts from several existing grooms and combining them. To pick
parts by what they are (a moustache card, a long back card, a braid) rather than by index, every part first gets
a region, a length and a side from where it is rooted on the head.

WHAT IT COMPUTES
- Parts: the connected components of the mesh, by shared vertices (`islands`). The part id is its index in the
  list, ordered by the part's lowest vertex index, so it is stable for the same mesh.
- Per part (`analyze_part`): the root (the vertex nearest the head surface), its signed distance to that
  surface (positive outside), the lowest z, the tip (the vertex farthest from the root) and the depth (how far the
  part pokes into the closed part of the head). A card is `rooted` when its root distance is under ROOTED_MAX_DIST,
  else `fill`.
- Beard regions: moustache, chin, cheek, sideburn. Hair regions: crown, side, back. Both get a length band; hair
  also gets `hang` from the tip's azimuth. Every threshold below is a named constant, to be tuned.
- Braids (`ring_membership`): a card belongs to ring n when any of its vertices lies within the ring's half
  extent of the ring's centre.
- Recipes (`validate_recipe`, `resolve`): which parts of which grooms make up each new groom, with the
  transforms (`mirror_x`, `swap_side_group`, `translate`, `max_move`) a later step applies.

COORDINATES
Blender world metres, Z up, the dwarf faces +Y, his left is -x. Azimuth is measured around a vertical axis
through (AXIS_X, AXIS_Y): 0 degrees is the face front (+Y), positive toward +x (his right), 180 is the back. Region
rules use the absolute value, so left and right classify alike; `side` keeps the distinction.
"""
import json
import math
import re

# --- head landmarks, metres ----------------------------------------------------------------------------
AXIS_X = 0.0
AXIS_Y = 0.065
HEAD_CLOSED_Z = 1.21            # the head mesh is closed only at or above this z (open neck below)
SIDE_DEAD_ZONE = 0.005          # |x| at or under this is "centre"
ROOTED_MAX_DIST = 0.01          # root distance under this: rooted; otherwise fill

# --- beard thresholds ------------------------------------------------------------------------------------
BEARD_FRONT_AZ = 40.0           # |az| under this: moustache or chin
BEARD_CHEEK_AZ = 80.0           # |az| under this (and at least BEARD_FRONT_AZ): cheek; beyond: sideburn
LIP_Z = 1.302                   # lips front height: at or above is moustache, below is chin
MOUSTACHE_MIN_Y = 0.14          # a moustache root must lie forward of this y
BEARD_CHEST_Z = 1.12            # lowest z under this: chest length
BEARD_MID_Z = 1.22              # lowest z under this: mid length, else short

# --- hair thresholds -------------------------------------------------------------------------------------
HAIR_CROWN_Z = 1.45             # root z at or above: crown
HAIR_BACK_AZ = 120.0            # root |az| at or beyond: back
HANG_FRONT_AZ = 60.0            # tip |az| under this: hangs front
HANG_SIDE_AZ = 120.0            # tip |az| under this: hangs at the side, else back
HAIR_LONG_Z = 1.21              # lowest z under this: long
HAIR_MID_Z = 1.32               # lowest z under this: mid, else short

UV_EPS = 1e-6                   # a UV extent at or under this on both axes is collapsed

FAMILIES = ("beard", "hair")
REGIONS = {"beard": ("moustache", "chin", "cheek", "sideburn", "fill"),
           "hair": ("crown", "back", "side", "fill")}
LENGTHS = {"beard": ("chest", "mid", "short"), "hair": ("long", "mid", "short")}
HANGS = ("front", "side", "back")
SIDES = ("left", "right", "centre")
TRANSFORM_OPS = ("mirror_copy", "translate")
FIRST_FINAL = {"beard": "SK_Dwarf_Beard_A_13", "hair": "Dwarf_Hair_K"}
_BEARD_FINAL = re.compile(r"^SK_Dwarf_Beard_A_(\d+)$")
_HAIR_FINAL = re.compile(r"^Dwarf_Hair_([A-Z])$")
RECIPE_VERSION = 1


# --- islands ---------------------------------------------------------------------------------------------
def islands(nverts, polys):
    """Connected components over polygon vertex lists. Returns the parts, each a sorted vertex list, ordered by
    their lowest vertex index. Vertices in no polygon belong to no part."""
    parent = list(range(nverts))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    used = [False] * nverts
    for poly in polys:
        if not poly:
            continue
        ra = find(poly[0])
        for v in poly:
            used[v] = True
            rb = find(v)
            if rb != ra:
                parent[rb] = ra
    groups = {}
    for v in range(nverts):
        if used[v]:
            groups.setdefault(find(v), []).append(v)
    return list(groups.values())  # insertion order is lowest-vertex order


def vertex_labels(parts, nverts):
    """Per vertex, the id of the part holding it, or -1 for a vertex in no part."""
    labels = [-1] * nverts
    for pid, verts in enumerate(parts):
        for v in verts:
            labels[v] = pid
    return labels


# --- position helpers --------------------------------------------------------------------------------------
def azimuth(p):
    """Degrees around the vertical axis through (AXIS_X, AXIS_Y): 0 = face front (+Y), positive toward +x."""
    return math.degrees(math.atan2(p[0] - AXIS_X, p[1] - AXIS_Y))


def side(p):
    """left (x under -SIDE_DEAD_ZONE), right (over +SIDE_DEAD_ZONE) or centre."""
    if p[0] < -SIDE_DEAD_ZONE:
        return "left"
    if p[0] > SIDE_DEAD_ZONE:
        return "right"
    return "centre"


def penetration(points, dists):
    """The deepest a part sits inside the head, over the vertices at or above HEAD_CLOSED_Z (0 if none)."""
    deepest = 0.0
    for p, d in zip(points, dists):
        if p[2] >= HEAD_CLOSED_Z and -d > deepest:
            deepest = -d
    return deepest


# --- classification ---------------------------------------------------------------------------------------
def beard_region(abs_az, y, z):
    """Region of a rooted beard card from its root's |azimuth|, y and z. A root in front of the face but high and
    not forward (above the lips yet behind MOUSTACHE_MIN_Y) is not a moustache and falls to cheek."""
    if abs_az < BEARD_FRONT_AZ:
        if z < LIP_Z:
            return "chin"
        return "moustache" if y > MOUSTACHE_MIN_Y else "cheek"
    if abs_az < BEARD_CHEEK_AZ:
        return "cheek"
    return "sideburn"


def beard_length(lowest_z):
    if lowest_z < BEARD_CHEST_Z:
        return "chest"
    return "mid" if lowest_z < BEARD_MID_Z else "short"


def hair_region(abs_az, z):
    """crown (root high), else back (root round the back), else side."""
    if z >= HAIR_CROWN_Z:
        return "crown"
    return "back" if abs_az >= HAIR_BACK_AZ else "side"


def hang_of(abs_tip_az):
    if abs_tip_az < HANG_FRONT_AZ:
        return "front"
    return "side" if abs_tip_az < HANG_SIDE_AZ else "back"


def hair_length(lowest_z):
    if lowest_z < HAIR_LONG_Z:
        return "long"
    return "mid" if lowest_z < HAIR_MID_Z else "short"


def classify_beard(root, root_dist, lowest_z):
    """-> {region, length, rooted, side}; an unrooted card is region `fill`."""
    rooted = root_dist < ROOTED_MAX_DIST
    region = beard_region(abs(azimuth(root)), root[1], root[2]) if rooted else "fill"
    return {"region": region, "length": beard_length(lowest_z), "rooted": rooted, "side": side(root)}


def classify_hair(root, root_dist, lowest_z, tip):
    """-> {region, length, hang, rooted, side}; an unrooted card is region `fill` but keeps length and hang."""
    rooted = root_dist < ROOTED_MAX_DIST
    region = hair_region(abs(azimuth(root)), root[2]) if rooted else "fill"
    return {"region": region, "length": hair_length(lowest_z), "hang": hang_of(abs(azimuth(tip))),
            "rooted": rooted, "side": side(root)}


def analyze_part(points, dists, family):
    """Everything about one part from its world vertices and their signed distances to the head surface:
    root, root_dist, lowest_z, depth, plus the classification (and `tip` for hair)."""
    if family not in FAMILIES:
        raise ValueError("unknown family %r" % family)
    if not points:
        raise ValueError("a part needs at least one vertex")
    ri = min(range(len(points)), key=lambda i: abs(dists[i]))
    root, root_dist = points[ri], dists[ri]
    lowest = min(p[2] for p in points)
    out = {"root": root, "root_dist": root_dist, "lowest_z": lowest, "depth": penetration(points, dists)}
    if family == "hair":
        tip = max(points, key=lambda p: math.dist(p, root))
        out["tip"] = tip
        out.update(classify_hair(root, root_dist, lowest, tip))
    else:
        out.update(classify_beard(root, root_dist, lowest))
    return out


# --- braids -----------------------------------------------------------------------------------------------
def ring_geometry(points):
    """(centre, half_extent) of a ring part: its bbox centre and half its largest bbox dimension."""
    lo = tuple(min(p[k] for p in points) for k in range(3))
    hi = tuple(max(p[k] for p in points) for k in range(3))
    centre = tuple((lo[k] + hi[k]) / 2.0 for k in range(3))
    return centre, max(hi[k] - lo[k] for k in range(3)) / 2.0


def ring_membership(card_vertices_by_part, rings):
    """Which ring each card is threaded on. `rings` is a list of (centre, half_extent). A card belongs to ring n
    when any vertex is within n's half extent of its centre (the edge counts). A card in two or more rings goes to
    the one whose centre it comes nearest and is reported in the conflicts as (part_id, [ring indices]).
    Returns ({part_id: ring index or None}, conflicts)."""
    membership, conflicts = {}, []
    for pid, pts in card_vertices_by_part.items():
        hits = []
        for n, (centre, half) in enumerate(rings):
            nearest = min((math.dist(p, centre) for p in pts), default=math.inf)
            if nearest <= half:
                hits.append((nearest, n))
        if not hits:
            membership[pid] = None
            continue
        membership[pid] = min(hits)[1]
        if len(hits) > 1:
            conflicts.append((pid, sorted(n for _, n in hits)))
    return membership, conflicts


# --- UVs --------------------------------------------------------------------------------------------------
def uv_span(uvs):
    """(umin, umax, vmin, vmax) of (u, v) pairs, or None for none."""
    if not uvs:
        return None
    us = [uv[0] for uv in uvs]
    vs = [uv[1] for uv in uvs]
    return (min(us), max(us), min(vs), max(vs))


def valid_uv_layer(uv_spans_by_layer):
    """The first layer (in the dict's order) whose span has a real extent, else None. Dwarf_Hair_I_lod0 has
    single-point UVs on 118 of its 223 parts in its first layer `UVMap`; the real ones are in `map1`."""
    for name, span in uv_spans_by_layer.items():
        if span is not None and (span[1] - span[0] > UV_EPS or span[3] - span[2] > UV_EPS):
            return name
    return None


# --- inventory summary ---------------------------------------------------------------------------------------
def summarize_groom(parts, conflicts):
    """Counts for one groom's parts list: cards, rings, fill (loose unrooted cards), loose rooted cards per region,
    cards per ring, ring conflicts, parts with no valid UV layer, and the deepest penetration."""
    regions, braid_members = {}, {}
    fill = 0
    for p in parts:
        if p["kind"] != "card":
            continue
        if p.get("braid") is not None:
            braid_members[p["braid"]] = braid_members.get(p["braid"], 0) + 1
        elif p["region"] == "fill":
            fill += 1
        else:
            regions[p["region"]] = regions.get(p["region"], 0) + 1
    rings = sum(1 for p in parts if p["kind"] == "ring")
    return {"parts": len(parts), "cards": len(parts) - rings, "rings": rings, "fill": fill, "regions": regions,
            "braid_members": braid_members, "conflicts": len(conflicts),
            "no_valid_uv": sum(1 for p in parts if p.get("uv_layer") is None),
            "max_depth": max((p.get("depth", 0.0) for p in parts), default=0.0)}


# --- transforms -------------------------------------------------------------------------------------------
def mirror_x(points):
    """Reflect across the x = 0 plane."""
    return [(-p[0], p[1], p[2]) for p in points]


def swap_side_group(name):
    """A vertex group name with its `l_` / `r_` prefix swapped; any other name is unchanged."""
    if name.startswith("l_"):
        return "r_" + name[2:]
    if name.startswith("r_"):
        return "l_" + name[2:]
    return name


def translate(points, d):
    return [(p[0] + d[0], p[1] + d[1], p[2] + d[2]) for p in points]


def max_move(before, after):
    """The largest distance any point moved between two same-length lists."""
    if len(before) != len(after):
        raise ValueError("point lists differ in length: %d and %d" % (len(before), len(after)))
    return max((math.dist(a, b) for a, b in zip(before, after)), default=0.0)


# --- recipes ----------------------------------------------------------------------------------------------
def _braid_selected(braids, n):
    if braids == "all":
        return True
    return isinstance(braids, list) and n in braids


def select_parts(spec, source):
    """Ids of the parts one recipe part-spec picks from an inventory source, in part order. Loose cards (in no
    braid) are picked by `regions` and then narrowed by `length`, `hang` and `side`; a braid (its ring part and
    its member cards together) is picked whole by `braids` and not narrowed."""
    regions = spec.get("regions", [])
    braids = spec.get("braids", "none")
    lengths, hangs = spec.get("length"), spec.get("hang")
    want_side = spec.get("side", "both")
    out = []
    for p in source["parts"]:
        if p["kind"] == "ring":
            picked = _braid_selected(braids, p["ring_index"])
        elif p.get("braid") is not None:
            picked = _braid_selected(braids, p["braid"])
        else:
            picked = (p["region"] in regions
                      and (lengths is None or p.get("length") in lengths)
                      and (hangs is None or p.get("hang") in hangs)
                      and want_side in ("both", p.get("side")))
        if picked:
            out.append(p["id"])
    return out


def _check_spec(spec, family, where, inventory, errors):
    """Validate one part-spec's fields; returns its source dict, or None when the source is unusable."""
    name = spec.get("source")
    source = inventory["sources"].get(name)
    if source is None:
        errors.append("%s: unknown source %r" % (where, name))
        return None
    if source["family"] != family:
        errors.append("%s: source %r is a %s source, not %s" % (where, name, source["family"], family))
        return None
    for r in spec.get("regions", []):
        if r not in REGIONS[family]:
            errors.append("%s: unknown region %r for %s" % (where, r, family))
    braids = spec.get("braids", "none")
    if not (braids in ("none", "all") or (isinstance(braids, list) and all(isinstance(n, int) for n in braids))):
        errors.append("%s: braids must be 'none', 'all' or a list of ring indices" % where)
    for v in spec.get("length", []) or []:
        if v not in LENGTHS[family]:
            errors.append("%s: unknown length %r for %s" % (where, v, family))
    for v in spec.get("hang", []) or []:
        if family != "hair" or v not in HANGS:
            errors.append("%s: unknown hang %r for %s" % (where, v, family))
    if spec.get("side", "both") not in SIDES + ("both",):
        errors.append("%s: unknown side %r" % (where, spec.get("side")))
    for t in spec.get("transform", []):
        op = t.get("op")
        if op not in TRANSFORM_OPS:
            errors.append("%s: unknown transform op %r" % (where, op))
        elif op == "mirror_copy" and spec.get("side") not in ("left", "right"):
            errors.append("%s: mirror_copy needs side left or right (a one-sided selection to copy across)" % where)
        elif op == "translate":
            d = t.get("d")
            if not (isinstance(d, (list, tuple)) and len(d) == 3 and all(isinstance(x, (int, float)) for x in d)):
                errors.append("%s: translate needs d as three numbers" % where)
    return source


def _resolve_candidate(cand, inventory, errors):
    """-> [(source, part_id, transforms)] for one candidate, appending any problem to `errors`."""
    cid = cand.get("id")
    family = cand.get("family")
    if family not in FAMILIES:
        errors.append("candidate %r: unknown family %r" % (cid, family))
        return []
    picked, seen = [], set()

    def take(name, pid, transforms, where):
        if (name, pid) in seen:
            errors.append("%s: part %s of %s selected twice" % (where, pid, name))
            return
        seen.add((name, pid))
        picked.append((name, pid, transforms))

    for i, spec in enumerate(cand.get("parts", [])):
        where = "candidate %r part %d" % (cid, i)
        source = _check_spec(spec, family, where, inventory, errors)
        if source is None:
            continue
        ids = select_parts(spec, source)
        if not ids:
            errors.append("%s selects nothing" % where)
        transforms = tuple(spec.get("transform", []))
        for pid in ids:
            take(spec["source"], pid, transforms, where)
    drops = cand.get("drop_parts", {})
    for name, ids in drops.items():
        source = inventory["sources"].get(name)
        have = {p["id"] for p in source["parts"]} if source else set()
        errors.extend("candidate %r: drop_parts of %r has no part %s" % (cid, name, pid)
                      for pid in ids if pid not in have)
    dropped = {(name, pid) for name, ids in drops.items() for pid in ids}
    picked = [t for t in picked if (t[0], t[1]) not in dropped]
    seen -= dropped
    for name, ids in cand.get("add_parts", {}).items():
        source = inventory["sources"].get(name)
        if source is None or source["family"] != family:
            errors.append("candidate %r: add_parts of unknown %s source %r" % (cid, family, name))
            continue
        have = {p["id"] for p in source["parts"]}
        for pid in ids:
            if pid not in have:
                errors.append("candidate %r: add_parts of %r has no part %s" % (cid, name, pid))
            else:
                take(name, pid, (), "candidate %r" % cid)
    if not picked:
        errors.append("candidate %r selects nothing" % cid)
    return picked


def _existing_names(inventory):
    names = set()
    for src in inventory["sources"].values():
        for n in src.get("mesh_names", []) + [src["object"]]:
            names.add(n)
            if n.endswith("_lod0"):
                names.add(n[:-5])
    return names


def final_family(name):
    """The family a final name implies (`SK_Dwarf_Beard_*` is a beard, `Dwarf_Hair_*` a hair), or None."""
    if name.startswith("SK_Dwarf_Beard_"):
        return "beard"
    if name.startswith("Dwarf_Hair_"):
        return "hair"
    return None


def object_name(final_name, family):
    """The Blender object a final groom is built as: bare for a beard, `_lod0` suffixed for a hair."""
    return final_name + "_lod0" if family == "hair" else final_name


def _uses_mirror_copy(cand):
    return any(t.get("op") == "mirror_copy" for spec in cand.get("parts", []) for t in spec.get("transform", []))


def _check_finals(recipe, inventory, errors):
    existing = _existing_names(inventory)
    cands = {c.get("id"): c for c in recipe.get("candidates", [])}
    nums = {f: [] for f in FAMILIES}
    for name, entry in recipe.get("final", {}).items():
        if not isinstance(entry, dict):
            errors.append("final %r must be an object {candidate, channels}" % name)
            continue
        cid = entry.get("candidate")
        cand = cands.get(cid)
        if cand is None:
            errors.append("final %r names unknown candidate %r" % (name, cid))
            continue
        channels = entry.get("channels")
        if channels not in ("keep", "drop"):
            errors.append("final %r: channels must be keep or drop, got %r" % (name, channels))
        family = cand.get("family")
        pattern = {"beard": _BEARD_FINAL, "hair": _HAIR_FINAL}.get(family)
        is_test = "_Test_" in name
        m = pattern.match(name) if pattern and not is_test else None
        if final_family(name) != family or not (is_test or m):
            errors.append("final name %r does not fit family %s" % (name, family))
            continue
        if name in existing:
            errors.append("final name %r already exists in the inventory" % name)
        if channels == "keep" and _uses_mirror_copy(cand):
            errors.append("final %r keeps channels but candidate %r uses mirror_copy" % (name, cid))
        if m:
            nums[family].append(int(m.group(1)) if family == "beard" else ord(m.group(1)))
    for family, found in nums.items():
        if not found:
            continue
        first = int(_BEARD_FINAL.match(FIRST_FINAL[family]).group(1)) if family == "beard" \
            else ord(_HAIR_FINAL.match(FIRST_FINAL[family]).group(1))
        if sorted(found) != list(range(first, first + len(found))):
            errors.append("final %s names are not consecutive from %s" % (family, FIRST_FINAL[family]))


def family_finals(recipe, family):
    """[(final name, candidate id, channels)] of one family, sorted by name: what a build of that family writes."""
    return sorted((name, e["candidate"], e["channels"]) for name, e in recipe.get("final", {}).items()
                  if isinstance(e, dict) and final_family(name) == family)


def validate_recipe(recipe, inventory):
    """Every reason a recipe cannot be built from this inventory, as strings (empty list = valid)."""
    errors = []
    if recipe.get("version") != RECIPE_VERSION:
        errors.append("recipe version must be %d, got %r" % (RECIPE_VERSION, recipe.get("version")))
    seen_ids = set()
    used_sources = set()
    for cand in recipe.get("candidates", []):
        if cand.get("id") in seen_ids:
            errors.append("duplicate candidate id %r" % cand.get("id"))
        seen_ids.add(cand.get("id"))
        _resolve_candidate(cand, inventory, errors)
        used_sources.update(s.get("source") for s in cand.get("parts", []))
        for key in ("drop_parts", "add_parts"):
            used_sources.update(cand.get(key, {}))
    recorded = recipe.get("inventory_sha1", {})
    for name in sorted(used_sources | set(recorded)):
        src = inventory["sources"].get(name)
        if src is not None and recorded.get(name) != src["sha1"]:
            errors.append("source %r sha1 differs: recipe %s, inventory %s" % (name, recorded.get(name), src["sha1"]))
    _check_finals(recipe, inventory, errors)
    return errors


def resolve(recipe, inventory):
    """{candidate_id: [(source, part_id, transforms)]} for a valid recipe. Raises ValueError listing the problems
    when it is not valid."""
    errors = validate_recipe(recipe, inventory)
    if errors:
        raise ValueError("invalid recipe: " + "; ".join(errors))
    scratch = []
    return {c["id"]: _resolve_candidate(c, inventory, scratch) for c in recipe["candidates"]}


# --- build planning ----------------------------------------------------------------------------------------
SHAPE_NAMES = ["shape_%02d" % i for i in range(1, 102)]   # the 101 face-morph channels, in order


def expected_keys():
    """The shape key order a built groom must have when it keeps its channels."""
    return ["Basis"] + SHAPE_NAMES


def plan_shape_keys(source_key_names):
    """For a source's key block names, what each of the 101 channels comes from: [(name, "copy" | "zero")].
    A source with no keys at all (beard A_02) gives zero offsets; extra channels (A_03 has 202) are dropped; a
    source with some but not all of the 101 is refused."""
    if not source_key_names:
        return [(n, "zero") for n in SHAPE_NAMES]
    missing = [n for n in SHAPE_NAMES if n not in set(source_key_names)]
    if missing:
        raise ValueError("source has %d of the 101 channels (missing %s...)" % (101 - len(missing), missing[0]))
    return [(n, "copy") for n in SHAPE_NAMES]


def group_parts(selected):
    """Group resolved (source, part_id, transforms) rows by (source, transforms), in first-seen order:
    [(source, [part ids], transforms)]. Parts of one group are built and moved together."""
    groups, order = {}, []
    for source, pid, transforms in selected:
        key = (source, json.dumps(list(transforms), sort_keys=True))
        if key not in groups:
            groups[key] = (source, [], transforms)
            order.append(key)
        groups[key][1].append(pid)
    return [groups[k] for k in order]


def plan_pieces(transforms):
    """What a part group becomes under its transforms: [(mirrored, offset)], the original first. Applied in
    order: `mirror_copy` adds a mirrored copy of every piece so far (offsets mirror too), `translate` moves
    every piece. A piece sits at (mirror_x if mirrored) of the original, plus the offset."""
    pieces = [(False, (0, 0, 0))]
    for t in transforms:
        if t.get("op") == "mirror_copy":
            pieces = pieces + [(not m, (-o[0], o[1], o[2])) for m, o in pieces]
        elif t.get("op") == "translate":
            d = t["d"]
            pieces = [(m, (o[0] + d[0], o[1] + d[1], o[2] + d[2])) for m, o in pieces]
    return pieces


def apply_piece(points, piece):
    mirrored, offset = piece
    return translate(mirror_x(points) if mirrored else list(points), offset)


def reverse_loop_order(loop_starts, loop_totals):
    """The loop permutation that reverses every polygon's winding in place: new loop k is old loop result[k]."""
    out = []
    for s, t in zip(loop_starts, loop_totals):
        out.extend(range(s + t - 1, s - 1, -1))
    return out
