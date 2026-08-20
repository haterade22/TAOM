#!/usr/bin/env python3
"""Author the Black Numenorean armor set (sk_md_num_* / sm_md_num_*).

Source-of-truth for the mesh list: the shipped .tpac packages under
  <armory>/Assets/Mordor/black_num_armors/
verified by length-prefix extraction, NOT the spec doc. The doc
(E:\\repos\\lotraom-assets\\tools\\mordor_armor_and_troops.md, the
"Black Numenorean Armors" section) is marked DRAFT and has two errors this
generator works around:

  1. It claims every chest has a `_slim` variant. `sm_md_num_chest_light_a`
     does not, so only the 18 tiered chests get has_gender_variations="true".
  2. It lists the T7 cape pauldrons as `sm_*`; on disk they are `clo_*` cloth
     proxies with no plain renderable sibling and no cloth_bodies.xml entry.
     They are therefore NOT authored here. cape_heavy / cape_elite are plain
     `sm_` meshes and are authored normally.

Black Numenoreans are corrupted Men. The meshes rig to the standard humanoid
skeleton (the source FBX carry spine/spine1/spine2/l_clavicle/l_thigh/l_calf),
so the troops that wear these carry no `race=` attribute at all.

Stats come from rebalance_armor.calculate_stats() rather than a local copy of
the tier table. That is deliberate: generate_mordor_armor.py and its three
siblings each carry a private STAT_TIERS dict that went stale and silently
reverted a shipped fix (see tools/tests/test_armor_curve_invariant.py
GeneratorCurveSyncTests, 2026-07-31). Importing the curve makes that class of
drift impossible here, and applies Mordor's -1 protection / x1.10 weight from
CULTURAL_MODS instead of hardcoding the result.

Do NOT try to get the same effect by running
`rebalance_armor.py --apply --cultures mordor`: mordor is in PRESERVE_CULTURES
because its items are hand-authored, and a scoped run rewrites existing kit
(measured 2026-08-17: Sauron's Pauldrons 50 -> 24, Captain's Chainmail 19 -> 60).

Usage:
    python tools/generate_black_numenorean_armor.py --dry-run
    python tools/generate_black_numenorean_armor.py --apply
    python tools/generate_black_numenorean_armor.py --apply --armory-path <dir> --armory-path <dir>
"""
import argparse
import os
import sys
from dataclasses import dataclass
from datetime import date
from typing import Optional

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rebalance_armor as ra  # noqa: E402  (curve source of truth)

CULTURE = "mordor"
ITEM_NAME_PREFIX = "Mordor"
LINE_NAME = "Black Numenorean"

# The live game install is the copy the engine actually loads. The assets repo
# copy is versioned but has drifted (see the feature doc); both get the append
# because appending cannot collide with the existing drift.
LIVE_ARMORY = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\mordor"
)
REPO_ARMORY = (
    r"E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory"
    r"\ModuleData\LOTRLOME_items\mordor"
)

APPEARANCE = {"light": 1, "medium": 3, "heavy": 4, "elite": 6, "lord": 7}

ROMAN = {"a": "I", "b": "II", "c": "III", "d": "IV"}

# Body items carry arm_armor as their secondary, matching every other Mordor
# chest on disk. calculate_stats() returns leg_armor for the body slot, which is
# the curve's own choice of secondary; the Armory convention differs and the
# convention wins. Measured in body_armors.xml: of 111 items, 103 carry
# arm_armor and only 7 carry leg_armor.
#
# Keyed on the WEARER LEVEL rather than the row, because T7, T8 and T9 all sit on
# the `lord` row and would otherwise tie. This secondary is what separates them,
# and it is free to do so: it is outside the two-tier invariant, which governs
# the body slot through leg_armor. Every value here is inside the range already
# shipped in that file (5 through 45).
BODY_ARM_BY_LEVEL = {26: 10, 31: 20, 36: 26, 41: 30, 46: 36}


