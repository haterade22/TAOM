"""Nest the 168 orphaned female-tavern conversation <action> elements (report §4, 2026-08-03).

Twelve `as_<race>_female_villager_in_aserai_tavern` action sets were authored as SELF-CLOSING
elements, which orphaned the 14 female-conversation overrides that belong inside each one. The
overrides landed as root-level `<action>` children of `<action_sets>` -- 12 x 14 = 168 stray
elements that no schema allows.

Build 1.4.7.117484 tolerates the malformed file. Build 117131 -- which TaleWorlds' DEDICATED
SERVER engine ships -- throws KeyNotFoundException in MBObjectManager.MergeElements at schema
path /action_sets/action when LOTRLOME_Armory loads before Alliance.Wargs with StoryMode
present, so every dedicated server had to run the single-player module order or crash on boot.

The fix mirrors vanilla exactly: Native's own `as_human_female_villager_in_aserai_tavern`
carries a base_set AND these same 14 nested actions, in this same order.

Both copies are rewritten: the LIVE Armory file (authoritative) and the tracked snapshot under
docs/reference/lotrlome-armory-snapshot/, which must not drift from it.

Idempotent: a file with zero self-closing target sets is left byte-identical.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

LIVE = Path(
    r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord"
    r"/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml"
)
SNAPSHOT = Path(__file__).resolve().parents[2] / "docs/reference/lotrlome-armory-snapshot/action_sets.xml"

RACES = [
    "dwarf", "uruk", "uruk_hai", "berserker", "orc", "nazghul",
    "hill_troll", "pale_uruk", "cave_troll", "dg_uruk", "goblin", "saruman",
]

# The 14 override types, in vanilla's order. Verified byte-identical against Native's
# as_human_female_villager_in_aserai_tavern on 2026-08-03. Order is asserted, not assumed:
# a group that does not match this exact sequence is left alone and reported.
VANILLA_ORDER = [
    "act_conversation_normal_start", "act_conversation_normal_loop",
    "act_conversation_normal2_start", "act_conversation_normal2_loop",
    "act_conversation_closed_start", "act_conversation_closed_loop",
    "act_conversation_closed2_start", "act_conversation_closed2_loop",
    "act_conversation_confident_start", "act_conversation_confident_loop",
    "act_conversation_confident2_start", "act_conversation_confident2_loop",
    "act_conversation_hip_start", "act_conversation_hip_loop",
]

ACTION_RE = re.compile(r"[ \t]*<action\b[^>]*?/>[ \t]*\r?\n", re.DOTALL)
TYPE_RE = re.compile(r'\btype="([^"]*)"')


def rewrite(text: str) -> tuple[str, int, list[str]]:
    """Return (new_text, groups_fixed, warnings)."""
    warnings: list[str] = []
    fixed = 0

    for race in RACES:
        set_id = f"as_{race}_female_villager_in_aserai_tavern"
        # Only self-closing sets are candidates. An already-nested set has no `/>` and is skipped,
        # which is what makes a re-run a no-op.
        set_re = re.compile(r'[ \t]*<action_set\b[^>]*?\bid="' + re.escape(set_id) + r'"[^>]*?/>[ \t]*\r?\n', re.DOTALL)
        m = set_re.search(text)
        if m is None:
            warnings.append(f"{set_id}: no self-closing action_set found (already nested?) -- skipped")
            continue

        # Consume the 14 immediately-following root-level <action .../> elements, allowing blank
        # lines between them (the file mixes a one-line and a three-line action style).
        pos = m.end()
        actions: list[str] = []
        while len(actions) < len(VANILLA_ORDER):
            blank = re.match(r"(?:[ \t]*\r?\n)*", text[pos:])
            probe = pos + blank.end()
            am = ACTION_RE.match(text, probe)
            if am is None:
                break
            actions.append(am.group(0))
            pos = am.end()

        got = [TYPE_RE.search(a).group(1) if TYPE_RE.search(a) else "?" for a in actions]
        if got != VANILLA_ORDER:
            warnings.append(
                f"{set_id}: following actions do not match vanilla's 14 "
                f"(found {len(got)}: {got[:3]}...) -- LEFT UNCHANGED"
            )
            continue

        set_tag = m.group(0)
        indent = re.match(r"[ \t]*", set_tag).group(0)
        opened = re.sub(r"[ \t]*/>[ \t]*(\r?\n)$", r">\1", set_tag)

        # Re-indent each action one level deeper, preserving its original internal layout.
        nested = []
        for a in actions:
            nested.append("".join(
                (indent + "\t" + line.lstrip(" \t")) if line.strip() else line
                for line in a.splitlines(keepends=True)
            ))

        newline = "\r\n" if set_tag.endswith("\r\n") else "\n"
        replacement = opened + "".join(nested) + f"{indent}</action_set>{newline}"
        text = text[:m.start()] + replacement + text[pos:]
        fixed += 1

    return text, fixed, warnings


def count_strays(path: Path) -> int:
    """Root-level <action> children of <action_sets>, counted by the same parser the engine uses."""
    import xml.parsers.expat

    stack: list[str] = []
    strays = 0
    parser = xml.parsers.expat.ParserCreate()

    def start(name, _attrs):
        nonlocal strays
        if name == "action" and stack and stack[-1] == "action_sets":
            strays += 1
        stack.append(name)

    parser.StartElementHandler = start
    parser.EndElementHandler = lambda _n: stack.pop()
    parser.Parse(path.read_bytes(), True)
    return strays


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    args = ap.parse_args()

    exit_code = 0
    for label, path in (("LIVE", LIVE), ("SNAPSHOT", SNAPSHOT)):
        if not path.exists():
            print(f"{label}: MISSING at {path}")
            exit_code = 1
            continue

        before = count_strays(path)
        raw = path.read_bytes().decode("utf-8-sig")
        new, fixed, warnings = rewrite(raw)

        for w in warnings:
            print(f"{label}: WARN {w}")

        print(f"{label}: {before} stray action(s) before, {fixed}/12 group(s) nested")
        if args.apply and new != raw:
            # Preserve the BOM the engine's own files carry.
            bom = "\ufeff" if path.read_bytes().startswith(b"\xef\xbb\xbf") else ""
            path.write_text(bom + new, encoding="utf-8", newline="")
            after = count_strays(path)
            print(f"{label}: {after} stray action(s) after")
            if after != 0:
                exit_code = 1
        elif not args.apply:
            print(f"{label}: dry run -- pass --apply to write")

    return exit_code


if __name__ == "__main__":
    sys.exit(main())
