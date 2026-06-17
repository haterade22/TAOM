#!/usr/bin/env python3
"""Unit tests for the battle-load triage tool (tools/triage_battle_load.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_triage_battle_load.py

Pure stdlib with synthetic `taom_debug.log` fixtures built in the REAL log format
(FileLogger prefix `[ts] [LEVEL]` + the `[BattleLoad]` phase markers / `slot=` dumps
/ watchdog line). The engine functions parse_battle_load_log / classify / triage are
called directly — no game install needed. The rgl cross-check reuses
validate_mesh_refs.parse_rgl_text (imported, not re-implemented).

Contract under test (one verdict class per block):
  - EQUIPMENT          : log ends at AgentEquipBegin with no matching AgentEquipOk
  - EQUIPMENT_CONFIRMED: + rgl_log has `get_object failed for body: <suspect>`
  - SCENE              : ends at MissionInitialize / BattleSceneSelected
  - PRE_SCENE          : ends at EncounterStart / MissionOpenNew
  - POST_EQUIP         : ends at AgentEquipOk (equipped fine, froze before playable)
  - COMPLETED          : a BattlePlayable phase is present
  - UNKNOWN            : no [BattleLoad] lines at all
  - watchdog line corroborates the terminal phase
  - FileLogger prefix is stripped (lines parse with and without it)
  - CLI exit codes (0 completed / 1 hang / 2 bad path)
"""
import os
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import triage_battle_load as tb  # noqa: E402
import validate_mesh_refs as vm  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "triage_battle_load.py"


def _line(level, payload, ts="2026-06-17 12:00:00"):
    """One FileLogger line: [ts] [LEVEL] <payload>."""
    return f"[{ts}] [{level}] {payload}"


# Reusable lifecycle fragments (real format) -------------------------------- #
def _encounter():
    return _line("INFO", "[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=42")


def _open_new():
    return _line("INFO", "[BattleLoad] seq=2 t=+12ms phase=MissionOpenNew "
                         "mission='Mission' scene='battle_terrain_b' attacker=gondor def=mordor")


def _scene_selected(scene="battle_terrain_b", idx=158):
    return _line("INFO", f"[BattleLoad] seq=3 t=+34ms phase=BattleSceneSelected "
                         f"mapIndex={idx} sceneId='{scene}' naval=False")


def _mission_init(scene="battle_terrain_b"):
    return _line("INFO", f"[BattleLoad] seq=4 t=+56ms phase=MissionInitialize scene='{scene}'")


def _equip_begin(seq, idx, name="Gondor Soldier", char="gondor_x", culture="gondor",
                 slots=2):
    return _line("INFO", f"[BattleLoad] seq={seq} t=+100ms phase=AgentEquipBegin "
                         f"agent#{idx} '{name}' char='{char}' culture='{culture}' slots={slots}")


def _slot(slot, item_id, bo="<null>", shield_bo="<null>", holster_bo="<null>",
          mesh="<null>", kind="Weapon"):
    return _line("DEBUG", f"[BattleLoad]   slot={slot} id={item_id} "
                          f"bo={bo} shieldBo={shield_bo} holsterBo={holster_bo} "
                          f"mesh={mesh} kind={kind}")


def _equip_ok(seq, idx, name="Gondor Soldier"):
    return _line("INFO", f"[BattleLoad] seq={seq} t=+120ms phase=AgentEquipOk agent#{idx} '{name}'")


def _playable(scene="battle_terrain_b", agents=120):
    return _line("INFO", f"[BattleLoad] seq=99 t=+8000ms phase=BattlePlayable "
                         f"scene='{scene}' agents={agents}")


def _watchdog(elapsed, last_phase, detail=""):
    tail = f"phase={last_phase} seq=5 {detail}".strip()
    return _line("ERROR", f"[BattleLoad] WATCHDOG STILL LOADING after {elapsed}s — last {tail}")


