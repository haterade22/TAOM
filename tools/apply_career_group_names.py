#!/usr/bin/env python3
"""Inject web-researched lore display names onto career <ChoiceGroup> elements.

Phase B of the career-screen revamp. Reads a reviewed mapping
(tools/career_group_names.json, produced by collating the
`career-group-lore-names` workflow output) and writes a
`display_name="{=taom_career_grp_<id>}<Name>"` attribute onto each matching
<ChoiceGroup> in taom_career_choices.xml. Also emits the companion
localization-string snippet for harvesting into taom_module_strings.xml.

Text/regex based (mirrors the other tools/apply_*.py scripts) so the 8800-line
choices file keeps its exact formatting, encoding, and CRLF line endings.

Idempotent: re-running replaces an existing display_name rather than duplicating.

Mapping JSON shape:
    { "<groupId>": { "name": "Wardens of Henneth Annun",
                     "sourceNote": "...", "attested": true }, ... }

Usage:
    python tools/apply_career_group_names.py --dry-run
    python tools/apply_career_group_names.py --apply
"""
import argparse
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_JSON = os.path.join(REPO, "tools", "career_group_names.json")
DEFAULT_XML = os.path.join(
    REPO, "Main", "_Module", "ModuleData", "career_system", "taom_career_choices.xml"
)
DEFAULT_STRINGS_SNIPPET = os.path.join(REPO, "tools", "career_group_strings.snippet.xml")


def xml_attr_escape(value):
    # Order matters: ampersand first. Escapes the set that is unsafe inside a
    # double-quoted XML attribute value.
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def key_for(group_id):
    return "taom_career_grp_" + group_id


def set_display_name(xml, group_id, name):
    """Insert/replace display_name on the <ChoiceGroup id="group_id"> opening tag.

    Returns (new_xml, status) where status is 'set', 'unchanged', or 'missing'.
    """
    tag_re = re.compile(
        r'<ChoiceGroup\b[^>]*\bid="' + re.escape(group_id) + r'"[^>]*?>'
    )
    m = tag_re.search(xml)
    if not m:
        return xml, "missing"

    tag = m.group(0)
    attr_value = "{=" + key_for(group_id) + "}" + xml_attr_escape(name)
    new_attr = 'display_name="' + attr_value + '"'

    # Strip any existing display_name on this tag.
    stripped = re.sub(r'\s+display_name="[^"]*"', "", tag)

    # Insert the fresh attribute immediately after the id="..." token. Use a
    # function replacement (NOT a \1 backref string) so a name containing
    # backslashes/digits can never corrupt the output. (feedback_re_sub_backref)
    def _ins(mm):
        return mm.group(0) + " " + new_attr

    new_tag = re.sub(
        r'\bid="' + re.escape(group_id) + r'"', _ins, stripped, count=1
    )

    if new_tag == tag:
        return xml, "unchanged"
    return xml[: m.start()] + new_tag + xml[m.end():], "set"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--json", default=DEFAULT_JSON)
    ap.add_argument("--xml", default=DEFAULT_XML)
    ap.add_argument("--strings-snippet", default=DEFAULT_STRINGS_SNIPPET)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", default=True)
    g.add_argument("--apply", dest="dry_run", action="store_false")
    args = ap.parse_args()

    if not os.path.exists(args.json):
        sys.exit("ERROR: mapping not found: %s (run/collate the workflow first)" % args.json)
    if not os.path.exists(args.xml):
        sys.exit("ERROR: choices xml not found: %s" % args.xml)

    with open(args.json, "r", encoding="utf-8") as f:
        mapping = json.load(f)

    # Read as bytes so we can preserve a leading BOM (if any) and CRLF byte-for-byte
    # on write (tools/README.md "XML I/O convention").
    raw = open(args.xml, "rb").read()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    xml = (raw[3:] if had_bom else raw).decode("utf-8")

    set_n = unchanged_n = missing_n = 0
    missing = []
    string_lines = []
    examples = []

    for group_id, entry in sorted(mapping.items()):
        if group_id.startswith("_"):  # skip _meta and any other metadata keys
            continue
        name = entry["name"] if isinstance(entry, dict) else str(entry)
        xml, status = set_display_name(xml, group_id, name)
        if status == "set":
            set_n += 1
            if len(examples) < 8:
                examples.append((group_id, name))
        elif status == "unchanged":
            unchanged_n += 1
        else:
            missing_n += 1
            missing.append(group_id)
        string_lines.append(
            '\t<string id="%s" text="{=%s}%s" />'
            % (key_for(group_id), key_for(group_id), xml_attr_escape(name))
        )

    print("groups in mapping : %d" % len(mapping))
    print("display_name set  : %d" % set_n)
    print("already current   : %d" % unchanged_n)
    print("NOT FOUND in xml  : %d" % missing_n)
    if missing:
        print("  missing ids: " + ", ".join(missing[:20]) + (" ..." if len(missing) > 20 else ""))
    if examples:
        print("examples:")
        for gid, name in examples:
            print("  %s -> %s" % (gid, name))

    if args.dry_run:
        print("\n[DRY-RUN] no files written. Re-run with --apply to write.")
        return

    out = (b"\xef\xbb\xbf" if had_bom else b"") + xml.encode("utf-8")
    open(args.xml, "wb").write(out)
    with open(args.strings_snippet, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("<!-- Career choice-group path names (harvest into taom_module_strings.xml) -->\n")
        f.write("\n".join(string_lines) + "\n")
    print("\n[APPLIED] wrote %s" % args.xml)
    print("[APPLIED] wrote loc snippet %s (%d strings)" % (args.strings_snippet, len(string_lines)))


if __name__ == "__main__":
    main()
