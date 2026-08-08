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
import json
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

# The pinned [MemSample] log-line contract (issue #386). These literals are the
# cross-language twin pin: MemoryPressureSamplerTests.FormatSample_KnownValues_
# MatchesContractLine asserts the C# sampler emits EXACTLY these message bodies.
PINNED_MEM_SESSION = "[MemSample] session totalPhysMB=16296 sysCommitLimitMB=31646"
PINNED_MEM_PERIODIC = ("[MemSample] privMB=4211 wsMB=3900 heapMB=654 "
                       "sysCommitUsedMB=14003 sysCommitLimitMB=31646 "
                       "availPhysMB=6200 memLoad=61%")
PINNED_MEM_WARN = ("[MemSample] WARN LOW COMMIT HEADROOM headroomMB=1799 "
                   "privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=29847 "
                   "sysCommitLimitMB=31646 availPhysMB=310 memLoad=97%")

# The pinned FinishMissionLoadingBegin wait-token contract (2026-08-07). Cross-language
# twin pin: BattleLoadDiagnosticsService.FormatFinishWaitDetail is asserted
# character-for-character on the C# side by
# FormatFinishWaitDetail_WithWait_ProducesPinnedLiteral / ..._WithoutWait_...  — waitMs is
# OMITTED when MissionInitializeDone was not observed, never rendered as a fabricated 0.
PINNED_WAIT_WITH_MS = "polls=87 waitMs=1449"
PINNED_WAIT_NO_MS = "polls=87"


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
                 slots=2, identity=False, loadout=None):
    """One AgentEquipBegin line.

    `identity` adds the race/monster/actionSet tokens (shipped 2026-08-02) and `loadout`
    the dedupe id (2026-08-03); both default off so the older log shapes stay covered.
    """
    ident = (" race='human' monster='human' actionSet='as_human_warrior'" if identity else "")
    tail = f" loadout=#{loadout}" if loadout is not None else ""
    return _line("INFO", f"[BattleLoad] seq={seq} t=+100ms phase=AgentEquipBegin "
                         f"agent#{idx} '{name}' char='{char}' culture='{culture}'"
                         f"{ident} slots={slots}{tail}")


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


# --- the 2026-08-07 bucket-split markers ----------------------------------- #
# Emit() writes `[BattleLoad] seq=N t=+Nms phase=<P> <detail>`; _phase() reproduces that
# shape verbatim so a fixture is the real line, not an approximation of it.
def _phase(seq, ms, phase, detail=""):
    return _line("INFO", f"[BattleLoad] seq={seq} t=+{ms}ms phase={phase} {detail}".rstrip())


def _memstats(gc="44/19/6", heap=655, priv=14002, ws=11540):
    """The MemStats() suffix. priv=None drops BOTH process tokens, which is exactly what
    the C# does when the process read fails (never a fabricated 0)."""
    head = f"gc={gc} heapMB={heap}"
    return head if priv is None else f"{head} privMB={priv} wsMB={ws}"


def _init(seq=4, ms=1000, scene="battle_terrain_b", mem=None):
    tail = f" {mem}" if mem else ""
    return _phase(seq, ms, "MissionInitialize", f"scene='{scene}'{tail}")


def _init_done(seq=5, ms=2000, scene="battle_terrain_b", mem=None):
    tail = f" {mem}" if mem else ""
    return _phase(seq, ms, "MissionInitializeDone", f"scene='{scene}'{tail}")


def _finish_begin(seq=6, ms=5000, polls=87, wait_ms=1449, mem=None):
    """FinishMissionLoadingBegin. wait_ms=None reproduces the omitted-waitMs shape."""
    detail = f"polls={polls}" if wait_ms is None else f"polls={polls} waitMs={wait_ms}"
    return _phase(seq, ms, "FinishMissionLoadingBegin", detail + (f" {mem}" if mem else ""))


def _finish_done(seq=9, ms=8200, mem=None):
    return _phase(seq, ms, "FinishMissionLoadingDone", mem or "")


def _after_start_begin(seq=7, ms=5500):
    return _phase(seq, ms, "MissionAfterStartBegin")


def _after_start_done(seq=8, ms=8000):
    return _phase(seq, ms, "MissionAfterStartDone")


def _watchdog(elapsed, last_phase, detail=""):
    tail = f"phase={last_phase} seq=5 {detail}".strip()
    return _line("ERROR", f"[BattleLoad] WATCHDOG STILL LOADING after {elapsed}s — last {tail}")


def _mem(priv=4211, ws=3900, heap=654, used=14003, limit=31646, avail=6200,
         load=61, warn=False, prefix=True, ts="2026-06-17 12:00:00"):
    """One [MemSample] periodic line per the pinned contract; warn=True prepends the
    WARN LOW COMMIT HEADROOM prefix (headroomMB computed like the C# sampler)."""
    warn_part = (f"WARN LOW COMMIT HEADROOM headroomMB={max(0, limit - used)} "
                 if warn else "")
    payload = (f"[MemSample] {warn_part}privMB={priv} wsMB={ws} heapMB={heap} "
               f"sysCommitUsedMB={used} sysCommitLimitMB={limit} "
               f"availPhysMB={avail} memLoad={load}%")
    return _line("WARNING" if warn else "INFO", payload, ts=ts) if prefix else payload


