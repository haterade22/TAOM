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
  - RENDER_WAIT        : ends at WaitingForRender (held at SceneView.ReadyToRender;
                         a falling shaders= count is a cold cache, frozen is a wedge)
  - COMPLETED          : a BattlePlayable phase is present
  - UNKNOWN            : no [BattleLoad] lines at all
  - watchdog line corroborates the terminal phase
  - FileLogger prefix is stripped (lines parse with and without it)
  - CLI exit codes (0 completed / 1 hang / 2 bad path)
"""
import json
import re
import pathlib
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

# The pinned [MemStation] screen-anchor contract (#386 follow-up). Cross-language twin pin:
# MemoryStationSamplerTests.FormatStation_EnterWithKnownValues_MatchesPinnedLiteral and
# ..._ExitWithKnownValues_... assert the C# sampler emits EXACTLY these message bodies.
# The 7-token tail is shared with [MemSample] by construction (one C# FormatSampleTokens).
PINNED_MEM_STATION_ENTER = ("[MemStation] enter screen='GauntletInventoryScreen' "
                            "privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=14003 "
                            "sysCommitLimitMB=31646 availPhysMB=6200 memLoad=61%")
PINNED_MEM_STATION_EXIT = ("[MemStation] exit screen='GauntletInventoryScreen' "
                           "privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=29847 "
                           "sysCommitLimitMB=31646 availPhysMB=310 memLoad=97%")

# The pinned FinishMissionLoadingBegin wait-token contract (2026-08-07). Cross-language
# twin pin: BattleLoadDiagnosticsService.FormatFinishWaitDetail is asserted
# character-for-character on the C# side by
# FormatFinishWaitDetail_WithWait_ProducesPinnedLiteral / ..._WithoutWait_...  — waitMs is
# OMITTED when MissionInitializeDone was not observed, never rendered as a fabricated 0.
PINNED_WAIT_WITH_MS = "polls=87 waitMs=1449"
PINNED_WAIT_NO_MS = "polls=87"

# The pinned WaitingForRender detail contract (bundle b18f3441, 2026-09-04). Cross-language
# twin pin: BattleLoadDiagnosticsService.FormatRenderWaitDetail is asserted
# character-for-character on the C# side by FormatRenderWaitDetail_BothKnown_/_NoOrigin_/
# _UnreadableCount_/_NeitherKnown_. EITHER token is OMITTED when unmeasured, never a
# fabricated 0 - a shaders=0 would read as "nothing was compiling", the opposite of
# "we could not read it".
PINNED_RENDER_WAIT_BOTH = "waitedMs=290000 shaders=412"
PINNED_RENDER_WAIT_NO_ORIGIN = "shaders=412"
PINNED_RENDER_WAIT_NO_SHADERS = "waitedMs=290000"
PINNED_RENDER_WAIT_NEITHER = ""


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


def _scene_load_wait(seq=6, ms=8000, scene="battle_terrain_biome_094", polls=487,
                     wait_ms=8000, mem=None):
    """WaitingForSceneLoad — the bucket-2 heartbeat. Reuses FinishMissionLoadingBegin's
    polls=/waitMs= token pair verbatim (that reuse is the point: one regex reads both)."""
    detail = f"scene='{scene}' " + (
        f"polls={polls}" if wait_ms is None else f"polls={polls} waitMs={wait_ms}")
    return _phase(seq, ms, "WaitingForSceneLoad", detail + (f" {mem}" if mem else ""))


def _finish_done(seq=9, ms=8200, mem=None):
    return _phase(seq, ms, "FinishMissionLoadingDone", mem or "")


def _after_start_begin(seq=7, ms=5500):
    return _phase(seq, ms, "MissionAfterStartBegin")


def _after_start_done(seq=8, ms=8000):
    return _phase(seq, ms, "MissionAfterStartDone")


def _render_wait(seq=10, ms=30000, waited_ms=10000, shaders=412, mem=None):
    """One WaitingForRender line. waited_ms=None / shaders=None reproduce the omitted-token
    shapes the C# formatter emits when a value was not observed."""
    parts = []
    if waited_ms is not None:
        parts.append("waitedMs=%d" % waited_ms)
    if shaders is not None:
        parts.append("shaders=%d" % shaders)
    if mem:
        parts.append(mem)
    return _phase(seq, ms, "WaitingForRender", " ".join(parts))


