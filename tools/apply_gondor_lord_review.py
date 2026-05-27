#!/usr/bin/env python3
"""Apply Gondor Lord Review edits to lords.xml (Fix 2, 3, 4, Tier 2).

Plan file: ~/.claude/plans/gondor-lord-review-amrothos-zippy-frost.md

Run --dry-run first to see counts; --apply to write the file.
"""
import argparse
import re
import sys
from pathlib import Path

LORDS_XML = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData" / "characters" / "lords.xml"

# The 15 user-supplied female body keys (age=22.12, weight=0.206, build=0.5532, all-female adult Gondor).
FEMALE_KEYS = [
    "0015000D800010020F800A1A098007985FFD8660BE886A0167895C1885BB9A60008836030809B70500000000000000000000000000000000000000003F042002",
    "0015BC0E40000006C45AC7B638DC687A68794B86835B7987189C98C73DB8566800883603088427A500000000000000000000000000000000000000003F042002",
    "0015740E400000068B5AC7B638DC687A6879381B2A488B7A189C98C73DB8566800883603088427A500000000000000000000000000000000000000003F043002",
    "0015780FC000000B5BA6B6479BC8D64868798769E4627AB4189C98C73DB8566800883603088427A500000000000000000000000000000000000000003F040002",
    "00156C0C400020017CBB9A64A5876968687977788959D6AA189C98C73DB8566800883603088427A500000000000000000000000000000000000000003F042002",
    "0015B80D8000300AA47918D76753B24A6879516D792B7467189C98C73DB8566800883603088427A500000000000000000000000000000000000000003F040002",
    "0015580A4000000D94255675D562A945CD8B4547EBCC549678EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
    "0015E808C000000FCA58384479AAA669CD8BE9BB34625E7978EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F043002",
    "00152C0A400000103E255675D562A945CD8B7764558E9F4B78EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F043002",
    "00157C08C000000F7B58384479AAA669CD8B3DA7A36B99AA78EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F042002",
    "0015FC08C000000F9158384479AAA669CD8B367B4918A49378EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
    "00153806C000000F9158384479AAA669CD8B367B4918A49378EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
    "00153808C00000109158384479AAA669CD8B367B4918A49378EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
    "0015100FC00000109158384479AAA669CD8B367B4918A49378EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
    "0015100FC00000159158384479AAA669CD8B367B4918A49378EA83A9CC4A9A3500883603083C678E00000000000000000000000000000000000000003F041002",
]
assert len(FEMALE_KEYS) == 15

# Round-robin alphabetical (1-indexed key positions from plan).
BODY_KEY_ASSIGNMENTS = [
    # (lord_id, key_index)  --  key_index is 1-based into FEMALE_KEYS
    ("lord_1_11_3", 1),   # Ainarauta
    ("lord_1_62_1", 2),   # Arytha
    ("lord_1_45_5", 3),   # Belwen
    ("lord_1_45_1", 4),   # Berethiel (Vanyalos -> Berethiel)
    ("lord_1_45_6", 5),   # Brithwen
    ("lord_1_53_1", 6),   # Calaniel
    ("lord_EW_1_3", 7),   # Dorwen
    ("lord_1_59_3", 8),   # Galinwen
    ("lord_1_11_4", 9),   # Ivriniel (Imrahil's family)
    ("lord_1_52_3", 10),  # Ivriniel (Hirluin's family)
    ("lord_1_71_1", 11),  # Laswen
    ("lord_1_40_1", 12),  # Lindariel (Catella -> Lindariel)
    ("lord_1_45_4", 13),  # Loreth
    ("lord_1_9_5",  14),  # Lothwen
    ("lord_1_11_5", 15),  # Narniel
    ("lord_1_52_4", 1),   # Nauriel (wrap)
    ("lord_1_60_1", 2),   # Nimriel
    ("lord_1_60_2", 3),   # Orlathiel
    ("lord_1_73_1", 4),   # Popilia
    ("lord_1_59_1", 5),   # Rondiel
    ("lord_1_11_2", 6),   # Silwen
    ("lord_1_57_1", 7),   # Sophalia
    ("lord_1_46_1", 8),   # Thorwen (Seorgys -> Thorwen)
    # Fix 3 - Ciriel matches Dorwen's post-Fix-4 key (#7).
    ("lord_EW_1_1", 7),   # Ciriel
]
assert len(BODY_KEY_ASSIGNMENTS) == 24

# Fix 2 name renames (inline default value of {=KEY}DEFAULT).
NAME_RENAMES = [
    ("lord_1_40_1", "Catella", "Lindariel"),
    ("lord_1_45_1", "Vanyalos", "Berethiel"),
    ("lord_1_46_1", "Seorgys", "Thorwen"),
]

# Fix 2 + Tier 2 culture flips (Mordor -> Gondor for Gondor lords).
CULTURE_FLIPS = [
    "lord_1_40_1",  # Lindariel
    "lord_1_45_1",  # Berethiel
    "lord_1_45_2",  # Brandir
    "lord_1_45_3",  # Borlong
    "lord_1_46_1",  # Thorwen
    "lord_1_52_1",  # Arador
    "lord_1_52_2",  # Arvedui
    "lord_1_57_1",  # Sophalia
    "lord_1_57_2",  # Jephalia
    "lord_1_62_1",  # Arytha
    "lord_1_73_1",  # Popilia
]


