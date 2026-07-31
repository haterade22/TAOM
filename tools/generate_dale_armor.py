#!/usr/bin/env python3
"""Generate Dale armor item XML for LOTRLOME_Armory from the mesh manifest.

Reads tools/dale_armor_meshes.txt (one mesh ID per line, output of
tpac_skeleton_scan.py --all-types on the 5 dale_kingdom .tpac files),
parses each ID, and emits per-slot XML files into the Armory's dale/ folder.

Naming convention (Solus's, preserved verbatim — note typos):
    [clo_]sk_dale_[lake_town_]<slot_word>_<class>_<variant>[_slim]

    slot_word ∈ {boots, chest, gauntlet, bracers, helmet, shoulder}
    class     ∈ {archer, chivlary, chivalry, infrantry, mariner}
    variant   ∈ {a01..a04, b01..b04}

Per-item rules:
- culture="Culture.sturgia" (Dale rides on the sturgia culture id; renamed
  via spcultures.xslt to "Barding")
- <Flags UseTeamColor="true" /> on every item (uniform per spec)
- covers_hands="false" on the 10 archer-gauntlet / Lake-town-bracer IDs in
  COVERS_HANDS_FALSE; all other arm-slot items get covers_hands="true"
- covers_legs="true" on all leg-slot items
- covers_body="true" on all body-slot items
- _slim variants are NOT authored as separate items; their base mesh is
  authored with has_gender_variations="true" so the engine auto-swaps for
  female agents

Usage:
    python tools/generate_dale_armor.py --dry-run
    python tools/generate_dale_armor.py --apply
    python tools/generate_dale_armor.py --apply --armory-path "<path>"
"""
import argparse
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

# =============================================================================
# DEFAULTS
# =============================================================================
ARMORY_BASE_DEFAULT = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
    r"\LOTRLOME_Armory\ModuleData\LOTRLOME_items\dale"
)
MESHES_DEFAULT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "dale_armor_meshes.txt")


# =============================================================================
# STAT BASELINES (cloned verbatim from generate_gondor_armor.py — culture-agnostic)
# =============================================================================
STAT_TIERS = {
    "head": {
        "light":  {"head_armor": 15, "weight": 1.5},
        "medium": {"head_armor": 24, "weight": 2.5},
        "heavy":  {"head_armor": 32, "weight": 3.5},
        "elite":  {"head_armor": 40, "weight": 4.5},
    },
    "body": {
        "light":  {"body_armor": 20, "leg_armor": 10, "weight": 8.0},
        "medium": {"body_armor": 32, "leg_armor": 16, "weight": 13.0},
        "heavy":  {"body_armor": 42, "leg_armor": 22, "weight": 18.0},
        "elite":  {"body_armor": 50, "leg_armor": 28, "weight": 22.0},
    },
    # Must mirror rebalance_armor.SHOULDER_BASELINES (pinned by
    # tools/tests/test_armor_curve_invariant.py::GeneratorCurveSyncTests).
    "shoulder": {
        "light":  {"body_armor": 5,  "arm_armor": 3,  "weight": 3.0},
        "medium": {"body_armor": 9,  "arm_armor": 6,  "weight": 5.0},
        "heavy":  {"body_armor": 13, "arm_armor": 11, "weight": 7.0},
        "elite":  {"body_armor": 19, "arm_armor": 17, "weight": 9.0},
    },
    "arm": {
        "light":  {"arm_armor": 8,  "weight": 0.6},
        "medium": {"arm_armor": 14, "weight": 1.0},
        "heavy":  {"arm_armor": 20, "weight": 1.5},
        "elite":  {"arm_armor": 26, "weight": 2.0},
    },
    "leg": {
        "light":  {"leg_armor": 12, "weight": 1.5},
        "medium": {"leg_armor": 20, "weight": 2.5},
        "heavy":  {"leg_armor": 28, "weight": 3.5},
        "elite":  {"leg_armor": 34, "weight": 4.0},
    },
}

# Body armor: arm_armor stat tier-by-tier (mirrors Gondor's noble line)
BODY_ARM_ARMOR = {"light": 6, "medium": 10, "heavy": 14, "elite": 20}

