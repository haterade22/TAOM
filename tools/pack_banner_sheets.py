#!/usr/bin/env python3
"""Pack the partly used banner icon sheets into as few sheets as they fit, name every sheet
taom_banners_NN, and audit the result (#1047).

A banner key names an icon id. Main/_Module/ModuleData/banner_icons.xml maps each id to a material
(one 4096 x 4096 green-on-black sheet of 16 cells, source in AssetSources/BannerIcons) and a
texture_index. Keys, saved banners and the UI sprites (named by icon id) never name a sheet, so moving
an icon to another sheet changes only its <Icon> row. All TAOM banner materials share one shader
(their _mtl.tpac files differ only in GUIDs, name and checksum, measured 2026-10-28), and the in-game
order comes from the rows, so a sheet's name says nothing about its icons.

Every sheet that uses fewer than 16 cells is taken whole, largest first (ties by name), and moved into
the free cells of an earlier partly used sheet with room; one that fits nowhere stays put and takes
later sheets itself. Then every sheet that is not yet named taom_banners_NN, or that receives cells
while its source is not a PNG, takes the first free taom_banners_NN, in the order its first icon
appears in banner_icons.xml. A renamed sheet that only changes name is copied byte for byte (a PSD
keeps its layers); a sheet that receives cells is written as a PNG. The old files stay until --prune,
and a second run is a no-op. Cells are copied verbatim (two-colour cells keep their red ink); free
cells keep the red and green placeholder checkerboard.

Usage:
    python tools/pack_banner_sheets.py                 # dry run: the plan and the free cells
    python tools/pack_banner_sheets.py --apply         # write the packed/renamed sources (repo and install) and rows
    python tools/pack_banner_sheets.py --audit         # the gate: exit 1 on any finding
    python tools/pack_banner_sheets.py --prune         # list the sheets no icon names
    python tools/pack_banner_sheets.py --prune --apply --backup-dir <dir>   # delete them, backed up first

Steps (docs/reference/banner-icon-generation.md "Packing partly used sheets"):
  1. --apply. It refuses while the audit reports a missing sheet, an empty or shared cell, or unnamed
     art, because it copies only the cells an icon row names.
  2. Compile each new or changed sheet: the TAOM Banner Studio's Build game resources, or the Modding
     Kit (import it as Albedo DXT1, Do Not Generate Mips, make a banner material of the same name for a
     new one, save, copy both .tpac into Main/_Module/Assets/BannerIcons).
  3. ./build.ps1 with the game and the Kit closed, so the install's banner_icons.xml names the packed sheets.
  4. --prune, then --prune --apply --backup-dir <dir>: deletes, in the repo and the install, every sheet
     that neither banner_icons.xml names (source, _mtl/_tex.tpac, and the texture's RuntimeDataCache .rdc).
     It refuses while the audit has any finding other than the unused sheets, and while the game or Kit runs.

Exit codes: 0 clean or done, 1 findings or a refusal, 2 the tool cannot run (a missing dependency, no rows).
"""
import argparse
import itertools
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path

try:
    import numpy as np
    import xxhash
    from PIL import Image
    MISSING = None
except ImportError as exc:  # main() reports it; the test module skips
    MISSING = exc

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import DEFAULT_GAME_ROOT, game_modules, game_or_kit_running  # noqa: E402
from check_rdc_entries import package_guid  # noqa: E402

REPO_MODULE = Path(__file__).resolve().parent.parent / "Main" / "_Module"
# The repo's module and the install's share this layout; every function takes a module root.
XML = Path("ModuleData", "banner_icons.xml")
SOURCES = Path("AssetSources", "BannerIcons")
PACKAGES = Path("Assets", "BannerIcons")
SPRITE_DATA = Path("GUI", "TAOMSpriteData.xml")
SPRITE_PNGS = Path("GUI", "SpriteParts", "ui_taom_bannericons")
CACHE = Path("RuntimeDataCache")
SPRITE_CATEGORY = "ui_taom_bannericons"

CELLS, GRID = 16, 4
SHEET_PX = 4096  # read at call time, so tests can patch a small sheet
SHEET = "taom_banners_{:02d}"
SHEET_RE = re.compile(r"taom_banners_\d+")
ICON = re.compile(r'(<Icon id=")(\d+)(" material_name=")([^"]+)(" texture_index=")(\d+)(" />)')
RED, GREEN = (255, 0, 0), (0, 255, 0)
# Bytes 0x34-0x43 of a _tex.tpac are its texture GUID, which its _mtl.tpac names; the source path the
# Kit imported is followed by the xxh64 of the source file (measured on all 24 sheets, 2026-10-28).
TEX_GUID = slice(0x34, 0x44)
SOURCE_PATH = re.compile(rb"\$BASE/Modules/[^/\x00]+/(AssetSources/BannerIcons/[^\x00]*?\.(?:png|psd))")
BLOCKING = ("missing_sheet", "empty_cell", "shared_cell", "unnamed_art", "duplicate_id")