def _mem_session(total=16296, limit=31646):
    return _line("INFO", f"[MemSample] session totalPhysMB={total} sysCommitLimitMB={limit}")


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

    # --- loadout dedupe (2026-08-03) --------------------------------------- #
    # The dump is emitted once per distinct loadout; every later agent wearing it carries
    # only `loadout=#N`. The stuck agent is usually one of the deduped ones, so without
    # re-attachment the EQUIPMENT verdict would name no suspect at all.

    def test_resolves_deduped_loadout_from_the_earlier_dump(self):
        text = _log(
            _mission_init(),
            _equip_begin(5, 0, slots=2, loadout=1),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
            _equip_ok(6, 0),
            _equip_begin(7, 1, slots=2, loadout=1),  # same kit — no dump of its own
        )
        tl = tb.parse_battle_load_log(text)
        begin = tl.events[-1]
        self.assertEqual(begin.phase, "AgentEquipBegin")
        self.assertEqual([s.item_id for s in begin.slots],
                         ["gondor_sword_a", "sk_gd_ano_body_a"])

    def test_deduped_loadout_still_names_suspects_in_the_verdict(self):
        text = _log(
            _mission_init(),
            _equip_begin(5, 0, slots=1, loadout=1),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _equip_ok(6, 0),
            _equip_begin(7, 57, slots=1, loadout=1),
        )
        v = tb.classify(tb.parse_battle_load_log(text))
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertIn("bo_gondor_sword_a", v.suspect_names)

    def test_loadout_ids_do_not_leak_across_missions(self):
        # The service clears its map at Mission.Initialize, so ids restart at #1 each load.
        # Resolving mission B's #1 against mission A's block would name the wrong item.
        text = _log(
            _mission_init(scene="battle_terrain_b"),
            _equip_begin(5, 0, slots=1, loadout=1),
            _slot("Weapon0", "mission_a_sword", bo="bo_mission_a", kind="Weapon"),
            _equip_ok(6, 0),
            _mission_init(scene="arena_empire_a"),
            _equip_begin(7, 0, slots=1, loadout=1),
        )
        tl = tb.parse_battle_load_log(text)
        self.assertEqual(tl.events[-1].slots, [])

    def test_earlier_missions_resolve_against_their_own_blocks(self):
        # Ids restart per load, so resolving in one global pass would match every mission's
        # #1 against the LAST mission's block. Each segment must resolve against its own.
        text = _log(
            _mission_init(scene="battle_terrain_b"),
            _equip_begin(5, 0, slots=1, loadout=1),
            _slot("Weapon0", "mission_a_sword", bo="bo_mission_a", kind="Weapon"),
            _equip_ok(6, 0),
            _equip_begin(7, 1, slots=1, loadout=1),      # deduped inside mission A
            _equip_ok(8, 1),
            _mission_init(scene="arena_empire_a"),
            _equip_begin(9, 0, slots=1, loadout=1),
            _slot("Weapon0", "mission_b_sword", bo="bo_mission_b", kind="Weapon"),
            _equip_ok(10, 0),
            _equip_begin(11, 1, slots=1, loadout=1),     # deduped inside mission B
        )
        tl = tb.parse_battle_load_log(text)
        by_seq = {e.seq: e for e in tl.events}
        self.assertEqual([s.item_id for s in by_seq[7].slots], ["mission_a_sword"])
        self.assertEqual([s.item_id for s in by_seq[11].slots], ["mission_b_sword"])

    def test_equip_begin_without_loadout_token_still_parses(self):
        # Logs from before the dedupe shipped must keep working unchanged.
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Weapon0", "old_format_sword", bo="bo_old", kind="Weapon"))
        tl = tb.parse_battle_load_log(text)
        self.assertEqual([s.item_id for s in tl.events[-1].slots], ["old_format_sword"])

    def test_identity_tokens_do_not_bleed_into_the_culture_field(self):
        # race/monster/actionSet sit between culture= and slots=. A lazy culture group
        # swallowed all of them, so the reported culture was the whole run of tokens.
        text = _log(_mission_init(),
                    _equip_begin(5, 57, char="townsman_dunland", culture="empire",
                                 slots=2, identity=True, loadout=3))
        tl = tb.parse_battle_load_log(text)
        agent = tb._parse_equip_begin(tl.events[-1].detail)
        self.assertEqual(agent["culture"], "empire")
        self.assertEqual(agent["char"], "townsman_dunland")
        self.assertEqual(agent["slots"], "2")


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
# [MemSample] memory telemetry (issue #386) — parse                            #
# --------------------------------------------------------------------------- #
class MemSampleParseTests(unittest.TestCase):
    def test_periodic_line_parses_with_filelogger_prefix(self):
        tl = tb.parse_battle_load_log(_log(_mem()))
        self.assertEqual(len(tl.mem_samples), 1)
        s = tl.mem_samples[0]
        self.assertEqual(s.priv_mb, 4211)
        self.assertEqual(s.ts, "2026-06-17 12:00:00")
        self.assertFalse(s.warned)
        self.assertFalse(tl.mem_warned)

    def test_bare_line_without_prefix_parses_with_ts_none(self):
        tl = tb.parse_battle_load_log(_log(_mem(prefix=False)))
        self.assertEqual(len(tl.mem_samples), 1)
        self.assertIsNone(tl.mem_samples[0].ts)

    def test_pinned_periodic_literal_parses_to_pinned_numbers(self):
        # Cross-language twin pin — the C# side asserts it emits exactly this line
        # (MemoryPressureSamplerTests.FormatSample_KnownValues_MatchesContractLine);
        # this side asserts the tool reads exactly these numbers back out of it.
        tl = tb.parse_battle_load_log(_log(_line("INFO", PINNED_MEM_PERIODIC)))
        s = tl.mem_samples[0]
        self.assertEqual((s.priv_mb, s.ws_mb, s.heap_mb, s.commit_used_mb,
                          s.commit_limit_mb, s.avail_phys_mb, s.mem_load),
                         (4211, 3900, 654, 14003, 31646, 6200, 61))
        self.assertEqual(s.headroom_mb, 31646 - 14003)
        self.assertFalse(s.warned)

    def test_pinned_warn_literal_sets_warned_and_mem_warned(self):
        tl = tb.parse_battle_load_log(_log(_line("WARNING", PINNED_MEM_WARN)))
        s = tl.mem_samples[0]
        self.assertTrue(s.warned)
        self.assertTrue(tl.mem_warned)
        self.assertEqual(s.commit_used_mb, 29847)
        self.assertEqual(s.headroom_mb, 1799)
        self.assertEqual(s.mem_load, 97)

    def test_pinned_session_literal_parses(self):
        tl = tb.parse_battle_load_log(_log(_line("INFO", PINNED_MEM_SESSION)))
        self.assertEqual(tl.mem_session,
                         {"total_phys_mb": 16296, "commit_limit_mb": 31646})
        self.assertEqual(tl.mem_samples, [])

    def test_mem_lines_do_not_become_phase_events(self):
        # Inertness pin: [MemSample] lines never touch the phase timeline.
        text = _log(_mem_session(), _mem(), _mission_init(), _mem(warn=True))
        tl = tb.parse_battle_load_log(text)
        self.assertEqual([e.phase for e in tl.events], ["MissionInitialize"])
        self.assertEqual(tl.events[0].seq, 4)

    def test_mem_sample_between_equip_begin_and_slots_keeps_attachment(self):
        # Inertness pin: a MemSample interleaved inside a slot-dump block must not
        # break attachment to the owning AgentEquipBegin.
        text = _log(
            _mission_init(),
            _equip_begin(5, 57, slots=2),
            _mem(),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _mem(warn=True),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
        )
        tl = tb.parse_battle_load_log(text)
        begin = tl.events[-1]
        self.assertEqual(begin.phase, "AgentEquipBegin")
        self.assertEqual([s.item_id for s in begin.slots],
                         ["gondor_sword_a", "sk_gd_ano_body_a"])
        self.assertEqual(len(tl.mem_samples), 2)
        self.assertTrue(tl.mem_warned)

    def test_mem_sample_after_last_phase_event_does_not_change_verdict(self):
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"),
                    _mem(warn=True))
        v = tb.triage(text)
        self.assertEqual(v.kind, "EQUIPMENT")