def _watchdog(elapsed, last_phase, detail="", tokens=""):
    """One WATCHDOG STILL LOADING line.

    `tokens` is the block the C# side inserts between the dash and the literal `last`
    (`shaders=N`, `churn-capped`). It defaults to empty ONLY so the pre-2026-09-04 log
    shape stays covered; real lines from a current build always carry at least `shaders=`.
    """
    tail = f"phase={last_phase} seq=5 {detail}".strip()
    head = f"{tokens} " if tokens else ""
    return _line("ERROR", f"[BattleLoad] WATCHDOG STILL LOADING after {elapsed}s — {head}last {tail}")


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

    # --- WaitingForSceneLoad: died INSIDE the async wait (2026-09-06) -------------- #
    # Two player CTDs on battle_terrain_biome_094 ended on MissionInitializeDone with
    # nothing after it, so a death inside the wait and a hang inside it read identically
    # and the fault was bounded only to "somewhere in ~30 s". The heartbeat closes that.
    def test_scene_load_wait_is_scene_with_and_without_memstats(self):
        plain = tb.triage(_log(*self._head(), _init_done(), _scene_load_wait()))
        suffixed = tb.triage(_log(*self._head(), _init_done(mem=self.MEM),
                                  _scene_load_wait(mem=self.MEM)))
        self.assertEqual(plain.kind, "SCENE")
        self.assertEqual(suffixed.kind, "SCENE")

    def test_scene_load_wait_names_its_scene_through_the_memstats_suffix(self):
        suffixed = tb.triage(_log(*self._head(), _init_done(mem=self.MEM),
                                  _scene_load_wait(mem=self.MEM)))
        self.assertEqual(suffixed.scene, "battle_terrain_biome_094")

    def test_scene_load_wait_supplies_the_wait_pair_without_finish_begin(self):
        # The whole payoff: polls=/waitMs= now reach the timing ledger on a log that
        # never reached FinishMissionLoadingBegin, so the polls=1 native-spin rule can
        # fire on a crash log instead of only on "a run that got further".
        t = tb.classify_phase_timings(tb.parse_battle_load_log(
            _log(*self._head(), _init_done(), _scene_load_wait(polls=1, wait_ms=9000))))
        self.assertEqual(t["polls"], 1)
        self.assertEqual(t["wait_ms"], 9000)
        self.assertTrue(t["wait_incomplete"])

    def test_finish_begin_wins_over_a_heartbeat_and_is_marked_complete(self):
        t = tb.classify_phase_timings(tb.parse_battle_load_log(_log(
            *self._head(), _init_done(), _scene_load_wait(polls=1, wait_ms=9000),
            _finish_begin(seq=7, ms=9200, polls=87, wait_ms=1449))))
        self.assertEqual(t["polls"], 87)
        self.assertEqual(t["wait_ms"], 1449)
        self.assertFalse(t["wait_incomplete"])

    def test_last_heartbeat_wins_over_earlier_ones(self):
        t = tb.classify_phase_timings(tb.parse_battle_load_log(_log(
            *self._head(), _init_done(),
            _scene_load_wait(seq=6, ms=8000, polls=100, wait_ms=5000),
            _scene_load_wait(seq=7, ms=13000, polls=400, wait_ms=10000))))
        self.assertEqual(t["polls"], 400)
        self.assertEqual(t["wait_ms"], 10000)

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

    # --- heartbeat-sourced timings (2026-09-06 deep review) ------------------------ #
    # The polls=1 reading INVERTS between the two sources and the first cut printed the
    # FinishMissionLoadingBegin diagnosis for heartbeat data, sending a triager to #352 for
    # a stall nowhere near WaitForMeshesToBeLoaded.
    def _heartbeat_report(self, polls=1, wait_ms=8001):
        return self._report(
            _init(ms=1000), _init_done(ms=2000),
            _scene_load_wait(seq=8, ms=10000, polls=polls, wait_ms=wait_ms))

    def test_heartbeat_polls_one_is_not_reported_as_the_352_native_spin(self):
        r = self._heartbeat_report()
        self.assertIn("the block is BEFORE the loop", r)
        self.assertNotIn("BLOCKED inside one frame", r)
        self.assertNotIn("#352 WaitForMeshesToBeLoaded shape", r)

    def test_completed_wait_polls_one_still_reports_the_352_native_spin(self):
        # The original reading must survive for the source it was written for.
        r = self._report(_init(ms=1000), _init_done(ms=2000),
                         _finish_begin(ms=12000, polls=1, wait_ms=10000))
        self.assertIn("BLOCKED inside one frame", r)
        self.assertNotIn("the block is BEFORE the loop", r)

    def test_heartbeat_gives_bucket2_a_lower_bound_instead_of_a_question_mark(self):
        r = self._heartbeat_report()
        self.assertIn(">=8001ms", r)

    def test_heartbeat_names_bucket2_dominant_not_a_closed_bucket(self):
        # bucket1 is measurable here and bucket2 is not, so the old code named bucket1 as
        # dominant on precisely the logs where bucket2 is the whole story.
        r = self._heartbeat_report()
        self.assertIn("dominant: bucket2", r)
        self.assertNotIn("dominant: bucket1", r)

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


