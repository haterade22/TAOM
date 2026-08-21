#!/usr/bin/env python3
"""Author the Black Numenorean weapon set (crafting pieces, presets, shields).

Touches the FOUR files the manual weapon workflow names
(docs/ai-includes/weapon-creation-workflow.md), in the game install AND the
assets-repo copy:

  1. LOTRLOME_crafting_pieces.xml        22 <CraftingPiece>
  2. weapon_descriptions.xslt            <AvailablePiece> registrations
  3. crafting_templates.xslt             <UsablePiece> registrations
  4. LOTRLOME_items/LOTRAOM_weapons.xml   7 <CraftedItem>
  5. LOTRLOME_items/LOTRAOM_shields.xml   6 <Item>

A piece id present in file 1 but missing from 2 or 3 makes the weapon fail to
load with NO log line, which is why all three are written in one pass.

Mesh names are the length-prefix-verified strings from
  <armory>/Assets/mordor_props/black_num_weapons/*_geo.tpac
NOT the spec doc. Two facts the doc gets wrong or omits:
  - The doc's "2H Sword Parts" block lists the 1H `bo_` hulls by copy-paste.
    The real 2H hulls are bo_sm_md_num_sword_2h_blade_{a,b,c} and they exist.
  - Only the `_a` shields carry collision bodies. inf_shield_*_b reuse the
    matching `_a` bo_ / bo_cap_ pair, the same hitbox-group pattern the orc
    shields use.

`body_name` must be `bo_` + the exact mesh id. An unresolvable body_name makes
PreloadHelper.WaitForMeshesToBeLoaded spin the main thread forever: no crash,
no log, one core pinned, the mission never loads (#352).

Usage:
    python tools/generate_black_numenorean_weapons.py --dry-run
    python tools/generate_black_numenorean_weapons.py --apply
"""
import argparse
import os
import re
import sys
from datetime import date

CULTURE = "Culture.mordor"
LINE = "Black Numenorean"

LIVE_MD = (r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
           r"\Modules\LOTRLOME_Armory\ModuleData")
REPO_MD = r"E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\ModuleData"

MARKER = "Black Numenorean weapons"

# ---------------------------------------------------------------------------
# Crafting pieces
# ---------------------------------------------------------------------------
# Damage anchored on measured shipped blades:
#   wm_mordor_set1_sword_a01_blade  tier 1   Pierce 1.78 / Cut 2.67
#   sm_uruk_sword_blade_a3          tier 3   Pierce 3.12 / Cut 3.74
#   wm_nazgul_sword_blade           tier 4   Pierce 3.10 / Cut 3.00   (hero)
#   wm_witch_king_sword_blade       tier 5   Pierce 3.50 / Cut 3.50   (hero)
#
# "Above uruk and below hero kit" is NOT a satisfiable constraint and an earlier
# draft of this file shipped believing it was: the shipped uruk TROOP blade
# already out-cuts both hero blades (3.74 against 3.50), so hero kit is not the
# game's ceiling on cut. The real ceiling for a troop blade is the best shipped
# troop blade, 3.74.
#
# So: lead the uruk on thrust (Numenorean steel takes a better point), and stay
# at or under 3.74 on cut. The first draft ran 3.8 to 4.0 cut, which beat every
# hero blade in the game. Deep review 2026-08-17 caught it.
ROMAN = {"a": "I", "b": "II", "c": "III"}

SWORD_1H_BLADES = [("a", 3.20, 3.60), ("b", 3.20, 3.60), ("c", 3.30, 3.70)]
SWORD_2H_BLADES = [("a", 3.30, 3.70), ("b", 3.30, 3.70), ("c", 3.40, 3.74)]


