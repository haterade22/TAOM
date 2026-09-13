#!/usr/bin/env python3
"""Unit tests for the ranged range ladder: tools/ranged_ladder.py (library),
tools/generate_ranged_ladder_items.py (the ladder_* items) and
tools/rebalance_ranged_ladders.py (the roster rewrite). Issue #582.

Run:  python -m unittest discover -s tools/tests -p "test_ranged_ladder.py"

Synthetic data only, no game install needed: launchers and troops live in temporary trees.

THE CONTRACT
------------
An archer's reach is its launcher's missile_speed and nothing else. Two rules, per launcher
class: inside a line a lower tier is never faster than a higher tier; inside a band a
better-ranked line is never slower than a worse-ranked one. Both are encoded as one grid,
speed = band_base[band] + rank_step * (n_lines - rank), and every cell is a generated
ladder_<line>_<bow|xbow>_<band> item cloned from the line's own donor bow.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ranged_ladder as rl  # noqa: E402

BOM = b"\xef\xbb\xbf"


def _spec(**over):
    spec = {
        "bands": {"E": [0, 2], "R": [3, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]},
        "band_base": {"E": 58, "R": 62, "V": 66, "X": 70, "C": 74},
        "rank_step": 2,
        "lines": [
            {"id": "elf", "folder": "elf", "files": ["elf", "elf_twin"],
             "donor": {"Bow": "elf_bow"}, "donor_by_band": {"Bow": {"C": "elf_bow_top"}}},
            {"id": "man_special", "folder": "man", "files": ["man"], "prefixes": ["man_sp_"],
             "donor": {"Bow": "man_bow"}},
            {"id": "man", "folder": "man", "files": ["man"],
             "donor": {"Bow": "man_bow", "Crossbow": "man_xbow"}},
        ],
    }
    spec.update(over)
    return spec


def _launchers():
    def mk(i, c, s):
        return rl.Launcher(id=i, cls=c, speed=s, accuracy=90, damage=80, name=f"[Test] {i}",
                           file="weapons.xml")
    return {i: mk(i, c, s) for i, c, s in (
        ("elf_bow", "Bow", 95), ("elf_bow_top", "Bow", 100), ("man_bow", "Bow", 80),
        ("man_xbow", "Crossbow", 75), ("hunting_bow", "Bow", 64),
        ("ladder_man_bow_e", "Bow", 60), ("ladder_man_bow_r", "Bow", 64),
    )}


def _troop(tid, culture, level, sets, upgrades=(), group="Ranged"):
    return rl.RangedTroop(id=tid, file=f"troops_{culture}.xml", culture=culture, level=level,
                          tier=rl.engine_tier(level), group=group, sets=list(sets),
                          upgrades=list(upgrades), skills={})


def _npc_xml(tid, level, sets, culture_id="man"):
    rosters = ""
    for st in sets:
        civ = ' civilian="true"' if st.get("_civilian") else ""
        eqs = "".join(f'\n        <equipment slot="{s}" id="Item.{i}" />'
                      for s, i in st.items() if s != "_civilian")
        rosters += f"\n      <EquipmentRoster{civ}>{eqs}\n      </EquipmentRoster>"
    return (f'\n  <NPCCharacter id="{tid}" level="{level}" default_group="Ranged" '
            f'culture="Culture.{culture_id}" name="{{=t_{tid}}}{tid}">'
            f'\n    <skills>\n      <skill id="Bow" value="100" />\n    </skills>'
            f'\n    <Equipments>{rosters}\n    </Equipments>\n  </NPCCharacter>')


# --------------------------------------------------------------------------- #
# Spec, bands, grid                                                             #
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

    def test_grid_is_monotone_in_both_directions(self):
        spec = _spec()
        # elf is rank 1 of 3: bonus 2 * (3 - 1) = 4
        self.assertEqual(rl.grid_speed("elf", "E", spec), 62)
        self.assertEqual(rl.grid_speed("elf", "C", spec), 78)
        self.assertEqual(rl.grid_speed("man", "E", spec), 58)
        self.assertEqual(rl.grid_speed("man_special", "R", spec), 64)
        self.assertGreater(rl.grid_speed("man", "R", spec), rl.grid_speed("man", "E", spec))
        self.assertGreater(rl.grid_speed("elf", "R", spec), rl.grid_speed("man", "R", spec))

    def test_ladder_id_shape(self):
        self.assertEqual(rl.ladder_id("man_special", "Bow", "X"), "ladder_man_special_bow_x")
        self.assertEqual(rl.ladder_id("man", "Crossbow", "E"), "ladder_man_xbow_e")

    def test_validate_spec_accepts_the_fixture(self):
        self.assertEqual(rl.validate_spec(_spec()), [])

    def test_validate_spec_rejects_non_monotone_bases_and_bad_shapes(self):
        bad = _spec(band_base={"E": 62, "R": 58, "V": 66, "X": 70, "C": 74})
        self.assertTrue(any("band_base" in p for p in rl.validate_spec(bad)))
        dup = _spec()
        dup["lines"].append(dict(dup["lines"][0]))
        self.assertTrue(any("duplicate" in p for p in rl.validate_spec(dup)))
        gap = _spec(bands={"E": [0, 2], "R": [4, 4], "V": [5, 6], "X": [7, 8], "C": [9, 10]})
        self.assertTrue(any("tier 3" in p for p in rl.validate_spec(gap)))
        zero = _spec(rank_step=0)
        self.assertTrue(any("rank_step" in p for p in rl.validate_spec(zero)))
        noclass = _spec()
        noclass["lines"][2]["donor"] = {"Sling": "x"}
        self.assertTrue(any("Sling" in p for p in rl.validate_spec(noclass)))

    def test_validate_spec_reports_a_non_numeric_base_instead_of_raising(self):
        bad = _spec(band_base={"E": "low", "R": 62, "V": 66, "X": 70, "C": 74})
        problems = rl.validate_spec(bad)   # must not raise (deep review 2026-09-12, tooling agent)
        self.assertTrue(any("band_base" in p and "integer" in p for p in problems))

    def test_validate_spec_reports_prefixes_that_overlap_across_lines(self):
        spec = _spec()
        spec["lines"].insert(0, {"id": "man_sp_elite", "folder": "man", "files": ["man"],
                                 "prefixes": ["man_sp_ranger_"], "donor": {"Bow": "man_bow"}})
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
        problems = rl.validate_spec(spec, launchers=_launchers())
        self.assertTrue(any("no_such_bow" in p for p in problems))
        wrong_class = _spec()
        wrong_class["lines"][2]["donor"]["Crossbow"] = "man_bow"
        problems = rl.validate_spec(wrong_class, launchers=_launchers())
        self.assertTrue(any("man_bow" in p and "Crossbow" in p for p in problems))

    def test_load_spec_reads_the_repo_spec_and_it_validates(self):
        spec = rl.load_spec()
        self.assertEqual(rl.validate_spec(spec), [])
        self.assertEqual([ln["id"] for ln in spec["lines"]][:3],
                         ["mirkwood", "rivendell", "gondor_special"])
        self.assertEqual(len(spec["lines"]), 18)


# --------------------------------------------------------------------------- #
# Lines                                                                         #
# --------------------------------------------------------------------------- #
class LineTests(unittest.TestCase):
    def test_prefix_claims_before_file(self):
        spec = _spec()
        self.assertEqual(rl.line_of(_troop("man_sp_ranger", "man", 36, []), spec), "man_special")
        self.assertEqual(rl.line_of(_troop("man_archer", "man", 21, []), spec), "man")

    def test_shared_file_maps_to_the_owning_line(self):
        spec = _spec()
        self.assertEqual(rl.line_of(_troop("twin_archer", "elf_twin", 21, []), spec), "elf")

    def test_unclaimed_file_is_none(self):
        self.assertIsNone(rl.line_of(_troop("x", "nobody", 21, []), _spec()))

    def test_rank_is_position_in_lines(self):
        spec = _spec()
        self.assertEqual([rl.rank_of(i, spec) for i in ("elf", "man_special", "man")], [1, 2, 3])


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

    def test_load_ranged_troops_keeps_weapon_slots_and_skips_civilian_sets(self):
        md = self.root / "ModuleData"
        (md / "troops").mkdir(parents=True)
        (md / "characters").mkdir()
        body = _npc_xml("man_archer", 21, [
            {"Item0": "man_bow", "Item1": "arrows_a", "Head": "cap_a"},
            {"Item0": "sword_a", "Item2": "man_bow", "Item3": "arrows_a"},
            {"_civilian": True, "Item0": "hunting_bow"},
        ]) + _npc_xml("man_footman", 16, [{"Item0": "sword_a"}])
        (md / "troops" / "troops_man.xml").write_bytes(
            BOM + f"<NPCCharacters>{body}\n</NPCCharacters>".encode())
        (md / "characters" / "npcs_man.xml").write_bytes(
            b"<NPCCharacters>" + _npc_xml("villager_man", 6, [{"Item0": "hunting_bow"}]).encode()
            + b"</NPCCharacters>")
        troops = rl.load_ranged_troops(md)
        self.assertEqual(set(troops), {"man_archer", "man_footman"})  # villagers are not on the ladder
        t = troops["man_archer"]
        self.assertEqual((t.culture, t.level, t.tier, t.group, t.skills.get("Bow")),
                         ("man", 21, 4, "Ranged", 100))
        self.assertEqual(t.sets, [{"Item0": "man_bow", "Item1": "arrows_a"},
                                  {"Item0": "sword_a", "Item2": "man_bow", "Item3": "arrows_a"}])

    def test_troop_speed_is_the_max_over_sets_per_class(self):
        idx = _launchers()
        t = _troop("t", "man", 21, [{"Item0": "man_bow"}, {"Item0": "hunting_bow", "Item2": "man_xbow"}])
        self.assertEqual(rl.troop_speed(t, idx, "Bow"), 80)
        self.assertEqual(rl.troop_speed(t, idx, "Crossbow"), 75)
        self.assertIsNone(rl.troop_speed(_troop("u", "man", 21, [{"Item0": "sword"}]), idx, "Bow"))


# --------------------------------------------------------------------------- #
# The two rules                                                                 #
# --------------------------------------------------------------------------- #
class InversionTests(unittest.TestCase):
    def test_tier_inversion_inside_a_line(self):
        idx = _launchers()
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),      # T2 at 80
            "man_b": _troop("man_b", "man", 26, [{"Item0": "hunting_bow"}]),  # T5 at 64
        }
        found = rl.inversions(troops, idx, _spec())
        self.assertEqual([(f.kind, f.cls, f.low, f.high, f.low_speed, f.high_speed) for f in found],
                         [("tier", "Bow", "man_a", "man_b", 80, 64)])

    def test_rank_inversion_inside_a_band(self):
        idx = _launchers()
        troops = {
            "elf_a": _troop("elf_a", "elf", 21, [{"Item0": "hunting_bow"}]),  # rank 1, R, 64
            "man_a": _troop("man_a", "man", 16, [{"Item0": "man_bow"}]),      # rank 3, R, 80
        }
        found = rl.inversions(troops, idx, _spec())
        self.assertEqual([(f.kind, f.low, f.high, f.low_speed, f.high_speed) for f in found],
                         [("rank", "man_a", "elf_a", 80, 64)])

    def test_ties_pass_and_classes_never_compare(self):
        idx = _launchers()
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),   # Bow 80, T2
            "man_b": _troop("man_b", "man", 26, [{"Item0": "man_bow"}]),   # Bow 80, T5: tie
            "man_x": _troop("man_x", "man", 36, [{"Item0": "man_xbow"}]),  # Crossbow 75, T7: other class
            "elf_a": _troop("elf_a", "elf", 11, [{"Item0": "man_bow"}]),   # rank 1, E, 80: tie with man_a
        }
        self.assertEqual(rl.inversions(troops, idx, _spec()), [])

    def test_unassigned_ranged_troops_are_listed_not_dropped(self):
        idx = _launchers()
        troops = {"x": _troop("x", "nobody", 21, [{"Item0": "man_bow"}]),
                  "y": _troop("y", "nobody", 21, [{"Item0": "sword"}])}
        self.assertEqual([t.id for t in rl.unassigned(troops, idx, _spec())], ["x"])

    def test_summary_groups_by_kind_class_and_scope_with_the_worst_pair(self):
        idx = _launchers()
        troops = {
            "man_a": _troop("man_a", "man", 11, [{"Item0": "man_bow"}]),      # 80 T2
            "man_b": _troop("man_b", "man", 26, [{"Item0": "hunting_bow"}]),  # 64 T5
            "man_c": _troop("man_c", "man", 31, [{"Item0": "ladder_man_bow_e"}]),  # 60 T6
        }
        groups = rl.summarize(rl.inversions(troops, idx, _spec()))
        self.assertEqual(len(groups), 1)
        g = groups[0]
        self.assertEqual((g.kind, g.cls, g.scope, g.count), ("tier", "Bow", "man", 3))  # a>b, a>c, b>c
        self.assertEqual((g.worst.low, g.worst.high), ("man_a", "man_c"))


# --------------------------------------------------------------------------- #
# Planning                                                                      #
# --------------------------------------------------------------------------- #
class PlanTests(unittest.TestCase):
    def test_planned_items_cover_every_band_of_every_declared_class(self):
        items = rl.planned_items(_spec())
        ids = [i.id for i in items]
        self.assertEqual(len(items), 5 * 4)  # elf Bow, man_special Bow, man Bow, man Crossbow
        self.assertIn("ladder_elf_bow_c", ids)
        self.assertIn("ladder_man_xbow_e", ids)
        top = next(i for i in items if i.id == "ladder_elf_bow_c")
        self.assertEqual((top.donor, top.speed, top.folder, top.line, top.cls, top.band),
                         ("elf_bow_top", 78, "elf", "elf", "Bow", "C"))
        mid = next(i for i in items if i.id == "ladder_elf_bow_r")
        self.assertEqual(mid.donor, "elf_bow")

    def test_planned_edits_rewrite_every_launcher_slot_and_nothing_else(self):
        idx = _launchers()
        troops = {
            "man_a": _troop("man_a", "man", 21, [{"Item0": "man_bow", "Item1": "arrows"},
                                                {"Item0": "hunting_bow", "Item2": "man_xbow", "Item3": "bolts"}]),
            "man_sp_r": _troop("man_sp_r", "man", 46, [{"Item0": "man_bow"}]),
            "man_done": _troop("man_done", "man", 16, [{"Item0": "ladder_man_bow_r"}]),
            "elf_x": _troop("elf_x", "elf_twin", 26, [{"Item0": "elf_bow"}]),
        }
        edits = rl.planned_edits(troops, idx, _spec())
        by = {(e.troop, e.slot, e.old): e.new for e in edits}
        self.assertEqual(by, {
            ("man_a", "Item0", "man_bow"): "ladder_man_bow_r",
            ("man_a", "Item0", "hunting_bow"): "ladder_man_bow_r",
            ("man_a", "Item2", "man_xbow"): "ladder_man_xbow_r",
            ("man_sp_r", "Item0", "man_bow"): "ladder_man_special_bow_c",
            ("elf_x", "Item0", "elf_bow"): "ladder_elf_bow_v",
        })
        self.assertTrue(all(e.file.endswith(".xml") for e in edits))

    def test_planned_edits_refuse_a_class_the_line_does_not_declare(self):
        idx = _launchers()
        troops = {"elf_x": _troop("elf_x", "elf", 26, [{"Item0": "man_xbow"}])}
        with self.assertRaises(rl.LadderError):
            rl.planned_edits(troops, idx, _spec())

    def test_flight_range_grows_with_speed(self):
        r = [rl.flight_range(v) for v in (58, 74, 90, 108)]
        self.assertEqual(r, sorted(r))
        self.assertTrue(190 < r[0] < 230)
        self.assertTrue(360 < r[3] < 410)


# --------------------------------------------------------------------------- #
# tools/rebalance_ranged_ladders.py                                             #
# --------------------------------------------------------------------------- #
class RebalanceToolTests(unittest.TestCase):
    """A fake install: a two-line spec, one Armory weapons file, one vanilla items file, one
    troop file with BOM + CRLF. The tool reads only through the library."""

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
        body = (_npc_xml("man_militia", 11, [{"Item0": "hunting_bow", "Item1": "arrows"}, {"Item0": "man_bow", "Item1": "arrows"}])
                + _npc_xml("man_archer", 21, [{"Item0": "hunting_bow", "Item1": "arrows", "Head": "cap"}])  # T4 slower than the T2 militia: a tier inversion
                + _npc_xml("man_xbowman", 26, [{"Item0": "man_xbow", "Item1": "bolts"}])
                + _npc_xml("man_sp_ranger", 46, [{"Item0": "hunting_bow", "Item1": "arrows"}])
                + _npc_xml("man_footman", 16, [{"Item0": "sword"}]))
        self.troop_file = self.md / "troops" / "troops_man.xml"
        self.troop_file.write_bytes(BOM + ("<NPCCharacters>" + body + "\n</NPCCharacters>\n").replace("\n", "\r\n").encode())
        (self.md / "troops" / "troops_elf.xml").write_bytes(
            ("<NPCCharacters>" + _npc_xml("elf_archer", 26, [{"Item0": "elf_bow", "Item1": "arrows"}], "elf") + "\n</NPCCharacters>\n").encode())
        (self.md / "troops" / "troops_elf_twin.xml").write_bytes(b"<NPCCharacters/>")   # claimed by the spec, must exist
        self.spec_path = root / "spec.json"
        import json
        self.spec_path.write_text(json.dumps(_spec()), encoding="utf-8")
        self.report_dir = root / "reports"

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return self.rr.main(["--game-modules", str(self.modules), "--moduledata", str(self.md),
                             "--spec", str(self.spec_path), "--report-dir", str(self.report_dir),
                             "--docs-html", str(self.report_dir / "docs" / "ranged-troops.html"), *extra])

    def test_dry_run_writes_the_report_and_touches_nothing(self):
        before = self.troop_file.read_bytes()
        self.assertEqual(self._run(), 0)
        self.assertEqual(self.troop_file.read_bytes(), before)
        md = (self.report_dir / "REPORT.md").read_text(encoding="utf-8")
        self.assertIn("ladder_man_bow_r", md)
        self.assertIn("man_sp_ranger", md)
        self.assertIn("| man |", md)            # the grid table names every line
        self.assertIn("inversions", md.lower())
        import json
        data = json.loads((self.report_dir / "ranged-ladders.json").read_text(encoding="utf-8"))
        self.assertEqual(data["edits_pending"], 6)   # militia x2 old ids, archer, xbowman, ranger, elf
        self.assertGreater(data["inversions_before"], 0)
        self.assertEqual(data["inversions_after"], 0)

    def test_dry_run_writes_the_html_report_per_kingdom(self):
        self.assertEqual(self._run(), 0)
        html = (self.report_dir / "REPORT.html").read_text(encoding="utf-8")
        self.assertIn("<title>", html)
        self.assertIn('id="kingdom-man"', html)               # one section per line, in rank order
        self.assertLess(html.index('id="kingdom-elf"'), html.index('id="kingdom-man"'))
        self.assertIn("man_xbowman", html)
        self.assertIn("man_sp_ranger", html)                   # the special line has its own section
        self.assertIn('id="kingdom-man_special"', html)
        self.assertIn("Crossbow", html)
        self.assertNotIn("—", html)                      # no long dashes in the prose
        self.assertIn("prefers-color-scheme", html)            # both themes
        tracked = (self.report_dir / "docs" / "ranged-troops.html").read_text(encoding="utf-8")
        self.assertEqual(tracked, html)                        # the tracked copy is the same document

    def test_apply_refuses_while_the_ladder_items_do_not_exist(self):
        before = self.troop_file.read_bytes()
        self.assertEqual(self._run("--apply"), 2)
        self.assertEqual(self.troop_file.read_bytes(), before)

    def _write_ladder_items(self):
        import ranged_ladder as rl2
        spec = rl2.load_spec(self.spec_path)
        rows = "".join(
            f'\n  <Item id="{i.id}" name="x" Type="{i.cls}"><ItemComponent><Weapon weapon_class="{i.cls}" '
            f'missile_speed="{i.speed}" accuracy="90" thrust_damage="80"/></ItemComponent></Item>'
            for i in rl2.planned_items(spec))
        (self.modules / "LOTRLOME_Armory" / "ModuleData" / "LOTRLOME_items" / "ranged_ladder.xml").write_bytes(
            f"<Items>{rows}\n</Items>".encode())

    def test_apply_rewrites_every_launcher_slot_byte_faithfully_and_is_idempotent(self):
        self._write_ladder_items()
        self.assertEqual(self._run("--apply"), 0)
        raw = self.troop_file.read_bytes()
        self.assertTrue(raw.startswith(BOM))
        self.assertIn(b"\r\n", raw)
        self.assertNotIn(b"\n<", raw.replace(b"\r\n", b""))  # every newline is still CRLF
        text = raw.decode("utf-8-sig")
        self.assertEqual(text.count('id="Item.ladder_man_bow_e"'), 2)     # both militia sets
        self.assertIn('id="Item.ladder_man_bow_r"', text)                  # archer T4
        self.assertIn('id="Item.ladder_man_xbow_v"', text)                 # crossbowman T5
        self.assertIn('id="Item.ladder_man_special_bow_c"', text)          # ranger T9
        self.assertEqual(text.count('id="Item.arrows"'), 4)                # ammo untouched
        self.assertIn('id="Item.bolts"', text)
        self.assertIn('id="Item.sword"', text)
        self.assertIn('slot="Head" id="Item.cap"', text)
        self.assertNotIn("man_bow", text.replace("ladder_man_bow", ""))
        second = self.troop_file.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self.troop_file.read_bytes(), second)
        import json
        data = json.loads((self.report_dir / "ranged-ladders.json").read_text(encoding="utf-8"))
        self.assertEqual((data["edits_pending"], data["inversions_before"]), (0, 0))

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
        self.mirror = root / "assets" / "v1.4" / "LOTRLOME_Armory"
        (self.mirror / "ModuleData" / "LOTRLOME_items" / "elf").mkdir(parents=True)
        (self.mirror / "ModuleData" / "LOTRLOME_items" / "man").mkdir(parents=True)
        self.md = root / "ModuleData"
        (self.md / "troops").mkdir(parents=True)
        for culture in ("elf", "elf_twin", "man"):   # the spec's files tokens must exist on disk
            (self.md / "troops" / f"troops_{culture}.xml").write_bytes(b"<NPCCharacters/>")
        import json
        self.spec_path = root / "spec.json"
        self.spec_path.write_text(json.dumps(_spec()), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def _run(self, *extra):
        return self.gen.main(["--game-modules", str(self.modules), "--asset-repo", str(self.mirror),
                              "--spec", str(self.spec_path), "--moduledata", str(self.md), *extra])

    def test_a_files_token_with_no_troop_file_is_refused(self):
        (self.md / "troops" / "troops_elf_twin.xml").unlink()
        self.assertEqual(self._run("--apply"), 2)
        self.assertFalse(self._elf_file().exists())

    def _elf_file(self, md=None):
        return (md or self.armory / "ModuleData") / "LOTRLOME_items" / "elf" / self.gen.ITEMS_FILE_NAME

    def test_ladder_name_strips_the_donor_numeral_and_suffixes(self):
        n = self.gen.ladder_name
        self.assertEqual(n("{=e}[Elf] Longbow III", "ladder_elf_bow_x", "X"), "{=ladder_elf_bow_x}[Elf] Longbow IV")
        self.assertEqual(n("[Man] Bow - Starting ", "ladder_man_bow_e", "E"), "{=ladder_man_bow_e}[Man] Bow I")
        self.assertEqual(n("[Mirkwood] LongBow II - Horse", "ladder_x_bow_v", "V"), "{=ladder_x_bow_v}[Mirkwood] LongBow III")
        self.assertEqual(n("Arbalest", "ladder_man_xbow_c", "C"), "{=ladder_man_xbow_c}Arbalest V")

    def test_clone_keeps_everything_but_id_name_speed_and_merchandise(self):
        import xml.etree.ElementTree as ET
        donor = ET.fromstring('<Item id="elf_bow" name="{=e}[Elf] Longbow III" Type="Bow" is_merchandise="true" value="900" mesh="m" culture="Culture.elf">'
                              '<ItemComponent><Weapon weapon_class="Bow" missile_speed="95" accuracy="100" thrust_damage="100" ammo_limit="1"/></ItemComponent>'
                              '<Flags UseTeamColor="true"/></Item>')
        item = rl.LadderItem(id="ladder_elf_bow_r", line="elf", cls="Bow", band="R", speed=66, donor="elf_bow", folder="elf")
        out = self.gen.clone_launcher(donor, item)
        self.assertEqual(out.get("id"), "ladder_elf_bow_r")
        self.assertEqual(out.get("name"), "{=ladder_elf_bow_r}[Elf] Longbow II")
        self.assertEqual(out.get("is_merchandise"), "false")
        self.assertEqual((out.get("Type"), out.get("mesh"), out.get("culture"), out.get("value")), ("Bow", "m", "Culture.elf", "900"))
        w = out.find("ItemComponent/Weapon")
        self.assertEqual((w.get("missile_speed"), w.get("accuracy"), w.get("thrust_damage"), w.get("ammo_limit")), ("66", "100", "100", "1"))
        self.assertIsNotNone(out.find("Flags"))
        self.assertEqual(donor.get("id"), "elf_bow")  # the donor is untouched

    def test_dry_run_writes_nothing_and_apply_writes_both_trees(self):
        self.assertEqual(self._run(), 0)
        self.assertFalse(self._elf_file().exists())
        self.assertEqual(self._run("--apply"), 0)
        for md in (self.armory / "ModuleData", self.mirror / "ModuleData"):
            elf = self._elf_file(md).read_bytes()
            self.assertTrue(elf.startswith(b'<?xml version="1.0" encoding="utf-8"?>'))
            self.assertIn(b"\r\n", elf)
            text = elf.decode("utf-8")
            self.assertIn('id="ladder_elf_bow_c"', text)
            self.assertIn('missile_speed="78"', text)      # elf C: 74 + 2 * 2
            self.assertIn('mesh="elf_top"', text)           # C band clones the top donor
            self.assertNotIn('id="elf_bow"', text)          # donors are never rewritten
            man = (md / "LOTRLOME_items" / "man" / self.gen.ITEMS_FILE_NAME).read_text(encoding="utf-8")
            self.assertEqual(man.count("<Item "), 15)        # man_special Bow + man Bow + man Crossbow, 5 bands each
            self.assertIn('id="ladder_man_xbow_e"', man)
            self.assertIn('mesh="xbow"', man)               # a vanilla donor clones too
        donors = (self.armory / "ModuleData" / "LOTRLOME_items" / "LOTRAOM_weapons.xml").read_bytes()
        self.assertTrue(donors.startswith(BOM))
        self.assertIn(b'missile_speed="95"', donors)

    def test_apply_is_idempotent_and_verify_sees_drift(self):
        self.assertEqual(self._run("--apply"), 0)
        first = self._elf_file().read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(self._elf_file().read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)
        tampered = first.replace(b'missile_speed="78"', b'missile_speed="60"')
        self._elf_file().write_bytes(tampered)
        self.assertEqual(self._run("--verify"), 1)
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
        import json
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
            self.assertNotIn(b"\n<", raw.replace(b"\r\n", b""))            # CRLF kept throughout
            text = raw.decode("utf-8")
            self.assertIn(self.gen.LOC_MARKER_START, text)
            self.assertIn('<string id="ladder_elf_bow_c" text="[Elf] Longbow V"/>', text)
            self.assertIn('<string id="old_key" text="Old"/>', text)
            self.assertEqual(text.count("<string id="), 6)                    # 1 old + 5 bands
            self.assertTrue((md / "Languages" / "loc_elf.xml.bak-rangedladder").exists())
            man = (md / "Languages" / "loc_man.xml").read_text(encoding="utf-8")
            self.assertEqual(man.count("<string id="), 16)                     # 1 old + 15 cells
        loc_elf = self.armory / "ModuleData" / "Languages" / "loc_elf.xml"
        first = loc_elf.read_bytes()
        self.assertEqual(self._run("--apply"), 0)
        self.assertEqual(loc_elf.read_bytes(), first)
        self.assertEqual(self._run("--verify"), 0)
        loc_elf.write_bytes(first.replace(b'id="ladder_elf_bow_c"', b'id="ladder_elf_bow_zz"'))
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
# The validator gate (tools/taom_schema.py RANGED_LADDER_INVERSION)             #
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
        spec = rl.load_spec()
        for line in spec["lines"]:
            for cls, donor in (line.get("donor") or {}).items():
                out.setdefault(donor, rl.Launcher(donor, cls, 70, 90, 80, donor, "w.xml"))
            for cls, per_band in (line.get("donor_by_band") or {}).items():
                for donor in per_band.values():
                    out.setdefault(donor, rl.Launcher(donor, cls, 70, 90, 80, donor, "w.xml"))
        return out

    def _gate(self, launchers):
        v = self.ts.Validator(self.md, self.schemas, self._regs(launchers))
        return [i for i in v._ranged_ladder_inversions()]

    def _troops(self, culture, *specs):
        body = "".join(_npc_xml(tid, level, [{"Item0": bow, "Item1": "arrows"}], culture)
                       for tid, level, bow in specs)
        (self.md / "troops" / f"troops_{culture}.xml").write_bytes(
            f"<NPCCharacters>{body}\n</NPCCharacters>".encode())

    def test_skipped_without_launchers(self):
        self._troops("dunland", ("dunland_a", 11, "fast"), ("dunland_b", 26, "slow"))
        self.assertEqual(self._gate({}), [])

    def test_tier_and_rank_inversions_are_reported_once_per_scope(self):
        launchers = self._with_real_donors(_launchers())
        launchers["fast"] = rl.Launcher("fast", "Bow", 80, 90, 80, "Fast", "w.xml")
        launchers["slow"] = rl.Launcher("slow", "Bow", 64, 90, 80, "Slow", "w.xml")
        self._troops("dunland", ("dunland_a", 11, "fast"), ("dunland_b", 26, "slow"), ("dunland_c", 31, "slow"))
        self._troops("mirkwood", ("mirkwood_a", 16, "slow"))     # rank 1 at R, slower than dunland? no: R has no dunland
        self._troops("rohan", ("rohan_a", 11, "slow"))            # rank 17 at E, slower than dunland_a (rank 18): inversion
        issues = self._gate(launchers)
        codes = {i.code for i in issues}
        self.assertEqual(codes, {"RANGED_LADDER_INVERSION"})
        self.assertTrue(all(i.severity == self.ts.Severity.WARNING for i in issues))
        messages = [i.message for i in issues]
        self.assertEqual([i.entry_id for i in issues], ["dunland_a", "dunland_a"])  # worst of both groups
        self.assertTrue(any("inside line" in m and "2 such pair" in m for m in messages))
        self.assertTrue(any("inside band E" in m and "rohan" in m for m in messages))
        self.assertTrue(all(i.file.startswith("troops/troops_") for i in issues))

    def test_clean_rosters_pass_and_exempt_troops_are_skipped(self):
        launchers = self._with_real_donors(_launchers())
        launchers["fast"] = rl.Launcher("fast", "Bow", 80, 90, 80, "Fast", "w.xml")
        launchers["slow"] = rl.Launcher("slow", "Bow", 64, 90, 80, "Slow", "w.xml")
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

    def test_unclaimed_file_is_a_finding(self):
        launchers = self._with_real_donors(_launchers())
        self._troops("nobody", ("nobody_a", 11, "man_bow"))
        issues = self._gate(launchers)
        self.assertEqual([i.entry_id for i in issues], ["nobody_a"])
        self.assertIn("no line", issues[0].message)

    def test_spec_that_contradicts_the_index_is_a_finding_not_a_pass(self):
        # The real spec names real donors; a registry without them is "the install changed".
        self._troops("dunland", ("dunland_a", 11, "man_bow"))
        issues = self._gate(_launchers())
        self.assertEqual([i.entry_id for i in issues], ["(spec)"])
        self.assertIn("contradicts", issues[0].message)

    def test_live_registry_builder_indexes_launchers_only_with_an_install(self):
        regs = self.ts.build_registries(self.md, None)
        self.assertEqual(regs.launchers, {})


if __name__ == "__main__":
    unittest.main()
