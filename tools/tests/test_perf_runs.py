#!/usr/bin/env python3
"""Unit tests for the mission perf-row tool (tools/perf_runs.py).

Run:  python -B -m unittest tools.tests.test_perf_runs

Pure stdlib with synthetic taom_debug logs built in the REAL line formats: the FileLogger prefix
`[ts] [LEVEL]`, the [BattleLoad] load markers copied from a 2026-10-02 custom battle, and the
[MissionPerf] heartbeat. No game install needed.
"""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import perf_runs as pr  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "perf_runs.py"

# The pinned [MissionPerf] contract. Cross-language twin pin: FrameStatsTests.
# BuildLine_FormatsEveryFieldInvariantly asserts MissionPerfLine.Build emits EXACTLY this body.
PINNED_MISSION_PERF = ("[MissionPerf] t=+65s frames=300 fps=60.0 avgMs=16.67 p95Ms=25.50 "
                       "maxMs=40.3 agents=812 active=640 formations=9 gc0=12 gc1=3 gc2=1")

# The mission tick profiler's four data lines. Cross-language twin pin: TickProfileLinesTests.
# BuildTickProfile_SampleWindow_, BuildHitch_SampleFrame_, BuildPerfContext_SampleContext_ and
# BuildTickSummary_SampleMission_MatchesThePinnedLiteral assert the C# lines are EXACTLY these
# literals. Change both or neither.
PINNED_TICK_PROFILE = ("[TickProfile] t=+65s frames=300 wallMs=5000.00 preDisplayMs=12.50 "
                       "missionTickMs=812.40 preTickMs=40.10 waitTickMs=95.00 "
                       "agentTickMs=1500.00 otherMs=4040.00 allocKB=2048 "
                       "top=BehaviorTreeMissionLogic:410.20/300/3.10/512,"
                       "AdvancedCombatBehavior:120.00/900/1.50/64")
PINNED_HITCH = ("[Hitch] t=+72s frameMs=812.35 preDisplayMs=0.40 missionTickMs=5.20 "
                "preTickMs=0.30 waitTickMs=790.00 agentTickMs=795.10 otherMs=16.45 gc0=1 gc1=1 "
                "gc2=0 allocKB=96 top=BehaviorTreeMissionLogic:2.10,AdvancedCombatBehavior:1.30,"
                "MissionPerfHeartbeatBehavior:0.05")
PINNED_PERF_CONTEXT = ("[PerfContext] build=Debug jitOptimized=false clr=4.0.30319.42000 "
                       "serverGC=false latency=Interactive missionInProcess=1 "
                       "scene=battle_terrain_029 agents=0 textureQuality=1 "
                       "shadowQuality=2 particleDetail=1 ragdolls=3 memLoad=61 "
                       "availPhysMB=12034 tickProfiler=on diag=battleLoad,stallWatchdog,"
                       "stallBundle,exitSampler,freezeSampler,memSampler,missionPerf")
PINNED_TICK_SUMMARY = ("[TickSummary] frames=4500 wallMs=75000.00 preDisplayMs=187.50 "
                       "missionTickMs=12186.00 preTickMs=601.50 waitTickMs=1425.00 "
                       "agentTickMs=22500.00 otherMs=60600.00 allocKB=30720 hitches=3 "
                       "worstHitchMs=1104.20 worstHitchT=+72s "
                       "top=BehaviorTreeMissionLogic:6153.00/4500/3.10/7680,"
                       "AdvancedCombatBehavior:1800.00/13500/1.50/960")
DEFAULT_DIAG = ("battleLoad", "stallWatchdog", "stallBundle", "exitSampler", "freezeSampler",
                "memSampler", "missionPerf")

BASE = datetime(2026, 10, 2, 12, 0, 0)
SCENE = "battle_terrain_biome_148"


def _at(seconds):
    return (BASE + timedelta(seconds=seconds)).strftime("%Y-%m-%d %H:%M:%S")


def _line(payload, at=0, level="INFO"):
    """One FileLogger line: [ts] [LEVEL] <payload>."""
    return f"[{_at(at)}] [{level}] {payload}"


def _perf(t, fps=117.0, avg=8.55, p95=9.20, mx=14.0, agents=100, active=None,
          gc=(3, 0, 0), frames=None):
    frames = int(round(fps * 5)) if frames is None else frames
    active = agents if active is None else active
    return (f"[MissionPerf] t=+{t}s frames={frames} fps={fps:.1f} avgMs={avg:.2f} "
            f"p95Ms={p95:.2f} maxMs={mx:.1f} agents={agents} active={active} formations=2 "
            f"gc0={gc[0]} gc1={gc[1]} gc2={gc[2]}")


def _load(start, scene=SCENE, kind="CustomBattle"):
    """A completed load, copied from a 2026-10-02 custom battle (seq numbers trimmed, the
    MissionOpenNewDone line left out: test_mission_open_new_done_is_not_a_second_mission has it)."""
    return [
        _line(f"[BattleLoad] seq=1 t=+0ms phase=MissionOpenNew mission='{kind}' scene='{scene}'", start),
        _line(f"[BattleLoad] seq=4 t=+500ms phase=MissionInitialize scene='{scene}' "
              "gc=1369/448/75 heapMB=128 privMB=8779 wsMB=4764", start),
        _line(f"[BattleLoad] seq=5 t=+502ms phase=MissionInitializeDone scene='{scene}' "
              "gc=1369/448/75 heapMB=128 privMB=8783 wsMB=4764", start),
        _line("[BattleLoad] seq=6 t=+1247ms phase=FinishMissionLoadingBegin polls=22 waitMs=745 "
              "gc=1369/448/75 heapMB=129 privMB=8663 wsMB=5033", start + 1),
        _line("[BattleLoad] seq=7 t=+1442ms phase=MissionAfterStartBegin", start + 1),
        _line("[BattleLoad] seq=44 t=+1832ms phase=MissionAfterStartDone", start + 1),
        _line("[BattleLoad] seq=45 t=+1983ms phase=FinishMissionLoadingDone "
              "gc=1375/450/75 heapMB=141 privMB=8812 wsMB=5138", start + 1),
        _line(f"[BattleLoad] seq=47 t=+2857ms phase=BattlePlayable scene='{scene}' agents=0 "
              "gc=1376/450/75 heapMB=143 privMB=9791 wsMB=5156", start + 2),
    ]


def _windows(start, specs):
    """Heartbeat lines; each spec is a dict of _perf arguments. The FileLogger clock runs with t."""
    return [_line(_perf(**s), start + 1 + s["t"]) for s in specs]


def _steady(n=6, first_t=31, **kw):
    return [dict(t=first_t + 5 * i, **kw) for i in range(n)]


def _mission(start, specs, scene=SCENE, kind="CustomBattle", load=True):
    return (_load(start, scene, kind) if load else []) + _windows(start, specs)


def _rows(lines, name="taom_debug_test.log"):
    rows, _ = pr.rows_for_log("\n".join(lines), name)
    return rows


class PinnedLineTests(unittest.TestCase):
    def test_pinned_mission_perf_parses_to_its_numbers(self):
        for text in (PINNED_MISSION_PERF, _line(PINNED_MISSION_PERF)):
            w = pr.parse_mission_perf(text)
            self.assertIsNotNone(w)
            self.assertEqual((w.t, w.frames, w.agents, w.active, w.formations, w.gc0, w.gc1, w.gc2),
                             (65, 300, 812, 640, 9, 12, 3, 1))
            self.assertAlmostEqual(w.fps, 60.0)
            self.assertAlmostEqual(w.avg_ms, 16.67)
            self.assertAlmostEqual(w.p95_ms, 25.50)
            self.assertAlmostEqual(w.max_ms, 40.3)

    def test_pinned_tick_profile_parses_to_its_numbers(self):
        t = pr.parse_tick_profile(_line(PINNED_TICK_PROFILE))
        self.assertIsNotNone(t)
        self.assertEqual((t["t"], t["frames"]), (65, 300))
        self.assertAlmostEqual(t["wallMs"], 5000.0)
        self.assertAlmostEqual(t["missionTickMs"], 812.4)
        self.assertAlmostEqual(t["otherMs"], 4040.0)
        self.assertAlmostEqual(t["allocKB"], 2048.0)
        self.assertEqual([b["type"] for b in t["top"]],
                         ["BehaviorTreeMissionLogic", "AdvancedCombatBehavior"])
        self.assertEqual((t["top"][0]["ms"], t["top"][0]["calls"], t["top"][0]["max_ms"],
                          t["top"][0]["kb"]), (410.2, 300, 3.1, 512.0))

    def test_pinned_hitch_parses_and_names_its_dominant_phase(self):
        h = pr.parse_hitch(_line(PINNED_HITCH))
        self.assertIsNotNone(h)
        self.assertEqual((h["t"], h["gc0"], h["gc1"], h["gc2"]), (72, 1, 1, 0))
        self.assertAlmostEqual(h["frameMs"], 812.35)
        self.assertAlmostEqual(h["allocKB"], 96.0)
        self.assertEqual(h["phase"], "agentTickMs")  # 795.10 beats waitTickMs 790.00
        self.assertEqual([b["type"] for b in h["top"]],
                         ["BehaviorTreeMissionLogic", "AdvancedCombatBehavior",
                          "MissionPerfHeartbeatBehavior"])
        # "na" (no allocation counter) stays None, never 0.
        no_alloc = pr.parse_hitch(PINNED_HITCH.replace("allocKB=96", "allocKB=na"))
        self.assertIsNone(no_alloc["allocKB"])

    def test_pinned_perf_context_parses(self):
        c = pr.parse_context(_line(PINNED_PERF_CONTEXT))
        self.assertIsNotNone(c)
        self.assertEqual(c["build"], "Debug")
        self.assertEqual(c["missionInProcess"], 1)
        self.assertEqual(c["scene"], "battle_terrain_029")
        self.assertEqual(c["textureQuality"], "1")
        self.assertEqual(c["tickProfiler"], "on")
        self.assertEqual(c["memLoad"], 61.0)
        self.assertEqual(c["diag"], list(DEFAULT_DIAG))
        no_mem = pr.parse_context(PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=na"))
        self.assertIsNone(no_mem["memLoad"])


class SegmentTests(unittest.TestCase):
    def test_one_mission_gives_one_row_with_its_load_buckets(self):
        rows = _rows(_mission(0, [dict(t=6)] + _steady()))
        self.assertEqual(len(rows), 1)
        r = rows[0]
        self.assertEqual((r["mission"], r["scene"], r["opened_by"]),
                         ("CustomBattle", SCENE, "MissionOpenNew"))
        self.assertEqual((r["windows"], r["steady_windows"]), (7, 6))
        self.assertEqual(r["load_ms"], 2857)
        self.assertEqual(r["load_buckets"], {"bucket1": 2, "bucket2": 745, "bucket3a": 195,
                                             "bucket3b": 390, "bucket3c": 151, "bucket4": 874})
        self.assertEqual(r["load_dominant"], "bucket4")

    def test_three_missions_in_one_log_give_three_rows_in_order(self):
        lines = (_mission(0, _steady(agents=273), scene="a")
                 + _mission(100, _steady(agents=83), scene="b")
                 + _mission(200, _steady(agents=164), scene="c"))
        rows = _rows(lines)
        self.assertEqual([r["scene"] for r in rows], ["a", "b", "c"])
        self.assertEqual([r["mission_index"] for r in rows], [1, 2, 3])
        self.assertEqual([r["agents_max"] for r in rows], [273, 83, 164])

    def test_log_without_the_profiler_lines_parses_without_them(self):
        r = _rows(_mission(0, _steady()))[0]
        self.assertIsNone(r["context"])
        self.assertIsNone(r["tick_profile"])
        self.assertEqual(r["hitches"]["count"], 0)
        self.assertEqual(r["malformed"], 0)

    def test_orphan_encounter_between_missions_keeps_the_previous_load_buckets(self):
        # 2026-09-29 campaign log shape: an EncounterStart that opened no mission sits after
        # mission 1, then the next encounter opens mission 2. The ledger anchors on the LAST
        # mission start, so mission 1's buckets survive only if its load is cut at BattlePlayable.
        lines = (_mission(0, _steady(), kind="Battle")
                 + [_line("[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=1", 90)]
                 + [_line("[BattleLoad] seq=1 t=+0ms phase=EncounterStart mainPartySize=1", 95)]
                 + _mission(100, _steady(), kind="Battle"))
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertIsNotNone(rows[0]["load_buckets"])
        self.assertEqual(rows[0]["load_dominant"], "bucket4")

    def test_heartbeat_only_log_splits_on_a_clock_restart(self):
        lines = _mission(0, _steady(), load=False) + _mission(100, _steady(), load=False)
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertEqual([r["opened_by"] for r in rows], ["MissionPerf", "MissionPerf"])

    def test_heartbeat_only_log_splits_when_t_grows_slower_than_the_wall_clock(self):
        # Mission 1 ends after one window at t=+6; mission 2's first window is t=+7, sixty
        # seconds of wall clock later. t grew by 1 while the clock grew by 60: a new mission.
        lines = _mission(0, [dict(t=6)], load=False) + _mission(60, [dict(t=7)], load=False)
        self.assertEqual(len(_rows(lines)), 2)

    def test_pause_inside_one_mission_keeps_one_row(self):
        # t and the wall clock jump together (64 s), so it is the same mission.
        lines = [_line(_perf(t=31), 32), _line(_perf(t=95), 96)]
        self.assertEqual(len(_rows(lines)), 1)

    def test_perf_context_opens_the_mission_when_battle_load_is_absent(self):
        ctx2 = PINNED_PERF_CONTEXT.replace("missionInProcess=1", "missionInProcess=3")
        lines = (_mission(0, _steady(), load=False)
                 + [_line(ctx2, 95)] + _mission(100, _steady(), load=False))
        rows = _rows(lines)
        self.assertEqual(len(rows), 2)
        self.assertIsNone(rows[0]["context"])
        self.assertEqual(rows[1]["context"]["missionInProcess"], 3)
        self.assertEqual(rows[1]["opened_by"], "PerfContext")

    def test_perf_context_after_mission_open_stays_in_that_mission(self):
        lines = _load(0) + [_line(PINNED_PERF_CONTEXT, 3)] + _windows(0, _steady())
        rows = _rows(lines)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["context"]["build"], "Debug")

    def test_heartbeat_disabled_line_is_noted_not_malformed(self):
        lines = _mission(0, _steady()) + [_line(
            "[MissionPerf] heartbeat disabled for this mission after NullReferenceException: x",
            70, level="ERROR")]
        r = _rows(lines)[0]
        self.assertTrue(r["heartbeat_disabled"])
        self.assertEqual(r["malformed"], 0)

    def test_mission_that_never_ticked_is_a_row_without_windows(self):
        r = _rows(_load(0)[:3])[0]
        self.assertEqual(r["windows"], 0)
        self.assertIsNone(r["fps_median"])
        self.assertIsNone(r["spawn_max_ms"])


