#!/usr/bin/env python3
"""Turn TAOM debug logs into one performance row per mission, and compare two groups of runs.

A frame-time or memory change is judged by numbers, not by feel. The `[MissionPerf]` heartbeat
(Main/Features/MissionPerf) writes one line every five seconds of wall clock while a mission
ticks; this tool cuts any number of `taom_debug_*.log` files into missions and reduces each
mission to one row, then compares the rows of an A group against a B group.

INPUT LINES (each carries the FileLogger prefix `[YYYY-MM-DD HH:MM:SS] [LEVEL] `; the tool finds
[MissionPerf], [TickProfile], [Hitch] and [PerfContext] lines by the tag where it sits, right after
that prefix or at the start of a line that has none, so a message that names one of them, as
[AnimMem]'s "[MissionPerf] is unaffected", is not that tag's line):
  [MissionPerf]  MissionPerfLine.Build, the twin pin is PINNED_MISSION_PERF in
                 tools/tests/test_perf_runs.py and FrameStatsTests.BuildLine_FormatsEveryFieldInvariantly
  [TickProfile], [Hitch], [PerfContext]
                 the mission tick profiler's lines (plan 028); optional, a log without them parses
  [BattleLoad], [MemSample]
                 read through triage_battle_load.py's own parser, never re-implemented here
  [BuildStamp]   the startup build identity line (Main/Core/Diagnostics/BuildStampReport.cs)

MISSION BOUNDARIES. A mission starts at its `[BattleLoad] phase=MissionOpenNew` line (one per
mission in custom battles, campaign battles, tournaments and town visits). When Battle Load
Diagnostics was off, a `[PerfContext]` line or the heartbeat's own clock starts it instead: `t=`
counts wall seconds from the mission's creation, so a window whose `t` does not advance with the
FileLogger clock belongs to a new mission.

ROW. Load buckets come from triage_battle_load.classify_phase_timings over the mission's load
lines (MissionOpenNew up to its first BattlePlayable). The first `[MissionPerf]` window is the
spawn window (BattlePlayable fires with agents=0, so spawning lands in it) and is reported on its
own, whatever its t: a long render wait before the first tick puts it past t=+30s. Steady-state
stats use the later windows from t >= 30 s with active > 0.

HITCHES. The profiler writes only the first 100 `[Hitch]` lines of a mission in full and counts
every slow frame in the `hitches=` of the mission's `[TickSummary]`, written when it ends. Each
summary covers the `[Hitch]` lines since the previous one, so hitches.count adds up what the row's
stretches report: a readable `hitches=` (the sum, when the row holds several summaries), and for
a stretch with no readable one the number of its `[Hitch]` lines, as in a row with no summary at
all. A stretch has none when its summary's `hitches=` is missing or not a whole number, and when no
summary follows its lines. hitches.lines is how many `[Hitch]` lines parsed (one that did not is
counted in the log header), and by_phase and max_frame_ms come from those lines. The text report
prints a row's hitches when either number is above 0, and says what its breakdown covers when
the two differ: "137 (100 parsed [Hitch] lines: ...)", "5 (no [Hitch] line parsed)". A
`[TickSummary]` whose `hitches=` is missing or not a whole number reports nothing and is counted
as unparsed.

CONFOUNDER FLAGS. FRAME_CAP, MEMORY_PRESSURE, DIAG_ON, DIRTY_BUILD, BUILD_PAIR_MISMATCH. A flag
says the numbers may not mean what they seem; it never changes them. DIAG_ON ignores the
default-on diagnostics (DIAG_BASELINE): it fires for the tick profiler or any other diagnostic.
Every flag carries its evidence (row["flag_evidence"]): the fps and the windows behind FRAME_CAP,
the [MemSample] line behind MEMORY_PRESSURE, and so on.

OTHER TAGS. Any other line between the mission's start and the next mission's start or the next
game's initialization (so the campaign map after a battle counts toward that battle) whose
payload is `[Tag] key=value ...` (a later instrument such as [TickSummary] or [AnimMem]) is kept
on the row as extra_tags (tag, timestamp, fields) and counted per tag in the text report. So is a
summary line,
`[Tag] summary key=value ...` or `[Tag] summary: key=value ...` ([AnimMem], [LoadXml] and
[XmlMerge] write one per mission or per game): the leading word is skipped, and a summary is told
from its tag's other lines by its keys. A value runs to the next space-led
key= outside brackets ([], {} and (), one depth for all three), so a list with spaces
([ShieldWall*1.00, Charge*0.30]) stays one value, a bracketed group after a space joins the value
before it ([MapLoad]'s per-kind counts are part of parties=), and so does a bare word or prose
after a value. A bracket left open runs its value to the end of the line. A line without the
logger's `[ts] [LEVEL]` prefix continues the entry above it ([Doctrine] writes a line per team)
and takes the timestamp of the newest prefixed line. A tagged line whose body does not start
with a key=value token (after that summary word, if any) is prose: neither collected nor
malformed. That includes the `[TickProfiler]` status lines of plans 028 and 041. The exception is
`[TickSummary]`, a data tag: a line that carries it with no usable `hitches=` is malformed, a bare
one included (NO SILENT SKIP).

GAME BOUNDARIES. A game initializes before its first mission, and plan 040's and plan 042's lines
of that initialization ([LoadPhase] hook steps, [LoadXml] and [XmlMerge] per module XML type, each
with a summary) belong to no mission. Every extra-tag line before a game's first mission goes on
the log header (header["extra_tags"], counted per tag in the text report), not on a row: for the
log's first game, everything before its first mission. The tool starts the next game at the first
of these read while a mission is open: the lifecycle trace's `[MapLoad] #N t=Nms STATE initialized:
InitialState` (the main menu, which the engine initializes only once the game before it is gone) or
`... STATE initialized: GameLoadingState` (what MBGameManager.StartNewGame pushes for a new
campaign, a saved game and a custom battle alike), a saved game's `[SaveLoad] seq=N t=+Nms
phase=LoadRequested` line (the Load Game click, written before the load reads anything), or a
[LoadPhase], [LoadXml] or [XmlMerge] line. The trace is Patch89 of Main/Features/MapLoadDiagnostics:
a build that applies it writes both state lines, and no setting turns it off. Plans 040 and 042
write their tags only while a game initializes, but each line follows the step it times, so the
first can come seconds after the load began; the state lines and the request come earlier. Such a
line closes the open mission. The campaign map after a battle counted toward it up to there, and
nothing after that line does (the next game's [MemSample] lines included); everything up to the
next mission's start goes on the header. The request is written before the load can fail, so a load
that never completes (a Cancel at the module-mismatch question, or a save that does not read) still
ends the row at its request: the game it was asked from goes on the header until its next mission,
and its [MemSample] lines on no row. A new campaign or a custom battle writes no load request, but
it starts from the main menu: the row of the game before it ends at the menu's line, the time in
the menu goes on the header, and the [MemSample] lines of the new game's load on no row (the header
holds no [MemSample] lines). A row keeps what its
game wrote up to the line that ends it, the teardown included. A log with none of these lines has no
boundary, and a mission keeps every line after its start, as before (the trace first shipped in
v2.0.29, and the 1.4.5 branch has none). A mod that loaded module XML during a mission would end
that mission's row at that line.

NO SILENT SKIP. The report opens with one header per log (path, size, lines, missions, lines of a
known tag it could not parse, and the extra tags before a game's first mission) and prints the
first five unparsed lines verbatim. A `[TickSummary]` whose `hitches=` is missing or not a whole
number counts as an unparsed line too, a line cut before its first key included: the `[Hitch]`
lines it would have covered count as themselves, and the header says so.

Usage:
  python tools/perf_runs.py <log> [<log> ...] [--json]
  python tools/perf_runs.py compare --a <log> [...] --b <log> [...] [--scene <id>] [--allow-mixed] [--json]

Exit code: 0 rows found (compare: both groups have rows), 1 no mission found, 2 usage error,
unreadable file, or a compare refused over mixed build or texture settings.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import re
import statistics
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import triage_battle_load as tb  # noqa: E402

STEADY_FROM_S = 30
MEMORY_PRESSURE_PCT = 80
FRAME_CAP_MIN_WINDOWS = 4
FRAME_CAP_TOLERANCE_FPS = 1.0
FRAME_CAP_AGENT_SPREAD = 0.30
# Whole-second rounding of both `t=` and the FileLogger stamp can disagree by about 2 s.
CLOCK_SLACK_S = 3
# The seven `[PerfContext] diag=` tokens of the mission tick profiler. Every one defaults on
# (BattleLoadDiagnosticsSettings), and missionPerf is the heartbeat this tool reads, so a default
# install lists them all: they are the measuring baseline. DIAG_ON means cost beyond it.
DIAG_BASELINE = frozenset(("battleLoad", "stallWatchdog", "stallBundle", "exitSampler",
                           "freezeSampler", "memSampler", "missionPerf"))

_TS_RE = re.compile(r"^\[(\d{4}-\d\d-\d\d)[ T](\d\d:\d\d:\d\d)\]")
_OPEN_RE = re.compile(r"\[BattleLoad\]\s+seq=\d+\s+t=\+\d+ms\s+phase=MissionOpenNew\b(.*)$")
_PLAYABLE_RE = re.compile(r"\[BattleLoad\]\s+seq=\d+\s+t=\+\d+ms\s+phase=BattlePlayable\b")
_MISSION_KIND_RE = re.compile(r"\bmission='([^']*)'")
_SCENE_RE = re.compile(r"\bscene='([^']*)'")
_NUM = r"(\d+(?:\.\d+)?)"
_PERF_RE = re.compile(
    r"\[MissionPerf\]\s+t=\+(\d+)s\s+frames=(\d+)\s+fps=" + _NUM + r"\s+avgMs=" + _NUM
    + r"\s+p95Ms=" + _NUM + r"\s+maxMs=" + _NUM + r"\s+agents=(\d+)\s+active=(\d+)\s+"
    r"formations=(\d+)\s+gc0=(\d+)\s+gc1=(\d+)\s+gc2=(\d+)\s*$")
# MissionPerfHeartbeatBehavior's one ERROR line: carries the tag, is not a window, not malformed.
_PERF_DISABLED = "[MissionPerf] heartbeat disabled"
_KV_RE = re.compile(r"(\w+)=(\S*)")
_KEY_RE = re.compile(r"(\w+)=")
_T_RE = re.compile(r"^\+(\d+)s$")
_STAMP_RE = re.compile(r"\[BuildStamp\]\s+TAOM=(.*?)\s+TAOM\.Dependencies=")
# The gap BuildStampReport.DescribeVerdict writes after MISMATCH (a dash, then "built <gap> apart").
_GAP_RE = re.compile(r"MISMATCH\W+built (.+?) apart")
# The FileLogger prefix `[YYYY-MM-DD HH:MM:SS] [LEVEL] `; a continuation line has none.
_PREFIX = r"(?:\[[^\]]*\]\s+\[[A-Z]+\]\s+)?"
# Where a line's tag sits: after the prefix, or at the start of a line without one. A tag named
# later in the text (plan 036's "... [MissionPerf] is unaffected.") is part of the message.
_TAG_RE = re.compile(r"^" + _PREFIX + r"(\[[A-Za-z]\w*\])")
# An optional FileLogger prefix, then `[Tag] key=value ...`: the shape of an instrument line. A
# leading `summary` or `summary:` word (the per-mission and per-game summaries of [AnimMem],
# [LoadXml] and [XmlMerge]) is skipped, so those lines are collected like any other.
_EXTRA_RE = re.compile(
    r"^" + _PREFIX + r"\[([A-Za-z]\w*)\]\s+(?:summary:?\s+)?(\w+=\S*.*)$")
MALFORMED_SHOWN = 5

TAG_PERF = "[MissionPerf]"
TAG_TICK = "[TickProfile]"
TAG_HITCH = "[Hitch]"
TAG_CONTEXT = "[PerfContext]"
# Tags this tool (or triage_battle_load.py's parser) reads field by field: never extra_tags.
_KNOWN_TAGS = frozenset(("MissionPerf", "TickProfile", "Hitch", "PerfContext", "BattleLoad",
                         "MemSample", "BuildStamp"))
# The one extra tag a row reads: the mission-end line whose hitches= counts every slow frame. The
# profiler writes its status text under [TickProfiler], never under this tag, so a line that carries
# it is data: one that gives no hitches= is damaged, however little of it is left.
TAG_SUMMARY = "[TickSummary]"
_WHOLE_NUMBER_RE = re.compile(r"[0-9]+")
# The tags a game writes only while it initializes: plan 040's [LoadPhase] hook steps and [LoadXml]
# lines, plan 042's [XmlMerge] lines, each with a per-game summary. The first of them read while a
# mission is open starts the next game (GAME BOUNDARIES in the module docstring).
_GAME_INIT_TAGS = frozenset(("LoadPhase", "LoadXml", "XmlMerge"))
# A saved game's load starts before any of those lines: SaveLoadDiagnosticsService writes this one
# when the Load Game click reaches SandBoxSaveHelper.TryLoadSave, before the load reads anything,
# while a [LoadXml] line waits for its first module XML type to finish loading. The other phases of a
# load, and a save's own (SaveBegin, SaveCompleted), do not start a game.
_LOAD_REQUEST_RE = re.compile(
    r"^" + _PREFIX + r"\[SaveLoad\]\s+seq=\d+\s+t=\+\d+ms\s+phase=LoadRequested\b")
# A new campaign and a custom battle write no load request, but every game starts at one of two
# states. The always-on lifecycle trace (Main/Features/MapLoadDiagnostics, Patch89:
# GameState_OnInitialize_Trace_Patch through MapLoadTracer.Trace) writes `[MapLoad] #N t=Nms STATE
# initialized: <state type>` for every state that initializes. InitialState is the main menu, which
# the engine initializes only once the game before it is gone. GameLoadingState is what
# MBGameManager.StartNewGame pushes for a new campaign, a saved game and a custom battle alike. The
# line is prose, not key=value, so it is read raw like the request, and only where its tag sits: the
# crash report copies old log lines into the log under its own tag.
_GAME_STATE_RE = re.compile(
    r"^" + _PREFIX + r"\[MapLoad\]\s+#\d+\s+t=\d+ms\s+STATE initialized: "
    r"(?:InitialState|GameLoadingState)\s*$")

PHASES = ("preDisplayMs", "missionTickMs", "preTickMs", "waitTickMs", "agentTickMs", "otherMs")
_MANAGED_PHASES = ("preDisplayMs", "missionTickMs", "preTickMs")
_CONTEXT_KEYS = ("build", "jitOptimized", "clr", "serverGC", "latency", "missionInProcess",
                 "scene", "agents", "textureQuality", "shadowQuality", "particleDetail",
                 "ragdolls", "memLoad", "availPhysMB", "tickProfiler", "diag")
# The [PerfContext] settings compare refuses to mix; a row without them is counted unchecked.
_COMPARED_KEYS = ("build", "textureQuality")

METRICS = ("fps_median", "avg_ms_median", "p95_ms_median", "max_ms_max", "gc0_per_min",
           "gc1_per_min", "gc2_per_min", "spawn_max_ms", "spawn_gc0", "agents_max", "load_ms",
           "mem_load_max")


# --------------------------------------------------------------------------- #
# Line parsers: each returns None for a malformed line                         #
# --------------------------------------------------------------------------- #
@dataclass
class Window:
    t: int
    frames: int
    fps: float
    avg_ms: float
    p95_ms: float
    max_ms: float
    agents: int
    active: int
    formations: int
    gc0: int
    gc1: int
    gc2: int

    @property
    def seconds(self) -> float | None:
        return self.frames / self.fps if self.fps > 0 else None


def parse_mission_perf(line: str) -> Window | None:
    m = _PERF_RE.search(line)
    if not m:
        return None
    g = m.groups()
    return Window(t=int(g[0]), frames=int(g[1]), fps=float(g[2]), avg_ms=float(g[3]),
                  p95_ms=float(g[4]), max_ms=float(g[5]), agents=int(g[6]), active=int(g[7]),
                  formations=int(g[8]), gc0=int(g[9]), gc1=int(g[10]), gc2=int(g[11]))


def _fields(line: str, tag: str) -> dict:
    return dict(_KV_RE.findall(line[line.index(tag) + len(tag):]))


def _seconds(value: str) -> int:
    m = _T_RE.match(value)
    if not m:
        raise ValueError(value)
    return int(m.group(1))


def _num(value: str) -> float:
    """A finite number. float() also reads NaN and Infinity, which are not numbers in the line
    contract (and would print as invalid JSON)."""
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(value)
    return number


def _number_or_na(value: str) -> float | None:
    """`na` is the contract's "not measured"; it stays None and is never read as 0."""
    if value == "na":
        return None
    return _num(value.rstrip("%"))


