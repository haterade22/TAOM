#!/usr/bin/env python3
"""
Audit usable siege resupply props (throwable rock piles, arrow/javelin barrels) per
town and castle.

Motivation: a player reported being unable to interact with rock piles and arrow
baskets in TAOM sieges. The engine fails this case SILENTLY -- if a prop carries no
script, or its ammo-pickup standing points are all disabled, the crosshair either
ignores it or focuses the machine root, whose GetDescriptionText returns null. The
player sees a prop with no prompt and a dead interact key, and the AI never uses it
either. Nothing is logged.

A prop is only usable if it carries the engine script (StonePile / ArrowBarrel /
JavelinBarrel). A scene can hold dozens of pile MESHES that look identical in-game
and do nothing. This tool separates the two.

Counts entities, not string matches: a scene entity can both reference a prefab AND
re-declare the script inline to override a variable, so counting the two forms
separately double-counts (that mistake produced a wrong count during the original
investigation).

Outputs:
  1. Per-settlement table: usable rock piles / ammo barrels, and decorative pile meshes.
  2. Settlements with ZERO usable rock piles (the reported symptom).
  3. Settlements whose scene shows decorative piles but no usable ones ("looks usable,
     isn't" -- the worst case for a player).
  4. Dead GivenItemID references: an id the item registry does not define makes the
     pile permanently unusable for player AND AI (StonePile passes the null item into
     InitGivenWeapon, and StandingPointWithWeaponRequirement.IsDisabledForAgent then
     falls through to `return true`). Vanilla ships one: stone_pile_l_usable ->
     "boulder_carry", which is defined nowhere in the install.

Usage:
  python tools/audit_siege_props.py                  # towns + castles
  python tools/audit_siege_props.py --all            # include villages
  python tools/audit_siege_props.py --scene NAME     # one scene, verbose breakdown
  python tools/audit_siege_props.py --game <path>    # override the install path
"""
from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

DEFAULT_GAME = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")

# ScriptComponentBehaviour class names that make a prop actually usable. These are
# bound by literal name in the scene/prefab XML, so the string IS the contract.
ROCK_SCRIPTS = {"StonePile", "SiegeMachineStonePile"}
BARREL_SCRIPTS = {"ArrowBarrel", "JavelinBarrel"}
USABLE_SCRIPTS = ROCK_SCRIPTS | BARREL_SCRIPTS

# Editor-only, debris and generic helper meshes carried by usable prefabs. Excluded
# when deriving the "looks like a usable pile" mesh set -- otherwise every entity with
# an icon_man or a destroyed-siege-engine chunk would count as a lookalike.
HELPER_MESH_RE = re.compile(
    r"icon_man|volume_box|dirty_|editor_|barrier_|_ghost|destroyed|spawner|projectile_",
    re.IGNORECASE,
)

AMMO_TAG = "ammopickup"


# --------------------------------------------------------------------------- model


class PropInfo:
    """What a single scene entity (or prefab definition) contributes."""

    __slots__ = ("scripts", "given_items", "has_ammo_point", "meshes")

    def __init__(self) -> None:
        self.scripts: set[str] = set()
        self.given_items: set[str] = set()
        self.has_ammo_point: bool = False
        self.meshes: set[str] = set()

    def merge(self, other: "PropInfo") -> None:
        self.scripts |= other.scripts
        self.given_items |= other.given_items
        self.has_ammo_point = self.has_ammo_point or other.has_ammo_point
        self.meshes |= other.meshes

    def __bool__(self) -> bool:
        return bool(self.scripts)


# ----------------------------------------------------------------------- xml walking


def _own_scripts(entity: ET.Element) -> tuple[set[str], set[str]]:
    """(script names, GivenItemID values) declared directly on this entity."""
    names: set[str] = set()
    given: set[str] = set()
    scripts = entity.find("scripts")
    if scripts is None:
        return names, given
    for script in scripts.findall("script"):
        name = script.get("name")
        if not name:
            continue
        names.add(name)
        variables = script.find("variables")
        if variables is None:
            continue
        for var in variables.findall("variable"):
            if var.get("name") == "GivenItemID":
                value = (var.get("value") or "").strip()
                if value:
                    given.add(value)
    return names, given


def _has_ammo_tag(entity: ET.Element) -> bool:
    """True if this entity or any descendant carries the `ammopickup` tag."""
    for tags in entity.iter("tags"):
        for tag in tags.findall("tag"):
            if tag.get("name") == AMMO_TAG:
                return True
    return False