# ---------------------------------------------------------------------------
# Stat anchoring
# ---------------------------------------------------------------------------
# Stats come from the LEVEL OF THE LOWEST TROOP THAT WEARS THE PIECE, not from
# the mesh's own tier token. That is already the project convention:
# derive_armor_tiers.py anchors a shared item to its lowest wearer. This applies
# it at authoring time instead of only reporting on it afterwards.
#
# It is needed because the KEYforce distribution deliberately puts low-tier
# meshes on high-level troops (light hoods on the level-31 Archer, light greaves
# too). Statting those by name gives the level-26 Initiate 50 total armour, below
# the entire level-26 cohort's floor of 82, and puts four of five tiers in the
# bottom quartile.
#
# The rows are the top of the shipped curve, because the Black Numenoreans are a
# rare elite line and are meant to out-armour every other Men or Orc troop,
# yielding only to Elves and Dwarves. That deliberately overrides #342 (which
# holds Mordor-exclusive kit two under Gondor's) for this line alone.
#
# Measured ceilings, best non-elf non-dwarf troop at each level:
#   L26 211, L31 226, L36 245, L41 250, L46 259.
# Best dwarf at L46 is 276 (erebor_noble_royal_warden), so the target window at
# the top is 260 to 275, sixteen points wide.
#
# A brand-new row above `lord` was ruled out by arithmetic, not taste: the
# two-tier invariant makes its minimum legal 5-slot total 278, which overshoots
# the dwarves. `lord` plus a lifted body arm secondary lands at 261, inside the
# window. The body's arm_armor is an Armory convention rather than the curve's
# governed secondary (the curve uses leg_armor there), so raising it does not
# touch the invariant.
#
# Resulting per-tier totals: 109 / 186 / 232 / 255 / 261, strictly increasing,
# topping the Men/Orc band at T8 and T9. T5 to T7 sit under their level's best
# because T5 is the entry tier and the distribution shares hood+greaves down to
# T6 and pauldron+bracer down to T7, which anchors both to the tier below.
LEVEL_ROW = {26: "heavy", 31: "elite", 36: "lord", 41: "lord", 46: "lord"}

K = "sk_md_num_"   # skinned: hoods, helmets, bracers
I = "sm_md_num_"   # static: chests, pauldrons, greaves

# item id -> level of its lowest wearer. Mirrors the rosters in
# tools/apply_black_numenorean_troops.py; test_black_numenorean_anchor.py fails
# if the two ever drift.
ANCHOR_LEVEL = {}
for _lv, _ids in {
    26: [K + "hood_light_a", K + "hood_light_b", I + "chest_light_a", I + "grvs_light_a"],
    31: [I + f"{l}_chest_med_{v}" for l in ("arc", "cav", "inf") for v in "ab"]
        + [I + f"{l}_pauld_med_a" for l in ("arc", "cav", "inf")]
        + [K + "arc_bracer_med_a", K + "inf_bracer_med_a",
           K + "cav_helmet_med_a", K + "inf_helmet_med_a"],
    36: [K + "hood_med_a", K + "hood_med_b", I + "grvs_med_a"]
        + [I + f"{l}_chest_heavy_b" for l in ("arc", "cav", "inf")],
    41: [K + f"hood_heavy_{v}" for v in "abcd"]
        + [I + f"{l}_chest_heavy_a" for l in ("arc", "cav", "inf")]
        + [I + "arc_pauld_heavy_a", I + "arc_pauld_heavy_b",
           I + "cav_pauld_heavy_a", I + "cav_pauld_cape_heavy_a",
           I + "inf_pauld_heavy_a", I + "inf_pauld_cape_heavy_a",
           K + "arc_bracer_heavy_a", K + "inf_bracer_heavy_a",
           K + "cav_helmet_heavy_a", K + "cav_helmet_heavy_b",
           K + "inf_helmet_heavy_a", K + "inf_helmet_heavy_b",
           I + "grvs_heavy_a"],
    46: [K + f"hood_elite_{v}" for v in "abcd"]
        + [I + f"{l}_chest_elite_{v}" for l in ("arc", "cav", "inf") for v in "ab"]
        + [I + "arc_pauld_elite_a", I + "arc_pauld_elite_b",
           I + "cav_pauld_elite_a", I + "cav_pauld_cape_elite_a",
           I + "inf_pauld_elite_a", I + "inf_pauld_elite_b",
           I + "inf_pauld_cape_elite_a", I + "inf_pauld_cape_elite_b",
           K + "arc_bracer_elite_a", K + "inf_bracer_elite_a",
           K + "cav_helmet_elite_a", K + "cav_helmet_elite_b",
           K + "inf_helmet_elite_a", K + "inf_helmet_elite_b",
           I + "grvs_elite_a"],
}.items():
    for _i in _ids:
        ANCHOR_LEVEL[_i] = _lv

