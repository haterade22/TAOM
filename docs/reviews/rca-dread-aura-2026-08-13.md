# RCA: DreadAura deep review, 2026-08-13

**Feature:** Aura of Unnatural Dread (`Main/Features/DreadAura/`), reviewed before its first commit.
**Review:** 5 parallel deep-review agents (Standards, API Compatibility, Efficiency, Completeness,
Cross-System Data Flow).
**Outcome:** 8 confirmed findings (1 HIGH, 1 MED-HIGH, 1 MED, 5 LOW) + 1 disputed. All fixed in the
same session. Suite 6,615 green.

## Top-line

Three of the eight findings are one root cause wearing three costumes: **the feature was designed as
"drain while running" and never asked what a PAUSE means.** Every skip path (MCM toggled off, mission
gate ineligible, a stall) was written as "stop doing work", and nobody asked what the skipped
*time* and the skipped *events* mean when work resumes. That produced an unbounded catch-up burst,
a permanently-unregistered wraith, and a slider that only affected future spawns.

The single highest-value agent was Cross-System Data Flow, which found all three plus a doc-count
drift in a file nobody had touched. The four per-file agents found one real defect between them (an
engine-behaviour claim in a comment). That matches the skill's own claim about which agent earns its
keep, and is worth restating: **the bugs were in the seams, not the files.**

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | **HIGH** | `DreadPulseScheduler` reported `elapsed = now - LastPulseTime` with no upper bound. Any window where the aura was skipped froze the stamp while the mission clock ran, and the whole window arrived as one pulse. Toggling the MCM off and back on mid-battle drained every enemy in radius from full morale to zero in a single frame. | Time integration / resume semantics | The per-source elapsed integration was designed to make the drain rate-exact under round-robin scheduling, which is correct. Nobody asked what elapsed means after a *skip* rather than a *reschedule*. Two tests exercised elapsed (0.25s, 6s) and both pinned the unbounded pass-through as intended. | Catch-up ceiling of `max(interval, 1s)` in `SelectDue`, mutation-verified (removing it reddens 2 tests). New lesson below. |
| 2 | MED-HIGH | `DreadRegistry.ResolveSource` folded the master toggle, and its answer gated `DreadSourceTracker`'s insertion into `_sources`, which is a state transition. A wraith spawning while the feature was off was never registered, and re-enabling did not rearm the one-shot mission scan, so it projected no dread for the rest of the battle. | Toggle gating a state transition | **Repeat offender.** `.claude/rules/harmony-patches.md` "Latches & Toggle Gates" already forbids exactly this, but its text and its examples are about *latch flags*. This instance was an *identity lookup* whose answer happened to gate registration, so the rule did not pattern-match at authoring time. `DreadRegistryTests.ResolveSource_Disabled_IsNotSource` pinned the defect as intended behaviour. | Toggle removed from `IDreadRegistry` entirely; identity is now toggle-independent by construction (the class has no settings reference to consult). Test replaced with one that pins the absence. Rule scope widened below. |
| 3 | MED | Radius and rate were snapshotted into a `DreadSourceProfile` at registration and read from there forever, so the MCM sliders only affected wraiths that spawned *after* a change. A code comment asserted the opposite ("an in-game retune takes effect without a rebuild"). | Snapshot vs live read | The snapshot was introduced for a speculative feature (per-source aura rows) that v1 does not have, since all sources share one profile. The comment was written from intent, not from tracing the read path. | `DreadSourceProfile` deleted; `DreadPulseRunner` reads geometry live from `IDreadAuraSettingsProvider` once per pulse. |
| 4 | LOW | `DreadAgentGate`'s doc comment justified the gate by claiming `AgentComponentExtensions.SetMorale` "dereferences with no null-conditional, so the player would NRE here." The installed engine null-checks and silently no-ops. | False engine claim in a comment | I wrote the comment from a research subagent's summary instead of reading `AgentComponentExtensions` myself. `evidence-over-claims.md` §A4 names this exact failure. The API-compatibility agent caught it. | Comment rewritten from the decompiled body. The gate's *actual* load-bearing clauses (`!IsMount`, `agent != null`) are now stated with their real reasons. |
| 5 | LOW | `DreadAuraConfigProvider` carried a comment describing an `AgentProximityMap.CanSearchRadius` probe "which the MissionLogic performs at mission start". That mechanism was designed in, then deliberately removed. | Stale comment | The removal was recorded in the feature doc's "Rejected seams" but not propagated back to the comment that motivated the constant. | Comment rewritten to say why the ceiling is a design choice, not an engine one. |
| 6 | LOW | `docs/features/bannerlord-together-compat.md` pinned "167 MCM settings, 112 simulation-relevant": stale before this change and made staler by it. | Cross-doc count drift | `SettingsFingerprintTests` pins the counts in `coop-interop.md`'s numbers, and I updated that file. Nothing pins the *second* doc that quotes the same figures, and `lint_docs.py` cannot see semantic drift. | Counts synced. Noted below as a known gap: two docs quote one number and only one is test-pinned. |
| 7 | LOW | The balance table's `Tier-5 dwarf / 113 s` row was the only row no test derived. | Untested doc claim | The golden-number `[DataRow]` set covered the three human tiers and the two elf cases; the dwarf row was written from the same arithmetic but never simulated. | `ComputeDrain_DwarfVeteran_RoutsAtTheDocumentedTime` added. All six rows now derived by a test. |
| 8 | LOW | `DreadSourceTracker` had zero direct tests despite holding real branching logic, and it is where findings 2 and 3 lived. | Coverage gap | The Completeness agent rated it "80%+, tracker tested indirectly". Indirect coverage of a class's *collaborators* is not coverage of the class. | `DreadSourceTrackerTests` added (7 tests) for everything reachable without a live `Agent`. Agent-prompt gap noted below. |
| 9 | DISPUTED | Standards agent reported `DreadAuraMissionLogic.cs` at 151 lines, one over the ADR-002 ceiling. | Miscount | Two independent counts (`wc -l`, `grep -c ''`) give 150. The agent likely counted a phantom trailing line. | No change. Recorded so the next reviewer does not re-raise it. |