APPEARANCE = {"light": 1, "medium": 3, "heavy": 4, "elite": 6}

# Variant suffix -> tier
VARIANT_TIER = {
    "a01": "light",  "a02": "light",
    "a03": "medium", "a04": "medium",
    "b01": "heavy",  "b02": "heavy",
    "b03": "elite",  "b04": "elite",
}

# Slot word -> generic slot key + Bannerlord Type/subtype
SLOT_WORD_TO_SLOT = {
    "boots":    "leg",
    "chest":    "body",
    "gauntlet": "arm",
    "bracers":  "arm",
    "helmet":   "head",
    "shoulder": "shoulder",
}

SLOT_TYPES = {
    "head":     ("HeadArmor", "head_armor"),
    "body":     ("BodyArmor", "body_armor"),
    "shoulder": ("Cape",      "head_armor"),  # Bannerlord convention — capes/pauldrons use head_armor subtype
    "arm":      ("HandArmor", "hand_armor"),
    "leg":      ("LegArmor",  "leg_armor"),
}

# Output filename per slot key
SLOT_FILENAME = {
    "head":     "head_armors.xml",
    "body":     "body_armors.xml",
    "shoulder": "shoulder_armors.xml",
    "arm":      "arm_armors.xml",
    "leg":      "leg_armors.xml",
}

# Material/modifier-group lookup: (class_normalized, tier) -> (material, modifier_group)
# Dale's class identity:
#   archer    — Bard's heritage; light-to-medium leather/mail, heavy/elite mail
#   chivalry  — Royal knights / cavalry; mail at light, plate elsewhere ("burnished mail/plate")
#   infrantry — Royal Guard infantry; leather at light, mail at medium, plate heavy/elite
#   mariner   — Lake-town civilian/sailor; cloth/leather, mail only at elite veteran
MATERIAL_BY_CLASS_TIER = {
    "archer": {
        "light":  ("Cloth",     "cloth"),
        "medium": ("Leather",   "leather"),
        "heavy":  ("Chainmail", "chain"),
        "elite":  ("Chainmail", "chain"),
    },
    "chivalry": {
        "light":  ("Chainmail", "chain"),
        "medium": ("Plate",     "plate"),
        "heavy":  ("Plate",     "plate"),
        "elite":  ("Plate",     "plate"),
    },
    "infrantry": {
        "light":  ("Leather",   "leather"),
        "medium": ("Chainmail", "chain"),
        "heavy":  ("Plate",     "plate"),
        "elite":  ("Plate",     "plate"),
    },
    "mariner": {
        "light":  ("Cloth",     "cloth"),
        "medium": ("Leather",   "leather"),
        "heavy":  ("Leather",   "leather"),
        "elite":  ("Chainmail", "chain"),
    },
}

# Cloth overlay items (clo_*) — always cloth-styled regardless of class
OVERLAY_MATERIAL = ("Cloth", "cloth")

# Display names for classes
CLASS_DISPLAY = {
    "archer":    "Archer",
    "chivalry":  "Knight",   # also covers Solus's "chivlary" typo
    "infrantry": "Infantry", # also covers Solus's "infrantry" typo
    "mariner":   "Mariner",
}

# Display words per slot
SLOT_WORD_DISPLAY = {
    "boots":    "Boots",
    "chest":    "Armour",
    "gauntlet": "Gauntlets",
    "bracers":  "Bracers",
    "helmet":   "Helmet",
    "shoulder": "Pauldron",
}

# Tier prefix in display names (only heavy/elite get a qualifier — mirrors Gondor)
TIER_PREFIX = {"light": "", "medium": "", "heavy": "Heavy ", "elite": "Elite "}

# 10 user-specified bracer/archer-gauntlet IDs that must NOT cover the hand mesh
COVERS_HANDS_FALSE = {
    "sk_dale_gauntlet_archer_a01",
    "sk_dale_gauntlet_archer_a02",
    "sk_dale_gauntlet_archer_a03",
    "sk_dale_gauntlet_archer_b01",
    "sk_dale_gauntlet_archer_b02",
    "sk_dale_gauntlet_archer_b03",
    "sk_dale_lake_town_bracers_mariner_a01",
    "sk_dale_lake_town_bracers_mariner_a02",
    "sk_dale_lake_town_bracers_mariner_b01",
    "sk_dale_lake_town_bracers_mariner_b02",
}


