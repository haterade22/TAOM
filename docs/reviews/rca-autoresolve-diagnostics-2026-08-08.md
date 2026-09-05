# RCA — AutoResolveDiagnostics, 2026-08-08

**Verdict: three defects shipped into review, all of the same class — a measurement tool that
produced confident, wrong numbers instead of failing.** None would have thrown. None would have
been noticed by looking at the output. All three were caught by review, two of them independently
by two reviewers.

The feature's entire purpose is to measure real armies so auto-resolve balance can be tuned against
them. Every defect below silently biased or nulled that measurement while the tool kept printing
neat tables.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | **P1** | Composition read from `Party.MemberRoster` at `MapEventEnded`. The engine strips captured troops from a *defeated* party's roster (`CaptureDefeatedPartyMembers`, `MapEvent.cs:2018`) and empties it on a rout (`MapEventSide.Route`, `:1250`), both from the `BattleState` setter at `:301` — **before** the `:2068` dispatch. Every composition, loss rate, culture multiplier and the whole `--replay` was measured on winners only. | Engine lifecycle | I verified the teardown that runs *after* the event (`HandleMapEventEnd`, `:2147`) and concluded the roster was "intact". I never asked what runs *before* it. Confirming the absence of one mutation is not confirming the absence of all mutations. | Rule below |
| 2 | **P1** | The `menStart` cross-check was asserted in three places (DTO comment, feature doc, CHANGELOG) and implemented in none. | False documentation | I wrote the justification for a safeguard at the same time as the field it justified, then never wrote the safeguard. The comment read as evidence that it existed. | Cross-check implemented (`report_reconstruction`); it is precisely what would have caught #1 on the first log |
| 3 | **P2** | Analyzer read a `losses` key the C# never wrote → **0.0% loss rate for every class**, reported without complaint. | Producer/consumer drift | I wrote the consumer first, then changed the producer's field names, and never re-checked the contract. The tool had no way to notice. | `report_schema` validates the field contract in both directions across *all* records and hard-stops |
| 4 | **P2** | Analyzer defaulted to a `.jsonl` path that never exists and parsed whole lines, so against the real prefixed log format it read **zero records**. | Producer/consumer drift | I chose the shared logger *after* writing the analyzer, and did not revisit the reader. | Same as #3, plus the log path derives from `BANNERLORD_GAME_DIR` |
| 5 | P2 | Whole-side roster attributed to the *leader's* culture, so a mixed army's men were all counted as one culture. | Sampling | I aggregated per side because it made the JSON smaller. Aggregation destroyed the per-culture attribution the tool exists to produce. | Schema is per-party, each with its own culture |
| 6 | P2 | Logged `leaderParty.MobileParty.Morale`, but the simulation reads `MapEventSide.GetSideMorale()` — strength-weighted, with a siege-defender clamp. Wrong for any stacked army. | Wrong source | I picked the property whose name matched the concept instead of the one the simulation actually calls. | Logs `GetSideMorale()` |
| 7 | P2 | `PartyBase.Culture` is `MapFaction.Culture` with no null guard (`PartyBase.cs:255`), so `leaderParty?.Culture` still throws when the faction is null — losing the *entire* record to the catch, not just the field. | Unguarded chain | `?.` on the *receiver* looks like a null-safe access. The throw is inside the getter. | Routes through `MapFaction?.Culture`. This is the documented `PartyBase.Owner` class (`adapters.md`) |
| 8 | P3 | Two "adapter throws" tests passed vacuously — production returned at the null-guard before reaching the mocked call. | Test theatre | I asserted "does not throw" on a path that never executed. A passing test proved nothing. | Emit path split out so it is genuinely reachable |

## Root-cause pattern: a measurement tool that cannot fail loudly

Findings 1–6 share one shape. **Every one of them produced plausible output.** A 0.0% loss rate, a
composition table of winners, a morale column that was simply the wrong number — all rendered in
neat columns with no warning. For ordinary features a silent wrong answer is a bug; for a tool whose
output is pasted into a balance config, it is worse than no tool, because it launders a guess into a
number.

The generalisable lesson:

> **A tool built to measure something must be able to detect that it is measuring the wrong thing.**
> Every measurement pipeline needs at least one independent quantity it can cross-check itself
> against, and it must stop rather than report when the check fails.

`menStart` was exactly that quantity — logged, documented as the safeguard, never used. Finding 2 is
therefore not a separate bug from finding 1; it is *why finding 1 survived*.

## The engine-lifecycle rule this produces

