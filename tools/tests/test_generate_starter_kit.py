#!/usr/bin/env python3
"""Unit tests for tools/generate_starter_kit.py, on synthetic XML, no game install.

Run:  python -m unittest tools.tests.test_generate_starter_kit

Every test pins a way the generator could be wrong and still look right in a dry run:
a floor that raises a weak donor, a class silently passed through with no anchor, a
donor that does not exist, a clone that drops the cover attribute that makes a mesh
render, a stylesheet registration that misses one of the donor blade's blocks, a
re-run that churns bytes, a revert that leaves a gap, a text-mode write that strips a
BOM, a file written into a folder the Armory never registers.
"""
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_starter_kit as gk  # noqa: E402


def el(xml: str) -> ET.Element:
    return ET.fromstring(xml)


SWORD = """<CraftedItem id="wm_gondor_sword_a01" name="{=aom_x_name}[Gondor] Sword Medium A"
    crafting_template="OneHandedSword" is_merchandise="true" culture="Culture.gondor" value="4000">
    <Pieces>
        <Piece id="wm_gondor_sword_a01_blade" Type="Blade" scale_factor="100" />
        <Piece id="wm_gondor_sword_a01_guard" Type="Guard" scale_factor="110" />
        <Piece id="wm_gondor_sword_a01_hilt" Type="Handle" scale_factor="100" />
        <Piece id="wm_gondor_sword_a01_pommel" Type="Pommel" scale_factor="100" />
    </Pieces>
</CraftedItem>"""

BLADE = """<CraftingPiece id="wm_gondor_sword_a01_blade" name="{=aom_b_name}Gondor Blade" tier="2"
    piece_type="Blade" mesh="wm_gondor_sword_a01_blade" length="76.08" weight="0.68">
    <BladeData stack_amount="3" physics_material="metal_weapon" body_name="bo_wm_gondor_sword_a01" holster_mesh="">
        <Thrust damage_type="Pierce" damage_factor="3.26" />
        <Swing damage_type="Cut" damage_factor="4.18" />
    </BladeData>
    <Flags><Flag name="Civilian" type="ItemFlags" /></Flags>
    <Materials><Material id="Iron6" count="5" /></Materials>
</CraftingPiece>"""

SPEAR_HEAD = """<CraftingPiece id="spear_head" tier="4" piece_type="Blade" mesh="spear_head" length="50" weight="0.8">
    <BladeData physics_material="wood_weapon" body_name="bo_spear_head">
        <Thrust damage_type="Pierce" damage_factor="2.46" />
    </BladeData>
</CraftingPiece>"""

BOW = """<Item id="wm_gondor_bow" name="{=aom_bow_name}[Gondor] Bow" body_name="bo_short_bow_a" mesh="gondor_bow_a"
    is_merchandise="true" culture="Culture.gondor" weight="0.1" difficulty="30" appearance="0.1" Type="Bow"
    item_holsters="bow_back:bow_back_2">
    <ItemComponent>
        <Weapon weapon_class="Bow" ammo_class="Arrow" ammo_limit="1" thrust_speed="78" speed_rating="88"
            missile_speed="85" weapon_length="182" accuracy="100" thrust_damage="85" thrust_damage_type="Pierce"
            item_usage="bow" physics_material="wood_weapon" modifier_group="bow">
            <WeaponFlags RangedWeapon="true" HasString="true" StringHeldByHand="true" AutoReload="true" />
        </Weapon>
    </ItemComponent>
    <Flags UseTeamColor="true" />
    <AdditionalMeshes><Mesh name="bow_string" affected_by_cover="false" /></AdditionalMeshes>
</Item>"""

SHIELD = """<Item id="wm_elven_shield_a" name="{=aom_sh_name}[Noldor] Shield" mesh="wm_elven_shield_a"
    culture="Culture.rivendell" is_merchandise="true" weight="4.0" Type="Shield">
    <ItemComponent>
        <Weapon weapon_class="LargeShield" body_armor="5" hit_points="600" weapon_length="148" item_usage="shield">
            <WeaponFlags CanBlockRanged="true" HasHitPoints="true" />
        </Weapon>
    </ItemComponent>
</Item>"""

