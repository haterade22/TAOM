# Cold review: plan 026 (extended to seven seams, decision 57)

**Plan:** `E:/repos/taom-improve/wt-026/plans/026-seam-decision-logic.md` (3,722 lines)
**Code checked:** worktree `E:/repos/taom-improve/wt-026` at `e452b0c7`, branch
`improve/026-seam-decision-logic` (`git diff --name-only 31cc629f HEAD -- Main TAOM.Tests` is empty).
**Template:** `E:/repos/TAOM/.claude/skills/improve/references/plan-template.md`, "Quality bar".
**Verdict:** no blocking findings; executable by a weak model. Four excerpt/line-number
mismatches and a handful of minor gaps, listed below.

## What I verified (evidence read this turn)

- **Worktree state:** HEAD `e452b0c7`, branch as named, status exactly the two expected `??` lines.
- **File facts:** line counts 654 / 1226 / 298 / 955 / 822 / 1797 / 314 / 1257 match the table
  (plan lines 147-154); all eight files are CRLF throughout; BOM present on exactly
  `RefugeService.cs`, `RefugeServiceTests.cs`, `CampService.cs`, `CampServiceTests.cs` (plan line 34-35).
- **Class baselines:** `[TestMethod]` counts 45, 112, 20, 81; no `[Ignore]`, no `DataRow`; no other
  class name contains the filter strings, so the filtered counts are clean.
- **Every "Current state" excerpt** was opened and compared: seam 1 (`SupplyOrderService.cs:469-519`),
  seam 2 (`RefugeService.cs:1166-1204`, `RaidThreat` 21-33, `StartRaid` 1206, `FindParty` 1218),
  seam 3 (`WardenService.cs:128-151`), seam 4 (193-235, constants 34-36, helpers 293-297),
  seam 5 (`SupplyOrderService.cs:574-624` plus helper), seam 6 (`RefugeService.cs:1019-1042`,
  `OnPeaceMade` 516-520), seam 7 (`CampService.cs:745-761`, `RefugeService.cs:830-854`), and every
  test-subclass excerpt and line anchor. Code text matches character for character; the only
  mismatches are line numbers, listed below.
- **Engine facts spot-checked** in `%USERPROFILE%/.taom-src/v1.5.3/`: `Settlement.Notables` is
  `MBReadOnlyList<Hero>` (267), `Settlement.All` (485), `Hero.VolunteerTypes` field (46),
  `TroopRoster.Count` (46), `GetCharacterAtIndex` throws past `_count` (567-574). All as quoted.
- **Name collisions:** none of `RaidCandidate`, `RaidScan`, `RefugePrisoner`, `PartyHeroInfo`,
  `PromotionSource`, `SettlementSite`, `FortificationSearch`, nor the new test members/helpers
  (`Hostile`, `Prisoner`, `TownSite`, `VillageSite`, `Consumed`, `PartyHero`, `SiteReads`, ...),
  exist in `Main`, `TAOM.Tests` or the imported TaleWorlds assemblies' metadata strings.
- **Only one subclass per service** (plus `HeroMergeProbeService`, which overrides none of the nine).
- **Step 15 greps run at baseline:** the four-file engine-call grep returns 20 hits today; I
  recomputed the post-change set from the plan's code blocks (every new doc comment checked for
  accidental matches) and it is exactly the 20-line multiset in plan lines 3376-3401 (5 + 4 + 9 + 2).
  Step 15.2 gives 1 / 4 / 2 / 2 = the nine internal methods; 15.3 stays 1 and 5; 15.6 leaves one hit.
- **Counts:** new tests 10 + 16 + 16 + 13 + 8 + 10 + 3 = 76 (Supply 23, Refuge 27, Warden 16,
  Camp 10); filtered finals 68 / 139 / 36 / 91; suite 10629 + 76 = 10705, total 10707. All
  consistent across Status, Why, Steps, Test plan, Done criteria, CHANGELOG and commit text.
- **Every new test's expected event list** was hand-traced against the plan's service code and
  overrides (charge sequences, refund slot walks including the per-type re-read and truncation, the
  raid filter order and war-check order, the 5-row mixed prisoner roster, the mint step indices and
  ages 29 / 33 / 22). All pass on paper.
- **Existing tests stay green on paper:** the raid tests (range reaching the seam is always finite
  and positive, so the synthetic candidate at 0f is picked), the 20 warden tests (`MintCalls` now
  counted in `ReadPromotionSource`, once per mint), `OnPeaceMade_ReleasesForEveryRefuge`, the
  fort-distance readers, and every SupplyOrder test that now runs the real charge/refund logic
  (no existing logger `DidNotReceive` on those paths; only line 268 asserts `CallSequence`).
- **Behaviour preservation:** all six log texts identical; iteration order kept (MobileParty.All,
  roster, dictionary, notable order); strict `<` kept in all three distance picks; NaN tests present
  for both moved gates; truncation silent with its own test and STOP; backwards prisoner walk kept
  (count read once, each row re-read by index) with two tests and a STOP; `MapFaction` still read
  only after the same filters (party and hero); RNG draw order in the mint unchanged.
- **Commit:** subject 64 characters; body lines all at most 72 after the 3-space de-indent.
- **Dashes:** zero U+2014 / U+2013 in the plan and in the existing review file (both untracked
  markdown that `lint_docs.py` scans whole). `lint_docs.py` exits 0 without flags.
- **Hooks:** `check-changelog-changed.sh` only fires on `.claude/`, `CLAUDE.md`, `AGENTS.md`; the
  subject hook reads `SubModule.xml` from the main tree, which is `v2.0.30` in both trees.

