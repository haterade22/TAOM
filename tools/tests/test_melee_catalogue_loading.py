"""melee_catalogue finds module data the way the engine and the shield gate do (#1372).

The engine applies the stylesheets a module's SubModule.xml <XmlName> rows name, which may sit in a
subfolder (TAOM's placeholder_weapons/), and it never loads a module TAOM lists under
<IncompatibleModules> (NavalDLC). The catalogue used to apply ModuleData/<name>.xslt by file name
and read every installed folder.

Map:
  - row_stylesheet_in_a_subfolder_applies    -> a row's subfolder sheet registers its pieces
  - stylesheet_without_a_row_is_ignored      -> a top-level sheet no row names does nothing
  - incompatible_module_is_left_out          -> its sheets, pieces, items and shields do not count
  - unparseable_submodule_is_a_catalogue_error -> bad input stops the load, naming the file
  - installed_taom_stylesheets_are_left_out  -> callers price TAOM's placeholders from their own
                                                tree, so a stale deployed sheet must not count
  - own_submodule_unreadable_or_missing      -> a CatalogueError, never a silent empty skip list
  - module_outside_the_playset_is_left_out   -> #1375: only the modules TAOM loads with count
  - data_files_for_another_game_type_are_left_out -> #1398: multiplayer-only rows do not load
"""
from __future__ import annotations

import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import melee_catalogue as mc  # noqa: E402

NATIVE_WD = """<WeaponDescriptions>
  <WeaponDescription id="OneHandedPolearm" weapon_class="OneHandedPolearm" item_usage_features="onehanded:polearm">
    <AvailablePieces><AvailablePiece id="native_blade"/></AvailablePieces>
  </WeaponDescription>
</WeaponDescriptions>
"""
NATIVE_CT = """<CraftingTemplates>
  <CraftingTemplate id="Spear" item_type="Polearm">
    <WeaponDescriptions><WeaponDescription id="OneHandedPolearm"/></WeaponDescriptions>
  </CraftingTemplate>
</CraftingTemplates>
"""


def registering(*piece_ids: str) -> str:
    """An additive stylesheet that adds pieces to OneHandedPolearm, shaped like the Armory's."""
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


def submodule(*rows: tuple[str, str], incompatible: tuple[str, ...] = (), module_id: str = "TestOwn",
              depends: tuple[str, ...] = ()) -> str:
    xml = "".join(f'<XmlNode><XmlName id="{i}" path="{p}"/></XmlNode>' for i, p in rows)
    inc = "".join(f'<Module Id="{m}"/>' for m in incompatible)
    deps = "".join(f'<DependedModule Id="{m}"/>' for m in depends)
    return (f'<Module><Id value="{module_id}"/><DependedModules>{deps}</DependedModules><Xmls>{xml}</Xmls>'
            f"<IncompatibleModules>{inc}</IncompatibleModules></Module>\n")


# The playset TAOM_SUBMODULE names (#1375): the catalogue reads these modules and no others.
PLAYSET = ("Native", "TestMod", "Naval", "Broken", "TAOM")


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