> **When reading engine state inside an event handler, enumerate every mutation that runs BEFORE the
> dispatch, not only the teardown that runs after it.**
>
> Verifying "the teardown is later, so the data is intact" is a half-audit. Decompile the dispatch
> site and walk *upward* through every call that reaches it — for `MapEventEnded` that means the
> `BattleState` setter path (`OnBattleWon` → `CalculateAndCommitMapEventResults` →
> `CaptureDefeatedPartyMembers`) and `Route()`, both of which gut the loser's roster before the
> event fires.
>
> **And do not stop at the second mechanism either.** The fix that replaced `MemberRoster` with
> `MapEventParty.Troops` was equally wrong: `MakeReadyParty` calls `MapEventParty.Update()`, which
> clears and rebuilds `_roster` from the same stripped `MemberRoster`. The only reliable record of
> a pre-battle state is one you captured pre-battle.

Sibling of the existing "GameModel Cross-Entity Propagation" rule in `csharp-architecture.md`: both
say the same thing — *open the engine consumer/producer, do not reason from the shape of the API*.

## Why each review agent missed what it missed

| Agent | Result | Why |
|---|---|---|
| Standards | PASS (correct) | ADR compliance was genuinely clean. Nothing in its rule set concerns data correctness. |
| Efficiency | PASS (correct) | Perf was genuinely fine. It even decompiled `GetElementCopyAtIndex` to confirm O(1). Right answer to the question asked. |
| Completeness | 4 real gaps, all valid | Found the missing feature doc, feature-map row, issue and stale co-op counts. Correctly scoped. |
| API compatibility | 43 verified, found #7 | Confirmed both timing claims **and** independently found the `PartyBase.Culture` chain. Its one soft spot: it accepted the adapter's own comment that the start roster was "reconstructed from `HealthyManCountAtStart` + per-troop counters", which was the *claim* (#2), not the implementation. **A reviewer reading a comment as evidence is the same failure as an author writing one.** |
| Cross-system data flow | Found #1, #3, #5, and more | The highest-value agent again. It traced the producer→consumer contract key by key and walked *upward* from the dispatch site — exactly the discipline the rule above now names. |
| Codex (adversarial) | Found #1, #6, #7 independently | Two independent reviewers converging on #1 raised confidence enough to act immediately rather than re-litigate. |

**I also mis-reported the Codex run as environmentally blocked** before it had finished, on the
strength of a partial read of a 13 MB output file polluted by 88 model-manager errors. The review
had in fact completed. Correcting this mattered: it contained one of the two independent P1s. The
lesson is narrow and mine — *do not declare a background job failed from a partial read of its
output; check for the completion signal first.*

## Postscript — the fix was wrong twice

v3 shipped reading `MapEventParty.Troops` on the data-flow agent's recommendation, and this RCA
originally recommended it as the durable rule. The first live session falsified it: losing sides
came back a median **55% short**, winners 1%. `MapEventSide.MakeReadyParty` calls
`MapEventParty.Update()` → `_roster.Clear()` → rebuild from the already-stripped `MemberRoster`.

Same bug class, third mechanism. What caught it both times was the `menStart` cross-check — finding
#2 above, the safeguard that was documented in three places and implemented in none. It has now
paid for itself twice, which is the strongest argument in this document for building the check
before the thing it checks.

v5 uses a start-of-battle snapshot, accepting the per-battle latch and handling it explicitly.

## What changed

- Schema v5: `fielded` from a start-of-battle snapshot; casualties from the engine's accumulating
  per-troop rosters, which genuinely do survive.
- Troop census: engine-side tier/power/formation/HP for every CharacterObject, once per session.
  Validated 829/829 on tier and classification; caught a missing ×1.2 mounted multiplier in the
  offline model.
- Siege telemetry: `settlementAdvantage` and friends — measured at 3.6–6.0, the term that decides
  a siege and previously invisible.
- `report_reconstruction` cross-checks per-party rosters against `menStart` and warns loudly.
- `report_schema` validates the contract both directions and **hard-stops**. **Correction, added
  2026-08-09:** as written at v5 this checked only the top level and one side of `records[0]`, and the
  version gate it claimed to enforce did not exist — `SUPPORTED_VERSIONS`, `EXPECTED_PARTY` and
  `OPTIONAL_PARTY` each had exactly one reference in the file, their own definition. The party-level
  and siege-level checks, the union across every record and both sides, and the enforced version drop
  all arrived in the 2026-08-08 review wave (Review 86). This bullet described the intended end-state
  as though it had shipped — the same documented-but-unimplemented-safeguard failure this RCA's own
  Finding #3 is about, recurring inside its retrospective. Left visible rather than silently rewritten.
- `session` (`Campaign.UniqueGameId`) and `rounds` (`MapEvent.UpdateCount`) added; the analyzer
  warns when two campaigns are pooled.
- `GetSideMorale()`, `MapFaction?.Culture`, battle-type histogram, replay never omits a row.

## Lessons to append

`docs/reviews/lessons/state-lifecycle-save.md` — the engine-lifecycle rule above.
`docs/reviews/lessons/build-tooling-workflow.md` — the measurement-tool rule above.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
