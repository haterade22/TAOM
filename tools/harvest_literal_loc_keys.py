#!/usr/bin/env python3
"""
Register the {=taom_*} localization keys that C# declares as plain literals but no
ModuleData strings XML carries a row for.

An unregistered key is invisible in English -- MBTextManager.GetLocalizedText
short-circuits on English and returns the inline default -- and untranslatable in the
other eleven languages, because the per-language files are keyed off the registered
rows. See #434.

This harvests only keys whose default text can be read straight out of the source
literal (`"{=taom_key}Default text"`, or the interpolated `$"{{=taom_key}}..."` form).
Keys COMPOSED at runtime from data ids cannot be seen here at all and are generated
per-family instead -- tools/generate_enlistment_duty_strings.py is the model.

Idempotent: a key already present in any ModuleData XML is skipped, so re-running
after hand-tuning a row leaves the tuned text alone.

Usage:
    python tools/harvest_literal_loc_keys.py --dry-run
    python tools/harvest_literal_loc_keys.py --apply
"""

import argparse
import io
import re
import sys
from collections import OrderedDict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MAIN = REPO_ROOT / "Main"
MODULE_DATA = MAIN / "_Module" / "ModuleData"

# Mirrors UnregisteredLocalizationKeyBaselineTests.KeyLiteral, plus a capture group for
# the default text that follows the key inside the same literal. The trailing quote is
# what bounds the default; (?:\\.|[^"\\])* steps over any C# escape, so an embedded \"
# does not end the match early.
LITERAL = re.compile(r'(\$?)"\{\{?=(taom_[A-Za-z0-9_]+)\}\}?((?:\\.|[^"\\])*)"')

# A registered row re-embeds its own key as a {=KEY} prefix on some attribute value.
EMBEDDED_PREFIX = re.compile(r"^\{=([A-Za-z0-9_]+)\}")
STRING_ID = re.compile(r'<string\s+[^>]*?id="([^"]+)"')
ATTR_VALUE = re.compile(r'="([^"]*)"')

# Key prefix -> the strings file that owns it. Longest prefix wins; DEFAULT_TARGET takes
# the rest. Routing follows docs/localization/TRANSLATOR_GUIDE.md: UI labels authored in
# C# live in taom_module_strings.xml unless the feature already owns a file.
ROUTES = [
    ("taom_cc_", "taom_cc_strings.xml"),
    ("taom_emissary_", "taom_emissary_strings.xml"),
]
DEFAULT_TARGET = "taom_module_strings.xml"

MARKER = "harvested by tools/harvest_literal_loc_keys.py"


def read_text(path: Path) -> str:
    """Read byte-faithfully -- newline='' keeps CRLF as CRLF so a rewrite is not a
    whole-file diff on Windows."""
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write_text(path: Path, text: str) -> None:
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def unescape(body: str, interpolated: bool) -> str:
    """Turn a C# literal body into the string the compiler would produce."""
    out = (body.replace('\\"', '"').replace("\\\\", "\\")
               .replace("\\n", "\n").replace("\\t", "\t").replace("\\r", "\r"))
    if interpolated:
        out = out.replace("{{", "{").replace("}}", "}")
    return out


def escape_xml_attr(text: str) -> str:
    return (text.replace("&", "&amp;").replace('"', "&quot;")
                .replace("<", "&lt;").replace(">", "&gt;"))


def declared_keys():
    """key -> (default_text, repo_relative_path, line_no), first declaration wins."""
    found = OrderedDict()
    for cs in sorted(MAIN.rglob("*.cs")):
        if any(part in ("bin", "obj") for part in cs.parts):
            continue
        text = read_text(cs)
        rel = cs.relative_to(REPO_ROOT).as_posix()
        for m in LITERAL.finditer(text):
            key = m.group(2)
            if key in found:
                continue
            default = unescape(m.group(3), m.group(1) == "$")
            found[key] = (default, rel, text.count("\n", 0, m.start()) + 1)
    return found


