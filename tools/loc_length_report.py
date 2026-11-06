#!/usr/bin/env python3
"""Report translated text that may not fit its widget (#1007).

TAOM ships AI-drafted text in 12 languages. German, Russian and Turkish rows run long, and a Gauntlet
widget with a fixed width clips or wraps them. This lists the keys whose translation is much longer than
its English default. It is advisory: exit 0 whatever it finds.

Length: the text with its `{=KEY}` prefix and every `{VAR}` / `{.x}` placeholder removed (a placeholder is
replaced at run time by a short value, not shown at its token length). Characters are counted, except that
a CJK character (CNs, CNt, JP, KO: ideographs, kana, hangul and Jamo, full-width forms; half-width katakana and
Hangul FF61-FFDC stay 1) weighs 2. That is an assumption about glyph width, not measured against
Bannerlord's fonts. A gender conditional (`{?X}A{?}B` plus its closing tag) counts as its longer branch.
The same rules apply to the English side.

A key is flagged when, for at least one language, weighted translated / weighted English >= --ratio AND the
translation is at least --min-extra weighted characters longer (so "OK" vs "Okay!!" is not noise). The
reported language is the worst ratio among those that qualify.

A row that is missing, or equal to the English default after the same stripping (seeded, translation
deferred), is untranslated: it is never used for the ratio. A key with no translated row in any checked
language is counted in the summary as untranslated or identical to English (names kept on purpose included), because
"0 flagged" there means unknown, not fits.

Reuses translate_with_claude.LANGUAGES and ._parse_string_xml, and _loc_sources.TAOM_SOURCES. Only the TAOM
module is read (there is no --module flag); TAOM_Map and the Armory live outside the repo.

Exit: 0 report, 1 language folder missing or git failed, 2 usage error.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _loc_sources import TAOM_SOURCES  # noqa: E402
from translate_with_claude import LANGUAGES, _parse_string_xml  # noqa: E402

DEFAULT_ROOT = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData"
CONDITIONAL = re.compile(r"\{\?[^{}]*\}(.*?)\{\\\?\}", re.DOTALL)
PLACEHOLDER = re.compile(r"\{[^{}]*\}")
ID_ON_LINE = re.compile(r'\bid="([^"]+)"')
KEY_ON_LINE = re.compile(r"\{=([^}]+)\}")


def _is_cjk(ch):
    cp = ord(ch)
    return (0x1100 <= cp <= 0x11FF or 0x2E80 <= cp <= 0x9FFF or 0xAC00 <= cp <= 0xD7AF
            or 0xF900 <= cp <= 0xFAFF or 0xFF00 <= cp <= 0xFF60 or 0xFFE0 <= cp <= 0xFFE6
            or 0x20000 <= cp <= 0x2FFFF)


def _longer_branch(match):
    branches = match.group(1).split("{?}")
    return max((PLACEHOLDER.sub("", b) for b in branches),
               key=lambda b: sum(2 if _is_cjk(c) else 1 for c in b))


def weighted_length(text):
    """Length with placeholders removed, a gender conditional measured by its longer branch, and CJK
    characters counted twice."""
    shown = PLACEHOLDER.sub("", CONDITIONAL.sub(_longer_branch, text))
    return sum(2 if _is_cjk(c) else 1 for c in shown)


def load_english(root):
    """key -> English default, first source wins (translate_with_claude.key_owners order)."""
    english, owner = {}, {}
    for src_name, tgt_template in TAOM_SOURCES:
        rows = _parse_string_xml(root / src_name, strip_keys=True)
        for key, text in rows.items():
            if key not in english:
                english[key] = text
                owner[key] = tgt_template
    return english, owner


def changed_keys(root, ref):
    """Ids on added or changed lines of `git diff <ref>` over the source and language files."""
    proc = subprocess.run(["git", "-C", str(root), "diff", "-U0", ref, "--", "."],
                          capture_output=True, text=True, encoding="utf-8")
    if proc.returncode != 0:
        raise SystemExit("loc_length_report: git diff %s failed: %s" % (ref, proc.stderr.strip()))
    keys = set()
    for line in proc.stdout.splitlines():
        if line.startswith("+") and not line.startswith("+++"):
            keys.update(ID_ON_LINE.findall(line))
            keys.update(KEY_ON_LINE.findall(line))
    new = subprocess.run(["git", "-C", str(root), "ls-files", "--others", "--exclude-standard", "--", "."],
                         capture_output=True, text=True, encoding="utf-8")
    if new.returncode != 0:
        raise SystemExit("loc_length_report: git ls-files failed: %s" % new.stderr.strip())
    for name in new.stdout.splitlines():
        text = (Path(root) / name).read_text(encoding="utf-8", errors="replace")
        keys.update(ID_ON_LINE.findall(text))
        keys.update(KEY_ON_LINE.findall(text))
    return keys


def analyse(root, langs, keys, ratio, min_extra):
    english, owner = load_english(root)
    translations = {}
    for lang in langs:
        locale = LANGUAGES[lang][0]
        merged = {}
        for tpl in dict.fromkeys(owner.values()):
            merged[tpl] = _parse_string_xml(root / "Languages" / lang / tpl.format(locale=locale),
                                            strip_keys=False)
        translations[lang] = merged
    wanted = [k for k in english if keys is None or k in keys]
    flagged = []
    untranslated = 0
    for key in wanted:
        en_len = weighted_length(english[key])
        if en_len == 0:
            continue
        best = None
        translated_somewhere = False
        for lang in langs:
            text = translations[lang][owner[key]].get(key)
            if text is None or PLACEHOLDER.sub("", text) == PLACEHOLDER.sub("", english[key]):
                continue  # missing or still the English default: its fit is unknown
            translated_somewhere = True
            tr_len = weighted_length(text)
            r = tr_len / en_len
            if r >= ratio and tr_len - en_len >= min_extra and (best is None or r > best[2]):
                best = (lang, tr_len, r)
        if not translated_somewhere:
            untranslated += 1
        if best:
            flagged.append({"key": key, "english_length": en_len, "language": best[0],
                            "translated_length": best[1], "ratio": round(best[2], 2),
                            "english": english[key]})
    flagged.sort(key=lambda row: (-row["ratio"], row["key"]))
    return flagged, len(wanted), untranslated


def main(argv=None):
    ap = argparse.ArgumentParser(description="Flag translations much longer than their English default.")
    ap.add_argument("--keys", nargs="+", metavar="K")
    ap.add_argument("--since", metavar="REF")
    ap.add_argument("--ratio", type=float, default=1.4)
    ap.add_argument("--min-extra", type=float, default=8)
    ap.add_argument("--lang", nargs="+", metavar="L")
    ap.add_argument("--root", type=Path, default=DEFAULT_ROOT, help="ModuleData folder (tests inject one)")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args(argv)

    unknown = [l for l in (args.lang or []) if l not in LANGUAGES]
    if unknown:
        ap.error("unknown language %s; known: %s" % (", ".join(unknown), " ".join(LANGUAGES)))

    lang_root = args.root / "Languages"
    if not lang_root.is_dir():
        print("loc_length_report: language folder not found: %s" % lang_root)
        return 1
    langs = args.lang or [l for l in LANGUAGES if (lang_root / l).is_dir()]
    missing = [l for l in langs if not (lang_root / l).is_dir()]
    if missing or not langs:
        print("loc_length_report: language folder not found: %s" %
              ", ".join(str(lang_root / l) for l in (missing or LANGUAGES)))
        return 1

    keys = None
    if args.keys:
        keys = set(args.keys)
    if args.since:
        since = changed_keys(args.root, args.since)
        keys = since if keys is None else keys & since

    flagged, total, untranslated = analyse(args.root, langs, keys, args.ratio, args.min_extra)
    summary = ("loc_length_report: %d of %d keys flagged (ratio >= %s); %d untranslated or identical to "
               "English (names included), so their fit is unknown" % (len(flagged), total, args.ratio, untranslated))
    if args.json:
        print(json.dumps(flagged, ensure_ascii=False, indent=2))
        print(summary, file=sys.stderr)
        return 0
    if flagged:
        print("%-44s %5s %5s %6s %6s  %s" % ("key", "en", "lang", "len", "ratio", "english"))
        for r in flagged:
            en = r["english"] if len(r["english"]) <= 50 else r["english"][:47] + "..."
            print("%-44s %5d %5s %6d %6.2f  %s" % (r["key"], r["english_length"], r["language"],
                                                  r["translated_length"], r["ratio"], en))
    print(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())