CHEST = """<Item id="mkwd_inf3_chest" name="{=aom_c_name}[Mirkwood] Chest" subtype="body_armor" mesh="mkwd_inf3_chest"
    culture="Culture.mirkwood" is_merchandise="true" weight="9" appearance="1" Type="BodyArmor">
    <ItemComponent>
        <Armor body_armor="28" leg_armor="25" arm_armor="10" has_gender_variations="true" covers_body="true"
            covers_legs="true" modifier_group="leather" material_type="Leather" />
    </ItemComponent>
    <Flags Civilian="true" UseTeamColor="true" />
</Item>"""

BOOTS = """<Item id="mirkwood_boots" name="{=aom_bt_name}[Mirkwood] Boots" mesh="mirkwood_boots" culture="Culture.mirkwood"
    is_merchandise="true" weight="1" Type="LegArmor">
    <ItemComponent><Armor leg_armor="26" covers_legs="true" modifier_group="leather" material_type="Leather" /></ItemComponent>
</Item>"""

ROSTERS = """<EquipmentRosters>
    <EquipmentRoster id="player_char_creation_gondor_retainer_m" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Item1" id="Item.wm_elven_shield_a" />
            <Equipment slot="Body" id="Item.mkwd_inf3_chest" />
            <Equipment slot="Leg" id="Item.starter_infantry_gondor_leg_a" />
            <Equipment slot="Horse" id="Item.saddle_horse" />
        </EquipmentSet>
        <EquipmentSet equipmentType="Civilian">
            <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
            <Equipment slot="Body" id="Item.civilian_only_coat" />
        </EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_char_creation_childhood_age_gondor_retainer_m" culture="Culture.gondor">
        <EquipmentSet><Equipment slot="Item0" id="Item.childhood_stick" /></EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_char_creation_education_age_gondor_retainer_m" culture="Culture.gondor">
        <EquipmentSet><Equipment slot="Item0" id="Item.education_stick" /></EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_char_creation_show_gondor" culture="Culture.gondor">
        <EquipmentSet><Equipment slot="Item0" id="Item.show_stick" /></EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="mother_char_creation_gondor_retainer" culture="Culture.gondor">
        <EquipmentSet><Equipment slot="Item0" id="Item.mother_stick" /></EquipmentSet>
    </EquipmentRoster>
    <EquipmentRoster id="player_career_gondor_ranged_f" culture="Culture.gondor">
        <EquipmentSet>
            <Equipment slot="Item0" id="Item.wm_gondor_bow" />
            <Equipment slot="Item2" id="Item.gondor_steel_bow_starter" />
        </EquipmentSet>
    </EquipmentRoster>
</EquipmentRosters>"""

WD_XSLT = (
    '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
    '\t<xsl:output omit-xml-declaration="yes"/>\n'
    '\t<xsl:template match="@*|node()">\n\t\t<xsl:copy>\n\t\t\t<xsl:apply-templates select="@*|node()"/>\n'
    '\t\t</xsl:copy>\n\t</xsl:template>\n'
    "\t<xsl:template match=\"WeaponDescription[@id='OneHandedSword']/AvailablePieces\">\n"
    '\t\t<AvailablePieces>\n'
    '\t\t\t<AvailablePiece id="wm_gondor_sword_a01_blade"/>\n'
    '\t\t\t<AvailablePiece id="wm_gondor_sword_a01_guard"/>\n'
    '\t\t\t<xsl:apply-templates select="@*|node()"/>\n'
    '\t\t</AvailablePieces>\n'
    '\t</xsl:template>\n'
    "\t<xsl:template match=\"WeaponDescription[@id='OneHandedBastardSword']/AvailablePieces\">\n"
    '\t\t<AvailablePieces>\n'
    '\t\t\t<AvailablePiece id="wm_gondor_sword_a01_blade"/>\n'
    '\t\t\t<AvailablePiece id="wm_gondor_sword_a01_blade"/>\n'
    '\t\t\t<xsl:apply-templates select="@*|node()"/>\n'
    '\t\t</AvailablePieces>\n'
    '\t</xsl:template>\n'
    '</xsl:stylesheet>\n'
)

NATIVE_WD = """<WeaponDescriptions>
    <WeaponDescription id="OneHandedSword" weapon_class="OneHandedSword">
        <AvailablePieces><AvailablePiece id="empire_blade_4" /><AvailablePiece id="wm_gondor_sword_a01_blade" /></AvailablePieces>
    </WeaponDescription>
    <WeaponDescription id="TwoHandedSwordAlt" weapon_class="TwoHandedSword">
        <AvailablePieces><AvailablePiece id="wm_gondor_sword_a01_blade" /></AvailablePieces>
    </WeaponDescription>
    <WeaponDescription id="Dagger" weapon_class="Dagger">
        <AvailablePieces><AvailablePiece id="seax_blade" /></AvailablePieces>
    </WeaponDescription>
</WeaponDescriptions>"""

