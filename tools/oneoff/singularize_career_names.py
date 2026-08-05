#!/usr/bin/env python3
"""Singularize the head noun of each career group/rank name (in-game review fix:
single-player career, so titles are singular -- "Warden of the East Bank", not
"Wardens"). Edits tools/career_group_names.json + tools/career_rank_names.json in
place. Dry-run by default; --apply to write.

Rule: the title's head noun is the last word of the segment BEFORE " of " (or the
last word if there is no " of "). Adjectives and the "of ..." object tail are left
alone, so "Lords of the Dome of Stars" -> "Lord of the Dome of Stars" (Stars kept).
Guards: never touch a word ending in 's/ss/us/is (possessive/non-plural like
"Aegis", "Sauron's") or a verb in STOP; -men -> -man; otherwise drop a trailing s.
OVERRIDES handle irregulars the rule would mangle (e.g. "Hooves")."""
import argparse, json, os, re

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GROUP = os.path.join(REPO, "tools", "career_group_names.json")
RANK = os.path.join(REPO, "tools", "career_rank_names.json")

# Last-word-of-head that must NOT be de-pluralized (verbs / not agent plurals).
STOP = {"Falls"}
# Full-name overrides where the head-noun rule would produce an awkward word.
OVERRIDES = {
    "Hooves of Asfaloth": "Gallop of Asfaloth",  # Asfaloth = Glorfindel's steed; mount-speed path
}


def singularize_word(w):
    if "'" in w:                       # possessive (Sauron's, Bolg's)
        return w
    if w in STOP:
        return w
    if w.endswith("men"):              # Bowmen->Bowman, Horsemen->Horseman, Shieldmen->Shieldman
        return w[:-3] + "man"
    if w.endswith(("ss", "us", "is")): # Aegis, ...ness  -- not plurals
        return w
    if w.endswith("s") and len(w) > 3:
        return w[:-1]
    return w


def singularize_name(name):
    if name in OVERRIDES:
        return OVERRIDES[name]
    # split off the tail at the FIRST preposition so only the title head noun is
    # singularized; objects of prepositions (Stars, Trees, Sands, ...) are preserved.
    m = re.search(r"\s+(?:of|under|at|in|on|from|to|upon|beneath)\s+", name)
    head = name[: m.start()] if m else name
    tail = name[m.start():] if m else ""
    words = head.split()
    if not words:
        return name
    words[-1] = singularize_word(words[-1])
    return " ".join(words) + tail


def process(path, dry):
    data = json.load(open(path, encoding="utf-8"))
    changes = []
    for k, v in data.items():
        if k.startswith("_"):
            continue
        if "name" in v:                       # group file
            new = singularize_name(v["name"])
            if new != v["name"]:
                changes.append((k, v["name"], new)); v["name"] = new
        else:                                  # rank file
            for rk in ("rank1", "rank2", "rank3"):
                new = singularize_name(v[rk]["name"])
                if new != v[rk]["name"]:
                    changes.append((k + "/" + rk, v[rk]["name"], new)); v[rk]["name"] = new
    if not dry:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
    return changes


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", default=True)
    g.add_argument("--apply", dest="dry_run", action="store_false")
    args = ap.parse_args()
    total = 0
    for path in (GROUP, RANK):
        ch = process(path, args.dry_run)
        total += len(ch)
        print("\n=== %s : %d change(s) ===" % (os.path.basename(path), len(ch)))
        for key, old, new in ch:
            print("  %-44s %s  ->  %s" % (key, old, new))
    print("\nTOTAL: %d changes%s" % (total, "" if not args.dry_run else "  [DRY-RUN, no write]"))


if __name__ == "__main__":
    main()
