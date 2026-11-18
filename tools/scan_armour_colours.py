#!/usr/bin/env python3
"""Write the colour family of every armour piece: tools/data/armour_colour_families.json.

WHY
---
A battle set that puts a silver chest beside bronze pauldrons looks wrong in game (the maintainer,
2026-11-18: the Mirkwood guards after the armour band clones, #1440). The tools that pick armour
(generate_armour_band_clones.py) need to know the colour of each piece, and nothing in the item XML
says it. This tool reads it from the art and writes one committed table, so the pickers and the
validator read a file, not the install.

HOW A PIECE GETS ITS FAMILY (first rule that answers wins)
-----------------------------------------------------------
1. tools/armour_colour_overrides.json: a hand value, for a piece the measure reads wrong: `overrides` by
   item id, else `prefixes` by the longest id prefix (a line that shares one texture atlas, such as
   Lothlorien's t_ll_gl_props_a_d gold trim on every line).
2. A colour word in the item id, its mesh or one of its diffuse textures (word_family):
   silvergold, silver, steel, gold, brass, bronze, copper, black, dark, green, red, blue, white. "iron" is
   no colour word: it names the Iron Hills line of the Dwarves.
3. The measured colour of its diffuse textures (classify_textures). The chain is read through
   tools/audit_battle_equipment_memory.py: item -> metamesh -> materials -> textures. The engine shows a
   male wearer `<mesh>_male`, then `_slim` or `_converted`, then the plain mesh (MissionScreen
   OnEquipItemsFromSpawnEquipment, GetMultiMeshCopyWithGenderData); the scanner reads the male non-slim
   pick: `<mesh>_male`, else `<mesh>`. The diffuse texture is the one a material holds in slot 0, the
   engine's Material.MBTextureType.DiffuseMap (audit_battle_equipment_memory.material_texture_slots); a
   mesh whose materials do not decode falls back to the name rule (a name ending _d, _diffuse, _albedo,
   _basecolor, _col or _color). Each texture is read from the module's AssetSources (or Assets) image of
   the same stem, and its pixels are counted as gold, bronze, silver, black and other (pixel_classes).
   A texture with 35% or more gold and bronze makes the piece warm (gold or bronze, whichever the warm
   textures hold more of). Else a texture with 50% or more silver that is not cloth, leather, mail or
   hair by its name makes the piece silver. Else the piece is neutral: cloth, leather, dark or mixed.
   A piece whose textures have no image is unknown.

The measure is a heuristic: a mesh with several materials can read wrong (sk_ll_mw_chest_heavy_a read
gold where the Kit shows silver). Fix such a piece in the override file, never in the table. An override
reaches the pieces derived from its row: a band clone (taom_band_*) and a lord variant read the row of
their source when they have none of their own (override_chain).

FAMILIES AND CLASHES
--------------------
gold and bronze are warm, silver is cold; silvergold, black, neutral, unknown and the cloth colours
clash with nothing. Two pieces clash when one is warm and the other cold (clashes()). Gold beside
bronze does not clash (the maintainer accepted a gold helmet over bronze plate, 2026-11-18).

USAGE
-----
  python tools/scan_armour_colours.py            # dry run: counts and the rows that would change
  python tools/scan_armour_colours.py --apply    # write tools/data/armour_colour_families.json
  python tools/scan_armour_colours.py --check    # exit 1 when the table is stale against the install
It reads the live install and changes nothing there. The install is $BANNERLORD_GAME_MODULES, else the
Modules folder of $BANNERLORD_GAME_DIR (tools/_gamedir.py), else the default install; --game-modules
names it.

Exit codes: 0 clean or written; 1 the table is stale (--check) or an override row is bad; 2 nothing could
be measured (no LOTRLOME_Armory under the install, no texture image under it, as a packaged install ships
no AssetSources, or no numpy or Pillow): nothing was checked or written; 3 a crash. data_gates.py reads 2
as NOT RUN.
"""
from __future__ import annotations

import argparse
import collections
import importlib
import json
import re
import sys
import traceback
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
REPO = TOOLS.parent
sys.path.insert(0, str(TOOLS))

TABLE = TOOLS / "data" / "armour_colour_families.json"
OVERRIDES = TOOLS / "armour_colour_overrides.json"
MODULEDATA_MODULE = REPO / "Main" / "_Module"
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
XML_MODULES = ("Native", "SandBoxCore", "SandBox", "LOTRLOME_Armory")
PACK_MODULES = ("LOTRLOME_Armory", "Native", "SandBox")
IMAGE_ROOTS = ("LOTRLOME_Armory/AssetSources", "LOTRLOME_Armory/Assets", "Native/AssetSources")
IMAGE_EXTS = (".png", ".tga", ".dds", ".jpg")