class SteadyStateTests(unittest.TestCase):
    def test_spawn_window_is_reported_alone_and_excluded_from_steady_stats(self):
        specs = [dict(t=6, fps=101.6, mx=624.6, gc=(41, 5, 0))] + _steady(mx=14.0)
        r = _rows(_mission(0, specs))[0]
        self.assertAlmostEqual(r["spawn_max_ms"], 624.6)
        self.assertEqual(r["spawn_gc0"], 41)
        self.assertAlmostEqual(r["max_ms_max"], 14.0)
        self.assertAlmostEqual(r["fps_median"], 117.0)

    def test_windows_before_30s_or_with_no_active_agents_are_not_steady(self):
        specs = [dict(t=6), dict(t=26, fps=50.0), dict(t=31), dict(t=36, fps=10.0, active=0)]
        r = _rows(_mission(0, specs))[0]
        self.assertEqual(r["steady_windows"], 1)
        self.assertAlmostEqual(r["fps_median"], 117.0)

    def test_medians_max_and_gc_rate_per_minute(self):
        specs = [dict(t=6),
                 dict(t=31, fps=120.0, avg=8.0, p95=9.0, mx=12.0, gc=(3, 1, 0), frames=600),
                 dict(t=36, fps=120.0, avg=9.0, p95=10.0, mx=40.0, gc=(6, 0, 0), frames=600),
                 dict(t=41, fps=120.0, avg=10.0, p95=11.0, mx=15.0, gc=(9, 2, 1), frames=600)]
        r = _rows(_mission(0, specs))[0]
        self.assertAlmostEqual(r["avg_ms_median"], 9.0)
        self.assertAlmostEqual(r["p95_ms_median"], 10.0)
        self.assertAlmostEqual(r["max_ms_max"], 40.0)
        # 18 gen-0 collections over 3 x 5 s = 15 s is 72 a minute.
        self.assertAlmostEqual(r["gc0_per_min"], 72.0)
        self.assertAlmostEqual(r["gc1_per_min"], 12.0)
        self.assertAlmostEqual(r["gc2_per_min"], 4.0)

    def test_no_steady_windows_reports_none_not_zero(self):
        r = _rows(_mission(0, [dict(t=6), dict(t=11), dict(t=16)]))[0]
        self.assertEqual(r["steady_windows"], 0)
        for key in ("fps_median", "avg_ms_median", "p95_ms_median", "max_ms_max", "gc0_per_min"):
            self.assertIsNone(r[key], key)


