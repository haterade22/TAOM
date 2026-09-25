# Cold review: plan 022 (Order of Battle Auto-Assign)

Reviewed file: `plans/022-order-of-battle-auto-assign.md` (untracked in the main tree, 790 lines).
Checked against the template `.claude/skills/improve/references/plan-template.md` ("Quality bar")
and against the code at `a39a9c86` (`git show a39a9c86:<path>`), the v1.5.3 decompile cache
`C:\Users\mikew\.taom-src\v1.5.3\`, and the tools the plan drives. No build or test was run;
test-count baselines are therefore unverified.

**Verdict:** not executable by a weak model as written. Three deterministic verification
failures (below) would make a literal executor STOP. Each fix is a one-line edit to the plan.
The design, TDD order, scope, single-owner handling and engine facts are otherwise sound.

## Blocking

1. **Step 0 stub-line check is wrong (plan line 316).** The plan says
   `grep -n "Phase-1 stub" .../UI/OOBButtonsVM.cs` shows lines 81, 88, 143 and 184. At
   `a39a9c86` it shows **81, 88, 143, 146 and 184**: line 146 is
   `DisplayMessage($"Preset \"{preset.Name}\" — Load is a Phase-1 stub (apply-to-OOB not yet wired).", ...)`.
   A literal executor treats this as the "Current state doesn't match" STOP condition before
   writing any code. Fix: list the five lines.
2. **Step 4 verify count is wrong (plan line 565).** `grep -c "Phase-1 stub" .../OOBButtonsVM.cs`
   is expected to print `2`. After Step 4 removes lines 81 and 88, lines 143, 146 and 184 remain,
   so it prints **`3`**. The step fails every time, and after two attempts the executor STOPs.
   Fix: expect `3`, or use `grep -c "Auto-Assign is a Phase-1 stub"` and expect `0`.
3. **Step 7 dash check contradicts the edits it verifies (plan lines 722, 725, 732).** Step 7 edits
   `docs/features/companion-tactics.md` line 8
   (`2. **FormationPresets** — saveable named OOB ...`) and the Tests bullet at line 159
   (`` `FormationPresets/HeroAutoAssignerTests.cs` — 7 tests; ... ``). Both lines already
   contain an em dash. `git diff -U0` prints each edited line as a `+` line, so the Step 7
   check prints a non-empty list, even though the plan says pre-existing dashes are exempt and
   must not be rewritten. A new `OOBButtonsVMTests.cs` bullet copied from the neighbouring
   bullets would also carry the dash. Fix: give the full replacement text for line 8 and both
   bullets with a colon instead of the dash (for example
   `` - `FormationPresets/HeroAutoAssignerTests.cs`: 17 tests; ... ``), and say that edited lines
   lose their dash.

## Non-blocking

- **Drift check never fires (lines 10 to 14, 305, 316).** The worktree is created at `a39a9c86`
  itself, so `git diff --stat a39a9c86..HEAD` inside it is always empty (the plan even says
  "expected: empty output"). Any change to these files on `bannerlord-1.5.x` since planning
  goes unseen, and the branch starts from a stale base. Suggest running
  `git -C E:/repos/TAOM diff --stat a39a9c86..bannerlord-1.5.x -- <paths>` before creating the
  worktree.
- **Drift paths omit files the plan depends on.** `Main/Adapters/HeroCombatAdapter.cs`,
  `tools/harvest_literal_loc_keys.py`, `tools/translate_with_claude.py` (the Step 6 script uses
  `REPO_ROOT`, `TAOM_LANG_DIR`, `LANGUAGES`, `sync_missing_ids`), `TAOM.Tests/Infrastructure/Localization/`
  and `AGENTS.md` (the CHANGELOG condition). `CHANGELOG.md` and `plans/README.md` are in Scope
  but not in the drift list, which is acceptable because those files churn.
- **The full-suite baseline is not exact (lines 260, 737, 759).** "10,313 or more passed" plus 12
  gives no exact target, and Step 0 runs only the filtered suite. Suggest a full
  `dotnet test ... -p:ModuleId=` run in Step 0, recording its passed count as the baseline.
- **`lint_docs.py` with no flags always exits 0 (lines 264, 732, 738, 765).** It returns 1
  only under `--fail-on-dead` or `--fail-on-drift` (see the tail of `main()` at `a39a9c86`), so
  the gate proves nothing. The new feature-map row adds a link, so `--fail-on-dead` is the useful
  flag. Note that this can fail on pre-existing dead links, so measure it at baseline first.
- **CHANGELOG instruction assumes a structure the file lacks (line 729).** There is no
  "unreleased section". `CHANGELOG.md` at `a39a9c86` uses dated `## 2026-09-23` headings, each
  holding `### <commit subject>` entries with a body. Say whether to add a `## <today>` heading
  or append under the top date, and give the `### feat(tactics): v2.0.30 - ...` header shape.
- **The handler-wiring fact names the wrong method (line 218; STOP condition line 782).** The two
  delegates are assigned in `private void InitializeFormationCallbacks()` (OrderOfBattleVM
  lines 569 to 592). `public void Initialize(...)` (line 702) calls it at line 717. A reader who
  checks the `Initialize` body for the assignments will not find them and may raise the line-782
  STOP falsely.
