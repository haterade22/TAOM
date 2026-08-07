# Handoff session prompts — performance & optimization (2026-08-07)

Derived from a 32-minute live gameplay log: `taom_debug_2026-08-07_12-50-34.log`
(6001 lines, 1.24 MB, **zero ERROR lines**), captured while play-testing the Enlistment feature.

Each prompt below is **self-contained** — paste it into a fresh session. Evidence is quoted with
real numbers so the next session does not have to re-derive it. Ordered by value.

---

## 1. CRITICAL — Native memory growth reaching the known CTD threshold

```
TAOM performance investigation: native memory growth during play, reaching the #385 CTD threshold.

EVIDENCE (measured, one 27.5-minute session, log
"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-08-07_12-50-34.log"):

  time      privMB   wsMB  heapMB  commitUsed  availPhys  load
  12:54:23   12070   5812     166       78978      25445   59%
  13:03:23   16540   5867     468       82388      25866   59%   <- first battle
  13:12:24   19825   7121     639       85862      23877   62%
  13:21:54   20406   9259     625       86348      21836   65%

  Private bytes : 12,070 -> 20,406 MB  (+8,336 MB = 303 MB/min)
  Managed heap  :    166 ->    625 MB  (+459 MB)
  => NATIVE growth accounts for 7,877 MB — 94% of the total
  System commit : 78,978 -> 86,348 MB of 128,662 (67% of limit)
  Machine total physical: 63,126 MB

WHY THIS MATTERS: TAOM's own memory record attributes the #385 CTD to a facegen morph null at
20.3 GB commit. This session ended at 20.4 GB — at that threshold.

IMPORTANT NUANCES, do not overstate the finding:
 - Growth is NOT unbounded. The last four samples are 20553 / 20712 / 20206 / 20406 — a plateau.
 - Growth correlates with battle loads (12.1 -> 16.5 GB on the first battle at 13:03).
 - So the shape is "each battle allocates native memory that is not fully released, settling at a
   high plateau", NOT a monotonic leak. Confirm or refute that shape before designing a fix.

YOUR TASK:
 1. Read docs/features/battle-load-diagnostics.md and
    docs/reviews/rca-memsample-telemetry-2026-08-05.md and
    docs/reviews/rca-battleload-agentbuild-2026-08-03.md first. Do not re-derive what they record.
 2. The sampler is Main/Features/BattleLoadDiagnostics/MemoryPressureSampler.cs; the
    battle-load instrument is BattleLoadDiagnosticsService.cs. Analysis tool: tools/triage_battle_load.py.
 3. Establish WHERE the native memory goes. Managed heap is nearly flat, so this is native:
    textures, meshes, facegen morphs, scene resources, or an engine cache TAOM inflates.
    Candidates worth testing explicitly: per-battle scene/resource retention, facegen/tableau
    morph allocation (the #385 lead), and TAOM's own vendored native DLLs.
 4. Decide whether TAOM CAUSES this or merely EXPOSES it. Vanilla Bannerlord at this scene scale
    may do the same. If you cannot separate the two, say so — a vanilla A/B (same save, TAOM
    disabled) is the honest experiment and should be proposed to the user rather than guessed at.
 5. Issue #385 is OPEN. Attach findings there; do not open a duplicate.

CONSTRAINTS:
 - TAOM conventions apply: adapters (ADR-007), thin entry points (ADR-002), TDD, /deep-review
   before any C# commit. Never guess TaleWorlds behaviour — verify with
   `pwsh tools/taom-src.ps1 path <Type>` against the INSTALLED 1.4.7 DLLs.
 - DO NOT attribute this to TAOM's campaign behaviours without evidence. Measured cost of TAOM
   behaviour registration in a battle load is 23 ms (see prompt 2).
 - Report, don't fix, anything outside the repo (installs, drivers, page file).

SUCCESS: a named allocation source with evidence, or a definitive "this is vanilla-scale
behaviour, not TAOM" with the A/B that proves it. Either outcome closes real uncertainty on #385.
```

---

## 2. HIGH — Battle loads take 29–63 seconds; find the 11.9-second engine hole