def parse_tick_profile(line: str) -> dict | None:
    f = _fields(line, TAG_TICK)
    try:
        out = {"t": _seconds(f["t"]), "frames": int(f["frames"]), "wallMs": _num(f["wallMs"]),
               "allocKB": _number_or_na(f["allocKB"]), "top": []}
        for key in PHASES:
            out[key] = _num(f[key])
        for item in f["top"].split(","):
            if not item or item == "none":
                continue
            name, _, nums = item.rpartition(":")
            ms, calls, max_ms, kb = nums.split("/")
            if not name:
                raise ValueError(item)
            out["top"].append({"type": name, "ms": _num(ms), "calls": int(calls),
                               "max_ms": _num(max_ms), "kb": _number_or_na(kb)})
    except (KeyError, ValueError):
        return None
    return out


def parse_hitch(line: str) -> dict | None:
    f = _fields(line, TAG_HITCH)
    try:
        out = {"t": _seconds(f["t"]), "frameMs": _num(f["frameMs"]),
               "gc0": int(f["gc0"]), "gc1": int(f["gc1"]), "gc2": int(f["gc2"]),
               "allocKB": _number_or_na(f["allocKB"]), "top": []}
        for key in PHASES:
            out[key] = _num(f[key])
        for item in f["top"].split(","):
            if not item or item == "none":
                continue
            name, _, ms = item.rpartition(":")
            if not name:
                raise ValueError(item)
            out["top"].append({"type": name, "ms": _num(ms)})
    except (KeyError, ValueError):
        return None
    # The phase that held the most ms; ties go to the earlier phase in PHASES.
    out["phase"] = max(PHASES, key=lambda k: (out[k], -PHASES.index(k)))
    return out


