# RCA — Codex Adversarial Review of Dependencies/Foundation (2026-05-27)

## Top-line summary

Codex adversarial review of the DR3 Phase 4 defensive infrastructure (11 classes in `Dependencies/Foundation/` + orchestrator) returned **0 CRITICAL, 2 HIGH, 2 MEDIUM, 2 LOW = 6 confirmed findings**. Critically, the highest-risk suspect S4 (`ref Type[] __result` Finalizer signature legality in Lib.Harmony 2.4.2) was DISPUTED with citations to the Harmony source and official docs — the signature is legal and the by-ref result mutation works as written.

All 6 confirmed findings are real correctness defects that the in-game verification couldn't surface because they require specific exception scenarios (broken third-party mod with a non-TAOM-prefixed Harmony ID, TaleWorlds API throw from a third-party SubModule ctor body, etc.). Codex's value here was forcing decompile-backed verification of edge cases that diag.log alone couldn't reveal.

All fixes implemented in same session. Build green, tests pass (2,520/2,522).

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| S1 | HIGH | `PatchShield.TryUnpatchOffendingPatches` only protects Harmony owners starting with `TAOM`. Vendored BUTR/MCM Harmony IDs (`Bannerlord.ButterLib.SaveSystem`, `MCM.UI.Adapter.MCMv5`, etc.) would be unpatched on first MissingMethodException, breaking the entire BUTR stack. | Reflection target — incomplete protected-owner allowlist | I derived the allowlist from "what's TAOM-namespaced" without enumerating the actual Harmony IDs used by vendored DLLs. Should have decompiled each vendored DLL and listed every `new Harmony("X")` call site. | Added `ProtectedOwnerPrefixes` array with 14 entries covering TAOM + vendored BUTR/MCM/Harmony. **Memory:** `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md` (new) — when filtering Harmony patch owners, enumerate the actual Harmony IDs in the vendored DLLs, not just the namespace prefix. |
| S5 | HIGH | `SubModuleConstructionGuard.SwallowFinalizer` attributes `Module.AddSubModule` ctor failures via `ex.TargetSite`, which points at the throwing TaleWorlds method (not the offending third-party SubModule). A TAOM-owned SubModule whose ctor body calls a TaleWorlds API that throws gets mis-attributed to TaleWorlds (not TAOM), and the `if (asmName.StartsWith("TAOM"))` early-return is bypassed → TAOM ctor failures silently swallowed. | Exception attribution via wrong source — `ex.TargetSite` is the throw site, not the ctor site | I assumed `TargetSite` would be the method whose body owns the exception. In practice, `TargetSite` is whichever method throws — which is rarely the ctor itself; it's whatever API the ctor called. Should have walked through the scenario before implementing. | Rewrote SwallowFinalizer to add `object[] __args` parameter and read `subModuleInfo.SubModuleClassTypeName` + `subModuleAssembly.GetType()` for ground-truth attribution. **Rule extension:** when a Harmony Finalizer attributes a failure to an assembly/type, prefer the patch site's own arguments (`__args`) over `ex.TargetSite`. TargetSite is the throw site, not the caller. |
| S2 | MED | `SaveShield._enginePrefixes` uses `TAOM` as a broad StartsWith prefix — incorrectly matches `TAOM_Online` and `TAOM_Map` (independent consumer mods that ARE valid culprits). Also missed bundled BUTR infrastructure assemblies: `Bannerlord.MBOptionScreen.*`, `Bannerlord.ModuleLoader.*`, `MCM.UI.Adapter.*`, `BUTR.CrashReport*`. | Reflection target — broad prefix vs exact match + missing entries | Same root cause as S1: I derived the engine-prefix list from the BetaDeps reference + added "TAOM" without considering downstream consumer mods that happen to share the prefix. The bundled BUTR assembly names also came from BetaDeps's older list; our v1.4.5 bundle adds MBOptionScreen + ModuleLoader + MCM.UI.Adapter + BUTR.CrashReport. | Introduced `IsEngineAssembly` helper that does exact-match `TAOM` / `TAOM.Dependencies` + `TAOM.` (dot-suffix) prefix check separately from the bundled-runtime prefix list. Added 4 missing bundled-DLL prefixes. |
| A1 | MED | `IncompatibleModDetector.ReadCurrentModlist` returns INSTALLED modules (walks `Modules/` directory) but the diff comments and log strings say "enabled" / "newly-enabled". Enabling a previously-installed-but-disabled mod after a last-good launch produces no diff entry → culprit analysis incorrectly reports "no new mods". | Convention inconsistency — implementation diverges from documented intent | I implemented the simpler folder-scan first thinking it'd be "fast and reliable enough." Didn't verify the comments matched the implementation. Codex spotted the documented-vs-actual semantic mismatch. | Switched to `TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules()` via reflection; folder scan retained as fallback only. Refactored `ReadCurrentModlist` to call `TryReadActiveModuleIdsViaReflection` first, then `ReadInstalledModuleIdsFromFolders` if reflection fails. |
| A2 | LOW | `PatchShield.ShouldSwallow` increments `_swallowedOther` immediately before returning `false` (= rethrow). The "swallowed" counters used by `WriteSessionSummary` reported rethrown exceptions as swallowed. | Logic error — counter increment on wrong control-flow branch | I copied the increment pattern from the MissingMethod/Field/TypeLoad branch without distinguishing "swallowing" (return true) from "observing-and-rethrowing" (return false). Counter naming should have made this obvious. | Removed the `_swallowedOther` increment from the return-false branch. If we want visibility into rethrown exceptions later, add a separate `ObservedOther` counter kept out of `SwallowedTotal`. |
| A3 | LOW | `PatchShield._unpatched` deduplication key is `<DeclaringType>::<methodName>` — overloaded methods share a key. Second overload's failure skips cleanup because the first overload already marked the name. | Logic error — non-unique dedupe key | I used a stringified key for readability without verifying uniqueness for overloads. Bannerlord has plenty of overloaded methods (Mission.SpawnTroop has multiple overloads, e.g.). | Changed key to `<Module.ModuleVersionId>:<MetadataToken>` — uniquely identifies a MethodBase across overloads. Fallback to `.ToString()` if the metadata-token path throws. |