class CrossLanguageContractTests(unittest.TestCase):
    """The C# emitter and this parser are twin literals. Nothing but a test keeps them equal."""

    def test_every_scene_terminal_has_a_hint(self):
        # classify() indexes _SCENE_HINTS[ph] unguarded, so a terminal with no hint is a
        # KeyError on exactly the log its phase was added for.
        self.assertEqual(set(tb.SCENE_TERMINALS) - set(tb._SCENE_HINTS), set())

    def test_scene_load_wait_interval_matches_the_csharp_constant(self):
        # SCENE_LOAD_WAIT_INTERVAL_MS is interpolated into player-facing prose ("within Ns of
        # the t=+ stamp"), so if the C# side is retuned and this is not, the tool states a
        # fault window that is simply wrong, with a green build on both sides.
        src = pathlib.Path(__file__).resolve().parents[2] / "Main" / "Features" /             "BattleLoadDiagnostics" / "BattleLoadDiagnosticsService.cs"
        m = re.search(r"SceneLoadWaitEmitIntervalMs\s*=\s*(\d+)L", src.read_text(encoding="utf-8"))
        self.assertIsNotNone(m, "SceneLoadWaitEmitIntervalMs not found in the C# service")
        self.assertEqual(int(m.group(1)), tb.SCENE_LOAD_WAIT_INTERVAL_MS)


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


# --------------------------------------------------------------------------- #
# [MemStation] screen anchors (#386 follow-up)                                  #
# --------------------------------------------------------------------------- #
def _station(kind="enter", screen="GauntletInventoryScreen", priv=4211, ws=3900,
             heap=654, used=14003, limit=31646, avail=6200, load=61, prefix=True,
             ts="2026-06-17 12:00:00"):
    payload = (f"[MemStation] {kind} screen='{screen}' "
               f"privMB={priv} wsMB={ws} heapMB={heap} "
               f"sysCommitUsedMB={used} sysCommitLimitMB={limit} "
               f"availPhysMB={avail} memLoad={load}%")
    return _line("INFO", payload, ts=ts) if prefix else payload