class FlagTests(unittest.TestCase):
    def _flags(self, lines):
        return _rows(lines)[0]["flags"]

    def test_frame_cap_on_a_plateau(self):
        specs = [dict(t=31 + 5 * i, fps=f) for i, f in enumerate([117.0, 116.8, 117.2, 97.3, 117.1])]
        self.assertIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_frame_cap_when_fps_holds_while_active_agents_fall(self):
        fps = [117.0, 100.0, 90.0, 80.0, 116.6]
        active = [300, 260, 230, 200, 150]
        specs = [dict(t=6)] + [dict(t=31 + 5 * i, fps=f, agents=300, active=a)
                               for i, (f, a) in enumerate(zip(fps, active))]
        self.assertIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_no_frame_cap_when_fps_moves(self):
        specs = [dict(t=31 + 5 * i, fps=f) for i, f in enumerate([60.0, 75.0, 90.0, 105.0, 120.0])]
        self.assertNotIn("FRAME_CAP", self._flags(_mission(0, specs)))

    def test_no_frame_cap_with_fewer_than_four_steady_windows(self):
        self.assertNotIn("FRAME_CAP", self._flags(_mission(0, [dict(t=6)] + _steady(n=3))))

    def test_memory_pressure_from_a_mem_sample_at_80_percent(self):
        sample = ("[MemSample] privMB=7526 wsMB=4240 heapMB=77 sysCommitUsedMB=93799 "
                  "sysCommitLimitMB=128662 availPhysMB=10529 memLoad={}%")
        hot = _mission(0, _steady()) + [_line(sample.format(83), 50)]
        cool = _mission(0, _steady()) + [_line(sample.format(46), 50)]
        self.assertIn("MEMORY_PRESSURE", self._flags(hot))
        self.assertNotIn("MEMORY_PRESSURE", self._flags(cool))

    def test_memory_pressure_from_perf_context(self):
        hot = _load(0) + [_line(PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=83"), 3)] \
            + _windows(0, _steady())
        cool = _load(0) + [_line(PINNED_PERF_CONTEXT, 3)] + _windows(0, _steady())
        self.assertIn("MEMORY_PRESSURE", self._flags(hot))
        self.assertNotIn("MEMORY_PRESSURE", self._flags(cool))

    def test_diag_on_only_beyond_the_default_diagnostics(self):
        # PINNED_PERF_CONTEXT lists all seven default-on diagnostics (missionPerf among them) and
        # tickProfiler=on. A default install lists the same seven with the profiler off: no flag.
        def flags(ctx):
            return self._flags(_load(0) + [_line(ctx, 3)] + _windows(0, _steady()))
        default = PINNED_PERF_CONTEXT.replace("tickProfiler=on", "tickProfiler=off")
        self.assertNotIn("DIAG_ON", flags(default))
        self.assertNotIn("DIAG_ON", flags(default.split(" diag=")[0] + " diag=none"))
        self.assertIn("DIAG_ON", flags(default.replace("missionPerf", "missionPerf,troopCountDiag")))
        self.assertIn("DIAG_ON", flags(PINNED_PERF_CONTEXT))

    def test_dirty_build_and_pair_mismatch_from_the_build_stamp_line(self):
        # Shapes from BuildStampReport.BuildReport. DescribeVerdict writes an em dash after
        # MISMATCH; chr(0x2014) keeps this file ASCII.
        dirty_mismatch = _line(
            "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4.dirty "
            "TAOM.Dependencies=v0.1.0.0 build.20261001-134226Z+6b00881b.dirty "
            "MISMATCH " + chr(0x2014) + " built 1d 02h 55m apart. These modules were not built "
            "together; update BOTH from the same release or expect the preview patches to fail "
            "(issue #371).")
        clean_paired = _line(
            "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4 "
            "TAOM.Dependencies=v0.1.0.0 build.20261002-163730Z+bc39f6e4 (pair OK)")
        flags = self._flags([dirty_mismatch] + _mission(10, _steady()))
        self.assertIn("DIRTY_BUILD", flags)
        self.assertIn("BUILD_PAIR_MISMATCH", flags)
        flags = self._flags([clean_paired] + _mission(10, _steady()))
        self.assertNotIn("DIRTY_BUILD", flags)
        self.assertNotIn("BUILD_PAIR_MISMATCH", flags)


class TickAndHitchTests(unittest.TestCase):
    def test_tick_profile_aggregates_per_behaviour_ms_per_second_and_share(self):
        lines = _mission(0, _steady()) + [_line(PINNED_TICK_PROFILE, 66),
                                          _line(PINNED_TICK_PROFILE.replace("t=+65s", "t=+70s"), 71)]
        tick = _rows(lines)[0]["tick_profile"]
        self.assertEqual(tick["windows"], 2)
        # 2 x 812.40 ms over 10 s of wall clock.
        self.assertAlmostEqual(tick["phase_ms_per_s"]["missionTickMs"], 162.48)
        bt = tick["behaviours"][0]
        self.assertEqual(bt["type"], "BehaviorTreeMissionLogic")
        # 820.4 ms over 10 s; the managed phases (preDisplay, missionTick, preTick) total
        # 2 x 865.00 ms, so the share is 820.4 / 1730; 1024 KB over 10 s is 102.4 KB/s.
        self.assertAlmostEqual(bt["ms_per_s"], 82.04)
        self.assertAlmostEqual(bt["share"], 820.4 / 1730.0)
        self.assertEqual(bt["calls"], 600)
        self.assertAlmostEqual(bt["kb_per_s"], 102.4)

    def test_hitches_counted_by_dominant_phase(self):
        tick_heavy = PINNED_HITCH.replace("missionTickMs=5.20", "missionTickMs=900.00")
        lines = _mission(0, _steady()) + [_line(PINNED_HITCH, 72), _line(PINNED_HITCH, 77),
                                          _line(tick_heavy, 82)]
        hitches = _rows(lines)[0]["hitches"]
        self.assertEqual(hitches["count"], 3)
        self.assertEqual(hitches["by_phase"], {"agentTickMs": 2, "missionTickMs": 1})
        self.assertAlmostEqual(hitches["max_frame_ms"], 812.35)


class MalformedTests(unittest.TestCase):
    def test_malformed_lines_are_skipped_and_counted(self):
        broken_perf = _perf(t=46).replace(" gc2=0", "")
        broken_tick = PINNED_TICK_PROFILE.replace(" wallMs=5000.00", "")
        lines = (_mission(0, _steady(n=3)) + [_line(broken_perf, 47), _line(broken_tick, 48)]
                 + _windows(0, [dict(t=51)]))
        r = _rows(lines)[0]
        self.assertEqual(r["malformed"], 2)
        self.assertEqual(r["windows"], 4)
        self.assertIsNone(r["tick_profile"])

    def test_malformed_line_before_any_mission_is_counted_too(self):
        rows, orphans = pr.rows_for_log(_line("[MissionPerf] t=+5s frames=x") + "\n", "x.log")
        self.assertEqual((rows, orphans), ([], 1))


class CompareTests(unittest.TestCase):
    def _group(self, fps_values, build=None, textures="1"):
        rows = []
        for i, fps in enumerate(fps_values):
            lines = _load(i * 100)
            if build is not None:
                ctx = PINNED_PERF_CONTEXT.replace("build=Debug", f"build={build}") \
                    .replace("textureQuality=1", f"textureQuality={textures}")
                lines.append(_line(ctx, i * 100 + 3))
            rows += _rows(lines + _windows(i * 100, _steady(fps=fps)))
        return rows

    def test_medians_delta_and_percent_with_n_per_group(self):
        result = pr.compare_rows(self._group([100.0, 110.0, 120.0]), self._group([90.0, 95.0, 100.0]))
        fps = next(m for m in result["metrics"] if m["metric"] == "fps_median")
        self.assertEqual((fps["n_a"], fps["n_b"]), (3, 3))
        self.assertAlmostEqual(fps["a"], 110.0)
        self.assertAlmostEqual(fps["b"], 95.0)
        self.assertAlmostEqual(fps["delta"], -15.0)
        self.assertAlmostEqual(fps["pct"], -13.636, places=2)
        self.assertEqual(result["context_unchecked"], {"a": 3, "b": 3})

    def test_refuses_mixed_builds_unless_allowed(self):
        a, b = self._group([100.0], build="Debug"), self._group([100.0], build="Release")
        with self.assertRaises(pr.MixedRunsError):
            pr.compare_rows(a, b)
        self.assertEqual(pr.compare_rows(a, b, allow_mixed=True)["a"]["rows"], 1)

    def test_refuses_mixed_texture_quality(self):
        a = self._group([100.0], build="Debug", textures="2")
        b = self._group([100.0], build="Debug", textures="4")
        with self.assertRaises(pr.MixedRunsError):
            pr.compare_rows(a, b)

    def test_same_build_and_textures_compare(self):
        a = self._group([100.0], build="Debug", textures="2")
        b = self._group([90.0], build="Debug", textures="2")
        self.assertEqual(pr.compare_rows(a, b)["context_unchecked"], {"a": 0, "b": 0})

    def test_flags_are_counted_per_group(self):
        a = self._group([117.0, 117.0])
        result = pr.compare_rows(a, a)
        self.assertEqual(result["flags"]["a"].get("FRAME_CAP"), 2)


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def _write(self, name, lines):
        path = Path(self.tmp.name) / name
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return str(path)

    def _run(self, *args):
        return subprocess.run([sys.executable, "-B", str(TOOL), *args],
                              capture_output=True, text=True, timeout=120)

    def test_top_level_help_names_the_compare_mode(self):
        done = self._run("--help")
        self.assertEqual(done.returncode, 0, done.stderr)
        # argparse wraps the epilog to the terminal width (COLUMNS), so compare unwrapped text.
        self.assertIn("perf_runs.py compare --a", " ".join(done.stdout.split()))

    def test_rows_exit_0_and_json_parses(self):
        log = self._write("a.log", _mission(0, _steady()))
        done = self._run(log, "--json")
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertEqual(len(json.loads(done.stdout)["rows"]), 1)

    def test_text_output_names_the_row_and_the_totals(self):
        done = self._run(self._write("a.log", _mission(0, _steady())))
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertIn(f"a.log #1 CustomBattle {SCENE}", done.stdout)
        self.assertIn("rows: 1  malformed lines skipped: 0", done.stdout)

    def test_exit_1_when_no_mission_is_found(self):
        done = self._run(self._write("empty.log", [_line("[Engine] Bannerlord=v1.5.3")]))
        self.assertEqual(done.returncode, 1)

    def test_exit_2_for_an_unreadable_file(self):
        done = self._run(str(Path(self.tmp.name) / "missing.log"))
        self.assertEqual(done.returncode, 2)

    def test_exit_2_for_no_arguments(self):
        self.assertEqual(self._run().returncode, 2)

    def test_compare_exit_0_and_refusal_exit_2(self):
        debug = PINNED_PERF_CONTEXT
        release = PINNED_PERF_CONTEXT.replace("build=Debug", "build=Release")
        a = self._write("a.log", _load(0) + [_line(debug, 3)] + _windows(0, _steady()))
        b = self._write("b.log", _load(0) + [_line(release, 3)] + _windows(0, _steady()))
        self.assertEqual(self._run("compare", "--a", a, "--b", a).returncode, 0)
        refused = self._run("compare", "--a", a, "--b", b)
        self.assertEqual(refused.returncode, 2)
        self.assertIn("refusing to compare", refused.stderr)
        self.assertEqual(self._run("compare", "--a", a, "--b", b, "--allow-mixed").returncode, 0)

    def test_compare_scene_filter_leaving_a_group_empty_exits_1(self):
        a = self._write("a.log", _mission(0, _steady()))
        self.assertEqual(self._run("compare", "--a", a, "--b", a, "--scene", "other").returncode, 1)


# --------------------------------------------------------------------------- #
# Plan 029 amendment (2026-10-03): generic key=value tags, the per-log header, #
# the first unparsed lines verbatim, and the evidence behind every flag.       #
# --------------------------------------------------------------------------- #
MEM_SAMPLE = ("[MemSample] privMB=7526 wsMB=4240 heapMB=77 sysCommitUsedMB=93799 "
              "sysCommitLimitMB=128662 availPhysMB=10529 memLoad={}%")
DIRTY_MISMATCH_STAMP = (
    "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4.dirty "
    "TAOM.Dependencies=v0.1.0.0 build.20261001-134226Z+6b00881b.dirty "
    "MISMATCH " + chr(0x2014) + " built 1d 02h 55m apart. These modules were not built "
    "together; update BOTH from the same release or expect the preview patches to fail "
    "(issue #371).")


class ExtraTagTests(unittest.TestCase):
    def test_key_value_tags_inside_a_mission_are_kept_on_the_row_in_order(self):
        no_hitch = (PINNED_TICK_SUMMARY.replace("hitches=3 worstHitchMs=1104.20 worstHitchT=+72s",
                                                "hitches=0 worstHitchMs=0.00 worstHitchT=na")
                    .replace("allocKB=30720", "allocKB=na")
                    .replace("/7680,", "/na,").replace("/960", "/na"))
        lines = _mission(0, _steady()) + [
            _line(PINNED_TICK_SUMMARY, 60),
            _line("[AnimMem] clips=420 residentMB=96", 61),
            _line(no_hitch, 65, level="DEBUG")]
        r = _rows(lines)[0]
        self.assertEqual([e["tag"] for e in r["extra_tags"]],
                         ["TickSummary", "AnimMem", "TickSummary"])
        self.assertEqual(r["extra_tags"][0], {
            "tag": "TickSummary", "timestamp": _at(60),
            "fields": {"frames": "4500", "wallMs": "75000.00", "preDisplayMs": "187.50",
                       "missionTickMs": "12186.00", "preTickMs": "601.50",
                       "waitTickMs": "1425.00", "agentTickMs": "22500.00",
                       "otherMs": "60600.00", "allocKB": "30720", "hitches": "3",
                       "worstHitchMs": "1104.20", "worstHitchT": "+72s",
                       "top": "BehaviorTreeMissionLogic:6153.00/4500/3.10/7680,"
                              "AdvancedCombatBehavior:1800.00/13500/1.50/960"}})
        # Plan 028 writes allocKB and every top KB from one allocAvailable flag: na in all of them.
        no_counter = r["extra_tags"][2]["fields"]
        self.assertEqual((no_counter["worstHitchT"], no_counter["allocKB"], no_counter["top"]),
                         ("na", "na", "BehaviorTreeMissionLogic:6153.00/4500/3.10/na,"
                                      "AdvancedCombatBehavior:1800.00/13500/1.50/na"))
        self.assertEqual(r["malformed"], 0)

    def test_untagged_or_prose_lines_are_neither_extra_nor_malformed(self):
        lines = _mission(0, _steady()) + [
            _line("[SomeFeature] applied the patch to every agent", 60),
            _line("plain text with no tag at all", 61),
            _line("[Deferred] skipped: reason=x", 62)]
        r = _rows(lines)[0]
        self.assertEqual(r["extra_tags"], [])
        self.assertEqual(r["malformed"], 0)

    def test_tags_this_tool_parses_are_not_collected_as_extra(self):
        lines = _mission(0, _steady()) + [
            _line(PINNED_HITCH, 72), _line(PINNED_TICK_PROFILE, 73), _line(MEM_SAMPLE.format(40), 74),
            _line("[BattleLoad] seq=50 t=+9000ms phase=ResourceClearOldBegin", 75)]
        self.assertEqual(_rows(lines)[0]["extra_tags"], [])

    def test_tags_before_the_first_mission_are_on_the_log_header_not_on_the_row(self):
        lines = [_line("[AnimMem] clips=420 residentMB=96", 0)] + _mission(10, _steady())
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual(rows[0]["extra_tags"], [])
        self.assertEqual(info["extra_tags"], [{"tag": "AnimMem", "timestamp": _at(0),
                                               "fields": {"clips": "420", "residentMB": "96"}}])

    def test_a_list_or_a_value_with_spaces_stays_one_value(self):
        # [Doctrine]'s team line verbatim from taom_debug_2026-09-29_08-29-26.log (08:42:49);
        # TeamDoctrineInstaller.cs writes registered=[...] last.
        registered = ("[ShieldWall*1.00, TwoLineWall*1.00, Charge*0.30, FullScaleAttack*0.50, "
                      "DefensiveEngagement*0.50, DefensiveLine*0.50, HoldChokePoint*0.50, "
                      "DefensiveRing*0.50, FrontalCavalryCharge*0.20]")
        lines = _mission(0, _steady()) + [_line(
            "[Doctrine] team=0 side=Defender player=yes culture=erebor doctrine=erebor troops=1 "
            f"tacticsSkill=0 morale=never-rout/bravery +15 registered={registered}", 60)]
        self.assertEqual(_rows(lines)[0]["extra_tags"][0]["fields"], {
            "team": "0", "side": "Defender", "player": "yes", "culture": "erebor",
            "doctrine": "erebor", "troops": "1", "tacticsSkill": "0",
            "morale": "never-rout/bravery +15", "registered": registered})

    def test_a_bracketed_group_after_a_space_stays_with_the_value_before_it(self):
        # [MapLoad]'s heartbeat verbatim from taom_debug_2026-09-29 (08:11:10). The per-kind party
        # counts sit in a space-led [...] group (MapLoadHeartbeatService.cs), so they are part of
        # parties, not keys of their own.
        lines = _mission(0, _steady()) + [_line(
            "[MapLoad] t=+0s frames=1 fps=0.0 tickMs=28.2 parties=2054(+2054) [lord=64 villager=0 "
            "caravan=302 bandit=546 militia=843 garrison=221 other=78] heroes=4765(+4765) "
            "clans=237 settlements=1002 campaignTime=2185857.000(+2185857.000) "
            "loadingWindow=True timeControl=Stop topScreen=MapScreen activeState=MapState "
            "stack=[MapState]", 60)]
        fields = _rows(lines)[0]["extra_tags"][0]["fields"]
        self.assertEqual(list(fields), ["t", "frames", "fps", "tickMs", "parties", "heroes",
                                        "clans", "settlements", "campaignTime", "loadingWindow",
                                        "timeControl", "topScreen", "activeState", "stack"])
        self.assertEqual((fields["parties"], fields["heroes"], fields["stack"]), (
            "2054(+2054) [lord=64 villager=0 caravan=302 bandit=546 militia=843 garrison=221 "
            "other=78]", "4765(+4765)", "[MapState]"))

    def test_a_parenthesised_group_stays_with_the_value_before_it(self):
        # [EnlistDiag]'s verdict, built as EnlistmentReconciler.cs writes it around
        # PlayerPresenceSnapshot.Describe(): the snapshot's keys sit inside ( ).
        body = ("verdict=Attached but the party is NOT parked (active=True visible=False "
                "attachedTo=True inMapEvent=False playerEncounter=False settlement=- army=- "
                "captive=False distToCommander=3.2 => parked=False encountersBlocked=True) "
                "\u2014 no sync will run this tick")
        lines = _mission(0, _steady()) + [_line(f"[EnlistDiag] {body}", 60, level="WARNING")]
        self.assertEqual(_rows(lines)[0]["extra_tags"][0]["fields"],
                         {"verdict": body[len("verdict="):]})

    def test_a_stray_closing_bracket_does_not_hide_the_keys_after_it(self):
        # Modelled on [Doctrine]'s periodic line with a failed behaviour: its status embeds the raw
        # exception message (TaomBehaviorBase.Fail), here one with a stray ")". One depth counts
        # every bracket kind, so the ")" closes the list and the list's own "]" is the closer with
        # nothing open, which the parser ignores.
        formations = ("[Infantry:2 BehaviorShieldWall:failed: InvalidOperationException: "
                      "3 ranks) too many/ShieldWall/FireAtWill]")
        lines = _mission(0, _steady()) + [_line(
            f"[Doctrine] t=+5s team=1 formations={formations} "
            "taom=[TaomTacticShieldWall:idle]", 60)]
        fields = _rows(lines)[0]["extra_tags"][0]["fields"]
        self.assertEqual((list(fields), fields["formations"], fields["taom"]),
                         (["t", "team", "formations", "taom"], formations,
                          "[TaomTacticShieldWall:idle]"))

    def test_an_unclosed_bracket_runs_its_value_to_the_end_of_the_line(self):
        # The same shape with a stray "(": the list's "]" closes it, so the list's "[" stays open
        # and the value keeps the rest of the line rather than turning text after it into keys.
        rest = ("[Infantry:2 BehaviorShieldWall:failed: InvalidOperationException: "
                "expected (rank/ShieldWall/FireAtWill] taom=[TaomTacticShieldWall:idle]")
        lines = _mission(0, _steady()) + [_line(f"[Doctrine] t=+5s team=1 formations={rest}", 60)]
        self.assertEqual(_rows(lines)[0]["extra_tags"][0]["fields"],
                         {"t": "+5s", "team": "1", "formations": rest})

    def test_a_closer_of_another_kind_closes_the_open_bracket(self):
        # Characterises the one-depth rule: the ")" closes the "[", so b= starts a key of its own
        # and the "]" after it has nothing open. A parser matching brackets by kind would keep
        # "[a) b=1]" as one value.
        lines = _mission(0, _steady()) + [_line("[Probe] x=[a) b=1] c=2", 60)]
        self.assertEqual(_rows(lines)[0]["extra_tags"][0]["fields"],
                         {"x": "[a)", "b": "1]", "c": "2"})

    def test_braces_keep_a_space_led_key_value_inside_the_value(self):
        # Only the braces decide here: without them b= would split off as a key.
        lines = _mission(0, _steady()) + [_line("[Probe] x={a=1 b=2} c=3", 60)]
        self.assertEqual(_rows(lines)[0]["extra_tags"][0]["fields"],
                         {"x": "{a=1 b=2}", "c": "3"})

    def test_key_value_text_inside_brackets_belongs_to_the_value(self):
        # [Doctrine]'s periodic line: a formation's behaviour weights sit inside its list entry.
        formations = ("[Infantry:2 BehaviorCharge/Line/FireAtWill"
                      "{Charge=10000,Stop=1,PullBack=1,Reserve=1}]")
        lines = _mission(0, _steady()) + [_line(
            f"[Doctrine] t=+5s team=1 formations={formations} "
            "taom=[TaomTacticShieldWall:idle, TaomTacticTwoLineWall:idle]", 60)]
        fields = _rows(lines)[0]["extra_tags"][0]["fields"]
        self.assertEqual(list(fields), ["t", "team", "formations", "taom"])
        self.assertEqual((fields["formations"], fields["taom"]),
                         (formations, "[TaomTacticShieldWall:idle, TaomTacticTwoLineWall:idle]"))

    def test_a_continuation_line_takes_its_entry_timestamp(self):
        # One logger entry can span lines ([Doctrine] writes a line per team); only its first line
        # carries the [ts] [LEVEL] prefix.
        lines = _mission(0, _steady()) + [
            _line("[Doctrine] t=+0s team=0 side=Defender tactic=none formations=[]", 60),
            "[Doctrine] t=+0s team=1 side=Attacker tactic=none formations=[]"]
        self.assertEqual([(e["fields"]["team"], e["timestamp"]) for e in _rows(lines)[0]["extra_tags"]],
                         [("0", _at(60)), ("1", _at(60))])

    def test_a_continuation_line_follows_the_newest_prefixed_line_even_an_uncollected_one(self):
        # The prefix-less line belongs to the newest prefixed entry above it. Here that entry is a
        # prose status line (not collected), so the older [Doctrine] stamp must not be borrowed.
        lines = _mission(0, _steady()) + [
            _line("[Doctrine] t=+0s team=0 side=Defender tactic=none formations=[]", 60),
            _line("[TickProfiler] frame boundary failed, measuring stopped: "
                  "InvalidOperationException: boom (x)", 62, level="ERROR"),
            "[Doctrine] t=+0s team=1 side=Attacker tactic=none formations=[]"]
        entries = _rows(lines)[0]["extra_tags"]
        self.assertEqual([(e["fields"]["team"], e["timestamp"]) for e in entries],
                         [("0", _at(60)), ("1", _at(62))])


class LogHeaderTests(unittest.TestCase):
    def test_scan_log_counts_lines_missions_and_lists_the_first_five_unparsed_verbatim(self):
        orphan = _line("[Hitch] t=+1s frameMs=x", 0)
        bad = [_line(_perf(t=40 + i).replace(" gc2=0", ""), 41 + i) for i in range(6)]
        lines = [orphan] + _mission(0, _steady(n=3)) + bad
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual(len(rows), 1)
        self.assertEqual((info["log"], info["lines"], info["missions"], info["malformed"]),
                         ("x.log", len(lines), 1, 7))
        # Line 1 is the orphan, lines 2 to 12 the mission, the broken windows start at line 13.
        self.assertEqual(info["malformed_first"],
                         [{"line": 1, "text": orphan}]
                         + [{"line": 13 + i, "text": bad[i]} for i in range(4)])


class FlagEvidenceTests(unittest.TestCase):
    def _row(self, lines):
        return _rows(lines)[0]

    def test_every_raised_flag_has_evidence_and_no_other(self):
        r = self._row([_line(DIRTY_MISMATCH_STAMP, 0)] + _load(10)
                      + [_line(PINNED_PERF_CONTEXT, 13)] + _windows(10, _steady())
                      + [_line(MEM_SAMPLE.format(83), 60)])
        self.assertEqual(r["flags"], ["FRAME_CAP", "MEMORY_PRESSURE", "DIAG_ON", "DIRTY_BUILD",
                                      "BUILD_PAIR_MISMATCH"])
        self.assertEqual(sorted(r["flag_evidence"]), sorted(r["flags"]))
        self.assertEqual(self._row(_mission(0, [dict(t=6)] + _steady(n=3)))["flag_evidence"], {})

    def test_frame_cap_plateau_names_the_fps_and_the_windows(self):
        specs = [dict(t=6)] + [dict(t=31 + 5 * i, fps=f)
                               for i, f in enumerate([117.0, 116.8, 117.2, 97.3, 117.1])]
        ev = self._row(_mission(0, specs))["flag_evidence"]["FRAME_CAP"]
        self.assertEqual(ev, "4 of 5 steady windows within 1.0 fps of 117.0 fps; windows: "
                             "t=+31s fps=117.0 active=100, t=+36s fps=116.8 active=100, "
                             "t=+41s fps=117.2 active=100, t=+46s fps=97.3 active=100, "
                             "t=+51s fps=117.1 active=100")

    def test_frame_cap_by_agent_spread_names_the_two_windows(self):
        fps = [117.0, 100.0, 90.0, 80.0, 116.6]
        active = [300, 260, 230, 200, 150]
        specs = [dict(t=6)] + [dict(t=31 + 5 * i, fps=f, agents=300, active=a)
                               for i, (f, a) in enumerate(zip(fps, active))]
        ev = self._row(_mission(0, specs))["flag_evidence"]["FRAME_CAP"]
        self.assertTrue(ev.startswith("active=300 at t=+31s ran 117.0 fps, active=150 at t=+51s "
                                      "ran 116.6 fps; windows: t=+31s fps=117.0 active=300"), ev)

    def test_memory_pressure_names_the_mem_sample_line_verbatim(self):
        hot = _line(MEM_SAMPLE.format(83), 50)
        lines = _mission(0, _steady()) + [_line(MEM_SAMPLE.format(81), 45), hot]
        self.assertEqual(self._row(lines)["flag_evidence"]["MEMORY_PRESSURE"], hot)

    def test_memory_pressure_from_perf_context_names_it(self):
        ctx = PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=83")
        r = self._row(_load(0) + [_line(ctx, 3)] + _windows(0, _steady()))
        self.assertEqual(r["flag_evidence"]["MEMORY_PRESSURE"], "[PerfContext] memLoad=83%")

    def test_diag_on_names_the_profiler_and_the_extra_tokens(self):
        ctx = PINNED_PERF_CONTEXT.replace("missionPerf", "missionPerf,troopCountDiag")
        r = self._row(_load(0) + [_line(ctx, 3)] + _windows(0, _steady()))
        self.assertEqual(r["flag_evidence"]["DIAG_ON"],
                         "[PerfContext] tickProfiler=on; diag beyond the default: troopCountDiag")

    def test_build_flags_name_the_stamp_and_the_gap(self):
        r = self._row([_line(DIRTY_MISMATCH_STAMP, 0)] + _mission(10, _steady()))
        self.assertEqual(r["flag_evidence"]["DIRTY_BUILD"],
                         "[BuildStamp] TAOM=v2.0.0.0 build.20261002-163736Z+bc39f6e4.dirty")
        self.assertEqual(r["flag_evidence"]["BUILD_PAIR_MISMATCH"],
                         "[BuildStamp] TAOM and TAOM.Dependencies built 1d 02h 55m apart")


class ReportCliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def _write(self, name, lines):
        path = Path(self.tmp.name) / name
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return str(path)

    def _run(self, *args):
        return subprocess.run([sys.executable, "-B", str(TOOL), *args],
                              capture_output=True, text=True, timeout=120)

    def _lines(self):
        bad = _line(_perf(t=46).replace(" gc2=0", ""), 47)
        return _mission(0, [dict(t=6)] + _steady()) + [
            bad, _line(PINNED_TICK_SUMMARY, 60), _line(PINNED_TICK_SUMMARY, 65)], bad

    def test_text_report_starts_with_the_log_header_and_its_unparsed_lines(self):
        lines, bad = self._lines()
        log = self._write("a.log", lines)
        done = self._run(log)
        self.assertEqual(done.returncode, 0, done.stderr)
        out = done.stdout.splitlines()
        self.assertEqual(out[0], f"log: {log} size={os.path.getsize(log)}B lines={len(lines)} "
                                 "missions=1 unparsed=1")
        self.assertEqual(out[1], f"  unparsed line 16: {bad}")
        self.assertIn("  tag [TickSummary]: 2 line(s)", out)
        self.assertIn("  FRAME_CAP: 6 of 6 steady windows within 1.0 fps of 117.0 fps; windows: "
                      "t=+31s fps=117.0 active=100, t=+36s fps=117.0 active=100, "
                      "t=+41s fps=117.0 active=100, t=+46s fps=117.0 active=100, "
                      "t=+51s fps=117.0 active=100, t=+56s fps=117.0 active=100", out)

    def test_json_carries_the_log_header_extra_tags_and_evidence(self):
        lines, bad = self._lines()
        log = self._write("a.log", lines)
        data = json.loads(self._run(log, "--json").stdout)
        self.assertEqual(data["logs"], [{"path": log, "size": os.path.getsize(log),
                                         "log": "a.log", "lines": len(lines), "missions": 1,
                                         "malformed": 1,
                                         "malformed_first": [{"line": 16, "text": bad}],
                                         "extra_tags": []}])
        row = data["rows"][0]
        self.assertEqual([e["tag"] for e in row["extra_tags"]], ["TickSummary", "TickSummary"])
        self.assertIn("FRAME_CAP", row["flag_evidence"])

    def test_the_report_and_json_carry_the_lines_before_the_first_mission_on_the_log(self):
        init = [_line(LOAD_XML_TYPE, 1), _line(LOAD_XML_SUMMARY, 2), _line(XML_MERGE_SUMMARY, 3)]
        log = self._write("a.log", init + _mission(10, _steady()))
        out = self._run(log).stdout.splitlines()
        self.assertIn("  tag [LoadXml]: 2 line(s) before the first mission", out)
        self.assertIn("  tag [XmlMerge]: 1 line(s) before the first mission", out)
        data = json.loads(self._run(log, "--json").stdout)
        self.assertEqual(_tags(data["logs"][0]["extra_tags"]), ["LoadXml", "LoadXml", "XmlMerge"])
        self.assertEqual(data["rows"][0]["extra_tags"], [])

    def test_compare_reports_both_groups_logs(self):
        lines, _ = self._lines()
        a = self._write("a.log", lines)
        b = self._write("b.log", lines)
        done = self._run("compare", "--a", a, "--b", b)
        self.assertEqual(done.returncode, 0, done.stderr)
        out = done.stdout.splitlines()
        self.assertTrue(out[0].startswith(f"A log: {a} size="), out[0])
        self.assertIn(f"B log: {b} size={os.path.getsize(b)}B lines={len(lines)} missions=1 "
                      "unparsed=1", out)
        data = json.loads(self._run("compare", "--a", a, "--b", b, "--json").stdout)
        self.assertEqual([x["log"] for x in data["logs"]["a"]], ["a.log"])
        self.assertEqual([x["log"] for x in data["logs"]["b"]], ["b.log"])


# --------------------------------------------------------------------------- #
# Deep review 2026-10-02: plan 028's real line shapes, the spawn window by     #
# position, unchecked settings, and the text the report prints.               #
# --------------------------------------------------------------------------- #
# Every [TickProfiler] status line shape the tick profiler and the hitch probe build (plans 028
# and 041, as of 041's commit 948fe42a), each text exactly as 041 writes it. The first 25 are
# plan 028's lines in 028's order: the 19 that TickProfileLinesTests builds in its
# StatusLines_NeverContainADataTag, then the other variants its pinned-literal tests assert;
# plan 041 rewords eight of them. The rest are the lines HitchProbeLines.cs adds.
# Whichever of plans 028, 029 and 041 merges last re-syncs this copy.
TICK_PROFILER_STATUS = (
    "[TickProfiler] off: 'Enable Tick Profiler' is off at game start (or MCM was not ready); "
    "no per-behaviour transpilers installed",
    "[TickProfiler] WaitTickCompletion could not be bound; waitTickMs reads 0 and the wait lands "
    "in otherMs",
    "[TickProfiler] on in MCM but the install at game start failed, so nothing is measured; see "
    "the [TickProfiler] install line and [PatchApply]",
    "[TickProfiler] on in MCM but it was off at game start, so its patches are not installed and "
    "nothing is measured; restart the game to measure",
    "[TickProfiler] mission 3: not measuring, 'Enable Tick Profiler' is off in MCM; its patches "
    "stay installed and only call through until a restart",
    "[TickProfiler] install: category failed, Mission.OnTick sites 0/2, Mission.OnPreTick sites "
    "1/1, allocation counter na",
    "[TickProfiler] install: category applied, Mission.OnTick sites 2/2, Mission.OnPreTick sites "
    "2/2, allocation counter available",
    "[TickProfiler] Mission.OnTick: MissionBehavior.OnMissionTick matched 0 times, expected 1; "
    "Mission.OnTick left vanilla, so no mission times behaviours by type (with 'Enable Hitch "
    "Probe' on, missions still measure in probe mode)",
    "[TickProfiler] Mission.OnPreTick: helper MissionTickProfilerHooks.TimedPreMissionTick does "
    "not fit MissionBehavior.OnPreMissionTick; Mission.OnPreTick left vanilla, so preTickMs "
    "reads 0 and that time lands in otherMs (the hitch probe still times the wait)",
    "[TickProfiler] frame boundary failed, measuring stopped: InvalidOperationException: (Hitch) "
    "(TickProfile) in a message",
    "[TickProfiler] Mission.OnTick rewrite failed: InvalidOperationException: (Hitch) "
    "(TickSummary) in a message; Mission.OnTick left vanilla, so no mission times behaviours by "
    "type (with 'Enable Hitch Probe' on, missions still measure in probe mode)",
    "[TickProfiler] mission 3: measuring, top 8 behaviours per line, hitch threshold 250 ms, "
    "first 100 hitch frames written in full, sites Mission.OnTick 2/2 Mission.OnPreTick 2/2",
    "[TickProfiler] mission end for generation 4 ignored: generation 5 is current and keeps "
    "measuring",
    "[TickProfiler] context read of scene failed, that field falls back to na, -1 or unknown: "
    "NullReferenceException: (PerfContext) broke",
    "[TickProfiler] hitch line cap reached at t=+412s: the first 100 slow frames of this mission "
    "were written in full; later ones are counted only in the mission summary's hitches= and "
    "worstHitchMs=",
    "[TickProfiler] mission end for generation 4: no frame closed while measuring, so no mission "
    "summary",
    "[TickProfiler] mission 2: measuring, top 8 behaviours per line, hitch threshold 250 ms, "
    "first 100 hitch frames written in full, sites Mission.OnTick 2/2 Mission.OnPreTick 2/2",
    "[TickProfiler] Mission.OnPreTick: MissionBehavior.OnPreMissionTick matched 2 times, expected "
    "1; Mission.OnPreTick left vanilla, so preTickMs reads 0 and that time lands in otherMs (the "
    "hitch probe still times the wait)",
    "[TickProfiler] Mission.OnTick rewrite failed: InvalidOperationException: boom (x); "
    "Mission.OnTick left vanilla, so no mission times behaviours by type (with 'Enable Hitch "
    "Probe' on, missions still measure in probe mode)",
    "[TickProfiler] Other.Method rewrite failed: InvalidOperationException: boom; Other.Method "
    "left vanilla, so per-type attribution records nothing for it (the hitch probe's totals stay)",
    "[TickProfiler] frame boundary failed, measuring stopped: InvalidOperationException: boom (x)",
    "[TickProfiler] context read of scene failed, that field falls back to na, -1 or unknown: "
    "NullReferenceException: gone",
    "[TickProfiler] memory status read failed, memLoad and availPhysMB fall back to na",
    "[TickProfiler] MCM 'Tick Profiler Top Behaviours' reads 0, out of range, so 8 is used",
    "[TickProfiler] MCM 'Hitch Threshold (ms)' reads 10, out of range, so 250 ms is used",
    "[TickProfiler] probe off: 'Enable Hitch Probe' and 'Enable Tick Profiler' are off at game "
    "start (or MCM was not ready); no probe patches installed, no hitch lines this session",
    "[TickProfiler] ScriptComponentBehavior.OnTick could not be bound; per-component script "
    "attribution is off, the script totals stay",
    "[TickProfiler] probe on in MCM but its patches are not installed: it was off at game start "
    "(restart the game to measure) or its install failed (see the probe install line and "
    "[PatchApply])",
    "[TickProfiler] on in MCM but it was off at game start, so per-type timing is not installed; "
    "the hitch probe still measures this mission; restart the game for per-type timing",
    "[TickProfiler] on in MCM but its install at game start failed, so per-type timing is off; the "
    "hitch probe still measures this mission; see the [TickProfiler] install line and [PatchApply]",
    "[TickProfiler] mission 3: no per-type timing, 'Enable Tick Profiler' is off in MCM; the hitch "
    "probe still measures this mission, and the profiler's patches only call through until a "
    "restart",
    "[TickProfiler] mission 3: not measuring, 'Enable Hitch Probe' and 'Enable Tick Profiler' are "
    "off in MCM; the probe's patches stay installed and only call through until a restart",
    "[TickProfiler] agent build hook failed, all per-type attribution (spawn callbacks, script "
    "components, script blocks) is off for this process: InvalidOperationException: boom (x)",
    "[TickProfiler] probe: Mission.WaitTickCompletion's bracket did not run inside "
    "Mission.OnPreTick in the first 30 frames (inlined by the JIT, or skipped by another patch); "
    "waitTickMs reads 0 and preTickMs includes the wait",
    "[TickProfiler] Mission.OnPreTick: the hitch probe bracket's prefix did not run before its "
    "finalizer (removed, or skipped by another patch), so no frame boundary runs, no frame closes "
    "and no hitch is detected",
    "[TickProfiler] Mission.WaitTickCompletion: the hitch probe bracket's prefix did not run "
    "before its finalizer (removed, or skipped by another patch), so in probe mode waitTickMs "
    "reads 0 and preTickMs includes the wait",
    "[TickProfiler] Mission.OnTick: the hitch probe bracket's prefix did not run before its "
    "finalizer (removed, or skipped by another patch), so onTickMs reads 0 and, in probe mode, "
    "so does missionTickMs",
    "[TickProfiler] ManagedScriptHolder.TickComponents: the hitch probe bracket's prefix did not "
    "run before its finalizer (removed, or skipped by another patch), so scriptTickMs and calls "
    "read 0",
    "[TickProfiler] Mission.SpawnAgent: the hitch probe bracket's prefix did not run before its "
    "finalizer (removed, or skipped by another patch), so spawns and spawnMs read 0",
    "[TickProfiler] probe install: category applied, enabled by hitch probe, targets "
    "Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder."
    "TickComponents,Mission.SpawnAgent, bookkeeping 1.25 us per frame (0.01% of a 10 ms frame, "
    "target 0.50%)",
    "[TickProfiler] probe install: category failed, enabled by both, targets "
    "Mission.OnPreTick,Mission.WaitTickCompletion,Mission.OnTick,ManagedScriptHolder."
    "TickComponents,Mission.SpawnAgent, bookkeeping 0.00 us per frame (0.00% of a 10 ms frame, "
    "target 0.50%)",
    "[TickProfiler] attribution install: Mission.SpawnAgent sites 2/2, "
    "ManagedScriptHolder.TickComponents sites 5/5, script tick delegate bound",
    "[TickProfiler] attribution install: Mission.SpawnAgent sites 0/2, "
    "ManagedScriptHolder.TickComponents sites 3/4, script tick delegate unbound",
    "[TickProfiler] mission 1: mode probe, hitch threshold 250 ms, spawn attribution off, script "
    "attribution off, anim-loading sample on",
    "[TickProfiler] script tick runs on the main thread (managed thread 1, main 1)",
    "[TickProfiler] script tick runs on another thread (managed thread 7, main 1); per-component "
    "script attribution is off for this process, the totals stay",
    "[TickProfiler] Mission.SpawnAgent ran off the main thread (managed thread 7); off-main spawns "
    "are counted in the mission summary but not timed",
    "[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median 3.20 us "
    "over 32 calls (budget 20.00 us); sampling every frame",
    "[TickProfiler] anim-loading sample: MBAnimation.IsAnyAnimationLoadingFromDisk median 25.00 us "
    "over 32 calls (budget 20.00 us); over budget, sampling is off for this process",
    "[TickProfiler] anim-loading sample failed, sampling is off for this process: "
    "InvalidOperationException: (Hitch) (HitchDetail) (TickSummaryExtra) in a message",
    "[TickProfiler] spawn hook failed, its timing is off for this process: "
    "InvalidOperationException: (Hitch) (HitchDetail) (TickSummaryExtra) in a message",
)
UNREADABLE_CONTEXT = (PINNED_PERF_CONTEXT.replace("scene=battle_terrain_029", "scene=unknown")
                      .replace("agents=0", "agents=-1")
                      .replace("textureQuality=1 shadowQuality=2 particleDetail=1 ragdolls=3 "
                               "memLoad=61 availPhysMB=12034",
                               "textureQuality=na shadowQuality=na particleDetail=na ragdolls=na "
                               "memLoad=na availPhysMB=na")
                      .split(" diag=")[0] + " diag=none")


def _context_row(fps, textures="1", at=0):
    ctx = PINNED_PERF_CONTEXT.replace("textureQuality=1", f"textureQuality={textures}")
    return _rows(_load(at) + [_line(ctx, at + 3)] + _windows(at, [dict(t=6)] + _steady(fps=fps)))[0]


class PlanTwentyEightShapeTests(unittest.TestCase):
    def test_top_none_and_na_kb_parse_as_028_writes_them(self):
        head = PINNED_TICK_PROFILE.split(" top=")[0]
        self.assertEqual(pr.parse_tick_profile(head + " top=none")["top"], [])
        self.assertEqual(pr.parse_hitch(PINNED_HITCH.split(" top=")[0] + " top=none")["top"], [])
        no_alloc = (PINNED_TICK_PROFILE.replace("allocKB=2048", "allocKB=na")
                    .replace("/512,", "/na,").replace("/1.50/64", "/1.50/na"))
        t = pr.parse_tick_profile(no_alloc)
        self.assertIsNone(t["allocKB"])
        self.assertEqual([b["kb"] for b in t["top"]], [None, None])

    def test_unreadable_context_fields_parse_as_not_measured(self):
        c = pr.parse_context(UNREADABLE_CONTEXT)
        self.assertIsNotNone(c)
        self.assertEqual((c["agents"], c["scene"], c["textureQuality"], c["diag"]),
                         (-1, "unknown", "na", []))
        self.assertIsNone(c["memLoad"])
        self.assertIsNone(c["availPhysMB"])

    def test_status_lines_inside_a_mission_are_neither_extra_nor_malformed(self):
        lines = (_mission(0, [dict(t=6)] + _steady(n=3))
                 + [_line(s, 30 + i // 2, level="WARNING")
                    for i, s in enumerate(TICK_PROFILER_STATUS)]
                 + _windows(0, _steady(n=3, first_t=46)))
        rows = _rows(lines)
        self.assertEqual(len(rows), 1)
        self.assertEqual((rows[0]["windows"], rows[0]["malformed"], rows[0]["extra_tags"]),
                         (7, 0, []))

    def test_028_shapes_inside_a_mission_feed_the_row(self):
        no_alloc_tick = (PINNED_TICK_PROFILE.replace("allocKB=2048", "allocKB=na")
                         .replace("/512,", "/na,").replace("/1.50/64", "/1.50/na"))
        lines = (_load(0) + [_line(UNREADABLE_CONTEXT, 3)] + _windows(0, [dict(t=6)] + _steady())
                 + [_line(no_alloc_tick, 66), _line(PINNED_HITCH.split(" top=")[0] + " top=none", 72)])
        r = _rows(lines)[0]
        self.assertEqual(r["malformed"], 0)
        self.assertEqual(r["tick_profile"]["windows"], 1)
        self.assertIsNone(r["tick_profile"]["behaviours"][0]["kb_per_s"])
        self.assertEqual(r["hitches"]["count"], 1)
        self.assertEqual(r["context"]["agents"], -1)

    def test_mission_open_new_done_is_not_a_second_mission(self):
        done = _line("[BattleLoad] seq=2 t=+337ms phase=MissionOpenNewDone mission='CustomBattle' "
                     "created=True", 0)
        load = _load(0)
        rows = _rows(load[:1] + [done] + load[1:] + _windows(0, [dict(t=6)] + _steady()))
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["load_ms"], 2857)


class SpawnWindowPositionTests(unittest.TestCase):
    def test_spawn_window_after_a_late_first_tick_is_not_steady(self):
        # The heartbeat's clock starts at OnCreated and its first window closes one interval
        # after the first tick, so a long render wait puts the spawn window past t=+30s.
        specs = [dict(t=310, fps=101.6, mx=624.6, gc=(41, 5, 0))] + _steady(first_t=315, mx=14.0)
        r = _rows(_mission(0, specs))[0]
        self.assertEqual((r["windows"], r["steady_windows"]), (7, 6))
        self.assertAlmostEqual(r["spawn_max_ms"], 624.6)
        self.assertAlmostEqual(r["max_ms_max"], 14.0)
        self.assertAlmostEqual(r["gc0_per_min"], 36.0)


class UncheckedSettingsTests(unittest.TestCase):
    def test_na_texture_quality_is_not_refused_but_counted_unchecked(self):
        a, b = [_context_row(100.0, textures="na")], [_context_row(90.0, textures="2")]
        result = pr.compare_rows(a, b)
        self.assertEqual(result["context_unchecked"], {"a": 1, "b": 0})
        self.assertIn("  1 A row(s) without a [PerfContext] build and texture quality: those "
                      "settings unchecked", pr.format_compare(result).splitlines())


class NonFiniteTests(unittest.TestCase):
    def test_nan_and_infinity_are_malformed_not_numbers(self):
        self.assertIsNone(pr.parse_tick_profile(PINNED_TICK_PROFILE.replace("wallMs=5000.00",
                                                                            "wallMs=NaN")))
        self.assertIsNone(pr.parse_tick_profile(PINNED_TICK_PROFILE.replace(
            "missionTickMs=812.40", "missionTickMs=Infinity")))
        self.assertIsNone(pr.parse_tick_profile(PINNED_TICK_PROFILE.replace("/512,", "/NaN,")))
        self.assertIsNone(pr.parse_hitch(PINNED_HITCH.replace("frameMs=812.35", "frameMs=NaN")))
        self.assertIsNone(pr.parse_hitch(PINNED_HITCH.replace(":2.10,", ":Infinity,")))
        self.assertIsNone(pr.parse_context(PINNED_PERF_CONTEXT.replace("memLoad=61", "memLoad=NaN")))


class MemSampleEvidenceTests(unittest.TestCase):
    def test_evidence_cites_only_a_mem_sample_line_the_flag_reads(self):
        good = _line(MEM_SAMPLE.format(83), 50)
        lines = _mission(0, _steady()) + [good, _line("[MemSample] memLoad=95%", 55)]
        r = _rows(lines)[0]
        self.assertEqual(r["mem_load_max"], 83)
        self.assertEqual(r["flag_evidence"]["MEMORY_PRESSURE"], good)


class ReportTextTests(unittest.TestCase):
    def test_compare_table_prints_each_group_in_its_own_column_with_signed_change(self):
        a = [_context_row(f, at=100 * i) for i, f in enumerate((100.0, 110.0, 120.0))]
        b = [_context_row(f, at=100 * i) for i, f in enumerate((90.0, 95.0, 100.0))]
        out = pr.format_compare(pr.compare_rows(a, b)).splitlines()
        self.assertEqual(out[0], "A: 3 rows   B: 3 rows")
        self.assertIn("fps_median         3     110.00    3      95.00    -15.00   -13.6%", out)

    def test_compare_warns_when_rows_carry_no_context(self):
        rows = _rows(_mission(0, [dict(t=6)] + _steady()))
        out = pr.format_compare(pr.compare_rows(rows, rows)).splitlines()
        self.assertIn("  1 A row(s) without a [PerfContext] build and texture quality: those "
                      "settings unchecked", out)
        self.assertIn("  1 B row(s) without a [PerfContext] build and texture quality: those "
                      "settings unchecked", out)

    def test_row_prints_gc_rates_load_hitches_and_a_disabled_heartbeat(self):
        lines = (_mission(0, [dict(t=6)] + _steady())
                 + [_line(PINNED_HITCH, 72), _line(PINNED_HITCH, 77),
                    _line(PINNED_HITCH.replace("missionTickMs=5.20", "missionTickMs=900.00"), 82),
                    _line("[MissionPerf] heartbeat disabled for this mission after "
                          "NullReferenceException: x", 90, level="ERROR")])
        out = pr.format_rows(_rows(lines), 0).splitlines()
        self.assertIn(" gc0/min=36.0 gc1/min=0.0 gc2/min=0.0 ", out[0])
        self.assertIn("  load: bucket1=2 bucket2=745 bucket3a=195 bucket3b=390 bucket3c=151 "
                      "bucket4=874 dominant=bucket4", out)
        self.assertIn("  hitches: 3 (agentTickMs x2, missionTickMs x1)", out)
        self.assertIn("  heartbeat disabled during this mission (see its [MissionPerf] ERROR line)",
                      out)

    def test_header_counts_the_unparsed_lines_it_does_not_show(self):
        bad = [_line(_perf(t=40 + i).replace(" gc2=0", ""), 41 + i) for i in range(7)]
        _, info = pr.scan_log("\n".join(_mission(0, _steady(n=3)) + bad), "x.log")
        out = pr.format_header([dict(path="x.log", size=1, **info)])
        self.assertEqual(out[-1], "  ... and 2 more unparsed")


# --------------------------------------------------------------------------- #
# Maintainer decisions 2026-10-03 (FOR-MIKE 16c and 16q): the hitch count      #
# comes from the mission's [TickSummary], the summary lines of later           #
# instruments reach extra_tags, and plan 040's total marker is a key=value.    #
# --------------------------------------------------------------------------- #
def _hitch_lines(n, start=60):
    """n written [Hitch] lines, one second apart."""
    return [_line(PINNED_HITCH, start + i) for i in range(n)]


def _summary_with(hitches):
    return PINNED_TICK_SUMMARY.replace("hitches=3", f"hitches={hitches}")


class HitchCountFromSummaryTests(unittest.TestCase):
    def test_count_is_the_summary_hitches_when_the_cap_hid_later_lines(self):
        # Plan 028 writes the first 100 [Hitch] lines of a mission in full and counts every slow
        # frame in the [TickSummary]'s hitches=: 137 slow frames here, 100 lines.
        lines = (_mission(0, _steady()) + _hitch_lines(100)
                 + [_line(_summary_with(137), 200)])
        hitches = _rows(lines)[0]["hitches"]
        self.assertEqual(hitches["count"], 137)
        self.assertEqual(hitches["lines"], 100)
        # The phase breakdown and the worst frame come from the lines that were written.
        self.assertEqual(hitches["by_phase"], {"agentTickMs": 100})
        self.assertAlmostEqual(hitches["max_frame_ms"], 812.35)

    def test_count_falls_back_to_the_hitch_lines_without_a_usable_summary(self):
        # No summary, a hitches= that is not a whole number (a stray suffix or a sign counts), and
        # a summary with no hitches= at all.
        summaries = [[]] + [[_line(_summary_with(bad), 200)] for bad in ("many", "3x", "-1")]
        summaries.append([_line(PINNED_TICK_SUMMARY.replace(" hitches=3", ""), 200)])
        for summary in summaries:
            hitches = _rows(_mission(0, _steady()) + _hitch_lines(3) + summary)[0]["hitches"]
            self.assertEqual(hitches["count"], 3, summary)
            self.assertEqual(hitches["lines"], 3, summary)

    def test_a_summary_without_a_usable_hitches_field_is_counted_as_unparsed(self):
        # The row falls back to its [Hitch] lines; the log header says so instead of staying
        # silent. The line is still a key=value line, so it stays in extra_tags too.
        head = _mission(0, _steady())
        unusable = [_summary_with(value) for value in ("many", "3x", "-1", "")]
        unusable.append(PINNED_TICK_SUMMARY.replace(" hitches=3", ""))
        for summary in unusable:
            bad = _line(summary, 200)
            rows, info = pr.scan_log("\n".join(head + [bad]), "x.log")
            self.assertEqual(rows[0]["malformed"], 1, summary)
            self.assertEqual([e["tag"] for e in rows[0]["extra_tags"]], ["TickSummary"], summary)
            self.assertEqual(info["malformed_first"], [{"line": len(head) + 1, "text": bad}],
                             summary)
            self.assertIn(f"  unparsed line {len(head) + 1}: {bad}",
                          pr.format_header([dict(path="x.log", size=1, **info)]), summary)
        # A usable one, zero included, is not.
        rows, info = pr.scan_log("\n".join(head + [_line(_summary_with(0), 200)]), "x.log")
        self.assertEqual((rows[0]["malformed"], info["malformed"]), (0, 0))
        # Before any mission there is no row to carry it, and the log still counts it.
        rows, outside = pr.rows_for_log(_line(_summary_with("many"), 5) + "\n", "x.log")
        self.assertEqual((rows, outside), ([], 1))

    def test_summaries_in_one_row_add_up_as_their_lines_would(self):
        # Two profiler missions that no [PerfContext] or [BattleLoad] line separated: the row holds
        # both missions' [Hitch] lines, so it holds both summaries' counts.
        lines = (_mission(0, _steady()) + _hitch_lines(2) + [_line(_summary_with(4), 100)]
                 + _hitch_lines(3, start=110) + [_line(_summary_with(6), 200)])
        hitches = _rows(lines)[0]["hitches"]
        self.assertEqual((hitches["count"], hitches["lines"]), (10, 5))

    def test_the_text_report_says_how_many_hitch_lines_it_parsed(self):
        capped = (_mission(0, _steady()) + _hitch_lines(100) + [_line(_summary_with(137), 200)])
        self.assertIn("  hitches: 137 (100 parsed [Hitch] lines: agentTickMs x100)",
                      pr.format_rows(_rows(capped), 0).splitlines())
        # A summary that counts hitches whose lines are not in the log (cut, or never written).
        no_lines = _mission(0, _steady()) + [_line(_summary_with(5), 200)]
        self.assertIn("  hitches: 5 (no [Hitch] line parsed)",
                      pr.format_rows(_rows(no_lines), 0).splitlines())
        # Every hitch has its line: the report reads as it did before.
        whole = _mission(0, _steady()) + _hitch_lines(3) + [_line(_summary_with(3), 200)]
        self.assertIn("  hitches: 3 (agentTickMs x3)", pr.format_rows(_rows(whole), 0).splitlines())

    def test_a_hitch_line_the_summary_does_not_count_is_still_reported(self):
        # The summary counts 0 slow frames and yet one [Hitch] line parsed. The clause is not only
        # for the 100-line cap: whatever makes the two differ, the parsed line must not vanish.
        lines = _mission(0, _steady()) + _hitch_lines(1) + [_line(_summary_with(0), 200)]
        row = _rows(lines)[0]
        self.assertEqual((row["hitches"]["count"], row["hitches"]["lines"]), (0, 1))
        self.assertIn("  hitches: 0 (1 parsed [Hitch] line: agentTickMs x1)",
                      pr.format_rows([row], 0).splitlines())

    def test_a_hitch_line_that_did_not_parse_is_not_called_parsed(self):
        # The line is unparsed (the header counts it), so the clause must not say it was read.
        head = _mission(0, _steady())
        bad = _line(PINNED_HITCH.replace(" frameMs=812.35", ""), 60)
        rows, info = pr.scan_log("\n".join(head + [bad, _line(_summary_with(1), 200)]), "x.log")
        self.assertEqual((rows[0]["hitches"]["count"], rows[0]["hitches"]["lines"],
                          rows[0]["malformed"]), (1, 0, 1))
        self.assertIn("  hitches: 1 (no [Hitch] line parsed)",
                      pr.format_rows(rows, 0).splitlines())
        self.assertEqual(info["malformed_first"], [{"line": len(head) + 1, "text": bad}])
        # Without a summary there is no count to show, and the header is the only report.
        rows, info = pr.scan_log("\n".join(head + [bad]), "x.log")
        self.assertEqual((rows[0]["hitches"]["count"], rows[0]["hitches"]["lines"],
                          info["malformed"]), (0, 0, 1))
        self.assertFalse([x for x in pr.format_rows(rows, 0).splitlines() if "hitches:" in x])

    def test_an_unreadable_summary_leaves_its_hitch_lines_counting_as_themselves(self):
        # Each summary covers the [Hitch] lines since the previous one: a readable count stands for
        # them, an unreadable one leaves them counting as themselves. Two lines under a readable 2,
        # then three lines under a summary whose hitches= reads "many": 2 + 3, not a 2 printed
        # beside five parsed lines.
        lines = (_mission(0, _steady()) + _hitch_lines(2) + [_line(_summary_with(2), 100)]
                 + _hitch_lines(3, start=110) + [_line(_summary_with("many"), 200)])
        row = _rows(lines)[0]
        self.assertEqual((row["hitches"]["count"], row["hitches"]["lines"], row["malformed"]),
                         (5, 5, 1))
        self.assertIn("  hitches: 5 (agentTickMs x5)", pr.format_rows([row], 0).splitlines())

    def test_a_readable_and_an_unreadable_summary_count_in_either_order(self):
        # The readable summary is over the 100-line cap (137 slow frames, 100 lines) and the
        # unreadable one covers 3 lines: 137 + 3 whichever comes first. A readable count equal to
        # its line count, as in the test above, cannot tell "the lines since the previous summary"
        # from "every line so far"; one past the cap can, and it has to sit before the unreadable
        # stretch in one order and after it in the other.
        unreadable_first = (_hitch_lines(3) + [_line(_summary_with("many"), 100)]
                            + _hitch_lines(100, start=110) + [_line(_summary_with(137), 300)])
        readable_first = (_hitch_lines(100) + [_line(_summary_with(137), 200)]
                          + _hitch_lines(3, start=210) + [_line(_summary_with("many"), 300)])
        for order, hitch_part in (("unreadable first", unreadable_first),
                                  ("readable first", readable_first)):
            with self.subTest(order=order):
                row = _rows(_mission(0, _steady()) + hitch_part)[0]
                self.assertEqual((row["hitches"]["count"], row["hitches"]["lines"]), (140, 103))
                self.assertIn("  hitches: 140 (103 parsed [Hitch] lines: agentTickMs x103)",
                              pr.format_rows([row], 0).splitlines())

    def test_a_hitch_line_below_the_rows_last_summary_counts_as_itself(self):
        # No summary reports it (the mission that wrote it never reached one), so the line is all
        # the row knows of that slow frame.
        lines = (_mission(0, _steady()) + _hitch_lines(2) + [_line(_summary_with(2), 100)]
                 + _hitch_lines(3, start=110))
        row = _rows(lines)[0]
        self.assertEqual((row["hitches"]["count"], row["hitches"]["lines"]), (5, 5))

    def test_a_summary_with_no_key_value_body_is_counted_as_unparsed_too(self):
        # [TickSummary] is a data tag: the profiler writes its status text under [TickProfiler], so
        # a line that lost its body (a cut write) is damage, not prose, however little survives.
        head = _mission(0, _steady()) + _hitch_lines(3)
        for text in ("[TickSummary]", "[TickSummary] frames", "[TickSummary]frames=1"):
            bad = _line(text, 200)
            rows, info = pr.scan_log("\n".join(head + [bad]), "x.log")
            self.assertEqual((rows[0]["malformed"], rows[0]["hitches"]["count"]), (1, 3), text)
            self.assertEqual(info["malformed_first"], [{"line": len(head) + 1, "text": bad}], text)
            self.assertIn(f"  unparsed line {len(head) + 1}: {bad}",
                          pr.format_header([dict(path="x.log", size=1, **info)]), text)
        # Before any mission there is no row to carry it, and the log still counts it.
        rows, outside = pr.rows_for_log(_line("[TickSummary]", 5) + "\n", "x.log")
        self.assertEqual((rows, outside), ([], 1))

    def test_another_tag_that_starts_with_tick_summary_is_not_a_summary(self):
        # Plan 041 writes [TickSummaryExtra] right after [TickSummary] (the literal is 041's pin,
        # HitchProbeLinesTests). A different tag: no count, and not a damaged summary.
        extra = ("[TickSummaryExtra] spawnMs=1843.20 scriptTickMs=6020.75 animLoadingFrames=41 "
                 "hitchesWithAnimLoading=3 mode=full frames=18000 spawns=1313 preFrameSpawns=2 "
                 "preFrameSpawnMs=3.50 offMainSpawns=0 scriptParallelMs=2400.50 occasionalMs=160.00 "
                 "onTickMs=41000.25 preTickAllMs=9800.00 spawnTop=AdvancedCombatBehavior:210.40/1313 "
                 "scriptTop=TaomHowdahMachine:1900.20/72000/4.80")
        lines = (_mission(0, _steady()) + _hitch_lines(2)
                 + [_line(_summary_with(2), 100), _line(extra, 101)])
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual((rows[0]["hitches"]["count"], rows[0]["malformed"], info["malformed"]),
                         (2, 0, 0))
        self.assertEqual([e["tag"] for e in rows[0]["extra_tags"]],
                         ["TickSummary", "TickSummaryExtra"])


# Real lines from the plans' feature docs: plan 036 (docs/features/mission-perf-heartbeat.md, the
# [AnimMem] table), plan 040 (docs/features/load-time-stamps.md, lines X0 to X2) and plan 042
# (docs/features/xml-merge-fast-path.md). Each summary opens with the word `summary`, and
# [AnimMem]'s adds a colon.
ANIM_MEM_SUMMARY = ("[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 "
                    "peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0")
LOAD_XML_SUMMARY = ("[LoadXml] summary game=Campaign calls=26 files=329 xslt=6 ms=28000.00 "
                    "merge_ms=26500.00 objects_ms=1500.00 max_ms=13302.00 max_id=NPCCharacters "
                    "failed=0")
XML_MERGE_SUMMARY = ("[XmlMerge] summary game=Campaign merges=32 fast=28 vanilla=4 files=329 "
                     "ms=6100 max_ms=1774 max_type=NPCCharacters xslt_compiles=9 "
                     "xslt_cache_hits=0 fast_path=on")
# The same tags' other lines: the ones that open with a key=value token, and the prose ones.
ANIM_MEM_PERIODIC = ("[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 "
                     "drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6")
LOAD_XML_TYPE = ("[LoadXml] id=NPCCharacters files=56 ms=13302.00 xslt=2 merge_ms=13001.50 "
                 "objects_ms=300.50 result=ok")
XML_MERGE_TYPE = ("[XmlMerge] type=NPCCharacters files=56 xslt=1 ms=1774 path=fast load_ms=361 "
                  "xslt_ms=1246 merge_ms=51 xslt_compiles=0 xslt_cache_hits=1")
SUMMARY_TAGS_PROSE = (
    "[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 "
    "pctOfBudget=83 loadingNow=0",
    "[AnimMem] armed: TaleWorlds.Native.dll base=0x7FFB12340000 text=0x1000+0xA240CC "
    "loadSite=0x21E00F budgetSite=0x21E034 counter=0xDABE40 budget=0xB2E2DC "
    "budgetBytes=12582912 scanMs=23.4",
    "[LoadXml] ready: one line per MBObjectManager.LoadXML call and a summary at every game "
    "initialization, always written",
    "[XmlMerge] fast path ready: engine bindings resolved (CreateDocumentFromXmlFile, "
    "MergeElements, ToXDocument, ToXmlDocument); applies to validated merges "
    "(skipValidation=false); xslt cache on",
)


class SummaryLineTests(unittest.TestCase):
    def test_summary_lines_of_later_instruments_reach_extra_tags(self):
        # [AnimMem]'s summary is written per mission, so a mission's row holds it. [LoadXml]'s and
        # [XmlMerge]'s are written per game, at its initialization, before the game's first
        # mission: the log header holds those.
        lines = ([_line(LOAD_XML_SUMMARY, 1), _line(XML_MERGE_SUMMARY, 2)]
                 + _mission(10, _steady()) + [_line(ANIM_MEM_SUMMARY, 70)])
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        r = rows[0]
        self.assertEqual([e["tag"] for e in r["extra_tags"]], ["AnimMem"])
        self.assertEqual([e["tag"] for e in info["extra_tags"]], ["LoadXml", "XmlMerge"])
        # The leading word is not a field: the line's own keys are, in order.
        self.assertEqual(r["extra_tags"][0], {
            "tag": "AnimMem", "timestamp": _at(70),
            "fields": {"t": "+7s", "samples": "6", "startKB": "10240", "endKB": "12288",
                       "peakKB": "12288", "peakPct": "100", "samplesAtOrAbove90Pct": "4",
                       "drops": "2", "loadingSamples": "0", "stopped": "0"}})
        self.assertEqual(info["extra_tags"][0]["fields"], {
            "game": "Campaign", "calls": "26", "files": "329", "xslt": "6", "ms": "28000.00",
            "merge_ms": "26500.00", "objects_ms": "1500.00", "max_ms": "13302.00",
            "max_id": "NPCCharacters", "failed": "0"})
        self.assertEqual(info["extra_tags"][1]["fields"], {
            "game": "Campaign", "merges": "32", "fast": "28", "vanilla": "4", "files": "329",
            "ms": "6100", "max_ms": "1774", "max_type": "NPCCharacters", "xslt_compiles": "9",
            "xslt_cache_hits": "0", "fast_path": "on"})
        self.assertEqual((r["malformed"], info["malformed"]), (0, 0))

    def test_the_other_lines_of_those_tags_parse_as_they_did(self):
        # The per-type [LoadXml] and [XmlMerge] lines are a game's initialization (the header's);
        # the periodic [AnimMem] line and the four prose lines sit in the mission.
        samples = (ANIM_MEM_PERIODIC,) + SUMMARY_TAGS_PROSE
        lines = ([_line(LOAD_XML_TYPE, 1), _line(XML_MERGE_TYPE, 2)] + _mission(10, _steady())
                 + [_line(s, 70 + i) for i, s in enumerate(samples)])
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        r = rows[0]
        # Only the lines that open with a key=value token are collected; the four prose lines are
        # neither collected nor unparsed.
        self.assertEqual([(e["tag"], next(iter(e["fields"]))) for e in r["extra_tags"]],
                         [("AnimMem", "t")])
        self.assertEqual([(e["tag"], next(iter(e["fields"]))) for e in info["extra_tags"]],
                         [("LoadXml", "id"), ("XmlMerge", "type")])
        self.assertEqual(r["extra_tags"][0]["fields"]["loadingSamples"], "0/6")
        self.assertEqual(info["extra_tags"][0]["fields"]["result"], "ok")
        self.assertEqual((r["malformed"], info["malformed"]), (0, 0))

    def test_only_a_leading_summary_word_is_skipped(self):
        # Synthetic lines. A key named summary stays a key, and so does a key that only starts with
        # the word (summaryMs: the word must be followed by a space); the word followed by prose is
        # prose.
        lines = _mission(0, _steady()) + [_line("[Probe] summary=3 x=4", 60),
                                          _line("[Probe] summary skipped: reason=x", 61),
                                          _line("[Probe] summaryMs=5 x=1", 62)]
        r = _rows(lines)[0]
        self.assertEqual([e["fields"] for e in r["extra_tags"]],
                         [{"summary": "3", "x": "4"}, {"summaryMs": "5", "x": "1"}])
        self.assertEqual(r["malformed"], 0)


class ScopeTotalTests(unittest.TestCase):
    def test_a_scope_total_pair_is_a_field_of_its_own(self):
        # Plan 040's total marker is the pair scope=total (maintainer decision 2026-10-03). As a
        # bare `total` word it joined the value before it (phase=GameInit total), so the pair is
        # what parses cleanly.
        lines = _mission(0, _steady()) + [
            _line("[LoadTime] phase=GameInit scope=total ms=1234", 60)]
        r = _rows(lines)[0]
        self.assertEqual(r["extra_tags"][0]["fields"],
                         {"phase": "GameInit", "scope": "total", "ms": "1234"})
        self.assertEqual(r["malformed"], 0)


# --------------------------------------------------------------------------- #
# Review of 84adc93c, applied 2026-10-03: where a line's tag sits, and the     #
# lines a game writes while it initializes, before its first mission.          #
# --------------------------------------------------------------------------- #
# Plan 036's disabled line, verbatim from its AnimMemLineTests.Disabled_FormatsReason (commit
# 50df8cbf, Main/Features/MissionPerf/AnimMemory/AnimMemLine.cs): the text names [MissionPerf].
ANIM_MEM_DISABLED = ("[AnimMem] disabled for this process: TaleWorlds.Native.dll is not loaded in "
                     "this process. No further [AnimMem] samples will be taken; [MissionPerf] is "
                     "unaffected.")


class TagPositionTests(unittest.TestCase):
    def test_plan_036s_disabled_line_is_not_a_malformed_mission_perf_line(self):
        r = _rows(_mission(0, _steady()) + [_line(ANIM_MEM_DISABLED, 70)])[0]
        self.assertEqual((r["malformed"], r["windows"], r["extra_tags"]), (0, 6, []))

    def test_a_tag_named_inside_the_text_of_another_line_starts_nothing(self):
        # Each tag the splitter acts on, named mid-line by a prose line of another tag. A mention
        # of [PerfContext] used to open a second mission inside the first.
        mentions = ("[Probe] compare with the [PerfContext] line",
                    "[Probe] the [TickProfile] lines say",
                    "[Probe] see [Hitch] and [MissionPerf] too")
        lines = _mission(0, _steady()) + [_line(m, 70 + i) for i, m in enumerate(mentions)]
        rows = _rows(lines)
        self.assertEqual(len(rows), 1)
        self.assertEqual((rows[0]["malformed"], rows[0]["extra_tags"]), (0, []))

    def test_a_tag_at_the_start_of_a_line_without_the_logger_prefix_is_still_read(self):
        # The prefix is optional, as it is for the generic tags: a continuation line carries none.
        lines = _mission(0, _steady(n=3)) + [_perf(t=46), PINNED_HITCH, PINNED_TICK_PROFILE]
        r = _rows(lines)[0]
        self.assertEqual((r["windows"], r["hitches"]["count"], r["tick_profile"]["windows"],
                          r["malformed"]), (4, 1, 1, 0))


# Lines a game writes while it initializes, before its first mission: plan 040
# (docs/features/load-time-stamps.md, lines L1 and C3), plan 042 (the per-merge line and summary
# above) and the trunk's [SaveLoad] stamp (SaveLoadDiagnosticsService.Stamp, memory tokens from
# ProcessMemoryTokens.Format). MAP_LOAD is a synthetic campaign-map line.
LOAD_PHASE_STEP = "[LoadPhase] hook=OnGameStart step=hand_wired ms=120.50"
LIFECYCLE_DISPATCH = ("[Lifecycle] dispatch=OnNewGameCreated ms=6650.00 listeners_ms=6600.00 "
                      "result=ok")
SAVE_LOAD_INIT_DONE = ("[SaveLoad] seq=3 t=+41230ms phase=GameInitializationFinished "
                       "gc=1375/450/75 heapMB=141 privMB=8812 wsMB=5138")
MAP_LOAD = "[MapLoad] t=+5s frames=300 fps=60.0 tickMs=16.2 parties=2054(+0)"
# A saved game's load starts with [SaveLoad] LoadRequested (SaveLoadDiagnosticsService.Stamp, from the
# Load Game click in SandBoxSaveHelper.TryLoadSave), then the save's identity as ModuleCheck lines.
# The campaign map's autosave writes SaveBegin and SaveCompleted. A new campaign writes no
# LoadRequested (docs/features/save-load-diagnostics.md, Log contract).
LOAD_REQUESTED = "[SaveLoad] seq=1 t=+0ms phase=LoadRequested name='save007'"
MODULE_CHECK = ("[SaveLoad] seq=2 t=+3ms phase=ModuleCheck appVersion='v1.5.3' "
                "created='2026-10-02 11:38' character='Aragorn' taomBuild='<pre-diagnostics>'")
SAVE_BEGIN = "[SaveLoad] seq=1 t=+0ms phase=SaveBegin name='autosave_1'"
SAVE_COMPLETED = "[SaveLoad] seq=2 t=+812ms phase=SaveCompleted result=Success"
# What the always-on lifecycle trace (Main/Features/MapLoadDiagnostics, Patch89: MapLoadTracer,
# called from the GameStateManager and GameState.OnInitialize postfixes) writes when a game ends and
# the next one starts, verbatim from taom_debug_2026-10-02_11-38-06.log lines 2669, 2675, 2676, 2681
# and 2682: a custom battle's game, then a new campaign from the main menu. These lines are prose,
# not key=value.
STACK_EMPTY = "[MapLoad] #111 t=794617ms STATE cleanStates (level 0) :: stack: <empty>"
MENU_INITIALIZED = "[MapLoad] #114 t=794979ms STATE initialized: InitialState"
MENU_PUSHED = ("[MapLoad] #115 t=795019ms STATE cleanAndPush InitialState (level 0) :: "
               "stack: InitialState")
LOADING_INITIALIZED = "[MapLoad] #118 t=797663ms STATE initialized: GameLoadingState"
LOADING_PUSHED = ("[MapLoad] #119 t=797704ms STATE cleanAndPush GameLoadingState (level 0) :: "
                  "stack: GameLoadingState")
# The crash report copies the tail of the log into the log, under its own tag, and an old line of
# the tail can be the main menu's initialization: line 1641 of taom_debug_2026-10-02_11-05-27.log.
CRASH_REPORT_COPY = ("[CrashReport]   [2026-10-02 11:14:03] [INFO] [MapLoad] #76 t=358470ms STATE "
                     "initialized: InitialState")


def _tags(entries):
    return [e["tag"] for e in entries]


class GameBoundaryTests(unittest.TestCase):
    """A game initializes before its first mission, so plan 040's and 042's lines belong to no
    mission: they stay on the log header, and a second game's must not land on the mission the
    first game ended with."""

    def test_the_lines_before_the_first_mission_stay_on_the_log_header(self):
        init = [_line(LOAD_XML_TYPE, 1), _line(XML_MERGE_TYPE, 2), _line(SAVE_LOAD_INIT_DONE, 3),
                _line(LOAD_XML_SUMMARY, 4), _line(XML_MERGE_SUMMARY, 5)]
        rows, info = pr.scan_log("\n".join(init + _mission(10, _steady())), "x.log")
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["extra_tags"], [])
        self.assertEqual(_tags(info["extra_tags"]),
                         ["LoadXml", "XmlMerge", "SaveLoad", "LoadXml", "XmlMerge"])
        # Kept in full: the summary's own fields and its timestamp.
        summary = info["extra_tags"][3]
        self.assertEqual((summary["timestamp"], summary["fields"]["game"],
                          summary["fields"]["calls"]), (_at(4), "Campaign", "26"))

    def test_a_second_games_init_lines_go_to_the_header_not_to_the_previous_mission(self):
        # Whichever tag a game's initialization writes first. [LoadPhase] comes first with
        # "Enable Load-Time Stamps" on, [LoadXml] or [XmlMerge] otherwise.
        for first, first_tag in ((LOAD_PHASE_STEP, "LoadPhase"), (LOAD_XML_TYPE, "LoadXml"),
                                 (XML_MERGE_TYPE, "XmlMerge")):
            with self.subTest(first=first_tag):
                game1 = ([_line(LOAD_XML_TYPE, 1), _line(LOAD_XML_SUMMARY, 2)]
                         + _mission(10, _steady(), scene="a"))
                # What the first game's mission still owns: its own summary, the campaign map and
                # a memory sample after the battle.
                after = [_line(PINNED_TICK_SUMMARY, 70), _line(MAP_LOAD, 75),
                         _line(MEM_SAMPLE.format(83), 80)]
                game2 = ([_line(first, 100), _line(LOAD_XML_TYPE, 101),
                          _line(XML_MERGE_SUMMARY, 102), _line(MEM_SAMPLE.format(90), 103),
                          _line(SAVE_LOAD_INIT_DONE, 104), _line(LIFECYCLE_DISPATCH, 105)]
                         + _mission(110, _steady(), scene="b"))
                rows, info = pr.scan_log("\n".join(game1 + after + game2), "x.log")
                self.assertEqual([r["scene"] for r in rows], ["a", "b"])
                self.assertEqual(_tags(rows[0]["extra_tags"]), ["TickSummary", "MapLoad"])
                # The second game's 90% sample is its own load's; the 83% one is the battle's.
                self.assertEqual(rows[0]["mem_load_max"], 83)
                self.assertEqual(rows[1]["extra_tags"], [])
                self.assertEqual(_tags(info["extra_tags"]),
                                 ["LoadXml", "LoadXml", first_tag, "LoadXml", "XmlMerge",
                                  "SaveLoad", "Lifecycle"])

    def test_a_game_that_opens_no_mission_keeps_its_init_lines_off_the_previous_one(self):
        lines = ([_line(LOAD_XML_TYPE, 1)] + _mission(10, _steady())
                 + [_line(LOAD_XML_TYPE, 100), _line(LOAD_XML_SUMMARY, 101),
                    _line(XML_MERGE_SUMMARY, 102)])
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual((len(rows), rows[0]["extra_tags"]), (1, []))
        self.assertEqual(_tags(info["extra_tags"]), ["LoadXml", "LoadXml", "LoadXml", "XmlMerge"])

    def test_the_boundary_closes_a_mission_the_heartbeat_opened_too(self):
        # Battle Load Diagnostics off: no MissionOpenNew line, so the heartbeat opens each mission.
        lines = (_mission(0, _steady(), load=False) + [_line(MAP_LOAD, 70)]
                 + [_line(LOAD_XML_TYPE, 100), _line(LOAD_XML_SUMMARY, 101)]
                 + _mission(110, _steady(), load=False))
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual([r["opened_by"] for r in rows], ["MissionPerf", "MissionPerf"])
        self.assertEqual(_tags(rows[0]["extra_tags"]), ["MapLoad"])
        self.assertEqual(rows[1]["extra_tags"], [])
        self.assertEqual(_tags(info["extra_tags"]), ["LoadXml", "LoadXml"])

    def test_other_lines_after_a_mission_start_do_not_start_a_game(self):
        # None of these starts a game, though [Lifecycle] and [SaveLoad] GameInitializationFinished
        # are written while one initializes: only a game-init tag, a load request or the trace's
        # menu or loading state is a boundary, so every other line after a mission stays on it.
        after = [_line(ANIM_MEM_SUMMARY, 70), _line(MAP_LOAD, 71), _line(LIFECYCLE_DISPATCH, 72),
                 _line(SAVE_LOAD_INIT_DONE, 73), _line(PINNED_TICK_SUMMARY, 74)]
        rows, info = pr.scan_log("\n".join(_mission(0, _steady()) + after), "x.log")
        self.assertEqual(_tags(rows[0]["extra_tags"]),
                         ["AnimMem", "MapLoad", "Lifecycle", "SaveLoad", "TickSummary"])
        self.assertEqual(info["extra_tags"], [])

    def test_the_header_names_each_tag_before_a_first_mission_with_its_count(self):
        lines = ([_line(LOAD_XML_TYPE, 1), _line(LOAD_XML_TYPE, 2), _line(LOAD_XML_SUMMARY, 3),
                  _line(XML_MERGE_SUMMARY, 4)] + _mission(10, _steady()))
        _, info = pr.scan_log("\n".join(lines), "x.log")
        out = pr.format_header([dict(path="x.log", size=1, **info)])
        self.assertEqual(out[1:], ["  tag [LoadXml]: 3 line(s) before the first mission",
                                   "  tag [XmlMerge]: 1 line(s) before the first mission"])
        # A log with none says nothing more than it did.
        _, bare = pr.scan_log("\n".join(_mission(0, _steady())), "y.log")
        self.assertEqual(len(pr.format_header([dict(path="y.log", size=1, **bare)])), 1)

    def test_a_saved_games_load_request_starts_the_next_game(self):
        # LoadRequested is the first line of a saved game's load. The first [LoadXml] line waits
        # for the first module XML type to finish loading, seconds later, so what the load writes in
        # between (its identity lines, its memory samples) is the next game's: its 90% sample must
        # not raise MEMORY_PRESSURE on the battle, whose own last sample read 45%.
        game1 = (_mission(10, _steady(), scene="a")
                 + [_line(MEM_SAMPLE.format(45), 70), _line(MAP_LOAD, 75)])
        load = [_line(LOAD_REQUESTED, 100), _line(MODULE_CHECK, 101),
                _line(MEM_SAMPLE.format(90), 105), _line(LOAD_XML_TYPE, 110)]
        rows, info = pr.scan_log("\n".join(game1 + load + _mission(120, _steady(), scene="b")),
                                 "x.log")
        self.assertEqual([r["scene"] for r in rows], ["a", "b"])
        self.assertEqual(rows[0]["mem_load_max"], 45)
        self.assertNotIn("MEMORY_PRESSURE", rows[0]["flags"])
        self.assertEqual(_tags(rows[0]["extra_tags"]), ["MapLoad"])
        self.assertEqual(rows[1]["extra_tags"], [])
        self.assertEqual(_tags(info["extra_tags"]), ["SaveLoad", "SaveLoad", "LoadXml"])

    def test_a_load_request_ends_the_mission_in_a_log_without_load_stamps(self):
        # This log has no [LoadXml] line (a build without plans 040 and 042) and no lifecycle trace
        # line, so the request is its only boundary.
        lines = (_mission(10, _steady(), scene="a") + [_line(MEM_SAMPLE.format(45), 70)]
                 + [_line(LOAD_REQUESTED, 100), _line(MEM_SAMPLE.format(90), 105)]
                 + _mission(120, _steady(), scene="b"))
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual([r["mem_load_max"] for r in rows], [45, None])
        self.assertEqual(rows[0]["flags"], ["FRAME_CAP"])
        self.assertEqual(_tags(info["extra_tags"]), ["SaveLoad"])

    def test_a_load_that_never_completes_leaves_its_game_on_the_header_until_the_next_mission(self):
        # The request is written before the load can fail (SandBoxSaveHelper.TryLoadSave, v1.5.3).
        # A Cancel at its module-mismatch question, and a save that does not read (LoadGameAction),
        # both leave the player where the Load menu was opened, here the campaign map. The log then
        # holds the request and the save's identity and no game-init line, so the row still ends at
        # the request: the map's lines go on the header and its memory samples on no row, until the
        # next mission opens a row of its own.
        lines = (_mission(10, _steady(), scene="a") + [_line(MEM_SAMPLE.format(45), 70)]
                 + [_line(LOAD_REQUESTED, 100), _line(MODULE_CHECK, 101),
                    _line(MAP_LOAD, 110), _line(MEM_SAMPLE.format(90), 115)]
                 + _mission(120, _steady(), scene="b"))
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual([r["scene"] for r in rows], ["a", "b"])
        self.assertEqual([r["mem_load_max"] for r in rows], [45, None])
        self.assertEqual([r["extra_tags"] for r in rows], [[], []])
        self.assertEqual(_tags(info["extra_tags"]), ["SaveLoad", "SaveLoad", "MapLoad"])

    def test_only_a_load_request_starts_a_game_not_a_save_or_another_line_like_it(self):
        # The campaign map's autosave writes SaveBegin and SaveCompleted after a battle, the same
        # line shape under another tag is another instrument's, a fault line may quote the phase in
        # its text, and a message may quote the whole request. All of them stay on the battle.
        after = [_line(SAVE_BEGIN, 70), _line(SAVE_COMPLETED, 71), _line(MAP_LOAD, 72),
                 _line("[Probe] seq=1 t=+0ms phase=LoadRequested name='x'", 73),
                 _line("[SaveLoad] seq=7 t=+9ms phase=LoadFault detail=phase=LoadRequested", 74),
                 _line("[Probe] note=[SaveLoad] seq=1 t=+0ms phase=LoadRequested name='x'", 75),
                 _line(MEM_SAMPLE.format(83), 80)]
        rows, info = pr.scan_log("\n".join(_mission(10, _steady()) + after), "x.log")
        self.assertEqual(_tags(rows[0]["extra_tags"]),
                         ["SaveLoad", "SaveLoad", "MapLoad", "Probe", "SaveLoad", "Probe"])
        self.assertEqual(rows[0]["mem_load_max"], 83)
        self.assertEqual(info["extra_tags"], [])

    def test_a_load_request_before_the_first_mission_stays_on_the_header(self):
        rows, info = pr.scan_log("\n".join([_line(LOAD_REQUESTED, 1), _line(MODULE_CHECK, 2)]
                                           + _mission(10, _steady())), "x.log")
        self.assertEqual((len(rows), rows[0]["extra_tags"]), (1, []))
        self.assertEqual(_tags(info["extra_tags"]), ["SaveLoad", "SaveLoad"])

    def test_the_menu_a_game_ends_in_and_the_next_games_loading_state_end_its_row(self):
        # Lines 2669 to 2702 of taom_debug_2026-10-02_11-38-06.log: the custom battles' game ends,
        # the player is at the main menu and starts a new campaign. That writes no load request, and
        # the build wrote no [LoadPhase], [LoadXml] or [XmlMerge] line (plans 040 and 042), so the
        # lifecycle trace is the only boundary. The 82% sample belongs to the new campaign's load;
        # without the boundary it lands on the last battle (45% here) and raises MEMORY_PRESSURE.
        game1 = (_mission(10, _steady(), scene="a")
                 + [_line(MEM_SAMPLE.format(45), 70), _line(MAP_LOAD, 75)])
        menu = [_line(STACK_EMPTY, 100), _line(MENU_INITIALIZED, 101), _line(MENU_PUSHED, 101),
                _line(LOADING_INITIALIZED, 103), _line(LOADING_PUSHED, 103),
                _line(MEM_SAMPLE.format(82), 107), _line(MAP_LOAD, 108)]
        rows, info = pr.scan_log("\n".join(game1 + menu + _mission(120, _steady(), scene="b")),
                                 "x.log")
        self.assertEqual([r["scene"] for r in rows], ["a", "b"])
        self.assertEqual([r["mem_load_max"] for r in rows], [45, None])
        self.assertNotIn("MEMORY_PRESSURE", rows[0]["flags"])
        # The battle keeps the campaign map up to the line; the new game's map line is the header's.
        self.assertEqual(_tags(rows[0]["extra_tags"]), ["MapLoad"])
        self.assertEqual(rows[1]["extra_tags"], [])
        self.assertEqual(_tags(info["extra_tags"]), ["MapLoad"])

    def test_each_of_the_two_state_lines_ends_the_mission_on_its_own(self):
        # A game left for the main menu shows the menu and nothing else. A saved game loaded from
        # the campaign map pushes the loading state without a main menu in between, and in a log
        # with no load request line that state is the first line of the load that ends the row.
        for state, line in (("InitialState", MENU_INITIALIZED),
                            ("GameLoadingState", LOADING_INITIALIZED)):
            with self.subTest(state=state):
                lines = (_mission(10, _steady()) + [_line(MEM_SAMPLE.format(45), 70),
                         _line(line, 100), _line(MEM_SAMPLE.format(90), 105), _line(MAP_LOAD, 106)])
                rows, info = pr.scan_log("\n".join(lines), "x.log")
                self.assertEqual((len(rows), rows[0]["mem_load_max"]), (1, 45))
                self.assertEqual(_tags(info["extra_tags"]), ["MapLoad"])

    def test_other_state_lines_and_copies_of_the_trace_do_not_end_the_mission(self):
        # The states a game initializes while it runs (a custom battle's menu, the clan screen), the
        # push of the two states rather than their initialization, the same text under another tag,
        # a message that quotes the line, a state whose name only starts with the word, and the
        # crash report's copy of the log tail. All of them stay on the battle.
        after = [_line("[MapLoad] #9 t=85638ms STATE initialized: CustomBattleState", 70),
                 _line("[MapLoad] #24 t=163593ms STATE initialized: ClanState", 71),
                 _line(MENU_PUSHED, 72), _line(LOADING_PUSHED, 73),
                 _line("[Probe] #118 t=797663ms STATE initialized: GameLoadingState", 74),
                 _line("[Probe] note=" + MENU_INITIALIZED, 75),
                 _line("[MapLoad] #118 t=797663ms STATE initialized: GameLoadingStateX", 76),
                 _line(CRASH_REPORT_COPY, 77, level="ERROR"),
                 _line(MEM_SAMPLE.format(83), 80)]
        rows, info = pr.scan_log("\n".join(_mission(10, _steady()) + after), "x.log")
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["mem_load_max"], 83)
        self.assertEqual(info["extra_tags"], [])

    def test_the_state_lines_before_a_games_first_mission_change_nothing(self):
        # The log's first game starts at the main menu and its loading state, ahead of its first
        # mission: no mission is open yet, and these prose lines are on no row and not on the
        # header.
        lines = ([_line(MENU_INITIALIZED, 1), _line(LOADING_INITIALIZED, 2),
                  _line(LOAD_XML_TYPE, 3)] + _mission(10, _steady()))
        rows, info = pr.scan_log("\n".join(lines), "x.log")
        self.assertEqual((len(rows), rows[0]["extra_tags"]), (1, []))
        self.assertEqual(_tags(info["extra_tags"]), ["LoadXml"])


if __name__ == "__main__":
    unittest.main()
