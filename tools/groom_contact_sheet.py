#!/usr/bin/env python3
"""Tile groom views from kitbash_grooms.py into one labelled contact sheet per family.

`build_loading_ring_sheet.py` packs fixed unlabelled RGBA cells with numpy, so it is not reused; this is a small
Pillow grid. Two kinds of input, both files named `<name>__<view>.png`:

  inventory regions   python tools/groom_contact_sheet.py REGIONS_DIR [--out DIR] [--cell 360]
      The `regions/` folder of an inventory run. One row per groom (sorted by name), written to
      `<out>/contact-beard.png` and `<out>/contact-hair.png` (default `<out>` is the folder above `regions/`).

  candidate previews  python tools/groom_contact_sheet.py PREVIEW_DIR --meta previews.json [--out DIR]
      The `preview/` folder of a `--mode preview` run, with its `previews.json`. One row per candidate, labelled
      with its id and note, its source grooms and its LOD0 triangle count, written to
      `<out>/contact-preview-<family>.png` (default `<out>` is the folder above `preview/`).

One column per view (front, side, back, threeq), the view names above the columns.

Needs Pillow. Exit 0 when at least one sheet was written, 2 otherwise.
"""
import argparse
import json
import os
import sys

from PIL import Image, ImageDraw

VIEWS = ("front", "side", "back", "threeq")
FAMILIES = {"beard": "SK_Dwarf_Beard", "hair": "Dwarf_Hair"}
BG = (20, 20, 24)
INK = (235, 235, 235)
LINE_H = 14
HEADER_H = 22


def scan(folder):
    """{family: {groom: {view: path}}} from the PNG names."""
    found = {f: {} for f in FAMILIES}
    for name in sorted(os.listdir(folder)):
        stem, ext = os.path.splitext(name)
        if ext.lower() != ".png" or "__" not in stem:
            continue
        groom, view = stem.rsplit("__", 1)
        for family, prefix in FAMILIES.items():
            if groom.startswith(prefix):
                found[family].setdefault(groom, {})[view] = os.path.join(folder, name)
    return found


def candidate_rows(meta, folder):
    """({id: {view: path}}, {id: [label lines]}) from a previews.json."""
    grooms, labels = {}, {}
    for c in meta["candidates"]:
        grooms[c["id"]] = {v: os.path.join(folder, "%s__%s.png" % (c["id"], v)) for v in VIEWS}
        labels[c["id"]] = ["%s: %s" % (c["id"], c.get("note", "")),
                           "from %s | LOD0 %d tris, %d verts" % (", ".join(c.get("sources", [])), c["tris"],
                                                                 c["verts"])]
    return grooms, labels


def build_sheet(grooms, cell, labels=None):
    """One labelled grid: a header row of view names, then a label bar and a row of views per groom."""
    labels = labels or {}
    lines = max([len(v) for v in labels.values()] + [1])
    label_h = LINE_H * lines + 8
    row_h = label_h + cell
    sheet = Image.new("RGB", (cell * len(VIEWS), HEADER_H + row_h * len(grooms)), BG)
    draw = ImageDraw.Draw(sheet)
    for c, view in enumerate(VIEWS):
        draw.text((c * cell + 6, 5), view, fill=INK)
    for r, (groom, views) in enumerate(sorted(grooms.items())):
        top = HEADER_H + r * row_h
        for i, text in enumerate(labels.get(groom, [groom])):
            draw.text((6, top + 5 + i * LINE_H), text, fill=INK)
        for c, view in enumerate(VIEWS):
            path = views.get(view)
            if path and os.path.isfile(path):
                with Image.open(path) as im:
                    sheet.paste(im.convert("RGB").resize((cell, cell), Image.Resampling.LANCZOS),
                                (c * cell, top + label_h))
            else:
                draw.text((c * cell + 6, top + label_h + 6), "missing " + view, fill=(220, 80, 80))
    return sheet


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("folder", help="an inventory run's regions/ folder, or a preview run's preview/ folder")
    ap.add_argument("--meta", help="previews.json of a preview run: label each candidate (preview mode)")
    ap.add_argument("--out", help="where to write the sheets (default: the folder above FOLDER)")
    ap.add_argument("--cell", type=int, default=360, help="pixels per view")
    args = ap.parse_args(argv)
    if not os.path.isdir(args.folder):
        print("failed: no folder %s" % args.folder, file=sys.stderr)
        return 2
    out = args.out or os.path.dirname(os.path.abspath(args.folder))
    os.makedirs(out, exist_ok=True)
    wrote = 0
    if args.meta:
        try:
            with open(args.meta, encoding="utf-8") as fh:
                meta = json.load(fh)
        except (OSError, ValueError) as exc:
            print("failed: cannot read %s: %s" % (args.meta, exc), file=sys.stderr)
            return 2
        grooms, labels = candidate_rows(meta, args.folder)
        if grooms:
            path = os.path.join(out, "contact-preview-%s.png" % meta.get("family", "family"))
            build_sheet(grooms, args.cell, labels).save(path)
            print("wrote %s: %d candidates (%s)" % (path, len(grooms), meta.get("renderer", "?")))
            wrote += 1
    else:
        for family, grooms in scan(args.folder).items():
            if not grooms:
                continue
            path = os.path.join(out, "contact-%s.png" % family)
            build_sheet(grooms, args.cell).save(path)
            print("wrote %s: %d grooms" % (path, len(grooms)))
            wrote += 1
    if not wrote:
        print("failed: nothing to tile in %s" % args.folder, file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