def registered_keys():
    """Every key any ModuleData XML already carries. Mirrors the baseline test: a row
    registers both its id and any {=KEY} prefix embedded in an attribute value, and the
    per-language files under Languages/ do not count as registration."""
    keys = set()
    for xml in MODULE_DATA.rglob("*.xml"):
        if "Languages" in xml.parts:
            continue
        try:
            text = read_text(xml)
        except OSError:
            continue
        keys.update(STRING_ID.findall(text))
        for value in ATTR_VALUE.findall(text):
            m = EMBEDDED_PREFIX.match(value)
            if m:
                keys.add(m.group(1))
    return keys


def target_for(key: str) -> str:
    for prefix, filename in sorted(ROUTES, key=lambda r: -len(r[0])):
        if key.startswith(prefix):
            return filename
    return DEFAULT_TARGET


def section_for(rel_path: str) -> str:
    """'Main/Features/CulturalFeats/TaomCulturalFeats.cs' -> 'CulturalFeats'."""
    parts = rel_path.split("/")
    if "Features" in parts:
        i = parts.index("Features")
        if i + 1 < len(parts):
            return parts[i + 1]
    return parts[-1].replace(".cs", "")


def insert_rows(path: Path, rows: list) -> None:
    """Insert rows immediately before the closing </strings>, matching the file's
    existing indent and line ending."""
    text = read_text(path)
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.split(nl)
    close = next(i for i, l in enumerate(lines) if "</strings>" in l)
    existing = next((l for l in lines if l.lstrip().startswith("<string ")), None)
    indent = re.match(r"\s*", existing).group(0) if existing else "\t"

    block = [f"{indent}<!-- {MARKER} -->"]
    current_section = None
    for key, default, rel, _line in rows:
        section = section_for(rel)
        if section != current_section:
            block.append(f"{indent}<!-- {section} -->")
            current_section = section
        block.append(
            f'{indent}<string id="{key}" text="{{={key}}}{escape_xml_attr(default)}" />')
    block.append("")

    # Drop a trailing blank line the file already has, so the block does not double it.
    at = close
    while at > 0 and lines[at - 1].strip() == "":
        at -= 1
    lines[at:at] = block
    write_text(path, nl.join(lines))


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    g = p.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true", help="report what would be added")
    g.add_argument("--apply", action="store_true", help="write the rows")
    args = p.parse_args()

    declared = declared_keys()
    registered = registered_keys()
    missing = [(k, v[0], v[1], v[2]) for k, v in declared.items() if k not in registered]

    print(f"[harvest] literal {{=taom_*}} keys declared in Main/**/*.cs: {len(declared)}")
    print(f"[harvest] already registered in ModuleData:                 "
          f"{len(declared) - len(missing)}")
    print(f"[harvest] unregistered:                                     {len(missing)}")

    blank = [k for k, d, _r, _l in missing if not d.strip()]
    if blank:
        print(f"\n  REFUSING: {len(blank)} key(s) have an empty default and would register "
              f"a blank row a translator cannot work from:", file=sys.stderr)
        for k in blank:
            print(f"    {k}", file=sys.stderr)
        return 1

    by_target = OrderedDict()
    for key, default, rel, line in missing:
        by_target.setdefault(target_for(key), []).append((key, default, rel, line))

    for filename, rows in by_target.items():
        print(f"\n  {filename}  +{len(rows)}")
        sections = OrderedDict()
        for _k, _d, rel, _l in rows:
            sections[section_for(rel)] = sections.get(section_for(rel), 0) + 1
        for section, n in sections.items():
            print(f"    {section:<24} {n:>4}")

    if args.dry_run:
        print("\n  (dry run -- nothing written)")
        return 0

    for filename, rows in by_target.items():
        insert_rows(MODULE_DATA / filename, rows)
        print(f"\n  wrote {len(rows)} rows -> {filename}")
    print("\n  Next: python tools/translate_with_claude.py --lang <L> --module TAOM "
          "--sync-ids --apply")
    return 0


if __name__ == "__main__":
    sys.exit(main())
