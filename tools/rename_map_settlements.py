#!/usr/bin/env python3
"""Rename, rebind or remove settlement rows in the LIVE TAOM_Map/settlements.xml and its 12 loc files.

docs/modding/settlements.md says never to rename a settlement id and never to delete one, and both
rules stand for ordinary edits: saves reference ids, the map scene entity must carry the same name,
and villages die with their fortification. This tool exists for the one case where the rule inverts:
the MAP AUTHOR has already renamed or removed the entity in the scene editor, so the data row is now
the orphan (a campaign load NREs in SettlementVisual.OnStartup, #269) and the only way back to a
consistent world is to follow the scene. Every change here is new-campaign-only, which an entity
rename already forces.

RENAMES: (old id, new id, new bound fortification). The whole block is rewritten in place: the
Settlement id, its name/text keys, the component id (add_map_villages.comp_id), `bound`, and
posX/posY from the NEW id's scene entity when that entity is saved (else the old position stays and
the editor's next save refreshes it). The old id may not survive anywhere in its block; a stray
mention fails the run rather than shipping a half-rename. Loc rows keep their translation under the
new key. Repo files that carry settlement ids (REPO_ID_FILES) get the same exact-token rename.
REMOVALS: the whole block and its loc rows go; a fortification that still has bound villages is
refused, because every one of them would dereference null on the next new campaign.

Before --apply the tool sweeps Main/**/*.cs and Main/_Module/ModuleData/**/*.json for every old or
removed id and refuses on a hit outside REPO_ID_FILES: a settlement id in code is a recruitment
pool or a guard, and a rename that leaves it behind is a dead dictionary key nobody logs.

Byte discipline as in tools/add_map_villages.py (#562): binary read, each file's own BOM and newline
preserved, a non-.xml backup before the write, every written document parsed first. Sequencing:
run this BEFORE add_map_villages.py when one batch renames and adds in the same region, and never
while the editor is mid-save (SettlementPositionScript.OnSceneSave rewrites the same file).

First batch (#597, 2026-09-13): castle_village_EW7_3/_2 became village_EW10_1/_2 under Serelond,
and hideout_desert_34 north of Methir was retired.

Usage:
  python tools/rename_map_settlements.py            # dry run: prints the plan and the repo sweep
  python tools/rename_map_settlements.py --apply    # writes the live master + 12 loc files + repo files (+ backups)
  python tools/rename_map_settlements.py --check    # exit 1 while any rename or removal is incomplete or drifted
"""
import argparse
import datetime
import json
import os
import re
import sys
from collections import namedtuple
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import add_map_villages as amv  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent

Rename = namedtuple("Rename", "old new bound")

RENAMES = [
    Rename("castle_village_EW7_3", "village_EW10_1", "town_EW10"),
    Rename("castle_village_EW7_2", "village_EW10_2", "town_EW10"),
]
REMOVALS = ["hideout_desert_34"]
# Repo files that list settlement ids and are rewritten with the rename (exact-token). Anything
# else in the sweep that names an old or removed id is a refusal, not a silent pass.
REPO_ID_FILES = ["Main/_Module/ModuleData/recruitment_pools/gondor.json"]

_ID_CHARS = r"A-Za-z0-9_"


def _open_tag(sid):
    return '<Settlement id="' + sid + '"'


def settlement_block_span(text, sid):
    """(start, end) of the settlement's block: from the start of its line to just past the newline
    that ends its </Settlement>. None when the id has no row."""
    start = text.find(_open_tag(sid))
    if start == -1:
        return None
    line_start = text.rfind("\n", 0, start) + 1
    tag_end = text.find(">", start)
    if tag_end == -1 or text[tag_end - 1] == "/":
        raise ValueError(f"{sid}: a self-closing <Settlement/> is not a shape this tool rewrites")
    close = text.find("</Settlement>", tag_end)
    if close == -1:
        raise ValueError(f"{sid}: no </Settlement> after its open tag")
    end = close + len("</Settlement>")
    nl_end = text.find("\n", end)
    return line_start, (nl_end + 1 if nl_end != -1 else end)


