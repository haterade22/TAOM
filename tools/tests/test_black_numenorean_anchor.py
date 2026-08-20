"""The Black Numenorean armour set anchors its stats to the wearer's level.

`generate_black_numenorean_armor.ANCHOR_LEVEL` maps each item to the level of the
lowest troop that wears it, and `apply_black_numenorean_troops.TROOPS` decides who
actually wears what. They live in two files, so they can drift: a roster edit that
moves a piece down a tier silently leaves the item statted for the tier above.

These tests recompute the anchor from the rosters and compare. They are the reason
the anchor map is safe to hand-maintain.
"""
import os
import sys
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

import generate_black_numenorean_armor as gen   # noqa: E402
import apply_black_numenorean_troops as troops  # noqa: E402

ARMOUR_SLOTS = {"Head", "Body", "Cape", "Gloves", "Leg"}


def _anchor_from_rosters():
    """{item id: lowest level of any troop wearing it}, from the roster table."""
    out = {}
    for tid, level, _group, _disp, _up, _skills, rosters in troops.TROOPS:
        for roster in rosters:
            for slot, item in roster:
                if slot not in ARMOUR_SLOTS:
                    continue
                if not item.startswith(("sk_md_num_", "sm_md_num_")):
                    continue
                if item not in out or level < out[item]:
                    out[item] = level
    return out


class AnchorMatchesRostersTests(unittest.TestCase):

    def test_every_worn_item_is_anchored_to_its_lowest_wearer(self):
        actual = _anchor_from_rosters()
        wrong = {i: (gen.ANCHOR_LEVEL.get(i), lv)
                 for i, lv in actual.items() if gen.ANCHOR_LEVEL.get(i) != lv}
        self.assertEqual(
            {}, wrong,
            "ANCHOR_LEVEL disagrees with the rosters as {item: (anchored, actual lowest wearer)}. "
            "A piece statted for a tier above its real wearer is exactly the defect the "
            "wearer-level anchoring exists to prevent.")

    def test_no_worn_item_is_listed_as_unworn(self):
        actual = _anchor_from_rosters()
        both = sorted(set(actual) & set(gen.UNWORN_ROW))
        self.assertEqual([], both,
                         "these items are in UNWORN_ROW but a troop wears them: %s" % both)

    def test_every_authored_item_is_classified(self):
        """Each of the 78 items needs an anchor or an explicit unworn row."""
        missing = []
        for _slot, (items, _fname) in gen.SLOT_MAP.items():
            for it in items:
                if it.id not in gen.ANCHOR_LEVEL and it.id not in gen.UNWORN_ROW:
                    missing.append(it.id)
        self.assertEqual([], sorted(missing),
                         "authored but classified in neither ANCHOR_LEVEL nor UNWORN_ROW")

    def test_anchor_map_has_no_items_that_do_not_exist(self):
        authored = {it.id for _s, (items, _f) in gen.SLOT_MAP.items() for it in items}
        stale = sorted((set(gen.ANCHOR_LEVEL) | set(gen.UNWORN_ROW)) - authored)
        self.assertEqual([], stale,
                         "anchor/unworn map names items the generator does not author: %s" % stale)


class LadderTests(unittest.TestCase):
    """The ladder must climb. A tier that grants no extra armour is a dead upgrade
    edge, which is the defect the 2026-08-17 Codex pass caught."""

    BRANCHES = {
        "cavalry": ["mordor_num_initiate", "mordor_num_cavalry", "mordor_num_vet_cavalry",
                    "mordor_num_knight", "mordor_num_temple_knight"],
        "infantry": ["mordor_num_initiate", "mordor_num_infantry", "mordor_num_vet_infantry",
                     "mordor_num_warden", "mordor_num_temple_guard"],
        "archer": ["mordor_num_initiate", "mordor_num_archer", "mordor_num_vet_archer",
                   "mordor_num_marksman", "mordor_num_shadowbow"],
    }

    def _totals(self):
        """Best-roster armour total per troop, computed from the generator's own stats
        so this test needs no game install."""
        import rebalance_armor as ra
        stat_key = {"head": "head_armor", "body": "body_armor",
                    "shoulder": "body_armor", "arm": "arm_armor", "leg": "leg_armor"}
        by_id = {it.id: it for _s, (items, _f) in gen.SLOT_MAP.items() for it in items}

        def value(item_id):
            it = by_id[item_id]
            s = ra.calculate_stats(it.tier, it.slot, gen.CULTURE)
            v = s[stat_key[it.slot]]
            if it.slot == "shoulder":
                v += s["arm_armor"]
            if it.slot == "body" and it.arm_armor_stat:
                v += it.arm_armor_stat
            return v

        out = {}
        for tid, _lv, _g, _d, _u, _sk, rosters in troops.TROOPS:
            out[tid] = max(
                sum(value(i) for slot, i in r
                    if slot in ARMOUR_SLOTS and i.startswith(("sk_md_num_", "sm_md_num_")))
                for r in rosters)
        return out

    def test_armour_strictly_increases_along_every_branch(self):
        tot = self._totals()
        for name, chain in self.BRANCHES.items():
            vals = [tot[t] for t in chain]
            for lo, hi, a, b in zip(chain, chain[1:], vals, vals[1:]):
                self.assertGreater(
                    b, a, f"{name}: {hi} ({b}) does not improve on {lo} ({a}). "
                          f"An upgrade that costs resources and grants no survivability "
                          f"is a dead edge.")


if __name__ == "__main__":
    unittest.main()