class MemStationTests(unittest.TestCase):
    def test_pinned_station_enter_literal_parses_to_pinned_numbers(self):
        # Cross-language twin pin — the C# side asserts it emits exactly this line
        # (MemoryStationSamplerTests.FormatStation_EnterWithKnownValues_MatchesPinnedLiteral).
        tl = tb.parse_battle_load_log(_log(_line("INFO", PINNED_MEM_STATION_ENTER)))
        self.assertEqual(len(tl.mem_stations), 1)
        s = tl.mem_stations[0]
        self.assertEqual((s.kind, s.screen), ("enter", "GauntletInventoryScreen"))
        self.assertEqual((s.priv_mb, s.ws_mb, s.heap_mb, s.commit_used_mb,
                          s.commit_limit_mb, s.avail_phys_mb, s.mem_load),
                         (4211, 3900, 654, 14003, 31646, 6200, 61))

    def test_pinned_station_exit_literal_parses_to_pinned_numbers(self):
        tl = tb.parse_battle_load_log(_log(_line("INFO", PINNED_MEM_STATION_EXIT)))
        s = tl.mem_stations[0]
        self.assertEqual(s.kind, "exit")
        self.assertEqual(s.commit_used_mb, 29847)
        self.assertEqual(s.commit_limit_mb, 31646)

    def test_station_lines_do_not_enter_the_memsample_trend(self):
        # The corruption guard. Station readings are event-driven; letting one into
        # mem_samples would skew first/peak/last and therefore the MEMORY PRESSURE note.
        tl = tb.parse_battle_load_log(_log(_station(), _station(kind="exit")))
        self.assertEqual(len(tl.mem_stations), 2)
        self.assertEqual(tl.mem_samples, [])
        self.assertIsNone(tb.classify_memory(tl))

    def test_memsample_lines_do_not_enter_the_station_series(self):
        tl = tb.parse_battle_load_log(_log(_mem(), _mem_session()))
        self.assertEqual(tl.mem_stations, [])
        self.assertIsNone(tb.classify_stations(tl))

    def test_station_delta_names_the_screen_with_the_largest_growth(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", screen="MapScreen", priv=10000),
            _station(kind="exit", screen="MapScreen", priv=10100),
            _station(kind="enter", screen="GauntletInventoryScreen", priv=10100),
            _station(kind="exit", screen="GauntletInventoryScreen", priv=15500),
        ))
        st = tb.classify_stations(tl)
        self.assertEqual(st["transitions"], 4)
        self.assertEqual(st["top"], "GauntletInventoryScreen")
        top = st["by_screen"][0]
        self.assertEqual(top["visits"], 1)
        self.assertEqual(top["total_delta_mb"], 5400)
        self.assertEqual(top["max_visit_delta_mb"], 5400)

    def test_repeat_visits_accumulate_and_report_the_worst_single_visit(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", priv=10000), _station(kind="exit", priv=13000),
            _station(kind="enter", priv=13000), _station(kind="exit", priv=13100),
        ))
        b = tb.classify_stations(tl)["by_screen"][0]
        self.assertEqual(b["visits"], 2)
        self.assertEqual(b["total_delta_mb"], 3100)
        self.assertEqual(b["max_visit_delta_mb"], 3000)

    def test_unmatched_enter_is_counted_not_dropped_and_never_produces_a_delta(self):
        # ScreenManager's bulk teardown is not confirmed to fire OnPopScreen per removed
        # screen, so an orphan enter is an expected shape. It must not become a 0 delta.
        tl = tb.parse_battle_load_log(_log(_station(kind="enter", priv=10000)))
        b = tb.classify_stations(tl)["by_screen"][0]
        self.assertEqual(b["unmatched_enters"], 1)
        self.assertEqual(b["visits"], 0)
        self.assertEqual(b["total_delta_mb"], 0)

    # An exit with no enter cannot be measured, but it is COUNTED: silently discarding it made
    # an incomplete station series look complete, which is the coverage-honesty failure this
    # tool exists to avoid.
    def test_exit_without_enter_is_counted_not_measured_and_not_silently_dropped(self):
        tl = tb.parse_battle_load_log(_log(_station(kind="exit", priv=10000)))
        st = tb.classify_stations(tl)
        self.assertEqual(st["transitions"], 1)
        self.assertEqual(st["unmatched_exits"], 1)
        b = st["by_screen"][0]
        self.assertEqual(b["visits"], 0)
        self.assertEqual(b["total_delta_mb"], 0)
        self.assertEqual(b["unmatched_exits"], 1)
        self.assertIsNone(b["max_visit_delta_mb"])

    def test_log_without_station_lines_produces_byte_identical_report(self):
        # The additive-contract guard: pre-feature logs must render exactly as before.
        text = _log(_encounter(), _open_new(), _mission_init(), _mem())
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None)
        self.assertNotIn("Memory by station", report)

    def test_report_names_the_top_station(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", priv=10000),
            _station(kind="exit", priv=15400)))
        report = tb.format_report(tb.classify(tl), tl, None)
        self.assertIn("Memory by station (2 transitions):", report)
        self.assertIn("GauntletInventoryScreen", report)
        self.assertIn("+5400 MB total", report)

    def test_station_lines_do_not_alter_the_verdict_or_terminal_phase(self):
        without = _log(_encounter(), _open_new(), _mission_init())
        with_st = _log(_encounter(), _open_new(), _station(), _mission_init(),
                       _station(kind="exit"))
        a = tb.classify(tb.parse_battle_load_log(without))
        b = tb.classify(tb.parse_battle_load_log(with_st))
        self.assertEqual(a.kind, b.kind)
        self.assertEqual(a.summary, b.summary)

    def test_malformed_station_line_is_skipped_without_raising(self):
        tl = tb.parse_battle_load_log(_log("[MemStation] enter screen='X' privMB=oops"))
        self.assertEqual(tl.mem_stations, [])

    # --- Regression: the report used to CRASH on this shape --------------------------------
    # classify_stations returns a truthy dict with an empty by_screen whenever every station is
    # an exit with no matching enter (the sampler subscribed while a screen was already open).
    # format_report then ran max() over an empty sequence and raised ValueError, taking the whole
    # run with it: no verdict, no --json, a raw traceback, and exit code 1 - the same exit code a
    # correctly diagnosed hang produces.
    def test_exit_only_station_log_does_not_crash_the_report(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="exit", priv=10000)))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("Memory by station (1 transitions):", report)
        self.assertIn("1 unmatched exit(s)", report)
        self.assertIn("n/a", report)   # no measurable visit, and it says so

    def test_exit_only_station_log_still_produces_a_verdict(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(), _station(kind="exit")))
        self.assertEqual(tb.classify(tl).kind, "SCENE")

    # --- Regression: a screen where memory FELL --------------------------------------------
    # The feature exists because commit and working set move independently, so a negative delta
    # is an expected reading, not an error. max_visit_delta_mb used to be seeded at 0, which is
    # outside the domain of an all-negative delta set, so the seed won every comparison and the
    # report printed "(max visit +0 MB)" beside a large negative total - contradicting it.
    def test_all_negative_deltas_report_the_least_negative_visit_not_zero(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", screen="ShrinkingScreen", priv=10000),
            _station(kind="exit", screen="ShrinkingScreen", priv=9000),
            _station(kind="enter", screen="ShrinkingScreen", priv=9000),
            _station(kind="exit", screen="ShrinkingScreen", priv=8500)))

        b = tb.classify_stations(tl)["by_screen"][0]

        self.assertEqual(b["total_delta_mb"], -1500)
        self.assertEqual(b["max_visit_delta_mb"], -500)
        self.assertNotEqual(b["max_visit_delta_mb"], 0)

    def test_negative_delta_renders_without_claiming_a_flat_visit(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", screen="ShrinkingScreen", priv=10000),
            _station(kind="exit", screen="ShrinkingScreen", priv=9550)))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("-450 MB total", report)
        self.assertIn("max visit   -450 MB", report)
        self.assertNotIn("+0 MB", report)

    def test_screen_with_only_unmatched_enters_renders_max_visit_as_not_available(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", screen="NeverClosed", priv=10000)))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("n/a", report)
        self.assertIn("1 unmatched enter(s)", report)

    # --- Nested screens ---------------------------------------------------------------------
    # Pairing is per screen NAME with no nesting awareness, so an outer screen's delta includes
    # whatever an inner screen grew while stacked on top of it. Both screens then report that
    # growth and summing the column double-counts it. The report has to say so itself, not only
    # in doc prose the reader may not have open.
    def test_nested_screens_are_paired_correctly_and_flagged_as_overlapping(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", screen="MapScreen", priv=10000),
            _station(kind="enter", screen="GauntletInventoryScreen", priv=10000),
            _station(kind="exit", screen="GauntletInventoryScreen", priv=13000),
            _station(kind="exit", screen="MapScreen", priv=13000)))

        st = tb.classify_stations(tl)
        by = {b["screen"]: b for b in st["by_screen"]}

        # Each screen is paired with its OWN enter despite the interleaving.
        self.assertEqual(by["GauntletInventoryScreen"]["total_delta_mb"], 3000)
        self.assertEqual(by["MapScreen"]["total_delta_mb"], 3000)
        # ...and the double-count risk is surfaced on the OUTER screen, whose delta is the
        # inflated one. The inner screen's own delta is clean and is not flagged.
        self.assertTrue(st["nested_overlap"])
        self.assertEqual(by["MapScreen"]["nested_visits"], 1)
        self.assertEqual(by["GauntletInventoryScreen"]["nested_visits"], 0)

    def test_nested_overlap_note_appears_in_the_report(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", screen="MapScreen", priv=10000),
            _station(kind="enter", screen="GauntletInventoryScreen", priv=10000),
            _station(kind="exit", screen="GauntletInventoryScreen", priv=13000),
            _station(kind="exit", screen="MapScreen", priv=13000)))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("do NOT sum these", report)

    # --- Session-reset marker -----------------------------------------------------------
    # The reset is a discontinuity in the artefact under analysis. Without segmenting on it, an
    # enter left pending before a return to the main menu would pair with an exit after it and
    # invent a delta spanning the boundary.
    def test_session_reset_marker_is_counted_and_is_not_a_measurement(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", screen="MapScreen", priv=10000),
            _line("INFO", "[MemStation] session-reset reason=main-menu budget=2000"),
            _station(kind="enter", screen="MapScreen", priv=12000),
            _station(kind="exit", screen="MapScreen", priv=12500)))

        st = tb.classify_stations(tl)

        self.assertEqual(st["session_resets"], 1)
        # 3 real stations; the marker is not one of them.
        self.assertEqual(st["transitions"], 3)
        b = st["by_screen"][0]
        # The post-reset pair measured 500, NOT 2500 spanning the boundary.
        self.assertEqual(b["total_delta_mb"], 500)
        self.assertEqual(b["unmatched_enters"], 0)

    def test_session_reset_note_appears_in_the_report(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", priv=10000),
            _station(kind="exit", priv=10500),
            _line("INFO", "[MemStation] session-reset reason=main-menu budget=2000")))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("session reset(s)", report)

    # The window bound decides what any delta can mean, so the report states it.
    def test_report_states_the_window_semantics(self):
        tl = tb.parse_battle_load_log(_log(
            _encounter(), _open_new(), _mission_init(),
            _station(kind="enter", priv=10000), _station(kind="exit", priv=10500)))

        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("AFTER HandleInitialize", report)

    def test_sequential_different_screens_are_not_flagged_as_overlapping(self):
        tl = tb.parse_battle_load_log(_log(
            _station(kind="enter", screen="MapScreen", priv=10000),
            _station(kind="exit", screen="MapScreen", priv=10100),
            _station(kind="enter", screen="GauntletInventoryScreen", priv=10100),
            _station(kind="exit", screen="GauntletInventoryScreen", priv=11000)))

        self.assertFalse(tb.classify_stations(tl)["nested_overlap"])