def read_icons(data):
    """[(id, material, texture_index)] of every <Icon> row; raises ValueError on no rows or an incomplete
    row, so an unreadable file never looks like a module with nothing to pack, audit or keep."""
    rows = []
    for e in ET.fromstring(data).iter("Icon"):
        i, m, t = e.get("id"), e.get("material_name"), e.get("texture_index")
        if not (i and m and t and t.isdigit()):
            raise ValueError(f"<Icon> row without a usable id, material_name or texture_index: {e.attrib}")
        rows.append((i, m, int(t)))
    if not rows:
        raise ValueError("banner_icons.xml holds no <Icon> row")
    return rows


def sheet_files(root):
    if root is None:
        return []
    return sorted([*(root / SOURCES).glob("taom_banners_*.*"), *(root / PACKAGES).glob("taom_banners_*.tpac")])


def sheet_name(p):
    """The material a file belongs to: x for x.png, x.psd, x_mtl.tpac and x_tex.tpac."""
    return p.stem[:-4] if p.suffix.lower() == ".tpac" else p.stem


def sources(root):
    return {sheet_name(p): p for p in sheet_files(root) if p.suffix.lower() in (".png", ".psd")}


def cells_by_sheet(icons):
    cells = defaultdict(dict)
    for i, m, t in icons:
        cells[m][t] = i
    return cells


def merges(icons):
    """{icon id: (old material, old index, target material, cell)} for the rows of every sheet that moves
    into another one, the target named by its current material.

    Sheets with fewer than 16 cells are taken largest first (ties by name, so row order does not matter);
    each moves whole into the free cells (in order) of the first earlier one with room, and one that fits
    nowhere stays and takes later ones. Two sheets left apart never fit together, so a second run finds
    nothing: a sheet that stayed did not fit any earlier one, whose free cells only shrank since."""
    cells = cells_by_sheet(icons)
    bins = []  # [material, free cells, [(id, old material, old index, new index)]]
    for m in sorted((m for m in cells if len(cells[m]) < CELLS), key=lambda m: (-len(cells[m]), m)):
        b = next((b for b in bins if len(b[1]) >= len(cells[m])), None)
        if b is None:
            bins.append([m, sorted(set(range(CELLS)) - set(cells[m])), []])
        else:
            b[2] += [(cells[m][t], m, t, b[1].pop(0)) for t in sorted(cells[m])]
    return {i: (m, t, b[0], c) for b in bins for i, m, t, c in b[2]}


def plan(icons, taken=frozenset(), non_png=frozenset()):
    """{icon id: (old material, old index, sheet, cell)} for every row that changes; {} when every sheet is
    packed and named taom_banners_NN.

    Sheets merge as `merges` says. A sheet then keeps its name when it is taom_banners_NN and, if it
    receives cells, its source is a PNG (`non_png` names the others), so a write never stands next to a
    source of another format under one name. Every other sheet takes the first taom_banners_NN that no
    file or row uses (`taken` and the rows, casefolded), in the order its first icon (its own or one
    moved into it) appears in `icons`. Ids are assumed unique (the audit's duplicate_id blocks packing)."""
    cells, merged = cells_by_sheet(icons), merges(icons)
    first = {}
    for n, (i, m, _) in enumerate(icons):
        s = merged[i][2] if i in merged else m
        first.setdefault(s, n)
    receives = {s for _, _, s, _ in merged.values()}
    non_png = {n.casefold() for n in non_png}

    def keeps(s):
        return SHEET_RE.fullmatch(s) and not (s in receives and s.casefold() in non_png)

    used = {n.casefold() for n in taken} | {m.casefold() for m in cells}
    fresh = (n for n in map(SHEET.format, itertools.count(1)) if n.casefold() not in used)
    name = {s: s if keeps(s) else next(fresh) for s in sorted(first, key=first.get)}
    moves = {}
    for i, m, t in icons:
        if i in merged:
            _, _, s, c = merged[i]
            moves[i] = (m, t, name[s], c)
        elif name[m] != m:
            moves[i] = (m, t, name[m], t)
    return moves


