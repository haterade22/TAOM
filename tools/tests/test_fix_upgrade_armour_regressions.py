#!/usr/bin/env python3
"""Unit tests for tools/fix_upgrade_armour_regressions.py, the armour half of the ladder rule.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_fix_upgrade_armour_regressions.py

Synthetic data only, no game install needed: the item table is handed to the planner directly
and the troop files live in a temporary ModuleData.

THE CONTRACT
------------
Upgrading a troop must never lower its armour total (battle-set average over Head, Body, Cape,
Gloves and Leg, an unfilled slot counting as 0). The clamp steps the target up its own item
family, falls back to the source's item, appends a slot the target never fills, exempts
militia-to-militia edges and the bare-chested-by-design troops' Body and Cape, demotes hero kit
on low troops before judging, and is idempotent.
"""
import os
import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import fix_upgrade_armour_regressions as fx  # noqa: E402
import rebalance_troops as rb  # noqa: E402


def _npc(tid, level, upgrades=(), sets=()):
    ups = "".join(f'<upgrade_target id="NPCCharacter.{u}" />' for u in upgrades)
    rosters = ""
    for st in sets:
        civ = ' civilian="true"' if st.get("_civilian") else ""
        eqs = "".join(f'\n                <equipment slot="{s}" id="Item.{i}" />'
                      for s, i in st.items() if s != "_civilian")
        rosters += f"\n            <EquipmentRoster{civ}>{eqs}\n            </EquipmentRoster>"
    return (f'\n    <NPCCharacter id="{tid}" level="{level}" default_group="Infantry">'
            f'\n        <upgrade_targets>{ups}</upgrade_targets>'
            f'\n        <Equipments>{rosters}\n        </Equipments>'
            f'\n    </NPCCharacter>')


def _item(iid, folder, slot_file, values):
    return {"value": values, "folder": folder, "file": slot_file}


class ClampTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        (self.md / "characters").mkdir(parents=True)
        # A culture binds no militia here; the militia test writes its own binding.
        (self.md / "taom_spcultures.xml").write_text(
            '<SPCultures><Culture id="c" militia_troop="NPCCharacter.mil_a" '
            'melee_militia_troop="NPCCharacter.mil_b" /></SPCultures>', encoding="utf-8")
        # The militia loader fails closed unless BOTH binding files exist.
        (self.md / "spcultures.xslt").write_text(
            '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0" />',
            encoding="utf-8")
        rb._militia_ids_cache.clear()
        self.items = {
            "helm_light_a": _item("helm_light_a", "cx", "head_armors.xml", 10),
            "helm_heavy_a": _item("helm_heavy_a", "cx", "head_armors.xml", 30),
            "helm_elite_a": _item("helm_elite_a", "cx", "head_armors.xml", 40),
            "other_helm_a": _item("other_helm_a", "cx", "head_armors.xml", 35),
            "chest_light_a": _item("chest_light_a", "cx", "body_armors.xml", 20),
            "chest_heavy_a": _item("chest_heavy_a", "cx", "body_armors.xml", 50),
            "glove_a": _item("glove_a", "cx", "arm_armors.xml", 12),
            "lord_torso": _item("lord_torso", "cx", "body_armors.xml", 175),
        }

    def tearDown(self):
        rb._militia_ids_cache.clear()
        self._tmp.cleanup()

    def _write_troops(self, *npcs):
        (self.md / "troops" / "troops_cx.xml").write_text(
            '<?xml version="1.0" encoding="utf-8"?>\n<NPCCharacters>' + "".join(npcs)
            + "\n</NPCCharacters>\n", encoding="utf-8")

    def _plan(self):
        troops = fx.load_troops(str(self.md))
        militia = rb.militia_troop_ids(str(self.md))
        before = fx.find_regressions(troops, self.items, militia)
        changes = fx.plan_fixes(troops, self.items, militia)
        after = fx.find_regressions(troops, self.items, militia)
        return before, changes, after

    def test_family_step_up_is_preferred_over_the_parents_item(self):
        self._write_troops(
            _npc("parent", 16, ["child"], [{"Head": "other_helm_a"}]),
            _npc("child", 21, [], [{"Head": "helm_light_a"}]))
        before, changes, after = self._plan()
        self.assertEqual(len(before), 1)
        self.assertEqual([(c["slot"], c["old"], c["new"], c["how"]) for c in changes],
                         [("Head", "helm_light_a", "helm_elite_a", "family")])
        self.assertEqual(after, [])

    def test_parent_item_is_the_fallback_when_the_family_has_nothing_high_enough(self):
        self.items["helm_light_b"] = _item("helm_light_b", "cx", "head_armors.xml", 12)
        self._write_troops(
            _npc("parent", 16, ["child"], [{"Head": "other_helm_a"}]),
            _npc("child", 21, [], [{"Head": "helm_light_b"}]))
        del self.items["helm_heavy_a"], self.items["helm_elite_a"]
        _, changes, after = self._plan()
        self.assertEqual([(c["new"], c["how"]) for c in changes], [("other_helm_a", "parent")])
        self.assertEqual(after, [])

    def test_a_slot_the_child_never_fills_is_appended_in_every_set(self):
        self._write_troops(
            _npc("parent", 16, ["child"], [{"Head": "helm_heavy_a", "Gloves": "glove_a"}]),
            _npc("child", 21, [], [{"Head": "helm_heavy_a"}, {"Head": "helm_elite_a"}]))
        _, changes, after = self._plan()
        # One record per (slot, old item): the same replacement lands in every set.
        self.assertEqual([(c["slot"], c["old"], c["new"]) for c in changes],
                         [("Gloves", None, "glove_a")])
        self.assertEqual(after, [])

    def test_a_clean_edge_and_a_flat_edge_are_left_alone(self):
        self._write_troops(
            _npc("parent", 16, ["same", "better"], [{"Head": "helm_heavy_a"}]),
            _npc("same", 21, [], [{"Head": "helm_heavy_a"}]),
            _npc("better", 21, [], [{"Head": "helm_elite_a"}]))
        before, changes, _ = self._plan()
        self.assertEqual(before, [])
        self.assertEqual(changes, [])

    def test_militia_to_militia_is_exempt(self):
        self._write_troops(
            _npc("mil_a", 6, ["mil_b"], [{"Head": "helm_heavy_a"}]),
            _npc("mil_b", 16, [], [{"Head": "helm_light_a"}]))
        before, changes, _ = self._plan()
        self.assertEqual(before, [])
        self.assertEqual(changes, [])

    def test_bare_chested_by_design_is_judged_without_body_and_cape(self):
        bodyless = sorted(fx.BODYLESS_BY_DESIGN)[0]
        self._write_troops(
            _npc("parent", 21, [bodyless], [{"Head": "helm_heavy_a", "Body": "chest_heavy_a"}]),
            _npc(bodyless, 26, [], [{"Head": "helm_heavy_a"}]))
        before, changes, _ = self._plan()
        self.assertEqual(before, [])
        self.assertEqual(changes, [])

    def test_civilian_sets_do_not_count(self):
        self._write_troops(
            _npc("parent", 16, ["child"], [{"Head": "helm_light_a"},
                                            {"Head": "helm_elite_a", "_civilian": True}]),
            _npc("child", 21, [], [{"Head": "helm_light_a"}]))
        before, _, _ = self._plan()
        self.assertEqual(before, [])

    def test_hero_kit_is_demoted_on_the_parent_before_the_edge_is_judged(self):
        saved = fx.DEMOTE
        fx.DEMOTE = ((re.compile(r"^lord_"), 46),)
        try:
            self._write_troops(
                _npc("parent", 21, ["child"], [{"Body": "lord_torso"}, {"Body": "chest_light_a"},
                                               {"Body": "chest_heavy_a"}]),
                _npc("child", 26, [], [{"Body": "chest_heavy_a"}]))
            _, changes, after = self._plan()
        finally:
            fx.DEMOTE = saved
        self.assertEqual([(c["troop"], c["old"], c["new"], c["how"]) for c in changes],
                         [("parent", "lord_torso", "chest_heavy_a", "demote")])
        self.assertEqual(after, [])

    def test_a_raise_propagates_down_a_chain(self):
        self._write_troops(
            _npc("a", 16, ["b"], [{"Head": "other_helm_a"}]),
            _npc("b", 21, ["c"], [{"Head": "helm_light_a"}]),
            _npc("c", 26, [], [{"Head": "helm_light_a"}]))
        _, changes, after = self._plan()
        self.assertEqual([c["troop"] for c in changes], ["b", "c"])
        self.assertEqual(after, [])

    def test_write_is_byte_faithful_and_idempotent(self):
        self._write_troops(
            _npc("parent", 16, ["child"], [{"Head": "helm_heavy_a", "Gloves": "glove_a"}]),
            _npc("child", 21, [], [{"Head": "helm_light_a"}]))
        path = self.md / "troops" / "troops_cx.xml"
        raw = path.read_bytes().replace(b"\n", b"\r\n")
        path.write_bytes(b"\xef\xbb\xbf" + raw)
        troops = fx.load_troops(str(self.md))
        changes = fx.plan_fixes(troops, self.items, set())
        self.assertEqual(fx.write_changes(changes), 1)
        out = path.read_bytes()
        self.assertTrue(out.startswith(b"\xef\xbb\xbf"))
        self.assertIn(b"\r\n", out)
        self.assertNotIn(b"\n\n", out.replace(b"\r\n", b"\n").replace(b"\n\n", b"\n\n"))
        text = out.decode("utf-8-sig")
        self.assertIn('slot="Head" id="Item.helm_heavy_a"', text)
        self.assertIn('<equipment slot="Gloves" id="Item.glove_a" />', text)
        # Second pass finds nothing to do.
        troops = fx.load_troops(str(self.md))
        self.assertEqual(fx.find_regressions(troops, self.items, set()), [])
        self.assertEqual(fx.plan_fixes(troops, self.items, set()), [])

    def test_a_self_closing_set_in_the_block_is_left_alone_and_does_not_swallow_the_next(self):
        # Review 2026-09-04: without the /> alternation a self-closing civilian-template reference
        # matched on its own ">" and ran forward to an unrelated close tag.
        block = ('<NPCCharacter id="x"><Equipments>'
                 '<EquipmentSet id="civ_template" equipmentType="Civilian" />'
                 '<EquipmentRoster><equipment slot="Head" id="Item.helm_light_a" />'
                 '</EquipmentRoster></Equipments></NPCCharacter>')
        out = fx._rewrite_block(block, {("Head", "helm_light_a"): "helm_heavy_a"})
        self.assertIn('<EquipmentSet id="civ_template" equipmentType="Civilian" />', out)
        self.assertIn('id="Item.helm_heavy_a"', out)
        self.assertNotIn("helm_light_a", out)

    def test_bodyless_fallback_matches_the_validators_allowlist(self):
        import taom_schema as ts
        self.assertEqual(fx.BODYLESS_BY_DESIGN, frozenset(ts.Validator._BODYLESS_BY_DESIGN))

    def test_family_strips_tier_and_variant_tokens(self):
        # The elf vocabulary: tierN and the silver/silvergold/gold colour suffixes.
        self.assertEqual(fx.family("rivendell_helmet_archer_tier1_silver"), "rivendell_helmet_archer")
        self.assertEqual(fx.family("rivendell_helmet_cavalry_tier3_silvergold"), "rivendell_helmet_cavalry")
        # "light" is itself a tier token, so the plain torsos (light_light / heavy) share one family.
        self.assertEqual(fx.family("rivendell_torso_light_light_tier4"), "rivendell_torso")
        self.assertEqual(fx.family("sk_rh_drag_plate_light_a"), "sk_rh_drag_plate")
        self.assertEqual(fx.family("sk_rh_drag_plate_elite_c"), "sk_rh_drag_plate")
        self.assertEqual(fx.family("sk_dale_helmet_archer_a03"), "sk_dale_helmet_archer")
        self.assertEqual(fx.family("sk_dg_uruk_helmet_med_a"), "sk_dg_uruk_helmet")
        self.assertNotEqual(fx.family("sk_dg_khml_hood_med_a"), fx.family("sk_dg_khml_helmet_inf_med_c"))


if __name__ == "__main__":
    unittest.main()