# Nothing wears these, so there is no wearer level to anchor to. The 8 lord
# pieces are hero-reserved by decision; hood_a / hood_b are untiered plain hoods
# the spec never lists.
UNWORN_ROW = {
    K + "hood_a": "medium", K + "hood_b": "medium",
    K + "hood_lord_a": "lord", K + "hood_lord_b": "lord",
    K + "cav_helmet_lord_a": "lord", K + "cav_helmet_lord_b": "lord",
    K + "inf_helmet_lord_a": "lord", K + "inf_helmet_lord_b": "lord",
    K + "arc_bracer_lord_a": "lord", K + "inf_bracer_lord_a": "lord",
}


def _row_for(item_id: str) -> str:
    """Curve row for an item, from its lowest wearer's level."""
    if item_id in ANCHOR_LEVEL:
        return LEVEL_ROW[ANCHOR_LEVEL[item_id]]
    if item_id in UNWORN_ROW:
        return UNWORN_ROW[item_id]
    raise KeyError(
        f"{item_id} is in neither ANCHOR_LEVEL nor UNWORN_ROW. Every authored item "
        f"needs one: add it to the roster map if a troop wears it, or to UNWORN_ROW.")


@dataclass
class ArmorItem:
    id: str
    display_name: str
    slot: str
    covers_body: bool = False
    covers_hands: bool = False
    covers_legs: bool = False
    gender_variations: bool = False
    tier: str = ""                          # derived from the wearer level
    arm_armor_stat: Optional[int] = None    # derived for the body slot

    def __post_init__(self):
        self.tier = _row_for(self.id)
        if self.slot == "body" and self.arm_armor_stat is None:
            lv = ANCHOR_LEVEL.get(self.id)
            # Unworn body items (none today) fall back to the row's own level.
            self.arm_armor_stat = BODY_ARM_BY_LEVEL[lv] if lv else 26


