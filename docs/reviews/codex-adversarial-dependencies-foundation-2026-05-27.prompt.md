# Codex Adversarial Review: TAOM.Dependencies/Foundation (DR3 Phase 4)

You are reviewing the TAOM.Dependencies runtime defensive infrastructure -- 11 classes in `Dependencies/Foundation/` plus `Dependencies/AliasStubSubModule.cs` and `Dependencies/SubModule.cs` (orchestrator). This is NOT a typical TAOM feature in `Main/Features/`. It's low-level Harmony-patching + reflection + exception-handling glue inspired by BetaDeps v0.7.5.1 (Nexus 11274), ported under MIT clean-room rewrite for Bannerlord v1.4.5.

Purpose: catch out-of-date third-party mod errors at runtime so the game keeps running. Patches every Harmony patch in the AppDomain with a Finalizer (PatchShield), patches 10 save/mission methods (SaveShield), reflectively detects Bannerlord version (VersionProbe), detects crash-loops via on-disk markers (IncompatibleModDetector), wraps SubModule construction (SubModuleConstructionGuard), wraps Assembly.GetTypes (CollectAssemblyTypesShim).

This work has been verified in-game across 4 launches (latest 2026-05-27 14:02:59). diag.log session 4 shows every shield installing without exceptions, 0 swallow events, v1.4.5 detected. Phase 4 is functionally complete. This Codex pass is to catch subtle bugs that the in-game verification couldn't surface (rare exception paths, race conditions, attribution errors, Harmony Finalizer signature quirks).

## READ FIRST

- `docs/migration/dr3-maintenance.md` (whole file -- DR3 architectural context + Phase 4 "Defensive infrastructure" section near the end)
- `Dependencies/_Module/SubModule.xml` (TAOM.Dependencies module manifest -- shows load order + which SubModules construct from this assembly)
- `Stubs/Bannerlord.Harmony/_Module/SubModule.xml` (canonical alias-stub example with the `<SubModuleClassType value="TAOM.Dependencies.AliasStubSubModule"/>` entry)

## Architectural decisions (locked -- do NOT flag as bugs)

1. We VENDOR upstream BUTR DLLs (Bannerlord.ButterLib 2.10.4, MCMv5 5.11.4, etc.) -- we do NOT clean-room reimplement them. The Foundation/ classes are clean-room originals.
2. Stub modules deploy via MSBuild target at build time, not at runtime. The 4 alias stub folders (Modules/Bannerlord.Harmony/, etc.) have NO bin/ folder -- they reference TAOM.Dependencies.dll which lives only in TAOM.Dependencies/bin/. As confirmed in diag.log session 4, the launcher silently skips constructing AliasStubSubModule for this reason. Practical impact: IncompatibleModDetector.RunEarlyPhase was moved from the stub ctor to Dependencies/SubModule.OnSubModuleLoad (commit bc8f5c3). The AliasStubSubModule code remains as fallback if DLL deployment changes later. Do NOT flag the stub ctor as "dead code" -- it's deliberately kept.
3. Comprehensive DiagLog instrumentation is intentional. Verbosity is acceptable trade-off for diagnostic visibility during this phase.
4. Opt-out flag files (`patchshield-disabled.flag`, `saveshield-swallow-disabled.flag`) are documented user-controllable escape hatches. Not bugs.

## Known Suspects (please CONFIRM or DISPUTE each with specific evidence)

### S1: PatchShield.TryUnpatchOffendingPatches owner-filter correctness

The method refuses to unpatch owners whose Harmony ID starts with "TAOM" (case-insensitive). Question: is this the right filter? Third-party BUTR mods, MCM, ButterLib, etc. have Harmony IDs like "Bannerlord.ButterLib.Implementation", "MCM.Implementation.MCMv5", etc. -- those would be unpatched on the first MissingMethodException, potentially breaking the entire BUTR stack.

