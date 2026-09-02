#!/usr/bin/env python3
"""Generate a committed, diffable inventory of every mesh the Armoury ships.

WHY THIS EXISTS

On 2026-09-01 an artist reorganised the Armoury across 7 commits. The sync was
clean, but the reorganisation deleted art that XML still named and item
definitions that troops still equipped: 275 validator errors, and the damage was
found from in-game symptoms rather than from a diff. Every hand-maintained list
in the repo had already rotted (`dale_armor_meshes.txt` untouched since May, the
`armory-guide.md` Gondor row that carried 5 of 17 tokens for months, a
`mesh-audit` dump 252 names out of date), so there was nothing to diff against.

This produces that missing baseline. Regenerate after any art drop, and
`--diff` tells you what moved, what was renamed and what genuinely went away
BEFORE the game does.

WHAT IT IS NOT: a validator. `validate_mesh_refs.py` answers "does every
referenced mesh exist"; this answers "what exists, and where did it come from".
The `referenced` column joins the two.

SOURCE OF TRUTH: the LIVE install, because that is what the engine loads. Proven
in an rgl_log: `Loading packages $BASE/Modules/LOTRLOME_Armory/Assets...`, and
TAOM/TAOM_Map ship BOTH trees while the engine still names `Assets` for each, so
loose wins over cooked. `--compare-repo` reports the delta against the versioned
copy.

EmAssetPackages is NEVER a source. This globs `<module>/Assets/**/*.tpac`
directly, so `AssetPackages/` and `EmAssetPackages/` are both out of scope by
construction rather than by inheriting a helper. That is deliberate: the
catalogue is an inventory of the loose tree the engine reads for this module,
not of whatever `tpac_paths_for_modules` would prefer for a module that also
ships cooked packs. `LOTRLOME_Armory` has no `EmAssetPackages` at all.

Usage:
  python tools/generate_armory_catalogue.py                  # write the catalogue
  python tools/generate_armory_catalogue.py --check          # drift gate, exit 1
  python tools/generate_armory_catalogue.py --diff           # rename-aware change report
  python tools/generate_armory_catalogue.py --compare-repo <dir>

Exit: 1 on drift (--check), on an unclassified mesh with no override row, or on
a bad input path (2).
"""
from __future__ import annotations

import argparse
import difflib
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import validate_mesh_refs as vm  # noqa: E402
from _gamedir import ensure_exists, game_dir  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_MODULE = "LOTRLOME_Armory"
OUT_DIR = REPO_ROOT / "docs" / "reference" / "armory-catalogue"
OUT_TSV = OUT_DIR / "catalogue.tsv"
OVERRIDES = REPO_ROOT / "tools" / "armory_catalogue_overrides.tsv"

COLUMNS = ["mesh", "kind", "prefix", "culture", "sub", "category", "tier",
           "variant", "folder", "tpac", "referenced", "source"]

# Position-1 prefixes, measured. `bo_` is the only one that holds at 100%
# (383 of 383 physics shapes). `ar` is ARNOR, not "legacy armour".
PREFIXES = {"sk", "sm", "wm", "clo", "bo", "ar"}

