#!/usr/bin/env python3
"""Re-tune the career `Health` pips from a 25-100 flat-HP spread down to a 5-10 band.

Context: the Health passive was authored against a mission-only consumer, where a hero's health
bar is the whole story, so the numbers grew to +75 / +100 on tier 3. The 2026-08-06 fix wired Health
into `TaomCharacterStatsModel.MaxHitpoints`, which makes it the CAMPAIGN max-HP too — it now moves the
character sheet, `Hero.MaxHitPoints`, the daily heal cap and the wounded threshold. Against
vanilla's flat 100 base, a +75 pip is a +75% swing; the maintainer wants 5-10 HP instead.

Mapping (maintainer-chosen "even spread" — keeps every authored rank distinction, uses all six
values, stays non-decreasing so a bigger old pip is never a smaller new one):

    25 -> 5      40 -> 7      60 ->  8
    30 -> 6      45 -> 7      75 ->  9
    35 -> 6      50 -> 8     100 -> 10

Both halves of a pip are rewritten so the text never lies about the number:
  1. the `magnitude=` / `value=` on the Health `<PassiveEffect>` (two authoring schemas coexist)
  2. the player-facing English default in `description="{=key}+75 max health."`, in BOTH
     taom_career_choices.xml and the `{=key}` source string in taom_career_strings.xml.

Description rewriting is ANCHORED on the pip's own old number followed by the health phrase, and
only inside a `<Choice>` block that actually carries a Health passive — so a "+15% horse health"
(MountHealth) or "hero health regeneration" (HeroHealing) line can never be caught by it.

The 12 translated language files ARE updated, which deliberately diverges from
retune_phantom_descriptions.py's "defer to translate_with_claude.py". That script changed the
SHAPE of the text (flat count -> percentage, so "+3 ammo" had to become "+10% ammo" and needed
re-wording per language); this one changes only a digit, and a digit is a digit in all 12
languages. Every translated health string was verified to carry exactly ONE number (1,968 of
them; the 12 exceptions are the number-free far_harad_halftroll_root prose), so the substitution
is unambiguous, preserves the hand-translated PL wording, and costs no re-translation run.

IDEMPOTENCY — read this before adding an EFFECTS profile. Re-running is a no-op, but NOT because
the per-pip swap is keyed on the old magnitude; that reasoning is wrong whenever a mapping's OLD
keys and NEW values overlap, and it shipped as a CRITICAL bug (deep review 2026-08-06). The
`troopdamage` mapping sends {0.03,0.05,0.06,0.08,0.10,0.12,0.15,0.20} to {0.02..0.08}, so four
magnitudes are simultaneously a key and a value: an already-retuned 0.05 (which came from 0.10)
is indistinguishable from a raw 0.05 (which should become 0.03), and a second --apply would have
re-shifted 71 of 105 pips across all four surfaces, silently. `already_applied()` therefore
decides at FILE level — if every pip sits on a target value and none sits on a key that is not
also a target, the retune has already run and the tool exits without touching anything.
Pinned for every profile by tools/tests/test_retune_career_health.py.

BACKUPS — this tool deliberately does NOT write .bak files, unlike sibling scripts such as
`rebalance_settlement_prosperity.py`. Those mutate the LIVE game install, which git cannot
recover; every target here is git-tracked inside the repo, so `git checkout --` is the restore
path. Deviation recorded rather than silently taken (deep review 2026-08-06, MEDIUM).

Usage:  python tools/retune_career_health.py [--effect health|troopdamage] [--dry-run | --apply]
"""
import argparse
import glob
import json
import re
import sys
from pathlib import Path

CHOICES = Path("Main/_Module/ModuleData/career_system/taom_career_choices.xml")
STRINGS = Path("Main/_Module/ModuleData/taom_career_strings.xml")
LANGUAGE_GLOB = "Main/_Module/ModuleData/Languages/*/std_taom_career_strings_*.xml"
CACHE_DIR = Path("tools/translation_cache")   # <lang>.json, lowercase of the Languages/ dir name

