# RCA: plan 019 maintainer decisions (deep review + Codex adversarial, second round, 2026-09-24)

## Top-line

The follow-up commit `503b933e` applied Mike's four decisions on plan 019 (#660): a per-field
fallback for `KingdomMessages`, the no-settlement siege-camp path closed as unreachable, and the
`/build-fix` scope extension kept. Six lenses and Codex found **no runtime defect**: `GetMessages`
handles a missing key, `""`, a JSON `null` entry and an unknown faction, always as a fresh copy, and
both consumers go through it.

**11 confirmed findings (5 LOW, 6 NIT), 1 false positive, 0 HIGH, 2 design questions for Mike.**
The commit added three nullable warnings to the test project (2,259 against a 2,256 baseline the
plan requires), its fresh-copy test could not catch a regression on the defaults path, and the doc
edits stopped short: the patch registry still called decision 2 open, `siege-defense.md` gave two
test counts, and the new token table promised tokens the code does not substitute. All 11 are
fixed in the review follow-up; the test count is back to 2,256.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | `SiegeDefenseServiceTests.cs:39` CS8619, `:393` CS8601, `:470` CS8602: making the dictionary value `KingdomSiegeMessages?` raised warnings at an unchanged Setup line, and the new test code added two more | Annotation change creates warnings at unchanged consumers | The executor verified the tier the change targets (Main, 0 CS86xx) and quoted the test run's pass count, not its warning count; test-project warnings are warnings, so the build stays green | Fixed (`?>`, `?? throw new AssertFailedException`, `Assert.IsNotNull` narrowing). Lesson in `lessons/build-tooling-workflow.md` |
| 2 | LOW | `harmony-patch-registry.md:67-69` still said choosing a no-settlement fallback "is an open decision" | Decision closed in one doc, open in another (**repeat**) | Decision 2 was recorded where the NEEDS MIKE item pointed (`siege.md`), and nobody grepped for "open decision" or "Deferred gameplay decision" | Fixed. **Recurred** line on the misc "wrong everywhere it was written" lesson |
| 3 | LOW | `siege-defense.md` Key Files said 30 tests, the Tests section "17 tests" with no `GetMessages` bullet; the first review's follow-up named both lines | Same as 2 | The count was fixed where it was noticed | Fixed; same Recurred line |
| 4 | LOW | The new `KingdomMessages` row said every field takes all five tokens; `AcceptButton` goes to `InquiryData` raw, and the two `Resolve` call sites fill different subsets | Doc written from the DTO, not the call sites | The row was written with the five field names in view, and the token list came from `Resolve`'s body, not its three callers | Fixed: tokens per field. One-off; covered by the misc lesson "a doc that says ... is a claim about code" |
| 5 | LOW | `GetMessages_PartialEntryResultMutatedByCaller_...` mutated only a configured result; mutant `if (configured is null) return DefaultMessages;` passed every `GetMessages` test (Codex P3) | Weak test oracle | The test was written to reproduce the config-entry bug the RED showed, and its "defaults unchanged" asserts read as covering the static | Fixed: `GetMessages_DefaultsResultMutatedByCaller_LaterLookupsStillGetDefaults`, failing under the mutant. Lesson in `lessons/testing-qa.md` |
| 6 | NIT | No test pinned `AcceptMessage`; mutant `AcceptMessage = OrDefault(configured?.AcceptButton, ...)` passed | Weak test oracle | The known-faction test asserted two of five fields | Fixed: all five fields asserted, failing under the mutant. Same lesson |
| 7 | NIT | CHANGELOG "popup never blank", the code comment and the doc row promised no blank text; a value of only spaces renders blank | Claim broader than the code | The decision named null and `""`, and the label was written from the goal | Wording fixed; the `IsNullOrWhiteSpace` change is NEEDS MIKE |
| 8 | NIT | `siege.md:76` status named the branch | Template vocabulary | Written while the branch was the status | Fixed: `Open (#660)` |
| 9 | NIT | `siege-defense.md` "GitHub Issue" listed only #67 | Same as 2 | #660 was added to `siege.md` only | Fixed |
| 10 | NIT | Key Files omitted `Models/KingdomSiegeMessages.cs` | Pre-existing gap exposed | The file's summary now states the contract; the table was not revisited | Fixed |
| 11 | NIT | "Both engine callers dereference `SiegeEvent.BesiegedSettlement`": the second passes it to `MapScene.GetSiegeCampFrames` (`MapScene.cs:623` dereferences it) | Imprecise engine claim | Carried from the first round's wording | Fixed in `siege.md:20` and the registry |

False positive: Codex's note that the test key `rohan` is not a production kingdom id (Rohan is
`vlandia`). The tests inject and query the same synthetic key, as the Setup fixture does with
`gondor`; nothing reaches production config.

Needs Mike (behaviour-changing, not applied): a warning per incomplete `KingdomMessages` entry
(Agent 6, from `csharp-architecture.md` "Config Providers MUST Validate" items 5-6), and treating a
value of only spaces as empty.

## Root-cause pattern: the follow-up verified what it changed, not what the change reached

Findings 1 to 3 and 9 share a theme. The commit checked the tier it targeted (Main, 0 CS86xx), the
doc it was pointed at (`siege.md`) and the line it edited (Key Files), and each check passed. What
the change reached beyond those (unchanged test lines that now warn, a second doc recording the same
decision, a second count in the same file) was not searched for. Finding 2 and 3 repeat the misc
lesson from #644/#645 ("a claim found wrong is wrong everywhere it was written"); finding 1 is the
same shape for compiler output: a count the plan requires was not re-counted because the build was
green.

Findings 5 and 6 are the second round of weak oracles on this branch (the first was the camp-2
length check). Both times the test was written from the RED it had to produce, and asserted less
than the claim it was named for.

## Why each agent missed these

- **Implementation (executor):** ran RED/GREEN and the full suite, but quoted the Main warning count
  only, so finding 1 was invisible in its own record; fixed the doc lines it was pointed at (2, 3).
- **Agent 1, standards:** found 1 to 5, 7, 8. It listed the Key Files omission (10) as a follow-up
  and did not check the caller wording (11).
- **Agent 2, engine:** found 2, 3, 5, 7, 11 and flagged 1 as UNVERIFIED (it does not build). The doc
  token table (4) is outside an engine lens.
- **Agent 3, efficiency:** confirmed 1 from the logs and routed it. The rest are outside its lens.
- **Agent 4, completeness:** found 1 to 3, 5, 7, 9, 10. It did not compare the token table with the
  call sites (4) or the mapping coverage (6).
- **Agent 5, data flow:** found 1 to 6. It traced tokens per call site, which is how it caught 4.
- **Agent 6, design:** found 1 to 3 and raised the silent-fallback question. It judged the tests'
  helper, not their oracles (5, 6).
- **Codex:** found 1 (two of three sites) and 5. Its prompt scoped it to the diff's files through
  git refs, so the registry (2) was out of view; it noted that `AcceptButton` is literal but not
  that the doc said otherwise (4); it did not count warnings (no builds allowed).

## Feedback memories to codify

None beyond the lesson entries: `lessons/build-tooling-workflow.md` (re-count the test project after
an annotation change), `lessons/testing-qa.md` (a "never shared" test mutates the result of the path
that could return the shared object, and a mutant proves it) and a Recurred line in
`lessons/misc.md`.
