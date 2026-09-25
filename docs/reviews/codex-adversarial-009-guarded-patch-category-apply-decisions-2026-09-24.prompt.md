# Codex adversarial review: plan 009 (guarded-patch-category-apply), branch improve/009-guarded-patch-category-apply

Feature: Apply every Harmony patch category through one guarded helper. `Main/SubModule.cs` applies TAOM's Harmony patches one category at a time with `_harmony.PatchCategory("PatchNN_X")`. Harmony 2.4.2 has no catch around a category: the first patch class whose target method no longer resolves (an engine rename after a Steam force-bump, or another mod reshaping IL) throws a `HarmonyException` straight out of the SubModule hook. 64 of the 84 call sites are bare. In `OnSubModuleLoad` (13 bare sites) the engine logs the throw and rethrows, so the game does not start with TAOM enabled. In `OnGameInitializationFinished` (50 bare sites) the once-per-process flag is set BEFORE the batch, so a throw skips every later category (including the crash guards Patch65, Patch82 and Patch84), the three watchdogs, `ManualPatchApplicator.ApplyAll` and the Harmony census; a crash bundle is written, a saved game never finishes loading, and a new game runs half-patched. After this plan, one drifted binding costs exactly one category: it is logged at Error with its cause, a red on-screen line names it, and every other category still applies.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 4c728dac..improve/009-guarded-patch-category-apply is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/009-guarded-patch-category-apply:docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 4c728dac..improve/009-guarded-patch-category-apply
- Any file as the branch has it: git show improve/009-guarded-patch-category-apply:<path>
- The base for comparison: git show 4c728dac:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/009-guarded-patch-category-apply:plans/009-guarded-patch-category-apply.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything, or `grep -c "_harmony\.PatchCategory(" Main/SubModule.cs` in the fresh worktree is not `86` (someone added or removed a category since `b2e387db`).
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 4 script prints a count other than 86 (it writes nothing in that case).
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Any of the 20 guarded blocks does not contain what the "Current state" table says (for example a K site with no `Initialize` call, or a C site with more than the apply).
4. From the plan's STOP conditions, did the change hit or mishandle this risk? `git worktree add` fails because `plan/009-guarded-patch-apply` or `E:/repos/wt-plan-009` already exists.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Either real-Harmony test fails for any reason other than a missing `PatchCategoryApplier` type. The likeliest cause: Harmony's `BuildCategoryCache` reads custom attributes (`GetCustomAttributes(true)`) off every type in the `TAOM.Tests` assembly, and a type that references an assembly the test host cannot load (for example `TaleWorlds.MountAndBlade.View`, see the comment at `TAOM.Tests/Features/EconomyDiagnostics/EconomyDiagnosticsPatchDiscoveryTests.cs:52-57`) throws `FileNotFoundException`, `TypeLoadException` or `ReflectionTypeLoadException` from `BuildCategoryCache` instead of the expected `HarmonyException`. Fallback you may take without asking, for that or any other cause: delete those two tests and the `Plan009UnresolvableTargetProbe` class (and the now-unused `using HarmonyLib;` and `UnresolvableCategory` constant), keep the other seven, continue with the 7-test counts, and put the exact failure output in your report.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The test build reports an error (not a warning) on `Plan009UnresolvableTargetProbe`, for example from a Harmony analyzer.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (35 files changed, 503 insertions(+), 58 deletions(-)):
C#:
- Main/PatchCategoryApplier.cs
- Main/PatchCategoryIndex.cs
- Main/SubModule.cs
Tests:
- TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs
- TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs
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
Scripts and hooks:
- tools/translation_cache/br.json
- tools/translation_cache/cns.json
- tools/translation_cache/cnt.json
- tools/translation_cache/de.json
- tools/translation_cache/fr.json
- tools/translation_cache/it.json
- tools/translation_cache/jp.json
- tools/translation_cache/ko.json
- tools/translation_cache/pl.json
- tools/translation_cache/ru.json
- tools/translation_cache/sp.json
- tools/translation_cache/tr.json
Harness and docs:
- CHANGELOG.md
- docs/features/crash-report.md
- docs/reference/engine/submodule-lifecycle-and-harmony.md
- docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md
- docs/reviews/lessons/harmony-il.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