def parse_context(line: str) -> dict | None:
    f = _fields(line, TAG_CONTEXT)
    if any(k not in f for k in _CONTEXT_KEYS):
        return None
    try:
        out = dict(f)
        out["missionInProcess"] = int(f["missionInProcess"])
        out["agents"] = int(f["agents"])
        out["memLoad"] = _number_or_na(f["memLoad"])
        out["availPhysMB"] = _number_or_na(f["availPhysMB"])
    except ValueError:
        return None
    out["diag"] = [] if f["diag"] in ("", "none") else f["diag"].split(",")
    return out


def parse_build_stamp(text: str) -> dict | None:
    """The first [BuildStamp] line of a log: TAOM's own stamp, and whether the startup report
    called the TAOM / TAOM.Dependencies pair a MISMATCH (BuildStampReport.DescribeVerdict)."""
    for raw in text.splitlines():
        if "[BuildStamp]" not in raw:
            continue
        m = _STAMP_RE.search(raw)
        gap = _GAP_RE.search(raw)
        return {"taom": m.group(1).strip() if m else None, "mismatch": " MISMATCH " in raw,
                "gap": gap.group(1) if gap else None}
    return None


def _instrument_fields(body: str) -> dict:
    """The key=value pairs of an instrument line's body. A value runs to the next space-led `key=`
    outside brackets. Space-led keeps a list ([ShieldWall*1.00, Charge*0.30]), key=value text that
    no space leads (FireAtWill{Charge=10000}) and a value with a space (never-rout/bravery +15)
    whole. The brackets keep a space-led group with the value before it: [MapLoad]'s
    `parties=2054(+2054) [lord=64 ... other=78]` and [EnlistDiag]'s `verdict=... (active=True ...)`
    are one value each. A bare word or prose after a value joins it. One depth counts [], {} and ()
    together: any closer closes the innermost opener whatever its kind, and a closer with nothing
    open is ignored. While an opener stays open the value runs to the end of the line, so its text
    is kept, not split."""
    starts, depth = [], 0
    for i, ch in enumerate(body):
        if ch in "[{(":
            depth += 1
        elif ch in "]})":
            depth = max(0, depth - 1)
        elif depth == 0 and (i == 0 or body[i - 1].isspace()):
            m = _KEY_RE.match(body, i)
            if m:
                starts.append(m)
    ends = [m.start() for m in starts[1:]] + [len(body)]
    return {m.group(1): body[m.end():end].rstrip() for m, end in zip(starts, ends)}