SUBMODULE = """<Module>
  <Xmls>
    <XmlNode><XmlName id="Items" path="LOTRLOME_items/gondor"/></XmlNode>
    <XmlNode><XmlName id="Items" path="LOTRLOME_items/mercenary"/></XmlNode>
    <XmlNode><XmlName id="Items" path="LOTRLOME_items/rivendell"/></XmlNode>
    <XmlNode><XmlName id="CraftingPieces" path="LOTRLOME_crafting_pieces"/></XmlNode>
    <XmlNode><XmlName id="Items" path="LOTRLOME_items/LOTRAOM_weapons"/></XmlNode>
  </Xmls>
</Module>"""


class TestAnchors(unittest.TestCase):
    def test_anchor_never_exceeds_donor(self):
        weak = el(BLADE.replace('damage_factor="4.18"', 'damage_factor="2.0"'))
        out = gk.clone_blade(weak, "starter_x_blade", gk.anchor_for("OneHandedSword"))
        self.assertEqual(out.find("BladeData/Swing").get("damage_factor"), "2.0")

    def test_anchor_applies_when_donor_is_stronger(self):
        out = gk.clone_blade(el(BLADE), "starter_x_blade", gk.anchor_for("OneHandedSword"))
        self.assertEqual(out.find("BladeData/Swing").get("damage_factor"), "2.5")
        self.assertEqual(out.find("BladeData/Thrust").get("damage_factor"), "1.7")

    def test_missing_anchor_for_class_raises(self):
        with self.assertRaises(gk.StarterKitError) as ctx:
            gk.anchor_for("Sling")
        self.assertIn("Sling", str(ctx.exception))

    def test_plain_weapon_floor_applies_and_never_raises(self):
        bow = gk.clone_plain_item(el(BOW), "starter_wm_gondor_bow", gk.anchor_for("Bow"))
        self.assertEqual(bow.find("ItemComponent/Weapon").get("thrust_damage"), "45")
        hunting = el(BOW.replace('thrust_damage="85"', 'thrust_damage="40"'))
        out = gk.clone_plain_item(hunting, "starter_hunting_bow", gk.anchor_for("Bow"))
        self.assertEqual(out.find("ItemComponent/Weapon").get("thrust_damage"), "40")
        shield = gk.clone_plain_item(el(SHIELD), "starter_wm_elven_shield_a", gk.anchor_for("LargeShield"))
        w = shield.find("ItemComponent/Weapon")
        self.assertEqual(w.get("hit_points"), "220")
        self.assertEqual(w.get("body_armor"), "1")

    def test_armour_floor_keeps_only_anchored_stats(self):
        chest = gk.clone_plain_item(el(CHEST), "starter_mkwd_inf3_chest", gk.anchor_for("BodyArmor"))
        a = chest.find("ItemComponent/Armor")
        self.assertEqual(a.get("body_armor"), "9")
        self.assertEqual(a.get("arm_armor"), "4")
        self.assertIsNone(a.get("leg_armor"), "a chest must not carry leg armour into the starter tier")
        boots = gk.clone_plain_item(el(BOOTS), "starter_mirkwood_boots", gk.anchor_for("LegArmor"))
        self.assertEqual(boots.find("ItemComponent/Armor").get("leg_armor"), "9")


class TestIds(unittest.TestCase):
    def test_starter_id_strips_trailing_starter_suffix(self):
        self.assertEqual(gk.starter_id("gondor_steel_bow_starter"), "starter_gondor_steel_bow")
        self.assertEqual(gk.starter_id("wm_gondor_sword_a01"), "starter_wm_gondor_sword_a01")

    def test_already_starter_donor_is_skipped(self):
        donors = gk.collect_donors([el(ROSTERS)])
        self.assertNotIn("starter_infantry_gondor_leg_a", donors)
        self.assertIn("wm_gondor_sword_a01", donors)

    def test_targets_exclude_childhood_education_show_and_parents(self):
        ids = gk.target_roster_ids(el(ROSTERS))
        self.assertEqual(ids, ["player_char_creation_gondor_retainer_m", "player_career_gondor_ranged_f"])
        donors = gk.collect_donors([el(ROSTERS)])
        for stick in ("childhood_stick", "education_stick", "show_stick", "mother_stick"):
            self.assertNotIn(stick, donors)

    def test_targets_include_both_equipment_sets_and_skip_mounts(self):
        donors = gk.collect_donors([el(ROSTERS)])
        self.assertIn("civilian_only_coat", donors, "the civilian set is applied independently and must be rewired")
        self.assertNotIn("saddle_horse", donors)
        self.assertEqual(donors["wm_gondor_sword_a01"], {"Item0"})