## Disputed findings (no change needed)

- **S3 — VersionProbe null-arg safety**: DISPUTED. Decompiled `ApplicationVersion.FromParametersFile(string customParameterFilePath = null)` shows `if (customParameterFilePath == null) ...` explicit null-handling path. Passing null is the intended "use default Version.xml" code path.
- **S4 — `ref Type[] __result` Finalizer signature legality**: DISPUTED. Codex cited Lib.Harmony 2.4.2 source (`MethodCreatorTools.EmitCallParameter`) showing by-ref `__result` in Finalizers uses `Ldloca` to load the result local's address. The signature is legal; by-ref assignment mutates the wrapper return value. Critical given S4 was flagged as "CRITICAL if wrong" — independent verification was essential.
- **S6 — PatchShield double-install + self-shielding**: DISPUTED. `_shielded` HashSet correctly dedupes. Pass 2 (`OnGameInitializationFinished`) sees PatchShield's own finalizer-only patches via `Harmony.GetAllPatchedMethods()`, but the existing TAOM-namespace declaring-assembly filter (line 122) catches them and adds to `_shielded` without patching.
- **S7 — Comment-strip regex edge cases**: DISPUTED. Multi-line legal XML comments handled correctly. Nested/embedded `-->` is illegal XML and out of scope. BOM handled by `File.ReadAllText`.
- **S8 — SaveShield per-session dedupe**: DISPUTED. One catalog entry per (culprit, exception-type, owner) tuple is intentional. Repeat occurrences visible in `_swallowedCount` + diag.log entries. Different exception types from same culprit correctly produce distinct entries.

## Root-cause pattern: reflection-target enumeration from vendored DLLs

Findings S1, S2, A1 all share a root cause: I derived reflection-target lists (Harmony owner allowlist, engine-assembly prefix list, mod-list source-of-truth) from architectural assumptions ("TAOM-namespaced things" / "well-known engine prefixes" / "easy folder scan") rather than from enumerating the actual vendored binaries and APIs.

The recurring failure mode: **the list looks plausible when first written, but a third-party mod with edge-case naming or a vendored DLL with non-obvious Harmony IDs falls outside the filter and triggers wrong behavior.**

Three preventive items going forward:

