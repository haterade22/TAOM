"""Draw the layout maps of a Painter stone candidate (round 2b) at twice the density of an earlier layout.

The blocks of an earlier layout folder (layout.json) are scaled, so the same wall is drawn again with the
maps at 2048 px and 922 px per metre; every size in millimetres keeps its world size. A candidate's options
(JSON) go to block_layout.make_layout.

Run:  python tools/painter/painter_stone_layout.py --from <layout folder> --out <dir> --cfg <json file>
cfg keys: ppm (922), size (2048), scale (2), seed, joint_mm, radius_mm, weather (dict or null) and any
          render_v2 option (radius_jitter, radius_detail, chip_frac, cloud_px, cloud_std, px_scale ...).
Run:  python tools/painter/painter_stone_layout.py --plan --out <dir> --courses-px 341,297,386 --seed 2
          (round 4: courses of its own at 1024 px, written as layout.json for a later --from)
Round 4 options (render_v2): face_level, face {tilt_deg, tilt_cap_mm, pillow_mm, pillow_width_mm, wave_mm,
          wave_m [min, max]}, face_depth_mm.
Round 3 options: profile ("round" or "chamfer"), edge_gain [top, right, bottom, left], line_px, tone_amp,
          tone_floor, chip_depth (see block_layout.render_v2). The blocks come from the 1024 px layout
          textures/v05_preview/alt_calm/layout, not from an already scaled folder.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import block_layout as bl  # noqa: E402


def build(src_folder, out, cfg):
    """Write the maps for `cfg` into `out`. Returns the info dict of the layout."""
    cfg = dict(cfg)
    meta = json.load(open(os.path.join(src_folder, "layout.json")))
    scale = int(cfg.pop("scale", 2))
    ppm = float(cfg.pop("ppm", 461.0 * scale))
    size = int(cfg.pop("size", 1024 * scale))
    blocks = bl.scale_blocks(meta["blocks"], scale)
    _, maps, info = bl.make_layout(ppm, size, blocks=blocks, **cfg)
    bl.write(out, maps, blocks)
    return info


def plan(out, courses_px, lengths_m, stagger_m, seed, ppm=461.0, size=1024):
    """Plan a layout of its own (a plinth, say) at 1024 px and write layout.json (blocks and info) into `out`.
    That folder is a `--from` folder for build(). `courses_px` are the course heights, top course first."""
    blocks = bl.plan_courses(ppm, size, courses_px, lengths_m, stagger_m, seed)
    info = {"ppm": ppm, "size": size, "courses_px": list(courses_px), "lengths_m": list(lengths_m),
            "stagger_m": stagger_m, "seed": seed, "blocks": len(blocks)}
    os.makedirs(out, exist_ok=True)
    json.dump({"blocks": blocks, "info": info}, open(os.path.join(out, "layout.json"), "w"), indent=1)
    return info


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="src")
    ap.add_argument("--out", required=True)
    ap.add_argument("--cfg", help="a JSON file")
    ap.add_argument("--plan", action="store_true", help="plan courses of your own and write layout.json only")
    ap.add_argument("--courses-px", help="with --plan: the course heights in px at 1024, top first")
    ap.add_argument("--lengths-m", default="0.95,1.30", help="with --plan: the shortest and longest block")
    ap.add_argument("--stagger-m", type=float, default=0.3)
    ap.add_argument("--seed", type=int, default=1)
    a = ap.parse_args(argv)
    if a.plan:
        info = plan(a.out, [int(v) for v in a.courses_px.split(",")],
                    tuple(float(v) for v in a.lengths_m.split(",")), a.stagger_m, a.seed)
    else:
        info = build(a.src, a.out, json.load(open(a.cfg)))
    print(json.dumps(info, indent=1))


if __name__ == "__main__":
    main()