def _build():
    head, body, shoulder, arm, leg = [], [], [], [], []

    # --- Hoods (archer line, also the shared pre-split helmet) -------------
    # hood_a / hood_b are untiered and absent from the spec doc. They are plain
    # cloth hoods; authored at the light row and left off every troop roster.
    for v in "ab":
        head.append(ArmorItem(
            f"sk_md_num_hood_{v}", f"{LINE_NAME} Plain Hood {ROMAN[v]}",
            "head"))
    for tok, label, letters in [
        ("light", "Light Hood", "ab"),
        ("med", "Hood", "ab"),
        ("heavy", "Heavy Hood", "abcd"),
        ("elite", "Elite Hood", "abcd"),
        ("lord", "Lord's Hood", "ab"),
    ]:
        for v in letters:
            head.append(ArmorItem(
                f"sk_md_num_hood_{tok}_{v}",
                f"{LINE_NAME} {label} {ROMAN[v]}",
                "head"))

    # --- Infantry + Cavalry helmets ---------------------------------------
    for lid, lname in [("inf", "Infantry"), ("cav", "Cavalry")]:
        for tok, label, letters in [
            ("med", "Helmet", "a"),
            ("heavy", "Heavy Helmet", "ab"),
            ("elite", "Elite Helmet", "ab"),
            ("lord", "Lord's Helmet", "ab"),
        ]:
            for v in letters:
                head.append(ArmorItem(
                    f"sk_md_num_{lid}_helmet_{tok}_{v}",
                    f"{LINE_NAME} {lname} {label} {ROMAN[v]}",
                    "head"))

    # --- Chests ------------------------------------------------------------
    # The shared pre-split chest. No _slim sibling ships, so no gender variations.
    body.append(ArmorItem(
        "sm_md_num_chest_light_a", f"{LINE_NAME} Initiate Chest",
        "body", covers_body=True))
    for lid, lname in [("arc", "Archer"), ("cav", "Cavalry"), ("inf", "Infantry")]:
        for tok, label in [("med", "Chest"), ("heavy", "Heavy Chest"), ("elite", "Elite Chest")]:
            for v in "ab":
                body.append(ArmorItem(
                    f"sm_md_num_{lid}_chest_{tok}_{v}",
                    f"{LINE_NAME} {lname} {label} {ROMAN[v]}",
                    "body", covers_body=True,
                    gender_variations=True))  # all 18 tiered chests ship a _slim

    # --- Pauldrons ---------------------------------------------------------
    # Only the meshes that actually ship as plain sm_ geometry. The T7 cape
    # (clo_sm_md_num_{cav,inf}_pauld_cape_a) is cloth-sim only and is skipped.
    pauldrons = [
        ("arc", "Archer", [("med", "Pauldron", "a"),
                           ("heavy", "Heavy Pauldron", "ab"),
                           ("elite", "Elite Pauldron", "ab")]),
        ("cav", "Cavalry", [("med", "Pauldron", "a"),
                            ("heavy", "Heavy Pauldron", "a"),
                            ("elite", "Elite Pauldron", "a")]),
        ("inf", "Infantry", [("med", "Pauldron", "a"),
                             ("heavy", "Heavy Pauldron", "a"),
                             ("elite", "Elite Pauldron", "ab")]),
    ]
    for lid, lname, rows in pauldrons:
        for tok, label, letters in rows:
            for v in letters:
                shoulder.append(ArmorItem(
                    f"sm_md_num_{lid}_pauld_{tok}_{v}",
                    f"{LINE_NAME} {lname} {label} {ROMAN[v]}",
                    "shoulder"))
    # Caped variants, heavy and elite only.
    capes = [
        ("cav", "Cavalry", [("heavy", "a"), ("elite", "a")]),
        ("inf", "Infantry", [("heavy", "a"), ("elite", "ab")]),
    ]
    for lid, lname, rows in capes:
        for tok, letters in rows:
            for v in letters:
                label = "Caped Heavy Pauldron" if tok == "heavy" else "Caped Elite Pauldron"
                shoulder.append(ArmorItem(
                    f"sm_md_num_{lid}_pauld_cape_{tok}_{v}",
                    f"{LINE_NAME} {lname} {label} {ROMAN[v]}",
                    "shoulder"))

    # --- Bracers -----------------------------------------------------------
    # The spec states every Black Numenorean bracer covers the hand, so these
    # carry covers_hands="true" (the opposite of the Morannon set, whose meshes
    # deliberately show the fingers).
    for lid, lname in [("arc", "Archer"), ("inf", "Infantry")]:
        for tok, label in [("med", "Bracer"), ("heavy", "Heavy Bracer"),
                           ("elite", "Elite Bracer"), ("lord", "Lord's Bracer")]:
            arm.append(ArmorItem(
                f"sk_md_num_{lid}_bracer_{tok}_a",
                f"{LINE_NAME} {lname} {label}",
                "arm", covers_hands=True))

    # --- Greaves (shared across all three lines) ---------------------------
    for tok, label in [("light", "Light Greaves"), ("med", "Greaves"),
                       ("heavy", "Heavy Greaves"), ("elite", "Elite Greaves")]:
        leg.append(ArmorItem(
            f"sm_md_num_grvs_{tok}_a", f"{LINE_NAME} {label}",
            "leg", covers_legs=True))

    return head, body, shoulder, arm, leg


HEAD_ARMORS, BODY_ARMORS, SHOULDER_ARMORS, ARM_ARMORS, LEG_ARMORS = _build()

SLOT_MAP = {
    "head":     (HEAD_ARMORS,     "head_armors.xml"),
    "body":     (BODY_ARMORS,     "body_armors.xml"),
    "shoulder": (SHOULDER_ARMORS, "shoulder_armors.xml"),
    "arm":      (ARM_ARMORS,      "arm_armors.xml"),
    "leg":      (LEG_ARMORS,      "leg_armors.xml"),
}

SLOT_TYPES = {
    "head":     ("HeadArmor", "head_armor"),
    "body":     ("BodyArmor", "body_armor"),
    "shoulder": ("Cape",      "head_armor"),
    "arm":      ("HandArmor", "hand_armor"),
    "leg":      ("LegArmor",  "leg_armor"),
}