def _line_tag(raw: str) -> str | None:
    """The `[Tag]` where a line's tag sits (right after the logger's prefix, or at the start of a
    line without one), or None. A tag named later in the text is not the line's tag."""
    m = _TAG_RE.match(raw)
    return m.group(1) if m else None


def parse_extra_tag(raw: str, entry_timestamp: str | None = None) -> dict | None:
    """A `[Tag] key=value ...` line (or `[Tag] summary key=value ...`) of a tag this tool does not
    read field by field, or None. A line without the logger's prefix continues an entry above it
    and takes entry_timestamp."""
    m = _EXTRA_RE.match(raw)
    if not m or m.group(1) in _KNOWN_TAGS:
        return None
    ts = _TS_RE.match(raw)
    return {"tag": m.group(1), "timestamp": f"{ts.group(1)} {ts.group(2)}" if ts else entry_timestamp,
            "fields": _instrument_fields(m.group(2))}


def _starts_game(raw: str, extra: dict | None) -> bool:
    """Whether this line says a game is initializing (GAME BOUNDARIES in the module docstring): a
    key=value line of a tag a game writes only while it initializes (extra, None for a prose line),
    a saved game's load request, or the lifecycle trace's main menu or loading state."""
    return ((extra is not None and extra["tag"] in _GAME_INIT_TAGS)
            or _LOAD_REQUEST_RE.match(raw) is not None
            or _GAME_STATE_RE.match(raw) is not None)


def _timestamp(raw: str) -> float | None:
    m = _TS_RE.match(raw)
    if not m:
        return None
    stamp = datetime.strptime(m.group(1) + " " + m.group(2), "%Y-%m-%d %H:%M:%S")
    return (stamp - datetime(1970, 1, 1)).total_seconds()


# --------------------------------------------------------------------------- #
# Mission segments                                                             #
# --------------------------------------------------------------------------- #
@dataclass
class Segment:
    log: str
    index: int
    start_line: int
    opened_by: str
    mission: str | None = None
    scene: str | None = None
    lines: list = field(default_factory=list)
    windows: list = field(default_factory=list)
    contexts: list = field(default_factory=list)
    ticks: list = field(default_factory=list)
    hitches: list = field(default_factory=list)
    extra_tags: list = field(default_factory=list)
    # (line number, raw line) of every known-tag line that did not parse, and of every
    # [TickSummary] line whose hitches= is missing or not a whole number.
    malformed_lines: list = field(default_factory=list)
    # One (parsed [Hitch] lines read so far, hitches= or None when unreadable) per [TickSummary] line
    # read, in order: which of the row's [Hitch] lines each summary covers (hitch_count).
    hitch_summaries: list = field(default_factory=list)
    heartbeat_disabled: bool = False
    last_perf_ts: float | None = None


def _starts_new_mission(seg: Segment, window: Window, ts: float | None) -> bool:
    """A heartbeat window belongs to a NEW mission when its clock restarted: `t` did not grow,
    or it grew 3+ s less than the FileLogger clock did. A pause stops both clocks' lines alike,
    so a resumed mission keeps its segment."""
    if not seg.windows:
        return False
    prev = seg.windows[-1]
    if window.t <= prev.t:
        return True
    if ts is not None and seg.last_perf_ts is not None:
        return (window.t - prev.t) < (ts - seg.last_perf_ts) - CLOCK_SLACK_S
    return False