FAMILIES = ("gold", "bronze", "silver", "silvergold", "black", "green", "red", "blue", "white", "neutral", "unknown")
WARM = frozenset({"gold", "bronze"})
COLD = frozenset({"silver"})
# Longest first: silvergold before silver and gold.
WORDS = (("silvergold", "silvergold"), ("silver_gold", "silvergold"), ("goldsilver", "silvergold"),
         ("silver", "silver"), ("steel", "silver"), ("gold", "gold"), ("brass", "gold"),
         ("bronze", "bronze"), ("copper", "bronze"), ("black", "black"), ("dark", "black"),
         ("green", "green"), ("red", "red"), ("blue", "blue"), ("white", "white"))
DIFFUSE_SLOT = 0   # Material.MBTextureType.DiffuseMap
DIFFUSE_RE = re.compile(r"(_d|_diffuse|_albedo|_basecolor|_col|_color)$")
CLOTH_RE = re.compile(r"(cloth|skirt|tunic|cape|robe|waist|cloak|hood|fur|leather|boot|hair|scarf|belt|chainmail|mail)")
WARM_SHARE = 0.35
COLD_SHARE = 0.50

DESCRIPTION = [
    "GENERATED by tools/scan_armour_colours.py --apply from the live install's art and",
    "tools/armour_colour_overrides.json. item id -> family (gold, bronze, silver, silvergold, black, a cloth colour,",
    "neutral or unknown), how it was found (override, word, measured) and the texture measured. Do not edit by",
    "hand: fix a wrong piece in the override file. docs/features/armor-balance.md \"Colour families\".",
]


class ColourError(Exception):
    pass


# =============================================================================
# Pure rules (tested in tools/tests/test_scan_armour_colours.py)
# =============================================================================

_WORD_PATTERNS = tuple((re.compile(r"(?:^|[_\-\s.\d])" + re.escape(word) + r"(?:$|[_\-\s.\d])"), fam, word)
                       for word, fam in WORDS)


def word_family(*names):
    """(family, word) of the first colour word in `names`, each read as tokens split on _ - . space and
    digits; (None, None) when none names a colour."""
    for name in names:
        low = (name or "").lower()
        for pattern, fam, word in _WORD_PATTERNS:
            if pattern.search(low):
                return fam, word
    return None, None


def pixel_classes(rgb, alpha=None):
    """{class: share} over the pixels of an (n, 3) float array in 0..1, the pixels with alpha 0.5 or more
    when `alpha` is given. Classes: black (value under 0.18), and among pixels of value over 0.30: silver
    (saturation under 0.18), gold (hue 38 to 65, saturation 0.30 or more), bronze (hue 12 to 38), red, green
    and blue by hue; everything else is other."""
    import numpy as np
    px = rgb if alpha is None else rgb[alpha >= 0.5]
    if len(px) == 0:
        return {}
    mx, mn = px.max(1), px.min(1)
    v = mx
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    r, g, b = px[:, 0], px[:, 1], px[:, 2]
    h = (np.degrees(np.arctan2(np.sqrt(3) * (g - b), 2 * r - g - b)) + 360) % 360
    cls = np.full(len(px), "other", dtype=object)
    bright = v > 0.30
    cls[v < 0.18] = "black"
    cls[bright & (s < 0.18)] = "silver"
    cls[bright & (s >= 0.30) & (h >= 38) & (h < 65)] = "gold"
    cls[bright & (s >= 0.30) & (h >= 12) & (h < 38)] = "bronze"
    cls[bright & (s >= 0.30) & ((h < 12) | (h >= 330))] = "red"
    cls[bright & (s >= 0.25) & (h >= 75) & (h < 170)] = "green"
    cls[bright & (s >= 0.25) & (h >= 190) & (h < 260)] = "blue"
    counts = collections.Counter(cls)
    return {k: round(n / len(px), 3) for k, n in sorted(counts.items())}


def classify_textures(measured):
    """(family, texture) from [(texture name, {class: share})]: warm first, then silver that is not cloth,
    else neutral; (unknown, "") when nothing was measured."""
    if not measured:
        return "unknown", ""
    warm = [(t, sh) for t, sh in measured if sh.get("gold", 0) + sh.get("bronze", 0) >= WARM_SHARE]
    if warm:
        gold = sum(sh.get("gold", 0) for _t, sh in warm)
        bronze = sum(sh.get("bronze", 0) for _t, sh in warm)
        best = max(warm, key=lambda x: x[1].get("gold", 0) + x[1].get("bronze", 0))
        return ("gold" if gold >= bronze else "bronze"), best[0]
    cold = [(t, sh) for t, sh in measured if sh.get("silver", 0) >= COLD_SHARE and not CLOTH_RE.search(t.lower())]
    if cold:
        return "silver", max(cold, key=lambda x: x[1].get("silver", 0))[0]
    return "neutral", measured[0][0]


