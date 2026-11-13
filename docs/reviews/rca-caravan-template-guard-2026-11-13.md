# RCA: Patch147 CaravanTemplateGuard review and rework (2026-11-13)

**Issue:** #482. **PR:** #1147. No crash report: the hazard is latent.
**Review:** two `/deep-review` runs, risk tier 1 (a Harmony patch), each with Agent 5 Data flow and
Agent 2 Engine compatibility (`deep-reviewer`). The first run judged the PR as opened (commit
`76355bd69`), the second the rework. Codex: NOT AUTHORIZED for this review (paid; the maintainer
approved the plan without it), so no Codex pass ran.

## Top-line

Vanilla `CaravansCampaignBehavior.SpawnCaravan` dereferences a null caravan template. The PR as
opened flagged a call in a prefix when either of the culture's two template lists could fail, and a
finalizer contained any `NullReferenceException` on a flagged call. The review found that the
finalizer handed other exceptions back raw, which erases their throw site, and that the design
rested on a wrong engine fact: the list vanilla reads is not a random roll, so the guard flagged
calls that could not hit the null pick and could then swallow an unrelated NRE after the caravan
party existed. The rework is a prefix that repeats vanilla's list choice and predicate and skips
vanilla only when the pick fails, with no finalizer of its own.

The second run found three defects in the rework, all fixed before the commit: a list entry with no
template data was read as a land template, no test ran the prefix's decision, and the docs said
"no finalizer, so nothing is swallowed" without PatchShield's finalizer.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (Agents 2 and 5, run 1) | The finalizer returned `__exception` unchanged. Harmony then ends with `throw`, so every other exception leaving `SpawnCaravan` lost its throw site. | Repeat: "A value-returning finalizer that hands back its exception erases the throw site" | The shape was copied from Patch65, which predates the rule in `harmony-patches.md`. `ContainmentFinalizerRethrowTests` lists patches by hand, so a new finalizer is not covered until someone adds it. | The rework has no finalizer. Patch65 kept the raw rethrow until #1155, the same day. |
| 2 | MEDIUM (Agent 2, run 1) | The patch, its rule class, its test and the registry said the elite or normal list is "a random roll inside the method". `hero.RandomFloat()` is the hero's saved `RandomValue` divided by 2^31, so the choice is fixed per hero and power. The guard therefore flagged on either list and could contain a different NRE on a call vanilla would have completed past the pick. | Engine fact inferred from a name | `RandomFloat` reads as a fresh roll, and the body of the extension was not opened (AGENTS.md "Effects: quote the line that produces the effect, never infer it from a name"). | The prefix calls the same model and the same `RandomFloat`; `ReadsEliteList_MirrorsVanillasStrictComparison` and `OnlyTheListVanillaReadsIsJudged` pin the choice. Lesson recorded. |
| 3 | MEDIUM (Agent 5, run 1; `/verify` Step 2d) | `taleworlds-api-snapshot/patch-targets.md` was not regenerated, so the snapshot check reported drift and the next engine bump would not have listed `SpawnCaravan`. | Gate not run | `/verify` Step 2d exists and was not run on the branch. | Regenerated; `-Check` reproduces. No new rule: the gate exists. |
| 4 | LOW (Agent 5, run 1) | The report key was stored before the log write, the statics were process-scoped with no campaign in the key, the warning gave no detail, the feature-map row named only Patch65, and the pair was never run through real Harmony. | Diagnostics, docs | The log was written as plumbing; `lessons/state-lifecycle-save.md` "A `static bool` diagnostic latch is process-scoped" was not read. | The key carries `Campaign.UniqueGameId` and is stored after the write; the warning names the list and what is missing; the feature-map row names Patch147. |
| 5 | MEDIUM (Agents 5 and 2, run 2) | The rework read a null list entry, or a template whose `ShipHulls` is null, as a land template, so in a land town it let vanilla run into the crash the guard exists to stop. Vanilla runs the predicate on every entry. | Mirror of a vanilla predicate not exact | The builder read a grep hit `ShipHulls = new MBList<...>()` as a constructor line and wrote "never null"; the line is inside `Deserialize`, which an entry with an undefined id never reaches. The builder also gave that wrong fact to Agent 2 in its brief; Agent 2 corrected it. | `Judge` returns `BrokenTemplateEntry` for such an entry; rule and staged prefix tests pin it; `CulturePartyTemplateTests` now fails on a caravan child with no `id`. Lesson recorded. |
| 6 | MEDIUM (Agent 5, run 2) | No test ran the prefix's decision: inverting it, swapping the two lists or returning true always passed the whole suite. The docs claimed the tests pinned the list choice. | Test gap; repeat of "Static cache state inside a Harmony patch is untestable by construction, so extract the decision" | The predicate was extracted into the rule, but the composition (which list, when to skip) stayed in the prefix, and the only test that called `Prefix` passed a null hero. | The composition moved into `CaravanTemplateGuard.Judge`. Four `RequiresGame` tests run the prefix on staged engine objects. Checked by mutation: inverting the decision, and swapping the two lists, each fail 4 of the 17 tests. |
| 7 | MEDIUM (Agent 2, run 2) | The class doc, registry and feature doc said "no finalizer, so nothing is swallowed". From the second game start in a process PatchShield's finalizer is on the method, swallows the three missing-API exceptions and strips TAOM's prefix. | Repeat: "A new patch on an engine method also gets PatchShield's finalizer: describe the target's whole finalizer set" (also row 9 of `rca-lastseen-guard-2026-10-26.md`) | The sentence described Patch147's own patch methods, and the lesson was read as applying only to a patch that has a finalizer. | The three texts now say "no finalizer of its own" and the registry describes the whole set. Promoted to one line in `.claude/rules/harmony-patches.md`, because this is the third time. |
| 8 | LOW (Agents 2 and 5, run 2) | Wording: the model limit named only one direction of a missed prediction; "throws on line 499" is true only for a null pick; "three days in four" for a 0.75 chance; the `:501` fallback condition was loose; `[0,1)` for a value that can be 1.0; the no-settlement warning told the author to add a template; the fault line had no stack; the in-game check could not tell "applied" from "not applied"; four docs still described a caravan binding fault as a crash with no TAOM frame. | Wording, docs | Each sentence was written from the builder's model of the code and not re-read against the engine line. | All corrected in the same change. |

