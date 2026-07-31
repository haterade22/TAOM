#!/usr/bin/env python3
"""Unit tests for the armor curve's two-tier overtake invariant.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_armor_curve_invariant.py

Synthetic data only (no game install required, except the one opt-in test that
cross-checks the shipped native modifier ladder and skips when absent).

THE CONTRACT
------------
Bannerlord item modifiers are a FLAT additive armor bonus, applied independently
to every nonzero armor stat (`EquipmentElement.GetModifiedBodyArmor()` /
`GetModifiedArmArmor()`, each guarded by `num > 0`). A `legendary_plate` roll is
+12 on body AND +12 on arm. TAOM's shoulder curve used to span only 16 points
across all six tiers, so one legendary roll could leap three tiers.

Observed 2026-07-31: `Legendary [Gondor] Anorien Infantry Pauldron I` (tier 2,
base 9/9 + legendary_plate) displayed 21/21 and beat the plain tier-6
`[Arnor] Noble Elite Pauldrons` at 20/14.

The invariant these tests pin: a legendary roll may beat the ADJACENT tier
(desirable loot excitement) but never an item TWO OR MORE tiers above it.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import analyze_armor_balance as ab  # noqa: E402
import rebalance_armor as ra        # noqa: E402


class CurveInvariantTests(unittest.TestCase):
    """The shipped curve constants must satisfy the two-tier invariant."""

    def test_no_two_tier_overtake_in_shipped_curve(self):
        """THE contract. For every slot, governed stat, tier pair (n, n-2) and
        cultural protection value: base[n] must exceed the worst-case legendary
        roll of tier n-2. Regression: the Gondor pauldron above."""
        violations = ab.check_curve_invariant()
        self.assertEqual(
            [], violations,
            "curve invariant violated:\n  " + "\n  ".join(
                f"{v['slot']}.{v['stat']} {v['lo_tier']}->{v['hi_tier']} "
                f"(culture protection {v['protection']:+d}): "
                f"hi={v['hi']} <= lo={v['lo']} + legendary {v['lego']}"
                for v in violations))

    def test_baselines_are_strictly_monotone(self):
        """Sufficiency of checking only the n-2 pair depends on monotone
        baselines — otherwise a wider gap could fail while n-2 passes."""
        for slot, rows in ra.SLOT_BASELINES.items():
            for stat in rows['lord']:
                if stat == 'weight':
                    continue
                vals = [rows[t][stat] for t in ra.TIERS]
                self.assertEqual(vals, sorted(vals),
                                 f"{slot}.{stat} baseline is not monotone: {vals}")

    def test_modifier_ladder_is_monotone_per_slot(self):
        """The legendary bonus must never INCREASE as tier decreases. Same
        sufficiency argument as above, applied to the modifier ladder."""
        for slot in ra.SLOT_BASELINES:
            legos = [ra.LEGENDARY_ARMOR[ra.modifier_group_for(slot, t)] for t in ra.TIERS]
            self.assertEqual(legos, sorted(legos),
                             f"{slot} modifier ladder is not monotone: {legos}")

    def test_every_mapped_modifier_group_is_known(self):
        """A typo'd group would silently score 0 headroom and fake a pass."""
        for slot in ra.SLOT_BASELINES:
            for tier in ra.TIERS:
                self.assertIn(ra.modifier_group_for(slot, tier), ra.LEGENDARY_ARMOR,
                              f"{slot}/{tier} maps to an unknown modifier group")


class ShoulderTaperTests(unittest.TestCase):
    """The cape taper relies on a stat of exactly 0 staying 0."""

    def test_civilian_shoulder_arm_is_zero(self):
        """0 is load-bearing: GetModifiedArmArmor() guards on `num > 0`, so a 0
        base is modifier-immune and drops out of the ladder entirely. A nonzero
        civilian arm re-arms the ladder AND trips the max(1, ...) clamp for
        negative-protection cultures (harad/thenn at -3)."""
        self.assertEqual(0, ra.SHOULDER_BASELINES['civilian']['arm_armor'])

    def test_calculate_stats_preserves_explicit_zero(self):
        """max(1, ...) in calculate_stats must not resurrect a 0 baseline."""
        for culture in ('gondor', 'harad', 'iron_hills', 'mercenary'):
            self.assertEqual(
                0, ra.calculate_stats('civilian', 'shoulder', culture)['arm_armor'],
                f"{culture}: civilian shoulder arm_armor was clamped up from 0")

    def test_nonzero_stats_still_floor_at_one(self):
        """The clamp must survive for genuinely nonzero baselines — harad (-3)
        civilian body is 2-3 = -1 and must land on 1, not 0 or -1."""
        self.assertEqual(1, ra.calculate_stats('civilian', 'shoulder', 'harad')['body_armor'])