```
TAOM performance investigation: battle load time of 29-63 seconds to playable.

EVIDENCE (same log as above). Phase timeline of one 29-second load:

  14,969 ms  MissionInitialize
  26,889 ms  MissionAfterStartBegin      <- 11.9-SECOND GAP with no instrumentation
  26,911 ms  TaomBehaviorsBegin
  26,912 ms  TaomBehaviorsDone           <- ALL TAOM behaviour registration: 23 ms
  26,981 ms  MissionAfterStartDone
  29,109 ms  BattlePlayable

A second load in the same session reached BattlePlayable at 62,974 ms.

THE KEY FACT: TAOM's own behaviour registration costs 23 milliseconds. The 11.9-second hole sits
entirely between the engine's MissionInitialize and MissionAfterStartBegin. Do NOT start from the
assumption that TAOM's behaviours are slow — they are measured and they are not.

YOUR TASK:
 1. Read docs/features/battle-load-diagnostics.md and
    docs/reviews/rca-battleload-agentbuild-2026-08-03.md. The instrument already exists
    (Main/Features/BattleLoadDiagnostics/) — extend it rather than building a new one.
 2. Instrument INSIDE the 11.9-second gap. Decompile the engine path between MissionInitialize and
    MissionAfterStart (`pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission`,
    MissionState, and the scene/resource loaders) and add phase markers at the real boundaries:
    scene load, terrain, atmosphere, agent spawn, resource residency.
 3. Determine how much of the gap is TAOM-INFLUENCED even if not TAOM code — e.g. scene size,
    mesh/texture counts from TAOM cultures, number of spawned agents, LOD settings. TAOM data can
    make an engine phase slow without any TAOM code being slow.
 4. Cross-reference with prompt 1: the memory jumps coincide with battle loads. These may be one
    problem (resource residency) rather than two. Check before treating them separately.
 5. Note the battles measured were small (mainPartySize=1, caravan/looter fights). A 29-63 second
    load for a TINY battle is disproportionate and is itself a strong clue — chase that.

CONSTRAINTS: as prompt 1. Any new logging must be sample-gated or DEBUG-level; see prompt 3 for
why (a diagnostic already flooded 81% of a log this session).

SUCCESS: the 11.9-second gap attributed to named phases with measurements, and a ranked list of
what TAOM can actually change.
```

---

## 3. MEDIUM — Diagnostic logging cost and signal-to-noise

```
TAOM optimization: diagnostic logging volume is drowning real signal and costing I/O.

EVIDENCE (log taom_debug_2026-08-07_12-50-34.log, 6001 lines / 1.24 MB over 32 minutes):
 - [EnlistDiag] alone: 4,856 of 6,001 lines = 81% of the entire log.
 - 291 of the 299 WARNING lines are one message:
     "[EnlistDiag] SYNC closed a drift of N to 'X' — the player had fallen behind"
   The threshold is `drift > 1f` and normal inter-tick drift is ~1.8, so essentially EVERY sync
   logs a warning. The threshold is simply wrong.
 - Subsystem volumes: EnlistDiag 4856, TournamentDiag 295, MissionDiag 237, BattleLoad 147,
   MemSample 57, CultureMarketplace 52, TableauDiag 29, SpecRes 21.

YOUR TASK:
 1. Read Main/Core/Logging/FileLogger.cs BEFORE proposing any level change. It drains INFO
    synchronously with a flush on the calling thread and leaves DEBUG on an async queue —
    DELIBERATELY, so a native CTD preserves the tail. Downgrading a crash-localisation stamp to
    DEBUG destroys its purpose while looking like an optimisation. There is a measured precedent:
    1287 durable stamps cost 145 ms, ~0.5% of load
    (docs/reviews/rca-battleload-agentbuild-2026-08-03.md).
 2. Fix the drift warning in Main/Adapters/MobilePartyAttachmentAdapter.cs: raise the threshold to
    something meaningful (normal drift is ~1.8; a real "left behind" event is an order of magnitude
    larger), and drop the per-tick "SYNC ok" line to DEBUG.
 3. Audit the per-tick [EnlistDiag] TICK line in Main/Features/Enlistment/EnlistmentReconciler.cs.
    It is genuinely valuable while diagnosing but should not run at INFO forever. Consider gating
    all [EnlistDiag] output behind an MCM/JSON diagnostic toggle, defaulting OFF, the way
    BattleLoadDiagnostics already does — reuse that pattern rather than inventing one.
 4. Quantify before and after: lines/minute and bytes/minute per subsystem.
 5. Check whether string interpolation is being evaluated for suppressed levels anywhere. NOTE:
    `_logger?.LogInfo($"...")` does NOT evaluate its argument when _logger is null — the
    null-conditional operator short-circuits the whole invocation. Do not "fix" that; it is not a
    bug. The real cost is when the logger is non-null and the level is disabled downstream.

SUCCESS: a log where a 30-minute session is readable end-to-end, real warnings are rare enough to
mean something, and no crash-localisation guarantee was traded away for it.
```