def rewrite_icons(text, moves):
    """The XML text with each moved <Icon> row repointed; every other byte unchanged."""
    seen = set()

    def sub(m):
        if m.group(2) not in moves:
            return m.group(0)
        seen.add(m.group(2))
        _, _, sheet, cell = moves[m.group(2)]
        return f"{m.group(1)}{m.group(2)}{m.group(3)}{sheet}{m.group(5)}{cell}{m.group(7)}"

    out = ICON.sub(sub, text)
    missing = set(moves) - seen
    if missing:
        raise ValueError(f"no <Icon> row the rewrite can match for {sorted(missing)}")
    ET.fromstring(out.encode("utf-8"))
    return out


def checker(i):
    r, c = divmod(i, GRID)
    return RED if (r + c) % 2 == 0 else GREEN


def assemble(slots):
    """A SHEET_PX sheet from 16 slots (an array, or None for the placeholder checkerboard)."""
    c = SHEET_PX // GRID
    sheet = np.zeros((SHEET_PX, SHEET_PX, 3), dtype=np.uint8)
    for i, cell in enumerate(slots):
        r, k = divmod(i, GRID)
        sheet[r * c:(r + 1) * c, k * c:(k + 1) * c] = checker(i) if cell is None else cell
    return sheet


def is_art(cell):
    """False for a placeholder (red, green or black) cell: art is at least 32 pixels more than 16 levels
    off the cell's corner colour, so stray near-colour pixels (Rohan cell 11 had 8, one level off) don't count."""
    flat = cell.reshape(-1, 3).astype(np.int16)
    off = (np.abs(flat - flat[0]) > 16).any(axis=1)
    return int(off.sum()) >= 32


def load_cells(path):
    a = np.asarray(Image.open(path).convert("RGB"))
    if a.shape[:2] != (SHEET_PX, SHEET_PX):
        raise ValueError(f"{path}: {a.shape[1]}x{a.shape[0]}, expected {SHEET_PX}x{SHEET_PX}")
    c = SHEET_PX // GRID
    return [a[r * c:(r + 1) * c, k * c:(k + 1) * c] for r in range(GRID) for k in range(GRID)]


def build_sheets(moves, srcs):
    """Each target sheet built from the moves themselves, so its cells sit where the rewritten rows point.
    An existing packed sheet keeps its art; a new one starts as the checkerboard."""
    by_sheet = defaultdict(list)
    for mat, old, sheet, new in moves.values():
        by_sheet[sheet].append((new, mat, old))
    out = {}
    for sheet, moved in sorted(by_sheet.items()):
        if sheet in srcs:
            base = load_cells(srcs[sheet])
            slots = [cell if is_art(cell) else None for cell in base]
        else:
            slots = [None] * CELLS
        decoded = {m: load_cells(srcs[m]) for m in {m for _, m, _ in moved}}
        for new, mat, old in moved:
            slots[new] = decoded[mat][old]
        out[sheet] = assemble(slots)
    return out


def sprite_parts(root):
    path = root / SPRITE_DATA
    if not path.exists():
        return set()
    return {p.findtext("Name") for p in ET.parse(path).getroot().iter("SpritePart")
            if p.findtext("CategoryName") == SPRITE_CATEGORY}


def package_problems(root, mat, srcs):
    """Why the Kit packages of `mat` do not match its source: [] when they do."""
    tex, mtl = root / PACKAGES / f"{mat}_tex.tpac", root / PACKAGES / f"{mat}_mtl.tpac"
    t = tex.read_bytes()
    out = []
    if t[TEX_GUID] not in mtl.read_bytes():
        out.append(f"{mat}_mtl.tpac does not name {mat}_tex.tpac")
    m = SOURCE_PATH.search(t)
    if not m:
        return out + [f"{mat}_tex.tpac names no AssetSources/BannerIcons source"]
    src = root / m.group(1).decode("utf-8")
    if mat in srcs and src.name != srcs[mat].name:
        out.append(f"{mat}_tex.tpac was imported from {src.name}, the source on disk is {srcs[mat].name}")
    elif not src.exists() or xxhash.xxh64(src.read_bytes()).intdigest().to_bytes(8, "little") != t[m.end():m.end() + 8]:
        out.append(f"{mat}_tex.tpac is older than {src.name}: re-import it in the Kit and copy it back")
    return out


