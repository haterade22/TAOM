# Codex Adversarial Review — EditorCacheRebuild MCM-trigger pivot + ICampaignSessionAdapter refactor

## Scope

Three commits (since a502ade) that together pivot the EditorCacheRebuild feature from editor-mode-only (Harmony patch on `NavigationCache<SettlementRecord>.GenerateCacheData`) to a singleplayer in-game MCM-driven trigger. Plus an ADR-007 refactor that extracts campaign-session access into a dedicated adapter, plus 11 new unit tests.

Commits in scope:
- `646484b` feat(EditorCacheRebuild): singleplayer MCM trigger + comprehensive logging
- `024e9e9` fix(EditorCacheRebuild): isolate Patch37 attach failure in singleplayer
- `6230c0c` refactor(EditorCacheRebuild): extract ICampaignSessionAdapter + add 11 unit tests

## Live in-game test result (already executed successfully)

The MCM trigger has been live-tested in singleplayer with TAOM's full 863-settlement map. Phase 1 ran in 1m 27s (371,953 pairs), checkpoint saved, resume after crash ran Phase 2 in 5m 37s (372 unique neighbor pairs), atomic write produced a 7.5MB cache file, round-trip verification deserialized cleanly (371,953 distance entries + 744 neighbor entries — vanilla stores neighbors in both directions for symmetric lookup, so 372 unique × 2 = 744 entries is correct). No crashes attributable to this feature.

Do NOT flag findings that were demonstrably correct in the live test. Focus on edge cases that did not exercise.

## TAOM ID CHEATSHEET (for context only — not directly used in this changeset)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs: gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar (and XSLT-passthrough: vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale)

## READ FIRST

- `CLAUDE.md` — TAOM project rules, especially "Critical Rules", "Adapter Pattern (ADR-007)", "Service-Locator anti-pattern", "Config Providers MUST Validate"
- `docs/features/editor-cache-rebuild.md` — feature doc (now describes both Path A and Path B)
- `CHANGELOG.md` — top 2026-05-12 entries describe both the original feature and this session's MCM pivot
- `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs` — primary code to review
- `Main/Adapters/ICampaignSessionAdapter.cs` + `CampaignSessionAdapter.cs` + `CampaignSnapshot.cs` — new adapter trio
- `Main/Features/TaomSettings.cs` (lines around `RebuildDistanceCacheAction`) — MCM button entry point
- `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs` + `Main/IoC.cs` (additions)
- `Main/SubModule.cs` (around `Patch37_EditorCacheRebuild` try/catch in PatchCategory call)
- `TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs` — new tests
- `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs` + `Phase2/ParallelPhase2Builder.cs` + `Validation/SmokeTestGate.cs` — logging enhancements

## Known Suspects (CONFIRM or DISPUTE each)

Walk these specific hypotheses. For each, decompile vanilla code, read TAOM source, decide CONFIRMED / DISPUTED / NEEDS_INVESTIGATION. Include line numbers when CONFIRMED.

1. **Race condition in `_runningFlag`**: `IsRunning` getter uses `Volatile.Read`, set/clear use `Interlocked.CompareExchange` and `Volatile.Write`. Is the memory ordering sufficient on .NET Framework 4.7.2 x64? Specifically: after `Volatile.Write(ref _runningFlag, 0)` in the finally block, can a thread observing `IsRunning == false` immediately afterward also see stale `_runningFlag` value via the Interlocked.CompareExchange path? Verify ECMA-335 memory model semantics.

2. **`SpawnBuild` overrideability vs Codex's preferred pattern**: The service is non-sealed with `internal virtual SpawnBuild`. Test subclasses override to no-op the `Task.Run`. Is this an acceptable test seam, or would Codex prefer a `Func<string, string>` constructor parameter / a `Task.Run` delegate injection? Argue from the trade-off, not just style.

3. **Path resolution under non-Windows paths**: `ResolveCacheOutputPath` does `Path.GetFullPath(Path.Combine(_pathService.ModuleRootPath, ".."))`. On non-Windows hosts (Bannerlord supports Linux/macOS via wine but the .NET 4.7.2 runtime is Windows-only) is `..` traversal correct? The mod runs on Windows so this is theoretical, but flag if the assumption is hardcoded.

4. **VerifyOutputRoundTrip when build was resumed**: When the build resumes from checkpoint, Phase1Result.PairsComputed is 0 (Phase 1 was skipped, state came from checkpoint deserialize). The verification then expects "~0 distance entries" but the deserialized file has 371,953. The current implementation has `expectedDistancePairs == 0 ? skip distance check`. Verify this is correct and doesn't open a hole where actual truncation in resume mode goes unnoticed.

