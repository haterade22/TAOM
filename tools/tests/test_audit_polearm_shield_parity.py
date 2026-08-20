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
  - first_description_wins                -> template order decides the primary usage
  - ranged_is_not_a_finding               -> a bow + shield is normal, not a defect
  - twohanded_is_advisory_until_strict    -> the ratchet boundary is real
  - missing_install_skips_cleanly         -> absent install exits 0, never a false PASS
"""
import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
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
        self.addCleanup(self._tmp.cleanup)

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
            with redirect_stdout(buffer):
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

    def test_twohanded_is_advisory_until_strict(self):
        """Two-handed swords hit the same rule but sit outside the ratchet until --strict."""
        write(self.rosters / "troops.xml", roster_xml("test_greatsword", "test_shield"))
        code, out = self.run_gate()
        self.assertEqual(code, 0, out)
        self.assertIn("WARN", out)
        self.assertIn("test_greatsword", out)

        code, out = self.run_gate("--strict")
        self.assertEqual(code, 1, out)

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