def _mesh_names(entity: ET.Element) -> list[str]:
    components = entity.find("components")
    if components is None:
        return []
    return [
        m.get("name", "")
        for m in components.findall("meta_mesh_component")
        if m.get("name")
    ]


def _levels(entity: ET.Element) -> set[str]:
    levels = entity.find("levels")
    if levels is None:
        return set()
    return {lv.get("name", "") for lv in levels.findall("level") if lv.get("name")}


# --------------------------------------------------------------------- prefab index


def build_prefab_index(modules: Path) -> dict[str, PropInfo]:
    """prefab name -> what instantiating it gives you.

    Prefabs may themselves reference other prefabs, so resolve to a fixpoint.
    """
    index: dict[str, PropInfo] = {}
    refs: dict[str, set[str]] = defaultdict(set)

    for prefab_file in sorted(modules.glob("*/Prefabs*/**/*.xml")):
        try:
            root = ET.parse(prefab_file).getroot()
        except ET.ParseError:
            continue
        for top in root.iter("game_entity"):
            name = top.get("name")
            if not name:
                continue
            info = PropInfo()
            for sub in top.iter("game_entity"):
                names, given = _own_scripts(sub)
                info.scripts |= names & USABLE_SCRIPTS
                info.given_items |= given
                info.meshes.update(_mesh_names(sub))
                child_prefab = sub.get("prefab")
                if child_prefab and child_prefab != name:
                    refs[name].add(child_prefab)
            info.has_ammo_point = _has_ammo_tag(top)
            if name in index:
                index[name].merge(info)
            else:
                index[name] = info

    # Resolve nested prefab references (prefab A instantiates prefab B).
    for _ in range(4):
        changed = False
        for name, targets in refs.items():
            for target in targets:
                source = index.get(target)
                if source is None or name not in index:
                    continue
                before = (
                    len(index[name].scripts),
                    len(index[name].given_items),
                    index[name].has_ammo_point,
                )
                index[name].merge(source)
                after = (
                    len(index[name].scripts),
                    len(index[name].given_items),
                    index[name].has_ammo_point,
                )
                changed = changed or before != after
        if not changed:
            break

    return index


# ---------------------------------------------------------------------- scene index


def find_scenes(modules: Path) -> dict[str, Path]:
    """lowercased scene name -> scene.xscene path.

    Skips Backups/ and wip_ scenes: neither is reachable from a settlement, and
    including them inflates the counts.
    """
    scenes: dict[str, Path] = {}
    for sceneobj in sorted(modules.glob("*/SceneObj")):
        for child in sorted(sceneobj.iterdir()):
            if not child.is_dir() or child.name.lower().startswith("wip_"):
                continue
            xscene = child / "scene.xscene"
            if xscene.is_file():
                scenes.setdefault(child.name.lower(), xscene)
    return scenes


class SceneReport:
    def __init__(self, name: str, path: Path | None) -> None:
        self.name = name
        self.path = path
        self.rock_piles = 0
        self.barrels = 0
        self.deco_piles = 0
        self.given_items: set[str] = set()
        self.props_missing_ammo_point = 0
        self.levels_seen: set[str] = set()
        self.props_without_levels = 0


def lookalike_meshes(prefabs: dict[str, PropInfo]) -> set[str]:
    """Meshes rendered by prefabs that ARE usable rock piles.

    Derived rather than hard-coded: an entity carrying one of these meshes but no
    script is visually indistinguishable in-game from a pile the player can use.
    Generic scenery rubble (stone_pile_desert_*, stone_pile_wall_*) is excluded
    precisely because no usable prefab renders it.

    Restricted to the standalone StonePile -- SiegeMachineStonePile rides on the
    mangonel/trebuchet prefabs and would drag in every siege-engine debris mesh.
    """
    meshes: set[str] = set()
    for info in prefabs.values():
        if "StonePile" in info.scripts:
            meshes |= info.meshes
    return {m for m in meshes if m and not HELPER_MESH_RE.search(m)}


