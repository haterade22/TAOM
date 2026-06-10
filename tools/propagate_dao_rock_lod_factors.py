#!/usr/bin/env python3
r"""Propagate each dao_rock placement's LOD0 (base mesh) ``factor`` down to explicit
LOD1..LOD5 ``<mesh>`` rows in TAOM_Map's ``Main_map/scene.xscene``.

Why this exists
---------------
In ``scene.xscene`` every placed ``dao_rock_*`` entity stores a packed uint32 ARGB
colour tint as ``factor`` on its ``<mesh>`` rows. The map maker sets the **LOD0 (base)**
mesh ``factor`` to the colour he wants the rock to be. But a *single* base ``<mesh>``
row's factor does **not** get applied to the engine-generated LOD sub-meshes at render
time -- so when the camera zooms out and the engine swaps to LOD1..LOD5 the rock loses
its tint and visibly changes colour. Writing an explicit per-LOD ``<mesh>`` row (carrying
the same factor as LOD0) makes every LOD render the intended colour -- exactly what the
in-editor "re-set the factor on each LOD tab" workaround does, but automated.

Each rock keeps its **own** colour: we only copy each entity's own base factor down to
that entity's own LOD rows. We never unify colours across rocks.

Key correctness rule
--------------------
LOD row names derive from the **mesh** name (the ``<meta_mesh_component name=...>`` /
base ``<mesh name=...>``), NOT the ``<game_entity>`` name. e.g. entity
``dao_rock_3_mordor_test`` uses mesh ``dao_rock_3`` -> its LOD rows are
``dao_rock_3.lod1`` .. ``dao_rock_3.lod5``. Naming them after the entity would not bind
to any sub-mesh and would render blank.

Behaviour
---------
* Idempotent: re-running changes nothing once every dao_rock has matching LOD rows.
* Formatting-preserving: new rows copy the base row's exact indentation + line ending;
  existing rows are left byte-for-byte unless their factor genuinely differs from LOD0.
* ``--dry-run`` (default) prints a summary + a sample before/after. ``--apply`` writes,
  creating a one-time ``scene.xscene.bak`` of the pristine original first.

IMPORTANT: close the Bannerlord editor before ``--apply`` -- the editor rewrites
``scene.xscene`` on its own save and will clobber an external edit made while it is open.
"""
from __future__ import annotations

import argparse
import re
import shutil
import sys
from pathlib import Path

DEFAULT_SCENE = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\TAOM_Map\SceneObj\Main_map\scene.xscene"
)

# A dao_rock base mesh is exactly dao_rock_<n> (no .lod suffix).
BASE_NAME_RE = re.compile(r"^dao_rock_\d+$")
LOD_NAME_RE = re.compile(r"^(.*)\.lod(\d+)$")
NAME_RE = re.compile(r'name="([^"]*)"')
FACTOR_RE = re.compile(r'factor="([^"]*)"')
MESH_RE = re.compile(r"<mesh\b")
MMC_OPEN_RE = re.compile(r"<meta_mesh_component\b")
MMC_CLOSE_RE = re.compile(r"</meta_mesh_component>")
# A self-closing component (<meta_mesh_component name="X"/>) has no mesh rows and no
# factor to propagate. It must NOT be treated as a block-opener -- otherwise the parser
# scans forward to the next </meta_mesh_component> elsewhere and swallows unrelated XML.
MMC_SELF_CLOSE_RE = re.compile(r"<meta_mesh_component\b[^>]*/>")
EOL_RE = re.compile(r"(\r\n|\r|\n)$")

DEFAULT_LODS = (1, 2, 3, 4, 5)


def build_observed_lod_map(lines):
    """First pass: mesh-type -> sorted set of LOD indices seen on already-exploded
    instances anywhere in the scene. Lets us reproduce the asset's real LOD count
    for types that already have an explicit example, instead of guessing."""
    observed = {}
    for ln in lines:
        if not MESH_RE.search(ln):
            continue
        nm = NAME_RE.search(ln)
        if not nm:
            continue
        m = LOD_NAME_RE.match(nm.group(1))
        if not m:
            continue
        base, idx = m.group(1), int(m.group(2))
        if BASE_NAME_RE.match(base):
            observed.setdefault(base, set()).add(idx)
    return observed


def target_lods(base_name, observed):
    """LOD index set to ensure for a given mesh type. Prefer the indices actually
    observed on an exploded instance of the same type; else fall back to 1..5."""
    if base_name in observed and observed[base_name]:
        return sorted(observed[base_name]), False  # data-driven, not a fallback
    return list(DEFAULT_LODS), True  # fallback default


