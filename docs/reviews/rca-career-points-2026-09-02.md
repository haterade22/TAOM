# RCA: career points never appeared, and the fix that nearly ate the save

**Date:** 2026-09-02
**Scope:** `Main/Features/CareerSystem/` career-points fix (uncommitted at review time)
**Review:** 6 agents (standards, engine API, efficiency, completeness, cross-system data flow, adversarial regression hunt)
**Outcome:** 4 findings fixed, 1 finding rejected on evidence, 2 pre-existing defects recorded

## Top line

The shipped bug was a lifecycle mistake: a legacy-save fallback gated on `!HasCareer(hero)` ran on
every NEW campaign, because `OnSessionLaunched` fires before character creation. It granted a
placeholder-culture career whose root choice then permanently consumed the player's level-1 career
point.

The more interesting story is the review. **Two of the four confirmed findings were defects in the
fix itself, and one of them would have deleted player save data.** Both came from the same root
cause, and neither was caught by the test suite, which was green at 7887 the whole time.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | CRITICAL | The repair pass built an allow-list from the career's choice groups and deleted everything outside it. `CareerConfigProvider` loads `taom_careers.xml` and `taom_career_choices.xml` under separate try/catch blocks, so a malformed choices file leaves careers resolvable and groups empty; the allow-list collapses to the root choice and the pass deletes the player's whole tree, permanently, on the next save | Destructive-default polarity | The guard written was `if (career == null) return 0`, which protects against the career being missing, not against the CHOICES being missing. Reviewed as a correctness question about the career, never about the allow-list's own trustworthiness | Deleting requires positive proof. New `ICareerRegistry.GetOwningCareerId`; drop only ids that positively resolve to a different career; unknown means keep. Lesson added |
| 2 | HIGH | Ownership resolved a choice through its group, but every root choice carries `group_id=""`, so the ghost root resolved to no owner and survived. The fix would not have fixed the bug | Test asserted against a fiction | The lifecycle tests stubbed `GetOwningCareerId("blademaster_of_ren_root")` to return a career id the real registry never returns for a root. Green tests, broken feature | Root choices indexed from `taom_careers.xml` directly. The pinning test (`GetOwningCareerId_RootChoiceWithEmptyGroupId_ResolvesToItsCareer`) runs against a real `CareerRegistry`, not a substitute |
| 3 | MED | `CareerCampaignBehavior` grew 121 → 181 lines against ADR-002's 150, holding business logic (which career to grant, what counts as foreign) | Entry-point creep | The standards agent measured 181 and called it "within policy". Caught by reading ADR-002 rather than trusting the verdict | Logic extracted to `ICareerLifecycleService`; behavior back to 124 lines and delegating |
| 4 | LOW | `owner == null` treated `""` as proof of foreignness | Degenerate value authorising a destructive action | Surfaced only because NSubstitute auto-values unstubbed `string` returns to `""` rather than null. Nothing in the design considered a non-null degenerate answer | `string.IsNullOrEmpty(owner)`. Same principle as #1, one layer down |
| 5 | LOW | Comments asserted `Hero.MainHero.StringId` is `"main_hero"` "in every campaign" | Overstated invariant | True of the vanilla template, false once Player Switcher calls `ChangePlayerCharacterAction`. The code never relied on it; only the comments did | Reworded to `Hero.MainHero.StringId` with the exception named |

### Rejected

The data-flow agent reported a CRITICAL: `HeroSwitchService` calling `OnCareerSelected` mid-session,
after the one-shot repair. **Rejected on evidence.** `HeroSwitchService.Execute` has exactly one
caller, `PlayerSwitchContentHandler.OnCharacterCreationFinalize`, an
`ICharacterCreationContentHandler` at priority 1100. It runs once at character creation, on a hero
with no prior career data. The adversarial agent independently reached the correct answer. Two
agents contradicted each other and the more emphatic one was wrong.

### Recorded, not fixed (pre-existing)

- **`TierUnlocks` / `Flags` have no career dimension.** `CareerDataService.IsTierUnlocked(heroId, tier)`
  does not know which career unlocked the tier, and only `ClearCareer` clears it. A tier unlocked by
  career A's quest survives into career B through any assignment path that does not clear, so B can
  read tier 2 or 3 as unlocked ahead of its level gate. Recorded as a CHANGELOG known limitation.
- **Co-op join can hold two roots for one session.** On a joining client the culture fallback can
  grant a career before `JoinReconciliationService`'s hourly re-grant applies the real one. Predates
  this change (the collision existed when the fallback lived on `OnSessionLaunchedEvent` too) and now
  self-heals on the next load via the repair pass.

## Root-cause pattern: absence of evidence used as evidence of absence

Findings 1, 2 and 4 are one mistake in three places. A destructive operation was written so that
**failing to prove membership counted as proving foreignness.** Every degenerate state therefore
pointed at "delete": a registry that could not load, a choice whose group did not resolve, an owner
id that came back empty.

This is the same shape as the NaN-gate family already in `csharp-architecture.md` ("Engine-Float
Decision Gates: NaN Must FAIL the Gate"), which says to write a gate as a positive requirement to
proceed. That rule is scoped to floats. Nothing extended it to destructive set operations, so the
rule existed, was understood, and did not fire.

It is also a second instance of "Lookup Functions With Fallbacks: Validate Before Lookup", already
in the same rule file. `GetChoicesForGroup` returns `EmptyChoices` as a survival fallback for an
unresolvable group, and that fallback was consumed as an acceptance criterion for a delete. The
rule's own words: *"The fallback exists for logging-and-survival, NOT for acceptance."* It was
written for `GetRaceNameFromId` and read as being about name lookups.

## Why each agent missed what it missed

- **Standards** measured the line count correctly and then misjudged it against the ADR. It reported
  "181 lines; within policy" for a 150-line ceiling. Verdicts from agents need the number checked,
  not just the conclusion.
- **Efficiency** examined `PruneForeignChoices`'s allocations in detail and never asked what the
  allow-list contains when the registry is degraded. Allocation profiling and destructive-correctness
  are different questions about the same loop.
- **Completeness** verified every guard had a test, which was true, and could not see that the guards
  covered the wrong failure mode. Coverage of the written guards says nothing about the missing one.
- **Data flow** correctly identified that the repair could destroy legitimate data and correctly
  enumerated the writers of `ChoiceIds`, but built its CRITICAL on a lifecycle assumption about
  Player Switcher it never checked, and did not reach the config-provider split that makes finding 1
  reachable.
- **Adversarial regression** was aimed directly at the registry-degradation question and got the
  wrong answer. It verified that `EnsureLoaded` leaves the collections "possibly empty, never null",
  then concluded a load failure "degrades to keeps-everything", which is true only if both files
  fail together. It read the provider and stopped one level above the two independent catch blocks. It did
  independently find the same structural weakness from the data side (a future group removal from a
  live career), which the same fix closes.
- **All six** were green on finding 2, because the test that should have caught it was in the
  changeset and asserted against a stub of the registry rather than the registry.

## Lessons to codify

Added to `docs/reviews/lessons/state-lifecycle-save.md`:

1. A destructive repair must require positive proof, and a lookup's fallback is never that proof.
2. A test that stubs the collaborator it is pinning behaviour against cannot pin that behaviour.

The first is a scope extension of two existing rules in `csharp-architecture.md` rather than a new
idea, which is the point: both rules were present and neither fired, because one was scoped to floats
and the other was read as being about name lookups.
