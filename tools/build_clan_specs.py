#!/usr/bin/env python3
"""Auto-generate per-culture clan_heraldry/<name>.json specs for the clan-heraldry
rollout: each clan gets a UNIQUE lore-based color (per-culture base + deterministic
per-clan hue/lightness variation) and an archetype-driven roster composed from that
culture's actual troops (troops_*.xml).

Consumed afterwards by tools/generate_clan_heraldry.py.

Covers the 15 troop-having LOTR cultures (Gondor is hand-authored separately and skipped).
Cultures with no own troop pool (abanissa, shaghana, khand, lothlorien) + vanilla minor
factions + bandit clans are handled outside this tool.

Usage:  python tools/build_clan_specs.py [--culture mordor] [--print]
        (no args -> writes specs for every culture in CULTURE_TABLE)
"""
import argparse
import colorsys
import glob
import json
import os
import re
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")
TROOPS_DIR = os.path.join(MD, "troops")
SPEC_DIR = os.path.join(MD, "clan_heraldry")
REGISTRY = os.path.join(ROOT, "docs", "reviews", "_clan_registry.json")

# lotr_name -> (culture StringId, base_primary, base_secondary)
CULTURE_TABLE = {
    "mordor":            ("mordor",            "FF1A1717", "FF7E1518"),
    "dunland":           ("empire",            "FF5A3D28", "FF8C3A2C"),
    "rohan":             ("vlandia",           "FF35632F", "FFE0D6A8"),
    "harad":             ("aserai",            "FF8E1C1C", "FFC9A227"),
    "rhun":              ("khuzait",           "FFC0962E", "FF5A1A14"),
    "dale":              ("sturgia",           "FF1E3A6E", "FFD4A53A"),
    "erebor":            ("erebor",            "FF1E5A3A", "FFB8860B"),
    "rivendell":         ("rivendell",         "FF8A93B0", "FF2A2A6E"),
    "mirkwood":          ("mirkwood",          "FF1F5A2C", "FFA8C47C"),
    "isengard":          ("isengard",          "FF2C2C2C", "FFD8D8D8"),
    "gundabad":          ("gundabad",          "FF2E2A26", "FF6E5A40"),
    "dolguldur":         ("dolguldur",         "FF3A2E22", "FF6E7A3E"),
    "umbar":             ("umbar",             "FF2E2A24", "FFB58A4E"),
    "goblin":            ("goblin",            "FF22301E", "FF4A5A30"),
}

# Cultures with no own troop pool. value: (clan_culture, pool_culture|None, base1, base2)
# pool_culture None -> colors-only (no per-clan template; keeps the culture's existing fallback).
TROOPLESS_TABLE = {
    "shaghana":   ("shaghana",   "aserai",    "FFA05020", "FF5A3018"),  # Near-Harad, fields Harad troops
    "abanissa":   ("abanissa",   "aserai",    "FF1A3560", "FFAA9240"),  # Far-Harad, fields Harad troops
    "lothlorien": ("lothlorien", "rivendell", "FF184031", "FFC0E3B5"),  # Galadhrim, fields Rivendell troops
    "khand":      ("battania",   None,        "FF8A5A1E", "FF5A1E18"),  # Variags: no TAOM pool -> colors only
    # Moved out of CULTURE_TABLE when troops_mistymountainorcs.xml was retired: it was a duplicate
    # of the goblin tree (same shape, same gear, different race tag and skill numbers), so the
    # Orc-host now fields troops_goblin.xml. Its clans keep their own ids and colors, hence
    # clan_culture stays mistymountainorcs while the roster pool comes from goblin. The one troop
    # it kept, mistymountainorcs_bolgs_ironfang, lives in troops_goblin.xml now.
    "mistymountainorcs": ("mistymountainorcs", "goblin", "FF2A2A2A", "FF6E5A40"),
}

# A culture that borrows another's pool but kept one bespoke troop of its own. Without this the
# composer hands the borrower the POOL culture's signature unit, so Misty Mountain lords would
# field Goblin-town's "Bolg's Ironfang", which is the exact distinction the bespoke troop exists
# to draw. Explicit pairs, not a computed suffix: an unbounded rule would silently swap a troop
# nobody meant to swap.  lotr -> {pool troop id: this culture's replacement}
BESPOKE_SWAPS = {
    "mistymountainorcs": {"goblin_bolgs_ironfang": "mistymountainorcs_bolgs_ironfang"},
}

