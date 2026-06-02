#!/usr/bin/env python3
"""Assign orc/goblin village production types per the design rule, IN PLACE on the live
TAOM_Map/settlements.xml — TARGETED edit (only the village_type attribute changes; positions,
culture, owner, bound, scenes, and all editor placements are preserved byte-for-byte).

Rule (Goblins + Misty Mountain Orcs villages; cultures goblin / mistymountainorcs):
  - Mostly animal food (swine_farm / cattle_range) + ~1 iron/silver mine per fief.
  - 1 village        -> [swine_farm]                      (animal only)
  - 2-3 villages      -> (n-1) animal + 1 mine
  - 4+ villages       -> (n-2) animal + 1 mine + 1 lumberjack (a bit more diverse)
  Mine type alternates iron_mine / silver_mine across fiefs.
  Lindon (rivendell) villages are NOT touched (elven economy).

Idempotent + deterministic. Writes a timestamped backup first.

Usage:
  python tools/assign_orc_village_types.py            # dry-run (prints plan)
  python tools/assign_orc_village_types.py --apply    # writes live file (+ backup)
"""
import argparse, re, shutil
from datetime import datetime

LIVE = r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml"
ORC_CULTURES = {"goblin", "mistymountainorcs"}  # bluecraig villages (culture goblin) covered when added
# v1.4.5 VillageType stringIds are CODE-registered in DefaultVillageTypes (NOT XML) — verify against the
# engine, never a plausible name. "cattle_range" does NOT exist (it's "cattle_farm"); using an unregistered
# id makes Village.Deserialize store a null VillageType -> NRE in UpdateTotalProduction. (deep-review HIGH 2026-06-02)
VALID_VILLAGE_TYPES = {
    "swine_farm", "cattle_farm", "sheep_farm", "wheat_farm", "vineyard", "fisherman", "lumberjack",
    "iron_mine", "silver_mine", "clay_mine", "salt_mine", "flax_plant", "date_farm", "olive_trees",
    "silk_plant", "trapper",
}
ANIMALS = ["swine_farm", "cattle_farm"]


def fief_types(n, mine):
    if n == 1:
        out = ["swine_farm"]
    elif n <= 3:
        out = [ANIMALS[i % 2] for i in range(n - 1)] + [mine]
    else:
        out = [ANIMALS[i % 2] for i in range(n - 2)] + [mine, "lumberjack"]
    bad = [t for t in out if t not in VALID_VILLAGE_TYPES]
    if bad:  # guard against an unregistered VillageType id reaching the live file -> in-game NRE
        raise ValueError(f"invalid VillageType id(s) {bad} — not registered in v1.4.5 DefaultVillageTypes")
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    text = open(LIVE, encoding="utf-8", errors="replace").read()

    # collect orc/goblin villages: id -> (bound fief, current type)
    villages = {}
    for b in re.findall(r'<Settlement\b.*?</Settlement>', text, re.DOTALL):
        sid = re.search(r'id="([^"]+)"', b).group(1)
        if "<Village" not in b:
            continue
        cul = re.search(r'culture="Culture\.([^"]+)"', b)
        if not cul or cul.group(1) not in ORC_CULTURES:
            continue
        bound = re.search(r'bound="Settlement\.([^"]+)"', b)
        cur = re.search(r'village_type="VillageType\.([^"]+)"', b)
        villages[sid] = (bound.group(1) if bound else None, cur.group(1) if cur else None)

    # group by fief
    fiefs = {}
    for vid, (fief, _) in villages.items():
        fiefs.setdefault(fief, []).append(vid)

    # assign (mine alternates per fief, deterministic by sorted fief id)
    plan = {}
    for fi, fief in enumerate(sorted(fiefs)):
        vids = sorted(fiefs[fief], key=lambda s: [int(x) if x.isdigit() else x for x in re.split(r'(\d+)', s)])
        mine = "iron_mine" if fi % 2 == 0 else "silver_mine"
        for vid, vt in zip(vids, fief_types(len(vids), mine)):
            plan[vid] = vt

    # report
    print(f"Orc/goblin villages: {len(villages)} across {len(fiefs)} fiefs")
    for fief in sorted(fiefs):
        vids = sorted(fiefs[fief], key=lambda s: [int(x) if x.isdigit() else x for x in re.split(r'(\d+)', s)])
        print(f"  {fief:14} ({len(vids)}): " + ", ".join(f"{v.split('_')[-1]}={plan[v]}({villages[v][1]})" for v in vids))

    if not args.apply:
        print("\nDRY RUN — re-run with --apply to write the live file.")
        return

    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    shutil.copy2(LIVE, LIVE + f".bak_vtypes_{ts}")
    changed = 0
    for sid, newtype in plan.items():
        m = re.search(r'<Settlement id="' + re.escape(sid) + r'".*?</Settlement>', text, re.DOTALL)
        block = m.group(0)
        nb = re.sub(r'village_type="VillageType\.[^"]+"', f'village_type="VillageType.{newtype}"', block, count=1)
        if nb != block:
            changed += 1
        text = text[:m.start()] + nb + text[m.end():]
    open(LIVE, "w", encoding="utf-8").write(text)
    import xml.etree.ElementTree as ET
    ET.parse(LIVE)
    print(f"\nApplied: {changed} village_type changes (backup .bak_vtypes_{ts}); XML well-formed.")


if __name__ == "__main__":
    main()