5. **VerifyOutputRoundTrip neighbor count assumption**: Comment says vanilla stores neighbors in BOTH directions (`_fortificationNeighbors[s1] += s2; _fortificationNeighbors[s2] += s1`). Verify by decompiling `NavigationCache<T>.AddNeighbor` in v1.3.15 (use `ilspycmd` on the installed DLL, NOT the v1.4 decompiled folder).

6. **`Patch37_EditorCacheRebuild` try/catch in SubModule.cs**: We wrap `_harmony.PatchCategory("Patch37_EditorCacheRebuild")` in try/catch. The catch swallows ALL exceptions and logs a warning. Could legitimate, unexpected exceptions (e.g., a partial patch that DID attach but failed mid-process, leaving a dangling Harmony hook) be silently swallowed? Should the catch be type-restricted to `HarmonyException` or `ArgumentException`?

7. **MCM lambda's static-ness**: `RebuildDistanceCacheAction` is `static () =>` and resolves `IoC.Resolve<IRuntimeCacheRebuildService>()`. Three concerns:
   (a) If `IoC.Configure()` hasn't been called yet (race during SubModule load), does the resolve throw a typed exception that the try/catch surfaces correctly?
   (b) Is the action property's setter `public` because MCMv5 needs it? Could MCMv5's settings persistence ever attempt to assign a deserialized lambda that doesn't have the IoC.Resolve body? Read MCMv5's JSON settings format to confirm `Action`-typed properties are NOT persisted.
   (c) The MCM hint text says "10-30 minutes" but our live run was 1.5 + 5.5 = 7 minutes wall time. Could a slower machine genuinely take 30 min, or is the upper bound misleading?

8. **Compatibility with the existing editor-mode Path B**: If a user somehow has both `SettlementPositionScript` triggered (editor button) AND the MCM button clicked (singleplayer) — could both code paths execute concurrently and race on the on-disk cache file? Practically MCM is inaccessible in editor mode, but verify the design isolation.

9. **`CampaignSessionAdapter.GetSnapshot` swallows exceptions**: The `catch { }` at end of `GetSnapshot` returns a partial snapshot rather than propagating. Is this the right call when, e.g., `Campaign.Current.UniqueGameId` is a sentinel "oldSave" string indicating save version mismatch? Should the snapshot expose a Boolean `IsFullyPopulated` field?

10. **Test subclass coverage gaps**: `RuntimeCacheRebuildServiceTests` covers 11 scenarios. Audit for missing high-priority cases:
    - What about the path `Trigger → SpawnBuild fires → exception during RunBuild → finally block clears _runningFlag`? The test subclass intercepts SpawnBuild so this path is never exercised. Should there be a separate test that drives RunBuild directly with a mocked adapter that throws mid-build?
    - The Verify round-trip logic isn't unit-tested (live test only).
    - `WriteOutputAtomically`'s rename sequence (final → .prev → .tmp → final) isn't unit-tested.

## REQUIRED SECTIONS

### VANILLA CODE (decompile required)

For each, paste the actual v1.3.15 source from `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll"`:

1. `TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache<T>.AddNeighbor` — confirm symmetric insertion
2. `TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache<T>.Deserialize` — confirm behavior on truncated input
3. `TaleWorlds.CampaignSystem.Map.DistanceCache.SandBoxNavigationCache` constructor — confirm Campaign.Current.Models dereferences

### FILE LISTS

**New services + adapters (highest scrutiny):**
- `Main/Adapters/ICampaignSessionAdapter.cs`
- `Main/Adapters/CampaignSessionAdapter.cs`
- `Main/Adapters/CampaignSnapshot.cs`
- `Main/Features/EditorCacheRebuild/IRuntimeCacheRebuildService.cs`
- `Main/Features/EditorCacheRebuild/RuntimeCacheRebuildService.cs`

**Modified:**
- `Main/Features/TaomSettings.cs` (MCM button addition)
- `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs` (new registration)
- `Main/Features/EditorCacheRebuild/Hooks/Patch37_CacheBuildOverride.cs` (FindSettlementRecordType fallback + logging)
- `Main/Features/EditorCacheRebuild/Phase1/ParallelPhase1Builder.cs` (logging only)
- `Main/Features/EditorCacheRebuild/Phase2/ParallelPhase2Builder.cs` (logging + tracked counter)
- `Main/Features/EditorCacheRebuild/Validation/SmokeTestGate.cs` (logging)
- `Main/IoC.cs` (new registration)
- `Main/SubModule.cs` (Patch37 try/catch)
- `Main/_Module/SubModule.xml` (editor mode tags — actually unnecessary now since editor path is abandoned, but kept as legacy fallback)