def split_missions(text: str, log_name: str = "") -> tuple[list, list, list]:
    """Cut one log into mission segments. Returns (segments, the (line number, raw line) of each
    malformed line outside any mission, the extra tags read outside any mission). Outside any
    mission is before a game's first mission: the log's first game, each later game's
    initialization (its main menu included), and, after a load request that never completes, the
    rest of the game it was asked from, up to its next mission (GAME BOUNDARIES in the module
    docstring)."""
    segments: list = []
    orphan_malformed: list = []
    pre_mission_tags: list = []
    cur: Segment | None = None

    def open_segment(line_no: int, opened_by: str) -> Segment:
        seg = Segment(log=log_name, index=len(segments) + 1, start_line=line_no,
                      opened_by=opened_by)
        segments.append(seg)
        return seg

    entry_timestamp = None
    for line_no, raw in enumerate(text.splitlines(), 1):
        stamp = _TS_RE.match(raw)
        if stamp:
            entry_timestamp = f"{stamp.group(1)} {stamp.group(2)}"
        line_tag = _line_tag(raw)
        m = _OPEN_RE.search(raw)
        if m:
            cur = open_segment(line_no, "MissionOpenNew")
            kind = _MISSION_KIND_RE.search(m.group(1))
            scene = _SCENE_RE.search(m.group(1))
            cur.mission = kind.group(1) if kind else None
            cur.scene = scene.group(1) if scene else None
            cur.lines.append(raw)
            continue
        if line_tag == TAG_CONTEXT:
            if cur is None or cur.contexts or cur.windows:
                cur = open_segment(line_no, "PerfContext")
            ctx = parse_context(raw)
            if ctx is None:
                cur.malformed_lines.append((line_no, raw))
            else:
                cur.contexts.append(ctx)
                if cur.scene is None:
                    cur.scene = ctx["scene"]
            cur.lines.append(raw)
            continue
        if line_tag == TAG_PERF:
            if _PERF_DISABLED in raw:
                if cur is not None:
                    cur.heartbeat_disabled = True
                    cur.lines.append(raw)
                continue
            window = parse_mission_perf(raw)
            if window is None:
                if cur is None:
                    orphan_malformed.append((line_no, raw))
                else:
                    cur.malformed_lines.append((line_no, raw))
                    cur.lines.append(raw)
                continue
            ts = _timestamp(raw)
            if cur is None or _starts_new_mission(cur, window, ts):
                cur = open_segment(line_no, "MissionPerf")
            cur.windows.append(window)
            cur.last_perf_ts = ts
            cur.lines.append(raw)
            continue
        for tag, parser, bucket in ((TAG_TICK, parse_tick_profile, "ticks"),
                                    (TAG_HITCH, parse_hitch, "hitches")):
            if line_tag == tag:
                parsed = parser(raw)
                if cur is None:
                    if parsed is None:
                        orphan_malformed.append((line_no, raw))
                elif parsed is None:
                    cur.malformed_lines.append((line_no, raw))
                else:
                    getattr(cur, bucket).append(parsed)
                break
        else:
            extra = parse_extra_tag(raw, entry_timestamp)
            if _starts_game(raw, extra):
                cur = None  # a game is initializing: the open mission ended before it
            if extra is not None:
                (pre_mission_tags if cur is None else cur.extra_tags).append(extra)
            if line_tag == TAG_SUMMARY:
                # By the tag, not by a key=value body: a summary cut before its first key has none.
                reported = None if extra is None else _hitches_field(extra)
                if cur is not None:
                    cur.hitch_summaries.append((len(cur.hitches), reported))
                if reported is None:
                    (orphan_malformed if cur is None else cur.malformed_lines).append((line_no, raw))
        if cur is not None:
            cur.lines.append(raw)
    return segments, orphan_malformed, pre_mission_tags


# --------------------------------------------------------------------------- #
# Rows                                                                         #
# --------------------------------------------------------------------------- #
def _median(values: list) -> float | None:
    return statistics.median(values) if values else None


def _load_text(lines: list) -> str:
    """The mission's load: its lines up to and including the first BattlePlayable. What follows
    (ResourceClearOld* on the way out, the next encounter's EncounterStart) is not this load."""
    out = []
    for raw in lines:
        out.append(raw)
        if _PLAYABLE_RE.search(raw):
            break
    return "\n".join(out)


def _load_ms(timeline) -> int | None:
    opened = next((e for e in timeline.events if e.phase == "MissionOpenNew"), None)
    playable = next((e for e in timeline.events if e.phase == "BattlePlayable"), None)
    if opened is None or playable is None or opened.ms is None or playable.ms is None:
        return None
    return playable.ms - opened.ms if playable.ms >= opened.ms else None


def _per_minute(steady: list, attr: str) -> float | None:
    timed = [w for w in steady if w.seconds]
    seconds = sum(w.seconds for w in timed)
    if seconds <= 0:
        return None
    return sum(getattr(w, attr) for w in timed) * 60.0 / seconds


def _window_list(steady: list) -> str:
    return ", ".join(f"t=+{w.t}s fps={w.fps:.1f} active={w.active}" for w in steady)


def frame_cap_evidence(steady: list) -> str | None:
    """FRAME_CAP: fps that does not move with load, or None. Either at least half the steady
    windows sit within 1 fps of one window's fps (a plateau), or the windows with the most and the
    fewest active agents (30% or more apart) run within 1 fps of each other. The evidence names
    the plateau's fps and its share, or the two windows; then every steady window."""
    if len(steady) < FRAME_CAP_MIN_WINDOWS:
        return None
    near = [[g for g in steady if abs(g.fps - w.fps) <= FRAME_CAP_TOLERANCE_FPS] for w in steady]
    best = max(range(len(steady)), key=lambda i: (len(near[i]), -i))
    if len(near[best]) * 2 >= len(steady):
        why = (f"{len(near[best])} of {len(steady)} steady windows within "
               f"{FRAME_CAP_TOLERANCE_FPS:.1f} fps of {steady[best].fps:.1f} fps")
    else:
        most = max(steady, key=lambda w: w.active)
        least = min(steady, key=lambda w: w.active)
        if (least.active > (1.0 - FRAME_CAP_AGENT_SPREAD) * most.active
                or abs(most.fps - least.fps) > FRAME_CAP_TOLERANCE_FPS):
            return None
        why = (f"active={most.active} at t=+{most.t}s ran {most.fps:.1f} fps, "
               f"active={least.active} at t=+{least.t}s ran {least.fps:.1f} fps")
    return f"{why}; windows: {_window_list(steady)}"