def rename_block(block, r):
    """The block with every id-bearing attribute moved to the new id and `bound` repointed."""
    out = block.replace('id="' + r.old + '"', 'id="' + r.new + '"')
    out = out.replace(".name." + r.old + "}", ".name." + r.new + "}")
    out = out.replace(".text." + r.old + "}", ".text." + r.new + "}")
    out = out.replace('id="' + amv.comp_id(r.old) + '"', 'id="' + amv.comp_id(r.new) + '"')
    out, n = re.subn(r'\bbound="Settlement\.[^"]+"', 'bound="Settlement.' + r.bound + '"', out)
    if n != 1:
        raise ValueError(f"{r.old}: expected exactly one bound= attribute in its block, found {n}")
    if r.old in out:
        raise ValueError(f"{r.old}: the old id survives in its own block after the rewrite; inspect the block "
                         f"before shipping, the engine would otherwise read a half-renamed settlement")
    return out


def set_position(block, pos):
    if not pos:
        return block
    out, n = re.subn(r'\bposX="[^"]*" posY="[^"]*"', 'posX="' + pos[0] + '" posY="' + pos[1] + '"', block, count=1)
    if n != 1:
        raise ValueError("no posX/posY pair in the block")
    return out


def rename_in_master(text, r, pos):
    span = settlement_block_span(text, r.old)
    if span is None:
        raise ValueError(f"{r.old}: no <Settlement> row in settlements.xml")
    if _open_tag(r.new) in text:
        raise ValueError(f"{r.new}: already has a row; refusing to create a duplicate id")
    s, e = span
    return text[:s] + set_position(rename_block(text[s:e], r), pos) + text[e:]


_BLOCK_RE = re.compile(r'<Settlement id="([^"]+)".*?</Settlement>', re.S)


def bound_dependents(text, sid):
    """Ids of every village bound to `sid`, in file order."""
    needle = 'bound="Settlement.' + sid + '"'
    return [m.group(1) for m in _BLOCK_RE.finditer(text) if needle in m.group(0)]


def remove_from_master(text, sid):
    span = settlement_block_span(text, sid)
    if span is None:
        return text
    deps = bound_dependents(text, sid)
    if deps:
        raise ValueError(f"{sid}: {len(deps)} village(s) are bound to it ({', '.join(deps)}); rebind them first")
    s, e = span
    return text[:s] + text[e:]


def _loc_key_re(sid):
    return re.compile(r'<string id="Settlements\.Settlement\.[a-z]+\.' + re.escape(sid) + r'"')


def loc_rename(text, old, new):
    return re.sub(r'(<string id="Settlements\.Settlement\.[a-z]+\.)' + re.escape(old) + r'"',
                  lambda m: m.group(1) + new + '"', text)


def loc_remove(text, sid):
    nl = amv.detect_newline(text)
    pat = _loc_key_re(sid)
    return nl.join(line for line in text.split(nl) if not pat.search(line))


def rename_json_ids(text, old, new):
    return re.subn('"' + re.escape(old) + '"', '"' + new + '"', text)


def repo_references(root, ids):
    """{id: [repo-relative paths]} of every Main/**/*.cs and Main/_Module/ModuleData/**/*.json that
    names the id as a whole token. The settlements.xml shadow is XML and deliberately outside it."""
    root = Path(root)
    files = list((root / "Main").rglob("*.cs")) + list((root / "Main" / "_Module" / "ModuleData").rglob("*.json"))
    files = [f for f in files if not ({"bin", "obj"} & set(f.parts))]
    pats = {sid: re.compile("(?<![" + _ID_CHARS + "])" + re.escape(sid) + "(?![" + _ID_CHARS + "])") for sid in ids}
    hits = {sid: [] for sid in ids}
    for f in sorted(files):
        try:
            text = f.read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        for sid, pat in pats.items():
            if pat.search(text):
                hits[sid].append(f.relative_to(root).as_posix())
    return hits


def id_state(master, old, new):
    has_old, has_new = _open_tag(old) in master, _open_tag(new) in master
    if has_old and has_new:
        return "conflict"
    if has_old:
        return "pending"
    return "done" if has_new else "missing"