**Tests:**
- `TAOM.Tests/Features/EditorCacheRebuild/RuntimeCacheRebuildServiceTests.cs` (11 new tests)

### FEATURE-SPECIFIC DEEP ANALYSIS

Walk these concrete scenarios. For each, evaluate whether the implementation behaves correctly:

**S1.** User clicks MCM button twice in rapid succession (double-click). Both events fire on the UI thread. First Trigger() returns true and spawns Task.Run. Second Trigger() runs synchronously on the UI thread; it sees `_runningFlag == 1` and returns false. The first build proceeds. Question: are there any race scenarios where both could pass the Interlocked check?

**S2.** Game crashes mid-Phase-2 (after checkpoint was saved). Next session: user loads same save (CRCs match), clicks MCM button. Service detects checkpoint, deserializes Phase 1 state, skips Phase 1, runs Phase 2 fresh. Question: does the .prev backup file exist correctly? When Phase 2 completes, atomic write executes — what happens to the .prev file from the previous successful build?

**S3.** User starts MCM rebuild, game freezes (not crashes) for 10 minutes due to disk I/O. User force-kills via Task Manager. Next session: .tmp file exists from interrupted serialization. Service detects stale .tmp on next rebuild, deletes it, proceeds. Question: is the .tmp detection reliable across NTFS/OneDrive/network-mounted drives where File.Delete might fail or take seconds?

**S4.** Multi-language: user runs game in non-English locale. `Settlement.IsTown`, `IsCastle`, `IsVillage` are locale-independent properties. `Campaign.Current.UniqueGameId` is a string. Are any of our log lines locale-dependent in a way that breaks parsing or testing?

**S5.** A user has the existing `settlements_distance_cache_Default.bin` from a vanilla rebuild (or a previous TAOM mod version). They click Rebuild. Vanilla deserialization at startup loaded the existing cache, but our rebuild creates a FRESH `SandBoxNavigationCache` — does it inherit any state from the runtime instance, or is it independent? If independent, the rebuild output won't match the runtime's loaded state. Is that the intended behavior? (Answer: yes, intended — rebuild is a complete replacement.)

### CONFIG CROSS-REFERENCE

Verify `Main/_Module/ModuleData/configs/cache_rebuild_config.json` fields are still consumed correctly. The MCM trigger path uses the same `ICacheRebuildConfigProvider` as the editor path, so all 17 fields should still apply. Confirm no field was orphaned by the pivot.

### FINDINGS OR OBSERVATIONS

Output format:
```
P1 — Title
File: <relative path>:<line>
Evidence: <quote vanilla source if relevant; quote TAOM source>
Impact: <what breaks for the user>
Fix: <minimal correct change>
```

Use P1 for showstopper bugs (crash, data corruption, security), P2 for correctness issues with workarounds, P3 for code-quality / nit issues.

If you find no findings in a category, write "P1: none / P2: none / P3: none" explicitly.

## QUALITY GATES

- Decompile ALL vanilla targets via ilspycmd before claiming findings about them. Use the INSTALLED v1.3.15 DLL, NOT the v1.4 decompiled folder.
- Confirm each Known Suspect with a verdict and supporting evidence.
- For false positives that look plausible but don't apply, write a one-line "Disputed: ..." with the citation that refutes the concern.
- Match the structured FINDINGS OR OBSERVATIONS format above.

## Prior review lessons

SUCCESSES:
- Config ID cross-references caught rohan/dol_guldur mismatches in prior reviews
- Vanilla decompilation has caught Add-only dict semantics (review #38 P1) and Campaign-coupled struct properties (review #38 P2)
- Lifecycle tracing has caught stale state across save/load (multiple reviews)

FAILURES TO AVOID:
- Codex previously assumed `empire=Rohan` — IT IS DUNLAND. (See cheatsheet above.)
- Codex previously flagged vanilla-matching code as bugs (e.g., claimed our SortedPathKey swap was wrong when it matched vanilla `NavigationCacheElement<T>.Sort`).
- Codex previously skipped hard analytical sections.
- Codex previously claimed signatures from v1.4 decompiled folder were authoritative — they're NOT for v1.3.15.

## Output destination

Write the review to `docs/reviews/codex-adversarial-runtimecacherebuild-2026-05-12-review.md` using the FINDINGS OR OBSERVATIONS format.