def _log(*lines):
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------- #
# Parsing                                                                      #
# --------------------------------------------------------------------------- #
class ParseTests(unittest.TestCase):
    def test_parses_phase_markers_in_order(self):
        text = _log(_encounter(), _open_new(), _scene_selected(), _mission_init())
        tl = tb.parse_battle_load_log(text)
        self.assertEqual([e.phase for e in tl.events],
                         ["EncounterStart", "MissionOpenNew", "BattleSceneSelected",
                          "MissionInitialize"])
        # seq + ms must land in the right capture groups (guards the seq=/t=+(\d+)ms order).
        self.assertEqual([e.seq for e in tl.events], [1, 2, 3, 4])
        self.assertEqual([e.ms for e in tl.events], [0, 12, 34, 56])

    def test_attaches_slot_dumps_to_owning_equip_begin(self):
        text = _log(
            _mission_init(),
            _equip_begin(5, 57, slots=2),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
        )
        tl = tb.parse_battle_load_log(text)
        begin = tl.events[-1]
        self.assertEqual(begin.phase, "AgentEquipBegin")
        self.assertEqual(len(begin.slots), 2)
        self.assertEqual(begin.slots[0].item_id, "gondor_sword_a")
        self.assertEqual(begin.slots[0].bo, "bo_gondor_sword_a")
        self.assertEqual(begin.slots[1].kind, "BodyArmor")

    def test_strips_filelogger_prefix_and_also_parses_bare_lines(self):
        # A line WITHOUT the [ts] [LEVEL] prefix must still parse (robustness).
        bare = "[BattleLoad] seq=4 t=+56ms phase=MissionInitialize scene='town_ES2'"
        text = _log(_encounter(), bare)
        tl = tb.parse_battle_load_log(text)
        self.assertEqual(tl.events[-1].phase, "MissionInitialize")

    def test_no_battleload_lines_yields_empty_timeline(self):
        text = _log(_line("INFO", "[SomeOtherFeature] hello"),
                    _line("DEBUG", "nothing to see"))
        tl = tb.parse_battle_load_log(text)
        self.assertEqual(tl.events, [])
        self.assertIsNone(tl.watchdog)

    def test_watchdog_line_is_captured(self):
        text = _log(_mission_init(), _equip_begin(5, 57),
                    _watchdog(300, "AgentEquipBegin", "agent#57 'Gondor Soldier'"))
        tl = tb.parse_battle_load_log(text)
        self.assertIsNotNone(tl.watchdog)
        self.assertEqual(tl.watchdog.elapsed_seconds, 300)
        self.assertEqual(tl.watchdog.last_phase, "AgentEquipBegin")

    def test_watchdog_phase_none_sentinel_parses(self):
        # A stall before the first phase Emit() leaves the C# sentinel "phase=<none>" (no seq).
        # _STATUS_RE must still yield a last_phase rather than blanking it.
        text = _log(_mission_init(),
                    _line("ERROR", "[BattleLoad] WATCHDOG STILL LOADING after 60s — last phase=<none>"))
        tl = tb.parse_battle_load_log(text)
        self.assertIsNotNone(tl.watchdog)
        self.assertEqual(tl.watchdog.last_phase, "<none>")

    def test_stray_slot_after_non_equip_begin_is_dropped(self):
        # A slot dump after a non-AgentEquipBegin phase (truncated/interleaved log) must NOT
        # attach to that event and must not crash.
        text = _log(_mission_init(),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"))
        tl = tb.parse_battle_load_log(text)
        self.assertEqual(tl.events[-1].phase, "MissionInitialize")
        self.assertEqual(tl.events[-1].slots, [])


# --------------------------------------------------------------------------- #
# Classification                                                               #
# --------------------------------------------------------------------------- #
class ClassifyTests(unittest.TestCase):
    def test_equipment_hang_when_begin_has_no_ok(self):
        text = _log(
            _encounter(), _open_new(), _scene_selected(), _mission_init(),
            _equip_begin(5, 57, name="Gondor Soldier", char="gondor_x", culture="gondor",
                         slots=2),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
        )
        v = tb.triage(text)
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertEqual(v.stuck_agent["index"], "57")
        self.assertEqual(v.stuck_agent["char"], "gondor_x")
        # Suspect names = every non-<null> mesh/body token on the stuck agent's slots.
        self.assertIn("bo_gondor_sword_a", v.suspect_names)
        self.assertIn("sk_gd_ano_body_a", v.suspect_names)

    def test_equipment_hang_picks_last_unmatched_agent(self):
        text = _log(
            _mission_init(),
            _equip_begin(5, 0), _equip_ok(6, 0),
            _equip_begin(7, 1), _equip_ok(8, 1),
            _equip_begin(9, 2, name="Boromir", char="boromir", slots=1),
            _slot("Body", "sk_gd_boromir_body", bo="bo_sk_gd_boromir_body", kind="BodyArmor"),
        )
        v = tb.triage(text)
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertEqual(v.stuck_agent["index"], "2")
        self.assertEqual(v.stuck_agent["name"], "Boromir")

    def test_completed_when_battleplayable_present(self):
        text = _log(_encounter(), _mission_init(),
                    _equip_begin(5, 0), _equip_ok(6, 0), _playable())
        v = tb.triage(text)
        self.assertEqual(v.kind, "COMPLETED")

    def test_scene_hang_when_terminal_is_mission_initialize(self):
        text = _log(_encounter(), _open_new(), _scene_selected("battle_terrain_158", 158),
                    _mission_init("battle_terrain_158"))
        v = tb.triage(text)
        self.assertEqual(v.kind, "SCENE")
        self.assertEqual(v.scene, "battle_terrain_158")

    def test_scene_hang_when_terminal_is_scene_selected(self):
        text = _log(_encounter(), _open_new(), _scene_selected("battle_terrain_158", 158))
        v = tb.triage(text)
        self.assertEqual(v.kind, "SCENE")
        self.assertEqual(v.scene, "battle_terrain_158")

    def test_pre_scene_hang_when_terminal_is_open_new(self):
        text = _log(_encounter(), _open_new())
        v = tb.triage(text)
        self.assertEqual(v.kind, "PRE_SCENE")

    def test_post_equip_stall_when_terminal_is_equip_ok(self):
        text = _log(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0))
        v = tb.triage(text)
        self.assertEqual(v.kind, "POST_EQUIP")

    def test_unknown_when_no_battleload_lines(self):
        text = _log(_line("INFO", "[Other] nothing"))
        v = tb.triage(text)
        self.assertEqual(v.kind, "UNKNOWN")

    def test_post_equip_keeps_apostrophe_in_agent_name(self):
        # Tolkien names contain apostrophes (e.g. "Sauron's Lieutenant"); the AgentEquipOk
        # detail regex must not truncate at the inner quote.
        text = _log(_mission_init(),
                    _equip_begin(5, 12, name="Sauron's Lieutenant"),
                    _equip_ok(6, 12, name="Sauron's Lieutenant"))
        v = tb.triage(text)
        self.assertEqual(v.kind, "POST_EQUIP")
        self.assertEqual(v.stuck_agent["name"], "Sauron's Lieutenant")

    def test_meshless_slot_is_flagged_in_notes(self):
        # A slot whose item declares NO mesh/body of any kind (all tokens <null>) is the
        # genuinely suspicious case — it must surface as a note. (classify-time behavior)
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "broken_placeholder_item"))  # all tokens default <null>
        v = tb.triage(text)
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertTrue(any("all tokens <null>" in n for n in v.notes),
                        f"expected a meshless-slot note, got {v.notes}")


