"""No Long Defeat shop upgrade lowers the troop's main weapon skill (#343).

Run:  python -m unittest tools.tests.test_long_defeat_shop_skills

The Long Defeat shop (Main/_Module/ModuleData/long_defeat/shop.json, read only here: the feature
belongs to its own work) upgrades Fords of War copies (fow_*) into campaign troops and back. Those
edges are not in any troop file, so the upgrade clamp of tools/rebalance_troops.py never reads
them, and must not: a clamp may never raise a campaign troop for a shop edge. This test reads them
instead. For each edge, the source's top skill over the weapon classes it carries must not be over
the target's value in the target's main class (troop_purpose.main_class). The kits come from the
install, so the test skips without it.
"""
import json
import os
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))

import ranged_ladder as rl     # noqa: E402
import rebalance_troops as rb  # noqa: E402
import troop_purpose as tp     # noqa: E402

SHOP = Path(rb.MODULEDATA_DIR) / "long_defeat" / "shop.json"


class LongDefeatShopSkillTests(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        if not os.path.isdir(rb.DEFAULT_GAME_MODULES):
            raise unittest.SkipTest("game install not found: %s" % rb.DEFAULT_GAME_MODULES)
        import taom_schema as ts
        cls.classes = ts.build_item_class_registry(rb.MODULEDATA_DIR, rb.DEFAULT_GAME_MODULES)
        cls.npcs = {}
        paths = sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml"))
        paths += [Path(rb.MODULEDATA_DIR, *rel.split("/")) for rel in tp.EXTRA_TROOP_FILES]
        for path in paths:
            for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
                cls.npcs[npc.get("id")] = npc
        cls.shops = json.loads(SHOP.read_text(encoding="utf-8-sig"))["shops"]

    def _troop(self, troop_id):
        npc = self.npcs[troop_id]
        kit, _ = rb.troop_kit(npc, self.classes)
        skills = {s.get("id"): int(s.get("value", "0")) for s in npc.findall("./skills/skill")}
        return npc, kit, skills

    def edges(self):
        return [(shop, u["from"], u["to"]) for shop, body in sorted(self.shops.items())
                for u in body.get("upgrades", [])]

    def test_the_shop_has_upgrade_edges_and_every_troop_exists(self):
        edges = self.edges()
        self.assertTrue(edges)
        missing = sorted({t for _, a, b in edges for t in (a, b) if t not in self.npcs})
        self.assertEqual(missing, [])

    def test_no_shop_upgrade_lowers_the_main_weapon_skill(self):
        lower = []
        for shop, source, target in self.edges():
            _, skit, sskills = self._troop(source)
            tnpc, tkit, tskills = self._troop(target)
            carried = [c for c in tp.WEAPON_CLASSES if skit.counts.get(c)]
            top_class = max(carried, key=lambda c: sskills.get(c, 0))
            main = tp.main_class(tkit, tskills)
            if main is None or tskills.get(main, 0) < sskills.get(top_class, 0):
                lower.append(
                    f"{shop}: {source} (T{rl.engine_tier(int(self.npcs[source].get('level')))}, "
                    f"{top_class} {sskills.get(top_class, 0)}) -> {target} "
                    f"(T{rl.engine_tier(int(tnpc.get('level')))}, {main} {tskills.get(main, 0)})")
        self.assertEqual(lower, [], f"{len(lower)} shop upgrade(s) lower the main weapon skill")


if __name__ == "__main__":
    unittest.main()
