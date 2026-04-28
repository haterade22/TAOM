"""Unit tests for the FBX -> 4-XML weapon-build pipeline.

Run with:
    python -m pytest tools/tests/test_build_weapon_xml.py
or:
    python tools/tests/test_build_weapon_xml.py    (uses unittest)
"""

from __future__ import annotations

import os
import sys
import textwrap
import unittest

# Make `tools/` importable so `from weapon_xml import ...` works.
HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

from weapon_xml import classify, manifest, pipeline, render_items, render_pieces, render_xslt, verify  # noqa: E402


# ---------------------------------------------------------------------------
# classify
# ---------------------------------------------------------------------------

class TestClassify(unittest.TestCase):

    def test_classifies_blade_guard_hilt_pommel(self):
        meshes = [
            "wm_swan_knight_sword_a_blade",
            "wm_swan_knight_sword_a_guard",
            "wm_swan_knight_sword_a_hilt",
            "wm_swan_knight_sword_a_pommel",
            "bo_wm_swan_knight_sword_a_blade",  # collision pair for the blade
        ]
        classified, unmatched = classify.classify_meshes(meshes)
        self.assertEqual(unmatched, [])
        roles = sorted(c.role for c in classified)
        self.assertEqual(roles, ["blade", "guard", "hilt", "pommel"])
        blade = next(c for c in classified if c.role == "blade")
        self.assertEqual(blade.collision, "bo_wm_swan_knight_sword_a_blade")
        self.assertEqual(blade.piece_type, "Blade")

    def test_handle_aliases_to_handle_piece_type(self):
        classified, _ = classify.classify_meshes(["wm_x_y_handle"])
        self.assertEqual(classified[0].piece_type, "Handle")
        classified, _ = classify.classify_meshes(["wm_x_y_hilt"])
        self.assertEqual(classified[0].piece_type, "Handle")

    def test_unmatched_mesh(self):
        _, unmatched = classify.classify_meshes(["wm_some_random_thing"])
        self.assertEqual(unmatched, ["wm_some_random_thing"])

    def test_culture_detection(self):
        self.assertEqual(classify.detect_culture_from_id("wm_swan_knight_sword_a_blade"), "gondor")
        self.assertEqual(classify.detect_culture_from_id("wm_witch_king_sword_blade"), "mordor")
        self.assertEqual(classify.detect_culture_from_id("wm_thranduil_sword_blade"), "mirkwood")
        self.assertIsNone(classify.detect_culture_from_id("wm_unknown_thing"))

    def test_derive_collision_name_keeps_wm_prefix(self):
        # wm_ stays on the bo_ name.
        self.assertEqual(
            classify.derive_collision_name("wm_witch_king_sword_blade"),
            "bo_wm_witch_king_sword_blade",
        )

    def test_derive_collision_name_strips_sm_prefix(self):
        # sm_ is dropped from the bo_ name (Armory convention; verified in real data).
        self.assertEqual(
            classify.derive_collision_name("sm_uruk_halberd_blade_a1"),
            "bo_uruk_halberd_blade_a1",
        )

    def test_derive_collision_name_no_prefix(self):
        self.assertEqual(classify.derive_collision_name("plain_mesh"), "bo_plain_mesh")

    def test_group_by_stem(self):
        classified, _ = classify.classify_meshes([
            "wm_a_b_blade", "wm_a_b_guard",
            "wm_c_d_blade", "wm_c_d_guard",
        ])
        groups = classify.group_by_stem(classified)
        self.assertEqual(set(groups.keys()), {"wm_a_b", "wm_c_d"})
        self.assertEqual(len(groups["wm_a_b"]), 2)


# ---------------------------------------------------------------------------
# manifest
# ---------------------------------------------------------------------------