@unittest.skipIf(mc.LET is None, "needs lxml")
class CatalogueLoadingTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.modules = self.tmp / "Modules"
        write(self.modules / "Native" / "ModuleData" / "weapon_descriptions.xml", NATIVE_WD)
        write(self.modules / "Native" / "ModuleData" / "crafting_templates.xml", NATIVE_CT)
        # The SubModule.xml whose <IncompatibleModules> decides what is left out (TAOM's in use).
        self.own = self.tmp / "own" / "SubModule.xml"
        write(self.own, submodule(depends=PLAYSET))
        old = mc.TAOM_SUBMODULE
        mc.TAOM_SUBMODULE = self.own
        self.addCleanup(setattr, mc, "TAOM_SUBMODULE", old)

    def available(self) -> frozenset[str]:
        return mc.load_descriptions(self.modules)["OneHandedPolearm"].available

    def test_row_stylesheet_in_a_subfolder_applies(self):
        mod = self.modules / "TestMod"
        write(mod / "SubModule.xml", submodule(("WeaponDescriptions", "ph/ph_descriptions"), module_id="TestMod"))
        write(mod / "ModuleData" / "ph" / "ph_descriptions.xslt", registering("ph_blade"))
        self.assertIn("ph_blade", self.available())

    def test_stylesheet_without_a_row_is_ignored(self):
        mod = self.modules / "TestMod"
        write(mod / "SubModule.xml", submodule(("CraftingTemplates", "crafting_templates"), module_id="TestMod"))
        write(mod / "ModuleData" / "weapon_descriptions.xslt", registering("ghost_blade"))
        self.assertNotIn("ghost_blade", self.available())
        self.assertIn("native_blade", self.available())

    def test_incompatible_module_is_left_out(self):
        write(self.own, submodule(incompatible=("Naval",), depends=PLAYSET))
        naval = self.modules / "Naval"
        write(naval / "SubModule.xml", submodule(("WeaponDescriptions", "weapon_descriptions"), module_id="Naval"))
        write(naval / "ModuleData" / "weapon_descriptions.xslt", registering("naval_blade"))
        write(naval / "ModuleData" / "naval_crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="naval_blade" piece_type="Blade" length="80" weight="1"/>'
              '</CraftingPieces>\n')
        write(naval / "ModuleData" / "items.xml",
              '<Items><Item id="naval_shield" Type="Shield"/>'
              '<CraftedItem id="naval_spear" crafting_template="Spear">'
              '<Pieces><Piece id="naval_blade" Type="Blade"/></Pieces></CraftedItem></Items>\n')
        self.assertNotIn("naval_blade", self.available())
        self.assertNotIn("naval_blade", mc.load_pieces(self.modules))
        self.assertNotIn("naval_blade", mc.load_excluded_features(self.modules))
        self.assertNotIn("naval_spear", mc.load_items(self.modules))
        self.assertNotIn("naval_shield", mc.load_shield_ids(self.modules))

    def test_unparseable_submodule_is_a_catalogue_error(self):
        write(self.modules / "Broken" / "SubModule.xml", "")
        with self.assertRaises(mc.CatalogueError) as ctx:
            mc.load_descriptions(self.modules)
        self.assertIn("Broken", str(ctx.exception))

    def test_installed_taom_stylesheets_are_left_out(self):
        """TAOM's placeholder registrations are priced from the caller's own tree
        (taom_schema.price_placeholders, generate_placeholder_weapons.price_new). A deployed copy
        in the catalogue would hide a registration the tree has lost."""
        write(self.own, submodule(module_id="TAOM", depends=PLAYSET))
        taom = self.modules / "TAOM"
        write(taom / "SubModule.xml", submodule(("WeaponDescriptions", "ph/ph_descriptions"), module_id="TAOM"))
        write(taom / "ModuleData" / "ph" / "ph_descriptions.xslt", registering("stale_blade"))
        self.assertNotIn("stale_blade", self.available())
        # Only its stylesheets: TAOM's pieces still load (callers overlay the tree's on them).
        write(taom / "ModuleData" / "ph" / "ph_crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="taom_piece" piece_type="Blade" length="80" weight="1"/>'
              '</CraftingPieces>\n')
        self.assertIn("taom_piece", mc.load_pieces(self.modules))

    def test_module_outside_the_playset_is_left_out(self):
        """#1375: an installed module TAOM neither depends on nor loads after (AWOIAF_Armoury)
        never loads beside it, so its sheets, pieces and items do not count."""
        other = self.modules / "Other"
        write(other / "SubModule.xml", submodule(("WeaponDescriptions", "weapon_descriptions"), module_id="Other"))
        write(other / "ModuleData" / "weapon_descriptions.xslt", registering("other_blade"))
        write(other / "ModuleData" / "other_crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="other_blade" piece_type="Blade" length="80" weight="1"/>'
              '</CraftingPieces>\n')
        self.assertNotIn("other_blade", self.available())
        self.assertNotIn("other_blade", mc.load_pieces(self.modules))

    def test_data_files_for_another_game_type_are_left_out(self):
        """#1398: a module's multiplayer-only rows (Native's mp_crafting_pieces, mpitems) name files
        a campaign never loads, so their pieces, items and shields do not count."""
        mod = self.modules / "TestMod"
        write(mod / "SubModule.xml",
              '<Module><Id value="TestMod"/><Xmls>'
              '<XmlNode><XmlName id="CraftingPieces" path="mp_crafting_pieces"/>'
              '<IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes></XmlNode>'
              '<XmlNode><XmlName id="Items" path="mpitems"/>'
              '<IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes></XmlNode>'
              '</Xmls></Module>\n')
        write(mod / "ModuleData" / "mp_crafting_pieces.xml",
              '<CraftingPieces><CraftingPiece id="mp_blade" piece_type="Blade" length="80" weight="1"/>'
              '</CraftingPieces>\n')
        write(mod / "ModuleData" / "mpitems.xml",
              '<Items><Item id="mp_shield" Type="Shield"/>'
              '<CraftedItem id="mp_spear" crafting_template="Spear">'
              '<Pieces><Piece id="native_blade" Type="Blade"/></Pieces></CraftedItem></Items>\n')
        self.assertNotIn("mp_blade", mc.load_pieces(self.modules))
        self.assertNotIn("mp_blade", mc.load_excluded_features(self.modules))
        self.assertNotIn("mp_spear", mc.load_items(self.modules))
        self.assertNotIn("mp_shield", mc.load_shield_ids(self.modules))

    def test_missing_native_base_is_a_catalogue_error(self):
        (self.modules / "Native" / "ModuleData" / "weapon_descriptions.xml").unlink()
        with self.assertRaises(mc.CatalogueError):
            mc.load_descriptions(self.modules)

    def test_own_submodule_unreadable_or_missing(self):
        write(self.own, "")
        with self.assertRaises(mc.CatalogueError):
            mc.load_pieces(self.modules)
        mc.TAOM_SUBMODULE = self.tmp / "missing" / "SubModule.xml"
        with self.assertRaises(mc.CatalogueError):
            mc.load_pieces(self.modules)


if __name__ == "__main__":
    unittest.main()
