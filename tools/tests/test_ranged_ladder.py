#!/usr/bin/env python3
"""Unit tests for the ranged ladder: tools/ranged_ladder.py (library),
tools/generate_ranged_ladder_items.py (the ladder_* items), tools/rebalance_ranged_ladders.py
(the roster and skill rewrite) and the RANGED_* validator gates. Issues #582 and #617.

Run:  python -m unittest discover -s tools/tests -p "test_ranged_ladder.py"

Synthetic data only, no game install needed: launchers and troops live in temporary trees.

THE CONTRACT
------------
Every line ranks on three lists (overall, damage, accuracy; 1 is best). Every tier a line lists
is a cell: a generated item ladder_<line>_<bow|xbow>_t<tier> carrying the cell's speed, damage
and accuracy, plus the Bow or Crossbow the troop carries. Two rules per class and stat: inside a
line a lower tier never beats a higher tier; at the same tier a better rank is never worse.
"""
import json
import os
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ranged_ladder as rl  # noqa: E402

BOM = b"\xef\xbb\xbf"


def _t(fn):
    return {str(t): fn(t) for t in range(0, 11)}


def _spec(**over):
    """Fixture numbers are chosen so no damage lands on a .5: anchor_rank 2, step 0.1, so rank 1
    is x1.1, rank 2 x1.0, rank 3 x0.9."""
    spec = {
        "bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
        "stats": {
            "anchor_rank": 2,
            "damage": {"Bow": _t(lambda t: 40 + 10 * t), "Crossbow": _t(lambda t: 70 + 10 * t),
                       "step": _t(lambda t: 0.1)},
            "accuracy": {"top": _t(lambda t: 80 + t), "rank_step": 2, "crossbow_bonus": 3, "max": 99},
            "speed": {"tier_base": _t(lambda t: 58 + 2 * t), "rank_step": 3},
            "skill": {"anchor": _t(lambda t: 20 * t), "step": _t(lambda t: 10), "min": 5},
        },
        "hero_ceiling": {"Bow": 90, "Crossbow": 105},
        "lines": [
            {"id": "elf", "folder": "elf", "files": ["elf", "elf_twin"],
             "ranks": {"overall": 1, "damage": 1, "accuracy": 1}, "tiers": {"Bow": [2, 5, 9, 10]},
             "donor": {"Bow": "elf_bow"}, "donor_by_band": {"Bow": {"C": "elf_bow_top"}}},
            {"id": "man_special", "folder": "man", "files": ["man"], "prefixes": ["man_sp_"],
             "ranks": {"overall": 2, "damage": 2, "accuracy": 2}, "tiers": {"Bow": [7, 8, 9]},
             "donor": {"Bow": "man_bow"}},
            {"id": "man", "folder": "man", "files": ["man"],
             "ranks": {"overall": 3, "damage": 3, "accuracy": 3},
             "tiers": {"Bow": [1, 2, 3, 4, 5, 6], "Crossbow": [4, 5]},
             "donor": {"Bow": "man_bow", "Crossbow": "man_xbow"}},
        ],
    }
    spec.update(over)
    return spec


def _launchers():
    def mk(i, c, s, usage="bow", damage=80, accuracy=90):
        return rl.Launcher(id=i, cls=c, speed=s, accuracy=accuracy, damage=damage, name=f"[Test] {i}",
                           file="weapons.xml", usage=usage)
    out = {i: mk(i, c, s) for i, c, s in (
        ("elf_bow", "Bow", 95), ("elf_bow_top", "Bow", 100), ("man_bow", "Bow", 80),
        ("man_xbow", "Crossbow", 75), ("hunting_bow", "Bow", 64), ("ladder_man_bow_t3", "Bow", 64),
    )}
    out["long_bow_item"] = mk("long_bow_item", "Bow", 85, usage="long_bow")
    out["heavy_bow"] = mk("heavy_bow", "Bow", 80, damage=120)
    out["sharp_bow"] = mk("sharp_bow", "Bow", 80, accuracy=99)
    return out


def _troop(tid, culture, level, sets, upgrades=(), group="Ranged", mounted=None, skills=None):
    if mounted is None:
        mounted = group in ("HorseArcher", "Cavalry")
    return rl.RangedTroop(id=tid, file=f"troops_{culture}.xml", culture=culture, level=level,
                          tier=rl.engine_tier(level), group=group, sets=list(sets),
                          upgrades=list(upgrades), skills=dict(skills or {}), mounted=mounted)


def _npc_xml(tid, level, sets, culture_id="man", upgrades=(), bow=100):
    rosters = ""
    for st in sets:
        civ = ' civilian="true"' if st.get("_civilian") else ""
        eqs = "".join(f'\n        <equipment slot="{s}" id="Item.{i}" />'
                      for s, i in st.items() if s != "_civilian")
        rosters += f"\n      <EquipmentRoster{civ}>{eqs}\n      </EquipmentRoster>"
    ups = ""
    if upgrades:
        ups = "\n    <upgrade_targets>" + "".join(
            f'\n      <upgrade_target id="NPCCharacter.{u}" />' for u in upgrades) + "\n    </upgrade_targets>"
    return (f'\n  <NPCCharacter id="{tid}" level="{level}" default_group="Ranged" '
            f'culture="Culture.{culture_id}" name="{{=t_{tid}}}{tid}">'
            f'\n    <skills>\n      <skill id="Bow" value="{bow}" />\n    </skills>{ups}'
            f'\n    <Equipments>{rosters}\n    </Equipments>\n  </NPCCharacter>')


# --------------------------------------------------------------------------- #
# Spec, cells                                                                   #
# --------------------------------------------------------------------------- #
class SpecTests(unittest.TestCase):
    def test_band_of_covers_every_engine_tier_once(self):
        spec = _spec()
        self.assertEqual([rl.band_of(t, spec) for t in range(0, 11)],
                         ["E", "E", "E", "R", "R", "V", "V", "X", "X", "C", "C"])

    def test_engine_tier_matches_the_validator(self):
        import taom_schema as ts
        for level in (0, 1, 5, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51, 60):
            self.assertEqual(rl.engine_tier(level), ts.Validator._troop_tier(level), level)

    def test_cells_follow_the_formulas(self):
        spec = _spec()
        self.assertEqual(rl.cell("elf", "Bow", 10, spec), rl.Cell(speed=84, damage=154, accuracy=90))
        self.assertEqual(rl.cell("elf", "Bow", 2, spec), rl.Cell(speed=68, damage=66, accuracy=82))
        self.assertEqual(rl.cell("man", "Bow", 1, spec), rl.Cell(speed=60, damage=45, accuracy=77))
        self.assertEqual(rl.cell("man", "Crossbow", 4, spec), rl.Cell(speed=66, damage=99, accuracy=83))
        self.assertEqual(rl.cell("man_special", "Bow", 7, spec), rl.Cell(speed=75, damage=110, accuracy=85))

    def test_accuracy_is_capped(self):
        spec = _spec()
        spec["stats"]["accuracy"]["top"] = _t(lambda t: 98)
        self.assertEqual(rl.cell("elf", "Bow", 5, spec).accuracy, 98)
        self.assertEqual(rl.cell("man", "Crossbow", 5, spec).accuracy, 97)      # 98 - 4 + 3
        spec["lines"][2]["ranks"]["accuracy"] = 1
        self.assertEqual(rl.cell("man", "Crossbow", 5, spec).accuracy, 99)      # 98 + 3, capped

    def test_skill_cell_follows_the_formula_and_its_floor(self):
        spec = _spec()
        self.assertEqual(rl.skill_cell("elf", 10, spec), 210)
        self.assertEqual(rl.skill_cell("man", 1, spec), 10)
        self.assertEqual(rl.skill_cell("man_special", 8, spec), 160)
        self.assertEqual(rl.skill_cell("man", 0, spec), 5)                     # 0 - 10, floored

    def test_ladder_id_shape(self):
        self.assertEqual(rl.ladder_id("man_special", "Bow", 7), "ladder_man_special_bow_t7")
        self.assertEqual(rl.ladder_id("man", "Crossbow", 4), "ladder_man_xbow_t4")

    def test_validate_spec_accepts_the_fixture(self):
        self.assertEqual(rl.validate_spec(_spec()), [])

    def test_validate_spec_rejects_missing_or_bad_ranks(self):
        spec = _spec()
        del spec["lines"][0]["ranks"]["damage"]
        spec["lines"][1]["ranks"]["accuracy"] = 0
        problems = rl.validate_spec(spec)
        self.assertTrue(any("'elf'" in p and "'damage'" in p for p in problems), problems)
        self.assertTrue(any("'man_special'" in p and "'accuracy'" in p for p in problems), problems)

    def test_validate_spec_rejects_bad_tier_lists(self):
        for bad in ([], [11], [3, 3], "3"):
            spec = _spec()
            spec["lines"][0]["tiers"]["Bow"] = bad
            self.assertTrue(any("'elf'" in p and "tier" in p for p in rl.validate_spec(spec)), bad)
        spec = _spec()
        spec["lines"][0]["tiers"]["Crossbow"] = [4]          # a class with no donor
        self.assertTrue(any("'elf'" in p and "donors" in p for p in rl.validate_spec(spec)))

    def test_validate_spec_rejects_a_curve_that_falls_with_tier(self):
        spec = _spec()
        spec["stats"]["damage"]["Bow"]["5"] = 10
        problems = rl.validate_spec(spec)
        self.assertTrue(any("'elf'" in p and "damage does not rise from tier 2" in p for p in problems), problems)

    def test_validate_spec_rejects_a_listed_tier_a_curve_does_not_cover(self):
        spec = _spec()
        del spec["stats"]["skill"]["anchor"]["9"]
        problems = rl.validate_spec(spec)
        self.assertTrue(any("tier 9" in p and "stats.skill.anchor" in p for p in problems), problems)

    def test_validate_spec_reports_a_non_numeric_value_instead_of_raising(self):
        spec = _spec()
        spec["stats"]["damage"]["Bow"]["5"] = "high"
        problems = rl.validate_spec(spec)   # must not raise
        self.assertTrue(any("stats.damage.Bow[5]" in p for p in problems), problems)

    def test_validate_spec_rejects_bad_structure(self):
        dup = _spec()
        dup["lines"].append(dict(dup["lines"][0]))
        self.assertTrue(any("duplicate" in p for p in rl.validate_spec(dup)))
        gap = _spec(bands={"E": [0, 2], "R": [4, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]})
        self.assertTrue(any("tier 3" in p for p in rl.validate_spec(gap)))
        zero = _spec()
        zero["stats"]["speed"]["rank_step"] = 0
        self.assertTrue(any("rank_step" in p for p in rl.validate_spec(zero)))
        noclass = _spec()
        noclass["lines"][2]["donor"] = {"Sling": "x"}
        self.assertTrue(any("Sling" in p for p in rl.validate_spec(noclass)))
        badrow = _spec(donor_stats={"b": {"damage": 0}}, ammo_stats={"a": -1})
        problems = rl.validate_spec(badrow)
        self.assertTrue(any("donor_stats" in p for p in problems) and any("ammo_stats" in p for p in problems))
        badcap = _spec(hero_ceiling={"Bow": "x"})
        self.assertTrue(any("hero_ceiling" in p for p in rl.validate_spec(badcap)))

    def test_a_whole_file_line_may_precede_its_prefix_lines(self):
        spec = _spec()
        spec["lines"] = [spec["lines"][2], spec["lines"][1], spec["lines"][0]]   # man before man_special
        self.assertEqual(rl.validate_spec(spec), [])
        self.assertEqual(rl.line_of(_troop("man_sp_ranger", "man", 36, []), spec), "man_special")
        two_whole = _spec()
        two_whole["lines"].append({"id": "man2", "folder": "man", "files": ["man"],
                                   "ranks": {"overall": 3, "damage": 3, "accuracy": 3},
                                   "tiers": {"Bow": [4]}, "donor": {"Bow": "man_bow"}})
        self.assertTrue(any("claimed whole" in p for p in rl.validate_spec(two_whole)))

    def test_validate_spec_reports_prefixes_that_overlap_across_lines(self):
        spec = _spec()
        spec["lines"].insert(0, {"id": "man_sp_elite", "folder": "man", "files": ["man"],
                                 "prefixes": ["man_sp_ranger_"], "ranks": {"overall": 1, "damage": 1, "accuracy": 1},
                                 "tiers": {"Bow": [9]}, "donor": {"Bow": "man_bow"}})
        problems = rl.validate_spec(spec)
        self.assertTrue(any("man_sp_ranger_" in p and "man_sp_" in p and "overlap" in p for p in problems))

    def test_validate_spec_checks_files_tokens_against_the_troop_files_present(self):
        spec = _spec()
        self.assertEqual(rl.validate_spec(spec, cultures={"elf", "elf_twin", "man"}), [])
        problems = rl.validate_spec(spec, cultures={"elf", "man"})
        self.assertTrue(any("elf_twin" in p and "troops_elf_twin.xml" in p for p in problems))

    def test_validate_spec_checks_donors_against_the_index(self):
        spec = _spec()
        spec["lines"][0]["donor"]["Bow"] = "no_such_bow"
        self.assertTrue(any("no_such_bow" in p for p in rl.validate_spec(spec, launchers=_launchers())))
        wrong_class = _spec()
        wrong_class["lines"][2]["donor"]["Crossbow"] = "man_bow"
        problems = rl.validate_spec(wrong_class, launchers=_launchers())
        self.assertTrue(any("man_bow" in p and "Crossbow" in p for p in problems))


