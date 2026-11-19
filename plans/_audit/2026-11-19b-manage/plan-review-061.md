# Cold review: plan 061, round 1

Reviewer: cold plan reviewer, read-only. Plan read at
`plans/061-prisoner-recruit-resource-gate.md` (1183 lines) against the worktree at `db5680c8a`.
No earlier review file for 061 existed in `plans/_audit/2026-11-19b-manage/`.

No dotnet run was made (the role is read-only and the prompt did not allow it), so the baseline
totals (plan line 21) and the binding gate on the base are UNVERIFIED. The RED and GREEN oracles
were simulated by hand against the code (see "Checked and found correct").

## Blocking

### B1. The stale-claim grep leaves four live "ungated" claims (plan lines 1053-1057, 1115, 435, 1139)

Step 8's `git grep -n -i -E "ungated prisoner|prisoner recruit is not|\(ungated, #563\)|#563 tracks"`
is a phrase grep, not a search for the duty. A search for `#563` across the repo at `db5680c8a`
finds these claims that become false once this plan lands, and the pattern matches none of them:

- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml:15`:
  "recruit_cost charges the notable route, gated, and a prisoner recruit, ungated (#563)."
- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml:35`:
  "the recruit cost is charged when the player recruits a captured troll, a path Patch51 does not
  gate (the balance floors at 0, #563)". Still literally true of Patch51, but it states the path as
  ungated.
- `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml:81`:
  "(the engine raises OnUnitRecruited there too, ungated, #563)".
- `docs/features/dire-wolf.md:108`: "A prisoner recruit is charged afterward with no gate, so it
  succeeds at a zero balance (#563, open, the same for every elite creature)."

The executor cannot fix them: none is in Scope (plan lines 405-422), and the STOP list (line 1139)
makes any ModuleData XML edit a STOP. The Done criterion "the Step 8 stale-claim `git grep` prints
nothing" (line 1115) passes while these ship. Fix: add `docs/features/dire-wolf.md` and the three
comment edits in `troop_resource_costs.xml` to Scope with exact replacement text (comment-only, no
attribute changes), carve the comment edit out of the STOP condition, add both paths to the drift
check (line 9) and the staged-path count (lines 405, 453, 1075, 1077), and widen the grep to the
whole repo by distinctive words, for example
`git grep -n -i -E "ungated|no gate|does not gate" -- . ':!docs/changelog-archive' ':!CHANGELOG.md' ':!docs/reviews' ':!plans'`
filtered to lines that also name `#563` or "prisoner", with the expected result stated.
(Historical files: `CHANGELOG.md:8775`, `docs/changelog-archive/...`, `docs/reviews/...` keep their
history and are correctly excluded.)

## Non-blocking

1. **Binding gate has no base run (lines 395, 460-466, 1061-1069, 1113).** Step 1 records the build,
   the suite and the doc lint, but not the binding gate, which Step 9 and the Done criteria require
   to exit 0. The gate runs with `TAOM_RUN_BENCHMARKS=1` and `MapInconclusiveToFailed`
   (`TAOM.Tests/binding-gate.runsettings`), so a machine-state failure on the base would look like
   the executor's. Add the gate to Step 1 and compare at Step 9.
2. **Untracked plan files (lines 1070-1071, 1117).** "only the 16 Scope paths (plus the pre-existing
   untracked `plans/_audit/` folder ...)". The run worktree at `db5680c8a` also shows untracked
   `plans/060-upgrade-skill-regression-templated-edges.md`, `plans/061-...md` and `plans/062-...md`.
   Say "plus untracked files under `plans/`" so the check can pass as written.
3. **Step 4 has no exact count (lines 842-844).** "Failed 2 ... and every other test in the class
   passing". The class has 9 `[TestMethod]` and one `[DataTestMethod]` with 5 `[DataRow]`s today;
   state the expected Total and Passed so a partial discovery cannot pass.
4. **Step 8.2 asks the executor to write prose (lines 1024-1026).** "Also add, in the same bullet
   after the yellow charge line clause, the two new lines" gives the content but not the sentence or
   the insertion point. Give the exact clause and the anchor text it follows.
5. **A doc count the plan makes staler.** `docs/features/special-resources.md:326` says
   "`SpecialResourceMessagesTests.cs`: 5 tests" (already not 5: 9 methods plus 5 data rows). The
   plan adds 2 more and Step 8 does not touch the line. CLAUDE.md asks that such a count name the
   command that prints it or be dropped; add a Step 8 item.
6. **RED step builds Main, not the test project (lines 726-728).** `dotnet test` builds `TAOM.Tests`
   implicitly, so it works, but the template asks the RED step to build the project holding the
   test (`dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`) so a compile error is
   told apart from an assertion failure.
7. **Co-op effect stated only as a "known limit" (lines 334-344, 1034).** Under BannerlordCoop a
   guest's prisoner recruit costs nothing in a session today; after this plan the guest is refused
   it whenever the guest's own (possibly stale or zero) client balance is short. That is a new
   player-visible restriction, not only an unchanged limit. Say so in the commit body and the
   feature-doc paragraph so the maintainer approves it knowingly.
8. **Drift check omits files whose excerpts the plan relies on (line 9).** `SpecialResourcesBehaviorLineCountTests.cs`
   (the 276 ceiling, line 331), `SpecialResourceStorageService.cs` and `SpecialResourceConfigProvider.cs`
   (lines 275-279) are quoted in Current state but are not in the drift check. Optional.

## Excerpt mismatches

- Lines 201-203: "The `PartyScreenClosedEvent` (raised at :1284, after :1264)". In
  `TaleWorlds.CampaignSystem.Party.PartyScreenLogic.cs` (v1.5.7), :1284 is inside
  `OnPartyScreenClosed(bool fromCancel)` (starts about :1278), a separate public method, not later in
  `DoneLogic` (:1228-1276). The ordering "prisoners first, queued upgrades second" holds only because
  the screen closes after Done; the plan should say that instead of implying one method.
- Line 308: "its baseline file is empty". `TAOM.Tests/Infrastructure/Localization/unregistered-loc-keys-baseline.txt`
  is 15 comment lines and no entries; `LoadBaseline` skips `#` lines, so the effect is as stated.
  Wording only.

## Checked and found correct

- Patch excerpt `PartyScreenLogic_AddCommand_Patch.cs:27-77` (78 lines), line 31 and line 72
  (`.GetField("<TotalNumber>k__BackingField", ...`) match.
- `SpecialResourceService.cs:237-258`, `:248-251` comment, `:260-284`, `:347-370` match;
  `FiniteFloatValidator` is already imported (`using TAOM.Core.Validation;`, line 4).
- `ISpecialResourceService.cs:31-32` and `:42`, `IOnPartyUpgradeResourceCheck.cs`,
  `PartyUpgradeResourceCheckHook.cs`, `SpecialResourceMessages.cs:38-43` (class is `internal static`,
  visible to tests), `RecruitmentVM_RecruitGate_Patch.cs:69-75` match.
- `SpecialResourcesBehavior.cs` is 276 lines, `:203-205` match; `Ceiling = 276` at
  `SpecialResourcesBehaviorLineCountTests.cs:14`.
- `ReflectionSiteBindingTests.cs:106` DataRow and `reflection-sites.md:93` match; the label test
  strips comments and checks the line contains `TotalNumber`, so the Step 6.4 grep for
  `k__BackingField` finds the right N. The inserted code holds no `//` or `/*` inside a string.
- `CoopVetoClassificationTests.cs:286-289` matches; the only reason constraint is non-empty.
- `SpecialResourceGoldOnlyTests.cs`: 24 `[TestMethod]`, `ChargeRecruitCost_GoldOn_ChargesNothing`
  at 178, `// ── Upgrade ──` at 187, `Elite` has `recruit_cost="7"`.
- Engine (taom-src, v1.5.7): `PartyCommand` is a class with `TotalNumber { get; private set; }`
  (:77-91); `FillForRecruitTroop` (:150-158); `CurrentData` (:415); `RecruitTroop` case (:577-579);
  `AddCommand` only calls `ProcessCommand` (:592-595); `RecruitPrisoner` (:942-970);
  `PartyScreenData.RecruitedPrisonersHistory` public field (:38); `DoneLogic` :1228, :1264, :1271;
  `FireCampaignRelatedEvents` :1295-1310; `AddRecruitToHistory` merges per troop (:1477-1488);
  `RecruitPrisonersCampaignBehavior.OnMainPartyPrisonerRecruited` :126-133 (`OnUnitRecruited(troop, 1)`);
  `PartyVM.ExecuteRecruit` sets 1, 5 or `NumOfRecruitablePrisoners`; `FillForRecruitTroop` is called
  only from `PartyVM` and `PartyScreenLogic` in the decompile. No TAOM patch on `ProcessCommand` or
  `RecruitPrisoner` (`git grep` over Main, Client, CoopCompat).
- Blast radius lines (`SubModule.cs:1296,1932,1936`, `SpecialResourcesIoC.cs:21`): match. No
  hand-written implementer of either interface in Main, Client, CoopCompat or either test project
  (CoopCompat tests use `Substitute.For<ISpecialResourceService>()`).
- RED simulation of the new class against the stub: 9 fail (partial, below one, zero, already
  recruited, other troops, queued upgrade, NaN, charge after clamp, GetRecruitCost), 5 pass, as
  Step 2 states. Gold-only: 1 fail, 1 pass. GREEN: every oracle holds (120/50 gives 2; 60-50=10 at 5
  gives 2; 100-60=40 below 50 gives 0; NaN fails the finite gate; charge 100 leaves 20).
- Step 9 arithmetic: base 43028+3+9 = 43040; +18 = 43058; 43045 passed + 4 failed + 9 skipped = 43058.
- Patch length after the edit: 78 + 1 + 1 + 44 = 124 lines, under 150.
- `taom_recruit_needs_resource` registered at `taom_module_strings.xml:2504` with the same default;
  one C# literal today and one after Step 5.
- Issue #563 is OPEN ("Elite creature prisoners recruit for free..."). `<Version value="v2.0.40" />`.
  Subject is 64 characters. Drift-check paths equal the 16 Scope paths. All dotnet commands carry
  both flags. No worktree path or branch name, no CHANGELOG step, no protected or single-owner edit.
- Prose: the only em dash in the plan is at line 326, inside a quoted excerpt of existing code that
  the plan replaces. No secret values.