# =============================================================================
# PARSER
# =============================================================================
# Pattern:
#   ^(clo_)?sk_dale_(lake_town_)?(boots|chest|gauntlet|bracers|helmet|shoulder)
#       _(archer|chivlary|chivalry|infrantry|mariner)_([ab][0-9]{2})(_slim)?$
MESH_RE = re.compile(
    r"^(clo_)?sk_dale_(lake_town_)?"
    r"(boots|chest|gauntlet|bracers|helmet|shoulder)_"
    r"(archer|chivlary|chivalry|infrantry|mariner)_"
    r"([ab][0-9]{2})(_slim)?$"
)


@dataclass
class ParsedMesh:
    raw_id: str
    is_overlay: bool
    is_lake_town: bool
    slot_word: str          # boots / chest / gauntlet / bracers / helmet / shoulder
    slot: str               # head / body / shoulder / arm / leg
    class_raw: str          # archer / chivlary / chivalry / infrantry / mariner (Solus's spelling)
    class_norm: str         # normalized: chivlary -> chivalry
    variant: str            # a01..b04
    tier: str               # light / medium / heavy / elite
    is_slim: bool


def parse_mesh(mesh_id: str) -> Optional[ParsedMesh]:
    m = MESH_RE.match(mesh_id)
    if not m:
        return None
    is_overlay = bool(m.group(1))
    is_lake_town = bool(m.group(2))
    slot_word = m.group(3)
    class_raw = m.group(4)
    variant = m.group(5)
    is_slim = bool(m.group(6))

    class_norm = "chivalry" if class_raw == "chivlary" else class_raw
    slot = SLOT_WORD_TO_SLOT[slot_word]
    tier = VARIANT_TIER[variant]

    return ParsedMesh(
        raw_id=mesh_id,
        is_overlay=is_overlay,
        is_lake_town=is_lake_town,
        slot_word=slot_word,
        slot=slot,
        class_raw=class_raw,
        class_norm=class_norm,
        variant=variant,
        tier=tier,
        is_slim=is_slim,
    )


# =============================================================================
# XML GENERATOR
# =============================================================================
def display_name(p: ParsedMesh) -> str:
    # The kingdom bracket prefix (e.g. "[Dale]") is added at the call site,
    # so the natural-language name drops the kingdom word and only annotates
    # the Lake-town sub-region when applicable.
    location = "Lake-town " if p.is_lake_town else ""
    overlay = "Cloth " if p.is_overlay else ""
    prefix = TIER_PREFIX[p.tier]
    cls = CLASS_DISPLAY[p.class_norm]
    slot = SLOT_WORD_DISPLAY[p.slot_word]
    variant_label = p.variant.upper()
    return f"{location}{overlay}{prefix}{cls} {slot} {variant_label}"


def material_and_modifier(p: ParsedMesh) -> tuple[str, str]:
    if p.is_overlay:
        return OVERLAY_MATERIAL
    return MATERIAL_BY_CLASS_TIER[p.class_norm][p.tier]