# --------------------------------------------------------------------------- #
# [MemSample] memory telemetry (issue #386) — classify_memory                  #
# --------------------------------------------------------------------------- #
class MemClassifyTests(unittest.TestCase):
    def _tl(self, *lines):
        return tb.parse_battle_load_log(_log(*lines))

    def test_no_samples_returns_none_for_old_logs(self):
        # Old logs (pre-#386) carry no [MemSample] lines — memory stays None and the
        # rest of the pipeline is byte-identical.
        tl = self._tl(_mission_init(), _equip_begin(5, 57, slots=1),
                      _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"))
        self.assertIsNone(tb.classify_memory(tl))

    def test_healthy_last_sample_no_warn_is_not_pressure(self):
        tl = self._tl(_playable(), _mem())  # headroom 17643 >= 2048 and >= 10%
        mem = tb.classify_memory(tl)
        self.assertFalse(mem["pressure"])
        self.assertFalse(mem["warn_seen"])
        self.assertEqual(mem["headroom_mb"], 31646 - 14003)

    def test_low_floor_on_last_sample_is_pressure(self):
        tl = self._tl(_playable(), _mem(used=30000))  # headroom 1646 < 2048
        self.assertTrue(tb.classify_memory(tl)["pressure"])

    def test_percent_rule_fires_above_the_floor(self):
        # headroom 3460 clears the 2048 floor but is < 10% of 40960 (4096) — low.
        tl = self._tl(_playable(), _mem(used=37500, limit=40960))
        self.assertTrue(tb.classify_memory(tl)["pressure"])

    def test_warn_seen_earlier_is_pressure_even_if_last_sample_healthy(self):
        tl = self._tl(_playable(), _mem(used=30000, warn=True), _mem())
        mem = tb.classify_memory(tl)
        self.assertTrue(mem["pressure"])
        self.assertTrue(mem["warn_seen"])

    def test_garbage_inputs_never_report_low(self):
        # Polarity pin: limit <= 0 or used < 0 is garbage — never report pressure.
        for used, limit in ((14003, 0), (14003, -1), (-5, 31646)):
            tl = self._tl(_playable(), _mem(used=used, limit=limit))
            self.assertFalse(tb.classify_memory(tl)["pressure"], (used, limit))

    def test_used_above_limit_clamps_headroom_and_is_low(self):
        # used > limit is allowed (not garbage) — headroom clamps at 0 and IS low.
        tl = self._tl(_playable(), _mem(used=32000, limit=31646))
        mem = tb.classify_memory(tl)
        self.assertEqual(mem["headroom_mb"], 0)
        self.assertTrue(mem["pressure"])

    def test_floor_override(self):
        # headroom 3000 with limit 20000: >= 2048 and >= 10% (2000) — healthy by
        # default; a raised floor flips it.
        tl = self._tl(_playable(), _mem(used=17000, limit=20000))
        self.assertFalse(tb.classify_memory(tl)["pressure"])
        self.assertTrue(tb.classify_memory(tl, floor_mb=4096)["pressure"])

    def test_peak_first_and_last(self):
        tl = self._tl(_playable(), _mem(used=10000), _mem(used=19000), _mem(used=14003))
        mem = tb.classify_memory(tl)
        self.assertEqual(mem["peak_commit_used_mb"], 19000)
        self.assertEqual(mem["first"]["commit_used_mb"], 10000)
        self.assertEqual(mem["last"]["commit_used_mb"], 14003)


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


class MemReportTests(unittest.TestCase):
    def test_report_memory_trend_and_pressure_note(self):
        text = _log(_mem_session(), _mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"),
                    _mem(), _mem(used=29847, avail=310, load=97, warn=True))
        tl = tb.parse_battle_load_log(text)
        v = tb.classify(tl)
        report = tb.format_report(v, tl, None)
        self.assertIn("Memory trend", report)
        self.assertIn("totalPhysMB=16296", report)
        self.assertIn("MEMORY PRESSURE", report)
        self.assertIn("may be a symptom, not the cause", report)

    def test_report_no_pressure_note_when_healthy(self):
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"),
                    _mem())
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None)
        self.assertIn("Memory trend", report)
        self.assertNotIn("MEMORY PRESSURE", report)

    def test_report_omits_memory_section_when_no_samples(self):
        text = _log(_mission_init(), _equip_begin(5, 57, slots=1),
                    _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a"))
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None)
        self.assertNotIn("Memory trend", report)
        self.assertNotIn("MEMORY PRESSURE", report)


