#!/usr/bin/env python3
"""Triage a TAOM battle-load hang: equipment issue or code/scene issue?

Players report that entering a battle/siege *sometimes* hangs forever on the
loading screen — works for some, fails for others on the SAME battle (the classic
signature of per-install data divergence: an item/mesh present on one player's
LOTRLOME_Armory install but missing on another). The `BattleLoadDiagnostics`
feature phase-stamps the load lifecycle to `Logs/taom_debug_*.log`; this tool reads
that log and prints a one-line VERDICT so the equipment-vs-code call is mechanical
instead of a manual read across three tools.

INPUT
  A player's `taom_debug.log` (the `[BattleLoad] seq=N t=+Nms phase=… ` lines).
  Optionally the player's newest `rgl_log_errors_*.txt` for the authoritative
  engine cross-check, or a crash-bundle .zip that contains both.

VERDICT classes (the phase the log ENDS on discriminates the hang)
  EQUIPMENT           ends at AgentEquipBegin with no matching AgentEquipOk — the
                      engine stalled equipping that agent; the dumped slots name the
                      suspect item + its declared mesh/`bo_` collision names.
  EQUIPMENT_CONFIRMED + the rgl_log says `get_object failed for body: <suspect>`.
  POST_EQUIP          ends at AgentEquipOk or FinishMissionLoadingDone — the agents
                      equipped fine but the battle never became playable; NOT an
                      equipment-resolution hang (look at non-equip spawn steps / scene /
                      script init / the first OnMissionTick).
  RENDER_WAIT         ends at WaitingForRender — everything loaded and the mission is
                      held at the native SceneView.ReadyToRender gate. A shaders= count
                      that is FALLING means a cold shader cache (a slow load, not a
                      hang); a frozen count is the wedge.
  SCENE               ends at MissionInitialize / BattleSceneSelected /
                      MissionInitializeDone / FinishMissionLoadingBegin /
                      WaitingForSceneLoad — froze during mission construction before any
                      agent equipped; a code/scene issue. Ending on WaitingForSceneLoad is
                      the most informative of the five: that marker only appears once the
                      async wait has already run long, and it carries polls=/waitMs= from
                      inside the wait, so the fault is bounded to the interval after it.
  PRE_SCENE           ends at EncounterStart / MissionOpenNew — froze in mission setup.
  COMPLETED           a BattlePlayable phase exists — the load finished; the hang (if
                      any) is not in the battle-load path.
  UNKNOWN             no `[BattleLoad]` lines — diagnostics were off or wrong file.

The rgl cross-check REUSES `validate_mesh_refs.parse_rgl_text` (the same engine the
mesh-ref validator uses) rather than re-implementing it. For offline "missing from
shipped Armory vs present (player-install problem)" confirmation, run
`tools/validate_mesh_refs.py` on the suspect item ids.

Since #386 the same log also carries `[MemSample]` memory-telemetry lines (periodic
commit/working-set samples + one session line + WARN LOW COMMIT HEADROOM markers).
The tool parses them into a "Memory trend" report section and a `memory` JSON block.
This is additive decoration only: the verdict lattice, kinds, and exit codes are
untouched. For logs without [MemSample] lines the report text is byte-identical;
the --json payload always carries a `memory` key (null when no samples), so JSON
output for old logs differs by exactly that one key.

The same log also carries `[MemStation]` screen-transition anchors (one line per screen
open and close) when the memory sampler is on. They give a "Memory by station" report
section and a `stations` JSON block (null when absent), naming WHICH screen a commit
rise happened on — the periodic samples can only say that one happened. Same additive
contract: disjoint tag, own list, no effect on the verdict lattice, kinds, or exit codes.

Since 2026-08-07 three more phase markers split the previously-dark
MissionInitialize -> MissionAfterStartBegin window (an 11.9 s measured blind spot):
MissionInitializeDone, FinishMissionLoadingBegin (carrying `polls=` / `waitMs=`) and
FinishMissionLoadingDone. They give the "Load timing" report section + a `timings` JSON
block reporting the six named buckets the Phase-2 runbook consumes
(docs/investigations/native-commit-audit-2026-08.md). Timings are **gap-based**: the
service's stopwatch is IsRunning-guarded, so a second mission in the same process
inherits the first's origin — absolutes keep growing while gaps stay valid. Same additive
contract as [MemSample]: no new verdict kinds, no exit-code change, no report section for
a log without those markers, and one extra `timings` JSON key (null when absent).

Usage:
  python tools/triage_battle_load.py <taom_debug.log>
  python tools/triage_battle_load.py <taom_debug.log> --rgl-log <rgl_log_errors_*.txt>
  python tools/triage_battle_load.py --bundle <taom_crash_*.zip>
  python tools/triage_battle_load.py <taom_debug.log> --json verdict.json

Exit code: 1 if a hang was diagnosed (EQUIPMENT*/SCENE/PRE_SCENE/POST_EQUIP/RENDER_WAIT),
2 if an input path is bad, else 0 (COMPLETED / UNKNOWN).
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import zipfile
from dataclasses import dataclass, field
from pathlib import Path

# Import the mesh-ref validator's rgl parser (sibling tool). Inserting the script
# dir makes `import validate_mesh_refs` work both run-as-script and import-as-module.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import validate_mesh_refs as vm  # noqa: E402


# --------------------------------------------------------------------------- #
# Log line grammar (the real contract — see FileLogger + BattleLoadDiagnostics #
# Service + EquipmentDumpFormatter; the phase names are a stable log contract)  #
# --------------------------------------------------------------------------- #
# FileLogger writes:  [yyyy-MM-dd HH:mm:ss] [LEVEL] <message>
_PREFIX_RE = re.compile(
    r"^\[\d{4}-\d\d-\d\d[ T]\d\d:\d\d:\d\d\]\s+\[(?:INFO|DEBUG|WARNING|ERROR)\]\s+")
_TAG = "[BattleLoad]"

# [BattleLoad] seq=N t=+Nms phase=PHASE <detail>
_PHASE_RE = re.compile(
    r"\[BattleLoad\]\s+seq=(\d+)\s+t=\+(\d+)ms\s+phase=(\w+)\s*(.*)$")
# [BattleLoad]   slot=X id=Y bo=Z shieldBo=Z holsterBo=Z mesh=Z kind=K  (all \S+; <null> ok)
_SLOT_RE = re.compile(
    r"\[BattleLoad\]\s+slot=(\S+)\s+id=(\S+)\s+bo=(\S+)\s+shieldBo=(\S+)\s+"
    r"holsterBo=(\S+)\s+mesh=(\S+)\s+kind=(\S+)")
# [BattleLoad] WATCHDOG STILL LOADING after Ns — [tokens ]last <statusline>  (em-dash or hyphen)
# The optional token block carries `shaders=N` and `churn-capped`, which the C# side inserts
# between the dash and the literal `last`. It was NOT optional here until 2026-09-04, so the
# regex matched only the token-free shape and silently dropped the watchdog line for exactly
# the bundles carrying telemetry. `(?:(.*?)\s)?` is non-greedy and stops at the FIRST
# `last`, so a status line that itself contains the word "last" still parses.
_WATCHDOG_RE = re.compile(
    r"\[BattleLoad\]\s+WATCHDOG STILL LOADING after (\d+)s\s+[—-]\s+"
    r"(?:(.*?)\s)?last\s+(.*)$")
# CurrentStatusLine: phase=PHASE seq=N <detail>. phase token is (\S+) so the initial
# sentinel `phase=<none>` still yields a last_phase; seq is optional for the same reason.
_STATUS_RE = re.compile(r"phase=(\S+)(?:\s+seq=(\d+))?\s*(.*)$")

# Per-phase detail parsers.
# char/culture are [^']* — ids never contain an apostrophe, and a lazy `.*?` swallowed the
# race/monster/actionSet tokens that sit between culture= and slots= (shipped 2026-08-02),
# reporting the whole run as the culture. The agent NAME keeps its lazy group because names
# legitimately contain apostrophes ('Sauron's Lieutenant') and `char='` anchors it.
_EQUIP_BEGIN_RE = re.compile(
    r"agent#(\d+)\s+'(.*?)'\s+char='([^']*)'\s+culture='([^']*)'.*?\bslots=(\d+)")
# Optional: absent in logs predating the per-loadout dedupe (2026-08-03).
_LOADOUT_RE = re.compile(r"\bloadout=#(\d+)")
# Greedy + end-anchored so an apostrophe in the name (e.g. 'Sauron's Lieutenant') is kept,
# not truncated at the first inner quote (AgentEquipOk has no trailing token to anchor on).
_EQUIP_OK_RE = re.compile(r"agent#(\d+)\s+'(.*)'\s*$")
_SCENE_ID_RE = re.compile(r"sceneId='(.*?)'")
_SCENE_RE = re.compile(r"scene='(.*?)'")

# FinishMissionLoadingBegin's wait pair + the MemStats process token. The C# twin is
# BattleLoadDiagnosticsService.FormatFinishWaitDetail, pinned character-for-character on
# both sides: "polls=87 waitMs=1449" and — when MissionInitializeDone was not observed —
# "polls=87" with waitMs OMITTED. An absent waitMs must stay None here; reading it as 0
# would claim an instantaneous wait that was never measured. privMB is likewise absent
# (not 0) whenever the process read failed.
_POLLS_RE = re.compile(r"\bpolls=(\d+)\b")
_WAIT_MS_RE = re.compile(r"\bwaitMs=(\d+)\b")
_PRIV_MB_RE = re.compile(r"\bprivMB=(-?\d+)\b")

# [MemSample] memory telemetry (#386). Token order, names, separators, and the %
# suffix are the pinned cross-language contract with the C# sampler; the WARN prefix
# is optional (present only on low-headroom lines, emitted at WARNING level).
#   [MemSample] session totalPhysMB=N sysCommitLimitMB=N
#   [MemSample] [WARN LOW COMMIT HEADROOM headroomMB=N ]privMB=N wsMB=N heapMB=N
#               sysCommitUsedMB=N sysCommitLimitMB=N availPhysMB=N memLoad=N%
_MEM_TAG = "[MemSample]"
_MEMSAMPLE_RE = re.compile(
    r"\[MemSample\]\s+(?:WARN LOW COMMIT HEADROOM headroomMB=(-?\d+)\s+)?"
    r"privMB=(-?\d+)\s+wsMB=(-?\d+)\s+heapMB=(-?\d+)\s+sysCommitUsedMB=(-?\d+)\s+"
    r"sysCommitLimitMB=(-?\d+)\s+availPhysMB=(-?\d+)\s+memLoad=(-?\d+)%")
_MEMSESSION_RE = re.compile(
    r"\[MemSample\]\s+session\s+totalPhysMB=(-?\d+)\s+sysCommitLimitMB=(-?\d+)")

# [MemStation] screen-transition anchors (#386 follow-up). Same 7-token tail as
# [MemSample] (the C# side shares one FormatSampleTokens helper), prefixed by the
# transition kind and the screen name. Pinned twin:
# MemoryStationSamplerTests.FormatStation_{Enter,Exit}WithKnownValues_MatchesPinnedLiteral.
#   [MemStation] enter screen='GauntletEncyclopediaScreen' privMB=N wsMB=N heapMB=N
#                sysCommitUsedMB=N sysCommitLimitMB=N availPhysMB=N memLoad=N%
# The tag is DISJOINT from [MemSample] on purpose: station readings are event-driven
# and must never enter the periodic trend, which would corrupt its first/peak/last.
_MEMSTATION_TAG = "[MemStation]"
# Session boundary emitted by MemoryStationSampler.FormatSessionReset. Not a measurement, so it
# deliberately does not match _MEMSTATION_RE; it is recognised by this literal alone.
_MEMSTATION_RESET = "session-reset"
_MEMSTATION_RE = re.compile(
    r"\[MemStation\]\s+(enter|exit)\s+screen='([^']*)'\s+"
    r"privMB=(-?\d+)\s+wsMB=(-?\d+)\s+heapMB=(-?\d+)\s+sysCommitUsedMB=(-?\d+)\s+"
    r"sysCommitLimitMB=(-?\d+)\s+availPhysMB=(-?\d+)\s+memLoad=(-?\d+)%")
# Capture the FileLogger timestamp (same shape _PREFIX_RE strips) for sample ts.
_TS_RE = re.compile(r"^\[(\d{4}-\d\d-\d\d[ T]\d\d:\d\d:\d\d)\]")

# Low-commit-headroom thresholds. Single source of truth is the C# sampler:
# Main/Features/BattleLoadDiagnostics/MemoryPressureSampler.cs (WarnHeadroomFloorMb,
# WarnHeadroomPercent; its RearmHysteresisMb only governs C#-side WARN re-arming and
# has no triage-side mirror). Keep these numerically identical to that class.
MEM_WARN_HEADROOM_FLOOR_MB = 2048
MEM_WARN_HEADROOM_PERCENT = 10

NULL = "<null>"
HANG_KINDS = {"EQUIPMENT", "EQUIPMENT_CONFIRMED", "SCENE", "PRE_SCENE", "POST_EQUIP",
              "RENDER_WAIT"}

# WaitingForRender detail tokens. Twin literal of
# BattleLoadDiagnosticsService.FormatRenderWaitDetail — both tokens are OMITTED when
# unmeasured (never rendered as 0), so both groups are optional here and a missing one
# means "not observed", not "zero".
_WAITED_MS_RE = re.compile(r"\bwaitedMs=(\d+)\b")
_SHADERS_RE = re.compile(r"\bshaders=(\d+)\b")

# Terminal phases that mean "mission construction, before any agent equipped". The two
# 2026-08-07 additions belong here and NOT in the PRE_SCENE fall-through: a log ending at
# MissionInitializeDone died in the native async load wait, which is the opposite end of
# the lifecycle from "froze before scene selection".
SCENE_TERMINALS = {"MissionInitialize", "BattleSceneSelected",
                   "MissionInitializeDone", "FinishMissionLoadingBegin",
                   "WaitingForSceneLoad"}

# Twin literal of BattleLoadDiagnosticsService.SceneLoadWaitEmitIntervalMs. It bounds how
# far past the last heartbeat the fault can be, which is the whole point of the marker.
SCENE_LOAD_WAIT_INTERVAL_MS = 5000

# Per-phase tail of the SCENE summary. The first two keep the original wording verbatim.
_SCENE_HINTS = {
    "MissionInitialize":
        "Audit the scene ref with tools/audit_battle_scenes.py / tools/audit_scene_names.py.",
    "BattleSceneSelected":
        "Audit the scene ref with tools/audit_battle_scenes.py / tools/audit_scene_names.py.",
    "MissionInitializeDone":
        "Mission.Initialize returned, so the native InitializeMission survived: the "
        "async load wait never completed (native Mission.IsLoadingFinished never returned "
        "true). On a build carrying WaitingForSceneLoad, the ABSENCE of any heartbeat here "
        "is itself a reading. The heartbeat is driven BY the TickLoading loop, so it cannot "
        "fire once the main thread wedges inside one native frame: silence on a load known "
        "to have run for many seconds IS the blocking-spin shape (#352), while silence on a "
        "short load just means it died before the 3s threshold. On an older log, read "
        "polls=/waitMs= on the FinishMissionLoadingBegin line of a run that got further.",
    "FinishMissionLoadingBegin":
        "The load wait completed; the freeze is in Scene.SetOwnerThread, one of the two "
        "warm-up Mission.Tick(0.001f) calls, or Handler.OnMissionAfterStarting.",
    # Unlike the MissionInitializeDone arm above, this one does NOT have to send the reader
    # off to another run: the heartbeat carries polls=/waitMs= from inside the wait itself,
    # so _scene_load_wait_notes() below reads them directly off this line.
    "WaitingForSceneLoad":
        "The async load wait was still running when the process stopped, and this line is "
        "the last heartbeat before it, so the fault lands within "
        f"{SCENE_LOAD_WAIT_INTERVAL_MS // 1000}s of the t=+ stamp above. Frames were still "
        "running right up to that point: the heartbeat is emitted FROM a TickLoading frame, "
        "so its presence rules OUT a main thread wedged inside one native frame.",
}

# The bucket ledger the Phase-2 runbook records (native-commit-audit-2026-08.md). Spans
# are GAPS between markers, never absolute t=+ values: the service stopwatch is
# IsRunning-guarded, so a chained second mission inherits the first mission's origin.
_BUCKETS = (
    ("bucket1", "MissionInitialize", "MissionInitializeDone",
     "native MBAPI.IMBMission.InitializeMission — scene / physics / terrain"),
    ("bucket2", "MissionInitializeDone", "FinishMissionLoadingBegin",
     "N x TickLoading polling native Mission.IsLoadingFinished"),
    ("bucket3a", "FinishMissionLoadingBegin", "MissionAfterStartBegin",
     "Scene.SetOwnerThread + 2 warm-up Mission.Tick(0.001f) + OnMissionAfterStarting"),
    ("bucket3b", "MissionAfterStartBegin", "MissionAfterStartDone",
     "Mission.AfterStart — the AgentEquip burst"),
    ("bucket3c", "MissionAfterStartDone", "FinishMissionLoadingDone",
     "OnMissionLoadingFinished + Scene.ResumeLoadingRenderings"),
    ("bucket4", "FinishMissionLoadingDone", "BattlePlayable",
     "SceneView.ReadyToRender gate, then the first OnMissionTick (see WaitingForRender)"),
)

# The four markers in this window that carry MemStats(). Their privMB trajectory is what
# decides whether the stall and the commit growth are ONE problem (rises across the
# dominant bucket) or TWO (flat across it).
_MEMSTATS_MARKERS = ("MissionInitialize", "MissionInitializeDone",
                     "FinishMissionLoadingBegin", "FinishMissionLoadingDone")

# The section exists only for logs that actually carry the 2026-08-07 markers, so older
# logs keep a byte-identical report.
_BUCKET_SPLIT_MARKERS = frozenset(
    ("MissionInitializeDone", "FinishMissionLoadingBegin", "FinishMissionLoadingDone"))


# --------------------------------------------------------------------------- #
# Model                                                                        #
# --------------------------------------------------------------------------- #
@dataclass
class Slot:
    slot: str
    item_id: str
    bo: str
    shield_bo: str
    holster_bo: str
    mesh: str
    kind: str

    def mesh_tokens(self) -> list:
        """Non-<null> mesh/body names declared on this slot (rgl cross-check candidates)."""
        return [t for t in (self.bo, self.shield_bo, self.holster_bo, self.mesh)
                if t and t != NULL]


@dataclass
class PhaseEvent:
    seq: int | None
    ms: int | None
    phase: str
    detail: str
    slots: list = field(default_factory=list)


@dataclass
class Watchdog:
    elapsed_seconds: int
    last_phase: str
    last_detail: str
    # The `shaders=N` / `churn-capped` tokens the line carried, or "" for an older log that
    # predates them. Absent is absent: never rendered as a fabricated shaders=0.
    tokens: str = ""


@dataclass
class MemSample:
    """One periodic [MemSample] line. ts comes from the FileLogger prefix (None on
    bare lines); warned marks the WARN LOW COMMIT HEADROOM variant."""
    ts: str | None
    priv_mb: int
    ws_mb: int
    heap_mb: int
    commit_used_mb: int
    commit_limit_mb: int
    avail_phys_mb: int
    mem_load: int
    warned: bool = False

    @property
    def headroom_mb(self) -> int:
        return max(0, self.commit_limit_mb - self.commit_used_mb)


@dataclass
class MemStation:
    """One [MemStation] screen-transition anchor. kind is 'enter' or 'exit'; screen is
    the sanitized CLR type name of the ScreenBase that was pushed or popped."""
    ts: str | None
    kind: str
    screen: str
    priv_mb: int
    ws_mb: int
    heap_mb: int
    commit_used_mb: int
    commit_limit_mb: int
    avail_phys_mb: int
    mem_load: int


@dataclass
class Timeline:
    events: list = field(default_factory=list)
    watchdog: Watchdog | None = None
    mem_samples: list = field(default_factory=list)
    mem_session: dict | None = None
    mem_warned: bool = False
    # Kept in its OWN list, never merged into mem_samples: these are event-driven
    # readings and would corrupt the periodic trend's first/peak/last.
    mem_stations: list = field(default_factory=list)
    mem_station_resets: int = 0


@dataclass
class Verdict:
    kind: str
    summary: str
    stuck_agent: dict | None = None
    suspect_names: list = field(default_factory=list)
    scene: str | None = None
    confirmed_assets: list = field(default_factory=list)
    notes: list = field(default_factory=list)


# --------------------------------------------------------------------------- #
# Parsing (pure over text — synthetic fixtures in the tests, no game install)  #
# --------------------------------------------------------------------------- #
def parse_battle_load_log(text: str) -> Timeline:
    """Extract the ordered [BattleLoad] phase events + slot dumps + watchdog line.

    Tolerates lines with or without the FileLogger `[ts] [LEVEL]` prefix. Slot
    dumps attach to the most recent AgentEquipBegin event.

    Since 2026-08-03 the dump is written once per DISTINCT loadout and later agents
    wearing it carry only `loadout=#N` (a 429-agent arena emitted 1,146 slot lines
    encoding 11 rows). The stuck agent is usually one of the deduped ones, so an agent
    citing an id gets the earlier block re-attached — otherwise the EQUIPMENT verdict
    would name no suspect at all. Resolution happens as the Begin line is read, against
    only the blocks seen since the last MissionInitialize: ids restart per load, so a
    single global pass would resolve every mission against the last one's map. Logs
    predating the dedupe carry no id and are unaffected."""
    tl = Timeline()
    loadouts: dict = {}
    collecting = None  # loadout id whose block is currently being read, if any
    for raw in text.splitlines():
        line = _PREFIX_RE.sub("", raw)
        # [MemStation] screen anchors (#386 follow-up) ride the same log and are inert to
        # the phase timeline in exactly the same way as [MemSample] below. Kept in their own
        # list so an event-driven reading can never land in the periodic trend.
        if _MEMSTATION_TAG in line:
            if _MEMSTATION_RESET in line:
                # A new measurement segment began (return to the main menu). Enters still
                # pending from the previous segment can never be closed, so drop them rather
                # than letting a later exit pair across the boundary and invent a delta that
                # spans a discontinuity.
                tl.mem_station_resets += 1
                tl.mem_stations.append(MemStation(
                    ts=(_TS_RE.match(raw).group(1) if _TS_RE.match(raw) else None),
                    kind="reset", screen="", priv_mb=0, ws_mb=0, heap_mb=0,
                    commit_used_mb=0, commit_limit_mb=0, avail_phys_mb=0, mem_load=0))
                continue
            m = _MEMSTATION_RE.search(line)
            if m:
                tm = _TS_RE.match(raw)
                tl.mem_stations.append(MemStation(
                    ts=tm.group(1) if tm else None,
                    kind=m.group(1), screen=m.group(2),
                    priv_mb=int(m.group(3)), ws_mb=int(m.group(4)),
                    heap_mb=int(m.group(5)), commit_used_mb=int(m.group(6)),
                    commit_limit_mb=int(m.group(7)), avail_phys_mb=int(m.group(8)),
                    mem_load=int(m.group(9))))
            continue
        # [MemSample] telemetry (#386) rides the same log but is inert to the phase
        # timeline: no PhaseEvent, no slot/loadout attachment, no seq/terminal impact.
        if _MEM_TAG in line:
            m = _MEMSAMPLE_RE.search(line)
            if m:
                tm = _TS_RE.match(raw)
                sample = MemSample(
                    ts=tm.group(1) if tm else None,
                    priv_mb=int(m.group(2)), ws_mb=int(m.group(3)),
                    heap_mb=int(m.group(4)), commit_used_mb=int(m.group(5)),
                    commit_limit_mb=int(m.group(6)), avail_phys_mb=int(m.group(7)),
                    mem_load=int(m.group(8)), warned=m.group(1) is not None)
                tl.mem_samples.append(sample)
                if sample.warned:
                    tl.mem_warned = True
            else:
                m = _MEMSESSION_RE.search(line)
                if m:
                    tl.mem_session = {"total_phys_mb": int(m.group(1)),
                                      "commit_limit_mb": int(m.group(2))}
            continue
        if _TAG not in line:
            continue

        m = _WATCHDOG_RE.search(line)
        if m:
            tokens = (m.group(2) or "").strip()
            status = m.group(3)
            sm = _STATUS_RE.search(status)
            tl.watchdog = Watchdog(
                elapsed_seconds=int(m.group(1)),
                last_phase=sm.group(1) if sm else "",
                last_detail=(sm.group(3).strip() if sm else status.strip()),
                tokens=tokens)
            continue

        m = _SLOT_RE.search(line)
        if m:
            slot = Slot(*m.groups())
            if tl.events and tl.events[-1].phase == "AgentEquipBegin":
                tl.events[-1].slots.append(slot)
                if collecting is not None:
                    loadouts.setdefault(collecting, []).append(slot)
            continue

        m = _PHASE_RE.search(line)
        if m:
            phase = m.group(3)
            # The service clears its map here, so ids restart at #1 for the next load.
            if phase == "MissionInitialize":
                loadouts.clear()
            event = PhaseEvent(seq=int(m.group(1)), ms=int(m.group(2)),
                               phase=phase, detail=m.group(4).strip())
            tl.events.append(event)

            collecting = None
            if phase == "AgentEquipBegin":
                lid = _loadout_id(event.detail)
                if lid is None:
                    pass                              # pre-dedupe log: block follows, no id to key
                elif lid in loadouts:
                    event.slots = list(loadouts[lid])  # deduped: block was written earlier
                else:
                    collecting = lid                   # this agent carries the block
    return tl


def _loadout_id(detail: str):
    m = _LOADOUT_RE.search(detail)
    return int(m.group(1)) if m else None


def _parse_equip_begin(detail: str) -> dict:
    m = _EQUIP_BEGIN_RE.search(detail)
    if not m:
        return {"raw": detail}
    return {"index": m.group(1), "name": m.group(2), "char": m.group(3),
            "culture": m.group(4), "slots": m.group(5)}


def _parse_equip_ok(detail: str) -> dict:
    m = _EQUIP_OK_RE.search(detail)
    if not m:
        return {"raw": detail}
    return {"index": m.group(1), "name": m.group(2)}


def _parse_scene(event: PhaseEvent) -> str | None:
    m = _SCENE_ID_RE.search(event.detail) or _SCENE_RE.search(event.detail)
    return m.group(1) if m else None


def _classify_render_wait(terminal: PhaseEvent) -> Verdict:
    """A log ending on WaitingForRender: the mission loaded and is waiting on
    SceneView.ReadyToRender(). The shaders= token decides whether that is a working slow
    load or a real wedge, and an ABSENT token is neither (unmeasured, not zero)."""
    m = _SHADERS_RE.search(terminal.detail)
    shaders = int(m.group(1)) if m else None
    m = _WAITED_MS_RE.search(terminal.detail)
    waited = int(m.group(1)) if m else None
    waited_part = f" after {waited}ms" if waited is not None else ""

    if shaders is None:
        v = Verdict(
            "RENDER_WAIT",
            "The mission loaded but never reached its first tick" + waited_part
            + ", and the shader-compilation count could NOT be read (shaders= is absent, "
            "which is unmeasured, not zero). Treat the render gate as unexplained.")
        v.notes.append(
            "An absent shaders= token means the native "
            "Utilities.GetNumberOfShaderCompilationsInProgress() call threw. Check the "
            "top of the log for 'Patch43 diagnostics failed to apply'.")
        return v

    if shaders > 0:
        v = Verdict(
            "RENDER_WAIT",
            f"The mission loaded but is held at the SceneView.ReadyToRender gate"
            + waited_part + f" with {shaders} shader compilations still in flight — a "
            "COLD SHADER CACHE, i.e. a slow load rather than a hang.")
        v.notes.append(
            "Read the WaitingForRender lines in order: a shaders= count trending DOWN is "
            "a load that is working and will finish. A count that stops changing is the "
            "wedge, and the stall watchdog fires its bundle on exactly that reading.")
        v.notes.append(
            "Cause, not symptom: every material the engine has never compiled costs one "
            "serial compile. Cross-check the player's rgl_log for 'Missing shader from "
            "sack' — 430 pbr_metallic misses in one load is what b18f3441 looked like.")
        return v

    v = Verdict(
        "RENDER_WAIT",
        "The mission loaded but never reached its first tick" + waited_part
        + ", with NOTHING compiling (shaders=0). The render gate is stuck for some other "
        "reason — this one is a real wedge, not a cold cache.")
    v.notes.append(
        "SceneView.ReadyToRender() is false with an empty compile queue, so suspect scene "
        "resource streaming or a MissionLogic / MissionBehavior added by TAOM or another "
        "mod — read the TaomBehaviorAdded stamps above.")
    return v


# --------------------------------------------------------------------------- #
# Classification                                                               #
# --------------------------------------------------------------------------- #
def classify(tl: Timeline) -> Verdict:
    if not tl.events:
        return Verdict(
            "UNKNOWN",
            "No [BattleLoad] lines found. Battle Load Diagnostics was disabled, or "
            "this is not a taom_debug.log. Enable it in MCM (TAOM — Battle Load "
            "Diagnostics, on by default) and recapture the hang.")

    # Scoped to the LAST mission, not the whole log. A session holds several missions and any
    # earlier one that completed would otherwise mask a final load that stalled: the b18f3441
    # bundle reported COMPLETED and "the battle-load path is clean" for a log whose last
    # mission never ticked and fired the stall watchdog.
    #
    # When no anchor survives at all (a truncated capture, or diagnostics enabled mid-mission)
    # the fallback is NOT "scan the whole log" — that reproduces the same false COMPLETED.
    # Without a segment boundary the only honest basis for calling a log clean is that it ENDS
    # on BattlePlayable; anything after it belongs to a load we cannot see the start of.
    scope = _last_mission_scope(tl.events)
    if any(e.phase == "BattlePlayable" for e in scope):
        return Verdict(
            "COMPLETED",
            "Battle reached BattlePlayable — the load completed. Any hang the player "
            "saw is not in the battle-load path (look elsewhere).")

    terminal = tl.events[-1]
    ph = terminal.phase

    if ph == "AgentEquipBegin":
        agent = _parse_equip_begin(terminal.detail)
        suspects = []
        for s in terminal.slots:
            suspects.extend(s.mesh_tokens())
        suspects = list(dict.fromkeys(suspects))  # dedup, preserve order
        # A single <null> token is normal (body armor has no collision body, a weapon
        # has no shieldBo). The suspicious case is a slot whose item declares NO mesh
        # AND no body of any kind — a resolved-but-meshless / placeholder item def.
        meshless = [f"{s.slot}={s.item_id}" for s in terminal.slots if not s.mesh_tokens()]
        v = Verdict("EQUIPMENT", _equip_summary(agent),
                    stuck_agent=agent, suspect_names=suspects)
        if meshless:
            v.notes.append(
                "Slots whose item declares NO mesh OR body at all (all tokens <null>) "
                "— a likely broken/placeholder item def: " + ", ".join(meshless))
        v.notes.append(
            "Authoritative confirmation: search the player's newest "
            "rgl_log_errors_*.txt for `get_object failed for body:` and re-run with "
            "--rgl-log, or run tools/validate_mesh_refs.py on the suspect item ids.")
        return v

    if ph == "AgentEquipOk":
        agent = _parse_equip_ok(terminal.detail)
        return Verdict(
            "POST_EQUIP",
            f"Froze AFTER agent#{agent.get('index', '?')} "
            f"'{agent.get('name', '?')}' equipped but before the battle became "
            "playable — NOT an equipment-resolution hang. Look at non-equip spawn "
            "steps (skin/skeleton mesh build) or scene/script post-spawn init.",
            stuck_agent=agent)

    if ph == "WaitingForRender":
        return _classify_render_wait(terminal)

    if ph == "FinishMissionLoadingDone":
        # Everything loaded — Mission.AfterStart returned, so the whole AgentEquip burst
        # completed — and the first OnMissionTick never arrived. There is no stuck agent
        # here, so the summary must not invent one the way the AgentEquipOk arm can.
        v = Verdict(
            "POST_EQUIP",
            "The mission finished loading (phase=FinishMissionLoadingDone) but never "
            "reached its first tick, so the battle never became playable — NOT an "
            "equipment-resolution hang: every agent equipped and Mission.AfterStart "
            "returned.")
        v.notes.append(
            "The remaining span is the SceneView.ReadyToRender gate: MissionState.OnTick "
            "reaches TickMission only through Handler.RenderIsReady(), which stays false "
            "while the scene's shaders compile (FinishMissionLoading ends with "
            "Scene.ResumeLoadingRenderings). Read the player's rgl_log for that window: "
            "a wall of compile_shader lines means a COLD SHADER CACHE and a slow load, "
            "not a hang (bundle b18f3441, 2026-09-04, 818 compiles in 290s).")
        v.notes.append(
            "This log predates the WaitingForRender marker, which is why the span is "
            "unattributed here. A current build stamps it at 1 Hz with a live shaders= "
            "count. Failing that, suspect a MissionLogic / MissionBehavior added by TAOM "
            "or another mod — read the TaomBehaviorAdded stamps above.")
        return v

    if ph in SCENE_TERMINALS:
        scene = _parse_scene(terminal)
        scene_part = f", scene='{scene}'" if scene else ""
        return Verdict(
            "SCENE",
            f"Froze during scene load (phase={ph}{scene_part}) before any agent "
            "equipped — a code/scene issue, not equipment. " + _SCENE_HINTS[ph],
            scene=scene)

    # EncounterStart / MissionOpenNew / anything earlier.
    # (BattleLoadPhase.StallWatchdog never reaches here — the watchdog writes a raw
    # WATCHDOG line, not an Emit() phase marker, so it's captured as tl.watchdog, not
    # as a phase event. If that ever changes, PRE_SCENE is still the right fallback:
    # it fires long before BattlePlayable and is not an equipment-resolution hang.)
    scene = _parse_scene(terminal)
    return Verdict(
        "PRE_SCENE",
        f"Froze very early (phase={ph}) before scene selection — a code issue in "
        "encounter/mission setup, not equipment.",
        scene=scene)


def _equip_summary(agent: dict) -> str:
    return ("Battle load froze while equipping "
            f"agent#{agent.get('index', '?')} '{agent.get('name', '?')}' "
            f"(char='{agent.get('char', '?')}', culture='{agent.get('culture', '?')}'). "
            "The engine stalled resolving one of this agent's items — likely a "
            "missing mesh / `bo_` collision body on this player's install.")


def _headroom_low(used_mb: int, limit_mb: int, floor_mb: int, percent: int) -> bool:
    """Low-commit-headroom test, mirroring the C# decision in
    Main/Features/BattleLoadDiagnostics/MemoryPressureSampler.cs: low if
    headroom < floor OR headroom < percent% of the limit. Garbage inputs
    (limit <= 0, used < 0) never report low; used > limit is a legitimate
    state whose headroom clamps at 0 (and IS low)."""
    if limit_mb <= 0 or used_mb < 0:
        return False
    headroom = max(0, limit_mb - used_mb)
    # Mirror C#'s INTEGER-FLOOR threshold exactly: Math.Max(floor, limit * percent / 100)
    # with C# long division. An exact cross-multiplied comparison (headroom*100 < limit*percent)
    # disagrees with the sampler in the ~1 MB band below the true 10% line whenever the limit
    # isn't a multiple of 10 — i.e. on essentially every real machine (deep-review 2026-08-05).
    return headroom < max(floor_mb, limit_mb * percent // 100)


def _sample_dict(s: MemSample) -> dict:
    return {"ts": s.ts, "priv_mb": s.priv_mb, "ws_mb": s.ws_mb, "heap_mb": s.heap_mb,
            "commit_used_mb": s.commit_used_mb, "commit_limit_mb": s.commit_limit_mb,
            "avail_phys_mb": s.avail_phys_mb, "mem_load": s.mem_load,
            "headroom_mb": s.headroom_mb, "warned": s.warned}


def classify_memory(tl: Timeline, floor_mb: int = MEM_WARN_HEADROOM_FLOOR_MB,
                    percent: int = MEM_WARN_HEADROOM_PERCENT) -> dict | None:
    """Summarize the [MemSample] trend (#386). None when the log carries no samples
    (pre-#386 logs — everything else stays byte-identical). Additive decoration
    only: never touches the verdict lattice, kinds, or exit codes."""
    if not tl.mem_samples:
        return None
    first, last = tl.mem_samples[0], tl.mem_samples[-1]
    low = _headroom_low(last.commit_used_mb, last.commit_limit_mb, floor_mb, percent)
    return {
        "pressure": tl.mem_warned or low,
        "headroom_mb": last.headroom_mb,
        "peak_commit_used_mb": max(s.commit_used_mb for s in tl.mem_samples),
        "first": _sample_dict(first),
        "last": _sample_dict(last),
        "warn_seen": tl.mem_warned,
    }


def classify_stations(tl: Timeline) -> dict | None:
    """Summarize the [MemStation] screen anchors. None when the log carries none (every
    pre-feature log, and any session where the sampler was off) so the report text stays
    byte-identical. Additive decoration only: never touches the verdict lattice.

    Pairs each 'exit' with the most recent unmatched 'enter' for the SAME screen and
    reports the privMB delta across the visit. Unmatched pairs are counted, never silently
    dropped and never turned into a zero delta. The reason is NOT bulk teardown:
    CleanAndPushScreen provably fires OnPopScreen once per removed screen via
    DeactivateAndFinalizeAllScreens (verified against installed v1.4.8), so that path is
    balanced. The real sources are an unmatched EXIT when the sampler subscribed while a screen
    was already open, a transient reader failure dropping one line, and process death before
    OnFinalize runs.

    WINDOW SEMANTICS, and this bounds what any delta here can mean: the engine raises
    OnPushScreen AFTER HandleInitialize/HandleActivate/HandleResume and OnPopScreen AFTER
    HandleFinalize. So a visit delta measures growth while the screen was OPEN, and cannot see
    allocation that happened during the screen's own construction. A screen that allocates on
    open and retains reads as ~0; one that allocates on open and frees on close reads NEGATIVE.
    Read a delta as "what happened while this was on screen", never as "what this screen cost".
    """
    if not tl.mem_stations:
        return None

    open_enters: dict = {}   # screen -> list of pending enter samples (a stack)
    stats: dict = {}         # screen -> accumulator

    def bucket(screen: str) -> dict:
        return stats.setdefault(screen, {
            "screen": screen, "visits": 0, "total_delta_mb": 0,
            # None, NOT 0: seeding a running max with a value outside the data's domain lets the
            # seed win whenever every real delta is negative, and the report then prints
            # "(max visit +0 MB)" beside a large negative total, contradicting it.
            "max_visit_delta_mb": None, "unmatched_enters": 0, "unmatched_exits": 0,
            "nested_visits": 0})

    events = tl.mem_stations
    unmatched_exits = 0
    for idx, st in enumerate(events):
        if st.kind == "reset":
            # Nothing pending survives a session boundary.
            open_enters.clear()
            continue
        if st.kind == "enter":
            open_enters.setdefault(st.screen, []).append((idx, st))
            bucket(st.screen)
            continue
        pending = open_enters.get(st.screen)
        if not pending:
            # An exit with no enter (the sampler subscribed while this screen was already open,
            # or its enter was dropped by a reader failure). Nothing to measure, but coverage is
            # incomplete and the report must say so rather than look complete.
            unmatched_exits += 1
            bucket(st.screen)["unmatched_exits"] = bucket(st.screen)["unmatched_exits"] + 1
            continue
        opened_idx, opened = pending.pop()
        delta = st.priv_mb - opened.priv_mb
        b = bucket(st.screen)
        b["visits"] += 1
        b["total_delta_mb"] += delta
        prev = b["max_visit_delta_mb"]
        b["max_visit_delta_mb"] = delta if prev is None else max(prev, delta)
        # Did another screen open INSIDE this visit? Pairing is per screen NAME with no nesting
        # awareness, so this screen's delta re-includes whatever that inner screen grew while
        # stacked on top of it - and the inner screen reports the same growth under its own name.
        # Summing total_delta_mb across screens therefore double-counts it. Flag the OUTER screen,
        # which is the one whose number is inflated; the inner screen's own delta is clean.
        # ANY enter inside the visit counts, including one of the same type: ScreenManager
        # permits two instances of a type stacked, and excluding same-name nesting made exactly
        # that case invisible while still double-counting it.
        if any(e.kind == "enter" for e in events[opened_idx + 1:idx]):
            b["nested_visits"] += 1

    for screen, pending in open_enters.items():
        if pending:
            bucket(screen)["unmatched_enters"] = len(pending)

    by_screen = sorted(stats.values(),
                       key=lambda b: (-b["total_delta_mb"], b["screen"]))
    nested_any = any(b["nested_visits"] for b in by_screen)
    return {
        "transitions": sum(1 for e in tl.mem_stations if e.kind != "reset"),
        "by_screen": by_screen,
        "top": by_screen[0]["screen"] if by_screen else None,
        "nested_overlap": nested_any,
        "session_resets": tl.mem_station_resets,
        "unmatched_exits": unmatched_exits,
    }


def _last_mission_segment(events: list) -> list:
    """Events from the LAST MissionInitialize onward — the load actually being triaged.

    Buckets must never span two missions: the service stopwatch is IsRunning-guarded, so a
    chained second mission inherits the first's origin and its absolute t=+ values keep
    climbing. Scoping to one segment is what makes the gaps comparable across runs."""
    start = None
    for i, e in enumerate(events):
        if e.phase == "MissionInitialize":
            start = i
    return list(events[start:]) if start is not None else []


# Any of these starts a mission. `_last_mission_segment` deliberately anchors on
# MissionInitialize alone because the bucket ledger's spans are defined against it; this wider
# set exists only for VERDICT scoping, where the question is "which mission does the tail
# belong to" rather than "where do the buckets start".
_MISSION_START_PHASES = ("MissionInitialize", "MissionOpenNew", "EncounterStart")


def _last_mission_start_index(events: list):
    """Index of the last mission-start marker, or None. Shared by the verdict scope and the
    bucket ledger so the two sections of one report cannot describe different missions."""
    start = None
    for i, e in enumerate(events):
        if e.phase in _MISSION_START_PHASES:
            start = i
    return start


def _last_mission_scope(events: list) -> list:
    """Events belonging to the LAST mission in the log, for verdict classification.

    Falls back, when nothing anchors a segment, to the tail from the last BattlePlayable
    onward — so a log that ends on BattlePlayable still reads COMPLETED, while one that
    carries a whole further load after it does not."""
    start = _last_mission_start_index(events)
    if start is not None:
        return list(events[start:])

    last_playable = None
    for i, e in enumerate(events):
        if e.phase == "BattlePlayable":
            last_playable = i
    if last_playable is None:
        return list(events)

    # Strictly AFTER the completed load. If nothing follows, the log ends on BattlePlayable
    # and the BattlePlayable itself is the scope, so COMPLETED still fires.
    tail = list(events[last_playable + 1:])
    return tail if tail else [events[last_playable]]


def _first_event(seg: list, phase: str):
    for e in seg:
        if e.phase == phase:
            return e
    return None


def _last_event(seg: list, phase: str):
    """Newest occurrence of a phase. The scene-load heartbeat repeats every few seconds, and
    only its LAST line carries the wait as it stood when the process stopped."""
    for e in reversed(seg):
        if e.phase == phase:
            return e
    return None


def _span_ms(seg: list, start_phase: str, end_phase: str):
    """Gap between two markers, or None when either is missing / the clock went backwards.
    None means "not measured" and must never be rendered as 0."""
    a, b = _first_event(seg, start_phase), _first_event(seg, end_phase)
    if a is None or b is None or a.ms is None or b.ms is None or b.ms < a.ms:
        return None
    return b.ms - a.ms


def classify_phase_timings(tl: Timeline) -> dict | None:
    """Gap-based bucket ledger for the 2026-08-07 marker split. None for a log without
    those markers (older logs keep a byte-identical report) or without a MissionInitialize
    to anchor the segment. Additive decoration only — never touches the verdict lattice,
    kinds, or exit codes."""
    seg = _last_mission_segment(tl.events)
    if not seg or not any(e.phase in _BUCKET_SPLIT_MARKERS for e in seg):
        return None

    # The ledger anchors on MissionInitialize; the verdict anchors on any mission-start marker.
    # When the LAST mission stalled before reaching MissionInitialize (a PRE_SCENE shape), the
    # ledger's anchor lands in an EARLIER mission and the report would print that mission's
    # healthy timings directly under a stall verdict for a different one. Render nothing rather
    # than something true about the wrong mission.
    start = _last_mission_start_index(tl.events)
    if start is not None:
        init_at = next((i for i, e in enumerate(tl.events) if e.phase == "MissionInitialize"
                        and i >= start), None)
        if init_at is None:
            return None

    buckets = [{"name": name, "from": a, "to": b, "ms": _span_ms(seg, a, b), "what": what}
               for name, a, b, what in _BUCKETS]
    measured = [b for b in buckets if b["ms"] is not None and b["ms"] > 0]
    dominant = max(measured, key=lambda b: b["ms"])["name"] if measured else None

    polls = wait_ms = None
    # FinishMissionLoadingBegin is the completed-wait source and stays preferred. When the load
    # never got there — the CTD/hang case this whole section exists for — fall back to the LAST
    # WaitingForSceneLoad heartbeat, which carries the same two tokens from inside the wait. That
    # is what lets the polls=1 native-spin rule below fire on a log that never reached seq=8.
    begin = _first_event(seg, "FinishMissionLoadingBegin") or _last_event(seg, "WaitingForSceneLoad")
    if begin is not None:
        m = _POLLS_RE.search(begin.detail)
        polls = int(m.group(1)) if m else None
        m = _WAIT_MS_RE.search(begin.detail)
        wait_ms = int(m.group(1)) if m else None

    priv_mb = {}
    for phase in _MEMSTATS_MARKERS:
        e = _first_event(seg, phase)
        m = _PRIV_MB_RE.search(e.detail) if e is not None else None
        priv_mb[phase] = int(m.group(1)) if m else None

    # The render wait inside bucket4. first/last are the extremes of the shaders= series:
    # a falling count is a working load, a flat one is the wedge. Absent when the log
    # predates the marker or the native count could not be read (never zero-filled).
    render = [e for e in seg if e.phase == "WaitingForRender"]
    shader_counts = [int(m.group(1)) for m in
                     (_SHADERS_RE.search(e.detail) for e in render) if m]
    waited = [int(m.group(1)) for m in
              (_WAITED_MS_RE.search(e.detail) for e in render) if m]
    render_wait = {
        "samples": len(render),
        "shaders_first": shader_counts[0] if shader_counts else None,
        "shaders_last": shader_counts[-1] if shader_counts else None,
        "shaders_peak": max(shader_counts) if shader_counts else None,
        "draining": (len(shader_counts) > 1 and shader_counts[-1] < shader_counts[0]) or None,
        "waited_ms_last": waited[-1] if waited else None,
    } if render else None

    return {
        "buckets": buckets,
        "dominant": dominant,
        "render_wait": render_wait,
        "polls": polls,
        "wait_ms": wait_ms,
        # polls=0 means the MissionState.TickLoading binding FAILED — NOT "there was no
        # wait". Distinguishing those two is the whole reason the counter exists.
        "tick_binding_failed": polls == 0,
        # True when the pair came from a heartbeat rather than FinishMissionLoadingBegin, i.e.
        # the wait was still RUNNING when the log stopped. The numbers are then a lower bound
        # on the real wait, and reporting them as a completed duration would understate it.
        "wait_incomplete": begin is not None and begin.phase == "WaitingForSceneLoad",
        "priv_mb": priv_mb,
    }


def cross_check_rgl(verdict: Verdict, rgl) -> Verdict:
    """Upgrade an EQUIPMENT verdict to EQUIPMENT_CONFIRMED when the engine's own log
    names one of the suspect assets. Reuses validate_mesh_refs.RglFindings."""
    if rgl is None or verdict.kind != "EQUIPMENT":
        return verdict
    suspects = set(verdict.suspect_names)
    # Bodies (bo_*) match raw — they carry no lodN. Visual meshes / materials carry the
    # engine-appended `.lodN`, so strip it on BOTH sides (validate_mesh_refs._base_mesh_name,
    # the same canonical compare classify() uses) or an `Unable to find material for mesh
    # X.lod0` line would never match the base-named suspect mesh token.
    suspect_base = {vm._base_mesh_name(s): s for s in suspects}
    confirmed = set(suspects & rgl.missing_bodies)
    for mat in rgl.missing_materials:
        base = vm._base_mesh_name(mat)
        if base in suspect_base:
            confirmed.add(suspect_base[base])
    confirmed = sorted(confirmed)
    if confirmed:
        verdict.kind = "EQUIPMENT_CONFIRMED"
        verdict.confirmed_assets = confirmed
        verdict.summary = (
            "CONFIRMED equipment hang — the engine logged it could not load "
            + ", ".join(confirmed) + ". " + verdict.summary)
    else:
        # Surface engine-logged misses (bodies AND materials) not in the snapshot.
        extra = sorted((rgl.missing_bodies | rgl.missing_materials) - suspects)
        if extra:
            verdict.notes.append(
                "Engine logged missing assets NOT captured in the agent snapshot "
                "(holster/region variants, materials, or a different agent): "
                + ", ".join(extra))
    return verdict


def triage(log_text: str, rgl_text: str | None = None) -> Verdict:
    """Convenience: parse -> classify -> (optional) rgl cross-check."""
    tl = parse_battle_load_log(log_text)
    v = classify(tl)
    if rgl_text is not None:
        v = cross_check_rgl(v, vm.parse_rgl_text(rgl_text))
    return v


# --------------------------------------------------------------------------- #
# Reporting                                                                    #
# --------------------------------------------------------------------------- #
_NEXT_STEPS = {
    "EQUIPMENT": [
        "Run: python tools/validate_mesh_refs.py --rgl-log <player rgl_log_errors_*.txt>",
        "Cross-reference the suspect item ids against troop rosters: "
        "python tools/validate_all_troop_refs.py",
        "If the asset is missing only on SOME installs, the player's LOTRLOME_Armory "
        "is out of date / incomplete — confirm their Armory version.",
    ],
    "EQUIPMENT_CONFIRMED": [
        "The engine named the missing asset (above). Verify it exists in shipped "
        "LOTRLOME_Armory: python tools/validate_mesh_refs.py --scan-bodies",
        "If present in shipped Armory, this is a PLAYER-INSTALL problem (stale/partial "
        "Armory). If absent, fix the item def / re-export the mesh.",
    ],
    "POST_EQUIP": [
        "Equipment is not the cause. Check the stuck agent's race meshes (skin/skeleton) "
        "and any scene/script that runs at first tick.",
        "Compare a working player's log: did theirs reach BattlePlayable on the same scene?",
    ],
    "RENDER_WAIT": [
        "Read the shaders= series in the WaitingForRender lines FIRST: falling means the "
        "load was working and the player simply waited less than it needed.",
        "Confirm against the player's rgl_log for the same window: count compile_shader "
        "lines and 'Missing shader from sack' misses.",
        "A cold shader cache is the cause, not the scene. It is refilled from scratch "
        "whenever the module list changes (v1.4.8 deletes the local cache) — see "
        "docs/features/shader-precompilation.md.",
    ],
    "SCENE": [
        "Audit the scene ref: python tools/audit_battle_scenes.py (map_index coverage) "
        "and tools/audit_scene_names.py.",
        "A scene renamed/removed between engine versions leaves a stale TAOM ref — see "
        "docs/reference/scene-reference-audit.md.",
    ],
    "PRE_SCENE": [
        "Froze before scene selection — inspect encounter/mission-open code paths and "
        "any TAOM Harmony patch on PlayerEncounter.Start / MissionState.OpenNew.",
    ],
    "COMPLETED": [
        "The battle-load path is clean. If the player still reports a hang, it is "
        "elsewhere (in-mission, save load, map). Collect a fresh log of that moment.",
    ],
    "UNKNOWN": [
        "Have the player confirm 'Enable Battle Load Diagnostics' is on in MCM, "
        "reproduce, and send the newest Logs/taom_debug_*.log.",
    ],
}


def _format_timings(t: dict) -> list:
    out = ["",
           "Load timing (GAPS between markers — t=+ is absolute and a chained mission "
           "inherits the clock):"]
    # bucket2's own span needs BOTH its markers, and FinishMissionLoadingBegin is exactly the
    # one a stalled load never writes — so on the logs this section matters most for, bucket2
    # printed "?" while a heartbeat one function away already held a lower bound for it.
    incomplete_wait = t["wait_ms"] if t.get("wait_incomplete") else None
    for b in t["buckets"]:
        ms = f"{b['ms']}ms" if b["ms"] is not None else "?"
        if b["ms"] is None and b["name"] == "bucket2" and incomplete_wait is not None:
            ms = f">={incomplete_wait}ms"
        out.append(f"  {b['name']:<8} {b['from']:<25} -> {b['to']:<25} "
                   f"{ms:>9}  {b['what']}")
    if t["dominant"] and incomplete_wait is None:
        dom = next(b for b in t["buckets"] if b["name"] == t["dominant"])
        out.append(f"  dominant: {dom['name']} ({dom['ms']}ms) — {dom['what']}")
    elif incomplete_wait is not None:
        # Naming a "dominant" bucket from the CLOSED buckets alone is actively misleading here:
        # the open one is unbounded and is the whole reason this log exists.
        out.append(f"  dominant: bucket2 (>={incomplete_wait}ms, still running when the log "
                   "stopped) — the closed buckets above cannot outrank an unbounded one.")
    if t["polls"] is not None:
        wait = (f"waitMs={t['wait_ms']}" if t["wait_ms"] is not None
                else "waitMs=<not observed>")
        out.append(f"  TickLoading: polls={t['polls']} {wait}")
        if t.get("wait_incomplete"):
            out.append("  These came from the LAST WaitingForSceneLoad heartbeat, not from a "
                       "completed wait: the load was still streaming when the log stopped, so "
                       "both numbers are a LOWER BOUND on the real wait.")
        if t["tick_binding_failed"]:
            out.append("  polls=0 — the MissionState.TickLoading binding FAILED. This is "
                       "NOT 'there was no wait': check the top of the log for "
                       "'Patch43 diagnostics failed to apply'.")
        elif t["polls"] == 1 and (t["wait_ms"] or 0) > 1000:
            # The polls=1 reading INVERTS between the two sources, so this note must know
            # which one it got. FinishMissionLoadingBegin is written after the wait ended, so
            # polls=1 there means the thread was blocked INSIDE frame 1. A heartbeat is
            # emitted FROM INSIDE a TickLoading frame, so polls=1 there means frame 1 had not
            # even arrived yet when the threshold elapsed: the block was BEFORE the frame.
            # Printing the first reading for the second case sends the reader to #352 for a
            # stall that is nowhere near WaitForMeshesToBeLoaded.
            if t.get("wait_incomplete"):
                out.append("  polls=1 on a heartbeat — the FIRST TickLoading frame had not "
                           "arrived yet when the threshold elapsed, so the block is BEFORE "
                           "the loop, not inside it. This is NOT the #352 shape: look at "
                           "what runs between Mission.Initialize returning and the first "
                           "TickLoading frame.")
            else:
                out.append("  polls=1 with a large wait — the main thread BLOCKED inside one "
                           "frame (a native spin, the #352 WaitForMeshesToBeLoaded shape), "
                           "not async streaming.")
        if t["wait_ms"] is None:
            out.append("  waitMs was omitted by the game (MissionInitializeDone not "
                       "observed) — it is unmeasured, not zero.")
    r = t.get("render_wait")
    if r:
        waited = (f"{r['waited_ms_last']}ms" if r["waited_ms_last"] is not None
                  else "<not observed>")
        out.append(f"  RenderWait: {r['samples']} sample(s) over {waited} at the "
                   "SceneView.ReadyToRender gate")
        if r["shaders_peak"] is None:
            out.append("  shaders= was omitted by the game (the native count could not be "
                       "read) — it is unmeasured, not zero.")
        elif r["draining"]:
            out.append(f"  shaders {r['shaders_first']} -> {r['shaders_last']} (peak "
                       f"{r['shaders_peak']}) — the compile queue is DRAINING, so this is "
                       "a cold shader cache and a slow load, not a hang.")
        else:
            out.append(f"  shaders {r['shaders_first']} -> {r['shaders_last']} (peak "
                       f"{r['shaders_peak']}) — the count is not falling. A frozen "
                       "non-zero count is a wedge; a flat 0 means the gate is held by "
                       "something other than shader compilation.")
    out.append("  privMB at MemStats markers: "
               + " ".join(f"{k}={'?' if v is None else v}"
                          for k, v in t["priv_mb"].items()))
    return out


def format_report(verdict: Verdict, tl: Timeline, rgl, mem: dict | None = None,
                  timings: dict | None = None, stations: dict | None = None) -> str:
    out = []
    out.append("=== BATTLE LOAD TRIAGE ===")
    out.append("")
    out.append(f"VERDICT: {verdict.kind} — {verdict.summary}")

    if verdict.confirmed_assets:
        out.append("")
        out.append("[CONFIRMED by engine log] could not load: "
                   + ", ".join(verdict.confirmed_assets))

    # Lifecycle tail (last few phases + the stuck agent's slots)
    out.append("")
    out.append("Lifecycle (last phases seen):")
    tail = tl.events[-6:]
    for e in tail:
        marker = "   <-- FROZE HERE" if e is tl.events[-1] and verdict.kind in HANG_KINDS else ""
        seq = f"seq={e.seq}" if e.seq is not None else "seq=?"
        ms = f"t=+{e.ms}ms" if e.ms is not None else ""
        out.append(f"  {seq:<8} {ms:<10} {e.phase:<20} {e.detail}{marker}")
        for s in e.slots:
            out.append(f"       slot={s.slot} id={s.item_id} bo={s.bo} "
                       f"shieldBo={s.shield_bo} holsterBo={s.holster_bo} "
                       f"mesh={s.mesh} kind={s.kind}")
    if not tl.events:
        out.append("  (none)")

    if timings is None:
        timings = classify_phase_timings(tl)
    if timings is not None:
        out.extend(_format_timings(timings))

    if tl.watchdog:
        out.append("")
        out.append(f"Stall watchdog fired after {tl.watchdog.elapsed_seconds}s — "
                   f"last phase={tl.watchdog.last_phase} {tl.watchdog.last_detail}".rstrip())
        if tl.watchdog.tokens:
            out.append(f"  reading at fire: {tl.watchdog.tokens}")
            if "churn-capped" in tl.watchdog.tokens:
                out.append("  churn-capped: the compile queue was STILL MOVING when the "
                           "watchdog fired, and was capped for never draining. Treat as a "
                           "cold shader cache on slow hardware, not a frozen compiler.")

    if tl.mem_samples:
        if mem is None:  # callers that computed with a custom floor pass their own
            mem = classify_memory(tl)
        warn_count = sum(1 for s in tl.mem_samples if s.warned)
        first, last = mem["first"], mem["last"]
        out.append("")
        out.append(f"Memory trend ({len(tl.mem_samples)} samples):")
        if tl.mem_session:
            out.append(f"  session: totalPhysMB={tl.mem_session['total_phys_mb']} "
                       f"sysCommitLimitMB={tl.mem_session['commit_limit_mb']}")
        out.append(f"  first: commitUsed={first['commit_used_mb']}/"
                   f"{first['commit_limit_mb']}MB headroom={first['headroom_mb']}MB "
                   f"load={first['mem_load']}%")
        out.append(f"  peak:  commitUsed={mem['peak_commit_used_mb']}MB")
        out.append(f"  last:  commitUsed={last['commit_used_mb']}/"
                   f"{last['commit_limit_mb']}MB headroom={last['headroom_mb']}MB "
                   f"load={last['mem_load']}%")
        out.append(f"  WARN LOW COMMIT HEADROOM lines: {warn_count}")
        if mem["pressure"]:
            out.append("  MEMORY PRESSURE: commit headroom was critically low — "
                       "the phase verdict above may be a symptom, not the cause.")

    if stations is None:
        stations = classify_stations(tl)
    if stations:
        out.append("")
        out.append(f"Memory by station ({stations['transitions']} transitions):")
        shown = stations["by_screen"][:8]
        if not shown:
            # Reachable: a log whose every station is an exit with no matching enter (the sampler
            # subscribed while a screen was already open) yields transitions > 0 and an empty
            # by_screen. max() over that raised ValueError and took the whole report with it: no
            # verdict, no --json, just a traceback exiting 1, which is the same exit code as a
            # diagnosed hang.
            out.append("  (no completed visits - every station was an exit with no matching enter)")
        else:
            width = max(len(b["screen"]) for b in shown)
            for b in shown:
                worst = b["max_visit_delta_mb"]
                worst_txt = f"{worst:>+6} MB" if worst is not None else "     n/a"
                line = (f"  {b['screen']:<{width}}  {b['visits']:>3} visits  "
                        f"{b['total_delta_mb']:>+7} MB total  "
                        f"(max visit {worst_txt})")
                if b["unmatched_enters"]:
                    line += f"  [{b['unmatched_enters']} unmatched enter(s)]"
                if b["unmatched_exits"]:
                    line += f"  [{b['unmatched_exits']} unmatched exit(s)]"
                if b["nested_visits"]:
                    line += f"  [{b['nested_visits']} visit(s) overlapped another screen]"
                out.append(line)
            # No silent truncation: a capped list that reads as the whole list is how a
            # report starts lying about coverage.
            hidden = len(stations["by_screen"]) - len(shown)
            if hidden > 0:
                out.append(f"  ... {hidden} more screen(s) not shown (see --json)")
            if stations.get("nested_overlap"):
                out.append("  NOTE: screens overlapped. An outer screen's delta includes whatever "
                           "an inner one grew while stacked on it, so do NOT sum these.")
            if stations.get("session_resets"):
                out.append(f"  NOTE: {stations['session_resets']} session reset(s) (return to main "
                           "menu). Each starts a fresh emit budget; deltas do not span a reset.")
            # The window is bounded by where the engine raises its events, and that bound decides
            # what any of these numbers can mean.
            out.append("  NOTE: a delta covers the time a screen was OPEN. The engine raises its "
                       "push event AFTER HandleInitialize and its pop event AFTER HandleFinalize, "
                       "so construction cost is NOT included; a screen that allocates on open and "
                       "keeps it reads ~0, and one that frees on close reads negative.")

    if verdict.suspect_names:
        out.append("")
        out.append("Suspect assets (declared on the stuck agent): "
                   + ", ".join(verdict.suspect_names))

    if verdict.scene:
        out.append("")
        out.append(f"Scene that never advanced: {verdict.scene}")

    if verdict.notes:
        out.append("")
        out.append("Notes:")
        for n in verdict.notes:
            out.append(f"  - {n}")

    out.append("")
    out.append("Next steps:")
    for s in _NEXT_STEPS.get(verdict.kind, []):
        out.append(f"  - {s}")

    return "\n".join(out)


# --------------------------------------------------------------------------- #
# Bundle handling                                                              #
# --------------------------------------------------------------------------- #
def _read_from_bundle(zip_path: Path):
    """Extract (log_text, rgl_text) from a crash-bundle .zip. Looks for a member
    named like taom_debug*.log and rgl_log*. Either may be absent (returns None)."""
    log_text = None
    rgl_text = None
    with zipfile.ZipFile(zip_path) as z:
        names = z.namelist()
        log_member = _pick_log(names)
        # Prefer the denser rgl_log_errors_*.txt over a plain rgl_log_*.txt.
        rgl_member = _pick(names, ("rgl_log_errors",)) or _pick(names, ("rgl_log",))
        if log_member:
            log_text = z.read(log_member).decode("utf-8", errors="ignore")
        if rgl_member:
            rgl_text = z.read(rgl_member).decode("utf-8", errors="ignore")
    return log_text, rgl_text


def _pick_log(names):
    # Anchor on the BASENAME so a decoy like `not_taom_debug.log` can't match — a
    # substring test ("taom_debug" in name) would. Crash bundles only carry the real
    # log, but the wrong pick would yield a wrong verdict, so be strict here.
    for n in names:
        base = os.path.basename(n).lower()
        if base.startswith("taom_debug") and base.endswith(".log"):
            return n
    return None


def _pick(names, needles):
    # Match on the BASENAME (not the full path) so a directory component like
    # `rgl_log_backup/unrelated.txt` can't satisfy an ('rgl_log',) needle — mirrors
    # _pick_log's decoy hardening.
    for n in names:
        base = os.path.basename(n).lower()
        if all(part.lower() in base for part in needles):
            return n
    return None


# --------------------------------------------------------------------------- #
# CLI                                                                          #
# --------------------------------------------------------------------------- #
def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("log", nargs="?", help="Path to the player's taom_debug_*.log")
    ap.add_argument("--bundle", help="A crash-bundle .zip containing the logs "
                                     "(instead of a loose taom_debug.log)")
    ap.add_argument("--rgl-log", help="Path to the player's newest "
                                      "rgl_log_errors_*.txt for the engine cross-check")
    ap.add_argument("--json", dest="json_out", help="Write the verdict to this JSON file")
    ap.add_argument("--mem-threshold-mb", type=int, default=None,
                    help="Override the low-commit-headroom floor for the memory-"
                         f"pressure call (default {MEM_WARN_HEADROOM_FLOOR_MB} MB, "
                         "mirroring MemoryPressureSampler.WarnHeadroomFloorMb)")
    args = ap.parse_args()

    log_text = None
    rgl_text = None

    if args.bundle:
        bp = Path(args.bundle)
        if not bp.exists():
            print(f"ERROR: bundle not found: {bp}", file=sys.stderr)
            return 2
        try:
            log_text, rgl_text = _read_from_bundle(bp)
        except (zipfile.BadZipFile, OSError) as e:
            print(f"ERROR: could not read bundle {bp}: {e}", file=sys.stderr)
            return 2
        if log_text is None:
            print(f"ERROR: no taom_debug*.log inside {bp}", file=sys.stderr)
            return 2
    else:
        if not args.log:
            print("ERROR: provide a taom_debug.log path or --bundle <zip>", file=sys.stderr)
            return 2
        lp = Path(args.log)
        if not lp.exists():
            print(f"ERROR: log not found: {lp}", file=sys.stderr)
            return 2
        log_text = lp.read_text(encoding="utf-8", errors="ignore")

    # --rgl-log overrides any rgl pulled from a bundle.
    if args.rgl_log:
        rp = Path(args.rgl_log)
        if not rp.exists():
            print(f"WARNING: --rgl-log not found: {rp} — engine cross-check skipped",
                  file=sys.stderr)
        else:
            rgl_text = rp.read_text(encoding="utf-8", errors="ignore")

    tl = parse_battle_load_log(log_text)
    verdict = classify(tl)
    rgl = vm.parse_rgl_text(rgl_text) if rgl_text else None
    if rgl is not None:
        verdict = cross_check_rgl(verdict, rgl)
    floor_mb = (args.mem_threshold_mb if args.mem_threshold_mb is not None
                else MEM_WARN_HEADROOM_FLOOR_MB)
    mem = classify_memory(tl, floor_mb=floor_mb)
    timings = classify_phase_timings(tl)
    stations = classify_stations(tl)

    print(format_report(verdict, tl, rgl, mem, timings, stations))

    if args.json_out:
        payload = {
            "kind": verdict.kind,
            "summary": verdict.summary,
            "stuck_agent": verdict.stuck_agent,
            "suspect_names": verdict.suspect_names,
            "scene": verdict.scene,
            "confirmed_assets": verdict.confirmed_assets,
            "notes": verdict.notes,
            "terminal_phase": tl.events[-1].phase if tl.events else None,
            "watchdog_fired": tl.watchdog is not None,
            "memory": mem,
            "stations": stations,
            "timings": timings,
        }
        Path(args.json_out).write_text(json.dumps(payload, indent=2), encoding="utf-8")
        print(f"\nWrote verdict to {args.json_out}", file=sys.stderr)

    return 1 if verdict.kind in HANG_KINDS else 0


if __name__ == "__main__":
    sys.exit(main())
