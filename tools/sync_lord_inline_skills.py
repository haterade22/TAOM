#!/usr/bin/env python3
"""Sync every inline <skills> block that sits beside a skill_template to the SkillSet it names.

Why this exists. Through Bannerlord v1.4.8, BasicCharacterObject.Deserialize read an NPCCharacter's
inline <skills> block only when its skill_template did not resolve, so TAOM's lord generators kept
the block as a documentation mirror of the SkillSet and the engine saw only the SkillSet. Since
v1.5.2 the loader copies the template into a fresh MBCharacterSkills registered under the
character's own id and then applies the inline block on top, so a mirror that drifted from its
SkillSet silently changes the lord's stats (64 lords in characters/lords.xml and 19 vanilla-id
lords in lords.xslt had drifted when the bump landed, both directions). The SkillSet stays the
source of truth; this script rewrites the mirrors so the override reproduces the template's own
numbers, which is what v1.4.8 gave.

Two shapes are covered. An XML NPCCharacter carries skill_template="SkillSet.X" on its opening tag
and a <skills> child. A lords.xslt template (<xsl:template match="NPCCharacter[@id='...']">)
writes <xsl:attribute name="skill_template">SkillSet.X</xsl:attribute> and a literal <skills>
block; the transform output is what the engine loads, so the same rule applies.

Only skills the block lists are rewritten; a skill the block omits keeps the copied template value
on v1.5.2. A skill the SkillSet does not define counts as 0, which is what the engine's property
owner returns for an unset skill.

SkillSets resolve from every *skill_sets*.xml in the installed vanilla modules (Native,
SandBoxCore, SandBox, StoryMode; the spc_* rookie templates live in SandBox) and then under
Main/_Module/ModuleData, in that load order, merged the way MBObjectManager.MergeElements merges
a duplicate id. Without the install, a template that resolves nowhere is reported and skipped.

Usage:
    python tools/sync_lord_inline_skills.py            # report; exit 1 if any block drifted
    python tools/sync_lord_inline_skills.py --apply    # rewrite drifted values in place (BOM/EOL kept)

Gate: TAOM.Tests/Core/LordInlineSkillParityTests.cs (BindingVerification) fails on any drift.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _gamedir import game_dir  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODULE_DATA = os.path.join(REPO, "Main", "_Module", "ModuleData")
DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
VANILLA_MODULES = ("Native", "SandBoxCore", "SandBox", "StoryMode")

NPC_RE = re.compile(r"<NPCCharacter\b.*?</NPCCharacter>", re.S)
TEMPLATE_RE = re.compile(r'\bskill_template="([^"]+)"')
ID_RE = re.compile(r'\bid="([^"]+)"')
XSLT_TEMPLATE_RE = re.compile(r"<xsl:template\b[^>]*>.*?</xsl:template>", re.S)
XSLT_MATCH_RE = re.compile(r"""match="NPCCharacter\[@id='([^']+)'\]\"""")
XSLT_SKILL_TEMPLATE_RE = re.compile(r'<xsl:attribute name="skill_template">\s*([^<\s]+)\s*</xsl:attribute>')
BLOCK_RE = re.compile(r"<[Ss]kills\b[^>/]*>(.*?)</[Ss]kills>", re.S)
SKILL_RE = re.compile(r'(<[Ss]kill\b[^>]*?\bid="([^"]+)"[^>]*?\bvalue=")([^"]*)(")')
SET_RE = re.compile(r'(<SkillSet\b[^>]*\bid="([^"]+)"[^>]*>)(.*?)</SkillSet>', re.S)
REPLACE_RE = re.compile(r'\b_replaceWhileMerging="true"', re.I)


def read_text(path: str) -> tuple[str, bool]:
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    return (raw[3:] if bom else raw).decode("utf-8"), bom


def write_text(path: str, text: str, bom: bool) -> None:
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def skill_set_files(game: str) -> list[str]:
    """In engine load order: the vanilla modules first, TAOM's own files last."""
    files: list[str] = []
    for module in VANILLA_MODULES:
        files += sorted(glob.glob(os.path.join(game, "Modules", module, "ModuleData", "**", "*skill_sets*.xml"), recursive=True))
    files += sorted(glob.glob(os.path.join(MODULE_DATA, "**", "*skill_sets*.xml"), recursive=True))
    return files


def load_skill_sets_from(files: list[str]) -> dict[str, dict[str, int]]:
    """Merge the way MBObjectManager.MergeElements does before any object exists: a later file's
    SkillSet with an id already seen overrides the skills it lists and keeps the ones it omits,
    unless it carries _replaceWhileMerging="true", which drops the earlier children first.
    (Vanilla itself declares infantry_heavyinfantry_level1_template_skills in both SandBoxCore and
    SandBox with different values, so this path is live even before any TAOM id collides.)"""
    sets: dict[str, dict[str, int]] = {}
    for path in files:
        text, _ = read_text(path)
        for tag, set_id, body in SET_RE.findall(text):
            values: dict[str, int] = {}
            for _, skill_id, value, _ in SKILL_RE.findall(body):
                try:
                    values[skill_id] = int(value)
                except ValueError:
                    pass
            if set_id in sets and not REPLACE_RE.search(tag):
                sets[set_id].update(values)
            else:
                sets[set_id] = values
    return sets