def _entity_present(scene, sid):
    return re.search(r'<game_entity name="' + re.escape(sid) + r'"', scene) is not None


def check(renames, removals, master, scene, loc_texts, tolerance=0.01):
    """Findings (strings) while any rename or removal is incomplete or its position drifted."""
    findings = []
    for r in renames:
        if _open_tag(r.old) in master:
            findings.append(f"{r.old}: still has a <Settlement> row in settlements.xml (the rename to {r.new} is not applied)")
        pos = amv.settlement_position(master, r.new)
        if not pos:
            findings.append(f"{r.new}: no <Settlement> row in settlements.xml")
        else:
            s, e = settlement_block_span(master, r.new)
            if 'bound="Settlement.' + r.bound + '"' not in master[s:e]:
                findings.append(f"{r.new}: not bound to {r.bound} in settlements.xml")
        for lang, text in loc_texts.items():
            if _loc_key_re(r.old).search(text):
                findings.append(f"{r.old}: loc key still present in Languages/{lang}/loc_settlements.xml")
            if f'id="Settlements.Settlement.name.{r.new}"' not in text:
                findings.append(f"{r.new}: no loc row in Languages/{lang}/loc_settlements.xml")
        entity = amv.scene_position(scene, r.new)
        if not entity:
            findings.append(f"{r.new}: no entity in scene.xscene (a campaign load would NRE SettlementVisual.OnStartup)")
        elif pos and (abs(float(pos[0]) - float(entity[0])) > tolerance or abs(float(pos[1]) - float(entity[1])) > tolerance):
            findings.append(f"{r.new}: settlements.xml has posX={pos[0]} posY={pos[1]} but scene.xscene has "
                            f"{entity[0]}, {entity[1]} (save the scene, or re-run --apply)")
    for sid in removals:
        if _open_tag(sid) in master:
            findings.append(f"{sid}: still has a <Settlement> row in settlements.xml")
        for lang, text in loc_texts.items():
            if _loc_key_re(sid).search(text):
                findings.append(f"{sid}: loc row still present in Languages/{lang}/loc_settlements.xml")
        if _entity_present(scene, sid):
            findings.append(f"{sid}: entity still in scene.xscene; delete it in the editor (an entity with no row is an inert icon)")
    return findings