def memory_pressure_evidence(lines: list, context: dict | None) -> str | None:
    """The [MemSample] line with the highest memLoad (verbatim), or the [PerfContext] reading
    when that is higher; None when neither reaches MEMORY_PRESSURE_PCT."""
    best, best_line = None, None
    for raw in lines:
        # The same parser that fills mem_load_max, so the line cited is one the flag read.
        samples = tb.parse_battle_load_log(raw).mem_samples if "[MemSample]" in raw else []
        if samples and (best is None or samples[0].mem_load > best):
            best, best_line = samples[0].mem_load, raw
    ctx = context["memLoad"] if context is not None else None
    if ctx is not None and ctx >= MEMORY_PRESSURE_PCT and (best is None or ctx > best):
        return f"[PerfContext] memLoad={ctx:g}%"
    return best_line if best is not None and best >= MEMORY_PRESSURE_PCT else None


def aggregate_ticks(ticks: list) -> dict | None:
    wall = sum(t["wallMs"] for t in ticks)
    if not ticks or wall <= 0:
        return None
    managed = sum(t[k] for t in ticks for k in _MANAGED_PHASES)
    by_type: dict = {}
    for t in ticks:
        for b in t["top"]:
            e = by_type.setdefault(b["type"], {"ms": 0.0, "calls": 0, "max_ms": 0.0, "kb": None})
            e["ms"] += b["ms"]
            e["calls"] += b["calls"]
            e["max_ms"] = max(e["max_ms"], b["max_ms"])
            if b["kb"] is not None:
                e["kb"] = (e["kb"] or 0.0) + b["kb"]
    behaviours = [{"type": name, "ms_per_s": e["ms"] * 1000.0 / wall,
                   "share": e["ms"] / managed if managed > 0 else None,
                   "calls": e["calls"], "max_ms": e["max_ms"],
                   "kb_per_s": e["kb"] * 1000.0 / wall if e["kb"] is not None else None}
                  for name, e in by_type.items()]
    behaviours.sort(key=lambda b: (-b["ms_per_s"], b["type"]))
    return {"windows": len(ticks), "wall_ms": wall,
            "phase_ms_per_s": {k: sum(t[k] for t in ticks) * 1000.0 / wall for k in PHASES},
            "behaviours": behaviours}


def _hitches_field(tag: dict) -> int | None:
    """The slow-frame count a [TickSummary] line reports in hitches=, or None when the field is
    missing or not a whole number."""
    value = tag["fields"].get("hitches", "")
    return int(value) if _WHOLE_NUMBER_RE.fullmatch(value) else None


def hitch_count(seg: Segment) -> int:
    """The row's slow-frame count. The profiler writes only the first 100 [Hitch] lines of a mission
    in full but counts every slow frame in the hitches= of its [TickSummary], written when the
    mission ends. So each summary covers the parsed [Hitch] lines since the previous one: a readable
    hitches= stands for them (a row that holds several summaries, missions that no [PerfContext] or
    [BattleLoad] line separated, adds them), and anything else leaves them counting as themselves:
    a summary whose hitches= is missing or not a whole number (split_missions records its line as
    unparsed), and the lines no summary follows, as in a row without any summary."""
    count = covered = 0
    for lines_before, reported in seg.hitch_summaries:
        count += lines_before - covered if reported is None else reported
        covered = lines_before
    return count + len(seg.hitches) - covered


def summarize(seg: Segment, stamp: dict | None) -> dict:
    windows = seg.windows
    first = windows[0] if windows else None
    # By position, not by t: the spawn window is never steady, even past STEADY_FROM_S.
    steady = [w for w in windows[1:] if w.t >= STEADY_FROM_S and w.active > 0]
    load_tl = tb.parse_battle_load_log(_load_text(seg.lines))
    timings = tb.classify_phase_timings(load_tl)
    whole = tb.parse_battle_load_log("\n".join(seg.lines))
    context = seg.contexts[0] if seg.contexts else None
    mem_loads = [s.mem_load for s in whole.mem_samples]
    if context is not None and context["memLoad"] is not None:
        mem_loads.append(context["memLoad"])
    hitch_phases: dict = {}
    for h in seg.hitches:
        hitch_phases[h["phase"]] = hitch_phases.get(h["phase"], 0) + 1
    row = {
        "log": seg.log, "mission_index": seg.index, "start_line": seg.start_line,
        "opened_by": seg.opened_by, "mission": seg.mission, "scene": seg.scene,
        "build_stamp": stamp["taom"] if stamp else None, "context": context,
        "windows": len(windows), "steady_windows": len(steady),
        "agents_max": max((w.agents for w in windows), default=None),
        "spawn_max_ms": first.max_ms if first else None,
        "spawn_gc0": first.gc0 if first else None,
        "fps_median": _median([w.fps for w in steady]),
        "avg_ms_median": _median([w.avg_ms for w in steady]),
        "p95_ms_median": _median([w.p95_ms for w in steady]),
        "max_ms_max": max((w.max_ms for w in steady), default=None),
        "gc0_per_min": _per_minute(steady, "gc0"),
        "gc1_per_min": _per_minute(steady, "gc1"),
        "gc2_per_min": _per_minute(steady, "gc2"),
        "load_ms": _load_ms(load_tl),
        "load_buckets": {b["name"]: b["ms"] for b in timings["buckets"]} if timings else None,
        "load_dominant": timings["dominant"] if timings else None,
        "mem_load_max": max(mem_loads) if mem_loads else None,
        "tick_profile": aggregate_ticks(seg.ticks),
        "hitches": {"count": hitch_count(seg),
                    "lines": len(seg.hitches), "by_phase": hitch_phases,
                    "max_frame_ms": max((h["frameMs"] for h in seg.hitches), default=None)},
        "heartbeat_disabled": seg.heartbeat_disabled,
        "malformed": len(seg.malformed_lines),
        "extra_tags": seg.extra_tags,
    }
    evidence: dict = {}
    frame_cap = frame_cap_evidence(steady)
    if frame_cap is not None:
        evidence["FRAME_CAP"] = frame_cap
    if row["mem_load_max"] is not None and row["mem_load_max"] >= MEMORY_PRESSURE_PCT:
        evidence["MEMORY_PRESSURE"] = (memory_pressure_evidence(seg.lines, context)
                                       or f"memLoad={row['mem_load_max']:g}%")
    if context is not None and (context["tickProfiler"] == "on"
                                or any(d not in DIAG_BASELINE for d in context["diag"])):
        extra = [d for d in context["diag"] if d not in DIAG_BASELINE]
        evidence["DIAG_ON"] = "; ".join(
            (["[PerfContext] tickProfiler=on"] if context["tickProfiler"] == "on" else [])
            + ([f"diag beyond the default: {','.join(extra)}"] if extra else []))
    if stamp and stamp["taom"] and ".dirty" in stamp["taom"]:
        evidence["DIRTY_BUILD"] = f"[BuildStamp] TAOM={stamp['taom']}"
    if stamp and stamp["mismatch"]:
        evidence["BUILD_PAIR_MISMATCH"] = ("[BuildStamp] TAOM and TAOM.Dependencies built "
                                           f"{stamp['gap'] or 'an unparsed time'} apart")
    row["flags"] = list(evidence)
    row["flag_evidence"] = evidence
    return row


