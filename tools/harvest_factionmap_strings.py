"""Harvest {=KEY}default strings from factions.json into taom_module_strings.xml.

One-off tool used to seed the keyed-string block for the CC faction-map
content rewrite (issue #260, Phase 2). Reads
Main/_Module/ModuleData/factionmap/factions.json, walks every string-valued
field recursively, extracts every {=key}default pair, and writes the
matching <string id="key" text="{=key}default" /> entries into
taom_module_strings.xml inside a clearly-marked block.

Idempotent: re-running replaces the existing block (delimited by sentinel
comments) without duplicating entries elsewhere in the file.

Usage:
  python tools/harvest_factionmap_strings.py
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parents[1]
FACTIONS_JSON = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "factionmap" / "factions.json"
STRINGS_XML = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "taom_module_strings.xml"

BLOCK_START = "\t<!-- ===== FactionMap (CC) — auto-harvested by tools/harvest_factionmap_strings.py ===== -->"
BLOCK_END = "\t<!-- ===== END FactionMap (CC) ===== -->"

KEY_RE = re.compile(r"^\{=([^}]+)\}(.*)$", re.DOTALL)


def walk_strings(node) -> Iterable[str]:
    if isinstance(node, str):
        yield node
    elif isinstance(node, dict):
        for v in node.values():
            yield from walk_strings(v)
    elif isinstance(node, list):
        for v in node:
            yield from walk_strings(v)


def xml_escape(s: str) -> str:
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def main() -> None:
    data = json.loads(FACTIONS_JSON.read_text(encoding="utf-8"))
    pairs: dict[str, str] = {}
    duplicates: dict[str, list[str]] = {}
    for s in walk_strings(data):
        m = KEY_RE.match(s)
        if not m:
            continue
        key, default = m.group(1), m.group(2)
        if key in pairs and pairs[key] != default:
            duplicates.setdefault(key, [pairs[key]]).append(default)
        pairs[key] = default

    if duplicates:
        print(f"WARN: {len(duplicates)} key(s) appear with conflicting defaults:")
        for k, vs in sorted(duplicates.items()):
            print(f"  {k}: {vs!r}")

    lines = [BLOCK_START, ""]
    for key in sorted(pairs):
        text = f"{{={key}}}{pairs[key]}"
        lines.append(
            f'\t<string id="{xml_escape(key)}" text="{xml_escape(text)}" />'
        )
    lines.append("")
    lines.append(BLOCK_END)
    block = "\n".join(lines) + "\n"

    xml = STRINGS_XML.read_text(encoding="utf-8")

    existing_start = xml.find(BLOCK_START)
    existing_end = xml.find(BLOCK_END)
    if existing_start != -1 and existing_end != -1:
        end_pos = existing_end + len(BLOCK_END)
        if end_pos < len(xml) and xml[end_pos] == "\n":
            end_pos += 1
        xml = xml[:existing_start] + block + xml[end_pos:]
    else:
        close_idx = xml.rfind("</strings>")
        if close_idx == -1:
            raise SystemExit("</strings> closer not found in taom_module_strings.xml")
        before = xml[:close_idx].rstrip() + "\n\n"
        after = xml[close_idx:]
        xml = before + block + "\n" + after

    STRINGS_XML.write_text(xml, encoding="utf-8")
    print(f"Wrote {len(pairs)} keys to {STRINGS_XML.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
