# Plan review 026: seam decision logic

Reviewed: `plans/026-seam-decision-logic.md` (2060 lines) against the worktree
`E:\repos\taom-improve\wt-026` at `31cc629f` (branch `improve/026-seam-decision-logic`,
`git status` clean apart from the untracked plan file). Reviewer: cold read, no prior context.
Nothing was built or run except `python tools/lint_docs.py` (to confirm the output layout that
Step 9.3's `head -14` depends on: the dash line is line 13).

## Verdict

Executable by a weak model. The plan is unusually complete: every edit is given verbatim, every
step ends in a command with a literal expected result, RED precedes GREEN for all three services,
and the expected compiler codes for each RED step match what the edits produce. One blocking item,
and it is caused by the review process rather than the plan text.

## Blocking

1. **This review file breaks the plan's `git status` expectations** (plan lines 36-37, 614-616,
   1912-1923, 1956, 1996). `plans/_audit/` is tracked, and `plans/_audit/2026-09-23-opus/plan-review-026.md`
   is not ignored (`git check-ignore` exit 1), so once this file exists the worktree shows a second
   untracked line, `?? plans/_audit/2026-09-23-opus/plan-review-026.md`. Step 0.2 expects "one
   status line only", Step 9.4 lists the exact lines, and the Done criteria require only the plan
   file after the commit. A literal executor fails Step 0's Verify. Fix: before dispatch, move
   this review out of the worktree (or commit it on another branch), or add the line to the
   plan's three expected outputs and its "never stage" note.

## Non-blocking

1. **Off-by-one line numbers** (the text anchors are right, so the Edit tool still works; see
   "Excerpt mismatches"). Lines 112, 130 and 810 say the `ChargePlayer` doc comment starts at
   470, but `/// <summary>` is on 469 (470 is `/// Takes the player's gold...`). Step 2.1 says
   "Replace lines 470-514 (... from `/// <summary>` ...)": the range and the anchor disagree, and
   an executor who goes by line numbers leaves an orphan `/// <summary>`. Line 671 also gives the
   wrong range.
2. **3-space list indent inside the CHANGELOG and commit-message blocks** (lines 1876-1890 and
   1928-1947). In the raw markdown every line of both blocks starts with 3 spaces. The C# blocks
   do not have this indent: their raw indentation is exactly what the file needs. An executor
   who copies raw text (which the C# steps train it to do) writes a commit subject and body that
   start with 3 spaces. The subject hook strips whitespace and lets it through, but the subject
   and body are then indented, and the Step 9 Verify "subject exactly" fails. Say explicitly:
   "remove the 3-space list indentation; the subject starts in column 1".
3. **The CHANGELOG already has a `## 2026-09-25` heading** at line 184 of `CHANGELOG.md`
   (out of order, below `## 2026-09-24` at line 5). Step 8.2 (lines 1871-1873) checks only the
   first `## ` heading, so it adds a second `## 2026-09-25` at the top. That is harmless but
   untidy. Say whether to reuse the existing heading or accept the duplicate.
4. **Step 8.3 dash check depends on the locale** (line 1895). In Git Bash `$'\u2014'` expands to
   the character only under a UTF-8 locale. Otherwise it stays the literal `\u2014`, and the
   check prints `0` even when dashes exist. Step 9.3's `lint_docs.py` dash count covers this, so
   nothing is lost, but a `python -c` check or `LC_ALL=C.UTF-8` would make Step 8 sound.
5. **Step 7.4 ends in a judgment** (lines 1856-1858): "Open each reported line and confirm it is
   inside the member named here". The expected hits could instead be listed as literal code
   lines (for example
   `DestroyPlayerGold ... GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount`)
   or as a count per file (SupplyOrderService 3, WardenService 4, RefugeService 6), so the step
   ends in a comparison.
6. **Step 9.3 exit code is masked** (line 1908): `python tools/lint_docs.py | head -14` reports
   head's exit code, not the linter's, yet the expected result says "exit 0". Use
   `set -o pipefail` or drop the exit-code claim. (Verified: at `31cc629f` the dash line is line 13
   of the output, so `head -14` does show it.)
7. **Full-suite duration and the tool timeout** (lines 619, 1905). The plan runs about 10,631
   tests in the foreground with `timeout: 600000`. If the suite takes longer than 10 minutes, the
   tool kills it and the executor sees a failure with no `exit=` line. Add one line saying what
   to do (re-run with `run_in_background`, or treat a missing `exit=` line as a timeout, not a
   failure).
8. **Behaviour-risk note, already partly documented** (lines 2039-2044). `EnrolPromotedHero` and
   `RenamePromotedHero` return silently when `FindHero` or `FindTroop` misses, and
   `MintCompanionFromTroop` still returns the hero id, so `ResolveWarden` removes the soldier.
   Before the change, the hero object was in hand, so a missed lookup could not happen. The
   engine facts (lines 525-537) support the lookups, and the maintenance note names the risk.
   A reviewer may still prefer `EnrolPromotedHero` to return `bool`, with the service logging a
   warning on false. This is a design choice, not an executor hazard.
9. **Engine-read breadth in `ScanForRaiders`** (lines 1198-1212). The new seam reads
   `MapEvent`, `PartyComponent`, `MemberRoster`, `GetPosition2D` and `StringId` for every party,
   including inactive and main parties. The old code read them only after the earlier filters.
   The plan's engine facts (lines 496-501) state that these getters are safe on any non-null
   party, and I confirmed `MapFaction`'s unsafe branches in the v1.5.3 decompile, which the plan
   correctly keeps behind `IsAtWarWithRefuge`. Flagged only as the spot the `/deep-review`
   should probe, as Maintenance notes lines 2032-2035 already say.

## Excerpt mismatches (plan vs code at `31cc629f`)

- Lines 112, 130: `SupplyOrderService.cs` seam `ChargePlayer` "470-514" and "470-519". Actual:
  the doc comment starts at **469**, the method is 477-514, and `FindHero` is 516-519. The
  excerpt text is identical.
- Line 810 (Step 2.1): "Replace lines 470-514". Actual **469-514**.
- Line 671 (Step 1.3): "replace exactly these lines (266-271 at `31cc629f`)". Actual **267-272**
  in `SupplyOrderServiceTests.cs`. The text is identical.
- Line 244: "`HourlyTick` has already rejected a non-finite or non-positive `raidRange` (lines
  397-399)". Actual: the `EnableRaids` gate is 396-397, `raidRange` is read at 398, and the
  finite/positive check with its `return` is **399-400**.