def analyse_scene(
    name: str, path: Path | None, prefabs: dict[str, PropInfo], deco_meshes: set[str]
) -> SceneReport:
    report = SceneReport(name, path)
    if path is None:
        return report
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        print(f"  !! parse error in {path}: {exc}", file=sys.stderr)
        return report

    for entity in root.iter("game_entity"):
        info = PropInfo()
        own_names, own_given = _own_scripts(entity)
        info.scripts |= own_names & USABLE_SCRIPTS
        info.given_items |= own_given

        prefab_name = entity.get("prefab")
        if prefab_name and prefab_name in prefabs:
            info.merge(prefabs[prefab_name])

        if info.scripts:
            # One entity, one prop -- regardless of how many forms declared it.
            if info.scripts & ROCK_SCRIPTS:
                report.rock_piles += 1
            if info.scripts & BARREL_SCRIPTS:
                report.barrels += 1
            report.given_items |= info.given_items
            # Only StonePile requires ammopickup-tagged children: OnInit calls
            # InitGivenWeapon exclusively on points carrying that tag. AmmoBarrelBase
            # iterates every StandingPoint, so vanilla's arrow_barrel prefab has no
            # such tag and is perfectly fine -- flagging it would be noise.
            if info.scripts & ROCK_SCRIPTS and not (info.has_ammo_point or _has_ammo_tag(entity)):
                report.props_missing_ammo_point += 1
            levels = _levels(entity)
            if levels:
                report.levels_seen |= levels
            else:
                report.props_without_levels += 1
            continue

        # No script: does it render a mesh a usable pile also renders?
        if any(m in deco_meshes for m in _mesh_names(entity)):
            report.deco_piles += 1

    return report


# ------------------------------------------------------------------------ item pool


def build_item_ids(modules: Path) -> set[str]:
    """Every item id the game can resolve, from all modules' ModuleData."""
    ids: set[str] = set()
    item_re = re.compile(r'<(?:Item|CraftedItem)\b[^>]*\bid="([^"]+)"')
    for xml_file in modules.glob("*/ModuleData/**/*.xml"):
        try:
            text = xml_file.read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        if "<Item" not in text and "<CraftedItem" not in text:
            continue
        ids.update(item_re.findall(text))
    return ids


# ---------------------------------------------------------------------- settlements


SETTLEMENT_RE = re.compile(r'<Settlement id="([^"]+)"(.*?)</Settlement>', re.DOTALL)
CENTER_RE = re.compile(r'<Location id="center"([^>]*)>?')
SCENE_ATTR_RE = re.compile(r'scene_name(?:_\d+)?="([^"]+)"')
NAME_RE = re.compile(r'\bname="(?:\{=[^}]*\})?([^"]*)"')


class Settlement:
    def __init__(self, sid: str, display: str, kind: str, scenes: list[str]) -> None:
        self.id = sid
        self.display = display
        self.kind = kind
        self.scenes = scenes


def parse_settlements(path: Path, include_villages: bool) -> list[Settlement]:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    out: list[Settlement] = []
    for match in SETTLEMENT_RE.finditer(text):
        sid, body = match.group(1), match.group(2)
        if "<Town " in body:
            kind = "castle" if 'is_castle="true"' in body else "town"
        elif "<Village " in body:
            kind = "village"
        else:
            kind = "other"
        if kind == "village" and not include_villages:
            continue
        if kind == "other":
            continue

        center = CENTER_RE.search(body)
        scenes: list[str] = []
        if center:
            for scene in SCENE_ATTR_RE.findall(center.group(1)):
                if scene not in scenes:
                    scenes.append(scene)

        header = body.split(">", 1)[0]
        name_match = NAME_RE.search(header)
        display = name_match.group(1) if name_match else sid
        out.append(Settlement(sid, display, kind, scenes))
    return out