class MemCliTests(unittest.TestCase):
    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_cli_json_carries_memory_block_and_exit_code_is_untouched(self):
        # COMPLETED + pressure: memory is additive decoration — exit stays 0.
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_mem_session(), _mission_init(), _equip_begin(5, 0),
                              _equip_ok(6, 0), _playable(),
                              _mem(used=29847, avail=310, load=97, warn=True)),
                         encoding="utf-8")
            out = Path(d) / "verdict.json"
            r = self._run([str(p), "--json", str(out)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            payload = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(payload["kind"], "COMPLETED")
            self.assertTrue(payload["memory"]["pressure"])
            self.assertTrue(payload["memory"]["warn_seen"])
            self.assertEqual(payload["memory"]["headroom_mb"], 31646 - 29847)

    def test_cli_json_memory_none_when_no_samples(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0),
                              _playable()), encoding="utf-8")
            out = Path(d) / "verdict.json"
            r = self._run([str(p), "--json", str(out)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            payload = json.loads(out.read_text(encoding="utf-8"))
            self.assertIsNone(payload["memory"])

    def test_cli_mem_threshold_flag_overrides_floor(self):
        # headroom 3000 (limit 20000) is healthy at the default 2048 floor but low
        # under --mem-threshold-mb 4096.
        log_text = _log(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0),
                        _playable(), _mem(used=17000, limit=20000, load=85))
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(log_text, encoding="utf-8")
            out = Path(d) / "verdict.json"
            r = self._run([str(p), "--json", str(out)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertFalse(json.loads(out.read_text(encoding="utf-8"))["memory"]["pressure"])
            r = self._run([str(p), "--json", str(out), "--mem-threshold-mb", "4096"])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertTrue(json.loads(out.read_text(encoding="utf-8"))["memory"]["pressure"])
            self.assertIn("MEMORY PRESSURE", r.stdout)


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


class MemThresholdTruncationTests(unittest.TestCase):
    """Pins the INTEGER-FLOOR percent threshold against the C# twin
    (MemoryPressureSamplerTests' truncation-boundary tests): threshold =
    max(floor, limit * percent // 100), exactly like C# long division.
    Deep-review 2026-08-05 caught the mirrors disagreeing in the ~1 MB band
    below the true 10% line on any limit not divisible by 10."""

    def test_headroom_exactly_at_floored_percent_threshold_is_not_low(self):
        # limit=31646 -> threshold = max(2048, 3164 [floored from 3164.6]); headroom 3164
        self.assertFalse(tb._headroom_low(28482, 31646, 2048, 10))

    def test_one_mb_below_floored_percent_threshold_is_low(self):
        self.assertTrue(tb._headroom_low(28483, 31646, 2048, 10))

    def test_headroom_exactly_at_floor_on_odd_limit_is_not_low(self):
        # limit=20481 -> threshold = max(2048, 2048 [floored from 2048.1]); headroom 2048
        self.assertFalse(tb._headroom_low(18433, 20481, 2048, 10))


class MemStatsPhaseTokenTests(unittest.TestCase):
    """Since #386 five phase lines carry trailing ' gc=... heapMB=... privMB=... wsMB='
    tokens (service MemStats). _EQUIP_BEGIN_RE broke once from a near-identical
    trailing-token addition (2026-08-02, noted in-source) — these fixtures pin that
    the phase parsers tolerate the suffix."""

    MEMSTATS = " gc=12/4/1 heapMB=654 privMB=4211 wsMB=3900"

    def _suffixed_log(self):
        return _log(
            _line("INFO", "[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=42"
                          + self.MEMSTATS),
            _open_new(),
            _scene_selected(),
            _line("INFO", "[BattleLoad] seq=4 t=+56ms phase=MissionInitialize "
                          "scene='battle_terrain_b'" + self.MEMSTATS),
            _line("INFO", "[BattleLoad] seq=99 t=+8000ms phase=BattlePlayable "
                          "scene='battle_terrain_b' agents=120" + self.MEMSTATS),
        )

    def test_verdict_kind_identical_with_and_without_memstats_suffix(self):
        plain = _log(_encounter(), _open_new(), _scene_selected(),
                     _mission_init(), _playable())
        v_plain = tb.classify(tb.parse_battle_load_log(plain))
        v_suffixed = tb.classify(tb.parse_battle_load_log(self._suffixed_log()))
        self.assertEqual(v_plain.kind, v_suffixed.kind)

    def test_phase_events_and_scene_survive_memstats_suffix(self):
        tl = tb.parse_battle_load_log(self._suffixed_log())
        self.assertEqual([e.phase for e in tl.events],
                         ["EncounterStart", "MissionOpenNew", "BattleSceneSelected",
                          "MissionInitialize", "BattlePlayable"])
        # the lazy quote-anchored scene regex must not be confused by the suffix
        self.assertIn("scene='battle_terrain_b'", tl.events[3].detail)


class MemSampleMalformedLineTests(unittest.TestCase):
    def test_torn_memsample_line_is_skipped_without_crash_or_count(self):
        # A torn write at crash time can truncate the final line mid-token.
        torn = _line("INFO", "[MemSample] privMB=4211 wsMB=39")
        tl = tb.parse_battle_load_log(_log(_mem(), torn))
        self.assertEqual(len(tl.mem_samples), 1)  # only the well-formed line counted


class NewLoadPhaseVerdictTests(unittest.TestCase):
    """The three markers added 2026-08-07 (the 11.9 s bucket split), one fixture each,
    WITH and WITHOUT the MemStats suffix — the exact shape of the two #386 tests, because
    RCA gap #8 records that a trailing token already broke a near-identical regex once
    (2026-08-02) and the mitigation is a FIXTURE, not reasoning about the regex.

    Before this, classify() knew only MissionInitialize/BattleSceneSelected as SCENE and
    let everything else fall through to PRE_SCENE, so a log that died in the native
    mission build was reported as 'froze very early ... before scene selection'."""

    MEM = _memstats()

    def _head(self):
        return [_encounter(), _open_new(), _scene_selected(), _init()]

    # --- MissionInitializeDone: bucket 1 closed, the async wait never finished ----- #
    def test_mission_initialize_done_is_scene_with_and_without_memstats(self):
        plain = tb.triage(_log(*self._head(), _init_done()))
        suffixed = tb.triage(_log(*self._head(), _init_done(mem=self.MEM)))
        self.assertEqual(plain.kind, "SCENE")
        self.assertEqual(suffixed.kind, "SCENE")
        self.assertEqual(plain.kind, suffixed.kind)

    def test_mission_initialize_done_scene_extraction_survives_memstats(self):
        plain = tb.triage(_log(*self._head(), _init_done(scene="battle_terrain_158")))
        suffixed = tb.triage(
            _log(*self._head(), _init_done(scene="battle_terrain_158", mem=self.MEM)))
        self.assertEqual(plain.scene, "battle_terrain_158")
        self.assertEqual(suffixed.scene, "battle_terrain_158")

    # --- FinishMissionLoadingBegin: froze in the warm-up ticks, pre-equip ---------- #
    def test_finish_begin_is_scene_with_and_without_memstats(self):
        plain = tb.triage(_log(*self._head(), _init_done(), _finish_begin()))
        suffixed = tb.triage(
            _log(*self._head(), _init_done(mem=self.MEM), _finish_begin(mem=self.MEM)))
        self.assertEqual(plain.kind, "SCENE")
        self.assertEqual(suffixed.kind, "SCENE")

    def test_finish_begin_carries_no_scene_token_either_way(self):
        # The line has no scene='…'; the suffix must not make one appear (a greedy or
        # mis-anchored regex could pull one out of the MemStats tokens).
        plain = tb.triage(_log(*self._head(), _init_done(), _finish_begin()))
        suffixed = tb.triage(
            _log(*self._head(), _init_done(mem=self.MEM), _finish_begin(mem=self.MEM)))
        self.assertIsNone(plain.scene)
        self.assertIsNone(suffixed.scene)

    # --- FinishMissionLoadingDone: fully loaded, first tick never came ------------- #
    def test_finish_done_is_post_equip_with_and_without_memstats(self):
        tail = [_init_done(), _finish_begin(), _after_start_begin(), _after_start_done()]
        plain = tb.triage(_log(*self._head(), *tail, _finish_done()))
        suffixed = tb.triage(_log(*self._head(), *tail, _finish_done(mem=self.MEM)))
        self.assertEqual(plain.kind, "POST_EQUIP")
        self.assertEqual(suffixed.kind, "POST_EQUIP")

    def test_finish_done_names_no_stuck_agent(self):
        # POST_EQUIP normally carries the agent from an AgentEquipOk terminal. This
        # terminal has no agent at all — the summary must not invent one.
        v = tb.triage(_log(*self._head(), _init_done(), _finish_begin(),
                           _after_start_begin(), _after_start_done(),
                           _finish_done(mem=self.MEM)))
        self.assertEqual(v.kind, "POST_EQUIP")
        self.assertIsNone(v.stuck_agent)
        self.assertNotIn("agent#", v.summary)

    # --- lifecycle tail ----------------------------------------------------------- #
    def test_new_phases_appear_in_the_lifecycle_tail(self):
        text = _log(*self._head(), _init_done(mem=self.MEM), _finish_begin(mem=self.MEM),
                    _after_start_begin(), _after_start_done(),
                    _finish_done(mem=self.MEM))
        tl = tb.parse_battle_load_log(text)
        self.assertEqual([e.phase for e in tl.events][-3:],
                         ["MissionAfterStartBegin", "MissionAfterStartDone",
                          "FinishMissionLoadingDone"])
        # Assert against the LIFECYCLE BLOCK only. A bare `assertIn(phase, report)` is
        # vacuous now that the Load-timing table names every bucket boundary: it survived
        # a mutation shrinking the tail slice to events[-2:].
        report = tb.format_report(tb.classify(tl), tl, None)
        lines = report.splitlines()
        start = lines.index("Lifecycle (last phases seen):")
        block = []
        for line in lines[start + 1:]:
            if not line.strip():
                break
            block.append(line)
        block_text = "\n".join(block)
        for phase in ("MissionInitializeDone", "FinishMissionLoadingBegin",
                      "FinishMissionLoadingDone"):
            self.assertIn(phase, block_text, f"{phase} missing from the lifecycle tail")

    def test_pre_scene_still_owns_the_genuinely_early_phases(self):
        # Guard the other side of the mapping: widening SCENE must not swallow the
        # phases PRE_SCENE legitimately owns.
        self.assertEqual(tb.triage(_log(_encounter(), _open_new())).kind, "PRE_SCENE")
        self.assertEqual(tb.triage(_log(_encounter())).kind, "PRE_SCENE")


class NewLoadPhaseInertnessTests(unittest.TestCase):
    """Inertness pins, copied from the #386 [MemSample] block: the new lines must not be
    mis-read as agent-equip lines and must not disturb slot/loadout attachment."""

    def test_new_phase_lines_are_not_read_as_agent_equip_lines(self):
        text = _log(_init(), _init_done(mem=_memstats()),
                    _finish_begin(mem=_memstats()), _finish_done(mem=_memstats()))
        tl = tb.parse_battle_load_log(text)
        for e in tl.events[1:]:
            self.assertEqual(tb._parse_equip_begin(e.detail), {"raw": e.detail})
            self.assertEqual(tb._parse_equip_ok(e.detail), {"raw": e.detail})
            self.assertEqual(e.slots, [])

    def test_new_phase_lines_do_not_break_slot_attachment(self):
        # Real emit order: the bucket markers bracket the equip burst. Slots must still
        # land on their owning AgentEquipBegin and nowhere else.
        text = _log(
            _init(), _init_done(mem=_memstats()), _finish_begin(mem=_memstats()),
            _after_start_begin(),
            _equip_begin(10, 57, slots=2),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _slot("Body", "sk_gd_ano_body_a", mesh="sk_gd_ano_body_a", kind="BodyArmor"),
            _equip_ok(11, 57),
            _after_start_done(), _finish_done(mem=_memstats()),
            # A stray trailing slot (truncated/interleaved log). Without it the arrange
            # could not redden: deleting the `tl.events[-1].phase == "AgentEquipBegin"`
            # guard in parse_battle_load_log would leave every assertion below true.
            _slot("Body", "stray_after_the_block", mesh="stray_mesh"),
        )
        tl = tb.parse_battle_load_log(text)
        by_phase = {e.phase: e for e in tl.events}
        self.assertEqual([s.item_id for s in by_phase["AgentEquipBegin"].slots],
                         ["gondor_sword_a", "sk_gd_ano_body_a"])
        for phase in ("MissionInitializeDone", "FinishMissionLoadingBegin",
                      "FinishMissionLoadingDone", "MissionAfterStartDone"):
            self.assertEqual(by_phase[phase].slots, [], phase)

    def test_mission_initialize_done_does_not_clear_the_loadout_map(self):
        # Regression guard on the ONE line `if phase == "MissionInitialize":
        # loadouts.clear()`. Loosening that `==` to a startswith/prefix test would make
        # MissionInitializeDone wipe the map and the deduped agent would name no suspect.
        text = _log(
            _init(),
            _equip_begin(5, 0, slots=1, loadout=1),
            _slot("Weapon0", "gondor_sword_a", bo="bo_gondor_sword_a", kind="Weapon"),
            _equip_ok(6, 0),
            _init_done(mem=_memstats()),
            _equip_begin(7, 57, slots=1, loadout=1),
        )
        v = tb.classify(tb.parse_battle_load_log(text))
        self.assertEqual(v.kind, "EQUIPMENT")
        self.assertIn("bo_gondor_sword_a", v.suspect_names)


class LoadTimingBucketTests(unittest.TestCase):
    """The gap-based bucket report the Phase-2 runbook consumes
    (docs/investigations/native-commit-audit-2026-08.md: bucket1Ms .. bucket4Ms)."""

    def _mission(self, base=0, priv=(9331, 9400, 14002, 15880)):
        """One full mission at `base` ms. A chained second mission INHERITS the running
        clock, so its absolutes are offset while every gap is unchanged."""
        p1, p2, p3, p4 = priv
        return [
            _init(seq=4, ms=base + 1000, mem=_memstats(priv=p1)),
            _init_done(seq=5, ms=base + 2000, mem=_memstats(priv=p2)),
            _finish_begin(seq=6, ms=base + 5000, mem=_memstats(priv=p3)),
            _after_start_begin(seq=7, ms=base + 5500),
            _after_start_done(seq=8, ms=base + 8000),
            _finish_done(seq=9, ms=base + 8200, mem=_memstats(priv=p4)),
            _phase(10, base + 8500, "BattlePlayable", "scene='battle_terrain_b' agents=120"),
        ]

    def _timings(self, *lines):
        return tb.classify_phase_timings(tb.parse_battle_load_log(_log(*lines)))

    def _ms(self, timings):
        return {b["name"]: b["ms"] for b in timings["buckets"]}

    def test_buckets_are_computed_from_the_marker_gaps(self):
        ms = self._ms(self._timings(_encounter(), *self._mission()))
        self.assertEqual(ms, {"bucket1": 1000, "bucket2": 3000, "bucket3a": 500,
                              "bucket3b": 2500, "bucket3c": 200, "bucket4": 300})

    def test_gaps_survive_a_chained_missions_inherited_clock(self):
        # THE reason the report is gap-based: mission B's absolutes are +100 s but its
        # buckets are identical. An absolute-based reading would report 101000 ms.
        single = self._ms(self._timings(*self._mission()))
        chained = self._ms(self._timings(*self._mission(), *self._mission(base=100000)))
        self.assertEqual(chained, single)
        self.assertEqual(chained["bucket1"], 1000)

    def test_dominant_bucket_is_the_largest_gap(self):
        t = self._timings(*self._mission())
        self.assertEqual(t["dominant"], "bucket2")

    def test_unreached_buckets_are_none_not_zero(self):
        # A log that dies at MissionInitializeDone knows bucket1 and nothing else.
        ms = self._ms(self._timings(_init(ms=1000), _init_done(ms=2000)))
        self.assertEqual(ms["bucket1"], 1000)
        for name in ("bucket2", "bucket3a", "bucket3b", "bucket3c", "bucket4"):
            self.assertIsNone(ms[name], name)

    def test_polls_and_waitms_parse_from_the_pinned_literal(self):
        t = self._timings(_init(), _init_done(),
                          _phase(6, 5000, "FinishMissionLoadingBegin",
                                 PINNED_WAIT_WITH_MS + " " + _memstats()))
        self.assertEqual(t["polls"], 87)
        self.assertEqual(t["wait_ms"], 1449)

    def test_waitms_absent_is_none_and_never_a_fabricated_zero(self):
        # The C# OMITS waitMs when MissionInitializeDone was not observed. Reading it as
        # 0 would claim an instantaneous wait that was never measured.
        t = self._timings(_init(), _phase(6, 5000, "FinishMissionLoadingBegin",
                                          PINNED_WAIT_NO_MS + " " + _memstats()))
        self.assertEqual(t["polls"], 87)
        self.assertIsNone(t["wait_ms"])

    def test_polls_zero_is_flagged_as_a_binding_failure(self):
        t = self._timings(_init(), _init_done(), _finish_begin(polls=0, wait_ms=11900))
        self.assertEqual(t["polls"], 0)
        self.assertTrue(t["tick_binding_failed"])
        self.assertFalse(self._timings(_init(), _init_done(),
                                       _finish_begin())["tick_binding_failed"])

    def test_priv_mb_trajectory_is_captured_per_memstats_marker(self):
        t = self._timings(*self._mission())
        self.assertEqual(t["priv_mb"], {"MissionInitialize": 9331,
                                        "MissionInitializeDone": 9400,
                                        "FinishMissionLoadingBegin": 14002,
                                        "FinishMissionLoadingDone": 15880})

    def test_priv_mb_is_none_when_the_process_read_failed(self):
        # MemStats drops BOTH process tokens on reader failure — never a 0.
        t = self._timings(_init(mem=_memstats(priv=None)),
                          _init_done(mem=_memstats(priv=None)))
        self.assertIsNone(t["priv_mb"]["MissionInitialize"])
        self.assertIsNone(t["priv_mb"]["MissionInitializeDone"])

    def test_timings_are_none_for_logs_without_the_new_markers(self):
        # Pre-2026-08-07 logs stay byte-identical through the whole pipeline.
        self.assertIsNone(self._timings(_encounter(), _open_new(), _scene_selected(),
                                        _mission_init(), _equip_begin(5, 0),
                                        _equip_ok(6, 0), _playable()))

    def test_timings_are_none_when_there_is_no_mission_initialize(self):
        self.assertIsNone(self._timings(_encounter(), _open_new()))


class LoadTimingReportTests(unittest.TestCase):
    def _report(self, *lines):
        tl = tb.parse_battle_load_log(_log(*lines))
        return tb.format_report(tb.classify(tl), tl, None)

    def test_report_renders_the_bucket_table_and_dominant(self):
        r = self._report(_init(ms=1000, mem=_memstats(priv=9331)),
                         _init_done(ms=2000, mem=_memstats(priv=9400)),
                         _finish_begin(ms=5000, mem=_memstats(priv=14002)),
                         _after_start_begin(ms=5500), _after_start_done(ms=8000),
                         _finish_done(ms=8200, mem=_memstats(priv=15880)))
        self.assertIn("Load timing", r)
        self.assertIn("bucket1", r)
        self.assertIn("bucket3c", r)
        self.assertIn("dominant: bucket2", r)
        self.assertIn("polls=87 waitMs=1449", r)
        self.assertIn("privMB", r)

    def test_report_never_prints_a_zero_for_an_absent_waitms(self):
        r = self._report(_init(ms=1000),
                         _phase(6, 5000, "FinishMissionLoadingBegin",
                                PINNED_WAIT_NO_MS + " " + _memstats()))
        self.assertIn("polls=87", r)
        self.assertNotIn("waitMs=0", r)
        self.assertIn("waitMs=<not observed>", r)

    def test_report_warns_when_polls_is_zero(self):
        r = self._report(_init(ms=1000), _init_done(ms=2000),
                         _finish_begin(ms=5000, polls=0, wait_ms=11900))
        self.assertIn("TickLoading binding FAILED", r)

    def test_report_omits_load_timing_for_old_logs(self):
        r = self._report(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0), _playable())
        self.assertNotIn("Load timing", r)


class LoadTimingCliTests(unittest.TestCase):
    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_cli_json_carries_the_timings_block(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_init(ms=1000, mem=_memstats(priv=9331)),
                              _init_done(ms=2000, mem=_memstats(priv=9400)),
                              _finish_begin(ms=5000, mem=_memstats(priv=14002)),
                              _after_start_begin(ms=5500), _after_start_done(ms=8000),
                              _finish_done(ms=8200, mem=_memstats(priv=15880)),
                              _phase(10, 8500, "BattlePlayable",
                                     "scene='battle_terrain_b' agents=120")),
                         encoding="utf-8")
            out = Path(d) / "verdict.json"
            r = self._run([str(p), "--json", str(out)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            payload = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(payload["kind"], "COMPLETED")
            self.assertEqual(payload["timings"]["dominant"], "bucket2")
            self.assertEqual(payload["timings"]["wait_ms"], 1449)
            self.assertEqual(payload["timings"]["priv_mb"]["FinishMissionLoadingDone"],
                             15880)

    def test_cli_json_timings_none_for_old_logs(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_mission_init(), _equip_begin(5, 0), _equip_ok(6, 0),
                              _playable()), encoding="utf-8")
            out = Path(d) / "verdict.json"
            r = self._run([str(p), "--json", str(out)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIsNone(json.loads(out.read_text(encoding="utf-8"))["timings"])

    def test_cli_exit_code_is_unchanged_by_the_new_terminal_phases(self):
        # SCENE and POST_EQUIP are both pre-existing HANG_KINDS -> exit 1. The lattice
        # and the exit contract are untouched by the new mapping.
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "taom_debug.log"
            p.write_text(_log(_init(ms=1000), _init_done(ms=2000, mem=_memstats())),
                         encoding="utf-8")
            r = self._run([str(p)])
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            # "VERDICT: SCENE", not a bare "SCENE" — PRE_SCENE contains it as a substring,
            # so the loose form passed against the exact bug this test exists to catch.
            self.assertIn("VERDICT: SCENE", r.stdout)
            self.assertNotIn("VERDICT: PRE_SCENE", r.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