def piece_blade(pid, name, tier, length, weight, thrust, swing, excluded=None):
    exc = f' excluded_item_usage_features="{excluded}"' if excluded else ""
    dmg = ""
    if thrust:
        dmg += f'\n            <Thrust damage_type="Pierce" damage_factor="{thrust}" />'
    if swing:
        dmg += f'\n            <Swing damage_type="Cut" damage_factor="{swing}" />'
    # Sword blades carry the Civilian ItemFlag like every shipped Mordor blade,
    # so the weapon can be worn in civilian scenes. The lance (thrust-only) does
    # not: it follows the spear-head flag set instead.
    flags = ('<Flag name="CanKnockDown" /><Flag name="Civilian" type="ItemFlags" />'
             if swing else
             '<Flag name="CanKnockDown" /><Flag name="CanDismount" />'
             '<Flag name="CanHook" /><Flag name="NotStackable" type="ItemFlags" />')
    return (
        f'    <CraftingPiece id="{pid}" name="{{=aom_{pid}_name}}{name}" tier="{tier}" '
        f'piece_type="Blade" mesh="{pid}" length="{length}" weight="{weight}"{exc}>\n'
        f'        <BladeData stack_amount="3" physics_material="metal_weapon" '
        f'body_name="bo_{pid}">{dmg}\n'
        f'        </BladeData>\n'
        f'        <BuildData piece_offset="0" next_piece_offset="0" previous_piece_offset="0" />\n'
        f'        <Flags>{flags}</Flags>\n'
        # Iron6 x9 is the tier-4 mode and what every Mordor, uruk, Nazgul and
        # Witch King blade costs. An earlier draft keyed the count off blade
        # length and made the 1H blades and the lance cheaper to craft than the
        # tier-2 uruk blade they outclass.
        f'        <Materials><Material id="Iron6" count="9" /></Materials>\n'
        f'    </CraftingPiece>'
    )


def piece_plain(pid, name, ptype, tier, length, weight, offset, material, count,
                armor_bonus=None):
    extra = (f'\n        <StatContributions armor_bonus="{armor_bonus}" />'
             if armor_bonus is not None else "")
    return (
        f'    <CraftingPiece id="{pid}" name="{{=aom_{pid}_name}}{name}" tier="{tier}" '
        f'piece_type="{ptype}" mesh="{pid}" length="{length}" weight="{weight}">\n'
        f'        <BuildData piece_offset="{offset}" next_piece_offset="0" '
        f'previous_piece_offset="0" />{extra}\n'
        f'        <Materials><Material id="{material}" count="{count}" /></Materials>\n'
        f'    </CraftingPiece>'
    )


def build_pieces():
    """Returns (xml_blocks, {category: [piece_ids]}) for the XSLT registrations."""
    blocks = []
    onehanded, twohanded, polearm = [], [], []

    # --- 1H sword: 3 blades / 2 guards / 2 handles / 2 pommels ---------------
    for v, thr, sw in SWORD_1H_BLADES:
        pid = f"sm_md_num_sword_1h_blade_{v}"
        blocks.append(piece_blade(
            pid, f"{LINE} Sword Blade {ROMAN[v]}", 4, 80.0, 0.82, thr, sw))
        onehanded.append(pid)
    for v in "ab":
        pid = f"sm_md_num_sword_1h_guard_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Sword Guard {ROMAN[v]}",
                                  "Guard", 4, 2.0, 0.14, 0, "Iron5", 9, armor_bonus=5))
        onehanded.append(pid)
    for v in "ab":
        pid = f"sm_md_num_sword_1h_handle_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Sword Handle {ROMAN[v]}",
                                  "Handle", 4, 16.0, 0.18, 0, "Wood", 1))
        onehanded.append(pid)
    for v in "ab":
        pid = f"sm_md_num_sword_1h_pommel_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Sword Pommel {ROMAN[v]}",
                                  "Pommel", 4, 7.0, 0.11, 0, "Iron4", 1))
        onehanded.append(pid)

    # --- 2H sword: 3 blades / 3 guards / 3 handles / 2 pommels ---------------
    for v, thr, sw in SWORD_2H_BLADES:
        pid = f"sm_md_num_sword_2h_blade_{v}"
        blocks.append(piece_blade(
            pid, f"{LINE} Greatsword Blade {ROMAN[v]}", 4, 100.0, 1.35, thr, sw))
        twohanded.append(pid)
    for v in "abc":
        pid = f"sm_md_num_sword_2h_guard_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Greatsword Guard {ROMAN[v]}",
                                  "Guard", 4, 2.0, 0.18, 0, "Iron5", 9, armor_bonus=5))
        twohanded.append(pid)
    for v in "abc":
        pid = f"sm_md_num_sword_2h_handle_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Greatsword Handle {ROMAN[v]}",
                                  "Handle", 4, 22.0, 0.26, 0, "Wood", 1))
        twohanded.append(pid)
    for v in "ab":
        pid = f"sm_md_num_sword_2h_pommel_{v}"
        blocks.append(piece_plain(pid, f"{LINE} Greatsword Pommel {ROMAN[v]}",
                                  "Pommel", 4, 10.0, 0.14, 0, "Iron4", 1))
        twohanded.append(pid)

    # --- Lance: thrust-only head + long shaft --------------------------------
    # The head declares <Thrust> only, and the TwoHandedPolearm description
    # carries a `swing` token, so `swing` MUST be excluded or the lance gets a
    # swing attack with no swing damage (the 20-mace-head defect class).
    lance_blade = "sm_md_num_lance_blade_a"
    blocks.append(piece_blade(
        lance_blade, f"{LINE} Lance Head", 4, 25.0, 0.9,
        thrust=3.4, swing=None, excluded="swing"))
    lance_handle = "sm_md_num_lance_handle_a"
    blocks.append(piece_plain(lance_handle, f"{LINE} Lance Shaft",
                              "Handle", 4, 250.0, 2.1, 0, "Wood", 2))
    polearm += [lance_blade, lance_handle]

    return blocks, {"onehanded": onehanded, "twohanded": twohanded, "polearm": polearm}


