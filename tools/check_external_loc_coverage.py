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
language, because that is what a reinstall looks like from in here. A missing language folder or
target file counts every source row as untranslated (#815); a missing TAOM_Map settlements.xml, no
Armory English loc_*.xml, or a file that does not parse exits 2, since the count would measure nothing.
--update-baseline refuses to record a count that falls from above 0 to 0 unless --allow-drop is given.

It also runs the processor check (#1026): a live TAOM_Map or LOTRLOME_Armory `language_data.xml` (root
or per language) with a `name` whose `text_processor` differs from Native's resets the language's
grammar processor, so any such file exits 1. An unreadable live file, or no Native processor to compare
against, exits 2.

Absent install is NOT a failure. The whole point of #416/#444 is that the pipeline must be
usable by contributors who have no Bannerlord install at all; this skips with exit 0 and says
why, rather than reporting perfect coverage it never measured.

Usage:
    python tools/check_external_loc_coverage.py                  # check against the baseline
    python tools/check_external_loc_coverage.py --update-baseline [--allow-drop]
    python tools/check_external_loc_coverage.py --processors-only   # the release check, no baseline
"""

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import name_tags   # noqa: E402
from _gamedir import DEFAULT_GAME_ROOT, ENV_VAR, ensure_exists, game_dir, game_modules   # noqa: E402
from validate_xml_schemas import _parse_like_engine   # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
BASELINE = REPO_ROOT / "tools" / "external-loc-coverage-baseline.json"

LANGUAGES = ["BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR"]
INLINE_KEY = re.compile(r'\w+="\{=([^}]+)\}([^"]*)"')


def _rows(path, strip_keys):
    """{key: text} for a <strings> file. Mirrors translate_with_claude._parse_string_xml.

    A missing file is {}: the caller then counts every source row as untranslated. A file that does
    not parse exits 2 naming it, because an empty read would count nothing (#815)."""
    if not path.exists():
        return {}
    try:
        tree = ET.parse(path)
    except (ET.ParseError, OSError) as exc:
        print(f"ERROR: {path} does not parse ({exc}); the coverage check cannot read it.", file=sys.stderr)
        raise SystemExit(2)
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
    # A missing target (or language folder) leaves tgt empty, so every source row counts (#815).
    settlements = modules_root / "TAOM_Map" / "ModuleData" / "settlements.xml"
    target = modules_root / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    if not settlements.exists():
        print(f"ERROR: no {settlements}; the coverage check would measure nothing for TAOM_Map.",
              file=sys.stderr)
        raise SystemExit(2)
    # Parse before the regex: a cut file still holds some names, and a count of fewer rows would pass.
    try:
        text = settlements.read_text(encoding="utf-8")
        ET.fromstring(text.lstrip("\ufeff"))
    except (ET.ParseError, OSError, ValueError) as exc:
        print(f"ERROR: {settlements} does not parse ({exc}); the coverage check cannot read it.",
              file=sys.stderr)
        raise SystemExit(2)
    src = {}
    for key, default in INLINE_KEY.findall(text):
        src.setdefault(key, default)
    tgt = _rows(target, strip_keys=False)
    # A name that is still English stays untranslated once it carries a grammar tag (#1023).
    map_count = sum(1 for k, v in src.items() if name_tags.strip_leading_tags(tgt.get(k, v)) == v)

    # Armory: root loc_*.xml are the English side, the per-language dir mirrors them.
    armory_root = modules_root / "LOTRLOME_Armory" / "ModuleData" / "Languages"
    sources = sorted(armory_root.glob("loc_*.xml"))
    if not sources:
        print(f"ERROR: no loc_*.xml under {armory_root}; the coverage check would measure nothing for "
              "the Armory.", file=sys.stderr)
        raise SystemExit(2)
    armory_count = 0
    for src_file in sources:
        src = _rows(src_file, strip_keys=True)
        tgt = _rows(armory_root / lang / src_file.name, strip_keys=False)
        armory_count += sum(1 for k, v in src.items() if tgt.get(k, v) == v)
    return map_count, armory_count


def native_modules_root():
    """Native is always read from the real install, even when BANNERLORD_GAME_MODULES points at a
    release channel tree, as package_release.py --game-root does."""
    return Path(game_dir(DEFAULT_GAME_ROOT)) / "Modules"


def _language_data_roots(langs_dir):
    """(path, root) for every language_data.xml LocalizedTextManager loads from a Languages folder
    (GetFiles, AllDirectories), read as its LoadXmlFile reads it (declared encoding ignored). A file
    that does not parse exits 2 naming it, never a skip."""
    for path in sorted(langs_dir.rglob("language_data.xml")):
        try:
            yield path, _parse_like_engine(path)
        except (ET.ParseError, OSError, ValueError, LookupError) as exc:
            print(f"ERROR: {path} does not parse ({exc}); the processor check cannot read it.",
                  file=sys.stderr)
            raise SystemExit(2)


def native_processors(modules_root):
    """{language id: text_processor} for every Native language_data.xml that declares one."""
    return {root.get("id"): root.get("text_processor")
            for _, root in _language_data_roots(modules_root / "Native" / "ModuleData" / "Languages")
            if root.get("id") and root.get("text_processor")}


def processor_mismatches(modules_root, native_root=None):
    """Live TAOM_Map / LOTRLOME_Armory language_data.xml files with a `name` and a text_processor that
    differs from Native's, including one where Native declares none (#1026). Exits 2 when it reads no
    language_data.xml under the two modules (an empty channel would otherwise pass).

    LanguageData.Deserialize assigns TextProcessor whenever `name` is non-empty, so such a file resets
    the language to the default processor and every grammar token ({.di}, {.cl}, '{.e}) prints nothing.
    A file without a `name` sets nothing and is ignored. TAOM.Dependencies is not checked: its
    install-only files carry a `name` and no processor, and are safe only because its SubModule.xml
    lists Native in ModulesToLoadAfterThis, so it always sorts before Native. Returns readable lines.
    """
    native = native_processors(native_root or modules_root)
    if not native:
        print(f"ERROR: no Native text_processor under {native_root or modules_root}; the processor "
              "check would check nothing.", file=sys.stderr)
        raise SystemExit(2)
    bad, read = [], 0
    for module in ("TAOM_Map", "LOTRLOME_Armory"):
        langs = modules_root / module / "ModuleData" / "Languages"
        for path, root in _language_data_roots(langs):
            read += 1
            want = native.get(root.get("id"))
            if root.get("name") and root.get("text_processor") != want:
                where = path.parent.relative_to(langs).as_posix()
                declares = f"Native declares {want}" if want else "Native declares none"
                bad.append(f"  {module}/{'(root)' if where == '.' else where}: name={root.get('name')!r} "
                           f"text_processor={root.get('text_processor')!r}, {declares}")
    if not read:
        print(f"ERROR: no language_data.xml under {modules_root} (TAOM_Map or LOTRLOME_Armory); the "
              "processor check would check nothing.", file=sys.stderr)
        raise SystemExit(2)
    processor_mismatches.files_read = read
    return bad


def report_mismatches(bad):
    """Print the FAIL block for processor_mismatches' lines."""
    print("FAIL: live language_data.xml files reset the language's text processor (#1026).",
          file=sys.stderr)
    print("\n".join(bad), file=sys.stderr)
    print("\nA file with a `name` must carry exactly Native's `text_processor` (none where Native "
          "declares none). Fix each file listed.", file=sys.stderr)


def processors_only():
    """The release check: only the processor check, on the Modules folder named by
    BANNERLORD_GAME_MODULES (a channel tree) or the install, against the real Native."""
    modules = ensure_exists(game_modules(DEFAULT_GAME_ROOT), "the Modules folder to check")
    bad = processor_mismatches(modules, native_modules_root())
    if bad:
        report_mismatches(bad)
        return 1
    print(f"PASS: {processor_mismatches.files_read} language_data.xml files under {modules} "
          "carry Native's text processor.")
    return 0


def tag_report(modules_root, lang, table):
    """How many of the table's names the live TAOM_Map loc_settlements.xml holds with the right tag. Read only.

    Returns None for a language without tags or a missing file; else a dict with `expected` (table ids the
    file holds), `correct`, `missing` (no leading tag), `wrong` (a different leading tag) and `orphan` (rows that
    start with a tag the table does not call for).
    """
    if lang not in table["languages"]:
        return None
    target = modules_root / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    if not target.exists():
        return None
    rows = _rows(target, strip_keys=False)
    report = {"expected": 0, "correct": 0, "missing": [], "wrong": [], "orphan": 0}
    for sid, text in rows.items():
        tag = name_tags.tag_for(lang, sid, table)
        if not tag:
            if name_tags.LEADING_TAGS.match(text):
                report["orphan"] += 1
            continue
        report["expected"] += 1
        have = name_tags.LEADING_TAGS.match(text)
        if have and name_tags.leads_with_article(lang, text, table):
            report["wrong"].append(sid)     # a tag on top of an article prints it twice
        elif have and have.group(0) == tag:
            report["correct"] += 1
        elif have:
            report["wrong"].append(sid)
        else:
            report["missing"].append(sid)
    return report


def format_tag_report(lang, report):
    state = ("complete" if report["correct"] == report["expected"]
             else "report only until the live file is applied")
    return (f"  {lang}: {report['correct']}/{report['expected']} live settlement names carry their grammar tag "
            f"({len(report['missing'])} missing, {len(report['wrong'])} wrong, {report['orphan']} orphan): {state}")


def tags_errors(reports):
    """Messages for a report that cannot be trusted: no live file, or no table id in it (exit 2 under --require-tags)."""
    return [f"{lang}: " + ("no live loc_settlements.xml to check" if r is None else "the live file holds none of the table's names")
            for lang, r in reports.items() if r is None or r["expected"] == 0]


def tags_failure(reports):
    """Messages for a language whose live names lack their tag or carry one the table does not call for."""
    out = [f"{lang}: {r['expected'] - r['correct']} live name(s) lack their grammar tag"
           for lang, r in reports.items() if r and r["correct"] != r["expected"]]
    out += [f"{lang}: {r['orphan']} live row(s) start with a grammar tag the table does not call for"
            for lang, r in reports.items() if r and r["orphan"]]
    return out


def measure():
    modules = game_modules(DEFAULT_GAME_ROOT)
    if not modules.exists():
        return None, modules
    return ({lang: dict(zip(("taom_map", "armory"), untranslated(modules, lang)))
             for lang in LANGUAGES}, modules)


def drops_to_zero(baseline, current):
    """Lines for each count that falls from above 0 to 0: a reinstall that empties the tree reads that
    way, so --update-baseline records it only with --allow-drop (#815)."""
    return [f"  {lang}/{module}: {baseline[lang][module]} -> 0"
            for lang in LANGUAGES for module in ("taom_map", "armory")
            if (baseline.get(lang, {}).get(module) or 0) > 0 and current[lang][module] == 0]


# The translator's --module value for each counted module, for the re-apply hint.
TRANSLATE_MODULE = {"taom_map": "TAOM_Map", "armory": "Armory"}


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--update-baseline", action="store_true",
                   help="record current coverage as the new baseline")
    p.add_argument("--allow-drop", action="store_true",
                   help="with --update-baseline, also record a count that falls from above 0 to 0")
    p.add_argument("--processors-only", action="store_true",
                   help="run only the text-processor check (exit 0 clean, 1 mismatch, 2 not found); "
                        "reads no baseline")
    p.add_argument("--require-tags", action="store_true",
                   help="fail when the live loc_settlements.xml lacks a grammar tag (default: report only)")
    args = p.parse_args(argv)

    if args.processors_only:
        return processors_only()

    current, modules = measure()
    if current is None:
        print(f"SKIP: no Bannerlord install at {modules} — set ${ENV_VAR} to check "
              f"TAOM_Map / Armory coverage. Not a failure: these modules are not in this repo. "
              f"With an install, a processor mismatch exits 1 and an unreadable file or no Native exits 2.")
        return 0

    if args.update_baseline:
        if BASELINE.exists() and not args.allow_drop:
            try:
                old = json.loads(BASELINE.read_text(encoding="utf-8"))
            except ValueError as exc:
                print(f"ERROR: {BASELINE.name} is not valid JSON ({exc}); resolve it (a merge conflict?), "
                      "then re-run. Nothing written.", file=sys.stderr)
                return 2
            drops = drops_to_zero(old, current)
            if drops:
                print("REFUSING: these counts fall to 0, which is what a tree the check cannot read "
                      "looks like:", file=sys.stderr)
                print("\n".join(drops), file=sys.stderr)
                print("Check the live files, then re-run with --allow-drop to record it. Nothing written.",
                      file=sys.stderr)
                return 1
        BASELINE.write_text(json.dumps(current, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
        total = sum(v["taom_map"] + v["armory"] for v in current.values())
        print(f"Baseline written: {BASELINE.name} "
              f"({total} untranslated entries across {len(current)} languages)")
        return 0

    bad = processor_mismatches(modules, native_modules_root())
    if bad:
        report_mismatches(bad)

    if not BASELINE.exists():
        print(f"ERROR: no baseline at {BASELINE.relative_to(REPO_ROOT)} — "
              f"run with --update-baseline once to record the current state.", file=sys.stderr)
        return 1 if bad else 2

    table = name_tags.load_table()
    reports = {lang: tag_report(modules, lang, table) for lang in name_tags.TAGGED_LANGUAGES}
    print("Name grammar tags in the live TAOM_Map loc_settlements.xml (tools/translation_name_tags.json):")
    for lang, report in reports.items():
        print(format_tag_report(lang, report) if report else f"  {lang}: no live loc_settlements.xml to check")
    if args.require_tags:
        errors = tags_errors(reports)
        if errors:
            print("\nERROR: --require-tags cannot judge the live file.", file=sys.stderr)
            print("\n".join(f"  {m}" for m in errors), file=sys.stderr)
            return 2
    tag_failures = tags_failure(reports) if args.require_tags else []

    baseline = json.loads(BASELINE.read_text(encoding="utf-8"))
    regressions, improvements, regressed_modules = [], [], set()
    for lang in LANGUAGES:
        for module in ("taom_map", "armory"):
            was = baseline.get(lang, {}).get(module)
            now = current[lang][module]
            if was is None:
                continue
            if now > was:
                regressions.append(f"  {lang}/{module}: {was} -> {now} untranslated "
                                   f"(+{now - was})")
                regressed_modules.add(module)
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
              "reinstall silently reverts them. Re-apply with:", file=sys.stderr)
        for module in ("taom_map", "armory"):
            if module in regressed_modules:
                print(f"  python tools/translate_with_claude.py --lang <L> --module {TRANSLATE_MODULE[module]} "
                      "--apply --allow-live-write", file=sys.stderr)
        if "taom_map" in regressed_modules:
            print("then re-tag the BR and IT names with:\n"
                  "  python tools/name_tags.py --target live-map --apply --allow-live-write", file=sys.stderr)

    if regressions or bad:
        return 1

    if tag_failures:
        print("\nFAIL: grammar tags are missing from the live file.", file=sys.stderr)
        print("\n".join(f"  {m}" for m in tag_failures), file=sys.stderr)
        return 1

    total = sum(v["taom_map"] + v["armory"] for v in current.values())
    print(f"PASS: external-module coverage holds at the baseline "
          f"({total} untranslated across {len(LANGUAGES)} languages); the processor check ran.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