SECTION_MARKER = "KEYforce Black Numenorean armor"
SECTION_COMMENT = (
    "\n    <!-- ============================================================== -->\n"
    "    <!--  KEYforce Black Numenorean armor (sk_md_num_*, sm_md_num_*)    -->\n"
    "    <!--  Corrupted Men: human skeleton, troops carry NO race attribute -->\n"
    "    <!--  Spec: lotraom-assets/tools/mordor_armor_and_troops.md         -->\n"
    "    <!-- ============================================================== -->\n\n"
)


def generate_item_xml(item: ArmorItem) -> str:
    slot_type, subtype = SLOT_TYPES[item.slot]
    stats = ra.calculate_stats(item.tier, item.slot, CULTURE)
    weight = stats["weight"]
    material = stats["material_type"]
    modifier_group = stats["modifier_group"]
    appearance = APPEARANCE[item.tier]

    attrs = []
    if item.slot == "head":
        attrs += [
            f'head_armor="{stats["head_armor"]}"',
            'has_gender_variations="false"',
            'hair_cover_type="all"',
            f'modifier_group="{modifier_group}"',
            f'material_type="{material}"',
            'beard_cover_type="all"',
        ]
    elif item.slot == "body":
        attrs.append(f'body_armor="{stats["body_armor"]}"')
        if item.arm_armor_stat is not None:
            attrs.append(f'arm_armor="{item.arm_armor_stat}"')
        attrs.append(f'has_gender_variations="{"true" if item.gender_variations else "false"}"')
        if item.covers_body:
            attrs.append('covers_body="true"')
        attrs.append(f'modifier_group="{modifier_group}"')
        attrs.append(f'material_type="{material}"')
    elif item.slot == "shoulder":
        attrs += [
            f'body_armor="{stats["body_armor"]}"',
            f'arm_armor="{stats["arm_armor"]}"',
            f'modifier_group="{modifier_group}"',
            f'material_type="{material}"',
        ]
    elif item.slot == "arm":
        attrs += [
            f'arm_armor="{stats["arm_armor"]}"',
            f'covers_hands="{"true" if item.covers_hands else "false"}"',
            f'modifier_group="{modifier_group}"',
            f'material_type="{material}"',
        ]
    elif item.slot == "leg":
        attrs.append(f'leg_armor="{stats["leg_armor"]}"')
        if item.covers_legs:
            attrs.append('covers_legs="true"')
        attrs.append(f'modifier_group="{modifier_group}"')
        attrs.append(f'material_type="{material}"')

    return (
        f'    <Item\n'
        f'        id="{item.id}"\n'
        f'        name="{{=aom_{item.id}_name}}[{ITEM_NAME_PREFIX}] {item.display_name}"\n'
        f'        subtype="{subtype}"\n'
        f'        mesh="{item.id}"\n'
        f'        culture="Culture.{CULTURE}"\n'
        f'        is_merchandise="true"\n'
        f'        weight="{weight}"\n'
        f'        difficulty="0"\n'
        f'        appearance="{appearance}"\n'
        f'        Type="{slot_type}">\n'
        f'        <ItemComponent>\n'
        f'            <Armor {" ".join(attrs)} />\n'
        f'        </ItemComponent>\n'
        f'        <Flags UseTeamColor="true" />\n'
        f'    </Item>'
    )


def _eol(text):
    """The file's dominant line ending, so inserted text matches it.

    Majority rather than "any CRLF wins": the mordor/*_armors.xml files are
    already mixed (measured 2356 CRLF against 517 bare LF in head_armors.xml),
    so a presence test picks the wrong answer on exactly the files this writes.
    """
    crlf = text.count("\r\n")
    return "\r\n" if crlf > text.count("\n") - crlf else "\n"


def _fit(block, eol):
    return block.replace("\r\n", "\n").replace("\n", eol)


def _pending(armory_base):
    """Per-file (new_items, already_present) without writing anything."""
    out = {}
    for _slot, (items, filename) in SLOT_MAP.items():
        path = os.path.join(armory_base, filename)
        if not os.path.exists(path):
            out[filename] = (items, [], None)
            continue
        with open(path, "r", encoding="utf-8", newline="") as f:
            content = f.read()
        present = [i for i in items if f'id="{i.id}"' in content]
        new = [i for i in items if i not in present]
        out[filename] = (new, present, content)
    return out