# --------------------------------------------------------------------------- #
# rgl cross-check (reuses validate_mesh_refs.parse_rgl_text)                   #
# --------------------------------------------------------------------------- #
class RglCrossCheckTests(unittest.TestCase):
    def _equipment_log(self):
        return _log(
            _mission_init(),
            _equip_begin(5, 57, slots=2),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
        )

    def test_rgl_confirms_missing_body_upgrades_to_confirmed(self):
        rgl_text = (
            "Some engine noise\n"
            "get_object failed for body: bo_gondor_sword_a\n"
            "more noise\n"
        )
        v = tb.triage(self._equipment_log(), rgl_text=rgl_text)
        self.assertEqual(v.kind, "EQUIPMENT_CONFIRMED")
        self.assertIn("bo_gondor_sword_a", v.confirmed_assets)

    def test_rgl_without_matching_body_stays_equipment(self):
        # The engine failed a DIFFERENT body — not one of our suspects.
        rgl_text = "get_object failed for body: bo_some_other_thing\n"
        v = tb.triage(self._equipment_log(), rgl_text=rgl_text)
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertEqual(v.confirmed_assets, [])

    def test_rgl_parse_is_the_validate_mesh_refs_function(self):
        # Guard against drift: we reuse vm.parse_rgl_text, not a private copy.
        findings = vm.parse_rgl_text("get_object failed for body: bo_x\n")
        self.assertIn("bo_x", findings.missing_bodies)

    def test_rgl_confirms_missing_material_upgrades_to_confirmed(self):
        # The Body slot's visual mesh is sk_gd_ano_body_a; the engine logs the material missing
        # WITH a .lodN suffix — must still upgrade (lodN-normalized match, not raw intersection).
        rgl_text = "Unable to find material for mesh sk_gd_ano_body_a.lod0\n"
        v = tb.triage(self._equipment_log(), rgl_text=rgl_text)
        self.assertEqual(v.kind, "EQUIPMENT_CONFIRMED")
        self.assertIn("sk_gd_ano_body_a", v.confirmed_assets)


