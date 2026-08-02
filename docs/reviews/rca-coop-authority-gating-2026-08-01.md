# RCA — co-op authority gating (BannerlordCoop interop), 2026-08-01

**Scope:** the change set that taught TAOM to detect BannerlordCoop (module id `Coop`), removed five
colliding assembly redirects, and added host-authority gating so a co-op client stops running
world-mutating campaign logic.

**Review shape:** two passes on the same change set the same day — 5 parallel deep-review agents
(Standards, Compatibility, Efficiency, Completeness, Data Flow), then a Codex adversarial pass.

*Pass 1 (5 agents), 8 findings:* 2 HIGH confirmed and fixed, 1 HIGH (test gap) confirmed and fixed,
1 MEDIUM confirmed and fixed, 1 MEDIUM raised as a design asymmetry (siege rewards — later fixed
properly, see the Codex section), 1 CRITICAL refuted, 2 MEDIUM fixes rejected as incorrect.

*Pass 2 (Codex `gpt-5.5` xhigh), 8 findings:* 3 HIGH + 2 MEDIUM fixed, 1 HIGH false positive, 1 HIGH
already fixed, 1 MEDIUM left open as a design decision.

**Suite green at 4,759 after both passes.** Commits `0b76d56e` (layer) and `46ce6436` (Codex fixes).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `WarOfTheRingBehavior.OnSessionLaunched` called `CheckPhaseTransition` (→ `DeclareWar`) ungated — the identical call `OnDailyTick` guards | Data flow | **Gated by event, not by mutation.** I enumerated global-tick subscribers and gated those, never asking "what else reaches this service method?" | Rule below + feature-doc instruction to grep the whole `RegisterEvents` |
| 2 | HIGH | `WarOfTheRingMomentumBehavior` — 6 of 8 handlers ungated, incl. `OnKingdomDestroyed` → `CheckAndApplyVictory` → `EndWar`/`MakePeace` | Data flow | Same root cause as #1 | Same |
| 3 | HIGH | 6 of 7 gates had no test proving a client stands down | Test coverage | Wrote the gate and the policy test; assumed the one-line gate was self-evident | Added `CoopAuthorityGateTests` (7 tests); handlers widened to `internal` per the `RaceAgeBehavior` precedent |
| 4 | MED | `CoopSessionProvider.EnsureBound` had an unsynchronised `if (_bound) _bound = true` | Concurrency | Wrote it as "benign duplicate work" without tracing the intermediate state | Fixed with double-checked lock + `volatile`; see below — the consequence was worse than duplicate work |
| 5 | MED | `SiegeDefenseBehavior`'s gate silently disables siege-defence rewards for clients forever, inconsistent with `CareerQuestCampaignBehavior` | Design asymmetry | Gated the whole handler because it mutates shared state, without noticing the reward is per-player | **Fixed** — `OnHourlyTickShared` (authority) / `OnHourlyTickLocalPlayer` (every peer). Codex then found the split leaked save-backed state; see C4 below |
| 6 | MED | Momentum's `_coopSession` field declared after the constructor | Style | Mechanical Python edit inserted at the wrong anchor | Moved; no rule needed |
| 7 | — | **REFUTED:** "field after ctor will not compile" (rated CRITICAL) | Agent error | — | C# has no field-declaration-order rule. `Build succeeded` disproved it. Verify agent claims against the build before acting |
| 8 | — | **REJECTED (2):** cache `CoopPresence.IsActive`; share a static `object[]` for `Invoke` | Agent error | — | The first breaks `Refresh()` (freezes a possibly-wrong first probe); the second is a data race — `TryGetContainer(out …)` writes back into the array |

## Root-cause pattern: gating the EVENT instead of the MUTATION

Findings #1 and #2 are one bug, twice. The reasoning that produced them was sound but scoped one
level too shallow:

1. Establish that BannerlordCoop leaves the *global* tick events firing on a client.
2. Enumerate every TAOM behaviour subscribing to a global tick event.
3. Gate those handlers.

Step 2 is the defect. It enumerates **entry points of one kind** rather than **paths to the
mutation**. `WarOfTheRingBehavior` subscribes to `OnSessionLaunchedEvent` as well as
`DailyTickEvent`, and both call `CheckPhaseTransition`. `WarOfTheRingMomentumBehavior` subscribes to
eight events, six of which reach `MomentumWarState` and one of which reaches the same
`CheckAndApplyVictory` → `EndWar`/`MakePeace` the tick gate was written to protect.

The tell was visible and I walked past it: I *read* `RegisterEvents` in every one of these files while
adding the constructor parameter, and gated only the line I had come for.

**Rule (added to `docs/reviews/lessons/campaign-mechanics.md`):** when gating a handler for co-op
authority, gate the **service method**, not the event. Enumerate every handler in the behaviour's
`RegisterEvents` and follow each to its service calls; any handler reaching the same mutating method
needs the same gate. Where practical, prefer gating inside the service so there is one chokepoint
instead of N.

## Secondary pattern: "benign race" was not benign (finding #4)

The initial `if (_bound) return; _bound = true;` was dismissed — by me and by the Standards agent —
as at worst duplicated reflection work. Tracing the intermediate state showed otherwise: the binder
deliberately **nulls** `_tryGetContainer` when the role property is missing, precisely so an
unreadable role keeps the peer authoritative. A second thread observing the window between "method
bound" and "null-out applied" reads `sessionActive=true` with `isServer` defaulting false, yielding
`IsAuthority=false` — standing TAOM down on exactly the input the fail-open design promises to
survive.

