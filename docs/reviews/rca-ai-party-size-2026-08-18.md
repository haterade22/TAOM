# RCA: AI Party Size (#461) deep review, 2026-08-18

Five review agents plus one user question against the uncommitted #461 changeset (AI lord party
size, food and wage relief, garrison scaling, startup-gold re-derivation). Standards, API
compatibility and completeness came back clean. Six findings survived verification, one of them a
real gating defect that shipped past my own tests, and one of them a wrong number I had already put
in front of the user.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Player-clan parties led by a companion collected the full AI treatment: 10x size cap, 90% food relief, 90% wage relief. The gate was `!isMainParty && isLordParty && hasLeaderHero`, and a player clan party is `IsLordParty` and not `IsMainParty`. | Gating / scope | I tested "player" as a single axis (main party) instead of enumerating the ways a party can belong to the player. `LordPartyComponent` itself branches on `owner.Clan == Clan.PlayerClan`, so the engine told me there were two cases and I read past it. | Added `isPlayerClan` to the pure predicate plus a named regression test. Lesson: when excluding "the player", enumerate every party ownership relation, not just the controlled party. |
| 2 | MED | I asserted heavy cultures settle at half their spawn and published a "Heavy roster" doc column derived from an assumed all-weight-2.0 roster. Measured: Mordor averages **1.20**, Isengard/Goblin/Rohan exactly **1.00**, only Rivendell is genuinely heavy at 1.93. | Fabricated derivation | The number came from arithmetic on an assumption ("orc cultures are weight-2 troops") rather than from parsing `troop_weights.xml` against the templates. It was internally consistent, which made it feel verified. | Replaced with measured per-culture retention parsed from the shipped data. Lesson below: a derived number is only as good as its least-verified input, and "obviously heavy faction" is an input. |
| 3 | MED | `EnableAiPartyScaling`'s MCM hint promised "Off = vanilla caps". The TroopWeight elite tax is a separate toggle, default on, and still deflates the limit. | User-facing promise | I wrote the hint describing my own feature's scope, not the observable state of the party-size limit, which two independent toggles contribute to. | Hint rewritten to say it restores pre-feature behaviour and to name the other toggle. |
| 4 | MED | A career `PartySize` passive is amplified inside a player-owned garrison. `CareerPassiveService.ApplyFlat` uses a base-frame `Add`, so the new 3x garrison factor multiplies it: +25 becomes about +80. | Cross-feature frame interaction | This feature's own `AddResultFrameBonus` exists precisely to avoid base-frame amplification, and I applied that reasoning only to my own flat knob, never asking which OTHER flat adds share the same `ExplainedNumber`. | FIXED on user instruction after the review: a perk must be worth what its message states. `ApplyFlat` now divides the factor frame back out. The feared blast radius was nil: only two call sites exist, and the Health one runs on a factor-free `ExplainedNumber` (vanilla `MaxHitpoints` uses only `Add`), so the conversion is a no-op there. My deferral reasoning was wrong because I estimated the blast radius instead of grepping it. |
| 5 | LOW | Redundant `TaomSettings.Instance` reads, up to three per scaling call. | Code quality | Not a performance problem (`Instance` is an O(1) cached lookup) and not worth churn during a review gate. | Accepted, no change. |
| 6 | LOW | No unit test covers the `IsEnabled()` master-toggle branch on the four public instance methods; only the pure statics are tested. | Test seam | The instance methods take sealed `PartyBase`/`MobileParty` and read a static MCM singleton, so there is no seam to inject through without restructuring. | Accepted and stated in the feature doc. A seam would be the fix if this feature grows. |

## Process finding (not a code defect)

While editing, I used Python `io.open(..., encoding='utf-8-sig')` for round-trip edits. That writes a
BOM unconditionally and, combined with default universal-newline reads, silently converted 11 files
from CRLF to LF and added BOMs to files that had none. Caught by `tools/lint_docs.py` flagging line 1
of a file I had never edited, then confirmed with `git ls-files --eol`.

`.claude/rules/moduledata-validation.md` already specifies the binary round-trip idiom for TAOM's XML
tooling. I did not apply it because I was editing markdown and C# rather than ModuleData XML, and
read the rule as scoped to the latter. It is not: the hazard is the I/O idiom, not the file type.

## Root-cause pattern

Findings 1, 2 and 4 share one shape: **I reasoned correctly about the case I had in mind and never
enumerated the sibling cases.**

- Finding 1: reasoned about "the player" and enumerated one of two ownership relations.
- Finding 2: reasoned about "heavy cultures" and never enumerated what the rosters actually contain.
- Finding 4: reasoned about frame amplification for my own flat knob and never enumerated the other
  flat adds landing on the same `ExplainedNumber`.

In each case the correct answer was one grep or one parse away, and in each case the reasoning was
self-consistent enough that it did not feel like a guess. Self-consistency is not verification.

## Why each agent missed these

- **Standards (Agent 1):** correctly scoped to ADR compliance. The gate was well-formed code that
  delegated properly; nothing about it violates an ADR. Finding 1 is a specification error, not a
  standards error, and this agent cannot see specification errors.
- **API compatibility (Agent 2):** verified 8/8 signatures and actually caught two useful engine
  facts I had not asked for (the garrison virtual-dispatch chain, and vanilla's `LimitMax(-0.01f)` on
  food consumption). It has no view on which parties SHOULD be scaled.
