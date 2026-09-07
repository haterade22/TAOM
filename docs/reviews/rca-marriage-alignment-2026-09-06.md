# RCA: MarriageAlignment deep review (2026-09-06)

**Feature:** MarriageAlignment (#542), blocking marriage between Free-aligned and Evil-aligned heroes.
**Review:** 5-agent `/deep-review`. **Verdict:** 0 HIGH, 3 confirmed findings fixed, 4 findings
rejected with evidence, 1 finding found during triage that no agent reported.

The two load-bearing questions both came back clean and are the reason this feature is not a
rewrite: the single-chokepoint claim was confirmed by decompile (`MarriageAction.ApplyInternal`
re-checks `IsCoupleSuitableForMarriage` unconditionally, so it is enforced at both proposal and
apply time), and the transpiler was confirmed live rather than dead code under shipped defaults.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `Patch81_MarriageClanDraw` was 188 lines with the whole pool cache inline, including the invalidation stamp. ADR-002 bans algorithms in entry points, and static cache logic inside a Harmony patch is unreachable by any test. | Architecture | Wrote the cache where it was used. The 150-line ceiling did not fire during authoring because most TAOM `Hooks/` files are far larger (445, 358, 354), so file size read as normal. | Extracted `MarriageClanPoolStamp` (pure, 8 tests) + `MarriageClanPoolCache`. Patch is now 147 lines and is transpiler plus delegation. Lesson appended to `lessons/harmony-il.md`. |
| 2 | MED | The cache stamp held a strong reference to the `Campaign` object, retaining a finished campaign's entire object graph until the next campaign's first daily clan tick. | Lifecycle / memory | **No agent reported this.** Found while verifying finding 5. `plans/001-cross-campaign-singleton-resets.md` and the architecture rule both frame cross-campaign statics as a *correctness* problem (stale state served to campaign B); neither frames the identity token itself as a *retention* problem, so the reviews checked "does it reset?" and stopped. | Key on `Campaign.UniqueGameId` (a string) instead of the object. Lesson appended to `lessons/state-lifecycle-save.md`: a cross-campaign guard must not be the thing that pins the old campaign in memory. |
| 3 | LOW | `CandidateClansFor` read `Clan.All` before its own `Campaign.Current == null` guard. Since `Clan.All` IS `Campaign.Current.Clans`, the guard could never fire; the read above would have thrown first. | Dead guard | Guard was written defensively without tracing what the line above it already required. Agent 2 caught it by decompiling `Clan.All`. | Guard removed, with a comment stating that a successful `Clan.All` read is itself the proof and that vanilla reads it unconditionally at the same point. |
| 4 | LOW | `taom.print_marriages` reported a cross-alignment count without stating whether the feature was enabled. A player who disabled it and watched the count grow would read that as "the fix broke". | UX / diagnostics | The command was designed around ground truth about couples, which is genuinely toggle-independent. The reporting-context question was not asked. | Header now prints `enabled/ai/player` state from `IMarriageAlignmentSettingsProvider`. |
| 5 | LOW | Code comment claimed the cache stamp catches "CultureConversion re-cultures". False: `CultureConversion` converts SETTLEMENT cultures and never touches `Clan.Culture`. | Wrong comment | Named a plausible-sounding mechanism without checking what that feature actually converts. Surfaced only because a rejected finding (below) was built on top of the same wrong premise. | Comment corrected to state the real mechanism: clan culture is assigned only at clan CREATION, which moves `Clan.All.Count`, which the stamp already covers. |

## Findings rejected, with the evidence

Recording these because rejecting a finding without writing down why is how the same wrong claim
comes back next review.

| Finding | Claimed | Actual |
|---|---|---|
| Redundant `GetCultureSide` lookups in pool building (claimed **HIGH**) | "~4,180 redundant dictionary lookups per day in the per-lord hot path" | Frequency error. The pool is cached **per culture per day**, so the loop runs once per culture per day, not per lord. Measured worst case: 26 clan cultures x 145 clans = 3,770 calls/day, 3,744 redundant `TryGetValue` calls, roughly 0.1 ms per in-game day. The proposed fix (a mutable one-entry cache field) would add mutable state to a `Reuse.Singleton` pure service, which is a net loss under `simplicity-criterion.md`. |
| Day-granular cache invalidation is over-conservative (MED) | Rebuild cost is avoidable | Same arithmetic. ~0.1 ms/day buys a bounded staleness window. The agent's own recommendation was "document why", and the code already carried that comment. |
| `a + "\|" + b` should be string interpolation (LOW) | Allocation | `a + "\|" + b` and `$"{a}\|{b}"` both compile to the same `string.Concat(a, "\|", b)`. Not a fix. Also a manual one-shot command. |
| Pool goes stale on same-day culture conversion (MED) | A third clan's culture flipping mid-day serves a stale pool for the rest of that day | Unreachable. Decompiled `Clan`: the only two runtime writes to `Clan.Culture` are in `CreateSettlementRebelClan` and `CreateCompanionToLordClan`, both at clan CREATION, and both move `Clan.All.Count`, which is already in the stamp. Nothing in vanilla or TAOM reassigns an existing clan's culture. The premise came from finding 5's wrong comment. |

## Root-cause pattern

Findings 1, 2 and 5 are one theme: **the cache was written as an implementation detail of the patch
rather than as its own thing with its own invariants.** Because it had no name and no file, it got
no test, its identity token was chosen for convenience (the object at hand) rather than for
lifetime, and its invalidation contract was documented from memory instead of from the code it
claimed to track. Extracting it forced all three to be stated explicitly, and the act of writing the
stamp's doc comment is what exposed the false `CultureConversion` claim.

Finding 3 is a smaller instance of the same thing: a guard written defensively without tracing what
the preceding line already guaranteed.

## Why each agent missed finding 2 (the retention leak)

| Agent | Why its rule set did not fire |
|---|---|
| 1 Standards | Checks ADR compliance and service-locator placement. Retention of an engine object by a static field is not in its checklist. It did flag the file-size symptom (finding 1) whose fix incidentally exposed this. |
| 2 Compatibility | Verifies signatures exist and match. `Campaign` as a field type is perfectly valid; the question is lifetime, not signature. |
| 3 Efficiency | Checks allocations, LINQ, hot-path work and GC *pressure*. A single retained reference allocates nothing; it is a retention problem, and the prompt has no retention rule. |
| 4 Completeness | Checks tests, docs, issue, CHANGELOG, registration. Out of scope by construction. |
| 5 Data flow | Ran the cross-campaign check and correctly concluded the reset *works*. Its rule asks "does stale state leak into campaign B", which is a correctness question; the field satisfies it while still pinning campaign A in memory. The rule's framing is exactly what let this through. |

The generalisable gap: every existing rule about cross-campaign statics is phrased as a
**correctness** question. None asks what the guard itself retains.

## Lessons appended

- `docs/reviews/lessons/state-lifecycle-save.md`: a cross-campaign identity guard must key on a
  value, not the campaign object.
- `docs/reviews/lessons/harmony-il.md`: static cache state inside a Harmony patch is untestable by
  construction; extract the invalidation decision.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/marriage-alignment.md](../features/marriage-alignment.md)
- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