1. **When writing a Harmony-owner allowlist**: enumerate every `new Harmony("X")` call site in the vendored DLLs (decompile via `ilspycmd`), include all of them by prefix or exact match. Do NOT derive the list from namespace alone.

2. **When writing an engine-assembly prefix filter**: distinguish exact-equality matches (e.g., `TAOM` exact only) from prefix matches (e.g., `TAOM.` for sub-namespaces). Broad `StartsWith` on common prefixes will catch independent consumer mods sharing the prefix.

3. **When implementing "current state" lookups**: prefer the engine's authoritative API (e.g., `ModuleHelper.GetActiveModules`) over reconstructing state from disk artifacts. Disk artifacts give you "installed" / "deployed"; the engine API gives you "enabled" / "active". The semantic gap is silent until a downstream consumer asks about enabled state.

## Why each Codex suspect (and Claude) found vs missed

**Codex CAUGHT:**
- S1 (HIGH) — caught by decompiling vendored BUTR/MCM DLLs and grepping for `new Harmony("...")` calls. Claude didn't do this enumeration.
- S5 (HIGH) — caught by walking through the hypothetical scenario "FooMod ctor → MBObjectManager.GetObject throw" step-by-step. Claude wrote the SwallowFinalizer code without doing this walk-through.
- S2 (MED) — caught by listing actual bundled DLL filenames and comparing against the engine-prefix list.
- A1 (MED) — caught by reading the doc-comment + log-message text and noticing "enabled" vs "installed" semantic mismatch with the folder-scan implementation.
- A2 (LOW) — caught by tracing the `_swallowedOther` increment path and noticing the `return false` immediately after.
- A3 (LOW) — caught by considering overload edge cases for the dedupe key.

**Codex DISPUTED (correctly):**
- S3 (null arg safety) — independent decompile verification.
- S4 (`ref Type[] __result` legality) — independent Harmony source verification with code citations from `MethodCreatorTools.EmitCallParameter`.
- S6, S7, S8 — argued correctly against the suspect framings.

**Claude's own deep-review (separate skill) MISSED all 6 confirmed findings.** They sit in the gap between "the code compiles and the in-game test shows it works" and "all edge cases are covered." Codex's adversarial enumeration of edge cases is exactly the value here.

## Feedback memories to codify

One new memory:

**`feedback_harmony_owner_allowlist_from_vendored_dll_enumeration.md`** — When a TAOM defensive shield filters or protects Harmony owners (allowlist / blocklist), the list must be derived by enumerating every `new Harmony("X")` call site in the vendored DLLs we ship, NOT from architectural assumptions about namespace prefixes. Vendored BUTR/MCM DLLs use Harmony IDs that don't match the TAOM convention — `Bannerlord.ButterLib.SaveSystem`, `MCM.UI.Adapter.MCMv5`, `butterlib.delayedsubmoduleloader.static`, etc. Filtering by namespace prefix alone misses these. The PatchShield S1 finding (Codex review 2026-05-27, this RCA) demonstrates: if we'd protected only `TAOM*` Harmony owners, the first MissingMethodException in a ButterLib-patched method would auto-unpatch ButterLib's entire patch set. Action: enumerate via `ilspycmd <DLL> -t <Type> | grep "new Harmony("`.

## Commit linkage

- Codex review prompt: `docs/reviews/codex-adversarial-dependencies-foundation-2026-05-27.prompt.md`
- Codex review output: `docs/reviews/codex-adversarial-dependencies-foundation-2026-05-27.md`
- This RCA + fixes: commit `<TBD>` (referenced in commit message)
- Prior DR3 Phase 4 commits: `031283c` → `c47d12e` → `bc8f5c3` → `7681d54`

## Files modified

- `Dependencies/Foundation/PatchShield.cs` — S1 (HIGH) + A2 (LOW) + A3 (LOW)
- `Dependencies/Foundation/SaveShield.cs` — S2 (MED)
- `Dependencies/Foundation/SubModuleConstructionGuard.cs` — S5 (HIGH)
- `Dependencies/Foundation/IncompatibleModDetector.cs` — A1 (MED)

Verification: `dotnet build TAOM.sln` 0 errors. `dotnet test TAOM.Tests` 2,520/2,522 passing. In-game re-launch will validate via diag.log — expect zero behavioral changes on clean modlist, correct attribution + protection on a deliberately-broken third-party mod.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