# --------------------------------------------------------------------------- #
# Reporting + CLI                                                              #
# --------------------------------------------------------------------------- #
class ReportTests(unittest.TestCase):
    def test_report_leads_with_verdict_line(self):
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"))
        v = tb.triage(text)
        report = tb.format_report(v, tb.parse_battle_load_log(text), None)
        self.assertIn("VERDICT:", report)
        self.assertIn("EQUIPMENT", report)


class PickerTests(unittest.TestCase):
    def test_pick_log_rejects_decoy_basename(self):
        self.assertIsNone(tb._pick_log(["not_taom_debug.log"]))
        self.assertEqual(tb._pick_log(["taom_debug_2026-06-17.log"]),
                         "taom_debug_2026-06-17.log")

    def test_pick_rejects_directory_component_match(self):
        # A directory named like the needle must NOT satisfy it (basename anchoring); the real
        # basenamed file must. Guards the rgl picker against `rgl_log_backup/unrelated.txt`.
        names = ["rgl_log_backup/unrelated.txt", "rgl_log_errors_2026.txt"]
        self.assertEqual(tb._pick(names, ("rgl_log_errors",)), "rgl_log_errors_2026.txt")
        self.assertIsNone(tb._pick(["rgl_log_backup/unrelated.txt"], ("rgl_log",)))


class BundleTests(unittest.TestCase):
    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_bundle_picks_real_log_over_decoy_and_prefers_rgl_errors(self):
        equip_log = _log(
            _mission_init(),
            _equip_begin(5, 57, slots=1),
            _slot("Cape", "wm_boromir_shield", bo="bo_cap_wm_boromir_shield", kind="Shield"))
        with tempfile.TemporaryDirectory() as d:
            zpath = Path(d) / "taom_crash_test.zip"
            with zipfile.ZipFile(zpath, "w") as z:
                z.writestr("not_taom_debug.log", "decoy — must NOT be picked\n")
                z.writestr("taom_debug_2026-06-17_12-00-00.log", equip_log)
                # plain rgl has no matching body; the errors variant confirms it — the
                # tool must prefer the errors file.
                z.writestr("rgl_log_2026.txt", "irrelevant noise\n")
                z.writestr("rgl_log_errors_2026.txt",
                           "get_object failed for body: bo_cap_wm_boromir_shield\n")
            r = self._run(["--bundle", str(zpath)])
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            # Real log parsed (not the decoy) AND the errors-variant rgl confirmed it.
            self.assertIn("EQUIPMENT_CONFIRMED", r.stdout)
            self.assertIn("bo_cap_wm_boromir_shield", r.stdout)


class CliTests(unittest.TestCase):
    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_cli_completed_exit_0(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0),
                              _playable()), encoding="utf-8")
            r = self._run([str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("COMPLETED", r.stdout)

    def test_cli_equipment_hang_exit_1(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_mission_init(), _equip_begin(5, 57, slots=1),
                              _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a")),
                         encoding="utf-8")
            r = self._run([str(p)])
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            self.assertIn("EQUIPMENT", r.stdout)

    def test_cli_bad_path_exit_2(self):
        r = self._run([str(Path("does_not_exist_xyz.log"))])
        self.assertEqual(r.returncode, 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