def audit(root):
    """Findings for the gate, by kind (empty lists when clean)."""
    icons = read_icons((root / XML).read_bytes())
    srcs = sources(root)
    names = {p.name for p in sheet_files(root)}
    parts = sprite_parts(root)
    rep = {k: [] for k in ("missing_sheet", "empty_cell", "shared_cell", "unnamed_art", "duplicate_id",
                           "missing_sprite", "missing_package", "stale_package", "unused_sheet", "mergeable",
                           "legacy_name", "no_alpha")}
    rep["duplicate_id"] = sorted(i for i, n in Counter(i for i, _, _ in icons).items() if n > 1)
    by_mat = defaultdict(lambda: defaultdict(list))
    for i, m, t in icons:
        by_mat[m][t].append(i)
        if i not in parts or not (root / SPRITE_PNGS / f"{i}.png").exists():
            rep["missing_sprite"].append(i)
    for mat, used in sorted(by_mat.items()):
        rep["shared_cell"] += [(mat, t, ids) for t, ids in sorted(used.items()) if len(ids) > 1]
        missing = [f"{mat}_{k}.tpac" for k in ("mtl", "tex") if f"{mat}_{k}.tpac" not in names]
        rep["missing_package"] += missing
        if mat not in srcs:
            rep["missing_sheet"].append(mat)
            continue
        if not missing:
            rep["stale_package"] += package_problems(root, mat, srcs)
        with Image.open(srcs[mat]) as img:
            if "A" not in img.getbands():  # an RGB source compiles to DXT1: speckled icons (#1272)
                rep["no_alpha"].append(mat)
        cells = load_cells(srcs[mat])
        rep["empty_cell"] += [(mat, t, ids) for t, ids in sorted(used.items())
                              if not 0 <= t < CELLS or not is_art(cells[t])]
        rep["unnamed_art"] += [(mat, t) for t, cell in enumerate(cells) if t not in used and is_art(cell)]
    rep["unused_sheet"] = sorted({sheet_name(p) for p in sheet_files(root)} - set(by_mat))
    rep["mergeable"] = sorted({m for m, _, _, _ in merges(icons).values()})
    rep["legacy_name"] = sorted(m for m in by_mat if not SHEET_RE.fullmatch(m))
    return rep


def free_cells(icons):
    """{sheet: [free cell indices]} of every partly used sheet, where a new emblem can go."""
    return {m: sorted(set(range(CELLS)) - set(c)) for m, c in sorted(cells_by_sheet(icons).items())
            if len(c) < CELLS}


def renames(icons, moves):
    """{target: old material} for each new sheet that only takes one whole sheet's rows at the same cells,
    so its source can be copied byte for byte (a PSD keeps its layers). A sheet that already exists and
    receives cells is never a rename, even when the cells line up."""
    cells = cells_by_sheet(icons)
    by_target = defaultdict(set)
    for m, t, s, c in moves.values():
        by_target[s].add((m, t == c))
    out = {}
    for s, v in by_target.items():
        (m, same), = v if len(v) == 1 else ((None, False),)
        if same and s not in cells and sum(x[0] == m for x in moves.values()) == len(cells[m]):
            out[s] = m
    return out


def findings(rep):
    return {k: v for k, v in rep.items() if v}


def pack(root, install, write):
    text = (root / XML).read_bytes().decode("utf-8")  # binary round trip keeps BOM and CRLF
    icons = read_icons(text.encode("utf-8"))
    taken = {sheet_name(p) for r in (root, install) for p in sheet_files(r)}
    srcs = sources(root)
    moves = plan(icons, taken, {m for m, p in srcs.items() if p.suffix.lower() != ".png"})
    print("free cells: " + (", ".join(f"{m} {c}" for m, c in free_cells(icons).items()) or "none"))
    if not moves:
        print("nothing to do: every sheet is packed and named taom_banners_NN")
        return 0
    targets = defaultdict(set)
    for mat, _, sheet, _ in moves.values():
        targets[sheet].add(mat)
    copies = renames(icons, moves)
    for sheet, mats in sorted(targets.items()):
        how = "rename" if sheet in copies else "re-import" if sheet in srcs else "new"
        print(f"{sheet} ({how}): " + ", ".join(sorted(mats)))
    print(f"{len(moves)} rows change")
    blocking = {k: v for k, v in findings(audit(root)).items() if k in BLOCKING}
    if blocking:
        print(f"refusing: packing copies only the named cells of sheets it can read: {blocking}")
        return 1
    if not write:
        print("dry run: nothing written (--apply to write)")
        return 0
    sheets = build_sheets({i: v for i, v in moves.items() if v[2] not in copies}, srcs)
    out_text = rewrite_icons(text, moves)
    dirs = [root / SOURCES] + ([install / SOURCES] if install and (install / SOURCES).is_dir() else [])
    for d in dirs:
        for sheet, old in copies.items():
            data = srcs[old].read_bytes()
            (d / f"{sheet}{srcs[old].suffix.lower()}").write_bytes(data)
        for sheet, a in sheets.items():
            # opaque alpha: the Kit's Albedo type compiles an RGB source to DXT1, and the banner
            # material draws a DXT1 sheet as speckled dots (#1272)
            Image.fromarray(a, "RGB").convert("RGBA").save(d / f"{sheet}.png")
    (root / XML).write_bytes(out_text.encode("utf-8"))
    print(f"wrote {len(copies) + len(sheets)} sheets to {', '.join(map(str, dirs))} and {root / XML}")
    if install is None:
        print("no install found: copy the new sheets into the install's AssetSources/BannerIcons before the Kit import")
    return 0


