#!/usr/bin/env python3
"""Unit tests for the shield-vs-unusable-weapon gate (tools/audit_polearm_shield_parity.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_audit_polearm_shield_parity.py

These build a SYNTHETIC Modules tree -- Native XML plus an additive module XSLT -- so they
encode the gate's contract independently of the real LOTRLOME_Armory and vanilla data. That
matters here more than usual: the whole point of the gate is to notice when the real Armory has
been refreshed back to a broken state, so its tests must not depend on the Armory being present.

Each test maps to a way the gate could be wrong and pass anyway:
  - detects_shield_with_unusable_polearm  -> the #445/#447 defect itself
  - passes_when_registered                -> the fix is recognised (no permanent red)
  - registration_must_cover_every_piece   -> Crafting.cs's all-pieces-must-match rule
  - stylesheet_named_by_a_submodule_row_applies -> #1354: a row's subfolder path is read
  - stylesheet_without_a_submodule_row_is_ignored -> only what the engine loads counts
  - incompatible_module_is_ignored        -> #1354: NavalDLC never loads beside TAOM, keyed by <Id>
  - xsl_sibling_wins_over_xslt            -> #1375: the engine reads <path>.xsl first
  - row_for_another_game_type_is_ignored  -> #1375: <IncludedGameTypes> without Campaign
  - module_outside_the_playset_does_not_count -> #1375: AWOIAF_Armoury never loads beside TAOM
  - load_before_metadata_module_counts    -> the Armory is only optional LoadBeforeThis metadata
  - row_xml_merge_is_bad_input            -> an XSD merge the gate does not model exits 2
  - piece_file_for_another_game_type_does_not_count -> #1398: mp_crafting_pieces.xml is not campaign data
  - item_file_for_another_game_type_does_not_count  -> #1398: nor is mpitems.xml
  - only_the_first_xmlname_of_a_node_counts -> #1398: the engine reads one per XmlNode
  - module_ids_compare_without_case       -> #1398: the engine lower-cases them
  - file_named_by_a_campaign_row_too_counts -> a file one TAOM-game row names still loads
  - custom_game_item_file_counts          -> TAOM plays custom battles (Fords of War) too
  - folder_for_another_game_type_does_not_count -> a row's folder, read only with no <path>.xml
  - comment_under_included_game_types_names_no_type -> only element children name a type
  - any_element_under_included_game_types_names_a_type -> whatever the child's tag
  - undefined_piece_vanilla_lists_is_not_a_finding -> Native's own mp-only list is not ours to fix
  - module_registering_a_vanilla_mp_piece_elsewhere_fails -> the exemption is per description
  - first_description_wins                -> template order decides the primary usage
  - ranged_is_not_a_finding               -> a bow + shield is normal, not a defect
  - twohanded_fails_like_polearm          -> #1312: two-handed weapons joined the ratchet
  - onehanded_weapon_with_blocking_usage_fails -> every melee type fails the same way
  - unheld_dangling_piece_fails           -> a piece id with no definition fails every run
  - held_dangling_piece_passes            -> #1312: a held dangling id prints its reason
  - stale_held_dangling_piece_fails       -> a held id that no longer dangles must go
  - missing_install_skips_cleanly         -> absent install exits 0, never a false PASS
  - standalone_roster_casing_is_scanned   -> #526: the walker read one element casing and so
                                             never opened a single equipmentsets/ file
  - known_failure_is_held_not_failed      -> the #526 ratchet holds the pre-existing pairs
  - stale_known_failure_entry_fails       -> a suppression that outlives its finding is caught
  - ratchet_does_not_absorb_a_second_occurrence -> #526 multiplicity: 10 keys were hiding 13 rows
  - ratchet_entry_that_overstates_the_debt_fails -> an inflated count absorbs a future regression
"""
import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from unittest import mock
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import audit_polearm_shield_parity as audit  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "audit_polearm_shield_parity.py"

