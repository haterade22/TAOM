"""Build the filterable picture page of donor pieces for a kingdom kit pilot.

Reads the catalog JSON that blender_catalog_ue_parts.py wrote and the control
geometry JSON that blender_measure_wall_profile.py wrote, and writes one
self-contained HTML page beside them. Pictures stay as files; the page links
them by relative path, so open the page from its own folder.

    python tools/oneoff/build_donor_pieces_page.py --work <work folder> --out <html>

The work folder holds kit\\kit_parts.json, kit\\thumbs\\<name>.png and
control\\control_geometry.json.
"""
import argparse
import json
import os
import re
import sys

FAMILIES = [
    ("tower", r"tower|spire"),
    ("gate", r"gate"),
    ("arch", r"arch|vault"),
    ("pillar", r"pillar|column"),
    ("railing", r"rail|fence|balust"),
    ("wall", r"wall"),
    ("foundation", r"foundation|platform|podium"),
    ("stair", r"stair|step"),
    ("bridge", r"bridge"),
    ("roof", r"roof"),
    ("gazebo", r"gazebo"),
    ("statue", r"statue"),
    ("rock", r"rock|cliff"),
    ("ground", r"road|floor|path|slab"),
]


def family_of(name):
    """First family whose pattern matches the part name; 'other' if none does."""
    low = name.lower()
    for fam, pat in FAMILIES:
        if re.search(pat, low):
            return fam
    return "other"


def kit_rows(work):
    with open(os.path.join(work, "kit", "kit_parts.json"), encoding="utf-8") as fh:
        data = json.load(fh)
    rows = []
    for part in data["parts"]:
        size = part.get("size_m") or [0, 0, 0]
        rows.append({
            "name": part["name"],
            "kit": "elven kit (raw UE export)",
            "family": family_of(part["name"]),
            "size": [round(v, 2) for v in size],
            "tris": part.get("tris", 0),
            "materials": part.get("materials", []),
            "uv": part.get("uv_layer_count_max", 0),
            "img": "kit/thumbs/" + part["name"] + ".png",
            "note": "",
        })
    return rows


