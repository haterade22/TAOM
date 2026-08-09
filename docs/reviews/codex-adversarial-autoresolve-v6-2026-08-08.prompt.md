# Adversarial review — AutoResolveDiagnostics schema v6 + MCM gating

You are an independent adversarial reviewer. Your job is to find bugs that a friendly review
would miss. Assume the author is competent and that the obvious mistakes are already gone.
Reward yourself for finding something real; do not pad the report with style opinions.

Work in the repo at `E:\repos\TAOM` (Bannerlord 1.4.7 total-conversion mod, .NET Framework 4.7.2,
C# with nullable reference types, MSTest + NSubstitute).

## What the feature is

A campaign-map diagnostic that logs every auto-resolved battle to a JSON-Lines stream so the
outcomes can be analysed offline by `tools/analyze_battle_logs.py`. It exists because the player
reports that the forces of Good steamroll the forces of Evil in auto-resolve, and the first three
attempts at measuring that produced *wrong data* — see the history below, which is the single most
important context for this review.

## Files in scope

```
Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehavior.cs        (237 lines)
Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsIoC.cs
Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsSettingsProvider.cs
Main/Features/AutoResolveDiagnostics/IAutoResolveDiagnosticsSettingsProvider.cs
Main/Features/AutoResolveDiagnostics/AutoResolveLogFormatter.cs
Main/Features/AutoResolveDiagnostics/Domain/BattleLogRecord.cs
Main/Features/AutoResolveDiagnostics/Domain/BattleStartSnapshot.cs
Main/Features/AutoResolveDiagnostics/Domain/TroopCensusRecord.cs
Main/Adapters/IMapEventBattleLogAdapter.cs
Main/Adapters/MapEventBattleLogAdapter.cs                                    (405 lines)
Main/Adapters/ITroopCensusAdapter.cs
Main/Adapters/TroopCensusAdapter.cs
Main/Features/TaomSettings.cs   — ONLY the "Battle Tactics/Auto-Resolve Diagnostics" group
Main/Features/CoopInterop/CoopSettingsRelevance.cs
Main/IoC.cs                     — ONLY the AutoResolveDiagnostics registration
Main/SubModule.cs               — ONLY the AutoResolveDiagnosticsBehavior registration
TAOM.Tests/Features/AutoResolveDiagnostics/*.cs
tools/analyze_battle_logs.py
```

## The bug history — this is the review's centre of gravity

This feature has shipped **wrong data three times**. Each time it looked correct and the tests were
green. Your highest-value job is to find the fourth instance.

1. **v1** read each party's composition from `Party.MemberRoster` at `MapEventEnded`. The engine
   calls `CaptureDefeatedPartyMembers` (MapEvent.cs:2018) and strips the defeated parties BEFORE the
   `:2068` dispatch, so every losing side's roster was missing everyone taken prisoner.
2. **v3/v4** switched to `MapEventParty.Troops` on advice that it was the untouched roster. It is
   not: `MapEventSide.MakeReadyParty` calls `MapEventParty.Update()`, which does `_roster.Clear()`
   and rebuilds from the already-stripped roster. Measured on real logs: **losing sides came out 55%
   short, winning sides 1% short.** The bias was invisible because it correlated perfectly with
   losing, which is exactly the variable under study.
3. **v5** fixed composition with a start-of-battle snapshot (median error +0.0%, 96% of losing sides
   within 5%) — but still read `leader`, `tactics`, `powerModifier` and `sideMorale` at
   `MapEventEnded`. Those are simulation *inputs*. Measured: losing sides had `sideMorale == 0` in
   **5,543 of 5,548** battles and a resolvable leader in **17 of 5,546 (0%)**, against 74% for
   winners — because the engine removes the loser's leader and zeroes its morale as part of losing.
   The log was recording the consequence of losing and labelling it the cause.
4. **v6** (this changeset) moves those four fields into the same start snapshot and adds a
   per-class `contextModifier` capture.

A separate instance of the same disease on the consumer side: `analyze_battle_logs.py` once read a
`losses` key that the C# never wrote, and therefore reported **0.0% loss rate for every troop class**
with no error. That is why `report_schema` exists.

**So: your primary question is not "does this compile" but "is any value in this log still measuring
something other than what its name claims?"**

## What to attack, in priority order

### 1. Timing correctness (highest value)
Go through `BattleLogRecord` and `BattleStartSnapshot` field by field. For each, decide whether it is
an INPUT to the simulation or an OUTCOME of it, then verify it is captured at the matching moment.
Flag anything still read at the wrong end. Be exhaustive — do not stop at the first hit.

Specifically interrogate: `strength`, `advantage`, `contextModifier`, everything under `siege`
(`settlementAdvantage`, `wallLevel`, `wallHitPoints`, `enginesBuilt`, `engineProgress`,
`settlementOwner`), `menStart`, `rounds`, `endedBy`, and the per-party `present` / `participating` /
`troopLimit` triple. Several of these are plausibly mutated by the battle itself.

### 2. The `PowerCalculationContext.Estimated` guard
`MapEventBattleLogAdapter` early-returns when `mapEvent.SimulationContext == Estimated`, on the
stated grounds that `GetContextModifier` throws `KeyNotFoundException` for that context because the
switch sets no terrain flag. **Verify that claim against the installed DLL**, not the decompiled
dump:

```
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel"
```

If it does not throw, the guard is silently discarding real battles and skewing the corpus. If it
does throw, check the guard actually covers every path that reaches `GetContextModifier`.

### 3. The consumer contract
Cross-check every `[JsonProperty]` name in `BattleLogRecord.cs` against `EXPECTED_TOP`,
`OPTIONAL_TOP`, `EXPECTED_SIDE`, `OPTIONAL_SIDE`, `EXPECTED_PARTY`, `OPTIONAL_PARTY` and
`SUPPORTED_VERSIONS` in `tools/analyze_battle_logs.py`, **in both directions**. A name written but
not listed, or listed but not written, is a real finding.

Then attack the analyzer's own arithmetic: does any statistic divide by a count that can be zero,
silently coerce a missing key to 0, or average across records of mixed schema versions in a way that
blends v5's known-bad morale/leader fields with v6's fixed ones?

### 4. NaN and malformed JSON
Newtonsoft serializes `float.NaN` as a bare `NaN` token, which is **not valid JSON** — Python's
`json.loads` rejects it. Trace every engine-sourced float into the record (`advantage`,
`powerModifier`, `sideMorale`, `strength`, `contextModifier` values, `settlementAdvantage`,
`engineProgress`). If any can be NaN or Infinity, one poisoned battle corrupts a line; check whether
the analyzer's malformed-line handling counts it or hides it. Check `FloatFormatHandling` /
`FloatParseHandling` on the serializer settings.

### 5. MCM gating — the user's explicit requirement
The requirement was verbatim: *"Ensure that autoresolve is gated behind MCM options to be able to
turn it off and on."* Verify `LogAutoResolvedBattles = false` stops **all** work, not merely the
write — no dictionary entry, no adapter call, no file created. Verify `LogAutoResolveTroopCensus`
nests correctly under it. Both are declared `RequireRestart = false`; verify nothing caches the value
at construction, which would make that flag a lie in the UI.

Then check `CoopSettingsRelevance.cs`: both names were added to the `Instrumentation` exclusion set.
That set's own doc comment says instrumentation "changes what is written to a log, never what is
computed." Prove or disprove that for both settings. A setting that can move a simulation outcome but
is excluded from the fingerprint is a silent co-op divergence.

### 6. Lifecycle and resource safety
`AutoResolveDiagnosticsBehavior` holds in-flight battles in a dictionary with `MaxTrackedBattles =
256`. Enumerate every way an entry can be added and never removed: exception during capture,
exception during write, save-load mid-battle, `OnSessionLaunched` firing twice, campaign teardown, a
MapEvent that never raises its ended event. Check whether hitting the cap drops battles **silently** —
large multi-party sieges are exactly what would be dropped, which biases the corpus rather than
merely losing data.

Check the troop-census once-per-session latch for re-entry, for a second campaign in the same
Bannerlord process, and for an exception mid-write leaving the latch set (or unset).

A diagnostic must never crash the campaign: confirm no exception can escape from a campaign-event
handler into the engine. Conversely flag any catch broad enough to hide the logger being broken.

### 7. TAOM house rules
- ADR-007: services take `IXxxAdapter`, never sealed TaleWorlds types. The adapters themselves may
  touch `MapEvent`/`CharacterObject` — that is their purpose.
- ADR-002: entry points under 150 lines and delegating. `AutoResolveDiagnosticsBehavior.cs` is 237
  lines — assess whether that is genuine logic that belongs in a service.
- ADR-003 no `#region`, ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG` outside `IoC.cs`.
- Constructor injection only; no `IoC.Resolve` outside boundary classes.
- DryIoc requires exactly one public constructor on every registered type.
- `PartyBase.Culture` is a computed getter `=> MapFaction.Culture` with no null guard — verify every
  culture resolution goes through `MapFaction?.Culture`.
- New player-facing MCM strings need `{=key}default` localization wrappers.

### 8. Tests
Do the tests actually pin the historical bugs — start-vs-end capture, roster stripping, the cap, each
gating condition — or only the happy path? Name the specific missing test rather than saying
"coverage could be better."

## Rules for your report

- **Verify before you assert.** Read the file or decompile the type this session. Do not report a
  signature, line number or measurement you have not actually seen. `ilspycmd` against the installed
  DLLs above is authoritative; `E:\Decompiled_Bannerlord\` may lag.
- Severity **P1** (data is wrong / campaign can crash / requirement unmet), **P2** (real bug, bounded
  blast radius), **P3** (worth fixing, not urgent).
- Each finding: severity, `file:line`, the concrete failure scenario (specific inputs → specific
  wrong output), and the concrete fix.
- If you check something and it is correct, say so in one line. A short honest report beats a long
  padded one.
- Do not modify any file. This is a review.