- Line 433: `WardenService.cs` private helpers "lines 292-296". Actual **293-297**. The text is
  identical.

Everything else I checked matches exactly:

- **Line counts:** 654, 1226, 298, 822, 1797 and 314.
- **`[TestMethod]` counts:** 45, 112 and 20, none using DataRow.
- **Test classes:** the `RequiresGame` tags; subclasses at lines 22, 27, 1713 and 19. These four
  are the only subclasses of the three services in `Main` and `TAOM.Tests`.
- **Callers:** lines 185, 414, 48 and 85.
- **Seam code:** 1166-1204, 1206 and 1218, and `RaidThreat` at 21-33. The seam regions are at
  455, 787 and 126, and `SaneBuildHours` is at 777-785. The 128-151, 193-235 and 34-36 ranges
  match.
- **Test-file ranges:** 33-34, 61-65, 257-275, 224/238/253, 55-56, 175-179, 219, 1040 and 1224.
  The nine raid tests using `Threat` are there. Also 1713-1750, 25-26, 45-49, 74, 79 and 36.
- **Other cited files:** `RefundConsumption` `FindHero` at 585; lines 794/799, 1035 and 1093;
  `TAOM.csproj:112-119`; `CastleRecruitmentBehavior.cs:96`; `CampService.cs` 18-36, 566 and 797;
  `IWardenService.cs:6-19`; `ISupplySourceService.cs:7`; the whole `SupplyQuote` type (its
  constructor only assigns, so the negative-amount test is valid); `SubModule.xml:6` `v2.0.30`.
- **Encoding:** CRLF on all six files; a BOM only on `RefugeService.cs`, `RefugeServiceTests.cs`
  and `CHANGELOG.md`.
- **Engine facts spot-checked in the v1.5.3 decompile:** `CharacterHelper` 650-657 verbatim,
  `IFaction.IsAtWarWith` at 94, and `MobileParty.MapFaction`'s unguarded
  `Party.Owner.HomeSettlement` and `HomeSettlement.OwnerClan` dereferences.
- **Baseline Step 7 greps:** they give the counts the plan's post-change expectations assume.
- **New member and type names** (`PayLord`, `ScanForRaiders`, `RaidCandidate`, `PartyHeroInfo`,
  `PromotionSource` and the others): none collide with anything in `Main` or `TAOM.Tests`.

## Checklist

1. **Executable with only the plan and the repo:** yes. Decisions 49, 55 and 56 and the ADR-007
   amendment text are inlined, and so are the engine signatures. The gaps are non-blocking items
   2, 3, 5 and 7.
2. **Every step ends in a command with an expected result:** yes, except Step 7.4's final
   "confirm it is inside the member" (non-blocking 5).
3. **Excerpts match:** yes, apart from the four off-by-one line ranges listed above. All excerpt
   text is identical.
4. **Checklist items:**
   - **TDD order:** RED then GREEN, three times. The expected compiler codes are correct:
     CS0115 for the new overrides, CS0122 for calls to still-protected members from the test
     class, and CS0246 for the new types.
   - **Issue first:** the orchestrator creates the issue; `Refs #` is conditional.
   - **Binding rules named with summaries:** ADR-007 (with the amendment), ADR-008 and ADR-002,
     plus `csharp-architecture.md` (Engine-Float gates, which makes the NaN test mandatory and
     present) and `tests.md`.
   - **Single-owner files:** `IoC.cs`, `SubModule.cs`, `TAOM.csproj` and `Directory.Build.props`
     are out of scope with "recommend, don't edit".
   - **STOP conditions:** specific: an extra subclass, the `FindHero` and `FindTroop` shapes,
     interface changes.
   - **Done criteria:** machine-checkable.
   - **Planned-at SHA and drift check:** planned at `31cc629f`; the drift check lists the six
     code paths, and `CHANGELOG.md` is left out on purpose and explained.
   - **Commands:** every build and test command carries
     `-p:DisableModuleCopy=true -p:ModuleId=`.
5. **Dashes and secrets:** no U+2013 or U+2014 in the plan (checked with Python); no secret
   values.