class TestClones(unittest.TestCase):
    def test_crafted_donor_clones_blade_only(self):
        item = gk.clone_crafted_item(el(SWORD), "starter_wm_gondor_sword_a01", "starter_wm_gondor_sword_a01_blade")
        pieces = {p.get("Type"): (p.get("id"), p.get("scale_factor")) for p in item.iter("Piece")}
        self.assertEqual(pieces["Blade"], ("starter_wm_gondor_sword_a01_blade", "100"))
        self.assertEqual(pieces["Guard"], ("wm_gondor_sword_a01_guard", "110"))
        self.assertEqual(pieces["Handle"], ("wm_gondor_sword_a01_hilt", "100"))
        self.assertEqual(pieces["Pommel"], ("wm_gondor_sword_a01_pommel", "100"))
        self.assertEqual(item.get("crafting_template"), "OneHandedSword")
        self.assertEqual(item.get("is_merchandise"), "false")
        # pinned: the engine prices a crafted weapon 40% from its fittings' tiers and iron
        # grades, which the clone keeps, so a computed price would stay in the thousands
        self.assertEqual(item.get("value"), str(gk.CRAFTED_STARTER_VALUE))
        self.assertIsNone(gk.clone_crafted_item(el(SWORD), "s", "b", value=None).get("value"))
        self.assertTrue(item.get("name").startswith("{=starter_wm_gondor_sword_a01}"))
        self.assertIn("[Gondor] Sword Medium A", item.get("name"))

    def test_blade_is_hidden_tier_one_and_otherwise_verbatim(self):
        out = gk.clone_blade(el(BLADE), "starter_wm_gondor_sword_a01_blade", gk.anchor_for("OneHandedSword"))
        self.assertEqual(out.get("id"), "starter_wm_gondor_sword_a01_blade")
        self.assertEqual(out.get("tier"), "1")
        self.assertEqual(out.get("is_hidden"), "true")
        self.assertEqual(out.get("mesh"), "wm_gondor_sword_a01_blade")
        self.assertEqual(out.get("length"), "76.08")
        self.assertEqual(out.find("BladeData").get("body_name"), "bo_wm_gondor_sword_a01")
        self.assertIsNotNone(out.find("Flags/Flag"))
        self.assertIsNotNone(out.find("Materials/Material"))

    def test_swing_omitted_when_donor_has_no_swing(self):
        out = gk.clone_blade(el(SPEAR_HEAD), "starter_spear_head", gk.anchor_for("TwoHandedPolearm"))
        self.assertIsNone(out.find("BladeData/Swing"))
        self.assertEqual(out.find("BladeData/Thrust").get("damage_factor"), "1.4")

    def test_cover_attributes_copied_verbatim(self):
        chest = gk.clone_plain_item(el(CHEST), "starter_mkwd_inf3_chest", gk.anchor_for("BodyArmor"))
        a = chest.find("ItemComponent/Armor")
        self.assertEqual(a.get("covers_body"), "true")
        self.assertEqual(a.get("covers_legs"), "true")
        self.assertEqual(a.get("has_gender_variations"), "true")
        self.assertEqual(a.get("material_type"), "Leather")

    def test_flags_mesh_holsters_and_additional_meshes_copied_verbatim(self):
        bow = gk.clone_plain_item(el(BOW), "starter_wm_gondor_bow", gk.anchor_for("Bow"))
        self.assertEqual(bow.get("mesh"), "gondor_bow_a")
        self.assertEqual(bow.get("body_name"), "bo_short_bow_a")
        self.assertEqual(bow.get("item_holsters"), "bow_back:bow_back_2")
        self.assertEqual(bow.find("Flags").get("UseTeamColor"), "true")
        self.assertIsNotNone(bow.find("AdditionalMeshes/Mesh"))
        self.assertIsNotNone(bow.find("ItemComponent/Weapon/WeaponFlags"))
        self.assertEqual(bow.find("ItemComponent/Weapon").get("speed_rating"), "88")

    def test_clone_is_not_merchandise_has_zero_difficulty_and_no_value(self):
        bow = gk.clone_plain_item(el(BOW), "starter_wm_gondor_bow", gk.anchor_for("Bow"))
        self.assertEqual(bow.get("is_merchandise"), "false")
        self.assertEqual(bow.get("difficulty"), "0")
        self.assertIsNone(bow.get("value"))
        self.assertEqual(bow.get("id"), "starter_wm_gondor_bow")
        self.assertEqual(bow.get("name"), "{=starter_wm_gondor_bow}[Gondor] Bow (Starter)")

    def test_classify_by_template_weapon_class_or_type(self):
        self.assertEqual(gk.classify(el(SWORD)), ("crafted", "OneHandedSword"))
        self.assertEqual(gk.classify(el(BOW)), ("weapon", "Bow"))
        self.assertEqual(gk.classify(el(CHEST)), ("armour", "BodyArmor"))
        with self.assertRaises(gk.StarterKitError):
            gk.classify(el('<Item id="rock" Type="Goods"><ItemComponent><Trade /></ItemComponent></Item>'))

    def test_weapon_tier_is_int_mean_of_fitted_pieces(self):
        self.assertEqual(gk.weapon_tier([1, 3, 3, 3]), 2)
        self.assertEqual(gk.weapon_tier([1, 5, 5, 5]), 4)
        self.assertEqual(gk.weapon_tier([1, 1]), 1)