def process(lines):
    observed = build_observed_lod_map(lines)
    out = []
    i, n = 0, len(lines)
    stats = {
        "entities_touched": 0,
        "rows_added": 0,
        "rows_rewritten": 0,
        "per_type": {},          # base_name -> [entities_touched, rows_added]
        "fallback_types": set(),  # types that used the 1..5 default
        "sample": None,          # (entity-ish name, [before lines], [after lines])
    }
    # Track the nearest enclosing game_entity name purely for the sample/report.
    cur_entity = None
    entity_name_re = re.compile(r'<game_entity\b[^>]*\bname="([^"]*)"')

    while i < n:
        line = lines[i]
        em = entity_name_re.search(line)
        if em:
            cur_entity = em.group(1)

        if (
            MMC_OPEN_RE.search(line)
            and not MMC_CLOSE_RE.search(line)
            and not MMC_SELF_CLOSE_RE.search(line)
        ):
            out.append(line)
            # collect the block body up to (not including) the close tag
            j = i + 1
            body = []
            while j < n and not MMC_CLOSE_RE.search(lines[j]):
                body.append(lines[j])
                j += 1
            close_line = lines[j] if j < n else None

            # parse mesh rows
            base = None          # (name, factor)
            base_line = None     # the raw base <mesh> line (for indent + eol)
            lod_factors = {}     # idx -> factor
            for bl in body:
                if not MESH_RE.search(bl):
                    continue
                nm = NAME_RE.search(bl)
                if not nm:
                    continue
                name = nm.group(1)
                fm = FACTOR_RE.search(bl)
                factor = fm.group(1) if fm else None
                lm = LOD_NAME_RE.match(name)
                if lm:
                    lod_factors[int(lm.group(2))] = factor
                else:
                    base = (name, factor)
                    base_line = bl

            is_dao = bool(
                base and base[1] is not None and BASE_NAME_RE.match(base[0])
            )

            before_snapshot = list(body) if is_dao else None

            # emit body, rewriting an existing LOD row only if its factor != LOD0
            for bl in body:
                if is_dao and MESH_RE.search(bl):
                    nm = NAME_RE.search(bl)
                    fm = FACTOR_RE.search(bl)
                    if nm and fm and LOD_NAME_RE.match(nm.group(1)):
                        if fm.group(1) != base[1]:
                            out.append(FACTOR_RE.sub(f'factor="{base[1]}"', bl, count=1))
                            stats["rows_rewritten"] += 1
                            continue
                out.append(bl)

            added = 0
            if is_dao:
                base_name, base_factor = base
                indent = re.match(r"[ \t]*", base_line).group(0)
                eolm = EOL_RE.search(base_line)
                eol = eolm.group(1) if eolm else "\n"
                wanted, used_fallback = target_lods(base_name, observed)
                if used_fallback:
                    stats["fallback_types"].add(base_name)
                for k in wanted:
                    if k not in lod_factors:
                        out.append(
                            f'{indent}<mesh name="{base_name}.lod{k}" '
                            f'factor="{base_factor}"/>{eol}'
                        )
                        added += 1
                if added:
                    stats["entities_touched"] += 1
                    stats["rows_added"] += added
                    pt = stats["per_type"].setdefault(base_name, [0, 0])
                    pt[0] += 1
                    pt[1] += added
                    if stats["sample"] is None:
                        after = list(body) + [
                            f'{indent}<mesh name="{base_name}.lod{k}" '
                            f'factor="{base_factor}"/>{eol}'
                            for k in wanted if k not in lod_factors
                        ]
                        stats["sample"] = (cur_entity or base_name, before_snapshot, after)

            if close_line is not None:
                out.append(close_line)
            i = j + 1
            continue

        out.append(line)
        i += 1

    return out, stats


def read_lines(path: Path):
    # newline="" preserves the original line endings inside the kept-ends list.
    text = path.read_text(encoding="utf-8", newline="")
    return text.splitlines(keepends=True)


def print_report(stats, applied: bool):
    print("=" * 70)
    print("dao_rock LOD factor propagation " + ("(APPLIED)" if applied else "(DRY-RUN)"))
    print("=" * 70)
    print(f"  entities needing LOD rows : {stats['entities_touched']}")
    print(f"  LOD rows added            : {stats['rows_added']}")
    print(f"  existing LOD rows rewritten: {stats['rows_rewritten']}")
    if stats["per_type"]:
        print("  per mesh type (entities / rows added):")
        for t in sorted(stats["per_type"]):
            e, r = stats["per_type"][t]
            print(f"      {t:<16} {e:>4} / {r:>4}")
    if stats["fallback_types"]:
        print("  NOTE: these types had no pre-exploded example; defaulted to lod1..5")
        print("        (spot-check one instance in-editor that it has exactly 5 LODs):")
        for t in sorted(stats["fallback_types"]):
            print(f"      {t}")
    if stats["sample"]:
        name, before, after = stats["sample"]
        print("-" * 70)
        print(f"  sample entity: {name}")
        print("  BEFORE:")
        for l in before:
            print("    " + l.rstrip("\r\n"))
        print("  AFTER:")
        for l in after:
            print("    " + l.rstrip("\r\n"))
    print("=" * 70)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--scene", default=DEFAULT_SCENE, help="path to scene.xscene")
    ap.add_argument("--apply", action="store_true", help="write changes (default is dry-run)")
    args = ap.parse_args(argv)

    path = Path(args.scene)
    if not path.is_file():
        print(f"ERROR: scene file not found: {path}", file=sys.stderr)
        return 2

    lines = read_lines(path)
    out, stats = process(lines)
    print_report(stats, applied=False)

    if not args.apply:
        print("\nDry-run only. Re-run with --apply (editor CLOSED) to write.")
        return 0

    if stats["rows_added"] == 0 and stats["rows_rewritten"] == 0:
        print("\nNothing to change -- scene already consistent. No write performed.")
        return 0

    bak = path.with_suffix(path.suffix + ".bak")
    if not bak.exists():
        shutil.copy2(path, bak)
        print(f"\nBacked up pristine original -> {bak}")
    else:
        print(f"\nBackup already exists, leaving it intact -> {bak}")

    path.write_text("".join(out), encoding="utf-8", newline="")
    print(f"Wrote {path}")
    print_report(stats, applied=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
