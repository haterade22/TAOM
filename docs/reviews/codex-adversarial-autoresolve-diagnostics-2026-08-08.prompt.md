# Adversarial review — AutoResolveDiagnostics (TAOM, Bannerlord 1.4.7)

You are an adversarial reviewer. Your job is to find bugs, not to agree. Default to reporting a
concern when you cannot verify something. Read the actual files before asserting anything.

## What was built and why

TAOM is a LOTR total-conversion mod for Mount & Blade II: Bannerlord (v1.4.7, .NET Framework 4.7.2).

Investigation established that Bannerlord's auto-resolve (simulated map battle) scores every soldier
from `troop.level` ALONE — `Tier = clamp(ceil((level-5)/5), 0, MaxCharacterTier)`, then
`power = tierTable[Tier] * (mounted ? 1.2 : 1)`. Skills, weapons, armour, race and hit points are
invisible to it. Damage is `(0.5+0.5*rand) * 40 * (P_striker/P_struck)^0.7 * advantage`, and the
per-round tick budget is `min(2*N_enemy, N_own^0.6)` per side.

Before changing that balance we need to know what REAL mid-campaign armies look like. Party
templates only seed a lord's party at spawn and drift badly. So this changeset adds a diagnostic
that records one raw JSON Lines record per completed map battle, for offline analysis.

Design decision under review: the log stores ONLY raw troop ids and counts. No class, no race group,
no tier, no computed power. All derivation happens offline in `tools/analyze_battle_logs.py` against
`Main/_Module/ModuleData/troops/*.xml`.

## Files to review

NEW:
- `Main/Features/AutoResolveDiagnostics/Domain/BattleLogRecord.cs`
- `Main/Features/AutoResolveDiagnostics/AutoResolveLogFormatter.cs`
- `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehavior.cs`
- `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsSettingsProvider.cs`
- `Main/Features/AutoResolveDiagnostics/IAutoResolveDiagnosticsSettingsProvider.cs`
- `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsIoC.cs`
- `Main/Adapters/IMapEventBattleLogAdapter.cs`
- `Main/Adapters/MapEventBattleLogAdapter.cs`
- `TAOM.Tests/Features/AutoResolveDiagnostics/*.cs` (4 files)
- `tools/analyze_battle_logs.py`

