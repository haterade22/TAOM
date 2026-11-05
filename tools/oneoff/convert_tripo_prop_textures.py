"""
Pack a prop's plain PBR maps into Bannerlord spec-gloss triples t_<stem>_{d,n,s}.png.

Sibling of convert_rivendell_textures.py, same conversion math (_d = albedo x (1-metallic);
_s = R:metallic, G:gloss=255-rough, B:AO, the empirical packing verified against the shipped
Gondor/Mirkwood kits), but fed by SEPARATE maps (no packed-map layout guessing): a bake staging
dir from tools/oneoff/blender_prep_tripo_prop.py, or a Substance Painter export.

Substance round-trip (the reason this stays its own script):
    1. Import the prepped FBX (clean chart UVs) into Substance Painter.
    2. Load the staging maps as fill layers / bake anchors, paint.
    3. Export as PNGs. Painter 12.1.5's scriptable export presets
       (list_predefined_export_presets()) offer no plain "PBR Metallic Roughness" one, so the
       TAOM scripts export "glTF PBR Metal Roughness". Its <set>_occlusionRoughnessMetallic map
       packs R AO, G roughness, B metallic and is read here; separate maps win over its channels.
    4. Re-run this script with --src <export dir> to regenerate the triples.

Usage (system Python; needs Pillow + numpy):
    python tools/oneoff/convert_tripo_prop_textures.py --src <export dir> --dst <textures dir> \
        [--stem t_mordor_mm_throne] [--max-size 2048] [--flip-green] [--dry-run] [--force] \
        [--match Elk_M_]

--src and --dst are required: there is no default folder, because a default --dst once pointed at
a live game module and a bare run overwrote shipped files.

Texture sets: a file belongs to the set named by whatever precedes its role word
(<set>_baseColor.png, <set>_normal.png, ...). Without --stem every set that has a basecolor is
converted on its own to t_<set>_{d,n,s}.png (a Painter export of 7 sets becomes 7 triples, each
named after its set, which is the FBX material). With --stem exactly one such set must remain
after --match; bare role file names (no set prefix, e.g. a bake staging folder) need --stem.
The Animalia packs (Fab, #646) use --match <prefix> --stem <name>.

Nothing is written unless every output of every set is free: an existing file stops the run
(all or nothing) unless --force. --dry-run only prints.

--flip-green: the in-editor smoke test is the arbiter for normal-map relief
direction (Tripo/bake output is OpenGL +Y; flip if relief looks inverted).
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# Painter 12 ships no unpacked PBR preset; its glTF one packs AO/rough/metal
# into one map. Classified first, so its name never claims the single-map roles.
ORM_PATTERN = r"occlusion_?roughness_?metal(lic)?|orm"
ORM_CHANNELS = {"ao": 0, "rough": 1, "metal": 2}

ROLE_PATTERNS = {
    "base": r"base_?colou?r|albedo|diffuse",
    "normal": r"normal",
    "rough": r"roughness",
    "metal": r"metal(lic|ness)?",
    "ao": r"ao|ambient_?occlusion|occlusion",
}

ALL_ROLES = "|".join([ORM_PATTERN, *ROLE_PATTERNS.values()])
SET_RE = re.compile(rf"(?P<set>.*?)[_-]?(?P<role>{ALL_ROLES})", re.IGNORECASE)


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255.0


def save_png(arr: np.ndarray, path: Path, max_size: int):
    im = Image.fromarray(np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8))
    if max_size and max(im.size) > max_size:
        scale = max_size / max(im.size)
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))),
                       Image.LANCZOS)
    im.save(path)


def classify(role_text: str) -> str:
    if re.fullmatch(ORM_PATTERN, role_text, re.IGNORECASE):
        return "orm"
    for role, pat in ROLE_PATTERNS.items():
        if re.fullmatch(pat, role_text, re.IGNORECASE):
            return role
    raise ValueError(role_text)


def find_sets(src: Path, match: str = "") -> dict[str, dict[str, Path]]:
    """{set prefix: {role: path}}; the prefix keeps its file-name case, '' for bare role names."""
    sets: dict[str, dict[str, Path]] = {}
    for path in sorted(src.iterdir()):
        if path.suffix.lower() not in (".png", ".tga", ".jpg", ".jpeg"):
            continue
        if match and match.lower() not in path.stem.lower():
            continue
        m = SET_RE.fullmatch(path.stem)
        if not m:
            # the role word must end the name (x_Normal_OpenGL, x_BaseColor_2k do not match)
            print(f"[convert] skipped {path.name}: its name does not end in a map role")
            continue
        sets.setdefault(path.stem[:m.end("set")], {}).setdefault(classify(m.group("role")), path)
    return sets


def find_maps(src: Path, match: str = "") -> dict[str, Path]:
    """Every role found, sets merged (first file wins): for a folder known to hold one set."""
    found: dict[str, Path] = {}
    for roles in find_sets(src, match).values():
        for role, path in roles.items():
            found.setdefault(role, path)
    return found


def output_names(stem: str, maps: dict[str, Path]) -> list[str]:
    names = [f"{stem}_d.png", f"{stem}_s.png"]
    if "normal" in maps:
        names.append(f"{stem}_n.png")
    return names


def convert_set(label: str, stem: str, maps: dict[str, Path], opts) -> None:
    albedo = load(maps["base"])
    h, w = albedo.shape[:2]

    def gray(role, default):
        if role in maps:
            arr = load(maps[role])[:, :, 0]
        elif "orm" in maps:
            arr = load(maps["orm"])[:, :, ORM_CHANNELS[role]]
        else:
            print(f"[convert] no {role} map: using constant {default}")
            return np.full((h, w), default, dtype=np.float32)
        if arr.shape != (h, w):
            arr = np.asarray(Image.fromarray((arr * 255).astype(np.uint8))
                             .resize((w, h), Image.BILINEAR)).astype(np.float32) / 255.0
        if arr.std() < (1.5 / 255.0):
            print(f"[convert] WARNING: {role} map is near-constant ({arr.mean():.3f})")
        return arr

    rough = gray("rough", 0.7)
    metal = gray("metal", 0.0)
    ao = gray("ao", 1.0)

    d_out = albedo[:, :, :3] * (1.0 - metal)[:, :, None]
    s_out = np.dstack([metal, 1.0 - rough, ao])
    outputs = {f"{stem}_d.png": d_out, f"{stem}_s.png": s_out}

    if "normal" in maps:
        normal = load(maps["normal"])[:, :, :3]
        if normal.std(axis=(0, 1)).max() < (1.5 / 255.0):
            print(f"[convert] WARNING: {label} normal map is flat (a uniform 128,128,255): "
                  "a flat normal can be shared instead of shipped per set")
        if opts.flip_green:
            normal[:, :, 1] = 1.0 - normal[:, :, 1]
        outputs[f"{stem}_n.png"] = normal
    else:
        print(f"[convert] WARNING: {label} has no normal map")

    for name, arr in outputs.items():
        out = opts.dst / name
        if opts.dry_run:
            print(f"[convert] dry-run: would write {out} {arr.shape}")
        else:
            save_png(arr, out, opts.max_size)
            print(f"[convert] wrote {out}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", type=Path, required=True)
    ap.add_argument("--dst", type=Path, required=True)
    ap.add_argument("--stem", help="output stem for the single set left after --match; "
                                   "omit to convert every set to t_<set>")
    ap.add_argument("--max-size", type=int, default=2048)
    ap.add_argument("--flip-green", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true", help="overwrite existing outputs")
    ap.add_argument("--match", default="",
                    help="only files whose name contains this (one folder holding several sets, e.g. "
                         "the Animalia elk's Elk_M_* body and Elk_Antlers_* maps)")
    opts = ap.parse_args(argv)

    sets = find_sets(opts.src, opts.match)
    print(f"[convert] sets found: "
          f"{({s or '(bare)': sorted(r) for s, r in sets.items()})}")
    based = {s: r for s, r in sets.items() if "base" in r}

    if opts.stem:
        if len(based) != 1:
            names = ", ".join(r["base"].name for r in based.values())
            print(f"[convert] ERROR: {len(based)} basecolor maps ({names}): pass --match <set>_ "
                  "or drop --stem to convert each set")
            return 1
        jobs = [(s or opts.stem, opts.stem, r) for s, r in based.items()]
    else:
        if not based:
            print("[convert] ERROR: no basecolor map in --src")
            return 1
        if "" in based:
            print("[convert] ERROR: basecolor maps with no set prefix "
                  f"({based['']['base'].name}): pass --stem <name>")
            return 1
        jobs = [(s, f"t_{s}", r) for s, r in based.items()]

    planned = [n for _, stem, r in jobs for n in output_names(stem, r)]
    existing = [n for n in planned if (opts.dst / n).exists()]
    if existing and not (opts.force or opts.dry_run):
        print(f"[convert] ERROR: would overwrite {', '.join(existing)}; pass --force")
        return 1

    if not opts.dry_run:
        opts.dst.mkdir(parents=True, exist_ok=True)
    for label, stem, maps in jobs:
        print(f"[convert] {label}: {({r: p.name for r, p in maps.items()})}")
        convert_set(label, stem, maps, opts)
    return 0


if __name__ == "__main__":
    sys.exit(main())