def prune(root, install, backup, write):
    rep = findings(audit(root))
    if set(rep) - {"unused_sheet"}:
        print(f"refusing: prune runs only when the unused sheets are the audit's only finding: {rep}")
        return 1
    named = {m for _, m, _ in read_icons((root / XML).read_bytes())}
    deployed = set()
    if install and (install / XML).exists():
        deployed = {m for _, m, _ in read_icons((install / XML).read_bytes())}
    keep = {m.casefold() for m in named | deployed}
    doomed = []  # (label, root, file) in deletion order: each .rdc before the _tex.tpac that names it
    for label, r in (("repo", root), ("install", install)):
        for p in sheet_files(r):
            if sheet_name(p).casefold() in keep:
                continue
            if p.name.lower().endswith("_tex.tpac") and (guid := package_guid(str(p))):
                rdc = r / CACHE / f"{guid.upper()}.rdc"
                if rdc.exists():
                    doomed.append((label, r, rdc))
            doomed.append((label, r, p))
    if install and (install / SPRITE_PNGS).is_dir():
        # a sprite PNG the repo deleted stays in the install, and the editor's sprite generation packs
        # it back into the picker atlas (#1272): keep what the repo has or either XML names
        ids = {i for r in (root, install) if (r / XML).exists() for i, _, _ in read_icons((r / XML).read_bytes())}
        tracked = {p.name.casefold() for p in (root / SPRITE_PNGS).glob("*")} if (root / SPRITE_PNGS).is_dir() else set()
        doomed += [("install", install, p) for p in sorted((install / SPRITE_PNGS).glob("*.png"))
                   if p.name.casefold() not in tracked and p.stem not in ids]
    if held := sorted(deployed - named):
        print(f"kept: the install's banner_icons.xml still names {held}; build, then prune again")
    for _, _, p in doomed:
        print(f"{'delete' if write else 'would delete'} {p}")
    if write and doomed:
        for label, r, p in doomed:
            dest = backup / label / p.relative_to(r)
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(p, dest)
        for _, _, p in doomed:
            p.unlink()
        print(f"{len(doomed)} files deleted; backups in {backup}")
    elif not write:
        print("dry run: nothing deleted (--prune --apply --backup-dir <dir> to delete)")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--audit", action="store_true")
    mode.add_argument("--prune", action="store_true")
    ap.add_argument("--apply", action="store_true", help="write (pack) or delete (--prune); default dry run")
    ap.add_argument("--backup-dir", type=Path)
    args = ap.parse_args(argv)
    if args.audit and args.apply:
        ap.error("--audit writes nothing; drop --apply")
    if args.backup_dir and not (args.prune and args.apply):
        ap.error("--backup-dir goes with --prune --apply")
    if args.prune and args.apply and not args.backup_dir:
        ap.error("--prune --apply needs --backup-dir")
    if MISSING:
        print(f"cannot run: {MISSING} (needs numpy, Pillow and xxhash)", file=sys.stderr)
        return 2
    install = game_modules(DEFAULT_GAME_ROOT) / "TAOM"
    install = install if install.is_dir() else None
    try:
        if args.audit:
            rep = findings(audit(REPO_MODULE))
            for k, v in rep.items():
                print(f"{k}: {v}")
            print("clean" if not rep else f"{len(rep)} finding kinds")
            return 1 if rep else 0
        if args.apply and install and game_or_kit_running():
            print("refusing: Bannerlord or the Modding Kit is running; close it before writing the install",
                  file=sys.stderr)
            return 1
        if args.prune:
            return prune(REPO_MODULE, install, args.backup_dir, args.apply)
        return pack(REPO_MODULE, install, args.apply)
    except (ValueError, OSError, ET.ParseError) as e:  # bad input or files: the tool cannot judge, exit 2
        print(f"cannot run: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