MODIFIED:
- `Main/Features/TaomSettings.cs` (new `LogAutoResolvedBattles` bool, default true)
- `Main/IoC.cs` (one registration line)
- `Main/SubModule.cs` (one `AddBehavior` line)
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` (classified the new setting as Instrumentation)
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (pinned count 153 -> 154)

## Claims made in the code that you should try to REFUTE

These are load-bearing. If any is wrong, the feature logs nothing, logs corrupt data, or crashes.

1. **TIMING.** `MapEventBattleLogAdapter` claims that at `CampaignEvents.MapEventEnded` time, the
   sides' `Parties` list, each party's `MemberRoster`, and `MapEventParty.DiedInBattle` /
   `WoundedInBattle` / `RoutedInBattle` are ALL still intact — because `OnMapEventEnded` is
   dispatched at `MapEvent.cs:2068` while the teardown `MapEventSide.HandleMapEventEnd()` runs at
   `:2147`, after it. **Verify this against the INSTALLED v1.4.7 DLL, not a decompile dump.** If
   parties are removed earlier (e.g. by `ControlAndUpdateDefeatedPartiesAfterBattle()` at `:2054`,
   which runs BEFORE the dispatch), the annihilated side's roster — the most important data — may
   be missing or zeroed, and the whole dataset is biased toward winners.

2. **`IsFinalized` trap.** The code deliberately does NOT gate on `mapEvent.IsFinalized`, claiming
   `State` is set to `WaitingRemoval` at `:2067`, one line BEFORE the dispatch, so `IsFinalized` is
   already true inside the handler. Verify. Other TAOM code (`EncounterAdapter.cs:137`) DOES gate on
   it — confirm these are genuinely different situations and not an inconsistency.

3. **Roster reconstruction.** The analyzer reconstructs a side's STARTING composition as
   `roster + died + routed` (wounded are claimed to still be present in `MemberRoster`). Verify that
   claim against the engine: are wounded troops still in `MemberRoster` at this point? Are routed
   troops removed from it? If either is wrong the reconstructed compositions are silently wrong, and
   the tuning built on them is wrong. Note the code also logs `menStart` from
   `HealthyManCountAtStart` so the analyzer can validate its own reconstruction — check that this
   cross-check is actually implemented and would catch the error.

4. **Member existence and signatures.** Verify EVERY TaleWorlds member used exists with the claimed
   signature on the installed 1.4.7 DLLs: `MapEvent.AttackerSide/DefenderSide/EventType/
   SimulationContext/MapEventSettlement/IsPlayerMapEvent/BattleState/EndedByRetreat/Winner`,
   `MapEventSide.Parties/LeaderParty`, `MapEventParty.Party/HealthyManCountAtStart/DiedInBattle/
   WoundedInBattle/RoutedInBattle`, `PartyBase.MemberRoster/NumberOfHealthyMembers/Culture/
   MapFaction/LeaderHero`, `TroopRoster.Count/GetElementCopyAtIndex`, `Hero.PowerModifier`,
   `CampaignTime.Now.ToDays/ToHours`.

5. **Banned getters.** TAOM has a hard rule that `PartyBase.Owner` is a throwing computed getter
   (crash `0b462fd8`, pinned by `PartyOwnerGetterBanTests`) and that `MapEventSide.MapFaction` derefs
   `LeaderParty`. Confirm the adapter avoids both. Check whether `PartyBase.Culture`,
   `PartyBase.MapFaction` or `PartyBase.NumberOfHealthyMembers` have the same hazard — decompile them.

## Also hunt for

- **Performance.** This runs once per completed battle, not per strike. But verify there is nothing
  accidentally quadratic, and that a very large army (an 8-party stacked army with 50 troop types
  each) does not produce a pathological amount of work or an enormous single log line. What is the
  realistic worst-case line length, and does anything downstream break on it?
- **Unbounded growth.** Is any state retained between battles? (There should be none but a sequence
  counter.) Does anything leak across save-load or a second campaign in the same process?
- **Exception safety.** A diagnostic must never propagate into the campaign tick. Find any path that
  can throw past the guards — including inside the `catch` blocks themselves, and including
  `JsonConvert.SerializeObject` on a cyclic or huge object.
- **The toggle.** `LogAutoResolvedBattles` defaults TRUE, unlike most TAOM diagnostics. The provider
  fallback is `?? true`. Is the gate placed such that flipping it mid-session leaves nothing
  half-open? (The code claims there is no latch because the record is reconstructed entirely from
  end-of-battle data — verify that claim.)
- **The Python analyzer.** `tools/analyze_battle_logs.py` — check the JSONL parsing tolerates a
  truncated final line, that the offline troop classifier's use of `default_group` plus item-id
  token matching is sound (or state clearly why it is not), that `derive_culture_multipliers` is
  mathematically what it claims, and that the Monte-Carlo `simulate()` faithfully reproduces the
  engine loop described at the top of this prompt. Flag any statistical claim the code makes that
  its method cannot support.
- **Schema/consumer contract drift.** The C# writes field names via `[JsonProperty]`; the Python
  reads them by string. Enumerate every field and confirm both sides agree. A mismatch produces a
  log that looks healthy and analyses to nothing.

## Output

Group findings by severity (P1 blocking / P2 should-fix / P3 nit). For each: file, line, what is
wrong, why it matters, and the minimal fix. If you cannot verify a claim, say so explicitly rather
than assuming it holds. Call out anything where you disagree with a comment in the code — the
comments make strong factual claims about engine behaviour and several are load-bearing.