def control_rows(work):
    path = os.path.join(work, "control", "control_geometry.json")
    if not os.path.exists(path):
        return []
    labels = {
        "wall_solid": ("gondor_castle_wall_20m_l3_a", "wall"),
        "wall_broken": ("gondor_castle_wall_20m_l3_a_dest", "wall"),
        "gatehouse": ("gondor_castle_gatehouse_l1_a", "gate"),
    }
    rows = []
    for key, (name, fam) in labels.items():
        img = "control/" + key + "/views/3q_front_left.png"
        if not os.path.exists(os.path.join(work, img.replace("/", os.sep))):
            continue
        rows.append({
            "name": name,
            "kit": "Gondor (control piece)",
            "family": fam,
            "size": [],
            "tris": 0,
            "materials": [],
            "uv": 0,
            "img": img,
            "note": "Control piece. Sizes and triangles: control/control_geometry.md",
        })
    return rows


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>Donor pieces: Rivendell siege set</title>
<style>
:root{color-scheme:light dark;--bg:#f4f3ef;--fg:#1c1c1a;--card:#fff;--line:#d5d2c8;--dim:#6a675e;--acc:#34405f}
@media (prefers-color-scheme:dark){:root{--bg:#17181a;--fg:#e9e7e1;--card:#222326;--line:#3a3b3f;--dim:#a09d94;--acc:#9fb0dc}}
*{box-sizing:border-box}body{margin:0;font:14px/1.45 system-ui,Segoe UI,sans-serif;background:var(--bg);color:var(--fg)}
header{position:sticky;top:0;background:var(--bg);border-bottom:1px solid var(--line);padding:12px 16px;z-index:2}
h1{font-size:17px;margin:0 0 8px}
.bar{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
input[type=search]{flex:1 1 220px;padding:7px 10px;border:1px solid var(--line);border-radius:6px;background:var(--card);color:var(--fg);font:inherit}
select{padding:7px 8px;border:1px solid var(--line);border-radius:6px;background:var(--card);color:var(--fg);font:inherit}
.chips{display:flex;flex-wrap:wrap;gap:6px;margin-top:8px}
.chip{border:1px solid var(--line);background:var(--card);color:var(--fg);border-radius:999px;padding:3px 10px;cursor:pointer;font:inherit}
.chip[aria-pressed=true]{background:var(--acc);border-color:var(--acc);color:#fff}
@media (prefers-color-scheme:dark){.chip[aria-pressed=true]{color:#111}}
#count{color:var(--dim);margin-left:auto}
main{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:12px;padding:16px}
.card{background:var(--card);border:1px solid var(--line);border-radius:8px;overflow:hidden;display:flex;flex-direction:column}
.card img{width:100%;aspect-ratio:1;object-fit:contain;background:#e9edf2;display:block}
.card .b{padding:8px 10px}
.card .n{font-weight:600;word-break:break-word}
.card .m{color:var(--dim);font-size:12.5px;font-variant-numeric:tabular-nums}
.tag{display:inline-block;font-size:11.5px;border:1px solid var(--line);border-radius:4px;padding:0 5px;margin-right:4px;color:var(--dim)}
.ctl{outline:2px solid var(--acc)}
</style></head><body>
<header>
<h1>Donor pieces for the Rivendell siege set</h1>
<div class="bar">
<input id="q" type="search" placeholder="Filter by name or material" aria-label="Filter by name or material">
<select id="sort" aria-label="Sort"><option value="name">Sort: name</option><option value="tris">Sort: triangles</option><option value="height">Sort: height</option><option value="width">Sort: width</option></select>
<span id="count"></span>
</div>
<div class="chips" id="chips"></div>
</header>
<main id="grid"></main>
<script>
const ROWS = __ROWS__;
const fams = ["all", ...Array.from(new Set(ROWS.map(r => r.family))).sort()];
let fam = "all";
const chips = document.getElementById("chips"), grid = document.getElementById("grid");
const q = document.getElementById("q"), sort = document.getElementById("sort"), count = document.getElementById("count");
fams.forEach(f => { const b = document.createElement("button"); b.className = "chip"; b.textContent = f + (f === "all" ? "" : " " + ROWS.filter(r => r.family === f).length);
  b.setAttribute("aria-pressed", f === fam); b.onclick = () => { fam = f; [...chips.children].forEach(c => c.setAttribute("aria-pressed", c === b)); draw(); }; chips.appendChild(b); });
function esc(s){ return String(s).replace(/[&<>"]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c])); }
function draw(){
  const t = q.value.trim().toLowerCase();
  let rows = ROWS.filter(r => (fam === "all" || r.family === fam) && (!t || r.name.toLowerCase().includes(t) || r.materials.join(" ").toLowerCase().includes(t)));
  const k = sort.value;
  rows.sort((a, b) => k === "tris" ? b.tris - a.tris : k === "height" ? (b.size[2] || 0) - (a.size[2] || 0) : k === "width" ? (b.size[0] || 0) - (a.size[0] || 0) : a.name.localeCompare(b.name));
  grid.innerHTML = rows.map(r => `<div class="card${r.note ? " ctl" : ""}"><img loading="lazy" src="${esc(r.img)}" alt="${esc(r.name)}"><div class="b">
<div class="n">${esc(r.name)}</div>
<div class="m">${r.size.length ? esc(r.size.join(" x ")) + " m" : ""}${r.tris ? " &middot; " + r.tris.toLocaleString() + " tris" : ""}</div>
<div class="m"><span class="tag">${esc(r.family)}</span><span class="tag">${esc(r.kit)}</span>${r.uv ? `<span class="tag">${r.uv} UV</span>` : ""}</div>
<div class="m">${esc(r.materials.join(", "))}${esc(r.note)}</div></div></div>`).join("");
  count.textContent = rows.length + " of " + ROWS.length + " pieces";
}
q.oninput = draw; sort.onchange = draw; draw();
</script></body></html>
"""


def build(work, out):
    rows = control_rows(work) + kit_rows(work)
    page = PAGE.replace("__ROWS__", json.dumps(rows).replace("</", "<\\/"))
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(page)
    return len(rows)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--work", required=True, help="work folder with kit\\ and control\\")
    parser.add_argument("--out", required=True, help="HTML file to write")
    args = parser.parse_args(argv)
    count = build(args.work, args.out)
    print(f"wrote {args.out} with {count} pieces")
    return 0


if __name__ == "__main__":
    sys.exit(main())