# --------------------------------------------------------------------------- #
# The render-wait window (bundle b18f3441, 2026-09-04)                         #
# --------------------------------------------------------------------------- #
class RenderWaitTests(unittest.TestCase):
    """FinishMissionLoadingDone -> BattlePlayable is the SceneView.ReadyToRender gate. A
    falling shaders= count is a cold shader cache (a slow load); a frozen one is a wedge."""

    def test_render_wait_detail_literals_match_the_csharp_formatter(self):
        # The whole cross-language contract in five lines: if the C# formatter changes, the
        # regexes below stop matching real logs and this pin is what says so.
        self.assertEqual("290000", tb._WAITED_MS_RE.search(PINNED_RENDER_WAIT_BOTH).group(1))
        self.assertEqual("412", tb._SHADERS_RE.search(PINNED_RENDER_WAIT_BOTH).group(1))
        self.assertIsNone(tb._WAITED_MS_RE.search(PINNED_RENDER_WAIT_NO_ORIGIN))
        self.assertIsNone(tb._SHADERS_RE.search(PINNED_RENDER_WAIT_NO_SHADERS))
        self.assertIsNone(tb._SHADERS_RE.search(PINNED_RENDER_WAIT_NEITHER))

    def test_waited_ms_token_is_not_matched_by_the_finish_wait_regex(self):
        # waitMs= and waitedMs= are different tokens on different phases; a sloppy regex
        # would let bucket2's wait leak into the render-wait reading and back.
        self.assertIsNone(tb._WAIT_MS_RE.search(PINNED_RENDER_WAIT_BOTH))
        self.assertIsNone(tb._WAITED_MS_RE.search(PINNED_WAIT_WITH_MS))

    def test_terminal_render_wait_with_shaders_in_flight_is_cold_cache(self):
        text = _log(_init(), _init_done(), _finish_begin(), _after_start_begin(),
                    _after_start_done(), _finish_done(),
                    _render_wait(shaders=478), _render_wait(seq=11, ms=200000, shaders=412))
        v = tb.triage(text)

        self.assertEqual(v.kind, "RENDER_WAIT")
        self.assertIn("COLD SHADER CACHE", v.summary)
        self.assertIn("412", v.summary)

    def test_terminal_render_wait_with_zero_shaders_is_a_real_wedge(self):
        text = _log(_init(), _init_done(), _finish_begin(), _after_start_begin(),
                    _after_start_done(), _finish_done(), _render_wait(shaders=0))
        v = tb.triage(text)

        self.assertEqual(v.kind, "RENDER_WAIT")
        self.assertIn("real wedge", v.summary)
        self.assertNotIn("COLD SHADER CACHE", v.summary)

    def test_terminal_render_wait_without_shaders_token_says_unmeasured(self):
        text = _log(_init(), _init_done(), _finish_begin(), _after_start_begin(),
                    _after_start_done(), _finish_done(), _render_wait(shaders=None))
        v = tb.triage(text)

        self.assertEqual(v.kind, "RENDER_WAIT")
        self.assertIn("unmeasured, not zero", v.summary)

    def test_render_wait_is_a_hang_kind_so_the_cli_exits_one(self):
        self.assertIn("RENDER_WAIT", tb.HANG_KINDS)

    def test_render_wait_has_next_steps(self):
        self.assertTrue(tb._NEXT_STEPS.get("RENDER_WAIT"))

    def test_timings_report_a_draining_queue(self):
        tl = tb.parse_battle_load_log(_log(
            _init(), _init_done(), _finish_begin(), _after_start_begin(),
            _after_start_done(), _finish_done(),
            _render_wait(seq=10, ms=30000, waited_ms=10000, shaders=478),
            _render_wait(seq=11, ms=200000, waited_ms=180000, shaders=120)))
        r = tb.classify_phase_timings(tl)["render_wait"]

        self.assertEqual(2, r["samples"])
        self.assertEqual(478, r["shaders_first"])
        self.assertEqual(120, r["shaders_last"])
        self.assertEqual(478, r["shaders_peak"])
        self.assertTrue(r["draining"])
        self.assertEqual(180000, r["waited_ms_last"])

    def test_timings_report_a_frozen_queue_as_not_draining(self):
        tl = tb.parse_battle_load_log(_log(
            _init(), _init_done(), _finish_begin(), _after_start_begin(),
            _after_start_done(), _finish_done(),
            _render_wait(seq=10, shaders=412), _render_wait(seq=11, shaders=412)))

        self.assertIsNone(tb.classify_phase_timings(tl)["render_wait"]["draining"])

    def test_render_wait_block_absent_for_a_log_without_the_marker(self):
        # Old logs keep a byte-identical report: the key exists and is None, never zero-filled.
        tl = tb.parse_battle_load_log(_log(
            _init(), _init_done(), _finish_begin(), _after_start_begin(),
            _after_start_done(), _finish_done()))

        self.assertIsNone(tb.classify_phase_timings(tl)["render_wait"])

    def test_finish_done_terminal_names_the_render_gate(self):
        # The pre-marker shape, which is what the b18f3441 bundle itself looks like.
        text = _log(_init(), _init_done(), _finish_begin(), _after_start_begin(),
                    _after_start_done(), _finish_done())
        v = tb.triage(text)

        self.assertEqual(v.kind, "POST_EQUIP")
        self.assertTrue(any("ReadyToRender" in n for n in v.notes))
        self.assertTrue(any("COLD SHADER CACHE" in n for n in v.notes))