## Blocking

None.

## Excerpt mismatches

1. **`RestoreVolunteerSlots` ends at line 647, not 646; `ShowMessage` is at 649, not 648.** Plan
   lines 147 ("626-646"), 513 ("626-646"), 517 ("ShowMessage (line 648)") and 2487 ("lines 574-646").
   Step 8's text anchor ("down to the closing `}` of `RestoreVolunteerSlots` ... just above the blank
   line before `ShowMessage`") is correct, so an executor who follows the text is fine; one who
   selects 574-646 by number leaves a stray `}` and gets a build error at Step 8.3.
2. **Seam 5-7 reference grep undercounts unrelated hits.** Plan lines 173-175 say "Two more hits are
   unrelated"; the grep prints four: `Main/Adapters/IMapReachAdapter.cs:31`,
   `Main/Adapters/MapReachAdapter.cs:52`,
   `Main/Features/ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:86`
   and `docs/features/army-targeting.md:79`. All are `GetNormalizedDistanceToNearestFortification`
   and out of scope, so no harm, but a literal executor comparing output may pause.
3. **"No doc under `docs/` names any of the four" (plan line 164)** holds at `31cc629f` only. At the
   worktree HEAD `e452b0c7` the same grep over `docs/` hits `docs/adrs/007-adapter-pattern.md`,
   `docs/reviews/deep-review-021-...md`, `docs/reviews/lessons/misc.md` and
   `docs/reviews/rca-architecture-rule-amendments-2026-09-24.md`. None is a feature doc, so the
   conclusion (no doc changes) stands.
4. **Minor line ranges:** `UnwindPromotion` is 112-124 (plan line 393 says 111-124);
   `HeroMergeProbeService` closes at 1751 (plan lines 385 and 940 say 1713-1750).

## Non-blocking

1. **The drift check is vacuous today.** Local `bannerlord-1.5.x` in `E:/repos/TAOM` is at `31cc629f`
   (the planned commit itself) while `origin/bannerlord-1.5.x` is at `e452b0c7`, so
   `git diff 31cc629f..bannerlord-1.5.x` compares the commit with itself (plan line 57). Step 0.2's
   worktree checks are the real guard, so execution is safe; for merge-time drift use
   `origin/bannerlord-1.5.x`.
2. **The prefix rule has unlisted exceptions.** Lines 20-24 say every Steps command starts with
   `cd E:/repos/taom-improve/wt-026 && ` except the drift check and the `mkdir`; the RED-step `grep`
   commands (Steps 1.6, 3.5, 5.6, 7.6, 9.4, 11.4, 13.4) and `date +%F` (16.1) do not. They use
   absolute paths, so they work as written; name them as exceptions too.
3. **Totals-line spacing.** Step 0.4 says "spacing may differ"; the later Verify lines
   (`Failed: 0, Passed: 55`, ...) do not repeat it, while dotnet prints `Failed:     0, Passed:    55`.
   A literal matcher could stumble; one sentence in "Commands you will need" would cover all steps.
4. **Refund nuance not in Maintenance notes.** For an unknown troop id with a positive count, the old
   code skipped before the slot walk; the new `RestoreVolunteerSlots` reads a snapshot and calls
   `FillVolunteerSlot` for free slots, each a no-op because the troop lookup fails. The engine outcome
   is identical (nothing written, the next type re-reads), but a reviewer should know the fill seam
   runs for unknown ids. Same for `ReturnTroopsToLord` / `ReturnGoodsToSettlement` (called, no-op).
5. **Prisoner walk re-finds the refuge per row.** `ReadRefugePrisonerAt` and
   `IsPrisonerAtWarWithRefuge` call `FindParty` each time; the old loop held one roster reference.
   Maintenance notes cover the faction re-read but not the roster; if the refuge party vanished
   mid-walk the new code skips the remaining rows. Not reachable from `EndCaptivityAction` on another
   hero in practice, and the plan forbids decompiling to "improve" it; worth one sentence.
6. **Allocation estimate.** `SettlementSite` (two bools and a float) packs to 8 bytes, so 1,002
   entries is about 8 KB, not "about 12 KB" (plan line 3674). Cosmetic.
7. **Drift-check paths vs Scope.** The drift check lists 8 paths; Scope has 9 (`CHANGELOG.md` left
   out on purpose with a stated reason, plan lines 64-65). Acceptable.
8. **RED error-code sets were reasoned, not compiled.** I could not run the RED builds without
   editing the worktree. The listed codes (CS0115, CS0122, CS0246, CS0103) are what C# reports for
   these edits (the test class is not derived from the service, so a protected call is CS0122, not
   CS1540); error-typed members should not cascade. If an executor sees an extra code, the plan's
   "fix your edit and re-run; if it persists, STOP" handles it without harm.

## Quality bar

- Self-contained: yes; every step names files, anchors on text, and inlines the code.
- Every step ends in a command with an expected result: yes.
- TDD: each GREEN (2, 4, 6, 8, 10, 12, 14) follows its RED (1, 3, 5, 7, 9, 11, 13) with an exact
  compile-failure expectation.
- STOP conditions are plan-specific (truncation, backwards walk, the two fortification copies,
  `FindHero`/`FindTroop` shape, stray subclass overrides, interface changes).
- Done criteria machine-checkable; all build/test commands carry `-p:DisableModuleCopy=true -p:ModuleId=`;
  `./build.ps1` forbidden.
- No em or en dashes in the plan prose.