## Root-cause pattern: skip paths need a resume contract

Findings 1, 2 and 3 are all the same omission. The feature has three ways to stop doing work
(master toggle, mission-gate ineligibility, no sources) and each was implemented as a bare early
return. None of them asked the two questions that a resume needs answered:

- **What does the elapsed time across the gap mean?** (finding 1: it meant a lethal burst)
- **What events did we miss while stopped, and are they recoverable?** (finding 2: a spawn, and no)
- **What did we cache before stopping that is now stale?** (finding 3: the geometry)

A feature that integrates a rate over time and can be interrupted is a state machine, not a filter,
and the interruption is a state.

## Why each agent missed what it missed

| Agent | Found | Why it missed the rest |
|---|---|---|
| **Standards** | Nothing real (one miscount) | Its rule set is ADRs, naming, registration, service-locator, MCM group ordering. Nothing in it asks about temporal semantics. Correctly out of scope. |
| **API Compatibility** | Finding 4 | It verified every signature and answered five behavioural questions from decompiled bodies, exactly its job, and it caught the one place I had asserted engine behaviour without reading it. It does not look at TAOM-side control flow. |
| **Efficiency** | Nothing | It costed the tick correctly and confirmed the steady state is allocation-free. The *magnitude* of `elapsed` is not a performance question; a 40-second elapsed costs the same as a 0.25-second one. |
| **Completeness** | Nothing; **actively wrong on finding 8** | It rated `DreadSourceTracker` "80%+ tested indirectly" and passed the feature as complete. Its prompt asks whether tests *exist* per class, and it accepted collaborator coverage as satisfying that. |
| **Data Flow** | Findings 1, 2, 3, 6 | Its prompt explicitly asks for parallel-entry-point consistency, latch closer coverage, and NaN polarity on engine-sourced floats: and `Mission.CurrentTime` is an engine-sourced float, which is how it reached finding 1 from the NaN-polarity check. |

## Preventive actions

1. **New lesson (State/Lifecycle/Save):** a rate integrated over elapsed time must bound its
   catch-up. Appended to `docs/reviews/lessons/state-lifecycle-save.md`.
2. **Widen the "toggles gate I/O, never state transitions" rule** beyond latch flags to cover any
   predicate whose answer gates a state transition, including identity and eligibility lookups.
   Appended to the same lessons file, cross-referencing
   `rca-tournament-exit-hang-2026-07-06.md` as the first instance.
3. **Completeness-agent prompt fix:** "tested indirectly" must not satisfy the per-class test check.
   The agent should name the test FILE for each non-entry-point class or report the class as
   uncovered. Recorded in `docs/reviews/lessons/testing-qa.md`.
4. **No new rule for finding 4.** `evidence-over-claims.md` §A4 already covers it and the process
   worked: an independent agent caught it. The lesson is that the rule applies to *code comments*,
   not only to user-facing summaries, which is worth one line in the testing-qa entry.

## The gap finding 6 exposed, now closed

`docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` both quote the
MCM settings counts, and only the first was pinned by `SettingsFingerprintTests`, so the second sat
on "167 settings, 112 simulation-relevant" while the first said 173/116. The two contradicted each
other inside a sentence that links one to the other, and nothing could catch it: `lint_docs.py` sees
formatting, not semantics.

