# Deep review: plan 057, first-byte prefilter in ClipBudgetSignature.Find (2026-11-19)

```
DEEP REVIEW REPORT
===================
Feature: Skeleton buffer and clip budget: jump to the first fixed byte in the engine
         signature scan (improve plan 057, #784)
Date: 2026-11-19
Range: 8e836fd40..66fcd4204 (branch improve/057-signature-find-first-byte-prefilter)

Scope:   C# (Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs, one method),
         tests (ClipBudgetSignatureTests.cs, 11 new tests). No XML, no hooks, no validators.
Blast radius: one method, ClipBudgetSignature.Find. Its production callers are
         SignatureScanSession.FindPlain (SignatureScanSession.cs:89), reached from
         ClipBudgetSignature.cs:74 and SkeletonBufferSignature.cs:81, 82, 85, 86, 106 and 109.
         graphify was not run (the lead did not refresh the graph); the lenses and the lead
         used git grep and reads.
Waves:   one wave: Agent 2 (Engine compatibility) and Agent 5 (Data flow). Codex not run.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS: 0 incompatible, 2 verified, 1 unverified commit-body claim (the lead
               verified its cache half; the timings stay UNVERIFIED, not a defect)
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS: 5 flows traced, 0 gaps, 0 inconsistencies
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

### Agent 1: Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2: Engine compatibility

No TaleWorlds API, Harmony target, reflection-by-name site or lifecycle virtual in the changed
hunks. Two usages verified: `Array.IndexOf<T>(T[], T, int, int)` exists on .NET Framework 4.7.2
(the target, `net472`), and the six `SkeletonBufferSignature` pattern constants the new test reads
are `internal const string`, reached through `InternalsVisibleTo`.

The lens proved equivalence with the old loop: bounds (`IndexOf` cannot throw), skipped offsets
(the old loop failed each at `j = 0`), a first token above 0xFF (the truncating cast finds
candidates, the `int` compare at ClipBudgetSignature.cs:50 rejects them, so both loops return
nothing), order and the `maxHits` stop (unchanged, ClipBudgetSignature.cs:54-56), and the leading
wildcard and empty pattern fallback.

One UNVERIFIED item in the commit body, not in code: "the scan runs only when the address cache
misses" and the 152 to 173 ms before, 48 to 58 ms after timings.

Lead check of that item: the cache half holds. `SignatureScanSession.Find` returns the cached
offset without a scan when the remembered address still matches (SignatureScanSession.cs:62-69),
and the two uncached `FindPlain` calls (SkeletonBufferSignature.cs:85 and :109) are skipped when
their guard came from the cache (:83-84, :107-108). So a launch where every entry hits runs no
`Find`. The timings come from issue #784 and were not re-measured here: UNVERIFIED, and they do
not bear on correctness.

### Agent 3: Efficiency

SKIPPED BY TIER.

### Agent 4: Completeness

SKIPPED BY TIER.

### Agent 5: Data flow

Five flows traced, all CONNECTED:

1. The callers through `SignatureScanSession.FindPlain` into `Find`, and the hit offsets into the
   cache entry and the RVAs that feed the native patch install. Equivalence proved against
   `git show 8e836fd40:` of the file; a null input throws the same `NullReferenceException` as
   before.
2. `SignatureScanSession.MatchesAt` (the cache-hit check, SignatureScanSession.cs:118-126) and
   `Find` (the miss path) still decide a match the same way: the diff changes which offsets are
   tested, not how a match is decided.
3. Every shipped pattern starts with a fixed byte, so all seven take the new path, and
   `Find_RealPatternsInRandomCode_MatchesTheBaseLoop` covers all seven.
4. The test oracle `BaseFind` matches the base body line for line; its planted-hit assert stops
   the differential test passing on a corpus with no matches.
5. The state, lifecycle, threading and XML checks do not apply: `Find` is a pure function over
   caller-owned arrays.

### Lead verification

The lead re-read ClipBudgetSignature.cs:36-59 in the worktree and confirms both lenses' proof:
`jump` implies `pattern.Length >= 1`, so `last - i + 1 >= 1` and `last + 1 <= haystack.Length`
inside the loop; a `-1` return breaks where the old loop would also have found nothing more in
`[i, last]`; and `i++` after a candidate keeps overlapping hits and ascending order.

### Agent 6: Design and elegance

SKIPPED BY TIER.

### Agent 7: XML and ModuleData integrity

NOT IN SCOPE.

### Tooling correctness

NOT IN SCOPE.

## ACTION ITEMS

None. No finding from either lens, and no Codex run.

## Suite evidence

- At the branch head 66fcd4204, run by the lead:
  `Failed!  - Failed:     3, Passed: 42954, Skipped:     9, Total: 42966`. The three failures are
  the known language-file tests (`EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles`); this diff
  touches no string or language file.
- The plan's baseline at the base (plans/057-signature-find-first-byte-prefilter.md:23-24):
  42943 passed, the same three failures, 9 skipped. The head is that baseline plus the 11 new
  tests, with nothing else changed.
- The lead changed no code, so no second run was owed. No tool, hook or validator changed: the
  Python and hook suites and the gate sweep do not apply.
- Not covered: no game run (tier 1, as the orchestrator ruled), so no launch with an empty address
  cache and no scan of the in-memory code section; the commit's `Not-tested:` trailer names both.

## IMPROVEMENTS (Step 4)

APPLIED:     none. Agents 3 and 6 were skipped by tier, so no APPLY-scoped fix or KEEP proposal exists.
NOT APPLIED: none.
FOLLOW-UP:   none. Neither lens named pre-existing code to follow up. One lead note, not a
             defect: no test pins a first token above 0xFF (the only input where the `(byte)`
             cast matters); `Parse` produces one only from a pattern with a three-digit hex
             token, which no shipped pattern has, and both lenses proved the result unchanged.

VERDICT: READY FOR COMMIT

## CODEX REVIEW

Codex not run for this item (out of usage; a paid call needs the maintainer's word). The Phase 3d
assessment therefore has no rows.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | not run | | | |

- Confirmed bugs from Codex: none (not run).
- False positives: none (not run).
- Design questions: none.
- Things Codex missed: not applicable; the lenses found nothing.

### Codex lessons (pending)

None. No Codex run, so no new false-positive pattern or missed category to record.

## Records

- RCA: none. No confirmed finding.
- Lessons: none. No systemic finding.
