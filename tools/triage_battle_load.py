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
  POST_EQUIP          ends at AgentEquipOk — the agent equipped fine but the battle
                      never became playable; NOT an equipment-resolution hang (look
                      at non-equip spawn steps / scene / script init).
  SCENE               ends at MissionInitialize / BattleSceneSelected — froze during
                      scene load before any agent equipped; a code/scene issue.
  PRE_SCENE           ends at EncounterStart / MissionOpenNew — froze in mission setup.
  COMPLETED           a BattlePlayable phase exists — the load finished; the hang (if
                      any) is not in the battle-load path.
  UNKNOWN             no `[BattleLoad]` lines — diagnostics were off or wrong file.

The rgl cross-check REUSES `validate_mesh_refs.parse_rgl_text` (the same engine the
mesh-ref validator uses) rather than re-implementing it. For offline "missing from
shipped Armory vs present (player-install problem)" confirmation, run
`tools/validate_mesh_refs.py` on the suspect item ids.

Usage:
  python tools/triage_battle_load.py <taom_debug.log>
  python tools/triage_battle_load.py <taom_debug.log> --rgl-log <rgl_log_errors_*.txt>
  python tools/triage_battle_load.py --bundle <taom_crash_*.zip>
  python tools/triage_battle_load.py <taom_debug.log> --json verdict.json

Exit code: 1 if a hang was diagnosed (EQUIPMENT*/SCENE/PRE_SCENE/POST_EQUIP),
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
# [BattleLoad] WATCHDOG STILL LOADING after Ns — last <statusline>   (em-dash or hyphen)
_WATCHDOG_RE = re.compile(
    r"\[BattleLoad\]\s+WATCHDOG STILL LOADING after (\d+)s\s+[—-]\s+last\s+(.*)$")
# CurrentStatusLine: phase=PHASE seq=N <detail>. phase token is (\S+) so the initial
# sentinel `phase=<none>` still yields a last_phase; seq is optional for the same reason.
_STATUS_RE = re.compile(r"phase=(\S+)(?:\s+seq=(\d+))?\s*(.*)$")

# Per-phase detail parsers
_EQUIP_BEGIN_RE = re.compile(
    r"agent#(\d+)\s+'(.*?)'\s+char='(.*?)'\s+culture='(.*?)'\s+slots=(\d+)")
# Greedy + end-anchored so an apostrophe in the name (e.g. 'Sauron's Lieutenant') is kept,
# not truncated at the first inner quote (AgentEquipOk has no trailing token to anchor on).
_EQUIP_OK_RE = re.compile(r"agent#(\d+)\s+'(.*)'\s*$")
_SCENE_ID_RE = re.compile(r"sceneId='(.*?)'")
_SCENE_RE = re.compile(r"scene='(.*?)'")

NULL = "<null>"
HANG_KINDS = {"EQUIPMENT", "EQUIPMENT_CONFIRMED", "SCENE", "PRE_SCENE", "POST_EQUIP"}


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


@dataclass
class Timeline:
    events: list = field(default_factory=list)
    watchdog: Watchdog | None = None


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
    dumps attach to the most recent AgentEquipBegin event."""
    tl = Timeline()
    for raw in text.splitlines():
        line = _PREFIX_RE.sub("", raw)
        if _TAG not in line:
            continue

        m = _WATCHDOG_RE.search(line)
        if m:
            status = m.group(2)
            sm = _STATUS_RE.search(status)
            tl.watchdog = Watchdog(
                elapsed_seconds=int(m.group(1)),
                last_phase=sm.group(1) if sm else "",
                last_detail=(sm.group(3).strip() if sm else status.strip()))
            continue

        m = _SLOT_RE.search(line)
        if m:
            slot = Slot(*m.groups())
            if tl.events and tl.events[-1].phase == "AgentEquipBegin":
                tl.events[-1].slots.append(slot)
            continue

        m = _PHASE_RE.search(line)
        if m:
            tl.events.append(PhaseEvent(
                seq=int(m.group(1)), ms=int(m.group(2)),
                phase=m.group(3), detail=m.group(4).strip()))
    return tl


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

    if any(e.phase == "BattlePlayable" for e in tl.events):
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

    if ph in ("MissionInitialize", "BattleSceneSelected"):
        scene = _parse_scene(terminal)
        return Verdict(
            "SCENE",
            f"Froze during scene load (phase={ph}, scene='{scene}') before any agent "
            "equipped — a code/scene issue, not equipment. Audit the scene ref with "
            "tools/audit_battle_scenes.py / tools/audit_scene_names.py.",
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


def format_report(verdict: Verdict, tl: Timeline, rgl) -> str:
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

    if tl.watchdog:
        out.append("")
        out.append(f"Stall watchdog fired after {tl.watchdog.elapsed_seconds}s — "
                   f"last phase={tl.watchdog.last_phase} {tl.watchdog.last_detail}".rstrip())

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

    print(format_report(verdict, tl, rgl))

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
        }
        Path(args.json_out).write_text(json.dumps(payload, indent=2), encoding="utf-8")
        print(f"\nWrote verdict to {args.json_out}", file=sys.stderr)

    return 1 if verdict.kind in HANG_KINDS else 0


if __name__ == "__main__":
    sys.exit(main())
