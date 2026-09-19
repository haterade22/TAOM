#!/usr/bin/env python3
"""Sync every inline <skills> block that sits beside a skill_template to the SkillSet it names.

Why this exists. Through Bannerlord v1.4.8, BasicCharacterObject.Deserialize read an NPCCharacter's
inline <skills> block only when it carried no skill_template attribute (a dangling id still returned a
presumed, empty SkillSet), so TAOM's lord generators kept the block as a documentation mirror of the
SkillSet and the engine saw only the SkillSet. Since
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

Gates: TAOM.Tests/Core/LordInlineSkillParityTests.cs (BindingVerification) fails on any drift, and
validate_moduledata.py reports it as SKILL_TEMPLATE_MISMATCH through sync_file in report mode (the
commit hook runs it). Nothing inside an XML comment is matched, judged, rewritten or loaded as a
SkillSet row; the engine never reads it.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys
from typing import NamedTuple

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
COMMENT_RE = re.compile(r"<!--.*?-->", re.S)


def read_text(path: str) -> tuple[str, bool]:
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    return (raw[3:] if bom else raw).decode("utf-8"), bom


def write_text(path: str, text: str, bom: bool) -> None:
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def skill_set_files(game: str, module_data: str = MODULE_DATA) -> list[str]:
    """In engine load order: the vanilla modules first, TAOM's own files last."""
    files: list[str] = []
    for module in VANILLA_MODULES:
        files += sorted(glob.glob(os.path.join(game, "Modules", module, "ModuleData", "**", "*skill_sets*.xml"), recursive=True))
    files += sorted(glob.glob(os.path.join(module_data, "**", "*skill_sets*.xml"), recursive=True))
    return files


def load_skill_sets_from(files: list[str]) -> dict[str, dict[str, int]]:
    """Merge the way MBObjectManager.MergeElements does before any object exists: a later file's
    SkillSet with an id already seen overrides the skills it lists and keeps the ones it omits,
    unless it carries _replaceWhileMerging="true", which drops the earlier children first.
    (Vanilla itself declares infantry_heavyinfantry_level1_template_skills in both SandBoxCore and
    SandBox with different values, so this path is live even before any TAOM id collides.)
    Commented-out rows are not read: vanilla keeps some inside SkillSet bodies."""
    sets: dict[str, dict[str, int]] = {}
    for path in files:
        text, _ = read_text(path)
        for tag, set_id, body in SET_RE.findall(COMMENT_RE.sub("", text)):
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


class Drift(NamedTuple):
    """One inline value that differs from its SkillSet."""
    char_id: str
    line: int          # 1-based line of the character's opening tag
    set_id: str
    skill: str
    inline: str        # as written, so a non-integer value is reported verbatim
    expected: int

    def describe(self) -> str:
        return f"{self.skill} {self.inline} -> {self.expected} ({self.set_id})"


class Scan(NamedTuple):
    text: str          # the synced text; the input itself when nothing drifted
    checked: int       # templated characters with at least one inline row
    drifts: list       # Drift, one per differing value
    unresolved: list   # (character id, line, template) for a template with rows that names no SkillSet


def blank_comments(text: str) -> str:
    """Every comment's characters turned to spaces, newlines kept: the same length, so an offset into
    the result is an offset into `text`. The engine never loads a comment, so nothing inside one is
    matched, judged or rewritten."""
    return COMMENT_RE.sub(lambda c: "\n".join(" " * len(part) for part in c.group(0).split("\n")), text)


def _scan(text: str, is_xslt: bool, sets: dict[str, dict[str, int]]) -> Scan:
    """Judge every templated character's inline rows against its SkillSet. Matching runs on the
    comment-blanked copy and every span is absolute, so an edit lands in `text` at the same offsets;
    all edits are joined once."""
    masked = blank_comments(text)
    drifts: list[Drift] = []
    unresolved: list[tuple[str, int, str]] = []
    edits: list[tuple[int, int, str]] = []
    checked = 0
    line, last = 1, 0
    for m in (XSLT_TEMPLATE_RE if is_xslt else NPC_RE).finditer(masked):
        block = m.group(0)
        if is_xslt:
            idm, tm = XSLT_MATCH_RE.search(block), XSLT_SKILL_TEMPLATE_RE.search(block)
            if not idm or not tm:
                continue
            char_id, template = idm.group(1), tm.group(1)
        else:
            head = block.split(">", 1)[0]
            tm = TEMPLATE_RE.search(head)
            if not tm:
                continue
            idm = ID_RE.search(head)
            char_id, template = (idm.group(1) if idm else "(no id)"), tm.group(1)
        bm = BLOCK_RE.search(masked, m.start(), m.end())
        if not bm or not SKILL_RE.search(masked, bm.start(1), bm.end(1)):
            continue
        checked += 1
        line += masked.count("\n", last, m.start())
        last = m.start()
        set_id = template.split(".", 1)[-1]
        if set_id not in sets:
            unresolved.append((char_id, line, template))
            continue
        for sm in SKILL_RE.finditer(masked, bm.start(1), bm.end(1)):
            expected = sets[set_id].get(sm.group(2), 0)
            try:
                current = int(sm.group(3))
            except ValueError:
                current = None
            if current != expected:
                drifts.append(Drift(char_id, line, set_id, sm.group(2), sm.group(3), expected))
                edits.append((sm.start(3), sm.end(3), str(expected)))
    if not edits:
        return Scan(text, checked, drifts, unresolved)
    parts, pos = [], 0
    for s, e, value in edits:        # found in ascending order
        parts += [text[pos:s], value]
        pos = e
    parts.append(text[pos:])
    return Scan("".join(parts), checked, drifts, unresolved)


def sync_file(path: str, sets: dict[str, dict[str, int]], apply: bool = False) -> Scan:
    """Scan one file for inline values that disagree with their SkillSet; with `apply`, rewrite them
    in place (BOM and line endings kept). Report mode, the default, never writes: the validator's
    SKILL_TEMPLATE_MISMATCH calls it that way (#626)."""
    text, bom = read_text(path)
    scan = _scan(text, path.lower().endswith(".xslt"), sets)
    if apply and scan.drifts:
        write_text(path, scan.text, bom)
    return scan


def data_files(module_data: str = MODULE_DATA) -> list[str]:
    files = glob.glob(os.path.join(module_data, "**", "*.xml"), recursive=True)
    files += glob.glob(os.path.join(module_data, "**", "*.xslt"), recursive=True)
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
        scan = sync_file(path, sets, args.apply)
        total_checked += scan.checked
        total_changed += len(scan.drifts)
        if scan.drifts:
            files_touched.append(os.path.relpath(path, REPO))
        if scan.drifts or scan.unresolved:
            print(os.path.relpath(path, REPO))
            for d in scan.drifts:
                print("   ", f"{d.char_id}: {d.describe()}")
            for char_id, _, template in scan.unresolved:
                print("   ", f"{char_id}: skill_template {template} resolves to no SkillSet (left alone)")
    print(f"{total_checked} inline block(s) beside a template checked against {len(sets)} SkillSet(s); "
          f"{total_changed} value(s) {'rewritten' if args.apply else 'drifted'} in {len(files_touched)} file(s)")
    if total_changed and not args.apply:
        print("re-run with --apply to sync them", file=sys.stderr)
        return 1
    return 0


def load_skill_sets(game: str, module_data: str = MODULE_DATA) -> dict[str, dict[str, int]]:
    return load_skill_sets_from(skill_set_files(game, module_data))


if __name__ == "__main__":
    sys.exit(main())