def clashes(a, b):
    """True when family `a` beside family `b` looks wrong: one warm (gold, bronze), the other silver."""
    return (a in WARM and b in COLD) or (a in COLD and b in WARM)


def set_clashes(families):
    """The (slot, slot) pairs that clash in one set, from {slot: family}."""
    slots = sorted(families)
    return [(x, y) for i, x in enumerate(slots) for y in slots[i + 1:] if clashes(families[x], families[y])]


def load_table(path=TABLE):
    """{item id: family} from the committed table; {} when the file is missing."""
    if not Path(path).exists():
        return {}
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    return {k: v["family"] for k, v in data.get("items", {}).items()}


def family_of(item_id, table):
    """The family of an item for a picker or the validator: its own row, else its band clone source's (a clone
    has a row of its own once the scanner has read it), else unknown. The one lookup both consumers use."""
    import rebalance_armor as ra
    return table.get(item_id) or table.get(ra.band_clone_source(item_id)) or "unknown"


def load_overrides(path=OVERRIDES):
    """(by id, by prefix): {key: {family, why}} each. The file may carry a byte order mark."""
    data = json.loads(Path(path).read_bytes().decode("utf-8-sig"))
    ids, prefixes = data.get("overrides", {}), data.get("prefixes", {})
    bad = {k: v for k, v in {**ids, **prefixes}.items() if v.get("family") not in FAMILIES}
    if bad:
        raise ColourError(f"{Path(path).name}: unknown family in {sorted(bad)[:5]}")
    return ids, prefixes


def override_for(item_id, ids, prefixes):
    """The hand row for `item_id`: its own row, else the longest prefix row, else None."""
    if item_id in ids:
        return ids[item_id]
    hits = [p for p in prefixes if item_id.startswith(p)]
    return prefixes[max(hits, key=len)] if hits else None


def override_chain(item_id, ids, prefixes):
    """The hand row for `item_id`, else for the piece it is derived from: the source of a band clone
    (rebalance_armor.band_clone_source), else the source of a lord variant
    (derive_armor_tiers.lord_variant_source). A derived piece has its source's art."""
    import derive_armor_tiers as dat
    import rebalance_armor as ra
    for name in (item_id, ra.band_clone_source(item_id), dat.lord_variant_source(item_id)):
        row = override_for(name, ids, prefixes) if name else None
        if row is not None:
            return row
    return None


def unused_overrides(ids, prefixes, item_ids):
    """The override keys that name no item: an id that is not in `item_ids`, then each prefix no id starts with."""
    item_ids = set(item_ids)
    return sorted(set(ids) - item_ids) + sorted(p for p in prefixes if not any(i.startswith(p) for i in item_ids))


def mesh_diffuse(abm, index, mesh):
    """The diffuse texture names of an item's mesh: the male metamesh `<mesh>_male`, else `<mesh>`, read through
    `abm` (audit_battle_equipment_memory). The slot data decides (pick_diffuse); the texture names of
    mesh_assets are read only when no material decodes. [] when the mesh has no metamesh."""
    for name in (f"{mesh}_male", mesh):
        meta = index.get("metamesh", name) if name else None
        if meta is None:
            continue
        slots = abm.material_texture_slots(meta, index)
        if slots:
            return pick_diffuse(slots, [])
        assets = abm.mesh_assets(meta, index)
        return pick_diffuse({}, sorted(v["name"] for (kind, _n), v in assets.items() if kind == "texture"))
    return []


def pick_diffuse(slot_names, names):
    """The diffuse textures of a mesh: those a material holds in slot DIFFUSE_SLOT when `slot_names`
    ({slot: texture names}) has data, else the names of `names` that the name rule (DIFFUSE_RE) accepts."""
    if slot_names:
        return sorted(slot_names.get(DIFFUSE_SLOT, ()))
    return [t for t in names if DIFFUSE_RE.search(t.lower())]


# =============================================================================
# Reading the install
# =============================================================================

def missing_imaging():
    """The names of the image libraries this machine lacks (numpy, PIL), in that order."""
    out = []
    for name in ("numpy", "PIL"):
        try:
            importlib.import_module(name)
        except ImportError:
            out.append(name)
    return out


def image_index(game_modules):
    """{lower-case stem: path} of every texture image under IMAGE_ROOTS of the install; the first path in
    sorted order wins a shared stem. Empty when the install ships no AssetSources."""
    images = {}
    for root in IMAGE_ROOTS:
        base = Path(game_modules) / root
        if base.is_dir():
            for p in sorted(base.rglob("*")):
                if p.suffix.lower() in IMAGE_EXTS:
                    images.setdefault(p.stem.lower(), p)
    return images