# Position-2 culture tokens, measured from the live tree. `sk_` and `wm_` do NOT
# share a vocabulary: wm_ mixes culture, region and hero names at that position,
# which is why this is a lookup and not a positional rule.
CULTURE_TOKENS = {
    "gd": "gondor", "rh": "rhun", "rhun": "rhun", "dg": "dol_guldur",
    "guldur": "dol_guldur", "dwarf": "dwarf", "uruk": "uruk", "md": "mordor",
    "mordor": "mordor", "dale": "dale", "gb": "gundabad", "gundabad": "gundabad",
    "ar": "arnor", "gn": "orc", "northern": "northern", "is": "isengard",
    "isengard": "isengard", "eb": "creature", "elf": "elf",
    "hd": "harad", "harad": "harad", "haradrim": "harad", "saruman": "isengard",
    # "pale uruk" is Gundabad art: the tpac path reads Race Test/Gundabad/SK_GB_Pale_...
    "pale": "gundabad", "lossarnach": "gondor", "strider": "arnor",
    "finwe": "elf", "fingon": "elf", "finarin": "elf", "ingwe": "elf",
    "turin": "elf", "voronwe": "elf",
    "goblin": "goblin", "spider": "creature", "elephant": "creature",
    "mumakil": "creature", "rohan": "rohan", "roh": "rohan", "cts": "rohan",
    "rivendell": "rivendell", "mirkwood": "mirkwood", "mkwd": "mirkwood",
    "dunland": "dunland", "thenn": "thenn", "ithilien": "gondor",
    "gondor": "gondor", "numenorean": "numenorean", "nazgul": "nazgul",
    "nazghul": "nazgul", "sauron": "mordor", "warg": "creature",
    "troll": "creature", "chariot": "creature", "orc": "orc",
    # The `weapons/` and `shield/` folders are cross-culture buckets carrying no
    # culture in the path, so named heroes and weapon families are the only
    # signal for 176 assets there.
    "elven": "elf", "hai": "uruk", "caerdh": "dunland", "wulf": "dunland",
    "loke": "rhun", "drag": "rhun", "khml": "dol_guldur", "num": "numenorean",
    "anduril": "gondor", "pelargir": "gondor", "pelagir": "gondor",
    "tolfalas": "gondor", "pavise": "gondor", "swan": "gondor", "ano": "gondor",
    "theoden": "rohan", "theodred": "rohan", "erkenbrand": "rohan",
    "glorfindel": "rivendell", "gf": "rivendell", "aranruth": "elf",
    "celegorm": "elf", "tuors": "elf", "thranduil": "mirkwood",
    "legolas": "mirkwood", "boromir": "gondor", "faramir": "gondor",
    "witch": "nazgul", "khamul": "dol_guldur", "arnor": "arnor",
}

SLOTS = {
    "helmet": "helmet", "helm": "helmet", "hood": "helmet", "crown": "helmet",
    "chest": "chest", "torso": "chest", "armor": "chest", "armour": "chest",
    "jerkin": "chest", "coat": "chest", "scalemail": "chest", "robe": "chest",
    "boots": "boots", "grvs": "greaves", "greaves": "greaves", "feet": "boots",
    "bracer": "bracer", "gloves": "bracer", "gauntlet": "bracer", "hand": "bracer",
    "pauldron": "pauldron", "pauld": "pauldron", "shoulder": "pauldron",
    "cape": "cape", "cloak": "cape",
    "shield": "shield", "sword": "weapon", "axe": "weapon", "mace": "weapon",
    "spear": "weapon", "bow": "weapon", "arrow": "weapon", "blade": "weapon",
    "guard": "weapon", "pommel": "weapon", "hilt": "weapon", "handle": "weapon",
    "longbow": "weapon", "dagger": "weapon", "poleaxe": "weapon",
    "beard": "beard", "body": "body", "head": "body", "hands": "body",
    "barding": "barding", "bard": "barding", "saddle": "barding",
    # Extracted from the live names rather than assumed. `chainmail` alone
    # accounts for 127 assets, and none of these were in the documented convention.
    "chainmail": "chest", "plate": "chest", "hplate": "chest", "lamellar": "chest",
    "dress": "chest", "tunic": "chest", "shirt": "chest", "mail": "chest",
    "quiver": "quiver", "vambrace": "bracer", "vambraces": "bracer",
    "hair": "hair", "basemesh": "body", "bm": "body", "legs": "body", "arm": "bracer",
    "cloth": "chest", "fur": "chest", "banner": "banner", "horse": "barding",
    "polearm": "weapon", "skirt": "chest", "bolt": "weapon",
    "underwear": "body", "platform": "prop",
    # Abbreviations and shipped MISSPELLINGS. These are mapped, never corrected:
    # a misspelled id that resolves is evidence the name is right
    # (armory-shield-audit.md, the bo_capwm_isengard_shield_a02_clean lesson).
    "shldr": "pauldron", "brcr": "bracer", "glove": "bracer", "greave": "greaves",
    # Rohan's `roh_nbl_*` set abbreviates every slot; `cts_rohan_*` uses others.
    "bts": "boots", "clk": "cape", "clo": "chest", "glv": "bracer",
    "hlm": "helmet", "gorg": "chest", "armguard": "bracer", "bracers": "bracer",
    "mask": "helmet", "shoes": "boots", "steelbow": "weapon", "lance": "weapon",
    "flag": "prop", "necklace": "prop", "goat": "body", "fellwarg": "body",
    "warg": "body", "spider": "body", "troll": "body", "elephant": "body",
    "toso": "chest", "armourr": "chest", "roha": "cape",
    "crossbow": "weapon", "horn": "prop", "bag": "prop", "prop": "prop",
    "sol": "chest",
}
# Quality tier. Deliberately does NOT include the troop-class tokens below:
# `sk_gd_dol_cav_helmet_elite_a` carries both, and mixing them into one set made
# whichever appeared first win, so half the catalogue reported `cav` as a tier.
TIERS = {"elite", "lord", "noble", "heavy", "med", "medium", "light",
         "legionary", "veteran", "nbl", "normal", "militia", "captain",
         "half", "full", "sergeant"}