def generate_item_xml(p: ParsedMesh, has_slim_variant: bool) -> str:
    """Render one <Item> block. has_slim_variant=True flips has_gender_variations."""
    slot_type, subtype = SLOT_TYPES[p.slot]
    stats = STAT_TIERS[p.slot][p.tier]
    weight = stats["weight"]
    appearance = APPEARANCE[p.tier]
    material, modifier_group = material_and_modifier(p)

    armor_attrs: list[str] = []

    if p.slot == "head":
        armor_attrs.append(f'head_armor="{stats["head_armor"]}"')
        armor_attrs.append('has_gender_variations="false"')
        armor_attrs.append('hair_cover_type="all"')
        armor_attrs.append('beard_cover_type="all"')
        armor_attrs.append(f'modifier_group="{modifier_group}"')
        armor_attrs.append(f'material_type="{material}"')

    elif p.slot == "body":
        # Cloth overlays get reduced body_armor (they layer over real armor)
        if p.is_overlay:
            body_stat = max(8, stats["body_armor"] // 3)
            arm_stat = max(2, BODY_ARM_ARMOR[p.tier] // 3)
        else:
            body_stat = stats["body_armor"]
            arm_stat = BODY_ARM_ARMOR[p.tier]
        armor_attrs.append(f'body_armor="{body_stat}"')
        armor_attrs.append(f'arm_armor="{arm_stat}"')
        # Engine auto-swaps to <id>_slim mesh for female agents when this is true
        armor_attrs.append(f'has_gender_variations="{"true" if has_slim_variant else "false"}"')
        armor_attrs.append('covers_body="true"')
        armor_attrs.append(f'modifier_group="{modifier_group}"')
        armor_attrs.append(f'material_type="{material}"')

    elif p.slot == "shoulder":
        armor_attrs.append(f'body_armor="{stats["body_armor"]}"')
        armor_attrs.append(f'arm_armor="{stats["arm_armor"]}"')
        armor_attrs.append(f'modifier_group="{modifier_group}"')
        armor_attrs.append(f'material_type="{material}"')

    elif p.slot == "arm":
        armor_attrs.append(f'arm_armor="{stats["arm_armor"]}"')
        # User-specified list of archer gauntlets + mariner bracers don't cover the hand mesh
        covers = "false" if p.raw_id in COVERS_HANDS_FALSE else "true"
        armor_attrs.append(f'covers_hands="{covers}"')
        armor_attrs.append(f'modifier_group="{modifier_group}"')
        armor_attrs.append(f'material_type="{material}"')

    elif p.slot == "leg":
        armor_attrs.append(f'leg_armor="{stats["leg_armor"]}"')
        armor_attrs.append('covers_legs="true"')
        armor_attrs.append(f'modifier_group="{modifier_group}"')
        armor_attrs.append(f'material_type="{material}"')

    armor_str = " ".join(armor_attrs)
    name = display_name(p)
    loc_key = f"{{=aom_{p.raw_id}_name}}"

    return f'''    <Item
        id="{p.raw_id}"
        name="{loc_key}[Dale] {name}"
        subtype="{subtype}"
        mesh="{p.raw_id}"
        culture="Culture.sturgia"
        is_merchandise="true"
        weight="{weight}"
        difficulty="0"
        appearance="{appearance}"
        Type="{slot_type}">
        <ItemComponent>
            <Armor {armor_str} />
        </ItemComponent>
        <Flags UseTeamColor="true" />
    </Item>'''


# =============================================================================
# DRIVER
# =============================================================================
def load_manifest(path: str) -> list[str]:
    if not os.path.exists(path):
        print(f"ERROR: manifest not found: {path}", file=sys.stderr)
        sys.exit(1)
    with open(path, "r", encoding="utf-8") as f:
        return [line.strip() for line in f if line.strip()]


def categorize(manifest: list[str]) -> tuple[dict[str, list[ParsedMesh]], set[str], list[str]]:
    """Returns:
        per_slot      — slot_key -> list[ParsedMesh] (the items we'll author)
        bases_with_slim — set of raw_ids that have a corresponding _slim mesh
        skipped       — list of mesh IDs we couldn't parse or chose to skip (slims)
    """
    per_slot: dict[str, list[ParsedMesh]] = {k: [] for k in SLOT_FILENAME}
    parsed_by_id: dict[str, ParsedMesh] = {}
    skipped: list[str] = []

    for mesh_id in manifest:
        p = parse_mesh(mesh_id)
        if p is None:
            skipped.append(f"{mesh_id} (unparseable)")
            continue
        if p.is_overlay:
            skipped.append(f"{mesh_id} (clo_ cloth-sim overlay - not emitted as an item)")
            continue
        parsed_by_id[mesh_id] = p

    # Find _slim variants and link to base
    bases_with_slim: set[str] = set()
    for mesh_id, p in parsed_by_id.items():
        if p.is_slim:
            base_id = mesh_id[:-len("_slim")]
            if base_id in parsed_by_id:
                bases_with_slim.add(base_id)
            skipped.append(f"{mesh_id} (engine auto-derives from base via has_gender_variations)")

    # Categorize non-slim parsed entries
    for mesh_id, p in parsed_by_id.items():
        if p.is_slim:
            continue
        per_slot[p.slot].append(p)

    # Stable sort within each slot for deterministic output
    for slot_key in per_slot:
        per_slot[slot_key].sort(key=lambda x: (
            x.is_overlay, x.is_lake_town, x.class_norm, x.variant
        ))

    return per_slot, bases_with_slim, skipped


def dry_run(per_slot: dict[str, list[ParsedMesh]], bases_with_slim: set[str], skipped: list[str]):
    total = 0
    for slot_key, items in per_slot.items():
        print(f"\n=== {SLOT_FILENAME[slot_key]} ({len(items)} items) ===")
        for p in items:
            slim = " [+slim]" if p.raw_id in bases_with_slim else ""
            overlay = " [overlay]" if p.is_overlay else ""
            lake = " [Lake-town]" if p.is_lake_town else ""
            covers = ""
            if p.slot == "arm":
                covers = "  covers_hands=false" if p.raw_id in COVERS_HANDS_FALSE else "  covers_hands=true"
            mat, _ = material_and_modifier(p)
            print(f"  {p.raw_id:55s} [{p.tier:6s}] {mat:10s}{overlay}{lake}{slim}{covers}")
        total += len(items)
    print(f"\nTotal items to author: {total}")
    if bases_with_slim:
        print(f"\n_slim auto-derived bases: {len(bases_with_slim)}")
        for b in sorted(bases_with_slim):
            print(f"  {b} (has_gender_variations=\"true\")")
    if skipped:
        print(f"\nSkipped from manifest: {len(skipped)}")
        for s in skipped:
            print(f"  {s}")


def apply(armory_base: str, per_slot: dict[str, list[ParsedMesh]], bases_with_slim: set[str]):
    armory_path = Path(armory_base)
    armory_path.mkdir(parents=True, exist_ok=True)

    for slot_key, items in per_slot.items():
        filename = SLOT_FILENAME[slot_key]
        filepath = armory_path / filename
        if not items:
            print(f"  {filename}: 0 items, skipping")
            continue

        body_blocks = []
        for p in items:
            has_slim = p.raw_id in bases_with_slim
            body_blocks.append(generate_item_xml(p, has_slim_variant=has_slim))

        xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<Items>\n'
            '    <!-- Dale armor — auto-generated by tools/generate_dale_armor.py from -->\n'
            '    <!-- tools/dale_armor_meshes.txt. Do not hand-edit; regenerate instead. -->\n'
            '\n'
            + "\n\n".join(body_blocks)
            + '\n\n</Items>\n'
        )

        filepath.write_text(xml, encoding="utf-8")
        print(f"  {filename}: wrote {len(items)} items -> {filepath}")


def main():
    parser = argparse.ArgumentParser(description="Generate Dale armor items for LOTRLOME_Armory")
    parser.add_argument("--dry-run", action="store_true", help="List items only, no file writes")
    parser.add_argument("--apply", action="store_true", help="Write 5 XML files to the Armory dale/ folder")
    parser.add_argument("--armory-path", default=ARMORY_BASE_DEFAULT,
                        help=f"Path to LOTRLOME_Armory/ModuleData/LOTRLOME_items/dale/ (default: Steam install)")
    parser.add_argument("--meshes", default=MESHES_DEFAULT,
                        help=f"Path to dale mesh manifest (default: {MESHES_DEFAULT})")
    args = parser.parse_args()

    if not args.dry_run and not args.apply:
        parser.print_help()
        print("\nError: specify --dry-run or --apply", file=sys.stderr)
        sys.exit(2)

    manifest = load_manifest(args.meshes)
    per_slot, bases_with_slim, skipped = categorize(manifest)

    if args.dry_run:
        dry_run(per_slot, bases_with_slim, skipped)
    else:
        apply(args.armory_path, per_slot, bases_with_slim)
        print(f"\nDone. Authored items across 5 XML files in: {args.armory_path}")


if __name__ == "__main__":
    main()