def scan_log(text: str, log_name: str) -> tuple[list, dict]:
    """(rows, header) for one log's text. The header counts the log's lines and missions, lists the
    first MALFORMED_SHOWN lines it could not parse verbatim, and carries the extra tags read
    before a game's first mission (extra_tags)."""
    segments, orphan_malformed, pre_mission_tags = split_missions(text, log_name)
    stamp = parse_build_stamp(text)
    rows = [summarize(s, stamp) for s in segments]
    bad = sorted(orphan_malformed + [x for s in segments for x in s.malformed_lines])
    header = {"log": log_name, "lines": len(text.splitlines()), "missions": len(rows),
              "malformed": len(bad),
              "malformed_first": [{"line": n, "text": raw} for n, raw in bad[:MALFORMED_SHOWN]],
              "extra_tags": pre_mission_tags}
    return rows, header


def rows_for_log(text: str, log_name: str) -> tuple[list, int]:
    """(rows, malformed lines outside any mission) for one log's text."""
    rows, header = scan_log(text, log_name)
    return rows, header["malformed"] - sum(r["malformed"] for r in rows)


# --------------------------------------------------------------------------- #
# Compare                                                                      #
# --------------------------------------------------------------------------- #
class MixedRunsError(ValueError):
    """The compared rows mix [PerfContext] build (Debug, Release) or textureQuality values."""


def _mixed(rows: list, key: str) -> list:
    values = {r["context"][key] for r in rows
              if r["context"] is not None and r["context"].get(key) not in (None, "na")}
    return sorted(values) if len(values) > 1 else []


def compare_rows(rows_a: list, rows_b: list, allow_mixed: bool = False) -> dict:
    if not allow_mixed:
        for key in _COMPARED_KEYS:
            mixed = _mixed(rows_a + rows_b, key)
            if mixed:
                raise MixedRunsError(f"{key} differs across the compared rows: "
                                     f"{', '.join(mixed)} (pass --allow-mixed to compare anyway)")
    metrics = []
    for name in METRICS:
        a = [r[name] for r in rows_a if r[name] is not None]
        b = [r[name] for r in rows_b if r[name] is not None]
        ma, mb = _median(a), _median(b)
        delta = mb - ma if ma is not None and mb is not None else None
        pct = delta * 100.0 / ma if delta is not None and ma else None
        metrics.append({"metric": name, "n_a": len(a), "a": ma, "n_b": len(b), "b": mb,
                        "delta": delta, "pct": pct})

    def flag_counts(rows: list) -> dict:
        counts: dict = {}
        for r in rows:
            for f in r["flags"]:
                counts[f] = counts.get(f, 0) + 1
        return dict(sorted(counts.items()))

    def unchecked(rows: list) -> int:
        """Rows the mixed-settings refusal could not check: no [PerfContext], or `na` in it."""
        return sum(1 for r in rows if r["context"] is None
                   or any(r["context"].get(k) in (None, "na") for k in _COMPARED_KEYS))

    return {"a": {"rows": len(rows_a)}, "b": {"rows": len(rows_b)}, "metrics": metrics,
            "flags": {"a": flag_counts(rows_a), "b": flag_counts(rows_b)},
            "context_unchecked": {"a": unchecked(rows_a), "b": unchecked(rows_b)}}


# --------------------------------------------------------------------------- #
# Text output                                                                  #
# --------------------------------------------------------------------------- #
def _fmt(value, digits: int = 2) -> str:
    if value is None:
        return "-"
    if isinstance(value, float):
        return f"{value:.{digits}f}"
    return str(value)


def _tag_lines(entries: list, where: str = "") -> list:
    """One `tag [Tag]: N line(s)` report line per tag of these extra-tag entries, in the order the
    tags first appear."""
    counts: dict = {}
    for e in entries:
        counts[e["tag"]] = counts.get(e["tag"], 0) + 1
    return [f"  tag [{tag}]: {n} line(s){where}" for tag, n in counts.items()]


def format_header(logs: list, prefix: str = "") -> list:
    """One line per log read, then its first unparsed lines verbatim, then its extra tags from
    before a game's first mission, counted per tag."""
    out = []
    for h in logs:
        out.append(f"{prefix}log: {h['path']} size={h['size']}B lines={h['lines']} "
                   f"missions={h['missions']} unparsed={h['malformed']}")
        out += [f"  unparsed line {b['line']}: {b['text']}" for b in h["malformed_first"]]
        if h["malformed"] > len(h["malformed_first"]):
            out.append(f"  ... and {h['malformed'] - len(h['malformed_first'])} more unparsed")
        out += _tag_lines(h["extra_tags"], " before the first mission")
    return out