- **Efficiency (Agent 3):** found the one LOW and correctly verified `PartySizeLimit` is cached
  against `MemberRoster.VersionNo`. Out of scope for all six.
- **Completeness (Agent 4):** reported COMPLETE, and was right that every artifact exists. It also
  reported 31 tests when the suite held 27 at the time, which is a reminder that agent-reported
  counts are claims. Worth recording that I then copied that inflated 31 straight into the feature
  doc, so the bad number survived one more hop before a second agent caught it. The suite is now 28
  after the player-clan regression test, plus 4 in `CareerPassiveServiceTests`. It cannot judge whether a passing test suite tests the right dimension, which is
  exactly what finding 1 was.
- **Data flow (Agent 5):** the highest-yield agent, and it independently found findings 3 and 4 and
  challenged my weight premise in finding 2. It did NOT find finding 1: it traced the gate's
  consistency across call sites rather than asking whether the gate's predicate was the right
  predicate. Notably its own weight numbers (1.10 average, 9.6% heavy) also disagreed with the
  measured 1.20 and 12.9%, so its correction needed correcting too.
- **The user found finding 1**, by asking whether the player's party was affected. A one-sentence
  question about scope beat five agents on the highest-severity issue in the changeset.

## Lessons to codify

1. **When excluding "the player" from a campaign feature, enumerate ownership relations.** At minimum:
   the main party, parties owned by the player's clan, garrisons of player-owned settlements, and
   caravans owned by the player. Testing only `IsMainParty` is the trap; vanilla's own
   `LordPartyComponent` distinguishes the first two and will tell you so if you read it.
2. **A flat `Add` on a shared `ExplainedNumber` is amplified by every factor any contributor adds.**
   Before introducing a large factor into an existing chain, grep every other contributor to that
   same `ExplainedNumber` for base-frame adds and state what the new factor does to each.
3. **The binary round-trip I/O idiom is about the idiom, not the file type.** `encoding='utf-8-sig'`
   on write always emits a BOM, and a default-newline read flattens CRLF. Use byte-level round-trip
   for any tracked file, not only ModuleData XML.

## Second review pass (same day), and what it left open

Three agents re-reviewed after the player-clan fix and the `ApplyFlat` frame fix. Fifteen data-flow
traces, all connected, no new defects. Two things are worth recording.

**The timing risk I was most worried about is not a risk, for a better reason than expected.** The
player-clan exclusion reads `MobileParty.ActualClan`, so the obvious hazard was a party whose size
limit gets queried before `ActualClan` is assigned. It cannot happen:
`DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` never calls
`GetPartyMemberSizeLimit` at all (it fills stacks from the template ratio), and
`LordPartyComponent.OnMobilePartySetOnCreation` assigns `ActualClan` before
`InitializeLordPartyProperties` runs. There is a genuine window on the promotion path
(`MobileParty.SetPartyComponent` sets `IsLordParty` before `_partyComponent.Create` sets
`ActualClan`), but the only thing running inside it is `AfterPartyComponentChanged`, which re-sorts an
internal bucket and reads no size limit.

**A standards finding I pushed back on rather than actioned.** Pass 2 flagged `IsPlayerClanParty`
reading static engine state (`Campaign.Current`, `Clan.PlayerClan`) from inside a service as an
ADR-007 breach, and recommended hoisting the resolution to the three model call sites. Declined, with
evidence: `Clan.PlayerClan` / `Hero.MainHero` already appear at 33 service call sites in this repo and
`Campaign.Current` at 12, so the pattern is established rather than novel. More importantly the
proposed shape is worse: three callers each computing `isPlayerClan` inline means any caller that
forgets silently reintroduces exactly the bug finding 1 was. Keeping the rule inside the service means
it cannot be forgotten by a caller. The decision the rule encodes is unit-tested through the pure
predicate; only the three-line null-guarded comparison is not.

### Follow-ups deliberately not taken in this changeset

- **`StartupResourcesConfigProvider.ParseGold` has no upper bound**, while its sibling
  `ParsePlayerGold` caps at 10,000,000. `.claude/rules/csharp-architecture.md` ("Config Providers MUST
  Validate") wants one. This changeset nearly doubled the shipped gold values (max 580,000), which is
  nowhere near the int ceiling, so nothing is tripped: it is pre-existing, not deepened. A bound
  mirroring `PlayerGoldMaxValue` is the obvious small fix.
- **`IsPlayerClanParty` itself has no test**, only the pure predicate it feeds. It touches three
  sealed engine reads; the `Campaign.Current == null` branch (main menu, custom battle) has no
  coverage.
- **Nothing pins the `ActualClan`-before-first-query ordering** verified above. It is an engine fact
  established by decompile, and a future engine version could reorder it without any test noticing.
- **No test exercises `ApplyFlat` with `PassiveEffectType.Health`.** The no-factor test stands in for
  that call site using `PartySize`, which is safe because `GetPassiveMagnitude` is generic over the
  type, but the suite never touches the Health path directly.
- **No test runs the composed model chain** (feats plus garrison scaling plus career passive
  together). The 3.2-factor scenario is asserted by unit arithmetic, never by the real chain. This is
  consistent with `gamemodels.md` rule 8, so it is accepted architecture rather than a defect.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
