# Auto-Resolve Diagnostics

**Status: REMOVED.** Removed from the 1.4.x line by WP123 (the first release after v2.0.28); on the 1.5.x line in v2.0.35. The code, its tests, its two MCM switches and `tools/analyze_battle_logs.py` are gone from the tree. The engine notes below were verified on v1.4.7; [balance-levers.md](../modding/balance-levers.md) qualifies the "`troop.level` alone" claim (the mounted factor, the leader's captain perks and the terrain term also count).

## What it was

A campaign behavior that wrote one raw record per completed map battle (troop ids and counts per party, casualties, the engine's own strength and advantage figures, the siege and terrain terms) to the TAOM log, for offline balance analysis of auto-resolve. An optional troop census dumped every troop's engine tier and power once per session. It shipped 2026-08-08, on by default, and changed no gameplay.

## Why it was removed

The maintainer decided it is no longer needed; its issue, #430, is closed. It was also the largest writer in the log. In one 50-minute campaign on 2026-10-26 it wrote 52,763 records, 126 MB of a 130 MB log, and each INFO record is a synchronous disk write on the campaign thread. On the public 1.4.8 build it was 39 percent of one 35-minute player log (225 records, about 421 KB of 1,092,378 bytes, 2026-11-01).

## Why this exists

Bannerlord's simulated map battles score every soldier from **`troop.level` alone**:

```
Tier  = clamp(ceil((level − 5) / 5), 0, MaxCharacterTier)   // TAOM raises the cap 6 → 10
power = tierTable[Tier] × (mounted ? 1.2 : 1.0)             // nothing else is consulted
dmg   = (0.5 + 0.5·rand) × 40 × (P_striker / P_struck)^0.7 × advantage
ticks = min(2 × N_enemy, N_own ^ 0.6)                        // strike budget per side per round
```

Skills, weapons, armour, race and hit points never reach the simulation. That means
[`tools/rebalance_troops.py`](../../tools/rebalance_troops.py) — the project's main balance lever —
has only ever tuned an axis auto-resolve cannot see, and no script in the repo sets or audits the
troop `level` ladder across factions. Measured consequence: Gondor beats Mordor 100% of the time,
Erebor beats Gundabad 100%, and Mordor is last of all seventeen cultures on power per man.

Fixing that has to be tuned against **real armies**, and party templates cannot supply them — they
only seed a lord's party at spawn and drift through recruitment, upgrades and casualties by
mid-campaign. This feature captured what actually fights.

## Three engine facts it depended on

All verified against the installed v1.4.7 binaries, not assumed:

1. **`Party.MemberRoster` is useless here.** The engine strips captured troops out of a *defeated*
   party's `MemberRoster` (`CaptureDefeatedPartyMembers`, `MapEvent.cs:2018`) and empties it again
   on a rout (`MapEventSide.Route`, `:1250`) — both reached from the `BattleState` setter at `:301`,
   which runs **before** `FinishBattle` dispatches the event. Measuring composition from it would
   sample winners only: a silent survivorship bias in the exact measurement this feature existed to
   make. This was shipped in v1 and caught in review.

2. **`MapEventParty.Troops` is no refuge either.** It looks like one — `OnTroopKilled` /
   `OnTroopWounded` / `OnTroopRouted` (`:305,:312,:318`) only flip per-descriptor state. But
   `MapEventSide.MakeReadyParty` (`:774-781`) calls `MapEventParty.Update()`, which does
   `_roster.Clear()` and **rebuilds from the already-stripped `MemberRoster`**. Measured over 4,380
   live battles: losing sides read a median **55% short**, winning sides 1%.

   So nothing readable at battle end holds the army as fielded, and the feature took a
   **start-of-battle snapshot**. That reintroduced a per-battle latch, handled per
   `rca-tournament-exit-hang-2026-07-06.md`: the closer ran in a `finally` and ran
   **unconditionally**, so flipping the toggle mid-session could not strand a pending entry, and a
   hard cap plus a session-launch clear caught anything that never ended. The *opener* was gated on
   the MCM toggle, because turning the feature off had to stop the expensive half, not just the
   write.

   The per-troop casualty rosters are a different matter — `DiedInBattle` / `WoundedInBattle` /
   `RoutedInBattle` are `TroopRoster` fields that accumulate and are never cleared, so losses ARE
   read at battle end.

   **The same trap a second time.** v5 fixed composition this way but still read `leader`,
   `tactics`, `powerModifier` and `sideMorale` at `MapEventEnded`. Those are simulation *inputs*, and
   the engine removes the defeated side's leader and zeroes its morale as part of losing. Measured
   over the v5 corpus: losing sides had `sideMorale == 0` in **5,543 of 5,548** battles and a
   resolvable leader in **17 of 5,546 (0%)**, against 74% for winners. The log was recording the
   consequence of losing and labelling it the cause. v6 moves all four into the same start snapshot.
   The general rule this feature keeps re-learning: **classify every field as an input or an outcome,
   and capture it at the matching end.**

3. **`IsFinalized` is already true inside the handler.** `MapEvent.State` is set to
   `WaitingRemoval` at `:2067`, one line *before* the dispatch at `:2068`. Sibling code
   (`EncounterAdapter.cs:302`) legitimately gates on `IsFinalized` for a different purpose: 
   **doing so here would log nothing, forever, with no error.**

## Where the code lives now

Git history only: `git log --diff-filter=D -1 -- Main/Features/AutoResolveDiagnostics` names the removal commit, and its parent holds the last copy. The deleted paths:

- `Main/Features/AutoResolveDiagnostics/` (behavior, IoC, settings provider, log formatter and writer, `Domain/` records)
- `Main/Adapters/IMapEventBattleLogAdapter.cs`, `MapEventBattleLogAdapter.cs`, `ITroopCensusAdapter.cs`, `TroopCensusAdapter.cs`
- `TAOM.Tests/Features/AutoResolveDiagnostics/`
- `tools/analyze_battle_logs.py`

The MCM settings `TaomSettings.LogAutoResolvedBattles` and `LogAutoResolveTroopCensus` went with it. A saved value for either is ignored on load, and saves are unaffected: the behavior's `SyncData` was empty and it defined no saveable type.

## History

- Issue: #430
- [RCA: the first review, 2026-08-08](../reviews/rca-autoresolve-diagnostics-2026-08-08.md)
- [`docs/features/battle-balance.md`](battle-balance.md): the GameModels this data was meant to tune

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/balance-levers.md](../modding/balance-levers.md)

<!-- backlinks-end -->
