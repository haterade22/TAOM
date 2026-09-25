#!/usr/bin/env python3
"""Bring every used Armoury mesh to LOD0 through LOD5, one FBX at a time, verified before install.

WHY THIS EXISTS
The rule (Mike, 2026-09-25): every mesh carries LOD0 to LOD5, with no gaps; `clo_` cloth needs
none. `tools/audit_fbx_lods.py` lists what falls short (docs/reference/armory-catalogue/
lod-audit.md): on 2026-09-25, 888 used chains in 104 FBX, most of them weapons and armour whose
chains skip a level (`0,2,4,5`) or stop early (`0..3`). A missing level is drawn as the level
before it, so a gap costs as much as no LOD at all at that range.

WHAT IT DOES, PER FBX
1. Backs the live source up once (the first original is kept) to
   `<work>/originals/AssetSources/<rel>`, and, when the FBX's own tpac holds a Skeleton, the tpac
   too (`<work>/originals/Assets/<rel>`): a Kit re-import regenerates that package, and the
   elephant's and chariot's hit capsules (#624) were patched straight into it.
2. Plans, in plain Python from the FBX itself, every missing level 1 to 5 of every chain that
   ships from this FBX's tpac and that an item or a skin uses (`plan_for`). A gap is filled
   geometrically between its two neighbours; below the last level the standard step continues
   (70/30/15/7/3% of LOD0, the elf and dwarf basemeshes' own chain). Existing levels are never
   touched, and a level past 5 is left alone. Seams are locked (`--lock-boundary`) on the body
   parts a race skin stitches (files under `Race Test/`), never on hair cards.
3. Runs `tools/blender/add_mesh_lods.py --plan` on a work copy, one launch at a time (the Store
   launcher drops launches fired back to back), and polls its report until `"stage": "done"`.
4. Verifies without Blender: `audit_fbx_lods.diff_meshes` may show only the planned new objects,
   the plan's fixes, the harmless relabelling of morph channels, and existing levels losing a
   few degenerate triangles (Blender's importer drops a face that uses a vertex twice: the Dale
   chests lost 1 to 14). `bind_pose_drift` must be round-off (axes 1e-3, offsets 1e-4 of the rig;
   the elephant's round trip shows 1.5e-5). Anything else refuses the install.
   Naming defects are fixed first when `tools/lod_defect_fixes.json` (`--fixes`) names the FBX:
   hand-decided renames (a `.lod` with no number into its chain's gap, a stray part to a part
   name) and deletes (a copy of LOD0 drawn on top of it).
5. With `--apply`, copies the verified file over the live source and checks its hash.
Every file gets a line in `<work>/ledger.jsonl`; the run ends with a summary.

After it: a Modding Kit re-import of every installed FBX (materials rebind by FBX name, so the
names are part of the check), then `python tools/audit_fbx_lods.py` and `--check`. For a file
whose tpac was backed up for its Skeleton, compare the skeleton after the re-import
(`tools/tpac_skeleton_dump.py`) and restore it with `tools/tpac_skeleton_swap.py` if it changed.

Usage:
  python tools/lod_fill_batch.py --all                      # plan and stage every file, install nothing
  python tools/lod_fill_batch.py --all --apply              # ... and install what verifies
  python tools/lod_fill_batch.py --fbx "dale_kingdom/sr_dale_kingdom_boots.fbx" --apply
  python tools/lod_fill_batch.py --all --only weapons --limit 5
"""
from __future__ import annotations

