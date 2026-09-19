#!/usr/bin/env python3
"""Every item id a data generator would write, checked against the items the game loads.

WHY THIS EXISTS

`validate_moduledata.py` checks the XML that ships. It never looks at the Python
that writes XML. So a generator or apply script can sit in tools/ naming items
the Armory retired months ago, and nothing says so until someone re-runs it and
a troop spawns naked (BROKEN_ITEM_REF on the output, if they remember to run the
validator) or a battle hangs on a missing collision body (#352). On 2026-09-13
the two troop-tree scaffolders between them carried 21 ids that had never
existed in this Armory era and 14 more that the 2026-09-01 and 2026-09-11 art
drops retired, and the character-creation, career, wanderer and starter-armour
tables another 20.

WHAT IT CHECKS

Each generator in GENERATORS is read one of two ways:

  run      the script prints its XML to stdout (the troop-tree scaffolders);
           it is executed with no arguments and every `id="Item.X"` is taken.
  tables   the script keeps its picks in module-level dicts, lists, tuples and
           XML template strings; the module is imported (nothing runs, every
           one guards `main()` under `__name__`) and the named tables are
           walked. A string leaf is an item id when it is `Item.X`, when it
           sits inside an `id="Item.X"` attribute, or when it has the shape
           `word_word[_...]`. Dict values under IGNORE_KEYS (folder names,
           culture ids) are skipped, and a `tuple_from` offset skips leading
           labels in tuple rows.

Every collected id is then resolved against the SAME registry the ModuleData
validator uses (`taom_schema.build_registries`): every `<Item>` and
`<CraftedItem>` in the live install's SandBoxCore, SandBox, Native, StoryMode,
CustomBattle, LOTRLOME_Armory, ADOD_Beasts and NavalDLC ModuleData plus this
repo's own. So "exists" means the same thing here as it does for
BROKEN_ITEM_REF, and the live Armory (weapons, armour, shields, everything
under LOTRLOME_items) is the authority, not any snapshot.

Without the install the registry is TAOM-only and every Armory id would read
as missing, so the check is SKIPPED and exits 2, never faked as a pass.

WIRING

  python tools/check_generator_item_refs.py          # exit 1 on any unresolved id
  python tools/validate_moduledata.py                # emits GENERATOR_RETIRED_ITEM_REF (warning)
  python -m unittest tools.tests.test_check_generator_item_refs   # skips without the install

A one-off swap map (apply_dead_mesh_item_swaps.py and friends) names retired
ids on purpose on its FROM side, so those are not generators and are not
listed here. Add a script to GENERATORS when it WRITES item ids into ModuleData.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
REPO_ROOT = TOOLS.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

ITEM_REF_RE = re.compile(r'\bid="Item\.([A-Za-z0-9_.\-]+)"')
ID_SHAPE_RE = re.compile(r"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$")
IGNORE_KEYS = frozenset({"folder", "extra_folders", "culture_id", "culture", "name", "id"})
RUN_TIMEOUT_S = 120


@dataclass(frozen=True)
class GeneratorSpec:
    path: str            # repo-relative, forward slashes
    mode: str            # "run" | "tables"
    tables: tuple = ()   # module-level names walked in tables mode
    tuple_from: int = 0  # tuple rows: skip this many leading label columns


GENERATORS = (
    GeneratorSpec("tools/generate_gondor_troops.py", "run"),
    GeneratorSpec("tools/generate_rhun_troops.py", "run"),
    GeneratorSpec("tools/generate_char_creation_equipment.py", "tables", ("CULTURES",)),
    GeneratorSpec("tools/extract_wanderers.py", "tables", ("KINGDOM_EQUIPMENT",)),
    GeneratorSpec("tools/generate_batch2_wanderers.py", "tables", ("KINGDOM_EQUIPMENT",)),
    GeneratorSpec("tools/generate_starter_armor.py", "tables", ("CULTURES",), tuple_from=2),
)


@dataclass
class Finding:
    generator: str
    unresolved: list     # sorted ids
    total: int           # distinct ids collected

    def as_dict(self) -> dict:
        return {"generator": self.generator, "unresolved": self.unresolved, "total": self.total}


# --------------------------------------------------------------------------- #
# Collection                                                                   #
# --------------------------------------------------------------------------- #
def refs_from_xml(text: str) -> set:
    """Every `id="Item.X"` in a block of XML text, without the prefix."""
    return set(ITEM_REF_RE.findall(text))


def refs_from_tables(obj, tuple_from: int = 0) -> set:
    """Item ids held in a nested table of dicts / lists / tuples / strings.

    A string leaf counts when it is `Item.X`, when it contains `id="Item.X"`
    attributes (an embedded XML template), or when it has the `word_word` id
    shape. Dict values under IGNORE_KEYS are skipped. The first `tuple_from`
    positions of every tuple are skipped (label columns)."""
    out = set()

    def walk(node, in_tuple=False):
        if node is None:
            return
        if isinstance(node, str):
            if 'id="Item.' in node:
                out.update(refs_from_xml(node))
            elif node.startswith("Item."):
                out.add(node[len("Item."):])
            elif ID_SHAPE_RE.match(node):
                out.add(node)
            return
        if isinstance(node, dict):
            for key, value in node.items():
                if isinstance(key, str) and key in IGNORE_KEYS:
                    continue
                walk(value)
            return
        if isinstance(node, tuple):
            for value in node[tuple_from:]:
                walk(value, in_tuple=True)
            return
        if isinstance(node, (list, set, frozenset)):
            for value in node:
                walk(value)

    walk(obj)
    return out


def load_module(path: Path):
    """Import a tools script by path without running it (each guards main())."""
    if str(TOOLS) not in sys.path:
        sys.path.insert(0, str(TOOLS))
    name = "_genrefs_" + path.stem
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def run_generator(path: Path) -> str:
    """Run a stdout-printing generator and return what it printed."""
    proc = subprocess.run([sys.executable, str(path)], cwd=str(REPO_ROOT),
                          capture_output=True, text=True, encoding="utf-8",
                          errors="replace", timeout=RUN_TIMEOUT_S)
    if proc.returncode != 0:
        tail = proc.stderr.strip().splitlines()[-3:]
        raise RuntimeError(f"{path.name} exited {proc.returncode}: " + " | ".join(tail))
    return proc.stdout


def collect(spec: GeneratorSpec, repo_root: Path = REPO_ROOT) -> set:
    path = repo_root / spec.path
    if spec.mode == "run":
        return refs_from_xml(run_generator(path))
    if spec.mode == "tables":
        module = load_module(path)
        refs = set()
        for table in spec.tables:
            if not hasattr(module, table):
                raise AttributeError(f"{spec.path} has no module-level {table}; "
                                     f"the GENERATORS entry is stale")
            refs |= refs_from_tables(getattr(module, table), spec.tuple_from)
        return refs
    raise ValueError(f"unknown mode {spec.mode!r} for {spec.path}")


# --------------------------------------------------------------------------- #
# Checking                                                                     #
# --------------------------------------------------------------------------- #
def check(registry: set, specs=GENERATORS, repo_root: Path = REPO_ROOT) -> list:
    """One Finding per generator that names an id the registry lacks."""
    findings = []
    for spec in specs:
        refs = collect(spec, repo_root)
        missing = sorted(r for r in refs if r not in registry)
        if missing:
            findings.append(Finding(spec.path, missing, len(refs)))
    return findings


def format_report(findings: list, n_generators: int, n_items: int) -> str:
    lines = [f"{n_generators} generator(s) checked against {n_items:,} item ids "
             f"(live install + repo)."]
    if not findings:
        lines.append("PASS: every id a generator would write is defined.")
        return "\n".join(lines)
    for f in findings:
        lines.append(f"\n{f.generator}: {len(f.unresolved)} of {f.total} ids resolve to no item:")
        for iid in f.unresolved:
            lines.append(f"    {iid}")
    lines.append(f"\nFAIL: {sum(len(f.unresolved) for f in findings)} unresolved id(s) in "
                 f"{len(findings)} generator(s). A run would write a reference the engine "
                 f"cannot fill (BROKEN_ITEM_REF; a naked troop, or a hang on a missing body).")
    return "\n".join(lines)


def build_registry(game_modules: Path) -> set:
    """The validator's own item registry, so 'exists' means one thing."""
    if str(TOOLS) not in sys.path:
        sys.path.insert(0, str(TOOLS))
    import taom_schema as ts
    return set(ts.build_registries(MODULEDATA, game_modules).items)


