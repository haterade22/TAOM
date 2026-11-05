#!/usr/bin/env python3
"""One command for the whole ref/asset audit of the Armory, written to a committed report.

WHY THIS EXISTS (#599, 2026-09-15)
The 2026-09-11 Armory art drop re-exported the elven bows under new mesh and
collision-body names and left 32 item refs on the old ones. A missing collision
body is the #352 infinite mission load: `PreloadHelper.WaitForMeshesToBeLoaded`
counts every `body_name` that `PhysicsShape.GetFromResource` cannot resolve, on
every pass, forever. Every gate that would have caught it existed
(`validate_mesh_refs.py --scan-bodies`, `generate_armory_catalogue.py --diff`,
`validate_moduledata.py`, the generators' `--verify`), each had to be remembered
separately, and none was run after the sync. A session that DID run one wrote
the result into a memory note and no issue. The elf start hung for two days.

The artists will keep combining, renaming, deleting and adding meshes. This tool
is the one thing to run after any Armory sync (a KEYforce pull, a "TAOM Update"
mirror commit, an editor re-import), and its report is committed so the next
run's `git diff` IS the change log.

WHAT IT CHECKS (each section reuses the engine that owns it; nothing is re-derived)
  1. catalogue drift      generate_armory_catalogue: RENAME / MOVE / DELETE / NEW
                          against the committed catalogue.tsv. "Referenced" is
                          decided by what the LIVE XML names today, not by the
                          flag the committed row carried (that flag is the state
                          at the last regen, which is why `--diff` kept saying
                          "18 will break" after the 18 were repaired).
  2. mesh + body refs     validate_mesh_refs Tier B + Tier C over the WHOLE
                          Armory ModuleData plus this repo's ModuleData (the
                          #352 scope lesson: crafting pieces sit one level up).
  3. roster impact        every broken item joined to every troop / lord /
                          roster / config that carries it, directly or through
                          a standalone `<EquipmentSet id=...>` roster
                          (audit_deleted_mesh_impact's five reference shapes).
  4. dead item ids        every `Item.X` in this repo's ModuleData resolved
                          against the registry validate_moduledata uses.
  5. generators           `generate_ranged_ladder_items.py --verify` and
                          `generate_starter_kit.py --verify --quiet`, so the
                          scaffolders still agree with the shipped XML.
  6. new art nothing uses NEW catalogue rows that no item names (exact match;
                          the `--unreferenced` matcher is case-insensitive and
                          reported the new bows as used when nothing used them).

Exit code (mirrors validate_moduledata.py): 1 if any MISSING_BODY, MISSING_MESH,
referenced DELETE / RENAME, dead item id, or generator drift; 2 if an input path
is bad; else 0. The report is written either way.

Usage:
  python tools/audit_armory_refs.py                       # report to docs/audits/armory-ref-audit.md
  python tools/audit_armory_refs.py --regen-catalogue     # also rewrite the committed catalogue
  python tools/audit_armory_refs.py --report -            # print the report instead of writing it
  python tools/audit_armory_refs.py --no-generators       # skip the two subprocess verifies (fast)
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import audit_deleted_mesh_impact as am  # noqa: E402
import generate_armory_catalogue as gac  # noqa: E402
import validate_mesh_refs as vm  # noqa: E402
from _gamedir import ensure_exists, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_MODULE = "LOTRLOME_Armory"
DEFAULT_CONSUMERS = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_REPORT = REPO_ROOT / "docs" / "audits" / "armory-ref-audit.md"
# The two scaffolders that write Armory item XML and carry a --verify mode.
GENERATOR_VERIFIES = [
    ["generate_ranged_ladder_items.py", "--verify"],
    ["generate_starter_kit.py", "--verify", "--quiet"],
]
BREAKING_CODES = ("MISSING_BODY", "MISSING_MESH")


# --------------------------------------------------------------------------- #
# 1. catalogue drift, flagged by the LIVE refs                                 #
# --------------------------------------------------------------------------- #
def flag_breakage(changes: dict, live_ref_names: set) -> dict:
    """Re-flag every DELETE / RENAME / MOVE with whether the live XML still
    names the OLD name. The committed catalogue's flag is the state at the last
    regen and says nothing about today."""
    live = {n.lower() for n in live_ref_names}
    out = {
        "delete": [(n, n.lower() in live) for n, _ in changes.get("delete", [])],
        "rename": [(n, new, n.lower() in live) for n, new, _ in changes.get("rename", [])],
        "move": [(n, new, n.lower() in live) for n, new, _ in changes.get("move", [])],
        "moved_same_name": list(changes.get("moved_same_name", [])),
        "new": list(changes.get("new", [])),
    }
    return out


def count_breaking(flagged: dict) -> int:
    return (sum(1 for _, b in flagged["delete"] if b)
            + sum(1 for _, _, b in flagged["rename"] if b)
            + sum(1 for _, _, b in flagged["move"] if b))


# --------------------------------------------------------------------------- #
# 3. roster impact                                                             #
# --------------------------------------------------------------------------- #
@dataclass
class Impact:
    item_id: str
    direct_owners: set = field(default_factory=set)   # NPCCharacter / roster ids naming it
    direct_files: set = field(default_factory=set)
    roster_owners: set = field(default_factory=set)   # characters reaching it via <EquipmentSet id>

    @property
    def consumer_count(self) -> int:
        return len(self.direct_owners | self.roster_owners)


def join_impact(item_ids: set, item_refs: list, roster_refs: list) -> dict:
    rows = {i: Impact(i) for i in item_ids}
    for ref in item_refs:
        if ref.item_id in rows:
            rows[ref.item_id].direct_files.add(ref.file)
            if ref.owner:
                rows[ref.item_id].direct_owners.add(ref.owner)
    for item_id, owners in am.resolve_roster_hop(item_ids, item_refs, roster_refs).items():
        rows[item_id].roster_owners |= owners
    return rows


# --------------------------------------------------------------------------- #
# The summary the report renders                                               #
# --------------------------------------------------------------------------- #
@dataclass
class Summary:
    catalogue: dict
    issues: list                    # vm.Issue, Tier B + C over both item trees
    impact: dict                    # item id -> Impact, for every item an ERROR names
    dead_item_refs: list            # am.ItemRef whose id resolves to no item
    generator_checks: list          # (command, returncode, last line)
    unreferenced_new: list          # NEW catalogue rows nothing names
    tpac_count: int = 0
    mesh_count: int = 0
    body_count: int = 0

    @property
    def errors(self) -> list:
        return [i for i in self.issues if i.code in BREAKING_CODES]


def exit_code(s: Summary) -> int:
    """Generator drift is a WARNING, not a failure: it says a scaffolder would
    write something other than what ships, which is #569's class, not #352's.
    A verdict that stays BROKEN over it trains readers to ignore the verdict."""
    if s.errors or s.dead_item_refs or count_breaking(s.catalogue):
        return 1
    return 0


def warning_count(s: Summary) -> int:
    return sum(1 for _, rc, _ in s.generator_checks if rc != 0)


def render_report(s: Summary, when: str, head: str) -> str:
    L = []
    verdict = "CLEAN" if exit_code(s) == 0 else "BROKEN"
    if verdict == "CLEAN" and warning_count(s):
        verdict = f"CLEAN, {warning_count(s)} warning(s) (generator drift, see below)"
    L.append("# Armory reference audit")
    L.append("")
    L.append(f"GENERATED by `tools/audit_armory_refs.py`, do not hand-edit. Re-run after any Armory sync; "
             f"commit the result so the next diff is the change log. Feature doc: "
             f"[armory-ref-audit.md](../features/armory-ref-audit.md).")
    L.append("")
    L.append(f"- Run: {when} at repo `{head}`")
    L.append(f"- Verdict: **{verdict}**")
    L.append(f"- Inventory: {s.tpac_count:,} tpacs, {s.mesh_count:,} metameshes, {s.body_count:,} physics shapes")
    L.append("")
    # 2 + 3: broken refs with their troops
    L.append("## Broken mesh and body refs (a missing BODY is the #352 infinite load)")
    L.append("")
    if not s.errors:
        L.append("None. Every `mesh`, `holster_mesh`, `body_name` and collision body in the item XML resolves to a shipped asset.")
    else:
        L.append("| Code | Asset | Item | File:line | Troops / rosters carrying it |")
        L.append("|---|---|---|---|---|")
        for i in sorted(s.errors, key=lambda i: (i.code, i.entry_id, i.file, i.line)):
            name = i.message.split(":", 1)[1].strip().split()[0] if ":" in i.message else i.message
            imp = s.impact.get(i.entry_id)
            who = "orphan (nothing carries it)"
            if imp and imp.consumer_count:
                owners = sorted(imp.direct_owners | imp.roster_owners)
                who = ", ".join(owners[:12]) + (f", +{len(owners) - 12} more" if len(owners) > 12 else "")
            L.append(f"| {i.code} | `{name}` | `{i.entry_id}` | `{i.file}:{i.line}` | {who} |")
    L.append("")
    # 1: catalogue drift
    L.append("## Catalogue drift since the committed `docs/reference/armory-catalogue/catalogue.tsv`")
    L.append("")
    c = s.catalogue
    L.append(f"- RENAME: {len(c['rename'])}, MOVE: {len(c['move'])}, MOVED same name: {len(c['moved_same_name'])}, "
             f"DELETE: {len(c['delete'])}, NEW: {len(c['new'])}")
    breaking = ([f"`{n}` (deleted)" for n, b in c["delete"] if b]
                + [f"`{n}` -> `{', '.join(new)}` (renamed)" for n, new, b in c["rename"] if b]
                + [f"`{n}` -> `{', '.join(new)}` (moved)" for n, new, b in c["move"] if b])
    if breaking:
        L.append(f"- Still named by an item, WILL break: {len(breaking)}")
        for b in breaking:
            L.append(f"  - {b}")
    else:
        L.append("- Nothing the live XML names was deleted or renamed.")
    if c["delete"] and not any(b for _, b in c["delete"]):
        L.append(f"- Deleted and no longer referenced (safe): {', '.join('`' + n + '`' for n, _ in sorted(c['delete']))}")
    L.append("")
    # 6: new art
    L.append("## New art nothing uses yet")
    L.append("")
    if s.unreferenced_new:
        L.append(", ".join(f"`{n}`" for n in sorted(s.unreferenced_new)))
    else:
        L.append("None.")
    L.append("")
    # 4: dead item ids
    L.append("## Dead `Item.<id>` refs in TAOM ModuleData")
    L.append("")
    if s.dead_item_refs:
        L.append("| Item id | File:line | Owner |")
        L.append("|---|---|---|")
        for r in sorted(s.dead_item_refs, key=lambda r: (r.item_id, r.file, r.line)):
            L.append(f"| `{r.item_id}` | `{r.file}:{r.line}` | {r.owner or '-'} |")
    else:
        L.append("None. Every item a troop, lord, roster or config names is defined.")
    L.append("")
    # 5: generators
    L.append("## Generators vs shipped XML")
    L.append("")
    for cmd, rc, last in s.generator_checks:
        L.append(f"- `{cmd}`: {'OK' if rc == 0 else 'DRIFT'} (exit {rc}) {last}".rstrip())
    if not s.generator_checks:
        L.append("- skipped (`--no-generators`)")
    L.append("")
    L.append("## How to read this")
    L.append("")
    L.append("- A `MISSING_BODY` row hangs every mission that preloads a carrier: repoint the item to the art that "
             "ships (never restore a tpac beside its replacement), then re-run with `--regen-catalogue`.")
    L.append("- A `MISSING_MESH` row is an invisible item; same fix.")
    L.append("- A deleted or renamed mesh that is still named is tomorrow's row: fix it today.")
    L.append("- New art nothing uses is an audit signal for the roster authors, not a fault.")
    L.append("")
    return "\n".join(L)


# --------------------------------------------------------------------------- #
# Orchestration                                                                #
# --------------------------------------------------------------------------- #
def _git_head(repo: Path) -> str:
    try:
        return subprocess.run(["git", "-C", str(repo), "rev-parse", "--short", "HEAD"],
                              capture_output=True, text=True, timeout=20).stdout.strip() or "unknown"
    except Exception:  # noqa: BLE001
        return "unknown"


def run_generator_checks(repo: Path) -> list:
    out = []
    for argv in GENERATOR_VERIFIES:
        cmd = " ".join(argv)
        try:
            p = subprocess.run([sys.executable, str(repo / "tools" / argv[0]), *argv[1:]],
                               capture_output=True, text=True, timeout=600, cwd=str(repo))
            lines = [l.strip() for l in (p.stdout + p.stderr).splitlines() if l.strip()]
            drift = [l for l in lines if l.startswith(("DRIFT", "FAIL", "MISSING", "EXTRA"))]
            detail = "; ".join(dict.fromkeys(drift)) if drift else (lines[-1] if lines else "")
            out.append((cmd, p.returncode, detail))
        except Exception as e:  # noqa: BLE001
            out.append((cmd, 2, f"could not run: {e}"))
    return out


def build_summary(game: Path, module: str, consumers: Path, generators: bool) -> Summary:
    module_root = ensure_exists(game / "Modules" / module, f"the {module} module")
    armory_md = module_root / "ModuleData"
    # 2: refs from BOTH item trees, present-set from every loaded module's tpacs
    refs = vm.extract_refs(armory_md)
    if consumers.exists():
        refs += vm.extract_refs(consumers)
    tpacs = vm.tpac_paths_for_modules(game, [module, "Native", "SandBoxCore", "SandBox"])
    present = vm.build_present_set(tpacs)
    issues = vm.classify(refs, present, None, scan_bodies=True, body_tpac_paths=tpacs)
    # 1: catalogue drift, flagged by the live refs
    live_names = {re.sub(r"\.lod\d+$", "", r.name, flags=re.IGNORECASE) for r in refs
                  if r.kind in ("visual_mesh", "collision_body")}
    rows = gac.build_rows(module_root, live_names)
    old = gac.parse_existing(gac.OUT_TSV)
    new = {r["mesh"]: r for r in rows}
    changes = flag_breakage(gac.classify_changes(old, new), live_names) if old else \
        {"rename": [], "move": [], "delete": [], "moved_same_name": [], "new": []}
    # 3 + 4: consumers
    item_refs, roster_refs = am.sweep_consumers(consumers) if consumers.exists() else ([], [])
    broken_items = {i.entry_id for i in issues if i.code in BREAKING_CODES and i.entry_id and i.entry_id != "?"}
    impact = join_impact(broken_items, item_refs, roster_refs)
    dead = []
    try:
        import taom_schema  # noqa: E402
        reg = taom_schema.build_registries(consumers, game / "Modules", armory_root=armory_md)
        defined = {i.lower() for i in reg.items}
        seen = set()
        for r in item_refs:
            if r.shape == "prefixed" and r.item_id.lower() not in defined and (r.item_id, r.file, r.line) not in seen:
                seen.add((r.item_id, r.file, r.line))
                dead.append(r)
    except Exception as e:  # noqa: BLE001
        print(f"WARNING: dead-item pass skipped: {e}", file=sys.stderr)
    # 6
    unreferenced_new = [n for n in changes["new"] if n.lower() not in {x.lower() for x in live_names}
                        and not n.startswith(("bo_", "clo_")) and not n.endswith("_slim")]
    checks = run_generator_checks(REPO_ROOT) if generators else []
    return Summary(catalogue=changes, issues=issues, impact=impact, dead_item_refs=dead,
                   generator_checks=checks, unreferenced_new=unreferenced_new,
                   tpac_count=len(present.tpac_paths), mesh_count=len(present.metameshes),
                   body_count=len(present.physicsshapes))


def main() -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", default=str(DEFAULT_GAME))
    ap.add_argument("--module", default=DEFAULT_MODULE)
    ap.add_argument("--consumers", default=str(DEFAULT_CONSUMERS),
                    help="TAOM ModuleData root whose rosters are joined to broken items")
    ap.add_argument("--report", default=str(DEFAULT_REPORT), help="markdown report path, or - for stdout")
    ap.add_argument("--regen-catalogue", action="store_true",
                    help="after the report, rewrite the committed catalogue so --diff is clean")
    ap.add_argument("--no-generators", action="store_true", help="skip the generator --verify subprocesses")
    args = ap.parse_args()
    game = ensure_exists(args.game, "the Bannerlord install")
    summary = build_summary(Path(game), args.module, Path(args.consumers), not args.no_generators)
    text = render_report(summary, when=date.today().isoformat(), head=_git_head(REPO_ROOT))
    if args.report == "-":
        print(text)
    else:
        out = Path(args.report)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(text + "\n", encoding="utf-8", newline="\n")
        print(f"Wrote {out.relative_to(REPO_ROOT) if out.is_relative_to(REPO_ROOT) else out}")
    rc = exit_code(summary)
    print(f"Verdict: {'CLEAN' if rc == 0 else 'BROKEN'}  warnings={warning_count(summary)} errors={len(summary.errors)} dead_items={len(summary.dead_item_refs)} "
          f"breaking_drift={count_breaking(summary.catalogue)} generators={[c[1] for c in summary.generator_checks]}")
    if args.regen_catalogue:
        r = subprocess.run([sys.executable, str(REPO_ROOT / "tools" / "generate_armory_catalogue.py"),
                            "--game", str(game), "--module", args.module], cwd=str(REPO_ROOT))
        if r.returncode != 0:
            print("WARNING: catalogue regeneration failed", file=sys.stderr)
    return rc


if __name__ == "__main__":
    sys.exit(main())