class MultiMissionScopeTests(unittest.TestCase):
    """A session log holds several missions. An earlier one that completed must not mask a
    final load that stalled: the b18f3441 bundle reported COMPLETED and "the battle-load
    path is clean" for a log whose last mission never ticked and fired the watchdog."""

    def test_earlier_completed_mission_does_not_mask_a_later_stall(self):
        text = _log(
            _init(seq=1, ms=1000), _playable(),
            _init(seq=10, ms=50000), _init_done(seq=11, ms=51000),
            _finish_begin(seq=12, ms=55000), _after_start_begin(seq=13, ms=55500),
            _after_start_done(seq=14, ms=58000), _finish_done(seq=15, ms=58200),
            _watchdog(305, "FinishMissionLoadingDone"))
        v = tb.triage(text)

        self.assertEqual(v.kind, "POST_EQUIP")

    def test_a_single_mission_that_completed_is_still_completed(self):
        text = _log(_encounter(), _open_new(), _scene_selected(), _mission_init(),
                    _equip_begin(5, 0), _equip_ok(6, 0), _playable())

        self.assertEqual(tb.triage(text).kind, "COMPLETED")

    def test_completion_without_a_mission_init_anchor_still_reads_completed(self):
        # Truncated log that ENDS on BattlePlayable: no segment anchor, but the log ending on
        # a completed load is the one honest basis for calling it clean.
        self.assertEqual(tb.triage(_log(_equip_ok(6, 0), _playable())).kind, "COMPLETED")

    def test_earlier_mission_is_anchored_by_encounter_start_not_only_mission_init(self):
        # MissionOpenNew and EncounterStart also start a mission. Anchoring on
        # MissionInitialize alone loses the boundary whenever the later load died before
        # reaching it, which is exactly when the verdict matters most.
        text = _log(
            _encounter(), _mission_init(), _playable(),
            _open_new(),
            _watchdog(305, "MissionOpenNew"))

        self.assertNotEqual(tb.triage(text).kind, "COMPLETED")

    def test_no_anchor_at_all_with_a_later_load_does_not_read_completed(self):
        # The residual hole the first scoping pass left: a truncated capture holding an
        # earlier completed mission AND a later stalled one, with no start marker for either
        # (log rotated mid-file, or diagnostics enabled mid-mission). Falling back to
        # whole-log scanning here reproduced the exact b18f3441 false COMPLETED.
        text = _log(
            _equip_ok(1, 0),
            _playable(),
            _finish_done(seq=3, ms=50000))
        v = tb.triage(text)

        self.assertNotEqual(v.kind, "COMPLETED",
                            "a load that ran past an earlier BattlePlayable must not be "
                            "reported as clean just because no anchor survived truncation")
        self.assertEqual(v.kind, "POST_EQUIP")