import argparse
import ast
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import audit_fbx_lods as afl  # noqa: E402
from _gamedir import ensure_exists  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
BLENDER_SCRIPT = REPO / "tools" / "blender" / "add_mesh_lods.py"
LAUNCHER = Path(os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe"))
DEFAULT_WORK = Path(r"E:\Bannerlord_Backups\lod_fill")
STEP = {0: 1.0, 1: 0.70, 2: 0.30, 3: 0.15, 4: 0.07, 5: 0.03}
MIN_TRIS = 4
BODY_ATTRS = {"body_meta_mesh", "body_meta_mesh_shoulders", "body_meta_mesh_upperbody",
              "face_meta_mesh", "hands_mesh", "legs_mesh", "underwear_bottom_mesh",
              "underwear_top_mesh"}
RELABEL = "channel labels renamed, shape geometry and order unchanged"


# --------------------------------------------------------------------------- #
# planning (pure)
# --------------------------------------------------------------------------- #
def fill_targets(tris: dict) -> dict:
    """Triangle targets for every missing level 1 to 5, given {level: tris} with LOD0 in it."""
    known = {lv: float(n) for lv, n in tris.items()}
    out = {}
    for level in range(1, 6):
        if level in known:
            continue
        prev = max(lv for lv in known if lv < level)
        later = [lv for lv in tris if lv > level]
        if later:
            nxt = min(later)
            t = known[prev] * (tris[nxt] / known[prev]) ** ((level - prev) / (nxt - prev))
        else:
            t = known[prev] * STEP[level] / STEP[prev]
        t = max(float(MIN_TRIS), t)
        known[level] = t
        out[level] = max(MIN_TRIS, int(round(t)))
    return out


def skins_body_meshes(path) -> set:
    """Lowercased meshes a skin stitches into a body: every body-part attribute, not hair."""
    out = set()
    for el in ET.parse(path).getroot().iter("skin"):
        for key in BODY_ATTRS:
            if el.get(key):
                out.add(el.get(key).lower())
    return out


def load_fixes(path) -> dict:
    """rel -> {"delete": [...], "renames": {old: new}} from tools/lod_defect_fixes.json."""
    if not path or not Path(path).exists():
        return {}
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    return {k: v for k, v in data.items() if not k.startswith("_")}


def apply_fixes(meshes, fix: dict):
    """The mesh list as it will be after `fix`, and the part of `fix` that applies. Deleting a
    name that is not there is dropped (a generated level the fill batch never made); renaming
    one is refused (the decision was made on a file that has changed)."""
    names = {m.name for m in meshes}
    delete = [n for n in fix.get("delete", []) if n in names]
    renames = dict(fix.get("renames", {}))
    missing = [old for old in renames if old not in names]
    if missing:
        raise SystemExit("rename of objects not in the file: %s" % ", ".join(missing))
    out = []
    for m in meshes:
        if m.name in delete:
            continue
        if m.name in renames:
            m = afl.FbxMesh(renames[m.name], m.tris, m.vertices, m.channels, m.materials, m.shapes)
        out.append(m)
    return out, {"delete": delete, "renames": renames}


def expected_additions(plan: dict, before_names: set) -> int:
    """`+` lines a correct run shows: every planned level, and every rename onto a new name."""
    levels = sum(len(m["levels"]) for m in plan["meshes"])
    deleted = set(plan.get("delete", []))
    fresh = [t for t in plan.get("renames", {}).values() if t not in before_names or t in deleted]
    replaced = [t for t in fresh if t in before_names]      # shows as `~`, not `+`
    return levels + len(fresh) - len(replaced)


def plan_for(rel: str, meshes, catalogue: dict, skins: set, body: set, fix: dict = None,
             materials: dict = None) -> dict:
    """The `add_mesh_lods.py --plan` for one FBX: its naming fixes first, then every used chain
    this FBX's tpac ships that lacks any of LOD1 to LOD5, with its missing levels' targets."""
    applied = {"delete": [], "renames": {}}
    if fix:
        meshes, applied = apply_fixes(meshes, fix)
    chains = afl.build_chains(rel, meshes)
    afl.mark_numbered_parts(chains)
    race_file = rel.replace("\\", "/").startswith("Race Test/")
    plan = []
    for c in chains:
        if afl.is_cloth(c.chain) or c.defect or 0 not in c.levels or not c.missing:
            continue
        row = catalogue.get(c.metamesh)
        if not row or afl.tpac_for(rel) not in row["tpacs"]:
            continue
        if row["referenced"] not in ("Y", "SLIM") and c.metamesh not in skins:
            continue
        levels = fill_targets(c.tris)
        if levels:
            plan.append({"base": c.chain, "levels": {str(k): v for k, v in sorted(levels.items())},
                         "lock": race_file and c.metamesh in body})
    out = {"fbx": rel, "meshes": plan}
    if materials:
        remap = expand_materials(meshes, materials)
        if remap:
            out["materials"] = remap
    if applied["delete"] or applied["renames"]:
        out.update(applied)
    return out


_DROP_RE = re.compile(r"^~ [^:]+: tris (\d+) -> (\d+)$")


def _is_degenerate_drop(line: str) -> bool:
    """`~ X: tris A -> B` and nothing else, B a little under A: Blender's FBX importer validates
    every mesh and drops degenerate triangles (a vertex used twice), which draw nothing. The
    vertex count is unchanged, or the diff line would name it too."""
    m = _DROP_RE.match(line)
    if not m:
        return False
    a, b = int(m.group(1)), int(m.group(2))
    return b < a and a - b <= max(2, 0.05 * a)


def degenerate_drops(diff_lines) -> int:
    return sum(1 for line in diff_lines if _is_degenerate_drop(line))


def verify(diff_lines, plan: dict) -> list:
    """Problems in a diff that should hold only the planned new levels and the plan's fixes."""
    wanted = {(m["base"].lower(), int(lv)) for m in plan["meshes"] for lv in m["levels"]}
    targets = set(plan.get("renames", {}).values())
    gone = set(plan.get("delete", [])) | set(plan.get("renames", {}))
    problems = []
    for line in diff_lines:
        kind, _, rest = line.partition(" ")
        name = rest.split(":", 1)[0]
        if kind == "+":
            if name in targets:
                continue
            chain, level, defect = afl.split_lod(name)
            if defect or (chain.lower(), level) not in wanted:
                problems.append("unplanned object " + line)
        elif kind == "-" and name in gone:
            continue
        elif kind == "~" and (name in targets or _change_ok(name, rest.split(": ", 1)[1], plan)):
            continue
        else:
            problems.append(line)
    return problems


def _change_ok(name: str, changes: str, plan: dict) -> bool:
    """Every part of a `~` line is harmless or planned: the morph relabel, a degenerate-face
    drop, or the plan's own material change (compared as sets: Blender's export folds two slots
    that name the same material into one)."""
    for part in changes.split("; "):
        if part == RELABEL or _is_degenerate_drop("~ %s: %s" % (name, part)):
            continue
        m = re.match(r"materials (\[.*\]) -> (\[.*\])$", part)
        want = plan.get("materials", {}).get(name)
        if m and want is not None and set(ast.literal_eval(m.group(2))) == set(want):
            continue
        return False
    return True


def expand_materials(meshes, entry: dict) -> dict:
    """object -> its new slot names, from a tools/lod_material_fixes.json entry: `chains` (a
    chain's rule, `*` for every slot) beats `slots` (a file-wide rename). Only objects whose
    slots change are listed; `bo_` collision bodies keep their physics materials."""
    chains = {k.lower(): v for k, v in entry.get("chains", {}).items()}
    slots = entry.get("slots", {})
    out = {}
    for m in meshes:
        if m.name.lower().startswith("bo_"):
            continue
        chain = afl.split_lod(m.name)[0].lower()
        rule = chains.get(chain)
        new = []
        for s in m.materials:
            if rule is not None:
                new.append(rule.get("*", rule.get(s, s)))
            else:
                new.append(slots.get(s, s))
        if new != list(m.materials):
            out[m.name] = new
    return out


def bind_ok(drift) -> bool:
    """Round-off only: axes within 1e-3, offsets within 1e-4 of the rig (0.4 mm on the 4 m
    elephant, whose round trip shows 1.5e-5; a moved bone is millimetres)."""
    bones, axis, offset, extent = drift
    if not bones:
        return True
    return axis <= 1e-3 and offset <= max(1e-4 * extent, 1e-6)


# --------------------------------------------------------------------------- #
# running
# --------------------------------------------------------------------------- #
def md5(path) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def has_skeleton(tpac: Path) -> bool:
    if not tpac.exists():
        return False
    out = subprocess.run([sys.executable, str(REPO / "tools" / "tpac_skeleton_scan.py"), str(tpac)],
                         capture_output=True, text=True).stdout
    return bool(re.search(r"type=Skeleton\s", out))


def backup_once(src: Path, dst: Path) -> None:
    if dst.exists():
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    if md5(src) != md5(dst):
        raise SystemExit("backup of %s does not match its source" % src)


def run_blender(work_fbx: Path, plan_path: Path, timeout: int) -> dict:
    report = Path(str(work_fbx) + ".lods-report.json")
    if report.exists():
        report.unlink()
    subprocess.run([str(LAUNCHER), "-b", "--factory-startup", "-P", BLENDER_SCRIPT.as_posix(), "--",
                    "--fbx", work_fbx.as_posix(), "--plan", plan_path.as_posix()], check=False)
    deadline = time.time() + timeout
    while time.time() < deadline:
        time.sleep(3)
        try:
            data = json.loads(report.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if data.get("stage") == "done":
            return data
    last = report.read_text(encoding="utf-8") if report.exists() else "{}"
    return {"stage": json.loads(last or "{}").get("stage", "never started"), "ok": False,
            "error": "timed out after %d s" % timeout}


def targets_from_audit(sources: Path, catalogue, skins, body, fixes=None, materials=None) -> list:
    out = []
    for f in sorted(p for p in sources.rglob("*") if p.suffix.lower() == ".fbx"):
        rel = f.relative_to(sources).as_posix()
        if rel in (fixes or {}) or rel in (materials or {}) or plan_for(rel, afl.read_fbx_meshes(f), catalogue, skins, body)["meshes"]:
            out.append(rel)
    return out


def process(rel, sources, assets, work, catalogue, skins, body, apply, timeout, fix=None,
            materials=None) -> dict:
    live = sources / rel
    entry = {"fbx": rel, "installed": False}
    backup_once(live, work / "originals" / "AssetSources" / rel)
    tpac = assets / afl.tpac_for(rel)
    if has_skeleton(tpac):
        backup_once(tpac, work / "originals" / "Assets" / afl.tpac_for(rel))
        entry["skeleton_tpac_backed_up"] = afl.tpac_for(rel)
    stage = work / "stage"
    stage.mkdir(parents=True, exist_ok=True)
    key = rel.replace("/", "__")
    pre = stage / key
    shutil.copy2(live, pre)
    before = afl.read_fbx_meshes(pre)
    plan = plan_for(rel, before, catalogue, skins, body, fix=fix, materials=materials)
    entry["chains"] = len(plan["meshes"])
    entry["levels"] = sum(len(m["levels"]) for m in plan["meshes"])
    entry["fixes"] = {k: plan[k] for k in ("delete", "renames") if plan.get(k)}
    entry["materials"] = len(plan.get("materials", {}))
    if not plan["meshes"] and not entry["fixes"] and not entry["materials"]:
        entry["result"] = "nothing to do"
        return entry
    plan_path = stage / (key + ".plan.json")
    plan_path.write_text(json.dumps(plan, indent=1), encoding="utf-8")
    t0 = time.time()
    report = run_blender(pre, plan_path, timeout)
    entry["seconds"] = round(time.time() - t0)
    entry["report_ok"] = bool(report.get("ok"))
    if not report.get("ok"):
        entry["result"] = "blender refused: %s" % (report.get("error") or report.get("diffs")
                                                   or report.get("gates") or report.get("stage"))
        return entry
    staged = Path(report["staged"])
    lines = afl.diff_meshes(before, afl.read_fbx_meshes(staged))
    problems = verify(lines, plan)
    drift = afl.bind_pose_drift(pre, staged)
    entry["bind"] = [drift[0], drift[1], drift[2], drift[3]]
    if not bind_ok(drift):
        problems.append("bind pose drift %r" % (drift,))
    added = sum(1 for line in lines if line.startswith("+"))
    entry["added_objects"] = added
    entry["degenerate_drops"] = degenerate_drops(lines)
    if problems:
        entry["result"] = "verify failed"
        entry["problems"] = problems[:10]
        return entry
    want = expected_additions(plan, {m.name for m in before})
    if added != want:
        entry["result"] = "verify failed"
        entry["problems"] = ["expected %d new objects, the diff adds %d" % (want, added)]
        return entry
    entry["result"] = "verified"
    if apply:
        shutil.copy2(staged, live)
        if md5(staged) != md5(live):
            entry["result"] = "INSTALL MISMATCH"
            return entry
        entry["installed"] = True
        entry["result"] = "installed"
    return entry


def main() -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", default=str(afl.DEFAULT_GAME))
    ap.add_argument("--module", default=afl.DEFAULT_MODULE)
    ap.add_argument("--work", default=str(DEFAULT_WORK))
    ap.add_argument("--all", action="store_true", help="every FBX the audit says falls short")
    ap.add_argument("--fbx", action="append", default=[], help="an AssetSources-relative FBX (repeatable)")
    ap.add_argument("--only", default=None, help="substring filter on the relative path")
    ap.add_argument("--limit", type=int, default=None)
    ap.add_argument("--timeout", type=int, default=1800, help="seconds per Blender run")
    ap.add_argument("--apply", action="store_true", help="install what verifies")
    ap.add_argument("--fixes", default=str(REPO / "tools" / "lod_defect_fixes.json"),
                    help="hand-decided naming fixes applied before the fill (renames, deletes)")
    ap.add_argument("--materials", default=str(REPO / "tools" / "lod_material_fixes.json"),
                    help="hand-decided FBX material slot fixes (Kit material names)")
    args = ap.parse_args()
    if not (args.all or args.fbx):
        ap.error("give --all or --fbx")

    game = ensure_exists(args.game, "the Bannerlord install")
    module_root = ensure_exists(game / "Modules" / args.module, f"the {args.module} module")
    sources, assets = module_root / "AssetSources", module_root / "Assets"
    ensure_exists(LAUNCHER, "the Blender launcher")
    work = Path(args.work)
    work.mkdir(parents=True, exist_ok=True)
    catalogue = afl.live_catalogue(module_root)
    skins_path = module_root / "ModuleData" / "skins.xml"
    skins, body = afl.skins_mesh_refs(skins_path), skins_body_meshes(skins_path)

    fixes = load_fixes(args.fixes)
    mats = load_fixes(args.materials)
    todo = args.fbx or targets_from_audit(sources, catalogue, skins, body, fixes, mats)
    if args.only:
        todo = [r for r in todo if args.only.lower() in r.lower()]
    if args.limit:
        todo = todo[:args.limit]
    print(f"{len(todo)} FBX to process ({'install' if args.apply else 'stage only'}); work {work}")
    ledger = work / "ledger.jsonl"
    results = []
    for n, rel in enumerate(todo, 1):
        try:
            entry = process(rel, sources, assets, work, catalogue, skins, body, args.apply, args.timeout,
                            fixes.get(rel), mats.get(rel))
        except Exception as exc:  # noqa: BLE001 - one bad file must not stop the batch
            entry = {"fbx": rel, "installed": False, "result": "error: %s: %s" % (type(exc).__name__, exc)}
        entry["when"] = time.strftime("%Y-%m-%d %H:%M:%S")
        with open(ledger, "a", encoding="utf-8") as f:
            f.write(json.dumps(entry) + "\n")
        results.append(entry)
        print(f"[{n}/{len(todo)}] {entry['result']:<18} {rel}  "
              f"({entry.get('chains', 0)} chains, {entry.get('levels', 0)} levels, {entry.get('seconds', 0)} s)")
        for p in entry.get("problems", []):
            print("      " + p)
    done = sum(1 for e in results if e["installed"])
    print(f"\n{done} installed, {sum(1 for e in results if e['result'] == 'verified')} verified not installed, "
          f"{sum(1 for e in results if e['result'] not in ('installed', 'verified', 'nothing to do'))} failed")
    return 0 if all(e["result"] in ("installed", "verified", "nothing to do") for e in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
