#!/usr/bin/env python3
"""
Add faction_banner_key to each LOTR bandit Culture in taom_spcultures.xml, sourced
from the matching bandit Clan's banner_key in characters/clans.xml (culture id == clan id
for every bandit faction). Vanilla bandit cultures omit faction_banner_key, but the
settled cultures all carry it and the map-UI banner can fall back to it — so the bandit
hideouts render their parent-kingdom heraldry at culture level too.

Idempotent: skips a culture that already has faction_banner_key.

Usage:
    python add_bandit_faction_banner_keys.py --dry-run
    python add_bandit_faction_banner_keys.py --apply
"""
from __future__ import annotations
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CLANS = ROOT / "Main" / "_Module" / "ModuleData" / "characters" / "clans.xml"
CULTURES = ROOT / "Main" / "_Module" / "ModuleData" / "taom_spcultures.xml"

BANDIT_IDS = [
    "dunland_raiders", "rhun_raiders", "harad_raiders", "gundabad_raiders",
    "umbar_corsairs", "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers",
]


def clan_banner_keys(text: str) -> dict[str, str]:
    keys = {}
    for sid in BANDIT_IDS:
        # <Faction id="sid" ... banner_key="..." ...>  (attributes across lines)
        m = re.search(
            r'<Faction\b[^>]*?id="' + re.escape(sid) + r'"[^>]*?banner_key="([^"]+)"',
            text, re.DOTALL,
        )
        if not m:
            # try id after banner_key ordering too
            m = re.search(
                r'<Faction\b(?=[^>]*id="' + re.escape(sid) + r'")[^>]*?banner_key="([^"]+)"',
                text, re.DOTALL,
            )
        if m:
            keys[sid] = m.group(1)
    return keys


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if not (args.dry_run or args.apply):
        ap.error("pass --dry-run or --apply")

    clans_text = CLANS.read_text(encoding="utf-8-sig")
    keys = clan_banner_keys(clans_text)
    missing = [s for s in BANDIT_IDS if s not in keys]
    if missing:
        sys.exit(f"ERROR: no clan banner_key found for: {', '.join(missing)}")

    text = CULTURES.read_text(encoding="utf-8-sig")
    had_bom = CULTURES.read_bytes().startswith(b"\xef\xbb\xbf")
    added, skipped = [], []

    for sid, key in keys.items():
        # Anchor on the culture's unique bandit_boss_party_template line; insert before it.
        anchor = f'bandit_boss_party_template="PartyTemplate.{sid}_boss_party_template">'
        if anchor not in text:
            sys.exit(f"ERROR: culture anchor not found for {sid}")
        # already has faction_banner_key in this culture block? check the slice just before anchor
        pre = text[:text.index(anchor)]
        block_start = pre.rfind('<Culture')
        if 'faction_banner_key=' in text[block_start:text.index(anchor)]:
            skipped.append(sid)
            continue
        repl = f'faction_banner_key="{key}"\n    {anchor}'
        text = text.replace(anchor, repl, 1)
        added.append(sid)

    print(f"faction_banner_key added: {len(added)}  skipped(existing): {len(skipped)}")
    for s in added:
        print(f"  + {s}: {keys[s][:48]}...")

    if args.dry_run:
        print("[DRY RUN] no write")
        return 0
    CULTURES.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))
    print(f"[APPLY] wrote {CULTURES}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