def format_rows(rows: list, malformed: int) -> str:
    out = []
    for r in rows:
        out.append(
            f"{r['log']} #{r['mission_index']} {r['mission'] or '-'} {r['scene'] or '-'} "
            f"windows={r['windows']}/{r['steady_windows']} fps={_fmt(r['fps_median'], 1)} "
            f"avgMs={_fmt(r['avg_ms_median'])} p95Ms={_fmt(r['p95_ms_median'])} "
            f"maxMs={_fmt(r['max_ms_max'], 1)} gc0/min={_fmt(r['gc0_per_min'], 1)} "
            f"gc1/min={_fmt(r['gc1_per_min'], 1)} gc2/min={_fmt(r['gc2_per_min'], 1)} "
            f"agents={_fmt(r['agents_max'])} "
            f"spawnMaxMs={_fmt(r['spawn_max_ms'], 1)} spawnGc0={_fmt(r['spawn_gc0'])} "
            f"loadMs={_fmt(r['load_ms'])} flags={','.join(r['flags']) or 'none'}")
        out += [f"  {flag}: {why}" for flag, why in r["flag_evidence"].items()]
        out += _tag_lines(r["extra_tags"])
        if r["heartbeat_disabled"]:
            out.append("  heartbeat disabled during this mission (see its [MissionPerf] ERROR line)")
        if r["load_buckets"]:
            out.append("  load: " + " ".join(f"{k}={_fmt(v)}" for k, v in r["load_buckets"].items())
                       + f" dominant={r['load_dominant'] or '-'}")
        tick = r["tick_profile"]
        if tick:
            out.append("  tick ms/s: " + " ".join(f"{k}={v:.1f}" for k, v in tick["phase_ms_per_s"].items()))
            for b in tick["behaviours"][:8]:
                share = f"{b['share'] * 100:.1f}%" if b["share"] is not None else "-"
                out.append(f"    {b['type']} {b['ms_per_s']:.2f} ms/s share={share}")
        hitches = r["hitches"]
        if hitches["count"] or hitches["lines"]:
            parsed = hitches["lines"]
            phases = ", ".join(f"{k} x{v}" for k, v in sorted(hitches["by_phase"].items()))
            if parsed != hitches["count"]:
                phases = (f"{parsed} parsed [Hitch] line{'' if parsed == 1 else 's'}: {phases}"
                          if parsed else "no [Hitch] line parsed")
            out.append(f"  hitches: {hitches['count']} ({phases})")
    out.append(f"rows: {len(rows)}  malformed lines skipped: {malformed}")
    return "\n".join(out)


def format_compare(result: dict) -> str:
    out = [f"A: {result['a']['rows']} rows   B: {result['b']['rows']} rows",
           f"{'metric':<15}{'N(A)':>5}{'A':>11}{'N(B)':>5}{'B':>11}{'delta':>10}{'pct':>9}"]
    for m in result["metrics"]:
        pct = f"{m['pct']:+.1f}%" if m["pct"] is not None else "-"
        delta = f"{m['delta']:+.2f}" if m["delta"] is not None else "-"
        out.append(f"{m['metric']:<15}{m['n_a']:>5}{_fmt(m['a']):>11}{m['n_b']:>5}"
                   f"{_fmt(m['b']):>11}{delta:>10}{pct:>9}")
    for side in ("a", "b"):
        flags = ", ".join(f"{k} x{v}" for k, v in result["flags"][side].items()) or "none"
        out.append(f"flags {side.upper()}: {flags}")
        if result["context_unchecked"][side]:
            out.append(f"  {result['context_unchecked'][side]} {side.upper()} row(s) without a "
                       "[PerfContext] build and texture quality: those settings unchecked")
    return "\n".join(out)


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def _read(path: str) -> str:
    with open(path, encoding="utf-8-sig", errors="replace") as fh:
        return fh.read()


def _load(paths: list) -> tuple[list, int, list]:
    """(rows, malformed lines in all, one header per log) for the logs at these paths."""
    rows, malformed, logs = [], 0, []
    for path in paths:
        r, header = scan_log(_read(path), Path(path).name)
        rows.extend(r)
        malformed += header["malformed"]
        logs.append({"path": path, "size": os.path.getsize(path), **header})
    return rows, malformed, logs


def _main_rows(argv: list) -> int:
    p = argparse.ArgumentParser(prog="perf_runs.py",
                                description="One performance row per mission in taom_debug logs.",
                                epilog="To compare two groups of logs: perf_runs.py compare --a <log> "
                                       "[...] --b <log> [...] (compare --help lists its options).")
    p.add_argument("logs", nargs="+")
    p.add_argument("--json", action="store_true", help="print JSON instead of text")
    args = p.parse_args(argv)
    try:
        rows, malformed, logs = _load(args.logs)
    except OSError as e:
        print(f"perf_runs: cannot read {e.filename}: {e.strerror}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps({"logs": logs, "rows": rows, "malformed": malformed}, indent=2))
    else:
        print("\n".join(format_header(logs) + [format_rows(rows, malformed)]))
    return 0 if rows else 1


def _main_compare(argv: list) -> int:
    p = argparse.ArgumentParser(prog="perf_runs.py compare",
                                description="Compare the mission rows of two groups of logs.")
    p.add_argument("--a", nargs="+", required=True, metavar="LOG")
    p.add_argument("--b", nargs="+", required=True, metavar="LOG")
    p.add_argument("--scene", help="keep only rows of this scene id")
    p.add_argument("--allow-mixed", action="store_true",
                   help="compare even when [PerfContext] build or textureQuality differ")
    p.add_argument("--json", action="store_true", help="print JSON instead of text")
    args = p.parse_args(argv)
    try:
        rows_a, _, logs_a = _load(args.a)
        rows_b, _, logs_b = _load(args.b)
    except OSError as e:
        print(f"perf_runs: cannot read {e.filename}: {e.strerror}", file=sys.stderr)
        return 2
    if args.scene:
        rows_a = [r for r in rows_a if r["scene"] == args.scene]
        rows_b = [r for r in rows_b if r["scene"] == args.scene]
    if not rows_a or not rows_b:
        print(f"perf_runs: no mission rows in group {'A' if not rows_a else 'B'}", file=sys.stderr)
        return 1
    try:
        result = compare_rows(rows_a, rows_b, allow_mixed=args.allow_mixed)
    except MixedRunsError as e:
        print(f"perf_runs: refusing to compare: {e}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps({"logs": {"a": logs_a, "b": logs_b}, **result}, indent=2))
    else:
        print("\n".join(format_header(logs_a, "A ") + format_header(logs_b, "B ")
                        + [format_compare(result)]))
    return 0


def main(argv: list | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    # Unparsed lines are printed verbatim, and a log may hold characters the console cannot show.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    if argv and argv[0] == "compare":
        return _main_compare(argv[1:])
    return _main_rows(argv)


if __name__ == "__main__":
    sys.exit(main())