# Troop class. Recognised so it is not mistaken for a sub-region, but not a tier.
CLASSES = {"inf", "cav", "arc", "arch", "archer", "ranged"}


def escape(name: str) -> str:
    """C-escape non-printables. One shipped mesh carries eight NUL bytes and
    four contain spaces, which is why this file is TSV and every value is
    escaped rather than written raw."""
    out = []
    for ch in name:
        if ch == "\t":
            out.append("\\t")
        elif ch == "\\":
            out.append("\\\\")
        elif ord(ch) < 0x20 or ord(ch) == 0x7F:
            out.append("\\x%02x" % ord(ch))
        else:
            out.append(ch)
    return "".join(out)


def classify(mesh: str, folder: str) -> dict:
    """Derive what can be derived from the name, falling back to the folder.

    Both axes are needed. The name carries sub-region and tier the folder does
    not (`gondor_assets/` holds all 17 Gondor regions); the folder carries a
    culture signal for the ~19% of names with no convention prefix at all.
    """
    # split on whitespace as well as underscore: four shipped names contain a
    # space, and one of them ("legolas gloves") has no underscore at all.
    segs = [t for t in re.split(r"[_\s]+", mesh.lower()) if t]
    prefix = segs[0] if segs and segs[0] in PREFIXES else "-"
    rest = segs[1:] if prefix != "-" else segs

    # `clo_` wraps a full base name: clo_sk_gd_vale_cape_a. Strip and re-parse.
    if prefix == "clo" and rest and rest[0] in PREFIXES:
        prefix = rest[0]
        rest = rest[1:]
    # `bo_cap_<name>` is a capsule collision body around <name>. `cap` is not a
    # culture, and leaving it in hid the real token one position along.
    if prefix == "bo" and rest and rest[0] == "cap":
        rest = rest[1:]
    # a collision body wraps a visual name, often with its own prefix
    if prefix == "bo" and rest and rest[0] in PREFIXES:
        rest = rest[1:]

    def singular(s):
        return s[:-1] if len(s) > 3 and s.endswith("s") and s[:-1] in SLOTS else s

    # Culture can sit at any position, not just 2. `sk_` uses position 2, but
    # `wm_` mixes culture, region and hero names there, and the 828 names with
    # no prefix at all carry it wherever it lands.
    culture = None
    cul_at = -1
    for idx, s in enumerate(rest):
        if s in CULTURE_TOKENS:
            culture, cul_at = CULTURE_TOKENS[s], idx
            break
    if not culture:
        # Tokenise first. A bare substring test matched `ar` inside `horse_armor`
        # and `eb` inside `erebor_weapons`, mislabelling 29 rows and making
        # 18.6% of everything called `arnor` wrong.
        f_toks = [t for t in re.split(r"[^a-z0-9]+", folder.lower()) if t]
        for tok, cul in sorted(CULTURE_TOKENS.items(), key=lambda kv: -len(kv[0])):
            if tok in f_toks:
                culture = cul
                break

    sub = "-"
    if cul_at >= 0 and len(rest) > cul_at + 2:
        nxt = singular(rest[cul_at + 1])
        if nxt not in SLOTS and nxt not in TIERS and nxt not in CLASSES:
            sub = rest[cul_at + 1]

    # Exact key first: singular("hands") is "hand" -> bracer, which shadowed
    # the exact "hands" -> body entry and mislabelled three bodies.
    category = next((SLOTS[s] for s in rest if s in SLOTS), None)
    if category is None:
        category = next((SLOTS[singular(s)] for s in rest if singular(s) in SLOTS), None)
    if category is None:
        # numbered variants: armor1, cape1, inf3 resolve like their base word
        def debase(s):
            # helmet1a / helmet4d / armor1 all reduce to their base slot word
            return re.sub(r"\d+[a-z]?$", "", s)
        category = next((SLOTS[debase(s)] for s in rest if debase(s) in SLOTS), None)
    if not category and prefix == "bo":
        category = "collision"
    tier = next((s for s in rest if s in TIERS), None)
    if tier is None:
        # 106 assets state the tier as `tier1` / `t4` rather than a word.
        tier = next((s for s in rest if re.fullmatch(r"(?:tier|t)\d", s)), "-")
    variant = rest[-1] if rest and re.fullmatch(r"[a-z]?\d*[a-z]?\d*", rest[-1] or "") and rest[-1] else "-"
    return {
        "prefix": prefix,
        "culture": culture or "unknown",
        "sub": sub,
        "category": category or "unknown",
        "tier": tier,
        "variant": variant if variant != rest[0] else "-",
    }


