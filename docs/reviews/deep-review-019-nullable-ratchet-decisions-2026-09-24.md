# Deep review: plan 019, maintainer decisions follow-up (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 019, maintainer decisions applied (#660): per-field KingdomMessages fallback,
         no-settlement path closed as unreachable, /build-fix extension kept
         (branch improve/019-nullable-ratchet, de288136..503b933e, 1 commit, 8 files)
Date: 2026-09-24

Scope:   C# (SiegeDefenseService.GetMessages/OrDefault, 2 DTOs, 4 tests), docs (CHANGELOG,
         siege-defense.md, siege.md, the first review record)
Waves:   lenses 1-6 ran before this lead pass; the review lead verified, fixed and wrote this
         report; Codex adversarial (second round) complete and included

STANDARDS:     FAIL - 4 LOW, 3 NIT; no code-standards violation in the changed C#
COMPATIBILITY: PASS - 0 incompatible, 0 unverified (29 usages); 1 LOW doc finding
EFFICIENCY:    PASS - no performance issue; 1 verified cross-lens defect (test warnings)
COMPLETENESS:  INCOMPLETE - test-project warnings, defaults half of the fresh-copy claim
               untested, registry still calls decision 2 open, stale Tests section
DATA FLOW:     PASS - 14 flows, 1 gap (NIT), 4 inconsistencies (LOW); no runtime gap
DESIGN:        4 KEEP proposals (4 apply: 3 applied, 1 behaviour-changing, needs Mike)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Verification of findings

Every finding was re-read against the worktree at `503b933e` before it was classified. The
warning claim was checked by re-counting the executor's own log
(`E:\repos\taom-improve\scratch\019\fulltest.log`: `SiegeDefenseServiceTests.cs(39,31) CS8619`,
`(393,18) CS8601`, `(470,23) CS8602` are the only Siege sites beyond the 9 in the baseline). The two
test-oracle claims were proved by mutants (below). The `MapScene.GetSiegeCampFrames` dereference
was read in the v1.5.3 dump (`MapScene.cs:623`, `settlement.Party.Id`).