GROUPS = ("Infantry", "Ranged", "Cavalry", "HorseArcher")
ARCHETYPES = ["balanced", "infantry", "ranged", "cavalry", "elite", "skirmisher"]
# group weights per archetype (missing groups get redistributed)
ARCH_W = {
    "balanced":   {"Infantry": 0.45, "Ranged": 0.30, "Cavalry": 0.20, "HorseArcher": 0.05},
    "infantry":   {"Infantry": 0.70, "Ranged": 0.18, "Cavalry": 0.10, "HorseArcher": 0.02},
    "ranged":     {"Infantry": 0.38, "Ranged": 0.52, "Cavalry": 0.07, "HorseArcher": 0.03},
    "cavalry":    {"Infantry": 0.32, "Ranged": 0.13, "Cavalry": 0.45, "HorseArcher": 0.10},
    "elite":      {"Infantry": 0.42, "Ranged": 0.28, "Cavalry": 0.25, "HorseArcher": 0.05},
    "skirmisher": {"Infantry": 0.30, "Ranged": 0.55, "Cavalry": 0.08, "HorseArcher": 0.07},
}


# ---------- color variation ----------
def vary_hex(hexstr, idx, n, hue_span=0.10, light_span=0.14):
    """Deterministically nudge a base FFRRGGBB color per clan index to a distinct shade."""
    r = int(hexstr[2:4], 16) / 255.0
    g = int(hexstr[4:6], 16) / 255.0
    b = int(hexstr[6:8], 16) / 255.0
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    if n > 1:
        t = (idx / (n - 1)) - 0.5  # -0.5..+0.5
    else:
        t = 0.0
    h = (h + t * hue_span) % 1.0
    l = min(0.92, max(0.06, l + t * light_span))
    r, g, b = colorsys.hls_to_rgb(h, l, s)
    return "FF%02X%02X%02X" % (round(r * 255), round(g * 255), round(b * 255))


# ---------- troop index ----------
def index_troops():
    """culture StringId -> list of {id, group, level} for combat troops."""
    pat = re.compile(
        r'<NPCCharacter\b[^>]*?\bid="([^"]+)"[^>]*?>', re.S)
    out = defaultdict(list)
    for path in glob.glob(os.path.join(TROOPS_DIR, "troops_*.xml")):
        text = open(path, "r", encoding="utf-8-sig").read()
        text = re.sub(r'<!--.*?-->', '', text, flags=re.S)  # drop commented-out (disabled) troops
        # iterate each NPCCharacter start tag
        for m in re.finditer(r'<NPCCharacter\b(.*?)>', text, re.S):
            tag = m.group(1)
            cid = re.search(r'\bid="([^"]+)"', tag)
            grp = re.search(r'\bdefault_group="([^"]+)"', tag)
            lvl = re.search(r'\blevel="([0-9]+)"', tag)
            cul = re.search(r'\bculture="Culture\.([^"]+)"', tag)
            if not (cid and grp and cul):
                continue
            if grp.group(1) not in GROUPS:
                continue
            if cid.group(1).endswith("_boss"):
                continue
            # Tavern mercenaries are Occupation.Mercenary leaves hired for gold from
            # <basic_mercenary_troops>, not troops a lord fields. They were never drawn into a
            # live clan roster, but only by luck of pool ordering: nothing excluded them until a
            # culture started pooling from another culture and the ordering shifted.
            if cid.group(1).endswith("_merc"):
                continue
            out[cul.group(1)].append({
                "id": cid.group(1), "group": grp.group(1),
                "level": int(lvl.group(1)) if lvl else 10,
            })
    return out