# ---------------------------------------------------------------------------- main


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--game", type=Path, default=DEFAULT_GAME, help="Bannerlord install root")
    parser.add_argument("--all", action="store_true", help="include villages")
    parser.add_argument("--scene", help="verbose breakdown for one scene, skip the settlement table")
    args = parser.parse_args()

    modules = args.game / "Modules"
    if not modules.is_dir():
        print(f"ERROR: no Modules directory at {modules}", file=sys.stderr)
        return 2

    settlements_xml = modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
    if not settlements_xml.is_file():
        print(f"ERROR: live settlements file not found: {settlements_xml}", file=sys.stderr)
        return 2

    print("Indexing prefabs, scenes and item ids ...", file=sys.stderr)
    prefabs = build_prefab_index(modules)
    scenes = find_scenes(modules)
    item_ids = build_item_ids(modules)

    usable_prefabs = sorted(n for n, i in prefabs.items() if i.scripts)
    deco_meshes = lookalike_meshes(prefabs)
    print(
        f"Prefabs indexed: {len(prefabs)} ({len(usable_prefabs)} carry a usable siege-prop script)\n"
        f"Scenes on disk:  {len(scenes)}  |  Item ids: {len(item_ids)}\n"
        f"Pile lookalike meshes: {', '.join(sorted(deco_meshes)) or '(none)'}\n"
        f"Settlements:     {settlements_xml}\n"
    )

    cache: dict[str, SceneReport] = {}

    def report_for(scene_name: str) -> SceneReport:
        key = scene_name.lower()
        if key not in cache:
            cache[key] = analyse_scene(scene_name, scenes.get(key), prefabs, deco_meshes)
        return cache[key]

    if args.scene:
        rep = report_for(args.scene)
        print(f"Scene: {rep.name}")
        print(f"  path:              {rep.path or '*** NOT FOUND ON DISK ***'}")
        print(f"  usable rock piles: {rep.rock_piles}")
        print(f"  usable barrels:    {rep.barrels}")
        print(f"  decorative piles:  {rep.deco_piles}")
        print(f"  GivenItemIDs:      {', '.join(sorted(rep.given_items)) or '(none declared)'}")
        for item in sorted(rep.given_items):
            if item not in item_ids:
                print(f"    !! DEAD ITEM ID: {item} -- prop is permanently unusable")
        print(f"  rock piles with no ammopickup point: {rep.props_missing_ammo_point}")
        print(f"  props with no <levels> element: {rep.props_without_levels}")
        print(f"  levels seen on props:           {', '.join(sorted(rep.levels_seen)) or '(none)'}")
        return 0

    settlements = parse_settlements(settlements_xml, args.all)

    rows = []
    for s in settlements:
        reports = [report_for(name) for name in s.scenes]
        rocks = sum(r.rock_piles for r in reports)
        barrels = sum(r.barrels for r in reports)
        deco = sum(r.deco_piles for r in reports)
        missing_scene = any(r.path is None for r in reports) or not s.scenes
        given: set[str] = set()
        for r in reports:
            given |= r.given_items
        rows.append((s, rocks, barrels, deco, missing_scene, given, reports))

    print("=" * 100)
    print(f"{'settlement':<28} {'kind':<7} {'rocks':>5} {'barrels':>7} {'deco':>5}  scene(s)")
    print("=" * 100)
    for s, rocks, barrels, deco, missing, _given, _reports in sorted(
        rows, key=lambda r: (r[1] > 0, r[0].kind, r[0].id)
    ):
        flag = " *** SCENE MISSING" if missing else ""
        scene_list = ", ".join(s.scenes) or "(no center scene)"
        print(f"{s.display[:27]:<28} {s.kind:<7} {rocks:>5} {barrels:>7} {deco:>5}  {scene_list}{flag}")

    no_rocks = [r for r in rows if r[1] == 0]
    looks_usable = [r for r in no_rocks if r[3] > 0]

    print("\n" + "=" * 100)
    print(f"SUMMARY  ({len(rows)} settlements audited)")
    print("=" * 100)
    print(f"  with usable rock piles:    {len(rows) - len(no_rocks)}")
    print(f"  with NO usable rock pile:  {len(no_rocks)}")
    print(f"  with usable ammo barrels:  {sum(1 for r in rows if r[2] > 0)}")

    if looks_usable:
        print("\n" + "-" * 100)
        print("LOOKS USABLE, ISN'T -- decorative pile meshes but zero usable rock piles.")
        print("These reproduce the report exactly: the player sees rock piles and cannot use them.")
        print("-" * 100)
        for s, _rocks, barrels, deco, _missing, _given, _reports in sorted(
            looks_usable, key=lambda r: -r[3]
        ):
            print(f"  {s.display[:34]:<35} {deco:>4} deco meshes, {barrels:>3} barrels  [{', '.join(s.scenes)}]")

    dead: dict[str, set[str]] = defaultdict(set)
    for s, _rocks, _barrels, _deco, _missing, given, _reports in rows:
        for item in given:
            if item not in item_ids:
                dead[item].add(s.display)
    if dead:
        print("\n" + "-" * 100)
        print("DEAD GivenItemID -- item is not defined anywhere; the prop silently disables")
        print("itself for player AND AI (null item -> IsDisabledForAgent falls through to true).")
        print("-" * 100)
        for item, users in sorted(dead.items()):
            sample = ", ".join(sorted(users)[:6])
            more = f" (+{len(users) - 6} more)" if len(users) > 6 else ""
            print(f"  {item}: {sample}{more}")
    else:
        print("\n  No dead GivenItemID references among audited settlements.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
