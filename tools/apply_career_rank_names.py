#!/usr/bin/env python3
"""Inject web-researched per-tier RANK titles onto <Career> elements.

Phase C of the career-screen revamp. Reads the reviewed
mapping (tools/career_rank_names.json) and writes
rank1_name/rank2_name/rank3_name="{=taom_career_rank{N}_<id>}<Name>" onto each
matching <Career> in taom_careers.xml. Emits the companion localization snippet.

Text/regex based (mirrors tools/apply_career_group_names.py) so the file keeps
its exact formatting, encoding (UTF-8 BOM), and CRLF line endings. Idempotent.

Usage:
    python tools/apply_career_rank_names.py --dry-run
    python tools/apply_career_rank_names.py --apply
"""
import argparse
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_JSON = os.path.join(REPO, "tools", "career_rank_names.json")
DEFAULT_XML = os.path.join(
    REPO, "Main", "_Module", "ModuleData", "career_system", "taom_careers.xml"
)
DEFAULT_STRINGS_SNIPPET = os.path.join(REPO, "tools", "career_rank_strings.snippet.xml")

RANKS = ("rank1", "rank2", "rank3")


def xml_attr_escape(value):
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def key_for(rank, career_id):
    return "taom_career_%s_%s" % (rank, career_id)


def set_rank_names(xml, career_id, ranks):
    """Insert/replace rank{1,2,3}_name on the <Career id="career_id"> opening tag."""
    # The <Career ...> opening tag spans multiple lines; [^>] matches newlines so
    # this captures the whole tag up to its first '>'.
    tag_re = re.compile(r'<Career\b[^>]*\bid="' + re.escape(career_id) + r'"[^>]*?>')
    m = tag_re.search(xml)
    if not m:
        return xml, "missing"

    tag = m.group(0)
    # Strip any existing rank attributes (idempotent).
    stripped = re.sub(r'\s+rank[123]_name="[^"]*"', "", tag)

    attrs = []
    for r in RANKS:
        name = ranks[r]["name"] if isinstance(ranks[r], dict) else str(ranks[r])
        attrs.append('%s_name="{=%s}%s"' % (r, key_for(r, career_id), xml_attr_escape(name)))
    insert = " " + " ".join(attrs)

    # Insert right after the id="..." token (function repl avoids backref hazards).
    def _ins(mm):
        return mm.group(0) + insert

    new_tag = re.sub(r'\bid="' + re.escape(career_id) + r'"', _ins, stripped, count=1)
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
        sys.exit("ERROR: mapping not found: %s" % args.json)
    if not os.path.exists(args.xml):
        sys.exit("ERROR: careers xml not found: %s" % args.xml)

    with open(args.json, "r", encoding="utf-8") as f:
        mapping = json.load(f)
    # Read as bytes to preserve a leading BOM (if any) + CRLF byte-for-byte on write
    # (tools/README.md "XML I/O convention").
    raw = open(args.xml, "rb").read()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    xml = (raw[3:] if had_bom else raw).decode("utf-8")

    set_n = unchanged_n = missing_n = 0
    missing = []
    string_lines = []
    examples = []

    for career_id, ranks in sorted(mapping.items()):
        if career_id.startswith("_"):
            continue
        xml, status = set_rank_names(xml, career_id, ranks)
        if status == "set":
            set_n += 1
            if len(examples) < 6:
                trio = " / ".join(ranks[r]["name"] for r in RANKS)
                examples.append((career_id, trio))
        elif status == "unchanged":
            unchanged_n += 1
        else:
            missing_n += 1
            missing.append(career_id)
        for r in RANKS:
            name = ranks[r]["name"] if isinstance(ranks[r], dict) else str(ranks[r])
            string_lines.append(
                '\t<string id="%s" text="{=%s}%s" />'
                % (key_for(r, career_id), key_for(r, career_id), xml_attr_escape(name))
            )

    print("careers in mapping : %d" % len([k for k in mapping if not k.startswith("_")]))
    print("rank names set     : %d careers (%d attrs)" % (set_n, set_n * 3))
    print("already current    : %d" % unchanged_n)
    print("NOT FOUND in xml   : %d" % missing_n)
    if missing:
        print("  missing: " + ", ".join(missing))
    for cid, trio in examples:
        print("  %s -> %s" % (cid, trio))

    if args.dry_run:
        print("\n[DRY-RUN] no files written. Re-run with --apply.")
        return

    out = (b"\xef\xbb\xbf" if had_bom else b"") + xml.encode("utf-8")
    open(args.xml, "wb").write(out)
    with open(args.strings_snippet, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("<!-- Career per-tier rank titles (harvest into taom_module_strings.xml) -->\n")
        f.write("\n".join(string_lines) + "\n")
    print("\n[APPLIED] wrote %s" % args.xml)
    print("[APPLIED] wrote loc snippet %s (%d strings)" % (args.strings_snippet, len(string_lines)))


if __name__ == "__main__":
    main()