`SettingsFingerprintTests.EveryDocQuotingTheSettingsCounts_AgreesWithReflection` now derives both
numbers by reflection and asserts every doc that quotes them agrees. Verified non-vacuous: reverting
either doc to the stale figure reddens it. The next person to add an MCM setting gets told about
both files instead of one.

## The Codex pass did not complete (environment)

An independent Codex adversarial review was dispatched twice and failed both times, for reasons
outside the repo:

1. **Context exhaustion, then a service error.** The first prompt asked Codex to decompile and paste
   seven engine bodies inline. It reached 274,710 tokens against a 272,000-token window, tried
   remote compaction, and got `404 Not Found` from
   `https://chatgpt.com/backend-api/codex/responses/compact`. That prompt was my error and is fixed:
   `docs/reviews/codex-adversarial-dreadaura-2026-08-13.prompt.md` now opens with an explicit
   context budget capping files read and lines quoted.
2. **Its own shell could not run.** The retry died on
   `InvalidOperation: Cannot set property. Property setting is supported only on core types in this
   language mode` from the PowerShell it drives, plus continuous
   `codex_models_manager: unknown variant 'max'` errors (codex-cli 0.128.0 against a server
   advertising a reasoning level the CLI does not know). Both are environment, not repo, so per
   `.claude/rules/environment-failures.md` they were reported rather than worked around.

Neither run produced a review. **The feature therefore shipped on the 5-agent internal review
alone.** The lean prompt is committed and ready to re-run once the Codex CLI is updated; the
questions it asks (thread safety against `OnTickParallel`, `SetRouted` bleed into the post-battle
roster, mission-type coverage, and the balance judgement) are the ones still owed an independent
opinion.

## Codex pass, second attempt: it ran, and it found three things

After updating codex-cli from 0.128.0 to 0.147.0 (the `unknown variant 'max'` decode failure was a
CLI-behind-server mismatch) and splitting the review into five single-question sections with stderr
redirected away from the output, all five completed. Outputs were 529 to 781 bytes each, against
11 MB of error log from the first attempt.

| # | Section | Verdict | Disposition |
|---|---|---|---|
| C1 | Thread safety vs `OnTickParallel` | RACE-REAL | **Accepted, not fixed.** Documented in the feature doc. Float stores are atomic on x64 so there is no tearing; the drain integrates from `LastPulseTime` rather than from the previous morale, so a lost update does not accumulate; vanilla writes the same field the same way. The proposed fix needs a "confirmed post-parallel sync point" that nothing identifies, and guessing it wrong is worse than the race. |
| C2 | `SetRouted` campaign bleed | ROSTER-AFFECTING | **Confirmed by reading the engine, documented.** `MapEventParty.OnTroopRouted:320` removes the troop from `Party.MemberRoster` and feeds `DesertersCampaignBehavior`. Heroes, siege defenders and ungrouped origins are exempt. Not a defect in this feature, but a consequence players must be told about, and the feature doc now leads with it. |
| C3 | Mission-type gate | GATE-WRONG | **Fixed.** `Mode == MissionMode.Battle` added. `MissionTeamAIType` is set before deployment, so the allowlist was already true while the player positioned formations. |
| C4 | Adversarial bug hunt | 1 x P2 | **Fixed.** A NaN `dt` poisoned `_timeSinceStart` permanently, and `NaN < WarmupSeconds` is false, so the warm-up gate silently stopped gating. Fourth instance of the NaN-polarity class in this codebase. |
| C5 | Balance | TABLE-CORRECT / OVERPOWERED | Arithmetic independently reproduced, including the elven hero never routing. The OVERPOWERED verdict is a tuning call for the owner, not a defect; `profile.moraleFloor` is the lever and the control battle is the arbiter. |

**C4 is the finding that should sting.** The internal Data Flow agent ran an explicit NaN-polarity
check over every engine-sourced float and enumerated twelve gates in `ComputeDrain` alone, but it
scoped that check to the pure service and the scheduler. It never looked at the entry point, where
`_timeSinceStart += dt` sits above an inverted `<` comparison. The rule the codebase already has
(`csharp-architecture.md`, "Engine-Float Decision Gates") says to check *every* float-to-decision
path in a touched method. The agent checked every path in the methods it considered in scope, and
the scope was wrong.

**Preventive action:** the deep-review Data Flow prompt's NaN check should name the ENTRY POINT
tick methods explicitly, not just services and pure helpers. `dt` is the most obviously
engine-sourced float in any `MissionBehavior`, and it was the one float nobody examined.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