def main() -> int:
    if str(TOOLS) not in sys.path:
        sys.path.insert(0, str(TOOLS))
    from _gamedir import game_modules as resolve_game_modules

    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game-modules", default=str(resolve_game_modules(DEFAULT_GAME)),
                    help="Path to the Bannerlord Modules folder")
    ap.add_argument("--generator", action="append", default=None,
                    help="Only check this generator (repo-relative path; repeatable)")
    ap.add_argument("--list", action="store_true", help="List the registered generators and exit")
    ap.add_argument("--json", dest="json_out", default=None, help="Write findings to this JSON file")
    args = ap.parse_args()

    if args.list:
        for spec in GENERATORS:
            print(f"{spec.path}  [{spec.mode}{' ' + ','.join(spec.tables) if spec.tables else ''}]")
        return 0

    game_modules = Path(args.game_modules)
    if not game_modules.exists():
        print(f"SKIPPED: Bannerlord Modules folder not found: {game_modules}\n"
              f"         The registry would be TAOM-only and every Armory id would read as\n"
              f"         missing. Set $BANNERLORD_GAME_DIR or pass --game-modules. Exit 2,\n"
              f"         not 0: a check that ran nothing must not read as a pass.",
              file=sys.stderr)
        return 2

    specs = GENERATORS
    if args.generator:
        wanted = {g.replace("\\", "/") for g in args.generator}
        specs = tuple(s for s in GENERATORS if s.path in wanted)
        unknown = wanted - {s.path for s in specs}
        if unknown:
            print(f"ERROR: not a registered generator: {', '.join(sorted(unknown))}", file=sys.stderr)
            return 2

    registry = build_registry(game_modules)
    findings = check(registry, specs)
    print(format_report(findings, len(specs), len(registry)))
    if args.json_out:
        Path(args.json_out).write_text(json.dumps([f.as_dict() for f in findings], indent=2),
                                       encoding="utf-8")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