# ---------------------------------------------------------------------------
# Crafted presets
# ---------------------------------------------------------------------------
CRAFTED = [
    ("sm_md_num_sword_1h_a", f"{LINE} Sword I", "OneHandedSword", None,
     [("sm_md_num_sword_1h_blade_a", "Blade"), ("sm_md_num_sword_1h_guard_a", "Guard"),
      ("sm_md_num_sword_1h_handle_a", "Handle"), ("sm_md_num_sword_1h_pommel_a", "Pommel")]),
    ("sm_md_num_sword_1h_b", f"{LINE} Sword II", "OneHandedSword", None,
     [("sm_md_num_sword_1h_blade_b", "Blade"), ("sm_md_num_sword_1h_guard_b", "Guard"),
      ("sm_md_num_sword_1h_handle_b", "Handle"), ("sm_md_num_sword_1h_pommel_b", "Pommel")]),
    ("sm_md_num_sword_1h_c", f"{LINE} Sword III", "OneHandedSword", None,
     [("sm_md_num_sword_1h_blade_c", "Blade"), ("sm_md_num_sword_1h_guard_a", "Guard"),
      ("sm_md_num_sword_1h_handle_b", "Handle"), ("sm_md_num_sword_1h_pommel_b", "Pommel")]),
    ("sm_md_num_sword_2h_a", f"{LINE} Greatsword I", "TwoHandedSword", None,
     [("sm_md_num_sword_2h_blade_a", "Blade"), ("sm_md_num_sword_2h_guard_a", "Guard"),
      ("sm_md_num_sword_2h_handle_a", "Handle"), ("sm_md_num_sword_2h_pommel_a", "Pommel")]),
    ("sm_md_num_sword_2h_b", f"{LINE} Greatsword II", "TwoHandedSword", None,
     [("sm_md_num_sword_2h_blade_b", "Blade"), ("sm_md_num_sword_2h_guard_b", "Guard"),
      ("sm_md_num_sword_2h_handle_b", "Handle"), ("sm_md_num_sword_2h_pommel_b", "Pommel")]),
    ("sm_md_num_sword_2h_c", f"{LINE} Greatsword III", "TwoHandedSword", None,
     [("sm_md_num_sword_2h_blade_c", "Blade"), ("sm_md_num_sword_2h_guard_c", "Guard"),
      ("sm_md_num_sword_2h_handle_c", "Handle"), ("sm_md_num_sword_2h_pommel_a", "Pommel")]),
    ("sm_md_num_lance_a", f"{LINE} Lance", "TwoHandedPolearm", "polearm",
     [("sm_md_num_lance_blade_a", "Blade"), ("sm_md_num_lance_handle_a", "Handle")]),
]


def crafted_xml(cid, name, template, modgroup, pieces):
    mg = f'\n        modifier_group="{modgroup}"' if modgroup else ""
    body = "\n".join(
        f'            <Piece id="{pid}" Type="{ptype}" scale_factor="100" />'
        for pid, ptype in pieces)
    return (
        f'    <CraftedItem\n'
        f'        id="{cid}"\n'
        f'        name="{{=aom_{cid}_name}}[Mordor] {name}"\n'
        f'        crafting_template="{template}"\n'
        f'        is_merchandise="true"\n'
        f'        culture="{CULTURE}"{mg}>\n'
        f'        <Pieces>\n{body}\n'
        f'        </Pieces>\n'
        f'    </CraftedItem>'
    )