def find_npc_block(text: str, npc_id: str) -> tuple[int, int]:
    """Return (start, end) byte offsets of the NPCCharacter block for npc_id.

    Block spans from the opening `<NPCCharacter id="..."` to its matching `</NPCCharacter>`.
    """
    pattern = r'<NPCCharacter id="' + re.escape(npc_id) + r'"[^>]*>'
    open_match = re.search(pattern, text)
    if not open_match:
        return None
    start = open_match.start()
    close = text.find("</NPCCharacter>", open_match.end())
    if close == -1:
        return None
    end = close + len("</NPCCharacter>")
    return (start, end)


def edit_block(block: str, npc_id: str, edits: dict) -> tuple[str, list[str]]:
    """Apply edits to a single NPCCharacter block. Returns (new_block, audit_messages)."""
    msgs = []
    new = block

    if "name_default" in edits:
        old_default, new_default = edits["name_default"]
        pattern = re.compile(r'(name="\{=' + re.escape(f"aom_{npc_id}_name") + r'\})' + re.escape(old_default) + r'(")')
        if pattern.search(new):
            new = pattern.sub(lambda m: m.group(1) + new_default + m.group(2), new, count=1)
            msgs.append(f"  name: {old_default} -> {new_default}")
        else:
            msgs.append(f"  !! name rename {old_default}->{new_default} pattern not found")

    if "culture" in edits:
        old_c, new_c = edits["culture"]
        pattern = re.compile(r'culture="' + re.escape(old_c) + r'"')
        if pattern.search(new):
            new = pattern.sub(f'culture="{new_c}"', new, count=1)
            msgs.append(f"  culture: {old_c} -> {new_c}")
        else:
            msgs.append(f"  !! culture flip {old_c}->{new_c} not found")

    if "body_key" in edits:
        old_k, new_k = edits["body_key"]
        # Replace within the FIRST BodyProperties element (the active one; the commented face_key block uses different tag)
        pattern = re.compile(r'(<BodyProperties [^>]*\bkey=")' + re.escape(old_k) + r'(")')
        if pattern.search(new):
            new = pattern.sub(lambda m: m.group(1) + new_k + m.group(2), new, count=1)
            msgs.append(f"  body_key: ...{old_k[-12:]} -> ...{new_k[-12:]} (key #{edits['_key_idx']})")
        else:
            msgs.append(f"  !! body_key swap not found (expected old key ending ...{old_k[-12:]})")

    return new, msgs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="Write changes (default is dry-run)")
    args = ap.parse_args()

    text = LORDS_XML.read_text(encoding="utf-8")
    original = text

    # Build per-NPC edit plan
    per_npc = {}
    for npc_id, key_idx in BODY_KEY_ASSIGNMENTS:
        per_npc.setdefault(npc_id, {})["body_key_idx"] = key_idx
    for npc_id, old_name, new_name in NAME_RENAMES:
        per_npc.setdefault(npc_id, {})["rename"] = (old_name, new_name)
    for npc_id in CULTURE_FLIPS:
        per_npc.setdefault(npc_id, {})["culture_flip"] = True

    # We need the CURRENT body key per NPC; re-grep on the fly per block.
    npc_ids = sorted(per_npc.keys())
    total_msgs = []
    for npc_id in npc_ids:
        bounds = find_npc_block(text, npc_id)
        if not bounds:
            total_msgs.append(f"{npc_id}: NPCCharacter block not found")
            continue
        block_start, block_end = bounds
        block = text[block_start:block_end]

        edits = {}
        if "rename" in per_npc[npc_id]:
            edits["name_default"] = per_npc[npc_id]["rename"]
        if per_npc[npc_id].get("culture_flip"):
            edits["culture"] = ("Culture.mordor", "Culture.gondor")
        if "body_key_idx" in per_npc[npc_id]:
            key_idx = per_npc[npc_id]["body_key_idx"]
            new_key = FEMALE_KEYS[key_idx - 1]
            # Grab the current body key
            cur = re.search(r'<BodyProperties [^>]*\bkey="([0-9A-Fa-f]+)"', block)
            if not cur:
                total_msgs.append(f"{npc_id}: BodyProperties key not found")
                continue
            old_key = cur.group(1)
            if old_key == new_key:
                total_msgs.append(f"{npc_id}: body key already matches target #{key_idx} -- skip")
            else:
                edits["body_key"] = (old_key, new_key)
                edits["_key_idx"] = key_idx

        if not edits:
            continue

        total_msgs.append(f"{npc_id}:")
        new_block, msgs = edit_block(block, npc_id, edits)
        total_msgs.extend(msgs)
        text = text[:block_start] + new_block + text[block_end:]

    diff_bytes = len(text) - len(original)

    print("\n".join(total_msgs))
    print(f"\nTotal NPCs touched: {len([m for m in total_msgs if m.endswith(':')])}")
    print(f"Net byte delta: {diff_bytes:+d}")

    if args.apply:
        LORDS_XML.write_text(text, encoding="utf-8")
        print(f"\nWROTE {LORDS_XML}")
    else:
        print("\n(dry-run -- pass --apply to write)")


if __name__ == "__main__":
    main()