# --------------------------------------------------------------------------- #
# The WATCHDOG line's token block (review pass 2, 2026-09-04)                   #
# --------------------------------------------------------------------------- #
class WatchdogTokenTests(unittest.TestCase):
    """`FormatShaderToken` / `FormatChurnToken` insert text between the em-dash and the literal
    `last`. _WATCHDOG_RE required whitespace there until 2026-09-04, so it matched ONLY the
    token-free shape and silently dropped the watchdog line for every bundle carrying real
    telemetry. The fixture above hid it by never emitting a token."""

    def test_watchdog_line_with_shaders_token_still_parses(self):
        tl = tb.parse_battle_load_log(_log(
            _mission_init(), _watchdog(305, "FinishMissionLoadingDone", tokens="shaders=412")))

        self.assertIsNotNone(tl.watchdog, "a watchdog line carrying shaders= must not be dropped")
        self.assertEqual(305, tl.watchdog.elapsed_seconds)
        self.assertEqual("FinishMissionLoadingDone", tl.watchdog.last_phase)
        self.assertEqual("shaders=412", tl.watchdog.tokens)

    def test_watchdog_line_with_shaders_and_churn_tokens_still_parses(self):
        tl = tb.parse_battle_load_log(_log(
            _mission_init(),
            _watchdog(900, "WaitingForRender", tokens="shaders=412 churn-capped")))

        self.assertIsNotNone(tl.watchdog)
        self.assertEqual(900, tl.watchdog.elapsed_seconds)
        self.assertEqual("shaders=412 churn-capped", tl.watchdog.tokens)

    def test_watchdog_line_without_tokens_still_parses(self):
        # The pre-2026-09-04 shape, which is what bundle b18f3441 itself carries.
        tl = tb.parse_battle_load_log(_log(
            _mission_init(), _watchdog(305, "FinishMissionLoadingDone")))

        self.assertIsNotNone(tl.watchdog)
        self.assertEqual("", tl.watchdog.tokens)

    def test_status_line_containing_the_word_last_is_not_mistaken_for_the_separator(self):
        # The token block is non-greedy, so it stops at the FIRST `last`.
        tl = tb.parse_battle_load_log(_log(
            _mission_init(),
            _watchdog(305, "AgentEquipBegin", detail="note='the last one'", tokens="shaders=1")))

        self.assertIsNotNone(tl.watchdog)
        self.assertEqual("shaders=1", tl.watchdog.tokens)
        self.assertEqual("AgentEquipBegin", tl.watchdog.last_phase)

    def test_report_surfaces_the_reading_the_watchdog_fired_on(self):
        text = _log(_mission_init(),
                    _watchdog(900, "WaitingForRender", tokens="shaders=412 churn-capped"))
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertIn("reading at fire: shaders=412 churn-capped", report)
        self.assertIn("STILL MOVING", report,
                      "a churn-capped fire must be explained, since it calls for the opposite "
                      "response to a frozen compiler")

    def test_report_omits_the_reading_line_when_the_log_carried_no_tokens(self):
        text = _log(_mission_init(), _watchdog(305, "FinishMissionLoadingDone"))
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None)

        self.assertNotIn("reading at fire", report)