# ---------------------------------------------------------------------------
# Shields
# ---------------------------------------------------------------------------
# Anchored on the shipped Mordor ladder (small 350 hp / mid 450 / tower 600)
# and Gondor's (360 / 420 / 520). Lengths are the spec doc's measured mesh
# lengths. All <Weapon> stats are whole numbers: the single-piece <Item>
# schema types them unsignedInt and a decimal is a hard load error.
#   id_suffix, body_stem, length, hp, weight, appearance, wood?, display
SHIELDS = [
    ("inf_shield_med_a",   "inf_shield_med_a",   118, 440, 4.5, 0.70, True,  "Infantry Shield I"),
    ("inf_shield_med_b",   "inf_shield_med_a",   118, 440, 4.5, 0.70, True,  "Infantry Shield II"),
    ("inf_shield_heavy_a", "inf_shield_heavy_a", 122, 505, 4.5, 0.80, True,  "Infantry Heavy Shield I"),
    ("inf_shield_heavy_b", "inf_shield_heavy_a", 122, 505, 4.5, 0.80, True,  "Infantry Heavy Shield II"),
    ("cav_shield_med_a",   "cav_shield_med_a",   111, 415, 4.0, 0.75, False, "Cavalry Shield"),
    ("cav_shield_heavy_a", "cav_shield_heavy_a", 116, 470, 4.5, 0.85, False, "Cavalry Heavy Shield"),
]


def shield_xml(suffix, body_stem, length, hp, weight, appearance, wooden, display):
    iid = f"sm_md_num_{suffix}"
    phys = "wood_shield" if wooden else "metal_shield"
    # hand_shield would need ForceAttachOffHandPrimaryItemBone instead; these are
    # forearm-strapped, matching every other Mordor shield. Never set both bones.
    wooden_parry = '\n            WoodenParry="true"' if wooden else ""
    return (
        f'    <Item\n'
        f'        id="{iid}"\n'
        f'        name="{{=aom_{iid}_name}}[Mordor] {LINE} {display}"\n'
        f'        body_name="bo_cap_sm_md_num_{body_stem}"\n'
        f'        shield_body_name="bo_sm_md_num_{body_stem}"\n'
        f'        recalculate_body="false"\n'
        f'        mesh="{iid}"\n'
        f'        culture="{CULTURE}"\n'
        f'        using_tableau="false"\n'
        f'        is_merchandise="true"\n'
        f'        weight="{weight}"\n'
        f'        appearance="{appearance}"\n'
        f'        Type="Shield"\n'
        f'        item_holsters="shield:shield_2:shield_3:shield_4"\n'
        f'        has_lower_holster_priority="true"\n'
        f'        holster_position_shift="-0.1,-0.1,0">\n'
        f'        <ItemComponent>\n'
        f'            <Weapon\n'
        f'                weapon_class="LargeShield"\n'
        f'                body_armor="1"\n'
        f'                thrust_speed="89"\n'
        f'                thrust_damage_type="Blunt"\n'
        f'                speed_rating="89"\n'
        f'                physics_material="{phys}"\n'
        f'                item_usage="shield"\n'
        f'                position="0.18, 0.14, -0.02"\n'
        f'                rotation="0.0,10.0,40.00"\n'
        f'                weapon_length="{length}"\n'
        f'                center_of_mass="-0.0,0.2,0.05"\n'
        f'                hit_points="{hp}"\n'
        f'                modifier_group="shield">\n'
        f'                <WeaponFlags\n'
        f'                    CanBlockRanged="true"\n'
        f'                    HasHitPoints="true" />\n'
        f'            </Weapon>\n'
        f'        </ItemComponent>\n'
        f'        <Flags{wooden_parry}\n'
        f'            HeldInOffHand="true"\n'
        f'            ForceAttachOffHandSecondaryItemBone="true" />\n'
        f'    </Item>'
    )