# One shield-incompatible set and one shield-compatible set, mirroring the real pair:
# polearm_block_long_shield_swing_thrust (requires_no_shield) vs
# onehanded_polearm_block_long_rshield_thrust (requires_shield).
USAGE_SETS = """<?xml version="1.0" encoding="utf-8"?>
<base type="item_usage_set">
  <item_usage_sets>
    <item_usage_set id="twohanded_polearm"><flags><flag name="requires_no_shield"/></flags></item_usage_set>
    <item_usage_set id="onehanded_polearm"><flags><flag name="requires_shield"/></flags></item_usage_set>
    <item_usage_set id="twohanded_sword"><flags><flag name="requires_no_shield"/></flags></item_usage_set>
    <item_usage_set id="bow"><flags><flag name="requires_no_shield"/></flags></item_usage_set>
    <item_usage_set id="inherits_nothing" base_set="twohanded_polearm"/>
  </item_usage_sets>
</base>
"""

# OneHandedPolearm first, exactly as Native orders TwoHandedPolearm's descriptions -- that order
# is what makes a registered piece set produce a one-handed PRIMARY rather than an alternate.
TEMPLATES = """<?xml version="1.0" encoding="utf-8"?>
<CraftingTemplates>
  <CraftingTemplate id="SpearTemplate" item_type="Polearm">
    <WeaponDescriptions>
      <WeaponDescription id="OneHandedPolearm"/>
      <WeaponDescription id="TwoHandedPolearm"/>
    </WeaponDescriptions>
  </CraftingTemplate>
  <CraftingTemplate id="OddswordTemplate" item_type="OneHandedWeapon">
    <WeaponDescriptions><WeaponDescription id="TwoHandedSword"/></WeaponDescriptions>
  </CraftingTemplate>
  <CraftingTemplate id="GreatswordTemplate" item_type="TwoHandedWeapon">
    <WeaponDescriptions><WeaponDescription id="TwoHandedSword"/></WeaponDescriptions>
  </CraftingTemplate>
</CraftingTemplates>
"""

DESCRIPTIONS = """<?xml version="1.0" encoding="utf-8"?>
<WeaponDescriptions>
  <WeaponDescription id="OneHandedPolearm" item_usage_features="onehanded_polearm">
    <AvailablePieces/>
  </WeaponDescription>
  <WeaponDescription id="TwoHandedPolearm" item_usage_features="twohanded_polearm">
    <AvailablePieces>
      <!-- a comment child, because lxml yields these too and an id-less node poisons the set -->
      <AvailablePiece id="spear_blade"/>
      <AvailablePiece id="spear_handle"/>
    </AvailablePieces>
  </WeaponDescription>
  <WeaponDescription id="TwoHandedSword" item_usage_features="twohanded_sword">
    <AvailablePieces><AvailablePiece id="great_blade"/><AvailablePiece id="great_handle"/></AvailablePieces>
  </WeaponDescription>
</WeaponDescriptions>
"""

PIECES = """<?xml version="1.0" encoding="utf-8"?>
<CraftingPieces>
  <CraftingPiece id="spear_blade" piece_type="Blade"/>
  <CraftingPiece id="spear_handle" piece_type="Handle"/>
  <CraftingPiece id="great_blade" piece_type="Blade"/>
  <CraftingPiece id="great_handle" piece_type="Handle"/>
</CraftingPieces>
"""

ITEMS = """<?xml version="1.0" encoding="utf-8"?>
<Items>
  <CraftedItem id="test_spear" crafting_template="SpearTemplate">
    <Pieces><Piece id="spear_blade"/><Piece id="spear_handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="test_greatsword" crafting_template="GreatswordTemplate">
    <Pieces><Piece id="great_blade"/><Piece id="great_handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="test_oddsword" crafting_template="OddswordTemplate">
    <Pieces><Piece id="great_blade"/><Piece id="great_handle"/></Pieces>
  </CraftedItem>
  <Item id="test_shield" Type="Shield"/>
  <Item id="test_bow" Type="Bow"><ItemComponent><Weapon item_usage="bow"/></ItemComponent></Item>
</Items>
"""


def roster_xml(*item_ids: str) -> str:
    equipment = "".join(f'<equipment slot="Item{i}" id="Item.{x}"/>' for i, x in enumerate(item_ids))
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>\n'
        '  <NPCCharacter id="test_troop"><Equipments><EquipmentRoster>'
        f"{equipment}"
        "</EquipmentRoster></Equipments></NPCCharacter>\n</NPCCharacters>\n"
    )