def _write_json(path, text):
    json.loads(text)  # refuse to write a document that no longer parses
    open(path, "wb").write(text.encode("utf-8"))


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the live files and the repo id files (default: dry run)")
    ap.add_argument("--check", action="store_true", help="verify every rename and removal is complete; exit 1 otherwise")
    args = ap.parse_args()

    for p in (amv.LIVE, amv.SCENE):
        if not os.path.isfile(p):
            print(f"ERROR: not found: {p} (set BANNERLORD_GAME_DIR, see tools/_gamedir.py)", file=sys.stderr)
            return 2
    master_bom, master = amv._read(amv.LIVE)
    scene = open(amv.SCENE, "rb").read().decode("utf-8", errors="replace")
    locs = {lang: amv._read(amv._loc_path(lang)) for lang in amv.LANGS if os.path.isfile(amv._loc_path(lang))}
    if len(locs) != len(amv.LANGS):
        print(f"ERROR: expected {len(amv.LANGS)} loc_settlements.xml files, found {len(locs)}", file=sys.stderr)
        return 2
    loc_texts = {lang: text for lang, (_, text) in locs.items()}
    retired = [r.old for r in RENAMES] + list(REMOVALS)

    if args.check:
        findings = check(RENAMES, REMOVALS, master, scene, loc_texts)
        for sid, paths in repo_references(REPO_ROOT, retired).items():
            findings.extend(f"{sid}: still named in {p}" for p in paths)
        for f in findings:
            print("FAIL " + f)
        if findings:
            print(f"\n{len(findings)} finding(s) across {len(RENAMES)} rename(s) and {len(REMOVALS)} removal(s).")
            return 1
        print(f"OK: {len(RENAMES)} rename(s) and {len(REMOVALS)} removal(s) complete in settlements.xml, "
              f"all {len(locs)} loc files, scene.xscene and the repo; positions match.")
        return 0

    renames, problems = [], []
    for r in RENAMES:
        state = id_state(master, r.old, r.new)
        if state == "pending":
            renames.append(r)
        elif state != "done":
            problems.append(f"{r.old} -> {r.new}: {state} in settlements.xml")
    removals = [sid for sid in REMOVALS if _open_tag(sid) in master]
    loc_plan = {lang: ([r for r in RENAMES if _loc_key_re(r.old).search(text)],
                       [sid for sid in REMOVALS if _loc_key_re(sid).search(text)])
                for lang, text in loc_texts.items()}
    repo_plan = {}
    for rel in REPO_ID_FILES:
        text = (REPO_ROOT / rel).read_bytes().decode("utf-8")
        due = [r for r in RENAMES if '"' + r.old + '"' in text]
        if due:
            repo_plan[rel] = (text, due)

    hits = repo_references(REPO_ROOT, retired)
    for sid, paths in hits.items():
        for p in paths:
            if p not in REPO_ID_FILES:
                problems.append(f"{sid}: named in {p}, which this tool does not rewrite; move that reference first")
    for sid in removals:
        deps = bound_dependents(master, sid)
        if deps:
            problems.append(f"{sid}: {len(deps)} village(s) bound to it ({', '.join(deps)}); rebind them first")
    if problems:
        for p in problems:
            print("ERROR " + p)
        return 2

    if not renames and not removals and not repo_plan and not any(a or b for a, b in loc_plan.values()):
        print(f"all {len(RENAMES)} rename(s) and {len(REMOVALS)} removal(s) already applied everywhere; nothing to do. Try --check.")
        return 0

    positions = {}
    print(f"Plan: {len(renames)} rename(s), {len(removals)} removal(s) in settlements.xml")
    for r in renames:
        pos = amv.scene_position(scene, r.new)
        positions[r.new] = pos
        old_pos = amv.settlement_position(master, r.old)
        where = f"posX/posY {pos[0]}, {pos[1]} from the scene entity" if pos else \
            f"NO scene entity for {r.new}: position stays {old_pos[0]}, {old_pos[1]}; the editor's next save refreshes it"
        print(f"  {r.old:26} -> {r.new:18} bound {r.bound:12} {where}")
    for sid in removals:
        print(f"  {sid:26} -> removed" + ("" if not _entity_present(scene, sid) else "  (entity still in the scene: delete it in the editor)"))
    for lang in amv.LANGS:
        rn, rm = loc_plan[lang]
        print(f"  loc {lang:3}: {len(rn)} key rename(s), {len(rm)} row removal(s)" + ("" if rn or rm else " (nothing to do)"))
    for rel, (_, due) in repo_plan.items():
        print(f"  repo {rel}: {', '.join(r.old + ' -> ' + r.new for r in due)}")
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files.")
        return 0

    tag = "renamesettlements_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    if renames or removals:
        text = master
        for r in renames:
            text = rename_in_master(text, r, positions[r.new])
        for sid in removals:
            text = remove_from_master(text, sid)
        amv._write(amv.LIVE, master_bom, text, tag)
    written = []
    for lang, (bom, text) in locs.items():
        rn, rm = loc_plan[lang]
        if not rn and not rm:
            continue
        for r in rn:
            text = loc_rename(text, r.old, r.new)
        for sid in rm:
            text = loc_remove(text, sid)
        amv._write(amv._loc_path(lang), bom, text, tag)
        written.append(lang)
    for rel, (text, due) in repo_plan.items():
        for r in due:
            text, _ = rename_json_ids(text, r.old, r.new)
        _write_json(REPO_ROOT / rel, text)
    print(f"\nApplied: {len(renames)} rename(s) and {len(removals)} removal(s) in settlements.xml; loc files "
          f"written for {len(written)} of {len(locs)} languages ({', '.join(written) or 'none'}); "
          f"{len(repo_plan)} repo file(s); backups *.bak_{tag}; every written file re-parsed. Run --check.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