- **`HasFormation` does not mean "has troops" (lines 233, 724).** The decompile sets
  `HasFormation = Classes.Any(c => c.Class != FormationClass.NumberOfAllFormations)`
  (OrderOfBattleFormationItemVM line 959): it is true when the formation has a class, not when it
  has troops. The code is unaffected, but the Step 7 doc prose ("fills formations that have
  troops") would be inaccurate.
- **`TextObject.SetTextVariable(...).ToString()` has no unit-test precedent (lines 228, 553 to 556).**
  The cited precedent `CareerScreenVM` (`TAOM.Tests/Features/CareerSystem/CareerScreenVMTests.cs:500`)
  only calls `ToString()` on variable-free `TextObject`s. No test under `TAOM.Tests` calls
  `SetTextVariable`. If variable substitution throws in the test host, the Step 4 GREEN test fails,
  and no STOP condition covers it. Name it as a STOP condition, or have the delegation test return
  `NoneAssigned`.
- **`DeploymentFormationClass` numbering is UNVERIFIED by this review (line 198).** It is not in the
  taom-src cache. `GetOrderOfBattleClass` confirms the member names `InfantryAndRanged`,
  `CavalryAndHorseArcher` and `Unset`, but not their numeric values. This affects comments only.
- **Line numbers go stale after earlier steps.** Step 1.5's "line 31" becomes 34 after Step 1.4's
  three usings. Step 2's "lines 11 and 12" become 13 and 14 after Step 1.3's usings. Step 3.5's
  "line 113" and Step 4's "lines 73 to 89" also shift after Step 3 adds a field and a constructor
  parameter. Each is paired with identifying text, so the risk is low. Saying "by content, not
  line" would remove it.
- **The doc test count is already off (line 725).** At `a39a9c86` the `[TestMethod]` sum under
  `TAOM.Tests/Features/CompanionTactics` is **85**, but the doc headline says 84, because
  `TroopStanceManagerTests.cs` has 9 tests and its bullet says 8. After the plan the sum is 97. The
  plan says to write measured numbers, but the per-file bullets will not add up unless the
  TroopStanceManager bullet is also corrected. Say so or accept it.
- **Undefined references for a cold executor (lines 25, 247, 729, 788).** "D21", "D19"
  (quoted, fine), "D37", "plan 025" and "plan 020". These are informative only.
- **The plan file is not in the worktree.** `plans/022-*.md` is untracked in the main tree, so a
  worktree at `a39a9c86` does not contain it. `plans/README.md` at `a39a9c86` has no 022 row; the
  plan does allow for that.
- **Worktree creation has no failure path (line 305).** If `E:/repos/wt-022` or the branch already
  exists, the command fails and the plan does not say what to do. Add a STOP.
- **"Category: feature" (line 22) is not one of the template's category values.** This is trivial.

## Excerpt check against `a39a9c86`

Mismatches: (1) the Step 0 grep line list (five matches, not four); (2) the Step 4 expected
count (3, not 2); (3) handler wiring is in `InitializeFormationCallbacks`, not directly in
`Initialize`; (4) the `HasFormation` semantics.

Matched exactly: `HeroAutoAssigner.cs:14-50` (51 lines), `IHeroAutoAssigner.cs:6-17` (17 lines),
`CompanionTacticsIoC.cs:25-29` (36 lines), `OOBButtonsVM.cs` constructor 55 to 65, stub 73 to 89,
`DisplayMessage` 216 to 219, usings 1 to 9, 220 lines, `OOBOverlayService.cs` 41 to 51, 70, 113
(134 lines), `HeroAutoAssignerTests.cs` (93 lines, 7 tests, `_roles`, `_sut`, `MakeHero`, line 31
comment), `RoleTooltipDecorator.cs` 76 to 79 and 135 to 142, `IOrderOfBattleVMTracker.cs` 5 to 10,
`OOBButtonsOverlay.xml` lines 24 and 32, `Main/IoC.cs:158`, `Main/SubModule.cs:1719`,
`CombatRole` values, `IFormationPresetService.Presets` type, `ICompanionTacticsSettingsProvider`
members, the `HeroCombatAdapter(Hero)` constructor, the record precedent `SaveDefinerRecord.cs:9`,
`MissionAdapterFactoryTests.cs:25`, the companion-tactics doc lines 8, 50, 52, 109 and
`## Configuration`, and the feature-map line 19 `CombatMechanics` row (no CompanionTactics row).
Engine members and line numbers cited in "Engine facts" 2 to 4 all match the v1.5.3 cache, apart
from item 218 above.

Tool behaviour verified statically at `a39a9c86`: `harvest_literal_loc_keys.py` has
`--dry-run`/`--apply` and prints `unregistered:`, `<file>  +N` and `wrote N rows -> <file>`. A
replica of its scan finds 792 declared keys and **0 unregistered** at baseline, and no existing
`oob_autoassign` key. `sync_missing_ids` run against the 12 language files finds **0** missing ids
at baseline, so `seeded 36` is the correct expectation.

## Template checklist

| Item | Result |
|---|---|
| TDD order for C# | Pass: Step 1 RED, 2 GREEN, 3 RED, 4 GREEN; Step 5 boundary named untestable |
| Issue-first | Pass (line 24) |
| Binding ADRs named with summaries | Pass (ADR-002, 003, 004, 005, 007, 008, D19, csharp-architecture) |
| Single-owner files | Pass: IoC.cs, SubModule.cs, TAOM.csproj out of scope, lines cited and verified |
| STOP conditions specific | Pass, but add worktree-exists and TextObject cases |
| Done criteria machine-checkable | Mostly; the baseline "or more" and the vacuous `lint_docs.py` are soft |
| Planned-at SHA and drift paths vs Scope | SHA filled; drift check vacuous by construction |
| Non-deploying commands with `-p:ModuleId=` | Pass: every build and test command carries both flags; `./build.ps1` banned |
| No em or en dash in prose | Pass: every dash is inside a code span, code block or quoted shipped string |
| No secret values | Pass |