# ── Effect profiles ───────────────────────────────────────────────────────────
# `scale` is how the DESCRIPTION number relates to the magnitude: Health authors a flat count and
# prints it as-is (scale 1), TroopDamage authors a fraction and prints a percentage (scale 100), so
# 0.05 reads "+5% troop damage". `phrase` is the effect wording the number must be attached to —
# anchoring on it is what stops a "+15% horse health" (MountHealth) or "hero health regeneration"
# (HeroHealing) line being caught by the Health profile.
EFFECTS = {
    "health": {
        "type": "Health",
        # Maintainer-chosen 2026-08-06 ("even spread"). Flat HP; #388.
        "mapping": {25: 5, 30: 6, 35: 6, 40: 7, 45: 7, 50: 8, 60: 8, 75: 9, 100: 10},
        "phrase": r'(max\s+)?health',
        "scale": 1,
    },
    "troopdamage": {
        "type": "TroopDamage",
        # Maintainer-chosen 2026-08-06: 3-20% -> 2-8%; #388. Army-wide, so it sits BELOW the hero's
        # own Damage band (5-18%) — a troop multiplier compounds across every soldier.
        "mapping": {0.03: 0.02, 0.05: 0.03, 0.06: 0.03, 0.08: 0.04,
                    0.10: 0.05, 0.12: 0.06, 0.15: 0.07, 0.20: 0.08},
        "phrase": r'troop\s+damage',
        "scale": 100,
    },
}

# Set from the chosen profile in main(). Module-level so the pass functions stay parameter-light.
MAPPING = {}
SCALE = 1
_EFFECT_PASSIVE = None
_EFFECT_PHRASE = None


def select_effect(name):
    """Bind the module-level profile globals to one entry of EFFECTS."""
    global MAPPING, SCALE, _EFFECT_PASSIVE, _EFFECT_PHRASE
    p = EFFECTS[name]
    MAPPING = {_norm(k): v for k, v in p["mapping"].items()}
    SCALE = p["scale"]
    _EFFECT_PASSIVE = re.compile(r'<PassiveEffect\b[^>]*\btype="' + p["type"] + r'"[^>]*>')
    # "+75 max health" / "+5% troop damage" — number, optional %, then the effect wording.
    _EFFECT_PHRASE = re.compile(r'\+\s*(\d+(?:\.\d+)?)(\s*%?\s*)(' + p["phrase"] + r')', re.I)
    return p


def overlapping_values(mapping=None):
    """Magnitudes that are BOTH an old key and a new value in the active mapping.

    When this is non-empty, per-pip idempotency detection is impossible: a pip sitting on 0.05
    might be a raw un-retuned 0.05 (which should become 0.03) or an already-retuned 0.05 (which
    came from 0.10 and must be left alone). The two are indistinguishable from the value alone.
    `troopdamage` overlaps on {0.03, 0.05, 0.06, 0.08}; `health` (25-100 -> 5-10) does not overlap
    at all. This is why `already_applied` below works at FILE level rather than per pip.
    """
    m = MAPPING if mapping is None else {_norm(k): v for k, v in mapping.items()}
    return {k for k in m if _norm(k) in {_norm(v) for v in m.values()}}


def already_applied(choices_text):
    """True when every shipped pip of this effect already sits on a post-retune value.

    THE IDEMPOTENCY GUARD. Deep review 2026-08-06 found the per-pip "already retuned" check in
    `process_choices` is provably wrong whenever `overlapping_values()` is non-empty — for
    `troopdamage` a second `--apply` would have re-shifted 71 of 105 already-correct pips across
    all four surfaces, silently. File level is decidable where per-pip is not: a mapping key that
    is NOT also a target value ("unambiguously old") proves the retune has not run yet.
    """
    current = {_norm(v) for v in _magnitudes_of_effect(choices_text)}
    if not current:
        return False
    targets = {_norm(v) for v in MAPPING.values()}
    unambiguously_old = set(MAPPING) - targets
    return current <= targets and not (current & unambiguously_old)


def _magnitudes_of_effect(choices_text):
    """Every magnitude currently authored for the active effect type."""
    out = []
    for block in _CHOICE.finditer(choices_text):
        passive = _EFFECT_PASSIVE.search(block.group(0))
        if not passive:
            continue
        mv = _MAGVAL.search(passive.group(0))
        if mv:
            out.append(float(mv.group(2)))
    return out


def _norm(v):
    """Mapping-key normaliser — magnitudes are floats (0.05) or counts (25); round kills 0.1+0.2 noise."""
    return round(float(v), 4)


# A <Choice> block: self-closing, or open..close. `\b` after "Choice" keeps <ChoiceGroup> out
# (no word boundary between "Choice" and "Group").
_CHOICE = re.compile(r'<Choice\b[^>]*?/>|<Choice\b.*?</Choice>', re.S)
_MAGVAL = re.compile(r'((?:magnitude|value)=")([^"]+)(")')
_DESC = re.compile(r'(description=")([^"]*)(")')
_KEY = re.compile(r'^\{=([^}]*)\}')
_ID = re.compile(r'\bid="([^"]+)"')