def dry_run(armory_base=None):
    total = 0
    pending = _pending(armory_base) if armory_base and os.path.isdir(armory_base) else None
    for _slot, (items, filename) in SLOT_MAP.items():
        if pending:
            new, present, _ = pending[filename]
            print(f"\n=== {filename} ({len(items)} items: "
                  f"+{len(new)} new, {len(present)} already present) ===")
        else:
            print(f"\n=== {filename} ({len(items)} items) ===")
        for item in items:
            stats = ra.calculate_stats(item.tier, item.slot, CULTURE)
            shown = ", ".join(
                f"{k}={v}" for k, v in stats.items()
                if k not in ("weight", "material_type", "modifier_group"))
            if item.arm_armor_stat is not None:
                shown += f", arm_armor={item.arm_armor_stat}(override)"
            print(f"  {item.id:44s} [{item.tier:6s}] {shown}"
                  f"  w={stats['weight']} {stats['material_type']}")
        total += len(items)
    print(f"\nTotal: {total} items")


def apply_to(armory_base: str):
    print(f"\nTarget: {armory_base}")
    if not os.path.isdir(armory_base):
        print(f"  ERROR: not a directory, skipping", file=sys.stderr)
        return 0, 0
    added = skipped = 0
    stamp = date.today().strftime("%Y%m%d")
    for _slot, (items, filename) in SLOT_MAP.items():
        filepath = os.path.join(armory_base, filename)
        if not os.path.exists(filepath):
            print(f"  ERROR: {filepath} not found", file=sys.stderr)
            continue

        # newline="" preserves the file's existing CRLF round-trip.
        with open(filepath, "r", encoding="utf-8", newline="") as f:
            original = f.read()

        present = {i.id for i in items if f'id="{i.id}"' in original}
        new_items = [i for i in items if i.id not in present]
        skipped += len(present)
        if not new_items:
            print(f"  {filename}: all {len(items)} already present, skipping")
            continue

        closing = "</Items>"
        if closing not in original:
            print(f"  ERROR: {closing} not found in {filepath}", file=sys.stderr)
            continue

        header = "" if SECTION_MARKER in original else SECTION_COMMENT
        new_xml = "\n\n".join(generate_item_xml(i) for i in new_items)
        # Match the file's own line endings, and replace exactly ONE closing tag.
        eol = _eol(original)
        payload = _fit(f"{header}{new_xml}\n\n", eol)
        updated = original.replace(closing, payload + closing, 1)

        # Backup on a non-.xml extension: the folder is globbed *.xml, so an
        # .xml backup injects duplicate item ids at load. Never overwrite an
        # existing same-day backup: a partial re-run (one new mesh added) would
        # otherwise replace the pristine copy with already-modified content.
        bak = f"{filepath}.bak-blacknum-{stamp}"
        if not os.path.exists(bak):
            with open(bak, "w", encoding="utf-8", newline="") as f:
                f.write(original)
        with open(filepath, "w", encoding="utf-8", newline="") as f:
            f.write(updated)

        note = f" ({len(present)} already present)" if present else ""
        print(f"  {filename}: +{len(new_items)} items{note}  (backup: {os.path.basename(bak)})")
        added += len(new_items)
    return added, skipped


def main():
    p = argparse.ArgumentParser(description="Black Numenorean armor generator")
    p.add_argument("--dry-run", action="store_true", help="List items only (default)")
    p.add_argument("--apply", action="store_true", help="Append to the XML files")
    p.add_argument("--armory-path", action="append", default=None,
                   help="LOTRLOME_items/mordor dir; repeat for multiple copies. "
                        "Defaults to the live install plus the assets-repo copy.")
    args = p.parse_args()

    if args.apply:
        targets = args.armory_path or [LIVE_ARMORY, REPO_ARMORY]
        total_added = total_skipped = 0
        for t in targets:
            a, s = apply_to(t)
            total_added += a
            total_skipped += s
        print(f"\nDone. Added {total_added}, skipped {total_skipped} already present.")
    else:
        targets = args.armory_path or [LIVE_ARMORY, REPO_ARMORY]
        dry_run(targets[0])


if __name__ == "__main__":
    main()
