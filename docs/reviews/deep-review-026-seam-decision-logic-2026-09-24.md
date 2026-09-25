# Deep review: plan 026, seam decision logic (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 026, move TAOM decision logic out of seven protected-virtual seams (decisions 56 and 57)
Branch:  improve/026-seam-decision-logic, worktree E:\repos\taom-improve\wt-026
Diff:    e452b0c7..af6329ea (code commit a3957541, plan records af6329ea)
Date:    2026-09-24 (review run 2026-09-25)

Scope:   C# only: four services (SupplyOrderService, RefugeService, WardenService, CampService),
         their four test classes, CHANGELOG. No XML, XSLT, script or harness file. The plan file
         and its two review files are records, not review targets.
Waves:   wave 1: Agent 1 Standards, Agent 2 Engine compatibility, Agent 5 Data flow, Agent 4
         Completeness; wave 2: Agent 3 Efficiency, Agent 6 Design. Codex adversarial
         (gpt-6-astra, ultra) in parallel. Review lead: verification, fixes, Step 4.

STANDARDS:     PASS: 0 violations (2 LOW notes: note 1 is S1, fixed, and S2, needs
               Mike; note 2 is S3, fixed)
COMPATIBILITY: PASS: 40 verified, 0 incompatible, 0 unverified (1 LOW doc finding, fixed)
EFFICIENCY:    PASS: 7 issues (0 high, 0 medium, 7 low); 1 applied, 5 no change, 1 follow-up
COMPLETENESS:  INCOMPLETE until merge: the GitHub issue is owed (orchestrator files it at merge
               under Mike's standing request); code, tests, CHANGELOG and IoC complete
DATA FLOW:     PASS: 13 flows, 0 gaps, 1 inconsistency (S1, fixed)
DESIGN:        3 KEEP proposals (2 apply, 1 follow-up): 1 applied, 1 needs Mike, 1 follow-up
XML:           NOT IN SCOPE (no XML or XSLT changed)
TOOLING:       NOT IN SCOPE (no script under tools/ or .claude/hooks/ changed)
CODEX:         0 P1 / 0 P2 / 1 P3 (plan text only), complete ("END OF CODEX REVIEW" present)
```

## Details

Every finding below was re-read against the worktree code (and, for engine claims, the v1.5.3
`taom-src` cache at `C:\Users\mikew\.taom-src\v1.5.3`) before it was classified.

### Findings and verdicts

| ID | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| S1 | Agent 5 flow 6, Agent 4 #2, Agent 1 note 1a | LOW | `RefugeService.IsPrisonerAtWarWithRefuge` re-reads the refuge's faction per row and answered `false` (release, irreversible) when it is missing. The old `ReleasePeacePrisoners` captured the faction once and returned before the loop without one (`git show e452b0c7:Main/Features/Refuge/RefugeService.cs`, lines 1023-1042). Only the `RefugePrisonRosterCount` guard, checked once at the start, kept the old "no faction, release nobody" rule. Unreachable today: nothing in the walk removes the refuge or changes its faction | CONFIRMED (latent fail-open introduced by the split) | Fixed: the seam now answers `true` (keep) when the refuge or its faction is missing; `false` when the prisoner's own faction is missing stays, as in the source. Seam body, engine-only, so no unit test reaches it |
| S2 | Agent 1 note 1b | LOW | `WardenService.ReadPromotionSource` refuses without a player clan or main party, which its own read does not use; it guards the promotion decision | NEEDS MIKE | ADR-007 reading: does "a seam with its null guards" cover a guard that protects the decision rather than the read? If not, a follow-up adds a flag to `PromotionSource` and refuses in `MintCompanionFromTroop` (no new seam). Behaviour today matches the source |
| S3 | Agent 1 note 2, Agent 4 #3, Agent 5 F3 | LOW | The 8 pure `FortificationSearch.NearestDistance` tests sat in `CampServiceTests`, tagged `RequiresGame` at class level (line 24); hosted CI filters `TestCategory!=RequiresGame` (`.github/workflows/csharp.yml:67`), so the one rule behind three keep-outs never ran on CI | CONFIRMED | Fixed: moved byte for byte into the new untagged `TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs`; the two seam tests that use the service stay in `CampServiceTests` |
| C1 | Agent 2 F1 | LOW | New doc comments at `RefugeService.cs` (`ReleasePeacePrisoners` summary and `RemoveFromRefugePrisonRoster`) said the dropped row's captor "is another party". The branch runs whenever `PartyBelongedToAsPrisoner != refuge.Party`, including null: `Hero.OnRemovedFromPartyAsPrisoner` sets it to null (v1.5.3 `Hero.cs:2255-2258`, read this run) | CONFIRMED | Fixed wording: "recorded captor is not the refuge (another party, or none)" |
| P1 | Agent 4 #1 | MEDIUM | No GitHub issue for plan 026; `gh issue list --state all --search` for "seam decision logic" and "plan 026" found none (run this review) | CONFIRMED (process) | Not fixable here. Mike's standing request (2026-09-24, recorded in the plan 021 REVIEW-LOG entry) is that the orchestrator files every sprint plan's issue at merge; it then carries the owed in-game checks under `triage-needs-ingame` |
| P2 | Agent 4 #4 | LOW | `docs/features/supply-lines.md` "Tests:" omitted the payee-routing and refund tests; `docs/features/field-camp.md` `CampService.cs` row did not say the file declares `SettlementSite` and `FortificationSearch` | CONFIRMED | Fixed both lines |
| P3 | Agent 4 follow-up list | LOW | "Name rendered only for the winner" (`FindNearestHostile`) was a documented contract no test pinned; `scan.Candidates == null` guard half untested | CONFIRMED (coverage) for the name; the null-list half NOT APPLIED | Added `FindNearestHostile_SeveralEligible_RendersOnlyTheWinnersName`. The null-list half is unreachable (the production seam always returns a list or null) and its sibling half (`scan == null`) is tested |
| X1 | Codex P3 | P3 | Plan RED checks at `plans/026-seam-decision-logic.md:1181-1184`, `:2494-2497`, `:3291-3294` require a `CS0122` line the retained RED logs do not contain (`red-supply.log`, `red-refund.log`, `red-refuge-forts.log`) | CONFIRMED (plan text only) | Not edited: the plan is a record (orchestrator note). The orchestrator note says the executor recorded it as a deviation; I did not find that record in the commit body or in `E:\repos\taom-improve\scratch\plans\026\` (grep for `CS0122` and "deviat"), so the orchestrator should confirm where it lives. Lesson added |

No finding was rated a false positive: the lens notes that are not defects (Agent 3 items 2 to 6,
Agent 2's 40 verified API uses) were reported as such by their own lenses.

### Agent 1: Standards

All ten checks pass (report verbatim in the orchestrator's record). Its two notes are S1/S2 and S3
above; its follow-ups are listed under FOLLOW-UP.

### Agent 2: Engine compatibility

40 API uses verified against the installed v1.5.3 DLLs, 0 incompatible. F1 is C1 above (fixed).
FU1 and FU2 are pre-existing (FOLLOW-UP). The first in-game check it asks for is the warden
promotion, the path whose engine interaction changed shape; after the Step 4 change below, rename
and enrol act on the created hero directly again, as the source did.

### Agent 3: Efficiency

No HIGH or MEDIUM. Item 1 (pre-size the raid-scan list) applied. Items 2 to 6 (settlement distances
for all settlements, repeated `FindParty` in the peace walk, repeated `FindHero` in the mint, refund
re-reads, extra name renders) were weighed by the lens against the simplicity criterion and left
as they are; item 4's two hero scans are removed anyway by the MintedHero change. Item 7 is a
FOLLOW-UP.

### Agent 4: Completeness

76 new tests counted and matching the CHANGELOG, IoC unchanged, CHANGELOG present. P1 (issue) and
P2 (docs) above; S1 and S3 overlap Agents 1 and 5.

### Agent 5: Data flow

13 flows traced, 0 gaps; flow 6 is S1 (fixed). F1 (refund may put recruits in slots the player
cannot use) and F2 (raid scan can throw out of the hourly tick) are pre-existing: FOLLOW-UP, F1
needs Mike.

### Agent 6: Design and elegance

Three KEEP proposals; see IMPROVEMENTS.

### Agent 7 and Tooling

NOT IN SCOPE.

## Action items

1. Orchestrator at merge: file the plan 026 GitHub issue (P1), add `Refs #N`, close it with
   `triage-needs-ingame` for the seven in-game checks in the CHANGELOG entry.
2. Orchestrator: confirm where the Codex P3 plan deviation is recorded (X1).
3. Mike: the three NEEDS MIKE items below.
4. Orchestrator: the Step 4 convergence pass (one `deep-reviewer` on the fix diff) was not run,
   because the review lead cannot spawn agents; run it on the fix commit.

## Improvements (Step 4)

APPLIED:
- `Main/Features/Refuge/WardenService.cs` (Agent 6 KEEP 2): `CreatePromotedHero` returns a new
  `MintedHero` (id plus opaque `EngineHero` handle); `RenamePromotedHero` and `EnrolPromotedHero`
  take it and act on that hero instead of `FindHero(heroId)`. PRESERVING on v1.5.3 (both lookups
  resolved; Agent 2 verified), and it removes the recorded silent path where an engine change to
  hero registration would consume a soldier for an unenrolled hero. Proof: the 11
  `MintCompanionFromTroop_*` tests and the `ResolveWarden` tests unchanged and green before and
  after (step strings unchanged), plus new `MintCompanionFromTroop_CreatedHero_HandedToRenameAndEnrolAsIs`.
- `Main/Features/Refuge/RefugeService.cs` `ScanForRaiders` (Agent 3 item 1): the candidate list is
  sized to `MobileParty.All.Count` (an `MBReadOnlyList<T> : List<T>`, v1.5.3 `MobileParty.cs:257`).
  PRESERVING (capacity only). The seam body is engine-only; the `FindNearestHostile_*` and raid
  tests stay green, and the owed raid smoke covers the body.
- Fixes S1, S3, C1, P2, P3 as in the table above.

NOT APPLIED:
- `CampService.cs:39-86`, `:801-817` and `RefugeService.cs:961-987` (Agent 6 KEEP 1, walk
  `Town.AllFiefs` and delete `SettlementSite`): needs Mike. It reverses the plan's own placement of
  "which settlements count" as a tested TAOM rule (plan "Seam 7": "Decision logic to move: which
  settlements count (towns and castles only ...)"), deletes four or five kind tests, and its
  equivalence (`Campaign.OnDataLoadFinished` fills the lists from `Settlement.All`) is engine-side
  and not unit-testable. The win is microseconds per menu refresh.
- `WardenService.cs` `ReadPromotionSource` (Agent 1 note 1b): needs Mike (S2).
- `RefugeService.cs` `FindNearestHostile` null-`Candidates` test (Agent 4): the half is unreachable.
- Agent 3 items 2 to 6: the lens recommends no change (simplicity criterion); item 4 is moot after
  the MintedHero change.

FOLLOW-UP (pre-existing code outside the change; no issue filed from a review-lead run, listed for
the orchestrator's follow-up plan):
- `WardenService.cs` `Candidates()`: the `if (companion != null)` guard is now dead (Agent 6 KEEP 3,
  Agent 1). Deletion holds parity.
- `WardenService.cs` `PromotableTroopsInMainParty`: the hero and empty-stack filter is decision
  logic in a seam, the same shape the plan moved (Agent 1).
- Deferred seams that still branch: `SupplyOrderService.DeliverCargoToPlayer`,
  `RefugeService.ResolveMilitiaTroopId`, `CampService.EnumerateHostileCandidates` and
  `CollectHostiles` (Agent 1; plan "Deferred").
- Refuge stale-row drop nulls the hero's recorded captor even when another party holds him; matches
  vanilla (`PrisonerReleaseCampaignBehavior.cs:190`), impact UNVERIFIED (Agent 2 FU1).
- `SupplyOrderService.cs` comment cites `Hero.cs:1467-1480, verified 1.4.8`; on v1.5.3 the
  registration is at `Hero.cs:1547-1565` (Agent 2 FU2, a context line of the diff).
- Raid scan over all of `MobileParty.All`; the engine locator would visit only nearby parties but
  changes tie order (Agent 3 item 7).
- Refund can place recruits above the player's recruit index or with a hidden notable (Agent 5 F1):
  needs Mike.
- `MobileParty.MapFaction` can throw out of the hourly raid tick with no catch (Agent 5 F2; raids
  off by default).
- ADR-007 "Why" still says "plan 026 moves that logic" and lists four seams; `plans/README.md:42`
  still says "plan being written"; the CHANGELOG has two `## 2026-09-25` headings;
  `CanUpgrade_TooCloseForAStronghold_Blocks` does not assert `party:r1`; the untracked
  `docs/reviews/codex-adversarial-026-...-2026-09-24.prompt.md` date (Agent 4).
- The refuge mint passes `bornSettlement: null`; FieldCommission resolves a birthplace instead
  (Agent 6; UNVERIFIED, `/research` `HeroCreationModel.GetBornSettlement` first).

VERDICT: READY FOR COMMIT (Step 4 complete; the convergence pass is owed to the orchestrator; full
suite green: Passed 10707, Skipped 2, Failed 0 in the worktree after the fixes).

## Test evidence

- Before any edit, the four affected classes: `Passed: 334, Failed: 0`
  (`E:\repos\taom-improve\scratch\review-026-lead\before.log`).
- After the edits, the four classes plus `FortificationSearchTests`: `Passed: 336, Failed: 0`
  (`after.log`; 334 plus the two new tests, the eight moved tests counted once).
- Full suite after the edits: `Passed! - Failed: 0, Passed: 10707, Skipped: 2, Total: 10709`
  (`full.log`). The branch contains `a39a9c86`, so no failure was allowed.

## Codex review

Raw output: `docs/reviews/raw/codex-adversarial-026-seam-decision-logic-2026-09-24.md` (gpt-6-astra,
reasoning ultra, 160,777 tokens, reviewed `e452b0c7..af6329ea`). It quoted the v1.5.3 dump for
every engine boundary the moved code touches (`GiveGoldAction`, the object managers, the prison
roster and `EndCaptivityAction`, `CharacterHelper`, `HeroCreator`, the settlement lists), traced
ten concrete decision paths, both moved catch boundaries, the lifecycle (first boot, load, new
campaign, mission, dedicated server, co-op UNVERIFIED) and every MCM default involved, and disputed
nine of its ten Known Suspects with evidence.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | P3 (plan text) | Yes | The three RED checks require `CS0122`; the three retained logs contain only `CS0115` (Codex read them; the plan lines were re-read this run). No code impact. The plan is a record, so no edit |
| KS 1-10 | dispositions | agree | Yes | Suspect 3 folds into finding 1; the others are disputed with the cited lines, which match the lenses' reads |

- **Confirmed bugs:** none in code.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** S1 (the fail direction of the per-row refuge war check; Codex's
  "Missing refuge/faction" row checked only the initial count), S3 (pure tests under a
  `RequiresGame` class), C1 (the captor can be null), P2 (two feature-doc lines), P3 (the
  winner-only render not pinned). Codex did find the plan's CS0122 defect no lens reported.

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | RED check requires a diagnostic the compiler does not emit | Other: over-specified verification step | The plan predicted the compiler's exact output instead of the diagnostics that prove the test is RED | Lesson in `lessons/misc.md` |

## AGENTS.md lessons (pending, Phase 3h consolidated later)

- **Bugs Codex typically misses:** when a precondition captured once before a loop is split into
  per-row seams, the fail direction of each seam's missing-input default (S1). Codex traced the
  missing-faction case only at the loop's entry.
- **Bugs Codex typically misses:** pure tests added to a class tagged `RequiresGame` never run on
  hosted CI (S3).
- **What Codex does well:** read the retained RED and GREEN logs and caught a plan instruction
  that no execution could satisfy, which no lens checked.

RCA: `docs/reviews/rca-seam-decision-logic-2026-09-24.md`.

## Convergence

A convergence pass on `af6329ea..12b1c200` found no code or test defect: the refuge peace-release
guard, the `MintedHero` hand-off, the raid-scan pre-size, the moved `FortificationSearchTests` and
the two new tests all matched the source and the v1.5.3 engine. It found two LOW accuracy errors
in the review records, both confirmed against the files and fixed:

| ID | Where | Defect | Fix |
|---|---|---|---|
| D1 | `CHANGELOG.md`, the `fix(seams)` entry | "the warden promotion smoke above" pointed up; the smoke is listed in the `refactor(seams)` entry below it | Now reads "in the refactor entry below" |
| D2 | RCA top-line; this report's STANDARDS line | The RCA reached seven findings by counting P2 as two doc gaps and leaving P1 out of the count; the STANDARDS line named Agent 1's two notes as S1 and S2, while its section says the notes are S1/S2 (note 1) and S3 (note 2) | RCA counts P1 as the process gap and P2 as one doc gap (two feature docs); the STANDARDS line now matches the Agent 1 section |

False positives: none. Full suite after the fixes (docs only, no code change):
`Passed! - Failed: 0, Passed: 10707, Skipped: 2, Total: 10709`
(`E:\repos\taom-improve\scratch\review-026-convergence\full-suite.log`).
