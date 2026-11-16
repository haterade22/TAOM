# RCA: game version warning (PR #1340 deep review, 2026-11-16)

## Top-line

`/deep-review` (lenses 2, 5, 7) of the TAOM.Dependencies game version guard found no CRITICAL or HIGH finding, 2 MEDIUM, 3 LOW and 1 owner decision. The MEDIUM and LOW items are fixed in cb7fee0ba and 7c2df21f9. The full suite shows no new failing test name against trunk (58 known failures on this PC).

## Findings and Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | MED | `Guard_Target_BindsOnTheReferencedEngine_WithThePrefixShape` reads the IL exception clauses of an engine method. In a no-game run against the BUTR reference assemblies the body is a stub, so the test fails there. | Test gate scope | The author ran the suite with the game installed, where the body is real. The sibling binding tests carry `RequiresGameIL` and `BindingVerification`; this one was written without checking them. | Tagged `RequiresGameIL` and `BindingVerification`. A test that reads a method body from a referenced engine assembly takes both tags. |
| F2 | MED | `GameVersionNotice.Run` had no Modding Kit check. TAOM.Dependencies also loads in the Kit (`Win64_Shipping_wEditor`), whose engine version can lag the client's, so the guard would turn TAOM's code off inside the Kit. | Convention inconsistency (sibling guard not copied) | `IncompatibleModDetector.RunEarlyPhase` already skips the Kit through `IsEditorBin`. The new startup code was written from the engine's own box rules (dedicated server, test mode) and the siblings in the same assembly were not compared. | `Run` returns early on `IsEditorBin` and logs `skipped: Modding Kit`; a source-order test pins the check between the server guard and the mismatch branch. Lesson added to `lessons/state-lifecycle-save.md`. |
| F3 | LOW | The `SubModule.xml` comment and one `startup-alerts.md` changelog line said the guard "warns". It turns TAOM's code off for the session and tells the player. | Documentation rot | The wording came from the first design (a box only) and was not revisited when the prefix was added. | Both reworded. |
| F4 | LOW | `GameVersionNotice.cs` and `startup-alerts.md` called the constructor "the last TAOM code that runs before" the engine box, and the doc said the version comes from `Parameters/Version.xml`. The constructor of TAOM.Dependencies runs before any other TAOM submodule loads; the version is the text compiled into `TaleWorlds.Library`, read through `VirtualFolders`. The doc also lacked two limits. | Documentation accuracy | Plausible wording was not checked against `Module.LoadSubModules` and `ApplicationVersion.FromParametersFile`. | Both corrected. Limits added: an engine member `Run` reads that differs on the player's version makes nothing run; the launcher (same major.minor series) and BUTR (minimum) use their own version rules. |
| F5 | LOW | `ReadSubModule_ShippedTaomFile_PinsNative157` hardcoded `v1.5.7.*`; the live-install test defaulted to `C:\STEAM`. | Test maintenance, convention | A literal was copied instead of reading `.claude/pinned-game-version.txt` as `BundledDependencyManifestTests.NativeConstraint_MatchesPinnedGameVersion` does. | Test renamed, compares with the pin file plus `.*`; default game path is `E:\Steam\...` with `BANNERLORD_GAME_DIR` first. |
| F6 | LOW | `GameVersionCheck.Text` said plain English is used because TAOM's strings are not loaded. On the skip path TAOM's language XMLs are loaded (`Module.LoadLocalizationXmls` runs for every module before DLLs). | Comment accuracy | The reason was assumed. | Comment now says the text must also work in the native fallback box and when TAOM's own files are the problem. Plain English stays (owner's choice). |
| F7 | FIXED | The popup's Ok button did not quit the game, so a session could load a save without TAOM's code and save it. | Owner decision | Taken on 16 Nov 2026: the button is now Native's Exit Game and closes the game (873e04f98, d671c33c8). | See rca-pr1340-game-version-warning-2026-11-16.md, row 1 |

## Root Cause Pattern: a new startup guard copies the engine's rules, not its siblings'

F2 and F5 are the same shape: the change followed one reference (the engine's box, a test written beside it) and skipped the neighbour that already solved the same problem in the same assembly (`IsEditorBin`, the pin file read). The lens that found F2 compared the new code with `IncompatibleModDetector`.

## Why Deep-Review Missed These Earlier

The first pass checked behaviour on the installed 1.5.7 game, where the Kit path and the stubbed IL do not occur. Both gaps appear only in another host (the Kit) or another build setup (reference assemblies).

## Tests Added or Changed

- `Notice_ChecksServerAndTestMode_First_AndCatchesEverything`: the Kit check sits after the server guard and before the mismatch branch.
- `IsEditorBin_AssemblyPath_TrueOnlyForTheModdingKitBin` already covers the path rule.
- `ReadSubModule_ShippedTaomFile_PinsTheGameVersionTheRepoTargets`: pin compared with `.claude/pinned-game-version.txt`.

## Follow-up review: Ok closes the game (873e04f98, 2026-11-16)

Lens 5 at effort high (tier 2: startup UI, no patch or save data). No CRITICAL, HIGH or MEDIUM; 3 LOW, fixed in the next commit. Filtered tests (GameVersionCheckTests, MissingTaomCheckTests) pass 36 of 36.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F8 | LOW | The `QuitGame` helper comment said `Utilities.QuitGame` is the call the engine's own Exit button uses. The Exit button calls `MBInitialScreenBase.DoExitButtonAction()` (`MBAPI.IMBScreen.OnExitButtonClick`). | Comment accuracy | The claim was written from the autorun hosts and generalised without reading the Exit option in `ViewSubModule`. | Comment now names only the autorun hosts, which do call it at the main menu. |
| F9 | LOW | `MenuNotice_OkClosesTheGame_OnlyForTheWrongVersionPopup` checked `QuitsOnOk` and the two `Tick` call strings, but not that `Show` passes `QuitGame` when the flag is true; a `Show` that ignored the flag passed. `QuitsOnOk` was a one-line `!= null` that existed only to be tested. | Test gate scope | The test pinned the decision, not the line that consumes it. | `QuitsOnOk` removed; `Tick` passes `quitOnOk: true` and `false`; the test also reads `Show` and asserts the `QuitGame` action and the label. `Body()` matches braces, so it cannot cut an expression-bodied member: the test slices from `Show(` to the next method. |
| F10 | LOW | The button on a popup that quits the game said Ok. | UX consistency | The owner decision covered what Ok does, not its label. | The quitting popup's button is Native's `{=YbpzLHzk}Exit Game`, which translates without TAOM's language files; the missing-TAOM popup keeps Ok. |