CRAFTED_MANIFEST = textwrap.dedent("""\
    <WeaponManifest>
      <CraftedWeapon id="wm_test_sword_a"
                     name="{=test_a_name}Test Sword A"
                     weapon_class="OneHandedSword"
                     culture="gondor"
                     tier="3">
        <Blade length="100" weight="0.9" body_name="bo_test_sword_a_blade">
          <Thrust damage_type="Pierce" damage_factor="3.5" />
          <Swing  damage_type="Cut"    damage_factor="3.5" />
        </Blade>
        <Guard length="3" weight="0.15" armor_bonus="5" />
        <Hilt  length="11" weight="0.2" />
        <Pommel length="3" weight="0.1" />
      </CraftedWeapon>
    </WeaponManifest>
""")

SINGLE_MANIFEST = textwrap.dedent("""\
    <WeaponManifest>
      <SinglePieceWeapon id="test_warbow_a"
                         name="{=test_warbow_a_name}Test Warbow"
                         weapon_class="Bow"
                         culture="gondor"
                         body_name="bo_test_warbow_a"
                         mesh="test_warbow_a"
                         weight="0.4"
                         Type="Bow">
        <Weapon thrust_speed="92" speed_rating="87" missile_speed="84"
                weapon_length="106" thrust_damage="30" thrust_damage_type="Pierce"
                item_usage="bow" physics_material="wood_weapon">
          <WeaponFlags RangedWeapon="true" HasString="true" />
        </Weapon>
      </SinglePieceWeapon>
    </WeaponManifest>
""")


