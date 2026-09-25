# Codex adversarial review: plan 022 (order-of-battle-auto-assign), branch improve/022-order-of-battle-auto-assign

Feature: Make the Order of Battle "Assign Heroes" button place heroes as captains through HeroAutoAssigner. The Order of Battle (OOB) screen shows a TAOM button labelled "Assign Heroes". Since commit `55950378` (2026-05-07) pressing it prints a literal, unlocalized message, `Auto-Assign is a Phase-1 stub — feature pending. See follow-up GitHub issue.` (the shipped string even contains an em dash), and does nothing. Meanwhile a tested role-to-formation scorer, `HeroAutoAssigner`, is registered in IoC and never resolved by anything. After this plan the button places the player's companions as captains of the open formations that suit their equipment (archers lead ranged formations, riders lead cavalry, and so on), through the same vanilla handler a manual drag uses, reports the result in a localized message, and leaves every captain the player already chose alone.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 1091f3b6..improve/022-order-of-battle-auto-assign
- Any file as the branch has it: git show improve/022-order-of-battle-auto-assign:<path>
- The base for comparison: git show 1091f3b6:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/022-order-of-battle-auto-assign:plans/022-order-of-battle-auto-assign.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check (run against `bannerlord-1.5.x` before the worktree exists) shows any listed file changed since `a39a9c86`, and the "Current state" excerpts no longer match `git show bannerlord-1.5.x:<path>`.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? `git worktree add` fails because `E:/repos/wt-022` or the branch `improve/022-order-of-battle-auto-assign` already exists.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0's grep for "Phase-1 stub" shows anything other than lines 81, 88, 143, 146 and 184.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Any engine member this plan relies on is missing or non-public in the build: `OrderOfBattleVM.IsPlayerGeneral`, `FormationsFirstHalf`, `FormationsSecondHalf`, `UnassignedHeroes`, `ExecuteClearHeroSelection`; `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`, `GetOrderOfBattleClass`, `Captain`, `HasFormation`, `HasCaptain`, `HeroTroops`, `Formation`; `OrderOfBattleHeroItemVM.OnHeroSelection`, `Agent`, `IsMainHero`, `IsLeadingAFormation`, `IsDisabled`. A compile error naming one of these means the engine drifted: report the error; do not reach for reflection.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? `FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM))` or the `OOBButtonsVM` constructor throws inside the test host (Step 3), or the Step 4 GREEN test throws from `TextObject`, `SetTextVariable`, `MBTextManager` or `ToString()`. Report the exception; do not remove or weaken the test.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The harvest dry run reports anything other than exactly 3 unregistered keys, or the seeding script seeds anything other than exactly 36 rows (3 per language).
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (28 files changed, 479 insertions(+), 23 deletions(-)):
C#:
- Main/Features/CompanionTactics/CompanionTacticsIoC.cs
- Main/Features/CompanionTactics/FormationPresets/HeroAutoAssigner.cs
- Main/Features/CompanionTactics/FormationPresets/IHeroAutoAssigner.cs
- Main/Features/CompanionTactics/FormationPresets/IOOBCaptainAutoAssigner.cs
- Main/Features/CompanionTactics/FormationPresets/Models/AutoAssignResult.cs
- Main/Features/CompanionTactics/FormationPresets/Models/AutoAssignStatus.cs
- Main/Features/CompanionTactics/FormationPresets/Models/CaptainAssignment.cs
- Main/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssigner.cs
- Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs
- Main/Features/CompanionTactics/FormationPresets/UI/OOBButtonsVM.cs
Tests:
- TAOM.Tests/Features/CompanionTactics/FormationPresets/HeroAutoAssignerTests.cs
- TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBButtonsVMTests.cs
XML/XSLT/JSON data:
- Main/_Module/ModuleData/Languages/BR/std_taom_module_strings_por-BR.xml
- Main/_Module/ModuleData/Languages/CNs/std_taom_module_strings_zho-CN.xml
- Main/_Module/ModuleData/Languages/CNt/std_taom_module_strings_zho-HK.xml
- Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml
- Main/_Module/ModuleData/Languages/FR/std_taom_module_strings_fre-FR.xml
- Main/_Module/ModuleData/Languages/IT/std_taom_module_strings_ita-IT.xml
- Main/_Module/ModuleData/Languages/JP/std_taom_module_strings_jpn-JP.xml
- Main/_Module/ModuleData/Languages/KO/std_taom_module_strings_kor-KO.xml
- Main/_Module/ModuleData/Languages/PL/std_taom_module_strings_pol-PL.xml
- Main/_Module/ModuleData/Languages/RU/std_taom_module_strings_rus-RU.xml
- Main/_Module/ModuleData/Languages/SP/std_taom_module_strings_spa-LA.xml
- Main/_Module/ModuleData/Languages/TR/std_taom_module_strings_tur-TR.xml
- Main/_Module/ModuleData/taom_module_strings.xml
Harness and docs:
- CHANGELOG.md
- docs/features/companion-tactics.md
- docs/reference/feature-map.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