def _measure(path):
    import numpy as np
    from PIL import Image
    try:
        im = Image.open(path)
        im.thumbnail((128, 128))
        a = np.asarray(im.convert("RGBA")).astype(float) / 255.0
    except Exception:  # noqa: BLE001  an unreadable image is a piece we cannot measure
        return None
    return pixel_classes(a[..., :3].reshape(-1, 3), a[..., 3].reshape(-1))


def build(game_modules, images):
    """{item id: {family, how, texture}} for every Armor item of the Armory and TAOM; `images` is image_index()."""
    import audit_battle_equipment_memory as abm
    import audit_map_scene_memory as amsm
    mods = [(m, Path(game_modules) / m) for m in XML_MODULES] + [("TAOM", MODULEDATA_MODULE)]
    defs = abm.load_definitions(mods)
    index = amsm.AssetIndex()
    for m in PACK_MODULES:
        index.add_module(m, Path(game_modules) / m, trees=amsm.PACK_TREES + ("Assets",))
    ids, prefixes = load_overrides()
    measured_cache = {}
    out = {}
    armour = sorted((i for i in defs.items.values()
                     if i.component == "Armor" and i.module in ("LOTRLOME_Armory", "TAOM")), key=lambda i: i.id)
    for item in armour:
        o = override_chain(item.id, ids, prefixes)
        if o is not None:
            out[item.id] = {"family": o["family"], "how": "override", "texture": ""}
            continue
        diffuse = mesh_diffuse(abm, index, item.mesh)
        fam, word = word_family(item.id, item.mesh, *diffuse)
        if fam:
            out[item.id] = {"family": fam, "how": f"word {word}", "texture": ""}
            continue
        measured = []
        for t in diffuse:
            key = t.lower()
            if key not in measured_cache:
                img = images.get(key)
                measured_cache[key] = _measure(img) if img is not None else None
            if measured_cache[key]:
                measured.append((t, measured_cache[key]))
        fam, tex = classify_textures(measured)
        out[item.id] = {"family": fam, "how": "measured" if measured else "no image", "texture": tex}
    unused = unused_overrides(ids, prefixes, out)
    if unused:
        raise ColourError(f"overrides name items no module defines: {', '.join(unused[:10])}")
    return out


def render(items):
    return json.dumps({"description": DESCRIPTION, "items": items}, indent=1, sort_keys=False) + "\n"


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the table")
    mode.add_argument("--check", action="store_true", help="exit 1 when the table is stale")
    ap.add_argument("--game-modules", default=None, help="the install's Modules folder")
    args = ap.parse_args(argv)
    from _gamedir import game_modules as resolve_modules
    game_modules = Path(args.game_modules) if args.game_modules else resolve_modules(DEFAULT_GAME_ROOT)
    if not (game_modules / "LOTRLOME_Armory").is_dir():
        verdict = "ERROR" if args.game_modules else "SKIPPED"
        print(f"{verdict}: no LOTRLOME_Armory under {game_modules}; nothing was checked or written")
        return 2
    lacking = missing_imaging()
    if lacking:
        print(f"ERROR: {' and '.join(lacking)} not installed; the art cannot be measured "
              f"(pip install numpy pillow); nothing was checked or written")
        return 2
    images = image_index(game_modules)
    if not images:
        print(f"ERROR: no texture image under {', '.join(str(game_modules / r) for r in IMAGE_ROOTS)}; nothing can be "
              f"measured (a packaged install ships no AssetSources); nothing was checked or written")
        return 2
    try:
        items = build(game_modules, images)
    except ColourError as exc:
        print(f"ERROR: {exc}")
        return 1
    text = render(items)
    old = TABLE.read_text(encoding="utf-8") if TABLE.exists() else ""
    old_items = json.loads(old)["items"] if old.startswith("{") else {}
    changed = sorted(k for k in set(items) | set(old_items) if items.get(k) != old_items.get(k))
    counts = collections.Counter(v["family"] for v in items.values())
    how = collections.Counter(v["how"].split()[0] for v in items.values())
    print(f"pieces {len(items)}; images {len(images)}; families {dict(counts.most_common())}; "
          f"found by {dict(how.most_common())}")
    print(f"rows that differ from {TABLE.name}: {len(changed)}")
    for k in changed[:40]:
        print(f"  {k}: {old_items.get(k, {}).get('family')} -> {items.get(k, {}).get('family')}")
    if args.check:
        return 1 if changed else 0
    if args.apply and text != old:
        TABLE.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {TABLE}")
    return 0


def entry(argv=None):
    """main() with a crash kept apart from a verdict: a traceback exits 3, never 0, 1 or 2."""
    try:
        return main(argv)
    except Exception:  # noqa: BLE001  every crash is the same exit
        traceback.print_exc()
        print("scan_armour_colours.py crashed; no verdict, exit 3", file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(entry())