## Root-cause pattern

Rows 2, 5 and 8 are one pattern: a statement about engine behaviour written from a name or from one
grep hit, without the enclosing code. "RandomFloat" was read as a roll, an assignment line was read
as a constructor, and "throws on line 499" was written from the one case the issue describes. Each
was caught only by a reviewer who opened the body. Rows 1 and 7 are repeats of recorded lessons
about finalizers; in both the author reasoned about TAOM's own patch method and not about what
Harmony emits for the whole target.

## Why the earlier passes missed these

- **The first read-only pass of the PR (the main session, before the lenses)** flagged the wide
  containment (row 2's consequence) as a design note but accepted "a random roll" without opening
  `RandomOwnerExtensions`, and did not check the finalizer against `harmony-patches.md` (row 1).
  It did predict row 3.
- **Run 1, Agent 5** did not read Harmony's source, so it rated row 1 from the lesson alone; Agent 2
  confirmed it in `MethodCreator`. Agent 5 traced the flow from the flag to the finalizer and took
  the roll as given; Agent 2 opened `RandomOwnerExtensions` and found row 2.
- **Run 2** found rows 5 to 8 in code the main session had just written. The builder's own tests
  could not find row 6, because they were the gap.

## Lessons recorded

- `lessons/harmony-il.md`: "A guard that mirrors a vanilla predicate judges every entry the engine
  judges, and its composition is tested, not only its parts (Patch147, 2026-11-13)".
- `lessons/harmony-il.md`: "`hero.RandomFloat()` is a saved value, not a roll: open a helper's body
  before you call a branch random (Patch147, 2026-11-13)".
- `.claude/rules/harmony-patches.md`: one line, "No finalizer of your own is not 'no finalizer'".

## Deferred

- FOLLOW-UP (#1155, fixed on 2026-11-13 with the eight other bare returns, and gated): Patch65's
  finalizer returned its exception raw (`Patch65_LandlessCultureSpawnGuard.cs`), the shape
  row 1 was copied from.
- FOLLOW-UP (#1155: no code change, because decision D13 already covers it): PatchShield's
  protected-owner list has the prefix `TAOM`, which does not match TAOM's own Harmony id
  `com.taom.mod`, so a missing-API exception on a shielded method strips TAOM's own prefixes there.
  D13 (`plans/_audit/2026-10-02-perf/DECISIONS.md`, answered 2026-10-03; its follow-up scope was recorded on
  2026-10-04) is a plan that strips only the owner whose patch threw and then protects TAOM's id; that
  plan is owed. Until then the list stays, because
  protecting the id alone keeps a broken TAOM patch swallowed on every call. A lesson, the registry
  and two comments that said TAOM's patches were protected are corrected, and a test pins today's
  state.
- Not run: no in-game check (no current data reaches the hazard), and no Codex pass.