def load_overrides() -> dict:
    """id -> {culture, category}. Hand-written, bounded, and each row states why."""
    out = {}
    if not OVERRIDES.exists():
        return out
    rejected = 0
    for line in OVERRIDES.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        # An override that writes back `unknown` satisfies the gate's predicate
        # while recording no decision, which turns the gate into a blanket
        # waiver. Reject it, so the asset stays visible until it is classified
        # for real. Seeding 206 such rows is exactly how the gate got hollowed.
        if parts[1] == "unknown" or parts[2] == "unknown":
            rejected += 1
            continue
        out[parts[0]] = {"culture": parts[1], "category": parts[2]}
    if rejected:
        print(f"WARNING: {rejected} override row(s) ignored because they record "
              f"'unknown'. An override must state a decision, not repeat the "
              f"parser's failure.", file=sys.stderr)
    return out


def build_rows(module_root: Path, referenced: set) -> list:
    """One row per asset, carrying the tpac it came from. That column is the
    rename-detection key: git cannot help here, because LFS pointers defeat
    similarity detection badly enough that -M40% fabricates renames between
    unrelated cultures."""
    assets = module_root / "Assets"
    overrides = load_overrides()
    rows = []
    for tpac in sorted(assets.rglob("*.tpac")):
        res = vm.scan_tpac_metameshes(tpac)
        if not res.parsed_ok:
            continue
        rel = tpac.relative_to(assets).as_posix()
        folder = rel.split("/")[0]
        for kind, names in (("metamesh", res.metamesh_names),
                            ("physicsshape", res.physicsshape_names)):
            for name in sorted(names):
                c = classify(name, rel)   # full path: culture often sits in a deeper segment
                src = "parsed"
                # Key on the ESCAPED name: that is what the TSV stores, and one
                # shipped mesh carries eight NUL bytes, so raw and escaped differ
                # for exactly the asset most likely to need an override.
                key = escape(name)
                if key in overrides:
                    c["culture"] = overrides[key]["culture"]
                    c["category"] = overrides[key]["category"]
                    src = "override"
                base = re.sub(r"\.lod\d+$", "", name, flags=re.IGNORECASE)
                ref = "Y" if base in referenced else (
                    "SLIM" if base.endswith("_slim") and base[:-5] in referenced else "N")
                rows.append({
                    "mesh": escape(name), "kind": kind, "prefix": c["prefix"],
                    "culture": c["culture"], "sub": c["sub"], "category": c["category"],
                    "tier": c["tier"], "variant": c["variant"], "folder": folder,
                    "tpac": rel, "referenced": ref, "source": src,
                })
    rows.sort(key=lambda r: (r["mesh"], r["kind"]))
    return rows


def render(rows: list, module_root: Path) -> str:
    n_tpac = len(list((module_root / "Assets").rglob("*.tpac")))
    meta = Counter(r["kind"] for r in rows)
    head = [
        f"# Armoury mesh catalogue. GENERATED by tools/generate_armory_catalogue.py, do not hand-edit.",
        f"# source={module_root.name}/Assets  tpacs={n_tpac}  "
        f"metameshes={meta['metamesh']}  physicsshapes={meta['physicsshape']}",
        f"# Regenerate after any art drop. `--diff` classifies rename vs move vs delete.",
        "\t".join(COLUMNS),
    ]
    return "\n".join(head + ["\t".join(r[c] for c in COLUMNS) for r in rows]) + "\n"


def parse_existing(path: Path) -> dict:
    """mesh -> row dict, from a committed catalogue."""
    if not path.exists():
        return {}
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("#") or not line.strip():
            continue
        parts = line.split("\t")
        if parts[0] == "mesh" or len(parts) != len(COLUMNS):
            continue
        out[parts[0]] = dict(zip(COLUMNS, parts))
    return out