**Lesson:** "benign race" is a conclusion, not an assumption. When a field is written more than once
inside an initialiser, enumerate what a reader sees *between* the writes — especially when the later
write exists to enforce a safety property.

## Why each agent missed what it missed

- **Standards** — found the field-order nit and the race, but manufactured a CRITICAL compile error
  that the build disproves. It did not check its own claim against a build it could have run.
- **Compatibility** — the strongest result: independently CONFIRMED the load-bearing engine premise
  (`Campaign.Tick` drives global events via `OnTick`/`SignalPeriodicEvents`, a *separate* call from
  `TickPeriodicEvents`) and correctly flagged that Coop's own `PartyTickPatch` was outside its reach.
  I closed that last link by reading Coop's decompiled patch directly.
- **Efficiency** — correct observations, both proposed fixes wrong. Useful confirmation that the
  solo-play early-out short-circuits before any reflection.
- **Completeness** — caught the test gap (#3) precisely, including that the
  gate-after-`RefreshMapMeter` ordering was unpinned.
- **Data Flow** — found #1 and #2, which nothing else came close to. Every other agent read the same
  files and saw only the gated lines. This remains the highest-value agent, and its value came
  specifically from the brief asking it to trace *non-tick paths into the same mutations*.

## Process note

Three agent findings were wrong (one CRITICAL, two proposed fixes). All three were caught by
verifying against source or the build before acting, per `.claude/rules/evidence-over-claims.md`. A
fourth self-inflicted error — asserting in a test comment that `CampaignTime.Now` does not need a
live `Campaign` — was caught by the test failing, and the comment now records the correction rather
than hiding it.

## Still owed

- ~~In-game verification of the whole layer.~~ **Done 2026-08-02** — a player completed a real
  session. It also surfaced what this RCA listed as open question #2: PatchShield's finalizer tax
  over Coop's AutoSync surface collapsed frame rate. Fixed by skipping install under co-op. What the
  session did NOT confirm: per-gate behaviour, or an object-set audit between peers.
- The `CultureConversion` client crash is source-verified end-to-end but not reproduced at runtime.
- Issue #370's title still says BannerlordTogether; it now tracks BannerlordCoop work too.

---

## Codex adversarial pass (same day, after the 5-agent review)

`gpt-5.5` at `xhigh`, prompt at `docs/reviews/codex-adversarial-coopinterop-2026-08-01.prompt.md`.
Verdict: **4 HIGH, 3 MEDIUM, 1 LOW — not safe to ship.** It DISPUTED all four Known Suspects aimed at
the layer I wrote (fail-open direction, reflection-binder thread safety, the assembly-redirect
deletion, and the detection-coupling tests), and confirmed the two aimed at its edges.

| # | Sev | Finding | Outcome |
|---|---|---|---|
| C1 | HIGH | 8 sites gated on module PRESENCE, disabling TAOM diplomacy for solo-with-Coop-enabled AND the co-op host | **Fixed** — new `ShouldDeferToHost`; see below |
| C2 | HIGH | Momentum `OnSessionLaunched` mutated before its authority check | Already fixed concurrently |
| C3 | HIGH | `DiplomacyBehavior` session launch ungated | **FALSE POSITIVE** — quoted `_ => _service.EnforcePermanentAlliances()`; the real line is `_ => OnSessionLaunched()`, which gates |
| C4 | HIGH | Siege local reward writes save-backed `RewardClaimed` | **Fixed** — client claims go to non-persisted `_locallyClaimed`, gated by `MayWriteSaveBackedState` |
| C5 | MED | `MessengerCampaignBehavior.SendMessenger` charges gold for a messenger the gated tick never delivers | **Fixed** |
| C6 | MED | `CultureConversionBehavior.OnSettlementOwnerChanged` ungated | **Fixed** — I had documented this as safe; it was not |
| C7 | MED | `new CareerQuest(...)` ungated; `QuestBase : MBObjectBase` sets `StringId` in its ctor | **Open** — product decision, gating removes career quests from clients |
| C8 | LOW | TimeAcceleration disabled by presence | **Fixed** with C1 |

**C1 is the interesting one: neither position was complete.** Codex's proposed fix (use session/role)
breaks BannerlordTogether, and the code said so in a comment Codex did not engage with — `IsAuthority`
fails open, so both BT peers report authoritative and nothing gates. The presence gate got the solo
and host rows wrong; the authority gate gets the BT row wrong. `ShouldDeferToHost` keys on whether the
ROLE PROBE RESOLVED, which satisfies all five rows, and is pinned by a test per row.

**C6 is a correction to my own reasoning.** I had gated the tick and left the owner-change handler
alone, documenting that it "only queues a pending timer, so it stays ungated". The store is
SyncData-backed and the daily processor that maintains it IS gated, so a client accumulates pending
conversions nothing services. Writing the justification into the doc made it look considered.

**Process note:** three of eight findings needed correction before use (one false positive, one
already-fixed, one whose proposed fix would have caused a different regression). Verifying each
against source before implementing — per `.claude/rules/evidence-over-claims.md` — is what caught
all three.