class RepoSpecTests(unittest.TestCase):
    """The shipped tools/ranged_ladders.json: the ranking Mike approved on 2026-09-18 (#617)."""

    @classmethod
    def setUpClass(cls):
        cls.spec = rl.load_spec()

    def test_it_validates(self):
        self.assertEqual(rl.validate_spec(self.spec), [])

    def test_lines_and_ranks(self):
        ids = [ln["id"] for ln in self.spec["lines"]]
        self.assertEqual(len(ids), 19)
        self.assertIn("ithilien", ids)
        self.assertIn("blackroot", ids)
        self.assertNotIn("gondor_special", ids)
        r = {ln["id"]: ln["ranks"] for ln in self.spec["lines"]}
        self.assertEqual(r["mirkwood"], {"overall": 1, "damage": 1, "accuracy": 1})
        self.assertEqual(r["harad"]["accuracy"], 3)          # third on accuracy, after the elves
        self.assertEqual(r["harad"]["damage"], 6)
        self.assertEqual(r["erebor"]["damage"], 2)           # crafting: elf-grade damage only
        self.assertEqual(r["erebor"]["overall"], 8)
        self.assertEqual(r["dunland"]["overall"], 11)

    def test_the_headline_cells(self):
        s = self.spec
        self.assertEqual(rl.cell("mirkwood", "Bow", 10, s), rl.Cell(speed=108, damage=133, accuracy=99))
        self.assertEqual(rl.cell("mirkwood", "Bow", 9, s).accuracy, 98)
        self.assertEqual(rl.skill_cell("mirkwood", 10, s), 410)
        self.assertEqual(rl.cell("rivendell", "Bow", 2, s), rl.Cell(speed=85, damage=49, accuracy=87))
        self.assertEqual(rl.cell("erebor", "Bow", 5, s).damage, rl.cell("rivendell", "Bow", 5, s).damage)
        self.assertEqual(rl.cell("dunland", "Bow", 6, s).speed, 66)

    def test_no_better_damage_rank_is_worse_at_the_same_tier(self):
        s = self.spec
        for cls in rl.CLASSES:
            cells = [(rl.rank(ln["id"], "damage", s), rl.cell(ln["id"], cls, t, s).damage, ln["id"], t)
                     for ln in s["lines"] for t in rl.tiers_for(ln["id"], cls, s)]
            for ra, da, la, ta in cells:
                for rb_, db, lb, tb in cells:
                    if ta == tb and ra < rb_:
                        self.assertGreaterEqual(da, db, f"{cls} T{ta}: {la} (rank {ra}) under {lb} (rank {rb_})")

    def test_one_item_per_listed_tier(self):
        self.assertEqual(len(rl.planned_items(self.spec)), 123)

    def test_donor_table_stays_under_the_hero_ceiling(self):
        s = self.spec
        self.assertEqual(s["donor_stats"]["highelf_longbowd"], {"damage": 90, "accuracy": 98})
        self.assertLessEqual(max(r["damage"] for r in s["donor_stats"].values()), s["hero_ceiling"]["Crossbow"])
        self.assertTrue(all(v <= 5 for v in s["ammo_stats"].values()))


# --------------------------------------------------------------------------- #
# Lines                                                                         #
# --------------------------------------------------------------------------- #
class UsageOverrideSpecTests(unittest.TestCase):
    def test_a_usage_override_must_name_a_ladder_class_and_a_usage_id(self):
        spec = _spec()
        spec["lines"][2]["usage"] = {"Bow": "bow"}
        self.assertEqual(rl.validate_spec(spec, _launchers()), [])
        spec["lines"][2]["usage"] = {"Javelin": "bow", "Crossbow": ""}
        problems = rl.validate_spec(spec, _launchers())
        self.assertEqual(len(problems), 2, problems)
        self.assertTrue(any("Javelin" in p for p in problems))
        self.assertTrue(any("Crossbow" in p and "empty" in p for p in problems))


class LineTests(unittest.TestCase):
    def test_prefix_claims_before_file(self):
        spec = _spec()
        self.assertEqual(rl.line_of(_troop("man_sp_ranger", "man", 36, []), spec), "man_special")
        self.assertEqual(rl.line_of(_troop("man_archer", "man", 21, []), spec), "man")
        self.assertEqual(rl.line_for("man_sp_x", "man", spec), "man_special")

    def test_shared_file_maps_to_the_owning_line(self):
        self.assertEqual(rl.line_of(_troop("twin_archer", "elf_twin", 21, []), _spec()), "elf")

    def test_unclaimed_file_is_none(self):
        self.assertIsNone(rl.line_of(_troop("x", "nobody", 21, []), _spec()))

    def test_rank_reads_the_stat_list(self):
        spec = _spec()
        spec["lines"][2]["ranks"]["accuracy"] = 1
        self.assertEqual([rl.rank("man", s, spec) for s in ("speed", "damage", "accuracy", "skill")], [3, 3, 1, 3])


