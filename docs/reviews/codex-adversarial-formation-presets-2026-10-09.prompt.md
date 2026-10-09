TAOM CODEX ADVERSARIAL REVIEW: formation presets (yotthani adoption item 5, issue #779) and the reflection-label gate, 2026-10-09

You review the UNCOMMITTED work in the git worktree E:\repos\taom-yotthani (branch feat/yotthani-20261008, HEAD 83ad1eab5, where adoption items 1 to 4 are already committed). Run `git diff HEAD` and `git ls-files --others --exclude-standard` there for the exact scope. A seven-lens Claude deep review already ran on this work and its fixes are applied; its record is docs/reviews/rca-formation-presets-2026-10-09.md. Read it first: its "Not applied, with reasons" list is settled, so do not re-report those items unless a fix itself is wrong.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

WHAT CHANGED:
W1. Formation presets now really save and load (they stored a name only, and Load was a stub). Main/Features/CompanionTactics/FormationPresets/:
- FormationPresetLayout.cs (new, pure, unit-tested): Capture (a PresetFormationSnapshot per formation into the existing saveable HoNFormationPreset: FormationClasses[Formation.Index] = DeploymentFormationClass value or -1; HeroFormationAssignments hero StringId to formation index; CaptainHeroIds), ResolveClass (vanilla's siege mapping 4 to 2, 3 to 1, 6 to 5), PlanClasses, RunClassPasses (repeated passes while vanilla allows a class change; returns the Applied and Stuck steps), PlanHeroes, and PlanLoad (heroes only into formations whose class step applied or already matched).
- OOBPresetApplier.cs and IOOBPresetApplier.cs (new boundary class, public view-model API only): a class change sets the formation's FormationClassSelector.SelectedIndex, only while IsAdjustable; a hero goes through vanilla's click path (ExecuteClearHeroSelection, OrderOfBattleHeroItemVM.OnHeroSelection, then ExecuteAcceptCaptain or ExecuteAcceptHeroTroops), checked after each step.
- UI/OOBButtonsVM.cs: Save captures through the applier and saves; Load applies and reports counts; the presets menu opens only when vanilla's IsPlayerGeneral is true; braces are removed from a typed preset name; every string is localized (21 new taom_oob_* rows in Main/_Module/ModuleData/taom_module_strings.xml); OK and Cancel use vanilla's own localization keys {=oHaWR73d} and {=3CpNUnVl}.
- OOBOverlayService.cs: the overlay attaches only when EnableFormationPresets is on AND Campaign.Current != null.
- Models/PresetApplyResult.cs (new); HoNFormationPreset.GetSummary and GetFormationClass removed (no callers; no saveable field touched).
- Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml: the Assign Heroes text is bound to the view model.
- The feature stays OFF by default (EnableFormationPresets = false). Nothing of the apply path has run in a real battle yet.
W2. TAOM.Tests/Migration/ReflectionSiteBindingTests.cs: a new test, EveryLineLabel_PointsAtALineThatNamesItsMember, pins every "File.cs:N" source label of the reflection-site DataRows to a line that names its member; 13 stale labels are fixed there and in docs/reference/taleworlds-api-snapshot/reflection-sites.md.

VANILLA v1.5.4 (decompile cache C:\Users\mikew\.taom-src\v1.5.4\<Full.Type.Name>.cs, or `pwsh -NoProfile -File tools/taom-src.ps1 path <Full.Type.Name>` from the worktree): TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM, OrderOfBattleFormationItemVM, OrderOfBattleFormationClassVM, OrderOfBattleHeroItemVM; SandBox.ViewModelCollection.SPOrderOfBattleVM (vanilla's own layout store: LoadConfiguration, SaveConfiguration); TaleWorlds.CampaignSystem.CampaignBehaviors.OrderOfBattleCampaignBehavior.

QUESTIONS (CONFIRM or DISPUTE each with code evidence):
Q1. Apply order and state. Classes in passes, then captains, then hero troops, all inside one synchronous call. Can any step leave the screen in a state the player could not reach by hand (a hero both captain and hero troop, a non-empty hero selection, a formation with heroes but no class, IsAdjustable or HasFormation stale inside the loop)? Can vanilla change a formation's class as a side effect of another formation's change within the same call, which would make PlanLoad's "ready" set (the Applied steps) wrong?
Q2. Capture. Is every hero captured once (main hero included; a hero in two lists of the screen)? Is -1 the right "unset" value in every state the screen produces? Does a preset saved by the old name-only build load as nothing, harmlessly?
Q3. Gates. Is Campaign.Current null in every mission that is not a campaign mission and non-null in every campaign mission while the Order of Battle handler ticks? Does the IsPlayerGeneral menu gate match the condition that vanilla's own accept paths and SaveConfiguration use?
Q4. Persistence. Do the now-populated containers (Dictionary<string,int>, List<string>, Dictionary<int,int>) on HoNFormationPreset survive SyncData save and load on v1.5.4? Does removing the two methods change anything the save system sees?
Q5. Text. Are {=oHaWR73d} and {=3CpNUnVl} exactly vanilla's str_ok and str_cancel keys on v1.5.4, with no TAOM row overriding them? Does removing braces from a new name leave any other path where player text reaches a template? A preset saved earlier with braces in its name: what does it show now?
Q6. The label gate. Can it pass vacuously or report a false failure for any label shape in the DataRows (no line, several lines, a suffix such as "(manual patch)", two files with one name)? Do the 13 fixed labels point at the reflection lookup itself, not at a declaration, a message or a comment?
Q7. Anything else this change broke, judged against the v1.5.4 engine and TAOM's rules (.ai/review-reference.md).

QUALITY GATES:
- Every claim cites a file and line you read in this run.
- Do not re-report items the RCA records as settled or not applied, unless a fix itself is wrong.
- Prefer one proven defect to several speculative ones, but report every proven defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

FINDINGS FORMAT: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the claim, evidence with line numbers, a reproduction or proving input, the fix. Mark anything you could not prove UNVERIFIED. End with a count per severity and a verdict.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write any file: the dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-formation-presets-2026-10-09.md, so never write that path yourself. Do not edit, create or delete any file anywhere. Do not run git commands that change state. If you build or test, use only: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1, which deploys into the game).
