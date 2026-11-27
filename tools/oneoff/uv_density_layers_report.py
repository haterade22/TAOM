"""Build uv_density_layers.json and .md from two blender_measure_uv_density.py reports
(--uv-layer 0 and --uv-layer 1).

    python tools/oneoff/uv_density_layers_report.py <layer0.json> <layer1.json> <out_dir>

Verdict rule for layer 1: it can carry a tiling stone set as is when the mesh has a tiling
layer 1 (not the lightmap) whose p95/p5 is at most SPREAD_MAX. The factor scales the UV so the
median lands on TARGET_PX (600 px/m at 1024); it is shown with the px/m it gives at p5 and p95.
"""
import json
import os
import sys

SIZE = 1024
TARGET_PX = 600.0
LOW_PX, HIGH_PX = 500.0, 700.0
SPREAD_MAX = 1.5


def rows(report, layer_index):
    out = []
    for f in report["files"]:
        part = os.path.splitext(os.path.basename(f["file"]))[0]
        for o in f["objects"]:
            for slot, s in o["slots"].items():
                r = {"part": part, "file": f["file"].replace("\\", "/"), "layer_index": layer_index,
                     "slot": slot, "layer_name": o["uv_layer_measured"], "layers_on_mesh": o["uv_layer_names"]}
                if "uvpm_median" in s:
                    r.update(uv_type=s["uv_type"], p5=s["uvpm_p5"], median=s["uvpm_median"], p95=s["uvpm_p95"],
                             texels_per_m_1024=SIZE * s["uvpm_median"], world_area_m2=s["world_area_m2"],
                             outside_share=s["outside_0_1_area_share"])
                else:
                    r.update(uv_type="none", note=s.get("note", "unmeasured"), world_area_m2=s["world_area_m2"])
                out.append(r)
    return out


def verdict(part, l1rows):
    bad = [r for r in l1rows if r["uv_type"] == "none"]
    if bad:
        return {"part": part, "ok": False, "reason": "layer 1 is %s and has zero UV area (no measurable triangles)" % bad[0]["layer_name"]}
    if any(r["layer_name"] == "LightMapUV" for r in l1rows):
        r = l1rows[0]
        return {"part": part, "ok": False, "reason": "layer 1 is the lightmap (the mesh has no UVmap_1); not a tiling layer",
                "p95_over_p5": max(x["p95"] / x["p5"] for x in l1rows)}
    spread = max(r["p95"] / r["p5"] for r in l1rows)
    med = l1rows[0]["median"]
    f = (TARGET_PX / SIZE) / med
    lo, hi = min(r["p5"] for r in l1rows) * f * SIZE, max(r["p95"] for r in l1rows) * f * SIZE
    if spread > SPREAD_MAX or lo < LOW_PX or hi > HIGH_PX:
        return {"part": part, "ok": False, "reason": "p5 to p95 outside 500 to 700 px/m or p95/p5 above 1.5", "p95_over_p5": spread,
                "factor_if_scaled": f, "px_m_range_after": [lo, hi]}
    return {"part": part, "ok": True, "factor": f, "p95_over_p5": spread, "px_m_range_after": [lo, hi],
            "px_m_median_now": med * SIZE}


def main():
    l0, l1, out = sys.argv[1:4]
    r0, r1 = json.load(open(l0)), json.load(open(l1))
    all_rows = rows(r0, 0) + rows(r1, 1)
    parts = []
    for r in all_rows:
        if r["part"] not in parts:
            parts.append(r["part"])
    verdicts = [verdict(p, [r for r in all_rows if r["part"] == p and r["layer_index"] == 1]) for p in parts]
    os.makedirs(out, exist_ok=True)
    json.dump({"texture_size": SIZE, "target_px_per_m": [LOW_PX, HIGH_PX], "spread_max": SPREAD_MAX,
               "sources": [l0.replace("\\", "/"), l1.replace("\\", "/")], "rows": all_rows, "verdicts": verdicts},
              open(os.path.join(out, "uv_density_layers.json"), "w"), indent=1)
    md = ["# UV density by layer, elven raw parts", "",
          "Raw UE FBX in centimetres; the importer applied object scale 0.01 (read from each report), so values are metres and the extra scale is 1.0. "
          "Measured by tools/oneoff/blender_measure_uv_density.py with --uv-layer 0 and --uv-layer 1. "
          "Layer 1 is the second layer by index: on SM_Support_Pillar and SM_Large_Gazebo (no UVmap_1) that is LightMapUV.", "",
          "| part | material slot | layer | UV type | median UV units/m | texels/m @1024 | p5 | p95 |", "|---|---|---|---|---|---|---|---|"]
    for r in all_rows:
        slot = r["slot"].split(":", 1)[1]
        if r["uv_type"] == "none":
            md.append("| %s | %s | %d %s | none (%s) | | | | |" % (r["part"], slot, r["layer_index"], r["layer_name"], r["note"]))
        else:
            md.append("| %s | %s | %d %s | %s | %.3f | %.0f | %.3f | %.3f |" % (
                r["part"], slot, r["layer_index"], r["layer_name"], r["uv_type"], r["median"],
                r["texels_per_m_1024"], r["p5"], r["p95"]))
    md += ["", "## Can layer 1 carry a 1K tiling stone set at 500 to 700 px/m as it is?", "",
           "Rule: a tiling layer 1 (not the lightmap) with p95/p5 at most %.1f, scaled by one factor so the median is %.0f px/m at 1024 (UV scale 1 is the layer as exported)." % (SPREAD_MAX, TARGET_PX), ""]
    for v in verdicts:
        if v["ok"]:
            md.append("- %s: yes, uniform UV scale x%.2f (p95/p5 %.2f; %.0f to %.0f px/m after)." % (
                v["part"], v["factor"], v["p95_over_p5"], v["px_m_range_after"][0], v["px_m_range_after"][1]))
        elif "factor_if_scaled" in v:
            md.append("- %s: no, p5 to p95 does not fit 500 to 700 px/m (p95/p5 %.2f; one factor x%.2f would give %.0f to %.0f px/m)." % (
                v["part"], v["p95_over_p5"], v["factor_if_scaled"], v["px_m_range_after"][0], v["px_m_range_after"][1]))
        else:
            extra = " (p95/p5 %.2f)" % v["p95_over_p5"] if "p95_over_p5" in v else ""
            md.append("- %s: no, %s%s." % (v["part"], v["reason"], extra))
    open(os.path.join(out, "uv_density_layers.md"), "w").write("\n".join(md) + "\n")


if __name__ == "__main__":
    main()