# --------------------------------------------------------------------------- #
# Indexes                                                                       #
# --------------------------------------------------------------------------- #
class IndexTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_index_launchers_reads_bows_and_crossbows_only_from_xml_files(self):
        (self.root / "a").mkdir()
        (self.root / "a" / "weapons.xml").write_bytes(BOM + b"""<Items>
  <Item id="b1" name="{=k}Bow One" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="88" accuracy="90" thrust_damage="70"/></ItemComponent></Item>
  <Item id="x1" name="Xbow" Type="Crossbow"><ItemComponent><Weapon weapon_class="Crossbow" missile_speed="60" accuracy="95" thrust_damage="97"/></ItemComponent></Item>
  <Item id="s1" name="Sword" Type="OneHandedWeapon"><ItemComponent><Weapon weapon_class="OneHandedSword" swing_speed="90"/></ItemComponent></Item>
  <Item id="a1" name="Arrow" Type="Arrows"><ItemComponent><Weapon weapon_class="Arrow" missile_speed="10"/></ItemComponent></Item>
</Items>""")
        (self.root / "a" / "weapons.xml.bak-old").write_bytes(
            b'<Items><Item id="ghost"><ItemComponent><Weapon weapon_class="Bow" missile_speed="1"/></ItemComponent></Item></Items>')
        (self.root / "a" / "broken.xml").write_bytes(b'<Items><Item id="q"><ItemComponent><Weapon weapon_class="Bow"')
        (self.root / "a" / "armour_broken.xml").write_bytes(b"<Items><Item id=")   # no launcher: never parsed, never reported
        failures = []
        idx = rl.index_launchers([self.root, self.root / "missing"], failures=failures)
        self.assertEqual(set(idx), {"b1", "x1"})
        self.assertEqual((idx["b1"].cls, idx["b1"].speed, idx["b1"].accuracy, idx["b1"].damage, idx["b1"].name),
                         ("Bow", 88, 90, 70, "Bow One"))
        self.assertEqual(idx["b1"].usage, "")
        self.assertEqual(idx["x1"].cls, "Crossbow")
        self.assertEqual(len(failures), 1)
        self.assertIn("broken.xml", failures[0])

    def test_index_ammo_reads_arrows_and_bolts_with_damage_and_stack(self):
        (self.root / "b").mkdir()
        (self.root / "b" / "weapons.xml").write_bytes(b"""<Items>
  <Item id="arr" name="{=k}Bodkin Arrows" Type="Arrows"><ItemComponent><Weapon weapon_class="Arrow" thrust_damage="6" stack_amount="28" missile_speed="10"/></ItemComponent></Item>
  <Item id="bolt" name="Bolts" Type="Bolts"><ItemComponent><Weapon weapon_class="Bolt" thrust_damage="12" stack_amount="20"/></ItemComponent></Item>
  <Item id="b1" name="Bow" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="88"/></ItemComponent></Item>
</Items>""")
        idx = rl.index_ammo([self.root])
        self.assertEqual(set(idx), {"arr", "bolt"})
        self.assertEqual((idx["arr"].cls, idx["arr"].damage, idx["arr"].stack, idx["arr"].name), ("Arrow", 6, 28, "Bodkin Arrows"))
        self.assertEqual((idx["bolt"].cls, idx["bolt"].damage, idx["bolt"].stack), ("Bolt", 12, 20))

    def test_load_ranged_troops_keeps_weapon_slots_skills_and_upgrades(self):
        md = self.root / "ModuleData"
        (md / "troops").mkdir(parents=True)
        (md / "characters").mkdir()
        body = _npc_xml("man_archer", 21, [
            {"Item0": "man_bow", "Item1": "arrows_a", "Head": "cap_a"},
            {"Item0": "sword_a", "Item2": "man_bow", "Item3": "arrows_a"},
            {"_civilian": True, "Item0": "hunting_bow"},
        ], upgrades=("man_footman",)) + _npc_xml("man_footman", 16, [{"Item0": "sword_a"}])
        (md / "troops" / "troops_man.xml").write_bytes(
            BOM + f"<NPCCharacters>{body}\n</NPCCharacters>".encode())
        (md / "characters" / "npcs_man.xml").write_bytes(
            b"<NPCCharacters>" + _npc_xml("villager_man", 6, [{"Item0": "hunting_bow"}], upgrades=("man_archer",)).encode()
            + b"</NPCCharacters>")
        troops = rl.load_ranged_troops(md)
        self.assertEqual(set(troops), {"man_archer", "man_footman"})  # villagers are not on the ladder
        t = troops["man_archer"]
        self.assertEqual((t.culture, t.level, t.tier, t.group, t.skills.get("Bow"), t.upgrades),
                         ("man", 21, 4, "Ranged", 100, ["man_footman"]))
        self.assertEqual(t.sets, [{"Item0": "man_bow", "Item1": "arrows_a"},
                                  {"Item0": "sword_a", "Item2": "man_bow", "Item3": "arrows_a"}])
        sources = rl.load_upgrade_sources(md)
        self.assertEqual(list(sources), ["villager_man"])
        self.assertEqual(sources["villager_man"].upgrades, ["man_archer"])

    def test_loader_marks_a_troop_mounted_by_group_or_by_a_horse_slot(self):
        md = self.root / "ModuleData"
        (md / "troops").mkdir(parents=True)
        body = (_npc_xml("man_foot", 21, [{"Item0": "man_bow"}])
                + _npc_xml("man_horse", 21, [{"Item0": "man_bow", "Horse": "pony"}])
                + _npc_xml("man_ha", 21, [{"Item0": "man_bow"}]).replace('default_group="Ranged"', 'default_group="HorseArcher"'))
        (md / "troops" / "troops_man.xml").write_bytes(f"<NPCCharacters>{body}\n</NPCCharacters>".encode())
        troops = rl.load_ranged_troops(md)
        self.assertEqual([troops[t].mounted for t in ("man_foot", "man_horse", "man_ha")], [False, True, True])

    def test_mount_barred_usages_come_from_the_install(self):
        modules = self.root / "Modules"
        (modules / "Native" / "ModuleData").mkdir(parents=True)
        (modules / "Native" / "ModuleData" / "item_usage_sets.xml").write_bytes(b"""<item_usage_sets>
  <item_usage_set id="bow"><flags/></item_usage_set>
  <item_usage_set id="long_bow" base_set="bow"><flags><flag name="requires_no_mount"/><flag name="requires_no_shield"/></flags></item_usage_set>
  <item_usage_set id="crossbow"><flags><flag name="requires_no_mount"/></flags></item_usage_set>
</item_usage_sets>""")
        self.assertEqual(rl.mount_barred_usages(modules), {"long_bow", "crossbow"})
        self.assertIsNone(rl.mount_barred_usages(self.root / "nowhere"))   # unreadable is None, never a guess

    def test_troop_values_are_worst_and_best_over_sets(self):
        idx = _launchers()
        t = _troop("t", "man", 21, [{"Item0": "man_bow"}, {"Item0": "hunting_bow", "Item2": "man_xbow"}],
                   skills={"Bow": 70})
        self.assertEqual(rl.troop_values(t, idx, "Bow", "speed"), (64, 80))
        self.assertEqual(rl.troop_values(t, idx, "Bow", "skill"), (70, 70))
        self.assertEqual(rl.troop_speed(t, idx, "Crossbow"), 75)
        self.assertIsNone(rl.troop_values(_troop("u", "man", 21, [{"Item0": "sword"}]), idx, "Bow", "speed"))


# --------------------------------------------------------------------------- #
# The two rules                                                                 #
# --------------------------------------------------------------------------- #
class InversionTests(unittest.TestCase):
    def _found(self, troops, idx=None):
        return [(f.kind, f.stat, f.cls, f.low, f.high, f.low_value, f.high_value)
                for f in rl.inversions(troops, idx or _launchers(), _spec())]

    def test_tier_inversion_inside_a_line(self):
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),      # T2 at 80
            "man_b": _troop("man_b", "man", 26, [{"Item0": "hunting_bow"}]),  # T5 at 64
        }
        self.assertEqual(self._found(troops), [("tier", "speed", "Bow", "man_a", "man_b", 80, 64)])

    def test_rank_inversion_at_the_same_tier(self):
        troops = {
            "elf_a": _troop("elf_a", "elf", 21, [{"Item0": "hunting_bow"}]),  # rank 1, T4, 64
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow"}]),      # rank 3, T4, 80
        }
        found = rl.inversions(troops, _launchers(), _spec())
        self.assertEqual([(f.kind, f.stat, f.scope, f.low, f.high) for f in found],
                         [("rank", "speed", "T4", "man_a", "elf_a")])

    def test_different_tiers_of_different_lines_are_not_compared(self):
        troops = {
            "elf_a": _troop("elf_a", "elf", 16, [{"Item0": "hunting_bow"}]),  # rank 1, T3
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow"}]),      # rank 3, T4
        }
        self.assertEqual(rl.inversions(troops, _launchers(), _spec()), [])

    def test_damage_accuracy_and_skill_are_rules_too(self):
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "heavy_bow"}], skills={"Bow": 90}),   # T2: 120 dmg, skill 90
            "man_b": _troop("man_b", "man", 26, [{"Item0": "man_bow"}], skills={"Bow": 60}),     # T5: 80 dmg, skill 60
            "man_c": _troop("man_c", "man", 16, [{"Item0": "sharp_bow"}], skills={"Bow": 60}),   # T3: acc 99
            "man_d": _troop("man_d", "man", 31, [{"Item0": "man_bow"}], skills={"Bow": 95}),     # T6: acc 90
        }
        got = {(k, s, lo, hi) for k, s, _c, lo, hi, _a, _b in self._found(troops)}
        self.assertIn(("tier", "damage", "man_a", "man_b"), got)
        self.assertIn(("tier", "skill", "man_a", "man_b"), got)
        self.assertIn(("tier", "accuracy", "man_c", "man_d"), got)
        self.assertNotIn(("tier", "skill", "man_a", "man_d"), got)   # 90 under 95: fine

    def test_the_higher_troop_is_judged_by_its_worst_set(self):
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),                              # T2: 80
            "man_b": _troop("man_b", "man", 26, [{"Item0": "hunting_bow"}, {"Item0": "elf_bow"}]),    # T5: 64 or 95
        }
        self.assertEqual([(lo, hi, a, b) for _k, _s, _c, lo, hi, a, b in self._found(troops)],
                         [("man_a", "man_b", 80, 64)])

    def test_mount_conflicts_name_every_mounted_troop_holding_a_barred_usage(self):
        idx = _launchers()
        troops = {
            "man_h": _troop("man_h", "man", 21, [{"Item0": "long_bow_item"}], group="HorseArcher"),
            "man_c": _troop("man_c", "man", 21, [{"Item0": "long_bow_item"}, {"Item0": "man_bow"}], group="Cavalry"),
            "man_f": _troop("man_f", "man", 21, [{"Item0": "long_bow_item"}]),
            "man_ok": _troop("man_ok", "man", 21, [{"Item0": "man_bow"}], group="HorseArcher"),
        }
        hits = rl.mount_conflicts(troops, idx, {"long_bow"})
        self.assertEqual([(h.troop, h.launcher, h.usage) for h in hits],
                         [("man_c", "long_bow_item", "long_bow"), ("man_h", "long_bow_item", "long_bow")])

    def test_ties_pass_and_classes_never_compare(self):
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),   # Bow 80, T2
            "man_b": _troop("man_b", "man", 26, [{"Item0": "man_bow"}]),   # Bow 80, T5: tie
            "man_x": _troop("man_x", "man", 36, [{"Item0": "man_xbow"}]),  # Crossbow 75, T7: other class
            "elf_a": _troop("elf_a", "elf", 11, [{"Item0": "man_bow"}]),   # rank 1, T2, 80: tie with man_a
        }
        self.assertEqual(rl.inversions(troops, _launchers(), _spec()), [])

    def test_unassigned_and_unlisted_troops_are_listed_not_dropped(self):
        idx = _launchers()
        troops = {"x": _troop("x", "nobody", 21, [{"Item0": "man_bow"}]),
                  "y": _troop("y", "nobody", 21, [{"Item0": "sword"}]),
                  "elf_t4": _troop("elf_t4", "elf", 21, [{"Item0": "elf_bow"}]),        # elf lists no T4
                  "man_t4": _troop("man_t4", "man", 21, [{"Item0": "man_bow"}])}
        self.assertEqual([t.id for t in rl.unassigned(troops, idx, _spec())], ["x"])
        self.assertEqual([(t.id, c) for t, c in rl.unlisted(troops, idx, _spec())], [("elf_t4", "Bow")])

    def test_summary_groups_by_kind_stat_class_and_scope_with_the_worst_pair(self):
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),           # 80 T2
            "man_b": _troop("man_b", "man", 26, [{"Item0": "hunting_bow"}]),       # 64 T5
            "man_c": _troop("man_c", "man", 31, [{"Item0": "ladder_man_bow_t3"}]),  # 64 T6
        }
        groups = rl.summarize(rl.inversions(troops, _launchers(), _spec()))
        self.assertEqual(len(groups), 1)
        g = groups[0]
        self.assertEqual((g.kind, g.stat, g.cls, g.scope, g.count), ("tier", "speed", "Bow", "man", 2))  # a>b, a>c
        self.assertEqual(g.worst.low, "man_a")


