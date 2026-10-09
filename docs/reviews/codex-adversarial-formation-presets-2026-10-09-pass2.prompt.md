TAOM CODEX ADVERSARIAL REVIEW, PASS 2: the fixes made after pass 1 (formation presets, yotthani adoption item 5, 2026-10-09)

You review only the fixes made after your first pass on the uncommitted work in the git worktree E:\repos\taom-yotthani (branch feat/yotthani-20261008, HEAD 83ad1eab5). Your first pass is docs/reviews/raw/codex-adversarial-formation-presets-2026-10-09.md (3 LOW). A Claude convergence lens ran on the same tree at the same time; its findings and the fixes are recorded in docs/reviews/rca-formation-presets-2026-10-09.md, section "Codex review and convergence pass", findings 14 to 19.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

THE FIXES TO REVIEW:
F1. Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs: a private static PlainName(string) removes { and } from a preset name. SaveCurrent uses it before Trim, and all six display sites pass PlainName(preset.Name) to SetTextVariable("NAME", ...). Test: OOBButtonsVMTests.ExecuteManagePresets_StoredNameWithBraces_ShowsTheNameWithoutThem.
F2. TAOM.Tests/Migration/ReflectionSiteBindingTests.cs: the label gate EveryLineLabel_PointsAtALineThatNamesItsMember now blanks comments first (RepoPaths.StripComments, which keeps line numbers), requires a label without a line number to name an existing file under Main/, and states the convention (label the line that spells the member: its string literal, its nameof, or the name filter inside the lookup). A new sibling test, EveryDataRow_HasAMatchingRowInTheCatalogue, requires every DataRow's member and label files to appear in a row of docs/reference/taleworlds-api-snapshot/reflection-sites.md; it found the missing Mission._initialPlayerAgent row, now added. Labels repointed under the convention: MonsterSizeCatalogAdapter.cs:22 and :29 to :23 and :30; NavigationCacheAdapter.cs:88 and :91 to :374 and :388.
F3. TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBPresetApplierBindingTests.cs: the IsControlledByPlayer assert is deleted (no TAOM code reads it now); HasFormation stays, marked as Auto-Assign's.
F4. Wording only: OOBPresetApplier's class comment (a hero the preset does not mention gets no step of its own, but a saved captain displaces the current captain); OOBOverlayService's gate comment (the campaign behavior persists the store in SyncData and resets it for a new game); OOBCaptainAutoAssigner's class comment (click path, not manual-drag); docs/features/companion-tactics.md (the same points, the file table without PresetApplyStatus and with PlanLoad, the tooltip patch in ManualPatchApplicator.cs); reflection-sites.md (the Maintenance paragraph, the new row, one dash).

QUESTIONS (CONFIRM or DISPUTE each with code evidence):
Q1. F1: does every path that puts a preset name into a TextObject now go through PlainName? Can PlainName make two stored presets look the same in the menu, and does that matter (they keep separate ids)? Does Save still refuse a name that is empty after the braces go?
Q2. F2: can either gate pass vacuously or fail falsely for any DataRow shape in the file (no line, several lines, "A.cs:1 / B.cs:2", a suffix such as "(manual patch)", a file name that exists twice under Main/)? Does StripComments keep line numbers for every comment form in the labelled files (block comments, comment markers inside strings or verbatim strings)? Is the catalogue match strict enough that a row for a different member in the same file cannot satisfy it? Do the four repointed labels now sit on the line that spells the member?
Q3. F3 and F4: does any test, doc or comment still describe removed code or the old behaviour?
Q4. Anything else these fixes broke.

QUALITY GATES:
- Every claim cites a file and line you read in this run.
- Do not re-report items the RCA records as settled or not applied, unless a fix itself is wrong.
- Prefer one proven defect to several speculative ones, but report every proven defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

FINDINGS FORMAT: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the claim, evidence with line numbers, a reproduction or proving input, the fix. Mark anything you could not prove UNVERIFIED. End with a count per severity and a verdict.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write any file: the dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-formation-presets-2026-10-09-pass2.md, so never write that path yourself. Do not edit, create or delete any file anywhere. Do not run git commands that change state. If you build or test, use only: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1, which deploys into the game).
