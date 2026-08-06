# RCA — EconomyDiagnostics + caravan-gate diagnostics (2026-08-06)

Review of the `Patch68_EconomyDiagnostics` changeset: 6 parallel dimension reviewers, then one
adversarial skeptic per deduped finding (instructed to default to refuted), then synthesis.

**Headline: the worst defect was not found by the review at all.** A DryIoc
`UnableToSelectSinglePublicConstructorFromMultiple` crashed the game in `SubModule.OnSubModuleLoad`
— before the main menu — and was reported by the user launching the build while the review ran. The
full suite was green and the build was clean at the moment it crashed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | **CRITICAL** | `TownGoldLedger` had two public ctors → DryIoc throws at `Register`, hard CTD before the main menu | DI wiring | Every test did `new TownGoldLedger(3)` directly. No test ever built a container. The repo's `*WiringTests` assert on **source text** of `IoC.cs`/`SubModule.cs`, which cannot detect a ctor-selection failure | `EconomyDiagnosticsWiringTests` — real `Register` + `Resolve` round-trip, singleton assertion, and a direct guard that every registered type has exactly one public ctor |
| 2 | **HIGH** | Player trade-screen gold lands in `Other`, not `Trade`; four shipped docs claimed otherwise | False doc claim | I traced `SellItemsAction` and stopped. `InventoryScreenHelper.SetGold` (:128) calls `ChangeGold` **directly**, never touching the action I tagged | Enumerate *all* callers of the recorded method, not just the ones the design anticipated. `Other`'s XML doc now names its three verified occupants |
| 3 | **HIGH** | Test class doc asserted the **opposite** of its own boundary tests (40% wounded exclusive vs inclusive) | Stale doc | I corrected the individual test's doc when I found the float-widening behaviour, and never re-read the class-level summary above it | When a discovery inverts an assumption, grep the whole file for the old claim — the fix site is rarely the only statement of it |
| 4 | MEDIUM | `CaravanGate.WoundedUnlikelyToLeave` had zero test coverage; feature doc's gate table omitted it while the doc claimed "one test per gate" | Coverage gap | The gate was introduced as a fall-through *between* two tested branches, so both neighbours passed | Enum-coverage check: every `CaravanGate` value needs a test that produces it |
| 5 | MEDIUM | `CaravanGate` missed `ShortTermBehavior == Hold` — the 2nd clause of the same exit guard I modelled `AiDisabled` from | Incomplete engine model | I read the guard, took the first disjunct, and moved on | When modelling a compound engine guard, enumerate **every disjunct** and decide explicitly which are in scope |
| 6 | MEDIUM | "six targets" (there are five) and "Six binding tests" (there are four) in four docs; "seven gates" in three docs (the enum has ten blocking values) | Fabricated counts | The design had five tag sites; I implemented four and never re-counted. The "seven" came from the plan's 7-cause table and survived the enum growing to 11 | Never restate a count from memory of the design. Count the artifact at write time |
| 7 | MEDIUM | `TownGoldFlow.Workshop` was dead — nothing could ever set it | Dead code | Same root cause as #6: the dropped fifth tag site left its enum member behind | Deleted (simplicity criterion — deletion that holds parity) |
| 8 | MEDIUM | `Patch68` applied unguarded mid-chain; a throw would abort `Patch30_MixedFormations` and everything after | Blast radius | I followed the *placement* of `Patch59` without following the *guarded* idiom that `Patch60/61/62/63` use for non-gameplay categories | A DIAGNOSTIC-ONLY category must always be try/caught — it can never be allowed to disable gameplay patches |
| 9 | LOW | `IoC.Resolve` ran before the `IsTown` early-out | Ordering | Wrote the resolve first out of habit | Early-outs go above lazy resolution |
| 10 | LOW | CLAUDE.md claimed 62 Harmony categories; the registry has 73 | Stale count | Pre-existing drift, surfaced by this review | Corrected; `lint_docs.py` could derive it |

Findings the skeptics **refuted**, recorded so they are not re-litigated:

- *"`CaravanGateDiagnosticsService` has no interface and is `new`ed — no repo precedent."* The
  supporting search (`rg "new [A-Z][A-Za-z]*Service\("`) filtered by name suffix and was therefore
  circular. Not a violation; left as-is.
- *"`_ledger` static survives module unload."* `OnSubModuleUnloaded` is process teardown. I had
  already added the reset mirroring `CrashReportPatchHelper`; it is harmless but was not required.
- *"The daily-tick roll smears attribution across towns."* My own suspicion, raised in the plan and
  **disproved**: `_campaignPeriodicEventManager.OnTick` runs before the daily-tick dispatch, so
  `DailyTickEvent` is the correct roll point. Recorded so it is not re-investigated.

Two skeptics disagreed on whether `Hold` is persistent. Resolved directly against the engine:
`Hold` is not in `HourlyTickParty`'s early-return set (:625), so it outlives the siege by a few
hours and then self-clears on the next re-decide — permanent only when the caravan is *also* trapped
by a zero trade score. Both the verdict text and the gate table now say exactly that.

## Root-cause pattern: I verified the parts I designed, not the seams I inherited

Findings 1, 2, 5 and 8 are one failure. In each case I built a component carefully, tested its
internals thoroughly, and did not exercise the boundary where it meets something I did not write:

| Component | Verified | Not verified |
|---|---|---|
| `TownGoldLedger` | logic, ring bounds, blank/zero rejection | that DryIoc can construct it |
| Flow tagging | outermost-wins semantics, tag scope | that all callers of `ChangeGold` route through a tagged entry |
| `CaravanGate` | precedence, the wounded ladder | that the enum covers every disjunct of the engine's guard |
| `Patch68` | discovery, binding, target resolution | what a throw does to the categories applied after it |

The tell is that I *did* verify one seam — Harmony's discovery of the nested patch classes — because
I had flagged it as unprecedented and therefore risky. The seams I skipped were the ones that felt
routine. **Novelty is a bad predictor of risk; the boring seam that runs first (DI) took the game
down, while the exotic one I worried about was fine.**

## Why each reviewer missed finding #1

Worth stating plainly, since six reviewers and twelve skeptics all missed the only crash:

- **Standards** checked that registrations *exist* in `IoC.cs`, not that they *resolve*.
- **Completeness** checked that test files exist per class; a container test is per-*feature*, so
  nothing was reported absent.
- **API compatibility** scoped to TaleWorlds signatures; DryIoc is a third-party library.
- **Performance / Data flow / Lifecycle** all reason about code that is already running.

The gap is structural: no dimension owned "does this feature survive startup". The prompt for a
future review of any IoC-registered feature should include *"build a real container, register the
feature, resolve every registration"* as an explicit check.

## Preventive actions taken

1. `EconomyDiagnosticsWiringTests` — the container round-trip, plus a one-public-constructor
   invariant over every registered type.
2. `EconomyDiagnosticsPatchDiscoveryTests` — proves Harmony's own enumerator finds the nested patch
   classes (written pre-emptively; it passed, but the failure mode was silent-dead patches).
3. `Patch68` now applies inside a try/catch like the other diagnostic categories.
4. Doc counts corrected and, where the number will keep moving (gate count), stated **once** in the
   feature doc's table rather than restated in four places.

## Lesson to codify

Appended to `docs/reviews/lessons/build-tooling-workflow.md`:

> **A feature is not verified until a real container has resolved it.** Unit tests that `new` the
> service directly, and wiring tests that assert on `IoC.cs` source text, both pass while DryIoc
> cannot construct the type. DryIoc validates constructor selection at `Register` time, so the
> failure is a hard CTD in `OnSubModuleLoad` — before the main menu, with a green suite and a clean
> build. Every IoC-registered feature needs one test that builds a container, registers the feature,
> and resolves each registration. **Why missed:** all six deep-review dimensions reason about code
> that is already running; none owned "does this survive startup." **Source:**
> `docs/reviews/rca-economy-diagnostics-2026-08-06.md`.