class VariantCapTests(unittest.TestCase):
    """Roman-numeral variants add straight into the stat and must be capped."""

    def test_variant_bonus_is_capped(self):
        """extract_variant_number() maps I..XVIII to +0..+17. Uncapped, a
        `Pauldron VI` sits 5 points above its own baseline and silently eats
        the invariant margin (which runs as thin as +1)."""
        lo = ra.calculate_stats('light', 'shoulder', 'gondor', variant_num=0)['body_armor']
        hi = ra.calculate_stats('light', 'shoulder', 'gondor', variant_num=17)['body_armor']
        self.assertLessEqual(hi - lo, ra.VARIANT_CAP,
                             "variant bonus exceeds VARIANT_CAP — invariant margins are unprovable")

    def test_variant_cap_is_what_the_invariant_assumes(self):
        """check_curve_invariant() budgets exactly VARIANT_CAP of headroom."""
        self.assertEqual([], ab.check_curve_invariant(variant_cap=ra.VARIANT_CAP))


class GovernedStatsTests(unittest.TestCase):
    """Shoulder arm_armor was invisible to every analyzer — that blind spot is
    why the defect shipped."""

    def test_shoulder_governs_both_stats(self):
        self.assertEqual(['body_armor', 'arm_armor'], ra.governed_stats('shoulder'))

    def test_primary_stat_is_unchanged_for_every_slot(self):
        """_get_primary_stat has 14 call sites relying on a scalar; the
        GOVERNED_STATS refactor must not change what it returns."""
        expected = {'head': 'head_armor', 'body': 'body_armor', 'arm': 'arm_armor',
                    'leg': 'leg_armor', 'shoulder': 'body_armor'}
        for slot, stat in expected.items():
            values = {stat: 42}
            self.assertEqual(42, ra._get_primary_stat(values, slot),
                             f"{slot} primary stat changed — call sites will break")

    def test_unknown_slot_is_empty_not_crash(self):
        self.assertEqual([], ra.governed_stats('nonexistent'))
        self.assertIsNone(ra._get_primary_stat({}, 'nonexistent'))


class GeneratorCurveSyncTests(unittest.TestCase):
    """The per-culture armor generators carry their OWN copy of the tier table and do not import
    the curve. Re-running one with a stale copy silently reverts the fix in the live tree, so the
    copies are pinned here. (Discovered 2026-07-31: all four still held the pre-fix shoulder row.)"""

    GENERATORS = ('generate_gondor_armor', 'generate_dale_armor',
                  'generate_mordor_armor', 'generate_erebor_armor')

    def test_generator_shoulder_tables_match_the_curve(self):
        import importlib
        for name in self.GENERATORS:
            gen = importlib.import_module(name)
            table = gen.STAT_TIERS['shoulder']
            for tier, row in table.items():
                curve = ra.SHOULDER_BASELINES[tier]
                for stat in ('body_armor', 'arm_armor', 'weight'):
                    self.assertEqual(
                        curve[stat], row[stat],
                        f"{name}.STAT_TIERS['shoulder']['{tier}']['{stat}'] = {row[stat]} "
                        f"but the curve says {curve[stat]} — a generator run would revert the fix")


class NativeLadderTests(unittest.TestCase):
    """Guard against an engine bump silently changing the armor deltas."""

    def test_legendary_table_matches_shipped_item_modifiers_xml(self):
        """LEGENDARY_ARMOR must mirror Native/ModuleData/item_modifiers.xml.
        v1.4.7: plate 12 / chain 9 / leather 7 / cloth 5 / cloth_unarmoured 3."""
        import re
        game_dir = os.environ.get('BANNERLORD_GAME_DIR') or \
            r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
        path = os.path.join(game_dir, 'Modules', 'Native', 'ModuleData', 'item_modifiers.xml')
        if not os.path.exists(path):
            self.skipTest(f"no game install at {path}")

        with open(path, encoding='utf-8-sig', errors='ignore') as fh:
            text = fh.read()
        shipped = {}
        for block in re.findall(r'<ItemModifier\b.*?/>', text, re.S):
            if 'quality="legendary"' not in block:
                continue
            group = re.search(r'modifier_group="ItemModifierGroup\.([a-z_]+)"', block)
            armor = re.search(r'\barmor="(-?\d+)"', block)
            if group and armor:
                shipped[group.group(1)] = int(armor.group(1))

        for group, bonus in ra.LEGENDARY_ARMOR.items():
            self.assertIn(group, shipped, f"{group} is no longer in item_modifiers.xml")
            self.assertEqual(bonus, shipped[group],
                             f"engine changed legendary_{group}: {shipped[group]} != {bonus}")


if __name__ == '__main__':
    unittest.main()