# ---------------------------------------------------------------------------
# Bows
# ---------------------------------------------------------------------------
# The Black Numenorean bow art already existed in the Armory and was missed on
# the first pass: the package is Assets/weapons/bow/Gondor/numenorean_steelbow_a_geo.tpac
# but the MESH inside it is `gondor_steelbow_a`, so a search of mesh names for
# "num" could never find it. Only the filename carries the provenance.
#
# Three Gondor items already share that mesh (gondor_steel_bow, _b, _starter),
# so authoring Mordor-culture items on it is established practice, not a clash.
# Nothing about Gondor's items changes.
#
# Placement: the best Men or Orc bow in the game is sm_rh_loke_longbow_a at 105
# damage and the best Elf bow is highelf_longbowd at 115, so a Black Numenorean
# bow belongs between 106 and 114. These sit at 107 and 111. Unlike the Rhun
# longbows they replace, this is a 113-length shortbow, which is the actual
# asset and suits a line that fields cavalry.
#
#   id, display, difficulty, thrust_damage, speed, missile, accuracy
BOWS = [
    ("sm_md_num_bow_a", "Numenorean Steelbow I",  60, 107, 96, 92, 93),
    ("sm_md_num_bow_b", "Numenorean Steelbow II", 80, 111, 97, 95, 96),
]

BOW_MESH = "gondor_steelbow_a"
BOW_BODY = "bo_short_bow_a"


def bow_xml(iid, display, difficulty, damage, speed, missile, accuracy):
    return (
        f'    <Item\n'
        f'        id="{iid}"\n'
        f'        name="{{=aom_{iid}_name}}[Mordor] {LINE} {display}"\n'
        f'        body_name="{BOW_BODY}"\n'
        f'        mesh="{BOW_MESH}"\n'
        f'        culture="{CULTURE}"\n'
        f'        is_merchandise="true"\n'
        f'        weight="1.2"\n'
        f'        difficulty="{difficulty}"\n'
        f'        appearance="0.1"\n'
        f'        Type="Bow"\n'
        f'        item_holsters="bow_hip:bow_hip_2:bow_back:bow_back_2">\n'
        f'        <ItemComponent>\n'
        f'            <Weapon\n'
        f'                weapon_class="Bow"\n'
        f'                ammo_class="Arrow"\n'
        f'                ammo_limit="1"\n'
        f'                thrust_speed="{speed}"\n'
        f'                speed_rating="{speed}"\n'
        f'                missile_speed="{missile}"\n'
        f'                weapon_length="113"\n'
        f'                accuracy="{accuracy}"\n'
        f'                thrust_damage="{damage}"\n'
        f'                thrust_damage_type="Pierce"\n'
        f'                item_usage="bow"\n'
        f'                physics_material="wood_weapon"\n'
        f'                center_of_mass="0.15,0,0"\n'
        f'                modifier_group="bow"\n'
        f'                position="0.0, 0.0, 0.0">\n'
        f'                <WeaponFlags\n'
        f'                    RangedWeapon="true"\n'
        f'                    HasString="true"\n'
        f'                    StringHeldByHand="true"\n'
        f'                    NotUsableWithOneHand="true"\n'
        f'                    TwoHandIdleOnMount="true"\n'
        f'                    AutoReload="true"\n'
        f'                    UnloadWhenSheathed="true" />\n'
        f'            </Weapon>\n'
        f'        </ItemComponent>\n'
        f'        <Flags ForceAttachOffHandPrimaryItemBone="true" />\n'
        f'    </Item>'
    )


# ---------------------------------------------------------------------------
# File I/O — byte-faithful, per-file line-ending aware, idempotent
# ---------------------------------------------------------------------------
def _piece_id(block):
    """The id="..." of a rendered <CraftingPiece> block."""
    m = re.search(r'<CraftingPiece id="([^"]+)"', block)
    if not m:
        raise ValueError(f"no CraftingPiece id in block: {block[:80]!r}")
    return m.group(1)


def _read(path):
    """newline='' keeps the file's own endings intact on the round-trip."""
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def _write(path, text, tag):
    stamp = date.today().strftime("%Y%m%d")
    bak = f"{path}.bak-blacknum-{stamp}"
    if not os.path.exists(bak):
        with open(bak, "w", encoding="utf-8", newline="") as f:
            f.write(_read(path))
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print(f"    wrote {tag}  (backup: {os.path.basename(bak)})")


def _eol(text):
    """The file's dominant line ending, so inserted text matches."""
    return "\r\n" if text.count("\r\n") > text.count("\n") - text.count("\r\n") else "\n"


def _fit(block, eol):
    return block.replace("\r\n", "\n").replace("\n", eol)


def insert_before_close(text, closing, block, header):
    eol = _eol(text)
    payload = _fit(f"{header}{block}{eol}{eol}", eol)
    return text.replace(closing, payload + closing, 1)