# --------------------------------------------------------------------------- #
# Planning                                                                      #
# --------------------------------------------------------------------------- #
class PlanTests(unittest.TestCase):
    def test_planned_items_cover_every_listed_tier_of_every_declared_class(self):
        items = rl.planned_items(_spec())
        ids = [i.id for i in items]
        self.assertEqual(len(items), 4 + 3 + 6 + 2)
        self.assertIn("ladder_elf_bow_t10", ids)
        self.assertIn("ladder_man_xbow_t4", ids)
        self.assertNotIn("ladder_elf_bow_t4", ids)
        top = next(i for i in items if i.id == "ladder_elf_bow_t10")
        self.assertEqual((top.donor, top.speed, top.damage, top.accuracy, top.folder, top.line, top.cls, top.tier, top.band),
                         ("elf_bow_top", 84, 154, 90, "elf", "elf", "Bow", 10, "C"))
        mid = next(i for i in items if i.id == "ladder_elf_bow_t5")
        self.assertEqual(mid.donor, "elf_bow")
        self.assertIsNone(mid.usage)

    def test_planned_items_carry_the_line_usage_override_per_class(self):
        spec = _spec()
        spec["lines"][2]["usage"] = {"Bow": "bow"}
        items = {i.id: i for i in rl.planned_items(spec)}
        self.assertEqual(items["ladder_man_bow_t3"].usage, "bow")
        self.assertIsNone(items["ladder_man_xbow_t4"].usage)
        self.assertIsNone(items["ladder_man_special_bow_t7"].usage)

    def test_planned_edits_accept_a_barred_donor_when_the_line_overrides_the_usage(self):
        idx = _launchers()
        spec = _spec()
        spec["lines"][2]["donor_by_band"] = {"Bow": {"R": "long_bow_item"}}
        spec["lines"][2]["usage"] = {"Bow": "bow"}
        troops = {"man_h": _troop("man_h", "man", 21, [{"Item0": "man_bow"}], group="HorseArcher")}
        self.assertEqual(len(rl.planned_edits(troops, idx, spec, barred={"long_bow"})), 1)
        spec["lines"][2]["usage"] = {"Bow": "long_bow"}   # an override can bar too
        with self.assertRaises(rl.LadderError):
            rl.planned_edits(troops, idx, spec, barred={"long_bow"})

    def test_planned_edits_rewrite_every_launcher_slot_to_the_tier_cell(self):
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow", "Item1": "arrows"},
                                                {"Item0": "hunting_bow", "Item2": "man_xbow", "Item3": "bolts"}]),
            "man_sp_r": _troop("man_sp_r", "man", 46, [{"Item0": "man_bow"}]),
            "man_done": _troop("man_done", "man", 16, [{"Item0": "ladder_man_bow_t3"}]),
            "elf_x": _troop("elf_x", "elf_twin", 26, [{"Item0": "elf_bow"}]),
        }
        edits = rl.planned_edits(troops, _launchers(), _spec())
        by = {(e.troop, e.slot, e.old): e.new for e in edits}
        self.assertEqual(by, {
            ("man_a", "Item0", "man_bow"): "ladder_man_bow_t4",
            ("man_a", "Item0", "hunting_bow"): "ladder_man_bow_t4",
            ("man_a", "Item2", "man_xbow"): "ladder_man_xbow_t4",
            ("man_sp_r", "Item0", "man_bow"): "ladder_man_special_bow_t9",
            ("elf_x", "Item0", "elf_bow"): "ladder_elf_bow_t5",
        })
        self.assertTrue(all(e.file.endswith(".xml") for e in edits))

    def test_retired_ladder_ids_are_planned_as_launchers_and_repointed(self):
        idx = _launchers()
        troops = {"man_a": _troop("man_a", "man", 21, [{"Item0": "ladder_man_bow_r", "Item1": "arrows"},
                                                      {"Item0": "ladder_man_xbow_e"}]),
                  "man_b": _troop("man_b", "man", 21, [{"Item0": "ladder_notes_about_bows"}])}
        retired = rl.retired_ladder_launchers(troops, idx)
        self.assertEqual({k: v.cls for k, v in retired.items()}, {"ladder_man_bow_r": "Bow", "ladder_man_xbow_e": "Crossbow"})
        self.assertNotIn("ladder_man_bow_t3", rl.retired_ladder_launchers(
            {"x": _troop("x", "man", 16, [{"Item0": "ladder_man_bow_t3"}])}, idx))   # defined: not retired
        edits = rl.planned_edits(troops, {**idx, **retired}, _spec())
        self.assertEqual({(e.old, e.new) for e in edits},
                         {("ladder_man_bow_r", "ladder_man_bow_t4"), ("ladder_man_xbow_e", "ladder_man_xbow_t4")})

    def test_planned_edits_refuse_a_troop_at_a_tier_its_line_lists_no_cell_for(self):
        troops = {"elf_t4": _troop("elf_t4", "elf", 21, [{"Item0": "elf_bow"}])}
        with self.assertRaises(rl.LadderError) as ctx:
            rl.planned_edits(troops, _launchers(), _spec())
        self.assertIn("tier 4", str(ctx.exception))

    def test_planned_edits_refuse_a_mounted_troop_in_a_cell_with_a_barred_donor(self):
        spec = _spec()
        spec["lines"][2]["donor_by_band"] = {"Bow": {"R": "long_bow_item"}}
        troops = {"man_h": _troop("man_h", "man", 21, [{"Item0": "man_bow"}], group="HorseArcher")}
        with self.assertRaises(rl.LadderError) as ctx:
            rl.planned_edits(troops, _launchers(), spec, barred={"long_bow"})
        self.assertIn("man_h", str(ctx.exception))
        self.assertIn("long_bow", str(ctx.exception))
        self.assertEqual(len(rl.planned_edits(troops, _launchers(), spec)), 1)   # install unreadable: plans

    def test_planned_edits_refuse_a_class_the_line_does_not_declare(self):
        troops = {"elf_x": _troop("elf_x", "elf", 26, [{"Item0": "man_xbow"}])}
        with self.assertRaises(rl.LadderError):
            rl.planned_edits(troops, _launchers(), _spec())

    def test_skill_edits_put_every_ladder_troop_on_its_cell(self):
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow"}], skills={"Bow": 100}),
            "man_x": _troop("man_x", "man", 26, [{"Item0": "man_xbow"}], skills={"Bow": 40, "Crossbow": 90}),
            "man_f": _troop("man_f", "man", 21, [{"Item0": "sword"}], skills={"Bow": 5}),
        }
        edits = rl.planned_skill_edits(troops, _launchers(), _spec())
        self.assertEqual([(e.troop, e.skill, e.old, e.new, e.reason) for e in edits],
                         [("man_a", "Bow", 100, 70, "cell")])          # man_x Crossbow already 90 at T5

    def test_skill_clamp_raises_a_non_ladder_child_and_refuses_a_ladder_one(self):
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow"}], upgrades=["man_f"], skills={"Bow": 100}),
            "man_f": _troop("man_f", "man", 26, [{"Item0": "sword"}], skills={"Bow": 50}),
        }
        edits = rl.planned_skill_edits(troops, _launchers(), _spec())
        self.assertIn(("man_f", "Bow", 50, 70, "clamp"), [(e.troop, e.skill, e.old, e.new, e.reason) for e in edits])
        villager = _troop("villager_man", "", 6, [], upgrades=["man_b"], skills={"Bow": 120})
        troops["man_b"] = _troop("man_b", "man", 26, [{"Item0": "man_bow"}], skills={"Bow": 90})
        with self.assertRaises(rl.LadderError) as ctx:
            rl.planned_skill_edits(troops, _launchers(), _spec(), sources={"villager_man": villager})
        self.assertIn("UPGRADE_SKILL_REGRESSION", str(ctx.exception))

    def test_skill_clamp_skips_militia_pairs_and_exempt_edges(self):
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow"}], upgrades=["man_f"], skills={"Bow": 70}),
            "man_f": _troop("man_f", "man", 26, [{"Item0": "sword"}], skills={"Bow": 50}),
        }
        self.assertEqual(rl.planned_skill_edits(troops, _launchers(), _spec(), militia={"man_a", "man_f"}), [])
        self.assertEqual(rl.planned_skill_edits(troops, _launchers(), _spec(),
                                                exempt_edges={("man_a", "man_f"): {"Bow", "Crossbow"}}), [])

    def test_troop_rows_pairs_ammo_with_the_chosen_launcher_set_and_marks_mounted(self):
        import rebalance_ranged_ladders as rr
        idx = _launchers()
        idx["fast_bow"] = rl.Launcher("fast_bow", "Bow", 90, 95, 80, "Fast", "w.xml")
        ammo = {"weak": rl.Ammo("weak", "Arrow", 2, 30, "Weak"), "strong": rl.Ammo("strong", "Arrow", 9, 20, "Strong"),
                "bolts": rl.Ammo("bolts", "Bolt", 12, 20, "Bolts")}
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "fast_bow", "Item1": "weak"}, {"Item0": "man_bow", "Item1": "strong"}]),
            "man_h": _troop("man_h", "man", 21, [{"Item0": "man_bow", "Item1": "strong"}], group="HorseArcher"),
            "man_x": _troop("man_x", "man", 26, [{"Item0": "man_xbow", "Item1": "bolts"}]),
        }
        ctx = rr.build_context(_spec(), idx, troops, "gm", "md", ammo)
        rows = {r["id"]: r for r in rr.troop_rows(ctx)["man"]}
        a = rows["man_a"]
        self.assertEqual((a["launcher"], a["ammo"], a["ammo_dmg"], a["total_dmg"], a["alts"]), ("fast_bow", "weak", 2, 82, 1))
        self.assertIsNone(a["opens"])
        self.assertIsNotNone(rows["man_h"]["opens"])
        self.assertLess(rows["man_h"]["opens"], rows["man_h"]["reach"])
        x = rows["man_x"]
        self.assertEqual((x["cls"], x["ammo"], x["total_dmg"]), ("Crossbow", "bolts", 80 + 12))

    def test_weapon_inaccuracy_uses_each_class_skill_factor(self):
        import rebalance_ranged_ladders as rr
        self.assertAlmostEqual(rr.weapon_inaccuracy(90, 150, "Bow"), 10 * (1 - 0.0009 * 150) * 0.001)
        self.assertAlmostEqual(rr.weapon_inaccuracy(90, 150, "Crossbow"), 10 * (1 - 0.0005 * 150) * 0.001)
        self.assertGreater(rr.weapon_inaccuracy(90, 150, "Crossbow"), rr.weapon_inaccuracy(90, 150, "Bow"))

    def test_flight_range_grows_with_speed(self):
        r = [rl.flight_range(v) for v in (58, 74, 90, 108)]
        self.assertEqual(r, sorted(r))
        self.assertTrue(190 < r[0] < 230)
        self.assertTrue(360 < r[3] < 410)


