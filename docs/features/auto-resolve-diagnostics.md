# Auto-Resolve Diagnostics

> **Status:** shipped 2026-08-08. Diagnostic only — changes no gameplay.
> **MCM toggles:** `Battle Tactics/Auto-Resolve Diagnostics` → `Log Auto-Resolved Battles` **default ON**
> (master switch), and `Log Troop Census` **default OFF** (see [Turning it off](#turning-it-off)).

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
mid-campaign. This feature captures what actually fights.

## What it records

One record per completed map battle, written on `CampaignEvents.MapEventEnded`. The record is
deliberately **raw**: troop ids and counts, nothing derived. Tier, class, race group and power are
all computed offline against `troops_*.xml`.

That choice is the point of the design. It means the analysis can be redone a dozen different ways
— different class taxonomies, different race groupings, different power curves — **without a
rebuild and without a second play session**, and it keeps the unverified `WeaponClass`
classification API off the data-collection path entirely.

| Field | Why it earns its place |
|---|---|
| `fielded` (troop id → count, **per party**) | the army as fielded, from a start-of-battle snapshot. **Healthy men only** — see below. Per party, because a side can hold several cultures |
| `killed`, `wounded`, `routed` (per party) | from the engine's accumulating casualty rosters, which do survive to battle end |
| `strength`, `advantage` | the engine's OWN power figure and the multiplier it applied — ground truth to check the offline model against |
| `siege` block | `settlementAdvantage` (3.6–6.0 measured), wall level, wall HP, engines built. The term that decides a siege |
| `menStart` | authoritative side total, independent of the per-party rosters — so the analyzer cross-checks the two and reports any divergence |
| `rounds`, `session` | how decisive the battle was; and which campaign it came from, so two campaigns don't pool silently |
| `terrain` (`MapEvent.SimulationContext`) | vanilla already grants type-vs-terrain power modifiers; without this, a class-loss skew cannot be separated from a counter effect |
| `tactics`, `powerModifier`, `sideMorale`, `leader` | the multipliers the sim actually applied — confounds any tuning must control for. **Start-snapshot, not battle-end** (see below) |
| `contextModifier` (per troop class) | `MilitaryPowerModel.GetContextModifier` (**not** `CombatSimulationModel` — a Codex review prompt sent reviewers to the wrong type; both abstract bases live in `ComponentInterfaces`), the terrain × class × side term spanning −0.50 to +0.30. The offline power model was missing it entirely |
| `player` | player battles use a different blunt-damage chance and difficulty multipliers, and may have been fought rather than simulated |
| `parties` | distinguishes a lone lord from a stacked army |
| `winner`, `endedBy` | how decisive battles really are |

`fielded` is the army as it stood when the battle began. The analyzer cross-checks the summed
per-party rosters against each side's independently-recorded `menStart` and reports any divergence —
that check is what caught two successive versions of this feature measuring winners only.

**Healthy men only, and that cross-check is why.** `AccumulateRoster` records
`element.Number - element.WoundedNumber`. It used to record `Number`, which counts the wounded — but
`menStart` sums `MapEventParty.HealthyManCountAtStart`, which does not, and a wounded troop is never
allocated into the simulation at all (vanilla's supplier skips `!IsWounded`). Measured before the
fix: **12.3% of sides reported more fielded men than `menStart`**, corpus-wide overstatement +4.5%,
worst case `menStart=1` against `fielded=141` — a party that was 140 wounded, credited with 141
fighters. The median error was +0.00%, which is exactly why three earlier review passes saw nothing.
After the fix, on a live 1,672-record session: **100% of sides on both the winning and losing side
within 5%**.

**A party that joins mid-battle** is folded in through `IMapEventBattleLogAdapter.SnapshotParty`,
wired from `OnPartyAddedToMapEvent`. It captures only the joiner's roster, plus a `contextModifier`
for any troop class the battle had not already seen — so a cavalry reinforcement into an
infantry-only fight does not end up in `fielded` with no terrain term to score it by. The obvious
implementation, re-running `SnapshotStart` and keeping its rosters, is what this replaced: it
re-derives every party on both sides plus both leaders, morale and advantage and discards all of it,
and because `PartyBase.MapEventSide`'s setter recurses into `MobileParty.AttachedParties`, a
reinforcing army raises the event once per attached party — quadratic in the size of the battle. The
per-side leader inputs are deliberately *not* revisited: a party arriving mid-fight does not
retroactively change the morale or advantage the simulation has already been applying.

## Three engine facts this depends on

All verified against the installed v1.4.7 binaries, not assumed:

1. **`Party.MemberRoster` is useless here.** The engine strips captured troops out of a *defeated*
   party's `MemberRoster` (`CaptureDefeatedPartyMembers`, `MapEvent.cs:2018`) and empties it again
   on a rout (`MapEventSide.Route`, `:1250`) — both reached from the `BattleState` setter at `:301`,
   which runs **before** `FinishBattle` dispatches the event. Measuring composition from it would
   sample winners only: a silent survivorship bias in the exact measurement this feature exists to
   make. This was shipped in v1 and caught in review.

2. **`MapEventParty.Troops` is no refuge either.** It looks like one — `OnTroopKilled` /
   `OnTroopWounded` / `OnTroopRouted` (`:305,:312,:318`) only flip per-descriptor state. But
   `MapEventSide.MakeReadyParty` (`:774-781`) calls `MapEventParty.Update()`, which does
   `_roster.Clear()` and **rebuilds from the already-stripped `MemberRoster`**. Measured over 4,380
   live battles: losing sides read a median **55% short**, winning sides 1%.

   So nothing readable at battle end holds the army as fielded, and the feature takes a
   **start-of-battle snapshot**. That reintroduces a per-battle latch, handled per
   `rca-tournament-exit-hang-2026-07-06.md`: the closer runs in a `finally` and runs
   **unconditionally**, so flipping the toggle mid-session cannot strand a pending entry, and a hard
   cap plus a session-launch clear catch anything that never ends. The *opener* is gated on the MCM
   toggle — turning the feature off has to stop the expensive half, not just the write.

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
   (`EncounterAdapter.cs:137`) legitimately gates on `IsFinalized` for a different purpose —
   **doing so here would log nothing, forever, with no error.**

## Where the log goes

Records are written through TAOM's shared `IModLogger`, so they inherit rotation, crash-bundle
inclusion, and — critically — **synchronous durability**. `LogInfo` drains on the calling thread;
`LogDebug` sits on an async queue and is lost on a hard native crash. For a diagnostic whose last
line is the evidence, that difference is the whole point (`FileLogger.cs:79-89`).

```
<game>\bin\Win64_Shipping_Client\Logs\taom_debug_<timestamp>.log
```

Each record looks like:

```
[2026-08-08 14:32:01] [INFO] [AutoResolve] {"v":6,"session":"...","id":"1084.3",...}
```

### Schema versions

`v` is the first field of every record and the analyzer hard-stops on a version it does not know,
because the failure mode of this feature is a log that looks healthy and analyses to nothing.

| Version | What changed | Is the data usable? |
|---|---|---|
| v1–v2 | composition read from `Party.MemberRoster` at battle end | **No** — losing sides missing everyone captured |
| v3–v4 | composition read from `MapEventParty.Troops` | **No** — losing sides a median 55% short, winners 1% |
| v5 | composition from a start-of-battle snapshot; added `strength`, `advantage`, per-party `present`/`participating`/`troopLimit`, the census | **Yes, except** `leader` / `tactics` / `powerModifier` / `sideMorale`, which are post-battle artefacts |
| v6 | those four moved into the start snapshot; added per-class `contextModifier` | Yes |

`analyze_battle_logs.py` accepts v5 and v6 and prints a warning naming the four unreliable fields
when any v5 record is present, rather than silently blending the two corpora. Anything outside
`SUPPORTED_VERSIONS` is **dropped**, with the count reported — that gate was declared but never
wired until 2026-08-08, so the refusal this section describes did not actually happen for the first
six schema versions. The party-level and siege-level field contracts are checked too, unioned across
every record and both sides rather than sampled from `records[0]`.

### Non-finite floats

Every engine-sourced float in a record (`advantage`, `powerModifier`, `sideMorale`, `strength`, each
`contextModifier` value, `settlementAdvantage`, `engineProgress`) is serialized with
`FloatFormatHandling.DefaultValue`, so a `NaN` or `Infinity` writes as JSON `null`.

The reason is not that a bare `NaN` token would crash the analyzer — **it would not**. Python's
`json.loads` accepts `NaN`/`Infinity` via `parse_constant` and hands back a float, so a poisoned
record would sail past the malformed-line counter and silently contaminate every mean, median and
comparison downstream. That is a quieter failure than a parse error, and therefore a worse one. No
non-finite value has been observed in a live corpus; the guard is there because "no path found this
time" has not been a reliable predictor for this bug class.

## Turning it off

Two MCM toggles under **Battle Tactics → Auto-Resolve Diagnostics**, both live (no restart):

| Setting | Default | Effect |
|---|---|---|
| `LogAutoResolvedBattles` | **on** | Master switch. Off means no start snapshot, no record, no file — not merely a suppressed write |
| `LogAutoResolveTroopCensus` | **off** | The once-per-session troop dump. Nested under the master: off-master means no census regardless |

The two defaults differ on purpose. A battle record describes a session that has already happened,
so it has to have been running before anyone knew they wanted it — that one is opt-out. The census
is static per build: the engine's tier, power and classification for a troop type only move when
troop data or the balance config moves, so **one capture serves until then**, and it is opt-in.

The size argument is not marginal. Measured on a live session, the census was **8,341 of 17,622 log
lines — 47% of the file**, rewritten identically on every launch, against 3,661 battle records doing
the actual work. Turn it on for one session after changing troop data or `battle_balance_config.json`,
let the analyzer consume it, then turn it back off.

Its per-record lines are also written with `LogDebug` rather than `LogInfo`, inverting the rule the
battle records follow. `LogInfo` takes the write lock and flushes on the calling thread per line, so
8,341 of them would be thousands of synchronous flushes on the session-launch thread. A census line
is not crash evidence — it is written before any gameplay — but the completion summary stays
`LogInfo` so there is still durable proof the census ran.

Both settings are classified `Instrumentation` in `CoopSettingsRelevance`, so neither enters the
co-op settings fingerprint: they gate log lines and change nothing the simulation reads.

## The troop census

Once per session the feature also dumps every `CharacterObject`'s engine-side stats — tier, the
power the simulation actually scores it at, formation class, hit points, race — tagged
`[AutoResolveCensus]`. That turns the offline analyzer's assumptions into data.

First run validated: **829/829 tier derivations correct**, **829/829 classifier agreements**, hit
points **uniformly 100** (so race and armour never reach the removal roll). It also caught a real
bug — the engine disagreed on power for exactly 146 troops, all mounted, all off by ×1.2, because
the offline model omitted `MountedMultiplier`. The analyzer now takes power from the engine
directly rather than from a hardcoded table, and supplements `troops_*.xml` with census entries for
`looter`, villagers, caravan guards and armed traders — 7% of every army that was previously
dropped from composition.

## Reading the data

```bash
python tools/analyze_battle_logs.py                 # summary + tools/reports/battle-logs/REPORT.md
python tools/analyze_battle_logs.py --stdout        # print only
python tools/analyze_battle_logs.py --no-player     # drop ALL player-involved battles
python tools/analyze_battle_logs.py --keep-player-fought  # keep battles the player FOUGHT (dropped by default)
python tools/analyze_battle_logs.py --min-men 100   # ignore skirmishes (default 40)
python tools/analyze_battle_logs.py --replay        # replay real armies under candidate knobs
```

It answers the five questions that decide every balance knob:

| Report | Decides |
|---|---|
| Army sizes + size-ratio distribution | `CountExponent` (how much numbers matter) |
| Outcome lopsidedness — winner's surviving fraction | whether the threshold problem is as sharp in practice as the model predicts |
| Composition by culture, vs the ~7-point template baseline | **whether a counter matrix is balance or merely texture** |
| Matchup frequency | which fights to actually tune for |
| Losses by class, dead vs wounded split | empirical check on any counter values before shipping them |

`--replay` re-runs the **real logged armies** through a faithful re-implementation of the engine's
round/tick/strike/removal loop under each candidate scenario, and derives per-culture power
multipliers from what actually fought.

## Files

| File | Role |
|---|---|
| `Main/Features/AutoResolveDiagnostics/Domain/BattleLogRecord.cs` | engine-free DTO; the `[JsonProperty]` names are the contract with the Python tool |
| `Main/Features/AutoResolveDiagnostics/Domain/BattleStartSnapshot.cs` | the start-of-battle capture: per-party rosters plus each side's leader-derived inputs |
| `Main/Features/AutoResolveDiagnostics/AutoResolveLogFormatter.cs` | pure DTO → one tagged JSON line; the whole testable surface |
| `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehavior.cs` | `CampaignBehaviorBase`. Event wiring plus the pending-battle map, nothing else (ADR-002). Subscribes `MapEventStarted`, `OnPartyAddedToMapEventEvent`, `MapEventEnded` and `OnSessionLaunchedEvent` |
| `Main/Features/AutoResolveDiagnostics/IAutoResolveLogWriter.cs` + `AutoResolveLogWriter.cs` | the write half — record ids, the once-per-session census, emit policy. Split out of the behavior, which had reached 237 lines against ADR-002's 150. The pending map stayed behind because it is keyed by the sealed `MapEvent` (ADR-007) |
| `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsSettingsProvider.cs` | MCM seam; each `??` fallback pinned against the matching compiled default (`?? true` for the master, `?? false` for the census) |
| `Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsIoC.cs` | DryIoc registration |
| `Main/Adapters/MapEventBattleLogAdapter.cs` | the ADR-007 boundary, two entry points: `SnapshotStart` walks `MapEvent` → sides → parties → rosters for the whole battle, `SnapshotParty` folds in one late joiner |
| `Main/Adapters/TroopCensusAdapter.cs` | reads the engine's own tier/power/formation/HP for every `CharacterObject` |
| `tools/analyze_battle_logs.py` | offline analysis + replay simulator |

## Cost

One capture and one log line per completed battle — a handful of times per in-game day. Nothing
runs per strike. Worst case measured: an 8-party stacked army with ~50 troop types per side is
~1,600 O(1) roster reads and a ~40 KB line. `TroopRoster.GetElementCopyAtIndex` is a plain array
index with no allocation (verified by decompile).

## Known limitations

- **Player-fought battles are logged but excluded from analysis by default.** A player battle carries
  `player: true`, and `playerSimulated` says whether the player auto-resolved it or fought it. A
  battle the player *fought* is a mission result, not a `SimulateHit` sample, so `analyze_battle_logs.py`
  drops `player && !playerSimulated` unless you pass `--keep-player-fought`. `--no-player` is the
  stricter option: it drops every player-involved battle, auto-resolved ones included.
- **Coverage is complete for anything with combat.** Field battles, sieges, sally-outs, blockades,
  raids and hideouts are all `MapEvent.BattleTypes` and all reach `OnMapEventEnded`. The one absent
  case is a siege that ends by starvation surrender (`SiegeEvent.OnBeforeSiegeEventEnd`, no
  `MapEvent`) — no combat occurred, so there is nothing to measure.
- **`routed` is structurally always empty for a siege defender** — `MapEventSide.OnTroopRouted`
  (`:682`) gates on `EventType != Siege || MissionSide == Attacker`. Not a capture bug.
- **Battle types are pooled unless you segment them.** Sieges and raids obey different mechanics;
  the analyzer prints a type histogram and warns about it.
- **Offline classification is a proxy.** The analyzer classifies troops from `default_group` plus
  item-id tokens. The shipping in-game classifier (when it lands) will resolve
  `ItemObject.WeaponClass` and may disagree on some troops.
- **The record id** is `day.sequence`, unique within a session only. `SyncData` is deliberately
  empty — a diagnostic ring has no business in the save file.

## Related

- [`docs/features/battle-balance.md`](battle-balance.md) — the three GameModels this data will tune
- [`docs/features/troop-weight-system.md`](troop-weight-system.md) — documents that auto-resolve is
  power-driven, not count-driven
- [`docs/features/economy-diagnostics.md`](economy-diagnostics.md) — the "retrospective diagnostics
  default on" precedent this follows