def insert_into_xslt(text, match_attr, lines, tagname, idattr):
    """Insert registration lines before the passthrough inside one template."""
    eol = _eol(text)
    start = text.find(match_attr)
    if start < 0:
        return text, f"template {match_attr} NOT FOUND"
    # Bound the search to THIS template. Without the bound, a template that has
    # lost its own passthrough silently borrows the next one and the
    # registrations land in the following template, which is the same
    # silent-weapon-death outcome this whole function exists to prevent.
    end = text.find("</xsl:template>", start)
    if end < 0:
        return text, f"template {match_attr} has no closing tag"
    passthrough = text.find("<xsl:apply-templates", start)
    if passthrough < 0 or passthrough > end:
        return text, f"passthrough inside {match_attr} NOT FOUND"
    # Reuse the indentation of the passthrough line.
    line_start = text.rfind("\n", 0, passthrough) + 1
    indent = text[line_start:passthrough]
    # Presence is checked across the WHOLE template body, not just the span up to
    # the passthrough where this function inserts. Another tool may legitimately
    # register pieces after the passthrough (the polearm/shield gate's
    # TAOM-1H-POLEARM block does exactly that), and a check bounded at the
    # insertion point cannot see those, so every re-run would duplicate them.
    todo = [i for i in lines if f'{idattr}="{i}"' not in text[start:end]]
    if not todo:
        return text, "already registered"
    block = "".join(f'{indent}<{tagname} {idattr}="{i}" />{eol}' for i in todo)
    return text[:line_start] + _fit(block, eol) + text[line_start:], f"+{len(todo)}"


DESC_CATEGORIES = {
    "onehanded": ["WeaponDescription[@id='OneHandedSword']/AvailablePieces"],
    "twohanded": ["WeaponDescription[@id='TwoHandedSword']/AvailablePieces"],
    # Long shafts + thrust heads register for couch and brace as well, or the
    # lance cannot be couched from horseback.
    #
    # OneHandedPolearm is deliberately NOT here. The lance IS registered there,
    # which is what lets it be used one- OR two-handed and lets its rider keep a
    # shield, but that registration is owned by tools/register_one_handed_polearms.py
    # (`sm_md_num_lance_a` is in its ONE_HANDED_ITEMS). That tool is the replay
    # half of the unversioned-Armory trap and writes a TAOM-1H-POLEARM marker
    # block; a second writer here would mean two tools appending the same pieces
    # to the same list. Verified 2026-08-21: with the lance registered, primary
    # usage resolves to OneHandedPolearm (shield allowed); without it, to
    # TwoHandedPolearm, which is `requires_no_shield`.
    "polearm":   ["WeaponDescription[@id='TwoHandedPolearm']/AvailablePieces",
                  "WeaponDescription[@id='TwoHandedPolearm_Couchable']/AvailablePieces",
                  "WeaponDescription[@id='TwoHandedPolearm_Bracing']/AvailablePieces"],
}
TEMPLATE_CATEGORIES = {
    "onehanded": ["CraftingTemplate[@id='OneHandedSword']/UsablePieces"],
    "twohanded": ["CraftingTemplate[@id='TwoHandedSword']/UsablePieces"],
    "polearm":   ["CraftingTemplate[@id='TwoHandedPolearm']/UsablePieces"],
}