---

## 4. MEDIUM — Sauron has no facegen action set (content, not performance)

```
TAOM content bug: Sauron's facegen falls back to a human warrior action set.

EVIDENCE (log taom_debug_2026-08-07_12-50-34.log, [TableauDiag] Spawner lines). Every race
resolves to its own facegen action set EXCEPT sauron:

  monster='elf'   suffix='_facegen' -> valid name='as_elf_facegen'
  monster='orc'   suffix='_facegen' -> valid name='as_orc_facegen'
  monster='uruk'  suffix='_facegen' -> valid name='as_uruk_facegen'
  monster='uruk'  suffix='_facegen' -> valid name='as_uruk_female_facegen'
  monster='sauron' suffix='_facegen' -> valid name='as_human_warrior'   <-- FALLBACK

So Sauron's portrait / encyclopedia / character tableau uses a human warrior action set and pose
rather than his own. Visible as a wrong pose, and it silently masks a missing action set.

YOUR TASK:
 1. Find the fallback logic that produced 'as_human_warrior' (grep TableauDiag / the action-set
    name resolver) and confirm whether the fallback is intended or an accident.
 2. Determine whether an `as_sauron_facegen` action set SHOULD exist. Check the Armory dependency
    (LOTRLOME_Armory) action_sets and TAOM's own — note the trap in CLAUDE.md: a root-level
    <action> parented by <action_sets> loads on the client but KILLS a dedicated server on boot.
    Gate any authoring with `python tools/audit_action_set_parity.py`.
 3. If Sauron legitimately has no facegen pose, make the fallback deliberate and documented rather
    than silent. If he should have one, author it.

CONSTRAINTS: `python tools/validate_moduledata.py` must pass. Any new action set needs a full game
restart to test, not a save-load.

SUCCESS: Sauron either has his own facegen action set, or the fallback is an explicit, documented
decision with a test pinning it.
```

---

## Already fixed this session (do not re-investigate)

- **Hunt-duty targets could not spawn in the field.** `FieldDutyRuntime` anchored the spawn on
  `CommanderSnapshot.SettlementId`, which is empty whenever the column is marching, so every
  `recon_sweep` failed (`SpawnLooterParty: settlement=''`). Now falls back to
  `FindNearestFriendlySettlement`.
- **Enlistment conversation's PlayerEncounter was never closed** — `playerEncounter=True` on 93 of
  93 ticks, blocking every main-party encounter for the whole term.
- **enlistment_config.json silently reverted to defaults** — Newtonsoft
  `ObjectCreationHandling.Auto` appends to initialized lists; fixed with `Replace`.
- Battle join, siege assault join, wait-menu re-assert after battle.

## Confirmed healthy in this log — no action needed

Tournament rosters all "all safe" across 8 towns (Patch69 guard working); Diplomacy corrected real
vanilla state corruption (`empire_s <-> aserai` simultaneously allied and at war); CastleRecruitment
transpilers landed; creature behaviour trees initialise cleanly; CultureMarketplace loaded
(empire 443 items, aserai 272, battania 177, dolguldur 161); SpecRes earning per battle.
