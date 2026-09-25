# Codex adversarial review: plan 025 (delete-unreachable-scaffolds), branch improve/025-delete-unreachable-scaffolds

Feature: Delete two unreachable scaffolds (IEditorSceneAdapter, the EditorCacheRebuild path cache) and the tests and binding row that keep them alive. Two pieces of code have been built, tested and carried since 2026-05-12 (commit `6a80bac6`) with no caller at all. `IEditorSceneAdapter` is an adapter interface with no implementation and no reference. The `EditorCacheRebuild/Caching/` folder (a path-reuse cache and its on-disk sidecar, 6 files, 292 lines) is registered in DryIoc but never resolved or injected, so it never runs. They cost 406 lines of tests (26 test methods), two reserved config properties that nothing reads, and one row in the engine binding gate (`ReflectionSiteBindingTests`) that is mislabelled as an engine type. The project's simplicity rule (`.claude/rules/simplicity-criterion.md`) says a deletion that holds parity (tests green, behaviour unchanged) is "Always keep", and code "in case we need it later" is "Reject". After this plan the scaffold is gone, the suite and binding gate are smaller and honest, and git history (`6a80bac6`) is the recovery path if path reuse is ever actually built.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 1091f3b6..improve/025-delete-unreachable-scaffolds
- Any file as the branch has it: git show improve/025-delete-unreachable-scaffolds:<path>
- The base for comparison: git show 1091f3b6:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/025-delete-unreachable-scaffolds:plans/025-delete-unreachable-scaffolds.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints output and any "Current state" excerpt no longer matches the branch tip.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Any Step 1 grep returns a hit beyond the listed ones: another file, a string-based lookup (`Type.GetType`, `AccessTools.TypeByName`, `TypeByName`), an XML, prefab, JSON or Python mention. Something could load a deleted type by name at runtime.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields` fails at Step 2 (the config loader rejects unknown keys, so deleting the properties would break a user's hand-edited config).
4. From the plan's STOP conditions, did the change hit or mishandle this risk? The RED run at Step 2 fails in any test other than `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly`, or that test fails for a reason other than the `IEditorSceneAdapter` assertion.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The build after Step 3, 4 or 5 fails with an error naming a file outside the in-scope list (a hidden reference), or seems to need an edit to `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props` or `TAOM.Tests/TAOM.Tests.csproj`.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The `ReflectionSiteBindingTests` filtered run in Step 6 skips (game assemblies not loaded) or any `NavigationCacheAdapter` row fails.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (19 files changed, 64 insertions(+), 751 deletions(-)):
C#:
- Main/Adapters/IEditorSceneAdapter.cs
- Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs
- Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs
- Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs
- Main/Features/EditorCacheRebuild/Caching/NavigationPathCloner.cs
- Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs
- Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs
- Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs
- Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs
Tests:
- TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs
- TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs
- TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs
- TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs
- TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs
- TAOM.Tests/Migration/GameAssemblies.cs
- TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/editor-cache-rebuild.md
- docs/reference/taleworlds-api-snapshot/reflection-sites.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
