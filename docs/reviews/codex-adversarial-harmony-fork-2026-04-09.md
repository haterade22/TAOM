# Codex Adversarial Review: Harmony Fork

**Date:** 2026-04-09
**Feature:** Fork Harmony 2.4.2 into TAOM.Dependencies
**Reviewer:** Codex + Claude independent verification

## Known Suspects

| # | Suspect | Verdict | Evidence |
|---|---------|---------|----------|
| 1 | UnpatchAll guard param name mismatch | DISPUTED (SAFE) | `Harmony.UnpatchAll(string harmonyID = null)` at line 217 matches guard exactly |
| 2 | Duplicate Harmony assembly comparison logic | DISPUTED (SAFE) | Logic correctly detects separate `0Harmony` assemblies; TAOM ships as `TAOM.Dependencies.dll` |
| 3 | TaleWorlds.CampaignSystem.dll exclusion breaks code | DISPUTED (SAFE) | No CampaignSystem types used in Dependencies/ or UIExtenderEx fork |
| 4 | Harmony.Extensions type conflicts | DISPUTED (SAFE) | Source-included package resolves against forked HarmonyLib types at compile time |
| 5 | Assembly load order circular race | DISPUTED (SAFE) | Static ctor loads GauntletUI assembly; Harmony types are in same assembly, no re-entry |
| 6 | FileLog usage when path uninitialized | DISPUTED (SAFE) | `FileLog.Log()` checks `if (LogPath == null) { return; }` — graceful no-op |

## New Findings

None. The implementation is clean infrastructure code with minimal TAOM-authored changes.

## Build Config Cross-Reference

- `Lib.Harmony` NuGet removed from both Dependencies and Main csproj -- CORRECT
- `IsExternalInit` and `Nullable` NuGets removed from Dependencies (Harmony source provides them) -- CORRECT
- `Harmony.Extensions` NuGet retained in both projects (UIExtenderEx needs AccessTools2) -- CORRECT
- `BUTR.Harmony.Analyzer` retained (Roslyn analyzer, no runtime impact) -- CORRECT
- `TaleWorlds.CampaignSystem.dll` excluded from Dependencies references -- CORRECT (Helpers namespace conflict)
- `LangVersion=preview` for vendored code -- ACCEPTABLE (needed for C# 12+ features in decompiled source)

## Verdict

**PASS -- No bugs found.** All 6 suspects independently verified as safe by both Codex and Claude.