class TestFolders(unittest.TestCase):
    def test_registered_folders_come_from_submodule(self):
        self.assertEqual(gk.registered_item_folders(SUBMODULE), {"gondor", "mercenary", "rivendell"})

    def test_folder_map_and_fallbacks(self):
        reg = {"gondor", "mercenary", "rohan", "rhun"}
        self.assertEqual(gk.folder_for("Culture.gondor", None, reg), "gondor")
        self.assertEqual(gk.folder_for("Culture.vlandia", None, reg), "rohan")
        self.assertEqual(gk.folder_for("Culture.battania", None, reg), "rhun")
        self.assertEqual(gk.folder_for("Culture.neutral_culture", "gondor", reg), "gondor")
        self.assertEqual(gk.folder_for(None, None, reg), "mercenary")

    def test_folder_for_rejects_unregistered_folder(self):
        with self.assertRaises(gk.StarterKitError):
            gk.folder_for("Culture.gondor", None, {"mercenary"})


class TestStylesheets(unittest.TestCase):
    def test_registrations_mirror_every_donor_block_deduplicated(self):
        targets = gk.blocks_for_piece("wm_gondor_sword_a01_blade", WD_XSLT, el(NATIVE_WD), "WeaponDescription")
        self.assertEqual(targets, ["OneHandedSword", "OneHandedBastardSword", "TwoHandedSwordAlt"])

    def test_apply_inserts_into_existing_block_and_creates_missing_one(self):
        out, actions = gk.apply_xslt(
            WD_XSLT, "WeaponDescription",
            {"OneHandedSword": ["starter_wm_gondor_sword_a01_blade"],
             "TwoHandedSwordAlt": ["starter_wm_gondor_sword_a01_blade"]})
        self.assertEqual(actions["OneHandedSword"], "inserted")
        self.assertEqual(actions["TwoHandedSwordAlt"], "created")
        self.assertEqual(out.count(gk.MARKER_START), 2)
        ET.fromstring(out.encode("utf-8"))
        created = out[out.index("TwoHandedSwordAlt"):]
        self.assertIn('<xsl:apply-templates select="@*|node()"/>', created)
        self.assertIn('<AvailablePiece id="starter_wm_gondor_sword_a01_blade"/>', created)
        # the created template sits before the stylesheet close
        self.assertLess(out.index("TwoHandedSwordAlt"), out.index("</xsl:stylesheet>"))

    def test_apply_is_idempotent(self):
        targets = {"OneHandedSword": ["starter_a", "starter_b"], "TwoHandedSwordAlt": ["starter_a"]}
        once, _ = gk.apply_xslt(WD_XSLT, "WeaponDescription", targets)
        twice, actions = gk.apply_xslt(once, "WeaponDescription", targets)
        self.assertEqual(twice, once)
        self.assertEqual(set(actions.values()), {"noop"})

    def test_apply_updates_a_changed_block(self):
        once, _ = gk.apply_xslt(WD_XSLT, "WeaponDescription", {"OneHandedSword": ["starter_a"]})
        out, actions = gk.apply_xslt(once, "WeaponDescription", {"OneHandedSword": ["starter_a", "starter_b"]})
        self.assertEqual(actions["OneHandedSword"], "updated")
        self.assertEqual(out.count("starter_a"), 1)
        self.assertEqual(out.count("starter_b"), 1)

    def test_revert_removes_blocks_exactly(self):
        targets = {"OneHandedSword": ["starter_a"], "TwoHandedSwordAlt": ["starter_a"]}
        applied, _ = gk.apply_xslt(WD_XSLT, "WeaponDescription", targets)
        reverted, count = gk.revert_xslt(applied)
        self.assertEqual(count, 2)
        self.assertEqual(reverted, WD_XSLT)

    def test_usable_pieces_side_uses_piece_id_attribute(self):
        ct = WD_XSLT.replace("WeaponDescription", "CraftingTemplate").replace("AvailablePieces", "UsablePieces") \
                    .replace("AvailablePiece id=", "UsablePiece piece_id=")
        out, actions = gk.apply_xslt(ct, "CraftingTemplate", {"OneHandedSword": ["starter_a"]})
        self.assertEqual(actions["OneHandedSword"], "inserted")
        self.assertIn('<UsablePiece piece_id="starter_a"/>', out)
        ET.fromstring(out.encode("utf-8"))

    def test_inserted_lines_use_majority_newline(self):
        crlf = WD_XSLT.replace("\n", "\r\n")
        out, _ = gk.apply_xslt(crlf, "WeaponDescription", {"OneHandedSword": ["starter_a"]})
        block = out[out.index(gk.MARKER_START):out.index(gk.MARKER_END)]
        self.assertIn("\r\n", block)
        self.assertEqual(block.count("\n"), block.count("\r\n"))