class RebalanceTroopsAgreementTests(unittest.TestCase):
    """tools/rebalance_troops.py gives a ladder troop the same Bow the ladder tool writes, so a
    later full rebaseline never undoes the ranking."""

    def test_process_file_takes_the_ladder_cell(self):
        import rebalance_troops as rbt
        spec = rl.load_spec()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "troops_rohan.xml"
            body = (_npc_xml("rohan_test_archer", 21, [{"Item0": "ladder_rohan_bow_t4", "Item1": "arrows"}], "vlandia")
                    + _npc_xml("rohan_test_foot", 21, [{"Item0": "sword"}], "vlandia"))
            path.write_bytes(f"<NPCCharacters>{body}\n</NPCCharacters>".encode())
            recs = {r["id"]: r for r in rbt.process_file(str(path), item_classes={"ladder_rohan_bow_t4": "Bow",
                                                                                  "sword": "OneHanded"})}
        self.assertEqual(recs["rohan_test_archer"]["new"]["Bow"], rl.skill_cell("rohan", 4, spec))
        self.assertEqual(rl.skill_cell("rohan", 4, spec), 100)                   # 130 + 10 * (7 - 10)
        self.assertNotEqual(recs["rohan_test_foot"]["new"]["Bow"], 100)         # no launcher: the level curve


# --------------------------------------------------------------------------- #
# Heroes                                                                        #
# --------------------------------------------------------------------------- #
class HeroTests(unittest.TestCase):
    def test_hero_launchers_reads_inline_rosters_and_lords_xslt_and_skips_troops(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp)
            (md / "characters").mkdir()
            (md / "equipmentsets").mkdir()
            (md / "troops").mkdir()
            (md / "characters" / "lords.xml").write_bytes(b"""<NPCCharacters>
  <NPCCharacter id="lord_a" occupation="Lord"><equipment slot="Item0" id="Item.elf_bow"/></NPCCharacter>
  <NPCCharacter id="lord_b" occupation="Lord"><Equipments><EquipmentSet id="kit_b"/><EquipmentSet id="civ_b" equipmentType="Civilian"/></Equipments></NPCCharacter>
  <NPCCharacter id="soldier" occupation="Soldier"><equipment slot="Item0" id="Item.hunting_bow"/></NPCCharacter>
</NPCCharacters>""")
            (md / "equipmentsets" / "sets.xml").write_bytes(b"""<EquipmentRosters>
  <EquipmentRoster id="kit_b"><EquipmentSet><Equipment slot="Item0" id="Item.man_xbow"/></EquipmentSet></EquipmentRoster>
  <EquipmentRoster id="civ_b"><EquipmentSet><Equipment slot="Item0" id="Item.long_bow_item"/></EquipmentSet></EquipmentRoster>
  <EquipmentRoster id="kit_x"><EquipmentSet><Equipment slot="Item0" id="Item.heavy_bow"/></EquipmentSet></EquipmentRoster>
</EquipmentRosters>""")
            (md / "troops" / "troops_man.xml").write_bytes(
                b'<NPCCharacters><NPCCharacter id="w" occupation="Wanderer"><equipment slot="Item0" id="Item.man_bow"/></NPCCharacter></NPCCharacters>')
            (md / "lords.xslt").write_bytes(b'<xsl:stylesheet><EquipmentSet id="kit_x" /></xsl:stylesheet>')
            heroes = rl.hero_launchers(md, _launchers())
        self.assertEqual(heroes, {"elf_bow": ["lord_a"], "man_xbow": ["lord_b"], "heavy_bow": ["lords.xslt"]})
        breaches = rl.ceiling_breaches(_spec(), _launchers(), heroes)
        self.assertEqual([(r.id, cap) for r, cap, _who in breaches], [("heavy_bow", 90)])


