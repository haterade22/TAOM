# RCA — career effect layer audit (Health + TroopDamage), 2026-08-06

**Scope:** two career-effect wiring bugs of the same class, the balance retunes that followed, and
the 6-agent deep review of that changeset. Filed under the #388 career arc — see "Issue numbering"
below for why, which is itself one of the findings.

## Top-line

A player reported that a "+75 max health" career choice did nothing to their character sheet.
`PassiveEffectType.Health` had a live consumer, correct units, and a green phantom gate — and was
still broken, because its only consumer sat on the **mission** layer while every surface the player
reads sits on the **campaign** layer. Fixing it exposed the same class a second time:
`TroopDamage` (105 pips, the second-most-used effect) promised battle performance while its only
consumer was `TaomRaidModel.CalculateHitDamage` — village burn speed.

The deep review then found a third, different bug in the *tool* written to fix the first two: the
retune script's idempotency reasoning was unsound, and a second `--apply` would have silently
double-shifted 71 of 105 pips across four data surfaces.

Nothing shipped broken. Every finding was caught before commit. But the first audit I ran **missed
the second bug**, and the reason is the interesting part.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `Health` consumed only by `TaomAgentStatCalculateModel` (mission). Character sheet, `Hero.MaxHitPoints`, daily heal cap and wounded threshold all read `CharacterStatsModel.MaxHitpoints`, which nothing touched. Pip invisible + inert outside battle | Wrong layer | `PassiveEffectConsumers` answers "is anything reading it", never "where". Gate green for the feature's whole life | `already_applied`-style layer column in the canonical table; `PassiveEffectConsumers` header now states the blind spot; lesson in `lessons/gamemodels-services.md` |
| 2 | HIGH | `TroopDamage` consumed only by `TaomRaidModel.CalculateHitDamage` — settlement raid progress, not combat. 105 pips promising "your troops smash through enemy lines" inert in every battle | Wrong layer (2nd instance) | **My own post-fix audit used the same lens as the gate** — "does a read site exist, are the units right". Both checks pass for `TroopDamage`. See root-cause section | The lens is now written down as *description vs consumer scope*, with both instances tabulated |
| 3 | CRITICAL | `tools/retune_career_health.py` not idempotent for `troopdamage`: mapping keys ∩ values = {0.03,0.05,0.06,0.08}, so `process_choices`' per-pip "already retuned" branch is unreachable for them. A second `--apply` would re-shift 71/105 pips + descriptions + source strings + 12 languages + 12 caches | Silent data corruption | I wrote the per-pip guard for `health` (disjoint sets, works), then added a second profile without re-checking that the guard's *precondition* still held | `already_applied()` decides at file level; `overlapping_values()` warns; 14 unit tests pin it for every profile |
| 4 | HIGH | `CareerPassiveService.GetPassiveMagnitude` interpolated a `LogDebug` string on every non-zero lookup. Eager interpolation + no level gate in `FileLogger` + `DateTime.Now.ToString` + second interpolation + unbounded queue ≈ 4 allocations per call | Hot-path allocation | Pre-existing and harmless when lookups were rare. **My change made it hot** — `MaxHitpoints` per agent spawn/tooltip/heal tick, and `TroopDamage` per blow per troop | Line removed; comment records why it must not come back. Cache contents still logged by `RefreshCache` |
| 5 | MED | Retune tool writes no `.bak`, contrary to the MANDATORY clause in `tools/README.md` | Convention deviation | Sibling scripts that mutate the LIVE game install need `.bak`; every target here is git-tracked | Deviation **recorded** in the docstring + README rather than silently taken |
| 6 | MED | Referenced issues `#390`/`#391` that I never verified. `#390` is the armory meta-mesh issue; `#391` did not exist | Fabrication | Invented a plausible next number instead of running `gh issue list` | 24 references corrected to #388; `gh` verified highest real issue is 390 |
| 7 | LOW | Doc named `HorseChargeDamage`/`HorseHealth`/`HealthRegeneration` — enum values that no longer exist | Doc rot | Pre-existing; found while building the canonical table | Corrected, with a note recording the rename |

## Root-cause pattern: a sufficient-looking check that is only necessary

Findings 1 and 2 are the same bug, and finding 3 is its structural twin one level up.

`PassiveEffectConsumers` is a compiled set with a load-time gate and a shipped-XML regression test.
It is genuinely good engineering, and it answers exactly one question: *is any code reading this
magnitude?* That question is **necessary but not sufficient**, and the set's own name and the
surrounding docs invited it to be read as sufficient.

The failure repeated because after fixing finding 1 I audited the remaining 22 types **using the
gate's own question**. I checked read sites and magnitude units, found both fine everywhere, and
reported all clear. `TroopDamage` passed both checks while being completely broken. The question
that finds this class is different and slightly awkward to mechanise:

> Read the pip's own description. Read the consumer's method body — not its name. Do they describe
> the same system?

`TaomRaidModel` *sounds* like it governs troops fighting. It governs how fast a settlement's hit
points drain. One decompile answers it; no amount of read-site analysis does.

Finding 3 is the same shape: a guard (`if old_value not in MAPPING`) that is correct **only under a
precondition** (old and new value sets disjoint) which held for the profile it was written for and
silently stopped holding when a second profile arrived. In both cases the check was right about the
thing it tested and wrong about the thing it implied.

**Generalisation now recorded:** when a mechanism exists to prove something is wired, write down
what it does *not* prove, next to the mechanism. Both `PassiveEffectConsumers` and
`retune_career_health.py` now carry that note in their headers.

## Why each deep-review agent missed what it missed

The review ran 6 agents (5 core + the mandated tooling agent, since the changeset adds a
file-writing Python tool).

| Agent | Result | Assessment |
|---|---|---|
| Standards | ALL PASSED | Correct. Verified `gamemodels.md` rule 4 on all three override bodies, confirmed the new private static extractor matches the existing victim-side precedent |
| API compatibility | 6 verified, 0 incompatible | **Highest-value pass.** Decompiled the installed v1.4.7 DLLs and CONFIRMED the chain the whole fix rests on (`GetEffectiveMaxHealth` hero branch → `CharacterObject.MaxHitPoints()` → `CharacterStatsModel.MaxHitpoints`). Also proved the two models register in lockstep, closing the "hero silently gets zero bonus" risk |
| Efficiency | NO ISSUES FOUND | **Missed finding 4, having looked directly at it.** Called the `LogDebug` "conditional, async, doesn't block" — true of the *write*, false of the allocation. The agent reasoned about blocking and stopped; it did not ask whether the string is built regardless, nor read `FileLogger` for a level gate. Its lock-contention analysis was excellent, which is likely why the adjacent line got a pass |
| Completeness | COMPLETE | Correct, and usefully verified the regression pins actually bite |
| Data flow | 38 flows, 0 gaps | Correct and thorough — decompiled `RaidEventComponent` to prove the two `TroopDamage` consumers are temporally disjoint rather than asserting it. One trivial misstatement (TroopDamage min quoted as 0.03, actually 0.02); changed no conclusion. **Did not look at the tool**, correctly, as that was the tooling agent's scope |
| Tooling correctness | 1 CRITICAL, 1 MED | Found finding 3, which no C#-centric agent could have. Vindicates the Step 2c rule that a file-writing script gets its own agent |

**The pattern worth keeping:** the two findings the agents missed or under-rated (4, and the
severity of it) were both caught by *spot-verifying a subagent's load-bearing claim against the
source* — the `evidence-over-claims.md` §A.4 reflex. An agent report is a hypothesis. The efficiency
agent's "conditional, async" was a confident, plausible, wrong sentence, and reading four lines of
`FileLogger` settled it.

## Issue numbering

Findings 6 exists because I referenced `#390` and `#391` without checking. `gh` was authenticated the
whole time; one command would have caught it. A concurrent session noticed the collision and
renumbered my CHANGELOG entries before I did.

All 24 references now read `#388`. That is accurate but imprecise — #388 is the diamond-screen
issue, and these are two distinct wiring bugs plus a tooling bug that warrant their own issues.
**Dedicated issues are still owed** and are recorded as owed in `docs/features/career-system.md`.

## Lessons codified

- `lessons/gamemodels-services.md` — "A career effect can have a live consumer and still be broken —
  check the LAYER, not the existence", with both instances tabulated.
- `lessons/build-tooling-workflow.md` — the `\r\r\n` language-file trap and the diff-asymmetry
  diagnostic (found mid-session when a text-mode write turned a 164-string edit into a 6,180-line
  whole-file rewrite).
- `lessons/localization-ui.md` — editing an existing English string does not invalidate its cached
  translation; `translate_with_claude.py` keys its cache on `string_id` with no source check.
- **New, from this review** → `lessons/build-tooling-workflow.md`: a value-remapping tool is only
  idempotent if its old and new value sets are disjoint; assert the precondition, or decide at file
  level.

## Verification at close

Build succeeded · suite **5672 passed / 0 failed** · 14 new tool tests pass ·
`validate_moduledata.py` PASS · both retune profiles report ALREADY APPLIED and write nothing ·
shipped data unchanged by the fixes (Health 165 pips in [5,10], TroopDamage 105 in [0.02,0.08]).

**Still owed:** in-game check that army-wide troop damage feels right at 2-8% (the max-health half
was confirmed in-game by the reporter); dedicated GitHub issues; `/verify-bindings` is worth a pass
since `GameModelOverrideBindingTests` now pins a raw reflection signature against v1.4.7.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](../features/career-system.md)

<!-- backlinks-end -->