def standalone_roster_xml(roster_id: str, *item_ids: str) -> str:
    """A standalone <EquipmentRosters> file, which spells the child `<Equipment>` (capital E).

    Every file under `Main/_Module/ModuleData/equipmentsets/` uses this shape, and the gate could
    not see any of them until 2026-09-01 (#526). Deliberately NOT a copy of `roster_xml` with the
    tag swapped: the owner id here sits on the EquipmentRoster itself rather than on a parent
    NPCCharacter, which is the other half of what the walker has to handle.
    """
    equipment = "".join(f'<Equipment slot="Item{i}" id="Item.{x}"/>' for i, x in enumerate(item_ids))
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n<EquipmentRosters>\n'
        f'  <EquipmentRoster id="{roster_id}" culture="Culture.neutral_culture"><EquipmentSet>'
        f"{equipment}"
        "</EquipmentSet></EquipmentRoster>\n</EquipmentRosters>\n"
    )


def xslt_registering(*piece_ids: str) -> str:
    """An additive override of OneHandedPolearm's AvailablePieces, shaped like the Armory's."""
    entries = "".join(f'<AvailablePiece id="{p}"/>' for p in piece_ids)
    return (
        '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
        '  <xsl:output omit-xml-declaration="yes"/>\n'
        '  <xsl:template match="@*|node()"><xsl:copy>'
        '<xsl:apply-templates select="@*|node()"/></xsl:copy></xsl:template>\n'
        "  <xsl:template match=\"WeaponDescription[@id='OneHandedPolearm']/AvailablePieces\">\n"
        f"    <AvailablePieces>{entries}<xsl:apply-templates select=\"@*|node()\"/></AvailablePieces>\n"
        "  </xsl:template>\n</xsl:stylesheet>\n"
    )


def submodule_xml(*rows: tuple[str, str]) -> str:
    """A SubModule.xml whose <XmlName> rows the engine reads to find a module's stylesheets."""
    nodes = "".join(f'<XmlNode><XmlName id="{i}" path="{p}"/></XmlNode>' for i, p in rows)
    return f'<Module><Xmls>{nodes}</Xmls></Module>\n'


