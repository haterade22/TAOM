# Adversarial review: TAOM memory diagnostics (crash-bundle verdict + [MemStation] anchors)

You are reviewing an uncommitted changeset in the TAOM Bannerlord mod. Be adversarial. Your job is to find defects that a 9-agent internal review already missed, not to confirm that the work is good.

## What the changeset does

Two related instruments, both aimed at one problem: TAOM commits ~15.7 GB where vanilla commits ~6 GB, and issue #385 killed a tester's 16 GB machine at 20.3 GB total commit, diagnosable only by hand-parsing a 1.3 GB minidump.

1. The crash bundle gains a System Memory section and a one-line memory verdict in the report header and the ZIP manifest. Before this it carried WorkingSet64 and PrivateMemorySize64 and no commit, headroom, or physical-memory figures at all -- i.e. none of the numbers #385 was actually diagnosed by.
2. New `[MemStation]` log lines, one per screen open and close, so a session log can say WHICH screen commit growth happened on. The periodic `[MemSample]` line (30s, pre-existing) can only say that growth happened.

## READ FIRST

- docs/features/battle-load-diagnostics.md -- the owning feature doc, including the new [MemStation] section
- docs/features/crash-report.md -- the crash bundle feature doc, including the new memory verdict section
- docs/reviews/rca-memory-diagnostics-2026-09-01.md -- the 19 findings the internal review already fixed. Do NOT re-report these. Finding something they fixed BADLY is valuable; repeating them is not.
- docs/reviews/rca-memsample-telemetry-2026-08-05.md -- the predecessor RCA. Its HIGH #1 was a C#/Python threshold mirror that diverged because C# floored integer division and Python cross-multiplied exactly.

## TAOM ID CHEATSHEET -- NOT APPLICABLE HERE

This changeset touches no culture, kingdom, troop, item or settlement IDs, and no ModuleData XML. Do not hunt for ID mismatches; there are none to find. Spend that effort on the sections below instead. (Recorded so you do not treat the omission as an oversight.)

## Files to review

C# production:
- Main/Features/CrashReport/Domain/SystemMemorySnapshot.cs (new)
- Main/Features/CrashReport/Rendering/MemoryPressureVerdict.cs (new)
- Main/Features/BattleLoadDiagnostics/MemoryStationSampler.cs (new)
- Main/Features/CrashReport/Domain/ExceptionContext.cs (one field added)
- Main/Features/CrashReport/Collectors/ProcessEnvironmentCollector.cs (CollectSystemMemory added)
- Main/Features/CrashReport/CrashReportService.cs (one Safe() call, one ctor arg)
- Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs (header line + section)
- Main/Features/CrashReport/Rendering/CrashBundleWriter.cs (manifest line, BuildManifest made internal)
- Main/Features/BattleLoadDiagnostics/MemoryPressureSampler.cs (FormatSampleTokens extracted)
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs (one registration)
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs + IBattleLoadDiagnosticsSettingsProvider.cs (hint text only)
- Main/SubModule.cs -- review ONLY the MemoryStationSampler.Start() block near line 519

Context, unchanged but load-bearing:
- Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs (the P/Invoke reader both instruments use)
- Main/Core/Logging/FileLogger.cs (INFO flushes synchronously and survives a hard crash; DEBUG is async and does not)

Tests:
- TAOM.Tests/Features/CrashReport/MemoryPressureVerdictTests.cs
- TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs
- TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs
- TAOM.Tests/Features/BattleLoadDiagnostics/MemoryStationSamplerTests.cs
- TAOM.Tests/Features/BattleLoadDiagnostics/ScreenManagerEventBindingTests.cs

Tooling:
- tools/triage_battle_load.py (the Python mirror: [MemStation] parsing, classify_stations, report section)
- tools/tests/test_triage_battle_load.py
- tools/Invoke-CommitMatrix.ps1 (new capture harness for a one-shot 3-hour in-game session)

IGNORE ENTIRELY: anything under Main/Features/AiPartySize/, docs/reference/armory-catalogue/, tools/generate_armory_catalogue.py, and the two RCA files dated 2026-09-01 that are not the memory one. A concurrent session owns those and they are unrelated.

## ENGINE BINDINGS -- verify these against the INSTALLED game, not the decompile dump

There are NO Harmony patches and NO GameModel overrides in this changeset. The engine surface is two public static events, which is the thing most likely to be wrong:

    TaleWorlds.ScreenSystem.ScreenManager.OnPushScreen  (delegate OnPushScreenEvent(ScreenBase))
    TaleWorlds.ScreenSystem.ScreenManager.OnPopScreen   (delegate OnPopScreenEvent(ScreenBase))

Installed DLLs are authoritative. Use:
    pwsh tools/taom-src.ps1 path TaleWorlds.ScreenSystem.ScreenManager
The dump at E:\Decompiled_Bannerlord\ may lag the installed 1.4.8 build.

Answer concretely:
1. Enumerate every method that raises each event. Is any raise inside a try/catch? What happens to the caller if a subscriber throws?
2. Can either event fire off the main thread? MemoryStationSampler mutates `_emitted`, `_capReported`, `_started` with no lock and no Interlocked. The code documents this as caller discipline rather than enforcement, citing a non-blocking `Debug.FailedAssert` that is absent from ReplaceTopScreen and SetAndActivateRootScreen. Confirm or refute that reasoning.
3. Is `Dispose()` on a DryIoc singleton actually reached in this process, and does it matter if it is not?

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each, with evidence