Verify by reading `PatchShield.cs` carefully. If the filter SHOULD include `Bannerlord.*`, `BUTR.*`, `MCM.*`, `HarmonyLib.*`, etc., flag this as HIGH. If the filter as-written is correct (because vanilla BUTR patches should be unpatched if they're broken), flag as DISPUTED.

### S2: SaveShield AttributeCulprit engine-prefix completeness

The `_enginePrefixes` array filters out engine/infrastructure assemblies from the stack walk to find the culprit. Read it. Then ask: is "TAOM" the right prefix to exclude? What about consumer mods that ARE TAOM-namespaced (e.g., `TAOM_Online`, `TAOM_Map`)? If a TAOM_Online assembly is the culprit, it'd get filtered out as "engine" and the attribution would fall through to "(unknown)".

Same question for `Bannerlord.Harmony` / `Bannerlord.UIExtenderEx` / `Bannerlord.ButterLib` -- our STUB modules have those Ids. The actual third-party mod whose code is on the stack is NOT one of those. So filtering them out is right. But verify the filter is complete enough vs. too aggressive.

### S3: VersionProbe FromParametersFile null-arg invocation safety

`Dependencies/Foundation/VersionProbe.cs:DetectViaApplicationVersion` invokes `ApplicationVersion.FromParametersFile` reflectively with `new object?[] { null }` for the single-arg overload. This passes `null` as the `customParameterFilePath` parameter. Decompile `TaleWorlds.Library.ApplicationVersion.FromParametersFile` from `bin/Win64_Shipping_Client/TaleWorlds.Library.dll` and verify the method handles null. If it throws NRE on null, we get a `TargetInvocationException` -> our catch swallows -> VersionProbe silently fails -> game continues but version is `0.0.0`. diag.log session 4 shows it succeeds, but verify the actual API contract.

### S4: CollectAssemblyTypesShim Finalizer `ref Type[] __result` signature

`Dependencies/Foundation/CollectAssemblyTypesShim.cs:GetTypesFinalizer` declares the signature `private static Exception? GetTypesFinalizer(Assembly __instance, ref Type[] __result, Exception __exception)`. Harmony Finalizers have specific rules for `__result` access. Verify:
- Is `ref Type[] __result` a legal Finalizer parameter shape in Lib.Harmony 2.4.2?
- Does writing to `__result` from inside a Finalizer actually mutate the caller's return value, or does Harmony only honor returns from prefix/postfix patches?
- If the assignment doesn't take effect, `Assembly.GetTypes()` still throws ReflectionTypeLoadException (we swallow the throw via returning null, but the caller gets a null reference if they relied on the return value).

This is the highest-risk component if the Finalizer signature is wrong -- ALL `Assembly.GetTypes()` calls would silently return null on error instead of partial.

### S5: SubModuleConstructionGuard culprit attribution on Module.AddSubModule path

`Dependencies/Foundation/SubModuleConstructionGuard.cs:SwallowFinalizer` handles two patch sites:
1. `MBSubModuleBase` ctor -- `__instance` is the derived SubModule itself (typed `MBSubModuleBase`), so `__instance.GetType()` correctly gives the third-party SubModule type.
2. `Module.AddSubModule` -- `__instance` is the `Module` (TaleWorlds), and the inner exception's `TargetSite` is the only way to find the offending third-party ctor.

Read the v1.4.5 decompiled `Module.AddSubModule` body (verify against `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll` via ilspycmd):
```csharp
ConstructorInfo constructor = subModuleAssembly.GetType(subModuleInfo.SubModuleClassTypeName).GetConstructor(...);
MBSubModuleBase value = (MBSubModuleBase)constructor.Invoke(new object[0]);
```

When `constructor.Invoke()` throws a `TargetInvocationException`, the InnerException is the real ctor exception. Our finalizer unwraps it (loop on `is TargetInvocationException`). Then we set `declTypeName = ex.TargetSite?.DeclaringType?.FullName`. But `ex.TargetSite` for the inner exception is the THROWING method inside the derived ctor body, which could be on a TaleWorlds API (e.g., if the derived ctor calls `MBObjectManager.GetObject<X>("missing")` -- that throws inside TaleWorlds, so TargetSite is on TaleWorlds.ObjectSystem). We'd attribute "TaleWorlds.ObjectSystem" as the culprit when the REAL culprit is the third-party mod that called the API badly.

Question: is the TargetSite-based attribution actually correct for the AddSubModule path? Should we instead walk the stack to find the first non-engine frame?

### S6: PatchShield double-install + per-session counters

PatchShield.Install is called twice -- once from OnSubModuleLoad, once from OnGameInitializationFinished. The `_shielded` HashSet correctly tracks already-shielded methods (idempotent). But the `_swallowedMissingMethod` etc. counters are session-global. Question: does `WriteSessionSummary` (registered on AppDomain.ProcessExit) correctly include events from the SECOND pass, or does the lifecycle order break this?

Also verify: does `Harmony.GetAllPatchedMethods()` return TAOM's own Patch-Shield-Finalizer patches as patched methods? If so, the second pass would try to shield our own shield-finalizers. The TAOM-namespace filter should catch this -- verify it does.

### S7: IncompatibleModDetector comment-strip regex correctness

`Dependencies/Foundation/IncompatibleModDetector.cs:ReadCurrentModlist` now strips XML comments with `Regex.Replace(text, @"<!--[\s\S]*?-->", "")` before matching `<Id>`. Question: does this correctly handle:
- Multi-line comments spanning many lines
- Nested-looking comments (XML doesn't allow nesting, but malformed XML could)
- Comments containing `-->` sequences (illegal in XML but possible in malformed files)
- Files with BOM markers or encoding issues

For each potential failure mode, decide whether it's a real bug or just a robustness improvement.

### S8: SaveShield dedupe + repeat-failure visibility

`Dependencies/Foundation/SaveShield.cs` + `FailedModsCatalog.cs`. Per-session dedupe by `(culprit, exception_type, owner)` tuple in `FailedModsCatalog._sessionSeen`. Question: if save loading throws DuplicateKeyException 5 times in a row from the SAME culprit (likely if the load loops), we only persist 1 line to `failed-mods-catalog.txt`. The `_swallowedCount` counter still increments. Is this the right behavior, or should we count repeat occurrences in the catalog file?

Also: if a save throws (a) DuplicateKey then (b) InvalidOperationException from the same culprit (different exception types), we'd persist TWO entries. Different exception types are not deduped. Confirm this is intended.

## File list (12 files, all critical)

C# source:
- `Dependencies/SubModule.cs` (orchestrator -- OnSubModuleLoad, OnGameInitializationFinished, InstallAssemblyResolveHandler)
- `Dependencies/AliasStubSubModule.cs` (ctor + OnSubModuleLoad -- never invoked but kept as fallback)
- `Dependencies/Foundation/DiagLog.cs` (threadsafe append-only logger to diag.log)
- `Dependencies/Foundation/RuntimeLog.cs` (path resolver via typeof(RuntimeLog).Assembly.Location)
- `Dependencies/Foundation/ReflectionUtils.cs` (small helpers)
- `Dependencies/Foundation/VersionProbe.cs` (Bannerlord version detection)
- `Dependencies/Foundation/IncompatibleModDetector.cs` (crash-loop + modlist diff)
- `Dependencies/Foundation/PatchShield.cs` (Harmony Finalizer wrapper on EVERY patched method)
- `Dependencies/Foundation/SaveShield.cs` (Harmony Finalizer on 10 save/mission methods)
- `Dependencies/Foundation/FailureRecord.cs` (DTO for shielded exceptions)
- `Dependencies/Foundation/FailedModsCatalog.cs` (writes failed-mods-catalog.txt)
- `Dependencies/Foundation/SubModuleConstructionGuard.cs` (Harmony Finalizer on MBSubModuleBase ctor + Module.AddSubModule)
- `Dependencies/Foundation/CollectAssemblyTypesShim.cs` (Harmony Finalizer on Assembly.GetTypes + GetExportedTypes)

XML manifests (architectural context only):
- `Dependencies/_Module/SubModule.xml` (load-order pins)
- `Stubs/Bannerlord.Harmony/_Module/SubModule.xml` (alias stub example)

Tests:
- None. This infrastructure has no unit tests because every code path requires a live Harmony runtime + TaleWorlds assemblies + AppDomain. Verify via diag.log evidence + decompile cross-reference. Flag missing test coverage as INFO, not as a bug -- adding unit tests would require Bannerlord's full stack in the test fixture.

## REQUIRED SECTIONS

### VANILLA CODE (decompile and paste these to verify our reflection targets)

Run via `ilspycmd` on the installed v1.4.5 DLLs at `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`:

1. `TaleWorlds.Library.ApplicationVersion.FromParametersFile` body (does it handle null arg safely?)
2. `TaleWorlds.MountAndBlade.Module.AddSubModule` body (verify the Invoke call site for SubModuleConstructionGuard S5)
3. `TaleWorlds.MountAndBlade.MBSubModuleBase` ctor (is it explicit or compiler-default? S5 + S1 verification)
4. `HarmonyLib.Harmony.GetAllPatchedMethods` body (for S6 -- does it include self-patches?)
5. `TaleWorlds.SaveSystem.SaveManager.Load` overloads (verify SaveShield's target list is correct)
6. `TaleWorlds.MountAndBlade.MissionState.OnInitialize` + `MissionState.FinishMissionLoading` signatures
7. `TaleWorlds.MountAndBlade.Mission.Initialize` + `Mission.SetMissionMode` + `Mission.SpawnTroop` signatures

For each, paste a code block from the decompile output and explain whether our reflection target matches.

### Finalizer-signature deep analysis (Suspect S4)

This is the highest-risk area. Verify:

(a) Read Lib.Harmony 2.4.2 source/docs to confirm the legal Finalizer parameter shapes. Find any one of:
- The official Harmony wiki page on Finalizers
- Source: https://github.com/pardeike/Harmony/blob/master/Harmony/Documentation/articles/patching-finalizer.md
- Or decompile the Harmony.dll's PatchProcessor / Finalizer-call-site code

(b) Determine: is `ref Type[] __result` legal in a Finalizer?

(c) If illegal: what's the correct shape to mutate `__result` from a Finalizer? (May require switching CollectAssemblyTypesShim to use a Postfix that wraps in try/catch instead of a Finalizer.)

(d) If legal: confirm with a specific Harmony source-code reference.

### Reflection-target deep analysis (Suspects S3, S5)

For each reflective target:
- ApplicationVersion.FromParametersFile -- expected signature: `public static ApplicationVersion FromParametersFile(string customParameterFilePath = null)`. Confirm via decompile.
- Module.AddSubModule -- expected: `private AssemblyLoader.AssemblyLoadResult AddSubModule(SubModuleInfo subModuleInfo, Assembly subModuleAssembly)`. Confirm.
- MBSubModuleBase ctors -- expected: at least one parameterless ctor (explicit or compiler-default). Confirm via decompile.

### Exception-attribution analysis (Suspect S5)

Walk through a hypothetical scenario:
1. Third-party mod "FooMod" has a SubModule class that calls `MBObjectManager.GetObject<Hero>("nonexistent")` in its ctor body.
2. `MBObjectManager.GetObject<T>` throws NullReferenceException or similar.
3. The exception bubbles up: derived ctor body -> derived ctor frame -> base ctor frame -> ConstructorInfo.Invoke -> Module.AddSubModule -> our Finalizer.
4. Our finalizer unwraps TargetInvocationException then reads `ex.TargetSite`.

Question: what does `ex.TargetSite` evaluate to in this scenario? Is it `MBObjectManager.GetObject` (TaleWorlds) or `FooMod.SubModule.ctor` (third-party)?

Conclusion: is our current attribution correct, or should we walk the stack frames instead?

### Race-condition analysis

- `DiagLog.Write` acquires a private lock. Confirm no deadlock possibility with Harmony Finalizers (which run mid-exception under whatever lock the throwing code held). Specifically: if FileLogger or Mission code holds a lock and throws, and our Finalizer calls DiagLog.Write, do we risk lock-order inversion?
- `PatchShield._shielded` HashSet is locked. Both passes acquire the same lock. Confirm no re-entrancy if a Harmony patch on `_shielded.Add` somehow recurses (theoretically possible if someone Harmony-patches HashSet -- absurd but possible).

### CONFIG CROSS-REFERENCE

This work has no XML/JSON config files. The only config-like surfaces are:
- Stub SubModule.xml `<Version>` values (must match Lib.Harmony 2.4.2, Bannerlord.UIExtenderEx 2.13.1, Bannerlord.ButterLib 2.10.4, Bannerlord.MCM 5.11.4 -- the v99.0 strategy: 2.4.99.0 / 2.13.99.0 / 2.10.99.0 / 5.11.99.0)
- Opt-out flag file names (`patchshield-disabled.flag`, `saveshield-swallow-disabled.flag`) -- must match between PatchShield.IsDisabled and SaveShield.IsSwallowEnabled. Verify the literal strings agree.

### FINDINGS OR OBSERVATIONS

Per-suspect verdict:

| # | Suspect | Verdict (CONFIRMED / DISPUTED / NEEDS-MORE-EVIDENCE) | Severity | Recommendation |
|---|---------|-----|----------|---------------|
| S1 | PatchShield owner-filter "TAOM" prefix only | | | |
| S2 | SaveShield engine-prefix completeness vs TAOM_Online | | | |
| S3 | VersionProbe null-arg FromParametersFile safety | | | |
| S4 | CollectAssemblyTypesShim Finalizer `ref Type[] __result` legality | | | |
| S5 | SubModuleConstructionGuard culprit-attribution via TargetSite | | | |
| S6 | PatchShield double-install + self-patch-shielding | | | |
| S7 | IncompatibleModDetector comment-strip regex edge cases | | | |
| S8 | SaveShield per-session dedupe behavior | | | |

Plus any ADDITIONAL findings Codex spots that weren't in the Known Suspects list.

## QUALITY GATES

1. EVERY suspect verdict must include a code-block citation from either TAOM source (with file:line) or a Bannerlord v1.4.5 decompile (paste the relevant method body).
2. NEVER agree with a Known Suspect on plausibility alone -- prove with evidence.
3. If you flag a finding, propose a SPECIFIC code change with the modified code block, not just "should be fixed".
4. If suspects S4 (Finalizer signature) reveals a bug, treat it as CRITICAL -- it would silently break Assembly.GetTypes() across all mods.
5. NO speculation about "this might happen on Linux/Mac" -- TAOM ships Windows-only via Steam.
6. Reasoning level is xhigh; take the time to actually decompile and verify each reflection target.

## Prior review lessons

SUCCESSES: ilspycmd against installed DLLs caught wrong-namespace types (TaleWorlds.SaveSystem.LoadResult vs Load.LoadResult). Decompiling Module.AddSubModule showed the ctor.Invoke call site, motivating the second SubModuleConstructionGuard patch.

FAILURES: Earlier Codex passes claimed `TaleWorlds.ModuleManager.ApplicationVersionHelper.GameVersion()` existed (it doesn't in v1.4.5). Earlier passes also claimed Module.CurrentModule.Version existed (it doesn't). Verify EVERY reflection target by decompiling.

## Output to

`docs/reviews/codex-adversarial-dependencies-foundation-2026-05-27.md`

Format: top section is a summary table of all suspect verdicts + additional findings. Each finding gets its own section with severity, file:line, code citation, recommended fix as a code block.