# --------------------------------------------------------------------------- #
# tools/rebalance_ranged_ladders.py                                             #
# --------------------------------------------------------------------------- #
class RebalanceToolTests(unittest.TestCase):
    """A fake install: a two-line spec, one Armory weapons file, one vanilla items file, one
    troop file with BOM + CRLF, and the culture files that bind the militia."""

    def setUp(self):
        import rebalance_ranged_ladders as rr
        self.rr = rr
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        armory = self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items"
        vanilla = self.modules / "SandBoxCore" / "ModuleData" / "items"
        armory.mkdir(parents=True)
        vanilla.mkdir(parents=True)
        (armory / "LOTRAOM_weapons.xml").write_bytes(BOM + b"""<Items>
  <Item id="elf_bow" name="{=e}[Elf] Bow I" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="95" accuracy="100" thrust_damage="100"/></ItemComponent></Item>
  <Item id="elf_bow_top" name="{=e2}[Elf] Bow II" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="100" accuracy="100" thrust_damage="110"/></ItemComponent></Item>
  <Item id="man_bow" name="[Man] Bow" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="80" accuracy="90" thrust_damage="80"/></ItemComponent></Item>
  <Item id="man_xbow" name="[Man] Crossbow" Type="Crossbow"><ItemComponent><Weapon weapon_class="Crossbow" missile_speed="75" accuracy="95" thrust_damage="97"/></ItemComponent></Item>
</Items>""")
        (vanilla / "weapons.xml").write_bytes(b"""<Items>
  <Item id="hunting_bow" name="Hunting Bow" Type="Bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="64" accuracy="85" thrust_damage="40"/></ItemComponent></Item>
</Items>""")
        self.md = root / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        (self.md / "taom_spcultures.xml").write_bytes(
            b'<SPCultures><Culture id="man" militia_troop="NPCCharacter.man_militia"/></SPCultures>')
        (self.md / "spcultures.xslt").write_bytes(b"<xsl:stylesheet/>")
        body = (_npc_xml("man_militia", 11, [{"Item0": "hunting_bow", "Item1": "arrows"}, {"Item0": "man_bow", "Item1": "arrows"}])
                + _npc_xml("man_archer", 21, [{"Item0": "hunting_bow", "Item1": "arrows", "Head": "cap"}])  # T4 slower than the T2 militia
                + _npc_xml("man_xbowman", 26, [{"Item0": "man_xbow", "Item1": "bolts"}])
                + _npc_xml("man_sp_ranger", 46, [{"Item0": "hunting_bow", "Item1": "arrows"}])
                + _npc_xml("man_footman", 16, [{"Item0": "sword"}]))
        self.troop_file = self.md / "troops" / "troops_man.xml"
        self.troop_file.write_bytes(BOM + ("<NPCCharacters>" + body + "\n</NPCCharacters>\n").replace("\n", "\r\n").encode())
        (self.md / "troops" / "troops_elf.xml").write_bytes(
            ("<NPCCharacters>" + _npc_xml("elf_archer", 26, [{"Item0": "elf_bow", "Item1": "arrows"}], "elf") + "\n</NPCCharacters>\n").encode())
        (self.md / "troops" / "troops_elf_twin.xml").write_bytes(b"<NPCCharacters/>")   # claimed by the spec, must exist
        self.spec_path = root / "spec.json"
        self.spec_path.write_text(json.dumps(_spec()), encoding="utf-8")
        self.report_dir = root / "reports"

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return self.rr.main(["--game-modules", str(self.modules), "--moduledata", str(self.md),
                             "--spec", str(self.spec_path), "--report-dir", str(self.report_dir),
                             "--docs-html", str(self.report_dir / "docs" / "ranged-troops.html"), *extra])

    def _json(self):
        return json.loads((self.report_dir / "ranged-ladders.json").read_text(encoding="utf-8"))

    def test_dry_run_writes_the_report_and_touches_nothing(self):
        before = self.troop_file.read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual(self.troop_file.read_bytes(), before)
        md = (self.report_dir / "REPORT.md").read_text(encoding="utf-8")
        self.assertIn("ladder_man_bow_t4", md)
        self.assertIn("man_sp_ranger", md)
        self.assertIn("| man |", md)            # the cell table names every line
        self.assertIn("inversions", md.lower())
        data = self._json()
        self.assertEqual(data["edits_pending"], 6)          # militia x2 old ids, archer, xbowman, ranger, elf
        self.assertEqual(data["skill_edits_pending"], 5)    # every ranged troop onto its cell
        self.assertGreater(data["inversions_before"], 0)
        self.assertEqual(data["inversions_after"], 0)
        self.assertEqual(data["cells"]["ladder_man_bow_t4"], {"speed": 66, "damage": 72, "accuracy": 80, "skill": 70})

    def test_dry_run_writes_the_html_report_per_kingdom(self):
        self.assertEqual(self._run(), 0)
        html = (self.report_dir / "REPORT.html").read_text(encoding="utf-8")
        self.assertIn("<title>", html)
        self.assertIn('id="kingdom-man"', html)
        self.assertLess(html.index('id="kingdom-elf"'), html.index('id="kingdom-man"'))
        self.assertIn("man_xbowman", html)
        self.assertIn('id="kingdom-man_special"', html)
        self.assertIn("Crossbow", html)
        self.assertNotIn("—", html)
        self.assertIn("prefers-color-scheme", html)
        tracked = (self.report_dir / "docs" / "ranged-troops.html").read_text(encoding="utf-8")
        self.assertEqual(tracked, html)
        self.assertTrue(html.startswith("<!DOCTYPE html>"))
        self.assertIn('<meta charset="utf-8">', html)
        self.assertNotIn(" UTC", html)

    def test_html_is_reproducible_and_escapes_names(self):
        self.troop_file.write_bytes(self.troop_file.read_bytes().replace(
            b'name="{=t_man_xbowman}man_xbowman"', b'name="{=t_man_xbowman}Beruthiel&apos;s &amp; Co &lt;Rangers&gt;"'))
        self.assertEqual(self._run(), 0)
        first = (self.report_dir / "REPORT.html").read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual((self.report_dir / "REPORT.html").read_bytes(), first)
        html = first.decode("utf-8")
        self.assertIn("Beruthiel&#x27;s &amp; Co &lt;Rangers&gt;", html)
        self.assertNotIn("<Rangers>", html)

    def test_a_troop_file_that_does_not_parse_is_reported_not_dropped(self):
        (self.md / "troops" / "troops_elf.xml").write_bytes(b"<NPCCharacters><NPCCharacter id=")
        failures = []
        troops = rl.load_ranged_troops(self.md, failures=failures)
        self.assertNotIn("elf_archer", troops)
        self.assertEqual(len(failures), 1)
        self.assertIn("troops_elf.xml", failures[0])

    def test_docs_html_dash_skips_the_tracked_copy(self):
        self.assertEqual(self._run("--docs-html", "-"), 0)
        self.assertTrue((self.report_dir / "REPORT.html").exists())
        self.assertFalse((self.report_dir / "docs" / "ranged-troops.html").exists())

    def test_apply_refuses_while_the_ladder_items_do_not_exist(self):
        before = self.troop_file.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.troop_file.read_bytes(), before)

    def test_unreadable_militia_bindings_refuse_the_run(self):
        (self.md / "spcultures.xslt").unlink()
        self.assertEqual(self._run(), 2)

    def _write_ladder_items(self):
        spec = rl.load_spec(self.spec_path)
        rows = "".join(
            f'\n  <Item id="{i.id}" name="x" Type="{i.cls}"><ItemComponent><Weapon weapon_class="{i.cls}" '
            f'missile_speed="{i.speed}" accuracy="{i.accuracy}" thrust_damage="{i.damage}"/></ItemComponent></Item>'
            for i in rl.planned_items(spec))
        (self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "ranged_ladder.xml").write_bytes(
            f"<Items>{rows}\n</Items>".encode())

    def _skill(self, tid, skill):
        root = ET.fromstring(self.troop_file.read_bytes().decode("utf-8-sig"))
        npc = next(n for n in root.iter("NPCCharacter") if n.get("id") == tid)
        return {s.get("id"): int(s.get("value")) for s in npc.iter("skill")}.get(skill)

    def test_apply_rewrites_slots_and_skills_byte_faithfully_and_is_idempotent(self):
        self._write_ladder_items()
        self.assertEqual(self._run("--apply"), 0)
        raw = self.troop_file.read_bytes()
        self.assertTrue(raw.startswith(BOM))
        self.assertNotIn(b"\n<", raw.replace(b"\r\n", b""))  # every newline is still CRLF
        text = raw.decode("utf-8-sig")
        self.assertEqual(text.count('id="Item.ladder_man_bow_t2"'), 2)     # both militia sets
        self.assertIn('id="Item.ladder_man_bow_t4"', text)
        self.assertIn('id="Item.ladder_man_xbow_t5"', text)
        self.assertIn('id="Item.ladder_man_special_bow_t9"', text)
        self.assertEqual(text.count('id="Item.arrows"'), 4)                # ammo untouched
        self.assertIn('slot="Head" id="Item.cap"', text)
        self.assertEqual(self._skill("man_militia", "Bow"), 30)            # militia ranked by tier too
        self.assertEqual(self._skill("man_archer", "Bow"), 70)
        self.assertEqual(self._skill("man_xbowman", "Crossbow"), 90)       # inserted, the troop declared none
        self.assertEqual(self._skill("man_sp_ranger", "Bow"), 180)
        self.assertEqual(self._skill("man_footman", "Bow"), 100)            # no launcher: untouched
        second = self.troop_file.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self.troop_file.read_bytes(), second)
        data = self._json()
        self.assertEqual((data["edits_pending"], data["skill_edits_pending"], data["inversions_before"]), (0, 0, 0))

    def test_apply_repoints_retired_band_ids_the_generator_already_removed(self):
        # The #617 order: the generator replaced the band items, the rosters still name them.
        self.troop_file.write_bytes(self.troop_file.read_bytes().replace(
            b'slot="Item0" id="Item.hunting_bow" />\r\n        <equipment slot="Item1" id="Item.arrows" />\r\n        <equipment slot="Head"',
            b'slot="Item0" id="Item.ladder_man_bow_r" />\r\n        <equipment slot="Item1" id="Item.arrows" />\r\n        <equipment slot="Head"'))
        self.assertIn(b"ladder_man_bow_r", self.troop_file.read_bytes())
        self._write_ladder_items()
        self.assertEqual(self._run("--apply"), 0)
        text = self.troop_file.read_bytes().decode("utf-8-sig")
        self.assertNotIn("ladder_man_bow_r", text)
        self.assertIn('id="Item.ladder_man_bow_t4"', text)
        self.assertEqual(self._skill("man_archer", "Bow"), 70)

    def test_missing_install_is_reported_not_faked(self):
        self.assertEqual(self._run("--game-modules", str(self.modules / "nope")), 2)
        self.assertFalse((self.report_dir / "REPORT.md").exists())