def _sync_block(block: str, char_id: str, template: str, sets: dict[str, dict[str, int]], report: list[str]) -> tuple[str, int, int]:
    """Rewrite the listed inline values inside one character block. Returns (block, checked, changed)."""
    bm = BLOCK_RE.search(block)
    if not bm or not SKILL_RE.search(bm.group(1)):
        return block, 0, 0
    set_id = template.split(".", 1)[-1]
    if set_id not in sets:
        report.append(f"{char_id}: skill_template {template} resolves to no SkillSet (left alone)")
        return block, 1, 0
    changed = 0

    def fix_skill(sm: re.Match) -> str:
        nonlocal changed
        skill_id, value = sm.group(2), sm.group(3)
        expected = sets[set_id].get(skill_id, 0)
        try:
            current = int(value)
        except ValueError:
            current = None
        if current == expected:
            return sm.group(0)
        changed += 1
        report.append(f"{char_id}: {skill_id} {value} -> {expected} ({set_id})")
        return f"{sm.group(1)}{expected}{sm.group(4)}"

    new_inner = SKILL_RE.sub(fix_skill, bm.group(1))
    return block[:bm.start(1)] + new_inner + block[bm.end(1):], 1, changed


def sync_file(path: str, sets: dict[str, dict[str, int]], apply: bool) -> tuple[int, int, list[str]]:
    """Returns (blocks checked, values changed, report lines)."""
    text, bom = read_text(path)
    report: list[str] = []
    checked = 0
    changed = 0
    is_xslt = path.lower().endswith(".xslt")

    def fix_character(m: re.Match) -> str:
        nonlocal checked, changed
        block = m.group(0)
        if is_xslt:
            idm = XSLT_MATCH_RE.search(block)
            tm = XSLT_SKILL_TEMPLATE_RE.search(block)
            if not idm or not tm:
                return block
            char_id, template = idm.group(1), tm.group(1)
        else:
            head = block.split(">", 1)[0]
            tm = TEMPLATE_RE.search(head)
            if not tm:
                return block
            idm = ID_RE.search(head)
            char_id, template = (idm.group(1) if idm else "(no id)"), tm.group(1)
        new_block, c, n = _sync_block(block, char_id, template, sets, report)
        checked += c
        changed += n
        return new_block

    pattern = XSLT_TEMPLATE_RE if is_xslt else NPC_RE
    new_text = pattern.sub(fix_character, text)
    if apply and new_text != text:
        write_text(path, new_text, bom)
    return checked, changed, report


def data_files() -> list[str]:
    files = glob.glob(os.path.join(MODULE_DATA, "**", "*.xml"), recursive=True)
    files += glob.glob(os.path.join(MODULE_DATA, "**", "*.xslt"), recursive=True)
    return sorted(f for f in files if "skill_sets" not in os.path.basename(f).lower())


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--apply", action="store_true", help="rewrite drifted inline values in place")
    ap.add_argument("--game-dir", default=game_dir(DEFAULT_GAME), help="Bannerlord install root (default: $BANNERLORD_GAME_DIR)")
    args = ap.parse_args(argv)

    sets = load_skill_sets(args.game_dir)
    if not sets:
        print("no SkillSet definitions found; nothing to compare against", file=sys.stderr)
        return 2
    vanilla_seen = any(os.path.isdir(os.path.join(args.game_dir, "Modules", m, "ModuleData")) for m in VANILLA_MODULES)
    if not vanilla_seen:
        print(f"warning: no vanilla ModuleData under {args.game_dir}; vanilla templates (spc_*) will not resolve", file=sys.stderr)

    total_checked = total_changed = 0
    files_touched: list[str] = []
    for path in data_files():
        checked, changed, report = sync_file(path, sets, args.apply)
        total_checked += checked
        total_changed += changed
        if changed:
            files_touched.append(os.path.relpath(path, REPO))
        if report:
            print(os.path.relpath(path, REPO))
            for line in report:
                print("   ", line)
    print(f"{total_checked} inline block(s) beside a template checked against {len(sets)} SkillSet(s); "
          f"{total_changed} value(s) {'rewritten' if args.apply else 'drifted'} in {len(files_touched)} file(s)")
    if total_changed and not args.apply:
        print("re-run with --apply to sync them", file=sys.stderr)
        return 1
    return 0


def load_skill_sets(game: str) -> dict[str, dict[str, int]]:
    return load_skill_sets_from(skill_set_files(game))


if __name__ == "__main__":
    sys.exit(main())