def apply_to(md_dir, dry):
    """Transform all five files in memory, validate, THEN write.

    Two phases on purpose. A piece present in LOTRLOME_crafting_pieces.xml but
    missing from either XSLT makes the weapon fail to load with NO log line,
    which is why all of them are written together. An earlier version validated
    and wrote file-by-file, so a template that could not be found still left the
    crafting pieces (and possibly one XSLT) written before it bailed. That is the
    same half-applied state the single pass exists to avoid. Nothing is written
    now unless every transformation succeeded.
    """
    print(f"\n=== {md_dir}")
    if not os.path.isdir(md_dir):
        print("    ERROR: not a directory, skipping", file=sys.stderr)
        return
    blocks, cats = build_pieces()
    staged = []   # (path, new_text, label)
    reports = []
    failed = []

    # --- 1. crafting pieces --------------------------------------------------
    path = os.path.join(md_dir, "LOTRLOME_crafting_pieces.xml")
    t = _read(path)
    todo = [b for b in blocks if f'id="{_piece_id(b)}"' not in t]
    if todo:
        eol = _eol(t)
        header = (f"{eol}    <!-- {MARKER}: 1H sword, 2H sword, lance "
                  f"(sm_md_num_*) -->{eol}") if MARKER not in t else ""
        staged.append((path, insert_before_close(
            t, "</CraftingPieces>", (eol + eol).join(todo), header),
            "LOTRLOME_crafting_pieces.xml"))
        reports.append(f"    LOTRLOME_crafting_pieces.xml: +{len(todo)} pieces")
    else:
        reports.append(f"    LOTRLOME_crafting_pieces.xml: all {len(blocks)} present")

    # --- 2 + 3. XSLT registrations ------------------------------------------
    for fname, mapping, tag, idattr in [
        ("weapon_descriptions.xslt", DESC_CATEGORIES, "AvailablePiece", "id"),
        ("crafting_templates.xslt", TEMPLATE_CATEGORIES, "UsablePiece", "piece_id"),
    ]:
        path = os.path.join(md_dir, fname)
        t = _read(path)
        notes = []
        for cat, matches in mapping.items():
            for m in matches:
                t, note = insert_into_xslt(t, m, cats[cat], tag, idattr)
                label = m.split(chr(39))[1]
                notes.append(f"{label}:{note}")
                if "NOT FOUND" in note or "no closing tag" in note:
                    failed.append(f"{fname} -> {label} ({note})")
        reports.append(f"    {fname}: " + ", ".join(notes))
        # Skip a byte-identical rewrite so `wrote ...` never lies about a no-op.
        if any(n != "already registered" for n in notes):
            staged.append((path, t, fname))

    # --- 4. crafted presets --------------------------------------------------
    path = os.path.join(md_dir, "LOTRLOME_items", "LOTRAOM_weapons.xml")
    t = _read(path)
    todo = [c for c in CRAFTED if f'id="{c[0]}"' not in t]
    todo_bows = [b for b in BOWS if f'id="{b[0]}"' not in t]
    if todo or todo_bows:
        eol = _eol(t)
        header = (f"{eol}    <!-- {MARKER} -->{eol}") if MARKER not in t else ""
        blocks = [crafted_xml(*c) for c in todo] + [bow_xml(*b) for b in todo_bows]
        staged.append((path, insert_before_close(
            t, "</Items>", (eol + eol).join(blocks), header),
            "LOTRAOM_weapons.xml"))
        reports.append(f"    LOTRAOM_weapons.xml: +{len(todo)} crafted items, "
                       f"+{len(todo_bows)} bows")
    else:
        reports.append(f"    LOTRAOM_weapons.xml: all {len(CRAFTED)} crafted "
                       f"+ {len(BOWS)} bows present")

    # --- 5. shields ----------------------------------------------------------
    path = os.path.join(md_dir, "LOTRLOME_items", "LOTRAOM_shields.xml")
    t = _read(path)
    todo = [sh for sh in SHIELDS if f'id="sm_md_num_{sh[0]}"' not in t]
    if todo:
        eol = _eol(t)
        header = (f"{eol}    <!-- {MARKER} -->{eol}") if MARKER not in t else ""
        staged.append((path, insert_before_close(
            t, "</Items>", (eol + eol).join(shield_xml(*sh) for sh in todo), header),
            "LOTRAOM_shields.xml"))
        reports.append(f"    LOTRAOM_shields.xml: +{len(todo)} shields")
    else:
        reports.append(f"    LOTRAOM_shields.xml: all {len(SHIELDS)} present")

    for line in reports:
        print(line)

    # --- gate ----------------------------------------------------------------
    if failed:
        print("    FATAL: could not register every piece. NOTHING was written.",
              file=sys.stderr)
        for f in failed:
            print(f"      {f}", file=sys.stderr)
        sys.exit(1)

    if dry:
        return
    for path, text, label in staged:
        _write(path, text, label)


def main():
    ap = argparse.ArgumentParser(description="Black Numenorean weapon generator")
    ap.add_argument("--dry-run", action="store_true", help="Report only (default)")
    ap.add_argument("--apply", action="store_true", help="Write the files")
    ap.add_argument("--module-data", action="append", default=None,
                    help="LOTRLOME_Armory/ModuleData dir; repeat for multiple copies")
    args = ap.parse_args()
    targets = args.module_data or [LIVE_MD, REPO_MD]
    for t in targets:
        apply_to(t, dry=not args.apply)
    if not args.apply:
        print("\n*** dry run, nothing written ***")


if __name__ == "__main__":
    main()