def classify_changes(old: dict, new: dict) -> dict:
    """RENAME / MOVE / DELETE / NEW, joined on the tpac column then basename."""
    gone = {k: v for k, v in old.items() if k not in new}
    added = {k: v for k, v in new.items() if k not in old}
    by_tpac = defaultdict(list)
    for k, v in added.items():
        by_tpac[v["tpac"]].append(k)
    by_base = defaultdict(list)
    for k, v in added.items():
        by_base[Path(v["tpac"]).name].append(k)

    # A geo tpac holds many meshes, so "same tpac" alone is far too coarse: a
    # genuine deletion from a tpac that also gained a name would read as a
    # rename. Pair by similarity WITHIN the candidate tpac and require a real
    # match, otherwise it is a deletion and must be reported as one.
    # Measured over the real catalogue: 46,091 intra-tpac sibling pairs, of which
    # 93% score >= 0.60 and the median is 0.807. A geo pack is full of names that
    # look alike, so a bare threshold cannot separate "renamed" from "sibling".
    # Two guards instead: a high floor, AND a margin over the runner-up, so a
    # deletion into a crowded pack has no single obvious counterpart and stays a
    # deletion. Getting this wrong is dangerous in one direction only: it reports
    # "nothing was lost" when something was.
    RENAME_MIN = 0.90
    RENAME_MARGIN = 0.10

    def score_all(name, pool):
        return sorted(((c, difflib.SequenceMatcher(None, name, c).ratio()) for c in pool),
                      key=lambda x: -x[1])

    def confident(scored):
        """(candidate, score) only when it clears the floor AND beats the
        runner-up by the margin."""
        if not scored or scored[0][1] < RENAME_MIN:
            return None, 0.0
        if len(scored) > 1 and (scored[0][1] - scored[1][1]) < RENAME_MARGIN:
            return None, 0.0
        return scored[0]

    # Score every (gone, candidate) pair first, then assign in descending score
    # order. Iterating `gone` in dict order is greedy and lets a weaker match
    # claim a candidate the right name wanted: measured, a->b2 at 0.936 stole
    # the candidate from b->b2 at 0.979, so the report named the wrong survivor
    # AND the wrong casualty.
    proposals = []
    for k, v in gone.items():
        cand, sc = confident(score_all(k, by_tpac.get(v["tpac"], [])))
        if cand:
            proposals.append((sc, k, cand, "rename"))
            continue
        cand, sc = confident(score_all(k, by_base.get(Path(v["tpac"]).name, [])))
        if cand:
            proposals.append((sc, k, cand, "move"))
    proposals.sort(key=lambda t: -t[0])

    res = {"rename": [], "move": [], "delete": [], "new": list(added)}
    claimed, matched = set(), set()
    for sc, k, cand, kind in proposals:
        if cand in claimed or k in matched:
            continue
        claimed.add(cand)
        matched.add(k)
        # carry `referenced` through: a RENAME breaks every item naming the old
        # id exactly as hard as a DELETE does, and only DELETE used to say so.
        res[kind].append((k, [cand], gone[k]["referenced"]))
    for k, v in gone.items():
        if k not in matched:
            res["delete"].append((k, v["referenced"]))
    named = {n for _, ns, _ in res["rename"] + res["move"] for n in ns}
    res["new"] = [n for n in res["new"] if n not in named]
    # same mesh, different tpac: a pure folder reorg, harmless to item XML
    res["moved_same_name"] = [k for k in old if k in new and old[k]["tpac"] != new[k]["tpac"]]
    return res


def report_unclassified(rows) -> None:
    """The staleness gate's voice. Without it the catalogue quietly becomes a
    hand list: a new naming shape lands in `unknown` and nobody notices."""
    print(f"\nUNCLASSIFIED: {len(rows)} asset(s) resolve to unknown with no "
          f"override row. Add them to {OVERRIDES.name} with a reason.", file=sys.stderr)
    for r in rows[:15]:
        print(f"    {r['mesh']}\tculture={r['culture']}\tcategory={r['category']}"
              f"\tfolder={r['folder']}", file=sys.stderr)
    if len(rows) > 15:
        print(f"    ... and {len(rows) - 15} more", file=sys.stderr)


