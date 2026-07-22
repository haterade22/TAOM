#!/usr/bin/env python3
"""Sync the six phantom-type pip DESCRIPTIONS to their re-tuned magnitudes (English only).

Companion to retune_phantom_passives.py. That script changed magnitudes to a uniform 10/13/15%
band; this one fixes the player-facing description text that still states the OLD numbers, and
converts the now-multiplicative MountHealth/Ammo descriptions from flat counts to percentages.

TYPE-PHRASE-ANCHORED so it never touches an ability-mutation number: it only rewrites a number
that is directly attached to the passive's own effect phrase (e.g. "+5% horse charge damage" or
"+3 ammo"). A keystone description like "...Sneak-Creeping radius +5." has no such phrase, so it
is left untouched (and reported).

Updates BOTH the inline description= defaults in taom_career_choices.xml and the {=key} source
strings in taom_career_strings.xml. The 11 AI-translated language files are NOT touched — re-run
tools/translate_with_claude.py to propagate (deferred per the maintainer's choice).

Usage:  python tools/retune_phantom_descriptions.py [--dry-run | --apply]
"""
import argparse
import re
import sys
from pathlib import Path

CHOICES = Path("Main/_Module/ModuleData/career_system/taom_career_choices.xml")
STRINGS = Path("Main/_Module/ModuleData/taom_career_strings.xml")

# type -> (effect phrase as it appears in descriptions). All six now render as a percentage.
PHRASES = {
    "MountChargeDamage": "horse charge damage",
    "MountHealth": "horse health",
    "StealthBonus": "stealth bonus",
    "TroopResistance": "troop resistance",
    "HeroHealing": "hero health regeneration",
    "Ammo": "ammo",
}

_PASSIVE = re.compile(r'<PassiveEffect\b[^>]*type="([^"]+)"')
_MAGVAL = re.compile(r'(?:magnitude|value)="([^"]+)"')
_DESC = re.compile(r'description="([^"]*)"')
_KEY = re.compile(r'^\{=([^}]*)\}')


def pct_for(mag_str):
    return str(int(round(float(mag_str) * 100)))


def rewrite_text(text, phrase, pct):
    """Rewrite the +N[%] <phrase> number to pct% (force % — all six are now multiplicative).

    Returns (new_text, changed). Anchored on the phrase so unrelated numbers are never touched.
    """
    # +N , optional decimals, optional %, optional separator, then the phrase. Normalize the whole
    # match to "+{pct}% {phrase}" (single space) so flat "+3 ammo" -> "+10% ammo" too.
    rx = re.compile(r'\+\s*\d+(?:\.\d+)?\s*%?\s*' + re.escape(phrase), re.IGNORECASE)
    new, n = rx.subn(f"+{pct}% {phrase}", text)
    return new, n > 0


def collect_edits():
    """Return (key->new_text, list of (line_index, old_desc, new_desc), skipped[])."""
    lines = CHOICES.read_text(encoding="utf-8").splitlines()
    last_desc, last_desc_line = None, None
    key_to_new = {}
    line_edits = []
    skipped = []

    for i, ln in enumerate(lines):
        d = _DESC.search(ln)
        if d:
            last_desc, last_desc_line = d.group(1), i
        pe = _PASSIVE.search(ln)
        if not pe or pe.group(1) not in PHRASES:
            continue
        mag = _MAGVAL.search(ln)
        if not mag or last_desc is None:
            continue
        pct = pct_for(mag.group(1))
        phrase = PHRASES[pe.group(1)]

        km = _KEY.match(last_desc)
        key = km.group(1) if km else None
        body = last_desc[km.end():] if km else last_desc

        new_body, changed = rewrite_text(body, phrase, pct)
        if not changed:
            skipped.append((key, pe.group(1), body))
            continue
        new_desc = (f"{{={key}}}" if key else "") + new_body
        if new_desc != last_desc:
            line_edits.append((last_desc_line, last_desc, new_desc))
            if key:
                key_to_new[key] = new_body

    return key_to_new, line_edits, skipped, lines


def apply_choice_edits(lines, line_edits):
    for idx, old, new in line_edits:
        lines[idx] = lines[idx].replace(f'description="{old}"', f'description="{new}"')
    return "\n".join(lines) + "\n"


def apply_strings_edits(key_to_new):
    text = STRINGS.read_text(encoding="utf-8")
    out = text
    missing = []
    for key, new_body in key_to_new.items():
        # <string id="KEY" text="{=KEY}OLD" />  ->  rewrite the body after {=KEY}
        rx = re.compile(r'(<string id="' + re.escape(key) + r'" text="\{=' + re.escape(key) + r'\})[^"]*(" ?/>)')
        new_out, n = rx.subn(lambda m: m.group(1) + new_body.replace('\\', '\\\\') + m.group(2), out)
        if n == 0:
            missing.append(key)
        else:
            out = new_out
    return out, missing


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    if not CHOICES.exists() or not STRINGS.exists():
        print("ERROR: run from repo root (choices/strings file not found)", file=sys.stderr)
        return 2

    key_to_new, line_edits, skipped, lines = collect_edits()
    new_strings, missing = apply_strings_edits(key_to_new)

    print(f"{'APPLY' if args.apply else 'DRY-RUN'}: {len(line_edits)} inline descriptions + "
          f"{len(key_to_new)} string keys to rewrite")
    for idx, old, new in line_edits[:12]:
        ob = re.sub(r'^\{=[^}]*\}', '', old)
        nb = re.sub(r'^\{=[^}]*\}', '', new)
        print(f"  L{idx+1}: {ob!r} -> {nb!r}")
    if len(line_edits) > 12:
        print(f"  ... and {len(line_edits) - 12} more")
    if skipped:
        print(f"\nSKIPPED {len(skipped)} phantom pip(s) whose description has no passive-phrase "
              f"(ability-mutation text — left untouched):")
        for key, t, body in skipped[:10]:
            print(f"  [{t}] {key}: {body!r}")
        if len(skipped) > 10:
            print(f"  ... and {len(skipped) - 10} more")
    if missing:
        print(f"\nWARN {len(missing)} key(s) not found in taom_career_strings.xml (inline only): "
              + ", ".join(missing[:10]))

    if args.apply:
        CHOICES.write_text(apply_choice_edits(lines, line_edits), encoding="utf-8")
        STRINGS.write_text(new_strings, encoding="utf-8")
        print("\nWROTE taom_career_choices.xml + taom_career_strings.xml")
        print("NOTE: re-run tools/translate_with_claude.py to propagate to the 11 AI languages.")
    else:
        print("\n(dry-run — pass --apply to write)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
