# Cold review: plan 048, round 1

Reviewer: review-048-r1, read-only. Code read from the baseline worktree at `7087fa633` (HEAD confirmed
`7087fa633`, clean). No earlier `plan-review-048*.md` existed in this audit folder, so nothing is carried over.
No dotnet build or test was run (read-only role): the base totals and the `11 Warning(s)` count are UNVERIFIED here.

## Verdict

One blocking item. The Step 6 verify grep and the matching Done criterion fail on a true line in a file
that is out of scope. Everything else checks out against the code: every excerpt matches, the line arithmetic
holds, and the engine facts match the decompile.

## Blocking

**B1. Step 6 Verify (plan line 856) and Done criterion (plan line 960) cannot print nothing.**
The pattern `commented out in SubModule`, run case-insensitive over `Main Client TAOM.Tests`, hits at baseline:

```
TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs:438:            "NavalTravel is PARKED (#120/#296) and the category is commented out in SubModule.cs. " +
```

That line is about NavalTravel (`Patch57_NavalAtSeaLandRescueGuard`), it is still true, and the file is not in
Scope. After a correct Step 6 the grep still prints this hit, so a weak executor either STOPs (line 982,
"verification fails twice") or edits an out-of-scope test string to make the check pass. Fix: anchor the
pattern to the Patch0 claim, for example
`git grep -n -i -E "Dormant|Patch0_BattleScenes is commented out|Patch0_BattleScenes.{0,20}(disabled|never registered)" -- Main Client TAOM.Tests`.
Before you rely on it, check that this pattern hits only the three target lines at `7087fa633`. Also say in
the Verify text that the CoopVeto line is expected and out of scope.

## Non-blocking

**N1. `Dormant today` never matched (lines 856, 960).** At baseline the two words are split across lines
18 and 19 of `MBMapScene_GetBattleSceneIndexMap_Patch.cs` (`... visibility matters. Dormant` / `// today (category ...`).
That alternative proves nothing. Use `Dormant` on its own (the only hits under `Main/Features/BattleScenes` are lines 18 and 19).

**N2. The filter `FullyQualifiedName~CultureMoraleAndAggressionTests` matches no test (Step 3 Verify, line 700;
Test plan line 937).** `TAOM.Tests/Features/CultureDoctrine/CultureMoraleAndAggressionTests.cs` declares the classes
`CultureMoraleTests` (:10), `CultureAggressionTests` (:45), `FormationRoutingTests` (:98) and
`CultureMoraleAndAggressionServiceTests` (:146). None of their names contains `CultureMoraleAndAggressionTests`,
and the namespace is `TAOM.Tests.Features.CultureDoctrine`. The `TryParseClass` and `TryRoute` tests (:101, :114)
are in `FormationRoutingTests`. The combined filter still shows a non-zero Passed count from the other terms,
so the step passes without running the tests the plan says pin `IsRoutable`'s neighbours. Step 7's full suite
does cover them. Replace the filter term with `FullyQualifiedName~FormationRoutingTests`. The blast-radius list
(lines 531 to 532) cites the right file but implies the wrong class name.

**N3. Step 1 Verify (line 645) wants exactly `11 Warning(s)`; elsewhere the plan says "at most 11"
(lines 574, 698, 744, 875, 950).** This is consistent enough, but a base with fewer warnings would STOP Step 1
for no reason. Use "at most" there too, or say that a lower count is fine.

**N4. Step 5 item 3 (line 789): the expected regenerate totals are `Passed: 1` with no Total.** That is fine.
It may help to add that the run touches nothing but the baseline file (the `git diff --stat` in item 4 checks this).

**N5. The PatchReportFormatterTests replacement line (line 852) is about 118 characters.** No gate limits comment
width as far as I read, but it is longer than its neighbours (about 100). Consider wrapping it.

## Checks run, and their results