def main() -> int:
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", default=str(DEFAULT_GAME))
    ap.add_argument("--module", default=DEFAULT_MODULE)
    ap.add_argument("--check", action="store_true",
                    help="regenerate in memory and fail on drift; writes nothing")
    ap.add_argument("--diff", action="store_true",
                    help="rename-aware change report against the committed catalogue")
    ap.add_argument("--compare-repo", default=None,
                    help="also report the delta against a versioned copy of the module")
    args = ap.parse_args()

    game = ensure_exists(args.game, "the Bannerlord install")
    module_root = ensure_exists(game / "Modules" / args.module, f"the {args.module} module")

    # The reference side, so the catalogue can say what nothing uses.
    refs = vm.extract_refs(module_root / "ModuleData")
    referenced = {re.sub(r"\.lod\d+$", "", r.name, flags=re.IGNORECASE)
                  for r in refs if r.kind in ("visual_mesh", "collision_body")}

    rows = build_rows(module_root, referenced)
    text = render(rows, module_root)

    unclassified = [r for r in rows
                    if (r["culture"] == "unknown" or r["category"] == "unknown")
                    and r["source"] != "override"]

    if args.diff or args.check:
        old = parse_existing(OUT_TSV)
        if not old:
            print(f"No committed catalogue at {OUT_TSV}; nothing to compare.")
        else:
            new = {r["mesh"]: r for r in rows}
            ch = classify_changes(old, new)
            def flag(r):
                # A RENAME breaks every item naming the old id exactly as hard
                # as a DELETE does. Only DELETE used to say so.
                return "  <-- REFERENCED, will break" if r in ("Y", "SLIM") else ""

            def show(rows, render, limit=20):
                for row in rows[:limit]:
                    print(f"      {render(row)}")
                if len(rows) > limit:
                    print(f"      ... and {len(rows) - limit} more")

            print(f"  RENAME (same tpac, new name) : {len(ch['rename'])}")
            show(ch["rename"], lambda t: f"{t[0]} -> {', '.join(t[1])}{flag(t[2])}")
            print(f"  MOVE (same basename, new path): {len(ch['move'])}")
            show(ch["move"], lambda t: f"{t[0]} -> {', '.join(t[1])}{flag(t[2])}")
            print(f"  MOVED, same name new folder   : {len(ch['moved_same_name'])}")
            print(f"  DELETE                        : {len(ch['delete'])}")
            show(ch["delete"], lambda t: f"{t[0]}{flag(t[1])}")
            print(f"  NEW                           : {len(ch['new'])}")
            broke = ([t for t in ch["delete"] if t[1] in ("Y", "SLIM")]
                     + [t for t in ch["rename"] + ch["move"] if t[2] in ("Y", "SLIM")])
            if broke:
                print(f"\n  {len(broke)} of these are REFERENCED by an item and will "
                      f"break on the next game restart.")

    if args.compare_repo:
        other = Path(args.compare_repo)
        if (other / "Assets").exists():
            mine = {r["mesh"] for r in rows}
            theirs = {r["mesh"] for r in build_rows(other, referenced)}
            print(f"\n  live-only : {len(mine - theirs)}")
            print(f"  repo-only : {len(theirs - mine)}")
            for n in sorted(theirs - mine)[:20]:
                print(f"      {n}")
        else:
            print(f"  --compare-repo: no Assets tree at {other}", file=sys.stderr)

    if args.check:
        if not OUT_TSV.exists():
            print(f"DRIFT: {OUT_TSV} does not exist", file=sys.stderr)
            return 1
        current = OUT_TSV.read_text(encoding="utf-8").replace("\r\n", "\n")
        if current != text.replace("\r\n", "\n"):
            print(f"DRIFT: {OUT_TSV} does not reproduce; regenerate it", file=sys.stderr)
            return 1
        if unclassified:
            # Never print OK and then exit 1 with no diagnostic. The old shape
            # returned before the explanation block, so a reproducing-but-
            # unclassified catalogue said "OK" and failed silently forever.
            report_unclassified(unclassified)
            return 1
        print(f"OK: {OUT_TSV.name} reproduces exactly ({len(rows):,} rows)")
        return 0

    if not args.diff:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        OUT_TSV.write_text(text, encoding="utf-8", newline="\n")
        meta = Counter(r["kind"] for r in rows)
        print(f"Wrote {OUT_TSV.relative_to(REPO_ROOT)}: {len(rows):,} rows "
              f"({meta['metamesh']:,} metamesh, {meta['physicsshape']} physicsshape)")
        print(f"  referenced: {sum(1 for r in rows if r['referenced'] == 'Y'):,}   "
              f"slim-resolved: {sum(1 for r in rows if r['referenced'] == 'SLIM')}   "
              f"unreferenced: {sum(1 for r in rows if r['referenced'] == 'N'):,}")

    if unclassified:
        report_unclassified(unclassified)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