def read_xml(path):
    """Byte-faithful read per the MANDATORY convention in tools/README.md.

    Binary, NOT read_text: the committed language files use `\\r\\r\\n` line endings (a pre-existing
    quirk — verify with `git show HEAD:<file> | head -c 80`). Python's universal-newline text mode
    reads that as TWO line breaks and writes it back as `\\r\\n\\r\\n`, which doubles every blank line
    and turns a 164-line edit into a 6,180-line whole-file diff. Binary in, binary out, so whatever
    the file already uses survives untouched.
    """
    raw = Path(path).read_bytes()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    return raw.decode("utf-8-sig" if had_bom else "utf-8"), had_bom


def write_xml(path, text, had_bom):
    """Byte-faithful write — the mirror of read_xml. No newline translation, BOM re-prepended."""
    Path(path).write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))


def _fmt(n):
    """Render a magnitude the way the files already write them: 5, 0.05, 0.1 — never 5.0 or 0.100."""
    return '%g' % float(n)


def _desc_num(magnitude):
    """The number as the DESCRIPTION prints it: flat for Health, percent for TroopDamage."""
    return float(magnitude) * SCALE


def rewrite_description(text, old_value, new_value):
    """Rewrite '+<old><sep><phrase>' -> '+<new><sep><phrase>', preserving any '%' and spacing.

    old_value/new_value are MAGNITUDES; the printed number is scaled per the effect profile.
    Returns (text, changed).
    """
    old_num, new_num = _desc_num(old_value), _desc_num(new_value)
    changed = [False]

    def rep(m):
        if abs(float(m.group(1)) - old_num) > 1e-9:
            return m.group(0)          # a different number — not this pip's, leave it
        changed[0] = True
        separator = m.group(2) if m.group(2).strip() else " "   # keeps "% " / " " as authored
        return f"+{_fmt(new_num)}{separator}{m.group(3)}"

    return _EFFECT_PHRASE.sub(rep, text), changed[0]


def process_choices(text):
    """Rewrite magnitudes + descriptions. Returns (new_text, edits, key_map, skipped)."""
    edits, skipped = [], []
    key_map = {}                        # loc key -> (old_value, new_value) for the strings pass
    out, cursor = [], 0

    for block in _CHOICE.finditer(text):
        passive = _EFFECT_PASSIVE.search(block.group(0))
        if not passive:
            continue

        body = block.group(0)
        choice_id = (_ID.search(body) or [None, "<unknown>"])[1] if _ID.search(body) else "<unknown>"

        mv = _MAGVAL.search(passive.group(0))
        if not mv:
            skipped.append((choice_id, "Health passive has no magnitude=/value="))
            continue

        old_value = float(mv.group(2))
        if _norm(old_value) not in MAPPING:
            # A value already in the target band is this script having run before, not an error.
            why = ("already retuned" if _norm(old_value) in {_norm(v) for v in MAPPING.values()}
                   else f"magnitude {mv.group(2)} is neither a known old value nor a retuned one")
            skipped.append((choice_id, why))
            continue
        new_value = MAPPING[_norm(old_value)]

        # 1. the magnitude on the PassiveEffect line
        new_passive = _MAGVAL.sub(
            lambda m: f"{m.group(1)}{_fmt(new_value)}{m.group(3)}", passive.group(0), count=1)
        new_body = body[:passive.start()] + new_passive + body[passive.end():]

        # 2. the English default inside description="{=key}..."
        desc = _DESC.search(new_body)
        desc_changed = False
        if desc:
            raw = desc.group(2)
            key = (_KEY.match(raw).group(1) if _KEY.match(raw) else None)
            stripped = _KEY.sub("", raw)
            new_stripped, desc_changed = rewrite_description(stripped, old_value, new_value)
            if desc_changed:
                prefix = f"{{={key}}}" if key else ""
                new_body = (new_body[:desc.start()]
                            + f'{desc.group(1)}{prefix}{new_stripped}{desc.group(3)}'
                            + new_body[desc.end():])
            if key:
                key_map[key] = (old_value, new_value)

        if not desc_changed:
            skipped.append((choice_id, "description states no number for this pip — left as-is"))

        edits.append((choice_id, old_value, new_value, desc_changed))
        out.append(text[cursor:block.start()])
        out.append(new_body)
        cursor = block.end()

    out.append(text[cursor:])
    return "".join(out), edits, key_map, skipped