# --------------------------------------------------------------------------- #
# tools/generate_ranged_ladder_items.py                                         #
# --------------------------------------------------------------------------- #
class GeneratorTests(unittest.TestCase):
    """A fake install: Armory SubModule.xml registering two folders, one Armory weapons file,
    one vanilla weapons file, and an assets mirror."""

    def setUp(self):
        import generate_ranged_ladder_items as gen
        self.gen = gen
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.modules = root / "Modules"
        self.armory = self.modules / "LOTRLOME_Armory"
        items = self.armory / "ModuleData" / "LOTRLOME_items"
        (items / "elf").mkdir(parents=True)
        (items / "man").mkdir(parents=True)
        (self.armory / "SubModule.xml").write_text(
            '<Module><Xmls><XmlNode><XmlName id="Items" path="LOTRLOME_items/elf"/></XmlNode>'
            '<XmlNode><XmlName id="Items" path="LOTRLOME_items/man"/></XmlNode>'
            '<XmlNode><XmlName id="Items" path="LOTRLOME_items/LOTRAOM_weapons"/></XmlNode></Xmls></Module>',
            encoding="utf-8")
        (items / "LOTRAOM_weapons.xml").write_bytes(BOM + b"""<Items>
  <Item id="elf_bow" name="{=e}[Elf] Longbow III" Type="Bow" is_merchandise="true" value="900" mesh="elf_bow_mesh" culture="Culture.elf">
    <ItemComponent><Weapon weapon_class="Bow" missile_speed="95" accuracy="100" thrust_damage="100" ammo_limit="1" item_usage="long_bow"/></ItemComponent>
    <Flags UseTeamColor="true"/>
  </Item>
  <Item id="elf_bow_top" name="[Elf] Longbow IV" Type="Bow" mesh="elf_top"><ItemComponent><Weapon weapon_class="Bow" missile_speed="100" accuracy="100" thrust_damage="110"/></ItemComponent></Item>
  <Item id="man_bow" name="[Man] Bow - Starting " Type="Bow" mesh="man_bow"><ItemComponent><Weapon weapon_class="Bow" missile_speed="80" accuracy="90" thrust_damage="80"/></ItemComponent></Item>
</Items>""")
        vanilla = self.modules / "SandBoxCore" / "ModuleData" / "items"
        vanilla.mkdir(parents=True)
        (vanilla / "weapons.xml").write_bytes(b"""<Items>
  <Item id="man_xbow" name="{=v}Arbalest" Type="Crossbow" mesh="xbow"><ItemComponent><Weapon weapon_class="Crossbow" missile_speed="87" accuracy="98" thrust_damage="89"/></ItemComponent></Item>
</Items>""")
        self.mirror = root / "assets" / "v1.5" / "LOTRLOME_Armory"
        (self.mirror / "ModuleData" / "LOTRLOME_items" / "elf").mkdir(parents=True)
        (self.mirror / "ModuleData" / "LOTRLOME_items" / "man").mkdir(parents=True)
        self.md = root / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        for culture in ("elf", "elf_twin", "man"):   # the spec's files tokens must exist on disk
            (self.md / "troops" / f"troops_{culture}.xml").write_bytes(b"<NPCCharacters/>")
        self.spec_path = root / "spec.json"
        self.spec_path.write_text(json.dumps(_spec()), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return self.gen.main(["--game-modules", str(self.modules), "--asset-repo", str(self.mirror),
                              "--spec", str(self.spec_path), "--moduledata", str(self.md), *extra])

    def _elf_file(self, md=None):
        return (md or self.armory / "ModuleData") / "LOTRLOME_items" / "elf" / self.gen.ITEMS_FILE_NAME

    def test_the_default_mirror_is_the_v15_tree(self):
        self.assertIn("v1.5", str(self.gen.DEFAULT_ASSET_REPO))

    def test_a_files_token_with_no_troop_file_is_refused(self):
        (self.md / "troops" / "troops_elf_twin.xml").unlink()
        self.assertEqual(self._run("--apply"), 2)
        self.assertFalse(self._elf_file().exists())

    def test_ladder_name_strips_the_donor_numeral_and_suffixes(self):
        n = self.gen.ladder_name
        self.assertEqual(n("{=e}[Elf] Longbow III", "ladder_elf_bow_t4", 4), "{=ladder_elf_bow_t4}[Elf] Longbow IV")
        self.assertEqual(n("[Man] Bow - Starting ", "ladder_man_bow_t1", 1), "{=ladder_man_bow_t1}[Man] Bow I")
        self.assertEqual(n("[Mirkwood] LongBow II - Horse", "ladder_x_bow_t8", 8), "{=ladder_x_bow_t8}[Mirkwood] LongBow VIII")
        self.assertEqual(n("Arbalest", "ladder_man_xbow_t10", 10), "{=ladder_man_xbow_t10}Arbalest X")

    def test_clone_sets_the_three_stats_and_keeps_everything_else(self):
        donor = ET.fromstring('<Item id="elf_bow" name="{=e}[Elf] Longbow III" Type="Bow" is_merchandise="true" value="900" mesh="m" culture="Culture.elf">'
                              '<ItemComponent><Weapon weapon_class="Bow" missile_speed="95" accuracy="100" thrust_damage="100" ammo_limit="1"/></ItemComponent>'
                              '<Flags UseTeamColor="true"/></Item>')
        item = rl.LadderItem(id="ladder_elf_bow_t3", line="elf", cls="Bow", tier=3, band="R", speed=66, damage=57,
                             accuracy=88, donor="elf_bow", folder="elf")
        out = self.gen.clone_launcher(donor, item)
        self.assertEqual(out.get("id"), "ladder_elf_bow_t3")
        self.assertEqual(out.get("name"), "{=ladder_elf_bow_t3}[Elf] Longbow III")
        self.assertEqual(out.get("is_merchandise"), "false")
        self.assertEqual((out.get("Type"), out.get("mesh"), out.get("culture"), out.get("value")), ("Bow", "m", "Culture.elf", "900"))
        w = out.find("ItemComponent/Weapon")
        self.assertEqual((w.get("missile_speed"), w.get("accuracy"), w.get("thrust_damage"), w.get("ammo_limit")), ("66", "88", "57", "1"))
        self.assertIsNotNone(out.find("Flags"))
        self.assertEqual(donor.find("ItemComponent/Weapon").get("thrust_damage"), "100")   # the donor is untouched
        self.assertIsNone(w.get("item_usage"))

    def test_clone_sets_the_usage_override_on_the_ladder_weapon_only(self):
        donor = ET.fromstring('<Item id="man_bow" name="[Man] Bow" Type="Bow">'
                              '<ItemComponent><Weapon weapon_class="Bow" missile_speed="80" item_usage="long_bow"/></ItemComponent></Item>')
        item = rl.LadderItem(id="ladder_man_bow_t3", line="man", cls="Bow", tier=3, band="R", speed=62, damage=50,
                             accuracy=80, donor="man_bow", folder="man", usage="bow")
        out = self.gen.clone_launcher(donor, item)
        self.assertEqual(out.find("ItemComponent/Weapon").get("item_usage"), "bow")
        self.assertEqual(donor.find("ItemComponent/Weapon").get("item_usage"), "long_bow")

    def test_verify_sees_a_ladder_item_whose_usage_is_not_the_override(self):
        spec = _spec()
        spec["lines"][0]["usage"] = {"Bow": "bow"}
        self.spec_path.write_text(json.dumps(spec), encoding="utf-8")
        self.assertEqual(self._run("--apply"), 0)
        text = self._elf_file().read_bytes()
        self.assertEqual(text.count(b'item_usage="bow"'), 4)      # every elf tier, from the long_bow donor
        self.assertNotIn(b'item_usage="long_bow"', text)
        self.assertEqual(self._run("--verify"), 0)
        self._elf_file().write_bytes(text.replace(b'item_usage="bow"', b'item_usage="long_bow"', 1))
        self.assertEqual(self._run("--verify"), 1)

    def test_dry_run_writes_nothing_and_apply_writes_both_trees(self):
        self.assertEqual(self._run(), 0)
        self.assertFalse(self._elf_file().exists())
        self.assertEqual(self._run("--apply"), 0)
        for md in (self.armory / "ModuleData", self.mirror / "ModuleData"):
            elf = self._elf_file(md).read_bytes()
            self.assertTrue(elf.startswith(b'<?xml version="1.0" encoding="utf-8"?>'))
            self.assertIn(b"\r\n", elf)
            root = ET.fromstring(elf)
            top = next(i for i in root.iter("Item") if i.get("id") == "ladder_elf_bow_t10")
            w = top.find("ItemComponent/Weapon")
            self.assertEqual((w.get("missile_speed"), w.get("thrust_damage"), w.get("accuracy")), ("84", "154", "90"))
            self.assertEqual(top.get("mesh"), "elf_top")                     # tier 10 is band C: the top donor
            self.assertNotIn('id="elf_bow"', elf.decode("utf-8"))            # donors are never rewritten
            man = (md / "LOTRLOME_items" / "man" / self.gen.ITEMS_FILE_NAME).read_text(encoding="utf-8")
            self.assertEqual(man.count("<Item "), 3 + 6 + 2)                 # man_special Bow + man Bow + man Crossbow
            self.assertIn('id="ladder_man_xbow_t4"', man)
            self.assertIn('mesh="xbow"', man)
        donors = (self.armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml").read_bytes()
        self.assertTrue(donors.startswith(BOM))
        self.assertIn(b'thrust_damage="100"', donors)

    def test_apply_is_idempotent_and_verify_sees_drift_in_each_stat(self):
        self.assertEqual(self._run("--apply"), 0)
        first = self._elf_file().read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self._elf_file().read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)
        for old, new in ((b'missile_speed="84"', b'missile_speed="60"'),
                         (b'thrust_damage="154"', b'thrust_damage="999"'),
                         (b'accuracy="90"', b'accuracy="100"')):
            self._elf_file().write_bytes(first.replace(old, new))
            self.assertEqual(self._run("--verify"), 1, old)
        self.assertEqual(self._run("--apply"), 0)           # apply repairs it
        self.assertEqual(self._run("--verify"), 0)
        self._elf_file(self.mirror / "ModuleData").unlink()
        self.assertEqual(self._run("--verify"), 1)          # the mirror drifted

    def test_revert_removes_only_the_generated_files(self):
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self._run("--revert"), 0)
        self.assertFalse(self._elf_file().exists())
        self.assertFalse(self._elf_file(self.mirror / "ModuleData").exists())
        self.assertTrue((self.armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml").exists())

    def test_unregistered_folder_and_missing_donor_are_errors(self):
        bad = _spec()
        bad["lines"][0]["folder"] = "orphan"
        self.spec_path.write_text(json.dumps(bad), encoding="utf-8")
        self.assertEqual(self._run("--apply"), 2)
        self.assertFalse(self._elf_file().exists())
        bad = _spec()
        bad["lines"][2]["donor"]["Crossbow"] = "no_such"
        self.spec_path.write_text(json.dumps(bad), encoding="utf-8")
        self.assertEqual(self._run("--apply"), 2)
        self.assertFalse(self._elf_file().exists())

    def _write_loc(self, md, folder, extra=""):
        lang = md / "Languages"
        lang.mkdir(exist_ok=True)
        body = ("<?xml version='1.0' encoding='utf-8'?>\n<base type=\"string\">\n  <tags>\n"
                "    <tag language=\"English\"/>\n  </tags>\n  <strings>\n"
                "    <string id=\"old_key\" text=\"Old\"/>\n" + extra + "  </strings>\n</base>\n")
        (lang / f"loc_{folder}.xml").write_bytes(body.replace("\n", "\r\n").encode())

    def test_apply_registers_english_loc_rows_and_revert_removes_them(self):
        for md in (self.armory / "ModuleData", self.mirror / "ModuleData"):
            self._write_loc(md, "elf")
            self._write_loc(md, "man")
        self.assertEqual(self._run("--apply"), 0)
        for md in (self.armory / "ModuleData", self.mirror / "ModuleData"):
            raw = (md / "Languages" / "loc_elf.xml").read_bytes()
            self.assertFalse(raw.startswith(BOM))
            self.assertNotIn(b"\n<", raw.replace(b"\r\n", b""))
            text = raw.decode("utf-8")
            self.assertIn(self.gen.LOC_MARKER_START, text)
            self.assertIn('<string id="ladder_elf_bow_t10" text="[Elf] Longbow X"/>', text)
            self.assertIn('<string id="old_key" text="Old"/>', text)
            self.assertEqual(text.count("<string id="), 5)                    # 1 old + 4 tiers
            self.assertTrue((md / "Languages" / "loc_elf.xml.bak-rangedladder").exists())
            man = (md / "Languages" / "loc_man.xml").read_text(encoding="utf-8")
            self.assertEqual(man.count("<string id="), 12)                     # 1 old + 11 cells
        loc_elf = self.armory / "ModuleData" / "Languages" / "loc_elf.xml"
        first = loc_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(loc_elf.read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)
        loc_elf.write_bytes(first.replace(b'id="ladder_elf_bow_t10"', b'id="ladder_elf_bow_zz"'))
        self.assertEqual(self._run("--verify"), 1)
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self._run("--verify"), 0)
        self.assertEqual(self._run("--revert"), 0)
        text = loc_elf.read_text(encoding="utf-8")
        self.assertNotIn("ladder_", text)
        self.assertNotIn(self.gen.LOC_MARKER_START, text)
        self.assertIn('<string id="old_key" text="Old"/>', text)

    def test_missing_loc_template_is_a_warning_not_a_failure(self):
        self.assertEqual(self._run("--apply"), 0)
        self.assertFalse((self.armory / "ModuleData" / "Languages" / "loc_elf.xml").exists())

    def test_missing_mirror_is_reported_and_skipped(self):
        self.assertEqual(self._run("--apply", "--asset-repo", str(self.mirror / "nope")), 0)
        self.assertTrue(self._elf_file().exists())


# --------------------------------------------------------------------------- #
# The validator gates (tools/taom_schema.py)                                   #
# --------------------------------------------------------------------------- #
class ValidatorGateTests(unittest.TestCase):
    """The gate reads the REAL spec (tools/ranged_ladders.json) against a temporary ModuleData
    whose troop files carry real culture tokens, and a hand-built launcher registry."""

    def setUp(self):
        import taom_schema as ts
        self.ts = ts
        self._tmp = tempfile.TemporaryDirectory()
        self.md = Path(self._tmp.name) / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        for line in rl.load_spec()["lines"]:            # the real spec's files must exist on disk
            for culture in line.get("files") or []:
                (self.md / "troops" / f"troops_{culture}.xml").write_bytes(b"<NPCCharacters/>")
        self.schemas = ts.load_schemas(Path(__file__).resolve().parent.parent / "schemas")

    def tearDown(self):
        self._tmp.cleanup()

    def _regs(self, launchers):
        return self.ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                  cultures=set(), party_templates=set(), launchers=launchers)

    @staticmethod
    def _with_real_donors(launchers):
        """The real spec names real donors; a registry lacking them is a spec finding, not a
        pass, so the inversion tests hand the gate every donor with a placeholder speed."""
        out = dict(launchers)
        for line in rl.load_spec()["lines"]:
            for cls, donor in (line.get("donor") or {}).items():
                out.setdefault(donor, rl.Launcher(donor, cls, 70, 90, 80, donor, "w.xml", "bow"))
            for cls, per_band in (line.get("donor_by_band") or {}).items():
                for donor in per_band.values():
                    out.setdefault(donor, rl.Launcher(donor, cls, 70, 90, 80, donor, "w.xml", "bow"))
        return out

    def _gate(self, launchers):
        v = self.ts.Validator(self.md, self.schemas, self._regs(launchers))
        return [i for i in v._ranged_ladder_inversions()]

    def _troops(self, culture, *specs):
        body = "".join(_npc_xml(tid, level, [{"Item0": bow, "Item1": "arrows"}], culture)
                       for tid, level, bow in specs)
        (self.md / "troops" / f"troops_{culture}.xml").write_bytes(
            f"<NPCCharacters>{body}\n</NPCCharacters>".encode())

    def _fast_slow(self):
        launchers = self._with_real_donors(_launchers())
        launchers["fast"] = rl.Launcher("fast", "Bow", 80, 90, 80, "Fast", "w.xml", "bow")
        launchers["slow"] = rl.Launcher("slow", "Bow", 64, 90, 80, "Slow", "w.xml", "bow")
        return launchers

    def test_skipped_without_launchers(self):
        self._troops("dunland", ("dunland_a", 11, "fast"), ("dunland_b", 26, "slow"))
        self.assertEqual(self._gate({}), [])

    def test_tier_and_rank_inversions_are_reported_once_per_scope(self):
        launchers = self._fast_slow()
        self._troops("dunland", ("dunland_a", 11, "fast"), ("dunland_b", 26, "slow"), ("dunland_c", 31, "slow"))
        self._troops("mirkwood", ("mirkwood_a", 16, "slow"))     # alone at T3: nothing to compare
        self._troops("rohan", ("rohan_a", 11, "slow"))            # rank 10 at T2, slower than dunland_a (rank 11)
        issues = self._gate(launchers)
        self.assertEqual({i.code for i in issues}, {"RANGED_LADDER_INVERSION"})
        self.assertTrue(all(i.severity == self.ts.Severity.WARNING for i in issues))
        messages = [i.message for i in issues]
        self.assertEqual([i.entry_id for i in issues], ["dunland_a", "dunland_a"])
        self.assertTrue(any("inside line" in m and "speed" in m and "2 such pair" in m for m in messages))
        self.assertTrue(any("at tier 2" in m and "rohan" in m for m in messages))
        self.assertTrue(all(i.file.startswith("troops/troops_") for i in issues))

    def test_clean_rosters_pass_and_exempt_troops_are_skipped(self):
        launchers = self._fast_slow()
        self._troops("dunland", ("dunland_a", 11, "slow"), ("dunland_b", 26, "fast"))
        self.assertEqual(self._gate(launchers), [])
        self._troops("dunland", ("dunland_a", 11, "fast"), ("dunland_b", 26, "slow"))
        self.assertEqual(len(self._gate(launchers)), 1)
        old = dict(self.ts.Validator._RANGED_LADDER_EXEMPT)
        try:
            self.ts.Validator._RANGED_LADDER_EXEMPT["dunland_a"] = "test"
            self.assertEqual(self._gate(launchers), [])
        finally:
            self.ts.Validator._RANGED_LADDER_EXEMPT.clear()
            self.ts.Validator._RANGED_LADDER_EXEMPT.update(old)

    def test_a_troop_at_an_unlisted_tier_is_a_finding(self):
        launchers = self._fast_slow()
        self._troops("dunland", ("dunland_hi", 41, "slow"))       # dunland lists no T8
        issues = self._gate(launchers)
        self.assertEqual([i.entry_id for i in issues], ["dunland_hi"])
        self.assertIn("tier 8", issues[0].message)

    def test_a_hero_launcher_above_the_ceiling_is_a_finding(self):
        launchers = self._fast_slow()
        launchers["big_bow"] = rl.Launcher("big_bow", "Bow", 90, 99, 150, "Big", "w.xml", "bow")
        (self.md / "characters").mkdir()
        (self.md / "characters" / "lords.xml").write_bytes(
            b'<NPCCharacters><NPCCharacter id="lord_x" occupation="Lord"><equipment slot="Item0" id="Item.big_bow"/>'
            b'</NPCCharacter></NPCCharacters>')
        issues = self._gate(launchers)
        self.assertEqual([(i.code, i.entry_id) for i in issues], [("RANGED_DAMAGE_CEILING", "big_bow")])
        self.assertIn("lord_x", issues[0].message)

    def test_a_troop_file_that_does_not_parse_is_a_finding_not_a_pass(self):
        (self.md / "troops" / "troops_dunland.xml").write_bytes(b"<NPCCharacters><NPCCharacter id=")
        issues = self._gate(self._with_real_donors(_launchers()))
        self.assertEqual([i.entry_id for i in issues], ["(file)"])
        self.assertIn("troops_dunland.xml", issues[0].message)
        self.assertIn("not checked", issues[0].message)

    def test_a_mounted_troop_with_a_barred_usage_is_a_finding(self):
        launchers = self._with_real_donors(_launchers())
        self._troops("dunland", ("dunland_h", 21, "long_bow_item"))
        (self.md / "troops" / "troops_dunland.xml").write_bytes(
            (self.md / "troops" / "troops_dunland.xml").read_bytes().replace(b'default_group="Ranged"', b'default_group="HorseArcher"'))
        v = self.ts.Validator(self.md, self.schemas, self._regs(launchers))
        v.reg.mount_barred_usages = {"long_bow"}
        issues = [i for i in v._ranged_ladder_inversions()]
        self.assertEqual([(i.code, i.entry_id) for i in issues], [("RANGED_MOUNT_USAGE", "dunland_h")])
        self.assertIn("long_bow", issues[0].message)

    def test_unclaimed_file_is_a_finding(self):
        self._troops("nobody", ("nobody_a", 11, "man_bow"))
        issues = self._gate(self._with_real_donors(_launchers()))
        self.assertEqual([i.entry_id for i in issues], ["nobody_a"])
        self.assertIn("no line", issues[0].message)

    def test_spec_that_contradicts_the_index_is_a_finding_not_a_pass(self):
        self._troops("dunland", ("dunland_a", 11, "man_bow"))
        issues = self._gate(_launchers())
        self.assertEqual([i.entry_id for i in issues], ["(spec)"])
        self.assertIn("contradicts", issues[0].message)

    def test_live_registry_builder_indexes_launchers_only_with_an_install(self):
        regs = self.ts.build_registries(self.md, None)
        self.assertEqual(regs.launchers, {})
        self.assertIsNone(regs.mount_barred_usages)

    def test_an_install_with_no_launchers_is_a_suspect_registry_not_a_pass(self):
        modules = Path(self._tmp.name) / "Modules"
        (modules / "SandBoxCore" / "ModuleData" / "items").mkdir(parents=True)
        regs = self.ts.build_registries(self.md, modules)
        self.assertEqual(regs.launchers, {})
        self.assertTrue(any("launchers" in s for s in regs.suspect_registries))


if __name__ == "__main__":
    unittest.main()
