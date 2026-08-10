#!/usr/bin/env python3
"""
Ratchet on TAOM_Map / LOTRLOME_Armory translation coverage.

Those two modules live in the GAME INSTALL, not this repo, so nothing here tracks their
`loc_*.xml`. That makes their translations quietly reversible: a module reinstall drops the
shipped English files back over the translated ones and no diff, no test and no build failure
says so. CLAUDE.md's rule for the whole class is "always land an in-repo validator gate
alongside the external edit" -- this is that gate for the localization half.

It measures what the translator would consider untranslated (target text still byte-identical
to its English source) and compares against a checked-in baseline. Coverage may improve freely;
a REGRESSION -- more untranslated entries than the baseline records -- exits 1 and names the
language, because that is what a reinstall looks like from in here.

Absent install is NOT a failure. The whole point of #416/#444 is that the pipeline must be
usable by contributors who have no Bannerlord install at all; this skips with exit 0 and says
why, rather than reporting perfect coverage it never measured.

Usage:
    python tools/check_external_loc_coverage.py                  # check against the baseline
    python tools/check_external_loc_coverage.py --update-baseline
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _gamedir import ENV_VAR, game_modules   # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
BASELINE = REPO_ROOT / "tools" / "external-loc-coverage-baseline.json"

LANGUAGES = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
INLINE_KEY = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')


def _rows(path, strip_keys):
    """{key: text} for a <strings> file. Mirrors translate_with_claude._parse_string_xml."""
    if not path.exists():
        return {}
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return {}
    out = {}
    for el in tree.iter("string"):
        sid, text = el.get("id", ""), el.get("text", "") or ""
        if strip_keys:
            m = re.match(r"^\{=([^}]+)\}(.*)$", text, re.DOTALL)
            if m:
                sid, text = m.group(1), m.group(2)
        if sid:
            out[sid] = text
    return out


def untranslated(modules_root, lang):
    """(taom_map_count, armory_count) of entries whose target text is still the English."""
    # TAOM_Map: settlements.xml carries inline {=KEY}default; the target mirrors it.
    settlements = modules_root / "TAOM_Map" / "ModuleData" / "settlements.xml"
    target = modules_root / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    map_count = 0
    if settlements.exists() and target.exists():
        src = {}
        for key, default in INLINE_KEY.findall(settlements.read_text(encoding="utf-8")):
            src.setdefault(key, default)
        tgt = _rows(target, strip_keys=False)
        map_count = sum(1 for k, v in src.items() if tgt.get(k, v) == v)

    # Armory: root loc_*.xml are the English side, the per-language dir mirrors them.
    armory_root = modules_root / "LOTRLOME_Armory" / "ModuleData" / "Languages"
    armory_count = 0
    if (armory_root / lang).is_dir():
        for src_file in armory_root.glob("loc_*.xml"):
            src = _rows(src_file, strip_keys=True)
            tgt = _rows(armory_root / lang / src_file.name, strip_keys=False)
            armory_count += sum(1 for k, v in src.items() if tgt.get(k, v) == v)
    return map_count, armory_count


def measure():
    modules = game_modules(DEFAULT_GAME_ROOT)
    if not modules.exists():
        return None, modules
    return ({lang: dict(zip(("taom_map", "armory"), untranslated(modules, lang)))
             for lang in LANGUAGES}, modules)


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--update-baseline", action="store_true",
                   help="record current coverage as the new baseline")
    args = p.parse_args()

    current, modules = measure()
    if current is None:
        print(f"SKIP: no Bannerlord install at {modules} — set ${ENV_VAR} to check "
              f"TAOM_Map / Armory coverage. Not a failure: these modules are not in this repo.")
        return 0

    if args.update_baseline:
        BASELINE.write_text(json.dumps(current, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
        total = sum(v["taom_map"] + v["armory"] for v in current.values())
        print(f"Baseline written: {BASELINE.relative_to(REPO_ROOT)} "
              f"({total} untranslated entries across {len(current)} languages)")
        return 0

    if not BASELINE.exists():
        print(f"ERROR: no baseline at {BASELINE.relative_to(REPO_ROOT)} — "
              f"run with --update-baseline once to record the current state.", file=sys.stderr)
        return 2

    baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
    regressions, improvements = [], []
    for lang in LANGUAGES:
        for module in ("taom_map", "armory"):
            was = baseline.get(lang, {}).get(module)
            now = current[lang][module]
            if was is None:
                continue
            if now > was:
                regressions.append(f"  {lang}/{module}: {was} -> {now} untranslated "
                                   f"(+{now - was})")
            elif now < was:
                improvements.append(f"  {lang}/{module}: {was} -> {now} (-{was - now})")

    if improvements:
        print("Coverage improved:")
        print("\n".join(improvements))
        print("  Re-run with --update-baseline to lock it in.")

    if regressions:
        print("\nFAIL: external-module translations went backwards.", file=sys.stderr)
        print("\n".join(regressions), file=sys.stderr)
        print("\nThese files live in the game install and are not tracked here, so a module "
              "reinstall silently reverts them. Re-apply with:\n"
              "  python tools/translate_with_claude.py --lang <L> --module TAOM_Map --apply",
              file=sys.stderr)
        return 1

    total = sum(v["taom_map"] + v["armory"] for v in current.values())
    print(f"PASS: external-module coverage holds at the baseline "
          f"({total} untranslated across {len(LANGUAGES)} languages).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