def process_strings(text, key_map):
    """Rewrite the {=key} source strings whose pip changed. Returns (new_text, n_changed)."""
    n = [0]

    def line_rep(m):
        key, body = m.group(2), m.group(3)
        if key not in key_map:
            return m.group(0)
        old_value, new_value = key_map[key]
        new_body, changed = rewrite_description(body, old_value, new_value)
        if changed:
            n[0] += 1
        return f'{m.group(1)}{{={key}}}{new_body}{m.group(4)}'

    # text="{=key}....."  — key repeated inside the value, as this file writes it.
    rx = re.compile(r'(text=")\{=([^}]+)\}([^"]*)(")')
    return rx.sub(line_rep, text), n[0]


def collect_health_keys(choices_text):
    """Return {loc key -> current Health magnitude} read back from the choices XML.

    Read from the POST-rewrite text so the language pass works identically on a fresh run and a
    re-run: it only ever needs the target value, never the historical one.
    """
    keys = {}
    for block in _CHOICE.finditer(choices_text):
        passive = _EFFECT_PASSIVE.search(block.group(0))
        if not passive:
            continue
        mv = _MAGVAL.search(passive.group(0))
        desc = _DESC.search(block.group(0))
        if not (mv and desc):
            continue
        key = _KEY.match(desc.group(2))
        if key:
            keys[key.group(1)] = float(mv.group(2))
    return keys


def process_language(text, key_new):
    """Swap the single number in each translated health string to the pip's new value.

    Idempotent and language-agnostic: a string is rewritten only when its number is one of the
    OLD magnitudes that map to this key's new value, so re-running does nothing and a string
    already carrying the new number is left alone. Strings with no number (the far_harad prose)
    are untouched. Returns (new_text, n_changed, n_unexpected).
    """
    changed = unexpected = 0
    out = text

    for key, new_value in key_new.items():
        # Work in DESCRIPTION-number space: a translated string prints "+5% troop damage", not the
        # 0.05 magnitude. Every OLD value that lands on this key's NEW value is what we accept.
        new_num = _desc_num(new_value)
        stale = {_norm(_desc_num(o)) for o, n in MAPPING.items() if _norm(n) == _norm(new_value)}
        m = re.search(r'(id="' + re.escape(key) + r'"\s+text=")([^"]*)(")', out)
        if not m:
            continue
        body = m.group(2)
        nums = re.findall(r'\d+(?:\.\d+)?', body)
        if not nums:
            continue                                   # number-free prose — nothing to sync
        if _norm(nums[0]) == _norm(new_num):
            continue                                   # already synced
        if _norm(nums[0]) not in stale:
            unexpected += 1                            # not a number this retune produced
            continue
        new_body = re.sub(r'\d+(?:\.\d+)?', _fmt(new_num), body, count=1)
        out = out[:m.start()] + m.group(1) + new_body + m.group(3) + out[m.end():]
        changed += 1

    return out, changed, unexpected