class TestPiecesFile(unittest.TestCase):
    PIECES = "<CraftingPieces>\n    <CraftingPiece id=\"a\" piece_type=\"Blade\" />\n</CraftingPieces>\n"

    def test_apply_pieces_inserts_block_before_close_and_is_idempotent(self):
        blade = gk.clone_blade(el(BLADE), "starter_x_blade", gk.anchor_for("OneHandedSword"))
        once, action = gk.apply_pieces(self.PIECES, [blade])
        self.assertEqual(action, "inserted")
        self.assertIn('id="starter_x_blade"', once)
        self.assertLess(once.index(gk.MARKER_END), once.index("</CraftingPieces>"))
        ET.fromstring(once.encode("utf-8"))
        twice, action = gk.apply_pieces(once, [blade])
        self.assertEqual(action, "noop")
        self.assertEqual(twice, once)

    def test_revert_pieces_restores_original(self):
        blade = gk.clone_blade(el(BLADE), "starter_x_blade", gk.anchor_for("OneHandedSword"))
        once, _ = gk.apply_pieces(self.PIECES, [blade])
        back, removed = gk.revert_pieces(once)
        self.assertTrue(removed)
        self.assertEqual(back, self.PIECES)

    def test_plain_donor_emits_no_crafting_piece(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = _plan_in(Path(tmp))
        self.assertEqual([p.get("id") for p in plan.pieces], ["starter_wm_gondor_sword_a01_blade"])
        kinds = {c.new_id: c.kind for c in plan.clones}
        self.assertEqual(kinds["starter_wm_gondor_bow"], "weapon")
        self.assertEqual(kinds["starter_wm_elven_shield_a"], "weapon")
        self.assertEqual(kinds["starter_mkwd_inf3_chest"], "armour")


class TestIo(unittest.TestCase):
    def test_roundtrip_preserves_bom_and_crlf(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "f.xml"
            p.write_bytes(b"\xef\xbb\xbf<CraftingPieces>\r\n    <CraftingPiece id=\"a\" />\r\n</CraftingPieces>\r\n")
            text, had_bom = gk.read_xml(p)
            self.assertTrue(had_bom)
            blade = gk.clone_blade(el(BLADE), "starter_x_blade", gk.anchor_for("OneHandedSword"))
            out, _ = gk.apply_pieces(text, [blade])
            self.assertIsNone(gk.checked_write(p, out, had_bom, "test"))
            raw = p.read_bytes()
            self.assertTrue(raw.startswith(b"\xef\xbb\xbf"))
            self.assertEqual(raw.count(b"\n"), raw.count(b"\r\n"))
            self.assertTrue((Path(tmp) / "f.xml.bak-test").exists())

    def test_refuses_to_write_malformed_xml(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "f.xml"
            original = b"<CraftingPieces />"
            p.write_bytes(original)
            err = gk.checked_write(p, "<CraftingPieces><oops></CraftingPieces>", False, "test")
            self.assertIsNotNone(err)
            self.assertEqual(p.read_bytes(), original)
            self.assertFalse((Path(tmp) / "f.xml.bak-test").exists())

    def test_backup_is_taken_once(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "f.xml"
            p.write_bytes(b"<a>1</a>")
            gk.checked_write(p, "<a>2</a>", False, "test")
            gk.checked_write(p, "<a>3</a>", False, "test")
            self.assertEqual((Path(tmp) / "f.xml.bak-test").read_bytes(), b"<a>1</a>")


def _plan_in(tmp: Path) -> "gk.Plan":
    """A miniature Armory + roster layout for the end-to-end planning tests."""
    md = tmp / "LOTRLOME_Armory" / "ModuleData"
    (md / "LOTRLOME_items" / "gondor").mkdir(parents=True)
    (md / "LOTRLOME_items" / "rivendell").mkdir(parents=True)
    (md / "LOTRLOME_items" / "mercenary").mkdir(parents=True)
    (md / "LOTRLOME_items" / "LOTRAOM_weapons.xml").write_text(
        "<Items>\n" + SWORD + "\n" + BOW + "\n"
        + BOW.replace('id="wm_gondor_bow"', 'id="gondor_steel_bow_starter"').replace("aom_bow_name", "aom_sbs_name")
        + "\n" + SHIELD + "\n</Items>\n", encoding="utf-8")
    (md / "LOTRLOME_items" / "gondor" / "body_armors.xml").write_text(
        "<Items>\n" + CHEST.replace('culture="Culture.mirkwood"', 'culture="Culture.gondor"')
        + "\n" + CHEST.replace('id="mkwd_inf3_chest"', 'id="civilian_only_coat"').replace("aom_c_name", "aom_cc_name")
        .replace('culture="Culture.mirkwood"', 'culture="Culture.gondor"') + "\n</Items>\n", encoding="utf-8")
    (md / "LOTRLOME_crafting_pieces.xml").write_text(
        "<CraftingPieces>\n" + BLADE + "\n"
        + '<CraftingPiece id="wm_gondor_sword_a01_guard" tier="3" piece_type="Guard" />\n'
        + '<CraftingPiece id="wm_gondor_sword_a01_hilt" tier="3" piece_type="Handle" />\n'
        + '<CraftingPiece id="wm_gondor_sword_a01_pommel" tier="3" piece_type="Pommel" />\n'
        + "</CraftingPieces>\n", encoding="utf-8")
    (md / "weapon_descriptions.xslt").write_text(WD_XSLT, encoding="utf-8")
    (md / "crafting_templates.xslt").write_text(
        WD_XSLT.replace("WeaponDescription", "CraftingTemplate").replace("AvailablePieces", "UsablePieces")
        .replace("AvailablePiece id=", "UsablePiece piece_id="), encoding="utf-8")
    (tmp / "LOTRLOME_Armory" / "SubModule.xml").write_text(SUBMODULE, encoding="utf-8")
    roster = tmp / "rosters.xml"
    roster.write_text(ROSTERS, encoding="utf-8")
    native = tmp / "native_wd.xml"
    native.write_text(NATIVE_WD, encoding="utf-8")
    native_ct = tmp / "native_ct.xml"
    native_ct.write_text(NATIVE_WD.replace("WeaponDescription", "CraftingTemplate")
                         .replace("AvailablePieces", "UsablePieces").replace("AvailablePiece id=", "UsablePiece piece_id="),
                         encoding="utf-8")
    sources = gk.Sources(rosters=[roster], armory=tmp / "LOTRLOME_Armory", vanilla_item_files=[],
                         native_pieces=None, native_descriptions=native, native_templates=native_ct)
    return gk.build_plan(sources)


class TestPlan(unittest.TestCase):
    def test_plan_covers_every_donor_once_and_places_by_culture(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = _plan_in(Path(tmp))
        ids = sorted(c.new_id for c in plan.clones)
        self.assertEqual(ids, ["starter_civilian_only_coat", "starter_gondor_steel_bow", "starter_mkwd_inf3_chest",
                               "starter_wm_elven_shield_a", "starter_wm_gondor_bow", "starter_wm_gondor_sword_a01"])
        folders = {c.new_id: c.folder for c in plan.clones}
        self.assertEqual(folders["starter_wm_gondor_sword_a01"], "gondor")
        self.assertEqual(folders["starter_wm_elven_shield_a"], "rivendell")
        self.assertEqual(folders["starter_mkwd_inf3_chest"], "gondor")

    def test_plan_registers_the_blade_in_every_donor_block(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = _plan_in(Path(tmp))
        self.assertEqual(plan.registrations["WeaponDescription"],
                         {"OneHandedSword": ["starter_wm_gondor_sword_a01_blade"],
                          "OneHandedBastardSword": ["starter_wm_gondor_sword_a01_blade"],
                          "TwoHandedSwordAlt": ["starter_wm_gondor_sword_a01_blade"]})
        self.assertEqual(plan.registrations["CraftingTemplate"],
                         {"OneHandedSword": ["starter_wm_gondor_sword_a01_blade"],
                          "OneHandedBastardSword": ["starter_wm_gondor_sword_a01_blade"],
                          "TwoHandedSwordAlt": ["starter_wm_gondor_sword_a01_blade"]})

    def test_missing_donor_fails_loudly(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            _plan_in(tmp)
            roster = tmp / "rosters.xml"
            roster.write_text(ROSTERS.replace("Item.wm_elven_shield_a", "Item.wm_gone_shield"), encoding="utf-8")
            sources = gk.Sources(rosters=[roster], armory=tmp / "LOTRLOME_Armory", vanilla_item_files=[],
                                 native_pieces=None, native_descriptions=tmp / "native_wd.xml",
                                 native_templates=tmp / "native_ct.xml")
            with self.assertRaises(gk.StarterKitError) as ctx:
                gk.build_plan(sources)
        self.assertIn("wm_gone_shield", str(ctx.exception))

    def test_report_states_ratio_and_tier(self):
        with tempfile.TemporaryDirectory() as tmp:
            plan = _plan_in(Path(tmp))
        sword = next(c for c in plan.clones if c.new_id == "starter_wm_gondor_sword_a01")
        self.assertEqual(sword.tier, 2)          # blade 1 + fittings 3, 3, 3
        self.assertAlmostEqual(sword.ratios["swing"], 2.5 / 4.18, places=4)
        self.assertAlmostEqual(sword.ratios["thrust"], 1.7 / 3.26, places=4)

    def test_apply_then_verify_then_revert(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            plan = _plan_in(tmp)
            md = tmp / "LOTRLOME_Armory" / "ModuleData"
            before = {p: p.read_bytes() for p in md.rglob("*") if p.is_file()}
            log = gk.apply_plan(plan, md, write=True)
            self.assertTrue(any("inserted" in line or "wrote" in line for line in log))
            self.assertTrue((md / "LOTRLOME_items" / "gondor" / "starter_kit.xml").exists())
            self.assertTrue((md / "LOTRLOME_items" / "rivendell" / "starter_kit.xml").exists())
            self.assertFalse((md / "LOTRLOME_items" / "mercenary" / "starter_kit.xml").exists())
            for p in md.rglob("*.xml"):
                ET.parse(p)
            for p in md.rglob("*.xslt"):
                ET.parse(p)
            self.assertEqual(gk.verify_plan(plan, md), [])
            # a second apply is a no-op on every file
            snapshot = {p: p.read_bytes() for p in md.rglob("*") if p.is_file()}
            gk.apply_plan(plan, md, write=True)
            self.assertEqual({p: p.read_bytes() for p in md.rglob("*") if p.is_file()}, snapshot)
            # drift: someone reverts one registration
            wd = md / "weapon_descriptions.xslt"
            wd.write_bytes(wd.read_bytes().replace(b'<AvailablePiece id="starter_wm_gondor_sword_a01_blade"/>', b"", 1))
            drift = gk.verify_plan(plan, md)
            self.assertTrue(drift and any("starter_wm_gondor_sword_a01_blade" in d for d in drift))
            gk.revert_plan(plan, md, write=True)
            after = {p: p.read_bytes() for p in md.rglob("*") if p.is_file() and not p.name.endswith(gk.BACKUP_SUFFIX)}
            self.assertEqual(after, before)


if __name__ == "__main__":
    unittest.main()
