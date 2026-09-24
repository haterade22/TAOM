# Cold review: plan 012 (loading-window trace per frame)

Reviewed file: `plans/012-loading-window-trace-per-frame.md`, against `.claude/skills/improve/references/plan-template.md` ("Quality bar"). Code compared at `b2e387db` with `git show`; engine source read from the `taom-src` cache; the measured logs re-counted.

**Verdict:** a weak executor could run this plan. I found nothing blocking. The excerpts match the code at `b2e387db`. The TDD order, the single-owner handling and the drift-check paths are all correct. The non-blocking items below would make it harder to misread or to stall on.

## What I verified (evidence read this turn)

- `LoadingWindow_Transitions_Patch.cs` at `b2e387db` is 36 lines and matches the plan's excerpt byte for byte. The second `<para>` is at lines 16-19 and the Disable class at 29-36, as Step 4 says.
- `MapLoadTracer.cs:27-31`, `:42-55` (Trace) and the `TraceWithCallers` body match, including the two-frame skip.
- `FileLogger.cs:85-95`, `Drain` `:99-136`, `Flush` `:124` and header `:8-11` match.
- `SubModule.cs` at `b2e387db`: the category loop is at 403-410, and `_harmony.PatchCategory(traceCategory)` is at line 410. The whole block runs with no MCM or debug gate.
- Engine `TaleWorlds.Engine.LoadingWindow` (v1.5.3 cache): `DisableGlobalLoadingWindow` at 31-44, the unconditional `IsLoadingWindowActive = false;` at 41 (inside `if (LoadingWindowManager != null)`), and the empty static ctor at 9-11. All match.
- `GauntletPartyScreen.OnFrameTick` at 113-119 calls `LoadingWindow.DisableGlobalLoadingWindow()` unconditionally, as the plan says.
- `TownGoldFlowTagPatches.cs:34-38` matches the `__state` precedent, and `HarmonyFieldInjectionNamingTests.cs:37` lists `__state`.
- `MapLoadDiagnosticsBehaviorTests.cs` matches the quoted whole file.
- At `b2e387db`, `TAOM.Tests` contains no `Parallelize` and no `MapLoadTracer`, and no test touches `TaleWorlds.Engine.LoadingWindow`.
- `docs/features/map-load-diagnostics.md:43-49` and `docs/reference/harmony-patch-registry.md:1024` match, and the phrase Step 5(b) replaces occurs once.
- The drift check (`b2e387db..bannerlord-1.5.x`, tip `4b5662b2`) prints nothing today. Its paths cover every Scope path.
- `SubModule.xml:6` is `v2.0.30`. The three example subjects are 64, 71 and 67 characters.
- Log evidence re-counted: the 35-minute log is 83,803,347 bytes and 265,061 lines, with 262,763 lowered, 8 raised and 7 raised-then-lowered. The 3-hour log is 1,158,497,918 bytes.
- `lint_docs.py` with no flags exits 0 (0 dead links). `validate_moduledata.py` in the main checkout gives 0 errors today.
- The plan contains no em or en dash characters. The only matches are the escape text `\u2013`/`\u2014` in the line 700 command. No secret values.
- The four baseline test names exist, and both Warg tests carry `[Ignore]`.

## Blocking

None.

## Non-blocking