def sync_cache(cache, lang_text, key_new):
    """Point each health key's CACHED translation at the language file's current text.

    Load-bearing, not housekeeping: tools/translate_with_claude.py looks the cache up by
    `string_id` alone (`elif e.string_id in cache`, line ~660) and never checks that the English
    source still matches. Leaving the old value here means the next /localize run serves
    "+75 points de vie maximum." straight back into all 12 language files and silently undoes this
    retune. Only keys ALREADY cached are touched — an absent key should be translated fresh.
    Returns the number of entries changed.
    """
    changed = 0
    for key in key_new:
        if key not in cache:
            continue
        m = re.search(r'id="' + re.escape(key) + r'"\s+text="([^"]*)"', lang_text)
        if not m:
            continue
        if cache[key] != m.group(1):
            cache[key] = m.group(1)
            changed += 1
    return changed


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", default=True)
    g.add_argument("--apply", action="store_true")
    ap.add_argument("--effect", choices=sorted(EFFECTS), default="health",
                    help="which career effect to retune (default: health)")
    args = ap.parse_args()

    profile = select_effect(args.effect)
    print(f"Effect: {args.effect}  (PassiveEffect type={profile['type']}, "
          f"description scale x{profile['scale']})\n")

    for path in (CHOICES, STRINGS):
        if not path.exists():
            print(f"ERROR: {path} not found — run from the repo root.", file=sys.stderr)
            return 1

    choices_text, choices_bom = read_xml(CHOICES)
    strings_text, strings_bom = read_xml(STRINGS)

    # Idempotency guard — MUST run before any per-pip work. See already_applied().
    overlap = overlapping_values()
    if overlap:
        print(f"  note: this mapping's old and new value sets overlap on "
              f"{sorted(_fmt(v) for v in overlap)}, so per-pip 'already retuned' detection is "
              f"impossible; using the file-level guard.")
    if already_applied(choices_text):
        print(f"ALREADY APPLIED — every shipped {profile['type']} pip is on a post-retune value. "
              f"Nothing to do.\n\nRe-running would be a double-shift, not a no-op. To retune "
              f"again, edit EFFECTS['{args.effect}']['mapping'] to map the CURRENT values.")
        return 0

    new_choices, edits, key_map, skipped = process_choices(choices_text)
    new_strings, n_strings = process_strings(strings_text, key_map)

    # Read the target values back off the rewritten choices XML so the language pass behaves the
    # same on a fresh run and a re-run (see collect_health_keys).
    key_new = collect_health_keys(new_choices)
    lang_results, cache_results = [], []
    for path in sorted(glob.glob(LANGUAGE_GLOB)):
        path = Path(path)
        lang_text, lang_bom = read_xml(path)
        new_lang, n_changed, n_unexpected = process_language(lang_text, key_new)
        lang_results.append((path, new_lang, n_changed, n_unexpected, lang_bom))

        # Cache is keyed on the language DIRECTORY name, lowercased (Languages/CNs -> cns.json).
        cache_path = CACHE_DIR / f"{path.parent.name.lower()}.json"
        if cache_path.exists():
            cache = json.loads(cache_path.read_text(encoding="utf-8"))
            n_cache = sync_cache(cache, new_lang, key_new)
            cache_results.append((cache_path, cache, n_cache))

    tally = {}
    for _, old_value, new_value, _ in edits:
        k = (_norm(old_value), _norm(new_value))
        tally[k] = tally.get(k, 0) + 1

    print(f"{profile['type']} pips found: {len(edits)}")
    # Show the magnitude AND what the description prints, since for a scaled effect they differ.
    print("\n  OLD -> NEW  (as printed)   count")
    for (old_value, new_value), count in sorted(tally.items()):
        printed = f"{_fmt(_desc_num(old_value))} -> {_fmt(_desc_num(new_value))}"
        printed += "%" if SCALE != 1 else ""
        print(f"  {_fmt(old_value):>5s} -> {_fmt(new_value):<5s} ({printed:>12s})   x{count}")
    print(f"\n  descriptions rewritten in choices XML: {sum(1 for e in edits if e[3])}")
    print(f"  source strings rewritten in strings XML: {n_strings}")

    if skipped:
        print(f"\n  skipped / no-number ({len(skipped)}):")
        for choice_id, why in skipped:
            print(f"    {choice_id}: {why}")

    total_lang = sum(r[2] for r in lang_results)
    total_odd = sum(r[3] for r in lang_results)
    print(f"\n  translated strings re-numbered: {total_lang} across {len(lang_results)} languages")
    for path, _, n_changed, n_unexpected, _bom in lang_results:
        odd = f"  ({n_unexpected} unexpected number, left alone)" if n_unexpected else ""
        print(f"    {path.parent.name:4s} {n_changed:4d}{odd}")
    if total_odd:
        print(f"  WARNING: {total_odd} translated string(s) carried a number this retune does not "
              f"recognise and were left untouched — inspect them by hand.")

    total_cache = sum(r[2] for r in cache_results)
    print(f"  translation-cache entries re-pointed: {total_cache} "
          f"across {len(cache_results)} caches "
          f"(stops the next /localize run restoring the old numbers)")

    if args.apply:
        write_xml(CHOICES, new_choices, choices_bom)
        write_xml(STRINGS, new_strings, strings_bom)
        for path, new_lang, n_changed, _, lang_bom in lang_results:
            if n_changed:
                write_xml(path, new_lang, lang_bom)
        for cache_path, cache, n_cache in cache_results:
            if n_cache:
                # Byte-for-byte the same shape as translate_with_claude.save_cache: same dump
                # options, and NO trailing newline (verified against the existing files), so the
                # only lines that move are the ones whose number actually changed.
                cache_path.write_text(
                    json.dumps(cache, ensure_ascii=False, indent=2, sort_keys=True),
                    encoding="utf-8")
        print(f"\nAPPLIED to {CHOICES}, {STRINGS}, {len(lang_results)} language files "
              f"and {sum(1 for r in cache_results if r[2])} translation caches.")
    else:
        print("\nDRY RUN — nothing written. Re-run with --apply.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