class TestManifest(unittest.TestCase):

    def _write(self, body: str, tmp_path: str) -> str:
        path = os.path.join(tmp_path, "manifest.xml")
        with open(path, "w", encoding="utf-8") as f:
            f.write(body)
        return path

    def test_parse_crafted(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(CRAFTED_MANIFEST, tmp)
            m = manifest.parse_manifest(path)
            self.assertEqual(len(m.crafted), 1)
            self.assertEqual(len(m.single), 0)
            spec = m.crafted[0]
            self.assertEqual(spec.base_id, "wm_test_sword_a")
            self.assertEqual(spec.weapon_class, "OneHandedSword")
            self.assertEqual(spec.culture, "gondor")
            self.assertEqual(spec.tier, "3")
            roles = [p.role for p in spec.pieces]
            self.assertEqual(roles, ["blade", "guard", "hilt", "pommel"])
            blade = spec.pieces[0]
            self.assertEqual(blade.piece_type, "Blade")
            self.assertEqual(blade.thrust, {"damage_type": "Pierce", "damage_factor": "3.5"})
            self.assertEqual(blade.swing,  {"damage_type": "Cut",    "damage_factor": "3.5"})
            self.assertEqual(blade.blade_data["body_name"], "bo_test_sword_a_blade")
            guard = spec.pieces[1]
            self.assertEqual(guard.piece_type, "Guard")
            self.assertEqual(guard.stat_contributions, {"armor_bonus": "5"})

    def test_parse_single_piece(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(SINGLE_MANIFEST, tmp)
            m = manifest.parse_manifest(path)
            self.assertEqual(len(m.single), 1)
            spec = m.single[0]
            self.assertEqual(spec.item_id, "test_warbow_a")
            self.assertEqual(spec.weapon_class, "Bow")
            self.assertEqual(spec.body_name, "bo_test_warbow_a")
            self.assertEqual(spec.weapon_attrs["thrust_speed"], "92")
            self.assertEqual(spec.weapon_flags, {"RangedWeapon": "true", "HasString": "true"})

    def test_missing_required_attribute_errors(self):
        import tempfile
        bad = "<WeaponManifest><CraftedWeapon id='x' name='n' /></WeaponManifest>"
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(bad, tmp)
            with self.assertRaises(manifest.ManifestError):
                manifest.parse_manifest(path)


# ---------------------------------------------------------------------------
# render_pieces
# ---------------------------------------------------------------------------

class TestRenderPieces(unittest.TestCase):

    def test_render_blade_block_shape(self):
        spec = manifest.PieceSpec(
            role="blade",
            piece_type="Blade",
            attrs={"length": "100", "weight": "0.9"},
            blade_data={"body_name": "bo_x_blade"},
            thrust={"damage_type": "Pierce", "damage_factor": "3.5"},
            swing={"damage_type": "Cut", "damage_factor": "3.5"},
        )
        rendered = render_pieces.render_piece(
            piece_id="wm_x_blade",
            piece=spec,
            name="{=k}Foo Blade",
            tier="3",
            mesh="wm_x_blade",
        )
        block = rendered.block
        # Header attribute-per-line.
        self.assertIn('<CraftingPiece\n', block)
        self.assertIn('id="wm_x_blade"', block)
        self.assertIn('piece_type="Blade"', block)
        self.assertIn('mesh="wm_x_blade"', block)
        # BladeData with Thrust and Swing, default stack_amount + physics_material.
        self.assertIn('<BladeData\n', block)
        self.assertIn('stack_amount="3"', block)
        self.assertIn('physics_material="metal_weapon"', block)
        self.assertIn('body_name="bo_x_blade"', block)
        self.assertIn('<Thrust\n', block)
        self.assertIn('damage_type="Pierce"', block)
        self.assertIn('damage_factor="3.5"', block)
        self.assertTrue(block.rstrip().endswith("</CraftingPiece>"))

    def test_idempotent_skip_existing(self):
        existing_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<CraftingPieces>\n'
            '    <CraftingPiece\n'
            '        id="wm_already_here_blade"\n'
            '        piece_type="Blade"\n'
            '        mesh="wm_already_here_blade">\n'
            '    </CraftingPiece>\n'
            '</CraftingPieces>\n'
        )
        spec = manifest.PieceSpec(role="blade", piece_type="Blade", attrs={"length": "100", "weight": "0.9"})
        same = render_pieces.render_piece(
            piece_id="wm_already_here_blade", piece=spec, name="x", tier="3", mesh="wm_already_here_blade"
        )
        new_xml, inserted = render_pieces.insert_blocks(existing_xml, [same])
        self.assertEqual(new_xml, existing_xml)
        self.assertEqual(inserted, [])

    def test_appends_before_root_close(self):
        existing_xml = (
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<CraftingPieces>\n'
            '</CraftingPieces>\n'
        )
        spec = manifest.PieceSpec(role="guard", piece_type="Guard", attrs={"length": "3", "weight": "0.15"})
        new_block = render_pieces.render_piece(
            piece_id="wm_x_guard", piece=spec, name="{=k}X Guard", tier="3", mesh="wm_x_guard"
        )
        new_xml, inserted = render_pieces.insert_blocks(existing_xml, [new_block])
        self.assertEqual(len(inserted), 1)
        self.assertIn('id="wm_x_guard"', new_xml)
        self.assertTrue(new_xml.rstrip().endswith("</CraftingPieces>"))


# ---------------------------------------------------------------------------
# render_items
# ---------------------------------------------------------------------------

class TestRenderItems(unittest.TestCase):

    def test_crafted_item_references_pieces(self):
        spec = manifest.CraftedWeaponSpec(
            base_id="wm_x_sword_a",
            name="{=k}X Sword A",
            weapon_class="OneHandedSword",
            culture="gondor",
            tier="3",
        )
        rendered = render_items.render_crafted_item(spec, piece_ids_by_role={
            "blade": "wm_x_sword_a_blade",
            "guard": "wm_x_sword_a_guard",
            "hilt": "wm_x_sword_a_hilt",
            "pommel": "wm_x_sword_a_pommel",
        })
        block = rendered.block
        self.assertIn('crafting_template="OneHandedSword"', block)
        self.assertIn('culture="Culture.gondor"', block)
        for piece_id in ("wm_x_sword_a_blade", "wm_x_sword_a_guard", "wm_x_sword_a_hilt", "wm_x_sword_a_pommel"):
            self.assertIn(f'id="{piece_id}"', block)
        # Order: Blade, Guard, Handle (hilt), Pommel
        idx_blade = block.index("wm_x_sword_a_blade")
        idx_guard = block.index("wm_x_sword_a_guard")
        idx_hilt  = block.index("wm_x_sword_a_hilt")
        idx_pommel = block.index("wm_x_sword_a_pommel")
        self.assertLess(idx_blade, idx_guard)
        self.assertLess(idx_guard, idx_hilt)
        self.assertLess(idx_hilt, idx_pommel)

    def test_single_piece_item_includes_weapon_component(self):
        spec = manifest.SinglePieceWeaponSpec(
            item_id="test_bow",
            name="{=k}Test Bow",
            weapon_class="Bow",
            culture="gondor",
            body_name="bo_test_bow",
            item_attrs={"mesh": "test_bow", "weight": "0.4", "Type": "Bow"},
            weapon_attrs={"weapon_class": "Bow", "thrust_speed": "92", "missile_speed": "84"},
            weapon_flags={"RangedWeapon": "true"},
        )
        rendered = render_items.render_single_piece_item(spec)
        block = rendered.block
        self.assertIn('id="test_bow"', block)
        self.assertIn('body_name="bo_test_bow"', block)
        self.assertIn('mesh="test_bow"', block)
        self.assertIn('Type="Bow"', block)
        self.assertIn('<ItemComponent>', block)
        self.assertIn('thrust_speed="92"', block)
        self.assertIn('<WeaponFlags RangedWeapon="true" />', block)


# ---------------------------------------------------------------------------
# render_xslt
# ---------------------------------------------------------------------------

XSLT_BASE = textwrap.dedent("""\
    <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    \t<xsl:output omit-xml-declaration="yes"/>
    \t<xsl:template match="@*|node()">
    \t\t<xsl:copy>
    \t\t\t<xsl:apply-templates select="@*|node()"/>
    \t\t</xsl:copy>
    \t</xsl:template>
    \t<xsl:template match="CraftingTemplate[@id='OneHandedSword']/UsablePieces">
    \t\t<UsablePieces>
    \t\t\t<UsablePiece piece_id="wm_existing_blade" />
    \t\t</UsablePieces>
    \t</xsl:template>
    </xsl:stylesheet>
""")


class TestRenderXslt(unittest.TestCase):

    def test_inserts_into_existing_block(self):
        new_xslt, inserted = render_xslt.insert_usable_pieces(
            XSLT_BASE, "OneHandedSword", ["wm_new_one_blade", "wm_new_two_blade"]
        )
        self.assertEqual(inserted.inserted, ["wm_new_one_blade", "wm_new_two_blade"])
        self.assertIn('<UsablePiece piece_id="wm_new_one_blade" />', new_xslt)
        self.assertIn('<UsablePiece piece_id="wm_new_two_blade" />', new_xslt)
        # Existing entry preserved.
        self.assertIn('<UsablePiece piece_id="wm_existing_blade" />', new_xslt)

    def test_skip_already_present(self):
        new_xslt, inserted = render_xslt.insert_usable_pieces(
            XSLT_BASE, "OneHandedSword", ["wm_existing_blade"]
        )
        self.assertEqual(inserted.inserted, [])
        self.assertEqual(new_xslt, XSLT_BASE)

    def test_synthesizes_when_template_missing(self):
        no_match = XSLT_BASE.replace("OneHandedSword", "TwoHandedSword")
        new_xslt, inserted = render_xslt.insert_usable_pieces(
            no_match, "TwoHandedAxe", ["wm_axe_blade"]
        )
        self.assertEqual(inserted.inserted, ["wm_axe_blade"])
        self.assertIn('match="CraftingTemplate[@id=\'TwoHandedAxe\']/UsablePieces"', new_xslt)
        self.assertIn('wm_axe_blade', new_xslt)


# ---------------------------------------------------------------------------
# pipeline (end-to-end with in-memory fixtures)
# ---------------------------------------------------------------------------

PIECES_FIXTURE = textwrap.dedent("""\
    <?xml version="1.0" encoding="utf-8"?>
    <CraftingPieces>
    </CraftingPieces>
""")

ITEMS_FIXTURE = textwrap.dedent("""\
    <?xml version="1.0" encoding="utf-8"?>
    <Items>
    </Items>
""")


class TestPipelineEndToEnd(unittest.TestCase):

    def setUp(self):
        import tempfile
        self.tmp = tempfile.mkdtemp()
        self.pieces_path = os.path.join(self.tmp, "pieces.xml")
        self.items_path  = os.path.join(self.tmp, "items.xml")
        self.ct_path     = os.path.join(self.tmp, "ct.xslt")
        self.wd_path     = os.path.join(self.tmp, "wd.xslt")
        for path, content in [
            (self.pieces_path, PIECES_FIXTURE),
            (self.items_path,  ITEMS_FIXTURE),
            (self.ct_path,     XSLT_BASE),
            (self.wd_path,     XSLT_BASE.replace("CraftingTemplate", "WeaponDescription")
                                        .replace("UsablePieces", "AvailablePieces")
                                        .replace("UsablePiece", "AvailablePiece")
                                        .replace("piece_id=", "id=")),
        ]:
            with open(path, "w", encoding="utf-8") as f:
                f.write(content)
        self.manifest_path = os.path.join(self.tmp, "m.xml")
        with open(self.manifest_path, "w", encoding="utf-8") as f:
            f.write(CRAFTED_MANIFEST)

    def tearDown(self):
        import shutil
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_dry_run_emits_all_four_files(self):
        m = manifest.parse_manifest(self.manifest_path)
        result = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        for k in ("crafting_pieces_xml", "items_xml", "crafting_templates_xslt", "weapon_descriptions_xslt"):
            self.assertIn(k, result.deltas)
            self.assertTrue(result.deltas[k].changed, f"expected change in {k}")
        self.assertEqual(set(result.new_piece_ids), {
            "wm_test_sword_a_blade",
            "wm_test_sword_a_guard",
            "wm_test_sword_a_hilt",
            "wm_test_sword_a_pommel",
        })
        self.assertEqual(result.new_crafted_ids, ["wm_test_sword_a"])

    def test_idempotency_second_run(self):
        m = manifest.parse_manifest(self.manifest_path)
        result1 = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        # Apply.
        for delta in result1.deltas.values():
            with open(delta.path, "w", encoding="utf-8") as f:
                f.write(delta.after_text)
        # Re-run.
        result2 = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        for k, delta in result2.deltas.items():
            self.assertFalse(delta.changed, f"{k} changed on second run (not idempotent)")
        self.assertEqual(result2.new_piece_ids, [])
        self.assertEqual(result2.new_crafted_ids, [])

    def test_xslt_self_heal_after_partial_first_run(self):
        """Regression test for deep-review #1, gap 2.

        Simulates a previous run that wrote crafting_pieces.xml + items.xml but crashed
        before updating the XSLTs. Re-running the same manifest should still register
        the now-orphaned pieces in both XSLTs (catching them up), even though the
        pieces themselves are skipped from crafting_pieces.xml as already-present.
        """
        m = manifest.parse_manifest(self.manifest_path)
        # First, do a clean run end-to-end.
        result = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        # Apply ONLY the pieces and items XML files. Leave the two XSLTs as their
        # original (no entries for the new pieces) — simulating a partial-failure state.
        for k in ("crafting_pieces_xml", "items_xml"):
            with open(result.deltas[k].path, "w", encoding="utf-8") as f:
                f.write(result.deltas[k].after_text)
        # XSLTs untouched on disk.

        # Re-run the pipeline.
        result2 = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        # Pieces and items XML should now be no-op (already on disk).
        self.assertFalse(result2.deltas["crafting_pieces_xml"].changed,
                         "crafting_pieces.xml should be unchanged on second run")
        self.assertFalse(result2.deltas["items_xml"].changed,
                         "items.xml should be unchanged on second run")
        # XSLTs should now contain the pieces (catch-up insertion).
        self.assertTrue(result2.deltas["crafting_templates_xslt"].changed,
                        "crafting_templates.xslt should self-heal on second run")
        self.assertTrue(result2.deltas["weapon_descriptions_xslt"].changed,
                        "weapon_descriptions.xslt should self-heal on second run")
        for piece_id in (
            "wm_test_sword_a_blade", "wm_test_sword_a_guard",
            "wm_test_sword_a_hilt",  "wm_test_sword_a_pommel",
        ):
            self.assertIn(f'piece_id="{piece_id}"', result2.deltas["crafting_templates_xslt"].after_text)
            self.assertIn(f'id="{piece_id}"',       result2.deltas["weapon_descriptions_xslt"].after_text)

    def test_culture_resolved_from_prefix_when_absent(self):
        """Regression test for deep-review #1, gap 6/7. Manifest with no culture=
        attribute should fall back to prefix detection."""
        no_culture_manifest = textwrap.dedent("""\
            <WeaponManifest>
              <CraftedWeapon id="wm_witch_king_test_sword"
                             name="{=k}Test"
                             weapon_class="OneHandedSword"
                             tier="3">
                <Blade length="100" weight="0.9">
                  <Thrust damage_type="Pierce" damage_factor="3.5" />
                  <Swing  damage_type="Cut"    damage_factor="3.5" />
                </Blade>
                <Guard length="3" weight="0.15" />
                <Hilt  length="11" weight="0.2" />
                <Pommel length="3" weight="0.1" />
              </CraftedWeapon>
            </WeaponManifest>
        """)
        path = os.path.join(self.tmp, "no_culture.xml")
        with open(path, "w", encoding="utf-8") as f:
            f.write(no_culture_manifest)
        m = manifest.parse_manifest(path)
        self.assertIsNone(m.crafted[0].culture)   # manifest had none

        result = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
            interactive_culture=False,    # no prompt; rely on prefix detection
        )
        # wm_witch_king_* prefix maps to mordor.
        self.assertIn('culture="Culture.mordor"', result.deltas["items_xml"].after_text)

    def test_body_name_strips_sm_prefix_for_blade(self):
        """Regression test for deep-review #1, gap 10. sm_-prefixed weapons must not
        produce body_name="bo_sm_..." — the sm_ is dropped in the Armory convention."""
        sm_manifest = textwrap.dedent("""\
            <WeaponManifest>
              <CraftedWeapon id="sm_uruk_test_halberd"
                             name="{=k}Test"
                             weapon_class="TwoHandedPolearm"
                             culture="isengard"
                             tier="3">
                <Blade length="100" weight="0.9">
                  <Thrust damage_type="Pierce" damage_factor="3.5" />
                  <Swing  damage_type="Cut"    damage_factor="3.5" />
                </Blade>
                <Guard length="3" weight="0.15" />
                <Hilt  length="11" weight="0.2" />
                <Pommel length="3" weight="0.1" />
              </CraftedWeapon>
            </WeaponManifest>
        """)
        path = os.path.join(self.tmp, "sm.xml")
        with open(path, "w", encoding="utf-8") as f:
            f.write(sm_manifest)
        m = manifest.parse_manifest(path)
        result = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        pieces_text = result.deltas["crafting_pieces_xml"].after_text
        # Should be bo_uruk_test_halberd_blade, NOT bo_sm_uruk_test_halberd_blade.
        self.assertIn('body_name="bo_uruk_test_halberd_blade"', pieces_text)
        self.assertNotIn('body_name="bo_sm_uruk_test_halberd_blade"', pieces_text)

    def test_verify_passes(self):
        m = manifest.parse_manifest(self.manifest_path)
        result = pipeline.build_deltas(
            m,
            pieces_xml_path=self.pieces_path,
            items_xml_path=self.items_path,
            crafting_templates_xslt_path=self.ct_path,
            weapon_descriptions_xslt_path=self.wd_path,
        )
        report = verify.verify_cross_refs(
            pieces_xml=result.deltas["crafting_pieces_xml"].after_text,
            items_xml=result.deltas["items_xml"].after_text,
            crafting_templates_xslt=result.deltas["crafting_templates_xslt"].after_text,
            weapon_descriptions_xslt=result.deltas["weapon_descriptions_xslt"].after_text,
            new_piece_ids=result.new_piece_ids,
            new_crafted_ids=result.new_crafted_ids,
            new_single_ids=result.new_single_ids,
            weapon_class_by_piece_id=result.weapon_class_by_piece_id,
        )
        self.assertEqual(report.errors, [])


if __name__ == "__main__":
    unittest.main()