| # | Finding (lenses) | Sev | Verdict |
|---|---|---|---|
| 1 | Three new TAOM.Tests nullable warnings (2,259 against the 2,256 baseline) break plan 019's Done criterion and the CHANGELOG's "test-project warnings untouched" (A1 S1, A2 F4, A3, A4 F1, A5 #1, A6 P1, Codex P3 #1) | LOW | CONFIRMED, fixed |
| 2 | `harmony-patch-registry.md:67-69` still calls the no-settlement fallback "an open decision" after decision 2 closed it (A1 S2, A2 F1, A4 F3, A5 #3, A6 P3) | LOW | CONFIRMED, fixed |
| 3 | `siege-defense.md` says 30 tests in Key Files and "17 tests" in Tests, with no `GetMessages` or Reset/Restore bullet (A1 S3, A2 F2, A4 F4, A5 #4, A6 P4) | LOW | CONFIRMED, fixed |
| 4 | The new `KingdomMessages` row promises all five tokens in every field; `AcceptButton` is passed raw (`SiegeDefenseService.cs:162`), Title/Body/AcceptMessage zero `{influence}`/`{relation}` (`:153-155`), RewardMessage gets the settlement id, `""` and `0` (`:351-352`) (A1 S4, A5 #2, Codex config table) | LOW | CONFIRMED, fixed |
| 5 | The fresh-copy test mutates only a configured result; `if (configured is null) return DefaultMessages;` passed every `GetMessages` test (A1 N1, A2 F3, A4 F2, A5 #5, Codex P3 #2) | LOW | CONFIRMED by mutant, fixed |
| 6 | No test pins the `AcceptMessage` mapping; `AcceptMessage = OrDefault(configured?.AcceptButton, ...)` passed every test (A5 #5) | NIT | CONFIRMED by mutant, fixed |
| 7 | "Popup never blank" / "always has a label": a value of only spaces is kept and renders blank (A1 N3, A2 C1, A4 N1) | NIT | CONFIRMED as wording, fixed; the code change is NEEDS MIKE |
| 8 | `siege.md:76` status names the branch instead of Open/Closed (A1 N2) | NIT | CONFIRMED, fixed |
| 9 | `siege-defense.md` "GitHub Issue" lists only #67 (A4 N2) | NIT | CONFIRMED, fixed |
| 10 | `siege-defense.md` Key Files omits `Models/KingdomSiegeMessages.cs`, whose summary now states the fallback contract (A4 N3; A1 follow-up) | NIT | CONFIRMED, fixed |
| 11 | "Both engine callers dereference `SiegeEvent.BesiegedSettlement`": the second only reads it and passes it to `MapScene.GetSiegeCampFrames`, which dereferences it (A2 C3) | NIT | CONFIRMED, fixed in `siege.md:20` and the registry |
| 12 | Silent fallback; `csharp-architecture.md` "Config Providers MUST Validate" items 5-6 want a logged warning; A6 P2 proposes a constructor-time warning per incomplete entry | design | NEEDS MIKE (behaviour-changing) |
| 13 | Test key `rohan` is not a production kingdom id (Rohan is `vlandia`) (Codex config table) | obs. | FALSE POSITIVE as a defect: the tests inject and query the same synthetic key, as the Setup fixture does with `gondor`; nothing reaches production config |
| 14 | Untracked `codex-adversarial-...-decisions-...prompt.md` (A4 worktree note) | obs. | Not a defect; committed with this review, as the first round's prompt was |

No HIGH finding was reported by any lens or by Codex. The runtime change is correct: decisions 1,
3 and 4 are implemented as decided, and decision 2 was recorded everywhere except the registry
(finding 2). Root causes and why each agent missed what it missed:
`docs/reviews/rca-nullable-ratchet-decisions-2026-09-24.md`.

## Details (condensed from the lens reports)

**Agent 1, standards.** ADR-007, banned constructs, structure, naming, prose, line endings and the
commit subject pass. Findings 1 to 4 and 5, 7, 8. Its UNVERIFIED note (the executor's RED run left
no log) stands; the mutant run below re-proves the RED side of the strengthened tests.

**Agent 2, engine.** 29 usages verified against v1.5.3, 0 incompatible. The popup label path
(`SingleQueryPopUpVM.SetData` copies `AffirmativeText` with no fallback) confirms that a missing
`AcceptButton` rendered a blank button before the change. Findings 2, 3, 5, 7, 11; follow-ups below.

**Agent 3, efficiency.** One fresh five-reference object per siege start or reward; no cost worth
measuring. Caching the merged messages would share a mutable object again: a Reject under the
simplicity criterion. Routed finding 1 with a PRESERVING fix, applied.

**Agent 4, completeness.** Tests, issue (#660 OPEN), CHANGELOG and IoC pass. Findings 1 to 3, 5, 7,
9, 10, 14; issue-body staleness to Mike.

**Agent 5, data flow.** 14 flows; the JSON-null entry path now closes both old null dereferences
(popup and `GrantReward`). Findings 1 to 4, 5, 6.

**Agent 6, design.** `GetMessages` merging on each call and `OrDefault` judged already optimal.
Proposals 1, 3, 4 (findings 1, 2, 3) applied; proposal 2 (finding 12) is behaviour-changing.

## CODEX REVIEW

Codex adversarial, second round, prompt
`docs/reviews/codex-adversarial-019-nullable-ratchet-decisions-2026-09-24.prompt.md`; raw output
`docs/reviews/raw/codex-adversarial-019-nullable-ratchet-decisions-2026-09-24.md` (complete, ends
"END OF CODEX REVIEW"). Verdict: 0 P1, 0 P2, 2 P3. It pasted the engine excerpts for every call the
change reaches (`InquiryData`, `SingleQueryPopUpVM.SetData`, `InformationManager`, both
`GetSiegeCampPartyPosition` callers, `MapScene.GetSiegeCampFrames`) and walked 13 scenarios,
including a whole-`KingdomMessages` JSON null (caught by the provider) and new campaign in the same
process.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | Test-project warnings at `:39` and `:470` confirmed from the executor's log; Codex missed the third (`:393`, Newtonsoft's `T? DeserializeObject<T>`). Its proposed `entry?.AcceptButton` after `IsNotNull` is redundant: the build shows `IsNotNull` narrows, so the fix uses `entry.AcceptButton` |
| 2 | P3 | LOW | Yes | Mutant 1 (`if (configured is null) return DefaultMessages;`) passed the original test and failed the new one |
| obs. | config table | none | No | `rohan` is synthetic in both inject and lookup (finding 13) |
| KS 1-10 | CONFIRMED 4 (static) and 9, DISPUTED 1, 6, 7, 8, 10, UNVERIFIED 2, 3, 5 | n/a | Yes | KS 4 and 9 are Codex findings 1 and 2; the disputes match the code; the UNVERIFIED items are historical runs outside this diff |

**Confirmed bugs:** findings 1 and 5 above. **False positives:** none among the numbered findings;
the `rohan` observation is not a defect. **Design questions:** none raised by Codex.
**Things Codex missed:** the stale patch registry (2), the stale test count (3), the token row (4;
its config table did note that `AcceptButton` is literal, but not that the doc says otherwise), the
unpinned `AcceptMessage` mapping (6), the whitespace wording (7), the doc nits (8 to 11) and the
silent-fallback design question (12). Codex read only the diff's files through git refs, so docs
outside the diff (the registry) were out of its view, as in the first round.

## Mutant proof for the strengthened tests

`SiegeDefenseService.cs` was edited temporarily with both mutants (defaults shortcut, and
`AcceptMessage` read from `AcceptButton`), then restored (`git diff Main/` empty afterwards):
`dotnet test ... --filter FullyQualifiedName~SiegeDefenseServiceTests` gave
`Failed: 2, Passed: 29, Total: 31`, the failures being exactly
`GetMessages_KnownFactionId_ReturnsConfigMessages` and
`GetMessages_DefaultsResultMutatedByCaller_LaterLookupsStillGetDefaults`. The original
`GetMessages_PartialEntryResultMutatedByCaller_DefaultsAndConfigEntryUnchanged` passed under
mutant 1, which confirms finding 5.

## Verification

- Siege tests before any edit: `Passed: 105, Total: 105`.
- `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`, counted with
  `count_cs86.py`: `total=2256`, `in_fragment=2256` for `TAOM.Tests\` (0 in Main), 9 for
  `Features\Siege\` (the baseline 9); `0 Error(s)`.
- Full suite `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10254, Skipped: 2, Total: 10258`; the failures are the known live-Armory tests
  `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
- `python tools/lint_docs.py --quick --summary`: `total_findings: 0`. No em or en dash on any added
  line.

## Action items

1. Mike: finding 12 (log a warning for an incomplete `KingdomMessages` entry) and the whitespace
   fallback (finding 7), both behaviour changes.
2. Mike: refresh the #660 body (public). Per Agent 4's read of it: its Status names tip
   `de288136`, "adds 14 tests" is now 19 (14 + 4 + 1), and its follow-ups list the null-entry bug and the `siege-defense.md` omission this branch
   fixed. The 2,256 test-project count it quotes is true again.
3. Orchestrator: one convergence `deep-reviewer` pass on the follow-up diff (Step 4.6); this lead
   cannot spawn agents. Then `plans/README.md:37` and `plans/019-nullable-ratchet.md:47` (019 still
   TODO, no issue) and `Refs #660` on the merge.

## AGENTS.md lessons (pending)

For the consolidated Phase 3h update, not applied here:
- **Bugs Codex typically misses:** a decision or claim recorded in a doc outside the diff (here the
  patch registry) goes stale when the diff changes it; Codex reads only the diff's files.
- **Bugs Codex typically misses:** warnings created at unchanged consumer sites by an annotation
  change (a `?` on a generic argument); Codex found two of three sites and did not count.
- **What Codex does well:** it proposed the exact mutant (`return DefaultMessages` for the null
  path) that the fresh-copy test could not catch.

```
─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:
- TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs:39, :396-397, :474-476 (A6 P1, A3):
  Dictionary<string, KingdomSiegeMessages?>; `?? throw new AssertFailedException(...)` on the
  deserialize (stricter than `?? new SiegeDefenseConfig()`, which would let a defaults-expecting
  test pass on a null config); `var entry = ...; Assert.IsNotNull(entry);`. PRESERVING; proof:
  count 2,259 to 2,256 and the Siege tests green.
- SiegeDefenseServiceTests.cs:363-367 and new
  GetMessages_DefaultsResultMutatedByCaller_LaterLookupsStillGetDefaults (:480): findings 5, 6;
  RED under the mutants, GREEN on the real code.
- docs/reference/harmony-patch-registry.md:67-70 (A6 P3): decision 2 recorded, caller wording.
- docs/features/siege-defense.md:51, :86, :93, :105, :116-117, :151, :159-160 (A6 P4 and
  findings 4, 9, 10): token table per field, Key Files row, 31 tests, two Tests bullets, #660.
- docs/features/siege.md:20, :76: caller wording, Status Open (#660).
- CHANGELOG.md:30-37 and SiegeDefenseService.cs:76-78 (comment only): "falls back per field" /
  "never missing its label"; spaces kept as written; 5 new tests; count 2,256.
- docs/reviews/deep-review-019-nullable-ratchet-2026-09-24.md: correction note on the omitted
  test-project count.
NOT APPLIED:
- SiegeDefenseService.cs:52 (A6 P2), log a warning per incomplete KingdomMessages entry:
  behaviour-changing (new [WARNING] log lines), needs Mike. Mandatory-rule basis:
  csharp-architecture.md "Config Providers MUST Validate" items 5-6.
- SiegeDefenseService.cs:96-97 (A1 N3, A4 N1), `string.IsNullOrWhiteSpace` fallback:
  behaviour-changing beyond the decided "missing or empty" scope, needs Mike.
- SiegeDefenseServiceTests.cs `rohan` to `vlandia` (Codex): not a defect; synthetic key.
FOLLOW-UP (pre-existing; no issue filed, since filing is public and this lead is not authorised):
- siege-defense.md:143 "Changes take effect on next game load": the provider and service are
  Reuse.Singleton (SiegeDefenseIoC.cs:10,13), so a JSON edit needs a full restart
  (csharp-architecture.md "Doc requirement"; file-catalogue.md:290 says so correctly).
- SiegeDefenseService.cs:352: RewardMessage `{settlement}` becomes the settlement string id
  (latent; no shipped RewardMessage uses it).
- siege-defense.md:101 says DisplayMessage is wrapped in try/catch; the one in
  GrantReward (:354) is not.
- siege-defense.md:19, :88 call SiegeEvent sealed; in v1.5.3 it is `public class SiegeEvent`.
- Popup text (DefaultMessages, JSON, "Ignore") bypasses {=KEY} localization: /localize candidate.
- A whole `"KingdomMessages": null` (or WatchedSettlementIds) is caught only because the
  provider's log line dereferences it inside the try.
- ResponseWindowDays is never read in Main, though siege-defense.md says MCM overrides it.
- SiegeDefenseService calls engine statics directly (ADR-007), already on the first review's list;
  :163 passes a `string?` to affirmativeText (breaks if TaleWorlds annotates its DLLs).
- SiegeDefenseService.cs:181-221: hourly ticks allocate with no active events (A3; already the
  first review's A3 #2).

VERDICT: READY FOR COMMIT (pending the orchestrator's Step 4.6 convergence pass and the two
NEEDS MIKE design questions, neither of which blocks the commit)
```

## Convergence

The convergence pass on `bdc80a5a` found 4 defects (1 LOW, 3 NIT) and no production-code change.
The review lead re-checked each against the code; all 4 were confirmed and fixed, with no false positives.

| # | Sev | Finding | Verified | Fix |
|---|---|---|---|---|
| 1 | LOW | The isolation test never mutated a `GetMessages("")` result, so `if (string.IsNullOrEmpty(factionId)) return DefaultMessages;` (the first half of the pre-#660 shape) passed all 31 tests | `GetMessages("")` is called only at `SiegeDefenseServiceTests.cs:385` and `:493`, and neither writes to the result | The test now mutates `_sut.GetMessages("")`, asserts `AreNotSame` against the next lookup, and the existing `AcceptButton` assert checks it. With the mutant applied, only this test failed (30 passed, 1 failed); after restoring, 31/31. `siege-defense.md:116` ("configured or defaults") is now backed by a test and stays as written |
| 2 | NIT | "the test count is back to 2,256" in `REVIEW-LOG.md` and the RCA: 2,256 is the nullable warning count | Both paragraphs use "test count" for tests elsewhere | Now "the test project's nullable warning count is back to 2,256" |
| 3 | NIT | The lesson's "Why missed" said the warnings sat at untouched lines | `git blame 503b933e`: `:39` is from `3c0a4870f`; `:393` and `:470` came from `503b933e` itself | Now says one sat at an untouched Setup line and two in the commit's own new test code |
| 4 | NIT | NOT APPLIED and FOLLOW-UP cited `SiegeDefenseService.cs` by `503b933e` numbers | The comment edit shifted each target down by one line; checked at HEAD | Renumbered: `:96-97`, `:352`, `:354`, `:163`, `:181-221` |

**Verification:** `dotnet build TAOM.Tests --no-incremental` then `count_cs86.py`: `total=2256`, 0 in
`Main`, 9 in `Features\Siege\` (the same count, since the new lines add no warning). Full suite:
`Failed: 2, Passed: 10254, Skipped: 2, Total: 10258`; the two failures are the live-Armory tests
(`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`, `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`),
the same pair as the previous run.