# ---------- roster composition ----------
def bands(troops):
    """split a group's troops (sorted by level) into low/mid/high thirds."""
    ts = sorted(troops, key=lambda t: t["level"])
    n = len(ts)
    if n == 0:
        return [], [], []
    a = max(1, n // 3)
    b = max(a, (2 * n) // 3)
    return ts[:a], ts[a:b] or ts[a:a + 1], ts[b:] or ts[-1:]


def compose(pool, archetype, seed):
    by_group = defaultdict(list)
    for t in pool:
        by_group[t["group"]].append(t)
    present = [g for g in GROUPS if by_group[g]]
    if not present:
        return []
    w = {g: ARCH_W[archetype].get(g, 0.0) for g in present}
    tot = sum(w.values()) or 1.0
    w = {g: w[g] / tot for g in present}

    target_stacks = 12
    roster, used = [], set()

    def pick(items, count, offset):
        chosen = []
        if not items:
            return chosen
        for k in range(count):
            it = items[(offset + k) % len(items)]
            if it["id"] in used:
                # try next distinct
                for j in range(len(items)):
                    cand = items[(offset + k + j) % len(items)]
                    if cand["id"] not in used:
                        it = cand
                        break
            if it["id"] in used:
                continue
            used.add(it["id"])
            chosen.append(it)
        return chosen

    for g in present:
        n_g = max(1, round(w[g] * target_stacks))
        low, mid, high = bands(by_group[g])
        elite_bias = (archetype == "elite")
        # distribute n_g across bands
        n_low = 0 if elite_bias else max(1, round(n_g * 0.45))
        n_high = max(1, round(n_g * (0.5 if elite_bias else 0.25)))
        n_mid = max(0, n_g - n_low - n_high)
        for band, cnt, (mn, mx) in (
            (low, n_low, (2, 4)), (mid, n_mid, (1, 2)), (high, n_high, (0, 1))):
            for t in pick(band, cnt, seed):
                roster.append({"troop": t["id"], "min": mn, "max": mx})
    # ensure a low-tier anchor stack exists
    if roster and all(r["min"] == 0 for r in roster):
        roster[0]["min"], roster[0]["max"] = 2, 4
    return roster


def normalize_culture(c):
    return (c or "").replace("Culture.", "")


def select_clans(registry, culture_id):
    out = []
    for c in registry["clans"]:
        if normalize_culture(c.get("culture")) != culture_id:
            continue
        if c.get("source") not in ("xml", "xslt"):
            continue
        if str(c.get("is_bandit")).lower() == "true" or str(c.get("is_minor_faction")).lower() == "true":
            continue
        if c["id"].startswith("clan_empire_west"):  # Gondor: hand-authored
            continue
        out.append(c)
    out.sort(key=lambda c: c["id"])
    return out


def apply_bespoke_swaps(lotr, roster):
    """Swap the pool culture's signature troop for the borrowing culture's own (see BESPOKE_SWAPS)."""
    swaps = BESPOKE_SWAPS.get(lotr)
    if not swaps:
        return roster
    for row in roster:
        if row["troop"] in swaps:
            row["troop"] = swaps[row["troop"]]
    return roster


def build_spec(lotr, clan_culture, pool_culture, base1, base2, registry, troop_index):
    clans = select_clans(registry, clan_culture)
    colors_only = pool_culture is None
    pool = [] if colors_only else troop_index.get(pool_culture, [])
    present = set(t["group"] for t in pool)
    archs = [a for a in ARCHETYPES if a != "cavalry" or ("Cavalry" in present or "HorseArcher" in present)]
    n = len(clans)
    note = ("Colors-only (no TAOM troop pool); per-clan color = %s/%s base + per-clan variation." % (base1, base2)
            if colors_only else
            "Per-clan color = %s/%s base + per-clan variation; rosters archetype-composed from troops_*.xml (culture=%s)." % (base1, base2, pool_culture))
    spec = {"culture": clan_culture, "_generated_by": "tools/build_clan_specs.py",
            "_note": note, "clans": []}
    for i, c in enumerate(clans):
        arch = archs[i % len(archs)] if archs else "balanced"
        entry = {
            "id": c["id"], "source": c["source"],
            "theme": "%s house %d%s" % (lotr, i + 1, "" if colors_only else " — " + arch),
            "color": vary_hex(base1, i, n), "color2": vary_hex(base2, i, n),
            "roster": [] if colors_only else apply_bespoke_swaps(lotr, compose(pool, arch, seed=i)),
        }
        if not colors_only:
            entry["template_id"] = "kingdom_hero_party_%s_%s_template" % (lotr, re.sub(r'^clan_', '', c["id"]))
        spec["clans"].append(entry)
    return spec, n, len(pool)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--culture", help="single lotr name (e.g. mordor); default all in table")
    ap.add_argument("--print", action="store_true", help="print one sample clan per culture")
    args = ap.parse_args()

    registry = json.load(open(REGISTRY, "r", encoding="utf-8"))
    troop_index = index_troops()
    os.makedirs(SPEC_DIR, exist_ok=True)

    # unified: lotr -> (clan_culture, pool_culture, b1, b2); troop-having cultures pool themselves
    ALL = {k: (cid, cid, b1, b2) for k, (cid, b1, b2) in CULTURE_TABLE.items()}
    ALL.update(TROOPLESS_TABLE)

    names = [args.culture] if args.culture else list(ALL)
    for lotr in names:
        clan_culture, pool_culture, b1, b2 = ALL[lotr]
        spec, n, npool = build_spec(lotr, clan_culture, pool_culture, b1, b2, registry, troop_index)
        path = os.path.join(SPEC_DIR, lotr + ".json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(spec, f, indent=2)
        empty = sum(1 for c in spec["clans"] if not c["roster"])
        tag = "colors-only" if pool_culture is None else ("pool=" + pool_culture)
        print("%-20s clan_culture=%-15s %-14s clans=%2d  troops=%3d  empty_rosters=%d  -> %s"
              % (lotr, clan_culture, tag, n, npool, empty, os.path.basename(path)))
        if args.print and spec["clans"]:
            s = spec["clans"][0]
            print("   sample %s col=%s/%s tmpl=%s stacks=%d"
                  % (s["id"], s["color"], s["color2"], s["template_id"], len(s["roster"])))


if __name__ == "__main__":
    main()
