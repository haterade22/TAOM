#!/usr/bin/env python3
"""
One-off: correct the party-size bonus NUMBER in the 13 wrapped-schema career-choice
descriptions across all 12 language files + 11 translation caches.

Context: the `<PassiveEffects>`-wrapped career choices were previously dead (parser
ignored them). Activating them (CareerConfigProvider now reads the wrapper + value=
alias) plus the surgical decision to treat PartySize as a FLAT count means these
descriptions must read "+N party size", not "+X% party size". The English source
(taom_career_strings.xml) and inline defaults (taom_career_choices.xml) are already
fixed; this script propagates the NUMBER change to the per-language files.

This is a numeral+symbol correction, NOT a re-translation: it preserves each
language's existing phrasing of "party size" and only swaps the bonus token
(handles "+12%", Turkish "+%12", and trailing placement). The AI translation tool
(translate_with_claude.py) cannot do this — its diff logic skips already-translated
entries — and requires ANTHROPIC_API_KEY (unset here). A proper linguistic polish
pass (percentage-shaped grammar -> flat) is left for a future API-backed run.

Usage:
    python tools/fix_wrapped_partysize_translations.py --dry-run
    python tools/fix_wrapped_partysize_translations.py --apply
"""

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LANG_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages"
CACHE_DIR = REPO_ROOT / "tools" / "translation_cache"

# 13 wrapped-schema PartySize description keys, grouped by their new flat value.
KEYS_TO_5 = [
    "taom_buc_t3_b_p4_desc", "taom_mc_t3_b_p4_desc", "taom_sr_t3_b_p3_desc",
    "taom_ohw_t3_a_p3_desc", "taom_moa_t3_b_p4_desc", "taom_ew_t3_a_p3_desc",
    "taom_ws_t3_b_p4_desc", "taom_cr_t3_a_p3_desc",
]
KEYS_TO_6 = [
    "taom_ohw_t3_b_p4_desc", "taom_ew_t3_b_p4_desc", "taom_alr_t3_b_p4_desc",
    "taom_wh_t3_b_p4_desc", "taom_cr_t3_b_p2_desc",
]

# Bonus-token variants -> flat replacement. Order: try both "%-after" and Turkish "%-before".
REPL_5 = [("+12%", "+5"), ("+%12", "+5")]
REPL_6 = [("+14%", "+6"), ("+%14", "+6")]


def replacement_for(key):
    if key in KEYS_TO_5:
        return REPL_5
    if key in KEYS_TO_6:
        return REPL_6
    return None


def process_file(path: Path, apply: bool):
    """Line-scoped fix: a line containing a target key id gets its bonus token swapped.
    Byte-faithful: preserves BOM and per-line newline style."""
    raw = path.read_bytes()
    has_bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig") if has_bom else raw.decode("utf-8")

    lines = text.splitlines(keepends=True)
    changed = 0
    seen_keys = set()
    for i, line in enumerate(lines):
        for key in KEYS_TO_5 + KEYS_TO_6:
            # Match the key as a token so e.g. taom_cr_t3_a_p3_desc doesn't match a longer id.
            if re.search(r'(["\s])' + re.escape(key) + r'(["\s:])', line):
                seen_keys.add(key)
                new_line = line
                for old, new in replacement_for(key):
                    new_line = new_line.replace(old, new)
                if new_line != line:
                    lines[i] = new_line
                    changed += 1
                break

    missing = [k for k in (KEYS_TO_5 + KEYS_TO_6) if k not in seen_keys]
    status = f"{path.relative_to(REPO_ROOT)}: {changed} line(s) changed"
    if missing:
        status += f"  [WARN: {len(missing)} key(s) not found: {', '.join(missing)}]"
    print(status)

    if apply and changed:
        out = "".join(lines)
        # Write BOM as bytes (not a U+FEFF string literal) per tools/README.md XML I/O convention.
        data = (b"\xef\xbb\xbf" if has_bom else b"") + out.encode("utf-8")
        path.write_bytes(data)
    return changed


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    apply = args.apply and not args.dry_run
    if not apply:
        print("== DRY RUN (no writes) ==")

    total = 0
    lang_files = sorted(LANG_DIR.glob("*/std_taom_career_strings_*.xml"))
    cache_files = sorted(CACHE_DIR.glob("*.json"))
    if not lang_files:
        print("ERROR: no language files found", file=sys.stderr)
        return 1

    print(f"\n-- {len(lang_files)} language file(s) --")
    for f in lang_files:
        total += process_file(f, apply)
    print(f"\n-- {len(cache_files)} cache file(s) --")
    for f in cache_files:
        total += process_file(f, apply)

    print(f"\nTotal line changes: {total}  (expected 13/file: 156 lang + 143 cache = 299)")
    if not apply:
        print("Re-run with --apply to write.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