S1. ENCYCLOPEDIA GAP MITIGATION. The internal review found that the encyclopedia is a `MapEncyclopediaView` overlay on `MapScreen`, not a `ScreenBase`, so no station line is ever emitted for it -- and encyclopedia browsing is the leading suspect for the growth this instrument exists to find. The changeset documents the gap rather than closing it. QUESTION: is there a clean, low-risk seam that WOULD close it (MapEncyclopediaView.IsEncyclopediaOpen has a protected setter on a SandBox base whose behaviour lives in a GauntletUI subclass)? Name the exact type and member if so. Also judge whether documenting-not-fixing is defensible given the feature's stated purpose.

S2. SESSION RESET SEMANTICS. `MemoryStationSampler.Start()` now calls `ResetSessionBudget()` BEFORE the `_started` early-return, so the emit budget and the cap-warning latch clear on every return to the main menu. QUESTION: is that the right boundary? Consider a player who returns to the menu and resumes the same campaign -- the budget resets mid-investigation and the log gains no marker saying so. Is a reset without a log line a silent discontinuity in the very artefact being analysed? Should the reset itself emit a line?

S3. WITHHOLDING DATA ON A GARBAGE READ. `MemoryPressureVerdict.HasUsableCommit` now suppresses the commit and headroom clauses when `SysCommitUsedMb < 0`. QUESTION: for a CRASH report, is suppression the right call, or does it hide the single most diagnostic fact (that the reader returned nonsense) from the person triaging? Argue both ways and recommend. Note the raw `sysCommitUsedMB=` token is still printed in the detail section but not in the header.

S4. PERCENTOF BOUNDS. `PercentOf` returns null when `part > long.MaxValue / 100` or the quotient exceeds int.MaxValue. QUESTION: can either bound be hit by a LEGITIMATE reading in MB units, which would silently hide a real percentage? Compute the actual thresholds in MB and say whether any real machine can reach them.

S5. NESTED-VISIT DETECTION COMPLEXITY. In `classify_stations`, each `exit` scans `events[opened_idx+1:idx]` for a foreign `enter`. QUESTION: what is the worst-case complexity given the C# side caps emission at 2000 lines, and can an adversarial (or merely unusual) screen sequence make this quadratic enough to matter for an offline tool? Also: is the nesting flag attributed to the correct screen? It is deliberately placed on the OUTER screen, on the reasoning that the outer delta re-includes the inner screen's growth.

S6. CROSS-LANGUAGE CONTRACT INTEGRITY. The C# `FormatStation` and the Python `_MEMSTATION_RE` are a pinned contract, and `FormatSampleTokens` was extracted so `[MemSample]` and `[MemStation]` share a token tail. QUESTION: prove or disprove that the extraction changed no byte of the existing `[MemSample]` output. Then check whether any output of `SanitizeScreenName` can break `screen='([^']*)'` or the surrounding tokens -- including the `<unknown>` sentinel, which bypasses the character allowlist entirely.

## REQUIRED SECTIONS in your output

1. ENGINE BINDINGS -- your answers to the three questions above, with the decompiled source pasted as code blocks.
2. KNOWN SUSPECTS -- S1 through S6, each CONFIRMED / DISPUTED / PARTIAL with evidence.
3. FAILURE-PATH ANALYSIS -- this code runs while the process is already crashing, and inside an engine multicast delegate every other mod subscribes to. Trace what happens if: the memory reader fails; the logger throws; the renderer throws mid-report; an OutOfMemoryException lands inside SanitizeScreenName. State which of these loses the whole crash report versus one section.
4. NEVER-FABRICATE-A-ZERO AUDIT -- TAOM has an explicit rule that an unmeasured value is omitted, never rendered as 0, because a fabricated zero in a user-uploaded report cannot be told from a real reading. Find any remaining path in the changed code where a 0 (or an empty string, or a default) reaches a human and is indistinguishable from a real measurement. The internal review found two such paths (a null snapshot and an arithmetic overflow); find a third or state that none remains.
5. PYTHON AND POWERSHELL CORRECTNESS -- these are not afterthoughts. `Invoke-CommitMatrix.ps1` will be run during a ONE-SHOT 3-hour in-game capture session that cannot easily be repeated, so a silent data-integrity bug there is expensive. Check the commit-vs-address-space semantics of `Win32_OperatingSystem.TotalVirtualMemorySize`/`FreeVirtualMemory`, CSV schema stability across the measured and UNMEASURED row shapes, and whether any failure mode is silent.
6. FINDINGS OR OBSERVATIONS -- everything else, severity-rated.

## QUALITY GATES

- Every finding must cite file and line and quote the code.
- Do not report anything already listed as fixed in docs/reviews/rca-memory-diagnostics-2026-09-01.md unless the fix itself is wrong -- in which case say so explicitly and explain why.
- If you cannot verify something, say UNVERIFIED. Do not guess.
- Distinguish a real defect from a stylistic preference. TAOM will implement what you confirm, so a false positive costs real work.
- State explicitly if you think the changeset is sound in a given area. A clean verdict backed by evidence is a useful result.

## LESSONS FROM PRIOR REVIEWS

SUCCESSES: cross-referencing config IDs caught real mismatches; decompiling vanilla caught missing gates; lifecycle tracing caught stale caches; on the immediately preceding review you correctly caught a C#/Python threshold mirror that diverged only in the integer-floor band.

FAILURES: you have previously assumed empire=Rohan (it is Dunland); flagged vanilla-matching code as a bug; and skipped the hardest section of a prompt. Do not skip section 3 or 4 above -- they are the ones most likely to contain a real defect.