# --------------------------------------------------------------------------- #
# Verdict and timings must describe the SAME mission (review pass 2)           #
# --------------------------------------------------------------------------- #
class VerdictTimingsAgreementTests(unittest.TestCase):
    """`classify` anchors on any mission-start marker; `classify_phase_timings` anchors on
    MissionInitialize alone. When the last mission stalls BEFORE MissionInitialize, the ledger's
    anchor lands in an earlier mission and the report printed that mission's healthy bucket table
    directly under a stall verdict for a different load."""

    def _two_missions_second_stalls_early(self):
        return _log(
            _encounter(), _init(seq=2, ms=1000), _init_done(seq=3, ms=2000),
            _finish_begin(seq=4, ms=5000), _after_start_begin(seq=5, ms=5500),
            _after_start_done(seq=6, ms=8000), _finish_done(seq=7, ms=8200),
            _playable(),
            _line("INFO", "[BattleLoad] seq=9 t=+50000ms phase=EncounterStart mainPartySize=51"),
            _line("INFO", "[BattleLoad] seq=10 t=+51000ms phase=MissionOpenNew "
                          "mission='Battle' scene='battle_terrain_p'"))

    def test_verdict_describes_the_last_mission(self):
        self.assertEqual("PRE_SCENE", tb.triage(self._two_missions_second_stalls_early()).kind)

    def test_timings_are_withheld_rather_than_describing_a_different_mission(self):
        tl = tb.parse_battle_load_log(self._two_missions_second_stalls_early())

        self.assertIsNone(tb.classify_phase_timings(tl),
                          "rendering the earlier mission's buckets under a stall verdict for a "
                          "later one is worse than rendering nothing")

    def test_report_does_not_contradict_itself(self):
        text = self._two_missions_second_stalls_early()
        tl = tb.parse_battle_load_log(text)
        report = tb.format_report(tb.classify(tl), tl, None, None, tb.classify_phase_timings(tl))

        self.assertIn("PRE_SCENE", report)
        self.assertNotIn("Load timing", report)

    def test_single_mission_still_renders_its_ledger(self):
        tl = tb.parse_battle_load_log(_log(
            _init(), _init_done(), _finish_begin(), _after_start_begin(),
            _after_start_done(), _finish_done()))

        self.assertIsNotNone(tb.classify_phase_timings(tl))

    def test_two_missions_both_reaching_mission_init_still_render_the_later_ledger(self):
        tl = tb.parse_battle_load_log(_log(
            _init(seq=1, ms=1000), _init_done(seq=2, ms=2000), _finish_begin(seq=3, ms=3000),
            _after_start_begin(seq=4, ms=3500), _after_start_done(seq=5, ms=4000),
            _finish_done(seq=6, ms=4200), _playable(),
            _init(seq=8, ms=50000), _init_done(seq=9, ms=51000),
            _finish_begin(seq=10, ms=55000), _after_start_begin(seq=11, ms=55500),
            _after_start_done(seq=12, ms=58000), _finish_done(seq=13, ms=58200)))
        t = tb.classify_phase_timings(tl)

        self.assertIsNotNone(t)
        # bucket2 is MissionInitializeDone -> FinishMissionLoadingBegin, i.e. 55000-51000 for the
        # SECOND mission (the first would be 3000-2000 = 1000ms).
        b2 = next(b for b in t["buckets"] if b["name"] == "bucket2")
        self.assertEqual(4000, b2["ms"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