MP_ROW = ('<XmlNode><XmlName id="{id}" path="{path}"/><IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes></XmlNode>')


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class GateContractTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.rosters = root / "ModuleData"
        native = self.modules / "Native" / "ModuleData"
        write(native / "item_usage_sets.xml", USAGE_SETS)
        write(native / "crafting_templates.xml", TEMPLATES)
        write(native / "weapon_descriptions.xml", DESCRIPTIONS)
        write(native / "crafting_pieces.xml", PIECES)
        write(self.modules / "TestArmory" / "ModuleData" / "items.xml", ITEMS)
        self.overlay = self.modules / "TestArmory" / "ModuleData" / "weapon_descriptions.xslt"
        write(self.modules / "TestArmory" / "SubModule.xml",
              submodule_xml(("WeaponDescriptions", "weapon_descriptions")))
        self.addCleanup(self._tmp.cleanup)
        # Empty ratchet by default. The shipped KNOWN_FAILURES names real roster ids, and every
        # one of them is stale against this synthetic tree, which would fail every test here for
        # a reason none of them is testing. The two ratchet tests install their own table.
        self.ratchet({})
        # The same for the shipped KNOWN_DANGLING: its ids are absent here, so each would read as
        # stale and fail every run for a reason no test here is testing.
        self.held_dangling({})

    def swap(self, table, mapping):
        """Replace a module table for one test. REPLACES rather than extends: the shipped tables
        name real ids, none of which exist in this synthetic tree, so merging would make every
        shipped entry read as stale and turn each test red for a reason it is not testing."""
        patcher = mock.patch.dict(table, mapping, clear=True)
        patcher.start()
        self.addCleanup(patcher.stop)

    def ratchet(self, table):
        self.swap(audit.KNOWN_FAILURES, table)

    def held_dangling(self, table):
        self.swap(audit.KNOWN_DANGLING, table)

    def run_gate(self, *extra: str):
        argv = [
            "audit_polearm_shield_parity.py",
            "--game-modules", str(self.modules),
            "--rosters", str(self.rosters),
            *extra,
        ]
        buffer = io.StringIO()
        old, sys.argv = sys.argv, argv
        try:
            with redirect_stdout(buffer), redirect_stderr(buffer):
                code = audit.main()
        finally:
            sys.argv = old
        return code, buffer.getvalue()

    def test_detects_shield_with_unusable_polearm(self):
        """The defect itself: unregistered spear + shield in one roster."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)
        self.assertIn("test_troop", out)

    def test_passes_when_registered(self):
        """Registering blade AND handle under OneHandedPolearm clears the finding."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS", out)

    def test_stylesheet_named_by_a_submodule_row_applies(self):
        """#1354: a row's path may sit in a subfolder (TAOM's placeholder_weapons/), and the
        engine applies it. Reading only `<name>.xslt` at the top level missed every one."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        module = self.modules / "TestMod"
        write(module / "SubModule.xml", submodule_xml(("WeaponDescriptions", "extra/ph_descriptions")))
        write(module / "ModuleData" / "extra" / "ph_descriptions.xslt",
              xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS", out)

    def test_row_for_another_id_is_not_applied(self):
        """A CraftingTemplates row's stylesheet must not register weapon descriptions."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        module = self.modules / "TestMod"
        write(module / "SubModule.xml", submodule_xml(("CraftingTemplates", "ct_overlay")))
        write(module / "ModuleData" / "ct_overlay.xslt", xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_checked_tree_replaces_its_installed_copy(self):
        """#1354 review: the module whose rosters are checked is read from that tree, not from its
        deployed copy, so an undeployed registration counts and a stale deployed one does not."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        own = self.rosters.parent
        write(own / "SubModule.xml",
              '<Module><Id value="TestOwn"/><DependedModules><DependedModule Id="TestArmory"/>'
              '</DependedModules><Xmls><XmlNode><XmlName id="WeaponDescriptions" '
              'path="ph/ph_descriptions"/></XmlNode></Xmls></Module>\n')
        write(own / "ModuleData" / "ph" / "ph_descriptions.xslt", xslt_registering("spear_blade", "spear_handle"))
        write(self.modules / "TestOwn" / "SubModule.xml", '<Module><Id value="TestOwn"/></Module>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS", out)

    def test_xsl_sibling_wins_over_xslt(self):
        """#1375: the engine tries `<path>.xsl` first and reads `.xslt` only when it is absent
        (MBObjectManager.HandleXsltList)."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.overlay.with_suffix(".xsl"), xslt_registering("spear_blade", "spear_handle"))
        # If the .xslt were applied too, its undefined piece would dangle and fail the run.
        write(self.overlay, xslt_registering("spear_blade", "spear_handle", "ghost_piece"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("PASS", out)

    def test_row_for_another_game_type_is_ignored(self):
        """#1375: a row whose <IncludedGameTypes> does not name Campaign never loads in a campaign."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Xmls><XmlNode><XmlName id="WeaponDescriptions" path="weapon_descriptions"/>'
              '<IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes>'
              '</XmlNode></Xmls></Module>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_row_naming_campaign_applies(self):
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Xmls><XmlNode><XmlName id="WeaponDescriptions" path="weapon_descriptions"/>'
              '<IncludedGameTypes><GameType value="MultiplayerGame"/><GameType value="Campaign"/>'
              '</IncludedGameTypes></XmlNode></Xmls></Module>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def own_submodule(self, *, depends=(), load_before=(), load_after=()):
        """The checked module's SubModule.xml: with one, only its playset counts (#1375)."""
        deps = "".join(f'<DependedModule Id="{m}"/>' for m in depends)
        meta = "".join(f'<DependedModuleMetadata id="{m}" order="LoadBeforeThis" optional="true"/>'
                       for m in load_before)
        meta += "".join(f'<DependedModuleMetadata id="{m}" order="LoadAfterThis" optional="true"/>'
                        for m in load_after)
        write(self.rosters.parent / "SubModule.xml",
              f'<Module><Id value="TestOwn"/><DependedModules>{deps}</DependedModules>'
              f'<DependedModuleMetadatas>{meta}</DependedModuleMetadatas></Module>\n')

    def register_in_testmod(self, depends=()):
        module = self.modules / "TestMod"
        deps = "".join(f'<DependedModule Id="{m}"/>' for m in depends)
        write(module / "SubModule.xml",
              f'<Module><Id value="TestMod"/><DependedModules>{deps}</DependedModules><Xmls>'
              '<XmlNode><XmlName id="WeaponDescriptions" path="weapon_descriptions"/></XmlNode>'
              '</Xmls></Module>\n')
        write(module / "ModuleData" / "weapon_descriptions.xslt", xslt_registering("spear_blade", "spear_handle"))

    def test_module_outside_the_playset_does_not_count(self):
        """#1375: AWOIAF_Armoury is installed but never loads beside TAOM, so its sheets must not count."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.own_submodule(depends=("Native", "TestArmory"))
        self.register_in_testmod()
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_load_before_metadata_module_counts(self):
        """TAOM names LOTRLOME_Armory only as optional LoadBeforeThis metadata, and it loads."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.own_submodule(depends=("Native", "TestArmory"), load_before=("TestMod",))
        self.register_in_testmod()
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def armory_rows(self, *nodes: str):
        """TestArmory's SubModule.xml with its WeaponDescriptions row and the given <XmlNode>s."""
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Id value="TestArmory"/><Xmls><XmlNode><XmlName id="WeaponDescriptions" '
              'path="weapon_descriptions"/></XmlNode>' + "".join(nodes) + '</Xmls></Module>\n')

    def test_piece_file_for_another_game_type_does_not_count(self):
        """#1398: Native's mp_crafting_pieces.xml loads only in multiplayer, so a piece defined
        only there is undefined in a campaign: a registration of it dangles."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.armory_rows(MP_ROW.format(id="CraftingPieces", path="mp_crafting_pieces"))
        write(self.modules / "TestArmory" / "ModuleData" / "mp_crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="mp_piece" piece_type="Blade"/></CraftingPieces>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle", "mp_piece"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("mp_piece", out)

    def test_item_file_for_another_game_type_does_not_count(self):
        """#1398: a shield defined only in a multiplayer item file (Native's mpitems.xml) is no
        shield in a campaign, so it pairs with nothing."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "mp_shield"))
        self.armory_rows(MP_ROW.format(id="Items", path="mpitems"))
        write(self.modules / "TestArmory" / "ModuleData" / "mpitems.xml",
              '<Items><Item id="mp_shield" Type="Shield"/></Items>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_file_named_by_a_campaign_row_too_counts(self):
        """The engine loads a file once any row for the game names it."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "mp_shield"))
        self.armory_rows(MP_ROW.format(id="Items", path="mpitems"),
                         '<XmlNode><XmlName id="Items" path="mpitems"/></XmlNode>')
        write(self.modules / "TestArmory" / "ModuleData" / "mpitems.xml",
              '<Items><Item id="mp_shield" Type="Shield"/></Items>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_folder_for_another_game_type_does_not_count(self):
        """A row whose `<path>.xml` is absent names every *.xml in the folder `<path>`
        (MBObjectManager.GetMergedXmlForManaged)."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "mp_shield"))
        self.armory_rows(MP_ROW.format(id="Items", path="mp"))
        write(self.modules / "TestArmory" / "ModuleData" / "mp" / "shields.xml",
              '<Items><Item id="mp_shield" Type="Shield"/></Items>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_folder_beside_a_file_of_the_same_path_still_counts(self):
        """With `<path>.xml` present the engine never reads the folder, so the folder is not skipped."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "mp_shield"))
        self.armory_rows(MP_ROW.format(id="Items", path="mp"))
        write(self.modules / "TestArmory" / "ModuleData" / "mp.xml", '<Items/>\n')
        write(self.modules / "TestArmory" / "ModuleData" / "mp" / "shields.xml",
              '<Items><Item id="mp_shield" Type="Shield"/></Items>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_comment_under_included_game_types_names_no_type(self):
        """The engine reads only element children; a comment-only list is empty, so the row loads."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Xmls><XmlNode><XmlName id="WeaponDescriptions" path="weapon_descriptions"/>'
              '<IncludedGameTypes><!-- note --></IncludedGameTypes></XmlNode></Xmls></Module>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_module_registering_a_vanilla_mp_piece_elsewhere_fails(self):
        """Vanilla lists `mp_only` under TwoHandedPolearm; a module that adds it to OneHandedPolearm
        registers an undefined piece of its own."""
        write(self.modules / "Native" / "ModuleData" / "weapon_descriptions.xml", DESCRIPTIONS.replace(
            '<AvailablePiece id="spear_blade"/>', '<AvailablePiece id="spear_blade"/><AvailablePiece id="mp_only"/>'))
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        write(self.overlay, xslt_registering("mp_only"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("mp_only", out)

    def test_custom_game_item_file_counts(self):
        """TAOM plays custom battles too (Fords of War): a CustomGame row's items load there."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "fords_shield"))
        self.armory_rows('<XmlNode><XmlName id="Items" path="fords_items"/><IncludedGameTypes>'
                         '<GameType value="CustomGame"/></IncludedGameTypes></XmlNode>')
        write(self.modules / "TestArmory" / "ModuleData" / "fords_items.xml",
              '<Items><Item id="fords_shield" Type="Shield"/></Items>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_undefined_piece_vanilla_lists_is_not_a_finding(self):
        """Native's campaign weapon_descriptions.xml lists 429 pieces only its multiplayer file
        defines. Vanilla's own list is not ours to fix (see the next test for a module's)."""
        write(self.modules / "Native" / "ModuleData" / "weapon_descriptions.xml", DESCRIPTIONS.replace(
            '<AvailablePiece id="spear_blade"/>', '<AvailablePiece id="spear_blade"/><AvailablePiece id="mp_only"/>'))
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertNotIn("mp_only", out)

    def test_only_the_first_xmlname_of_a_node_counts(self):
        """#1398: the engine reads one XmlName per XmlNode (SelectSingleNode)."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Xmls><XmlNode><XmlName id="CraftingTemplates" path="none"/>'
              '<XmlName id="WeaponDescriptions" path="weapon_descriptions"/></XmlNode></Xmls></Module>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_any_element_under_included_game_types_names_a_type(self):
        """#1398: the engine reads `value` from every child element, whatever its tag."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Xmls><XmlNode><XmlName id="WeaponDescriptions" path="weapon_descriptions"/>'
              '<IncludedGameTypes><Type value="MultiplayerGame"/></IncludedGameTypes>'
              '</XmlNode></Xmls></Module>\n')
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)

    def test_module_ids_compare_without_case(self):
        """#1398: the engine lower-cases module ids, so a dependency spelled in another case loads."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.own_submodule(depends=("native", "testarmory"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_load_after_metadata_module_does_not_count(self):
        """TAOM names Coop as LoadAfterThis: a module that only loads after it is not its playset."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.own_submodule(depends=("Native", "TestArmory"), load_after=("TestMod",))
        self.register_in_testmod()
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_first_of_two_modules_with_one_id_wins(self):
        """The engine keeps the first module it finds for an Id (ModuleHelper), so a stray copy
        folder that sorts later must not replace the real one."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.overlay, xslt_registering("spear_blade", "spear_handle"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              submodule_xml(("WeaponDescriptions", "weapon_descriptions"))
              .replace("<Module>", '<Module><Id value="TestArmory"/>'))
        write(self.modules / "TestArmory - Copy" / "SubModule.xml", '<Module><Id value="TestArmory"/></Module>\n')
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_dependency_of_a_dependency_counts(self):
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        self.own_submodule(depends=("Native", "TestArmory"))
        write(self.modules / "TestArmory" / "SubModule.xml",
              '<Module><Id value="TestArmory"/><DependedModules><DependedModule Id="TestMod"/>'
              '</DependedModules></Module>\n')
        self.register_in_testmod()
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_row_xml_merge_is_bad_input(self):
        """#1375: a row naming an .xml file (or a folder) merges by the engine's XSD rules, which
        the gate does not model, so a non-Native one stops the run (exit 2) and names the row."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        write(self.modules / "TestArmory" / "ModuleData" / "weapon_descriptions.xml", DESCRIPTIONS)
        code, out = self.run_gate()
        self.assertEqual(code, 2, out)
        self.assertIn("TestArmory", out)
        self.assertIn("weapon_descriptions", out)

    def test_row_folder_merge_is_bad_input(self):
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        (self.modules / "TestArmory" / "ModuleData" / "weapon_descriptions").mkdir(parents=True)
        code, out = self.run_gate()
        self.assertEqual(code, 2, out)
        self.assertIn("TestArmory", out)

    def test_roster_weapon_that_resolves_no_usage_fails(self):
        """#1354's root: a weapon whose pieces no description lists resolved to no usage, and the
        gate passed in silence. A roster weapon must resolve, or the run fails naming it."""
        write(self.modules / "TestArmory" / "ModuleData" / "items.xml", ITEMS.replace(
            "</Items>",
            '  <CraftedItem id="test_orphan" crafting_template="SpearTemplate">'
            '<Pieces><Piece id="spear_blade"/><Piece id="great_handle"/></Pieces></CraftedItem>\n</Items>'))
        write(self.rosters / "troops.xml", roster_xml("test_orphan"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_orphan", out)
        self.assertIn("resolve no usage", out)

    def test_missing_submodule_falls_back_to_taoms_and_says_so(self):
        """--rosters with no SubModule.xml beside it reads TAOM's own, and names the file it used."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("Skipped:", out)
        self.assertIn("NavalDLC", out)

    def test_malformed_submodule_exits_2_and_names_it(self):
        """A broken SubModule.xml in any installed module is bad input (exit 2), not a finding."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        write(self.modules / "Broken" / "SubModule.xml", "")
        code, out = self.run_gate()
        self.assertEqual(code, 2, out)
        self.assertIn("Broken", out)

    def test_stylesheet_without_a_submodule_row_is_ignored(self):
        """The engine reads no stylesheet a SubModule.xml row does not name, so neither does the gate."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.modules / "TestMod" / "ModuleData" / "weapon_descriptions.xslt",
              xslt_registering("spear_blade", "spear_handle"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_incompatible_module_is_ignored(self):
        """#1354: TAOM's <IncompatibleModules> (NavalDLC) never load beside it, so a registration
        or a piece id there must not count. With no SubModule.xml beside --rosters the list is
        TAOM's. The engine keys a module by its <Id>, not its folder name (#1375)."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        module = self.modules / "NavalFolder"
        write(module / "SubModule.xml", submodule_xml(("WeaponDescriptions", "weapon_descriptions"))
              .replace("<Module>", '<Module><Id value="NavalDLC"/>'))
        write(module / "ModuleData" / "weapon_descriptions.xslt",
              xslt_registering("spear_blade", "spear_handle", "ghost_piece"))
        # Its pieces and items must not count either: a piece defined there would hide a dangling
        # id, and a shield defined there would hide a pair.
        write(self.overlay, xslt_registering("naval_piece"))
        write(module / "ModuleData" / "crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="naval_piece" piece_type="Blade"/></CraftingPieces>\n')
        write(module / "ModuleData" / "items.xml", '<Items><Item id="naval_shield" Type="Shield"/></Items>\n')
        write(self.rosters / "troops2.xml", roster_xml("test_greatsword", "naval_shield").replace(
            'id="test_troop"', 'id="naval_troop"'))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)
        self.assertNotIn("ghost_piece", out)
        self.assertIn("naval_piece", out)
        self.assertNotIn("naval_troop", out)

    def test_registration_must_cover_every_piece(self):
        """Half a registration is no registration -- Crafting.cs needs every used piece."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        write(self.overlay, xslt_registering("spear_blade"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)

    def test_first_description_wins(self):
        """A spear with no shield in its roster is not a finding, however it resolves."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)

    def test_ranged_is_not_a_finding(self):
        """A bow's usage set is requires_no_shield too; pairing it with a shield is normal."""
        write(self.rosters / "troops.xml", roster_xml("test_bow", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertNotIn("test_bow", out)

    def test_twohanded_fails_like_polearm(self):
        """#1312: a two-handed sword beside a shield is in the ratchet, so the default run fails."""
        write(self.rosters / "troops.xml", roster_xml("test_greatsword", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("\n\nFAIL:", out)
        self.assertIn("test_greatsword", out)
        self.assertIn("no one-handed mode", out)  # the fix text sits inside the FAIL block

    def test_onehanded_weapon_with_blocking_usage_fails(self):
        """A one-handed weapon whose primary usage forbids a shield is the same defect."""
        write(self.rosters / "troops.xml", roster_xml("test_oddsword", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_oddsword", out)

    def test_unheld_dangling_piece_fails(self):
        """An <AvailablePiece> with no <CraftingPiece> fails the default run unless it is held."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        write(self.overlay, xslt_registering("ghost_piece"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        # At line start after a blank line: the hook cuts FAIL blocks on exactly that shape.
        self.assertIn("\n\nFAIL: 1 <AvailablePiece> id(s)", out)
        self.assertIn("ghost_piece", out)

    def test_held_dangling_piece_passes(self):
        """#1312: a held dangling id is printed with its reason and fails neither mode."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        write(self.overlay, xslt_registering("ghost_piece"))
        self.held_dangling({"ghost_piece": "#000 -- fixture"})
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("\n\nHELD: 1 dangling", out)
        self.assertIn("#000 -- fixture", out)

    def test_stale_held_dangling_piece_fails(self):
        """A held id that is no longer dangling is a suppression that outlived its finding."""
        write(self.rosters / "troops.xml", roster_xml("test_spear"))
        self.held_dangling({"gone_piece": "#000 -- fixture"})
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("FAIL: 1 KNOWN_DANGLING id(s) no longer dangle", out)
        self.assertIn("gone_piece", out)

    def test_standalone_roster_casing_is_scanned(self):
        """#526: `<Equipment>` in an equipmentsets-shaped file must be read like `<equipment>`.

        Before the walker matched both casings this exact input printed PASS, which is how 13
        real shield+polearm pairs (player starting gear among them) stayed invisible.
        """
        write(self.rosters / "equipmentsets" / "sets.xml",
              standalone_roster_xml("test_roster", "test_spear", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("test_spear", out)
        self.assertIn("test_roster", out)

    def test_known_failure_is_held_not_failed(self):
        """A ratcheted pair reports under KNOWN and does not fail the run."""
        write(self.rosters / "equipmentsets" / "sets.xml",
              standalone_roster_xml("test_roster", "test_spear", "test_shield"))
        self.ratchet({("test_roster", "test_spear"): (1, "#000 -- fixture")})
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("KNOWN", out)
        self.assertIn("test_spear", out)
        self.assertNotIn("FAIL:", out)

    def test_stale_known_failure_entry_fails(self):
        """An entry matching nothing is a suppression outliving its finding, and must FAIL.

        Without this the ratchet decays the way `DocumentedExceptions` in
        EnlistmentRosterCultureCoverageTests is written to prevent: silently green forever,
        widening the blind spot for whoever inherits it.
        """
        write(self.rosters / "troops.xml", roster_xml("test_spear"))  # no shield, so no finding
        self.ratchet({("gone_troop", "gone_spear"): (1, "#000 -- fixture")})
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("stale KNOWN_FAILURES", out)
        self.assertIn("gone_spear", out)

    def test_ratchet_does_not_absorb_a_second_occurrence(self):
        """A ratcheted pair occurring MORE often than ratcheted is new debt, and must fail.

        Keying the ratchet on (roster, item) alone made it blind to multiplicity: the shipped
        baseline had 10 keys suppressing 13 occurrences, so an already-listed roster gaining a
        second copy of the same unusable polearm would have been filed as old debt.
        """
        write(self.rosters / "equipmentsets" / "sets.xml",
              standalone_roster_xml("test_roster", "test_spear", "test_shield"))
        write(self.rosters / "equipmentsets" / "sets2.xml",
              standalone_roster_xml("test_roster", "test_spear", "test_shield"))
        self.ratchet({("test_roster", "test_spear"): (1, "#000 -- fixture")})
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("FAIL", out)

    def test_ratchet_entry_that_overstates_the_debt_fails(self):
        """A count HIGHER than reality would silently absorb a future regression back up to it."""
        write(self.rosters / "equipmentsets" / "sets.xml",
              standalone_roster_xml("test_roster", "test_spear", "test_shield"))
        self.ratchet({("test_roster", "test_spear"): (5, "#000 -- fixture")})
        code, out = self.run_gate()
        self.assertEqual(code, 1, out)
        self.assertIn("expects 5 occurrence(s)", out)

    def test_missing_install_skips_cleanly(self):
        """An absent install must SKIP at exit 0 -- never a false PASS, never a hard failure."""
        write(self.rosters / "troops.xml", roster_xml("test_spear", "test_shield"))
        argv = [
            sys.executable, str(TOOL),
            "--game-modules", str(Path(self._tmp.name) / "no-such-install"),
            "--rosters", str(self.rosters),
        ]
        done = subprocess.run(argv, capture_output=True, text=True)
        self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
        self.assertIn("SKIP", done.stdout)
        self.assertNotIn("PASS", done.stdout)


if __name__ == "__main__":
    unittest.main()