1. **Excerpts against `7087fa633`**: all match.
   - `ReflectionUtils.cs` 5 to 16, 47 to 81: match. The em dash on line 9 is deleted with lines 8 to 10, and line 12's stays (out of scope, correct).
   - `MutationParams.cs` 24 to 41: match. `NumberStyles` and `CultureInfo` are on line 19. Deleting 28 to 40 leaves 27 `    }` and then 41 `}`.
   - `FormationRouting.cs` 51 to 57: match. Deleting 53 to 56 leaves one blank line (52).
   - `StackFrameSnapshotBuilder.cs` 19 to 31: match. Deleting 21 to 30 leaves blank line 20.
   - `CareerChoiceGroupObjectVM.cs` line 10, 44 to 61, 86 to 94: match. Deleting 47 to 60 leaves 45, 46 blank, then 61. Deleting 88 to 93 leaves 87 then 94.
   - Controller lines 17, 554 to 561, 644 to 651, 678 to 681: match. The file has 681 lines (`wc -l`). After deleting 4 lines the class spans 17 to 677, which is 661.
   - Baseline file line 7 `... AutonomousMovementPlayerController 665`, 23 lines, CRLF, ends with CRLF. The regenerator (`EntryPointLineCountGateTests.cs:101-125`) rebuilds the header in the same text and writes with `File.WriteAllLines`. `MaxBaselineEntries = 18` is at :34. The message at :78 and the formula at :304 match. `RepoPaths.RepoPath` resolves from `[CallerFilePath]` (`RepoPaths.cs:35-39`), so the write lands in the worktree that compiled the tests.
   - `SubModule.cs` 490 to 495 and 530 to 535: match. `OnSubModuleLoad` is at :114, and no `return` sits between 114 and 495.
   - `MBMapScene_GetBattleSceneIndexMap_Patch.cs` 16 to 21 and 23 to 30: match. `PatchReportFormatter.cs` 12 to 17: match. `PatchReportFormatterTests.cs` 14 to 17: match.
   - `LoadingScreen_ShaderProgress_Patch.cs:17`: match. `git grep Patch21_ShaderPrecompilation -- Main Client` prints only that line and `SubModule.cs:530,535`.
   - The zero-caller grep output (lines 89 to 142) matches my run exactly. The `ReflectionUtils\.` callers (lines 256 to 260) match: three in Dependencies, five in CoopInterop.
   - The doc fragments at `gui-sprite-system.md` lines 241, 304 and 371 each occur once (`grep -c -F` printed 1 for each). None of the three lines holds an em or en dash, so Step 7's dash grep will not flag the rewritten long lines.
   - The only non-history `.txt` or `.json` row naming a changed type is the baseline's line 7 (`THIRD-PARTY-LICENSES.txt:109` names `ReflectionUtils` as a file, which is fine).
   - `Main/TAOM.csproj:84` references Dependencies, and `TAOM.Tests/TAOM.Tests.csproj:28` references Client: both match.
2. **Engine facts**: `taom-src` v1.5.7 cache. `MissionMainAgentController` :13, :74, :714, :841 match, and `MissionGauntletMainAgentEquipDropView` :16, :28, :84, :250, :252 match.
3. **Step 5 grep counts**: `IsGameKeyPressed(17)` and `ToggleAlternativeWeapon` each count 1 at baseline and stay at 1 after the edit.
4. **Step 6 simulated**: the three new comment lines do not match the Step 6 pattern (exit 1). The pattern fails only on the CoopVeto line (B1).
5. **Commit message**: the subject is 65 characters with `v2.0.38` (`Main/_Module/SubModule.xml:6`). Every body line is at most 70 characters. `tools/check_commit_msg.py` SUBJECT regex (:26) accepts `refactor(dead-code): v2.0.38 - ...`. No AI trailer.
6. **Git status**: `bin/`, `obj/` and `TestResults/` are gitignored (`.gitignore:2,3,5`), so Step 7's "exactly the eleven in-scope files" can hold.
7. **Dash grep tool**: `LC_ALL=C.UTF-8 grep -P "[\x{2013}\x{2014}]"` works in Git Bash (it found ReflectionUtils :9 and :12). Without `LC_ALL` it errors, and the plan sets it.
8. **Checklist**: TDD for a deletion is RED in the log (Step 2 grep, Step 5 gate failure), per the lesson at `docs/reviews/lessons/testing-qa.md:1315`. The issue line is present (38, 989). ADR-002, 007, 008 and 013 are named. No protected file is touched. Single-owner `SubModule.cs` is read-only and named. STOP conditions are specific. Planned-at is `7087fa633`, and the drift-check paths equal Scope plus three read-only dependencies, which the plan explains. Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path, no branch name, no CHANGELOG step.
9. **Prose**: the plan's em dashes are only inside quoted code and grep output (lines 91, 94, 220, 223, 462, 491, 849), which is exempt. No secret values.