1. **Line 53 and the text the plan ships (lines 608, 689-690): "about 360 a second" does not describe the 35-minute log.** 262,763 lines over about 2,100 s is about 125 per second. The per-second median is 115 and the maximum is 361. The 3-hour log averages about 379 per second. The rate follows the frame rate. As written, the doc paragraph puts "about 360 lines a second" next to "84 MB in a 35-minute session", which contradicts itself. Suggested wording: "one per rendered frame, up to about 360 a second".
2. **Line 689: "Before that guard (shipped in v2.0.29 and v2.0.30)" reads as though the guard shipped in those versions.** The unguarded trace is what shipped: it was added in `502f7cde`, and tags v2.0.29 and v2.0.30 contain it. Suggested wording: "In v2.0.29 and v2.0.30, before this guard, ...".
3. **Line 425-426, Step 0.4: the Windows path pasted unquoted fails in Git Bash.** `taom-src` prints `C:\Users\mikew\.taom-src\v1.5.3\TaleWorlds.Engine.LoadingWindow.cs`. If that path is pasted unquoted into `sed -n 31,44p <that path>`, bash strips the backslashes. Give the literal command: `sed -n 31,44p "$(pwsh tools/taom-src.ps1 path TaleWorlds.Engine.LoadingWindow 2>/dev/null | tail -1)"`.
4. **Line 433 (Step 0 Verify) and line 702 (Step 5 Verify) end in a judgment, not a command result.** "The excerpt and the engine body match" and "reports no dead link in either file" both need the executor to judge. Replace them with checks such as `grep -c "IsLoadingWindowActive = false;"` on the engine file (expect 1), and `python tools/lint_docs.py | grep -c "map-load-diagnostics.md\|harmony-patch-registry.md"` (expect 0), or grep the `Dead links: **0**` line.
5. **Lines 746-748, Done criteria: no command produces the per-class counts.** The criterion wants "the trx or console output shows `LoadingWindowTraceGateTests` 4 passed and `LoadingWindowDisablePatchTests` 4 passed". The default `dotnet test` console output prints only totals and failures. Say to run the two filtered commands (expect `Passed: 4`, `Failed: 0`), or add `--logger "trx"` and give the grep.
6. **Line 357 and Step 0.5: no timeout guidance for the full suite.** The suite has about 10,239 tests and needs a cold build and restore in a new worktree. The Bash tool defaults to a 120 s timeout. Tell the executor to pass `timeout: 600000` or run it in the background. (UNVERIFIED: I did not time the suite.)
7. **STOP conditions (lines 763-782) do not cover three environment failures a weak executor can hit:**
   - (a) `git worktree add` fails because `E:/repos/wt-012-loading-window-trace` or branch `plan-012-loading-window-trace` already exists, for example on a re-run.
   - (b) An Edit, Write or `cd` into W is denied by permissions. W is outside the project directory and is not in `.claude/settings.local.json` `additionalDirectories`. (UNVERIFIED: this depends on the orchestrator's permission mode.)
   - (c) A PreToolUse commit hook denies a commit. Those hooks run `git diff --cached` in `CLAUDE_PROJECT_DIR` (the main checkout), not in W, so another session's staging there can deny W's commits. The main index is empty today.
   
   Add "STOP and report; never fall back to editing or committing under `E:\repos\TAOM`" for each of the three.
8. **Line 761: the Data criterion has no Step 0 baseline.** The plan changes no data, but the validator reads the live Armory, which line 343 says another session is editing. An ERROR there would fail Done with no rule for what to do. Either record a Step 0 baseline, as for tests, or drop the criterion.
9. **Lines 404-409: the commit body has no wrap check.** "Body wrapped at 72" relies on the executor's own care, while every other git rule has a command. Consider a one-liner that checks the body's maximum line length.
10. **Lines 501, 658, 704: the plan never says /deep-review happens later.** It commits C# on the branch, which is correct for an executor that cannot invoke skills. It should say in one line that the orchestrator runs `/deep-review` on the branch before merge (CLAUDE.md gate). Lines 795-797 only imply it.
11. **Lines 790-794: the predicted caller-chain frame is a guess stated as expected behaviour.** The note predicts `?.TaleWorlds.Engine.LoadingWindow.DisableGlobalLoadingWindow_PatchN`. Mark it as a guess, so the in-game checker does not treat a different first frame as a failure.
12. **Line 286: the ADR-008 summary slightly misstates the ADR.** The plan says "pure decision code needs 100% coverage". ADR-008 says services must be 100% unit testable without game initialization. This does not affect execution.

## Excerpt mismatches (exact)

1. Line 174 labels the SubModule excerpt `Main/SubModule.cs:394-415`. At `b2e387db`, `var mapLoadLogger = IoC.Resolve<IModLogger>();` is line 393 (not 394), and the last excerpted line, `try { _harmony.PatchCategory(traceCategory); }`, is line 410. The content matches; the range is off by one at the start and overstates the end.
2. Line 120 labels the second MapLoadTracer excerpt `:57-82`. The excerpt starts at line 62 (`public static void TraceWithCallers`) and leaves out the doc comment on lines 57-61. The content matches.
3. `Main/IoC.cs` is not excerpted, and the plan needs no IoC change, which is correct because the gate is static.

## Checklist (item 4)

| Check | Result |
|---|---|
| TDD order | Pass: Step 1 RED, Step 2 GREEN, Step 3 RED, Step 4 GREEN, each with a named compile error. |
| Issue-first | Pass: the Status line (line 44) says the orchestrator creates the issue before the change lands. |
| Binding ADRs | Pass: ADR-002, 007, 008, 003/004/005 and `csharp-architecture.md` are named with summaries (lines 281-293). |
| Single-owner files | Pass: "recommend, don't edit", with the reason and the fallback `<Compile>` line (lines 377-382). |
| STOP conditions specific | Pass: they target the real risks (engine body drift, `__state` shape, the test-host type load, the precondition flag). Gaps are in item 7 above. |
| Done criteria machine-checkable | Mostly: see items 5 and 8. |
| Planned-at SHA vs drift-check vs Scope | Pass: `b2e387db` throughout, and the drift-check paths cover every Scope path. |
| Non-deploying commands | Pass: build and both test commands carry `-p:DisableModuleCopy=true -p:ModuleId=`, and `./build.ps1` is forbidden. |
| Dashes and secrets | Pass. |
